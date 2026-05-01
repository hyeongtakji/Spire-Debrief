using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using SpireDebrief.SpireDebriefCode.Models;

namespace SpireDebrief.SpireDebriefCode.Services;

public static class RunHistoryDebriefFactory
{
    public static bool TryCreate(object? source, string modVersion, out RunDebriefLog log)
    {
        log = new RunDebriefLog();
        object? historySource = ReflectionDataExtractor.TryReadValue(source, "_history") ?? source;
        if (historySource is not RunHistory history || history.Players.Count == 0)
            return false;

        RunHistoryPlayer player = ResolveSelectedPlayer(source, history);
        List<FloorLog> floors = BuildFloors(history, player);
        log = new RunDebriefLog
        {
            RunId = $"history-{history.StartTime}-{player.Id}",
            Metadata = BuildMetadata(history, player, modVersion),
            FinalState = BuildFinalState(history, player),
            Floors = floors,
            Pathing = BuildPathing(history, floors)
        };
        log.Summary = BuildSummary(history, player, log);
        return true;
    }

    private static RunHistoryPlayer ResolveSelectedPlayer(object? source, RunHistory history)
    {
        object? selectedPlayer = ReflectionDataExtractor.TryReadValue(
            source,
            "_selectedPlayerIcon.Player",
            "SelectedPlayer",
            "Player");
        return selectedPlayer as RunHistoryPlayer ?? history.Players[0];
    }

    private static RunMetadata BuildMetadata(
        RunHistory history,
        RunHistoryPlayer player,
        string modVersion)
    {
        int finalFloor = history.MapPointHistory.Sum(act => act.Count);
        MapPointHistoryEntry? lastPoint = history.MapPointHistory.LastOrDefault()?.LastOrDefault();
        MapPointRoomHistoryEntry? lastRoom = lastPoint?.Rooms.LastOrDefault();
        return new RunMetadata
        {
            GameRunId = $"history-{history.StartTime}",
            Character = Text(SaveUtil.CharacterOrDeprecated(player.Character).Title) ?? player.Character.ToString(),
            Ascension = history.Ascension.ToString(),
            Seed = history.Seed,
            StartedAt = FormatStartTime(history.StartTime),
            GameVersion = history.BuildId,
            ModVersion = modVersion,
            Result = history.Win ? "Victory" : history.WasAbandoned ? "Abandoned" : "Defeat",
            FinalAct = history.Acts.LastOrDefault()?.ToString(),
            FinalFloor = finalFloor > 0 ? finalFloor : null,
            FinalRoom = NormalizeRoomType(lastRoom?.RoomType.ToString() ?? lastPoint?.MapPointType.ToString())
        };
    }

    private static FinalRunState BuildFinalState(RunHistory history, RunHistoryPlayer player)
    {
        PlayerMapPointHistoryEntry? lastStats = history.MapPointHistory
            .SelectMany(act => act)
            .Select(point => GetPlayerStats(point, player))
            .LastOrDefault(stats => stats != null);
        return new FinalRunState
        {
            Deck = player.Deck.Select(ToCardItem).WhereNotNull().ToList(),
            Relics = player.Relics.Select(ToRelicItem).WhereNotNull().ToList(),
            Potions = player.Potions.Select(ToPotionItem).WhereNotNull().ToList(),
            Gold = lastStats?.CurrentGold,
            CurrentHp = lastStats?.CurrentHp,
            MaxHp = lastStats?.MaxHp
        };
    }

    private static List<FloorLog> BuildFloors(RunHistory history, RunHistoryPlayer player)
    {
        List<FloorLog> floors = [];
        int floorNumber = 1;
        foreach (List<MapPointHistoryEntry> act in history.MapPointHistory)
        {
            foreach (MapPointHistoryEntry point in act)
            {
                PlayerMapPointHistoryEntry? stats = GetPlayerStats(point, player);
                FloorLog floor = BuildFloor(floorNumber, point, stats);
                floors.Add(floor);
                floorNumber++;
            }
        }

        return floors;
    }

