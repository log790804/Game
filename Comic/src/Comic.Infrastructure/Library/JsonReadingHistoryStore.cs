using System.Text.Json;
using System.Text.RegularExpressions;
using Comic.Core.Models;

namespace Comic.Infrastructure.Library;

public sealed partial class JsonReadingHistoryStore
{
    private const long MaxHistoryBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _historyPath;
    private readonly object _syncRoot = new();

    public JsonReadingHistoryStore(string historyPath)
    {
        if (string.IsNullOrWhiteSpace(historyPath))
        {
            throw new ArgumentException("閱讀紀錄路徑不可為空白。", nameof(historyPath));
        }

        _historyPath = Path.GetFullPath(historyPath);
    }

    public IReadOnlyList<ReadingHistoryEntry> LoadAll()
    {
        lock (_syncRoot)
        {
            return LoadAllCore();
        }
    }

    public void Save(ReadingHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!IsSafe(entry))
        {
            throw new ArgumentException("閱讀紀錄包含不安全的識別碼或檔名。", nameof(entry));
        }

        lock (_syncRoot)
        {
            var entries = LoadAllCore()
                .Where(item => !item.ComicId.Equals(entry.ComicId, StringComparison.OrdinalIgnoreCase))
                .Append(entry)
                .OrderByDescending(item => item.UpdatedAt)
                .ToArray();

            var directory = Path.GetDirectoryName(_historyPath)
                ?? throw new InvalidOperationException("無法建立閱讀紀錄目錄。");
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

    private IReadOnlyList<ReadingHistoryEntry> LoadAllCore()
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
            var entries = JsonSerializer.Deserialize<ReadingHistoryEntry[]>(stream, JsonOptions) ?? [];
            return entries
                .Where(IsSafe)
                .GroupBy(entry => entry.ComicId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(entry => entry.UpdatedAt).First())
                .OrderByDescending(entry => entry.UpdatedAt)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static bool IsSafe(ReadingHistoryEntry entry)
    {
        var pageFileName = Path.GetFileName(entry.PageFileName);
        return SafeIdentifierRegex().IsMatch(entry.ComicId) &&
               (SafeIdentifierRegex().IsMatch(entry.ChapterId) ||
                CanonicalChapterFolderRegex().IsMatch(entry.ChapterId)) &&
               pageFileName.Equals(entry.PageFileName, StringComparison.Ordinal) &&
               pageFileName.Length is > 0 and <= 255 &&
               SupportedPageExtensionRegex().IsMatch(Path.GetExtension(pageFileName));
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,120}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdentifierRegex();

    [GeneratedRegex("^(?:第[1-9][0-9]{0,5}回|其他_[A-Za-z0-9_-]{1,120})$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalChapterFolderRegex();

    [GeneratedRegex("^\\.(?:jpe?g|png|webp|gif|avif)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SupportedPageExtensionRegex();
}
