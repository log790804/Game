using System.Net;
using System.Text;
using Comic.Core.Exceptions;
using Comic.Infrastructure.HappyMh;

namespace Comic.Tests;

public sealed class HappyMhSourceClientTests
{
    private const string MangaHtml = """
        <html><body>
          <h1>測試漫畫</h1>
          <a href="/mangaread/demo/chapter-1">第 1 話</a>
        </body></html>
        """;

    [Fact]
    public async Task LoadComicAsync_ParsesSuccessfulResponse()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(MangaHtml, Encoding.UTF8, "text/html")
        }));
        var client = new HappyMhSourceClient(httpClient, new HappyMhHtmlParser());

        var result = await client.LoadComicAsync(
            new Uri("https://m.happymh.com/manga/demo"),
            CancellationToken.None);

        Assert.Equal("測試漫畫", result.Title);
        Assert.Single(result.Chapters);
    }

    [Fact]
    public async Task LoadComicAsync_ReportsAccessBlockForForbiddenResponse()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Forbidden)));
        var client = new HappyMhSourceClient(httpClient, new HappyMhHtmlParser());

        var exception = await Assert.ThrowsAsync<SourceAccessException>(() => client.LoadComicAsync(
            new Uri("https://m.happymh.com/manga/demo"),
            CancellationToken.None));

        Assert.Contains("403", exception.Message);
    }

    [Fact]
    public async Task LoadChapterPagesAsync_SendsMangaPageAsReferrer()
    {
        HttpRequestMessage? capturedRequest = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "<html><body><img src=\"https://img.happymh.com/demo/001.jpg\"></body></html>",
                    Encoding.UTF8,
                    "text/html")
            };
        }));
        var client = new HappyMhSourceClient(httpClient, new HappyMhHtmlParser());
        var chapter = new Comic.Core.Models.ChapterInfo(
            "1070416",
            "1 第一回",
            new Uri("https://m.happymh.com/mangarcard/butiangc/1070416"),
            1);

        await client.LoadChapterPagesAsync(chapter, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(
            "https://m.happymh.com/manga/butiangc",
            capturedRequest.Headers.Referrer?.AbsoluteUri);
    }

    [Fact]
    public async Task LoadComicAsync_RejectsOversizedHtmlBeforeReadingBody()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("small")
            };
            response.Content.Headers.ContentLength = 10_001;
            return response;
        }));
        var client = new HappyMhSourceClient(httpClient, new HappyMhHtmlParser(), maxHtmlBytes: 10_000);

        await Assert.ThrowsAsync<InvalidDataException>(() => client.LoadComicAsync(
            new Uri("https://m.happymh.com/manga/demo"),
            CancellationToken.None));
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(responseFactory(request));
    }
}
