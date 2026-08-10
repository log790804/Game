using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using Comic.Core.Models;
using Comic.Core.Security;
using Comic.Infrastructure.HappyMh;
using Microsoft.Web.WebView2.Core;

namespace Comic.Desktop;

public partial class ManualVerificationWindow : Window
{
    private const string MangaExpandScript = """
        (() => {
          const expandButton = Array.from(document.querySelectorAll('button'))
            .find(button => {
              const text = (button.textContent || '').replace(/\s/g, '');
              return text.includes('全部章') && (text.includes('章节') || text.includes('章節'));
            });
          if (!expandButton) return -1;
          window.__comicChapterCache = new Map();
          const initialCount = document.querySelectorAll(
            'a[href^="/mangaread/"], a[href^="/mangarcard/"]').length;
          expandButton.click();
          return initialCount;
        })()
        """;
    private const string MangaChapterCountScript = """
        document.querySelectorAll('a[href^="/mangaread/"], a[href^="/mangarcard/"]').length
        """;
    private const string MangaCollectAndScrollScript = """
        (() => {
          const links = Array.from(document.querySelectorAll(
            'a[href^="/mangaread/"], a[href^="/mangarcard/"]'));
          const cache = window.__comicChapterCache instanceof Map
            ? window.__comicChapterCache
            : (window.__comicChapterCache = new Map());
          for (const link of links) {
            const href = (link.getAttribute('href') || '').slice(0, 2048);
            if (href) {
              cache.set(href, {
                title: (link.textContent || '').trim().slice(0, 500),
                href
              });
            }
          }

          const candidates = [];
          for (const link of links) {
            for (let element = link.parentElement; element; element = element.parentElement) {
              const style = getComputedStyle(element);
              if (element.scrollHeight > element.clientHeight + 10 &&
                  (style.overflowY === 'auto' || style.overflowY === 'scroll')) {
                candidates.push(element);
              }
            }
          }
          const scroller = candidates.sort((left, right) => right.scrollHeight - left.scrollHeight)[0];
          if (!scroller) {
            return JSON.stringify({ count: cache.size, atEnd: true });
          }

          const atEnd = Math.ceil(scroller.scrollTop + scroller.clientHeight) >= scroller.scrollHeight - 2;
          if (!atEnd) {
            scroller.scrollTop = Math.min(
              scroller.scrollTop + Math.max(1, Math.floor(scroller.clientHeight * 0.8)),
              scroller.scrollHeight);
          }
          return JSON.stringify({ count: cache.size, atEnd });
        })()
        """;
    private const string MangaSnapshotScript = """
        (() => {
          const chapterLinks = Array.from(document.querySelectorAll('a[href]'))
            .filter(link => {
              try {
                const url = new URL(link.getAttribute('href'), location.href);
                return url.origin === location.origin &&
                  (url.pathname.startsWith('/mangaread/') || url.pathname.startsWith('/mangarcard/'));
              } catch {
                return false;
              }
            });
          const cache = window.__comicChapterCache instanceof Map
            ? window.__comicChapterCache
            : (window.__comicChapterCache = new Map());
          for (const link of chapterLinks) {
            const href = (link.getAttribute('href') || '').slice(0, 2048);
            if (href) {
              cache.set(href, {
                title: (link.textContent || '').trim().slice(0, 500),
                href
              });
            }
          }
          const documentTitle = (document.title || '').trim();
          const comicTitle = documentTitle.split(/\u6f2b\u753b-|\u6f2b\u756b-/)[0].trim() ||
            (document.querySelector('h1')?.textContent || '').trim();
          return JSON.stringify({
            title: comicTitle.slice(0, 500),
            chapters: Array.from(cache.values())
              .slice(0, 5000)
          });
        })()
        """;
    private static readonly TimeSpan ScriptTimeout = TimeSpan.FromSeconds(15);
    private readonly Uri _mangaUri;
    private readonly HappyMhHtmlParser _parser = new();
    private bool _isInitialized;
    private bool _isCompleting;

    public ManualVerificationWindow(Uri mangaUri)
    {
        _mangaUri = SourceUrlPolicy.ParseHappyMhMangaUrl(mangaUri.AbsoluteUri);
        InitializeComponent();
    }

