using System.ComponentModel;
using AIAttributes.Sample.Garden.Models;
using AIAttributes.Sample.Garden.Services;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden.Tools;

/// <summary>
/// Catalog browse tools. The catalog is static data so these are pure static
/// methods with no <c>[FromServices]</c> parameters.
/// </summary>
public static class CatalogTools
{
    [Description("Searches the garden shop catalog. Returns every product when no query is given, or filters by name, category, or sku.")]
    [ExportAIFunction("search_products")]
    public static List<Product> SearchProducts(
        [Description("Optional text to filter by product name, sku, or category. Leave blank to list everything.")]
        string? query = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [.. ProductCatalog.All];

        var q = query.Trim();
        return [.. ProductCatalog.All.Where(p =>
            p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            p.Sku.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            p.Category.Contains(q, StringComparison.OrdinalIgnoreCase))];
    }

    [Description("Looks up a single product by sku or exact name.")]
    [ExportAIFunction("get_product")]
    public static Product? GetProduct(
        [Description("The product sku or exact name (e.g., 'seed-tomato' or 'Heirloom Tomato Seeds').")]
        string skuOrName)
        => ProductCatalog.FindByName(skuOrName);
}
