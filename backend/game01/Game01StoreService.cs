using System.Text.Json;

namespace backend.Game01;

public sealed class Game01StoreService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;

    public Game01StoreService(IWebHostEnvironment environment)
    {
        var configuredRoot = Environment.GetEnvironmentVariable("GAME_DATA_ROOT");
        var storageRoot = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(environment.ContentRootPath, "game01")
            : Path.Combine(configuredRoot.Trim(), "game01");

        Directory.CreateDirectory(storageRoot);
        _filePath = Path.Combine(storageRoot, "game01.json");
    }

    public async Task<Game01Store> GetStoreAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var store = await ReadStoreUnsafeAsync();
            return NormalizeStore(store);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Game01Store> SaveStateAsync(Game01State state)
    {
        await _gate.WaitAsync();
        try
        {
            var store = await ReadStoreUnsafeAsync();
            state.BoardSize = NormalizeBoardSize(state.BoardSize);
            state.UpdatedAt = DateTimeOffset.UtcNow;
            store.CurrentGame = NormalizeState(state);
            store.UpdatedAt = DateTimeOffset.UtcNow;
            await WriteStoreUnsafeAsync(store);
            return store;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Game01Store> ResetStateAsync(Game01ResetRequest request)
    {
        await _gate.WaitAsync();
        try
        {
            var store = await ReadStoreUnsafeAsync();
            store.CurrentGame = new Game01State
            {
                SessionId = Guid.NewGuid().ToString("N"),
                BoardSize = NormalizeBoardSize(request.BoardSize),
                BackImage = request.BackImage?.Trim() ?? string.Empty,
                FrontImages = request.FrontImages?.Where(static item => !string.IsNullOrWhiteSpace(item)).ToList() ?? [],
                Players = Game01Defaults.CreatePlayers(),
                StartedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                LastAction = "Game reset and waiting for a new round"
            };
            store.UpdatedAt = DateTimeOffset.UtcNow;
            await WriteStoreUnsafeAsync(store);
            return store;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Game01Store> AppendRecordAsync(Game01Record record)
    {
        await _gate.WaitAsync();
        try
        {
            var store = await ReadStoreUnsafeAsync();
            if (!store.Records.Any(existing => existing.SessionId == record.SessionId))
            {
                store.Records.Insert(0, NormalizeRecord(record));
                store.UpdatedAt = DateTimeOffset.UtcNow;
                await WriteStoreUnsafeAsync(store);
            }

            return store;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Game01Store> ClearRecordsAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var store = await ReadStoreUnsafeAsync();
            store.Records = [];
            store.UpdatedAt = DateTimeOffset.UtcNow;
            await WriteStoreUnsafeAsync(store);
            return store;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Game01Store> ReadStoreUnsafeAsync()
    {
        if (!File.Exists(_filePath))
        {
            var initialStore = new Game01Store();
            await WriteStoreUnsafeAsync(initialStore);
            return initialStore;
        }

        await using var stream = File.OpenRead(_filePath);
        var store = await JsonSerializer.DeserializeAsync<Game01Store>(stream, JsonOptions);
        return store ?? new Game01Store();
    }

    private async Task WriteStoreUnsafeAsync(Game01Store store)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, NormalizeStore(store), JsonOptions);
    }

    private static Game01Store NormalizeStore(Game01Store store)
    {
        store.CurrentGame = NormalizeState(store.CurrentGame);
        store.Records = store.Records?.Select(NormalizeRecord).ToList() ?? [];
        return store;
    }

    private static Game01State NormalizeState(Game01State? state)
    {
        state ??= Game01Defaults.CreateState();
        state.SessionId = string.IsNullOrWhiteSpace(state.SessionId) ? Guid.NewGuid().ToString("N") : state.SessionId;
        state.BoardSize = NormalizeBoardSize(state.BoardSize);
        state.BackImage = state.BackImage?.Trim() ?? string.Empty;
        state.FrontImages = state.FrontImages?.Where(static item => !string.IsNullOrWhiteSpace(item)).ToList() ?? [];
        state.Cards = state.Cards ?? [];
        state.Players = state.Players?.Take(2).ToList() ?? Game01Defaults.CreatePlayers();

        while (state.Players.Count < 2)
        {
            state.Players.Add(new Game01PlayerScore
            {
                Name = $"Player {state.Players.Count + 1}",
                Score = 0
            });
        }

        state.CurrentPlayerIndex = Math.Clamp(state.CurrentPlayerIndex, 0, 1);
        state.LastAction = string.IsNullOrWhiteSpace(state.LastAction) ? "Waiting for the next move" : state.LastAction.Trim();
        state.UpdatedAt = state.UpdatedAt == default ? DateTimeOffset.UtcNow : state.UpdatedAt;
        state.StartedAt = state.StartedAt == default ? DateTimeOffset.UtcNow : state.StartedAt;
        return state;
    }

    private static Game01Record NormalizeRecord(Game01Record record)
    {
        record.SessionId = string.IsNullOrWhiteSpace(record.SessionId) ? Guid.NewGuid().ToString("N") : record.SessionId;
        record.BoardSize = NormalizeBoardSize(record.BoardSize);
        record.FinalScores = record.FinalScores?.Take(2).ToList() ?? Game01Defaults.CreatePlayers();
        record.Winner = string.IsNullOrWhiteSpace(record.Winner) ? "Draw" : record.Winner.Trim();
        record.StartedAt = record.StartedAt == default ? DateTimeOffset.UtcNow : record.StartedAt;
        record.CompletedAt = record.CompletedAt == default ? DateTimeOffset.UtcNow : record.CompletedAt;
        return record;
    }

    private static int NormalizeBoardSize(int boardSize) => boardSize switch
    {
        4 or 6 or 8 => boardSize,
        < 5 => 4,
        < 7 => 6,
        _ => 8
    };
}
