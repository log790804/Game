using Comic.Core.Security;

namespace Comic.Tests;

public sealed class SourceUrlPolicyTests
{
    [Fact]
    public void CreateHappyMhMangaUriFromComicId_MapsOfflineFolderIdToMangaPage()
    {
        var result = SourceUrlPolicy.CreateHappyMhMangaUriFromComicId("butiange_2-demo");

        Assert.Equal(
            "https://m.happymh.com/manga/butiange_2-demo",
            result.AbsoluteUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../admin")]
    [InlineData("a/b")]
    [InlineData("https://example.com/manga/demo")]
    [InlineData("漫畫")]
    public void CreateHappyMhMangaUriFromComicId_RejectsUnsafeFolderIds(string comicId)
    {
        Assert.Throws<ArgumentException>(() =>
            SourceUrlPolicy.CreateHappyMhMangaUriFromComicId(comicId));
    }

    [Fact]
    public void CreateHappyMhMangaUriFromComicId_RejectsOverlongFolderId()
    {
        Assert.Throws<ArgumentException>(() =>
            SourceUrlPolicy.CreateHappyMhMangaUriFromComicId(new string('a', 121)));
    }

    [Fact]
    public void ParseHappyMhMangaUrl_AcceptsExpectedHttpsMangaUrl()
    {
        var result = SourceUrlPolicy.ParseHappyMhMangaUrl(" https://m.happymh.com/manga/butiange ");

        Assert.Equal("https://m.happymh.com/manga/butiange", result.AbsoluteUri);
    }

    [Fact]
    public void EnsureAllowedHappyMhPage_AcceptsCurrentMangarcardChapterRoute()
    {
        var exception = Record.Exception(() => SourceUrlPolicy.EnsureAllowedHappyMhPage(
            new Uri("https://m.happymh.com/mangarcard/butiangc/1070416")));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("http://m.happymh.com/manga/butiange")]
    [InlineData("https://example.com/manga/butiange")]
    [InlineData("https://m.happymh.com:444/manga/butiange")]
    [InlineData("https://user:pass@m.happymh.com/manga/butiange")]
    [InlineData("https://m.happymh.com/latest")]
    public void ParseHappyMhMangaUrl_RejectsUnsafeOrUnsupportedUrls(string value)
    {
        Assert.Throws<ArgumentException>(() => SourceUrlPolicy.ParseHappyMhMangaUrl(value));
    }
}
