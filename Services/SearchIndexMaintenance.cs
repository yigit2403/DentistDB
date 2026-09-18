using DentistDB.Data;
using Microsoft.EntityFrameworkCore;

namespace DentistDB.Services;

/// <summary>
/// Fills search-index columns for rows written before the column existed. Runs at
/// startup after migrations; cheap because it only touches rows with an empty index.
/// </summary>
public static class SearchIndexMaintenance
{
    public static async Task BackfillAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        var operations = await db.PreviousOperations
            .Where(o => o.SearchIndex == "")
            .ToListAsync(cancellationToken);
        if (operations.Count == 0) return;

        foreach (var operation in operations)
        {
            operation.SearchIndex = SearchNormalizer.BuildOperationIndex(operation);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
