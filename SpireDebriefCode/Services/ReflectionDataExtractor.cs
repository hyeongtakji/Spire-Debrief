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
        return value switch
        {
            null => null,
            string s when string.IsNullOrWhiteSpace(s) => null,
            string s => s,
            _ => value.ToString()
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
        if (source == null) return new DebriefItem();

        object model = TryReadValue(source, "Model", "CardModel", "RelicModel", "PotionModel") ?? source;
        string? name = TryReadString(model, NameMembers) ?? TryReadString(source, NameMembers);
        string? id = TryReadString(model, IdMembers) ?? TryReadString(source, IdMembers);

        int? upgradeCount =
            TryReadInt(source, "UpgradeCount", "TimesUpgraded", "Upgrades") ??
            TryReadInt(model, "UpgradeCount", "TimesUpgraded", "Upgrades");

        bool upgraded =
            TryReadBool(source, "Upgraded", "IsUpgraded", "IsPlus") ??
            TryReadBool(model, "Upgraded", "IsUpgraded", "IsPlus") ??
            false;

        return new DebriefItem
        {
            Id = Clean(id),
            Name = Clean(name) ?? Clean(id) ?? source.GetType().Name,
            UpgradeCount = upgradeCount is > 0 ? upgradeCount : upgraded ? 1 : null
        };
    }

    public static List<DebriefItem> ExtractItems(object? source, params string[] containerMembers)
    {
        List<DebriefItem> items = [];
        foreach (object? candidate in EnumerateCandidates(source, containerMembers))
        {
            if (candidate == null || candidate is string) continue;
            items.Add(ToItem(candidate));
        }

        return DeduplicateItems(items);
    }

    public static List<string> ExtractStrings(object? source, params string[] containerMembers)
    {
        List<string> strings = [];
        foreach (object? candidate in EnumerateCandidates(source, containerMembers))
        {
            string? value = candidate is string s ? s : TryReadString(candidate, NameMembers);
            value = Clean(value);
            if (value != null) strings.Add(value);
        }

        return strings.Distinct(StringComparer.Ordinal).ToList();
    }

    public static void FillMetadata(RunDebriefLog log, object? source)
    {
        if (source == null) return;

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
            finalState.Deck = ExtractItems(player, "Deck", "Cards", "MasterDeck", "DrawPile", "CardPile");
        if (finalState.Relics.Count == 0)
            finalState.Relics = ExtractItems(player, "Relics", "RelicInventory");
        if (finalState.Potions.Count == 0)
            finalState.Potions = ExtractItems(player, "Potions", "PotionSlots");

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
