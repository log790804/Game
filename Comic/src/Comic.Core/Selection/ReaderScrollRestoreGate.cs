namespace Comic.Core.Selection;

public sealed class ReaderScrollRestoreGate
{
    public bool CanTrackScroll { get; private set; }

    public void BeginRestore() => CanTrackScroll = false;

    public void CompleteRestore() => CanTrackScroll = true;
}
