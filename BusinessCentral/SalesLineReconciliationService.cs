using BCETL.Data;

namespace BCETL.BusinessCentral;

/// <summary>
/// Performs complete source-presence reconciliation for Business Central
/// Sales Line records.
///
/// Purpose:
/// Incremental SystemModifiedAt extraction detects inserts and updates, but
/// cannot detect line records that disappear after posting or deletion.
///
/// Process:
/// 1. Retrieve the complete current Sales Line population from BC.
/// 2. Hold all SystemIds in memory until API retrieval fully succeeds.
/// 3. Transactionally replace SQL reconciliation staging keys.
/// 4. Execute [bc].[dbo].[ReconcileSalesLines].
/// 5. Retain missing SQL rows while marking them inactive.
///
/// Safety:
/// A zero-row BC result aborts without changing SQL staging or active states.
/// </summary>
public sealed class SalesLineReconciliationService
{
    private readonly BusinessCentralClient _client;
    private readonly SalesLineReconciliationRepository _repository;
    private static readonly TimeZoneInfo PacificTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

    public SalesLineReconciliationService(
        BusinessCentralClient client,
        SalesLineReconciliationRepository repository)
    {
        _client = client;
        _repository = repository;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        DateTime requestStartedUtc = DateTime.UtcNow;

        Console.WriteLine("============================================================");
        Console.WriteLine("Currently reconciling SalesLines");
        Console.WriteLine("============================================================");
        Console.WriteLine($"Started PT:  {FormatPt(requestStartedUtc)}");
        Console.WriteLine($"Started UTC: {FormatUtc(requestStartedUtc)}");
        Console.WriteLine();
        Console.WriteLine("Retrieving the complete current Sales Line population from BC...");

        var systemIds = new HashSet<Guid>();

        await foreach (BcSalesLine line in
            _client.ReadSalesLinesAsync(
                null,
                cancellationToken))
        {
            systemIds.Add(line.SystemId);
        }

        if (systemIds.Count == 0)
        {
            throw new InvalidOperationException(
                "Sales Line reconciliation aborted because Business Central returned zero records.");
        }

        Console.WriteLine($"Current BC SystemIds fetched: {systemIds.Count:N0}");
        Console.WriteLine("Replacing staging keys and executing SQL reconciliation...");

        SalesLineReconciliationResult result =
            await _repository.ReconcileAsync(systemIds, cancellationToken);

        Console.WriteLine();
        Console.WriteLine("SalesLines reconciliation completed successfully.");
        Console.WriteLine($"Source IDs fetched: {result.SourceIdsFetched:N0}");
        Console.WriteLine($"SQL rows checked:  {result.RowsChecked:N0}");
        Console.WriteLine($"Rows deactivated:  {result.RowsDeactivated:N0}");
        Console.WriteLine($"Started PT:        {FormatPt(result.StartedUtc)}");
        Console.WriteLine($"Started UTC:       {FormatUtc(result.StartedUtc)}");
        Console.WriteLine($"Completed PT:      {FormatPt(result.CompletedUtc)}");
        Console.WriteLine($"Completed UTC:     {FormatUtc(result.CompletedUtc)}");
    }

    private static string FormatPt(DateTime value) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(value, DateTimeKind.Utc),
            PacificTimeZone)
        .ToString("yyyy-MM-dd HH:mm:ss.fffffff");

    private static string FormatUtc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");
}
