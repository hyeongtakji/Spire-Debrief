using MegaCrit.Sts2.Core.Map;

namespace SpireDebrief.SpireDebriefCode.Services;

public static class PathingText
{
    public static string NormalizeRoomType(MapPointType pointType) =>
        NormalizeRoomType(pointType.ToString());

    public static string NormalizeRoomType(string? roomType)
    {
        if (string.IsNullOrWhiteSpace(roomType))
            return "Unknown";
        if (roomType.Equals("RestSite", StringComparison.OrdinalIgnoreCase))
            return "Rest";
        if (roomType.Equals("Ancient", StringComparison.OrdinalIgnoreCase))
            return "Event";
        if (roomType.Equals("Unassigned", StringComparison.OrdinalIgnoreCase))
            return "Start";
        return roomType;
    }

    public static string FormatPathingChoice(string roomType, string? nodeId) =>
        string.IsNullOrWhiteSpace(nodeId)
            ? $"chose {roomType}"
            : $"chose {roomType} at {nodeId}";
}
