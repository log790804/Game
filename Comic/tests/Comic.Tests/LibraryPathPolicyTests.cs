using Comic.Core.Storage;
using Comic.Core.Models;

namespace Comic.Tests;

public sealed class LibraryPathPolicyTests
{
    [Fact]
    public void GetPagePath_BuildsStablePathInsideLibraryRoot()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "comic-tests"));

        var result = LibraryPathPolicy.GetPagePath(root, "butiange", "chapter-2", 3, ".webp");

        Assert.Equal(
            Path.Combine(root, "butiange", "chapter-2", "0003.webp"),
            result);
    }

    [Fact]
    public void GetPagePath_UsesHumanReadableChapterNumberFolder()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "comic-tests"));
        var chapter = new ChapterInfo(
            "1070413",
            "1 第一回",
            new Uri("https://m.happymh.com/mangarcard/butiange/1070413"),
            1);

        var result = LibraryPathPolicy.GetPagePath(root, "butiange", chapter, 3, ".png");

        Assert.Equal(Path.Combine(root, "butiange", "第1回", "0003.png"), result);
    }

    [Fact]
    public void DefaultLibraryRoot_MatchesDesktopProjectFolder()
    {
        Assert.Equal(@"D:\Game\Game\Comic\Library", LibraryPathPolicy.DefaultLibraryRoot);
    }

    [Fact]
    public void CreateUniqueChapterFolders_KeepsNewestNumericIdForDuplicateChapterNumber()
    {
        var chapters = new[]
        {
            new ChapterInfo("100", "108 第一版", new Uri("https://m.happymh.com/mangarcard/demo/100"), 108),
            new ChapterInfo("200", "108 更新版", new Uri("https://m.happymh.com/mangarcard/demo/200"), 108),
            new ChapterInfo("300", "109 下一回", new Uri("https://m.happymh.com/mangarcard/demo/300"), 109)
        };

        var result = LibraryPathPolicy.CreateUniqueChapterFolders(chapters);

        Assert.Equal(["200", "300"], result.Select(chapter => chapter.Id));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("chapter/2")]
    [InlineData("chapter:2")]
    public void GetPagePath_RejectsUnsafeIdentifiers(string chapterId)
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "comic-tests"));

        Assert.Throws<ArgumentException>(() =>
            LibraryPathPolicy.GetPagePath(root, "butiange", chapterId, 1, ".jpg"));
    }

    [Fact]
    public void GetPagePath_RejectsUnsupportedExtension()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "comic-tests"));

        Assert.Throws<ArgumentException>(() =>
            LibraryPathPolicy.GetPagePath(root, "butiange", "chapter-1", 1, ".exe"));
    }
}
