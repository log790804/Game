using System.Net;
using System.Text;
using Comic.Core.Abstractions;
using Comic.Core.Exceptions;
using Comic.Core.Models;
using Comic.Core.Security;

namespace Comic.Infrastructure.HappyMh;

public sealed class HappyMhSourceClient : IComicSourceClient
{
    private const long DefaultMaxHtmlBytes = 5 * 1024 * 1024;

    private readonly HttpClient _httpClient;
    private readonly HappyMhHtmlParser _parser;
    private readonly long _maxHtmlBytes;

    public HappyMhSourceClient(
        HttpClient httpClient,
        HappyMhHtmlParser parser,
        long maxHtmlBytes = DefaultMaxHtmlBytes)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(parser);

        if (maxHtmlBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHtmlBytes));
        }

        _httpClient = httpClient;
        _parser = parser;
        _maxHtmlBytes = maxHtmlBytes;
    }

    public async Task<ComicInfo> LoadComicAsync(
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        var mangaUri = SourceUrlPolicy.ParseHappyMhMangaUrl(sourceUri.AbsoluteUri);
        var html = await GetHtmlAsync(mangaUri, referrer: null, cancellationToken).ConfigureAwait(false);
        return _parser.ParseManga(html, mangaUri);
    }

    public async Task<IReadOnlyList<ComicPage>> LoadChapterPagesAsync(
        ChapterInfo chapter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        SourceUrlPolicy.EnsureAllowedHappyMhPage(chapter.SourceUri);

        var segments = SourceUrlPolicy.SplitPath(chapter.SourceUri);
        var mangaUri = new Uri($"https://m.happymh.com/manga/{segments[1]}");
        var html = await GetHtmlAsync(
            chapter.SourceUri,
            mangaUri,
            cancellationToken).ConfigureAwait(false);
        return _parser.ParseChapterPages(html, chapter.SourceUri);
    }

    private async Task<string> GetHtmlAsync(
        Uri uri,
        Uri? referrer,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml");
        request.Headers.Referrer = referrer;

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Unauthorized or
            HttpStatusCode.Forbidden or
            HttpStatusCode.TooManyRequests)
        {
            throw new SourceAccessException(
                $"來源網站拒絕自動存取（HTTP {(int)response.StatusCode}）；程式不會繞過人機驗證或存取限制。");
        }

        if ((int)response.StatusCode is >= 300 and < 400)
        {
            throw new SourceAccessException("來源網站要求重新導向；為避免跨網域存取，已停止請求。");
        }

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength > _maxHtmlBytes)
        {
            throw new InvalidDataException("來源 HTML 超過允許大小。");
        }

        var bytes = await ReadBoundedAsync(
            response.Content,
            _maxHtmlBytes,
            cancellationToken).ConfigureAwait(false);

        return GetEncoding(response.Content.Headers.ContentType?.CharSet).GetString(bytes);
    }

    private static Encoding GetEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset.Trim(' ', '\"', '\''));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    internal static async Task<byte[]> ReadBoundedAsync(
        HttpContent content,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        await using var input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[81_920];
        long total = 0;

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new InvalidDataException("來源內容超過允許大小。");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return output.ToArray();
    }
}
