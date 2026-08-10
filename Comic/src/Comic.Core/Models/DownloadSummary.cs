namespace Comic.Core.Models;

public sealed record DownloadSummary(
    int CompletedChapters,
    int DownloadedPages,
    int SkippedPages,
    IReadOnlyList<string> Errors,
    IReadOnlyList<DownloadChapterResult> Chapters);
