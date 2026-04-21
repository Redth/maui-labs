using AIAttributes.Sample.Garden.ViewModels;

namespace AIAttributes.Sample.Garden.Views;

public partial class ChatView : ContentView
{
    private ChatViewModel? _previousVm;

    public ChatView()
    {
        InitializeComponent();
        BindingContextChanged += OnBindingContextChanged;
    }

    private void OnBindingContextChanged(object? sender, EventArgs e)
    {
        if (_previousVm is not null)
            _previousVm.MessageAdded -= OnMessageAdded;

        _previousVm = BindingContext as ChatViewModel;

        if (_previousVm is not null)
            _previousVm.MessageAdded += OnMessageAdded;
    }

    private void OnMessageAdded(ChatMessageViewModel message)
    {
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(50), () =>
        {
            try { MessagesView.ScrollTo(message, position: ScrollToPosition.End, animate: true); }
            catch { /* item may have been removed */ }
        });
    }
}
