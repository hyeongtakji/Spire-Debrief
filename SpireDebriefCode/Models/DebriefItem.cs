namespace SpireDebrief.SpireDebriefCode.Models;

public sealed class DebriefItem
{
    public string? Id { get; set; }
    public string Name { get; set; } = "Unknown";
    public int? UpgradeCount { get; set; }

    public string DisplayName
    {
        get
        {
            if (UpgradeCount is > 0)
                return $"{Name}{new string('+', Math.Min(UpgradeCount.Value, 3))}";
            return Name;
        }
    }
}
