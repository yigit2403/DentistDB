using Microsoft.Data.Sqlite;
using DentistDB.Models;

namespace DentistDB.Data;

public static class DeploymentPaths
{
    public static string ResolveSqliteConnectionString(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredConnection = configuration.GetConnectionString("DefaultConnection");
        var databasePath = ResolveSqliteDatabasePath(configuredConnection, environment);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        var builder = new SqliteConnectionStringBuilder(configuredConnection)
        {
            DataSource = databasePath
        };

        return builder.ToString();
    }

    public static string ResolveScanStoragePath(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredPath = configuration.GetSection(StorageOptions.SectionName)[nameof(StorageOptions.ScanStoragePath)];
        return ResolveScanStoragePath(configuredPath, environment);
    }

    public static string ResolveDataProtectionKeysPath(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredPath = configuration.GetSection(StorageOptions.SectionName)[nameof(StorageOptions.DataProtectionKeysPath)];
        return ResolveDataProtectionKeysPath(configuredPath, environment);
    }

    public static string ResolveScanStoragePath(string? configuredPath, IHostEnvironment environment)
    {
        var fallbackPath = environment.IsDevelopment()
            ? Path.Combine(environment.ContentRootPath, "wwwroot", "uploads", "scans")
            : Path.Combine(GetDefaultDataRoot(), "scans");

        var resolvedPath = ResolvePath(configuredPath, environment, fallbackPath);
        Directory.CreateDirectory(resolvedPath);
        return resolvedPath;
    }

    public static string ResolveDataProtectionKeysPath(string? configuredPath, IHostEnvironment environment)
    {
        var fallbackPath = environment.IsDevelopment()
            ? Path.Combine(environment.ContentRootPath, ".aspnet", "keys")
            : Path.Combine(GetDefaultDataRoot(), "keys");

        var resolvedPath = ResolvePath(configuredPath, environment, fallbackPath);
        Directory.CreateDirectory(resolvedPath);
        return resolvedPath;
    }

    public static string ResolveStoredScanPath(IWebHostEnvironment environment, string scanStoragePath, string storedPath)
    {
        if (Path.IsPathRooted(storedPath))
        {
            return storedPath;
        }

        var normalizedStoredPath = storedPath
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        if (normalizedStoredPath.StartsWith($"uploads{Path.DirectorySeparatorChar}scans{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(environment.WebRootPath, normalizedStoredPath);
        }

        return Path.Combine(scanStoragePath, Path.GetFileName(normalizedStoredPath));
    }

    private static string ResolveSqliteDatabasePath(string? configuredConnection, IHostEnvironment environment)
    {
        var fallbackPath = environment.IsDevelopment()
            ? Path.Combine(environment.ContentRootPath, "DentistDB_dev.db")
            : Path.Combine(GetDefaultDataRoot(), "data", "DentistDB.db");

        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            return fallbackPath;
        }

        var builder = new SqliteConnectionStringBuilder(configuredConnection);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:")
        {
            return fallbackPath;
        }

        return ResolvePath(builder.DataSource, environment, fallbackPath);
    }

    private static string ResolvePath(string? configuredPath, IHostEnvironment environment, string fallbackPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath) ? fallbackPath : configuredPath;
        path = Environment.ExpandEnvironmentVariables(path);

        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, path));
    }

    private static string GetDefaultDataRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "DentistDB");
    }
}
