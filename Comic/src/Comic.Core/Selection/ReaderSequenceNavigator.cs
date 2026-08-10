namespace Comic.Core.Selection;

public readonly record struct ReaderLocation(int ChapterIndex, int PageIndex);

public static class ReaderSequenceNavigator
{
    public static ReaderLocation MoveNext(
        IReadOnlyList<int> pageCounts,
        ReaderLocation current)
    {
        Validate(pageCounts, current);
        if (current.PageIndex + 1 < pageCounts[current.ChapterIndex])
        {
            return current with { PageIndex = current.PageIndex + 1 };
        }

        return current.ChapterIndex + 1 < pageCounts.Count
            ? new ReaderLocation(current.ChapterIndex + 1, 0)
            : current;
    }

    public static ReaderLocation MovePrevious(
        IReadOnlyList<int> pageCounts,
        ReaderLocation current)
    {
        Validate(pageCounts, current);
        if (current.PageIndex > 0)
        {
            return current with { PageIndex = current.PageIndex - 1 };
        }

        if (current.ChapterIndex == 0)
        {
            return current;
        }

        var previousChapter = current.ChapterIndex - 1;
        return new ReaderLocation(previousChapter, pageCounts[previousChapter] - 1);
    }

    public static bool CanMoveNext(IReadOnlyList<int> pageCounts, ReaderLocation current) =>
        MoveNext(pageCounts, current) != current;

    public static bool CanMovePrevious(IReadOnlyList<int> pageCounts, ReaderLocation current) =>
        MovePrevious(pageCounts, current) != current;

    private static void Validate(IReadOnlyList<int> pageCounts, ReaderLocation current)
    {
        ArgumentNullException.ThrowIfNull(pageCounts);
        if (pageCounts.Count == 0 || pageCounts.Any(count => count < 1))
        {
            throw new ArgumentException("每個章節必須至少包含一頁。", nameof(pageCounts));
        }

        if (current.ChapterIndex < 0 || current.ChapterIndex >= pageCounts.Count ||
            current.PageIndex < 0 || current.PageIndex >= pageCounts[current.ChapterIndex])
        {
            throw new ArgumentOutOfRangeException(nameof(current));
        }
    }
}
