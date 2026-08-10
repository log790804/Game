using System.Net;
using System.Net.Http.Headers;
using Comic.Core.Abstractions;
using Comic.Core.Exceptions;
using Comic.Core.Models;
using Comic.Infrastructure.Downloads;

namespace Comic.Tests;

public sealed class SequentialDownloadServiceTests
{
    [Theory]
    [InlineData(DownloadMode.Safe, 1)]
    [InlineData(DownloadMode.Standard, 2)]
    [InlineData(DownloadMode.Fast, 3)]
    public async Task DownloadSelectedAsync_RespectsDownloadModeConcurrencyLimit(
        DownloadMode downloadMode,
        int expectedPeakConcurrency)
    {
        var chapter = Chapter("chapter-1", 1);
        var pages = Enumerable.Range(1, 6)
            .Select(number => new ComicPage(
                number,
                new Uri($"https://img.happymh.com/demo/chapter-1/{number}.webp")))
            .ToArray();
        var source = new RecordingComicSource(new Dictionary<string, IReadOnlyList<ComicPage>>
        {
            [chapter.Id] = pages
        });
        var handler = new ConcurrencyTrackingHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var service = new SequentialDownloadService(
            httpClient,
            source,
            maxImageBytes: 1_024,
            requestDelay: TimeSpan.Zero);
        var root = CreateTempDirectory();

        try
        {
            var comic = new ComicInfo(
                "demo",
                "Test comic",
                new Uri("https://m.happymh.com/manga/demo"),
                [chapter]);

            var result = await service.DownloadSelectedAsync(
                comic,
                [chapter],
                root,
                progress: null,
                CancellationToken.None,
                downloadMode);

            Assert.Equal(expectedPeakConcurrency, handler.PeakConcurrency);
            Assert.Equal(pages.Length, result.DownloadedPages);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadSelectedAsync_PropagatesVerificationFailureFromChapterSource()
    {
        var chapter = Chapter("chapter-1", 1);
        var source = new ThrowingComicSource(new SourceAccessException("verification required"));
        using var httpClient = new HttpClient(new ThrowingHttpMessageHandler());
        var service = new SequentialDownloadService(httpClient, source, maxImageBytes: 1_024);
        var root = CreateTempDirectory();

        try
        {
            var comic = new ComicInfo(
                "demo",
                "Test comic",
                new Uri("https://m.happymh.com/manga/demo"),
                [chapter]);

            await Assert.ThrowsAsync<SourceAccessException>(() => service.DownloadSelectedAsync(
                comic,
                [chapter],
                root,
                progress: null,
                CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadSelectedAsync_ConvertsForbiddenImageResponseToVerificationFailure()
    {
        var chapter = Chapter("chapter-1", 1);
        var source = new RecordingComicSource(new Dictionary<string, IReadOnlyList<ComicPage>>
        {
            [chapter.Id] =
            [
                new ComicPage(1, new Uri("https://img.happymh.com/demo/chapter-1/1.webp"))
            ]
        });
        using var httpClient = new HttpClient(
            new StatusCodeHttpMessageHandler(HttpStatusCode.Forbidden));
        var service = new SequentialDownloadService(httpClient, source, maxImageBytes: 1_024);
        var root = CreateTempDirectory();

        try
        {
            var comic = new ComicInfo(
                "demo",
                "Test comic",
                new Uri("https://m.happymh.com/manga/demo"),
                [chapter]);

            await Assert.ThrowsAsync<SourceAccessException>(() => service.DownloadSelectedAsync(
                comic,
                [chapter],
                root,
                progress: null,
                CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadSelectedAsync_ProcessesChaptersAndPagesInAscendingOrder()
    {
        var chapter2 = Chapter("chapter-2", 2);
        var chapter10 = Chapter("chapter-10", 10);
        var source = new RecordingComicSource(new Dictionary<string, IReadOnlyList<ComicPage>>
        {
            [chapter2.Id] = new[]
            {
                new ComicPage(1, new Uri("https://img.happymh.com/demo/chapter-2/1.webp")),
                new ComicPage(2, new Uri("https://img.happymh.com/demo/chapter-2/2.webp"))
            },
            [chapter10.Id] = new[]
            {
                new ComicPage(1, new Uri("https://img.happymh.com/demo/chapter-10/1.webp"))
            }
        });
        using var httpClient = new HttpClient(new ImageHttpMessageHandler());
        var service = new SequentialDownloadService(httpClient, source, maxImageBytes: 1_024);
        var root = CreateTempDirectory();

        try
        {
            var comic = new ComicInfo(
                "demo",
                "測試漫畫",
                new Uri("https://m.happymh.com/manga/demo"),
                new[] { chapter10, chapter2 });

            var result = await service.DownloadSelectedAsync(
                comic,
                new[] { chapter10, chapter2 },
                root,
                progress: null,
                CancellationToken.None);

            Assert.Equal(new[] { "chapter-2", "chapter-10" }, source.RequestedChapterIds);
            Assert.Equal(2, result.CompletedChapters);
            Assert.Equal(3, result.DownloadedPages);
            Assert.Empty(result.Errors);
            Assert.Collection(
                result.Chapters,
                chapter =>
                {
                    Assert.Equal("chapter-2", chapter.ChapterId);
                    Assert.Equal(2, chapter.PageCount);
                },
                chapter =>
                {
                    Assert.Equal("chapter-10", chapter.ChapterId);
                    Assert.Equal(1, chapter.PageCount);
                });
            Assert.True(File.Exists(Path.Combine(root, "demo", "第2回", "0001.webp")));
            Assert.True(File.Exists(Path.Combine(root, "demo", "第2回", "0002.webp")));
            Assert.True(File.Exists(Path.Combine(root, "demo", "第10回", "0001.webp")));
            var metadata = new Comic.Infrastructure.Library.JsonComicMetadataStore()
                .Load(Path.Combine(root, "demo"));
            Assert.NotNull(metadata);
            Assert.Equal("測試漫畫", metadata.Title);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadSelectedAsync_RejectsNonImageResponseWithoutLeavingPartialFile()
    {
        var chapter = Chapter("chapter-1", 1);
        var source = new RecordingComicSource(new Dictionary<string, IReadOnlyList<ComicPage>>
        {
            [chapter.Id] = new[]
            {
                new ComicPage(1, new Uri("https://img.happymh.com/demo/chapter-1/1.webp"))
            }
        });
        using var httpClient = new HttpClient(new ImageHttpMessageHandler("text/html"));
        var service = new SequentialDownloadService(httpClient, source, maxImageBytes: 1_024);
        var root = CreateTempDirectory();

        try
        {
            var comic = new ComicInfo(
                "demo",
                "測試漫畫",
                new Uri("https://m.happymh.com/manga/demo"),
                new[] { chapter });

            var result = await service.DownloadSelectedAsync(
                comic,
                new[] { chapter },
                root,
                progress: null,
                CancellationToken.None);

            Assert.Empty(Directory.EnumerateFiles(root, "*.partial", SearchOption.AllDirectories));
            Assert.Single(result.Errors);
            Assert.Equal(0, result.DownloadedPages);
            Assert.Empty(result.Chapters);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadSelectedAsync_SendsChapterPageAsImageReferrer()
    {
        var chapter = Chapter("chapter-1", 1);
        var source = new RecordingComicSource(new Dictionary<string, IReadOnlyList<ComicPage>>
        {
            [chapter.Id] =
            [
                new ComicPage(1, new Uri("https://img.happymh.com/demo/chapter-1/1.webp"))
            ]
        });
        var handler = new RecordingImageHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var service = new SequentialDownloadService(httpClient, source, maxImageBytes: 1_024);
        var root = CreateTempDirectory();

        try
        {
            var comic = new ComicInfo(
                "demo",
                "測試漫畫",
                new Uri("https://m.happymh.com/manga/demo"),
                [chapter]);

            var result = await service.DownloadSelectedAsync(
                comic,
                [chapter],
                root,
                progress: null,
                CancellationToken.None);

            Assert.Empty(result.Errors);
            Assert.Equal(chapter.SourceUri, handler.Referrer);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadSelectedAsync_UsesVerifiedPageLoaderWhenProvided()
    {
        var chapter = Chapter("chapter-1", 1);
        var page = new ComicPage(1, new Uri("https://img.happymh.com/demo/chapter-1/1.webp"));
        var source = new RecordingComicSource(new Dictionary<string, IReadOnlyList<ComicPage>>
        {
            [chapter.Id] = [page]
        });
        using var httpClient = new HttpClient(new ThrowingHttpMessageHandler());
        var pageLoader = new FakeComicPageLoader();
        var service = new SequentialDownloadService(
            httpClient,
            source,
            maxImageBytes: 1_024,
            pageLoader: pageLoader);
        var root = CreateTempDirectory();

        try
        {
            var comic = new ComicInfo(
                "demo",
                "測試漫畫",
                new Uri("https://m.happymh.com/manga/demo"),
                [chapter]);

            var result = await service.DownloadSelectedAsync(
                comic,
                [chapter],
                root,
                progress: null,
                CancellationToken.None);

            Assert.Empty(result.Errors);
            Assert.Equal(page.SourceUri, Assert.Single(pageLoader.RequestedPages));
            Assert.True(File.Exists(Path.Combine(root, "demo", "第1回", "0001.webp")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadSelectedAsync_StandardModeUsesConcurrentHttpWhenPageLoaderExists()
    {
        var chapter = Chapter("chapter-1", 1);
        var pages = Enumerable.Range(1, 4)
            .Select(number => new ComicPage(
                number,
                new Uri($"https://img.happymh.com/demo/chapter-1/{number}.webp")))
            .ToArray();
        var source = new RecordingComicSource(new Dictionary<string, IReadOnlyList<ComicPage>>
        {
            [chapter.Id] = pages
        });
        var handler = new ConcurrencyTrackingHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var pageLoader = new FakeComicPageLoader();
        var service = new SequentialDownloadService(
            httpClient,
            source,
            maxImageBytes: 1_024,
            requestDelay: TimeSpan.Zero,
            pageLoader: pageLoader);
        var root = CreateTempDirectory();

        try
        {
            var comic = new ComicInfo(
                "demo",
                "Test comic",
                new Uri("https://m.happymh.com/manga/demo"),
                [chapter]);

            var result = await service.DownloadSelectedAsync(
                comic,
                [chapter],
                root,
                progress: null,
                CancellationToken.None,
                DownloadMode.Standard);

            Assert.Equal(2, handler.PeakConcurrency);
            Assert.Empty(pageLoader.RequestedPages);
            Assert.Equal(pages.Length, result.DownloadedPages);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ChapterInfo Chapter(string id, int sequence) => new(
        id,
        $"第 {sequence} 話",
        new Uri($"https://m.happymh.com/mangaread/demo/{id}"),
        sequence);

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "Comic.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingComicSource(
        IReadOnlyDictionary<string, IReadOnlyList<ComicPage>> pagesByChapter) : IComicSourceClient
    {
        public List<string> RequestedChapterIds { get; } = [];

        public Task<ComicInfo> LoadComicAsync(Uri sourceUri, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ComicPage>> LoadChapterPagesAsync(
            ChapterInfo chapter,
            CancellationToken cancellationToken)
        {
            RequestedChapterIds.Add(chapter.Id);
            return Task.FromResult(pagesByChapter[chapter.Id]);
        }
    }

    private sealed class ImageHttpMessageHandler(string mediaType = "image/webp") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var content = new ByteArrayContent([1, 2, 3, 4]);
            content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class ThrowingComicSource(Exception exception) : IComicSourceClient
    {
        public Task<ComicInfo> LoadComicAsync(Uri sourceUri, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ComicPage>> LoadChapterPagesAsync(
            ChapterInfo chapter,
            CancellationToken cancellationToken) =>
            Task.FromException<IReadOnlyList<ComicPage>>(exception);
    }

    private sealed class StatusCodeHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }

    private sealed class ConcurrencyTrackingHttpMessageHandler : HttpMessageHandler
    {
        private readonly object _sync = new();
        private int _activeRequests;

        public int PeakConcurrency { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                _activeRequests++;
                PeakConcurrency = Math.Max(PeakConcurrency, _activeRequests);
            }

            try
            {
                await Task.Delay(75, cancellationToken);
                var content = new ByteArrayContent([1, 2, 3, 4]);
                content.Headers.ContentType = new MediaTypeHeaderValue("image/webp");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            }
            finally
            {
                lock (_sync)
                {
                    _activeRequests--;
                }
            }
        }
    }

    private sealed class RecordingImageHttpMessageHandler : HttpMessageHandler
    {
        public Uri? Referrer { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Referrer = request.Headers.Referrer;
            var content = new ByteArrayContent([1, 2, 3, 4]);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/webp");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The direct HTTP handler must not be used.");
    }

    private sealed class FakeComicPageLoader : IComicPageLoader
    {
        public List<Uri> RequestedPages { get; } = [];

        public Task<ComicPageContent> LoadAsync(
            ChapterInfo chapter,
            ComicPage page,
            long maxBytes,
            CancellationToken cancellationToken)
        {
            RequestedPages.Add(page.SourceUri);
            return Task.FromResult(new ComicPageContent("image/webp", [1, 2, 3, 4]));
        }
    }
}
