using System.ClientModel;
using System.ComponentModel;
using AIAttributes.Sample.DIParameters;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.AI.Attributes;

// This sample focuses on the parameter binding shapes Microsoft.Maui.AI.Attributes
// supports — the stuff that makes it more than "attributes wrapping ReflectionAIFunction":
//
//   • inferred DI for interface/abstract parameters
//   • [FromKeyedServices] for keyed services
//   • [FromArguments] to force a DI-able type into the tool schema
//   • CancellationToken + IServiceProvider as direct parameters
//
// See the single tool in TranslatorService below.

var apiKey = Environment.GetEnvironmentVariable("AI_API_KEY");
var endpoint = Environment.GetEnvironmentVariable("AI_ENDPOINT");
var deployment = Environment.GetEnvironmentVariable("AI_DEPLOYMENT");

if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(deployment))
{
    Console.Error.WriteLine("""
        AI_API_KEY, AI_ENDPOINT and AI_DEPLOYMENT environment variables must be set.

          export AI_API_KEY="<your key>"
          export AI_ENDPOINT="<https://your-resource.openai.azure.com>"
          export AI_DEPLOYMENT="<your deployment name>"
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

namespace AIAttributes.Sample.DIParameters
{
    public interface ITranslator { string Translate(string text); }

    public sealed class PigLatinTranslator : ITranslator
    {
        public string Translate(string text) =>
            string.Join(' ', text.Split(' ').Select(w =>
                w.Length > 1 ? w[1..] + w[0] + "ay" : w));
    }

    public interface IModelProvider { string Name { get; } }

    public sealed class PremiumModelProvider : IModelProvider { public string Name => "premium-v2"; }
    public sealed class FreeModelProvider : IModelProvider { public string Name => "free-v1"; }

    /// <summary>Options passed in by the AI model as part of the tool call schema.</summary>
    public sealed record TranslationOptions(bool Verbose = false);

    public class TranslatorService
    {
        [Description("Translates a phrase using the configured translator.")]
        [ExportAIFunction("translate")]
        public string Translate(
            [Description("The text to translate")] string text,
            // Inferred DI: interface parameter pulled from the IServiceProvider
            // at invocation time. Not part of the tool schema.
            ITranslator translator,
            // Explicit keyed DI: resolved via [FromKeyedServices].
            [FromKeyedServices("premium")] IModelProvider model,
            // [FromArguments] forces a DI-resolvable type to be treated as a
            // model argument instead — the AI fills it in per call.
            [FromArguments] TranslationOptions options,
            // Direct CancellationToken support — never appears in the schema.
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var translated = translator.Translate(text);
            return options.Verbose
                ? $"[model: {model.Name}] {text} => {translated}"
                : translated;
        }
    }

    [AIToolSource(typeof(TranslatorService))]
    public partial class TranslatorTools : AIToolContext { }
}
