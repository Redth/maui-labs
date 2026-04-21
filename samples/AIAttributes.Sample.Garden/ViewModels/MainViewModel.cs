using System.Collections.ObjectModel;
using AIAttributes.Sample.Garden.Models;
using AIAttributes.Sample.Garden.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.AI;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden.ViewModels;

/// <summary>
/// Cart display modes: collapsed (summary bar), compact (dense rows), normal (full cards).
/// </summary>
public enum CartMode { Normal, Compact, Collapsed }

/// <summary>
/// View model for a single tool shown in the empty-state placeholder.
/// </summary>
public sealed record ToolInfoViewModel(
    string Name,
    string Description);

/// <summary>
/// Top-level view model bound to <see cref="MainPage"/>. Owns the chat loop,
/// the <see cref="CurrentCart"/>, and projects the
/// <see cref="IOrderArchive"/> into the UI.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>
    /// Source-generated tool context that merges all tool sources into one.
    /// Nested inside <see cref="MainViewModel"/> to demonstrate that the generator
    /// fully supports private and nested context classes.
    ///
    /// Demonstrates three distinct attribute patterns:
    /// <list type="bullet">
    ///   <item><b>Static class</b> — ProductCatalog: tools on a plain static class.</item>
    ///   <item><b>Instance class</b> — CurrentCart: tools on a DI-registered instance.</item>
    ///   <item><b>Interface</b> — IOrderArchive: tools declared on the interface so
    ///     any implementation (InMemoryOrderArchive, PreferencesOrderArchive, …)
    ///     is AI-capable without changing a single attribute.</item>
    /// </list>
    /// </summary>
    [AIToolSource(typeof(ProductCatalog))]
    [AIToolSource(typeof(CurrentCart))]
    [AIToolSource(typeof(IOrderArchive))]
    [AIToolSource(typeof(MainViewModel))]
    private partial class GardenShopTools : AIToolContext { }

    private readonly IChatClient _chatClient;
    private readonly CurrentCart _currentCart;
    private readonly IOrderArchive _archive;

    private List<ChatMessage> _history = [];
    private ToolApprovalRequestContent? _pendingApproval;
    private CancellationTokenSource _cts = new();
    private bool _initialized;

    public MainViewModel(
        IServiceProvider rootProvider,
        IChatClient innerChatClient,
        CurrentCart currentCart,
        IOrderArchive archive)
    {
        _chatClient = new ChatClientBuilder(innerChatClient)
            .UseFunctionInvocation()
            .Build(rootProvider);
        _currentCart = currentCart;
        _archive = archive;

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
    }

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];
    public ObservableCollection<ToolInfoViewModel> AvailableTools { get; } = [];
    public ObservableCollection<ShoppingListItemViewModel> ShoppingList { get; } = [];
    public ObservableCollection<OrderViewModel> PastOrders { get; } = [];
    public ObservableCollection<CatalogItemViewModel> CatalogProducts { get; }

    public IReadOnlyList<string> SuggestionPrompts { get; } =
    [
        "Add 5 packs of tomato seeds and a hand trowel",
        "Show compact cart",
        "Collapse the cart",
        "Check out my list",
        "Show me the catalog",
        "Show my orders",
        "Go back to shopping",
        "Re-order my last order",
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? _inputText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInputVisible))]
    private bool _isApprovalPending;

    public bool IsInputVisible => !IsApprovalPending;

    [ObservableProperty]
    private string _approvalText = "";

    [ObservableProperty]
    private string _shoppingListTotal = $"Total: {0:C}";

    // ─── Cart mode (3 states) ──────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNormalMode))]
    [NotifyPropertyChangedFor(nameof(IsCompactMode))]
    [NotifyPropertyChangedFor(nameof(IsCollapsedMode))]
    [NotifyPropertyChangedFor(nameof(CartModeLabel))]
    [NotifyPropertyChangedFor(nameof(CartSummary))]
    private CartMode _cartMode = CartMode.Normal;

    public bool IsNormalMode => CartMode == CartMode.Normal;
    public bool IsCompactMode => CartMode == CartMode.Compact;
    public bool IsCollapsedMode => CartMode == CartMode.Collapsed;
    public string CartModeLabel => CartMode switch
    {
        CartMode.Normal => "Compact",
        CartMode.Compact => "Collapse",
        CartMode.Collapsed => "Expand",
        _ => "Toggle"
    };
    public string CartSummary
    {
        get
        {
            var items = _currentCart.Items;
            var count = items.Sum(i => i.Quantity);
            var total = items.Sum(i => i.Subtotal);
            return $"{total:C} · {count} item{(count != 1 ? "s" : "")}";
        }
    }

    // ─── Cart item count for badge ─────────────────────────────────

    /// <summary>Raised whenever a new message is appended, so views can scroll.</summary>
    public event Action<ChatMessageViewModel>? MessageAdded;

    /// <summary>Called once from <see cref="MainPage.OnAppearing"/>.</summary>
    public void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;

        StartNewSession();
        RefreshAvailableTools();
        RefreshArchive();
    }

    [RelayCommand]
    private void StartNewSession()
    {
        try { _cts.Cancel(); } catch { /* best effort */ }
        _cts.Dispose();
        _cts = new CancellationTokenSource();

        _currentCart.Clear();

        _history =
        [
            new(ChatRole.System,
                """
                You are a helpful garden-shop assistant. Help the user browse seeds, soil,
                tools, and equipment, manage their shopping list, and review past orders.

                IMPORTANT RULES:
                - Always use tools to perform actions. Never assume you know the cart state
                  from previous messages — call show_list to check.
                - Use search_products to discover items by name or category.
                - When the user says "check out", call checkout_list (which requires approval).
                - After checkout clears the cart, the cart is EMPTY. If the user asks to add
                  items again, always call add_to_list — do not say items are already there.

                NAVIGATION TOOLS:
                - Use navigate_to_page("catalog") when the user wants to browse the full product catalog.
                - Use navigate_to_page("orders") when the user wants to see their past orders.
                - Use dismiss_page() to close any modal overlay and return to the main shop view.

                CART DISPLAY TOOLS:
                - Use set_cart_mode("normal") for the full card view with emoji and details.
                - Use set_cart_mode("compact") for a dense single-line list.
                - Use set_cart_mode("collapsed") to minimize the cart to just a summary bar.
                - Use get_cart_mode() to check the current display mode.

                Be concise and friendly.
                """)
        ];

        Messages.Clear();
        _pendingApproval = null;
        IsApprovalPending = false;
        CartMode = CartMode.Normal;
        RefreshShoppingList();
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var text = InputText?.Trim();
        if (string.IsNullOrWhiteSpace(text) || IsBusy)
            return;

        InputText = string.Empty;
        IsBusy = true;

        AddMessage(ChatMessageKind.User, text);
        _history.Add(new ChatMessage(ChatRole.User, text));

        try
        {
            var options = new ChatOptions { Tools = [.. GardenShopTools.Default.Tools] };
            await SendAndProcessResponseAsync(options);
        }
        catch (Exception ex)
        {
            AddMessage(ChatMessageKind.Error, $"\u274c Error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            RefreshShoppingList();
        }
    }

    [RelayCommand]
    private async Task ApproveAsync() => await ResolveApprovalAsync(approved: true);

    [RelayCommand]
    private async Task RejectAsync() => await ResolveApprovalAsync(approved: false, reason: "User rejected");

    [RelayCommand]
    private async Task RunSuggestionAsync(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt) || IsBusy)
            return;
        InputText = prompt;
        await SendAsync();
    }

    [RelayCommand]
    private void CheckoutCart()
    {
        if (_currentCart.Items.Count == 0)
            return;

        var order = _archive.Checkout(_currentCart);

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
            CartMode.Compact => CartMode.Collapsed,
            CartMode.Collapsed => CartMode.Normal,
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
    private async Task ShowCartModalAsync()
    {
        // For narrow layout, navigate to a cart page (or just switch mode)
        CartMode = CartMode.Normal;
    }

    // ─── ViewModel-level AI tools ───────────────────────────────────
    // These demonstrate [ExportAIFunction] on a ViewModel — the AI can
    // directly drive UI navigation and view state, not just service calls.

    [ExportAIFunction("navigate_to_page",
        Description = "Navigate to a page in the app. Use 'catalog' to browse products, 'orders' to see past orders. Pages open as modal overlays.")]
    public async Task<string> NavigateToPageAsync(
        [System.ComponentModel.Description("The page to navigate to: 'catalog' or 'orders'")] string page)
    {
        var route = page?.ToLowerInvariant() switch
        {
            "catalog" => "catalog",
            "orders" => "orders",
            _ => throw new ArgumentException($"Unknown page '{page}'. Valid pages: 'catalog', 'orders'.")
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
        Description = "Change the shopping cart display mode. 'normal' shows full cards with emoji. 'compact' shows dense single-line rows. 'collapsed' minimizes to just a summary bar showing total and item count.")]
    public string SetCartViewMode(
        [System.ComponentModel.Description("The view mode: 'normal', 'compact', or 'collapsed'")] string mode)
    {
        CartMode = mode?.ToLowerInvariant() switch
        {
            "normal" => CartMode.Normal,
            "compact" => CartMode.Compact,
            "collapsed" => CartMode.Collapsed,
            _ => throw new ArgumentException($"Unknown mode '{mode}'. Valid modes: 'normal', 'compact', 'collapsed'.")
        };
        return $"Cart display mode set to {CartMode.ToString().ToLowerInvariant()}.";
    }

    [ExportAIFunction("get_cart_mode",
        Description = "Get the current cart display mode ('normal', 'compact', or 'collapsed').")]
    public string GetCartViewMode() => CartMode.ToString().ToLowerInvariant();

    // ─────────────────────────────────────────────────────────────────

    private void RefreshShoppingList()
    {
        var source = _currentCart.Items;
        SyncCollection(ShoppingList, source, v => v.Sku, i => i.Product.Sku, i => new ShoppingListItemViewModel(i));
        ShoppingListTotal = $"Total: {source.Sum(i => i.Subtotal):C}";
        OnPropertyChanged(nameof(CartSummary));
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

        // Remove items no longer in source (iterate backwards).
        for (int i = target.Count - 1; i >= 0; i--)
        {
            if (!sourceKeys.Contains(vmKey(target[i])))
                target.RemoveAt(i);
        }

        // Add new items that aren't already in the target.
        var existing = new HashSet<string>(target.Select(vmKey));
        foreach (var item in source)
        {
            if (!existing.Contains(modelKey(item)))
                target.Add(create(item));
        }
    }

    private void RefreshAvailableTools()
    {
        AvailableTools.Clear();
        var tools = GardenShopTools.Default.Tools;
        foreach (var tool in tools.OrderBy(t => t.Name))
            AvailableTools.Add(new ToolInfoViewModel(tool.Name, tool.Description ?? ""));
    }

    private async Task SendAndProcessResponseAsync(ChatOptions options)
    {
        var responseText = string.Empty;
        ChatMessageViewModel? assistantMessage = null;
        var updates = new List<ChatResponseUpdate>();

        await foreach (var update in _chatClient.GetStreamingResponseAsync(_history, options, _cts.Token))
        {
            updates.Add(update);

            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case ToolApprovalRequestContent approval:
                    {
                        var toolName = approval.ToolCall is FunctionCallContent fcc ? fcc.Name : "unknown";
                        var args = approval.ToolCall is FunctionCallContent fc && fc.Arguments is not null
                            ? string.Join(", ", fc.Arguments.Select(kv => $"{kv.Key}: {kv.Value}"))
                            : "";
                        AddMessage(ChatMessageKind.Tool, $"\u26a0\ufe0f Approval required: {toolName}({args})");
                        _pendingApproval = approval;
                        break;
                    }

                    case FunctionCallContent call:
                        AddMessage(ChatMessageKind.Tool, $"🔧 {call.Name}");
                        break;

                    case FunctionResultContent:
                        // Result details are noisy (serialized JSON / type names).
                        // The assistant's text reply already summarises the outcome.
                        break;

                    case TextContent tc when tc.Text is not null:
                        responseText += tc.Text;
                        if (assistantMessage is null)
                            assistantMessage = AddMessage(ChatMessageKind.Assistant, responseText);
                        else
                            assistantMessage.Text = responseText;
                        break;
                }
            }
        }

        _history.AddMessages(updates);

        if (_pendingApproval is not null)
        {
            var name = _pendingApproval.ToolCall is FunctionCallContent fc2 ? fc2.Name?.TrimEnd('(', ')') : "tool";
            ApprovalText = $"🔒 {name} — approve?";
            IsApprovalPending = true;
            return;
        }

        if (assistantMessage is null && string.IsNullOrEmpty(responseText))
            AddMessage(ChatMessageKind.Assistant, "(no response)");
    }

    private async Task ResolveApprovalAsync(bool approved, string? reason = null)
    {
        if (_pendingApproval is null)
            return;

        var approval = _pendingApproval;
        _pendingApproval = null;
        IsApprovalPending = false;
        IsBusy = true;

        try
        {
            var response = approval.CreateResponse(approved, reason);
            _history.Add(new ChatMessage(ChatRole.User, [response]));
            AddMessage(ChatMessageKind.Tool, approved ? "\u2705 Approved" : "\u274c Rejected");

            var options = new ChatOptions { Tools = [.. GardenShopTools.Default.Tools] };
            await SendAndProcessResponseAsync(options);
        }
        catch (Exception ex)
        {
            AddMessage(ChatMessageKind.Error, $"\u274c Error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            RefreshShoppingList();
            RefreshArchive();
        }
    }

    private ChatMessageViewModel AddMessage(ChatMessageKind kind, string text)
    {
        var vm = new ChatMessageViewModel(kind, text);
        Messages.Add(vm);
        MessageAdded?.Invoke(vm);
        return vm;
    }
}
