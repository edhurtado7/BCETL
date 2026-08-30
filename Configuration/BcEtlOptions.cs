using System.Text.Json;

namespace BCETL.Configuration;

public sealed class BcEtlOptions
{
    public required BusinessCentralOptions BusinessCentral { get; init; }
    public static BcEtlOptions Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        return JsonSerializer.Deserialize<BcEtlOptions>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Unable to read appsettings.json.");
    }
}

public sealed class BusinessCentralOptions
{
    public required string TenantId { get; init; }
    public required string ClientId { get; init; }
    public required string EnvironmentName { get; init; }
    public required string CompanyName { get; init; }
    public required Guid CompanyId { get; init; }
    public required string ApiPublisher { get; init; }
    public required string ApiGroup { get; init; }
    public required string ApiVersion { get; init; }
    public required string CustomerEntitySet { get; init; }
    public required string SalesInvoiceHeaderEntitySet { get; init; }
    public required string SalesInvoiceLineEntitySet { get; init; }
    public required string SalesHeaderEntitySet { get; init; }
    public required string SalesLineEntitySet { get; init; }
    public string ClientSecret => Environment.GetEnvironmentVariable("BCETL_CLIENT_SECRET")
        ?? throw new InvalidOperationException("BCETL_CLIENT_SECRET is not configured.");
    public string ApiRoot => $"https://api.businesscentral.dynamics.com/v2.0/{TenantId}/{EnvironmentName}/api/{ApiPublisher}/{ApiGroup}/{ApiVersion}";
}

public static class RuntimeSettings
{
    public static string SqlConnectionString => Environment.GetEnvironmentVariable("BCETL_SQL_CONNECTION")
        ?? throw new InvalidOperationException("BCETL_SQL_CONNECTION is not configured.");
}
