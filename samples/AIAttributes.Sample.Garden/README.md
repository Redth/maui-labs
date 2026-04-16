# Garden — scoped lifetime & approvals

A single-page MAUI chat app where the AI can add, water, move, and remove
plants from your garden. The left side is the chat; the right side is a live
panel showing what the AI actually did.

## What this demonstrates

- **Scoped services per chat session.** `GardenService` is registered as
  `AddScoped`, and the page creates a fresh `IServiceScope` every time you
  tap **New Chat**. The garden panel resets — proof the scope is real.
- **Singleton vs scoped.** `PlantCatalogService` is a singleton (shared across
  sessions) while `GardenService` is per scope.
- **Approval flow.** `RemoveFromGarden` is marked with
  `[ExportAIFunction(ApprovalRequired = true)]`. The UI intercepts the tool
  call and prompts you before forwarding the result.
- **MAUI DevFlow agent integration.** The app exposes itself to
  `maui devflow` for live UI inspection during development.

## Run

All four `AIAttributes.Sample.*` apps share one `UserSecretsId`
(`ai-attributes-secrets`), so you configure the endpoint once:

```bash
dotnet user-secrets --id ai-attributes-secrets set "AI:Endpoint" "https://<resource>.openai.azure.com"
dotnet user-secrets --id ai-attributes-secrets set "AI:ApiKey" "<your-key>"
dotnet user-secrets --id ai-attributes-secrets set "AI:DeploymentName" "<deployment-name>"

dotnet build samples/AIAttributes.Sample.Garden -f net10.0-maccatalyst
```

Then run the resulting bundle, or `dotnet run -f net10.0-maccatalyst`.

## When to look at this sample

You want to understand how scope boundaries, lifetime, and the approval flow
behave together inside a real UI.
