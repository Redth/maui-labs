using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.DIParameters;

/// <summary>
/// Options the AI model supplies as part of the tool call. Even though
/// this type is DI-resolvable, <c>[FromArguments]</c> on the parameter
/// forces it into the tool schema instead of being inferred from DI.
/// </summary>
public sealed record TranslationOptions(bool Verbose = false);

public class TranslatorService
{
    [Description("Translates a phrase using the configured translator.")]
    [ExportAIFunction("translate")]
    public string Translate(
        [Description("The text to translate")] string text,
        // Inferred DI: interface parameter pulled from the IServiceProvider
        // at invocation time. Not part of the tool schema.
        ITranslator translator,
        // Explicit keyed DI: resolved via [FromKeyedServices].
        [FromKeyedServices("premium")] IModelProvider model,
        // [FromArguments] forces a DI-resolvable type to be treated as a
        // model argument instead — the AI fills it in per call.
        [FromArguments] TranslationOptions options,
        // Direct CancellationToken support — never appears in the schema.
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var translated = translator.Translate(text);
        return options.Verbose
            ? $"[model: {model.Name}] {text} => {translated}"
            : translated;
    }
}
