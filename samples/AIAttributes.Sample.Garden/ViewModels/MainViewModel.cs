using System.Collections.ObjectModel;
using AIAttributes.Sample.Garden.Models;
using AIAttributes.Sample.Garden.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.AI;

namespace AIAttributes.Sample.Garden.ViewModels;

/// <summary>
/// Top-level view model bound to <see cref="MainPage"/>. Owns the chat loop,
/// the per-session <see cref="Cart"/>, and projects the singleton
/// <see cref="OrderArchive"/> into the UI.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IChatClient _chatClient;
    private readonly CurrentCart _currentCart;
    private readonly OrderArchive _archive;

    private List<ChatMessage> _history = [];
    private ToolApprovalRequestContent? _pendingApproval;
    private bool _initialized;

    public MainViewModel(
        IServiceProvider rootProvider,
        IChatClient innerChatClient,
        CurrentCart currentCart,
        OrderArchive archive)
    {
        _chatClient = new ChatClientBuilder(innerChatClient)
            .UseFunctionInvocation()
            .Build(rootProvider);
        _currentCart = currentCart;
        _archive = archive;
    }

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];
    public ObservableCollection<ToolInfoViewModel> AvailableTools { get; } = [];
    public ObservableCollection<ShoppingListItemViewModel> ShoppingList { get; } = [];
    public ObservableCollection<OrderViewModel> PastOrders { get; } = [];

    public IReadOnlyList<string> SuggestionPrompts { get; } =
    [
        "Add 5 packs of tomato seeds and a hand trowel to my list",
        "Show me my list",
        "Check out my list",
        "List my past orders",
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
        var previous = _currentCart.Cart;
        try { previous.Cts.Cancel(); } catch { /* best effort */ }
        previous.Cts.Dispose();

        var fresh = new Cart($"cart-{Guid.NewGuid():N}");
        _currentCart.Set(fresh);

        _history =
        [
            new(ChatRole.System,
                """
                You are a helpful garden-shop assistant. Help the user browse seeds, soil,
                tools, and equipment, manage their shopping list, and review past orders.
                Use search_products to discover items by name or category. When the user
                says "check out" call checkout_list and let the approval flow run. Be
                concise and friendly.
                """)
        ];

        Messages.Clear();
        _pendingApproval = null;
        IsApprovalPending = false;
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
            RefreshArchive();
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

    private void RefreshShoppingList()
    {
        ShoppingList.Clear();
        var items = _currentCart.Cart.Snapshot();
        foreach (var item in items)
            ShoppingList.Add(new ShoppingListItemViewModel(item));
        ShoppingListTotal = $"Total: {items.Sum(i => i.Subtotal):C}";
    }

    private void RefreshArchive()
    {
        PastOrders.Clear();
        foreach (var o in _archive.Orders)
            PastOrders.Add(new OrderViewModel(o));
    }

    private IEnumerable<ShoppingListItemViewModel> AllListItems => ShoppingList;

    /// <summary>
    /// Marks list items affected by an in-flight approval so the panel can ghost them.
    /// </summary>
    private void MarkPendingFromApproval(ToolApprovalRequestContent approval)
    {
        if (approval.ToolCall is not FunctionCallContent fcc)
            return;

        switch (fcc.Name)
        {
            case "checkout_list":
                foreach (var i in AllListItems) i.Pending = PendingAction.Checkout;
                break;
            case "cancel_list":
                foreach (var i in AllListItems) i.Pending = PendingAction.Cancel;
                break;
            case "remove_from_list" when fcc.Arguments?.TryGetValue("skuOrName", out var raw) == true:
            {
                var query = raw?.ToString();
                if (string.IsNullOrWhiteSpace(query))
                    return;
                var product = ProductCatalog.FindByName(query!);
                if (product is null)
                    return;
                foreach (var i in AllListItems)
                    i.Pending = string.Equals(i.Sku, product.Sku, StringComparison.OrdinalIgnoreCase)
                        ? PendingAction.Remove
                        : PendingAction.None;
                break;
            }
        }
    }

    private void ClearPending()
    {
        foreach (var i in AllListItems)
            i.Pending = PendingAction.None;
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

        await foreach (var update in _chatClient.GetStreamingResponseAsync(_history, options, _currentCart.Cart.Cts.Token))
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
            MarkPendingFromApproval(_pendingApproval);
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
        ClearPending();
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
