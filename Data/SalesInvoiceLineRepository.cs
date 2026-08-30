using BCETL.BusinessCentral;

namespace BCETL.Data;

public sealed class SalesInvoiceLineRepository
{
    private readonly string _connectionString;
    public SalesInvoiceLineRepository(string connectionString) => _connectionString = connectionString;

    public Task<MergeResult> MergeAsync(IReadOnlyCollection<BcSalesInvoiceLine> rows,
        DateTime extractedAtUtc, CancellationToken ct) =>
        MergeHelper.ExecuteAsync(_connectionString, "[bc].[dbo].[LoadSalesInvoiceLines]",
            rows, extractedAtUtc, ct);
}
