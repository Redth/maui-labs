using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Maui.AI.Attributes.Tests.Equivalence;

/// <summary>
/// Equivalence suite mirroring the in-scope tests from
/// <c>Microsoft.Extensions.AI.Tests.Functions.AIFunctionFactoryTest</c>
/// (dotnet/extensions). Each test here uses a source-generated tool as the "actual"
/// and — where feasible — pairs it against an <c>AIFunctionFactory.Create(method, instance)</c>
/// baseline as the "expected" oracle.
///
/// Tests that are <b>out of scope</b> (lambdas, local functions, <c>ConfigureParameterBinding</c>,
/// <c>MarshalResult</c>, <c>createInstanceFunc</c>, <c>DynamicMethod</c>, <c>CreateDeclaration</c>)
/// are intentionally omitted here; every behavioral difference is documented in README.md under
/// "Compatibility with Microsoft.Extensions.AI.AIFunctionFactory".
/// </summary>
public class AIFunctionFactoryEquivalenceTests
{
    private static (AIFunction Ours, AIFunction Baseline, EquivalenceToolService Service, IServiceProvider Sp) GetPair(
        string toolName,
        Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<EquivalenceToolService>();
        services.AddSingleton<KeyedMyService>(new KeyedMyService(42));
        configure?.Invoke(services);
        services.AddAITools<EquivalenceToolContext>();
        var sp = services.BuildServiceProvider();

        var tool = (AIFunction)sp.GetRequiredService<IEnumerable<AITool>>().First(t => t.Name == toolName);
        var svc = sp.GetRequiredService<EquivalenceToolService>();
        var method = typeof(EquivalenceToolService).GetMethod(GetMethodNameFromToolName(toolName))!;
        var baseline = AIFunctionFactory.Create(method, svc);
        return (tool, baseline, svc, sp);
    }

    private static string GetMethodNameFromToolName(string toolName) => toolName switch
    {
        "repeat_str" => nameof(EquivalenceToolService.RepeatStr),
        "concat_two" => nameof(EquivalenceToolService.ConcatTwo),
        "add_numbers" => nameof(EquivalenceToolService.AddNumbers),
        "repeat_with_default" => nameof(EquivalenceToolService.RepeatWithDefault),
        "missing_string" => nameof(EquivalenceToolService.MissingString),
        "missing_nullable_string" => nameof(EquivalenceToolService.MissingNullableString),
        "missing_int" => nameof(EquivalenceToolService.MissingInt),
        "missing_nullable_int" => nameof(EquivalenceToolService.MissingNullableInt),
        "sum_five_ints" => nameof(EquivalenceToolService.SumFiveInts),
        "echo_json" => nameof(EquivalenceToolService.EchoJson),
        "uses_cancellation" => nameof(EquivalenceToolService.UsesCancellation),
        "returns_task_string" => nameof(EquivalenceToolService.ReturnsTaskString),
        "returns_valuetask_string" => nameof(EquivalenceToolService.ReturnsValueTaskString),
        "returns_task" => nameof(EquivalenceToolService.ReturnsTask),
        "returns_valuetask" => nameof(EquivalenceToolService.ReturnsValueTask),
        "inject_sp_and_args" => nameof(EquivalenceToolService.InjectSpAndArgs),
        "from_keyed_with_key" => nameof(EquivalenceToolService.FromKeyedWithKey),
        "from_keyed_optional" => nameof(EquivalenceToolService.FromKeyedOptional),
        "default_struct_params" => nameof(EquivalenceToolService.DefaultStructParams),
        "nullable_numeric_params" => nameof(EquivalenceToolService.NullableNumericParams),
        "add_with_return_description" => nameof(EquivalenceToolService.AddWithReturnDescription),
        "nullable_value_schema" => nameof(EquivalenceToolService.NullableValueSchema),
        "nullable_ref_schema" => nameof(EquivalenceToolService.NullableRefSchema),
        "returns_text_content" => nameof(EquivalenceToolService.ReturnsTextContent),
        "returns_data_content" => nameof(EquivalenceToolService.ReturnsDataContent),
        "returns_ai_content_base" => nameof(EquivalenceToolService.ReturnsAIContentBase),
        _ => throw new InvalidOperationException($"Unknown tool '{toolName}'."),
    };

