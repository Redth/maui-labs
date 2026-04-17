# Hello — minimal console sample

The smallest possible `Microsoft.Maui.AI.Attributes` app: one DI-bound
service, one static service, one console REPL.

## What this demonstrates

- `[ExportAIFunction]` on regular instance methods (resolved via DI) **and**
  on `static` methods (no DI required).
- `[AIToolSource(typeof(Service))]` on an empty `partial class : AIToolContext`
  — the source generator fills it in at build time.
- `Tools.Default.GetTools()` as the headline API for getting the tool list.
  No `services.AddAITools<T>()` registration ceremony.
- `ChatClientBuilder.UseFunctionInvocation().Build(sp)` wiring the service
  provider through to each tool invocation.

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
Move on to one of the other samples once you want to see scopes, approval
flows, or DI parameter binding.

## Inspecting the generated source

This csproj sets `EmitCompilerGeneratedFiles=true` so you can see exactly
what `Microsoft.Maui.AI.Attributes.Generators` emits for each tool context.
It is **not required for the sample to  delete the property if yourun** 
don't care about generator output.

After a build, look under:

```
artifacts/obj/<ProjectName>/<Config>/<TargetFramework>/generated/Microsoft.Maui.AI.Attributes.Generators/Microsoft.Maui.AI.Attributes.Generators.AIToolContextGenerator/*.g.cs
```