    private static PathingLog BuildPathing(RunHistory history, IReadOnlyList<FloorLog> floors)
    {
        PathingLog pathing = new()
        {
            Source = "run_history_only",
            Note = "Old RunHistory data may not include node coordinates or full alternative-route map data. Full alternative-route analysis requires live telemetry captured during a run."
        };

        int floorNumber = 1;
        int floorIndex = 0;
        for (int actIndex = 0; actIndex < history.MapPointHistory.Count; actIndex++)
        {
            foreach (MapPointHistoryEntry point in history.MapPointHistory[actIndex])
            {
                MapPointRoomHistoryEntry? room = point.Rooms.LastOrDefault();
                string roomType = NormalizeRoomType(room?.RoomType.ToString() ?? point.MapPointType.ToString());
                string mapPointType = NormalizeRoomType(point.MapPointType.ToString());
                string summary = FormatPathingChoice(mapPointType, null);

                pathing.ActualPath.Add(new ActualPathStepLog
                {
                    Floor = floorNumber,
                    ActIndex = actIndex + 1,
                    MapPointType = mapPointType,
                    RoomType = roomType,
                    PathingChoiceSummary = summary
                });

                if (floorIndex < floors.Count)
                    floors[floorIndex].PathingChoice = summary;

                floorNumber++;
                floorIndex++;
            }
        }

        return pathing;
    }

    private static FloorLog BuildFloor(
        int floorNumber,
        MapPointHistoryEntry point,
        PlayerMapPointHistoryEntry? stats)
    {
        MapPointRoomHistoryEntry? room = point.Rooms.LastOrDefault();
        string roomType = NormalizeRoomType(room?.RoomType.ToString() ?? point.MapPointType.ToString());
        FloorLog floor = new()
        {
            Floor = floorNumber,
            RoomType = roomType,
            Encounter = ResolveEncounter(room),
            TurnsTaken = room?.TurnsTaken > 0 ? room.TurnsTaken : null,
            CurrentHp = stats?.CurrentHp,
            MaxHp = stats?.MaxHp,
            Gold = stats?.CurrentGold,
            DamageTaken = stats?.DamageTaken > 0 ? stats.DamageTaken : null
        };

        if (stats == null)
            return floor;

        AddCardChoices(floor, stats);
        AddCardGains(floor, stats);
        AddRewards(floor, stats);
        AddCardRemovals(floor, stats);
        AddEvent(floor, point, room, stats);
        AddShop(floor, stats);
        AddRestSite(floor, stats);
        return floor;
    }

    private static void AddCardChoices(FloorLog floor, PlayerMapPointHistoryEntry stats)
    {
        if (stats.CardChoices.Count == 0)
            return;

        CardRewardDecision reward = new()
        {
            Choices = stats.CardChoices.Select(choice => ToCardItem(choice.Card)).WhereNotNull().ToList()
        };
        CardChoiceHistoryEntry? picked = stats.CardChoices.Cast<CardChoiceHistoryEntry?>()
            .FirstOrDefault(choice => choice?.wasPicked == true);
        if (picked.HasValue)
            reward.Picked = ToCardItem(picked.Value.Card);
        else
            reward.Skipped = reward.Choices.Count > 0;

        if (reward.Choices.Count > 0)
            floor.CardRewards.Add(reward);
    }

    private static void AddCardGains(FloorLog floor, PlayerMapPointHistoryEntry stats)
    {
        if (floor.RoomType.Equals("Shop", StringComparison.OrdinalIgnoreCase))
            return;

        HashSet<string> representedPickedCards = stats.CardChoices
            .Where(choice => choice.wasPicked)
            .Select(choice => CardKey(choice.Card))
            .WhereText()
            .ToHashSet(StringComparer.Ordinal);

        foreach (SerializableCard card in stats.CardsGained)
        {
            string? key = CardKey(card);
            if (key != null && representedPickedCards.Remove(key))
                continue;

            DebriefItem? item = ToCardItem(card);
            if (item != null)
                floor.CardsGained.Add(item);
        }
    }

    private static void AddRewards(FloorLog floor, PlayerMapPointHistoryEntry stats)
    {
        foreach (ModelChoiceHistoryEntry choice in stats.RelicChoices.Where(choice => choice.wasPicked))
        {
            DebriefItem? item = ToRelicItem(choice.choice);
            if (item != null)
                floor.RelicRewards.Add(item);
        }

        foreach (ModelChoiceHistoryEntry choice in stats.PotionChoices.Where(choice => choice.wasPicked))
        {
            DebriefItem? item = ToPotionItem(choice.choice);
            if (item != null)
                floor.PotionRewards.Add(item);
        }
    }

    private static void AddCardRemovals(FloorLog floor, PlayerMapPointHistoryEntry stats)
    {
        floor.CardsRemoved.AddRange(stats.CardsRemoved.Select(ToCardItem).WhereNotNull());
    }

