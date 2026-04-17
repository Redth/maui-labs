using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AIAttributes.Sample.Garden.Models;

public enum ChatMessageKind
{
    User,
    Assistant,
    Tool,
    System,
    Error,
}

/// <summary>
/// View model for one chat message row. Text is mutable so streaming assistant
/// replies can be updated in-place while the CollectionView is bound.
/// </summary>
public sealed class ChatMessageViewModel : INotifyPropertyChanged
{
    private string _text;

    public ChatMessageViewModel(ChatMessageKind kind, string text)
    {
        Kind = kind;
        _text = text;
    }

    public ChatMessageKind Kind { get; }

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

/// <summary>View model for a tool listed in the empty-state placeholder.</summary>
public sealed record ToolInfoViewModel(string Name, string Description);
