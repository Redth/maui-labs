using Microsoft.CodeAnalysis;
using VerifyXunit;

namespace Microsoft.Maui.AI.Attributes.Generators.Tests;

public class GeneratorSnapshotTests
{
    private static Task VerifyAsync(string source, string scenarioName)
    {
        var driver = GeneratorTestHarness.RunGenerator(source, out _, out _);
        return Verifier.Verify(driver)
            .UseParameters(scenarioName)
            .ScrubLinesWithReplace(line =>
            {
                // Scrub GeneratedCode version attribute so snapshots don't churn on version bumps.
                if (line.Contains("GeneratedCodeAttribute") || line.Contains("GeneratedCode("))
                {
                    var versionIdx = line.IndexOf("\", \"", StringComparison.Ordinal);
                    if (versionIdx > 0)
                    {
                        var closeIdx = line.IndexOf("\")", versionIdx, StringComparison.Ordinal);
                        if (closeIdx > versionIdx)
                            return line.Substring(0, versionIdx) + "\", \"VERSION" + line.Substring(closeIdx);
                    }
                }
                return line;
            });
    }

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
    [InlineData(nameof(Inputs.MultipleAIToolSources))]
    [InlineData(nameof(Inputs.CrossContextSameService))]
    [InlineData(nameof(Inputs.NoNamespace))]
    [InlineData(nameof(Inputs.NestedNamespace))]
    [InlineData(nameof(Inputs.EmptyToolSource))]
    [InlineData(nameof(Inputs.UnsupportedDelegateParam))]
    [InlineData(nameof(Inputs.GenericMethod))]
    [InlineData(nameof(Inputs.RefParam))]
    [InlineData(nameof(Inputs.OutParam))]
    [InlineData(nameof(Inputs.InParam))]
    public Task Snapshot(string scenario)
    {
        var source = Inputs.Get(scenario);
        return VerifyAsync(source, scenario);
    }
}
