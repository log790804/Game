using Comic.Core.Selection;

namespace Comic.Tests;

public sealed class ReaderSequenceNavigatorTests
{
    private static readonly IReadOnlyList<int> PageCounts = [2, 3, 1];

    [Fact]
    public void MoveNext_AtChapterEnd_ContinuesAtFirstPageOfNextChapter()
    {
        var result = ReaderSequenceNavigator.MoveNext(PageCounts, new ReaderLocation(0, 1));

        Assert.Equal(new ReaderLocation(1, 0), result);
    }

    [Fact]
    public void MovePrevious_AtChapterStart_ContinuesAtLastPageOfPreviousChapter()
    {
        var result = ReaderSequenceNavigator.MovePrevious(PageCounts, new ReaderLocation(1, 0));

        Assert.Equal(new ReaderLocation(0, 1), result);
    }

    [Fact]
    public void MoveNext_AtLibraryEnd_RemainsAtLastPage()
    {
        var current = new ReaderLocation(2, 0);

        var result = ReaderSequenceNavigator.MoveNext(PageCounts, current);

        Assert.Equal(current, result);
        Assert.False(ReaderSequenceNavigator.CanMoveNext(PageCounts, current));
    }
}
