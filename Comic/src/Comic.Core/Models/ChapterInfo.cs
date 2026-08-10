namespace Comic.Core.Models;

public sealed record ChapterInfo(
    string Id,
    string Title,
    Uri SourceUri,
    int Sequence);

