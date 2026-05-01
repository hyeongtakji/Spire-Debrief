using SpireDebrief.SpireDebriefCode.Services;
using Xunit;

namespace SpireDebrief.Tests.Exporter;

public sealed class RunHistoryTextTests
{
    [Fact]
    public void FormatFinalAct_uses_last_visited_act()
    {
        string? finalAct = RunHistoryText.FormatFinalAct(
            3,
            new[] { 17, 6 });

        Assert.Equal("Act 2", finalAct);
    }

    [Fact]
    public void FormatFinalAct_falls_back_to_history_act_count()
    {
        string? finalAct = RunHistoryText.FormatFinalAct(
            3,
            Array.Empty<int>());

        Assert.Equal("Act 3", finalAct);
    }
}
