using System.Net;
using Comic.Core.Security;

namespace Comic.Infrastructure.HappyMh;

public sealed record BrowserSessionCookie(
    string Name,
    string Value,
    string Domain,
    string Path,
    bool IsHttpOnly,
    bool IsSecure,
    DateTimeOffset? Expires);

public static class BrowserSessionCookieImporter
{
    private const int MaxNameLength = 256;
    private const int MaxValueLength = 4096;
    private const int MaxPathLength = 2048;

    public static int Import(
        CookieContainer destination,
        Uri sourceUri,
        IEnumerable<BrowserSessionCookie> sourceCookies)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(sourceUri);
        ArgumentNullException.ThrowIfNull(sourceCookies);
        SourceUrlPolicy.EnsureAllowedHappyMhPage(sourceUri);

        var imported = 0;
        foreach (var sourceCookie in sourceCookies)
        {
            if (!IsSafe(sourceCookie))
            {
                continue;
            }

            try
            {
                var cookie = new Cookie(
                    sourceCookie.Name,
                    sourceCookie.Value,
                    sourceCookie.Path,
                    sourceCookie.Domain)
                {
                    HttpOnly = sourceCookie.IsHttpOnly,
                    // The importer is only used for the HTTPS-only HappyMH source.
                    Secure = true
                };

                if (sourceCookie.Expires is { } expires)
                {
                    cookie.Expires = expires.UtcDateTime;
                }

                destination.Add(cookie);
                imported++;
            }
            catch (CookieException)
            {
                // Browser data is untrusted input. Invalid cookies are ignored.
            }
        }

        return imported;
    }

    private static bool IsSafe(BrowserSessionCookie cookie)
    {
        if (string.IsNullOrWhiteSpace(cookie.Name) ||
            cookie.Name.Length > MaxNameLength ||
            cookie.Value.Length > MaxValueLength ||
            cookie.Path.Length > MaxPathLength ||
            ContainsControlCharacter(cookie.Name) ||
            ContainsControlCharacter(cookie.Value) ||
            ContainsControlCharacter(cookie.Domain) ||
            ContainsControlCharacter(cookie.Path) ||
            !cookie.Path.StartsWith("/", StringComparison.Ordinal) ||
            cookie.Expires is { } expires && expires <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        var domain = cookie.Domain.Trim().TrimStart('.');
        return domain.Equals("happymh.com", StringComparison.OrdinalIgnoreCase) ||
               domain.Equals("m.happymh.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsControlCharacter(string value) =>
        value.Any(char.IsControl);
}
