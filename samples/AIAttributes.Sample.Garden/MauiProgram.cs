using System.ClientModel;
using System.Reflection;
using AIAttributes.Sample.Garden.Services;
using AIAttributes.Sample.Garden.ViewModels;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.DevFlow.Agent;

namespace AIAttributes.Sample.Garden;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Configuration.AddUserSecrets();

#if DEBUG
        builder.AddMauiDevFlowAgent();
#endif

        // ── Services ────────────────────────────────────────────────
        // Everything is a singleton. There is no AddScoped, no
        // CreateScope() — this is the Phase 2 punchline. Per-session
        // state lives on a plain ChatSession object owned by the view
        // model and published to AI tools through ICurrentSession.
        builder.Services.AddSingleton<OrderArchive>();
        builder.Services.AddSingleton<ChatSessionFactory>();
        builder.Services.AddSingleton<ICurrentSession, CurrentSession>();

        // ── AI Tools (source-generated) ─────────────────────────────
        // GardenShopTools.Default.GetTools() returns the AI tool list. No
        // DI registration needed — the source generator emits a static
        // singleton on the context. Each tool reads
        // AIFunctionArguments.Services at invocation time, which is
        // populated by UseFunctionInvocation().Build(sp) below.

        // ── AI Client ───────────────────────────────────────────────
        builder.AddOpenAIServices();

        // ── Pages ───────────────────────────────────────────────────
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void AddUserSecrets(this ConfigurationManager manager)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();
        var secretsResource = resourceNames.FirstOrDefault(n => n.EndsWith("secrets.json"));
        if (secretsResource is not null)
        {
            var stream = assembly.GetManifestResourceStream(secretsResource);
            if (stream is not null)
                manager.AddJsonStream(stream);
        }
    }

    private static MauiAppBuilder AddOpenAIServices(this MauiAppBuilder builder)
    {
        var aiSection = builder.Configuration.GetSection("AI");
        var apiKey = aiSection["ApiKey"];
        var endpoint = aiSection["Endpoint"];
        var deploymentName = aiSection["DeploymentName"];

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(deploymentName))
        {
            throw new InvalidOperationException(
                """
                AI services are not configured. Set up user secrets (shared across all AIAttributes samples):

                  dotnet user-secrets --id ai-attributes-secrets set "AI:Endpoint" "<your-endpoint>"
                  dotnet user-secrets --id ai-attributes-secrets set "AI:ApiKey" "<your-key>"
                  dotnet user-secrets --id ai-attributes-secrets set "AI:DeploymentName" "<your-deployment>"
                """);
        }

        var azureClient = new AzureOpenAIClient(
            new Uri(endpoint),
            new ApiKeyCredential(apiKey));
        var chatClient = azureClient.GetChatClient(deploymentName);

        // Raw IChatClient — the view model wraps it in
        // ChatClientBuilder(...).UseFunctionInvocation().Build(sp).
        builder.Services.AddSingleton<IChatClient>(chatClient.AsIChatClient());

        return builder;
    }
}
