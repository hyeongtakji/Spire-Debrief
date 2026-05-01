using Xunit;

namespace SpireDebrief.Tests.Prompt;

public sealed class ReviewPromptTests
{
    [Fact]
    public void Prompt_treats_card_modifiers_as_deck_defining()
    {
        string prompt = ReadReviewPrompt();

        Assert.Contains(
            "Treat card instance modifiers, enchantments, affixes, " +
            "and special statuses as first-class deck-defining information.",
            prompt);
    }

    [Fact]
    public void Prompt_does_not_treat_impossible_unknowns_as_combat()
    {
        string prompt = ReadReviewPrompt();

        Assert.Contains(
            "Do not treat an Unknown room as possible normal combat if " +
            "a current relic, unknown-room odds field, or exported pathing " +
            "note says combat was prevented or impossible.",
            prompt);
    }

    [Fact]
    public void Prompt_labels_missing_relevant_export_data_uncertain()
    {
        string prompt = ReadReviewPrompt();

        Assert.Contains(
            "If the export states that a strategically relevant data category " +
            "is unavailable from RunHistory, label conclusions depending on " +
            "that data as Uncertain rather than Mistake.",
            prompt);
    }

    private static string ReadReviewPrompt()
    {
        string promptPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../SpireDebriefCode/Resources/ReviewPrompt.md"));

        return File.ReadAllText(promptPath);
    }
}
