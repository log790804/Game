using Comic.Core.Models;

namespace Comic.Desktop.ViewModels;

public sealed class DownloadHistoryItemViewModel(
    DownloadHistoryEntry entry,
    int localPageCount)
{
    public DownloadHistoryEntry Entry { get; } = entry;

    public string Title => $"{Entry.ChapterFolderName} · {Entry.ChapterTitle}";

    public string Details => localPageCount >= Entry.PageCount
        ? $"{Entry.PageCount} 張 · {Entry.CompletedAt.ToLocalTime():yyyy/MM/dd HH:mm}"
        : $"紀錄 {Entry.PageCount} 張，本機僅 {localPageCount} 張 · 可重新下載";
}
