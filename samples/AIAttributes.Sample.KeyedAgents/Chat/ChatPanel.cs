using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AIAttributes.Sample.KeyedAgents.Chat;

/// <summary>
/// Reusable chat UI for the Browse and Manage pages.
/// The hosting page supplies tools via <see cref="Tools"/> and a chat client
/// wrapped with <c>UseFunctionInvocation</c>; this control handles history,
/// streaming, and rendering.
/// </summary>
public class ChatPanel : ContentView
{
    private readonly IChatClient _chatClient;
    private List<ChatMessage> _history = [];
    private bool _isBusy;

    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly VerticalStackLayout _messagesStack;
    private readonly ScrollView _chatScrollView;
    private readonly Entry _chatInput;
    private readonly Button _sendButton;

    public string Title
    {
        get => _titleLabel.Text ?? string.Empty;
        set => _titleLabel.Text = value;
    }

    public string Subtitle
    {
        get => _subtitleLabel.Text ?? string.Empty;
        set => _subtitleLabel.Text = value;
    }

    public string SystemPrompt { get; set; } = "You are a helpful assistant.";

    public IReadOnlyList<AITool> Tools { get; set; } = [];

    public ChatPanel(IChatClient chatClient, IServiceProvider services)
    {
        _chatClient = new ChatClientBuilder(chatClient)
            .UseFunctionInvocation()
            .Build(services);

        _titleLabel = new Label { FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Colors.White };
        _subtitleLabel = new Label { FontSize = 11, TextColor = Color.FromArgb("#DDEFDD") };
        _messagesStack = new VerticalStackLayout { Spacing = 8 };
        _chatScrollView = new ScrollView { Padding = new Thickness(12, 8), Content = _messagesStack };
        _chatInput = new Entry
        {
            Placeholder = "Ask something...",
            ReturnType = ReturnType.Send,
            FontSize = 14,
        };
        _chatInput.Completed += OnSendClicked;

        _sendButton = new Button
        {
            Text = "Send",
            BackgroundColor = Color.FromArgb("#5B8C5A"),
            TextColor = Colors.White,
            FontSize = 14,
            CornerRadius = 8,
            Padding = new Thickness(16, 8),
        };
        _sendButton.Clicked += OnSendClicked;

        var header = new Border
        {
            BackgroundColor = Color.FromArgb("#5B8C5A"),
            Padding = new Thickness(16, 12),
            StrokeThickness = 0,
            Content = new VerticalStackLayout { Children = { _titleLabel, _subtitleLabel } },
        };

        var input = new Grid { Padding = new Thickness(12, 8, 12, 12), ColumnSpacing = 8 };
        input.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        input.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        input.Add(_chatInput, 0);
        input.Add(_sendButton, 1);

        var root = new Grid { RowSpacing = 0 };
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.Add(header, 0, 0);
        root.Add(_chatScrollView, 0, 1);
        root.Add(input, 0, 2);

        Content = root;

        _history = [new ChatMessage(ChatRole.System, SystemPrompt)];
    }

    public void Reset()
    {
        _history = [new ChatMessage(ChatRole.System, SystemPrompt)];
        _messagesStack.Children.Clear();
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        var text = _chatInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text) || _isBusy)
            return;

        _chatInput.Text = string.Empty;
        _isBusy = true;
        _chatInput.IsEnabled = false;

        AddUserMessage(text);
        _history.Add(new ChatMessage(ChatRole.User, text));

        try
        {
            var options = new ChatOptions { Tools = [.. Tools] };
            var responseText = string.Empty;
            Label? responseLabel = null;
            var updates = new List<ChatResponseUpdate>();

            await foreach (var update in _chatClient.GetStreamingResponseAsync(_history, options))
            {
                updates.Add(update);

                foreach (var content in update.Contents)
                {
                    switch (content)
                    {
                        case FunctionCallContent call:
                            AddToolMessage($"🔧 {call.Name}");
                            break;
                        case FunctionResultContent result:
                            var s = result.Result?.ToString() ?? "(no result)";
                            if (s.Length > 160) s = s[..160] + "...";
                            AddToolMessage($"✅ {s}");
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
            if (responseLabel is null && string.IsNullOrEmpty(responseText))
                AddAssistantMessage("(no response)");
        }
        catch (Exception ex)
        {
            AddErrorMessage(ex.Message);
        }
        finally
        {
            _isBusy = false;
            _chatInput.IsEnabled = true;
        }
    }

    private void AddUserMessage(string text) => Append(new Border
    {
        BackgroundColor = Color.FromArgb("#DCF8C6"),
        Padding = new Thickness(12, 8),
        HorizontalOptions = LayoutOptions.End,
        MaximumWidthRequest = 320,
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
        StrokeThickness = 0,
        Content = new Label { Text = text, FontSize = 14, TextColor = Colors.Black },
    });

    private Label AddAssistantMessage(string text)
    {
        var label = new Label { Text = text, FontSize = 14 };
        Append(new Border
        {
            BackgroundColor = Color.FromArgb("#F0F0F0"),
            Padding = new Thickness(12, 8),
            HorizontalOptions = LayoutOptions.Start,
            MaximumWidthRequest = 320,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            StrokeThickness = 0,
            Content = label,
        });
        return label;
    }

    private void AddToolMessage(string text) => Append(new Label
    {
        Text = text,
        FontSize = 12,
        TextColor = Colors.Gray,
        FontAttributes = FontAttributes.Italic,
        Padding = new Thickness(8, 2),
    });

    private void AddErrorMessage(string text) => Append(new Label
    {
        Text = $"❌ {text}",
        FontSize = 12,
        TextColor = Colors.Red,
        Padding = new Thickness(8, 2),
    });

    private void Append(IView view)
    {
        _messagesStack.Children.Add(view);
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(40),
            async () => await _chatScrollView.ScrollToAsync(0, _chatScrollView.ContentSize.Height, true));
    }
}
