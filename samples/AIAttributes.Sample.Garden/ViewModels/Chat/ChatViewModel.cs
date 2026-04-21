using System.Collections.ObjectModel;
using AIAttributes.Sample.Garden.Messages;
using AIAttributes.Sample.Garden.Models;
using AIAttributes.Sample.Garden.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.AI;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden.ViewModels;

/// <summary>
/// Owns the AI chat loop, message history, tool invocation, and approval flow.
/// Designed to be reusable — any page can host a ChatView bound to this VM.
/// </summary>
public sealed partial class ChatViewModel : ObservableObject, IRecipient<StartNewChatSessionMessage>
{
    /// <summary>
    /// Source-generated tool context that merges all tool sources into one.
    /// Demonstrates three distinct attribute patterns:
    /// <list type="bullet">
    ///   <item><b>Static class</b> — ProductCatalog: tools on a plain static class.</item>
    ///   <item><b>Instance class</b> — CurrentCart: tools on a DI-registered instance.</item>
    ///   <item><b>Interface</b> — IOrderArchive: tools declared on the interface.</item>
    /// </list>
    /// </summary>
    [AIToolSource(typeof(ProductCatalog))]
    [AIToolSource(typeof(CurrentCart))]
    [AIToolSource(typeof(IOrderArchive))]
    [AIToolSource(typeof(MainViewModel))]
    [AIToolSource(typeof(CartViewModel))]
    private partial class GardenShopTools : AIToolContext { }

    private readonly IChatClient _chatClient;
    private List<ChatMessage> _history = [];
    private ToolApprovalRequestContent? _pendingApproval;
    private CancellationTokenSource _cts = new();

    public ChatViewModel(IServiceProvider rootProvider, IChatClient innerChatClient)
    {
        _chatClient = new ChatClientBuilder(innerChatClient)
            .UseFunctionInvocation()
            .Build(rootProvider);

        WeakReferenceMessenger.Default.Register(this);

        RefreshAvailableTools();
    }

    void IRecipient<StartNewChatSessionMessage>.Receive(StartNewChatSessionMessage message)
        => StartNewSession();

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    public ObservableCollection<ToolInfoViewModel> AvailableTools { get; } = [];

    public IReadOnlyList<string> SuggestionPrompts { get; } =
    [
        "Add 5 packs of tomato seeds and a trowel",
        "Show compact cart",
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

    public void StartNewSession()
    {
        try { _cts.Cancel(); } catch { /* best effort */ }
        _cts.Dispose();
        _cts = new CancellationTokenSource();

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
                - Use get_cart_mode() to check the current display mode.

                Be concise and friendly.
                """)
        ];

        Messages.Clear();
        _pendingApproval = null;
        IsApprovalPending = false;
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
            AddMessage(ChatMessageKind.Error, $"Error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            WeakReferenceMessenger.Default.Send(new ChatTurnCompletedMessage());
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
                        AddMessage(ChatMessageKind.Tool, $"Approval required: {toolName}({args})", FluentIcons.LockClosed);
                        _pendingApproval = approval;
                        break;
                    }

                    case FunctionCallContent call:
                        AddMessage(ChatMessageKind.Tool, call.Name, FluentIcons.Wrench);
                        break;

                    case FunctionResultContent:
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
            ApprovalText = $"{name} — approve?";
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
            AddMessage(ChatMessageKind.Tool, approved ? "Approved" : "Rejected", approved ? FluentIcons.Checkmark : FluentIcons.Dismiss);

            var options = new ChatOptions { Tools = [.. GardenShopTools.Default.Tools] };
            await SendAndProcessResponseAsync(options);
        }
        catch (Exception ex)
        {
            AddMessage(ChatMessageKind.Error, $"Error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            WeakReferenceMessenger.Default.Send(new ChatTurnCompletedMessage());
        }
    }

    private ChatMessageViewModel AddMessage(ChatMessageKind kind, string text, string? icon = null)
    {
        var vm = new ChatMessageViewModel(kind, text, icon);
        Messages.Add(vm);
        WeakReferenceMessenger.Default.Send(new ChatMessageAddedMessage(vm));
        return vm;
    }

    private void RefreshAvailableTools()
    {
        AvailableTools.Clear();
        var tools = GardenShopTools.Default.Tools;
        foreach (var tool in tools.OrderBy(t => t.Name))
            AvailableTools.Add(new ToolInfoViewModel(tool.Name, tool.Description ?? ""));
    }
}
