using Comic.Core.Models;

namespace Comic.Core.Selection;

public static class OrderedChapterSelection
{
    public static IReadOnlyList<ChapterInfo> Create(IEnumerable<ChapterInfo> chapters)
    {
        ArgumentNullException.ThrowIfNull(chapters);

        return chapters
            .GroupBy(chapter => chapter.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(chapter => chapter.Sequence)
            .ThenBy(chapter => chapter.Id, StringComparer.Ordinal)
            .ToArray();
    }
}

