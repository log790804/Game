using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Comic.Core.Abstractions;
using Comic.Core.Exceptions;
using Comic.Core.Models;
using Comic.Core.Security;
using Comic.Infrastructure.Downloads;
using Comic.Infrastructure.HappyMh;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Comic.Desktop;

internal sealed class VerifiedWebViewComicSourceClient : IComicSourceClient, IComicPageLoader, IDisposable
{
    private const int MaxHtmlCharacters = 5 * 1024 * 1024;
    private const string PngDataUrlPrefix = "data:image/png;base64,";
    private const string ConvertDisplayedImageToPngScript = """
        (() => {
          const image = document.querySelector('img');
          if (!image || !image.complete || image.naturalWidth < 1 || image.naturalHeight < 1) {
            return null;
          }

          const canvas = document.createElement('canvas');
          canvas.width = image.naturalWidth;
          canvas.height = image.naturalHeight;
          const context = canvas.getContext('2d');
          if (!context) return null;
          context.drawImage(image, 0, 0);
          return canvas.toDataURL('image/png');
        })()
        """;
    private const string CaptureChapterScansScript = """
        (() => {
          const nativeParse = JSON.parse;
          window.__comicChapterScans = null;

          JSON.parse = function(text, reviver) {
            const value = nativeParse.call(this, text, reviver);
            try {
              const scans = Array.isArray(value)
                ? value
                : (Array.isArray(value?.data?.scans) ? value.data.scans : null);
              if (scans?.some(scan =>
                  scan && typeof scan.url === 'string' &&
                  Object.prototype.hasOwnProperty.call(scan, 'n')) &&
                  scans.length > (window.__comicChapterScans?.length ?? 0)) {
                window.__comicChapterScans = scans.map(scan => ({
                  url: scan.url,
                  width: scan.width,
                  height: scan.height,
                  n: scan.n
                }));
              }
            } catch {
              // Keep the site running if an unrelated JSON payload is malformed.
            }
            return value;
          };
        })();
        """;
    private static readonly TimeSpan NavigationTimeout = TimeSpan.FromSeconds(25);
    private readonly IComicSourceClient _directSource;
    private readonly HappyMhHtmlParser _parser;
    private readonly Dispatcher _dispatcher;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Window? _hostWindow;
    private WebView2? _browser;
    private CoreWebView2Environment? _environment;
    private bool _verifiedSessionEnabled;
    private bool _disposed;

    public VerifiedWebViewComicSourceClient(
        IComicSourceClient directSource,
        HappyMhHtmlParser parser,
        Dispatcher dispatcher)
    {
        _directSource = directSource;
        _parser = parser;
        _dispatcher = dispatcher;
    }

    public void EnableVerifiedSession() => _verifiedSessionEnabled = true;

    public Task<ComicInfo> LoadComicAsync(Uri sourceUri, CancellationToken cancellationToken) =>
        _directSource.LoadComicAsync(sourceUri, cancellationToken);

