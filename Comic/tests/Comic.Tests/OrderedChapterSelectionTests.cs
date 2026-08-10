using Comic.Core.Models;
using Comic.Core.Selection;

namespace Comic.Tests;

public sealed class OrderedChapterSelectionTests
{
    [Fact]
    public void Create_SortsBySequenceAndRemovesDuplicateChapterIds()
    {
        var chapters = new[]
        {
            new ChapterInfo("chapter-10", "第 10 話", new Uri("https://m.happymh.com/mangaread/demo/chapter-10"), 10),
            new ChapterInfo("chapter-2", "第 2 話", new Uri("https://m.happymh.com/mangaread/demo/chapter-2"), 2),
            new ChapterInfo("chapter-2", "第 2 話（重複）", new Uri("https://m.happymh.com/mangaread/demo/chapter-2"), 2)
        };

        var result = OrderedChapterSelection.Create(chapters);

        Assert.Collection(
            result,
            chapter => Assert.Equal("chapter-2", chapter.Id),
            chapter => Assert.Equal("chapter-10", chapter.Id));
    }
}

