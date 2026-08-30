using System.Data;
using Microsoft.Data.SqlClient;

namespace BCETL.Data;

/// <summary>
/// Purpose:
/// Executes [bc].[dbo].[ValidateSalesHeaders] and counts every validation
/// exception returned across all SQL result sets.
///
/// The validation stored procedure returns one result set for each
/// validation check. A result set containing only column headers represents
/// a successful check. Each data row represents one validation exception.
///
/// This repository reads every result set by using NextResultAsync so that
/// exceptions returned by later validation checks are not overlooked.
///
/// Safety:
/// - Validation is read-only.
/// - No SalesHeaders records are inserted, updated, or deleted.
/// - No SQL transaction is required because the procedure does not modify data.
///
/// Business Purpose:
/// Provides the data-access layer required for BCETL operators to validate
/// retained SalesHeaders data without opening SQL Server Management Studio.
///
/// UTC remains the system-of-record timezone for recorded execution times.
/// PT conversion belongs in the operator-facing validation service.
///
/// Future Enhancement Backlog:
/// - Persist validation execution history.
/// - Return selected exception details for operator review.
/// - Add validation duration and performance metrics.
/// </summary>
public sealed class SalesHeaderValidationRepository
{
    private readonly string _connectionString;

    public SalesHeaderValidationRepository(string connectionString) =>
        _connectionString = connectionString;

    public async Task<SalesHeaderValidationResult> ValidateAsync(
        CancellationToken cancellationToken)
    {
        DateTime startedUtc = DateTime.UtcNow;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            "[bc].[dbo].[ValidateSalesHeaders]",
            connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 600
        };

        var exceptionsByIssue =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        int resultSetsChecked = 0;
        int totalExceptions = 0;

        await using SqlDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);

        do
        {
            resultSetsChecked++;

            int validationIssueOrdinal = GetValidationIssueOrdinal(reader);

            while (await reader.ReadAsync(cancellationToken))
            {
                totalExceptions++;

                string validationIssue =
                    validationIssueOrdinal >= 0 &&
                    !reader.IsDBNull(validationIssueOrdinal)
                        ? reader.GetString(validationIssueOrdinal)
                        : $"Unnamed validation result set {resultSetsChecked}";

                if (exceptionsByIssue.TryGetValue(
                    validationIssue,
                    out int currentCount))
                {
                    exceptionsByIssue[validationIssue] = currentCount + 1;
                }
                else
                {
                    exceptionsByIssue[validationIssue] = 1;
                }
            }
        }
        while (await reader.NextResultAsync(cancellationToken));

        DateTime completedUtc = DateTime.UtcNow;

        return new SalesHeaderValidationResult(
            ResultSetsChecked: resultSetsChecked,
            TotalExceptions: totalExceptions,
            ExceptionsByIssue: exceptionsByIssue,
            StartedUtc: startedUtc,
            CompletedUtc: completedUtc);
    }

    private static int GetValidationIssueOrdinal(SqlDataReader reader)
    {
        for (int ordinal = 0; ordinal < reader.FieldCount; ordinal++)
        {
            if (string.Equals(
                reader.GetName(ordinal),
                "ValidationIssue",
                StringComparison.OrdinalIgnoreCase))
            {
                return ordinal;
            }
        }

        return -1;
    }
}

/// <summary>
/// Purpose:
/// Contains the summarized result returned after all SalesHeaders validation
/// result sets have been inspected.
///
/// TotalExceptions equal to zero indicates that SalesHeaders validation passed.
/// A value greater than zero indicates that one or more validation checks failed.
/// </summary>
public sealed record SalesHeaderValidationResult(
    int ResultSetsChecked,
    int TotalExceptions,
    IReadOnlyDictionary<string, int> ExceptionsByIssue,
    DateTime StartedUtc,
    DateTime CompletedUtc)
{
    public bool Passed => TotalExceptions == 0;
}
