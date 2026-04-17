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
| Everything else (`string`, records, enums, interfaces without `[FromServices]`, …) | Bound from the JSON argument dictionary. |

The tool class never calls `AIFunctionFactory.Create` and never uses `MethodInfo.Invoke`. The generated code resolves the host service and each dependency from the service provider, reads arguments from the dictionary, and calls your method directly.

## Service Lifetimes & Scopes

The library **never creates a DI scope**. It uses whatever service provider the caller threads in via `AIFunctionArguments.Services`, falling back to the provider captured at registration time when that's null.

You control the scope purely by how you register `IChatClient`:

| Registration | Resulting scope behavior |
|---|---|
| Build `IChatClient` once at app start with `app.Services` | Root-scope: `AddSingleton` and `AddTransient` work. `AddScoped` services are resolved from the root and behave like singletons, which is rarely what you want. |
| Build `IChatClient` per chat session with a session-specific `IServiceScope` | Scoped services live for the chat session. Reset the chat by disposing the scope and creating a new one. |
| Register `IChatClient` as transient | New client per resolution — rarely useful. |

Example (scope-per-session, matches `samples/AIAttributes.Sample.Garden`):

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
| `FromServicesAttribute` | Resolves a parameter from `IServiceProvider` (lives in `Microsoft.Extensions.DependencyInjection` for discoverability alongside `[FromKeyedServices]`) |
| `AddAITools<T>()` | Extension method to register tools from a context |

## Samples

The repository ships four focused samples under `samples/`. Each one tells
exactly one story, so pick whichever matches what you want to learn:

| Sample | Type | Demonstrates |
|---|---|---|
| [`AIAttributes.Sample.Hello`](../../../samples/AIAttributes.Sample.Hello) | Console | Smallest possible end-to-end: one service, one attribute, one REPL. |
| [`AIAttributes.Sample.Garden`](../../../samples/AIAttributes.Sample.Garden) | MAUI | Scoped lifetime per chat session, approval-required tools, DevFlow integration. |
| [`AIAttributes.Sample.KeyedAgents`](../../../samples/AIAttributes.Sample.KeyedAgents) | MAUI | Multiple keyed tool sets in a single app (e.g. read-only vs mutation agent). |
| [`AIAttributes.Sample.DIParameters`](../../../samples/AIAttributes.Sample.DIParameters) | Console | Every parameter binding shape: `[FromServices]`, `[FromKeyedServices]`, plain records, `CancellationToken`. |

## Hand-crafted tools alongside generated ones

Generated tools are plain `AITool` DI registrations, so they compose with
anything. To mix hand-crafted `AIFunction`s into the same pipeline, register
them alongside:

```csharp
services.AddAITools<MyTools>();
services.AddSingleton<AITool>(AIFunctionFactory.Create(
    ([Description("ISO-8601 timestamp")] string _ = "") => DateTime.UtcNow.ToString("o"),
    name: "get_current_datetime"));
```

`sp.GetServices<AITool>()` returns both.

## Resolving a specific context's tools

`AddAITools<T>()` also registers the strongly-typed `T` itself. If you need
only one context's tools (for example, for a panel that exposes different
capabilities), resolve the context directly:

```csharp
var gardenTools = sp.GetRequiredService<GardenTools>().GetTools(sp);
```

Or use the keyed overload: `services.AddAITools<T>("key")` then
`sp.GetKeyedServices<AITool>("key")`. See the KeyedAgents sample.

## AOT compatibility

The hot invocation path contains **no reflection and no dynamic code emission**: each tool is a compile-time-generated `AIFunction` subclass that looks up its service from `IServiceProvider`, reads named arguments, and calls your method directly.

Schema generation still goes through `AIJsonUtilities.CreateFunctionJsonSchema` (reflective, but invoked once per tool at warmup and cached). Full AOT schema emission is a planned follow-up.

## Diagnostics

| ID | Severity | Meaning |
|---|---|---|
| `MAUIAI002` | Warning | A parameter's type is unlikely to round-trip through JSON (e.g. delegate or pointer). Annotate with `[FromServices]` or change the signature. |
| `MAUIAI003` | Warning | An `[AIToolSource(typeof(T))]` references a type that has no `[ExportAIFunction]` methods. |
| `MAUIAI004` | Error | The method has an unsupported signature (generic method, `ref`/`out` parameters, etc.). |

## Compatibility with `Microsoft.Extensions.AI.AIFunctionFactory`

