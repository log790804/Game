using Comic.Core.Selection;

namespace Comic.Tests;

public sealed class ReaderViewportTrackerTests
{
    [Fact]
    public void FindMostVisiblePage_ReturnsPageWithLargestVisibleArea()
    {
        var pages = new[]
        {
            new PageViewportBounds(-700, 1000),
            new PageViewportBounds(300, 1000),
            new PageViewportBounds(1300, 1000)
        };

        var result = ReaderViewportTracker.FindMostVisiblePage(pages, viewportHeight: 800);

        Assert.Equal(1, result);
    }

    [Fact]
    public void FindMostVisiblePage_ReturnsMinusOneWhenNothingIsVisible()
    {
        var pages = new[]
        {
            new PageViewportBounds(-1000, 300),
            new PageViewportBounds(900, 300)
        };

        var result = ReaderViewportTracker.FindMostVisiblePage(pages, viewportHeight: 800);

        Assert.Equal(-1, result);
    }
}
