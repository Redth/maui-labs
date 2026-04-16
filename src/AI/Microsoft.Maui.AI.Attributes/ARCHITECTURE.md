# Architecture — Microsoft.Maui.AI.Attributes

This is a compile-time, AOT-friendly, DI-aware replacement for `Microsoft.Extensions.AI.AIFunctionFactory.Create(MethodInfo, …)`. Where `ReflectionAIFunction` does all its work at runtime (reflect method, build marshalers, derive schema, `MethodInfo.Invoke` on each call), this package emits a sealed `AIFunction` subclass **per method** at build time.

## Compile-time pipeline

```
 Your code:                     Source generator (AIToolContextGenerator):
 [ExportAIFunction("x")]        1. Find partial classes deriving AIToolContext
 public async Task<Y> Foo(...)     via ForAttributeWithMetadataName.
 on service class S;            2. For each [AIToolSource(typeof(S))], scan S
                                   for [ExportAIFunction] methods.
 [AIToolSource(typeof(S))]      3. Classify each parameter (CancellationToken,
 partial class Ctx                 IServiceProvider, AIFunctionArguments,
     : AIToolContext;              [FromServices], [FromKeyedServices],
                                   [FromArguments], interface-infer-DI, or
                                   JSON-bound).
                                4. Emit:
                                   a) A sealed private nested AIFunction
                                      subclass inside the context class.
                                   b) Overrides of GetTools, RegisterTools,
                                      RegisterTools(key) that `new` those
                                      classes (wrapping in
                                      ApprovalRequiredAIFunction when the
                                      attribute says so).
```

### What the emitted `AIFunction` subclass looks like

For `[ExportAIFunction("get_plants")] List<Plant> GetPlants(IPlantDb db, string species, int max = 10, CancellationToken ct = default)`:

```csharp
private sealed class PlantCatalog_GetPlants_Tool : AIFunction
{
    private readonly IServiceProvider? _fallback;
    private static readonly Lazy<JsonElement> s_schema = new(BuildSchema);
    private static readonly Lazy<JsonElement?> s_returnSchema = new(BuildReturnSchema);

    // Only populated with names bound from DI/special types, so they're excluded
    // from the schema via AIJsonSchemaCreateOptions.IncludeParameter.
    private static readonly HashSet<string> s_schemaExcludedParameters = new()
    {
        "db", // interface parameter → inferred DI
    };

    public PlantCatalog_GetPlants_Tool(IServiceProvider? fallback = null) => _fallback = fallback;

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
        var __provider = AIToolContext.Helpers.RequireServices(arguments, _fallback);
        var __service = __provider.GetRequiredService<PlantCatalog>();
        var __arg_db = __provider.GetRequiredService<IPlantDb>();         // inferred DI
        var __arg_species = AIToolContext.Helpers.GetRequiredArg<string>(arguments, "species");
        var __arg_max = AIToolContext.Helpers.GetOptionalArg<int>(arguments, "max", 10);
        var __arg_ct = cancellationToken;
        var __result = __service.GetPlants(__arg_db, __arg_species, __arg_max, __arg_ct);
        return __result;
    }
}
```

All binding happens with statically-typed generic helpers. No `MethodInfo.Invoke`, no `AIFunctionFactory.Create`, no reflection on the hot path.

## Runtime flow

```
 FunctionInvokingChatClient (from Microsoft.Extensions.AI)
     │
     │ sets AIFunctionArguments.Services = _functionInvocationServices
     │ (whichever IServiceProvider it was constructed with)
     ▼
 AIFunction.InvokeAsync(args, ct)
     ▼
 generated-tool.InvokeCoreAsync
     │ — uses args.Services ?? fallback
     │ — resolves the host service (S) via GetRequiredService<S>()
     │ — resolves each [FromServices]/[FromKeyedServices]/interface param
     │ — reads JSON-bound params via Helpers.Get{Required,Optional}Arg<T>
     ▼
 calls your method directly
     │
     ▼
 returns the raw CLR object; FunctionInvokingChatClient JSON-serializes it
```

## Scope policy

**We never create scopes.** The developer chooses the scope by how they register `IChatClient`:

- Register singleton → root provider flows everywhere → scoped services fail under `ValidateScopes=true`, behave like singletons otherwise. Rarely what you want for per-session state.
- Register scoped (or build the client per-session yourself) → each scope's `IServiceProvider` flows through `FunctionInvokingChatClient` → scoped services are fresh per session and consistent across tool calls within the session. Dispose the scope on "New Chat" for a full reset.

The fallback provider (captured at `AddAITools`/`GetTools` time) only kicks in when `args.Services` is null — typically in unit-test scenarios where tests call `InvokeAsync` directly without a `FunctionInvokingChatClient`.

If both `args.Services` and the fallback are null, `AIToolContext.Helpers.RequireServices` throws `InvalidOperationException` with a descriptive message.

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
| `MAUIAI001` | Info | Parameter classified as inferred DI (interface/abstract, no `[FromServices]`/`[FromKeyedServices]`/`[FromArguments]`). |
| `MAUIAI002` | Warning | Parameter type is unlikely to round-trip through JSON (e.g. delegates, pointers). |
| `MAUIAI003` | Warning | `[AIToolSource]` references a type with no exportable methods. |
| `MAUIAI004` | Error | Unsupported signature (generic method, `ref`/`out`/`in` parameter). |

## Comparison with `AIFunctionFactory.Create`

| Behavior | `AIFunctionFactory.Create(MethodInfo, target, opts)` | This package |
|---|---|---|
| Build cost per tool | One-time reflection + marshaler build | Zero (emitted at compile time) |
| Per-invocation overhead | Reflected marshalers + `MethodInfo.Invoke` | Direct method call |
| Schema build | `CreateFunctionJsonSchema` on each factory call | `CreateFunctionJsonSchema` once (cached in `Lazy<>`) |
| DI of parameters | Only via `ConfigureParameterBinding` in options | Built in: `[FromServices]`, `[FromKeyedServices]`, interface-inference, `[FromArguments]` |
| Scoping | Caller decides | Caller decides (same) |
| AOT | Not clean | Hot path is clean; schema build is pending |

The input API (`[ExportAIFunction]`, `[AIToolSource]`, `AIToolContext`, `AddAITools<T>()`) is a small, attribute-driven surface designed for apps that want many tools registered declaratively without writing plumbing.
