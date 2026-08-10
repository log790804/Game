using System.Text.RegularExpressions;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Comic.Core.Models;
using Comic.Core.Security;
using Comic.Core.Selection;

namespace Comic.Infrastructure.HappyMh;

public sealed partial class HappyMhHtmlParser
{
    private const int MaxSnapshotCharacters = 2 * 1024 * 1024;
    private const int MaxSnapshotChapters = 5000;
    private const int MaxChapterPages = 10_000;
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly HtmlParser _parser = new();

    public ComicInfo ParseManga(string html, Uri sourceUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);
        SourceUrlPolicy.EnsureAllowedHappyMhPage(sourceUri);
        ThrowIfAccessBlocked(html);

        var document = _parser.ParseDocument(html);
        var sourceSegments = SourceUrlPolicy.SplitPath(sourceUri);
        var comicId = sourceSegments[1];
        var title = GetTitle(document);

        var chapters = document
            .QuerySelectorAll("a[href]")
            .Select((element, index) => TryCreateChapter(element, sourceUri, comicId, index))
            .Where(chapter => chapter is not null)
            .Cast<ChapterInfo>()
            .ToArray();

        if (chapters.Length == 0)
        {
            throw new InvalidDataException("漫畫頁面中找不到章節資料，網站可能已改版或要求人機驗證。");
        }

