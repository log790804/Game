namespace Comic.Core.Models;

public sealed record OfflineComicInfo(
    string Id,
    string DirectoryPath,
    IReadOnlyList<OfflineChapterInfo> Chapters,
    string? Title = null)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Title) ? Id : Title;
}
