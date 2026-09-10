using DentistDB.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DentistDB.Services;

/// <summary>Creates a consistent copy of the live SQLite database using VACUUM INTO.</summary>
public sealed class BackupService
{
    private readonly ApplicationDbContext _db;

    public BackupService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>Writes a backup file and returns its path. The caller is responsible for deleting it.</summary>
    public async Task<string> CreateBackupFileAsync(CancellationToken ct = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"DentistDB-backup-{Guid.NewGuid():N}.db");

        var connection = (SqliteConnection)_db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "VACUUM INTO $path;";
            command.Parameters.AddWithValue("$path", tempPath);
            await command.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return tempPath;
    }

    public static string SuggestedFileName(string clinicName)
    {
        var safeName = string.Concat(clinicName.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "DentistDB";
        }

        return $"{safeName}-yedek-{DateTime.Now:yyyyMMdd-HHmm}.db";
    }
}
