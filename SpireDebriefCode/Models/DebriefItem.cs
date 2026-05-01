namespace SpireDebrief.SpireDebriefCode.Models;

public sealed class DebriefItem
{
    public string? Id { get; set; }
    public string Name { get; set; } = "Unknown";
    public int? UpgradeCount { get; set; }
    public List<CardInstanceMetadata> InstanceMetadata { get; set; } = [];

    public string DisplayName
    {
        get
        {
            string displayName = BaseDisplayName;

            if (InstanceMetadata.Count == 0)
                return displayName;

            return $"{displayName} [{string.Join("; ", InstanceMetadata.Select(metadata => metadata.DisplayText))}]";
        }
    }

    public string BaseDisplayName
    {
        get
        {
            if (UpgradeCount is > 0)
            {
                string baseName = Name.TrimEnd('+');
                if (string.IsNullOrWhiteSpace(baseName))
                    baseName = Name;
                return $"{baseName}{new string('+', Math.Min(UpgradeCount.Value, 3))}";
            }

            return Name;
        }
    }
}

public sealed class CardInstanceMetadata
{
    public string Kind { get; set; } = "metadata";
    public string? Id { get; set; }
    public string? Name { get; set; }
    public int? Amount { get; set; }
    public string? RawValue { get; set; }
    public bool IsUnlocalized { get; set; }

    public string DisplayText
    {
        get
        {
            string label = !string.IsNullOrWhiteSpace(Name)
                ? Name
                : IsUnlocalized && !string.IsNullOrWhiteSpace(Id)
                    ? $"unlocalized {Kind}: {Id}"
                    : !string.IsNullOrWhiteSpace(Id)
                        ? Id
                        : !string.IsNullOrWhiteSpace(RawValue)
                            ? $"{Kind} unknown: raw={RawValue}"
                            : $"{Kind} unknown";

            return Amount is > 1 ? $"{label} {Amount.Value}" : label;
        }
    }
}
