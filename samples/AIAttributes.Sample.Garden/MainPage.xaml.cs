using System.Collections.ObjectModel;
using AIAttributes.Sample.Garden.Models;
using AIAttributes.Sample.Garden.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AIAttributes.Sample.Garden;

public partial class MainPage : ContentPage
{
    private readonly IServiceProvider _rootProvider;
    private readonly IChatClient _innerChatClient;
    private IServiceScope? _sessionScope;
    private IChatClient? _sessionClient;
    private List<ChatMessage> _history = [];
    private bool _isBusy;
    private ToolApprovalRequestContent? _pendingApproval;

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    public ObservableCollection<ToolInfoViewModel> AvailableTools { get; } = [];

    public MainPage(IServiceProvider rootProvider, IChatClient innerChatClient)
    {
        _rootProvider = rootProvider;
        _innerChatClient = innerChatClient;
        InitializeComponent();
        BindingContext = this;
        StartNewSession();
    }

    /// <summary>
    /// Creates a new DI scope, a new chat history, and a fresh chat client.
    /// The scoped <see cref="GardenService"/> is recreated too, which is why
    /// the "Your Garden" side panel clears on New Chat.
    /// </summary>
    private void StartNewSession()
    {
        _sessionScope?.Dispose();
        _sessionScope = _rootProvider.CreateScope();
        _history =
        [
            new(ChatRole.System,
                "You are a helpful gardening assistant. Help users browse plants, manage their garden, and get care advice. Be concise and friendly.")
        ];

        _sessionClient = new ChatClientBuilder(_innerChatClient)
            .UseFunctionInvocation()
            .Build(_sessionScope.ServiceProvider);

        Messages.Clear();
        RefreshAvailableTools();
        RefreshGardenPanel();
    }

    private GardenService Garden => _sessionScope!.ServiceProvider.GetRequiredService<GardenService>();

    private void RefreshGardenPanel()
    {
        GardenList.ItemsSource = Garden.ListMyGarden();
    }

    /// <summary>
    /// Rebuilds <see cref="AvailableTools"/> from the DI container so the
    /// empty-state view always reflects what <c>AddAITools&lt;T&gt;()</c>
    /// registered for this session scope.
    /// </summary>
    private void RefreshAvailableTools()
    {
        AvailableTools.Clear();
        var tools = _sessionScope!.ServiceProvider.GetServices<AITool>();
        foreach (var tool in tools.OrderBy(t => t.Name))
            AvailableTools.Add(new ToolInfoViewModel(tool.Name, tool.Description ?? ""));
    }

    private void OnNewChatClicked(object? sender, EventArgs e) => StartNewSession();

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        var text = ChatInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text) || _isBusy || _sessionClient is null)
            return;

        ChatInput.Text = string.Empty;
        SetBusy(true);

        AddMessage(ChatMessageKind.User, text);
        _history.Add(new ChatMessage(ChatRole.User, text));

        try
        {
            var tools = _sessionScope!.ServiceProvider.GetServices<AITool>();
            var options = new ChatOptions { Tools = [.. tools] };

            await SendAndProcessResponseAsync(options);
        }
        catch (Exception ex)
        {
            AddMessage(ChatMessageKind.Error, $"❌ Error: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
            RefreshGardenPanel();
        }
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        ChatInput.IsEnabled = !busy;
    }

    private async Task SendAndProcessResponseAsync(ChatOptions options)
    {
        var responseText = string.Empty;
        ChatMessageViewModel? assistantMessage = null;
        var updates = new List<ChatResponseUpdate>();

        await foreach (var update in _sessionClient!.GetStreamingResponseAsync(_history, options))
        {
            updates.Add(update);

            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case ToolApprovalRequestContent approval:
                        var toolName = approval.ToolCall is FunctionCallContent fcc ? fcc.Name : "unknown";
                        var args = approval.ToolCall is FunctionCallContent fc && fc.Arguments is not null
                            ? string.Join(", ", fc.Arguments.Select(kv => $"{kv.Key}: {kv.Value}"))
                            : "";
                        AddMessage(ChatMessageKind.Tool, $"⚠️ Approval required: {toolName}({args})");
                        _pendingApproval = approval;
                        break;

                    case FunctionCallContent call:
                        AddMessage(ChatMessageKind.Tool, $"🔧 Calling: {call.Name}");
                        break;

                    case FunctionResultContent result:
                        var resultText = result.Result?.ToString() ?? "(no result)";
                        if (resultText.Length > 200)
                            resultText = resultText[..200] + "...";
                        AddMessage(ChatMessageKind.Tool, $"✅ Result: {resultText}");
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
            var name = _pendingApproval.ToolCall is FunctionCallContent fc2 ? fc2.Name : "tool";
            ShowApprovalUI(name);
            return;
        }

        if (assistantMessage is null && string.IsNullOrEmpty(responseText))
            AddMessage(ChatMessageKind.Assistant, "(no response)");
    }

    private void ShowApprovalUI(string toolName)
    {
        ApprovalLabel.Text = $"🔒 {toolName} — approve?";
        InputArea.IsVisible = false;
        ApprovalArea.IsVisible = true;
    }

    private void HideApprovalUI()
    {
        ApprovalArea.IsVisible = false;
        InputArea.IsVisible = true;
        _pendingApproval = null;
    }

    private async void OnApproveClicked(object? sender, EventArgs e) =>
        await ResolveApprovalAsync(approved: true);

    private async void OnRejectClicked(object? sender, EventArgs e) =>
        await ResolveApprovalAsync(approved: false, reason: "User rejected");

    private async Task ResolveApprovalAsync(bool approved, string? reason = null)
    {
        if (_pendingApproval is null || _sessionClient is null)
            return;

        var approval = _pendingApproval;
        HideApprovalUI();
        SetBusy(true);

        try
        {
            var response = approval.CreateResponse(approved, reason);
            _history.Add(new ChatMessage(ChatRole.User, [response]));
            AddMessage(ChatMessageKind.Tool, approved ? "✅ Approved" : "❌ Rejected");

            var tools = _sessionScope!.ServiceProvider.GetServices<AITool>();
            var options = new ChatOptions { Tools = [.. tools] };
            await SendAndProcessResponseAsync(options);
        }
        catch (Exception ex)
        {
            AddMessage(ChatMessageKind.Error, $"❌ Error: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
            RefreshGardenPanel();
        }
    }

    private ChatMessageViewModel AddMessage(ChatMessageKind kind, string text)
    {
        var vm = new ChatMessageViewModel(kind, text);
        Messages.Add(vm);
        ScrollToBottom(vm);
        return vm;
    }

    private void ScrollToBottom(ChatMessageViewModel item)
    {
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(50), () =>
        {
            try { MessagesView.ScrollTo(item, position: ScrollToPosition.End, animate: true); }
            catch { /* item may have been removed */ }
        });
    }
}
