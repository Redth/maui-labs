using System.ComponentModel;
using AIAttributes.Sample.Garden.Models;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden.Services;

/// <summary>
/// Manages the active shopping list (<see cref="Cart"/>) and exposes
/// AI-callable operations for adding, removing, and checking out items.
/// </summary>
public sealed class CurrentCart
{
    private Cart _cart = new("cart-initial");

    public Cart Cart => _cart;

    public void Set(Cart cart) => _cart = cart;

    // ── AI Tool Methods ──────────────────────────────────────

    [Description("Returns every item currently on the shopping list with quantity, unit price, and subtotal.")]
    [ExportAIFunction("show_list")]
    public IReadOnlyList<ListItem> ShowList() => Cart.Snapshot();

    [Description("Adds a product to the current shopping list, or increments the quantity if it's already there.")]
    [ExportAIFunction("add_to_list")]
    public string AddToList(
        [Description("The product sku or name to add (e.g., 'seed-tomato' or 'Heirloom Tomato Seeds').")] string skuOrName,
        [Description("How many to add. Defaults to 1.")] int quantity = 1)
    {
        var product = ProductCatalog.FindByName(skuOrName)
            ?? throw new InvalidOperationException($"No product matched '{skuOrName}'. Try search_products to browse the catalog.");
        var item = Cart.AddOrIncrement(product, quantity);
        return $"Added {quantity}× {product.Emoji} {product.Name}. Now {item.Quantity} on the list (subtotal {item.Subtotal:C}).";
    }

    [Description("Sets a new quantity for an item already on the list. Setting it to 0 removes the item.")]
    [ExportAIFunction("change_qty")]
    public string ChangeQuantity(
        [Description("The product sku or name on the list.")] string skuOrName,
        [Description("The new quantity. Use 0 to remove the item.")] int quantity)
    {
        var product = ProductCatalog.FindByName(skuOrName)
            ?? throw new InvalidOperationException($"No product matched '{skuOrName}'.");
        var updated = Cart.ChangeQuantity(product.Sku, quantity);
        return updated is null
            ? $"Removed {product.Emoji} {product.Name} from the list."
            : $"Set {product.Emoji} {product.Name} to {updated.Quantity} (subtotal {updated.Subtotal:C}).";
    }

    [Description("Removes a product from the current shopping list entirely.")]
    [ExportAIFunction("remove_from_list")]
    public string RemoveFromList(
        [Description("The product sku or name to remove.")] string skuOrName)
    {
        var product = ProductCatalog.FindByName(skuOrName)
            ?? throw new InvalidOperationException($"No product matched '{skuOrName}'.");
        return Cart.Remove(product.Sku)
            ? $"Removed {product.Emoji} {product.Name} from the list."
            : $"{product.Name} wasn't on the list.";
    }

    [Description("Checks the current shopping list out as a finalized order and clears the list.")]
    [ExportAIFunction("checkout_list", ApprovalRequired = true)]
    public string CheckoutList(
        [FromServices] OrderArchive archive)
    {
        var items = Cart.Snapshot();
        if (items.Count == 0)
            return "The shopping list is empty — nothing to check out.";

        var order = archive.Place(items);
        Cart.Clear();
        return $"Order {order.Id} placed with {order.Items.Count} item(s) totalling {order.Total:C}.";
    }

    [Description("Discards every item from the current shopping list.")]
    [ExportAIFunction("cancel_list", ApprovalRequired = true)]
    public string CancelList()
    {
        var count = Cart.Snapshot().Count;
        if (count == 0)
            return "The shopping list is already empty.";
        Cart.Clear();
        return $"Discarded {count} item(s) from the shopping list.";
    }
}
