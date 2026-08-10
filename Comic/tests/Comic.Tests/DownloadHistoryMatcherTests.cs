using Comic.Core.Models;
using Comic.Infrastructure.Downloads;

namespace Comic.Tests;

public sealed class DownloadHistoryMatcherTests
{
    [Fact]
    public void GetCompletedChapterIds_OnlyReturnsHistoryBackedByAllExpectedFiles()
    {
        var libraryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ComicLibrary"));
        var history = new[]
        {
            new DownloadHistoryEntry(
                libraryRoot, "butiange", "步天歌", "chapter-1", "第一回",
                "第1回", 31, DateTimeOffset.UtcNow),
            new DownloadHistoryEntry(
                libraryRoot, "butiange", "步天歌", "chapter-2", "第二回",
                "第2回", 30, DateTimeOffset.UtcNow)
        };
        var localPageCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["第1回"] = 31,
            ["第2回"] = 12
        };

        var result = DownloadHistoryMatcher.GetCompletedChapterIds(
            libraryRoot,
            "butiange",
            history,
            localPageCounts);

        Assert.Equal(["chapter-1"], result);
    }

    [Fact]
    public void GetCompletedChapterIds_DoesNotMatchAnotherLibrary()
    {
        var libraryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ComicLibrary"));
        var history = new[]
        {
            new DownloadHistoryEntry(
                Path.Combine(libraryRoot, "other"), "butiange", "步天歌", "chapter-1", "第一回",
                "第1回", 31, DateTimeOffset.UtcNow)
        };

        var result = DownloadHistoryMatcher.GetCompletedChapterIds(
            libraryRoot,
            "butiange",
            history,
            new Dictionary<string, int> { ["第1回"] = 31 });

        Assert.Empty(result);
    }
}