This library aims to match the runtime behavior of `AIFunctionFactory.Create(MethodInfo, target)` as closely as possible. The `Microsoft.Maui.AI.Attributes.Tests/Equivalence/` suite mirrors the in-scope tests from [`dotnet/extensions`'s `AIFunctionFactoryTest.cs`](https://github.com/dotnet/extensions/blob/main/test/Libraries/Microsoft.Extensions.AI.Tests/Functions/AIFunctionFactoryTest.cs) and, where feasible, pairs each generated tool against an `AIFunctionFactory.Create`-backed oracle for side-by-side comparison.

### Behaviors that match exactly

- **Parameter mapping by name.** Plain parameters are bound from `AIFunctionArguments` by case-sensitive name.
- **C# default values.** Parameters with C# defaults (`= value`) are treated as optional and their defaults are reflected in the JSON schema.
- **Missing required parameters throw `ArgumentException`** whose message contains the parameter name.
- **JSON-encoded argument tolerance.** `JsonElement`, `JsonNode`, `JsonDocument`, and boxed numeric types are all accepted and deserialized to the target type. Raw JSON strings are parsed if they represent valid JSON for the target type, otherwise treated as literal strings where the target type allows.
- **Framework-provided parameters** (`CancellationToken`, `IServiceProvider`, `AIFunctionArguments`) are injected by type, excluded from the JSON schema, and do not require a name match.
- **Async return types.** `Task`, `ValueTask`, `Task<T>`, `ValueTask<T>` are awaited correctly; `void`/`Task`/`ValueTask` produce a null `ReturnJsonSchema`.
- **`[FromKeyedServices]`** with a string or `null` key resolves via `IKeyedServiceProvider`. The parameter is excluded from the JSON schema.
- **Nullable schema shape.** Both `int?`/`DateTime?` value types and `string?`/other nullable reference types produce JSON schemas with `"type": ["…", "null"]`; defaulted nullables produce `"default": null` and are omitted from the `required` list.
- **`AIContent`-typed returns** are returned as-is without JSON serialization, matching the default `MarshalResult` behavior in `AIFunctionFactory`.
- **Struct defaults** (`Guid`, `StructWithDefaultCtor`) are passed as CLR `default(T)`, matching the `= default` behavior observed by `AIFunctionFactory`.

### Intentional behavioral differences

The table below lists every place our behavior differs from `AIFunctionFactory.Create`. Each row links to a corresponding test or documents why no test is applicable.

| # | Area | `AIFunctionFactory.Create` | `Microsoft.Maui.AI.Attributes` | Why |
|---|------|----------------------------|--------------------------------|-----|
| 1 | **Source** | Delegates, lambdas, local functions, anonymous methods, and `DynamicMethod` all work. | Only `[ExportAIFunction]` methods on a reference type. Lambdas, local functions, `DynamicMethod` aren't supported. | The generator runs at compile time against Roslyn symbols; it needs a declared method to emit code for. |
| 2 | **Instance acquisition** | Caller supplies `target`, a `createInstanceFunc`, or lets `ActivatorUtilities` construct the type. | The service is always resolved via `IServiceProvider.GetRequiredService<TService>()` from either `AIFunctionArguments.Services` or the fallback captured at registration. | Encourages clean DI wiring; avoids per-invocation `Activator` reflection. |
| 3 | **Static methods** | Fails at `AIFunctionFactory.Create` time with `ArgumentException`. | Skipped silently by the generator. | A static method has no service to inject into; no DI resolution makes sense. If you need this, extract to an instance method or use `AIFunctionFactory.Create` directly for that one tool. |
| 4 | **Instance disposal** | When `createInstanceFunc` is used, disposable instances are disposed after each invocation. | Not applicable — lifetimes are managed entirely by DI. | DI already manages `IDisposable`/`IAsyncDisposable` lifetimes; re-implementing it would conflict. |
| 5 | **Automatic DI scope** | None (caller is responsible). | None (caller is responsible). | Matching behavior — neither library creates a scope automatically. **Your `IChatClient` pipeline must thread the appropriate `IServiceProvider` to `FunctionInvokingChatClient`**; see the sample app for a per-chat-session scope pattern. |
| 6 | **`[FromServices]` / arbitrary DI parameters** | Unsupported by default (would attempt to JSON-serialize the interface). | Supported via explicit `[FromServices]` and `[FromKeyedServices]` attributes. Parameters so-marked are resolved from DI and excluded from the JSON schema. | A deliberate ergonomic improvement — this is the main reason this library exists. There is **no implicit DI inference**: interface/abstract parameters without `[FromServices]` are still treated as JSON arguments, matching reflection behavior. |
| 7 | **`[FromKeyedServices]` with missing key and default value** | Falls back to the parameter's default value. | Throws `InvalidOperationException` from `GetRequiredKeyedService<T>`. | Simplifies the emitted code; the registration-time contract is "this key must exist". If you need optional-keyed semantics, take `[FromKeyedServices] T? p = null` is not enough — inject `IServiceProvider` and call `GetKeyedService(...)` yourself. |
| 8 | **`IServiceProvider?` parameter, `arguments.Services == null`** | Passes `null` to the method. | Passes the fallback provider captured at registration (never null as long as the tool was registered via `AddAITools<T>()`). | A consequence of (2): we always have *some* provider to give you. |
| 9 | **`AIFunctionFactoryOptions`** (`Name`, `Description`, `AdditionalProperties`, `ExcludeResultSchema`, `ConfigureParameterBinding`, `MarshalResult`, `SerializerOptions`) | First-class — overrides every aspect of the produced tool. | Not exposed. Name/description come from `[ExportAIFunction]` and `[Description]`; parameter binding is decided at compile time; result marshaling uses the default JSON behavior. | The design goal of this library is compile-time correctness: runtime options that rewrite binding/marshaling behavior undercut that. If you need them, call `AIFunctionFactory.Create` directly for that one tool and mix it into your context via the `AITool` DI registrations (`AddAITools<T>()` preserves any `AITool`s registered before it). |
| 10 | **`ConfigureParameterBinding`** (custom parameter binders like `FromContext`, `IHttpContextAccessor`, etc.) | First-class runtime hook. | Not supported. Use `[FromServices]`/`[FromKeyedServices]` for DI binding; use `AIFunctionArguments` parameter for ad-hoc context. | Compile-time decision, same rationale as (9). |
| 11 | **`MarshalResult`** | First-class runtime hook for post-processing method results. | Not exposed. Methods return their result directly (`AIContent`-typed returns pass through; other types are serialized to JSON by the `AIFunction` infrastructure). | Same rationale as (9). |
| 12 | **`ExcludeResultSchema`** | Option to suppress `ReturnJsonSchema`. | Always emits `ReturnJsonSchema` for non-void returns. | No runtime option to set; file a request if needed. |
| 13 | **`[return: Description]`** | Propagated into `ReturnJsonSchema` as `"description"`. | Not currently propagated — `ReturnJsonSchema` reflects the CLR return type only. | Generator gap; tracked for a follow-up. The method-level `[Description]` *is* propagated into `AIFunction.Description`. |
| 14 | **`[DefaultValue]` attribute** | Read and used when no C# default is present; also overrides a C# default when both are specified. | Only C# defaults (`p = value`) are honored. | Generator gap; if you need `[DefaultValue]`, annotate with `= value` in the method signature instead. |
| 15 | **`[DisplayName]` attribute on methods** | Used as the tool name when no explicit name is provided. | Not consulted — use `[ExportAIFunction("explicit_name")]`. | Style choice: one attribute rather than two to look up. |
| 16 | **Name cleanup for local functions / lambdas** | Strips compiler-generated prefixes and `Async` suffixes, appends an ordinal for uniqueness. | Not applicable (see (1)). Tool names are exactly what you pass to `[ExportAIFunction(...)]` or the method name. | — |
| 17 | **`IAsyncEnumerable<T>` return type** | Buffered and JSON-serialized to an array. | Returned as-is. The consumer (e.g. `FunctionInvokingChatClient`) will JSON-serialize whatever type it receives, which may or may not work for `IAsyncEnumerable<T>`. | Generator gap; if you need this shape, materialize to an array/list in the method body. Tracked as a follow-up. |
| 18 | **Generic methods** | Supported if the generic arguments are bound. | Hard error `MAUIAI004` at compile time. | We'd need to pick concrete type arguments at generator time; easier to ask you to declare the specialized overload. |
| 19 | **`ref` / `out` / `in` / `ref readonly` parameters** | Skipped with a runtime error. | Hard error `MAUIAI004` at compile time. | Not meaningful for JSON-serialized arguments. |
| 20 | **`AIFunctionFactory.CreateDeclaration(...)`** | Produces a tool that is advertised but never invocable. | Not provided. | Every generated tool is invocable. If you need a declaration-only tool, register one via `AIFunctionFactory.CreateDeclaration` before `AddAITools<T>()`. |
| 21 | **`InvalidArguments_Throw`** tests (null `method`, null `target`, non-constructed generic method, etc.) | Validates factory arguments at `Create` time. | Not applicable — there is no factory to pass bad arguments to. | — |
| 22 | **Invocation result shape** | Default `MarshalResult` serializes the result to `JsonElement`. | Returns the CLR object as-is (consumers typically accept both). | Tests normalize via `JsonSerializer.SerializeToElement` before comparing, so both forms are equivalent for practical consumers. |

If you encounter a behavior not covered here, please open an issue — we consider any *silent* divergence a bug.
