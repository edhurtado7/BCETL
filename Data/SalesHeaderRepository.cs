using System.Data;
using System.Text.Json;
using BCETL.BusinessCentral;
using Microsoft.Data.SqlClient;

namespace BCETL.Data;

public sealed class SalesHeaderRepository
{
    private readonly string _connectionString;
    public SalesHeaderRepository(string connectionString) => _connectionString = connectionString;
    public async Task<MergeResult> MergeAsync(IReadOnlyCollection<BcSalesHeader> rows, DateTime extractedAtUtc, CancellationToken ct)
    {
        if (rows.Count == 0) return new MergeResult(0, 0, 0, null);
        string json = JsonSerializer.Serialize(rows, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand("[bc].[dbo].[LoadSalesHeaders]", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 600 };
        cmd.Parameters.Add("@JsonPayload", SqlDbType.NVarChar, -1).Value = json;
        cmd.Parameters.Add("@ExtractedAt", SqlDbType.DateTime2).Value = extractedAtUtc;
        await using SqlDataReader rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct)) throw new InvalidOperationException("LoadSalesHeaders returned no result.");
        return new MergeResult(rd.GetInt32(0), rd.GetInt32(1), rd.GetInt32(2), rd.IsDBNull(3) ? null : DateTime.SpecifyKind(rd.GetDateTime(3), DateTimeKind.Utc));
    }
}