    private static void AddEvent(
        FloorLog floor,
        MapPointHistoryEntry point,
        MapPointRoomHistoryEntry? room,
        PlayerMapPointHistoryEntry stats)
    {
        if (point.MapPointType == MapPointType.Ancient)
        {
            floor.RoomType = "Event";
            floor.Event = new EventDecision
            {
                Name = "Ancient",
                Options = stats.AncientChoices.Select(choice => Text(choice.Title)).WhereText().ToList(),
                Chosen = Text(stats.GetAncientPickedChoiceLoc())
            };
            return;
        }

        if (stats.EventChoices.Count == 0 && room?.RoomType != RoomType.Event)
            return;

        EventDecision evt = new()
        {
            Name = ResolveEventName(room)
        };
        List<string> chosenOptions = stats.EventChoices
            .Select(choice => Text(choice.Title))
            .WhereText()
            .ToList();
        evt.Chosen = chosenOptions.Count == 0 ? null : string.Join(", ", chosenOptions);
        floor.Event = evt;
    }

    private static void AddShop(FloorLog floor, PlayerMapPointHistoryEntry stats)
    {
        bool isShopRoom = floor.RoomType.Equals("Shop", StringComparison.OrdinalIgnoreCase);
        List<DebriefItem> purchased = [];
        if (isShopRoom)
            purchased.AddRange(stats.CardsGained.Select(ToCardItem).WhereNotNull());
        purchased.AddRange(stats.BoughtColorless.Select(ToCardItem).WhereNotNull());
        purchased.AddRange(stats.BoughtRelics.Select(ToRelicItem).WhereNotNull());
        purchased.AddRange(stats.BoughtPotions.Select(ToPotionItem).WhereNotNull());

        List<DebriefItem> removed = isShopRoom ? floor.CardsRemoved.ToList() : [];
        if (purchased.Count == 0 && removed.Count == 0)
            return;

        floor.Shop = new ShopDecision
        {
            Purchased = purchased,
            Removed = removed
        };
    }

    private static void AddRestSite(FloorLog floor, PlayerMapPointHistoryEntry stats)
    {
        string? action = stats.RestSiteChoices.FirstOrDefault();
        if (action == null)
            return;

        floor.RoomType = "Rest";
        floor.RestSite = new RestSiteDecision
        {
            Action = NormalizeRestAction(action),
            Target = stats.UpgradedCards.Select(ToCardItem).FirstOrDefault(item => item != null)
        };
    }

    private static SummaryCounts BuildSummary(
        RunHistory history,
        RunHistoryPlayer player,
        RunDebriefLog log)
    {
        int startingRelics = SaveUtil.CharacterOrDeprecated(player.Character).StartingRelics.Count;
        int acquiredRelics = Math.Max(0, player.Relics.Count() - startingRelics);
        return new SummaryCounts
        {
            CardsPicked = log.Floors.SelectMany(f => f.CardRewards).Count(r => r.Picked != null),
            CardRewardsSkipped = log.Floors.SelectMany(f => f.CardRewards).Count(r => r.Skipped),
            CardsRemoved = log.Floors.Sum(f => f.CardsRemoved.Count),
            CardsUpgraded = history.MapPointHistory.SelectMany(act => act)
                .Select(GetPlayerStats)
                .WhereNotNull()
                .Sum(stats => stats.UpgradedCards.Count),
            RelicsAcquired = acquiredRelics,
            ShopsVisited = log.Floors.Count(f => f.RoomType.Equals("Shop", StringComparison.OrdinalIgnoreCase)),
            ElitesFought = log.Floors.Count(f => f.RoomType.Equals("Elite", StringComparison.OrdinalIgnoreCase))
        };

        PlayerMapPointHistoryEntry? GetPlayerStats(MapPointHistoryEntry point) =>
            RunHistoryDebriefFactory.GetPlayerStats(point, player);
    }

    private static PlayerMapPointHistoryEntry? GetPlayerStats(
        MapPointHistoryEntry point,
        RunHistoryPlayer player) =>
        point.PlayerStats.FirstOrDefault(stats => stats.PlayerId == player.Id);

