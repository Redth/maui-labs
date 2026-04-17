using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace Microsoft.Maui.AI.Attributes;

/// <summary>
/// Base class for source-generated AI tool contexts. Subclasses decorated with
/// <see cref="AIToolSourceAttribute"/> have their <see cref="GetTools"/> override implemented
/// by the source generator at compile time.
/// </summary>
/// <remarks>
/// This follows the same pattern as <c>System.Text.Json.Serialization.JsonSerializerContext</c>:
/// declare a partial class, decorate it with attributes, and the source generator fills in the
/// implementation &#8212; no runtime reflection for discovery.
/// </remarks>
public abstract class AIToolContext
{
    /// <summary>
    /// Returns the AI tools defined by this context. The returned list is built once and does
    /// not capture any <see cref="IServiceProvider"/>; tools whose backing method requires
    /// services read them from <see cref="AIFunctionArguments.Services"/> at invocation time.
    /// </summary>
    /// <remarks>
    /// If any tool in this context binds to an instance method or a <c>[FromServices]</c>
    /// parameter, callers must set <see cref="AIFunctionArguments.Services"/> before invoking
    /// the tool. <see cref="Microsoft.Extensions.AI.ChatClientBuilderChatClientExtensions.UseFunctionInvocation"/>
    /// combined with <c>ChatClientBuilder.Build(IServiceProvider)</c> does this automatically.
    /// </remarks>
    public abstract IReadOnlyList<AITool> GetTools();

    /// <summary>
    /// Helpers used by generated code. These are not intended for direct use by applications.
    /// </summary>
    protected static class Helpers
    {
        /// <summary>
        /// Returns the service provider supplied by the caller on
        /// <see cref="AIFunctionArguments.Services"/>. Throws if it is not set.
        /// </summary>
        public static IServiceProvider RequireServices(AIFunctionArguments args)
        {
            var provider = args.Services;
            if (provider is null)
            {
                throw new InvalidOperationException(
                    "This tool requires services but no IServiceProvider was supplied. Set " +
                    "AIFunctionArguments.Services before invoking the tool (ChatClientBuilder's " +
                    "UseFunctionInvocation().Build(sp) does this automatically), or author the " +
                    "backing method as static with no [FromServices] parameters.");
            }
            return provider;
        }

        /// <summary>
        /// Reads a required argument from <see cref="AIFunctionArguments"/>, converting it to
        /// <typeparamref name="T"/> via a direct cast, JSON element conversion, or JSON round-trip.
        /// </summary>
        public static T GetRequiredArg<T>(AIFunctionArguments args, string name, JsonSerializerOptions? options = null)
        {
            if (!args.TryGetValue(name, out var value))
            {
                throw new ArgumentException($"Missing required argument '{name}'.", nameof(args));
            }
            return ConvertArg<T>(value, name, options);
        }

        /// <summary>
        /// Reads an optional argument. If the value is missing or <see langword="null"/>, returns
        /// <paramref name="defaultValue"/>.
        /// </summary>
        public static T? GetOptionalArg<T>(AIFunctionArguments args, string name, T? defaultValue, JsonSerializerOptions? options = null)
        {
            if (!args.TryGetValue(name, out var value) || value is null)
            {
                return defaultValue;
            }
            return ConvertArg<T>(value, name, options);
        }

        private static T ConvertArg<T>(object? value, string name, JsonSerializerOptions? options)
        {
            if (value is null)
            {
                if (default(T) is null)
                {
                    return default!;
                }
                throw new ArgumentException($"Argument '{name}' is null but target type '{typeof(T)}' is non-nullable.", nameof(name));
            }

            if (value is T typed)
            {
                return typed;
            }

            var opts = options ?? AIJsonUtilities.DefaultOptions;

            if (value is JsonElement je)
            {
                return je.Deserialize<T>(opts)!;
            }

            if (value is JsonNode jn)
            {
                return jn.Deserialize<T>(opts)!;
            }

            // If the LLM supplied a raw JSON string for a non-string target, try to parse it.
            if (value is string s && typeof(T) != typeof(string))
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(s, opts)!;
                }
                catch
                {
                    // Fall through to round-trip.
                }
            }

            // Fallback: JSON round-trip. This matches ReflectionAIFunction's behavior for
            // general object-to-T coercion.
            var json = JsonSerializer.Serialize(value, value.GetType(), opts);
            return JsonSerializer.Deserialize<T>(json, opts)!;
        }
    }
}