    public ManualVerificationResult? Result { get; private set; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        try
        {
            var profilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Comic",
                "WebView2Profile");
            Directory.CreateDirectory(profilePath);

            // Microsoft recommends a writable custom user-data folder for WPF apps.
            // https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder
            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: profilePath);
            await Browser.EnsureCoreWebView2Async(environment);

            Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
            Browser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            Browser.CoreWebView2.NavigationStarting += OnNavigationStarting;
            Browser.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            Browser.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
            Browser.Source = _mangaUri;
            StatusTextBlock.Text = "請在頁面中完成真人驗證。";
        }
        catch (Exception exception) when (
            exception is WebView2RuntimeNotFoundException or InvalidOperationException)
        {
            StatusTextBlock.Text = "無法啟動 WebView2；請先安裝 Microsoft Edge WebView2 Runtime。";
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (IsAllowedBrowserUri(e.Uri))
        {
            CompleteButton.IsEnabled = false;
            StatusTextBlock.Text = "頁面載入中…";
            return;
        }

        e.Cancel = true;
        StatusTextBlock.Text = "已阻擋離開 m.happymh.com 的導覽。";
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        CompleteButton.IsEnabled = e.IsSuccess;
        StatusTextBlock.Text = e.IsSuccess
            ? "若驗證已完成，請確認目前是漫畫詳情頁，再按「驗證完成並載入」。"
            : $"頁面載入失敗：{e.WebErrorStatus}";
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
    }

    private void OnReturnToMangaClick(object sender, RoutedEventArgs e)
    {
        if (Browser.CoreWebView2 is not null)
        {
            Browser.CoreWebView2.Navigate(_mangaUri.AbsoluteUri);
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        if (Browser.CoreWebView2 is null)
        {
            return;
        }

        CompleteButton.IsEnabled = false;
        StatusTextBlock.Text = "正在重新整理驗證頁面……";
        Browser.CoreWebView2.Reload();
    }

    private async void OnCompleteClick(object sender, RoutedEventArgs e)
    {
        if (_isCompleting)
        {
            return;
        }

        if (Browser.CoreWebView2 is null ||
            !IsRequestedMangaPage(Browser.CoreWebView2.Source))
        {
            StatusTextBlock.Text = "請先按「回到漫畫頁」，並等待漫畫詳情頁載入完成。";
            return;
        }

        _isCompleting = true;
        CompleteButton.IsEnabled = false;
        CompleteButton.Content = "正在載入章節…";
        StatusTextBlock.Text = "正在讀取已顯示的章節清單，請稍候…";

        try
        {
            await ExpandChapterListAsync();
            await CollectVirtualizedChaptersAsync();
            var wrappedSnapshot = await Browser.CoreWebView2
                .ExecuteScriptAsync(MangaSnapshotScript)
                .WaitAsync(ScriptTimeout);
            var snapshotJson = UnwrapSnapshotJson(wrappedSnapshot);
            if (string.IsNullOrWhiteSpace(snapshotJson))
            {
                throw new InvalidDataException("無法從目前頁面讀取章節資料。");
            }

            var comic = _parser.ParseMangaSnapshot(snapshotJson, _mangaUri);
            StatusTextBlock.Text = $"已找到 {comic.Chapters.Count} 個章節，正在套用驗證工作階段…";

            var userAgentJson = await Browser.CoreWebView2
                .ExecuteScriptAsync("navigator.userAgent")
                .WaitAsync(ScriptTimeout);
            var userAgent = JsonSerializer.Deserialize<string>(userAgentJson) ?? string.Empty;

            // Cookies are read through WebView2's profile API, never through page JavaScript.
            // https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2cookiemanager.getcookiesasync
            var browserCookies = await Browser.CoreWebView2.CookieManager
                .GetCookiesAsync("https://m.happymh.com/")
                .WaitAsync(ScriptTimeout);
            var cookies = browserCookies
                .Select(cookie => new BrowserSessionCookie(
                    cookie.Name,
                    cookie.Value,
                    cookie.Domain,
                    cookie.Path,
                    cookie.IsHttpOnly,
                    cookie.IsSecure,
                    GetSafeExpiration(cookie)))
                .ToArray();

            Result = new ManualVerificationResult(comic, cookies, userAgent);
            DialogResult = true;
        }
        catch (TimeoutException)
        {
            ShowImportError("讀取章節資料逾時，請確認章節清單已顯示後再試一次。");
        }
        catch (InvalidDataException exception)
        {
            ShowImportError(exception.Message);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or ArgumentException or COMException)
        {
            ShowImportError($"無法讀取驗證結果：{exception.Message}");
        }
        finally
        {
            _isCompleting = false;
            if (IsVisible)
            {
                CompleteButton.Content = "驗證完成並載入 (_C)";
                CompleteButton.IsEnabled = true;
            }
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static bool IsAllowedBrowserUri(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
               uri.IdnHost.Equals("m.happymh.com", StringComparison.OrdinalIgnoreCase) &&
               uri.IsDefaultPort &&
               string.IsNullOrEmpty(uri.UserInfo);
    }

    private bool IsRequestedMangaPage(string? value)
    {
        if (!IsAllowedBrowserUri(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var segments = SourceUrlPolicy.SplitPath(uri);
        var requestedSegments = SourceUrlPolicy.SplitPath(_mangaUri);
        return segments.Length == 2 &&
               segments[0].Equals("manga", StringComparison.OrdinalIgnoreCase) &&
               segments[1].Equals(requestedSegments[1], StringComparison.Ordinal);
    }

    private void ShowImportError(string message)
    {
        StatusTextBlock.Text = message;
        MessageBox.Show(
            this,
            message,
            "尚未能載入漫畫",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static string? UnwrapSnapshotJson(string scriptResult)
    {
        if (string.IsNullOrWhiteSpace(scriptResult))
        {
            return null;
        }

        using var document = JsonDocument.Parse(scriptResult);
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.String => document.RootElement.GetString(),
            JsonValueKind.Object => scriptResult,
            _ => null
        };
    }

    private async Task ExpandChapterListAsync()
    {
        var initialCountJson = await Browser.CoreWebView2
            .ExecuteScriptAsync(MangaExpandScript)
            .WaitAsync(ScriptTimeout);
        if (!int.TryParse(initialCountJson, out var initialCount) || initialCount < 0)
        {
            return;
        }

        var previousCount = -1;
        var stableChecks = 0;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(250);
            var currentCountJson = await Browser.CoreWebView2
                .ExecuteScriptAsync(MangaChapterCountScript)
                .WaitAsync(ScriptTimeout);
            if (!int.TryParse(currentCountJson, out var currentCount))
            {
                continue;
            }

            stableChecks = currentCount == previousCount ? stableChecks + 1 : 0;
            previousCount = currentCount;
            if (currentCount > initialCount && stableChecks >= 3)
            {
                return;
            }
        }
    }

    private async Task CollectVirtualizedChaptersAsync()
    {
        var previousCount = -1;
        var stableEndChecks = 0;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var wrappedState = await Browser.CoreWebView2
                .ExecuteScriptAsync(MangaCollectAndScrollScript)
                .WaitAsync(ScriptTimeout);
            var stateJson = UnwrapSnapshotJson(wrappedState);
            if (string.IsNullOrWhiteSpace(stateJson))
            {
                return;
            }

            using var state = JsonDocument.Parse(stateJson);
            var count = state.RootElement.GetProperty("count").GetInt32();
            var atEnd = state.RootElement.GetProperty("atEnd").GetBoolean();
            stableEndChecks = atEnd && count == previousCount ? stableEndChecks + 1 : 0;
            previousCount = count;
            if (stableEndChecks >= 2)
            {
                return;
            }

            await Task.Delay(100);
        }
    }

    private static DateTimeOffset? GetSafeExpiration(CoreWebView2Cookie cookie)
    {
        if (cookie.IsSession || cookie.Expires.Year < 1971)
        {
            return null;
        }

        return new DateTimeOffset(cookie.Expires.ToUniversalTime());
    }
}

public sealed record ManualVerificationResult(
    ComicInfo Comic,
    IReadOnlyList<BrowserSessionCookie> Cookies,
    string UserAgent);
