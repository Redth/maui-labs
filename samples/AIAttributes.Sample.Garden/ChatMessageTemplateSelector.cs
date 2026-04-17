using AIAttributes.Sample.Garden.ViewModels;

namespace AIAttributes.Sample.Garden;

/// <summary>
/// Picks a message DataTemplate based on <see cref="ChatMessageViewModel.Kind"/>.
/// Templates are defined in MainPage.xaml resources.
/// </summary>
public sealed class ChatMessageTemplateSelector : DataTemplateSelector
{
    public DataTemplate? UserTemplate { get; set; }
    public DataTemplate? AssistantTemplate { get; set; }
    public DataTemplate? ToolTemplate { get; set; }
    public DataTemplate? SystemTemplate { get; set; }
    public DataTemplate? ErrorTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        item is ChatMessageViewModel m
            ? m.Kind switch
            {
                ChatMessageKind.User => UserTemplate ?? throw new InvalidOperationException($"{nameof(UserTemplate)} not set"),
                ChatMessageKind.Assistant => AssistantTemplate ?? throw new InvalidOperationException($"{nameof(AssistantTemplate)} not set"),
                ChatMessageKind.Tool => ToolTemplate ?? throw new InvalidOperationException($"{nameof(ToolTemplate)} not set"),
                ChatMessageKind.System => SystemTemplate ?? throw new InvalidOperationException($"{nameof(SystemTemplate)} not set"),
                ChatMessageKind.Error => ErrorTemplate ?? throw new InvalidOperationException($"{nameof(ErrorTemplate)} not set"),
                _ => AssistantTemplate ?? throw new InvalidOperationException($"{nameof(AssistantTemplate)} not set"),
            }
            : AssistantTemplate ?? throw new InvalidOperationException($"{nameof(AssistantTemplate)} not set");
}
