using Comic.Infrastructure.HappyMh;

namespace Comic.Tests;

public sealed class HappyMhHtmlParserTests
{
    private const string MangaHtml = """
        <!doctype html>
        <html>
          <head><title>測試漫畫 - 嗨皮漫畫</title></head>
          <body>
            <h1>測試漫畫</h1>
            <a href="/mangaread/demo/chapter-10">第 10 話</a>
            <a href="/mangaread/demo/chapter-2">第 2 話</a>
            <a href="/mangaread/demo/chapter-2">第 2 話</a>
          </body>
        </html>
        """;

    [Fact]
    public void ParseManga_ExtractsTitleAndNaturallyOrderedUniqueChapters()
    {
        var parser = new HappyMhHtmlParser();

        var result = parser.ParseManga(
            MangaHtml,
            new Uri("https://m.happymh.com/manga/demo"));

        Assert.Equal("demo", result.Id);
        Assert.Equal("測試漫畫", result.Title);
        Assert.Collection(
            result.Chapters,
            chapter => Assert.Equal("chapter-2", chapter.Id),
            chapter => Assert.Equal("chapter-10", chapter.Id));
    }

    [Fact]
    public void ParseManga_ThrowsDiagnosticErrorWhenNoChaptersExist()
    {
        var parser = new HappyMhHtmlParser();

        var exception = Assert.Throws<InvalidDataException>(() => parser.ParseManga(
            "<html><body><h1>沒有章節</h1></body></html>",
            new Uri("https://m.happymh.com/manga/empty")));

        Assert.Contains("章節", exception.Message);
    }

    [Fact]
    public void ParseMangaSnapshot_LoadsVerifiedDynamicChapterListWithoutFullPageHtml()
    {
        const string snapshotJson = """
            {
              "title": "動態漫畫",
              "chapters": [
                { "title": "第 10 話", "href": "/mangaread/demo/chapter-10" },
                { "title": "第 2 話", "href": "https://m.happymh.com/mangaread/demo/chapter-2" },
                { "title": "外站", "href": "https://example.com/mangaread/demo/evil" }
              ]
            }
            """;
        var parser = new HappyMhHtmlParser();

        var result = parser.ParseMangaSnapshot(
            snapshotJson,
            new Uri("https://m.happymh.com/manga/demo"));

        Assert.Equal("動態漫畫", result.Title);
        Assert.Collection(
            result.Chapters,
            chapter => Assert.Equal("chapter-2", chapter.Id),
            chapter => Assert.Equal("chapter-10", chapter.Id));
    }

    [Fact]
    public void ParseMangaSnapshot_ReportsWhenDynamicChaptersAreNotReady()
    {
        var parser = new HappyMhHtmlParser();

        var exception = Assert.Throws<InvalidDataException>(() => parser.ParseMangaSnapshot(
            "{\"title\":\"仍在載入\",\"chapters\":[]}",
            new Uri("https://m.happymh.com/manga/demo")));

        Assert.Contains("等待", exception.Message);
    }

    [Fact]
    public void ParseMangaSnapshot_AcceptsCurrentMangarcardChapterRoute()
    {
        const string snapshotJson = """
            {
              "title": "步天歌",
              "chapters": [
                { "title": "第155话", "href": "/mangarcard/butiangc/6383932" }
              ]
            }
            """;
        var parser = new HappyMhHtmlParser();

        var result = parser.ParseMangaSnapshot(
            snapshotJson,
            new Uri("https://m.happymh.com/manga/butiangc"));

        var chapter = Assert.Single(result.Chapters);
        Assert.Equal("6383932", chapter.Id);
        Assert.Equal("https://m.happymh.com/mangarcard/butiangc/6383932", chapter.SourceUri.AbsoluteUri);
    }

    [Fact]
    public void ParseChapterPages_UsesLazyImageUrlsAndPreservesPageOrder()
    {
        const string html = """
            <html><body>
              <mip-img data-src="https://img.happymh.com/demo/002.webp"></mip-img>
              <img src="https://img.happymh.com/demo/003.jpg">
              <img src="data:image/png;base64,AAAA">
            </body></html>
            """;
        var parser = new HappyMhHtmlParser();

        var result = parser.ParseChapterPages(
            html,
            new Uri("https://m.happymh.com/mangaread/demo/chapter-2"));

        Assert.Collection(
            result,
            page => Assert.Equal("https://img.happymh.com/demo/002.webp", page.SourceUri.AbsoluteUri),
            page => Assert.Equal("https://img.happymh.com/demo/003.jpg", page.SourceUri.AbsoluteUri));
    }

    [Fact]
    public void ParseChapterScanSnapshot_ReturnsEveryNormalPageInApiOrder()
    {
        const string snapshotJson = """
            [
              { "url": "https://img.happymh.com/demo/001.avif?q=70", "width": 800, "height": 1200, "n": 0 },
              { "url": "https://img.happymh.com/demo/ad.avif", "width": 800, "height": 1200, "n": 1 },
              { "url": "https://img.happymh.com/demo/002.avif?q=70", "width": 800, "height": 1200, "n": 0 },
              { "url": "https://img.happymh.com/demo/003.avif?q=70", "width": 800, "height": 1200, "n": 0 }
            ]
            """;

        var result = new HappyMhHtmlParser().ParseChapterScanSnapshot(
            snapshotJson,
            new Uri("https://m.happymh.com/mangarcard/demo/123"));

        Assert.Equal(3, result.Count);
        Assert.Equal(
            ["001.avif", "002.avif", "003.avif"],
            result.Select(page => Path.GetFileName(page.SourceUri.AbsolutePath)));
        Assert.Equal([1, 2, 3], result.Select(page => page.Number));
    }

    [Fact]
    public void ParseMangaSnapshot_DoesNotSortAnnouncementByDateNumber()
    {
        const string snapshotJson = """
            {
              "title": "動態漫畫",
              "chapters": [
                { "title": "延更公告 1月1日見", "href": "/mangarcard/demo/notice" },
                { "title": "2 第二回", "href": "/mangarcard/demo/2" },
                { "title": "1 第一回", "href": "/mangarcard/demo/1" }
              ]
            }
            """;

        var result = new HappyMhHtmlParser().ParseMangaSnapshot(
            snapshotJson,
            new Uri("https://m.happymh.com/manga/demo"));

        Assert.Equal(["1", "2", "notice"], result.Chapters.Select(chapter => chapter.Id));
    }
}
