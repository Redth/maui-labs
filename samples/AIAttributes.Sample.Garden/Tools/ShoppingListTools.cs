using System.ComponentModel;
using AIAttributes.Sample.Garden.Models;
using AIAttributes.Sample.Garden.Services;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden.Tools;

/// <summary>
/// Shopping list tools. Reads the active <see cref="Cart"/> from
/// <see cref="CurrentCart"/> to add, remove, and manage items.
/// </summary>
public static class ShoppingListTools
{
    [Description("Adds a product to the current shopping list, or increments the quantity if it's already there.")]
    [ExportAIFunction("add_to_list")]
    public static string AddToList(
        [FromServices] CurrentCart current,
        [Description("The product sku or name to add (e.g., 'seed-tomato' or 'Heirloom Tomato Seeds').")] string skuOrName,
        [Description("How many to add. Defaults to 1.")] int quantity = 1)
    {
        var product = ProductCatalog.FindByName(skuOrName)
            ?? throw new InvalidOperationException($"No product matched '{skuOrName}'. Try search_products to browse the catalog.");
        var item = current.Cart.AddOrIncrement(product, quantity);
        return $"Added {quantity}× {product.Emoji} {product.Name}. Now {item.Quantity} on the list (subtotal {item.Subtotal:C}).";
    }

    [Description("Sets a new quantity for an item already on the list. Setting it to 0 removes the item.")]
    [ExportAIFunction("change_qty")]
    public static string ChangeQuantity(
        [FromServices] CurrentCart current,
        [Description("The product sku or name on the list.")] string skuOrName,
        [Description("The new quantity. Use 0 to remove the item.")] int quantity)
    {
        var product = ProductCatalog.FindByName(skuOrName)
            ?? throw new InvalidOperationException($"No product matched '{skuOrName}'.");
        var updated = current.Cart.ChangeQuantity(product.Sku, quantity);
        return updated is null
            ? $"Removed {product.Emoji} {product.Name} from the list."
            : $"Set {product.Emoji} {product.Name} to {updated.Quantity} (subtotal {updated.Subtotal:C}).";
    }

    [Description("Removes a product from the current shopping list entirely.")]
    [ExportAIFunction("remove_from_list")]
    public static string RemoveFromList(
        [FromServices] CurrentCart current,
        [Description("The product sku or name to remove.")] string skuOrName)
    {
        var product = ProductCatalog.FindByName(skuOrName)
            ?? throw new InvalidOperationException($"No product matched '{skuOrName}'.");
        return current.Cart.Remove(product.Sku)
            ? $"Removed {product.Emoji} {product.Name} from the list."
            : $"{product.Name} wasn't on the list.";
    }

    [Description("Returns every item currently on the shopping list with quantity, unit price, and subtotal.")]
    [ExportAIFunction("show_list")]
    public static IReadOnlyList<ListItem> ShowList(
        [FromServices] CurrentCart current)
        => current.Cart.Snapshot();

    [Description("Checks the current shopping list out as a finalized order and clears the list.")]
    [ExportAIFunction("checkout_list", ApprovalRequired = true)]
    public static string CheckoutList(
        [FromServices] CurrentCart current,
        [FromServices] OrderArchive archive)
    {
        var items = current.Cart.Snapshot();
        if (items.Count == 0)
            return "The shopping list is empty — nothing to check out.";

        var order = archive.Place(items);
        current.Cart.Clear();
        return $"Order {order.Id} placed with {order.Items.Count} item(s) totalling {order.Total:C}.";
    }

    [Description("Discards every item from the current shopping list.")]
    [ExportAIFunction("cancel_list", ApprovalRequired = true)]
    public static string CancelList(
        [FromServices] CurrentCart current)
    {
        var count = current.Cart.Snapshot().Count;
        if (count == 0)
            return "The shopping list is already empty.";
        current.Cart.Clear();
        return $"Discarded {count} item(s) from the shopping list.";
    }
}
