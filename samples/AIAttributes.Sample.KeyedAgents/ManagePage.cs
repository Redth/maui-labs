namespace AIAttributes.Sample.KeyedAgents;

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
