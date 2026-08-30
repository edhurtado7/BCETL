using System.Data;
using System.Text.Json;
using BCETL.BusinessCentral;
using Microsoft.Data.SqlClient;

namespace BCETL.Data;

public sealed class CustomerRepository
{
    private readonly string _connectionString;
    public CustomerRepository(string connectionString) => _connectionString = connectionString;

    public async Task<MergeResult> MergeAsync(
        IReadOnlyCollection<BcCustomer> customers,
        DateTime extractedAtUtc,
        CancellationToken cancellationToken)
    {
        if (customers.Count == 0) return new MergeResult(0, 0, 0, null);
        string json = JsonSerializer.Serialize(customers, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("[bc].[dbo].[LoadCustomers]", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 600
        };
        command.Parameters.Add("@JsonPayload", SqlDbType.NVarChar, -1).Value = json;
        command.Parameters.Add("@ExtractedAt", SqlDbType.DateTime2).Value = extractedAtUtc;

        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("[bc].[dbo].[LoadCustomers] returned no result.");

        return new MergeResult(
            reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
            reader.IsDBNull(3) ? null : DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc));
    }
}
