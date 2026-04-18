using AIAttributes.Sample.Garden.Models;

namespace AIAttributes.Sample.Garden.Services;

/// <summary>
/// In-memory order archive. Orders live for the lifetime of the app
/// and are lost on restart. Good for demos and testing.
/// </summary>
public sealed class InMemoryOrderArchive : IOrderArchive
{
    private readonly List<Order> _orders = [];

    public IReadOnlyList<Order> Orders => _orders;

    public Order? FindOrder(string orderId) =>
        _orders.FirstOrDefault(o => string.Equals(o.Id, orderId, StringComparison.OrdinalIgnoreCase));

    public Order Checkout(CurrentCart cart)
    {
        var items = cart.Items;
        if (items.Count == 0)
            throw new InvalidOperationException("The cart is empty — nothing to check out.");

        var order = new Order(
            Id: $"ord-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            PlacedAt: DateTime.Now,
            Items: [.. items]);
        _orders.Insert(0, order);

        cart.Clear();
        return order;
    }

    public void Reorder(string orderId, CurrentCart cart)
    {
        var order = FindOrder(orderId)
            ?? throw new InvalidOperationException($"No past order with id '{orderId}'. Call list_past_orders to see available ids.");
        foreach (var item in order.Items)
            cart.AddItem(item.Product.Sku, item.Quantity);
    }
}
