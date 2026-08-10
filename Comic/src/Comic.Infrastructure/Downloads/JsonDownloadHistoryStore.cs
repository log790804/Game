using System.Text.Json;
using System.Text.RegularExpressions;
using Comic.Core.Models;

namespace Comic.Infrastructure.Downloads;

public sealed partial class JsonDownloadHistoryStore
{
    private const long MaxHistoryBytes = 2 * 1024 * 1024;
    private const int MaxEntries = 10_000;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _historyPath;
    private readonly object _syncRoot = new();

    public JsonDownloadHistoryStore(string historyPath)
    {
        if (string.IsNullOrWhiteSpace(historyPath))
        {
            throw new ArgumentException("下載歷史路徑不可為空白。", nameof(historyPath));
        }

        _historyPath = Path.GetFullPath(historyPath);
    }

    public IReadOnlyList<DownloadHistoryEntry> LoadAll()
    {
        lock (_syncRoot)
        {
            return LoadAllCore();
        }
    }

    public void Save(DownloadHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!IsSafe(entry))
        {
            throw new ArgumentException("下載歷史包含不安全或無效的資料。", nameof(entry));
        }

        var normalized = entry with { LibraryRoot = Path.GetFullPath(entry.LibraryRoot) };
        lock (_syncRoot)
        {
            var entries = LoadAllCore()
                .Where(item => !HasSameKey(item, normalized))
                .Append(normalized)
                .OrderByDescending(item => item.CompletedAt)
                .Take(MaxEntries)
                .ToArray();

            var directory = Path.GetDirectoryName(_historyPath)
                ?? throw new InvalidOperationException("無法建立下載歷史目錄。");
            Directory.CreateDirectory(directory);

            var temporaryPath = _historyPath + ".tmp";
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
                    JsonSerializer.Serialize(stream, entries, JsonOptions);
                    stream.Flush(flushToDisk: true);
                }

                File.Move(temporaryPath, _historyPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }

    private IReadOnlyList<DownloadHistoryEntry> LoadAllCore()
    {
        if (!File.Exists(_historyPath))
        {
            return [];
        }

        try
        {
            var fileInfo = new FileInfo(_historyPath);
            if (fileInfo.Length > MaxHistoryBytes)
            {
                return [];
            }

            using var stream = new FileStream(
                _historyPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.SequentialScan);
            return (JsonSerializer.Deserialize<DownloadHistoryEntry[]>(stream, JsonOptions) ?? [])
                .Where(IsSafe)
                .GroupBy(
                    entry => $"{Path.GetFullPath(entry.LibraryRoot)}\0{entry.ComicId}\0{entry.ChapterId}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(entry => entry.CompletedAt).First())
                .OrderByDescending(entry => entry.CompletedAt)
                .Take(MaxEntries)
                .ToArray();
        }
        catch (Exception exception) when (
            exception is IOException or JsonException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }

    private static bool HasSameKey(DownloadHistoryEntry left, DownloadHistoryEntry right) =>
        Path.GetFullPath(left.LibraryRoot).Equals(
            Path.GetFullPath(right.LibraryRoot),
            StringComparison.OrdinalIgnoreCase) &&
        left.ComicId.Equals(right.ComicId, StringComparison.OrdinalIgnoreCase) &&
        left.ChapterId.Equals(right.ChapterId, StringComparison.Ordinal);

    private static bool IsSafe(DownloadHistoryEntry entry)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(entry.LibraryRoot) &&
                   Path.IsPathFullyQualified(entry.LibraryRoot) &&
                   entry.LibraryRoot.Length <= 1024 &&
                   SafeIdentifierRegex().IsMatch(entry.ComicId) &&
                   SafeIdentifierRegex().IsMatch(entry.ChapterId) &&
                   SafeChapterFolderRegex().IsMatch(entry.ChapterFolderName) &&
                   IsSafeTitle(entry.ComicTitle) &&
                   IsSafeTitle(entry.ChapterTitle) &&
                   entry.PageCount is >= 1 and <= 100_000;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsSafeTitle(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 500 &&
        !value.Any(char.IsControl);

    [GeneratedRegex("^[A-Za-z0-9_-]{1,120}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdentifierRegex();

    [GeneratedRegex("^(?:第[1-9][0-9]{0,5}回|其他_[A-Za-z0-9_-]{1,120})$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeChapterFolderRegex();
}
