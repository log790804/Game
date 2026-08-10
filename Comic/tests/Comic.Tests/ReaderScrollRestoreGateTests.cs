using Comic.Core.Selection;

namespace Comic.Tests;

public sealed class ReaderScrollRestoreGateTests
{
    [Fact]
    public void Restart_BlocksInitialScrollEventsUntilSavedPositionIsRestored()
    {
        var gate = new ReaderScrollRestoreGate();

        Assert.False(gate.CanTrackScroll);

        gate.CompleteRestore();

        Assert.True(gate.CanTrackScroll);
    }

    [Fact]
    public void BeginRestore_BlocksScrollTrackingAgainWhenChapterLayoutChanges()
    {
        var gate = new ReaderScrollRestoreGate();
        gate.CompleteRestore();

        gate.BeginRestore();

        Assert.False(gate.CanTrackScroll);
    }
}
