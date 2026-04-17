namespace AIAttributes.Sample.Garden.Models;

/// <summary>
/// A line item in a shopping list or order.
/// </summary>
public record ListItem(
    Product Product,
    int Quantity)
{
    public decimal Subtotal => Product.Price * Quantity;
}
