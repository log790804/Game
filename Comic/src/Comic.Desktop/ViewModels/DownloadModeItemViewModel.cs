using Comic.Core.Models;

namespace Comic.Desktop.ViewModels;

public sealed record DownloadModeItemViewModel(
    DownloadMode Mode,
    string DisplayName,
    string Description)
{
    public static IReadOnlyList<DownloadModeItemViewModel> CreateDefaults() =>
    [
        new(
            DownloadMode.Safe,
            "安全",
            "單通道、800ms 間隔，優先使用驗證瀏覽器；相容性最高。"),
        new(
            DownloadMode.Standard,
            "標準",
            "最多同時 2 張、350ms 間隔；速度與網站負載較平衡。"),
        new(
            DownloadMode.Fast,
            "快速",
            "最多同時 3 張、150ms 間隔；較容易觸發網站驗證。")
    ];
}
