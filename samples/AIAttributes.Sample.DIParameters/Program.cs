using System.ClientModel;
using AIAttributes.Sample.DIParameters;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.AI.Attributes;

// This sample focuses on the parameter binding shapes Microsoft.Maui.AI.Attributes
// supports — the stuff that makes it more than "attributes wrapping ReflectionAIFunction":
//
//   • [FromServices] for DI-resolved parameters
//   • [FromKeyedServices] for keyed services
//   • CancellationToken as a direct parameter
//   • plain records / primitives bind from the argument dictionary (no annotation)
//
// See TranslatorService.Translate for the single tool.

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var apiKey = configuration["AI:ApiKey"];
var endpoint = configuration["AI:Endpoint"];
var deployment = configuration["AI:DeploymentName"];

if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(deployment))
{
    Console.Error.WriteLine("""
        AI:Endpoint, AI:ApiKey and AI:DeploymentName must be set. Configure user-secrets:

          dotnet user-secrets --id ai-attributes-secrets set "AI:Endpoint" "<endpoint>"
          dotnet user-secrets --id ai-attributes-secrets set "AI:ApiKey" "<key>"
          dotnet user-secrets --id ai-attributes-secrets set "AI:DeploymentName" "<deployment>"

        (shared across all 4 AI.Attributes samples)
        """);
    return 1;
}

var services = new ServiceCollection();

// Backing service that owns the [ExportAIFunction] method.
services.AddSingleton<TranslatorService>();

// Inferred DI: ITranslator is an interface. The generator sees the parameter
// type is abstract and emits a `sp.GetRequiredService<ITranslator>()` call
// at invocation time, hiding it from the AI model's schema.
services.AddSingleton<ITranslator, PigLatinTranslator>();

// Keyed DI: [FromKeyedServices("premium")] pulls this specific instance.
services.AddKeyedSingleton<IModelProvider, PremiumModelProvider>("premium");
services.AddKeyedSingleton<IModelProvider, FreeModelProvider>("free");

// Tool registration.
services.AddAITools<TranslatorTools>();

// Chat client.
var azure = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
services.AddSingleton<IChatClient>(azure.GetChatClient(deployment).AsIChatClient());

var root = services.BuildServiceProvider();
var chat = new ChatClientBuilder(root.GetRequiredService<IChatClient>())
    .UseFunctionInvocation()
    .Build(root);

var tools = root.GetServices<AITool>().ToList();
var options = new ChatOptions { Tools = [.. tools] };

Console.WriteLine($"{tools.Count} tool(s) registered:");
foreach (var t in tools)
    Console.WriteLine($"  - {t.Name}: {t.Description}");
Console.WriteLine();
Console.WriteLine("Try: \"Translate 'hello world' to pig latin with verbose output.\"");
Console.WriteLine("Ctrl+C to exit.");
Console.WriteLine();

var history = new List<ChatMessage>
{
    new(ChatRole.System,
        "You are a translation assistant. Use the translate tool when the user asks to translate text.")
};

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input))
        continue;

    history.Add(new ChatMessage(ChatRole.User, input));
    var response = await chat.GetResponseAsync(history, options);
    history.AddMessages(response);
    Console.WriteLine(response.Text);
    Console.WriteLine();
}
