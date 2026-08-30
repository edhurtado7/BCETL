using Microsoft.Data.SqlClient;

namespace BCETL.Configuration;

/// <summary>
/// Purpose:
/// Displays a safe operational summary of BCETL configuration without
/// exposing SQL passwords, Business Central client secrets, or other
/// sensitive credential values.
///
/// The service reports whether required environment variables are configured,
/// identifies the scope from which each value is available, and displays
/// nonsecret SQL connection properties.
///
/// Business Purpose:
/// Helps BCETL operators identify missing or incorrect configuration before
/// running extraction, reconciliation, or validation commands.
///
/// Safety:
///
/// - Does not display BCETL_CLIENT_SECRET.
/// - Does not display SQL passwords.
/// - Does not display the complete SQL connection string.
/// - Does not open SQL or Business Central connections.
///
/// Environment Scope Notes:
///
/// Process:
/// The value is available to the current BCETL process. The value may have
/// been inherited from User or Machine configuration or assigned directly
/// within the current PowerShell session.
///
/// User:
/// The value is stored for the current Windows user.
///
/// Machine:
/// The value is stored for the local computer.
///
/// Future Enhancement Backlog:
///
/// - Add config test for SQL connectivity verification.
/// - Report the login identity recognized by SQL Server.
/// - Add optional Business Central connectivity testing.
/// </summary>
public sealed class ConfigurationStatusService
{
    public void Run()
    {
        EnvironmentVariableStatus sqlStatus =
            GetEnvironmentVariableStatus(
                "BCETL_SQL_CONNECTION");

        EnvironmentVariableStatus secretStatus =
            GetEnvironmentVariableStatus(
                "BCETL_CLIENT_SECRET");

        Console.WriteLine(
            "============================================================");
        Console.WriteLine("BCETL Configuration Status");
        Console.WriteLine(
            "============================================================");
        Console.WriteLine();

        DisplayEnvironmentVariable(
            "BCETL_SQL_CONNECTION",
            sqlStatus);

        Console.WriteLine();

        DisplayEnvironmentVariable(
            "BCETL_CLIENT_SECRET",
            secretStatus);

        if (sqlStatus.IsConfigured &&
            !string.IsNullOrWhiteSpace(sqlStatus.Value))
        {
            DisplaySqlConfiguration(sqlStatus.Value);
        }

        Console.WriteLine();
        Console.WriteLine(
            "Sensitive credential values are not displayed.");
    }

    /// <summary>
    /// Purpose:
    /// Determines whether an environment variable is available to the
    /// current process and identifies its most specific persistent scope.
    ///
    /// Process is reported when the current process contains a value that
    /// does not match a stored User or Machine value.
    /// </summary>
    private static EnvironmentVariableStatus
        GetEnvironmentVariableStatus(
            string variableName)
    {
        string? processValue =
            Environment.GetEnvironmentVariable(
                variableName,
                EnvironmentVariableTarget.Process);

        string? userValue =
            Environment.GetEnvironmentVariable(
                variableName,
                EnvironmentVariableTarget.User);

        string? machineValue =
            Environment.GetEnvironmentVariable(
                variableName,
                EnvironmentVariableTarget.Machine);

        if (string.IsNullOrWhiteSpace(processValue))
        {
            return new EnvironmentVariableStatus(
                IsConfigured: false,
                Scope: "None",
                Value: null);
        }

        if (!string.IsNullOrWhiteSpace(userValue) &&
            string.Equals(
                processValue,
                userValue,
                StringComparison.Ordinal))
        {
            return new EnvironmentVariableStatus(
                IsConfigured: true,
                Scope: "User",
                Value: processValue);
        }

        if (!string.IsNullOrWhiteSpace(machineValue) &&
            string.Equals(
                processValue,
                machineValue,
                StringComparison.Ordinal))
        {
            return new EnvironmentVariableStatus(
                IsConfigured: true,
                Scope: "Machine",
                Value: processValue);
        }

        return new EnvironmentVariableStatus(
            IsConfigured: true,
            Scope: "Process",
            Value: processValue);
    }

