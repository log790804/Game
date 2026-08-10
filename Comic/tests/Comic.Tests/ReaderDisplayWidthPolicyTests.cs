using Comic.Core.Selection;

namespace Comic.Tests;

public sealed class ReaderDisplayWidthPolicyTests
{
    [Fact]
    public void Defaults_AreComfortableForVerticalMangaOnLandscapeScreens()
    {
        Assert.Equal(480, ReaderDisplayWidthPolicy.Minimum);
        Assert.Equal(760, ReaderDisplayWidthPolicy.Default);
        Assert.Equal(1200, ReaderDisplayWidthPolicy.Maximum);
        Assert.Equal(80, ReaderDisplayWidthPolicy.Step);
    }

    [Theory]
    [InlineData(200, 480)]
    [InlineData(760, 760)]
    [InlineData(1600, 1200)]
    public void Clamp_ConstrainsWidthToSupportedRange(double width, double expected)
    {
        Assert.Equal(expected, ReaderDisplayWidthPolicy.Clamp(width));
    }

    [Fact]
    public void IncreaseAndDecrease_StayWithinSupportedRange()
    {
        Assert.Equal(480, ReaderDisplayWidthPolicy.Decrease(480));
        Assert.Equal(680, ReaderDisplayWidthPolicy.Decrease(760));
        Assert.Equal(840, ReaderDisplayWidthPolicy.Increase(760));
        Assert.Equal(1200, ReaderDisplayWidthPolicy.Increase(1200));
    }
}
