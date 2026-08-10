using Comic.Core.Models;
using Comic.Infrastructure.Library;

namespace Comic.Tests;

public sealed class JsonReadingHistoryStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "Comic.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Save_PersistsAndUpdatesOnePositionPerComic()
    {
        var historyPath = Path.Combine(_root, "reading-history.json");
        var store = new JsonReadingHistoryStore(historyPath);

        store.Save(new ReadingHistoryEntry(
            "butiange", "2", "0003.jpg", new DateTimeOffset(2026, 8, 4, 10, 0, 0, TimeSpan.Zero)));
        store.Save(new ReadingHistoryEntry(
            "butiange", "10", "0001.jpg", new DateTimeOffset(2026, 8, 4, 11, 0, 0, TimeSpan.Zero)));

        var reloaded = Assert.Single(new JsonReadingHistoryStore(historyPath).LoadAll());
        Assert.Equal("butiange", reloaded.ComicId);
        Assert.Equal("10", reloaded.ChapterId);
        Assert.Equal("0001.jpg", reloaded.PageFileName);
        Assert.False(File.Exists(historyPath + ".tmp"));
    }

    [Fact]
    public void LoadAll_IgnoresMalformedOrUnsafeEntries()
    {
        Directory.CreateDirectory(_root);
        var historyPath = Path.Combine(_root, "reading-history.json");
        File.WriteAllText(
            historyPath,
            "[{\"comicId\":\"../escape\",\"chapterId\":\"1\",\"pageFileName\":\"../page.jpg\",\"updatedAt\":\"2026-08-04T00:00:00Z\"}]");

        var entries = new JsonReadingHistoryStore(historyPath).LoadAll();

        Assert.Empty(entries);
    }

    [Fact]
    public void Save_AcceptsCanonicalNumberedChapterFolder()
    {
        var historyPath = Path.Combine(_root, "reading-history.json");
        var store = new JsonReadingHistoryStore(historyPath);

        store.Save(new ReadingHistoryEntry(
            "butiange",
            "第1回",
            "0031.webp",
            new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero)));

        var entry = Assert.Single(store.LoadAll());
        Assert.Equal("第1回", entry.ChapterId);
        Assert.Equal("0031.webp", entry.PageFileName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
