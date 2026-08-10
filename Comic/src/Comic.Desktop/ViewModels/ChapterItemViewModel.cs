using Comic.Core.Models;

namespace Comic.Desktop.ViewModels;

public sealed class ChapterItemViewModel(ChapterInfo chapter) : ObservableObject
{
    private bool _isSelected;

    public ChapterInfo Chapter { get; } = chapter;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Title => Chapter.Title;

    public int Sequence => Chapter.Sequence;
}

