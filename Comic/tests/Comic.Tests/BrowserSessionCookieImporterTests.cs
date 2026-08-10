using System.Net;
using Comic.Infrastructure.HappyMh;

namespace Comic.Tests;

public sealed class BrowserSessionCookieImporterTests
{
    private static readonly Uri MangaUri = new("https://m.happymh.com/manga/butiange");

    [Fact]
    public void Import_AddsAllowedCookieWithoutExposingItOutsideHappyMh()
    {
        var container = new CookieContainer();
        var cookies = new[]
        {
            new BrowserSessionCookie(
                "cf_clearance",
                "verified-value",
                ".happymh.com",
                "/",
                IsHttpOnly: true,
                IsSecure: true,
                Expires: null)
        };

        var imported = BrowserSessionCookieImporter.Import(container, MangaUri, cookies);

        var importedCookie = Assert.Single(container.GetCookies(MangaUri).Cast<Cookie>());
        Assert.Equal(1, imported);
        Assert.Equal("cf_clearance", importedCookie.Name);
        Assert.Equal("verified-value", importedCookie.Value);
        Assert.True(importedCookie.HttpOnly);
        Assert.True(importedCookie.Secure);
        Assert.Empty(container.GetCookies(new Uri("https://example.com/")));
    }

    [Theory]
    [InlineData("session", "value", ".example.com", "/")]
    [InlineData("bad\r\nname", "value", ".happymh.com", "/")]
    [InlineData("session", "bad\r\nvalue", ".happymh.com", "/")]
    [InlineData("session", "value", ".happymh.com", "relative")]
    public void Import_SkipsForeignOrMalformedCookies(
        string name,
        string value,
        string domain,
        string path)
    {
        var container = new CookieContainer();
        var cookies = new[]
        {
            new BrowserSessionCookie(name, value, domain, path, true, true, null)
        };

        var imported = BrowserSessionCookieImporter.Import(container, MangaUri, cookies);

        Assert.Equal(0, imported);
        Assert.Empty(container.GetCookies(MangaUri));
    }

    [Fact]
    public void Import_SkipsExpiredCookies()
    {
        var container = new CookieContainer();
        var cookies = new[]
        {
            new BrowserSessionCookie(
                "expired",
                "value",
                "m.happymh.com",
                "/",
                IsHttpOnly: false,
                IsSecure: true,
                Expires: DateTimeOffset.UtcNow.AddMinutes(-1))
        };

        var imported = BrowserSessionCookieImporter.Import(container, MangaUri, cookies);

        Assert.Equal(0, imported);
        Assert.Empty(container.GetCookies(MangaUri));
    }
}
