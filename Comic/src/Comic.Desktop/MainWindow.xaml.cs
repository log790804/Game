using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Comic.Core.Exceptions;
using Comic.Core.Selection;
using Comic.Desktop.ViewModels;
using Comic.Infrastructure.Downloads;
using Comic.Infrastructure.HappyMh;
using Comic.Infrastructure.Library;
using Microsoft.Win32;

namespace Comic.Desktop;

public partial class MainWindow : Window
{
    private const int MaxVerificationRefreshAttempts = 3;

    private readonly CookieContainer _sessionCookies = new();
    private readonly HappyMhHtmlParser _parser = new();
    private readonly HttpClient _sourceHttpClient;
    private readonly HttpClient _assetHttpClient;
    private readonly VerifiedWebViewComicSourceClient _verifiedSourceClient;
    private readonly MainWindowViewModel _viewModel;
    private readonly ReaderScrollRestoreGate _readerScrollRestoreGate = new();
    private bool _isFullScreen;
    private bool _isReaderRestoreQueued;
    private WindowState _previousWindowState;

    public MainWindow()
    {
        InitializeComponent();
        UpdateSharedChromeVisibility();

        _sourceHttpClient = CreateHttpClient(_sessionCookies);
        _assetHttpClient = CreateHttpClient(_sessionCookies);

        var sourceClient = new HappyMhSourceClient(_sourceHttpClient, _parser);
        _verifiedSourceClient = new VerifiedWebViewComicSourceClient(
            sourceClient,
            _parser,
            Dispatcher);

        var historyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Comic",
            "reading-history.json");
        var downloadHistoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Comic",
            "download-history.json");
        var metadataStore = new JsonComicMetadataStore();
        var downloadHistoryStore = new JsonDownloadHistoryStore(downloadHistoryPath);
        ComicMetadataBackfiller.Backfill(metadataStore, downloadHistoryStore.LoadAll());
        var downloadService = new SequentialDownloadService(
            _assetHttpClient,
            _verifiedSourceClient,
            requestDelay: TimeSpan.FromMilliseconds(800),
            pageLoader: _verifiedSourceClient,
            metadataStore: metadataStore);
        _viewModel = new MainWindowViewModel(
            sourceClient,
            downloadService,
            new ComicLibraryScanner(metadataStore),
            new JsonReadingHistoryStore(historyPath),
            downloadHistoryStore);
        DataContext = _viewModel;
        ReaderPagesControl.ItemContainerGenerator.StatusChanged +=
            OnReaderContainersStatusChanged;
        _viewModel.Reader.LoadLibrary(_viewModel.LibraryRoot);
        QueueReaderScrollRestore();
    }

    private static HttpClient CreateHttpClient(CookieContainer cookieContainer)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = true,
            CookieContainer = cookieContainer,
            AutomaticDecompression = DecompressionMethods.All
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(25)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ComicOfflineReader/0.1");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-TW,zh;q=0.9,en;q=0.7");
        return client;
    }

    private async void OnLoadComicClick(object sender, RoutedEventArgs e) =>
        await _viewModel.LoadComicAsync();

    private void OnManualVerificationClick(object sender, RoutedEventArgs e)
    {
        Uri mangaUri;
        try
        {
            mangaUri = Comic.Core.Security.SourceUrlPolicy.ParseHappyMhMangaUrl(_viewModel.MangaUrl);
        }
        catch (ArgumentException exception)
        {
            _viewModel.ReportStatus(exception.Message);
            return;
        }

        var verificationResult = ShowManualVerification(mangaUri, importComic: true);
        if (verificationResult == VerificationRefreshResult.Cancelled)
        {
            _viewModel.ReportStatus("已取消手動驗證。");
        }
    }

    private VerificationRefreshResult ShowManualVerification(Uri mangaUri, bool importComic)
    {
        var verificationWindow = new ManualVerificationWindow(mangaUri)
        {
            Owner = this
        };

        if (verificationWindow.ShowDialog() != true || verificationWindow.Result is not { } result)
        {
            return VerificationRefreshResult.Cancelled;
        }

        try
        {
            var importedCookieCount = BrowserSessionCookieImporter.Import(
                _sessionCookies,
                result.Comic.SourceUri,
                result.Cookies);
            ApplyVerifiedUserAgent(result.UserAgent);
            _verifiedSourceClient.EnableVerifiedSession();
            if (importComic)
            {
                _viewModel.ImportVerifiedComic(result.Comic, importedCookieCount);
            }
            else
            {
                _viewModel.ReportStatus(
                    $"驗證工作階段已更新（匯入 {importedCookieCount} 個 Cookie）。");
            }

            return VerificationRefreshResult.Completed;
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
        {
            _viewModel.ReportStatus($"驗證頁面仍無法解析：{exception.Message}");
            return VerificationRefreshResult.Failed;
        }
    }

    private async void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        var refreshAttempts = 0;
        while (true)
        {
            try
            {
                await _viewModel.DownloadSelectedAsync();
                return;
            }
            catch (SourceAccessException exception)
            {
                if (refreshAttempts >= MaxVerificationRefreshAttempts)
                {
                    _viewModel.ReportStatus(
                        $"下載已停止：連續 {MaxVerificationRefreshAttempts} 次更新驗證後仍無法存取。已下載內容會保留。最後錯誤：{exception.Message}");
                    return;
                }

                refreshAttempts++;
                _viewModel.ReportStatus(
                    $"下載已暫停，需要重新驗證（{refreshAttempts}/{MaxVerificationRefreshAttempts}）。已下載內容會保留。");

                Uri mangaUri;
                try
                {
                    mangaUri = Comic.Core.Security.SourceUrlPolicy.ParseHappyMhMangaUrl(
                        _viewModel.MangaUrl);
                }
                catch (ArgumentException parseException)
                {
                    _viewModel.ReportStatus(
                        $"下載已停止：無法開啟原漫畫驗證頁。{parseException.Message}");
                    return;
                }

                var verificationResult = ShowManualVerification(mangaUri, importComic: false);
                if (verificationResult != VerificationRefreshResult.Completed)
                {
                    if (verificationResult == VerificationRefreshResult.Cancelled)
                    {
                        _viewModel.ReportStatus("已取消重新驗證；本次批次停止，已下載內容會保留。");
                    }

                    return;
                }

                _viewModel.ReportStatus("驗證已更新，正在從中斷處安全續傳……");
            }
        }
    }

    private void OnSelectAllClick(object sender, RoutedEventArgs e) =>
        _viewModel.SelectAll(isSelected: true);

    private void OnClearSelectionClick(object sender, RoutedEventArgs e) =>
        _viewModel.SelectAll(isSelected: false);

    private void OnCancelClick(object sender, RoutedEventArgs e) =>
        _viewModel.CancelCurrentOperation();

    private void OnBrowseLibraryClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "選擇漫畫庫資料夾",
            Multiselect = false,
            InitialDirectory = GetInitialFolder(_viewModel.LibraryRoot)
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.LibraryRoot = dialog.FolderName;
            _readerScrollRestoreGate.BeginRestore();
            _viewModel.Reader.LoadLibrary(dialog.FolderName);
            QueueReaderScrollRestore();
        }
    }

    private void OnRefreshLibraryClick(object sender, RoutedEventArgs e)
    {
        _readerScrollRestoreGate.BeginRestore();
        _viewModel.Reader.LoadLibrary(_viewModel.LibraryRoot);
        QueueReaderScrollRestore();
    }

    private void OnOpenChapterFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "選擇已下載的章節資料夾",
            Multiselect = false,
            InitialDirectory = GetInitialFolder(_viewModel.LibraryRoot)
        };

        if (dialog.ShowDialog(this) == true)
        {
            _readerScrollRestoreGate.BeginRestore();
            _viewModel.Reader.LoadChapterFolder(dialog.FolderName);
            QueueReaderScrollRestore();
        }
    }

    private void OnPreviousChapterClick(object sender, RoutedEventArgs e) =>
        GoPreviousChapter();

    private void OnNextChapterClick(object sender, RoutedEventArgs e) =>
        GoNextChapter();

    private async void OnCheckLatestClick(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanStartOperation)
        {
            _viewModel.ReportStatus("目前有作業正在執行，完成或取消後再查看最新章節。");
            return;
        }

        Uri? sourceUri;
        try
        {
            sourceUri = _viewModel.Reader.SelectedComicSourceUri;
        }
        catch (ArgumentException exception)
        {
            _viewModel.ReportStatus(exception.Message);
            return;
        }

        if (sourceUri is null)
        {
            _viewModel.ReportStatus("請先在離線閱讀選擇一本漫畫。");
            return;
        }

        _viewModel.Reader.SaveCurrentPosition();
        _viewModel.MangaUrl = sourceUri.AbsoluteUri;
        MainTabs.SelectedIndex = 0;
        await _viewModel.LoadComicAsync();
    }

    private void OnDecreasePageWidthClick(object sender, RoutedEventArgs e) =>
        _viewModel.Reader.DecreasePageWidth();

    private void OnIncreasePageWidthClick(object sender, RoutedEventArgs e) =>
        _viewModel.Reader.IncreasePageWidth();

    private void OnResetPageWidthClick(object sender, RoutedEventArgs e) =>
        _viewModel.Reader.ResetPageWidth();

    private void OnReaderContainersStatusChanged(object? sender, EventArgs e)
    {
        if (ReaderPagesControl.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
        {
            _readerScrollRestoreGate.BeginRestore();
            return;
        }

        QueueReaderScrollRestore();
    }

    private void QueueReaderScrollRestore()
    {
        if (_isReaderRestoreQueued ||
            ReaderPagesControl.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
        {
            return;
        }

        _isReaderRestoreQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, RestoreReaderScrollPosition);
    }

    private void RestoreReaderScrollPosition()
    {
        _isReaderRestoreQueued = false;
        var pageIndex = _viewModel.Reader.CurrentPageIndex;
        if (pageIndex < 0)
        {
            _readerScrollRestoreGate.CompleteRestore();
            return;
        }

        ReaderPagesControl.UpdateLayout();
        if (ReaderPagesControl.ItemContainerGenerator.ContainerFromIndex(pageIndex) is not FrameworkElement page)
        {
            _readerScrollRestoreGate.CompleteRestore();
            return;
        }

        page.BringIntoView();
        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            _readerScrollRestoreGate.CompleteRestore);
    }

    private void OnReaderScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (!_readerScrollRestoreGate.CanTrackScroll || ReaderScrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        var pageBounds = new PageViewportBounds[_viewModel.Reader.PageItems.Count];
        for (var index = 0; index < pageBounds.Length; index++)
        {
            if (ReaderPagesControl.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement page)
            {
                continue;
            }

            try
            {
                var top = page.TransformToAncestor(ReaderScrollViewer)
                    .Transform(new Point(0, 0)).Y;
                pageBounds[index] = new PageViewportBounds(top, page.ActualHeight);
            }
            catch (InvalidOperationException)
            {
                // Layout is still being regenerated; the next scroll/layout event will retry.
            }
        }

        var visiblePage = ReaderViewportTracker.FindMostVisiblePage(
            pageBounds,
            ReaderScrollViewer.ViewportHeight);
        if (visiblePage >= 0)
        {
            _viewModel.Reader.SetCurrentPage(visiblePage);
        }
    }

    private void OnMainTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, MainTabs))
        {
            return;
        }

        UpdateSharedChromeVisibility();
        if (MainTabs.SelectedIndex == 1 && DataContext is MainWindowViewModel)
        {
            _readerScrollRestoreGate.BeginRestore();
            QueueReaderScrollRestore();
        }
    }

    private void UpdateSharedChromeVisibility()
    {
        if (AppHeader is null || GlobalStatusBar is null)
        {
            return;
        }

        var isReaderMode = MainTabs.SelectedIndex == 1;
        AppHeader.Visibility = isReaderMode ? Visibility.Collapsed : Visibility.Visible;
        GlobalStatusBar.Visibility = isReaderMode ? Visibility.Collapsed : Visibility.Visible;
        MainTabs.Margin = isReaderMode
            ? new Thickness(8, 0, 8, 8)
            : new Thickness(16);
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
            return;
        }

        if (MainTabs.SelectedIndex != 1)
        {
            return;
        }

        if (e.Key == Key.Left)
        {
            GoPreviousChapter();
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            GoNextChapter();
            e.Handled = true;
        }
    }

    private void GoPreviousChapter()
    {
        if (!_viewModel.Reader.CanGoPreviousChapter)
        {
            return;
        }

        _readerScrollRestoreGate.BeginRestore();
        _viewModel.Reader.GoPreviousChapter();
        QueueReaderScrollRestore();
    }

    private void GoNextChapter()
    {
        if (!_viewModel.Reader.CanGoNextChapter)
        {
            return;
        }

        _readerScrollRestoreGate.BeginRestore();
        _viewModel.Reader.GoNextChapter();
        QueueReaderScrollRestore();
    }

    private void ToggleFullScreen()
    {
        if (!_isFullScreen)
        {
            _previousWindowState = WindowState;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            _isFullScreen = true;
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = _previousWindowState;
            _isFullScreen = false;
        }
    }

    private static string GetInitialFolder(string preferredPath) =>
        Directory.Exists(preferredPath)
            ? preferredPath
            : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

    private void ApplyVerifiedUserAgent(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent) ||
            userAgent.Length > 512 ||
            userAgent.Any(char.IsControl))
        {
            return;
        }

        using var probe = new HttpRequestMessage();
        if (!probe.Headers.UserAgent.TryParseAdd(userAgent))
        {
            return;
        }

        foreach (var client in new[] { _sourceHttpClient, _assetHttpClient })
        {
            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.TryParseAdd(userAgent);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Reader.SaveCurrentPosition();
        _viewModel.CancelCurrentOperation();
        _sourceHttpClient.Dispose();
        _assetHttpClient.Dispose();
        _verifiedSourceClient.Dispose();
        base.OnClosed(e);
    }

    private enum VerificationRefreshResult
    {
        Completed,
        Cancelled,
        Failed
    }
}