    private static string? ResolveEncounter(MapPointRoomHistoryEntry? room)
    {
        if (room == null)
            return null;
        if (room.ModelId != null && IsCombatRoom(room.RoomType))
            return Text(SaveUtil.EncounterOrDeprecated(room.ModelId));
        if (room.MonsterIds.Count > 0)
            return string.Join(", ", room.MonsterIds.Select(id => Text(SaveUtil.MonsterOrDeprecated(id))));
        return room.ModelId?.ToString();
    }

    private static string? ResolveEventName(MapPointRoomHistoryEntry? room)
    {
        if (room?.ModelId == null)
            return null;
        return Text(SaveUtil.EventOrDeprecated(room.ModelId)) ?? room.ModelId.ToString();
    }

    private static DebriefItem? ToCardItem(SerializableCard card)
    {
        if (card.Id == null)
            return null;
        CardModel model = CardModel.FromSerializable(card);
        return new DebriefItem
        {
            Id = card.Id.ToString(),
            Name = model.Title,
            UpgradeCount = card.CurrentUpgradeLevel > 0 ? card.CurrentUpgradeLevel : null
        };
    }

    private static string? CardKey(SerializableCard card) =>
        card.Id == null ? null : $"{card.Id}:{card.CurrentUpgradeLevel}";

    private static DebriefItem? ToCardItem(ModelId id)
    {
        if (id == ModelId.none)
            return null;
        return new DebriefItem
        {
            Id = id.ToString(),
            Name = Text(SaveUtil.CardOrDeprecated(id)) ?? id.ToString()
        };
    }

    private static DebriefItem? ToRelicItem(SerializableRelic relic)
    {
        if (relic.Id == null)
            return null;
        return ToRelicItem(relic.Id);
    }

    private static DebriefItem? ToRelicItem(ModelId id)
    {
        if (id == ModelId.none)
            return null;
        return new DebriefItem
        {
            Id = id.ToString(),
            Name = Text(SaveUtil.RelicOrDeprecated(id).Title) ?? id.ToString()
        };
    }

    private static DebriefItem? ToPotionItem(SerializablePotion potion)
    {
        if (potion.Id == null)
            return null;
        return ToPotionItem(potion.Id);
    }

    private static DebriefItem? ToPotionItem(ModelId id)
    {
        if (id == ModelId.none)
            return null;
        return new DebriefItem
        {
            Id = id.ToString(),
            Name = Text(SaveUtil.PotionOrDeprecated(id).Title) ?? id.ToString()
        };
    }

    private static bool IsCombatRoom(RoomType roomType) =>
        roomType is RoomType.Monster or RoomType.Elite or RoomType.Boss;

    private static string NormalizeRoomType(string? roomType)
    {
        if (string.IsNullOrWhiteSpace(roomType))
            return "Unknown";
        if (roomType.Equals("RestSite", StringComparison.OrdinalIgnoreCase))
            return "Rest";
        if (roomType.Equals("Ancient", StringComparison.OrdinalIgnoreCase))
            return "Event";
        return roomType;
    }

    private static string NormalizeRestAction(string action) =>
        action.ToUpperInvariant() switch
        {
            "SMITH" => "Upgrade",
            "HEAL" => "Rest",
            _ => action
        };

    internal static string FormatPathingChoice(string roomType, string? nodeId) =>
        string.IsNullOrWhiteSpace(nodeId)
            ? $"chose {roomType}"
            : $"chose {roomType} at {nodeId}";

    private static string FormatStartTime(long startTime)
    {
        try
        {
            DateTimeOffset date = startTime > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(startTime)
                : DateTimeOffset.FromUnixTimeSeconds(startTime);
            return date.ToLocalTime().ToString("O");
        }
        catch
        {
            return startTime.ToString();
        }
    }

    private static string? Text(AbstractModel? model) =>
        model switch
        {
            CardModel card => card.Title,
            RelicModel relic => Text(relic.Title),
            PotionModel potion => Text(potion.Title),
            CharacterModel character => Text(character.Title),
            EncounterModel encounter => Text(encounter.Title),
            MonsterModel monster => Text(monster.Title),
            EventModel evt => Text(evt.Title),
            _ => model?.Id.ToString()
        };

    private static string? Text(LocString? locString) =>
        locString?.GetFormattedText() ?? locString?.GetRawText();
}

internal static class RunHistoryDebriefEnumerableExtensions
{
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
        where T : class =>
        source.Where(item => item != null)!;

    public static IEnumerable<string> WhereText(this IEnumerable<string?> source) =>
        source.Where(text => !string.IsNullOrWhiteSpace(text))!;
}
