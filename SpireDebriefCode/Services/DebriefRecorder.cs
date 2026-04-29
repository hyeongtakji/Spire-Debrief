using SpireDebrief.SpireDebriefCode.Models;

namespace SpireDebrief.SpireDebriefCode.Services;

public static class DebriefRecorder
{
    private static readonly object Sync = new();
    private static string _modVersion = "unknown";
    private static RunDebriefLog? _active;
    private static CardRewardDecision? _pendingCardReward;
    private static int _lastFloor;

    public static RunDebriefLog ActiveOrLatest
    {
        get
        {
            lock (Sync)
            {
                _active ??= DebriefStorage.LoadLatestInProgressJson();
                if (_active != null) return _active;

                return DebriefStorage.LoadLatestJson() ?? CreateRun();
            }
        }
    }

    public static void Initialize(string modVersion)
    {
        lock (Sync)
        {
            _modVersion = modVersion;
            _active = DebriefStorage.LoadLatestInProgressJson();
            if (_active != null)
            {
                _active.Metadata.ModVersion ??= modVersion;
                RestoreTransientState(_active);
            }
        }
    }

    public static void BeginRun(object? runSource = null)
    {
        lock (Sync)
        {
            RunDebriefLog? matchingLog = DebriefStorage.LoadBestMatchingInProgressJson(runSource);
            if (matchingLog != null)
            {
                _active = matchingLog;
                ReflectionDataExtractor.FillMetadata(_active, runSource);
                RestoreTransientState(_active);
                Save();
                return;
            }

            if (_active != null &&
                DebriefStorage.IsInProgress(_active) &&
                CanContinueActiveRun(_active, runSource))
            {
                ReflectionDataExtractor.FillMetadata(_active, runSource);
                RestoreTransientState(_active);
                Save();
                return;
            }

            _active = CreateRun();
            ReflectionDataExtractor.FillMetadata(_active, runSource);
            RestoreTransientState(_active);
            Save();
        }
    }

    public static void EnterRoom(object? roomSource = null, string? fallbackRoomType = null)
    {
        lock (Sync)
        {
            RunDebriefLog log = EnsureRun();
            string roomType = ReflectionDataExtractor.TryReadString(
                roomSource,
                "RoomType",
                "Type",
                "Room.Type",
                "Room.RoomType",
                "MapPointType") ?? fallbackRoomType ?? "Unknown";
            roomType = NormalizeRoomType(roomType);
            if (roomType is "Map" or "Unassigned")
                return;

            int floor = ResolveFloor(roomSource);
            _lastFloor = Math.Max(_lastFloor, floor);
            FloorLog floorLog = EnsureFloor(log, floor, roomType);

            floorLog.RoomType = roomType;
            floorLog.Encounter ??= ReflectionDataExtractor.TryReadString(roomSource, "Encounter.Name", "Encounter.Id", "MonsterGroup.Name", "Name");
            if (floorLog.RoomType.Equals("Elite", StringComparison.OrdinalIgnoreCase))
                log.Summary.ElitesFought = CountRooms(log, "Elite");
            if (floorLog.RoomType.Equals("Shop", StringComparison.OrdinalIgnoreCase))
                log.Summary.ShopsVisited = CountRooms(log, "Shop");

            ReflectionDataExtractor.FillMetadata(log, roomSource);
            Save();
        }
    }

    public static void RecordCardRewardShown(object? source)
    {
        lock (Sync)
        {
            RunDebriefLog log = EnsureRun();
            FloorLog floor = EnsureFloor(log, _lastFloor, "Unknown");
            List<DebriefItem> choices = ReflectionDataExtractor.ExtractItemsWithIdPrefix(
                source,
                "CARD.",
                "Cards", "CardRewards", "RewardCards", "Choices", "Options", "_cards", "_rewardCards");
            if (choices.Count == 0) return;

            if (_pendingCardReward != null && _pendingCardReward.Picked == null && !_pendingCardReward.Skipped)
                return;

            _pendingCardReward = new CardRewardDecision
            {
                Choices = choices
            };
            floor.CardRewards.Add(_pendingCardReward);
            Save();
        }
    }

