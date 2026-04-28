using System.Collections;
using System.Reflection;
using SpireDebrief.SpireDebriefCode.Models;

namespace SpireDebrief.SpireDebriefCode.Services;

public static class ReflectionDataExtractor
{
    private static readonly string[] NameMembers =
    [
        "DisplayName", "Name", "Title", "Label", "LocalizedName", "LocName", "EventName"
    ];

    private static readonly string[] IdMembers =
    [
        "Id", "ID", "ModelId", "CardId", "RelicId", "PotionId", "Key"
    ];

    public static string? TryReadString(object? source, params string[] memberNames)
    {
        object? value = TryReadValue(source, memberNames);
        string? resolved = ResolveString(value);
        if (resolved != null) return resolved;

        return null;
    }

    public static string? ResolveString(object? value)
    {
        return value switch
        {
            null => null,
            string s when string.IsNullOrWhiteSpace(s) => null,
            string s => s,
            _ => ResolveObjectString(value)
        };
    }

    public static int? TryReadInt(object? source, params string[] memberNames)
    {
        object? value = TryReadValue(source, memberNames);
        if (value == null) return null;
        if (value is int i) return i;
        if (value is long l) return (int)l;
        if (value is short s) return s;
        if (value is byte b) return b;
        if (int.TryParse(value.ToString(), out int parsed)) return parsed;
        return null;
    }

    public static object? TryReadValue(object? source, params string[] memberNames)
    {
        if (source == null) return null;

        foreach (string memberName in memberNames)
        {
            object? current = source;
            foreach (string part in memberName.Split('.'))
            {
                current = TryReadDirectValue(current, part);
                if (current == null) break;
            }

            if (current != null) return current;
        }

        return null;
    }

    public static DebriefItem ToItem(object? source)
    {
        return TryToItem(source, out DebriefItem? item) ? item : new DebriefItem();
    }

