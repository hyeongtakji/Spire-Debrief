using SpireDebrief.SpireDebriefCode.Models;

namespace SpireDebrief.SpireDebriefCode.Services;

public static class PathChoiceAnalyzer
{
    public static List<PathOptionSummary> AnalyzeOptions(
        ActPathGraphSnapshot? graph,
        IEnumerable<string> optionNodeIds)
    {
        if (graph == null)
            return [];

        Graph model = new(graph);
        List<PathOptionSummary> summaries = [];
        foreach (string optionNodeId in optionNodeIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
        {
            if (!model.Nodes.ContainsKey(optionNodeId))
                continue;

            summaries.Add(AnalyzeOption(model, optionNodeId));
        }

        return summaries;
    }

    public static PathChoiceRanks? CalculateRanks(IReadOnlyList<PathOptionSummary> summaries, string? chosenNodeId)
    {
        if (summaries.Count == 0 || string.IsNullOrWhiteSpace(chosenNodeId))
            return null;

        PathOptionSummary? chosen = summaries.FirstOrDefault(summary => summary.NodeId.Equals(chosenNodeId, StringComparison.Ordinal));
        if (chosen == null)
            return null;

        return new PathChoiceRanks
        {
            ChosenRankByMostElites = RankDescending(summaries, chosen, summary => summary.MaxElitesReachable),
            ChosenRankByFewestElites = RankAscending(summaries, chosen, summary => summary.MinElitesReachable),
            ChosenRankByMostRestSites = RankDescending(summaries, chosen, summary => summary.MaxRestSitesReachable),
            ChosenRankByShortestRestDistance = RankAscendingNullable(summaries, chosen, summary => summary.NearestRestDistance),
            ChosenRankByPathCount = RankDescending(summaries, chosen, summary => summary.ReachablePathCount)
        };
    }

    private static PathOptionSummary AnalyzeOption(Graph graph, string nodeId)
    {
        PathRange range = CountRanges(graph, nodeId, [], []);
        PathOptionSummary summary = new()
        {
            NodeId = nodeId,
            NodeType = graph.Nodes[nodeId].MapPointType,
            ReachablePathCount = CountPaths(graph, nodeId, [], []),
            MinElitesReachable = range.Min.Elites,
            MaxElitesReachable = range.Max.Elites,
            MinMonstersReachable = range.Min.Monsters,
            MaxMonstersReachable = range.Max.Monsters,
            MinRestSitesReachable = range.Min.RestSites,
            MaxRestSitesReachable = range.Max.RestSites,
            MinShopsReachable = range.Min.Shops,
            MaxShopsReachable = range.Max.Shops,
            MinTreasuresReachable = range.Min.Treasures,
            MaxTreasuresReachable = range.Max.Treasures,
            MinUnknownsReachable = range.Min.Unknowns,
            MaxUnknownsReachable = range.Max.Unknowns,
            MinEventsReachable = range.Min.Events,
            MaxEventsReachable = range.Max.Events,
            EliteForced = range.Min.Elites > 0,
            ImmediateRest = IsRest(graph.Nodes[nodeId].MapPointType) ? true : null,
            ForcedFollowUp = ForcedFollowUp(graph, nodeId),
            RestSiteReachable = range.Max.RestSites > 0,
            NearestRestDistance = NearestDistance(graph, nodeId, "Rest"),
            NearestShopDistance = NearestDistance(graph, nodeId, "Shop"),
            NearestEliteDistance = NearestDistance(graph, nodeId, "Elite"),
            PathFlexibilityScore = CountReachableNodes(graph, nodeId, [])
        };
        summary.RiskNote = BuildRiskNote(summary, null);
        return summary;
    }

    public static void ApplyRuntimeContext(
        IReadOnlyList<PathOptionSummary> summaries,
        PlayerStateSnapshot? playerState)
    {
        foreach (PathOptionSummary summary in summaries)
        {
            if (IsUnknown(summary.NodeType) && playerState?.UnknownRoomOdds != null)
            {
                bool combatPossible = (playerState.UnknownRoomOdds.MonsterOdds ?? 0f) > 0f;
                summary.UnknownCombatPossible = combatPossible;
                summary.UnknownCombatReason = combatPossible
                    ? $"Live unknown-room odds before this choice reported MonsterOdds={FormatOdds(playerState.UnknownRoomOdds.MonsterOdds)}."
                    : $"Live unknown-room odds before this choice reported MonsterOdds={FormatOdds(playerState.UnknownRoomOdds.MonsterOdds)}, so normal combat was not in the current unknown-room roll table.";
            }

            summary.RiskNote = BuildRiskNote(summary, playerState);
        }
    }

    private static int CountPaths(
        Graph graph,
        string nodeId,
        Dictionary<string, int> memo,
        HashSet<string> visiting)
    {
        if (memo.TryGetValue(nodeId, out int cached))
            return cached;
        if (!visiting.Add(nodeId))
            return 0;

        IReadOnlyList<string> children = graph.Children(nodeId);
        int result;
        if (children.Count == 0 || IsBoss(graph, nodeId))
        {
            result = 1;
        }
        else
        {
            long total = 0;
            foreach (string child in children)
                total += CountPaths(graph, child, memo, visiting);
            result = total > int.MaxValue ? int.MaxValue : (int)total;
        }

        visiting.Remove(nodeId);
        memo[nodeId] = result;
        return result;
    }

    private static PathRange CountRanges(
        Graph graph,
        string nodeId,
        Dictionary<string, PathRange> memo,
        HashSet<string> visiting)
    {
        if (memo.TryGetValue(nodeId, out PathRange cached))
            return cached;
        if (!visiting.Add(nodeId))
            return PathRange.Zero;

        PathCounts current = PathCounts.ForNode(graph.Nodes[nodeId]);
        IReadOnlyList<string> children = graph.Children(nodeId);
        PathRange result;
        if (children.Count == 0 || IsBoss(graph, nodeId))
        {
            result = new PathRange(current, current);
        }
        else
        {
            PathCounts? min = null;
            PathCounts? max = null;
            foreach (string child in children)
            {
                PathRange childRange = CountRanges(graph, child, memo, visiting);
                PathCounts childMin = current + childRange.Min;
                PathCounts childMax = current + childRange.Max;
                min = min == null ? childMin : PathCounts.Min(min.Value, childMin);
                max = max == null ? childMax : PathCounts.Max(max.Value, childMax);
            }

            result = new PathRange(min ?? current, max ?? current);
        }

        visiting.Remove(nodeId);
        memo[nodeId] = result;
        return result;
    }

    private static int? NearestDistance(Graph graph, string startNodeId, string nodeType)
    {
        Queue<(string NodeId, int Distance)> queue = [];
        HashSet<string> visited = [];
        queue.Enqueue((startNodeId, 0));
        visited.Add(startNodeId);

        while (queue.Count > 0)
        {
            (string nodeId, int distance) = queue.Dequeue();
            if (graph.Nodes[nodeId].MapPointType.Equals(nodeType, StringComparison.OrdinalIgnoreCase))
                return distance;

            foreach (string child in graph.Children(nodeId))
            {
                if (visited.Add(child))
                    queue.Enqueue((child, distance + 1));
            }
        }

        return null;
    }

    private static List<string> ForcedFollowUp(Graph graph, string nodeId)
    {
        List<string> followUp = [];
        string current = nodeId;
        HashSet<string> visited = [nodeId];

        while (true)
        {
            IReadOnlyList<string> children = graph.Children(current);
            if (children.Count != 1)
                break;

            string child = children[0];
            if (!visited.Add(child))
                break;

            followUp.Add(FormatNodeStep(graph.Nodes[child]));
            current = child;
        }

        return followUp;
    }

    private static int CountReachableNodes(Graph graph, string nodeId, HashSet<string> visited)
    {
        if (!visited.Add(nodeId))
            return 0;

        int count = 1;
        foreach (string child in graph.Children(nodeId))
            count += CountReachableNodes(graph, child, visited);
        return count;
    }

    private static bool IsBoss(Graph graph, string nodeId) =>
        graph.Nodes[nodeId].MapPointType.Equals("Boss", StringComparison.OrdinalIgnoreCase);

    private static string BuildRiskNote(PathOptionSummary option, PlayerStateSnapshot? playerState)
    {
        if (option.ImmediateRest == true && option.EliteForced && option.NearestEliteDistance == 1)
            return "Rest now, but this line commits to an elite shortly after.";

        if (option.UnknownCombatPossible == false)
            return "Unknown is safer than normal because live telemetry reports normal combat was not possible in ? rooms before this choice.";

        if (option.EliteForced && option.NearestRestDistance is > 1 or null)
            return "This line has a forced elite before a nearby rest site is guaranteed.";

        if (IsLowHp(playerState) && option.NearestRestDistance is > 1)
            return $"Low HP and nearest rest is {option.NearestRestDistance} nodes away on this line.";

        if (option.ForcedFollowUp.Count > 0 && option.EliteForced)
            return "This option has forced follow-up before the next branch; judge the whole sequence, not only the immediate node.";

        return string.Empty;
    }

    private static bool IsLowHp(PlayerStateSnapshot? playerState) =>
        playerState?.CurrentHp != null &&
        playerState.MaxHp is > 0 &&
        playerState.CurrentHp.Value <= Math.Max(15, (int)Math.Ceiling(playerState.MaxHp.Value * 0.3));

    private static string FormatNodeStep(PathNodeLog node) =>
        string.IsNullOrWhiteSpace(node.MapPointType)
            ? node.Id
            : $"{node.Id} {node.MapPointType}";

    private static string FormatOdds(float? odds) =>
        odds.HasValue ? odds.Value.ToString("0.###") : "unknown";

    private static bool IsRest(string? type) =>
        Is(type, "Rest") || Is(type, "RestSite");

    private static bool IsUnknown(string? type) =>
        Is(type, "Unknown");

    private static int RankDescending(
        IReadOnlyList<PathOptionSummary> summaries,
        PathOptionSummary chosen,
        Func<PathOptionSummary, int> selector)
    {
        int chosenValue = selector(chosen);
        return 1 + summaries.Count(summary => selector(summary) > chosenValue);
    }

    private static int RankAscending(
        IReadOnlyList<PathOptionSummary> summaries,
        PathOptionSummary chosen,
        Func<PathOptionSummary, int> selector)
    {
        int chosenValue = selector(chosen);
        return 1 + summaries.Count(summary => selector(summary) < chosenValue);
    }

    private static int? RankAscendingNullable(
        IReadOnlyList<PathOptionSummary> summaries,
        PathOptionSummary chosen,
        Func<PathOptionSummary, int?> selector)
    {
        if (summaries.All(summary => selector(summary) == null))
            return null;

        int chosenValue = selector(chosen) ?? int.MaxValue;
        return 1 + summaries.Count(summary => (selector(summary) ?? int.MaxValue) < chosenValue);
    }

    private sealed class Graph
    {
        private readonly Dictionary<string, List<string>> _children;

        public Graph(ActPathGraphSnapshot graph)
        {
            Nodes = graph.Nodes
                .Where(node => !string.IsNullOrWhiteSpace(node.Id))
                .GroupBy(node => node.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            _children = graph.Edges
                .Where(edge => Nodes.ContainsKey(edge.FromNodeId) && Nodes.ContainsKey(edge.ToNodeId))
                .GroupBy(edge => edge.FromNodeId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(edge => edge.ToNodeId).Distinct(StringComparer.Ordinal).ToList(),
                    StringComparer.Ordinal);
        }

        public Dictionary<string, PathNodeLog> Nodes { get; }

        public IReadOnlyList<string> Children(string nodeId) =>
            _children.TryGetValue(nodeId, out List<string>? children) ? children : [];
    }

    private readonly record struct PathRange(PathCounts Min, PathCounts Max)
    {
        public static PathRange Zero { get; } = new(PathCounts.Zero, PathCounts.Zero);
    }

    private readonly record struct PathCounts(
        int Elites,
        int Monsters,
        int RestSites,
        int Shops,
        int Treasures,
        int Unknowns,
        int Events)
    {
        public static PathCounts Zero { get; } = new(0, 0, 0, 0, 0, 0, 0);

        public static PathCounts ForNode(PathNodeLog node)
        {
            string type = node.MapPointType;
            return new PathCounts(
                Is(type, "Elite") ? 1 : 0,
                Is(type, "Monster") ? 1 : 0,
                Is(type, "Rest") || Is(type, "RestSite") ? 1 : 0,
                Is(type, "Shop") ? 1 : 0,
                Is(type, "Treasure") ? 1 : 0,
                Is(type, "Unknown") ? 1 : 0,
                Is(type, "Event") || Is(type, "Ancient") ? 1 : 0);
        }

        public static PathCounts operator +(PathCounts left, PathCounts right) =>
            new(
                left.Elites + right.Elites,
                left.Monsters + right.Monsters,
                left.RestSites + right.RestSites,
                left.Shops + right.Shops,
                left.Treasures + right.Treasures,
                left.Unknowns + right.Unknowns,
                left.Events + right.Events);

        public static PathCounts Min(PathCounts left, PathCounts right) =>
            new(
                Math.Min(left.Elites, right.Elites),
                Math.Min(left.Monsters, right.Monsters),
                Math.Min(left.RestSites, right.RestSites),
                Math.Min(left.Shops, right.Shops),
                Math.Min(left.Treasures, right.Treasures),
                Math.Min(left.Unknowns, right.Unknowns),
                Math.Min(left.Events, right.Events));

        public static PathCounts Max(PathCounts left, PathCounts right) =>
            new(
                Math.Max(left.Elites, right.Elites),
                Math.Max(left.Monsters, right.Monsters),
                Math.Max(left.RestSites, right.RestSites),
                Math.Max(left.Shops, right.Shops),
                Math.Max(left.Treasures, right.Treasures),
                Math.Max(left.Unknowns, right.Unknowns),
                Math.Max(left.Events, right.Events));

        private static bool Is(string value, string expected) =>
            value.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Is(string? value, string expected) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Equals(expected, StringComparison.OrdinalIgnoreCase);
}
