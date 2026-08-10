using Comic.Core.Models;
using Comic.Infrastructure.Library;

namespace Comic.Tests;

public sealed class JsonComicMetadataStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "Comic.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_PersistsVersionedComicIdToTitleAssociation()
    {
        var store = new JsonComicMetadataStore();
        var updatedAt = new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero);

        store.Save(_root, "butiange", "步天歌", updatedAt);

        var comicDirectory = Path.Combine(_root, "butiange");
        var metadata = store.Load(comicDirectory);
        Assert.NotNull(metadata);
        Assert.Equal(JsonComicMetadataStore.CurrentSchemaVersion, metadata.SchemaVersion);
        Assert.Equal("butiange", metadata.ComicId);
        Assert.Equal("步天歌", metadata.Title);
        Assert.Equal(updatedAt, metadata.UpdatedAt);
        Assert.True(File.Exists(Path.Combine(comicDirectory, JsonComicMetadataStore.FileName)));
        Assert.False(File.Exists(Path.Combine(comicDirectory, JsonComicMetadataStore.FileName + ".tmp")));
    }

    [Fact]
    public void Load_IgnoresMetadataWhoseComicIdDoesNotMatchDirectory()
    {
        var comicDirectory = Path.Combine(_root, "butiange");
        Directory.CreateDirectory(comicDirectory);
        File.WriteAllText(
            Path.Combine(comicDirectory, "comic-info.json"),
            "{\"schemaVersion\":1,\"comicId\":\"other\",\"title\":\"錯誤名稱\",\"updatedAt\":\"2026-08-05T10:00:00Z\"}");

        var metadata = new JsonComicMetadataStore().Load(comicDirectory);

        Assert.Null(metadata);
    }

    [Fact]
    public void Save_RejectsUnsafeRemoteTitle()
    {
        var store = new JsonComicMetadataStore();

        Assert.Throws<ArgumentException>(() =>
            store.Save(_root, "butiange", "步天歌\0偽造", DateTimeOffset.UtcNow));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
