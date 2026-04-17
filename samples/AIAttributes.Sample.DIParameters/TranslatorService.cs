using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.DIParameters;

/// <summary>
/// Options the AI model supplies as part of the tool call. A plain record
/// (not an interface/abstract class) so the generator schemas it by default
/// — no explicit attribute is needed to keep it in the tool schema.
/// </summary>
public sealed record TranslationOptions(bool Verbose = false);

public class TranslatorService
{
    [Description("Translates a phrase using the configured translator.")]
    [ExportAIFunction("translate")]
    public string Translate(
        [Description("The text to translate")] string text,
        // Explicit DI: the ITranslator here is interface-inferred too, but
        // [FromServices] makes the intent explicit and is required for
        // concrete/class services.
        [FromServices] ITranslator translator,
        // Explicit keyed DI: resolved via [FromKeyedServices].
        [FromKeyedServices("premium")] IModelProvider model,
        // A plain record — no attribute needed. The AI fills it in per call.
        TranslationOptions options,
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