        return new ComicInfo(
            comicId,
            title,
            sourceUri,
            OrderedChapterSelection.Create(chapters));
    }

    public ComicInfo ParseMangaSnapshot(string snapshotJson, Uri sourceUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotJson);
        SourceUrlPolicy.EnsureAllowedHappyMhPage(sourceUri);
        if (snapshotJson.Length > MaxSnapshotCharacters)
        {
            throw new InvalidDataException("驗證頁面的章節快照過大。");
        }

        MangaSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<MangaSnapshot>(snapshotJson, SnapshotJsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("驗證頁面的章節快照格式無效。", exception);
        }

        var title = NormalizeWhitespace(snapshot?.Title);
        if (string.IsNullOrWhiteSpace(title) || title.Length > 500)
        {
            throw new InvalidDataException("驗證頁面缺少有效的漫畫標題。");
        }

        var sourceSegments = SourceUrlPolicy.SplitPath(sourceUri);
        var comicId = sourceSegments[1];
        var chapters = (snapshot?.Chapters ?? [])
            .Take(MaxSnapshotChapters)
            .Select((chapter, index) => TryCreateChapter(
                chapter.Title,
                chapter.Href,
                sourceUri,
                comicId,
                index))
            .Where(chapter => chapter is not null)
            .Cast<ChapterInfo>()
            .ToArray();

        if (chapters.Length == 0)
        {
            throw new InvalidDataException("尚未讀到章節清單，請等待頁面顯示章節後再試一次。");
        }

        return new ComicInfo(
            comicId,
            title,
            sourceUri,
            OrderedChapterSelection.Create(chapters));
    }

    public IReadOnlyList<ComicPage> ParseChapterPages(string html, Uri sourceUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);
        SourceUrlPolicy.EnsureAllowedHappyMhPage(sourceUri);
        ThrowIfAccessBlocked(html);

        var document = _parser.ParseDocument(html);
        var pages = new List<ComicPage>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var element in document.QuerySelectorAll("mip-img, img, source"))
        {
            var rawUrl = FirstNonEmptyAttribute(
                element,
                "data-src",
                "data-original",
                "data-lazy-src",
                "src");

            if (string.IsNullOrWhiteSpace(rawUrl) ||
                !Uri.TryCreate(sourceUri, rawUrl.Trim(), out var imageUri) ||
                !SourceUrlPolicy.IsAllowedHappyMhAsset(imageUri) ||
                !seen.Add(imageUri.AbsoluteUri))
            {
                continue;
            }

            pages.Add(new ComicPage(pages.Count + 1, imageUri));
        }

        if (pages.Count == 0)
        {
            throw new InvalidDataException("章節頁面中找不到可下載的圖片。");
        }

        return pages;
    }

    public IReadOnlyList<ComicPage> ParseChapterScanSnapshot(string snapshotJson, Uri sourceUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotJson);
        SourceUrlPolicy.EnsureAllowedHappyMhPage(sourceUri);
        if (snapshotJson.Length > MaxSnapshotCharacters)
        {
            throw new InvalidDataException("章節圖片資料超過允許大小。");
        }

        ChapterScanSnapshot[]? scans;
        try
        {
            scans = JsonSerializer.Deserialize<ChapterScanSnapshot[]>(snapshotJson, SnapshotJsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("章節圖片資料格式無效。", exception);
        }

        var pages = new List<ComicPage>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scan in (scans ?? []).Take(MaxChapterPages))
        {
            if (scan.N != 0 ||
                string.IsNullOrWhiteSpace(scan.Url) ||
                !Uri.TryCreate(sourceUri, scan.Url.Trim(), out var imageUri) ||
                !SourceUrlPolicy.IsAllowedHappyMhAsset(imageUri) ||
                !seen.Add(imageUri.AbsoluteUri))
            {
                continue;
            }

            pages.Add(new ComicPage(pages.Count + 1, imageUri));
        }

        if (pages.Count == 0)
        {
            throw new InvalidDataException("章節資料中找不到可下載的漫畫圖片。");
        }

        return pages;
    }

    private static ChapterInfo? TryCreateChapter(
        IElement element,
        Uri sourceUri,
        string expectedComicId,
        int sourceIndex) => TryCreateChapter(
            element.TextContent,
            element.GetAttribute("href"),
            sourceUri,
            expectedComicId,
            sourceIndex);

    private static ChapterInfo? TryCreateChapter(
        string? rawTitle,
        string? href,
        Uri sourceUri,
        string expectedComicId,
        int sourceIndex)
    {
        if (string.IsNullOrWhiteSpace(href) ||
            !Uri.TryCreate(sourceUri, href, out var chapterUri))
        {
            return null;
        }

        try
        {
            SourceUrlPolicy.EnsureAllowedHappyMhPage(chapterUri);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var segments = SourceUrlPolicy.SplitPath(chapterUri);
        if (segments.Length != 3 ||
            (!segments[0].Equals("mangaread", StringComparison.OrdinalIgnoreCase) &&
             !segments[0].Equals("mangarcard", StringComparison.OrdinalIgnoreCase)) ||
            !segments[1].Equals(expectedComicId, StringComparison.Ordinal))
        {
            return null;
        }

        var title = NormalizeWhitespace(rawTitle);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = segments[2];
        }

        var numberMatch = ChapterNumberRegex().Match(title);
        var sequence = numberMatch.Success &&
                       int.TryParse(numberMatch.Groups["number"].Value, out var number)
            ? number
            : 1_000_000 + sourceIndex;

        return new ChapterInfo(segments[2], title, chapterUri, sequence);
    }

    private static string GetTitle(IDocument document)
    {
        var heading = NormalizeWhitespace(document.QuerySelector("h1")?.TextContent);
        if (!string.IsNullOrWhiteSpace(heading))
        {
            return heading;
        }

        var title = NormalizeWhitespace(document.Title);
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Split(new[] { " - ", " — ", "–" }, StringSplitOptions.TrimEntries)[0];
        }

        throw new InvalidDataException("漫畫頁面缺少標題。");
    }

    private static string? FirstNonEmptyAttribute(IElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var value = element.GetAttribute(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string NormalizeWhitespace(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : WhitespaceRegex().Replace(value, " ").Trim();

    private static void ThrowIfAccessBlocked(string html)
    {
        if (html.Contains("人机验证", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("人機驗證", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("captcha", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("來源網站要求人機驗證；本程式不會繞過此限制。");
        }
    }

    [GeneratedRegex("(?:^\\s*(?<number>\\d+)(?:\\s|$))|(?:第\\s*(?<number>\\d+)\\s*[回話话])", RegexOptions.CultureInvariant)]
    private static partial Regex ChapterNumberRegex();

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    private sealed record MangaSnapshot(string? Title, ChapterSnapshot[]? Chapters);

    private sealed record ChapterSnapshot(string? Title, string? Href);

    private sealed record ChapterScanSnapshot(string? Url, int N);
}
