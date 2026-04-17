using System.ClientModel;
using System.ComponentModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.AI.Attributes;

// Smallest possible end-to-end example of Microsoft.Maui.AI.Attributes:
//   - WeatherTools  -> tools backed by a DI-registered WeatherService
//   - GreetingTools -> pure static tools that need no DI at all
// Both are consumed identically via MyContext.Default.GetTools().

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

        (shared across all AI.Attributes samples)
        """);
    return 1;
}

var services = new ServiceCollection();

// 1. Register the backing service for the DI-bound tools.
services.AddSingleton<WeatherService>();

// 2. Register the chat client.
var azure = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
IChatClient innerClient = azure.GetChatClient(deployment).AsIChatClient();
services.AddSingleton(innerClient);

var root = services.BuildServiceProvider();

// 3. Build the chat client. UseFunctionInvocation().Build(sp) wires the root
//    service provider into AIFunctionArguments.Services on every invocation,
//    which is how DI-bound tools resolve their backing service.
var chat = new ChatClientBuilder(root.GetRequiredService<IChatClient>())
    .UseFunctionInvocation()
    .Build(root);

// 4. Compose the tool list from the source-generated contexts. No DI bag,
//    no keyed lookup — just the 'Default' singleton on each partial class.
//    WeatherTools needs an IServiceProvider at invocation time (to resolve
//    WeatherService); GreetingTools does not, because its [ExportAIFunction]
//    methods are static.
var tools = new List<AITool>();
tools.AddRange(WeatherTools.Default.GetTools());
tools.AddRange(GreetingTools.Default.GetTools());
var options = new ChatOptions { Tools = tools };

Console.WriteLine($"{tools.Count} tool(s) registered:");
foreach (var t in tools)
    Console.WriteLine($"  - {t.Name}: {t.Description}");
Console.WriteLine();
Console.WriteLine("Try asking: \"What's the weather in Tokyo?\" or \"Say hello to Ada.\"");
Console.WriteLine("Ctrl+C to exit.");
Console.WriteLine();

var history = new List<ChatMessage>
{
    new(ChatRole.System, "You are a helpful assistant. Use the available tools when relevant.")
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

/// <summary>
/// Source-generated tool context. The generator fills this partial class with
/// a <c>GetTools()</c> override that returns an <see cref="AITool"/> per
/// <c>[ExportAIFunction]</c> method on <see cref="WeatherService"/>.
/// Each generated tool resolves <see cref="WeatherService"/> from
/// <see cref="AIFunctionArguments.Services"/> at invocation time.
/// </summary>
[AIToolSource(typeof(WeatherService))]
public partial class WeatherTools : AIToolContext { }

/// <summary>
/// Source-generated tool context backed by a type whose <c>[ExportAIFunction]</c>
/// methods are <see langword="static"/>. The generated tools call the static
/// methods directly and never touch <see cref="IServiceProvider"/> &#8212; they
/// can be invoked without any DI container at all.
/// </summary>
[AIToolSource(typeof(GreetingService))]
public partial class GreetingTools : AIToolContext { }

/// <summary>
/// Stateless service with two exported AI tools that need <see cref="WeatherService"/>
/// to be available via DI at invocation time.
/// </summary>
public class WeatherService
{
    [Description("Gets the current temperature in a city.")]
    [ExportAIFunction("get_temperature")]
    public string GetTemperature(
        [Description("The city name")] string city,
        [Description("Unit: 'celsius' or 'fahrenheit'. Defaults to celsius.")] string unit = "celsius")
    {
        var temp = city.GetHashCode() % 30;
        return unit.Equals("fahrenheit", StringComparison.OrdinalIgnoreCase)
            ? $"{temp * 9 / 5 + 32}°F in {city}"
            : $"{temp}°C in {city}";
    }

    [Description("Gets a short forecast for the next few days in a city.")]
    [ExportAIFunction("get_forecast")]
    public string GetForecast(
        [Description("The city name")] string city,
        [Description("Number of days (1-7). Defaults to 3.")] int days = 3)
    {
        return $"{days}-day forecast for {city}: mostly pleasant.";
    }
}

/// <summary>
/// Pure-static service &#8212; no instance, no DI. The generator emits tools
/// that call these methods directly.
/// </summary>
public static class GreetingService
{
    [Description("Greets someone by name in plain English.")]
    [ExportAIFunction("say_hello")]
    public static string SayHello(
        [Description("The name of the person to greet")] string name)
        => $"Hello, {name}!";

    [Description("Returns the number of letters in a word.")]
    [ExportAIFunction("count_letters")]
    public static int CountLetters(
        [Description("The word to measure")] string word)
        => word?.Length ?? 0;
}
