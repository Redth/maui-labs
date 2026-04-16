using AIAttributes.Sample.KeyedAgents.Chat;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AIAttributes.Sample.KeyedAgents;

/// <summary>
/// Base page for a keyed-agent chat. Each subclass specifies the DI key
/// used to resolve its tool set. A fresh DI scope is created per page so
/// scoped services (like <c>GardenService</c>) are isolated between tabs.
/// </summary>
public abstract class KeyedAgentPage : ContentPage
{
    private readonly IServiceScope _scope;
    private readonly ChatPanel _panel;

    protected KeyedAgentPage(IServiceProvider services, string toolKey, string title, string subtitle, string systemPrompt)
    {
        _scope = services.CreateScope();
        var sp = _scope.ServiceProvider;

        var tools = sp.GetKeyedServices<AITool>(toolKey).ToArray();
        var chatClient = sp.GetRequiredService<IChatClient>();

        _panel = new ChatPanel(chatClient, sp)
        {
            Title = title,
            Subtitle = subtitle,
            SystemPrompt = systemPrompt,
            Tools = tools,
        };

        Title = title;
        Content = _panel;
    }
}
