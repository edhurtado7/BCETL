using System.Data;
using Microsoft.Data.SqlClient;

namespace BCETL.Data;

/// <summary>
/// Replaces the complete Sales Header reconciliation-key staging population
/// and executes the SQL reconciliation procedure in one transaction.
///
/// Safety:
/// - Rejects an empty Business Central key population.
/// - Clears staging only after the complete BC API retrieval has succeeded.
/// - Rolls back staging and reconciliation together if any SQL operation fails.
///
/// Warehouse result:
/// Sales Header rows missing from the current BC population remain in SQL,
/// but are marked inactive for lifecycle, Fabric, and Power BI reporting.
/// </summary>
public sealed class SalesHeaderReconciliationRepository
{
    private readonly string _connectionString;

    public SalesHeaderReconciliationRepository(string connectionString) =>
        _connectionString = connectionString;

    public async Task<SalesHeaderReconciliationResult> ReconcileAsync(
        IReadOnlyCollection<Guid> systemIds,
        CancellationToken cancellationToken)
    {
        if (systemIds.Count == 0)
        {
            throw new InvalidOperationException(
                "Sales Header reconciliation aborted because Business Central returned zero SystemIds.");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using SqlTransaction transaction =
            (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using (var clearCommand = new SqlCommand(
                "TRUNCATE TABLE [bc].[dbo].[ReconciliationSalesHeaders];",
                connection,
                transaction))
            {
                await clearCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            var staging = new DataTable();
            staging.Columns.Add("SystemId", typeof(Guid));

            foreach (Guid systemId in systemIds.Distinct())
            {
                staging.Rows.Add(systemId);
            }

            using (var bulkCopy = new SqlBulkCopy(
                connection,
                SqlBulkCopyOptions.CheckConstraints,
                transaction))
            {
                bulkCopy.DestinationTableName =
                    "[bc].[dbo].[ReconciliationSalesHeaders]";
                bulkCopy.ColumnMappings.Add("SystemId", "SystemId");
                bulkCopy.BatchSize = 5000;
                bulkCopy.BulkCopyTimeout = 600;

                await bulkCopy.WriteToServerAsync(
                    staging,
                    cancellationToken);
            }

            await using var command = new SqlCommand(
                "[bc].[dbo].[ReconcileSalesHeaders]",
                connection,
                transaction)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 600
            };

            await using SqlDataReader reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "[bc].[dbo].[ReconcileSalesHeaders] returned no result row.");
            }

            var result = new SalesHeaderReconciliationResult(
                SourceIdsFetched: systemIds.Distinct().Count(),
                RowsChecked: reader.GetInt32(reader.GetOrdinal("RowsChecked")),
                RowsDeactivated: reader.GetInt32(reader.GetOrdinal("RowsDeactivated")),
                StartedUtc: DateTime.SpecifyKind(
                    reader.GetDateTime(reader.GetOrdinal("StartedUtc")),
                    DateTimeKind.Utc),
                CompletedUtc: DateTime.SpecifyKind(
                    reader.GetDateTime(reader.GetOrdinal("CompletedUtc")),
                    DateTimeKind.Utc));

            await reader.CloseAsync();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}

public sealed record SalesHeaderReconciliationResult(
    int SourceIdsFetched,
    int RowsChecked,
    int RowsDeactivated,
    DateTime StartedUtc,
    DateTime CompletedUtc);