    public async Task<IReadOnlyList<ComicPage>> LoadChapterPagesAsync(
        ChapterInfo chapter,
        CancellationToken cancellationToken)
    {
        if (!_verifiedSessionEnabled)
        {
            return await _directSource
                .LoadChapterPagesAsync(chapter, cancellationToken)
                .ConfigureAwait(false);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _dispatcher
                .InvokeAsync(() => LoadChapterOnUiThreadAsync(chapter, cancellationToken))
                .Task
                .Unwrap()
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ComicPageContent> LoadAsync(
        ChapterInfo chapter,
        ComicPage page,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        if (!_verifiedSessionEnabled)
        {
            throw new SourceAccessException("請先完成手動驗證，再下載漫畫圖片。");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _dispatcher
                .InvokeAsync(() => LoadPageOnUiThreadAsync(chapter, page, maxBytes, cancellationToken))
                .Task
                .Unwrap()
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ComicPageContent> LoadPageOnUiThreadAsync(
        ChapterInfo chapter,
        ComicPage page,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SourceUrlPolicy.EnsureAllowedHappyMhPage(chapter.SourceUri);
        if (!SourceUrlPolicy.IsAllowedHappyMhAsset(page.SourceUri))
        {
            throw new InvalidDataException("圖片來源不在允許的 HappyMH 網域內。");
        }

        await EnsureBrowserAsync();
        var core = _browser!.CoreWebView2;
        var responseCompletion = new TaskCompletionSource<ComicPageContent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var navigationCompletion = new TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args) =>
            navigationCompletion.TrySetResult(args);

        async void OnResponseReceived(
            object? sender,
            CoreWebView2WebResourceResponseReceivedEventArgs args)
        {
            if (!IsSameRequest(args.Request.Uri, page.SourceUri))
            {
                return;
            }

            try
            {
                var statusCode = args.Response.StatusCode;
                if (statusCode is 401 or 403 or 429 || statusCode is >= 300 and < 400)
                {
                    throw new SourceAccessException(
                        $"圖片工作階段需要重新驗證（HTTP {statusCode}）。");
                }

                if (statusCode is < 200 or >= 300)
                {
                    throw new HttpRequestException(
                        $"驗證瀏覽器下載圖片失敗（HTTP {statusCode}）。");
                }

                await using var responseStream = await args.Response
                    .GetContentAsync()
                    .ConfigureAwait(false);
                if (responseStream is null)
                {
                    throw new InvalidDataException("驗證瀏覽器沒有傳回圖片內容。");
                }

                var bytes = await ReadBoundedAsync(
                    responseStream,
                    maxBytes,
                    cancellationToken).ConfigureAwait(false);
                if (LooksLikeHtmlDocument(bytes))
                {
                    throw new SourceAccessException(
                        "圖片請求回傳驗證頁面，請重新完成手動驗證。");
                }

                var mediaType = DetectImageMediaType(bytes);
                responseCompletion.TrySetResult(new ComicPageContent(mediaType, bytes));
            }
            catch (Exception exception)
            {
                responseCompletion.TrySetException(exception);
            }
        }

        core.NavigationCompleted += OnNavigationCompleted;
        core.WebResourceResponseReceived += OnResponseReceived;
        try
        {
            var headers =
                $"Referer: {chapter.SourceUri.AbsoluteUri}\r\n" +
                "Accept: image/webp,image/png,image/jpeg;q=0.9,*/*;q=0.1\r\n";
            var request = _environment!.CreateWebResourceRequest(
                page.SourceUri.AbsoluteUri,
                "GET",
                postData: null,
                headers);
            core.NavigateWithWebResourceRequest(request);
            var content = await responseCompletion.Task.WaitAsync(NavigationTimeout, cancellationToken);
            if (!content.MediaType.Equals("image/avif", StringComparison.OrdinalIgnoreCase))
            {
                return content;
            }

            var navigation = await navigationCompletion.Task
                .WaitAsync(NavigationTimeout, cancellationToken);
            if (!navigation.IsSuccess)
            {
                throw new SourceAccessException(
                    $"驗證瀏覽器無法顯示 AVIF 圖片（{navigation.WebErrorStatus}）。");
            }

            return await ConvertDisplayedImageToPngAsync(core, maxBytes, cancellationToken);
        }
        finally
        {
            core.NavigationCompleted -= OnNavigationCompleted;
            core.WebResourceResponseReceived -= OnResponseReceived;
        }
    }

    private static async Task<ComicPageContent> ConvertDisplayedImageToPngAsync(
        CoreWebView2 core,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resultJson = await core
                .ExecuteScriptAsync(ConvertDisplayedImageToPngScript)
                .WaitAsync(NavigationTimeout, cancellationToken);
            var dataUrl = JsonSerializer.Deserialize<string>(resultJson);
            if (dataUrl?.StartsWith(PngDataUrlPrefix, StringComparison.Ordinal) == true)
            {
                var payload = dataUrl[PngDataUrlPrefix.Length..];
                var maximumBase64Length = checked(((maxBytes + 2) / 3) * 4 + 4);
                if (payload.Length > maximumBase64Length)
                {
                    throw new InvalidDataException("轉換後的 PNG 圖片超過允許上限。");
                }

                var bytes = Convert.FromBase64String(payload);
                if (bytes.LongLength > maxBytes)
                {
                    throw new InvalidDataException("轉換後的 PNG 圖片超過允許上限。");
                }

                return new ComicPageContent("image/png", bytes);
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new InvalidDataException("驗證瀏覽器無法將 AVIF 圖片轉換為離線 PNG。");
    }

    private async Task<IReadOnlyList<ComicPage>> LoadChapterOnUiThreadAsync(
        ChapterInfo chapter,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SourceUrlPolicy.EnsureAllowedHappyMhPage(chapter.SourceUri);
        await EnsureBrowserAsync();

        var core = _browser!.CoreWebView2;
        var navigation = new TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args) =>
            navigation.TrySetResult(args);

        core.NavigationCompleted += OnNavigationCompleted;
        try
        {
            core.Navigate(chapter.SourceUri.AbsoluteUri);
            var result = await navigation.Task.WaitAsync(NavigationTimeout, cancellationToken);
            if (!result.IsSuccess)
            {
                throw new SourceAccessException(
                    $"驗證瀏覽器無法載入章節頁（{result.WebErrorStatus}）。");
            }
        }
        finally
        {
            core.NavigationCompleted -= OnNavigationCompleted;
        }

        if (!IsExpectedChapterPage(core.Source, chapter.SourceUri))
        {
            throw new SourceAccessException(
                "章節頁被導向其他位置；請重新開啟手動驗證後再試一次。");
        }

        InvalidDataException? lastParseError = null;
        IReadOnlyList<ComicPage>? fallbackDomPages = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scanJson = await core
                .ExecuteScriptAsync("window.__comicChapterScans ?? null")
                .WaitAsync(NavigationTimeout, cancellationToken);
            if (!scanJson.Equals("null", StringComparison.Ordinal))
            {
                try
                {
                    return _parser.ParseChapterScanSnapshot(scanJson, chapter.SourceUri);
                }
                catch (InvalidDataException exception)
                {
                    lastParseError = exception;
                }
            }

            var htmlJson = await core
                .ExecuteScriptAsync("document.documentElement.outerHTML")
                .WaitAsync(NavigationTimeout, cancellationToken);
            var html = JsonSerializer.Deserialize<string>(htmlJson);
            if (!string.IsNullOrWhiteSpace(html))
            {
                if (html.Length > MaxHtmlCharacters)
                {
                    throw new InvalidDataException("驗證瀏覽器取得的章節頁面過大。");
                }

                try
                {
                    fallbackDomPages = _parser.ParseChapterPages(html, chapter.SourceUri);
                }
                catch (InvalidDataException exception)
                {
                    lastParseError = exception;
                    if (exception.Message.Contains("人機驗證", StringComparison.Ordinal) ||
                        exception.Message.Contains("人机验证", StringComparison.Ordinal))
                    {
                        throw;
                    }
                }
            }

            await Task.Delay(250, cancellationToken);
        }

        return fallbackDomPages ??
            throw lastParseError ?? new InvalidDataException("章節頁面中找不到可下載的圖片。");
    }

    private async Task EnsureBrowserAsync()
    {
        if (_browser?.CoreWebView2 is not null)
        {
            return;
        }

        var profilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Comic",
            "WebView2Profile");
        Directory.CreateDirectory(profilePath);

        _browser = new WebView2();
        _hostWindow = new Window
        {
            Width = 800,
            Height = 600,
            Left = -10_000,
            Top = -10_000,
            Opacity = 0,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Content = _browser
        };
        _hostWindow.Show();

        _environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: profilePath);
        await _browser.EnsureCoreWebView2Async(_environment);
        await _browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
            CaptureChapterScansScript);
        _browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
        _browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
        _browser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
        _browser.CoreWebView2.NewWindowRequested += (_, args) => args.Handled = true;
    }

    private static bool IsExpectedChapterPage(string? actual, Uri expected)
    {
        if (!Uri.TryCreate(actual, UriKind.Absolute, out var actualUri))
        {
            return false;
        }

        try
        {
            SourceUrlPolicy.EnsureAllowedHappyMhPage(actualUri);
        }
        catch (ArgumentException)
        {
            return false;
        }

        return actualUri.AbsolutePath.Equals(expected.AbsolutePath, StringComparison.Ordinal);
    }

    private static bool IsSameRequest(string? actual, Uri expected) =>
        Uri.TryCreate(actual, UriKind.Absolute, out var actualUri) &&
        Uri.Compare(
            actualUri,
            expected,
            UriComponents.HttpRequestUrl,
            UriFormat.SafeUnescaped,
            StringComparison.OrdinalIgnoreCase) == 0;

    private static async Task<byte[]> ReadBoundedAsync(
        Stream input,
        long maxBytes,
        CancellationToken cancellationToken)
    {
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
                throw new InvalidDataException("圖片大小超過允許上限。");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return output.ToArray();
    }

    private static string DetectImageMediaType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 &&
            bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 8 &&
            bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "image/png";
        }

        if (bytes.Length >= 6 &&
            (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8)))
        {
            return "image/gif";
        }

        if (bytes.Length >= 12 &&
            bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        if (bytes.Length >= 12 &&
            bytes.Slice(4, 4).SequenceEqual("ftyp"u8) &&
            (bytes.Slice(8, 4).SequenceEqual("avif"u8) ||
             bytes.Slice(8, 4).SequenceEqual("avis"u8)))
        {
            return "image/avif";
        }

        throw new InvalidDataException("驗證瀏覽器傳回的內容不是支援的圖片格式。");
    }

    private static bool LooksLikeHtmlDocument(ReadOnlySpan<byte> bytes)
    {
        var index = 0;
        while (index < bytes.Length && bytes[index] is 9 or 10 or 13 or 32)
        {
            index++;
        }

        return index < bytes.Length && bytes[index] == (byte)'<';
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _browser?.Dispose();
        _hostWindow?.Close();
        _gate.Dispose();
    }
}
