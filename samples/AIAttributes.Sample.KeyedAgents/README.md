# KeyedAgents — multiple tool sets in one app

A two-tab MAUI app. The **Browse** tab is a read-only catalog agent. The
**Manage** tab can mutate the user's garden. Each tab talks to a different
`AITool` set resolved by DI key.

## What this demonstrates

- **Keyed tool registration.**
  ```csharp
  services.AddAITools<CatalogTools>("browse");
  services.AddAITools<GardenManagementTools>("manage");
  ```
  Each context exports its own `[ExportAIFunction]` methods; they do not see
  each other's tools.
- **Per-tab DI scope.** Each page creates its own scope, so scoped services
  like `GardenService` stay isolated between tabs.
- **Per-tab tool resolution** via
  `sp.GetKeyedServices<AITool>("browse" | "manage")` rather than a single
  global tool list.

## When to look at this sample

You want an app where different UI surfaces have different capabilities —
one read-only agent, one agent with write access, and so on. This is also
the pattern for role-based or persona-based agents.

## Run

All four `AIAttributes.Sample.*` apps share one `UserSecretsId`
(`ai-attributes-secrets`), so you configure the endpoint once:

```bash
dotnet user-secrets --id ai-attributes-secrets set "AI:Endpoint" "https://<resource>.openai.azure.com"
dotnet user-secrets --id ai-attributes-secrets set "AI:ApiKey" "<your-key>"
dotnet user-secrets --id ai-attributes-secrets set "AI:DeploymentName" "<deployment-name>"

dotnet build samples/AIAttributes.Sample.KeyedAgents -f net10.0-maccatalyst
```

## Inspecting the generated source

This csproj sets `EmitCompilerGeneratedFiles=true` so you can see exactly
what `Microsoft.Maui.AI.Attributes.Generators` emits for each tool context.
It is **not required for the sample to  delete the property if yourun** 
don't care about generator output.

After a build, look under:

```
artifacts/obj/<ProjectName>/<Config>/<TargetFramework>/generated/Microsoft.Maui.AI.Attributes.Generators/Microsoft.Maui.AI.Attributes.Generators.AIToolContextGenerator/*.g.cs
```