    public static void RecordCardPicked(object? cardSource)
    {
        lock (Sync)
        {
            RunDebriefLog log = EnsureRun();
            FloorLog floor = EnsureFloor(log, _lastFloor, "Unknown");
            CardRewardDecision reward = _pendingCardReward ?? new CardRewardDecision();
            if (reward.Choices.Count == 0) return;
            if (!floor.CardRewards.Contains(reward))
                floor.CardRewards.Add(reward);

            DebriefItem? picked = ResolvePickedCard(reward, cardSource);
            if (picked == null) return;

            reward.Picked = picked;
            reward.Skipped = false;
            log.Summary.CardsPicked++;
            _pendingCardReward = null;
            Save();
        }
    }

    public static void RecordCardRewardSkipped(string methodName)
    {
        lock (Sync)
        {
            RunDebriefLog log = EnsureRun();
            FloorLog floor = EnsureFloor(log, _lastFloor, "Unknown");
            CardRewardDecision reward = _pendingCardReward ?? new CardRewardDecision();
            if (reward.Choices.Count == 0 && _pendingCardReward == null) return;
            if (!floor.CardRewards.Contains(reward))
                floor.CardRewards.Add(reward);

            reward.Skipped = true;
            reward.Source = $"skip:{methodName}";
            log.Summary.CardRewardsSkipped++;
            _pendingCardReward = null;
            Save();
        }
    }

    public static void RecordEventOptions(object? source)
    {
        lock (Sync)
        {
            RunDebriefLog log = EnsureRun();
            FloorLog floor = EnsureFloor(log, _lastFloor, "Event");
            floor.RoomType = "Event";
            floor.Event ??= new EventDecision();
            floor.Event.Name ??= ReflectionDataExtractor.TryReadString(source, "_event.Name", "_event.Id", "Event.Name", "Event.Id", "Name", "Id");
            List<string> options = ReflectionDataExtractor.ExtractStrings(source, "OptionButtons", "Options", "EventOptions", "CurrentOptions", "_options");
            foreach (string option in options)
            {
                if (!floor.Event.Options.Any(existing => string.Equals(existing, option, StringComparison.Ordinal)))
                    floor.Event.Options.Add(option);
            }

            Save();
        }
    }

    public static void RecordEventChoice(object? choiceSource, object? eventSource = null)
    {
        lock (Sync)
        {
            RunDebriefLog log = EnsureRun();
            FloorLog floor = EnsureFloor(log, _lastFloor, "Event");
            floor.RoomType = "Event";
            floor.Event ??= new EventDecision();
            floor.Event.Name ??= ReflectionDataExtractor.TryReadString(eventSource, "_event.Name", "_event.Id", "Event.Name", "Event.Id", "Name", "Id");
            string? chosen = ReflectionDataExtractor.TryReadString(choiceSource, "Text", "Label", "Name", "Title", "Id");
            if (!IsCleanDecisionText(chosen) || IsGenericEventChoice(chosen)) return;

            floor.Event.Chosen = chosen;
            floor.Event.Result ??= ReflectionDataExtractor.TryReadString(eventSource, "Result", "Outcome", "LastResult");
            Save();
        }
    }

    public static void RecordShopPurchase(object? purchaseSource)
    {
        lock (Sync)
        {
            RunDebriefLog log = EnsureRun();
            FloorLog floor = EnsureFloor(log, _lastFloor, "Shop");
            floor.RoomType = "Shop";
            floor.Shop ??= new ShopDecision();
            if (!TryResolveShopItem(purchaseSource, out DebriefItem item))
                return;
            if (!IsShopItem(item)) return;

            floor.Shop.Purchased.Add(item);
            log.Summary.ShopsVisited = Math.Max(log.Summary.ShopsVisited, CountRooms(log, "Shop"));
            Save();
        }
    }

