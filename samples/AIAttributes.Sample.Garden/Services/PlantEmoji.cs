namespace AIAttributes.Sample.Garden.Services;

/// <summary>
/// Maps common plant species names to emoji.
/// Falls back to a generic seedling when no match is found.
/// </summary>
public static class PlantEmoji
{
    private static readonly Dictionary<string, string> Map =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["tomato"] = "🍅",
        ["basil"] = "🌿",
        ["lavender"] = "💜",
        ["fern"] = "🌿",
        ["mint"] = "🌱",
        ["sunflower"] = "🌻",
        ["aloe vera"] = "🪴",
        ["aloe"] = "🪴",
        ["snake plant"] = "🪴",
        ["rosemary"] = "🌿",
        ["pepper"] = "🌶️",
        ["rose"] = "🌹",
        ["tulip"] = "🌷",
        ["cactus"] = "🌵",
        ["orchid"] = "🌸",
        ["daisy"] = "🌼",
    };

    public static string ForSpecies(string? species)
    {
        if (string.IsNullOrWhiteSpace(species))
            return "🌱";
        return Map.TryGetValue(species.Trim(), out var emoji) ? emoji : "🌱";
    }
}
