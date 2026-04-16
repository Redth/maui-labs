# Microsoft.Maui.AI.Attributes

Source-generated AI tool discovery for .NET 10. Decorate service methods with `[ExportAIFunction]`, group them into tool contexts with `[AIToolSource]`, and register everything in DI — no runtime reflection needed.

## How It Works

### 1. Annotate your service methods

```csharp
using System.ComponentModel;
using Microsoft.Maui.AI.Attributes;

public class PlantCatalogService
{
    [Description("Searches the plant catalog by name or category.")]
    [ExportAIFunction("search_plants")]
    public List<PlantInfo> SearchPlants(
        [Description("Optional filter text")] string? query = null)
    {
        // ...
    }
}
```

- `[ExportAIFunction]` marks a method as an AI-callable tool
- `[ExportAIFunction("custom_name")]` overrides the tool name (defaults to method name)
- `[Description]` on the method and parameters provides AI-visible documentation
- `ApprovalRequired = true` wraps the tool so it requires user approval before execution

### 2. Define a tool context

```csharp
using Microsoft.Maui.AI.Attributes;

[AIToolSource(typeof(PlantCatalogService))]
[AIToolSource(typeof(GardenService))]
public partial class AllGardenTools : AIToolContext { }
```

The **source generator** scans each `[AIToolSource]` type for `[ExportAIFunction]` methods at compile time and emits the registration code. No reflection, AOT-safe.

Multiple contexts can overlap — the same service can appear in several contexts:

```csharp
[AIToolSource(typeof(PlantCatalogService))]
public partial class CatalogTools : AIToolContext { }

[AIToolSource(typeof(GardenService))]
public partial class GardenManagementTools : AIToolContext { }
```

### 3. Register tools in DI

```csharp
// Default (non-keyed) — tools go into IEnumerable<AITool>
builder.Services.AddAITools<AllGardenTools>();

// Keyed — tools registered under a service key
builder.Services.AddAITools<GardenManagementTools>("management");

// Hand-crafted tools coexist with generated tools
builder.Services.AddSingleton<AITool>(
    AIFunctionFactory.Create(() => DateTime.Now.ToString("f"),
        "get_current_datetime", "Gets the current date and time."));
```

### 4. Use tools on demand (no DI registration)

```csharp
var tools = CatalogTools.Default.GetTools(serviceProvider);
// Use with any IChatClient directly
```

### 5. Inject tools into an IChatClient pipeline

Use `ConfigureOptions` to inject tools into every request automatically:

```csharp
var tools = AllGardenTools.Default.GetTools(serviceProvider);
var client = chatClient.AsBuilder()
    .UseFunctionInvocation()
    .ConfigureOptions(opts =>
    {
        opts.Tools ??= [];
        foreach (var tool in tools)
            opts.Tools.Add(tool);
    })
    .Build(serviceProvider);

// No need to pass ChatOptions.Tools on each request — just call:
await foreach (var update in client.GetStreamingResponseAsync(messages))
{
    Console.Write(update.Text);
}
```

## Dependency Injection & Parameter Binding

At compile time the source generator classifies each parameter and emits the right binding code. You don't need to configure anything at runtime.

| Parameter shape | Binding |
|---|---|
| `CancellationToken` | Flows from the function-invocation pipeline. **Not** in the tool schema. |
| `IServiceProvider` | The service provider active for the call. Not in schema. |
| `AIFunctionArguments` | The raw argument bag. Not in schema. |
| `[FromServices] IMyThing x` | `provider.GetRequiredService<IMyThing>()`. Not in schema. |
| `[FromKeyedServices("k")] IMyThing x` | `provider.GetRequiredKeyedService<IMyThing>("k")`. Not in schema. |
| `IMyThing x` (interface, no attribute) | Inferred DI — `provider.GetRequiredService<IMyThing>()`. Not in schema. A `MAUIAI001` info diagnostic records the inference. |
| `[FromArguments] IMyThing x` | Escape hatch: include the interface in the schema and deserialize from JSON. |
| Everything else (`string`, records, enums, collections, …) | Bound from the JSON argument dictionary. |

The tool class never calls `AIFunctionFactory.Create` and never uses `MethodInfo.Invoke`. The generated code resolves the host service and each dependency from the service provider, reads arguments from the dictionary, and calls your method directly.

## Service Lifetimes & Scopes

The library **never creates a DI scope**. It uses whatever service provider the caller threads in via `AIFunctionArguments.Services`, falling back to the provider captured at registration time when that's null.

You control the scope purely by how you register `IChatClient`:

| Registration | Resulting scope behavior |
|---|---|
| Build `IChatClient` once at app start with `app.Services` | Root-scope: `AddSingleton` and `AddTransient` work. `AddScoped` services are resolved from the root and behave like singletons, which is rarely what you want. |
| Build `IChatClient` per chat session with a session-specific `IServiceScope` | Scoped services live for the chat session. Reset the chat by disposing the scope and creating a new one. |
| Register `IChatClient` as transient | New client per resolution — rarely useful. |

Example (scope-per-session, matches `samples/AIAttributesSample`):

```csharp
_sessionScope?.Dispose();
_sessionScope = _rootProvider.CreateScope();

_sessionClient = new ChatClientBuilder(_innerChatClient)
    .UseFunctionInvocation(configure: fic => fic.AdditionalTools = [.. tools])
    .Build(_sessionScope.ServiceProvider); // ← scope flows through to tools
```

Inside each tool invocation, `AIFunctionArguments.Services` is the session scope's provider, so `AddScoped<GardenService>()` gets a fresh instance per session but stays consistent across tool calls within the session.

## Project References

This library ships as two projects:

```xml
<!-- The attributes and base types -->
<ProjectReference Include="Microsoft.Maui.AI.Attributes.csproj" />

<!-- The source generator (analyzer, not a runtime reference) -->
<ProjectReference Include="Microsoft.Maui.AI.Attributes.Generators.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

## Key Types

| Type | Description |
|---|---|
| `ExportAIFunctionAttribute` | Marks a method as an AI tool |
| `AIToolSourceAttribute` | Declares which service contributes tools to a context |
| `AIToolContext` | Base class for source-generated tool contexts |
| `FromArgumentsAttribute` | Forces an interface/abstract parameter to be bound from the argument dictionary (instead of the default DI inference) |
| `AddAITools<T>()` | Extension method to register tools from a context |

## AOT compatibility

The hot invocation path contains **no reflection and no dynamic code emission**: each tool is a compile-time-generated `AIFunction` subclass that looks up its service from `IServiceProvider`, reads named arguments, and calls your method directly.

Schema generation still goes through `AIJsonUtilities.CreateFunctionJsonSchema` (reflective, but invoked once per tool at warmup and cached). Full AOT schema emission is a planned follow-up.

## Diagnostics

| ID | Severity | Meaning |
|---|---|---|
| `MAUIAI001` | Info | A parameter was inferred as a DI service because its type is an interface or abstract class. Use `[FromArguments]` to opt out. |
| `MAUIAI002` | Warning | A parameter's type is unlikely to round-trip through JSON (e.g. delegate or pointer). Annotate with `[FromServices]` or change the signature. |
| `MAUIAI003` | Warning | An `[AIToolSource(typeof(T))]` references a type that has no `[ExportAIFunction]` methods. |
| `MAUIAI004` | Error | The method has an unsupported signature (generic method, `ref`/`out` parameters, etc.). |
