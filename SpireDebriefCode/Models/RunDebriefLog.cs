namespace SpireDebrief.SpireDebriefCode.Models;

public sealed class RunDebriefLog
{
    public int SchemaVersion { get; set; } = 1;
    public string RunId { get; set; } = Guid.NewGuid().ToString("N");
    public RunMetadata Metadata { get; set; } = new();
    public FinalRunState FinalState { get; set; } = new();
    public List<FloorLog> Floors { get; set; } = [];
    public SummaryCounts Summary { get; set; } = new();
    public PathingLog? Pathing { get; set; }
}

public sealed class RunMetadata
{
    public string? GameRunId { get; set; }
    public string? Character { get; set; }
    public string? Ascension { get; set; }
    public string? Difficulty { get; set; }
    public string? Seed { get; set; }
    public string StartedAt { get; set; } = DateTimeOffset.Now.ToString("O");
    public string? EndedAt { get; set; }
    public string? GameVersion { get; set; }
    public string? ModVersion { get; set; }
    public string? Result { get; set; }
    public string? FinalAct { get; set; }
    public int? FinalFloor { get; set; }
    public string? FinalRoom { get; set; }
}

public sealed class FinalRunState
{
    public List<DebriefItem> Deck { get; set; } = [];
    public List<DebriefItem> Relics { get; set; } = [];
    public List<DebriefItem> Potions { get; set; } = [];
    public int? Gold { get; set; }
    public int? CurrentHp { get; set; }
    public int? MaxHp { get; set; }
}

public sealed class FloorLog
{
    public int Floor { get; set; }
    public string RoomType { get; set; } = "Unknown";
    public string? Encounter { get; set; }
    public string? PathingChoice { get; set; }
    public int? CurrentHp { get; set; }
    public int? MaxHp { get; set; }
    public int? Gold { get; set; }
    public int? TurnsTaken { get; set; }
    public int? DamageTaken { get; set; }
    public List<DebriefItem> CardsGained { get; set; } = [];
    public List<DebriefItem> CardsRemoved { get; set; } = [];
    public List<CardRewardDecision> CardRewards { get; set; } = [];
    public List<DebriefItem> RelicRewards { get; set; } = [];
    public List<DebriefItem> PotionRewards { get; set; } = [];
    public EventDecision? Event { get; set; }
    public ShopDecision? Shop { get; set; }
    public RestSiteDecision? RestSite { get; set; }
    public List<string> Notes { get; set; } = [];
}

public sealed class CardRewardDecision
{
    public List<DebriefItem> Choices { get; set; } = [];
    public DebriefItem? Picked { get; set; }
    public bool Skipped { get; set; }
    public string? Source { get; set; }
}

public sealed class EventDecision
{
    public string? Name { get; set; }
    public List<string> Options { get; set; } = [];
    public string? Chosen { get; set; }
    public string? Result { get; set; }
}

public sealed class ShopDecision
{
    public List<DebriefItem> Purchased { get; set; } = [];
    public List<DebriefItem> Removed { get; set; } = [];
}

public sealed class RestSiteDecision
{
    public string? Action { get; set; }
    public DebriefItem? Target { get; set; }
}

public sealed class SummaryCounts
{
    public int CardsPicked { get; set; }
    public int CardRewardsSkipped { get; set; }
    public int CardsRemoved { get; set; }
    public int CardsUpgraded { get; set; }
    public int RelicsAcquired { get; set; }
    public int ShopsVisited { get; set; }
    public int ElitesFought { get; set; }
}

public sealed class PathingLog
{
    public int SchemaVersion { get; set; } = 1;
    public string Source { get; set; } = "run_history_only";
    public string? Note { get; set; }
    public List<ActualPathStepLog> ActualPath { get; set; } = [];
    public List<ActPathGraphSnapshot> Acts { get; set; } = [];
    public List<PathChoiceLog> Choices { get; set; } = [];
}

