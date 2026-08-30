using BCETL.Data;

namespace BCETL.BusinessCentral;

/// <summary>
/// Purpose:
/// Runs the SalesLines warehouse validation and presents the validation
/// result in an operator-friendly BCETL console format.
///
/// This service calls SalesLineValidationRepository, which executes
/// [bc].[dbo].[ValidateSalesLines] and inspects every SQL result set.
///
/// Validation is read-only and does not modify SalesLines data.
///
/// A successful validation indicates:
///
/// - Lifecycle fields are populated correctly.
/// - Active and inactive states are internally consistent.
/// - Lifecycle timestamp sequencing is valid.
/// - Required document and line identifiers are populated.
/// - Duplicate Business Central SystemIds were not detected.
///
/// Business Purpose:
///
/// Allows BCETL operators to verify retained SalesLines data integrity
/// without requiring direct access to SQL Server Management Studio.
///
/// Validation can be run after ETL loads, reconciliation cycles,
/// deployments, troubleshooting activities, or before downstream
/// consumption by Power BI, Fabric, reporting, or analytical processes.
///
/// Time Handling:
///
/// SQL validation execution times are retained in UTC.
/// Operator-facing execution times are also presented in Pacific Time (PT).
///
/// Future Enhancement Backlog:
///
/// - Add LastSeenUtc validation after LastSeenUtc is implemented.
/// - Add parent/child validation between SalesLines and SalesHeaders.
/// - Persist validation execution history.
/// - Display selected validation exception details.
/// - Add validation duration and performance metrics.
/// - Include validation results in automated operational reporting.
/// </summary>
public sealed class SalesLineValidationService
{
    private readonly SalesLineValidationRepository _repository;

    private static readonly TimeZoneInfo PacificTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

    public SalesLineValidationService(
        SalesLineValidationRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Purpose:
    /// Executes SalesLines validation, displays the resulting operational
    /// summary, and returns true when no validation exceptions are found.
    /// </summary>
    public async Task<bool> RunAsync(
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            "============================================================");
        Console.WriteLine("Currently validating SalesLines");
        Console.WriteLine(
            "============================================================");
        Console.WriteLine();
        Console.WriteLine(
            "Executing [bc].[dbo].[ValidateSalesLines]...");

        SalesLineValidationResult result =
            await _repository.ValidateAsync(cancellationToken);

        Console.WriteLine();
        Console.WriteLine(
            $"Result sets checked:   {result.ResultSetsChecked:N0}");
        Console.WriteLine(
            $"Validation exceptions: {result.TotalExceptions:N0}");
        Console.WriteLine(
            $"Started PT:            {FormatPt(result.StartedUtc)}");
        Console.WriteLine(
            $"Started UTC:           {FormatUtc(result.StartedUtc)}");
        Console.WriteLine(
            $"Completed PT:          {FormatPt(result.CompletedUtc)}");
        Console.WriteLine(
            $"Completed UTC:         {FormatUtc(result.CompletedUtc)}");
        Console.WriteLine();

        if (result.Passed)
        {
            Console.WriteLine("SalesLines Validation = PASSED");
            return true;
        }

        Console.WriteLine("SalesLines Validation = FAILED");
        Console.WriteLine();
        Console.WriteLine("Validation exceptions by issue:");

        foreach (KeyValuePair<string, int> exception
                 in result.ExceptionsByIssue.OrderBy(item => item.Key))
        {
            Console.WriteLine(
                $"  {exception.Key}: {exception.Value:N0}");
        }

        return false;
    }

    /// <summary>
    /// Purpose:
    /// Converts a UTC timestamp to Pacific Time for operator presentation.
    /// The SQL and repository values remain stored and transported in UTC.
    /// </summary>
    private static string FormatPt(DateTime value)
    {
        DateTime utcValue =
            DateTime.SpecifyKind(value, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(
                utcValue,
                PacificTimeZone)
            .ToString("yyyy-MM-dd HH:mm:ss.fffffff");
    }

    /// <summary>
    /// Purpose:
    /// Formats a timestamp explicitly as UTC using the round-trip format.
    /// </summary>
    private static string FormatUtc(DateTime value)
    {
        return DateTime.SpecifyKind(
                value,
                DateTimeKind.Utc)
            .ToString("O");
    }
}
