using System.Collections.ObjectModel;
using System.IO;
using Comic.Core.Models;
using Comic.Core.Security;
using Comic.Core.Selection;
using Comic.Infrastructure.Library;

namespace Comic.Desktop.ViewModels;

public sealed class ReaderViewModel(
    ComicLibraryScanner libraryScanner,
    JsonReadingHistoryStore historyStore) : ObservableObject
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif"
    };

    private readonly ComicLibraryScanner _libraryScanner = libraryScanner;
    private readonly JsonReadingHistoryStore _historyStore = historyStore;
    private OfflineComicInfo? _selectedComic;
    private OfflineChapterInfo? _selectedChapter;
    private string _libraryPath = "尚未掃描漫畫庫";
    private string _chapterPath = "尚未選擇本機章節資料夾";
    private string _pageLabel = "0 / 0";
    private string _statusText = "掃描漫畫庫後即可從上次閱讀位置繼續。";
    private string _historyText = "尚無閱讀紀錄";
    private string _selectionHint = "尚無已下載漫畫可供選擇。";
    private double _pageMaxWidth = ReaderDisplayWidthPolicy.Default;
    private int _currentIndex = -1;
    private bool _isUpdatingSelection;

    public ObservableCollection<OfflineComicInfo> Comics { get; } = [];

    public ObservableCollection<OfflineChapterInfo> Chapters { get; } = [];

    public ObservableCollection<string> Pages { get; } = [];

    public ObservableCollection<ReaderPageItemViewModel> PageItems { get; } = [];

    public int CurrentPageIndex => _currentIndex;

    public OfflineComicInfo? SelectedComic
    {
        get => _selectedComic;
        set
        {
            if (!SetProperty(ref _selectedComic, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanCheckLatest));
            OnPropertyChanged(nameof(SelectedComicSourceUri));
            if (!_isUpdatingSelection)
            {
                PopulateSelectedComic(value, FindHistory(value?.Id));
            }
        }
    }

    public OfflineChapterInfo? SelectedChapter
    {
        get => _selectedChapter;
        set
        {
            if (SetProperty(ref _selectedChapter, value) && !_isUpdatingSelection)
            {
                var history = FindHistory(SelectedComic?.Id);
                var preferredPage = history?.ChapterId.Equals(
                    value?.Id,
                    StringComparison.OrdinalIgnoreCase) == true
                    ? history.PageFileName
                    : null;
                LoadChapter(value, preferredPage, preferredIndex: 0);
            }
        }
    }

    public string LibraryPath
    {
        get => _libraryPath;
        private set => SetProperty(ref _libraryPath, value);
    }

    public string ChapterPath
    {
        get => _chapterPath;
        private set => SetProperty(ref _chapterPath, value);
    }

    public string PageLabel
    {
        get => _pageLabel;
        private set => SetProperty(ref _pageLabel, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string HistoryText
    {
        get => _historyText;
        private set => SetProperty(ref _historyText, value);
    }

    public string SelectionHint
    {
        get => _selectionHint;
        private set => SetProperty(ref _selectionHint, value);
    }

    public double PageMaxWidth
    {
        get => _pageMaxWidth;
        set
        {
            if (SetProperty(ref _pageMaxWidth, ReaderDisplayWidthPolicy.Clamp(value)))
            {
                OnPropertyChanged(nameof(PageWidthLabel));
            }
        }
    }

    public string PageWidthLabel => $"{PageMaxWidth:0} px";

    public bool CanGoPrevious => SelectedChapter is null
        ? _currentIndex > 0
        : TryGetCurrentLocation(out var location) &&
          ReaderSequenceNavigator.CanMovePrevious(GetPageCounts(), location);

    public bool CanGoNext => SelectedChapter is null
        ? _currentIndex >= 0 && _currentIndex < Pages.Count - 1
        : TryGetCurrentLocation(out var location) &&
          ReaderSequenceNavigator.CanMoveNext(GetPageCounts(), location);

    public bool CanGoPreviousChapter => SelectedChapter is not null &&
        Chapters.IndexOf(SelectedChapter) > 0;

    public bool CanGoNextChapter => SelectedChapter is not null &&
        Chapters.IndexOf(SelectedChapter) >= 0 &&
        Chapters.IndexOf(SelectedChapter) < Chapters.Count - 1;

    public bool CanCheckLatest => SelectedComic is not null;

    public Uri? SelectedComicSourceUri => SelectedComic is null
        ? null
        : SourceUrlPolicy.CreateHappyMhMangaUriFromComicId(SelectedComic.Id);

    public void LoadLibrary(string libraryRoot)
    {
        try
        {
            var comics = _libraryScanner.Scan(libraryRoot);
            LibraryPath = Path.GetFullPath(libraryRoot);
            Comics.Clear();
            foreach (var comic in comics)
            {
                Comics.Add(comic);
            }

            if (Comics.Count == 0)
            {
                SetSelectedComic(null, history: null);
                StatusText = "漫畫庫目前沒有可閱讀的章節圖片。";
                HistoryText = "尚無可恢復的閱讀位置";
                return;
            }

            var histories = _historyStore.LoadAll();
            var latestHistory = histories.FirstOrDefault(history =>
                Comics.Any(comic => comic.Id.Equals(history.ComicId, StringComparison.OrdinalIgnoreCase)));
            var targetComic = latestHistory is null
                ? Comics[0]
                : Comics.First(comic => comic.Id.Equals(
                    latestHistory.ComicId,
                    StringComparison.OrdinalIgnoreCase));
            SetSelectedComic(targetComic, latestHistory);

            if (Pages.Count > 0)
            {
                StatusText = latestHistory is null
                ? $"已載入 {Comics.Count} 本漫畫。"
                : $"已恢復上次閱讀位置：{latestHistory.ComicId} / {latestHistory.ChapterId}。";
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ResetReader();
            StatusText = $"無法掃描漫畫庫：{exception.Message}";
        }
    }

    public void LoadChapterFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            StatusText = "找不到選取的章節資料夾。";
            return;
        }

        var pages = Directory
            .EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        SetSelectionWithoutLoading(comic: null, chapter: null);
        SetPages(pages, preferredPageName: null, preferredIndex: 0);
        ChapterPath = Path.GetFullPath(folderPath);
        StatusText = Pages.Count == 0
            ? "此資料夾沒有支援的圖片。"
            : $"已載入外部章節資料夾，共 {Pages.Count} 頁；此模式不會自動跨章。";
        HistoryText = "外部資料夾模式不寫入漫畫閱讀紀錄";
    }

    public void GoPrevious()
    {
        if (SelectedChapter is null)
        {
            if (_currentIndex > 0)
            {
                _currentIndex--;
                UpdateCurrentPageState();
            }

            return;
        }

        if (!TryGetCurrentLocation(out var current))
        {
            return;
        }

        var target = ReaderSequenceNavigator.MovePrevious(GetPageCounts(), current);
        MoveTo(target, target.ChapterIndex != current.ChapterIndex
            ? "已接續上一章。"
            : null);
    }

    public void GoNext()
    {
        if (SelectedChapter is null)
        {
            if (_currentIndex >= 0 && _currentIndex < Pages.Count - 1)
            {
                _currentIndex++;
                UpdateCurrentPageState();
            }

            return;
        }

        if (!TryGetCurrentLocation(out var current))
        {
            return;
        }

        var target = ReaderSequenceNavigator.MoveNext(GetPageCounts(), current);
        MoveTo(target, target.ChapterIndex != current.ChapterIndex
            ? "本章已讀完，已自動接續下一章。"
            : null);
    }

    public void GoPreviousChapter()
    {
        if (!CanGoPreviousChapter || SelectedChapter is null)
        {
            return;
        }

        var chapterIndex = Chapters.IndexOf(SelectedChapter) - 1;
        SetSelectedChapter(Chapters[chapterIndex], preferredPageName: null, preferredIndex: 0);
        StatusText = $"已切換至上一回：{SelectedChapter?.Id}。";
    }

    public void GoNextChapter()
    {
        if (!CanGoNextChapter || SelectedChapter is null)
        {
            return;
        }

        var chapterIndex = Chapters.IndexOf(SelectedChapter) + 1;
        SetSelectedChapter(Chapters[chapterIndex], preferredPageName: null, preferredIndex: 0);
        StatusText = $"本回已讀完，已接續下一回：{SelectedChapter?.Id}。";
    }

    public void DecreasePageWidth() =>
        PageMaxWidth = ReaderDisplayWidthPolicy.Decrease(PageMaxWidth);

    public void IncreasePageWidth() =>
        PageMaxWidth = ReaderDisplayWidthPolicy.Increase(PageMaxWidth);

    public void ResetPageWidth() =>
        PageMaxWidth = ReaderDisplayWidthPolicy.Default;

    public void SetCurrentPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= Pages.Count || pageIndex == _currentIndex)
        {
            return;
        }

        _currentIndex = pageIndex;
        UpdateCurrentPageState();
    }

    public void SaveCurrentPosition() => SaveHistory();

    private void SetSelectedComic(OfflineComicInfo? comic, ReadingHistoryEntry? history)
    {
        _isUpdatingSelection = true;
        SelectedComic = comic;
        _isUpdatingSelection = false;
        PopulateSelectedComic(comic, history);
    }

    private void PopulateSelectedComic(OfflineComicInfo? comic, ReadingHistoryEntry? history)
    {
        Chapters.Clear();
        if (comic is null)
        {
            SetSelectedChapter(null, preferredPageName: null, preferredIndex: -1);
            UpdateSelectionHint();
            return;
        }

        foreach (var chapter in comic.Chapters)
        {
            Chapters.Add(chapter);
        }

        var targetChapter = history is null
            ? Chapters[0]
            : Chapters.FirstOrDefault(chapter => chapter.Id.Equals(
                history.ChapterId,
                StringComparison.OrdinalIgnoreCase)) ?? Chapters[0];
        var preferredPage = targetChapter.Id.Equals(history?.ChapterId, StringComparison.OrdinalIgnoreCase)
            ? history?.PageFileName
            : null;
        SetSelectedChapter(targetChapter, preferredPage, preferredIndex: 0);
        UpdateSelectionHint();
    }

    private void SetSelectedChapter(
        OfflineChapterInfo? chapter,
        string? preferredPageName,
        int preferredIndex)
    {
        _isUpdatingSelection = true;
        SelectedChapter = chapter;
        _isUpdatingSelection = false;
        LoadChapter(chapter, preferredPageName, preferredIndex);
    }

    private void LoadChapter(
        OfflineChapterInfo? chapter,
        string? preferredPageName,
        int preferredIndex)
    {
        if (chapter is null)
        {
            Pages.Clear();
            PageItems.Clear();
            ChapterPath = "尚未選擇本機章節資料夾";
            _currentIndex = -1;
            UpdateCurrentPageState();
            return;
        }

        ChapterPath = chapter.DirectoryPath;
        SetPages(chapter.Pages, preferredPageName, preferredIndex);
        var failedPages = PageItems.Count(page => page.Image is null);
        StatusText = failedPages == 0
            ? $"漫畫 {SelectedComic?.Id} · 章節 {chapter.Id} · 共 {Pages.Count} 頁，可向下連續閱讀。"
            : $"漫畫 {SelectedComic?.Id} · 章節 {chapter.Id} · 共 {Pages.Count} 頁，其中 {failedPages} 頁無法顯示。";
    }

    private void SetPages(
        IEnumerable<string> pages,
        string? preferredPageName,
        int preferredIndex)
    {
        Pages.Clear();
        PageItems.Clear();
        foreach (var page in pages)
        {
            Pages.Add(page);
            PageItems.Add(new ReaderPageItemViewModel(page, PageItems.Count + 1));
        }

        var historyIndex = string.IsNullOrWhiteSpace(preferredPageName)
            ? -1
            : Pages
                .Select((page, index) => new { page, index })
                .FirstOrDefault(item => Path.GetFileName(item.page).Equals(
                    preferredPageName,
                    StringComparison.OrdinalIgnoreCase))?.index ?? -1;
        _currentIndex = Pages.Count == 0
            ? -1
            : historyIndex >= 0
                ? historyIndex
                : Math.Clamp(preferredIndex, 0, Pages.Count - 1);
        UpdateCurrentPageState();
    }

    private void MoveTo(ReaderLocation target, string? transitionMessage)
    {
        var chapter = Chapters[target.ChapterIndex];
        if (!ReferenceEquals(chapter, SelectedChapter))
        {
            SetSelectedChapter(chapter, preferredPageName: null, preferredIndex: target.PageIndex);
        }
        else
        {
            _currentIndex = target.PageIndex;
            UpdateCurrentPageState();
        }

        if (!string.IsNullOrWhiteSpace(transitionMessage))
        {
            StatusText = $"{transitionMessage} 現在閱讀章節 {chapter.Id}。";
        }
    }

    private bool TryGetCurrentLocation(out ReaderLocation location)
    {
        var chapterIndex = SelectedChapter is null ? -1 : Chapters.IndexOf(SelectedChapter);
        if (chapterIndex < 0 || _currentIndex < 0 || _currentIndex >= Pages.Count)
        {
            location = default;
            return false;
        }

        location = new ReaderLocation(chapterIndex, _currentIndex);
        return true;
    }

    private IReadOnlyList<int> GetPageCounts() => Chapters
        .Select(chapter => chapter.Pages.Count)
        .ToArray();

    private ReadingHistoryEntry? FindHistory(string? comicId)
    {
        if (string.IsNullOrWhiteSpace(comicId))
        {
            return null;
        }

        return _historyStore.LoadAll().FirstOrDefault(entry =>
            entry.ComicId.Equals(comicId, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateCurrentPageState()
    {
        OnPropertyChanged(nameof(CurrentPageIndex));

        if (_currentIndex < 0 || _currentIndex >= Pages.Count)
        {
            PageLabel = "0 / 0";
            NotifyNavigationState();
            return;
        }

        PageLabel = $"{_currentIndex + 1} / {Pages.Count}";
        SaveHistory();

        NotifyNavigationState();
    }

    private void SaveHistory()
    {
        if (SelectedComic is null || SelectedChapter is null ||
            _currentIndex < 0 || _currentIndex >= Pages.Count)
        {
            return;
        }

        try
        {
            var pageFileName = Path.GetFileName(Pages[_currentIndex]);
            _historyStore.Save(new ReadingHistoryEntry(
                SelectedComic.Id,
                SelectedChapter.Id,
                pageFileName,
                DateTimeOffset.UtcNow));
            HistoryText = $"已記錄：{SelectedComic.Id} / {SelectedChapter.Id} / 第 {_currentIndex + 1} 頁";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            HistoryText = $"閱讀進度未能保存：{exception.Message}";
        }
    }

    private void SetSelectionWithoutLoading(OfflineComicInfo? comic, OfflineChapterInfo? chapter)
    {
        _isUpdatingSelection = true;
        SelectedComic = comic;
        SelectedChapter = chapter;
        _isUpdatingSelection = false;
    }

    private void ResetReader()
    {
        Comics.Clear();
        Chapters.Clear();
        Pages.Clear();
        PageItems.Clear();
        SetSelectionWithoutLoading(comic: null, chapter: null);
        _currentIndex = -1;
        PageLabel = "0 / 0";
        UpdateSelectionHint();
        NotifyNavigationState();
    }

    private void UpdateSelectionHint()
    {
        SelectionHint = Comics.Count switch
        {
            0 => "尚無已下載漫畫可供選擇。",
            _ when Chapters.Count == 0 => $"目前有 {Comics.Count} 本漫畫；所選漫畫尚無可閱讀章節。",
            1 when Chapters.Count == 1 =>
                "目前只有 1 本漫畫、1 個已下載章節；下載更多內容後即可在此切換。",
            _ => $"可選 {Comics.Count} 本漫畫；目前漫畫有 {Chapters.Count} 個已下載章節。"
        };
    }

    private void NotifyNavigationState()
    {
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoPreviousChapter));
        OnPropertyChanged(nameof(CanGoNextChapter));
    }
}
