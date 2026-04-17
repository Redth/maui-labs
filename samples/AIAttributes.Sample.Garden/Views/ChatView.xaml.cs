using AIAttributes.Sample.Garden.ViewModels;

namespace AIAttributes.Sample.Garden.Views;

public partial class ChatView : ContentView
{
    public ChatView()
    {
        InitializeComponent();
        BindingContextChanged += OnBindingContextChanged;
    }

    private void OnBindingContextChanged(object? sender, EventArgs e)
    {
        if (BindingContext is MainViewModel vm)
            vm.MessageAdded += OnMessageAdded;
    }

    private void OnMessageAdded(ChatMessageViewModel message)
    {
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(50), () =>
        {
            try { MessagesView.ScrollTo(message, position: ScrollToPosition.End, animate: true); }
            catch { /* item removed */ }
        });
    }
}
