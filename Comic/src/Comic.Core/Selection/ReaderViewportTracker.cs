namespace Comic.Core.Selection;

public readonly record struct PageViewportBounds(double Top, double Height);

public static class ReaderViewportTracker
{
    public static int FindMostVisiblePage(
        IReadOnlyList<PageViewportBounds> pages,
        double viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(pages);
        if (viewportHeight <= 0)
        {
            return -1;
        }

        var result = -1;
        var largestVisibleHeight = 0d;
        for (var index = 0; index < pages.Count; index++)
        {
            var page = pages[index];
            if (page.Height <= 0)
            {
                continue;
            }

            var visibleTop = Math.Max(page.Top, 0);
            var visibleBottom = Math.Min(page.Top + page.Height, viewportHeight);
            var visibleHeight = Math.Max(visibleBottom - visibleTop, 0);
            if (visibleHeight > largestVisibleHeight)
            {
                largestVisibleHeight = visibleHeight;
                result = index;
            }
        }

        return result;
    }
}
