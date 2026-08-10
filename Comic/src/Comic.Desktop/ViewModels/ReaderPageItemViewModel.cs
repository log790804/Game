using System.IO;
using System.Windows.Media.Imaging;

namespace Comic.Desktop.ViewModels;

public sealed class ReaderPageItemViewModel
{
    public ReaderPageItemViewModel(string filePath, int pageNumber)
    {
        FilePath = filePath;
        PageNumber = pageNumber;
        PageLabel = $"第 {pageNumber} 頁";

        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            var image = BitmapFrame.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            image.Freeze();
            Image = image;
            ErrorMessage = string.Empty;
        }
        catch (Exception exception) when (
            exception is IOException or NotSupportedException or ArgumentException)
        {
            ErrorMessage = $"無法開啟 {Path.GetFileName(filePath)}：{exception.Message}";
        }
    }

    public string FilePath { get; }

    public int PageNumber { get; }

    public string PageLabel { get; }

    public BitmapSource? Image { get; }

    public string ErrorMessage { get; }
}
