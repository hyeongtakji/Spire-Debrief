using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using SpireDebrief.SpireDebriefCode.Models;

namespace SpireDebrief.SpireDebriefCode.Services;

public static class PathTelemetryService
{
    private static bool _installed;
    private static RunState? _runState;
    private static PathingTelemetryRun? _current;
    private static string? _telemetryPath;
    private static MapPoint? _lastMapPoint;
    private static string? _lastChosenNodeId;
    private static int? _lastActIndex;
    private static PathChoiceLog? _lastChoice;

    public static void Install()
    {
        if (_installed)
            return;

        try
        {
            RunManager manager = RunManager.Instance;
            manager.RunStarted += OnRunStarted;
            manager.ActEntered += OnActEntered;
            manager.RoomEntered += OnRoomEntered;
            manager.RoomExited += OnRoomExited;
            _installed = true;
            MainFile.Logger.Info("Spire Debrief path telemetry hooks installed.");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"Path telemetry hooks unavailable: {ex.Message}");
        }
    }

    private static void OnRunStarted(RunState runState) =>
        SafeHook("RunStarted", () =>
        {
            _runState = runState;
            StartTelemetry(runState);
            CaptureGraphSnapshot(runState);
            Persist();
        });

    private static void OnActEntered() =>
        SafeHook("ActEntered", () =>
        {
            RunState? runState = GetRunState();
            if (runState == null)
                return;

            EnsureTelemetry(runState);
            _lastMapPoint = null;
            _lastChosenNodeId = null;
            _lastActIndex = PathGraphExtractor.ToActNumber(runState.CurrentActIndex);
            _lastChoice = null;
            CaptureGraphSnapshot(runState);
            Persist();
        });

    private static void OnRoomEntered() =>
        SafeHook("RoomEntered", () =>
        {
            RunState? runState = GetRunState();
            if (runState == null)
                return;

            EnsureTelemetry(runState);
            CaptureGraphSnapshot(runState);
            RecordCurrentChoice(runState);
            Persist();
        });

    private static void OnRoomExited() =>
        SafeHook("RoomExited", () =>
        {
            RunState? runState = GetRunState();
            if (runState == null || _current == null || _lastChoice == null)
                return;

            _lastChoice.PlayerStateAfter = CapturePlayerState(runState);
            Persist();
        });

    private static void SafeHook(string hookName, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"Path telemetry {hookName} failed: {ex.Message}");
        }
    }

    private static RunState? GetRunState()
    {
        if (_runState != null)
            return _runState;

        try
        {
            _runState = ReflectionDataExtractor.TryReadValue(RunManager.Instance, "State", "<State>k__BackingField") as RunState;
        }
        catch
        {
            _runState = null;
        }

        return _runState;
    }

    private static void StartTelemetry(RunState runState)
    {
        string startedAt = DateTimeOffset.Now.ToString("O");
        string? seed = ReadSeed(runState);
        string? character = ReadCharacter(runState);
        string? gameRunId = ReadGameRunId();

        _current = new PathingTelemetryRun
        {
            Source = "live_telemetry",
            RunId = gameRunId ?? $"telemetry-{seed ?? DateTimeOffset.Now.ToUnixTimeSeconds().ToString()}",
            GameRunId = gameRunId,
            Character = character,
            Seed = seed,
            StartedAt = startedAt,
            UpdatedAt = startedAt
        };
        _telemetryPath = null;
        _lastMapPoint = null;
        _lastChosenNodeId = null;
        _lastActIndex = null;
        _lastChoice = null;
    }

    private static void EnsureTelemetry(RunState runState)
    {
        _runState = runState;
        if (_current == null)
            StartTelemetry(runState);

        if (_current == null)
            return;

        _current.GameRunId ??= ReadGameRunId();
        _current.Seed ??= ReadSeed(runState);
        _current.Character ??= ReadCharacter(runState);
    }

    private static void CaptureGraphSnapshot(RunState runState)
    {
        if (_current == null)
            return;

        ActPathGraphSnapshot? snapshot = PathGraphExtractor.Extract(runState, DateTimeOffset.Now);
        if (snapshot == null)
            return;

        int index = _current.Acts.FindIndex(act => act.ActIndex == snapshot.ActIndex);
        if (index >= 0)
            _current.Acts[index] = snapshot;
        else
            _current.Acts.Add(snapshot);
    }

    private static void RecordCurrentChoice(RunState runState)
    {
        if (_current == null)
            return;

        MapPoint? current = runState.CurrentMapPoint;
        if (current == null)
            return;

        int actIndex = PathGraphExtractor.ToActNumber(runState.CurrentActIndex);
        string chosenNodeId = PathGraphExtractor.NodeId(actIndex, current);
        if (_lastActIndex == actIndex && string.Equals(_lastChosenNodeId, chosenNodeId, StringComparison.Ordinal))
            return;

        MapPoint? previousPoint = _lastActIndex == actIndex ? _lastMapPoint : null;
        MapPoint? startingPoint = runState.Map?.StartingMapPoint;

        int floor = runState.TotalFloor > 0 ? runState.TotalFloor : _current.ActualPath.Count + 1;
        string chosenType = PathingText.NormalizeRoomType(current.PointType);
        if (previousPoint == null && IsSameMapPoint(current, startingPoint, actIndex))
        {
            AddActualPathStep(floor, actIndex, current, chosenNodeId, chosenType);
            UpdateLastPosition(current, chosenNodeId, actIndex, null);
            return;
        }

        MapPoint? fromPoint = previousPoint ?? startingPoint;
        string? fromNodeId = fromPoint == null ? null : PathGraphExtractor.NodeId(actIndex, fromPoint);
        ActPathGraphSnapshot? graph = _current.Acts.FirstOrDefault(act => act.ActIndex == actIndex)
            ?? PathGraphExtractor.Extract(runState, DateTimeOffset.Now);
        List<string> availableNodeIds = GetAvailableNodeIds(fromPoint, actIndex, graph, fromNodeId);
        if (availableNodeIds.Count == 0)
        {
            AddActualPathStep(floor, actIndex, current, chosenNodeId, chosenType);
            UpdateLastPosition(current, chosenNodeId, actIndex, null);
            return;
        }

        if (!availableNodeIds.Contains(chosenNodeId, StringComparer.Ordinal))
        {
            MainFile.Logger.Warn(
                $"Path telemetry skipped inconsistent choice: chosen {chosenNodeId} was not available from {fromNodeId ?? "unknown"}.");
            AddActualPathStep(floor, actIndex, current, chosenNodeId, chosenType);
            UpdateLastPosition(current, chosenNodeId, actIndex, null);
            return;
        }

        PlayerStateSnapshot? playerStateBefore = CapturePlayerState(runState);
        List<PathOptionSummary> optionSummaries = PathChoiceAnalyzer.AnalyzeOptions(graph, availableNodeIds);
        PathChoiceAnalyzer.ApplyRuntimeContext(optionSummaries, playerStateBefore);

        PathChoiceLog choice = new()
        {
            Id = $"{_current.RunId}:floor-{floor}:choice-{_current.Choices.Count + 1}",
            Floor = floor,
            ActIndex = actIndex,
            CapturedAt = DateTimeOffset.Now.ToString("O"),
            FromNodeId = fromNodeId,
            AvailableNodeIds = availableNodeIds,
            ChosenNodeId = chosenNodeId,
            ChosenNodeType = chosenType,
            PlayerStateBefore = playerStateBefore,
            OptionSummaries = optionSummaries,
            Ranks = PathChoiceAnalyzer.CalculateRanks(optionSummaries, chosenNodeId)
        };

        _current.Choices.Add(choice);
        AddActualPathStep(floor, actIndex, current, chosenNodeId, chosenType);
        UpdateLastPosition(current, chosenNodeId, actIndex, choice);
    }

    private static void AddActualPathStep(
        int floor,
        int actIndex,
        MapPoint current,
        string chosenNodeId,
        string chosenType)
    {
        if (_current == null)
            return;

        _current.ActualPath.Add(new ActualPathStepLog
        {
            Floor = floor,
            ActIndex = actIndex,
            NodeId = chosenNodeId,
            Row = current.coord.row,
            Column = current.coord.col,
            Coordinate = PathGraphExtractor.Coordinate(current),
            MapPointType = chosenType,
            RoomType = chosenType,
            PreviousNodeId = _lastActIndex == actIndex ? _lastChosenNodeId : null,
            PathingChoiceSummary = PathingText.FormatPathingChoice(chosenType, chosenNodeId)
        });
    }

    private static void UpdateLastPosition(MapPoint current, string chosenNodeId, int actIndex, PathChoiceLog? choice)
    {
        _lastMapPoint = current;
        _lastChosenNodeId = chosenNodeId;
        _lastActIndex = actIndex;
        _lastChoice = choice;
    }

    private static bool IsSameMapPoint(MapPoint point, MapPoint? other, int actIndex)
    {
        if (other == null)
            return false;

        return PathGraphExtractor.NodeId(actIndex, point)
            .Equals(PathGraphExtractor.NodeId(actIndex, other), StringComparison.Ordinal);
    }

    private static List<string> GetAvailableNodeIds(
        MapPoint? fromPoint,
        int actIndex,
        ActPathGraphSnapshot? graph,
        string? fromNodeId)
    {
        List<string> liveChildren = fromPoint?.Children == null
            ? []
            : fromPoint.Children
                .Where(child => child != null)
                .Select(child => PathGraphExtractor.NodeId(actIndex, child))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();
        if (liveChildren.Count > 0)
            return liveChildren;

        if (string.IsNullOrWhiteSpace(fromNodeId) || graph == null)
            return [];

        return graph.Edges
            .Where(edge => edge.FromNodeId.Equals(fromNodeId, StringComparison.Ordinal))
            .Select(edge => edge.ToNodeId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static PlayerStateSnapshot? CapturePlayerState(RunState runState)
    {
        try
        {
            Player? player = runState.Players.FirstOrDefault(player => player.IsActiveForHooks)
                ?? runState.Players.FirstOrDefault();
            if (player == null)
                return null;

            return new PlayerStateSnapshot
            {
                CurrentHp = player.Creature?.CurrentHp,
                MaxHp = player.Creature?.MaxHp,
                Gold = player.Gold,
                DeckSize = player.Deck?.Cards.Count,
                RelicCount = player.Relics?.Count,
                Relics = player.Relics?.Select(ToRelicItem).WhereNotNull().ToList() ?? [],
                UnknownRoomOdds = CaptureUnknownRoomOdds(runState)
            };
        }
        catch
        {
            return null;
        }
    }

    private static UnknownRoomOddsSnapshot? CaptureUnknownRoomOdds(RunState runState)
    {
        try
        {
            return new UnknownRoomOddsSnapshot
            {
                MonsterOdds = runState.Odds?.UnknownMapPoint?.MonsterOdds,
                EliteOdds = runState.Odds?.UnknownMapPoint?.EliteOdds,
                EventOdds = runState.Odds?.UnknownMapPoint?.EventOdds,
                ShopOdds = runState.Odds?.UnknownMapPoint?.ShopOdds,
                TreasureOdds = runState.Odds?.UnknownMapPoint?.TreasureOdds
            };
        }
        catch
        {
            return null;
        }
    }

    private static DebriefItem? ToRelicItem(RelicModel relic)
    {
        if (relic.Id == null)
            return null;

        return new DebriefItem
        {
            Id = relic.Id.ToString(),
            Name = Text(relic.Title) ?? relic.Id.ToString()
        };
    }

    private static string? Text(LocString? locString) =>
        locString?.GetFormattedText() ?? locString?.GetRawText();

    private static void Persist()
    {
        if (_current == null)
            return;

        _current.GameRunId ??= ReadGameRunId();
        _telemetryPath = PathTelemetryStorage.Save(_current, _telemetryPath);
    }

    private static string? ReadSeed(RunState runState)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(runState.Rng?.StringSeed)
                ? runState.Rng.StringSeed
                : runState.Rng?.Seed.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadCharacter(RunState runState)
    {
        try
        {
            Player? player = runState.Players.FirstOrDefault(player => player.IsActiveForHooks)
                ?? runState.Players.FirstOrDefault();
            return player?.Character?.Id.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadGameRunId()
    {
        try
        {
            long startTime = RunManager.Instance.History?.StartTime ?? 0;
            return startTime > 0 ? $"history-{startTime}" : null;
        }
        catch
        {
            return null;
        }
    }
}
