namespace Microsoft.Maui.AI.Attributes.Generators.Tests;

internal static class Inputs
{
    // Keep each scenario self-contained. Inputs must reference only types available to the test
    // compilation: netcore BCL, Microsoft.Extensions.AI, Microsoft.Extensions.DependencyInjection,
    // and the Microsoft.Maui.AI.Attributes runtime library.

    public const string SimpleInstanceMethod = """
        using System.ComponentModel;
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class GreeterService
        {
            [ExportAIFunction]
            public string Greet(string name) => "Hello " + name;
        }

        [AIToolSource(typeof(GreeterService))]
        public partial class GreeterTools : AIToolContext { }
        """;

    public const string ExplicitToolName = """
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Svc
        {
            [ExportAIFunction("say_hello")]
            public string Hello() => "hi";
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string DescriptionAttribute = """
        using System.ComponentModel;
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Svc
        {
            [ExportAIFunction]
            [Description("Adds two numbers and returns the sum.")]
            public int Add(
                [Description("First addend")] int a,
                [Description("Second addend")] int b) => a + b;
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string DefaultValues = """
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Svc
        {
            [ExportAIFunction]
            public string Do(string name, int count = 3, bool verbose = false, string prefix = "hi") => name;
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string NullableParams = """
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Svc
        {
            [ExportAIFunction]
            public string Do(string? name, int? count) => name ?? "";
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string CancellationTokenParam = """
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Svc
        {
            [ExportAIFunction]
            public Task<string> DoAsync(string name, CancellationToken ct) => Task.FromResult(name);
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string IServiceProviderAndArgsInjection = """
        using System;
        using Microsoft.Extensions.AI;
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Svc
        {
            [ExportAIFunction]
            public string Do(string name, IServiceProvider services, AIFunctionArguments args) => name;
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string FromKeyedServicesString = """
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public interface ICache { }

        public class Svc
        {
            [ExportAIFunction]
            public string Do(string name, [FromKeyedServices("primary")] ICache cache) => name;
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string FromKeyedServicesNullKey = """
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public interface ICache { }

        public class Svc
        {
            [ExportAIFunction]
            public string Do(string name, [FromKeyedServices(null)] ICache cache) => name;
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string FromServicesOnInterface = """
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public interface ICache { }

        public class Svc
        {
            [ExportAIFunction]
            public string Do(string name, [FromServices] ICache cache) => name;
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string FromServicesOnConcreteClass = """
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class CacheImpl { }

        public class Svc
        {
            [ExportAIFunction]
            public string Do(string name, [FromServices] CacheImpl cache) => name;
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string ReturnTypeVoid = """
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Svc
        {
            [ExportAIFunction]
            public void Do(string name) { }
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string ReturnTypeTask = """
        using System.Threading.Tasks;
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Svc
        {
            [ExportAIFunction]
            public Task DoAsync(string name) => Task.CompletedTask;
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string ReturnTypeValueTask = """
        using System.Threading.Tasks;
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Svc
        {
            [ExportAIFunction]
            public ValueTask DoAsync(string name) => default;
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string ReturnTypeTaskOfT = """
        using System.Threading.Tasks;
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Svc
        {
            [ExportAIFunction]
            public Task<int> GetAsync(string name) => Task.FromResult(1);
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string ReturnTypeValueTaskOfT = """
        using System.Threading.Tasks;
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Svc
        {
            [ExportAIFunction]
            public ValueTask<int> GetAsync(string name) => new ValueTask<int>(1);
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string ApprovalRequired = """
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Svc
        {
            [ExportAIFunction(ApprovalRequired = true)]
            public string Dangerous(string name) => name;
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string MultipleAIToolSources = """
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class SvcA { [ExportAIFunction] public string DoA(string x) => x; }
        public class SvcB { [ExportAIFunction] public string DoB(string x) => x; }

        [AIToolSource(typeof(SvcA))]
        [AIToolSource(typeof(SvcB))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string CrossContextSameService = """
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Shared { [ExportAIFunction] public string Do(string x) => x; }

        [AIToolSource(typeof(Shared))]
        public partial class CtxA : AIToolContext { }

        [AIToolSource(typeof(Shared))]
        public partial class CtxB : AIToolContext { }
        """;

    public const string NoNamespace = """
        using Microsoft.Maui.AI.Attributes;

        public class Svc { [ExportAIFunction] public string Do(string x) => x; }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string NestedNamespace = """
        using Microsoft.Maui.AI.Attributes;

        namespace Sample.Inner.Deeper;

        public class Svc { [ExportAIFunction] public string Do(string x) => x; }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string EmptyToolSource = """
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Svc { public string Do(string x) => x; }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string UnsupportedDelegateParam = """
        using System;
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Svc
        {
            [ExportAIFunction]
            public string Do(string name, Func<int, int> fn) => name;
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string GenericMethod = """
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Svc
        {
            [ExportAIFunction]
            public T Do<T>(T value) => value;
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string RefParam = """
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Svc
        {
            [ExportAIFunction]
            public void Do(ref int value) { value++; }
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string OutParam = """
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Svc
        {
            [ExportAIFunction]
            public void Do(out int value) { value = 1; }
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public const string InParam = """
        using Microsoft.Maui.AI.Attributes;

        namespace Sample;

        public class Svc
        {
            [ExportAIFunction]
            public int Do(in int value) => value + 1;
        }

        [AIToolSource(typeof(Svc))]
        public partial class ToolsCtx : AIToolContext { }
        """;

    public static string Get(string name) => name switch
    {
        nameof(SimpleInstanceMethod) => SimpleInstanceMethod,
        nameof(ExplicitToolName) => ExplicitToolName,
        nameof(DescriptionAttribute) => DescriptionAttribute,
        nameof(DefaultValues) => DefaultValues,
        nameof(NullableParams) => NullableParams,
        nameof(CancellationTokenParam) => CancellationTokenParam,
        nameof(IServiceProviderAndArgsInjection) => IServiceProviderAndArgsInjection,
        nameof(FromKeyedServicesString) => FromKeyedServicesString,
        nameof(FromKeyedServicesNullKey) => FromKeyedServicesNullKey,
        nameof(FromServicesOnInterface) => FromServicesOnInterface,
        nameof(FromServicesOnConcreteClass) => FromServicesOnConcreteClass,
        nameof(ReturnTypeVoid) => ReturnTypeVoid,
        nameof(ReturnTypeTask) => ReturnTypeTask,
        nameof(ReturnTypeValueTask) => ReturnTypeValueTask,
        nameof(ReturnTypeTaskOfT) => ReturnTypeTaskOfT,
        nameof(ReturnTypeValueTaskOfT) => ReturnTypeValueTaskOfT,
        nameof(ApprovalRequired) => ApprovalRequired,
        nameof(MultipleAIToolSources) => MultipleAIToolSources,
        nameof(CrossContextSameService) => CrossContextSameService,
        nameof(NoNamespace) => NoNamespace,
        nameof(NestedNamespace) => NestedNamespace,
        nameof(EmptyToolSource) => EmptyToolSource,
        nameof(UnsupportedDelegateParam) => UnsupportedDelegateParam,
        nameof(GenericMethod) => GenericMethod,
        nameof(RefParam) => RefParam,
        nameof(OutParam) => OutParam,
        nameof(InParam) => InParam,
        _ => throw new KeyNotFoundException(name),
    };
}
