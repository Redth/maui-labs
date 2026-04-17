using AIAttributes.Sample.Garden.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAttributes.Sample.Garden.ViewModels;

/// <summary>
/// View-model wrapper around a <see cref="ListItem"/> so the workspace
/// can ghost items that are about to be removed/checked-out without
/// mutating the immutable record on <see cref="Services.ChatSession"/>.
/// </summary>
public sealed partial class ShoppingListItemViewModel(ListItem item) : ObservableObject
{
    public ListItem Item { get; } = item;

    public string Sku => Item.Product.Sku;
    public string Name => Item.Product.Name;
    public string Emoji => Item.Product.Emoji;
    public string Category => Item.Product.Category;
    public int Quantity => Item.Quantity;
    public string Subtotal => Item.Subtotal.ToString("C");
    public string QuantityLine => $"× {Item.Quantity}  ·  {Item.Subtotal:C}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPending))]
    [NotifyPropertyChangedFor(nameof(PendingLabel))]
    private PendingAction _pending;

    public bool IsPending => Pending != PendingAction.None;

    public string PendingLabel => Pending switch
    {
        PendingAction.Checkout => "🔒 Pending checkout",
        PendingAction.Cancel   => "🔒 Pending cancel",
        PendingAction.Remove   => "🔒 Pending removal",
        _ => string.Empty,
    };
}

/// <summary>
/// Which approval-pending action is queued against a shopping-list item.
/// </summary>
public enum PendingAction
{
    None,
    Checkout,
    Cancel,
    Remove,
}
