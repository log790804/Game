using System.Text.RegularExpressions;

namespace Comic.Core.Security;

public static partial class SourceUrlPolicy
{
    private const string HappyMhHost = "m.happymh.com";
    private const int MaxComicIdLength = 120;

    public static Uri CreateHappyMhMangaUriFromComicId(string? comicId)
    {
        var normalizedId = comicId?.Trim() ?? string.Empty;
        if (normalizedId.Length is < 1 or > MaxComicIdLength ||
            !SafeIdentifierRegex().IsMatch(normalizedId))
        {
            throw new ArgumentException("本機漫畫資料夾名稱不是有效的 HappyMH 漫畫 ID。", nameof(comicId));
        }

        return ParseHappyMhMangaUrl(
            $"https://{HappyMhHost}/manga/{normalizedId}");
    }

    public static Uri ParseHappyMhMangaUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("請輸入有效的漫畫網址。", nameof(value));
        }

        EnsureSafeHttpsUri(uri, HappyMhHost);

        var segments = SplitPath(uri);
        if (segments.Length != 2 ||
            !segments[0].Equals("manga", StringComparison.OrdinalIgnoreCase) ||
            !SafeIdentifierRegex().IsMatch(segments[1]))
        {
            throw new ArgumentException("網址必須是 HappyMH 漫畫詳情頁。", nameof(value));
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("漫畫網址不可包含查詢參數或片段。", nameof(value));
        }

        return uri;
    }

    public static void EnsureAllowedHappyMhPage(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        EnsureSafeHttpsUri(uri, HappyMhHost);

        var segments = SplitPath(uri);
        var isManga = segments.Length == 2 &&
            segments[0].Equals("manga", StringComparison.OrdinalIgnoreCase);
        var isChapter = segments.Length == 3 &&
            (segments[0].Equals("mangaread", StringComparison.OrdinalIgnoreCase) ||
             segments[0].Equals("mangarcard", StringComparison.OrdinalIgnoreCase));

        if (!isManga && !isChapter)
        {
            throw new ArgumentException("不支援的 HappyMH 頁面網址。", nameof(uri));
        }
    }

    public static bool IsAllowedHappyMhAsset(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri ||
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        return uri.IdnHost.Equals("happymh.com", StringComparison.OrdinalIgnoreCase) ||
            uri.IdnHost.EndsWith(".happymh.com", StringComparison.OrdinalIgnoreCase);
    }

    public static string[] SplitPath(Uri uri) => uri.AbsolutePath
        .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(Uri.UnescapeDataString)
        .ToArray();

    private static void EnsureSafeHttpsUri(Uri uri, string expectedHost)
    {
        if (!uri.IsAbsoluteUri ||
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !uri.IdnHost.Equals(expectedHost, StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("僅允許安全的 HappyMH HTTPS 網址。", nameof(uri));
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdentifierRegex();
}
