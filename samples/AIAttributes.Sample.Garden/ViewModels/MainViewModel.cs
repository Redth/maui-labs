using System.Collections.ObjectModel;
using AIAttributes.Sample.Garden.Models;
using AIAttributes.Sample.Garden.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden.ViewModels;

/// <summary>
/// Top-level view model for <see cref="Pages.MainPage"/>.
/// Owns page navigation, catalog browsing, and order history.
/// Chat and cart views resolve their own VMs via <see cref="ViewModelBinder"/>.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly CurrentCart _currentCart;
    private readonly IOrderArchive _archive;
    private readonly ChatViewModel _chat;
    private bool _initialized;

    public MainViewModel(
        ChatViewModel chat,
        CurrentCart currentCart,
        IOrderArchive archive)
    {
        _chat = chat;
        _currentCart = currentCart;
        _archive = archive;

        // After every AI turn, refresh orders (cart self-refreshes via CurrentCart.Changed)
        _chat.TurnCompleted += RefreshArchive;

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

    public ObservableCollection<OrderViewModel> PastOrders { get; } = [];
    public ObservableCollection<CatalogItemViewModel> CatalogProducts { get; }
    public IReadOnlyList<CatalogGroupViewModel> CatalogGroups { get; }

    /// <summary>Called once from <see cref="Pages.MainPage.OnAppearing"/>.</summary>
    public void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;

        _chat.Initialize();
        StartNewSession();
        RefreshArchive();
    }

    [RelayCommand]
    private void StartNewSession()
    {
        _currentCart.Clear();
        _chat.StartNewSession();
    }

    [RelayCommand]
    private void ReorderPastOrder(string? orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return;
        _archive.Reorder(orderId, _currentCart);
    }

    [RelayCommand]
    private async Task ShowCartAsync()
    {
        await Shell.Current.GoToAsync("cart");
    }

    [RelayCommand]
    private void ClearPastOrders()
    {
        _archive.Clear();
        RefreshArchive();
    }

    // ─── Navigation AI tools ────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────────────

    private void RefreshArchive()
    {
        var source = _archive.Orders;
        SyncCollection(PastOrders, source, v => v.OrderId, o => o.Id, o => new OrderViewModel(o));
    }

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
