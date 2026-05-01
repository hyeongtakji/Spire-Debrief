using System.Collections;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using SpireDebrief.SpireDebriefCode.Models;

namespace SpireDebrief.SpireDebriefCode.Services;

public static class CardInstanceMetadataExtractor
{
    private static readonly string[] StrategicSavedPropertyFragments =
    [
        "affix",
        "enchant",
        "innate",
        "modifier",
        "preserve",
        "retain",
        "stable",
        "status"
    ];

    public static List<CardInstanceMetadata> Extract(SerializableCard card)
    {
        List<CardInstanceMetadata> metadata = [];
        AddEnchantment(metadata, card.Enchantment, null);
        AddSavedProperties(metadata, card.Props);
        return Dedupe(metadata);
    }

    public static CardInstanceMetadata? ExtractEnchantment(
        SerializableEnchantment? serializableEnchantment,
        ModelId? fallbackId)
    {
        List<CardInstanceMetadata> metadata = [];
        AddEnchantment(metadata, serializableEnchantment, fallbackId);
        return metadata.FirstOrDefault();
    }

    public static string FormatForChange(IReadOnlyList<CardInstanceMetadata> metadata) =>
        metadata.Count == 0
            ? "instance metadata changed"
            : string.Join(" / ", metadata.Select(item => item.DisplayText));

    private static void AddEnchantment(
        List<CardInstanceMetadata> metadata,
        SerializableEnchantment? serializableEnchantment,
        ModelId? fallbackId)
    {
        ModelId? id = fallbackId;
        int? amount = null;
        string? raw = null;

        if (serializableEnchantment != null && serializableEnchantment.Id != ModelId.none)
        {
            id = serializableEnchantment.Id;
            amount = serializableEnchantment.Amount > 0 ? serializableEnchantment.Amount : null;
            raw = serializableEnchantment.ToString();
        }

        if (id == null || id == ModelId.none)
            return;

        (string? name, bool unlocalized) = ResolveEnchantmentName(id);
        metadata.Add(new CardInstanceMetadata
        {
            Kind = "enchantment",
            Id = id.ToString(),
            Name = name,
            Amount = amount,
            RawValue = raw ?? id.ToString(),
            IsUnlocalized = unlocalized
        });
    }

    private static (string? Name, bool Unlocalized) ResolveEnchantmentName(ModelId id)
    {
        try
        {
            EnchantmentModel model = SaveUtil.EnchantmentOrDeprecated(id);
            string? text = Text(model.Title);
            if (!string.IsNullOrWhiteSpace(text) && !text.Equals(id.ToString(), StringComparison.Ordinal))
                return (text, false);
        }
        catch
        {
            // Early Access schemas may leave a saved enchantment id without a loadable model.
        }

        return (null, true);
    }

    private static void AddSavedProperties(List<CardInstanceMetadata> metadata, SavedProperties? props)
    {
        if (props == null)
            return;

        foreach ((string Name, object? Value) prop in EnumerateSavedProperties(props))
        {
            if (!IsStrategicSavedProperty(prop.Name) || IsDefaultValue(prop.Value))
                continue;

            metadata.Add(new CardInstanceMetadata
            {
                Kind = "modifier",
                RawValue = $"{prop.Name}={FormatRawValue(prop.Value)}",
                IsUnlocalized = true
            });
        }
    }

    private static IEnumerable<(string Name, object? Value)> EnumerateSavedProperties(SavedProperties props)
    {
        foreach (string listName in new[] { "bools", "ints", "modelIds", "strings" })
        {
            if (ReflectionDataExtractor.TryReadValue(props, listName) is not IEnumerable entries)
                continue;

            foreach (object? entry in entries)
            {
                string? name = ReflectionDataExtractor.TryReadValue(entry, "name") as string;
                object? value = ReflectionDataExtractor.TryReadValue(entry, "value");
                if (!string.IsNullOrWhiteSpace(name))
                    yield return (name, value);
            }
        }
    }

    private static bool IsStrategicSavedProperty(string name) =>
        StrategicSavedPropertyFragments.Any(fragment =>
            name.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static bool IsDefaultValue(object? value) =>
        value switch
        {
            null => true,
            bool boolValue => !boolValue,
            int intValue => intValue == 0,
            string stringValue => string.IsNullOrWhiteSpace(stringValue),
            ModelId modelId => modelId == ModelId.none,
            _ => false
        };

    private static string FormatRawValue(object? value) =>
        value switch
        {
            null => "null",
            IEnumerable enumerable when value is not string => string.Join(",", enumerable.Cast<object?>().Select(FormatRawValue)),
            _ => value.ToString() ?? string.Empty
        };

    private static List<CardInstanceMetadata> Dedupe(IEnumerable<CardInstanceMetadata> metadata) =>
        metadata
            .GroupBy(item => $"{item.Kind}:{item.Id}:{item.RawValue}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

    private static string? Text(LocString? locString) =>
        locString?.GetFormattedText() ?? locString?.GetRawText();
}