    public static void RecordRelicReward(object? relicSource)
    {
        lock (Sync)
        {
            RunDebriefLog log = EnsureRun();
            FloorLog floor = EnsureFloor(log, _lastFloor, "Unknown");
            if (!TryResolveRewardItem(relicSource, "RELIC.", out DebriefItem item))
                return;

            floor.RelicRewards.Add(item);
            log.Summary.RelicsAcquired++;
            Save();
        }
    }

    public static void RecordPotionReward(object? potionSource)
    {
        lock (Sync)
        {
            RunDebriefLog log = EnsureRun();
            FloorLog floor = EnsureFloor(log, _lastFloor, "Unknown");
            if (!TryResolveRewardItem(potionSource, "POTION.", out DebriefItem item))
                return;

            floor.PotionRewards.Add(item);
            Save();
        }
    }

    public static void RecordCardRemoved(object? cardSource)
    {
        lock (Sync)
        {
            RunDebriefLog log = EnsureRun();
            FloorLog floor = EnsureFloor(log, _lastFloor, "Shop");
            floor.Shop ??= new ShopDecision();
            if (!ReflectionDataExtractor.TryToItem(cardSource, out DebriefItem item, "CARD."))
                return;

            floor.Shop.Removed.Add(item);
            log.Summary.CardsRemoved++;
            Save();
        }
    }

    public static void RecordRestSiteAction(string action, object? targetSource = null)
    {
        lock (Sync)
        {
            RunDebriefLog log = EnsureRun();
            FloorLog floor = EnsureFloor(log, _lastFloor, "Rest");
            floor.RoomType = "Rest";
            floor.RestSite = new RestSiteDecision
            {
                Action = action,
                Target = ReflectionDataExtractor.TryToItem(targetSource, out DebriefItem item, "CARD.") ? item : null
            };
            if (action.Equals("Upgrade", StringComparison.OrdinalIgnoreCase) ||
                action.Equals("Smith", StringComparison.OrdinalIgnoreCase))
            {
                log.Summary.CardsUpgraded++;
            }
            Save();
        }
    }

    public static void CompleteRun(object? runSource = null, string? result = null)
    {
        lock (Sync)
        {
            RunDebriefLog log = EnsureRun();
            ReflectionDataExtractor.FillMetadata(log, runSource);
            ReflectionDataExtractor.FillFinalState(log.FinalState, runSource);
            log.Metadata.Result = result ?? log.Metadata.Result ?? ReflectionDataExtractor.TryReadString(runSource, "Result", "Outcome") ?? "Unknown";
            log.Metadata.EndedAt = DateTimeOffset.Now.ToString("O");
            if (log.Metadata.FinalFloor is null or <= 0)
                log.Metadata.FinalFloor = _lastFloor;
            Save();
        }
    }

    public static RunDebriefLog? CreateExportLog(object? runHistorySource)
    {
        lock (Sync)
        {
            RunDebriefLog? sourceLog = DebriefStorage.LoadBestMatchingJson(runHistorySource);
            if (sourceLog == null)
                return null;

            RunDebriefLog log = CloneRun(sourceLog);
            ReflectionDataExtractor.FillMetadata(log, runHistorySource);
            ReflectionDataExtractor.FillFinalState(log.FinalState, runHistorySource);
            NormalizeForExport(log);
            return log;
        }
    }

    private static RunDebriefLog CreateRun() => new()
    {
        Metadata =
        {
            ModVersion = _modVersion,
            StartedAt = DateTimeOffset.Now.ToString("O")
        }
    };

    private static RunDebriefLog CloneRun(RunDebriefLog source) => new()
    {
        SchemaVersion = source.SchemaVersion,
        RunId = source.RunId,
        Metadata = CloneMetadata(source.Metadata),
        FinalState = CloneFinalState(source.FinalState),
        Floors = source.Floors.Select(CloneFloor).ToList(),
        Summary = CloneSummary(source.Summary)
    };

