using SpireDebrief.SpireDebriefCode.Models;

namespace SpireDebrief.SpireDebriefCode.Services;

public static class DebriefRecorder
{
    private static readonly object Sync = new();
    private static string _modVersion = "unknown";
    private static RunDebriefLog? _active;
    private static CardRewardDecision? _pendingCardReward;
    private static int _lastFloor = 1;

    public static RunDebriefLog ActiveOrLatest
    {
        get
        {
            lock (Sync)
            {
                _active ??= DebriefStorage.LoadLatestJson() ?? CreateRun();
                return _active;
            }
        }
    }

    public static void Initialize(string modVersion)
    {
        lock (Sync)
        {
            _modVersion = modVersion;
            _active = DebriefStorage.LoadLatestJson();
            if (_active != null)
                _active.Metadata.ModVersion ??= modVersion;
        }
    }

    public static void BeginRun(object? runSource = null)
    {
        lock (Sync)
        {
            _active = CreateRun();
            ReflectionDataExtractor.FillMetadata(_active, runSource);
            Save();
        }
    }

    public static void EnterRoom(object? roomSource = null, string? fallbackRoomType = null)
    {
        lock (Sync)
        {
            RunDebriefLog log = EnsureRun();
            int floor = ReflectionDataExtractor.TryReadInt(roomSource, "Floor", "FloorNumber", "Room.Floor") ?? _lastFloor;
            _lastFloor = Math.Max(_lastFloor, floor);
            string roomType = ReflectionDataExtractor.TryReadString(roomSource, "RoomType", "Type", "MapPointType") ?? fallbackRoomType ?? "Unknown";
            FloorLog floorLog = EnsureFloor(log, floor, roomType);

            floorLog.RoomType = NormalizeRoomType(roomType);
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
            List<DebriefItem> choices = ReflectionDataExtractor.ExtractItems(
                source,
                "Cards", "CardRewards", "RewardCards", "Choices", "Options", "_cards", "_rewardCards");
            if (choices.Count == 0) return;

            if (_pendingCardReward != null && _pendingCardReward.Picked == null && !_pendingCardReward.Skipped)
                return;

            _pendingCardReward = new CardRewardDecision
            {
                Choices = choices,
                Source = source?.GetType().Name
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
            if (!floor.CardRewards.Contains(reward))
                floor.CardRewards.Add(reward);

            reward.Picked = ResolvePickedCard(reward, cardSource);
            reward.Skipped = false;
            log.Summary.CardsPicked++;
            _pendingCardReward = null;
            Save();
        }
    }

    public static void RecordCardRewardSkipped()
    {
        lock (Sync)
        {
            RunDebriefLog log = EnsureRun();
            FloorLog floor = EnsureFloor(log, _lastFloor, "Unknown");
            CardRewardDecision reward = _pendingCardReward ?? new CardRewardDecision();
            if (!floor.CardRewards.Contains(reward))
                floor.CardRewards.Add(reward);

            reward.Skipped = true;
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
            floor.Event.Name ??= ReflectionDataExtractor.TryReadString(source, "Event.Name", "Event.Id", "Name", "Id");
            List<string> options = ReflectionDataExtractor.ExtractStrings(source, "Options", "EventOptions", "CurrentOptions", "_options");
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
            floor.Event.Name ??= ReflectionDataExtractor.TryReadString(eventSource, "Event.Name", "Event.Id", "Name", "Id");
            floor.Event.Chosen = ReflectionDataExtractor.TryReadString(choiceSource, "Text", "Label", "Name", "Title", "Id")
                ?? choiceSource?.ToString();
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
            floor.Shop.Purchased.Add(ReflectionDataExtractor.ToItem(purchaseSource));
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
            floor.RelicRewards.Add(ReflectionDataExtractor.ToItem(relicSource));
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
            floor.PotionRewards.Add(ReflectionDataExtractor.ToItem(potionSource));
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
            floor.Shop.Removed.Add(ReflectionDataExtractor.ToItem(cardSource));
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
                Target = targetSource == null ? null : ReflectionDataExtractor.ToItem(targetSource)
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
            log.Metadata.FinalFloor ??= _lastFloor;
            Save();
        }
    }

    public static RunDebriefLog CreateExportLog(object? runHistorySource)
    {
        lock (Sync)
        {
            RunDebriefLog log = DebriefStorage.LoadBestMatchingJson(runHistorySource) ?? ActiveOrLatest;
            ReflectionDataExtractor.FillMetadata(log, runHistorySource);
            ReflectionDataExtractor.FillFinalState(log.FinalState, runHistorySource);
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

    private static RunDebriefLog EnsureRun() => _active ??= CreateRun();

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

    private static int CountRooms(RunDebriefLog log, string roomType) =>
        log.Floors.Count(f => f.RoomType.Equals(roomType, StringComparison.OrdinalIgnoreCase));

    private static DebriefItem ResolvePickedCard(CardRewardDecision reward, object? cardSource)
    {
        if (TryConvertIndex(cardSource, out int index) && index >= 0 && index < reward.Choices.Count)
            return reward.Choices[index];
        return ReflectionDataExtractor.ToItem(cardSource);
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

    private static void Save()
    {
        if (_active == null) return;
        DebriefStorage.SaveJson(_active);
    }
}
