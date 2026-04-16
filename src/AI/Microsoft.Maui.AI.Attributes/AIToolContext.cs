using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Maui.AI.Attributes;

/// <summary>
/// Base class for source-generated AI tool contexts. Subclasses decorated with
/// <see cref="AIToolSourceAttribute"/> have their tool registration methods implemented by the
/// source generator at compile time.
/// </summary>
/// <remarks>
/// This follows the same pattern as <c>System.Text.Json.Serialization.JsonSerializerContext</c>:
/// declare a partial class, decorate it with attributes, and the source generator fills in the
/// implementation &#8212; no runtime reflection for discovery.
/// </remarks>
public abstract class AIToolContext
{
    /// <summary>
    /// Returns the AI tools defined by this context. Each tool resolves its backing service
    /// from <see cref="AIFunctionArguments.Services"/> at invocation time, falling back to
    /// <paramref name="serviceProvider"/> if the caller did not supply a provider.
    /// </summary>
    /// <param name="serviceProvider">
    /// The fallback service provider. Used only when the invoker does not set
    /// <see cref="AIFunctionArguments.Services"/>. Typically the root DI container.
    /// </param>
    public abstract IReadOnlyList<AITool> GetTools(IServiceProvider serviceProvider);

    /// <summary>
    /// Registers all tools from this context as singleton <see cref="AITool"/> services.
    /// </summary>
    public abstract void RegisterTools(IServiceCollection services);

    /// <summary>
    /// Registers all tools from this context as keyed singleton <see cref="AITool"/> services.
    /// </summary>
    public abstract void RegisterTools(IServiceCollection services, string key);

    /// <summary>
    /// Helpers used by generated code. These are not intended for direct use by applications.
    /// </summary>
    protected static class Helpers
    {
        /// <summary>
        /// Returns the service provider to use for a single invocation. Prefers the caller-
        /// supplied <see cref="AIFunctionArguments.Services"/> and falls back to the provider
        /// captured at tool-registration time. Throws if neither is available.
        /// </summary>
        public static IServiceProvider RequireServices(AIFunctionArguments args, IServiceProvider? fallback)
        {
            var provider = args.Services ?? fallback;
            if (provider is null)
            {
                throw new InvalidOperationException(
                    "No IServiceProvider is available. Either set AIFunctionArguments.Services before invoking the tool, " +
                    "or construct this tool via AddAITools<T>()/GetTools(serviceProvider) so a fallback provider is captured.");
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