    private static RunMetadata CloneMetadata(RunMetadata source) => new()
    {
        GameRunId = source.GameRunId,
        Character = source.Character,
        Ascension = source.Ascension,
        Difficulty = source.Difficulty,
        Seed = source.Seed,
        StartedAt = source.StartedAt,
        EndedAt = source.EndedAt,
        GameVersion = source.GameVersion,
        ModVersion = source.ModVersion,
        Result = source.Result,
        FinalAct = source.FinalAct,
        FinalFloor = source.FinalFloor,
        FinalRoom = source.FinalRoom
    };

    private static FinalRunState CloneFinalState(FinalRunState source) => new()
    {
        Deck = CloneItems(source.Deck),
        Relics = CloneItems(source.Relics),
        Potions = CloneItems(source.Potions),
        Gold = source.Gold,
        CurrentHp = source.CurrentHp,
        MaxHp = source.MaxHp
    };

    private static FloorLog CloneFloor(FloorLog source) => new()
    {
        Floor = source.Floor,
        RoomType = source.RoomType,
        Encounter = source.Encounter,
        PathingChoice = source.PathingChoice,
        DamageTaken = source.DamageTaken,
        CardRewards = source.CardRewards.Select(CloneCardReward).ToList(),
        RelicRewards = CloneItems(source.RelicRewards),
        PotionRewards = CloneItems(source.PotionRewards),
        Event = source.Event == null ? null : CloneEvent(source.Event),
        Shop = source.Shop == null ? null : CloneShop(source.Shop),
        RestSite = source.RestSite == null ? null : CloneRestSite(source.RestSite),
        Notes = source.Notes.ToList()
    };

    private static CardRewardDecision CloneCardReward(CardRewardDecision source) => new()
    {
        Choices = CloneItems(source.Choices),
        Picked = source.Picked == null ? null : CloneItem(source.Picked),
        Skipped = source.Skipped,
        Source = source.Source
    };

    private static EventDecision CloneEvent(EventDecision source) => new()
    {
        Name = source.Name,
        Options = source.Options.ToList(),
        Chosen = source.Chosen,
        Result = source.Result
    };

    private static ShopDecision CloneShop(ShopDecision source) => new()
    {
        Purchased = CloneItems(source.Purchased),
        Removed = CloneItems(source.Removed)
    };

    private static RestSiteDecision CloneRestSite(RestSiteDecision source) => new()
    {
        Action = source.Action,
        Target = source.Target == null ? null : CloneItem(source.Target)
    };

    private static SummaryCounts CloneSummary(SummaryCounts source) => new()
    {
        CardsPicked = source.CardsPicked,
        CardRewardsSkipped = source.CardRewardsSkipped,
        CardsRemoved = source.CardsRemoved,
        CardsUpgraded = source.CardsUpgraded,
        RelicsAcquired = source.RelicsAcquired,
        ShopsVisited = source.ShopsVisited,
        ElitesFought = source.ElitesFought
    };

    private static List<DebriefItem> CloneItems(IEnumerable<DebriefItem> source) =>
        source.Select(CloneItem).ToList();

    private static DebriefItem CloneItem(DebriefItem source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        UpgradeCount = source.UpgradeCount
    };

    private static RunDebriefLog EnsureRun()
    {
        if (_active != null && DebriefStorage.IsInProgress(_active))
            return _active;

        _active = DebriefStorage.LoadLatestInProgressJson() ?? CreateRun();
        RestoreTransientState(_active);
        return _active;
    }

    private static bool CanContinueActiveRun(RunDebriefLog log, object? runSource)
    {
        if (!DebriefStorage.HasUsableIdentity(runSource))
            return true;

        if (log.Metadata.GameRunId == null && log.Metadata.Seed == null)
            return true;

        return DebriefStorage.MatchesIdentity(log, runSource);
    }

    private static void RestoreTransientState(RunDebriefLog log)
    {
        _pendingCardReward = null;

        int maxFloor = log.Floors.Count == 0 ? 0 : log.Floors.Max(floor => floor.Floor);
        if (log.Metadata.FinalFloor is int finalFloor)
            maxFloor = Math.Max(maxFloor, finalFloor);

        _lastFloor = Math.Max(0, maxFloor);
    }

