using AIAttributes.Sample.Garden.Models;

namespace AIAttributes.Sample.Garden.Services;

/// <summary>
/// Mutable shopping list for one chat conversation. The view model creates
/// a new instance on "New Chat" and publishes it via <see cref="CurrentCart"/>.
/// </summary>
public sealed class Cart(string id)
{
    private readonly List<ListItem> _items = [];

    public string Id { get; } = id;
    public DateTime StartedAt { get; } = DateTime.Now;
    public CancellationTokenSource Cts { get; } = new();

    public IReadOnlyList<ListItem> Items => _items;

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

    public void Clear()
    {
        if (_items.Count == 0)
            return;
        _items.Clear();
    }
}
