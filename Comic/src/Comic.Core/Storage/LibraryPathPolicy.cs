using System.Text.RegularExpressions;
using Comic.Core.Models;

namespace Comic.Core.Storage;

public static partial class LibraryPathPolicy
{
    public const string DefaultLibraryRoot = @"D:\Game\Game\Comic\Library";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif",
        ".avif"
    };

    public static string GetPagePath(
        string libraryRoot,
        string comicId,
        string chapterId,
        int pageNumber,
        string extension)
    {
        if (string.IsNullOrWhiteSpace(libraryRoot))
        {
            throw new ArgumentException("漫畫庫路徑不可為空白。", nameof(libraryRoot));
        }

        ValidateIdentifier(comicId, nameof(comicId));
        ValidateIdentifier(chapterId, nameof(chapterId));

        if (pageNumber is < 1 or > 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        var normalizedExtension = extension.StartsWith('.') ? extension : $".{extension}";
        if (!AllowedExtensions.Contains(normalizedExtension))
        {
            throw new ArgumentException("不支援的圖片副檔名。", nameof(extension));
        }

        var root = Path.GetFullPath(libraryRoot);
        var result = Path.GetFullPath(Path.Combine(
            root,
            comicId,
            chapterId,
            $"{pageNumber:0000}{normalizedExtension.ToLowerInvariant()}"));

        var rootBoundary = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!result.StartsWith(rootBoundary, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("產生的檔案路徑超出漫畫庫範圍。 ");
        }

        return result;
    }

    public static string GetPagePath(
        string libraryRoot,
        string comicId,
        ChapterInfo chapter,
        int pageNumber,
        string extension)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        return GetPagePath(
            libraryRoot,
            comicId,
            GetChapterFolderName(chapter),
            pageNumber,
            extension);
    }

    public static string GetChapterFolderName(ChapterInfo chapter)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ValidateIdentifier(chapter.Id, nameof(chapter));

        if (chapter.Sequence is >= 1 and < 1_000_000)
        {
            return $"第{chapter.Sequence}回";
        }

        return $"其他_{chapter.Id}";
    }

    public static IReadOnlyList<ChapterInfo> CreateUniqueChapterFolders(
        IEnumerable<ChapterInfo> chapters)
    {
        ArgumentNullException.ThrowIfNull(chapters);

        return chapters
            .GroupBy(GetChapterFolderName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(chapter =>
                    long.TryParse(chapter.Id, out var numericId) ? numericId : long.MinValue)
                .ThenByDescending(chapter => chapter.Id, StringComparer.Ordinal)
                .First())
            .OrderBy(chapter => chapter.Sequence)
            .ThenBy(chapter => chapter.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            (!SafeIdentifierRegex().IsMatch(value) && !ChapterFolderRegex().IsMatch(value)))
        {
            throw new ArgumentException("識別碼包含不安全字元。", parameterName);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,120}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdentifierRegex();

    [GeneratedRegex("^第[1-9][0-9]{0,5}回$", RegexOptions.CultureInvariant)]
    private static partial Regex ChapterFolderRegex();
}
