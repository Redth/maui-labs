using AIAttributes.Sample.Garden.Models;

namespace AIAttributes.Sample.Garden.Services;

/// <summary>
/// Persists completed orders across chat sessions.
/// </summary>
public interface IOrderArchive
{
    IReadOnlyList<Order> Orders { get; }

    Order Place(IReadOnlyList<ListItem> items);
    Order? FindOrder(string id);
    string Reorder(CurrentCart current, string orderId);
}
