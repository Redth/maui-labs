using System.ComponentModel;

namespace AIAttributes.Sample.Garden.ViewModels;

/// <summary>
/// View model for one chat message row. <see cref="Text"/> is mutable so a
/// streaming assistant reply can be updated in place while bound.
/// </summary>
public sealed class ChatMessageViewModel(ChatMessageKind kind, string text) : INotifyPropertyChanged
{
    private string _text = text;

    public ChatMessageKind Kind { get; } = kind;

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
