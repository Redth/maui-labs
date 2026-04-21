using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Maui.AI.Attributes.Generators.Tests;

/// <summary>
/// Tests that the generator produces compilable output and emits the expected diagnostics.
/// These complement the snapshot tests by asserting structural properties of the output
/// (tool count, diagnostic IDs, compilation success) rather than exact text.
/// </summary>
public class GeneratorCompilationTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private static (ImmutableArray<Diagnostic> GeneratorDiags, ImmutableArray<Diagnostic> CompilationDiags, Compilation Output) RunAndCompile(string source)
    {
        var driver = GeneratorTestHarness.RunGenerator(source, out var output, out var diags);
        var generatorDiags = driver.GetRunResult().Diagnostics;
        var compilationDiags = output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        return (generatorDiags, compilationDiags, output);
    }

    private static void AssertCleanCompilation(string source)
    {
        var (genDiags, compDiags, _) = RunAndCompile(source);
        Assert.Empty(genDiags.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(compDiags);
    }

    private static int CountGeneratedSources(string source)
    {
        var driver = GeneratorTestHarness.RunGenerator(source, out _, out _);
        return driver.GetRunResult().GeneratedTrees.Length;
    }

    // ── Static class scenarios ──────────────────────────────────────────

    [Fact]
    public void StaticClass_WithStaticMethods_CompilesCleanly()
        => AssertCleanCompilation(Inputs.StaticClassWithStaticMethods);

    [Fact]
    public void StaticClass_WithStaticMethods_EmitsTwoTools()
    {
        var (_, _, output) = RunAndCompile(Inputs.StaticClassWithStaticMethods);
        var generated = output.SyntaxTrees.Last().ToString();
        Assert.Contains("new MathHelper_Add_Tool()", generated);
        Assert.Contains("new MathHelper_Negate_Tool()", generated);
    }

    [Fact]
    public void StaticMethodOnNonStaticClass_CompilesCleanly()
        => AssertCleanCompilation(Inputs.StaticMethodOnNonStaticClass);

    [Fact]
    public void StaticMethodOnNonStaticClass_EmitsBothStaticAndInstance()
    {
        var (_, _, output) = RunAndCompile(Inputs.StaticMethodOnNonStaticClass);
        var generated = output.SyntaxTrees.Last().ToString();
        // Static method: class name derived from method name "Echo"
        Assert.Contains("Utility_Echo_Tool", generated);
        // Instance method: class name derived from method name "EchoInstance"
        Assert.Contains("Utility_EchoInstance_Tool", generated);
    }

    [Fact]
    public void StaticMethodWithFromServices_CompilesCleanly()
        => AssertCleanCompilation(Inputs.StaticMethodWithFromServices);

    [Fact]
    public void StaticMethodWithFromServices_InlinesServiceProviderCheck()
    {
        var (_, _, output) = RunAndCompile(Inputs.StaticMethodWithFromServices);
        var generated = output.SyntaxTrees.Last().ToString();
        Assert.Contains("arguments.Services ?? throw new", generated);
        Assert.Contains("GetRequiredService<global::Sample.ILogger>()", generated);
    }

    [Fact]
    public void StaticMethodNoDI_CompilesCleanly()
        => AssertCleanCompilation(Inputs.StaticMethodNoDI);

    [Fact]
    public void StaticMethodNoDI_DoesNotInlineServiceProviderCheck()
    {
        var (_, _, output) = RunAndCompile(Inputs.StaticMethodNoDI);
        var generated = output.SyntaxTrees.Last().ToString();
        Assert.DoesNotContain("arguments.Services", generated);
        Assert.DoesNotContain("__provider", generated);
    }

    [Fact]
    public void StaticClassWithFromServicesAndProperty_CompilesCleanly()
        => AssertCleanCompilation(Inputs.StaticClassWithFromServicesAndProperty);

    // ── Interface scenarios ─────────────────────────────────────────────

    [Fact]
    public void InterfaceAsSourceType_CompilesCleanly()
        => AssertCleanCompilation(Inputs.InterfaceAsSourceType);

    [Fact]
    public void InterfaceAsSourceType_ResolvesViaInterface()
    {
        var (_, _, output) = RunAndCompile(Inputs.InterfaceAsSourceType);
        var generated = output.SyntaxTrees.Last().ToString();
        Assert.Contains("GetRequiredService<global::Sample.IOrderService>()", generated);
    }

    [Fact]
    public void InterfaceWithFromServices_CompilesCleanly()
        => AssertCleanCompilation(Inputs.InterfaceWithFromServices);

    [Fact]
    public void InterfaceWithFromServices_ExcludesFromServicesFromSchema()
    {
        var (_, _, output) = RunAndCompile(Inputs.InterfaceWithFromServices);
        var generated = output.SyntaxTrees.Last().ToString();
        Assert.Contains("\"cart\"", generated); // excluded from schema
    }

    [Fact]
    public void InterfaceWithProperty_CompilesCleanly()
        => AssertCleanCompilation(Inputs.InterfaceWithProperty);

    [Fact]
    public void InterfaceWithProperty_UsesGetProperty()
    {
        var (_, _, output) = RunAndCompile(Inputs.InterfaceWithProperty);
        var generated = output.SyntaxTrees.Last().ToString();
        Assert.Contains("GetProperty(\"Items\"", generated);
    }

    [Fact]
    public void InterfaceWithApproval_CompilesCleanly()
        => AssertCleanCompilation(Inputs.InterfaceWithApproval);

    [Fact]
    public void InterfaceWithApproval_WrapsApprovalRequired()
    {
        var (_, _, output) = RunAndCompile(Inputs.InterfaceWithApproval);
        var generated = output.SyntaxTrees.Last().ToString();
        Assert.Contains("ApprovalRequiredAIFunction(new IDangerousService_Write_Tool())", generated);
        // safe_read should NOT be wrapped
        Assert.Contains("new IDangerousService_Read_Tool(),", generated);
    }

    // ── Nested class scenarios ──────────────────────────────────────────

    [Fact]
    public void NestedClassContext_CompilesCleanly()
        => AssertCleanCompilation(Inputs.NestedClassContext);

    [Fact]
    public void NestedClassContext_GeneratesOneSource()
        => Assert.Equal(1, CountGeneratedSources(Inputs.NestedClassContext));

    [Fact]
    public void NestedClassContext_EmitsContainingTypeWrapper()
    {
        var (_, _, output) = RunAndCompile(Inputs.NestedClassContext);
        var generated = output.SyntaxTrees.Last().ToString();
        Assert.Contains("public partial class OuterViewModel", generated);
        Assert.Contains("private partial class InnerTools", generated);
    }

    [Fact]
    public void DeeplyNestedClassContext_CompilesCleanly()
        => AssertCleanCompilation(Inputs.DeeplyNestedClassContext);

    [Fact]
    public void DeeplyNestedClassContext_EmitsMultipleLevels()
    {
        var (_, _, output) = RunAndCompile(Inputs.DeeplyNestedClassContext);
        var generated = output.SyntaxTrees.Last().ToString();
        Assert.Contains("public partial class LevelOne", generated);
        Assert.Contains("public partial class LevelTwo", generated);
        Assert.Contains("private partial class DeepTools", generated);
    }

    [Fact]
    public void NestedClassNoNamespace_CompilesCleanly()
        => AssertCleanCompilation(Inputs.NestedClassNoNamespace);

    [Fact]
    public void NestedClassNoNamespace_EmitsNoNamespaceWrapper()
    {
        var (_, _, output) = RunAndCompile(Inputs.NestedClassNoNamespace);
        var generated = output.SyntaxTrees.Last().ToString();
        Assert.DoesNotContain("namespace", generated);
        Assert.Contains("public partial class Outer", generated);
        Assert.Contains("internal partial class NestedTools", generated);
    }

    // ── Accessibility scenarios ─────────────────────────────────────────

    [Fact]
    public void InternalContextClass_CompilesCleanly()
        => AssertCleanCompilation(Inputs.InternalContextClass);

    [Fact]
    public void InternalContextClass_EmitsInternalAccessibility()
    {
        var (_, _, output) = RunAndCompile(Inputs.InternalContextClass);
        var generated = output.SyntaxTrees.Last().ToString();
        Assert.Contains("internal partial class InternalTools", generated);
    }

    // ── Cross-feature combinations ──────────────────────────────────────

    [Fact]
    public void MixedMethodsAndProperties_CompilesCleanly()
        => AssertCleanCompilation(Inputs.MixedMethodsAndProperties);

    [Fact]
    public void MultipleAIToolSources_CompilesCleanly()
        => AssertCleanCompilation(Inputs.MultipleAIToolSources);

    [Fact]
    public void CrossContextSameService_GeneratesTwoSources()
        => Assert.Equal(2, CountGeneratedSources(Inputs.CrossContextSameService));

    // ── Diagnostic scenarios ────────────────────────────────────────────

    [Fact]
    public void EmptyToolSource_EmitsDiagnostic()
    {
        var (genDiags, _, _) = RunAndCompile(Inputs.EmptyToolSource);
        Assert.Contains(genDiags, d => d.Id == "MAUIAI003");
    }

    [Fact]
    public void GenericMethod_EmitsDiagnostic()
    {
        var (genDiags, _, _) = RunAndCompile(Inputs.GenericMethod);
        Assert.Contains(genDiags, d => d.Id == "MAUIAI004");
    }

    [Fact]
    public void RefParam_EmitsDiagnostic()
    {
        var (genDiags, _, _) = RunAndCompile(Inputs.RefParam);
        Assert.Contains(genDiags, d => d.Id == "MAUIAI004");
    }

    [Fact]
    public void OutParam_EmitsDiagnostic()
    {
        var (genDiags, _, _) = RunAndCompile(Inputs.OutParam);
        Assert.Contains(genDiags, d => d.Id == "MAUIAI004");
    }

    [Fact]
    public void InParam_EmitsDiagnostic()
    {
        var (genDiags, _, _) = RunAndCompile(Inputs.InParam);
        Assert.Contains(genDiags, d => d.Id == "MAUIAI004");
    }

    [Fact]
    public void UnsupportedDelegateParam_EmitsWarningDiagnostic()
    {
        var (genDiags, _, _) = RunAndCompile(Inputs.UnsupportedDelegateParam);
        Assert.Contains(genDiags, d => d.Id == "MAUIAI002");
    }

    // ── Existing features still compile ─────────────────────────────────

    [Theory]
    [InlineData(nameof(Inputs.SimpleInstanceMethod))]
    [InlineData(nameof(Inputs.ExplicitToolName))]
    [InlineData(nameof(Inputs.DescriptionAttribute))]
    [InlineData(nameof(Inputs.DefaultValues))]
    [InlineData(nameof(Inputs.NullableParams))]
    [InlineData(nameof(Inputs.CancellationTokenParam))]
    [InlineData(nameof(Inputs.IServiceProviderAndArgsInjection))]
    [InlineData(nameof(Inputs.FromKeyedServicesString))]
    [InlineData(nameof(Inputs.FromKeyedServicesNullKey))]
    [InlineData(nameof(Inputs.FromServicesOnInterface))]
    [InlineData(nameof(Inputs.FromServicesOnConcreteClass))]
    [InlineData(nameof(Inputs.ReturnTypeVoid))]
    [InlineData(nameof(Inputs.ReturnTypeTask))]
    [InlineData(nameof(Inputs.ReturnTypeValueTask))]
    [InlineData(nameof(Inputs.ReturnTypeTaskOfT))]
    [InlineData(nameof(Inputs.ReturnTypeValueTaskOfT))]
    [InlineData(nameof(Inputs.ApprovalRequired))]
    [InlineData(nameof(Inputs.NoNamespace))]
    [InlineData(nameof(Inputs.NestedNamespace))]
    [InlineData(nameof(Inputs.StaticProperty))]
    [InlineData(nameof(Inputs.InstanceProperty))]
    public void AllValidInputs_CompileCleanly(string scenario)
        => AssertCleanCompilation(Inputs.Get(scenario));
}
