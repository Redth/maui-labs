using AIAttributes.Sample.Garden.Models;

namespace AIAttributes.Sample.Garden.Services;

/// <summary>
/// Singleton archive of past orders and saved drafts. Lives for the lifetime
/// of the app — does NOT clear on "New Chat". This is the durable bottom-half
/// of the workspace panel.
/// </summary>
/// <remarks>
/// Storage is in-memory: orders and drafts vanish when the app exits.
/// Realistic apps would back this with <c>Preferences</c>, a database, or a
/// service call; the sample stays in-memory to keep focus on the DI lifetime
/// story.
/// </remarks>
public sealed class OrderArchive
{
    private readonly List<Order> _orders = [];
    private readonly List<Draft> _drafts = [];

    public IReadOnlyList<Order> Orders => _orders;
    public IReadOnlyList<Draft> Drafts => _drafts;

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

    public Draft SaveDraft(IReadOnlyList<ListItem> items)
    {
        var draft = new Draft(
            Id: $"draft-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            SavedAt: DateTime.Now,
            Items: [.. items]);
        _drafts.Insert(0, draft);
        Changed?.Invoke();
        return draft;
    }

    public Order? FindOrder(string id) =>
        _orders.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));
}
