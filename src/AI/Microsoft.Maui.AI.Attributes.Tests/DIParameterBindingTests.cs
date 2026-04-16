using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.AI.Attributes;

namespace Microsoft.Maui.AI.Attributes.Tests;

// Service dependencies ------------------------------------------------------

internal interface IAddressBook
{
    string Lookup(string name);
}

internal sealed class AddressBook : IAddressBook
{
    public string Lookup(string name) => $"addr:{name}";
}

internal sealed class WeatherProbe
{
    public string Read(string city) => $"weather:{city}";
}

internal abstract class TranslatorBase
{
    public abstract string Translate(string text);
}

internal sealed class EchoTranslator : TranslatorBase
{
    public override string Translate(string text) => $"echo:{text}";
}

// Services with [ExportAIFunction] ------------------------------------------

internal sealed class ContactsToolService
{
    [ExportAIFunction("inferred_di_tool")]
    public string Find(IAddressBook book, string name) => book.Lookup(name);

    [ExportAIFunction("from_keyed_tool")]
    public string FindKeyed(
        [FromKeyedServices("primary")] IAddressBook book,
        string name) => book.Lookup(name);

    [ExportAIFunction("from_arguments_tool")]
    public string FindExplicitArgs(
        [FromArguments] IAddressBook book,
        string name) => book.Lookup(name);

    [ExportAIFunction("abstract_inferred_tool")]
    public string Translate(TranslatorBase t, string text) => t.Translate(text);
}

[AIToolSource(typeof(ContactsToolService))]
internal partial class ContactsToolContext : AIToolContext { }

public class DIParameterBindingTests
{
    [Fact]
    public async Task Interface_parameter_is_inferred_as_DI_and_excluded_from_schema()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAddressBook, AddressBook>();
        services.AddSingleton<ContactsToolService>();
        services.AddAITools<ContactsToolContext>();
        using var provider = services.BuildServiceProvider();

        var tool = (AIFunction)provider.GetRequiredService<IEnumerable<AITool>>().First(t => t.Name == "inferred_di_tool");
        var schema = tool.JsonSchema.ToString();
        Assert.DoesNotContain("\"book\"", schema);
        Assert.Contains("\"name\"", schema);

        var result = await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["name"] = "alice" }));
        Assert.Equal("addr:alice", result?.ToString());
    }

    [Fact]
    public async Task Abstract_parameter_is_inferred_as_DI()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TranslatorBase, EchoTranslator>();
        services.AddSingleton<ContactsToolService>();
        services.AddAITools<ContactsToolContext>();
        using var provider = services.BuildServiceProvider();

        var tool = (AIFunction)provider.GetRequiredService<IEnumerable<AITool>>().First(t => t.Name == "abstract_inferred_tool");
        Assert.DoesNotContain("\"t\"", tool.JsonSchema.ToString());

        var result = await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["text"] = "hi" }));
        Assert.Equal("echo:hi", result?.ToString());
    }

    [Fact]
    public async Task FromKeyedServices_resolves_by_key()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IAddressBook, AddressBook>("primary");
        services.AddSingleton<ContactsToolService>();
        services.AddAITools<ContactsToolContext>();
        using var provider = services.BuildServiceProvider();

        var tool = (AIFunction)provider.GetRequiredService<IEnumerable<AITool>>().First(t => t.Name == "from_keyed_tool");
        var result = await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["name"] = "carol" }));
        Assert.Equal("addr:carol", result?.ToString());
    }

    [Fact]
    public async Task FromKeyedServices_with_missing_key_throws()
    {
        var services = new ServiceCollection();
        // Note: no keyed registration — only unkeyed.
        services.AddSingleton<IAddressBook, AddressBook>();
        services.AddSingleton<ContactsToolService>();
        services.AddAITools<ContactsToolContext>();
        using var provider = services.BuildServiceProvider();

        var tool = (AIFunction)provider.GetRequiredService<IEnumerable<AITool>>().First(t => t.Name == "from_keyed_tool");
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["name"] = "x" })).AsTask());
    }

    [Fact]
    public void FromArguments_attribute_includes_interface_param_in_schema()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ContactsToolService>();
        services.AddAITools<ContactsToolContext>();
        using var provider = services.BuildServiceProvider();

        var tool = (AIFunction)provider.GetRequiredService<IEnumerable<AITool>>().First(t => t.Name == "from_arguments_tool");
        // The interface param MUST be in the schema (though runtime deserialization will
        // typically fail — the developer has explicitly asked for this behavior).
        Assert.Contains("\"book\"", tool.JsonSchema.ToString());
    }
}
