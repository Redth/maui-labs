using System.Collections.ObjectModel;
using AIAttributes.Sample.Garden.Models;
using AIAttributes.Sample.Garden.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden.ViewModels;

/// <summary>
/// Cart display modes: compact (dense rows), normal (full cards).
/// </summary>
public enum CartMode { Normal, Compact }

/// <summary>
/// Top-level view model for <see cref="Pages.MainPage"/>.
/// Owns cart state, catalog, orders, navigation, and hosts
/// a <see cref="ChatViewModel"/> for the AI chat loop.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly CurrentCart _currentCart;
    private readonly IOrderArchive _archive;
    private bool _initialized;

    public MainViewModel(
        ChatViewModel chat,
        CurrentCart currentCart,
        IOrderArchive archive)
    {
        Chat = chat;
        _currentCart = currentCart;
        _archive = archive;

        // Wire up: after every AI turn, refresh cart + archive
        Chat.TurnCompleted += () =>
        {
            RefreshShoppingList();
            RefreshArchive();
        };

        // Build catalog grouped by category
        var groups = ProductCatalog.All
            .GroupBy(p => p.Category)
            .Select(g =>
            {
                var group = new CatalogGroupViewModel(g.Key);
                group.AddRange(g.Select(p => new CatalogItemViewModel(p)));
                return group;
            })
            .ToList();
        CatalogProducts = new(groups.SelectMany(g => g));
        CatalogGroups = groups;
    }

    /// <summary>The chat sub-system — bind ChatView to this.</summary>
    public ChatViewModel Chat { get; }

    public ObservableCollection<ShoppingListItemViewModel> ShoppingList { get; } = [];
    public ObservableCollection<OrderViewModel> PastOrders { get; } = [];
    public ObservableCollection<CatalogItemViewModel> CatalogProducts { get; }
    public IReadOnlyList<CatalogGroupViewModel> CatalogGroups { get; }

    [ObservableProperty]
    private string _shoppingListTotal = $"Total: {0:C}";

    // ─── Cart mode ──────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNormalMode))]
    [NotifyPropertyChangedFor(nameof(IsCompactMode))]
    [NotifyPropertyChangedFor(nameof(CartModeLabel))]
    private CartMode _cartMode = CartMode.Normal;

    public bool IsNormalMode => CartMode == CartMode.Normal;
    public bool IsCompactMode => CartMode == CartMode.Compact;
    public string CartModeLabel => CartMode switch
    {
        CartMode.Normal => "Compact",
        CartMode.Compact => "Normal",
        _ => "Toggle"
    };

    [ObservableProperty]
    private bool _hasCartItems;

    /// <summary>Called once from <see cref="Pages.MainPage.OnAppearing"/>.</summary>
    public void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;

        Chat.Initialize();
        StartNewSession();
        RefreshArchive();
    }

    [RelayCommand]
    private void StartNewSession()
    {
        _currentCart.Clear();
        Chat.StartNewSession();
        CartMode = CartMode.Normal;
        RefreshShoppingList();
    }

    [RelayCommand]
    private void CheckoutCart()
    {
        if (_currentCart.Items.Count == 0)
            return;

        _archive.Checkout(_currentCart);

        RefreshShoppingList();
        RefreshArchive();
    }

    [RelayCommand]
    private void ReorderPastOrder(string? orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return;

        _archive.Reorder(orderId, _currentCart);
        RefreshShoppingList();
    }

    [RelayCommand]
    private void ClearPastOrders()
    {
        _archive.Clear();
        RefreshArchive();
    }

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
    private void AddFromCatalog(string? sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
            return;
        _currentCart.AddItem(sku);
        RefreshShoppingList();
    }

    [RelayCommand]
    private async Task ShowCartAsync()
    {
        await Shell.Current.GoToAsync("cart");
    }

    // ─── ViewModel-level AI tools ───────────────────────────────────
    // These demonstrate [ExportAIFunction] on a ViewModel — the AI can
    // directly drive UI navigation and view state.

    [ExportAIFunction("navigate_to_page",
        Description = "Navigate to a page in the app. Use 'catalog' to browse products, 'orders' to see past orders, 'cart' to view the shopping cart. Pages open as modal overlays.")]
    public async Task<string> NavigateToPageAsync(
        [System.ComponentModel.Description("The page to navigate to: 'catalog', 'orders', or 'cart'")] string page)
    {
        var route = page?.ToLowerInvariant() switch
        {
            "catalog" => "catalog",
            "orders" => "orders",
            "cart" => "cart",
            _ => throw new ArgumentException($"Unknown page '{page}'. Valid pages: 'catalog', 'orders', 'cart'.")
        };

        var tcs = new TaskCompletionSource();
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Shell.Current.GoToAsync(route);
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        await tcs.Task;
        return $"Navigated to {page}. The {page} page is now showing as a modal overlay.";
    }

    [ExportAIFunction("dismiss_page",
        Description = "Close the current modal page (catalog or orders) and return to the main shop view.")]
    public async Task<string> DismissPageAsync()
    {
        var tcs = new TaskCompletionSource();
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Shell.Current.GoToAsync("..");
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        await tcs.Task;
        return "Returned to the main shop view.";
    }

    [ExportAIFunction("set_cart_mode",
        Description = "Change the shopping cart display mode. 'normal' shows full cards with emoji. 'compact' shows dense single-line rows.")]
    public string SetCartViewMode(
        [System.ComponentModel.Description("The view mode: 'normal' or 'compact'")] string mode)
    {
        CartMode = mode?.ToLowerInvariant() switch
        {
            "normal" => CartMode.Normal,
            "compact" => CartMode.Compact,
            _ => throw new ArgumentException($"Unknown mode '{mode}'. Valid modes: 'normal', 'compact'.")
        };
        return $"Cart display mode set to {CartMode.ToString().ToLowerInvariant()}.";
    }

    [ExportAIFunction("get_cart_mode",
        Description = "Get the current cart display mode ('normal' or 'compact').")]
    public string GetCartViewMode() => CartMode.ToString().ToLowerInvariant();

    // ─────────────────────────────────────────────────────────────────

    private void RefreshShoppingList()
    {
        var source = _currentCart.Items;
        SyncCollection(ShoppingList, source, v => v.Sku, i => i.Product.Sku, i => new ShoppingListItemViewModel(i));
        ShoppingListTotal = $"Total: {source.Sum(i => i.Subtotal):C}";
        HasCartItems = source.Count > 0;
    }

    private void RefreshArchive()
    {
        var source = _archive.Orders;
        SyncCollection(PastOrders, source, v => v.OrderId, o => o.Id, o => new OrderViewModel(o));
    }

    /// <summary>
    /// Syncs an <see cref="ObservableCollection{T}"/> with a source list by key,
    /// adding/removing only the deltas so the UI doesn't flicker.
    /// </summary>
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
