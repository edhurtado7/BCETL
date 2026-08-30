using BCETL.Data;

namespace BCETL.BusinessCentral;

public sealed class CustomerExtractor
{
    private const string EntityName = "Customers";
    private const int BatchSize = 1000;
    private readonly BusinessCentralClient _client;
    private readonly CustomerRepository _customers;
    private readonly WatermarkRepository _watermarks;

    public CustomerExtractor(BusinessCentralClient client,
        CustomerRepository customers, WatermarkRepository watermarks)
    {
        _client = client;
        _customers = customers;
        _watermarks = watermarks;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        DateTime started = DateTime.UtcNow;
        WatermarkState watermark = await _watermarks.GetAsync(EntityName, ct);
        string mode = watermark.LastStamp.HasValue ? "Incremental" : "Full";
        DateTime? requestFrom = watermark.LastStamp?.AddSeconds(-watermark.OverlapSeconds);
        long runId = await _watermarks.BeginRunAsync(EntityName, mode, started, requestFrom, ct);

        long fetched = 0, inserted = 0, updated = 0;
        DateTime? maxStamp = null;
        try
        {
            Console.WriteLine("============================================================");
            Console.WriteLine("Currently executing Customers");
            Console.WriteLine("============================================================");
            Console.WriteLine($"Load Mode:       {mode}");
            Console.WriteLine($"Started UTC:     {started:O}");
            Console.WriteLine($"Saved Watermark: {Fmt(watermark.LastStamp)}");
            Console.WriteLine($"Request From:    {Fmt(requestFrom)}");
            Console.WriteLine($"Overlap Seconds: {watermark.OverlapSeconds}");

            var batch = new List<BcCustomer>(BatchSize);
            await foreach (BcCustomer customer in _client.ReadCustomersAsync(requestFrom, ct))
            {
                batch.Add(customer);
                if (batch.Count == BatchSize)
                    await FlushAsync(batch);
            }
            if (batch.Count > 0) await FlushAsync(batch);

            DateTime completed = DateTime.UtcNow;
            await _watermarks.CompleteAsync(runId, EntityName, completed,
                maxStamp, fetched, inserted, updated, ct);

            Console.WriteLine();
            Console.WriteLine("Customers extraction completed successfully.");
            Console.WriteLine($"Total fetched:    {fetched:N0}");
            Console.WriteLine($"Total inserted:   {inserted:N0}");
            Console.WriteLine($"Total updated:    {updated:N0}");
            Console.WriteLine($"Last Stamp:       {Fmt(maxStamp)}");
            Console.WriteLine($"Completed Local:  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            async Task FlushAsync(List<BcCustomer> current)
            {
                BcCustomer[] payload = current.ToArray();
                current.Clear();
                MergeResult result = await _customers.MergeAsync(payload, DateTime.UtcNow, ct);
                fetched += result.Fetched;
                inserted += result.Inserted;
                updated += result.Updated;
                if (result.MaxStamp.HasValue && (!maxStamp.HasValue || result.MaxStamp > maxStamp))
                    maxStamp = result.MaxStamp;
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

    private static string Fmt(DateTime? value) => value?.ToUniversalTime().ToString("O") ?? "(none)";
}
