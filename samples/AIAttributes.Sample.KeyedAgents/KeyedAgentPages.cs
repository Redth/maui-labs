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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }
}

public sealed class BrowsePage : KeyedAgentPage
{
    public BrowsePage(IServiceProvider services)
        : base(services,
               toolKey: "browse",
               title: "Browse Catalog",
               subtitle: "Read-only agent — searches the plant catalog",
               systemPrompt: "You are a gardening reference assistant. You can search a catalog of plants " +
                             "but cannot modify anyone's garden. Use the catalog tools to answer questions.")
    { }
}

public sealed class ManagePage : KeyedAgentPage
{
    public ManagePage(IServiceProvider services)
        : base(services,
               toolKey: "manage",
               title: "Manage Garden",
               subtitle: "Mutation agent — adds, moves, removes plants",
               systemPrompt: "You are a garden management assistant. You can add plants, water them, " +
                             "move them, and remove them. Do not make up plant data; only use the tools.")
    { }
}
