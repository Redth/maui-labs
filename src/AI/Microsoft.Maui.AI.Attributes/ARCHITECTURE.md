# Architecture — Microsoft.Maui.AI.Attributes

This is a compile-time, AOT-friendly, DI-aware replacement for `Microsoft.Extensions.AI.AIFunctionFactory.Create(MethodInfo, …)`. Where `ReflectionAIFunction` does all its work at runtime (reflect method, build marshalers, derive schema, `MethodInfo.Invoke` on each call), this package emits a sealed `AIFunction` subclass **per method** at build time, plus a `Default` singleton on the tool context.

## Compile-time pipeline

```
 Your code:                     Source generator (AIToolContextGenerator):
 [ExportAIFunction("x")]        1. Find partial classes deriving AIToolContext
 public Task<Y> Foo(...)           via ForAttributeWithMetadataName.
 on service class S;            2. For each [AIToolSource(typeof(S))], scan S
                                   for [ExportAIFunction] methods (instance OR static).
 [AIToolSource(typeof(S))]      3. Classify each parameter (CancellationToken,
 partial class Ctx                 IServiceProvider, AIFunctionArguments,
     : AIToolContext;              [FromServices], [FromKeyedServices],
                                   or JSON-bound).
                                4. Emit:
                                   a) A sealed private nested AIFunction subclass
                                      per method.
                                   b) A static `Default` singleton on the context.
                                   c) An override of GetTools() that `new`s each
                                      tool (wrapping in ApprovalRequiredAIFunction
                                      when the attribute says so).
```

### What the emitted `AIFunction` subclass looks like (instance method)

For `[ExportAIFunction("get_plants")] List<Plant> GetPlants(IPlantDb db, string species, int max = 10, CancellationToken ct = default)`:

```csharp
private sealed class PlantCatalog_GetPlants_Tool : AIFunction
{
    private static readonly Lazy<JsonElement> s_schema = new(BuildSchema);
    private static readonly Lazy<JsonElement?> s_returnSchema = new(BuildReturnSchema);

    private static readonly HashSet<string> s_schemaExcludedParameters = new()
    {
        "db", // [FromServices] IPlantDb db
    };

    public override string Name => "get_plants";
    public override string Description => "...";
    public override JsonElement JsonSchema => s_schema.Value;
    public override JsonElement? ReturnJsonSchema => s_returnSchema.Value;

    private static MethodInfo GetTargetMethod() => /* typeof(PlantCatalog).GetMethod(...) */;

    private static JsonElement BuildSchema() =>
        AIJsonUtilities.CreateFunctionJsonSchema(
            GetTargetMethod(),
            title: string.Empty, description: string.Empty,
            inferenceOptions: new AIJsonSchemaCreateOptions
            {
                IncludeParameter = static p => !s_schemaExcludedParameters.Contains(p.Name!),
            });

    private static JsonElement? BuildReturnSchema() =>
        AIJsonUtilities.CreateJsonSchema(typeof(List<Plant>));

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var __provider = AIToolContext.Helpers.RequireServices(arguments);
        var __service = __provider.GetRequiredService<PlantCatalog>();
        var __arg_db = __provider.GetRequiredService<IPlantDb>();         // [FromServices]
        var __arg_species = AIToolContext.Helpers.GetRequiredArg<string>(arguments, "species");
        var __arg_max = AIToolContext.Helpers.GetOptionalArg<int>(arguments, "max", 10);
        var __arg_ct = cancellationToken;
        var __result = __service.GetPlants(__arg_db, __arg_species, __arg_max, __arg_ct);
        return __result;
    }
}
```

### What the emitted subclass looks like (static method, no DI)

For `[ExportAIFunction("say_hello")] public static string SayHello(string name)`:

```csharp
private sealed class GreetingService_SayHello_Tool : AIFunction
{
    public override string Name => "say_hello";
    // … schema as above …

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        // No __provider, no __service — static call.
        var __arg_name = AIToolContext.Helpers.GetRequiredArg<string>(arguments, "name");
        var __result = global::MyApp.GreetingService.SayHello(__arg_name);
        return __result;
    }
}
```

The provider is only required when the tool actually needs it. A static method with zero DI parameters can be invoked with `new AIFunctionArguments(dict)` and no `Services` set.

### What the emitted context looks like