public sealed class ActualPathStepLog
{
    public int Floor { get; set; }
    public int? ActIndex { get; set; }
    public string? NodeId { get; set; }
    public int? Row { get; set; }
    public int? Column { get; set; }
    public string? Coordinate { get; set; }
    public string? MapPointType { get; set; }
    public string RoomType { get; set; } = "Unknown";
    public string? PreviousNodeId { get; set; }
    public string? PathingChoiceSummary { get; set; }
}

public sealed class ActPathGraphSnapshot
{
    public int ActIndex { get; set; }
    public string? CapturedAt { get; set; }
    public List<PathNodeLog> Nodes { get; set; } = [];
    public List<PathEdgeLog> Edges { get; set; } = [];
}

public sealed class PathNodeLog
{
    public string Id { get; set; } = string.Empty;
    public int ActIndex { get; set; }
    public int? Row { get; set; }
    public int? Column { get; set; }
    public string? Coordinate { get; set; }
    public string MapPointType { get; set; } = "Unknown";
    public string? RoomType { get; set; }
}

public sealed class PathEdgeLog
{
    public string FromNodeId { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
}

public sealed class PathChoiceLog
{
    public string Id { get; set; } = string.Empty;
    public int Floor { get; set; }
    public int ActIndex { get; set; }
    public string? CapturedAt { get; set; }
    public string? FromNodeId { get; set; }
    public List<string> AvailableNodeIds { get; set; } = [];
    public string? ChosenNodeId { get; set; }
    public string? ChosenNodeType { get; set; }
    public PlayerStateSnapshot? PlayerStateBefore { get; set; }
    public PlayerStateSnapshot? PlayerStateAfter { get; set; }
    public List<PathOptionSummary> OptionSummaries { get; set; } = [];
    public PathChoiceRanks? Ranks { get; set; }
}

public sealed class PathOptionSummary
{
    public string NodeId { get; set; } = string.Empty;
    public string? NodeType { get; set; }
    public int ReachablePathCount { get; set; }
    public int MinElitesReachable { get; set; }
    public int MaxElitesReachable { get; set; }
    public int MinMonstersReachable { get; set; }
    public int MaxMonstersReachable { get; set; }
    public int MinRestSitesReachable { get; set; }
    public int MaxRestSitesReachable { get; set; }
    public int MinShopsReachable { get; set; }
    public int MaxShopsReachable { get; set; }
    public int MinTreasuresReachable { get; set; }
    public int MaxTreasuresReachable { get; set; }
    public int MinUnknownsReachable { get; set; }
    public int MaxUnknownsReachable { get; set; }
    public int MinEventsReachable { get; set; }
    public int MaxEventsReachable { get; set; }
    public bool EliteForced { get; set; }
    public bool RestSiteReachable { get; set; }
    public int? NearestRestDistance { get; set; }
    public int? NearestShopDistance { get; set; }
    public int? NearestEliteDistance { get; set; }
    public int PathFlexibilityScore { get; set; }
}

public sealed class PathChoiceRanks
{
    public int? ChosenRankByMostElites { get; set; }
    public int? ChosenRankByFewestElites { get; set; }
    public int? ChosenRankByMostRestSites { get; set; }
    public int? ChosenRankByShortestRestDistance { get; set; }
    public int? ChosenRankByPathCount { get; set; }
}

public sealed class PlayerStateSnapshot
{
    public int? CurrentHp { get; set; }
    public int? MaxHp { get; set; }
    public int? Gold { get; set; }
    public int? DeckSize { get; set; }
    public int? RelicCount { get; set; }
}

public sealed class PathingTelemetryRun
{
    public int SchemaVersion { get; set; } = 1;
    public string Source { get; set; } = "live_telemetry";
    public string RunId { get; set; } = Guid.NewGuid().ToString("N");
    public string? GameRunId { get; set; }
    public string? Character { get; set; }
    public string? Seed { get; set; }
    public string StartedAt { get; set; } = DateTimeOffset.Now.ToString("O");
    public string UpdatedAt { get; set; } = DateTimeOffset.Now.ToString("O");
    public List<ActualPathStepLog> ActualPath { get; set; } = [];
    public List<ActPathGraphSnapshot> Acts { get; set; } = [];
    public List<PathChoiceLog> Choices { get; set; } = [];
}