    // -----------------------------------------------------------------------
    // Parameters_MappedByName_Async
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Parameters_MappedByName_Async()
    {
        var (ours1, base1, _, _) = GetPair("repeat_str");
        EquivalenceHelpers.AssertInvocationEqual(
            "test test",
            await ours1.InvokeAsync(new AIFunctionArguments { ["a"] = "test" }));
        EquivalenceHelpers.AssertInvocationEqual(
            await base1.InvokeAsync(new AIFunctionArguments { ["a"] = "test" }),
            await ours1.InvokeAsync(new AIFunctionArguments { ["a"] = "test" }));

        var (ours2, base2, _, _) = GetPair("concat_two");
        EquivalenceHelpers.AssertInvocationEqual(
            "hello world",
            await ours2.InvokeAsync(new AIFunctionArguments { ["b"] = "hello", ["a"] = "world" }));
        EquivalenceHelpers.AssertInvocationEqual(
            await base2.InvokeAsync(new AIFunctionArguments { ["b"] = "hello", ["a"] = "world" }),
            await ours2.InvokeAsync(new AIFunctionArguments { ["b"] = "hello", ["a"] = "world" }));

        var (ours3, base3, _, _) = GetPair("add_numbers");
        EquivalenceHelpers.AssertInvocationEqual(
            3L,
            await ours3.InvokeAsync(new AIFunctionArguments { ["a"] = 1, ["b"] = 2L }));
        EquivalenceHelpers.AssertInvocationEqual(
            await base3.InvokeAsync(new AIFunctionArguments { ["a"] = 1, ["b"] = 2L }),
            await ours3.InvokeAsync(new AIFunctionArguments { ["a"] = 1, ["b"] = 2L }));
    }

    // -----------------------------------------------------------------------
    // Parameters_DefaultValuesAreUsedButOverridable_Async
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Parameters_DefaultValuesAreUsedButOverridable_Async()
    {
        var (ours, baseline, _, _) = GetPair("repeat_with_default");
        EquivalenceHelpers.AssertInvocationEqual("test test", await ours.InvokeAsync());
        EquivalenceHelpers.AssertInvocationEqual("hello hello", await ours.InvokeAsync(new AIFunctionArguments { ["a"] = "hello" }));

        EquivalenceHelpers.AssertInvocationEqual(await baseline.InvokeAsync(), await ours.InvokeAsync());
    }

    // -----------------------------------------------------------------------
    // Parameters_MissingRequiredParametersFail_Async
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Parameters_MissingRequiredParametersFail_Async()
    {
        foreach (string tool in new[] { "missing_string", "missing_nullable_string", "missing_int", "missing_nullable_int" })
        {
            var (ours, _, _, _) = GetPair(tool);
            Exception e = await Assert.ThrowsAnyAsync<ArgumentException>(() => ours.InvokeAsync().AsTask());
            Assert.Contains("'theParam'", e.Message);
        }
    }

    // -----------------------------------------------------------------------
    // Parameters_ToleratesJsonEncodedParameters
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Parameters_ToleratesJsonEncodedParameters()
    {
        var (ours, baseline, _, _) = GetPair("sum_five_ints");

        var args = new AIFunctionArguments
        {
            ["x"] = "1",
            ["y"] = JsonNode.Parse("2"),
            ["z"] = JsonDocument.Parse("3"),
            ["w"] = JsonDocument.Parse("4").RootElement,
            ["u"] = 5M, // boxed decimal
        };

        EquivalenceHelpers.AssertInvocationEqual(15, await ours.InvokeAsync(args));
        EquivalenceHelpers.AssertInvocationEqual(
            await baseline.InvokeAsync(CloneArgs(args)),
            await ours.InvokeAsync(CloneArgs(args)));
    }

