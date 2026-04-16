# DIParameters — parameter binding shapes

A console app that shows the parameter shapes
`Microsoft.Maui.AI.Attributes` supports beyond "plain value in, value out".

## What this demonstrates

All on a single `[ExportAIFunction]` method:

- **Inferred DI.** An `ITranslator translator` parameter is resolved from the
  service provider at invocation time. The AI never sees it in the schema.
- **Keyed DI.** `[FromKeyedServices("premium")] IModelProvider model` pulls
  the keyed registration.
- **`[FromArguments]`.** Forces `TranslationOptions` — which would otherwise
  be DI-resolvable — to appear as a tool argument filled in by the model.
- **`CancellationToken`.** Bound automatically; never in the schema.

## Why this exists

Other attribute-based libraries typically only wrap `ReflectionAIFunction`
and expect you to hand-author `AIFunctionFactory.Create` calls to get DI
support. This sample is the proof the source generator emits the same
behaviour without runtime reflection.

## Run

All four `AIAttributes.Sample.*` apps share one `UserSecretsId`
(`ai-attributes-secrets`), so you configure the endpoint once:

```bash
dotnet user-secrets --id ai-attributes-secrets set "AI:Endpoint" "https://<resource>.openai.azure.com"
dotnet user-secrets --id ai-attributes-secrets set "AI:ApiKey" "<your-key>"
dotnet user-secrets --id ai-attributes-secrets set "AI:DeploymentName" "<deployment-name>"

dotnet run --project samples/AIAttributes.Sample.DIParameters
```

Try: `Translate 'hello world' to pig latin with verbose output.`
