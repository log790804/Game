namespace Comic.Core.Models;

public sealed record OfflineChapterInfo(
    string Id,
    string DirectoryPath,
    IReadOnlyList<string> Pages)
{
    public string DisplayName => Id;
}