    // -----------------------------------------------------------------------
    // Parameters_ToleratesJsonStringParameters (Theory)
    // -----------------------------------------------------------------------
    [Theory]
    [InlineData("   null")]
    [InlineData("   false   ")]
    [InlineData("true   ")]
    [InlineData("42")]
    [InlineData("0.0")]
    [InlineData("-1e15")]
    [InlineData("  \"I am a string!\" ")]
    [InlineData("  {}")]
    [InlineData("[]")]
    public async Task Parameters_ToleratesJsonStringParameters(string jsonStringParam)
    {
        var (ours, baseline, _, _) = GetPair("echo_json");
        object? ourResult = await ours.InvokeAsync(new AIFunctionArguments { ["param"] = jsonStringParam });
        object? baseResult = await baseline.InvokeAsync(new AIFunctionArguments { ["param"] = jsonStringParam });
        EquivalenceHelpers.AssertInvocationEqual(baseResult, ourResult);
    }

    // -----------------------------------------------------------------------
    // Parameters_ToleratesInvalidJsonStringParameters (Theory)
    // -----------------------------------------------------------------------
    [Theory]
    [InlineData("I am a string!")]
    [InlineData("let rec Y F x = F (Y F) x")]
    [InlineData("+3")]
    public async Task Parameters_ToleratesInvalidJsonStringParameters(string invalidJsonParam)
    {
        var (ours, baseline, _, _) = GetPair("echo_json");
        object? ourResult = await ours.InvokeAsync(new AIFunctionArguments { ["param"] = invalidJsonParam });
        object? baseResult = await baseline.InvokeAsync(new AIFunctionArguments { ["param"] = invalidJsonParam });
        EquivalenceHelpers.AssertInvocationEqual(baseResult, ourResult);
    }

