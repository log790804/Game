using Comic.Core.Abstractions;
using Comic.Core.Exceptions;
using Comic.Core.Models;
using Comic.Core.Security;
using Comic.Core.Selection;
using Comic.Core.Storage;
using Comic.Infrastructure.Library;

namespace Comic.Infrastructure.Downloads;

public sealed class SequentialDownloadService : ISequentialDownloadService
{
    private const long DefaultMaxImageBytes = 30 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> ExtensionByMediaType =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
            ["image/gif"] = ".gif",
            ["image/avif"] = ".avif"
        };

    private readonly HttpClient _httpClient;
    private readonly IComicSourceClient _sourceClient;
    private readonly long _maxImageBytes;
    private readonly TimeSpan _requestDelay;
    private readonly IComicPageLoader? _pageLoader;
    private readonly JsonComicMetadataStore _metadataStore;

    public SequentialDownloadService(
        HttpClient httpClient,
        IComicSourceClient sourceClient,
        long maxImageBytes = DefaultMaxImageBytes,
        TimeSpan? requestDelay = null,
        IComicPageLoader? pageLoader = null,
        JsonComicMetadataStore? metadataStore = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(sourceClient);

        if (maxImageBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxImageBytes));
        }

        _httpClient = httpClient;
        _sourceClient = sourceClient;
        _maxImageBytes = maxImageBytes;
        _requestDelay = requestDelay ?? TimeSpan.Zero;
        _pageLoader = pageLoader;
        _metadataStore = metadataStore ?? new JsonComicMetadataStore();
    }

    public async Task<DownloadSummary> DownloadSelectedAsync(
        ComicInfo comic,
        IEnumerable<ChapterInfo> selectedChapters,
        string libraryRoot,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken,
        DownloadMode downloadMode = DownloadMode.Safe)
    {
        ArgumentNullException.ThrowIfNull(comic);
        ArgumentNullException.ThrowIfNull(selectedChapters);
        if (!Enum.IsDefined(downloadMode))
        {
            throw new ArgumentOutOfRangeException(nameof(downloadMode));
        }

        var chapters = LibraryPathPolicy.CreateUniqueChapterFolders(
            OrderedChapterSelection.Create(selectedChapters));
        var errors = new List<string>();
        var completedChapters = 0;
        var downloadedPages = 0;
        var skippedPages = 0;
        var chapterResults = new List<DownloadChapterResult>();

        try
        {
            _metadataStore.Save(
                libraryRoot,
                comic.Id,
                comic.Title,
                DateTimeOffset.UtcNow);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            errors.Add("漫畫名稱關聯檔無法保存。");
        }

        for (var chapterIndex = 0; chapterIndex < chapters.Count; chapterIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chapter = chapters[chapterIndex];

            progress?.Report(CreateProgress(
                chapter,
                chapterIndex,
                chapters.Count,
                0,
                0,
                DownloadProgressState.StartingChapter,
                $"正在載入 {chapter.Title}"));

            try
            {
                var pages = await _sourceClient
                    .LoadChapterPagesAsync(chapter, cancellationToken)
                    .ConfigureAwait(false);
                var chapterDownloadedPages = 0;
                var chapterSkippedPages = 0;

                var pageResults = await DownloadPagesAsync(
                    comic,
                    chapter,
                    pages,
                    libraryRoot,
                    downloadMode,
                    cancellationToken).ConfigureAwait(false);

                foreach (var pageResult in pageResults)
                {
                    var page = pageResult.Page;
                    var wasDownloaded = pageResult.WasDownloaded;

                    if (wasDownloaded)
                    {
                        downloadedPages++;
                        chapterDownloadedPages++;
                    }
                    else
                    {
                        skippedPages++;
                        chapterSkippedPages++;
                    }

                    progress?.Report(CreateProgress(
                        chapter,
                        chapterIndex,
                        chapters.Count,
                        page.Number,
                        pages.Count,
                        wasDownloaded ? DownloadProgressState.Downloading : DownloadProgressState.Skipped,
                        wasDownloaded ? $"已下載第 {page.Number} 頁" : $"略過已存在的第 {page.Number} 頁"));
                }

                completedChapters++;
                chapterResults.Add(new DownloadChapterResult(
                    chapter.Id,
                    chapter.Title,
                    LibraryPathPolicy.GetChapterFolderName(chapter),
                    pages.Count,
                    chapterDownloadedPages,
                    chapterSkippedPages,
                    DateTimeOffset.UtcNow));
                progress?.Report(CreateProgress(
                    chapter,
                    chapterIndex,
                    chapters.Count,
                    pages.Count,
                    pages.Count,
                    DownloadProgressState.CompletedChapter,
                    $"完成 {chapter.Title}"));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SourceAccessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                var message = $"{chapter.Title}：{exception.Message}";
                errors.Add(message);
                progress?.Report(CreateProgress(
                    chapter,
                    chapterIndex,
                    chapters.Count,
                    0,
                    0,
                    DownloadProgressState.Failed,
                    message));
            }
        }

        return new DownloadSummary(
            completedChapters,
            downloadedPages,
            skippedPages,
            errors,
            chapterResults);
    }

    private async Task<IReadOnlyList<PageDownloadResult>> DownloadPagesAsync(
        ComicInfo comic,
        ChapterInfo chapter,
        IReadOnlyList<ComicPage> pages,
        string libraryRoot,
        DownloadMode downloadMode,
        CancellationToken cancellationToken)
    {
        var orderedPages = pages.OrderBy(page => page.Number).ToArray();
        var maxConcurrency = GetMaxConcurrency(downloadMode);
        var requestDelay = GetRequestDelay(downloadMode);
        var useVerifiedPageLoader = downloadMode == DownloadMode.Safe;
        var results = new List<PageDownloadResult>(orderedPages.Length);

        for (var offset = 0; offset < orderedPages.Length; offset += maxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = orderedPages
                .Skip(offset)
                .Take(maxConcurrency)
                .Select(page => DownloadPageWithDelayAsync(
                    comic,
                    chapter,
                    page,
                    libraryRoot,
                    requestDelay,
                    useVerifiedPageLoader,
                    cancellationToken));
            results.AddRange(await Task.WhenAll(batch).ConfigureAwait(false));
        }

        return results;
    }

    private async Task<PageDownloadResult> DownloadPageWithDelayAsync(
        ComicInfo comic,
        ChapterInfo chapter,
        ComicPage page,
        string libraryRoot,
        TimeSpan requestDelay,
        bool useVerifiedPageLoader,
        CancellationToken cancellationToken)
    {
        if (requestDelay > TimeSpan.Zero)
        {
            await Task.Delay(requestDelay, cancellationToken).ConfigureAwait(false);
        }

        var wasDownloaded = await DownloadPageAsync(
            comic,
            chapter,
            page,
            libraryRoot,
            useVerifiedPageLoader,
            cancellationToken).ConfigureAwait(false);
        return new PageDownloadResult(page, wasDownloaded);
    }

    private static int GetMaxConcurrency(DownloadMode downloadMode) => downloadMode switch
    {
        DownloadMode.Safe => 1,
        DownloadMode.Standard => 2,
        DownloadMode.Fast => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(downloadMode))
    };

    private TimeSpan GetRequestDelay(DownloadMode downloadMode)
    {
        var maximumDelay = downloadMode switch
        {
            DownloadMode.Safe => _requestDelay,
            DownloadMode.Standard => TimeSpan.FromMilliseconds(350),
            DownloadMode.Fast => TimeSpan.FromMilliseconds(150),
            _ => throw new ArgumentOutOfRangeException(nameof(downloadMode))
        };
        return _requestDelay <= maximumDelay ? _requestDelay : maximumDelay;
    }

    private async Task<bool> DownloadPageAsync(
        ComicInfo comic,
        ChapterInfo chapter,
        ComicPage page,
        string libraryRoot,
        bool useVerifiedPageLoader,
        CancellationToken cancellationToken)
    {
        if (!SourceUrlPolicy.IsAllowedHappyMhAsset(page.SourceUri))
        {
            throw new InvalidDataException("圖片網址不在允許的 HappyMH 網域內。");
        }

        if (useVerifiedPageLoader && _pageLoader is not null)
        {
            var content = await _pageLoader
                .LoadAsync(chapter, page, _maxImageBytes, cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(content.MediaType) ||
                !ExtensionByMediaType.TryGetValue(content.MediaType, out var loadedExtension))
            {
                throw new InvalidDataException("下載內容不是支援的圖片格式。");
            }

            if (content.Bytes.Length == 0 || content.Bytes.LongLength > _maxImageBytes)
            {
                throw new InvalidDataException("圖片大小無效或超過允許上限。");
            }

            return await WriteLoadedPageAsync(
                comic,
                chapter,
                page,
                libraryRoot,
                loadedExtension,
                content.Bytes,
                cancellationToken).ConfigureAwait(false);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, page.SourceUri);
        request.Headers.Referrer = chapter.SourceUri;
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if ((int)response.StatusCode is 401 or 403 or 429)
        {
            throw new SourceAccessException(
                $"圖片存取驗證已失效（HTTP {(int)response.StatusCode}），請重新完成手動驗證。");
        }

        if ((int)response.StatusCode is >= 300 and < 400)
        {
            throw new SourceAccessException(
                "圖片請求被重新導向，可能需要重新完成手動驗證。");
        }

        response.EnsureSuccessStatusCode();

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (response.Content.Headers.ContentLength > _maxImageBytes)
        {
            throw new InvalidDataException("圖片超過允許大小。");
        }

        if (mediaType?.Equals("text/html", StringComparison.OrdinalIgnoreCase) == true)
        {
            var bytes = await ReadBoundedBytesAsync(
                response.Content,
                _maxImageBytes,
                cancellationToken).ConfigureAwait(false);
            if (LooksLikeHtmlDocument(bytes))
            {
                throw new SourceAccessException(
                    "圖片請求回傳驗證頁面，請重新完成手動驗證。");
            }

            throw new InvalidDataException("來源回應不是支援的圖片格式。");
        }

        if (string.IsNullOrWhiteSpace(mediaType) ||
            !ExtensionByMediaType.TryGetValue(mediaType, out var extension))
        {
            throw new InvalidDataException("來源回應不是支援的圖片格式。");
        }

        var finalPath = LibraryPathPolicy.GetPagePath(
            libraryRoot,
            comic.Id,
            chapter,
            page.Number,
            extension);

        var directory = Path.GetDirectoryName(finalPath)
            ?? throw new InvalidOperationException("無法建立圖片目錄。");
        EnsureSafeDirectory(libraryRoot, directory);

        if (File.Exists(finalPath))
        {
            return false;
        }

        var partialPath = finalPath + ".partial";
        if (File.Exists(partialPath))
        {
            File.Delete(partialPath);
        }

        try
        {
            await WriteBoundedFileAsync(
                response.Content,
                partialPath,
                _maxImageBytes,
                cancellationToken).ConfigureAwait(false);
            File.Move(partialPath, finalPath, overwrite: false);
            return true;
        }
        finally
        {
            if (File.Exists(partialPath))
            {
                File.Delete(partialPath);
            }
        }
    }

    private static async Task<bool> WriteLoadedPageAsync(
        ComicInfo comic,
        ChapterInfo chapter,
        ComicPage page,
        string libraryRoot,
        string extension,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var finalPath = LibraryPathPolicy.GetPagePath(
            libraryRoot,
            comic.Id,
            chapter,
            page.Number,
            extension);

        var directory = Path.GetDirectoryName(finalPath)
            ?? throw new InvalidOperationException("無法建立圖片資料夾。");
        EnsureSafeDirectory(libraryRoot, directory);

        if (File.Exists(finalPath))
        {
            return false;
        }

        var partialPath = finalPath + ".partial";
        if (File.Exists(partialPath))
        {
            File.Delete(partialPath);
        }

        try
        {
            await File.WriteAllBytesAsync(partialPath, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(partialPath, finalPath, overwrite: false);
            return true;
        }
        finally
        {
            if (File.Exists(partialPath))
            {
                File.Delete(partialPath);
            }
        }
    }

    private static async Task WriteBoundedFileAsync(
        HttpContent content,
        string partialPath,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        await using var input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(
            partialPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[81_920];
        long total = 0;

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new InvalidDataException("圖片超過允許大小。");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadBoundedBytesAsync(
        HttpContent content,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        await using var input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[81_920];
        long total = 0;

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new InvalidDataException("來源回應超過允許大小。");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return output.ToArray();
    }

    private static bool LooksLikeHtmlDocument(ReadOnlySpan<byte> bytes)
    {
        var index = 0;
        if (bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            index = 3;
        }

        while (index < bytes.Length && bytes[index] is 9 or 10 or 13 or 32)
        {
            index++;
        }

        return index < bytes.Length && bytes[index] == (byte)'<';
    }

    private static void EnsureSafeDirectory(string libraryRoot, string targetDirectory)
    {
        var root = Path.GetFullPath(libraryRoot);
        var target = Path.GetFullPath(targetDirectory);
        var rootBoundary = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!target.StartsWith(rootBoundary, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("下載目錄超出漫畫庫範圍。");
        }

        Directory.CreateDirectory(target);

        var current = new DirectoryInfo(target);
        var rootInfo = new DirectoryInfo(root);
        while (current.FullName.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("漫畫庫路徑不可包含連結或 junction。");
            }

            if (current.FullName.Equals(rootInfo.FullName, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = current.Parent
                ?? throw new InvalidOperationException("無法驗證漫畫庫路徑。");
        }
    }

    private static DownloadProgress CreateProgress(
        ChapterInfo chapter,
        int chapterIndex,
        int chapterCount,
        int pageNumber,
        int pageCount,
        DownloadProgressState state,
        string message) => new(
            chapter.Id,
            chapter.Title,
            chapterIndex + 1,
            chapterCount,
            pageNumber,
            pageCount,
            state,
            message);

    private sealed record PageDownloadResult(ComicPage Page, bool WasDownloaded);
}
