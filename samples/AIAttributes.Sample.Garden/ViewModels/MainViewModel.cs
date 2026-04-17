using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AIAttributes.Sample.Garden.Models;
using AIAttributes.Sample.Garden.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AIAttributes.Sample.Garden.ViewModels;

/// <summary>
/// Top-level view model bound to <see cref="MainPage"/> and its child views.
/// Owns the chat loop, DI session scope, and mutable UI state.
/// </summary>
public sealed class MainViewModel(IServiceProvider rootProvider, IChatClient innerChatClient) : INotifyPropertyChanged
{
    private readonly IServiceProvider _rootProvider = rootProvider;
    private readonly IChatClient _innerChatClient = innerChatClient;

    private IServiceScope? _sessionScope;
    private IChatClient? _sessionClient;
    private List<ChatMessage> _history = [];
    private ToolApprovalRequestContent? _pendingApproval;

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];
    public ObservableCollection<ToolInfoViewModel> AvailableTools { get; } = [];
    public ObservableCollection<PlantEntry> GardenPlants { get; } = [];

    /// <summary>
    /// Seed prompts shown as one-tap chips in the empty view so the user
    /// can immediately exercise the registered tools.
    /// </summary>
    public IReadOnlyList<string> SuggestionPrompts { get; } =
    [
        "Add basil to my kitchen windowsill",
        "What plants are easy to grow indoors?",
        "Water everything in my garden",
        "Give me a care guide for tomatoes",
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

    public ICommand NewChatCommand => new Command(StartNewSession);
    public ICommand SendCommand => new Command(async () => await SendAsync());
    public ICommand ApproveCommand => new Command(async () => await ResolveApprovalAsync(approved: true));
    public ICommand RejectCommand => new Command(async () => await ResolveApprovalAsync(approved: false, reason: "User rejected"));

    /// <summary>
    /// Fills the input with a suggestion and sends it immediately.
    /// </summary>
    public ICommand RunSuggestionCommand => new Command<string>(async prompt =>
    {
        if (string.IsNullOrWhiteSpace(prompt) || IsBusy)
            return;
        InputText = prompt;
        await SendAsync();
    });

    /// <summary>Raised whenever a new message is appended, so views can scroll.</summary>
    public event Action<ChatMessageViewModel>? MessageAdded;

    public void Initialize() => StartNewSession();

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
                """
                You are a helpful gardening assistant. Help users browse plants, 
                manage their garden, and get care advice. Be concise and friendly.
                """)
        ];

        // FunctionInvokingChatClient is built per session so the session
        // scope's IServiceProvider is passed into AIFunctionArguments.Services
        // for every tool invocation.
        _sessionClient = new ChatClientBuilder(_innerChatClient)
            .UseFunctionInvocation()
            .Build(_sessionScope.ServiceProvider);

        Messages.Clear();
        _pendingApproval = null;
        IsApprovalPending = false;

        RefreshAvailableTools();
        RefreshGardenPanel();
    }

    private GardenService Garden => _sessionScope!.ServiceProvider.GetRequiredService<GardenService>();

    private void RefreshGardenPanel()
    {
        GardenPlants.Clear();
        foreach (var plant in Garden.ListMyGarden())
            GardenPlants.Add(plant);
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

    private async Task SendAsync()
    {
        var text = InputText?.Trim();
        if (string.IsNullOrWhiteSpace(text) || IsBusy || _sessionClient is null)
            return;

        InputText = string.Empty;
        IsBusy = true;

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
            AddMessage(ChatMessageKind.Error, $"\u274c Error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            RefreshGardenPanel();
        }
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
                        AddMessage(ChatMessageKind.Tool, $"\u26a0\ufe0f Approval required: {toolName}({args})");
                        _pendingApproval = approval;
                        break;

                    case FunctionCallContent call:
                        AddMessage(ChatMessageKind.Tool, $"\ud83d\udd27 Calling: {call.Name}");
                        break;

                    case FunctionResultContent result:
                        var resultText = result.Result?.ToString() ?? "(no result)";
                        if (resultText.Length > 200)
                            resultText = resultText[..200] + "...";
                        AddMessage(ChatMessageKind.Tool, $"\u2705 Result: {resultText}");
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
            ApprovalText = $"\ud83d\udd12 {name} \u2014 approve?";
            IsApprovalPending = true;
            return;
        }

        if (assistantMessage is null && string.IsNullOrEmpty(responseText))
            AddMessage(ChatMessageKind.Assistant, "(no response)");
    }

    private async Task ResolveApprovalAsync(bool approved, string? reason = null)
    {
        if (_pendingApproval is null || _sessionClient is null)
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

            var tools = _sessionScope!.ServiceProvider.GetServices<AITool>();
            var options = new ChatOptions { Tools = [.. tools] };
            await SendAndProcessResponseAsync(options);
        }
        catch (Exception ex)
        {
            AddMessage(ChatMessageKind.Error, $"\u274c Error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            RefreshGardenPanel();
        }
    }

    private ChatMessageViewModel AddMessage(ChatMessageKind kind, string text)
    {
        var vm = new ChatMessageViewModel(kind, text);
        Messages.Add(vm);
        MessageAdded?.Invoke(vm);
        return vm;
    }

    // ── INotifyPropertyChanged plumbing ─────────────────────────────
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
