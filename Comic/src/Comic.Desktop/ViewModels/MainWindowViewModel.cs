using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using Comic.Core.Abstractions;
using Comic.Core.Exceptions;
using Comic.Core.Models;
using Comic.Core.Security;
using Comic.Core.Storage;
using Comic.Infrastructure.Downloads;
using Comic.Infrastructure.Library;

namespace Comic.Desktop.ViewModels;

public sealed class MainWindowViewModel(
    IComicSourceClient sourceClient,
    ISequentialDownloadService downloadService,
    ComicLibraryScanner libraryScanner,
    JsonReadingHistoryStore historyStore,
    JsonDownloadHistoryStore downloadHistoryStore) : ObservableObject
{
    private static readonly HashSet<string> DownloadedPageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".avif"
    };

    private readonly IComicSourceClient _sourceClient = sourceClient;
    private readonly ISequentialDownloadService _downloadService = downloadService;
    private readonly JsonDownloadHistoryStore _downloadHistoryStore = downloadHistoryStore;
    private CancellationTokenSource? _operationCancellation;
    private ComicInfo? _comic;
    private string _mangaUrl = "https://m.happymh.com/manga/butiange";
    private string _libraryRoot = LibraryPathPolicy.DefaultLibraryRoot;
    private string _comicTitle = "尚未載入漫畫";
    private string _comicSummary = "輸入 HappyMH 漫畫詳情網址，載入後即可選擇章節。";
    private string _statusText = "就緒";
    private DownloadMode _selectedDownloadMode = DownloadMode.Safe;
    private bool _isBusy;
    private double _downloadPercent;

    public ObservableCollection<ChapterItemViewModel> Chapters { get; } = [];

    public ObservableCollection<DownloadHistoryItemViewModel> DownloadHistory { get; } = [];

    public IReadOnlyList<DownloadModeItemViewModel> DownloadModes { get; } =
        DownloadModeItemViewModel.CreateDefaults();

    public ReaderViewModel Reader { get; } = new(libraryScanner, historyStore);

    public string MangaUrl
    {
        get => _mangaUrl;
        set => SetProperty(ref _mangaUrl, value);
    }

    public string LibraryRoot
    {
        get => _libraryRoot;
        set => SetProperty(ref _libraryRoot, value);
    }

    public string ComicTitle
    {
        get => _comicTitle;
        private set => SetProperty(ref _comicTitle, value);
    }

    public string ComicSummary
    {
        get => _comicSummary;
        private set => SetProperty(ref _comicSummary, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public DownloadMode SelectedDownloadMode
    {
        get => _selectedDownloadMode;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (SetProperty(ref _selectedDownloadMode, value))
            {
                OnPropertyChanged(nameof(DownloadModeDescription));
            }
        }
    }

    public string DownloadModeDescription => DownloadModes
        .First(item => item.Mode == SelectedDownloadMode)
        .Description;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanStartOperation));
                OnPropertyChanged(nameof(CanDownload));
                OnPropertyChanged(nameof(CanCancel));
            }
        }
    }

    public double DownloadPercent
    {
        get => _downloadPercent;
        private set => SetProperty(ref _downloadPercent, value);
    }

    public bool CanStartOperation => !IsBusy;

    public bool CanDownload => !IsBusy && _comic is not null && Chapters.Count > 0;

    public bool CanCancel => IsBusy;

    public async Task LoadComicAsync()
    {
        if (IsBusy)
        {
            return;
        }

        Uri sourceUri;
        try
        {
            sourceUri = SourceUrlPolicy.ParseHappyMhMangaUrl(MangaUrl);
        }
        catch (ArgumentException exception)
        {
            StatusText = exception.Message;
            return;
        }

        StartOperation();
        StatusText = "正在連線並載入漫畫資料…";
        DownloadPercent = 0;

        try
        {
            var comic = await _sourceClient.LoadComicAsync(
                sourceUri,
                _operationCancellation!.Token);
            ApplyComic(comic);
            StatusText = "漫畫資料載入完成。";
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消載入。";
        }
        catch (SourceAccessException exception)
        {
            StatusText = $"{exception.Message} 可改用「手動驗證…」由你親自完成網站驗證。";
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidDataException)
        {
            StatusText = $"載入失敗：{exception.Message}";
        }
        finally
        {
            FinishOperation();
            OnPropertyChanged(nameof(CanDownload));
        }
    }

    public async Task DownloadSelectedAsync()
    {
        if (IsBusy || _comic is null)
        {
            return;
        }

        var selected = Chapters
            .Where(item => item.IsSelected)
            .Select(item => item.Chapter)
            .ToArray();

        if (selected.Length == 0)
        {
            StatusText = "請至少勾選一個章節。";
            return;
        }

        if (string.IsNullOrWhiteSpace(LibraryRoot))
        {
            StatusText = "請先選擇漫畫庫資料夾。";
            return;
        }

        StartOperation();
        DownloadPercent = 0;
        var selectedModeName = DownloadModes
            .First(item => item.Mode == SelectedDownloadMode)
            .DisplayName;
        StatusText = $"準備以{selectedModeName}模式下載 {selected.Length} 個章節…";
        var progress = new Progress<DownloadProgress>(UpdateDownloadProgress);

        try
        {
            var summary = await _downloadService.DownloadSelectedAsync(
                _comic,
                selected,
                LibraryRoot,
                progress,
                _operationCancellation!.Token,
                SelectedDownloadMode);

            DownloadPercent = 100;
            StatusText = summary.Errors.Count == 0
                ? $"完成：{summary.CompletedChapters} 章，下載 {summary.DownloadedPages} 頁，略過 {summary.SkippedPages} 頁。"
                : $"部分完成：{summary.CompletedChapters} 章；{summary.Errors.Count} 個錯誤。第一個錯誤：{summary.Errors[0]}";
            var historySaved = SaveDownloadHistory(summary.Chapters);
            RefreshDownloadState();
            if (!historySaved)
            {
                StatusText += " 下載已完成，但下載歷史寫入失敗。";
            }

            Reader.LoadLibrary(LibraryRoot);
        }
        catch (OperationCanceledException)
        {
            StatusText = "下載已取消；已完成的圖片會保留，下次可續跑。";
        }
        finally
        {
            FinishOperation();
        }
    }

    public void SelectAll(bool isSelected)
    {
        if (IsBusy)
        {
            return;
        }

        foreach (var chapter in Chapters)
        {
            chapter.IsSelected = isSelected;
        }

        StatusText = isSelected ? "已選取全部章節。" : "已清除章節選取。";
    }

    public void CancelCurrentOperation() => _operationCancellation?.Cancel();

    public void ImportVerifiedComic(ComicInfo comic, int importedCookieCount)
    {
        ArgumentNullException.ThrowIfNull(comic);
        ApplyComic(comic);
        MangaUrl = comic.SourceUri.AbsoluteUri;
        StatusText = importedCookieCount > 0
            ? $"手動驗證完成，已載入漫畫並套用 {importedCookieCount} 個工作階段 Cookie。"
            : "已從驗證視窗載入漫畫；網站未提供可匯入的工作階段 Cookie。";
    }

    public void ReportStatus(string message)
    {
        StatusText = string.IsNullOrWhiteSpace(message) ? "就緒" : message;
    }

    private void StartOperation()
    {
        _operationCancellation?.Dispose();
        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
    }

    private void FinishOperation()
    {
        IsBusy = false;
        _operationCancellation?.Dispose();
        _operationCancellation = null;
    }

    private void ApplyComic(ComicInfo comic)
    {
        _comic = comic;
        ComicTitle = comic.Title;
        RefreshDownloadState();
        OnPropertyChanged(nameof(CanDownload));
    }

    private void RefreshDownloadState()
    {
        Chapters.Clear();
        DownloadHistory.Clear();
        if (_comic is null || string.IsNullOrWhiteSpace(LibraryRoot))
        {
            OnPropertyChanged(nameof(CanDownload));
            return;
        }

        var libraryRoot = Path.GetFullPath(LibraryRoot);
        var uniqueChapters = LibraryPathPolicy.CreateUniqueChapterFolders(_comic.Chapters);
        var localPageCounts = uniqueChapters.ToDictionary(
            chapter => LibraryPathPolicy.GetChapterFolderName(chapter),
            chapter => CountLocalPages(
                libraryRoot,
                _comic.Id,
                LibraryPathPolicy.GetChapterFolderName(chapter)),
            StringComparer.OrdinalIgnoreCase);

        var history = _downloadHistoryStore.LoadAll();
        SeedExistingLibraryHistory(libraryRoot, localPageCounts, history);
        history = _downloadHistoryStore.LoadAll();

        var completedChapterIds = DownloadHistoryMatcher.GetCompletedChapterIds(
            libraryRoot,
            _comic.Id,
            history,
            localPageCounts).ToHashSet(StringComparer.Ordinal);

        foreach (var chapter in uniqueChapters.Where(chapter => !completedChapterIds.Contains(chapter.Id)))
        {
            Chapters.Add(new ChapterItemViewModel(chapter));
        }

        foreach (var entry in history
                     .Where(entry => MatchesCurrentLibrary(entry, libraryRoot, _comic.Id))
                     .OrderByDescending(entry => entry.CompletedAt))
        {
            localPageCounts.TryGetValue(entry.ChapterFolderName, out var localPageCount);
            DownloadHistory.Add(new DownloadHistoryItemViewModel(entry, localPageCount));
        }

        ComicSummary = $"共 {uniqueChapters.Count} 個章節；可下載 {Chapters.Count} 個，已記錄完成 {DownloadHistory.Count} 個。";
        OnPropertyChanged(nameof(CanDownload));
    }

    private void SeedExistingLibraryHistory(
        string libraryRoot,
        IReadOnlyDictionary<string, int> localPageCounts,
        IReadOnlyList<DownloadHistoryEntry> history)
    {
        if (_comic is null)
        {
            return;
        }

        foreach (var chapter in LibraryPathPolicy.CreateUniqueChapterFolders(_comic.Chapters))
        {
            var folderName = LibraryPathPolicy.GetChapterFolderName(chapter);
            if (!localPageCounts.TryGetValue(folderName, out var pageCount) ||
                pageCount == 0 ||
                history.Any(entry =>
                    MatchesCurrentLibrary(entry, libraryRoot, _comic.Id) &&
                    entry.ChapterId.Equals(chapter.Id, StringComparison.Ordinal)))
            {
                continue;
            }

            _downloadHistoryStore.Save(new DownloadHistoryEntry(
                libraryRoot,
                _comic.Id,
                _comic.Title,
                chapter.Id,
                chapter.Title,
                folderName,
                pageCount,
                DateTimeOffset.UtcNow));
        }
    }

    private bool SaveDownloadHistory(IReadOnlyList<DownloadChapterResult> chapters)
    {
        if (_comic is null)
        {
            return false;
        }

        try
        {
            foreach (var chapter in chapters)
            {
                _downloadHistoryStore.Save(new DownloadHistoryEntry(
                    Path.GetFullPath(LibraryRoot),
                    _comic.Id,
                    _comic.Title,
                    chapter.ChapterId,
                    chapter.ChapterTitle,
                    chapter.ChapterFolderName,
                    chapter.PageCount,
                    chapter.CompletedAt));
            }

            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private static int CountLocalPages(string libraryRoot, string comicId, string chapterFolderName)
    {
        var directory = Path.Combine(libraryRoot, comicId, chapterFolderName);
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Count(path => DownloadedPageExtensions.Contains(Path.GetExtension(path)));
    }

    private static bool MatchesCurrentLibrary(
        DownloadHistoryEntry entry,
        string libraryRoot,
        string comicId) =>
        Path.GetFullPath(entry.LibraryRoot).Equals(libraryRoot, StringComparison.OrdinalIgnoreCase) &&
        entry.ComicId.Equals(comicId, StringComparison.OrdinalIgnoreCase);

    private void UpdateDownloadProgress(DownloadProgress progress)
    {
        StatusText = progress.Message;

        if (progress.ChapterCount == 0)
        {
            DownloadPercent = 0;
            return;
        }

        var pageFraction = progress.PageCount > 0
            ? Math.Clamp((double)progress.PageNumber / progress.PageCount, 0, 1)
            : 0;
        DownloadPercent = Math.Clamp(
            100d * (progress.ChapterNumber - 1 + pageFraction) / progress.ChapterCount,
            0,
            100);
    }
}
