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
        log.ExportLimitations = BuildExportLimitations(history, player, log);
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
            FinalAct = FormatFinalAct(history),
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
        AddPotionUsage(floor, stats);
        AddCardInstanceChanges(floor, stats);
        return floor;
    }

    private static void AddCardChoices(FloorLog floor, PlayerMapPointHistoryEntry stats)
    {
        if (IsShopRoom(floor))
            return;

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

        Dictionary<string, int> representedPickedCards = CardCounts(stats.CardChoices
            .Where(choice => choice.wasPicked)
            .Select(choice => choice.Card));
        Dictionary<string, int> removedCardCounts = CardCounts(stats.CardsRemoved);

        foreach (SerializableCard card in stats.CardsGained)
        {
            string? key = CardKey(card);
            if (ConsumeCount(representedPickedCards, key))
                continue;

            if (ConsumeCount(removedCardCounts, key))
                continue;

            DebriefItem? item = ToCardItem(card);
            if (item != null)
                floor.CardsGained.Add(item);
        }
    }

    private static void AddRewards(FloorLog floor, PlayerMapPointHistoryEntry stats)
    {
        if (IsShopRoom(floor))
            return;

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
        Dictionary<string, int> nonRewardGainedCards = NonRewardCardGainCounts(stats);
        foreach (SerializableCard card in stats.CardsRemoved)
        {
            if (ConsumeCount(nonRewardGainedCards, CardKey(card)))
                continue;

            DebriefItem? item = ToCardItem(card);
            if (item != null)
                floor.CardsRemoved.Add(item);
        }
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

        MapPointRoomHistoryEntry? eventRoom = point.Rooms.LastOrDefault(IsEventRoom);
        if (stats.EventChoices.Count == 0 && eventRoom == null && room?.RoomType != RoomType.Event)
            return;

        EventDecision evt = new()
        {
            Name = ResolveEventName(eventRoom ?? room)
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
        Dictionary<string, int> shopCardGainCounts = [];
        if (isShopRoom)
        {
            foreach (SerializableCard card in stats.CardsGained)
            {
                DebriefItem? item = ToCardItem(card);
                if (item != null)
                    purchased.Add(item);

                string? key = CardIdKey(card);
                if (key != null)
                    shopCardGainCounts[key] = shopCardGainCounts.GetValueOrDefault(key) + 1;
            }
        }

        foreach (ModelId id in stats.BoughtColorless)
        {
            string? key = CardIdKey(id);
            if (ConsumeCount(shopCardGainCounts, key))
                continue;

            DebriefItem? item = ToCardItem(id);
            if (item != null)
                purchased.Add(item);
        }

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
            Target = stats.UpgradedCards.Select(ToUpgradedCardItem).FirstOrDefault(item => item != null)
        };
    }

    private static void AddPotionUsage(FloorLog floor, PlayerMapPointHistoryEntry stats)
    {
        floor.PotionsUsed.AddRange(stats.PotionUsed.Select(ToPotionItem).WhereNotNull());
        floor.PotionsDiscarded.AddRange(stats.PotionDiscarded.Select(ToPotionItem).WhereNotNull());
    }

    private static void AddCardInstanceChanges(FloorLog floor, PlayerMapPointHistoryEntry stats)
    {
        foreach (ModelId upgradedCardId in stats.UpgradedCards)
        {
            DebriefItem? before = ToCardItem(upgradedCardId);
            DebriefItem? after = ToUpgradedCardItem(upgradedCardId);
            if (before == null || after == null)
                continue;

            floor.CardInstanceChanges.Add(new CardInstanceChangeLog
            {
                Floor = floor.Floor,
                CardBefore = before,
                CardAfter = after,
                ChangeKind = "upgrade",
                Description = $"{before.BaseDisplayName} upgraded to {after.BaseDisplayName}",
                IsUpgrade = true
            });
        }

        foreach (CardEnchantmentHistoryEntry entry in stats.CardsEnchanted)
        {
            SerializableCard? enchantedCard = entry.Card;
            CardInstanceMetadata? metadata = CardInstanceMetadataExtractor.ExtractEnchantment(
                enchantedCard?.Enchantment,
                entry.Enchantment);
            DebriefItem? card = enchantedCard == null ? null : ToCardItem(enchantedCard);
            string metadataText = metadata == null
                ? "unknown enchantment"
                : CardInstanceMetadataExtractor.FormatForChange([metadata]);

            floor.CardInstanceChanges.Add(new CardInstanceChangeLog
            {
                Floor = floor.Floor,
                CardAfter = card,
                ChangeKind = "enchantment",
                Description = card == null
                    ? $"card enchantment target unavailable; gained {metadataText}"
                    : $"{card.BaseDisplayName} gained {metadataText}",
                IsUncertain = card == null
            });
        }
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

    private static List<string> BuildExportLimitations(
        RunHistory history,
        RunHistoryPlayer player,
        RunDebriefLog log)
    {
        List<string> limitations = [];

        if (log.Floors.Any(floor => floor.Encounter != null ||
            floor.RoomType.Equals("Monster", StringComparison.OrdinalIgnoreCase) ||
            floor.RoomType.Equals("Elite", StringComparison.OrdinalIgnoreCase) ||
            floor.RoomType.Equals("Boss", StringComparison.OrdinalIgnoreCase)))
        {
            limitations.Add("Turn-by-turn combat order is not available from RunHistory.");
        }

        if (log.Floors.Any(floor => floor.Shop != null ||
            floor.RoomType.Equals("Shop", StringComparison.OrdinalIgnoreCase)))
        {
            limitations.Add("Full shop inventories for unpurchased items are not available from RunHistory.");
        }

        if (log.Floors.Any(floor => floor.Event != null))
            limitations.Add("Some event option text and option effects may not be available from RunHistory.");

        if (history.MapPointHistory
            .SelectMany(act => act)
            .Select(point => GetPlayerStats(point, player))
            .WhereNotNull()
            .Any(stats => stats.PotionUsed.Count > 0 || stats.PotionDiscarded.Count > 0))
        {
            limitations.Add("Potion use/discard is floor-level only; exact turn timing is not available.");
        }

        List<int> cardTargetUnavailableFloors = log.Floors
            .Where(floor => floor.CardInstanceChanges.Any(change =>
                change.ChangeKind.Equals("enchantment", StringComparison.OrdinalIgnoreCase) &&
                change.IsUncertain))
            .Select(floor => floor.Floor)
            .ToList();
        if (cardTargetUnavailableFloors.Count > 0)
        {
            limitations.Add(
                $"Card modifier target data was unavailable for floors: {string.Join(", ", cardTargetUnavailableFloors)}.");
        }

        return limitations;
    }

    private static PlayerMapPointHistoryEntry? GetPlayerStats(
        MapPointHistoryEntry point,
        RunHistoryPlayer player) =>
        point.PlayerStats.FirstOrDefault(stats => stats.PlayerId == player.Id);

    private static string? ResolveEncounter(MapPointRoomHistoryEntry? room)
    {
        if (room == null)
            return null;

        if (!IsCombatRoom(room.RoomType))
            return null;

        if (room.ModelId != null)
            return Text(SaveUtil.EncounterOrDeprecated(room.ModelId));
        if (room.MonsterIds.Count > 0)
            return string.Join(", ", room.MonsterIds.Select(id => Text(SaveUtil.MonsterOrDeprecated(id))));
        return null;
    }

    private static string? ResolveEventName(MapPointRoomHistoryEntry? room)
    {
        if (room?.ModelId == null)
            return null;

        try
        {
            return Text(SaveUtil.EventOrDeprecated(room.ModelId)) ?? room.ModelId.ToString();
        }
        catch
        {
            return room.ModelId.ToString();
        }
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
            UpgradeCount = card.CurrentUpgradeLevel > 0 ? card.CurrentUpgradeLevel : null,
            InstanceMetadata = CardInstanceMetadataExtractor.Extract(card)
        };
    }

    private static string? CardKey(SerializableCard card) =>
        card.Id == null ? null : $"{card.Id}:{card.CurrentUpgradeLevel}";

    private static string? CardIdKey(SerializableCard card) =>
        card.Id == null ? null : card.Id.ToString();

    private static string? CardIdKey(ModelId id) =>
        id == ModelId.none ? null : id.ToString();

    private static Dictionary<string, int> NonRewardCardGainCounts(PlayerMapPointHistoryEntry stats)
    {
        Dictionary<string, int> representedPickedCards = CardCounts(stats.CardChoices
            .Where(choice => choice.wasPicked)
            .Select(choice => choice.Card));
        Dictionary<string, int> counts = [];

        foreach (SerializableCard card in stats.CardsGained)
        {
            string? key = CardKey(card);
            if (ConsumeCount(representedPickedCards, key) || key == null)
                continue;

            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts;
    }

    private static Dictionary<string, int> CardCounts(IEnumerable<SerializableCard> cards)
    {
        Dictionary<string, int> counts = [];
        foreach (SerializableCard card in cards)
        {
            string? key = CardKey(card);
            if (key == null)
                continue;

            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts;
    }

    private static bool ConsumeCount(Dictionary<string, int> counts, string? key)
    {
        if (key == null || !counts.TryGetValue(key, out int count) || count <= 0)
            return false;

        if (count == 1)
            counts.Remove(key);
        else
            counts[key] = count - 1;

        return true;
    }

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

    private static DebriefItem? ToUpgradedCardItem(ModelId id)
    {
        DebriefItem? item = ToCardItem(id);
        if (item != null)
            item.UpgradeCount = 1;
        return item;
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

    private static bool IsShopRoom(FloorLog floor) =>
        floor.RoomType.Equals("Shop", StringComparison.OrdinalIgnoreCase);

    private static bool IsEventRoom(MapPointRoomHistoryEntry room) =>
        room.RoomType == RoomType.Event;

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

    private static string? FormatFinalAct(RunHistory history) =>
        history.Acts.Count > 0 ? $"Act {history.Acts.Count}" : null;

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
            EnchantmentModel enchantment => Text(enchantment.Title),
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
