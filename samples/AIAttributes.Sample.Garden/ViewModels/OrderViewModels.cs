using AIAttributes.Sample.Garden.Models;

namespace AIAttributes.Sample.Garden.ViewModels;

/// <summary>
/// View model for one row in the past-orders list.
/// </summary>
public sealed class OrderViewModel(Order order)
{
    public string OrderId => order.Id;
    public string PlacedAt => order.PlacedAt.ToString("MMM d, h:mm tt");
    public string Total => order.Total.ToString("C");
    public IReadOnlyList<OrderLineViewModel> Lines { get; } =
        [.. order.Items.Select(i => new OrderLineViewModel(i))];
}

/// <summary>
/// One line item inside an expanded order card.
/// </summary>
public sealed class OrderLineViewModel(ListItem item)
{
    public string Emoji => item.Product.Emoji;
    public string Line => $"{item.Quantity}× {item.Product.Name}  ·  {item.Subtotal:C}";
}