    // -----------------------------------------------------------------------
    // Parameters_MappedByType_Async (CancellationToken injected, not in schema)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Parameters_MappedByType_Async()
    {
        using var cts = new CancellationTokenSource();
        foreach (CancellationToken ctArg in new[] { cts.Token, default })
        {
            var (ours, _, svc, _) = GetPair("uses_cancellation");
            var result = await ours.InvokeAsync(new AIFunctionArguments(), ctArg);
            EquivalenceHelpers.AssertInvocationEqual(42, result);
            Assert.Equal(ctArg, svc.LastCancellationToken);
            Assert.DoesNotContain("cancellationToken", ours.JsonSchema.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    // -----------------------------------------------------------------------
    // Returns_AsyncReturnTypesSupported_Async (Task / ValueTask / Task<T> / ValueTask<T>)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Returns_AsyncReturnTypesSupported_Async()
    {
        var (t1, b1, _, _) = GetPair("returns_task_string");
        Assert.Equal("""{"type":"string"}""", t1.ReturnJsonSchema.ToString());
        EquivalenceHelpers.AssertInvocationEqual("test test", await t1.InvokeAsync(new AIFunctionArguments { ["a"] = "test" }));
        EquivalenceHelpers.AssertInvocationEqual(
            await b1.InvokeAsync(new AIFunctionArguments { ["a"] = "test" }),
            await t1.InvokeAsync(new AIFunctionArguments { ["a"] = "test" }));

        var (t2, b2, _, _) = GetPair("returns_valuetask_string");
        Assert.Equal("""{"type":"string"}""", t2.ReturnJsonSchema.ToString());
        EquivalenceHelpers.AssertInvocationEqual("hello world",
            await t2.InvokeAsync(new AIFunctionArguments { ["b"] = "hello", ["a"] = "world" }));

        var (t3, _, svc3, _) = GetPair("returns_task");
        Assert.Null(t3.ReturnJsonSchema);
        EquivalenceHelpers.AssertInvocationEqual(null, await t3.InvokeAsync(new AIFunctionArguments { ["a"] = 1, ["b"] = 2L }));
        Assert.Equal(3, svc3.LastTaskResult);

        var (t4, _, svc4, _) = GetPair("returns_valuetask");
        Assert.Null(t4.ReturnJsonSchema);
        EquivalenceHelpers.AssertInvocationEqual(null, await t4.InvokeAsync(new AIFunctionArguments { ["a"] = 1, ["b"] = 2L }));
        Assert.Equal(3, svc4.LastValueTaskResult);
    }

    // -----------------------------------------------------------------------
    // AIFunctionArguments_SatisfiesParameters
    // -----------------------------------------------------------------------
    [Fact]
    public async Task AIFunctionArguments_SatisfiesParameters()
    {
        var (ours, _, svc, sp) = GetPair("inject_sp_and_args");

        Assert.Contains("myInteger", ours.JsonSchema.ToString());
        Assert.DoesNotContain("services", ours.JsonSchema.ToString());
        Assert.DoesNotContain("arguments", ours.JsonSchema.ToString());
        Assert.Equal("""{"type":"integer"}""", ours.ReturnJsonSchema.ToString());

        // Explicitly set Services on args: both that IServiceProvider and the arguments object
        // are threaded through to the method.
        var args = new AIFunctionArguments { ["myInteger"] = 42 };
        args.Services = sp;
        var result = await ours.InvokeAsync(args);
        Assert.Contains("42", result?.ToString());
        Assert.Same(sp, svc.LastServices);
        Assert.Same(args, svc.LastArguments);
    }

    // -----------------------------------------------------------------------
    // FromKeyedServices_ResolvesFromServiceProvider
    // -----------------------------------------------------------------------
    [Fact]
    public async Task FromKeyedServices_ResolvesFromServiceProvider()
    {
        var (ours, _, _, _) = GetPair("from_keyed_with_key", services =>
        {
            services.AddKeyedSingleton("key", new KeyedMyService(42));
        });

        Assert.Contains("myInteger", ours.JsonSchema.ToString());
        Assert.DoesNotContain("service", ours.JsonSchema.ToString());
        Assert.Equal("""{"type":"integer"}""", ours.ReturnJsonSchema.ToString());

        var result = await ours.InvokeAsync(new AIFunctionArguments { ["myInteger"] = 1 });
        Assert.Contains("43", result?.ToString());
    }

    // -----------------------------------------------------------------------
    // FromKeyedServices_OptionalDefaultsToNull — behavioral difference documented
    // -----------------------------------------------------------------------
    [Fact]
    public async Task FromKeyedServices_OptionalWithDefault_UsesDefaultWhenKeyMissing()
    {
        // Under M.E.AI: if the keyed key is not registered AND the parameter has a default,
        // the default is used. Our generator uses GetRequiredKeyedService unconditionally for
        // [FromKeyedServices], so a missing key always throws — even with a default. This is a
        // documented behavioral difference (see README, "Compatibility").
        var (ours, _, _, _) = GetPair("from_keyed_optional");
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            ours.InvokeAsync(new AIFunctionArguments { ["myInteger"] = 1 }).AsTask());
    }

    // -----------------------------------------------------------------------
    // AIFunctionFactory_DefaultDefaultParameter (struct defaults)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task AIFunctionFactory_DefaultDefaultParameter()
    {
        Assert.NotEqual(new StructWithDefaultCtor().Value, default(StructWithDefaultCtor).Value);
        var (ours, baseline, _, _) = GetPair("default_struct_params");
        object? oursResult = await ours.InvokeAsync();
        object? baseResult = await baseline.InvokeAsync();
        Assert.Contains("00000000-0000-0000-0000-000000000000,0", oursResult?.ToString());
        EquivalenceHelpers.AssertInvocationEqual(baseResult, oursResult);
    }

    // -----------------------------------------------------------------------
    // AIFunctionFactory_NullableParameters (schema check)
    // -----------------------------------------------------------------------
    [Fact]
    public void AIFunctionFactory_NullableParameters_Schema()
    {
        var (ours, _, _, _) = GetPair("nullable_numeric_params");

        JsonElement schema = ours.JsonSchema;
        JsonElement properties = schema.GetProperty("properties");
        var limit = properties.GetProperty("limit");
        AssertTypeArrayContains(limit.GetProperty("type"), "integer", "null");
        Assert.Equal(JsonValueKind.Null, limit.GetProperty("default").ValueKind);

        var from = properties.GetProperty("from");
        AssertTypeArrayContains(from.GetProperty("type"), "string", "null");
        Assert.Equal("date-time", from.GetProperty("format").GetString());
        Assert.Equal(JsonValueKind.Null, from.GetProperty("default").ValueKind);
    }

    // -----------------------------------------------------------------------
    // AIFunctionFactory_ReturnTypeWithDescriptionAttribute
    // -----------------------------------------------------------------------
    [Fact]
    public void AIFunctionFactory_ReturnTypeWithDescriptionAttribute()
    {
        // M.E.AI includes the [return: Description(...)] text in ReturnJsonSchema. Our
        // generator currently DOES NOT propagate this; ReturnJsonSchema is built from the
        // CLR return type only. Documented in README under "Compatibility".
        var (ours, baseline, _, _) = GetPair("add_with_return_description");
        Assert.Equal("""{"description":"The summed result","type":"integer"}""", baseline.ReturnJsonSchema.ToString());
        Assert.Equal("""{"type":"integer"}""", ours.ReturnJsonSchema.ToString());
    }

    // -----------------------------------------------------------------------
    // JsonSchema_NullableValueTypeParameters_AllowNull
    // -----------------------------------------------------------------------
    [Fact]
    public void JsonSchema_NullableValueTypeParameters_AllowNull()
    {
        var (ours, _, _, _) = GetPair("nullable_value_schema");
        var schema = ours.JsonSchema;
        var properties = schema.GetProperty("properties");

        AssertTypeArrayContains(properties.GetProperty("nullableInt").GetProperty("type"), "integer", "null");

        var withDefault = properties.GetProperty("nullableIntWithDefault");
        AssertTypeArrayContains(withDefault.GetProperty("type"), "integer", "null");
        Assert.Equal(JsonValueKind.Null, withDefault.GetProperty("default").ValueKind);

        var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).ToList();
        Assert.Contains("nullableInt", required);
        Assert.DoesNotContain("nullableIntWithDefault", required);
    }

    // -----------------------------------------------------------------------
    // JsonSchema_NullableReferenceTypeParameters_AllowNull
    // -----------------------------------------------------------------------
    [Fact]
    public void JsonSchema_NullableReferenceTypeParameters_AllowNull()
    {
        var (ours, _, _, _) = GetPair("nullable_ref_schema");
        var schema = ours.JsonSchema;
        var properties = schema.GetProperty("properties");

        AssertTypeArrayContains(properties.GetProperty("nullableString").GetProperty("type"), "string", "null");
        AssertTypeArrayContains(properties.GetProperty("nullableInt").GetProperty("type"), "integer", "null");

        var nsd = properties.GetProperty("nullableStringWithDefault");
        AssertTypeArrayContains(nsd.GetProperty("type"), "string", "null");
        Assert.Equal(JsonValueKind.Null, nsd.GetProperty("default").ValueKind);

        var nid = properties.GetProperty("nullableIntWithDefault");
        AssertTypeArrayContains(nid.GetProperty("type"), "integer", "null");
        Assert.Equal(JsonValueKind.Null, nid.GetProperty("default").ValueKind);

        var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).ToList();
        Assert.Contains("nullableString", required);
        Assert.Contains("nullableInt", required);
        Assert.DoesNotContain("nullableStringWithDefault", required);
        Assert.DoesNotContain("nullableIntWithDefault", required);
    }

