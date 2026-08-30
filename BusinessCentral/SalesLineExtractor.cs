using BCETL.Data;

namespace BCETL.BusinessCentral;

public sealed class SalesLineExtractor
{
    private const string EntityName = "SalesLines";
    private const int BatchSize = 1000;
    private readonly BusinessCentralClient _client;
    private readonly SalesLineRepository _repository;
    private readonly WatermarkRepository _watermarks;
    public SalesLineExtractor(BusinessCentralClient client, SalesLineRepository repository, WatermarkRepository watermarks) { _client=client; _repository=repository; _watermarks=watermarks; }
    public async Task RunAsync(CancellationToken ct)
    {
        DateTime started=DateTime.UtcNow; WatermarkState wm=await _watermarks.GetAsync(EntityName,ct);
        string mode=wm.LastStamp.HasValue?"Incremental":"Full"; DateTime? from=wm.LastStamp?.AddSeconds(-wm.OverlapSeconds);
        long runId=await _watermarks.BeginRunAsync(EntityName,mode,started,from,ct); long fetched=0,inserted=0,updated=0; DateTime? max=null;
        try
        {
            Console.WriteLine("============================================================"); Console.WriteLine("Currently executing SalesLines"); Console.WriteLine("============================================================");
            Console.WriteLine($"Load Mode:       {mode}"); Console.WriteLine($"Started PT:     {Pt(started)}"); Console.WriteLine($"Started UTC:     {Utc(started)}");
            Console.WriteLine($"Saved Watermark PT: {Pt(wm.LastStamp)}"); Console.WriteLine($"Saved Watermark UTC: {Utc(wm.LastStamp)}");
            Console.WriteLine($"Request From PT:    {Pt(from)}"); Console.WriteLine($"Request From UTC:    {Utc(from)}"); Console.WriteLine($"Overlap Seconds: {wm.OverlapSeconds}");
            var batch=new List<BcSalesLine>(BatchSize);
            await foreach(var row in _client.ReadSalesLinesAsync(from,ct)) { batch.Add(row); if(batch.Count==BatchSize) await Flush(); }
            if(batch.Count>0) await Flush();
            await _watermarks.CompleteAsync(runId,EntityName,DateTime.UtcNow,max,fetched,inserted,updated,ct);
            Console.WriteLine(); Console.WriteLine("SalesLines extraction completed successfully."); Console.WriteLine($"Total fetched: {fetched:N0}"); Console.WriteLine($"Total inserted: {inserted:N0}"); Console.WriteLine($"Total updated: {updated:N0}"); Console.WriteLine($"Last Stamp PT: {Pt(max)}"); Console.WriteLine($"Last Stamp UTC: {Utc(max)}");
            async Task Flush() { var payload=batch.ToArray(); batch.Clear(); MergeResult r=await _repository.MergeAsync(payload,DateTime.UtcNow,ct); fetched+=r.Fetched; inserted+=r.Inserted; updated+=r.Updated; if(r.MaxStamp.HasValue&&(!max.HasValue||r.MaxStamp>max))max=r.MaxStamp; Console.WriteLine($"Rows fetched/inserted/updated: {fetched:N0} / {inserted:N0} / {updated:N0}"); }
        }
        catch(Exception ex) { await _watermarks.FailAsync(runId,EntityName,DateTime.UtcNow,fetched,inserted,updated,ex.ToString(),CancellationToken.None); throw; }
    }
    private static readonly TimeZoneInfo Pacific=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
    private static string Pt(DateTime? d)=>d.HasValue?TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(d.Value,DateTimeKind.Utc),Pacific).ToString("yyyy-MM-dd HH:mm:ss.fffffff"):"(none)";
    private static string Utc(DateTime? d)=>d?.ToUniversalTime().ToString("O")??"(none)";
}
