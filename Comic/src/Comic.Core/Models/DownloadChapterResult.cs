namespace Comic.Core.Models;

public sealed record DownloadChapterResult(
    string ChapterId,
    string ChapterTitle,
    string ChapterFolderName,
    int PageCount,
    int DownloadedPages,
    int SkippedPages,
    DateTimeOffset CompletedAt);
