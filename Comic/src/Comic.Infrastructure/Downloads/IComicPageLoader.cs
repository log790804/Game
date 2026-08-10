using Comic.Core.Models;

namespace Comic.Infrastructure.Downloads;

public sealed record ComicPageContent(string MediaType, byte[] Bytes);

public interface IComicPageLoader
{
    Task<ComicPageContent> LoadAsync(
        ChapterInfo chapter,
        ComicPage page,
        long maxBytes,
        CancellationToken cancellationToken);
}
