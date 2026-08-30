using BCETL.Authentication;
using BCETL.BusinessCentral;
using BCETL.Configuration;
using BCETL.Data;

namespace BCETL;

/// <summary>
/// BCETL command-line entry point.
///
/// Purpose:
/// Routes entity load, composite load, reconciliation, and validation
/// commands while ensuring help output does not require SQL or OAuth
/// configuration.
///
/// Command structure:
/// - Entity loads: customers, sih, sil, sh, sl
/// - Composite loads: invoices, open, all
/// - Reconciliation: reconcile sh, reconcile sl, reconcile all
/// - Validation: validate sh, validate sl, validate all
/// - Configuration: config
/// Configuration behavior:
/// - Validation requires SQL configuration only.
/// - Load and reconciliation require SQL and Business Central configuration.
/// - Help does not require SQL or Business Central configuration.
/// - Configuration: config
/// - Configuration: config, config test

/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            string command = args.Length == 0
                ? "help"
                : args[0].Trim().ToLowerInvariant();

            string? subcommand = args.Length > 1
                ? args[1].Trim().ToLowerInvariant()
                : null;

            if (command is "help" or "-help" or "--help")
            {
                PrintHelp();
                return 0;
            }

bool isConfig =
    command == "config" &&
    subcommand is null;

bool isConfigTest =
    command == "config" &&
    subcommand == "test";
	
            bool isReconcileSalesHeaders =
                command == "reconcile" && subcommand == "sh";

            bool isReconcileSalesLines =
                command == "reconcile" && subcommand == "sl";

            bool isReconcileAll =
                command == "reconcile" && subcommand == "all";

            bool isValidateSalesHeaders =
                command == "validate" && subcommand == "sh";

            bool isValidateSalesLines =
                command == "validate" && subcommand == "sl";

            bool isValidateAll =
                command == "validate" && subcommand == "all";



            string[] loadModes =
            [
                "customers", "customer", "cust",
                "sih", "salesinvoiceheaders",
                "sil", "salesinvoicelines",
                "sh", "salesheaders",
                "sl", "saleslines",
                "invoices", "open", "all"
            ];

            if (!loadModes.Contains(command) &&
				!isConfig &&
				!isConfigTest &&
                !isReconcileSalesHeaders &&
                !isReconcileSalesLines &&
                !isReconcileAll &&
                !isValidateSalesHeaders &&
                !isValidateSalesLines &&
                !isValidateAll)
            {
                if (command == "reconcile")
                {
                    Console.Error.WriteLine(
                        $"Unknown reconciliation target: {subcommand ?? "(missing)"}");
                }
                else if (command == "validate")
                {
                    Console.Error.WriteLine(
                        $"Unknown validation target: {subcommand ?? "(missing)"}");
                }
                else
                {
                    Console.Error.WriteLine($"Unknown mode: {command}");
                }

                Console.Error.WriteLine();
                PrintHelp();
                return 2;
            }

if (isConfig)
{
    var configuration =
        new ConfigurationStatusService();

    configuration.Run();

    return 0;
}
            string sql = RuntimeSettings.SqlConnectionString;
			/*
Purpose:
Test the configured SQL connection and report the SQL-recognized
server, database, and security identities.

The test is read-only and does not expose the SQL connection string,
SQL passwords, BCETL client secrets, or access tokens.
*/
if (isConfigTest)
{
    var configurationTest =
        new ConfigurationTestService(sql);

    bool passed =
        await configurationTest.RunAsync(cts.Token);

    return passed ? 0 : 5;
}

