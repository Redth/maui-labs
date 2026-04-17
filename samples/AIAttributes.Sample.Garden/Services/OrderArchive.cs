using AIAttributes.Sample.Garden.Models;

namespace AIAttributes.Sample.Garden.Services;

/// <summary>
/// In-memory archive of past orders. Lives for the lifetime of the app and
/// persists across chat sessions.
/// </summary>
public sealed class OrderArchive
{
    private readonly List<Order> _orders = [];

    public IReadOnlyList<Order> Orders => _orders;

    /// <summary>Raised whenever the archive contents change.</summary>
    public event Action? Changed;

    public Order Place(IReadOnlyList<ListItem> items)
    {
        var order = new Order(
            Id: $"ord-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            PlacedAt: DateTime.Now,
            Items: [.. items]);
        _orders.Insert(0, order);
        Changed?.Invoke();
        return order;
    }

    public Order? FindOrder(string id) =>
        _orders.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));
}