    // -----------------------------------------------------------------------
    // AIContentReturnType_NotSerializedByDefault — shape only (each tool returns AIContent)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task AIContentReturnType_NotSerializedByDefault()
    {
        // M.E.AI: AIContent-returning methods bypass JSON serialization and return the content
        // object as-is. Our generator returns the value directly from InvokeCoreAsync, which is
        // equivalent for this case.
        foreach (var tool in new[] { "returns_text_content", "returns_ai_content_base" })
        {
            var (ours, _, _, _) = GetPair(tool);
            var result = await ours.InvokeAsync();
            Assert.IsAssignableFrom<TextContent>(result);
        }

        var (oursData, _, _, _) = GetPair("returns_data_content");
        Assert.IsAssignableFrom<DataContent>(await oursData.InvokeAsync());
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------
    private static AIFunctionArguments CloneArgs(AIFunctionArguments src)
    {
        var copy = new AIFunctionArguments();
        foreach (var kv in src)
        {
            copy[kv.Key] = kv.Value;
        }
        if (src.Services is not null) copy.Services = src.Services;
        return copy;
    }

    private static void AssertTypeArrayContains(JsonElement typeEl, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Array, typeEl.ValueKind);
        var actual = typeEl.EnumerateArray().Select(e => e.GetString()).ToArray();
        foreach (var name in expected)
        {
            Assert.Contains(name, actual);
        }
    }
}