    private static FloorLog EnsureFloor(RunDebriefLog log, int floor, string roomType)
    {
        floor = Math.Max(1, floor);
        FloorLog? existing = log.Floors.FirstOrDefault(f => f.Floor == floor);
        if (existing != null) return existing;

        FloorLog created = new()
        {
            Floor = floor,
            RoomType = NormalizeRoomType(roomType)
        };
        log.Floors.Add(created);
        log.Floors.Sort((a, b) => a.Floor.CompareTo(b.Floor));
        return created;
    }

    private static string NormalizeRoomType(string roomType)
    {
        if (string.IsNullOrWhiteSpace(roomType)) return "Unknown";
        string lower = roomType.ToLowerInvariant();
        if (lower.Contains("elite")) return "Elite";
        if (lower.Contains("boss chest")) return "Boss Chest";
        if (lower.Contains("boss")) return "Boss";
        if (lower.Contains("shop") || lower.Contains("merchant")) return "Shop";
        if (lower.Contains("treasure") || lower.Contains("chest")) return "Treasure";
        if (lower.Contains("rest") || lower.Contains("campfire")) return "Rest";
        if (lower.Contains("event") || lower.Contains("ancient")) return "Event";
        if (lower.Contains("monster") || lower.Contains("enemy") || lower.Contains("combat")) return "Monster";
        return roomType.Trim();
    }

    private static int ResolveFloor(object? roomSource)
    {
        int? floor = ReflectionDataExtractor.TryReadInt(
            roomSource,
            "Floor",
            "FloorNumber",
            "Room.Floor",
            "MapPoint.Floor",
            "MapPoint.FloorNumber",
            "Depth",
            "Y",
            "Coords.Y",
            "Coordinates.Y",
            "Position.Y");
        if (floor is int explicitFloor && explicitFloor > 0)
            return explicitFloor;

        int? roomId = ReflectionDataExtractor.TryReadInt(roomSource, "Id");
        if (roomId is int id && id >= 0)
            return id + 1;

        return _lastFloor + 1;
    }

    private static int CountRooms(RunDebriefLog log, string roomType) =>
        log.Floors.Count(f => f.RoomType.Equals(roomType, StringComparison.OrdinalIgnoreCase));

    private static DebriefItem? ResolvePickedCard(CardRewardDecision reward, object? cardSource)
    {
        if (TryConvertIndex(cardSource, out int index) && index >= 0 && index < reward.Choices.Count)
            return reward.Choices[index];
        DebriefItem? selected = ResolvePickedCardItem(reward, cardSource);
        if (selected != null) return selected;
        return ReflectionDataExtractor.TryToItem(cardSource, out DebriefItem item, "CARD.") ? item : null;
    }

    private static DebriefItem? ResolvePickedCardItem(CardRewardDecision reward, object? cardSource)
    {
        List<DebriefItem> selectedCards = ReflectionDataExtractor.ExtractItemsWithIdPrefix(
            cardSource,
            "CARD.",
            "SelectedCard",
            "SelectedCards",
            "Selected",
            "Selection",
            "CardNode.Model",
            "_cardNode.Model",
            "Card",
            "Cards");
        if (selectedCards.Count == 0)
            selectedCards = ReflectionDataExtractor.ExtractItemsWithIdPrefix(cardSource, "CARD.");
        if (selectedCards.Count == 0)
            return null;

        DebriefItem selected = selectedCards[0];
        return reward.Choices.FirstOrDefault(choice =>
            selected.Id != null &&
            string.Equals(choice.Id, selected.Id, StringComparison.OrdinalIgnoreCase)) ?? selected;
    }

    private static bool TryConvertIndex(object? source, out int index)
    {
        index = -1;
        if (source is int i)
        {
            index = i;
            return true;
        }
        if (source == null) return false;
        return int.TryParse(source.ToString(), out index);
    }

