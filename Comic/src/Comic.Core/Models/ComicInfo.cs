namespace Comic.Core.Models;

public sealed record ComicInfo(
    string Id,
    string Title,
    Uri SourceUri,
    IReadOnlyList<ChapterInfo> Chapters);

