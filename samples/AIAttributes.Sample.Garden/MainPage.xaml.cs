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

    public MainPage(IServiceProvider rootProvider, IChatClient innerChatClient)
    {
        _rootProvider = rootProvider;
        _innerChatClient = innerChatClient;
        InitializeComponent();
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

        // FunctionInvokingChatClient is built per session so the session
        // scope's IServiceProvider is passed into AIFunctionArguments.Services
        // for every tool invocation. This lets scoped services (GardenService)
        // resolve from the session scope instead of the root container.
        _sessionClient = new ChatClientBuilder(_innerChatClient)
            .UseFunctionInvocation()
            .Build(_sessionScope.ServiceProvider);

        MessagesStack.Children.Clear();
        AddSystemMessage("🌱 New chat session — garden cleared");
        RefreshGardenPanel();
    }

    /// <summary>Gets the scoped GardenService for the current session.</summary>
    private GardenService Garden => _sessionScope!.ServiceProvider.GetRequiredService<GardenService>();

    /// <summary>
    /// Rebinds the side-panel CollectionView to the current garden contents.
    /// Called after every chat turn so mutating tool calls are visible.
    /// </summary>
    private void RefreshGardenPanel()
    {
        GardenList.ItemsSource = Garden.ListMyGarden();
    }

    private void OnNewChatClicked(object? sender, EventArgs e) => StartNewSession();

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        var text = ChatInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text) || _isBusy || _sessionClient is null)
            return;

        ChatInput.Text = string.Empty;
        SetBusy(true);

        AddUserMessage(text);
        _history.Add(new ChatMessage(ChatRole.User, text));

        try
        {
            // Tools come from DI (registered via AddAITools<GardenTools>()).
            // Resolved from the session scope so scoped services bind correctly.
            var tools = _sessionScope!.ServiceProvider.GetServices<AITool>();
            var options = new ChatOptions { Tools = [.. tools] };

            await SendAndProcessResponseAsync(options);
        }
        catch (Exception ex)
        {
            AddErrorMessage(ex.Message);
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
        Label? responseLabel = null;
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
                        AddToolMessage($"⚠️ Approval required: {toolName}({args})");
                        _pendingApproval = approval;
                        break;

                    case FunctionCallContent call:
                        AddToolMessage($"🔧 Calling: {call.Name}");
                        break;

                    case FunctionResultContent result:
                        var resultText = result.Result?.ToString() ?? "(no result)";
                        if (resultText.Length > 200)
                            resultText = resultText[..200] + "...";
                        AddToolMessage($"✅ Result: {resultText}");
                        break;

                    case TextContent tc when tc.Text is not null:
                        responseText += tc.Text;
                        if (responseLabel is null)
                            responseLabel = AddAssistantMessage(responseText);
                        else
                            responseLabel.Text = responseText;
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

        if (responseLabel is null && string.IsNullOrEmpty(responseText))
            AddAssistantMessage("(no response)");
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
            AddToolMessage(approved ? "✅ Approved" : "❌ Rejected");

            var tools = _sessionScope!.ServiceProvider.GetServices<AITool>();
            var options = new ChatOptions { Tools = [.. tools] };
            await SendAndProcessResponseAsync(options);
        }
        catch (Exception ex)
        {
            AddErrorMessage(ex.Message);
        }
        finally
        {
            SetBusy(false);
            RefreshGardenPanel();
        }
    }

    private void AddUserMessage(string text)
    {
        var frame = new Border
        {
            BackgroundColor = Color.FromArgb("#DCF8C6"),
            Padding = new Thickness(12, 8),
            HorizontalOptions = LayoutOptions.End,
            MaximumWidthRequest = 300,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            StrokeThickness = 0,
            Content = new Label
            {
                Text = text,
                FontSize = 14,
                TextColor = Colors.Black,
            }
        };
        MessagesStack.Children.Add(frame);
        ScrollToBottom();
    }

    private Label AddAssistantMessage(string text)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 14,
        };
        var frame = new Border
        {
            BackgroundColor = Color.FromArgb("#F0F0F0"),
            Padding = new Thickness(12, 8),
            HorizontalOptions = LayoutOptions.Start,
            MaximumWidthRequest = 300,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            StrokeThickness = 0,
            Content = label,
        };
        MessagesStack.Children.Add(frame);
        ScrollToBottom();
        return label;
    }

    private void AddToolMessage(string text)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 12,
            TextColor = Colors.Gray,
            FontAttributes = FontAttributes.Italic,
            Padding = new Thickness(8, 2),
        };
        MessagesStack.Children.Add(label);
        ScrollToBottom();
    }

    private void AddSystemMessage(string text)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 12,
            TextColor = Color.FromArgb("#5B8C5A"),
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            Padding = new Thickness(8, 4),
        };
        MessagesStack.Children.Add(label);
        ScrollToBottom();
    }

    private void AddErrorMessage(string text)
    {
        var label = new Label
        {
            Text = $"❌ Error: {text}",
            FontSize = 12,
            TextColor = Colors.Red,
            Padding = new Thickness(8, 2),
        };
        MessagesStack.Children.Add(label);
        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(50), async () =>
        {
            await ChatScrollView.ScrollToAsync(0, ChatScrollView.ContentSize.Height, true);
        });
    }
}
