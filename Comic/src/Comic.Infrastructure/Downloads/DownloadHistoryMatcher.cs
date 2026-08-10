using Comic.Core.Models;

namespace Comic.Infrastructure.Downloads;

public static class DownloadHistoryMatcher
{
    public static IReadOnlyList<string> GetCompletedChapterIds(
        string libraryRoot,
        string comicId,
        IEnumerable<DownloadHistoryEntry> history,
        IReadOnlyDictionary<string, int> localPageCounts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(comicId);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(localPageCounts);

        var normalizedRoot = Path.GetFullPath(libraryRoot);
        return history
            .Where(entry =>
                Path.GetFullPath(entry.LibraryRoot).Equals(
                    normalizedRoot,
                    StringComparison.OrdinalIgnoreCase) &&
                entry.ComicId.Equals(comicId, StringComparison.OrdinalIgnoreCase) &&
                localPageCounts.TryGetValue(entry.ChapterFolderName, out var localPageCount) &&
                localPageCount >= entry.PageCount)
            .OrderBy(entry => entry.CompletedAt)
            .Select(entry => entry.ChapterId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
