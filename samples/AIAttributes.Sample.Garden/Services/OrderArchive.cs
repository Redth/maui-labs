using System.ComponentModel;
using AIAttributes.Sample.Garden.Models;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden.Services;

/// <summary>
/// In-memory archive of past orders. Lives for the lifetime of the app and
/// persists across chat sessions.
/// Demonstrates: exporting tools from a singleton DI service, including
/// a property and a method that uses [FromServices] to inject a sibling service.
/// </summary>
public sealed class OrderArchive
{
    private readonly List<Order> _orders = [];

    // Feature: [ExportAIFunction] on an instance property — the generator
    // resolves OrderArchive from DI then reads the property getter.
    [Description("Lists every past order, newest first.")]
    [ExportAIFunction("list_past_orders")]
    public IReadOnlyList<Order> Orders => _orders;

    public Order Place(IReadOnlyList<ListItem> items)
    {
        var order = new Order(
            Id: $"ord-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            PlacedAt: DateTime.Now,
            Items: [.. items]);
        _orders.Insert(0, order);
        return order;
    }

    public Order? FindOrder(string id) =>
        _orders.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));

    // Feature: [FromServices] injects a different DI service (CurrentCart)
    // as a parameter — the AI never sees it, but the generator wires it up
    // automatically at invocation time. This lets one service call another
    // without constructor coupling.
    [Description("Copies every item from a past order onto the current shopping list.")]
    [ExportAIFunction("reorder")]
    public string Reorder(
        [FromServices] CurrentCart current,
        [Description("The id of the past order to copy (from list_past_orders).")] string orderId)
    {
        var order = FindOrder(orderId)
            ?? throw new InvalidOperationException($"No past order with id '{orderId}'. Call list_past_orders to see available ids.");
        foreach (var item in order.Items)
            current.AddOrIncrement(item.Product, item.Quantity);
        return $"Copied {order.Items.Count} item(s) from {order.Id} onto the current list.";
    }
}
