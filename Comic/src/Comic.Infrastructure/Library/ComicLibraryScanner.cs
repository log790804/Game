using System.Text.RegularExpressions;
using Comic.Core.Models;

namespace Comic.Infrastructure.Library;

public sealed partial class ComicLibraryScanner(JsonComicMetadataStore? metadataStore = null)
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif"
    };

    private readonly JsonComicMetadataStore _metadataStore = metadataStore ?? new JsonComicMetadataStore();

    public IReadOnlyList<OfflineComicInfo> Scan(string libraryRoot)
    {
        if (string.IsNullOrWhiteSpace(libraryRoot))
        {
            throw new ArgumentException("漫畫庫路徑不可為空白。", nameof(libraryRoot));
        }

        var root = Path.GetFullPath(libraryRoot);
        if (!Directory.Exists(root))
        {
            return [];
        }

        return new DirectoryInfo(root)
            .EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
            .Where(IsSafeDirectory)
            .Select(CreateComic)
            .Where(comic => comic is not null)
            .Cast<OfflineComicInfo>()
            .OrderBy(comic => comic.Id, NaturalStringComparer.Instance)
            .ToArray();
    }

    private OfflineComicInfo? CreateComic(DirectoryInfo comicDirectory)
    {
        var chapterDirectories = comicDirectory
            .EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
            .Where(IsSafeDirectory)
            .ToArray();
        if (chapterDirectories.Any(directory => CanonicalChapterFolderRegex().IsMatch(directory.Name)))
        {
            chapterDirectories = chapterDirectories
                .Where(directory => !LegacyNumericChapterFolderRegex().IsMatch(directory.Name))
                .ToArray();
        }

        var chapters = chapterDirectories
            .Select(CreateChapter)
            .Where(chapter => chapter is not null)
            .Cast<OfflineChapterInfo>()
            .OrderBy(chapter => chapter.Id, NaturalStringComparer.Instance)
            .ToArray();

        if (chapters.Length == 0)
        {
            return null;
        }

        var metadata = _metadataStore.Load(comicDirectory.FullName);
        return new OfflineComicInfo(
            comicDirectory.Name,
            comicDirectory.FullName,
            chapters,
            metadata?.Title);
    }

    private static OfflineChapterInfo? CreateChapter(DirectoryInfo chapterDirectory)
    {
        var pages = chapterDirectory
            .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
            .Where(file =>
                (file.Attributes & FileAttributes.ReparsePoint) == 0 &&
                SupportedExtensions.Contains(file.Extension))
            .OrderBy(file => file.Name, NaturalStringComparer.Instance)
            .Select(file => file.FullName)
            .ToArray();

        return pages.Length == 0
            ? null
            : new OfflineChapterInfo(chapterDirectory.Name, chapterDirectory.FullName, pages);
    }

    private static bool IsSafeDirectory(DirectoryInfo directory) =>
        (directory.Attributes & FileAttributes.ReparsePoint) == 0 &&
        SafeIdentifierRegex().IsMatch(directory.Name);

    [GeneratedRegex("^(?:[A-Za-z0-9_-]{1,120}|第[1-9][0-9]{0,5}回)$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdentifierRegex();

    [GeneratedRegex("^第[1-9][0-9]{0,5}回$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalChapterFolderRegex();

    [GeneratedRegex("^[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex LegacyNumericChapterFolderRegex();

    private sealed class NaturalStringComparer : IComparer<string>
    {
        public static NaturalStringComparer Instance { get; } = new();

        public int Compare(string? left, string? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            var leftIndex = 0;
            var rightIndex = 0;
            while (leftIndex < left.Length && rightIndex < right.Length)
            {
                if (char.IsDigit(left[leftIndex]) && char.IsDigit(right[rightIndex]))
                {
                    var leftStart = leftIndex;
                    var rightStart = rightIndex;
                    while (leftIndex < left.Length && char.IsDigit(left[leftIndex])) leftIndex++;
                    while (rightIndex < right.Length && char.IsDigit(right[rightIndex])) rightIndex++;

                    var leftDigits = left.AsSpan(leftStart, leftIndex - leftStart).TrimStart('0');
                    var rightDigits = right.AsSpan(rightStart, rightIndex - rightStart).TrimStart('0');
                    var lengthComparison = leftDigits.Length.CompareTo(rightDigits.Length);
                    if (lengthComparison != 0)
                    {
                        return lengthComparison;
                    }

                    var digitComparison = leftDigits.CompareTo(rightDigits, StringComparison.Ordinal);
                    if (digitComparison != 0)
                    {
                        return digitComparison;
                    }

                    continue;
                }

                var characterComparison = char.ToUpperInvariant(left[leftIndex])
                    .CompareTo(char.ToUpperInvariant(right[rightIndex]));
                if (characterComparison != 0)
                {
                    return characterComparison;
                }

                leftIndex++;
                rightIndex++;
            }

            return left.Length.CompareTo(right.Length);
        }
    }
}
