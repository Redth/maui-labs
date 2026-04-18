using System.ComponentModel;
using AIAttributes.Sample.Garden.Models;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden.Services;

/// <summary>
/// Manages the active shopping cart — item list, running total, and
/// AI-callable operations for adding, removing, and checking out items.
/// Registered as a singleton in DI; call <see cref="Reset"/> on "New Chat".
/// Demonstrates: exporting tools from a DI-registered instance service.
/// </summary>
public sealed class CurrentCart
{
    private readonly List<ListItem> _items = [];

    public string Id { get; private set; } = $"cart-{Guid.NewGuid():N}";
    public CancellationTokenSource Cts { get; private set; } = new();

    public IReadOnlyList<ListItem> Items => _items;

    /// <summary>
    /// Cancels any in-flight chat, clears the item list, and issues a fresh CTS.
    /// </summary>
    public void Reset()
    {
        try { Cts.Cancel(); } catch { /* best effort */ }
        Cts.Dispose();

        _items.Clear();
        Id = $"cart-{Guid.NewGuid():N}";
        Cts = new CancellationTokenSource();
    }

    public ListItem AddOrIncrement(Product product, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");

        var existingIndex = _items.FindIndex(i => string.Equals(i.Product.Sku, product.Sku, StringComparison.OrdinalIgnoreCase));
        ListItem updated;
        if (existingIndex >= 0)
        {
            updated = _items[existingIndex] with { Quantity = _items[existingIndex].Quantity + quantity };
            _items[existingIndex] = updated;
        }
        else
        {
            updated = new ListItem(product, quantity);
            _items.Add(updated);
        }
        return updated;
    }

    public ListItem? ChangeQuantity(string sku, int newQuantity)
    {
        if (newQuantity <= 0)
        {
            Remove(sku);
            return null;
        }

        var idx = _items.FindIndex(i => string.Equals(i.Product.Sku, sku, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
            return null;

        var updated = _items[idx] with { Quantity = newQuantity };
        _items[idx] = updated;
        return updated;
    }

    public bool Remove(string sku)
    {
        var idx = _items.FindIndex(i => string.Equals(i.Product.Sku, sku, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
            return false;
        _items.RemoveAt(idx);
        return true;
    }

    public IReadOnlyList<ListItem> Snapshot() => [.. _items];

    // ── AI Tool Methods ──────────────────────────────────────

    // Feature: instance method on a DI service — the generator resolves
    // CurrentCart from IServiceProvider automatically at invocation time.
    [Description("Returns every item currently on the shopping list with quantity, unit price, and subtotal.")]
    [ExportAIFunction("show_list")]
    public IReadOnlyList<ListItem> ShowList() => Snapshot();

    // Feature: multiple [Description] parameters — each parameter gets its
    // own description that the AI model sees in the JSON schema.
    [Description("Adds a product to the current shopping list, or increments the quantity if it's already there.")]
    [ExportAIFunction("add_to_list")]
    public string AddToList(
        [Description("The product sku or name to add (e.g., 'seed-tomato' or 'Heirloom Tomato Seeds').")] string skuOrName,
        [Description("How many to add. Defaults to 1.")] int quantity = 1)
    {
        var product = ProductCatalog.FindByName(skuOrName)
            ?? throw new InvalidOperationException($"No product matched '{skuOrName}'. Try search_products to browse the catalog.");
        var item = AddOrIncrement(product, quantity);
        return $"Added {quantity}× {product.Emoji} {product.Name}. Now {item.Quantity} on the list (subtotal {item.Subtotal:C}).";
    }

    [Description("Sets a new quantity for an item already on the list. Setting it to 0 removes the item.")]
    [ExportAIFunction("change_qty")]
    public string ChangeQty(
        [Description("The product sku or name on the list.")] string skuOrName,
        [Description("The new quantity. Use 0 to remove the item.")] int quantity)
    {
        var product = ProductCatalog.FindByName(skuOrName)
            ?? throw new InvalidOperationException($"No product matched '{skuOrName}'.");
        var updated = ChangeQuantity(product.Sku, quantity);
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
        return Remove(product.Sku)
            ? $"Removed {product.Emoji} {product.Name} from the list."
            : $"{product.Name} wasn't on the list.";
    }

    // Feature: ApprovalRequired = true — the AI pipeline pauses and asks
    // the user to confirm before this tool is actually executed.
    [Description("Checks the current shopping list out as a finalized order and clears the list.")]
    [ExportAIFunction("checkout_list", ApprovalRequired = true)]
    public string CheckoutList(
        // Feature: [FromServices] on a parameter — OrderArchive is injected
        // from DI at invocation time, not passed by the AI model.
        [FromServices] OrderArchive archive)
    {
        var items = Snapshot();
        if (items.Count == 0)
            return "The shopping list is empty — nothing to check out.";

        var order = archive.Place(items);
        _items.Clear();
        return $"Order {order.Id} placed with {order.Items.Count} item(s) totalling {order.Total:C}.";
    }

    // Feature: ApprovalRequired on a simple no-arg method — demonstrates
    // that destructive operations can be gated behind user approval.
    [Description("Discards every item from the current shopping list.")]
    [ExportAIFunction("cancel_list", ApprovalRequired = true)]
    public string CancelList()
    {
        var count = _items.Count;
        if (count == 0)
            return "The shopping list is already empty.";
        _items.Clear();
        return $"Discarded {count} item(s) from the shopping list.";
    }
}
