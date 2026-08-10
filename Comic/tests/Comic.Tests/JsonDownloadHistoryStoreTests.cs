using Comic.Core.Models;
using Comic.Infrastructure.Downloads;

namespace Comic.Tests;

public sealed class JsonDownloadHistoryStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "Comic.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Save_PersistsAndUpdatesOneEntryPerLibraryComicAndChapter()
    {
        var historyPath = Path.Combine(_root, "download-history.json");
        var store = new JsonDownloadHistoryStore(historyPath);
        var libraryRoot = Path.Combine(_root, "Library");

        store.Save(new DownloadHistoryEntry(
            libraryRoot, "butiange", "步天歌", "1070413", "1 第一回",
            "第1回", 30, new DateTimeOffset(2026, 8, 4, 10, 0, 0, TimeSpan.Zero)));
        store.Save(new DownloadHistoryEntry(
            libraryRoot, "butiange", "步天歌", "1070413", "1 第一回",
            "第1回", 31, new DateTimeOffset(2026, 8, 4, 11, 0, 0, TimeSpan.Zero)));

        var entry = Assert.Single(new JsonDownloadHistoryStore(historyPath).LoadAll());
        Assert.Equal("第1回", entry.ChapterFolderName);
        Assert.Equal(31, entry.PageCount);
        Assert.Equal(new DateTimeOffset(2026, 8, 4, 11, 0, 0, TimeSpan.Zero), entry.CompletedAt);
        Assert.False(File.Exists(historyPath + ".tmp"));
    }

    [Fact]
    public void Save_RejectsUnsafeChapterFolderName()
    {
        var store = new JsonDownloadHistoryStore(Path.Combine(_root, "download-history.json"));

        Assert.Throws<ArgumentException>(() => store.Save(new DownloadHistoryEntry(
            Path.Combine(_root, "Library"), "butiange", "步天歌", "1070413", "1 第一回",
            "..\\outside", 31, DateTimeOffset.UtcNow)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
