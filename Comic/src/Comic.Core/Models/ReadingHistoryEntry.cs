namespace Comic.Core.Models;

public sealed record ReadingHistoryEntry(
    string ComicId,
    string ChapterId,
    string PageFileName,
    DateTimeOffset UpdatedAt);
