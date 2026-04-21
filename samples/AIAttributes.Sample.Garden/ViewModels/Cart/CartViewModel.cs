using System.Collections.ObjectModel;
using System.ComponentModel;
using AIAttributes.Sample.Garden.Messages;
using AIAttributes.Sample.Garden.Models;
using AIAttributes.Sample.Garden.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden.ViewModels;

/// <summary>
/// Owns all cart state: items, display mode, checkout, and the AI tools
/// that manipulate the cart display. Designed to be reusable — any page
/// can host a cart view bound to this VM.
/// </summary>
public sealed partial class CartViewModel : ObservableObject, IRecipient<CartChangedMessage>
{
    private readonly CurrentCart _currentCart;
    private readonly IOrderArchive _archive;

    public CartViewModel(CurrentCart currentCart, IOrderArchive archive)
    {
        _currentCart = currentCart;
        _archive = archive;

        WeakReferenceMessenger.Default.Register(this);
    }

    void IRecipient<CartChangedMessage>.Receive(CartChangedMessage message) => Refresh();

    public ObservableCollection<CartItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial string CartTotal { get; set; } = $"Total: {0:C}";

    [ObservableProperty]
    public partial bool HasItems { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNormalMode))]
    [NotifyPropertyChangedFor(nameof(IsCompactMode))]
    [NotifyPropertyChangedFor(nameof(CartModeLabel))]
    [ExportAIFunction("get_cart_mode")]
    [Description("Get the current cart display mode.")]
    public partial CartMode CartMode { get; set; } = CartMode.Normal;

    public bool IsNormalMode => CartMode == CartMode.Normal;
    public bool IsCompactMode => CartMode == CartMode.Compact;
    public string CartModeLabel => CartMode switch
    {
        CartMode.Normal => "Compact",
        CartMode.Compact => "Normal",
        _ => "Toggle"
    };

    [RelayCommand]
    private void CycleCartMode()
    {
        CartMode = CartMode switch
        {
            CartMode.Normal => CartMode.Compact,
            CartMode.Compact => CartMode.Normal,
            _ => CartMode.Normal
        };
    }

    [RelayCommand]
    private void Checkout()
    {
        if (_currentCart.Items.Count == 0)
            return;

        _archive.Checkout(_currentCart);
    }

    [RelayCommand]
    private void AddFromCatalog(string? sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
            return;

        _currentCart.AddItem(sku);
    }

    /// <summary>
    /// Refresh the observable collections from the underlying cart model.
    /// </summary>
    public void Refresh()
    {
        var source = _currentCart.Items;

        SyncCollection(Items, source, v => v.Sku, i => i.Product.Sku, i => new CartItemViewModel(i));

        CartTotal = $"Total: {source.Sum(i => i.Subtotal):C}";
        HasItems = source.Count > 0;
    }

    /// <summary>
    /// Clear cart and reset mode.
    /// </summary>
    public void Clear()
    {
        _currentCart.Clear();
        CartMode = CartMode.Normal;
    }

    // ─── AI tools for cart display ──────────────────────────────────

    [ExportAIFunction("set_cart_mode")]
    [Description("Change the shopping cart display mode. 'normal' shows full cards with icons and details. 'compact' shows dense single-line rows.")]
    public string SetCartViewMode(
        [Description("The view mode: 'normal' or 'compact'")] string mode)
    {
        CartMode = mode?.ToLowerInvariant() switch
        {
            "normal" => CartMode.Normal,
            "compact" => CartMode.Compact,
            _ => throw new ArgumentException($"Unknown mode '{mode}'. Valid modes: 'normal', 'compact'.")
        };
        return $"Cart display mode set to {CartMode.ToString().ToLowerInvariant()}.";
    }

    // ─────────────────────────────────────────────────────────────────

    private static void SyncCollection<TVM, TModel>(
        ObservableCollection<TVM> target,
        IReadOnlyList<TModel> source,
        Func<TVM, string> vmKey,
        Func<TModel, string> modelKey,
        Func<TModel, TVM> create)
    {
        var sourceKeys = new HashSet<string>(source.Select(modelKey));
        for (int i = target.Count - 1; i >= 0; i--)
        {
            if (!sourceKeys.Contains(vmKey(target[i])))
                target.RemoveAt(i);
        }
        var existing = new HashSet<string>(target.Select(vmKey));
        foreach (var item in source)
        {
            if (!existing.Contains(modelKey(item)))
                target.Add(create(item));
        }
    }
}
