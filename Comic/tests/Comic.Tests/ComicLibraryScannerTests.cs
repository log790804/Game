using Comic.Infrastructure.Library;

namespace Comic.Tests;

public sealed class ComicLibraryScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "Comic.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Scan_BuildsComicChapterImageHierarchyInNaturalOrder()
    {
        WritePage("butiange", "第10回", "0002.png");
        WritePage("butiange", "第10回", "0001.jpg");
        WritePage("butiange", "第2回", "0001.webp");
        WritePage("butiange", "第2回", "notes.txt");
        Directory.CreateDirectory(Path.Combine(_root, "bad comic", "1"));

        var comics = new ComicLibraryScanner().Scan(_root);

        var comic = Assert.Single(comics);
        Assert.Equal("butiange", comic.Id);
        Assert.Equal(["第2回", "第10回"], comic.Chapters.Select(chapter => chapter.Id));
        Assert.Equal(
            ["0001.jpg", "0002.png"],
            comic.Chapters[1].Pages.Select(Path.GetFileName));
    }

    [Fact]
    public void Scan_SkipsEmptyChaptersAndUnsupportedFiles()
    {
        WritePage("butiange", "1", "readme.txt");
        WritePage("butiange", "1", "0001.avif");
        WritePage("butiange", "2", "0001.jpg");

        var comic = Assert.Single(new ComicLibraryScanner().Scan(_root));

        var chapter = Assert.Single(comic.Chapters);
        Assert.Equal("2", chapter.Id);
    }

    [Fact]
    public void Scan_ReturnsEmptyForMissingLibraryRoot()
    {
        var comics = new ComicLibraryScanner().Scan(Path.Combine(_root, "missing"));

        Assert.Empty(comics);
    }

    [Fact]
    public void Scan_HidesLegacyNumericChapterIdWhenNumberedFoldersExist()
    {
        WritePage("butiange", "1070413", "0001.png");
        WritePage("butiange", "第1回", "0001.png");
        WritePage("butiange", "第1回", "0002.png");

        var comic = Assert.Single(new ComicLibraryScanner().Scan(_root));

        var chapter = Assert.Single(comic.Chapters);
        Assert.Equal("第1回", chapter.Id);
        Assert.Equal(2, chapter.Pages.Count);
    }

    [Fact]
    public void Scan_UsesPersistedComicTitleAsDropdownDisplayName()
    {
        WritePage("butiange", "第1回", "0001.webp");
        new JsonComicMetadataStore().Save(
            _root,
            "butiange",
            "步天歌",
            new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero));

        var comic = Assert.Single(new ComicLibraryScanner().Scan(_root));

        Assert.Equal("butiange", comic.Id);
        Assert.Equal("步天歌", comic.DisplayName);
    }

    [Fact]
    public void Scan_FallsBackToFolderIdWhenMetadataIsMissing()
    {
        WritePage("butiange", "第1回", "0001.webp");

        var comic = Assert.Single(new ComicLibraryScanner().Scan(_root));

        Assert.Equal("butiange", comic.DisplayName);
    }

    private void WritePage(string comicId, string chapterId, string fileName)
    {
        var directory = Path.Combine(_root, comicId, chapterId);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), "test");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
