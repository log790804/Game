using Comic.Core.Models;

namespace Comic.Infrastructure.Library;

public static class ComicMetadataBackfiller
{
    public static int Backfill(
        JsonComicMetadataStore metadataStore,
        IEnumerable<DownloadHistoryEntry> downloadHistory)
    {
        ArgumentNullException.ThrowIfNull(metadataStore);
        ArgumentNullException.ThrowIfNull(downloadHistory);

        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var savedCount = 0;
        foreach (var entry in downloadHistory.OrderByDescending(item => item.CompletedAt))
        {
            try
            {
                var libraryRoot = Path.GetFullPath(entry.LibraryRoot);
                var key = $"{libraryRoot}\0{entry.ComicId}";
                if (!processed.Add(key))
                {
                    continue;
                }

                var rootBoundary = libraryRoot.EndsWith(Path.DirectorySeparatorChar)
                    ? libraryRoot
                    : libraryRoot + Path.DirectorySeparatorChar;
                var comicDirectory = Path.GetFullPath(Path.Combine(libraryRoot, entry.ComicId));
                if (!comicDirectory.StartsWith(rootBoundary, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!Directory.Exists(comicDirectory))
                {
                    continue;
                }

                var existing = metadataStore.Load(comicDirectory);
                if (existing is not null && existing.UpdatedAt >= entry.CompletedAt)
                {
                    continue;
                }

                metadataStore.Save(
                    libraryRoot,
                    entry.ComicId,
                    entry.ComicTitle,
                    entry.CompletedAt);
                savedCount++;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                // One stale or unsafe history row must not prevent other comics from being backfilled.
            }
        }

        return savedCount;
    }
}
