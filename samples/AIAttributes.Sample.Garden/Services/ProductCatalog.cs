using AIAttributes.Sample.Garden.Models;

namespace AIAttributes.Sample.Garden.Services;

/// <summary>
/// Hard-coded product catalog for the garden shop.
/// </summary>
public static class ProductCatalog
{
    public static IReadOnlyList<Product> All { get; } =
    [
        // Seeds
        new("seed-tomato",     "Heirloom Tomato Seeds",   "Seeds",      3.49m, "🍅"),
        new("seed-basil",      "Sweet Basil Seeds",       "Seeds",      2.49m, "🌿"),
        new("seed-pepper",     "Bell Pepper Seeds",       "Seeds",      2.99m, "🌶️"),
        new("seed-sunflower",  "Giant Sunflower Seeds",   "Seeds",      3.99m, "🌻"),
        new("seed-lettuce",    "Mixed Lettuce Seeds",     "Seeds",      2.29m, "🥬"),

        // Soil & amendments
        new("soil-pottingmix", "All-Purpose Potting Mix", "Soil",      11.99m, "🪴"),
        new("soil-compost",    "Organic Compost (10 lb)", "Soil",       8.49m, "🌱"),
        new("soil-mulch",      "Cedar Mulch (2 cu ft)",   "Soil",      14.99m, "🪵"),

        // Fertilizer
        new("fert-tomato",     "Tomato Plant Food",       "Fertilizer", 9.99m,  "💧"),
        new("fert-allpurpose", "All-Purpose Fertilizer",  "Fertilizer", 7.99m,  "💧"),

        // Tools & equipment
        new("tool-trowel",     "Hand Trowel",             "Tools",     12.49m, "🔨"),
        new("tool-pruner",     "Bypass Pruners",          "Tools",     18.99m, "✂️"),
        new("tool-glove",      "Garden Gloves (pair)",    "Tools",      6.99m, "🧤"),
        new("tool-hose",       "50 ft Garden Hose",       "Equipment", 29.99m, "💦"),
        new("tool-watering",   "Watering Can (1 gal)",    "Equipment", 14.99m, "🚿"),
    ];

    public static Product? Find(string sku) =>
        All.FirstOrDefault(p => string.Equals(p.Sku, sku, StringComparison.OrdinalIgnoreCase));

    public static Product? FindByName(string nameOrSku)
    {
        if (string.IsNullOrWhiteSpace(nameOrSku))
            return null;
        var q = nameOrSku.Trim();
        return Find(q)
            ?? All.FirstOrDefault(p => string.Equals(p.Name, q, StringComparison.OrdinalIgnoreCase))
            ?? All.FirstOrDefault(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
    }
}
