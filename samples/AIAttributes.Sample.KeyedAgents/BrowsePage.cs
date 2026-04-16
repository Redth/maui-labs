namespace AIAttributes.Sample.KeyedAgents;

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