    private static bool IsShopItem(DebriefItem item) =>
        item.Id != null &&
        (item.Id.StartsWith("CARD.", StringComparison.OrdinalIgnoreCase) ||
         item.Id.StartsWith("RELIC.", StringComparison.OrdinalIgnoreCase) ||
         item.Id.StartsWith("POTION.", StringComparison.OrdinalIgnoreCase));

    private static bool TryResolveShopItem(object? source, out DebriefItem item)
    {
        if (ReflectionDataExtractor.TryToItem(source, out item) && IsShopItem(item))
            return true;

        foreach (string prefix in new[] { "CARD.", "RELIC.", "POTION." })
        {
            List<DebriefItem> items = ReflectionDataExtractor.ExtractItemsWithIdPrefix(
                source,
                prefix,
                "Entry",
                "MerchantEntry",
                "Item",
                "Reward",
                "_cardEntry.CreationResult.Card",
                "Entry.CreationResult.Card",
                "_relic",
                "_relicEntry.Model",
                "Entry.Model",
                "_potion",
                "_potionEntry.Model",
                "Card",
                "Relic",
                "Potion",
                "Model",
                "CardModel",
                "RelicModel",
                "PotionModel");
            if (items.Count == 0) continue;

            item = items[0];
            return true;
        }

        item = new DebriefItem();
        return false;
    }

    private static bool TryResolveRewardItem(object? source, string prefix, out DebriefItem item)
    {
        if (ReflectionDataExtractor.TryToItem(source, out item, prefix))
            return true;

        List<DebriefItem> items = ReflectionDataExtractor.ExtractItemsWithIdPrefix(
            source,
            prefix,
            "_relic",
            "ClaimedRelic",
            "Relic",
            "_potion",
            "Potion",
            "ClaimedPotion",
            "Model");
        if (items.Count == 0)
        {
            item = new DebriefItem();
            return false;
        }

        item = items[0];
        return true;
    }

    private static void NormalizeForExport(RunDebriefLog log)
    {
        log.FinalState.Deck = FilterItems(log.FinalState.Deck, "CARD.");
        log.FinalState.Relics = FilterItems(log.FinalState.Relics, "RELIC.");
        log.FinalState.Potions = FilterItems(log.FinalState.Potions, "POTION.");

        foreach (FloorLog floor in log.Floors)
        {
            floor.CardRewards = floor.CardRewards
                .Select(NormalizeCardReward)
                .Where(reward => reward != null)
                .Cast<CardRewardDecision>()
                .ToList();
            floor.RelicRewards = FilterItems(floor.RelicRewards, "RELIC.");
            floor.PotionRewards = FilterItems(floor.PotionRewards, "POTION.");

            if (floor.Shop != null)
            {
                floor.Shop.Purchased = floor.Shop.Purchased.Where(IsShopItem).Where(IsCleanItem).ToList();
                floor.Shop.Removed = FilterItems(floor.Shop.Removed, "CARD.");
            }

            if (floor.Event != null)
                floor.Event = NormalizeEvent(floor.Event);
            if (floor.RestSite != null &&
                (!IsKnownRestSiteAction(floor.RestSite.Action) ||
                 !floor.RoomType.Equals("Rest", StringComparison.OrdinalIgnoreCase)))
            {
                floor.RestSite = null;
            }
        }

        log.Summary = new SummaryCounts
        {
            CardsPicked = log.Floors.SelectMany(f => f.CardRewards).Count(r => r.Picked != null),
            CardRewardsSkipped = log.Floors.SelectMany(f => f.CardRewards).Count(r => r.Skipped),
            CardsRemoved = log.Floors.Sum(f => f.Shop?.Removed.Count ?? 0),
            CardsUpgraded = log.Floors.Count(f => f.RestSite?.Action is "Upgrade" or "Smith"),
            RelicsAcquired = log.Floors.Sum(f => f.RelicRewards.Count),
            ShopsVisited = log.Floors.Count(f => f.RoomType.Equals("Shop", StringComparison.OrdinalIgnoreCase)),
            ElitesFought = log.Floors.Count(f => f.RoomType.Equals("Elite", StringComparison.OrdinalIgnoreCase))
        };
    }

