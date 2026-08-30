using System.Data;
using BCETL.BusinessCentral;
using Microsoft.Data.SqlClient;

namespace BCETL.Data;

public sealed class WatermarkRepository
{
    private readonly string _connectionString;
    public WatermarkRepository(string connectionString) => _connectionString = connectionString;

    public async Task<WatermarkState> GetAsync(string entityName, CancellationToken ct)
    {
        const string sql = """
            SELECT [LastSuccessfulModifiedAt], [OverlapSeconds]
            FROM [bc].[dbo].[EtlWatermarks]
            WHERE [EntityName] = @EntityName AND [IsEnabled] = 1;
            """;
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@EntityName", SqlDbType.NVarChar, 100).Value = entityName;
        await using var rd = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        if (!await rd.ReadAsync(ct))
            throw new InvalidOperationException($"No enabled watermark row for {entityName}.");
        DateTime? stamp = rd.IsDBNull(0) ? null : DateTime.SpecifyKind(rd.GetDateTime(0), DateTimeKind.Utc);
        return new WatermarkState(stamp, rd.GetInt32(1));
    }

    public async Task<long> BeginRunAsync(string entityName, string mode, DateTime started,
        DateTime? watermarkFrom, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO [bc].[dbo].[EtlRuns]
            ([EntityName],[RunStatus],[LoadMode],[RunStartedAt],[WatermarkFrom])
            OUTPUT INSERTED.[RunId]
            VALUES (@EntityName,N'Running',@Mode,@Started,@WatermarkFrom);
            """;
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@EntityName", SqlDbType.NVarChar, 100).Value = entityName;
        cmd.Parameters.Add("@Mode", SqlDbType.NVarChar, 20).Value = mode;
        cmd.Parameters.Add("@Started", SqlDbType.DateTime2).Value = started;
        cmd.Parameters.Add("@WatermarkFrom", SqlDbType.DateTime2).Value = watermarkFrom ?? (object)DBNull.Value;
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task CompleteAsync(long runId, string entityName, DateTime completed,
        DateTime? through, long fetched, long inserted, long updated, CancellationToken ct)
    {
        const string sql = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            UPDATE [bc].[dbo].[EtlRuns]
            SET [RunStatus]=N'Success',[RunCompletedAt]=@Completed,
                [WatermarkThrough]=@Through,[RowsFetched]=@Fetched,
                [RowsInserted]=@Inserted,[RowsUpdated]=@Updated
            WHERE [RunId]=@RunId;
            UPDATE [bc].[dbo].[EtlWatermarks]
            SET [LastSuccessfulModifiedAt] = CASE
                    WHEN @Through IS NULL THEN [LastSuccessfulModifiedAt]
                    WHEN [LastSuccessfulModifiedAt] IS NULL OR @Through > [LastSuccessfulModifiedAt]
                    THEN @Through ELSE [LastSuccessfulModifiedAt] END,
                [LastRunCompletedAt]=@Completed,[LastRowsFetched]=@Fetched,
                [LastRowsInserted]=@Inserted,[LastRowsUpdated]=@Updated
            WHERE [EntityName]=@EntityName;
            COMMIT;
            """;
        await ExecuteAsync(sql, runId, entityName, completed, through, fetched, inserted, updated, null, ct);
    }

    public async Task FailAsync(long runId, string entityName, DateTime completed,
        long fetched, long inserted, long updated, string error, CancellationToken ct)
    {
        const string sql = """
            UPDATE [bc].[dbo].[EtlRuns]
            SET [RunStatus]=N'Failed',[RunCompletedAt]=@Completed,
                [RowsFetched]=@Fetched,[RowsInserted]=@Inserted,[RowsUpdated]=@Updated,
                [ErrorMessage]=@Error
            WHERE [RunId]=@RunId;
            """;
        await ExecuteAsync(sql, runId, entityName, completed, null, fetched, inserted, updated, error, ct);
    }

    private async Task ExecuteAsync(string sql, long runId, string entityName, DateTime completed,
        DateTime? through, long fetched, long inserted, long updated, string? error, CancellationToken ct)
    {
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@RunId", runId);
        cmd.Parameters.Add("@EntityName", SqlDbType.NVarChar, 100).Value = entityName;
        cmd.Parameters.Add("@Completed", SqlDbType.DateTime2).Value = completed;
        cmd.Parameters.Add("@Through", SqlDbType.DateTime2).Value = through ?? (object)DBNull.Value;
        cmd.Parameters.AddWithValue("@Fetched", fetched);
        cmd.Parameters.AddWithValue("@Inserted", inserted);
        cmd.Parameters.AddWithValue("@Updated", updated);
        cmd.Parameters.Add("@Error", SqlDbType.NVarChar, -1).Value = error ?? (object)DBNull.Value;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
