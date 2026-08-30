using System.Data;
using System.Text.Json;
using BCETL.BusinessCentral;
using Microsoft.Data.SqlClient;

namespace BCETL.Data;

public sealed class SalesInvoiceHeaderRepository
{
    private readonly string _connectionString;
    public SalesInvoiceHeaderRepository(string connectionString) => _connectionString = connectionString;

    public Task<MergeResult> MergeAsync(IReadOnlyCollection<BcSalesInvoiceHeader> rows,
        DateTime extractedAtUtc, CancellationToken ct) =>
        MergeHelper.ExecuteAsync(_connectionString, "[bc].[dbo].[LoadSalesInvoiceHeaders]",
            rows, extractedAtUtc, ct);
}

internal static class MergeHelper
{
    public static async Task<MergeResult> ExecuteAsync<T>(string connectionString,
        string procedureName, IReadOnlyCollection<T> rows, DateTime extractedAtUtc,
        CancellationToken ct)
    {
        if (rows.Count == 0) return new MergeResult(0, 0, 0, null);
        string json = JsonSerializer.Serialize(rows, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(procedureName, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 600
        };
        command.Parameters.Add("@JsonPayload", SqlDbType.NVarChar, -1).Value = json;
        command.Parameters.Add("@ExtractedAt", SqlDbType.DateTime2).Value = extractedAtUtc;

        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException($"{procedureName} returned no result.");

        return new MergeResult(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
            reader.IsDBNull(3) ? null : DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc));
    }
}