    private static CardRewardDecision? NormalizeCardReward(CardRewardDecision reward)
    {
        bool verifiedSkip = reward.Source?.StartsWith("skip:", StringComparison.Ordinal) == true;
        reward.Source = null;
        reward.Choices = FilterItems(reward.Choices, "CARD.");
        if (reward.Picked != null && (!HasPrefix(reward.Picked, "CARD.") || !IsCleanItem(reward.Picked)))
            reward.Picked = null;
        if (reward.Skipped && !verifiedSkip)
            reward.Skipped = false;
        if (reward.Picked == null && !reward.Skipped) return null;
        if (reward.Choices.Count == 0 && reward.Picked == null) return null;
        return reward;
    }

    private static EventDecision? NormalizeEvent(EventDecision evt)
    {
        evt.Name = IsCleanDecisionText(evt.Name) ? evt.Name : null;
        evt.Chosen = IsCleanDecisionText(evt.Chosen) && !IsGenericEventChoice(evt.Chosen)
            ? evt.Chosen
            : null;
        evt.Result = IsCleanDecisionText(evt.Result) ? evt.Result : null;
        evt.Options = evt.Options
            .Where(IsCleanDecisionText)
            .Where(option => !IsGenericEventChoice(option))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return evt.Name == null && evt.Chosen == null && evt.Result == null && evt.Options.Count == 0
            ? null
            : evt;
    }

    private static List<DebriefItem> FilterItems(List<DebriefItem> items, string idPrefix) =>
        items.Where(item => HasPrefix(item, idPrefix)).Where(IsCleanItem).ToList();

    private static bool HasPrefix(DebriefItem item, string idPrefix) =>
        item.Id != null && item.Id.StartsWith(idPrefix, StringComparison.OrdinalIgnoreCase);

    private static bool IsKnownRestSiteAction(string? action) =>
        action is "Rest" or "Upgrade" or "Smith" or "Dig" or "Recall";

    private static bool IsGenericEventChoice(string? value) =>
        value != null &&
        (value.Equals("Proceed", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("진행", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Continue", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("계속", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Confirm", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("확인", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Back", StringComparison.OrdinalIgnoreCase));

    private static bool IsCleanDecisionText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        return !value.Contains("EventOption title:", StringComparison.Ordinal) &&
               !value.Contains(" textKey:", StringComparison.Ordinal) &&
               !value.EndsWith("EventLayout", StringComparison.Ordinal) &&
               !value.Contains("MegaCrit.", StringComparison.Ordinal) &&
               !value.Contains("System.", StringComparison.Ordinal) &&
               !value.StartsWith("Func`", StringComparison.Ordinal) &&
               !value.StartsWith("Action`", StringComparison.Ordinal);
    }

    private static bool IsCleanItem(DebriefItem item)
    {
        string name = item.Name;
        return !name.Contains("MegaCrit.", StringComparison.Ordinal) &&
               !name.Contains("System.", StringComparison.Ordinal) &&
               !name.StartsWith("Func`", StringComparison.Ordinal) &&
               !name.StartsWith("Action`", StringComparison.Ordinal) &&
               !name.Equals("Action", StringComparison.Ordinal) &&
               !name.EndsWith("Action", StringComparison.Ordinal) &&
               !name.EndsWith("Screen", StringComparison.Ordinal) &&
               !name.Contains("Holder", StringComparison.Ordinal) &&
               !name.EndsWith("Inventory", StringComparison.Ordinal) &&
               !name.EndsWith("Reward", StringComparison.Ordinal) &&
               !name.Equals("PurchaseStatus", StringComparison.Ordinal) &&
               !name.Equals("CardCreationOptions", StringComparison.Ordinal) &&
               !name.Equals("CardCreationResult", StringComparison.Ordinal) &&
               !name.Equals("CardPile", StringComparison.Ordinal);
    }

    private static void Save()
    {
        if (_active == null) return;
        DebriefStorage.SaveJson(_active);
    }
}
