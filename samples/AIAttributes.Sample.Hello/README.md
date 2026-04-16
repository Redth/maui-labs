# Hello — minimal console sample

The smallest possible `Microsoft.Maui.AI.Attributes` app: one service, one
attribute, one console REPL.

## What this demonstrates

- `[ExportAIFunction]` on regular methods of a regular class.
- `[AIToolSource(typeof(Service))]` on an empty `partial class : AIToolContext`
  — the source generator fills it in at build time.
- `services.AddAITools<WeatherTools>()` discovering every exported function.
- `ChatClientBuilder.UseFunctionInvocation()` wiring the tools into an
  `IChatClient` pipeline.

## Run

All four `AIAttributes.Sample.*` apps share one `UserSecretsId`
(`ai-attributes-secrets`), so you configure the endpoint once:

```bash
dotnet user-secrets --id ai-attributes-secrets set "AI:Endpoint" "https://<resource>.openai.azure.com"
dotnet user-secrets --id ai-attributes-secrets set "AI:ApiKey" "<your-key>"
dotnet user-secrets --id ai-attributes-secrets set "AI:DeploymentName" "<deployment-name>"

dotnet run --project samples/AIAttributes.Sample.Hello
```

Type a prompt like `What's the weather in Paris?` and the model will call the
`get_temperature` or `get_forecast` tool.

## When to look at this sample

You are new to the library and want to see the smallest end-to-end wiring.
Move on to one of the other samples once you want to see scopes, keyed
agents, or DI parameter binding.
