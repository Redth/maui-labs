using AIAttributes.Sample.Garden.Models;

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
                ChatMessageKind.User => UserTemplate!,
                ChatMessageKind.Assistant => AssistantTemplate!,
                ChatMessageKind.Tool => ToolTemplate!,
                ChatMessageKind.System => SystemTemplate!,
                ChatMessageKind.Error => ErrorTemplate!,
                _ => AssistantTemplate!,
            }
            : AssistantTemplate!;
}
