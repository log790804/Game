using Comic.Core.Models;
using Comic.Infrastructure.Library;

namespace Comic.Tests;

public sealed class ComicMetadataBackfillerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "Comic.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Backfill_CreatesAssociationForExistingComicFromDownloadHistory()
    {
        var chapterDirectory = Path.Combine(_root, "butiange", "第1回");
        Directory.CreateDirectory(chapterDirectory);
        File.WriteAllText(Path.Combine(chapterDirectory, "0001.webp"), "test");
        var history = new[]
        {
            new DownloadHistoryEntry(
                _root,
                "butiange",
                "步天歌",
                "chapter-1",
                "第一回",
                "第1回",
                1,
                new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero))
        };

        var count = ComicMetadataBackfiller.Backfill(
            new JsonComicMetadataStore(),
            history);

        Assert.Equal(1, count);
        var comic = Assert.Single(new ComicLibraryScanner().Scan(_root));
        Assert.Equal("步天歌", comic.DisplayName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