if (isValidateSalesHeaders)
{
    var validation =
        new SalesHeaderValidationService(
            new SalesHeaderValidationRepository(sql));

    bool passed =
        await validation.RunAsync(cts.Token);

    return passed ? 0 : 4;
}

            /*
            Purpose:
            Run SalesHeaders validation without initializing Business Central
            OAuth or creating a Business Central API client.
            */
            if (isValidateSalesHeaders)
            {
                var validation =
                    new SalesHeaderValidationService(
                        new SalesHeaderValidationRepository(sql));

                bool passed =
                    await validation.RunAsync(cts.Token);

                return passed ? 0 : 4;
            }

            /*
            Purpose:
            Run SalesLines validation without initializing Business Central
            OAuth or creating a Business Central API client.
            */
            if (isValidateSalesLines)
            {
                var validation =
                    new SalesLineValidationService(
                        new SalesLineValidationRepository(sql));

                bool passed =
                    await validation.RunAsync(cts.Token);

                return passed ? 0 : 4;
            }

            /*
            Purpose:
            Run all currently supported SQL-only validations and provide a
            consolidated operator-facing result.

            Current scope:
            - SalesHeaders
            - SalesLines

            Exit codes:
            - 0: All validations passed.
            - 4: Validation completed, but one or more exceptions were found.

            Future Enhancement Backlog:
            - Add LastSeenUtc validation after LastSeenUtc is implemented.
            - Add validations for additional BCETL datasets.
            - Persist consolidated validation history and metrics.
            */
            if (isValidateAll)
            {
                var salesHeaderValidation =
                    new SalesHeaderValidationService(
                        new SalesHeaderValidationRepository(sql));

                var salesLineValidation =
                    new SalesLineValidationService(
                        new SalesLineValidationRepository(sql));

                bool salesHeadersPassed =
                    await salesHeaderValidation.RunAsync(cts.Token);

                Console.WriteLine();

                bool salesLinesPassed =
                    await salesLineValidation.RunAsync(cts.Token);

                bool allPassed =
                    salesHeadersPassed && salesLinesPassed;

                Console.WriteLine();
                Console.WriteLine(
                    "============================================================");
                Console.WriteLine("BCETL Validation Summary");
                Console.WriteLine(
                    "============================================================");
                Console.WriteLine();
                Console.WriteLine(
                    $"SalesHeaders: {FormatValidationStatus(salesHeadersPassed)}");
                Console.WriteLine(
                    $"SalesLines:   {FormatValidationStatus(salesLinesPassed)}");
                Console.WriteLine();
                Console.WriteLine(
                    $"Overall Validation Status: {FormatValidationStatus(allPassed)}");

                return allPassed ? 0 : 4;
            }

            BcEtlOptions options = BcEtlOptions.Load();

            var tokenService =
                new OAuthTokenService(options.BusinessCentral);

            var bcClient =
                new BusinessCentralClient(
                    options.BusinessCentral,
                    tokenService);

            var watermarks =
                new WatermarkRepository(sql);

            if (isReconcileSalesHeaders)
            {
                var reconciliation =
                    new SalesHeaderReconciliationService(
                        bcClient,
                        new SalesHeaderReconciliationRepository(sql));

                await reconciliation.RunAsync(cts.Token);
                return 0;
            }

            if (isReconcileSalesLines)
            {
                var reconciliation =
                    new SalesLineReconciliationService(
                        bcClient,
                        new SalesLineReconciliationRepository(sql));

                await reconciliation.RunAsync(cts.Token);
                return 0;
            }

            if (isReconcileAll)
            {
                var salesHeaderReconciliation =
                    new SalesHeaderReconciliationService(
                        bcClient,
                        new SalesHeaderReconciliationRepository(sql));

                var salesLineReconciliation =
                    new SalesLineReconciliationService(
                        bcClient,
                        new SalesLineReconciliationRepository(sql));

                await salesHeaderReconciliation.RunAsync(cts.Token);
                Console.WriteLine();
                await salesLineReconciliation.RunAsync(cts.Token);
                return 0;
            }



            var customers =
                new CustomerExtractor(
                    bcClient,
                    new CustomerRepository(sql),
                    watermarks);

            var sih =
                new SalesInvoiceHeaderExtractor(
                    bcClient,
                    new SalesInvoiceHeaderRepository(sql),
                    watermarks);

            var sil =
                new SalesInvoiceLineExtractor(
                    bcClient,
                    new SalesInvoiceLineRepository(sql),
                    watermarks);

            var sh =
                new SalesHeaderExtractor(
                    bcClient,
                    new SalesHeaderRepository(sql),
                    watermarks);

            var sl =
                new SalesLineExtractor(
                    bcClient,
                    new SalesLineRepository(sql),
                    watermarks);

            if (command is "customers" or "customer" or "cust")
            {
                await customers.RunAsync(cts.Token);
            }
            else if (command is "sih" or "salesinvoiceheaders")
            {
                await sih.RunAsync(cts.Token);
            }
            else if (command is "sil" or "salesinvoicelines")
            {
                await sil.RunAsync(cts.Token);
            }
            else if (command is "sh" or "salesheaders")
            {
                await sh.RunAsync(cts.Token);
            }
            else if (command is "sl" or "saleslines")
            {
                await sl.RunAsync(cts.Token);
            }
            else if (command == "invoices")
            {
                await sih.RunAsync(cts.Token);
                Console.WriteLine();
                await sil.RunAsync(cts.Token);
            }
            else if (command == "open")
            {
                await sh.RunAsync(cts.Token);
                Console.WriteLine();
                await sl.RunAsync(cts.Token);
            }
            else if (command == "all")
            {
                await customers.RunAsync(cts.Token);
                Console.WriteLine();
                await sih.RunAsync(cts.Token);
                Console.WriteLine();
                await sil.RunAsync(cts.Token);
                Console.WriteLine();
                await sh.RunAsync(cts.Token);
                Console.WriteLine();
                await sl.RunAsync(cts.Token);
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("BCETL was cancelled.");
            return 3;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("BCETL terminated with an error.");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    /// <summary>
    /// Purpose:
    /// Converts a validation Boolean into a consistent operator-facing
    /// PASSED or FAILED status value.
    /// </summary>
    private static string FormatValidationStatus(bool passed)
    {
        return passed ? "PASSED" : "FAILED";
    }

    /// <summary>
    /// Purpose:
    /// Displays supported BCETL commands and configuration requirements
    /// without initializing SQL or Business Central authentication.
    /// </summary>
    private static void PrintHelp()
    {
        Console.WriteLine("BCETL - Business Central extraction utility");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  BCETL <mode>");
        Console.WriteLine("  BCETL reconcile <target>");
        Console.WriteLine("  BCETL validate <target>");
        Console.WriteLine();

        Console.WriteLine("Entity Load Modes");
        Console.WriteLine("-----------------");
        Console.WriteLine("customers  Load [bc].[dbo].[Customers]");
        Console.WriteLine("sih        Load [bc].[dbo].[SalesInvoiceHeaders]");
        Console.WriteLine("sil        Load [bc].[dbo].[SalesInvoiceLines]");
        Console.WriteLine("sh         Load [bc].[dbo].[SalesHeaders]");
        Console.WriteLine("sl         Load [bc].[dbo].[SalesLines]");
        Console.WriteLine();

        Console.WriteLine("Composite Load Modes");
        Console.WriteLine("--------------------");
        Console.WriteLine("invoices   Run SIH, then SIL");
        Console.WriteLine("open       Run SH, then SL");
        Console.WriteLine("all        Run all five entity loads");
        Console.WriteLine();

Console.WriteLine("Configuration");
Console.WriteLine("-------------");
Console.WriteLine(
    "config       Display BCETL configuration status");
Console.WriteLine(
    "config test  Test SQL connectivity and report the SQL-recognized identity");
Console.WriteLine();

        Console.WriteLine("Reconciliation Modes");
        Console.WriteLine("--------------------");
        Console.WriteLine(
            "reconcile sh   Reconcile current BC Sales Headers against SQL");
        Console.WriteLine(
            "reconcile sl   Reconcile current BC Sales Lines against SQL");
        Console.WriteLine(
            "reconcile all  Run Sales Header reconciliation, then Sales Line reconciliation");
        Console.WriteLine();

        Console.WriteLine("Validation Modes");
        Console.WriteLine("----------------");
        Console.WriteLine(
            "validate sh    Validate lifecycle and data integrity in [bc].[dbo].[SalesHeaders]");
        Console.WriteLine(
            "validate sl    Validate lifecycle and data integrity in [bc].[dbo].[SalesLines]");
        Console.WriteLine(
            "validate all   Run SalesHeaders validation, then SalesLines validation");
        Console.WriteLine();

        Console.WriteLine("Configuration Requirements");
        Console.WriteLine("--------------------------");
        Console.WriteLine(
            "BCETL_SQL_CONNECTION  Required for load, reconciliation, and validation modes");
        Console.WriteLine(
            "BCETL_CLIENT_SECRET   Required for load and reconciliation modes");
    }
}
