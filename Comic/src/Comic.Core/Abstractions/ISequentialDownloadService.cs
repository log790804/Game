using Comic.Core.Models;

namespace Comic.Core.Abstractions;

public interface ISequentialDownloadService
{
    Task<DownloadSummary> DownloadSelectedAsync(
        ComicInfo comic,
        IEnumerable<ChapterInfo> selectedChapters,
        string libraryRoot,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken,
        DownloadMode downloadMode = DownloadMode.Safe);
}
