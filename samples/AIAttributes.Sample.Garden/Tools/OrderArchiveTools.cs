using System.ComponentModel;
using AIAttributes.Sample.Garden.Models;
using AIAttributes.Sample.Garden.Services;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden.Tools;

/// <summary>
/// Tools for browsing the order archive.
/// </summary>
public static class OrderArchiveTools
{
    [Description("Lists every past order, newest first.")]
    [ExportAIFunction("list_past_orders")]
    public static IReadOnlyList<Order> ListPastOrders(
        [FromServices] OrderArchive archive)
        => archive.Orders;

    [Description("Copies every item from a past order onto the current shopping list.")]
    [ExportAIFunction("reorder")]
    public static string Reorder(
        [FromServices] OrderArchive archive,
        [FromServices] CurrentCart current,
        [Description("The id of the past order to copy (from list_past_orders).")] string orderId)
    {
        var order = archive.FindOrder(orderId)
            ?? throw new InvalidOperationException($"No past order with id '{orderId}'. Call list_past_orders to see available ids.");
        foreach (var item in order.Items)
            current.Cart.AddOrIncrement(item.Product, item.Quantity);
        return $"Copied {order.Items.Count} item(s) from {order.Id} onto the current list.";
    }
}
