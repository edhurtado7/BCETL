using BCETL.Data;

namespace BCETL.BusinessCentral;

public sealed class SalesInvoiceLineExtractor
{
    private const string EntityName = "SalesInvoiceLines";
    private const int BatchSize = 1000;
    private readonly BusinessCentralClient _client;
    private readonly SalesInvoiceLineRepository _repository;
    private readonly WatermarkRepository _watermarks;

    public SalesInvoiceLineExtractor(BusinessCentralClient client,
        SalesInvoiceLineRepository repository, WatermarkRepository watermarks)
    {
        _client = client;
        _repository = repository;
        _watermarks = watermarks;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        DateTime started = DateTime.UtcNow;
        WatermarkState wm = await _watermarks.GetAsync(EntityName, ct);
        string mode = wm.LastStamp.HasValue ? "Incremental" : "Full";
        DateTime? requestFrom = wm.LastStamp?.AddSeconds(-wm.OverlapSeconds);
        long runId = await _watermarks.BeginRunAsync(EntityName, mode, started, requestFrom, ct);
        long fetched = 0, inserted = 0, updated = 0;
        DateTime? maxStamp = null;

        try
        {
            PrintStart(mode, started, wm.LastStamp, requestFrom, wm.OverlapSeconds);
            var batch = new List<BcSalesInvoiceLine>(BatchSize);

            await foreach (BcSalesInvoiceLine row in _client.ReadSalesInvoiceLinesAsync(requestFrom, ct))
            {
                batch.Add(row);
                if (batch.Count == BatchSize) await FlushAsync();
            }
            if (batch.Count > 0) await FlushAsync();

            DateTime completed = DateTime.UtcNow;
            await _watermarks.CompleteAsync(runId, EntityName, completed,
                maxStamp, fetched, inserted, updated, ct);
            PrintComplete(fetched, inserted, updated, maxStamp);

            async Task FlushAsync()
            {
                BcSalesInvoiceLine[] payload = batch.ToArray();
                batch.Clear();
                MergeResult r = await _repository.MergeAsync(payload, DateTime.UtcNow, ct);
                fetched += r.Fetched; inserted += r.Inserted; updated += r.Updated;
                if (r.MaxStamp.HasValue && (!maxStamp.HasValue || r.MaxStamp > maxStamp)) maxStamp = r.MaxStamp;
                Console.WriteLine($"Rows fetched/inserted/updated: {fetched:N0} / {inserted:N0} / {updated:N0}");
                Console.WriteLine($"Dataset watermark candidate: {Fmt(maxStamp)}");
            }
        }
        catch (Exception ex)
        {
            await _watermarks.FailAsync(runId, EntityName, DateTime.UtcNow,
                fetched, inserted, updated, ex.ToString(), CancellationToken.None);
            throw;
        }
    }

    private static void PrintStart(string mode, DateTime started, DateTime? saved, DateTime? from, int overlap)
    {
        Console.WriteLine("============================================================");
        Console.WriteLine("Currently executing SalesInvoiceLines");
        Console.WriteLine("============================================================");
        Console.WriteLine($"Load Mode:       {mode}");
        Console.WriteLine($"Started UTC:     {started:O}");
        Console.WriteLine($"Saved Watermark: {Fmt(saved)}");
        Console.WriteLine($"Request From:    {Fmt(from)}");
        Console.WriteLine($"Overlap Seconds: {overlap}");
    }

    private static void PrintComplete(long fetched, long inserted, long updated, DateTime? stamp)
    {
        Console.WriteLine();
        Console.WriteLine("SalesInvoiceLines extraction completed successfully.");
        Console.WriteLine($"Total fetched:    {fetched:N0}");
        Console.WriteLine($"Total inserted:   {inserted:N0}");
        Console.WriteLine($"Total updated:    {updated:N0}");
        Console.WriteLine($"Last Stamp:       {Fmt(stamp)}");
        Console.WriteLine($"Completed Local:  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    }

    private static string Fmt(DateTime? value) => value?.ToUniversalTime().ToString("O") ?? "(none)";
}
