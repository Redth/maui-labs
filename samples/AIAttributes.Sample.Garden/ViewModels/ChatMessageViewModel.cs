using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAttributes.Sample.Garden.ViewModels;

public enum ChatMessageKind
{
    User,
    Assistant,
    Tool,
    System,
    Error,
}

/// <summary>
/// View model for one chat message row. <see cref="Text"/> is mutable so a
/// streaming assistant reply can be updated in place while bound.
/// </summary>
public sealed partial class ChatMessageViewModel(ChatMessageKind kind, string text) : ObservableObject
{
    public ChatMessageKind Kind { get; } = kind;

    [ObservableProperty]
    private string _text = text;
}
