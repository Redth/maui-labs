using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AIAttributes.Sample.Garden.Models;
using AIAttributes.Sample.Garden.Services;
using Microsoft.Extensions.AI;

namespace AIAttributes.Sample.Garden.ViewModels;

/// <summary>
/// Top-level view model bound to <see cref="MainPage"/>. Owns the chat loop,
/// the per-session <see cref="ChatSession"/>, and projects the singleton
/// <see cref="OrderArchive"/> into the UI.
/// </summary>
/// <remarks>
/// No <c>IServiceScope</c> anywhere. Per-session state is a plain object
/// owned by this view model and published to AI tools via
/// <see cref="ICurrentSession"/>.
/// </remarks>
public sealed class MainViewModel(
    IServiceProvider rootProvider,
    IChatClient innerChatClient,
    ChatSessionFactory sessionFactory,
    ICurrentSession currentSession,
    OrderArchive archive) : INotifyPropertyChanged
{
    private readonly IChatClient _chatClient = new ChatClientBuilder(innerChatClient)
        .UseFunctionInvocation()
        .Build(rootProvider);

    private List<ChatMessage> _history = [];
    private ToolApprovalRequestContent? _pendingApproval;

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];
    public ObservableCollection<ToolInfoViewModel> AvailableTools { get; } = [];
    public ObservableCollection<CategoryGroup> ShoppingList { get; } = [];
    public ObservableCollection<OrderViewModel> PastOrders { get; } = [];
    public ObservableCollection<DraftViewModel> Drafts { get; } = [];

    /// <summary>
    /// Seed prompts shown as one-tap chips. Each prompt is fully self-contained
    /// so the assistant can execute the tool call without a follow-up question.
    /// </summary>
    public IReadOnlyList<string> SuggestionPrompts { get; } =
    [
        "Add 5 packs of tomato seeds and a hand trowel to my list",
        "Show me my list",
        "Check out my list",
        "List my past orders",
        "Re-order my last order",
    ];

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set => Set(ref _isBusy, value, nameof(IsBusy), nameof(IsNotBusy));
    }
    public bool IsNotBusy => !IsBusy;

    private string? _inputText;
    public string? InputText
    {
        get => _inputText;
        set => Set(ref _inputText, value);
    }

    private bool _isApprovalPending;
    public bool IsApprovalPending
    {
        get => _isApprovalPending;
        private set => Set(ref _isApprovalPending, value, nameof(IsApprovalPending), nameof(IsInputVisible));
    }
    public bool IsInputVisible => !IsApprovalPending;

    private string _approvalText = "";
    public string ApprovalText
    {
        get => _approvalText;
        private set => Set(ref _approvalText, value);
    }

    private string _shoppingListTotal = "$0.00";
    public string ShoppingListTotal
    {
        get => _shoppingListTotal;
        private set => Set(ref _shoppingListTotal, value);
    }

    public ICommand NewChatCommand => new Command(StartNewSession);
    public ICommand SendCommand => new Command(async () => await SendAsync());
    public ICommand ApproveCommand => new Command(async () => await ResolveApprovalAsync(approved: true));
    public ICommand RejectCommand => new Command(async () => await ResolveApprovalAsync(approved: false, reason: "User rejected"));

    public ICommand RunSuggestionCommand => new Command<string>(async prompt =>
    {
        if (string.IsNullOrWhiteSpace(prompt) || IsBusy)
            return;
        InputText = prompt;
        await SendAsync();
    });

    /// <summary>Raised whenever a new message is appended, so views can scroll.</summary>
    public event Action<ChatMessageViewModel>? MessageAdded;

    public void Initialize()
    {
        archive.Changed += RefreshArchive;
        StartNewSession();
        RefreshAvailableTools();
        RefreshArchive();
    }

    /// <summary>
    /// "New Chat": save the current shopping list as a draft (if any),
    /// cancel any in-flight tool calls, then publish a fresh
    /// <see cref="ChatSession"/>. No DI scope manipulation involved.
    /// </summary>
    private void StartNewSession()
    {
        var previous = currentSession.Session;
        var pendingItems = previous.Snapshot();
        if (pendingItems.Count > 0)
            archive.SaveDraft(pendingItems);

        try { previous.Cts.Cancel(); } catch { /* best effort */ }

        var fresh = sessionFactory.Create();
        fresh.ListChanged += RefreshShoppingList;
        currentSession.Set(fresh);

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

    private void RefreshShoppingList()
    {
        ShoppingList.Clear();
        var items = currentSession.Session.Snapshot();
        var groups = items
            .GroupBy(i => i.Product.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var g in groups)
            ShoppingList.Add(new CategoryGroup(g.Key, g.Select(i => new ShoppingListItemViewModel(i))));
        ShoppingListTotal = items.Sum(i => i.Subtotal).ToString("C");
    }

    private void RefreshArchive()
    {
        PastOrders.Clear();
        foreach (var o in archive.Orders)
            PastOrders.Add(new OrderViewModel(o));
        Drafts.Clear();
        foreach (var d in archive.Drafts)
            Drafts.Add(new DraftViewModel(d));
    }

    private IEnumerable<ShoppingListItemViewModel> AllListItems =>
        ShoppingList.SelectMany(g => g);

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
        var tools = GardenShopTools.Default.GetTools();
        foreach (var tool in tools.OrderBy(t => t.Name))
            AvailableTools.Add(new ToolInfoViewModel(tool.Name, tool.Description ?? ""));
    }

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
            var options = new ChatOptions { Tools = [.. GardenShopTools.Default.GetTools()] };
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

    private async Task SendAndProcessResponseAsync(ChatOptions options)
    {
        var responseText = string.Empty;
        ChatMessageViewModel? assistantMessage = null;
        var updates = new List<ChatResponseUpdate>();

        await foreach (var update in _chatClient.GetStreamingResponseAsync(_history, options, currentSession.Session.Cts.Token))
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
                        AddMessage(ChatMessageKind.Tool, $"\ud83d\udd27 Calling: {call.Name}");
                        break;

                    case FunctionResultContent result:
                    {
                        var resultText = result.Result?.ToString() ?? "(no result)";
                        if (resultText.Length > 200)
                            resultText = resultText[..200] + "...";
                        AddMessage(ChatMessageKind.Tool, $"\u2705 Result: {resultText}");
                        break;
                    }

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
            var name = _pendingApproval.ToolCall is FunctionCallContent fc2 ? fc2.Name : "tool";
            ApprovalText = $"\ud83d\udd12 {name} \u2014 approve?";
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

            var options = new ChatOptions { Tools = [.. GardenShopTools.Default.GetTools()] };
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

    private ChatMessageViewModel AddMessage(ChatMessageKind kind, string text)
    {
        var vm = new ChatMessageViewModel(kind, text);
        Messages.Add(vm);
        MessageAdded?.Invoke(vm);
        return vm;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null, params string[] alsoNotify)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        foreach (var other in alsoNotify)
            if (other != name)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(other));
    }
}