    /// <summary>
    /// Purpose:
    /// Displays whether an environment variable is configured and where
    /// the current value is available without displaying the value itself.
    /// </summary>
    private static void DisplayEnvironmentVariable(
        string variableName,
        EnvironmentVariableStatus status)
    {
        Console.WriteLine(
            $"{variableName,-21}: " +
            $"{FormatConfigured(status.IsConfigured)}");

        Console.WriteLine(
            $"{"Scope",-21}: {status.Scope}");
    }

    /// <summary>
    /// Purpose:
    /// Parses and displays only nonsecret SQL connection properties.
    /// </summary>
    private static void DisplaySqlConfiguration(
        string sqlConnection)
    {
        try
        {
            var builder =
                new SqlConnectionStringBuilder(
                    sqlConnection);

            Console.WriteLine();
            Console.WriteLine("SQL Connection");
            Console.WriteLine("--------------");

            Console.WriteLine(
                $"Server               : " +
                $"{FormatValue(builder.DataSource)}");

            Console.WriteLine(
                $"Database             : " +
                $"{FormatValue(builder.InitialCatalog)}");

            Console.WriteLine(
                $"Authentication       : " +
                $"{DetermineAuthenticationMode(builder)}");

            Console.WriteLine(
                $"Configured identity  : " +
                $"{DetermineConfiguredIdentity(builder)}");

            Console.WriteLine(
                $"Encrypt              : {builder.Encrypt}");

            Console.WriteLine(
                $"Trust certificate    : " +
                $"{builder.TrustServerCertificate}");

            Console.WriteLine(
                $"Connection timeout   : " +
                $"{builder.ConnectTimeout} seconds");
        }
        catch (ArgumentException)
        {
            Console.WriteLine();
            Console.WriteLine("SQL Connection");
            Console.WriteLine("--------------");
            Console.WriteLine(
                "Connection status    : Configured but invalid");
        }
    }

    /// <summary>
    /// Purpose:
    /// Identifies the authentication method represented by the parsed
    /// SQL connection string.
    /// </summary>
    private static string DetermineAuthenticationMode(
        SqlConnectionStringBuilder builder)
    {
        if (builder.IntegratedSecurity)
        {
            return "Windows Integrated Security";
        }

        if (!string.IsNullOrWhiteSpace(builder.UserID))
        {
            return "SQL Authentication";
        }

        if (builder.Authentication !=
            SqlAuthenticationMethod.NotSpecified)
        {
            return builder.Authentication.ToString();
        }

        return "Not explicitly specified";
    }

    /// <summary>
    /// Purpose:
    /// Displays the configured SQL identity without exposing a password.
    ///
    /// For SQL Authentication, the User ID from the connection string is
    /// displayed.
    ///
    /// For Windows Integrated Security, the Windows identity running the
    /// current BCETL process is displayed.
    /// </summary>
    private static string DetermineConfiguredIdentity(
        SqlConnectionStringBuilder builder)
    {
        if (builder.IntegratedSecurity)
        {
            return
                $"{Environment.UserDomainName}\\" +
                $"{Environment.UserName}";
        }

        if (!string.IsNullOrWhiteSpace(builder.UserID))
        {
            return builder.UserID;
        }

        return "(Not specified in connection string)";
    }

    private static string FormatConfigured(
        bool isConfigured)
    {
        return isConfigured
            ? "Configured"
            : "Not configured";
    }

    private static string FormatValue(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "(Not specified)"
            : value;
    }
}

/// <summary>
/// Purpose:
/// Represents the safe configuration state of one BCETL environment
/// variable without exposing its sensitive value to console output.
/// </summary>
public sealed record EnvironmentVariableStatus(
    bool IsConfigured,
    string Scope,
    string? Value);
	