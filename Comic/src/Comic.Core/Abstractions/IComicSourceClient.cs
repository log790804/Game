using Comic.Core.Models;

namespace Comic.Core.Abstractions;

public interface IComicSourceClient
{
    Task<ComicInfo> LoadComicAsync(Uri sourceUri, CancellationToken cancellationToken);

    Task<IReadOnlyList<ComicPage>> LoadChapterPagesAsync(
        ChapterInfo chapter,
        CancellationToken cancellationToken);
}

