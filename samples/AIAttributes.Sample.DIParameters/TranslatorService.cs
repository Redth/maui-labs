using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.DIParameters;

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
