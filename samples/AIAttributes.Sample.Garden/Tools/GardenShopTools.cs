using System.ComponentModel;
using AIAttributes.Sample.Garden.Models;
using AIAttributes.Sample.Garden.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden.Tools;

/// <summary>
/// Catalog browse tools — no DI of any kind. The catalog is pure static data
/// so every method here is a <c>static</c> tool and the generator emits a
/// fully DI-free <see cref="Microsoft.Extensions.AI.AIFunction"/>.
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

/// <summary>
/// Per-session shopping list tools. Reads the active <see cref="ChatSession"/>
/// from the singleton <see cref="ICurrentSession"/> so per-session state
/// flows in without any <c>IServiceScope</c>.
/// </summary>
public static class ShoppingListTools
{
    [Description("Adds a product to the current shopping list, or increments the quantity if it's already there.")]
    [ExportAIFunction("add_to_list")]
    public static string AddToList(
        [FromServices] ICurrentSession current,
        [Description("The product sku or name to add (e.g., 'seed-tomato' or 'Heirloom Tomato Seeds').")] string skuOrName,
        [Description("How many to add. Defaults to 1.")] int quantity = 1)
    {
        var product = ProductCatalog.FindByName(skuOrName)
            ?? throw new InvalidOperationException($"No product matched '{skuOrName}'. Try search_products to browse the catalog.");
        var item = current.Session.AddOrIncrement(product, quantity);
        return $"Added {quantity}× {product.Emoji} {product.Name}. Now {item.Quantity} on the list (subtotal {item.Subtotal:C}).";
    }

    [Description("Sets a new quantity for an item already on the list. Setting it to 0 removes the item.")]
    [ExportAIFunction("change_qty")]
    public static string ChangeQuantity(
        [FromServices] ICurrentSession current,
        [Description("The product sku or name on the list.")] string skuOrName,
        [Description("The new quantity. Use 0 to remove the item.")] int quantity)
    {
        var product = ProductCatalog.FindByName(skuOrName)
            ?? throw new InvalidOperationException($"No product matched '{skuOrName}'.");
        var updated = current.Session.ChangeQuantity(product.Sku, quantity);
        return updated is null
            ? $"Removed {product.Emoji} {product.Name} from the list."
            : $"Set {product.Emoji} {product.Name} to {updated.Quantity} (subtotal {updated.Subtotal:C}).";
    }

    [Description("Removes a product from the current shopping list entirely.")]
    [ExportAIFunction("remove_from_list")]
    public static string RemoveFromList(
        [FromServices] ICurrentSession current,
        [Description("The product sku or name to remove.")] string skuOrName)
    {
        var product = ProductCatalog.FindByName(skuOrName)
            ?? throw new InvalidOperationException($"No product matched '{skuOrName}'.");
        return current.Session.Remove(product.Sku)
            ? $"Removed {product.Emoji} {product.Name} from the list."
            : $"{product.Name} wasn't on the list.";
    }

    [Description("Returns every item currently on the shopping list with quantity, unit price, and subtotal.")]
    [ExportAIFunction("show_list")]
    public static IReadOnlyList<ListItem> ShowList(
        [FromServices] ICurrentSession current)
        => current.Session.Snapshot();

    [Description("Checks the current shopping list out as a finalized order. Moves it to the singleton order archive and clears the list.")]
    [ExportAIFunction("checkout_list", ApprovalRequired = true)]
    public static string CheckoutList(
        [FromServices] ICurrentSession current,
        [FromServices] OrderArchive archive)
    {
        var items = current.Session.Snapshot();
        if (items.Count == 0)
            return "The shopping list is empty — nothing to check out.";

        var order = archive.Place(items);
        current.Session.Clear();
        return $"Order {order.Id} placed with {order.Items.Count} item(s) totalling {order.Total:C}.";
    }

    [Description("Discards every item from the current shopping list without saving it.")]
    [ExportAIFunction("cancel_list", ApprovalRequired = true)]
    public static string CancelList(
        [FromServices] ICurrentSession current)
    {
        var count = current.Session.Snapshot().Count;
        if (count == 0)
            return "The shopping list is already empty.";
        current.Session.Clear();
        return $"Discarded {count} item(s) from the shopping list.";
    }
}

/// <summary>
/// Read tools for the durable, singleton archive. Show that singletons are
/// the natural home for cross-session data.
/// </summary>
public static class OrderArchiveTools
{
    [Description("Lists every past order from the singleton archive, newest first.")]
    [ExportAIFunction("list_past_orders")]
    public static IReadOnlyList<Order> ListPastOrders(
        [FromServices] OrderArchive archive)
        => archive.Orders;

    [Description("Lists every saved-as-draft shopping list from the archive, newest first. Drafts are created automatically when the user starts a new chat without checking out.")]
    [ExportAIFunction("list_drafts")]
    public static IReadOnlyList<Draft> ListDrafts(
        [FromServices] OrderArchive archive)
        => archive.Drafts;

    [Description("Copies every item from a past order onto the current shopping list. Useful for re-buying the same kit.")]
    [ExportAIFunction("reorder")]
    public static string Reorder(
        [FromServices] OrderArchive archive,
        [FromServices] ICurrentSession current,
        [Description("The id of the past order to copy (from list_past_orders).")] string orderId)
    {
        var order = archive.FindOrder(orderId)
            ?? throw new InvalidOperationException($"No past order with id '{orderId}'. Call list_past_orders to see available ids.");
        foreach (var item in order.Items)
            current.Session.AddOrIncrement(item.Product, item.Quantity);
        return $"Copied {order.Items.Count} item(s) from {order.Id} onto the current list.";
    }
}
