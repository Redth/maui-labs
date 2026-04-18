using System.ComponentModel;
using AIAttributes.Sample.Garden.Models;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden.Services;

/// <summary>
/// Hard-coded product catalog for the garden shop.
/// </summary>
public static class ProductCatalog
{
    [ExportAIFunction("list_all_products")]
    [Description("Returns every product in the garden shop catalog.")]
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

    [ExportAIFunction("search_products")]
    [Description("Searches the garden shop catalog by name, category, or sku. Returns every product when no query is given.")]
    public static List<Product> SearchProducts(
        [Description("Optional text to filter by product name, sku, or category. Leave blank to list everything.")]
        string? query = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [.. All];

        var q = query.Trim();
        return [.. All.Where(p =>
            p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            p.Sku.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            p.Category.Contains(q, StringComparison.OrdinalIgnoreCase))];
    }

    [ExportAIFunction("get_product")]
    [Description("Looks up a single product by sku or exact name.")]
    public static Product? FindByName(
        [Description("The product sku or exact name (e.g., 'seed-tomato' or 'Heirloom Tomato Seeds').")]
        string nameOrSku)
    {
        if (string.IsNullOrWhiteSpace(nameOrSku))
            return null;
        var q = nameOrSku.Trim();
        return All.FirstOrDefault(p => string.Equals(p.Sku, q, StringComparison.OrdinalIgnoreCase))
            ?? All.FirstOrDefault(p => string.Equals(p.Name, q, StringComparison.OrdinalIgnoreCase))
            ?? All.FirstOrDefault(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
    }
}
