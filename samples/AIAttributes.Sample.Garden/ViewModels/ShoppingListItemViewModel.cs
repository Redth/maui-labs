using System.ComponentModel;
using System.Runtime.CompilerServices;
using AIAttributes.Sample.Garden.Models;

namespace AIAttributes.Sample.Garden.ViewModels;

/// <summary>
/// View-model wrapper around a <see cref="ListItem"/> so the workspace
/// can ghost items that are about to be removed/checked-out without
/// mutating the immutable record on <see cref="Services.ChatSession"/>.
/// </summary>
public sealed class ShoppingListItemViewModel(ListItem item) : INotifyPropertyChanged
{
    public ListItem Item { get; } = item;

    public string Sku => Item.Product.Sku;
    public string Name => Item.Product.Name;
    public string Emoji => Item.Product.Emoji;
    public string Category => Item.Product.Category;
    public int Quantity => Item.Quantity;
    public string Subtotal => Item.Subtotal.ToString("C");
    public string QuantityLine => $"× {Item.Quantity}  ·  {Item.Subtotal:C}";

    private PendingAction _pending;
    public PendingAction Pending
    {
        get => _pending;
        set
        {
            if (_pending == value)
                return;
            _pending = value;
            OnChanged();
            OnChanged(nameof(IsPending));
            OnChanged(nameof(PendingLabel));
        }
    }

    public bool IsPending => _pending != PendingAction.None;

    public string PendingLabel => _pending switch
    {
        PendingAction.Checkout => "🔒 Pending checkout",
        PendingAction.Cancel   => "🔒 Pending cancel",
        PendingAction.Remove   => "🔒 Pending removal",
        _ => string.Empty,
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Which approval-pending action is queued against a shopping-list item.
/// Drives the per-item ghosting overlay.
/// </summary>
public enum PendingAction
{
    None,
    Checkout,
    Cancel,
    Remove,
}
