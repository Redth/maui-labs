using System.Collections.ObjectModel;
using AIAttributes.Sample.Garden.Models;

namespace AIAttributes.Sample.Garden.ViewModels;

/// <summary>
/// Read-only view model for a row in the "Past Orders" list.
/// </summary>
public sealed class OrderViewModel(Order order)
{
    public Order Order { get; } = order;
    public string Id => Order.Id;
    public string PlacedAt => Order.PlacedAt.ToString("MMM d, h:mm tt");
    public string Total => Order.Total.ToString("C");
    public string Summary => $"{Order.Items.Count} item(s)  ·  {Order.Total:C}";
    public IReadOnlyList<OrderLineViewModel> Lines { get; } =
        [.. order.Items.Select(i => new OrderLineViewModel(i))];
}

/// <summary>
/// Read-only view model for a row in the "Saved Drafts" list.
/// </summary>
public sealed class DraftViewModel(Draft draft)
{
    public Draft Draft { get; } = draft;
    public string Id => Draft.Id;
    public string SavedAt => Draft.SavedAt.ToString("MMM d, h:mm tt");
    public string Summary => $"{Draft.Items.Count} item(s)  ·  {Draft.Total:C}";
    public IReadOnlyList<OrderLineViewModel> Lines { get; } =
        [.. draft.Items.Select(i => new OrderLineViewModel(i))];
}

/// <summary>
/// One line item inside an expanded order/draft card.
/// </summary>
public sealed class OrderLineViewModel(ListItem item)
{
    public string Emoji => item.Product.Emoji;
    public string Name => item.Product.Name;
    public string Line => $"{item.Quantity}× {item.Product.Name}  ·  {item.Subtotal:C}";
}
