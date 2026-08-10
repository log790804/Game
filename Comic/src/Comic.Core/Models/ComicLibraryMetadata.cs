namespace Comic.Core.Models;

public sealed record ComicLibraryMetadata(
    int SchemaVersion,
    string ComicId,
    string Title,
    DateTimeOffset UpdatedAt);
