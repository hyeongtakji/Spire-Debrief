namespace SpireDebrief.SpireDebriefCode.Models;

public sealed class RunDebriefLog
{
    public int SchemaVersion { get; set; } = 1;
    public string RunId { get; set; } = Guid.NewGuid().ToString("N");
    public RunMetadata Metadata { get; set; } = new();
    public FinalRunState FinalState { get; set; } = new();
    public List<FloorLog> Floors { get; set; } = [];
    public SummaryCounts Summary { get; set; } = new();
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
    public int? DamageTaken { get; set; }
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
