using System.Text.Json;
using System.Text.RegularExpressions;
using Comic.Core.Models;

namespace Comic.Infrastructure.Library;

public sealed partial class JsonComicMetadataStore
{
    public const int CurrentSchemaVersion = 1;
    public const string FileName = "comic-info.json";

    private const long MaxMetadataBytes = 64 * 1024;
    private const int MaxTitleLength = 500;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public ComicLibraryMetadata? Load(string comicDirectory)
    {
        if (string.IsNullOrWhiteSpace(comicDirectory))
        {
            return null;
        }

        try
        {
            var directory = new DirectoryInfo(Path.GetFullPath(comicDirectory));
            if (!directory.Exists ||
                (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return null;
            }

            var metadataPath = Path.Combine(directory.FullName, FileName);
            var fileInfo = new FileInfo(metadataPath);
            if (!fileInfo.Exists || fileInfo.Length > MaxMetadataBytes)
            {
                return null;
            }

            using var stream = new FileStream(
                metadataPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.SequentialScan);
            var metadata = JsonSerializer.Deserialize<ComicLibraryMetadata>(stream, JsonOptions);
            return metadata is not null &&
                   IsSafe(metadata) &&
                   metadata.ComicId.Equals(directory.Name, StringComparison.OrdinalIgnoreCase)
                ? metadata
                : null;
        }
        catch (Exception exception) when (
            exception is IOException or JsonException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    public void Save(
        string libraryRoot,
        string comicId,
        string title,
        DateTimeOffset updatedAt)
    {
        if (string.IsNullOrWhiteSpace(libraryRoot))
        {
            throw new ArgumentException("漫畫庫路徑不可為空白。", nameof(libraryRoot));
        }

        var normalizedTitle = title?.Trim() ?? string.Empty;
        var metadata = new ComicLibraryMetadata(
            CurrentSchemaVersion,
            comicId,
            normalizedTitle,
            updatedAt);
        if (!IsSafe(metadata))
        {
            throw new ArgumentException("漫畫名稱關聯資料包含不安全或無效的內容。", nameof(title));
        }

        var root = Path.GetFullPath(libraryRoot);
        var rootBoundary = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        var comicDirectory = Path.GetFullPath(Path.Combine(root, comicId));
        if (!comicDirectory.StartsWith(rootBoundary, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("漫畫名稱關聯檔超出漫畫庫範圍。");
        }

        Directory.CreateDirectory(root);
        EnsureDirectoryIsNotLink(root);
        Directory.CreateDirectory(comicDirectory);
        EnsureDirectoryIsNotLink(comicDirectory);

        var metadataPath = Path.Combine(comicDirectory, FileName);
        var temporaryPath = metadataPath + ".tmp";
        EnsureFileIsNotLink(metadataPath);
        EnsureFileIsNotLink(temporaryPath);
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, metadata, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, metadataPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static bool IsSafe(ComicLibraryMetadata metadata) =>
        metadata.SchemaVersion == CurrentSchemaVersion &&
        SafeIdentifierRegex().IsMatch(metadata.ComicId) &&
        !string.IsNullOrWhiteSpace(metadata.Title) &&
        metadata.Title.Length <= MaxTitleLength &&
        !metadata.Title.Any(char.IsControl);

    private static void EnsureDirectoryIsNotLink(string path)
    {
        var directory = new DirectoryInfo(path);
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("漫畫名稱關聯檔路徑不可包含連結或 junction。");
        }
    }

    private static void EnsureFileIsNotLink(string path)
    {
        if (File.Exists(path) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("漫畫名稱關聯檔不可使用連結。");
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,120}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdentifierRegex();
}
