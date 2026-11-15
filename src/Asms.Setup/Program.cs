using System.Data.Common;
using System.Text;
using System.Text.Json;
using Asms.Setup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

internal static class Program
{
    private const string UserSecretsId = "asms-setup-tool";

    private static async Task<int> Main()
    {
        try
        {
            var provider = ReadSetting("ASMS_SETUP_PROVIDER");
            if (string.IsNullOrWhiteSpace(provider))
            {
                return WriteError("SETUP_PROVIDER_REQUIRED");
            }

            var connectionString = ReadSetting("ASMS_SETUP_CONNECTION");
            if (string.IsNullOrWhiteSpace(connectionString) && provider == "Sqlite")
            {
                connectionString = CreateDevelopmentSqliteConnectionString();
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return WriteError("SETUP_CONNECTION_REQUIRED");
            }

            var schoolName = ReadLine("School name: ");
            var schoolCode = ReadLine("School code: ");
            var province = ReadOptionalLine("Province (optional): ");
            var district = ReadOptionalLine("District (optional): ");
            var defaultLanguage = ReadLine("Default language (en, ps, fa-AF): ");
            var ownerFullName = ReadLine("School owner full name: ");
            var ownerUserName = ReadLine("School owner user name: ");
            var ownerEmail = ReadOptionalLine("School owner email (optional): ");
            var ownerPhone = ReadOptionalLine("School owner phone (optional): ");
            var password = ReadPassword(out var passwordConfirmationMatches);
            if (!passwordConfirmationMatches)
            {
                return WriteError("SETUP_PASSWORD_MISMATCH");
            }

            var request = new SetupRequest
            {
                Provider = provider,
                ConnectionString = connectionString,
                SchoolName = schoolName,
                SchoolCode = schoolCode,
                Province = province,
                District = district,
                DefaultLanguage = defaultLanguage,
                OwnerFullName = ownerFullName,
                OwnerUserName = ownerUserName,
                OwnerEmail = ownerEmail,
                OwnerPhone = ownerPhone,
                OwnerPassword = password
            };

            var result = await new SetupService().RunAsync(request);
            SetupConsoleOutput.WriteResult(Console.Out, request.SchoolCode, request.OwnerUserName, result);
            return result.Succeeded ? 0 : 1;
        }
        catch (Exception exception) when (IsSafeToReport(exception))
        {
            return WriteError("SETUP_CONFIGURATION_ERROR");
        }
        catch (Exception)
        {
            return WriteError("SETUP_FAILED");
        }
    }

    private static string ReadPassword(out bool confirmationMatches)
    {
        var configuredPassword = ReadSetting("ASMS_SETUP_OWNER_PASSWORD");
        if (configuredPassword is not null)
        {
            confirmationMatches = true;
            return configuredPassword;
        }

        var password = ReadHidden("Owner password: ");
        var confirmation = ReadHidden("Confirm owner password: ");
        confirmationMatches = string.Equals(password, confirmation, StringComparison.Ordinal);
        return password;
    }

    private static string ReadHidden(string prompt)
    {
        Console.Write(prompt);
        var value = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return value.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (value.Length > 0)
                {
                    value.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                value.Append(key.KeyChar);
            }
        }
    }

    private static string CreateDevelopmentSqliteConnectionString()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException();
        }

        var directory = Path.Combine(localAppData, "ASMS", "dev");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "asms-dev.db");
        return new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    }

    private static string? ReadSetting(string name)
    {
        var environmentValue = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return environmentValue;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            return null;
        }

        var secretsPath = Path.Combine(
            appData,
            "Microsoft",
            "UserSecrets",
            UserSecretsId,
            "secrets.json");
        if (!File.Exists(secretsPath))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(secretsPath));
        return document.RootElement.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private static string ReadLine(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine() ?? string.Empty;
    }

    private static string? ReadOptionalLine(string prompt)
    {
        var value = ReadLine(prompt);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int WriteError(string errorCode)
    {
        Console.WriteLine(errorCode);
        return 1;
    }

    private static bool IsSafeToReport(Exception exception) =>
        exception is DbException or
            DbUpdateException or
            InvalidOperationException or
            ArgumentException or
            IOException or
            UnauthorizedAccessException or
            JsonException;
}
