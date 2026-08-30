using System.Data;
using Microsoft.Data.SqlClient;

namespace BCETL.Configuration;

/// <summary>
/// Purpose:
/// Tests the configured BCETL SQL connection and reports the server,
/// database, and security identities recognized by the SQL endpoint.
///
/// This service performs an actual SQL connection test. It differs from
/// ConfigurationStatusService, which inspects configuration without opening
/// a connection.
///
/// Business Purpose:
/// Allows BCETL operators to verify that the configured SQL connection is
/// usable and is reaching the intended server and database under the expected
/// security identity.
///
/// Safety:
/// - Does not display the SQL connection string.
/// - Does not display SQL passwords.
/// - Does not display the Business Central client secret.
/// - Executes a read-only diagnostic query.
///
/// Compatibility Note:
/// ORIGINAL_LOGIN() is intentionally excluded because the connected SQL
/// endpoint reported that the function is not supported.
///
/// Future Enhancement Backlog:
/// - Add optional Business Central connectivity testing.
/// - Add execution duration metrics.
/// - Add a machine-readable result mode for scheduling and monitoring.
/// </summary>
public sealed class ConfigurationTestService
{
    private readonly string _connectionString;

    public ConfigurationTestService(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Purpose:
    /// Opens the configured SQL connection, executes a read-only diagnostic
    /// query, displays the SQL-recognized connection details, and returns true
    /// when the connectivity test succeeds.
    /// </summary>
    public async Task<bool> RunAsync(
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            "============================================================");
        Console.WriteLine("BCETL Configuration and Connectivity Test");
        Console.WriteLine(
            "============================================================");
        Console.WriteLine();
        Console.WriteLine("SQL Configuration    : Configured");
        Console.WriteLine("SQL Connectivity     : Testing...");

        try
        {
            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(
                GetSqlIdentityQuery(),
                connection)
            {
                CommandType = CommandType.Text,
                CommandTimeout = 30
            };

            await using SqlDataReader reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "The SQL connectivity query returned no result row.");
            }

            string serverName =
                ReadText(reader, "ServerName");

            string databaseName =
                ReadText(reader, "DatabaseName");

            string currentLogin =
                ReadText(reader, "CurrentLogin");

            string systemUser =
                ReadText(reader, "SystemUser");

            string databaseUser =
                ReadText(reader, "DatabaseUser");

            string sessionUser =
                ReadText(reader, "SessionUser");

            Console.WriteLine("SQL Connectivity     : PASSED");
            Console.WriteLine();
            Console.WriteLine("SQL Connection Result");
            Console.WriteLine("---------------------");
            Console.WriteLine(
                $"SQL Server           : {serverName}");
            Console.WriteLine(
                $"SQL Database         : {databaseName}");
            Console.WriteLine(
                $"Current Login        : {currentLogin}");
            Console.WriteLine(
                $"System User          : {systemUser}");
            Console.WriteLine(
                $"Database User        : {databaseUser}");
            Console.WriteLine(
                $"Session User         : {sessionUser}");
            Console.WriteLine();
            Console.WriteLine(
                "Sensitive credential values are not displayed.");

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine("SQL Connectivity     : FAILED");
            Console.WriteLine();
            Console.WriteLine("Failure Type         : " + ex.GetType().Name);
            Console.WriteLine("Failure Message      : " + ex.Message);
            Console.WriteLine();
            Console.WriteLine(
                "Sensitive credential values are not displayed.");

            return false;
        }
    }

    /// <summary>
    /// Purpose:
    /// Returns the read-only SQL query used to identify the connected server,
    /// database, and supported SQL security identities.
    /// </summary>
    private static string GetSqlIdentityQuery()
    {
        return """
            SELECT
                CAST(SERVERPROPERTY('ServerName') AS nvarchar(128)) AS ServerName,
                DB_NAME() AS DatabaseName,
                SUSER_SNAME() AS CurrentLogin,
                SYSTEM_USER AS SystemUser,
                CURRENT_USER AS DatabaseUser,
                SESSION_USER AS SessionUser;
            """;
    }

    /// <summary>
    /// Purpose:
    /// Safely reads a text value from the current SQL result row and returns
    /// a descriptive placeholder when the value is NULL or blank.
    /// </summary>
    private static string ReadText(
        SqlDataReader reader,
        string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);

        if (reader.IsDBNull(ordinal))
        {
            return "(NULL)";
        }

        string value = Convert.ToString(reader.GetValue(ordinal))
            ?? string.Empty;

        return string.IsNullOrWhiteSpace(value)
            ? "(Blank)"
            : value;
    }
}
