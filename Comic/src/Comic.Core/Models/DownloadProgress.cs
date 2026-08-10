namespace Comic.Core.Models;

public sealed record DownloadProgress(
    string ChapterId,
    string ChapterTitle,
    int ChapterNumber,
    int ChapterCount,
    int PageNumber,
    int PageCount,
    DownloadProgressState State,
    string Message);

public enum DownloadProgressState
{
    StartingChapter,
    Downloading,
    Skipped,
    CompletedChapter,
    Failed
}

