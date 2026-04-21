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
public sealed partial class ChatMessageViewModel(ChatMessageKind kind, string text, string? icon = null) : ObservableObject
{
    public ChatMessageKind Kind { get; } = kind;

    /// <summary>Optional Fluent icon glyph rendered with FluentFilled font.</summary>
    public string? Icon { get; } = icon;

    public bool HasIcon => Icon is not null;

    [ObservableProperty]
    private string _text = text;
}
