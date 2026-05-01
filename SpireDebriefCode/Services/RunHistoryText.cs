namespace SpireDebrief.SpireDebriefCode.Services;

public static class RunHistoryText
{
    public static string? FormatFinalAct(
        int historyActCount,
        IEnumerable<int> visitedActFloorCounts)
    {
        int lastVisitedAct = 0;
        int actIndex = 1;
        foreach (int floorCount in visitedActFloorCounts)
        {
            if (floorCount > 0)
                lastVisitedAct = actIndex;
            actIndex++;
        }

        if (lastVisitedAct > 0)
            return $"Act {lastVisitedAct}";
        return historyActCount > 0 ? $"Act {historyActCount}" : null;
    }
}