```csharp
public partial class GardenTools
{
    public static GardenTools Default { get; } = new GardenTools();

    public override IReadOnlyList<AITool> GetTools()
        => new AITool[] { new PlantCatalog_GetPlants_Tool(), /* … */ };
}
```

`Default` is the canonical singleton. `GetTools()` allocates the array on each call but the underlying `AIFunction` instances are stateless and safe to reuse. Most callers cache the result once at startup.

All binding happens with statically-typed generic helpers. No `MethodInfo.Invoke`, no `AIFunctionFactory.Create`, no reflection on the hot path.

## Runtime flow

```
 FunctionInvokingChatClient (from Microsoft.Extensions.AI)
     │
     │ sets AIFunctionArguments.Services = _functionInvocationServices
     │ (the IServiceProvider passed to .Build(sp))
     ▼
 AIFunction.InvokeAsync(args, ct)
     ▼
 generated-tool.InvokeCoreAsync
     │ — if any DI parameter exists: __provider = RequireServices(args) (throws if null)
     │ — if instance method: __service = __provider.GetRequiredService<S>()
     │ — resolves each [FromServices]/[FromKeyedServices] param
     │ — reads JSON-bound params via Helpers.Get{Required,Optional}Arg<T>
     ▼
 calls your method directly (instance or static)
     │
     ▼
 returns the raw CLR object; FunctionInvokingChatClient JSON-serializes it
```

## Scope policy

**We never create scopes.** The developer chooses the scope by how they build `IChatClient`:

- `Build(app.Services)` once at startup → root provider flows everywhere → scoped services fail under `ValidateScopes=true`, behave like singletons otherwise. Rarely what you want for per-session state.
- `Build(scope.ServiceProvider)` per chat session → each scope's provider flows through `FunctionInvokingChatClient` → scoped services are fresh per session and consistent across tool calls within the session. Dispose the scope on "New Chat" for a full reset.

If `args.Services` is null when the tool needs it, `AIToolContext.Helpers.RequireServices` throws `InvalidOperationException` with a message pointing at `UseFunctionInvocation().Build(sp)` — or suggesting the method be made `static` if no DI is needed.

## What's still reflective (and what isn't)

| Piece | AOT-clean? | Notes |
|---|---|---|
| Tool discovery & registration | Yes | Emitted at compile time. |
| Parameter classification | Yes | Emitted at compile time. |
| Service resolution | Yes | `IServiceProvider.GetRequiredService<T>` is an instantiated generic. |
| JSON parameter binding | Partial | `JsonSerializer.Deserialize<T>(…)` paths require type metadata; pair with a `JsonSerializerContext` for AOT. |
| Parameter JSON schema | **No (first iteration)** | `AIJsonUtilities.CreateFunctionJsonSchema` reflects over the method once per tool at warmup, via `Lazy<JsonElement>`. |
| Invocation | Yes | Method call is direct. |

Follow-up: emit the JSON schema as a pre-computed string constant at generator time so the runtime never calls `CreateFunctionJsonSchema`. That removes the last reflective piece.

## Diagnostics

| ID | Severity | Description |
|---|---|---|
| `MAUIAI002` | Warning | Parameter type is unlikely to round-trip through JSON (e.g. delegates, pointers). |
| `MAUIAI003` | Warning | `[AIToolSource]` references a type with no exportable methods. |
| `MAUIAI004` | Error | Unsupported signature (generic method, `ref`/`out`/`in` parameter). |

## Comparison with `AIFunctionFactory.Create`

| Behavior | `AIFunctionFactory.Create(MethodInfo, target, opts)` | This package |
|---|---|---|
| Build cost per tool | One-time reflection + marshaler build | Zero (emitted at compile time) |
| Per-invocation overhead | Reflected marshalers + `MethodInfo.Invoke` | Direct method call |
| Schema build | `CreateFunctionJsonSchema` on each factory call | `CreateFunctionJsonSchema` once (cached in `Lazy<>`) |
| DI of parameters | Only via `ConfigureParameterBinding` in options | Built-in: `[FromServices]`, `[FromKeyedServices]` |
| Static methods | Throws | Supported (no DI required) |
| Scoping | Caller decides | Caller decides (same) |
| AOT | Not clean | Hot path is clean; schema build is pending |

The input API is a small, attribute-driven surface — `[ExportAIFunction]`, `[AIToolSource]`, `AIToolContext.GetTools()` — designed for apps that want many tools available declaratively without writing plumbing.
