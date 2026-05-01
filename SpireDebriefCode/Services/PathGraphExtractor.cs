using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;
using SpireDebrief.SpireDebriefCode.Models;

namespace SpireDebrief.SpireDebriefCode.Services;

public static class PathGraphExtractor
{
    public static ActPathGraphSnapshot? Extract(RunState? runState, DateTimeOffset capturedAt)
    {
        if (runState?.Map == null)
            return null;

        return Extract(runState.Map, ToActNumber(runState.CurrentActIndex), capturedAt);
    }

    public static ActPathGraphSnapshot? Extract(ActMap? map, int actIndex, DateTimeOffset capturedAt)
    {
        if (map == null)
            return null;

        ActPathGraphSnapshot snapshot = new()
        {
            ActIndex = actIndex,
            CapturedAt = capturedAt.ToString("O")
        };

        HashSet<MapPoint> visited = [];
        HashSet<string> edgeKeys = [];

        foreach (MapPoint root in GetRoots(map))
            Traverse(root, actIndex, snapshot, visited, edgeKeys);

        snapshot.Nodes.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
        snapshot.Edges.Sort((left, right) =>
        {
            int from = string.CompareOrdinal(left.FromNodeId, right.FromNodeId);
            return from != 0 ? from : string.CompareOrdinal(left.ToNodeId, right.ToNodeId);
        });

        return snapshot.Nodes.Count == 0 ? null : snapshot;
    }

    public static int ToActNumber(int currentActIndex) =>
        currentActIndex + 1;

    public static string NodeId(int actIndex, MapPoint point) =>
        $"A{actIndex}:{point.coord.row},{point.coord.col}";

    public static string Coordinate(MapPoint point) =>
        $"{point.coord.row},{point.coord.col}";

    private static IEnumerable<MapPoint> GetRoots(ActMap map)
    {
        if (map.StartingMapPoint != null)
            yield return map.StartingMapPoint;

        if (map.startMapPoints != null)
        {
            foreach (MapPoint start in map.startMapPoints)
            {
                if (start != null)
                    yield return start;
            }
        }

        if (map.BossMapPoint != null)
            yield return map.BossMapPoint;

        if (map.SecondBossMapPoint != null)
            yield return map.SecondBossMapPoint;
    }

    private static void Traverse(
        MapPoint point,
        int actIndex,
        ActPathGraphSnapshot snapshot,
        HashSet<MapPoint> visited,
        HashSet<string> edgeKeys)
    {
        if (!visited.Add(point))
            return;

        snapshot.Nodes.Add(ToNode(point, actIndex));

        if (point.Children == null)
            return;

        string from = NodeId(actIndex, point);
        foreach (MapPoint child in point.Children)
        {
            if (child == null)
                continue;

            string to = NodeId(actIndex, child);
            string edgeKey = $"{from}->{to}";
            if (edgeKeys.Add(edgeKey))
            {
                snapshot.Edges.Add(new PathEdgeLog
                {
                    FromNodeId = from,
                    ToNodeId = to
                });
            }

            Traverse(child, actIndex, snapshot, visited, edgeKeys);
        }
    }

    private static PathNodeLog ToNode(MapPoint point, int actIndex)
    {
        string type = PathingText.NormalizeRoomType(point.PointType);
        return new PathNodeLog
        {
            Id = NodeId(actIndex, point),
            ActIndex = actIndex,
            Row = point.coord.row,
            Column = point.coord.col,
            Coordinate = Coordinate(point),
            MapPointType = type,
            RoomType = type
        };
    }
}
