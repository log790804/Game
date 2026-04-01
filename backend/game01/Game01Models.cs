namespace backend.Game01;

public sealed class Game01Store
{
    public Game01State CurrentGame { get; set; } = Game01Defaults.CreateState();

    public List<Game01Record> Records { get; set; } = [];

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Game01State
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");

    public int BoardSize { get; set; } = 4;

    public string BackImage { get; set; } = string.Empty;

    public List<string> FrontImages { get; set; } = [];

    public List<Game01Card> Cards { get; set; } = [];

    public List<Game01PlayerScore> Players { get; set; } = Game01Defaults.CreatePlayers();

    public int CurrentPlayerIndex { get; set; }

    public int Moves { get; set; }

    public int Matches { get; set; }

    public bool IsCompleted { get; set; }

    public bool RecordCommitted { get; set; }

    public string LastAction { get; set; } = "遊戲尚未開始";

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Game01Card
{
    public string Id { get; set; } = string.Empty;

    public string PairId { get; set; } = string.Empty;

    public string FaceImage { get; set; } = string.Empty;

    public bool IsRevealed { get; set; }

    public bool IsMatched { get; set; }
}

public sealed class Game01PlayerScore
{
    public string Name { get; set; } = string.Empty;

    public int Score { get; set; }
}

public sealed class Game01Record
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");

    public int BoardSize { get; set; }

    public int Moves { get; set; }

    public int Matches { get; set; }

    public string Winner { get; set; } = string.Empty;

    public List<Game01PlayerScore> FinalScores { get; set; } = [];

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Game01ResetRequest
{
    public int BoardSize { get; set; } = 4;

    public string BackImage { get; set; } = string.Empty;

    public List<string> FrontImages { get; set; } = [];
}

public static class Game01Defaults
{
    public static List<Game01PlayerScore> CreatePlayers() =>
    [
        new() { Name = "玩家 1", Score = 0 },
        new() { Name = "玩家 2", Score = 0 }
    ];

    public static Game01State CreateState() => new()
    {
        Players = CreatePlayers(),
        LastAction = "點擊開始新局後即可遊玩"
    };
}