    public static bool TryToItem(object? source, out DebriefItem item, string? requiredIdPrefix = null)
    {
        item = new DebriefItem();
        if (source == null || IsIgnoredItemSource(source)) return false;

        object model = TryReadValue(source, "Model", "CardModel", "RelicModel", "PotionModel") ?? source;
        if (IsIgnoredItemSource(model)) return false;

        string? name = TryReadString(model, NameMembers) ?? TryReadString(source, NameMembers);
        string? id = TryReadString(model, IdMembers) ?? TryReadString(source, IdMembers);
        id = Clean(id);
        name = Clean(name);

        if (requiredIdPrefix != null &&
            (id == null || !id.StartsWith(requiredIdPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (id == null && name == null) return false;

        int? upgradeCount =
            TryReadInt(source, "UpgradeCount", "TimesUpgraded", "Upgrades") ??
            TryReadInt(model, "UpgradeCount", "TimesUpgraded", "Upgrades");

        bool upgraded =
            TryReadBool(source, "Upgraded", "IsUpgraded", "IsPlus") ??
            TryReadBool(model, "Upgraded", "IsUpgraded", "IsPlus") ??
            false;

        item = new DebriefItem
        {
            Id = id,
            Name = name ?? id ?? "Unknown",
            UpgradeCount = upgradeCount is > 0 ? upgradeCount : upgraded ? 1 : null
        };
        return true;
    }

    public static List<DebriefItem> ExtractItems(object? source, params string[] containerMembers) =>
        ExtractItemsWithIdPrefix(source, null, containerMembers);

    public static List<DebriefItem> ExtractItemsWithIdPrefix(object? source, string? requiredIdPrefix, params string[] containerMembers)
    {
        List<DebriefItem> items = [];
        foreach (object? candidate in EnumerateCandidates(source, containerMembers))
        {
            if (candidate == null || candidate is string) continue;
            if (TryToItem(candidate, out DebriefItem item, requiredIdPrefix))
                items.Add(item);
        }

        return DeduplicateItems(items);
    }

    public static List<string> ExtractStrings(object? source, params string[] containerMembers)
    {
        List<string> strings = [];
        foreach (object? candidate in EnumerateCandidates(source, containerMembers))
        {
            string? value = ResolveString(candidate) ?? TryReadString(candidate, NameMembers);
            value = Clean(value);
            if (value != null) strings.Add(value);
        }

        return strings.Distinct(StringComparer.Ordinal).ToList();
    }

    public static void FillMetadata(RunDebriefLog log, object? source)
    {
        if (source == null) return;

        log.Metadata.GameRunId ??= TryReadString(
            source,
            "GameRunId",
            "RunId",
            "Run.Id",
            "Run.ID",
            "SaveId",
            "SaveKey",
            "SaveData.Id",
            "SaveData.RunId",
            "RunGuid",
            "RunUuid",
            "RunUUID");
        log.Metadata.Character ??= TryReadString(source, "Character.Name", "Character.Id", "Character", "Player.Character.Name", "Player.Character.Id");
        log.Metadata.Ascension ??= TryReadString(source, "Ascension", "AscensionLevel", "Difficulty.Ascension");
        log.Metadata.Difficulty ??= TryReadString(source, "Difficulty", "RunDifficulty");
        log.Metadata.Seed ??= TryReadString(source, "Seed", "RngSeed", "RunSeed");
        log.Metadata.GameVersion ??= TryReadString(source, "GameVersion", "Version", "BuildVersion");
        log.Metadata.FinalAct ??= TryReadString(source, "Act", "CurrentAct", "FinalAct");
        log.Metadata.FinalFloor ??= TryReadInt(source, "Floor", "CurrentFloor", "FinalFloor", "Room.Floor");
        log.Metadata.FinalRoom ??= TryReadString(source, "Room.Name", "Room.Type", "CurrentRoom", "FinalRoom");
    }

    public static void FillFinalState(FinalRunState finalState, object? source)
    {
        if (source == null) return;
        object player = TryReadValue(source, "Player", "CurrentPlayer", "Players.0") ?? source;

        if (finalState.Deck.Count == 0)
            finalState.Deck = ExtractItemsWithIdPrefix(player, "CARD.", "Deck.Cards", "Cards", "MasterDeck.Cards", "DrawPile.Cards", "CardPile.Cards");
        if (finalState.Relics.Count == 0)
            finalState.Relics = ExtractItemsWithIdPrefix(player, "RELIC.", "Relics", "RelicInventory", "RelicInventory.Relics");
        if (finalState.Potions.Count == 0)
            finalState.Potions = ExtractItemsWithIdPrefix(player, "POTION.", "Potions", "PotionSlots", "PotionSlots.Potions");

        finalState.Gold ??= TryReadInt(player, "Gold", "Money");
        finalState.CurrentHp ??= TryReadInt(player, "CurrentHp", "CurrentHP", "Hp", "HP", "Health");
        finalState.MaxHp ??= TryReadInt(player, "MaxHp", "MaxHP", "MaxHealth");
    }

    private static IEnumerable<object?> EnumerateCandidates(object? source, string[] containerMembers)
    {
        if (source == null) yield break;

        if (containerMembers.Length == 0)
        {
            foreach (object? item in Enumerate(source))
                yield return item;
            yield break;
        }

        foreach (string member in containerMembers)
        {
            object? container = TryReadValue(source, member);
            foreach (object? item in Enumerate(container))
                yield return item;
        }
    }

    private static IEnumerable<object?> Enumerate(object? source)
    {
        if (source == null) yield break;

        if (source is IEnumerable enumerable and not string)
        {
            foreach (object? item in enumerable)
                yield return item;
            yield break;
        }

        yield return source;
    }

    private static object? TryReadDirectValue(object? source, string memberName)
    {
        if (source == null) return null;

        if (int.TryParse(memberName, out int index) && source is IEnumerable enumerable and not string)
        {
            int currentIndex = 0;
            foreach (object? item in enumerable)
            {
                if (currentIndex == index) return item;
                currentIndex++;
            }

            return null;
        }

        Type type = source.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        PropertyInfo? property = type.GetProperty(memberName, flags);
        if (property?.GetIndexParameters().Length == 0)
        {
            try { return property.GetValue(source); }
            catch { return null; }
        }

        FieldInfo? field = type.GetField(memberName, flags);
        if (field != null)
        {
            try { return field.GetValue(source); }
            catch { return null; }
        }

        return null;
    }

    private static string? ResolveObjectString(object value)
    {
        foreach (string member in new[] { "Text", "Value", "RawText", "LocalizedText", "Localized", "Key", "Id" })
        {
            string? result = ResolveString(TryReadDirectValue(value, member));
            if (Clean(result) != null) return result;
        }

        foreach (string methodName in new[] { "GetRawText", "GetText", "GetLocalizedText" })
        {
            MethodInfo? method = value.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                Type.DefaultBinder,
                Type.EmptyTypes,
                null);
            if (method == null) continue;

            try
            {
                string? result = ResolveString(method.Invoke(value, []));
                if (Clean(result) != null) return result;
            }
            catch
            {
                // Ignore localization helpers that are unavailable for this object.
            }
        }

        string? fallback = value.ToString();
        if (string.IsNullOrWhiteSpace(fallback)) return null;
        Type type = value.GetType();
        if (fallback == type.FullName || fallback == type.Name) return null;
        if (fallback.StartsWith("MegaCrit.", StringComparison.Ordinal)) return null;
        if (fallback.StartsWith("System.", StringComparison.Ordinal)) return null;
        return fallback;
    }

    private static bool? TryReadBool(object? source, params string[] memberNames)
    {
        object? value = TryReadValue(source, memberNames);
        if (value == null) return null;
        if (value is bool b) return b;
        if (bool.TryParse(value.ToString(), out bool parsed)) return parsed;
        return null;
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim();
    }

    private static bool IsIgnoredItemSource(object source)
    {
        Type type = source.GetType();
        if (typeof(Delegate).IsAssignableFrom(type)) return true;

        string name = type.Name;
        string fullName = type.FullName ?? name;

        if (name.StartsWith("Func`", StringComparison.Ordinal) ||
            name.StartsWith("Action`", StringComparison.Ordinal) ||
            name.Equals("Action", StringComparison.Ordinal) ||
            name.EndsWith("Action", StringComparison.Ordinal) ||
            name.EndsWith("Screen", StringComparison.Ordinal) ||
            name.EndsWith("Entry", StringComparison.Ordinal) ||
            name.EndsWith("Holder", StringComparison.Ordinal) ||
            name.Contains("Holder", StringComparison.Ordinal) ||
            name.EndsWith("Inventory", StringComparison.Ordinal) ||
            name.EndsWith("Reward", StringComparison.Ordinal) ||
            name.Equals("PurchaseStatus", StringComparison.Ordinal) ||
            name.Equals("CardCreationOptions", StringComparison.Ordinal) ||
            name.Equals("CardCreationResult", StringComparison.Ordinal) ||
            name.Equals("CardPile", StringComparison.Ordinal))
        {
            return true;
        }

        return fullName.StartsWith("System.", StringComparison.Ordinal);
    }

    private static List<DebriefItem> DeduplicateItems(List<DebriefItem> items)
    {
        List<DebriefItem> result = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (DebriefItem item in items)
        {
            string key = $"{item.Id}|{item.Name}|{item.UpgradeCount}";
            if (seen.Add(key)) result.Add(item);
        }

        return result;
    }
}
