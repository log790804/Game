namespace Comic.Core.Models;

public sealed record DownloadHistoryEntry(
    string LibraryRoot,
    string ComicId,
    string ComicTitle,
    string ChapterId,
    string ChapterTitle,
    string ChapterFolderName,
    int PageCount,
    DateTimeOffset CompletedAt);
