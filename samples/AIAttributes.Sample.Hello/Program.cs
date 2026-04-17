using System.ClientModel;
using System.ComponentModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.AI.Attributes;

// Smallest possible end-to-end example of Microsoft.Maui.AI.Attributes:
// one service, two [ExportAIFunction] methods, AddAITools<T>(), and a
// console chat loop powered by FunctionInvokingChatClient.

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

// 1. Register the backing service for our AI tools.
services.AddSingleton<WeatherService>();

// 2. Register the source-generated tools. At compile time the generator
//    scanned [ExportAIFunction] methods on the service types listed on
//    [AIToolSource] and emitted DI-aware AIFunction wrappers for each.
services.AddAITools<WeatherTools>();

// 3. Register the chat client wrapped with FunctionInvokingChatClient so
//    it can discover and call our tools.
var azure = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
IChatClient innerClient = azure.GetChatClient(deployment).AsIChatClient();
services.AddSingleton(innerClient);

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
Console.WriteLine("Try asking: \"What's the weather in Tokyo?\"");
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
/// Source-generated tool context. The generator fills this partial class
/// with <c>GetTools</c> and <c>RegisterTools</c> overrides that wire each
/// <c>[ExportAIFunction]</c> method on <see cref="WeatherService"/> into
/// an <see cref="AITool"/>.
/// </summary>
[AIToolSource(typeof(WeatherService))]
public partial class WeatherTools : AIToolContext { }

/// <summary>
/// Stateless service with two exported AI tools. The source generator
/// emits one <see cref="AIFunction"/> wrapper per method.
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
