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
    [Description("Translates a phrase using the configured translator. Always return the tool result verbatim.")]
    [ExportAIFunction("translate")]
    public string Translate(
        [Description("The text to translate")] string text,
        // Explicit DI: [FromServices] resolves from IServiceProvider and
        // excludes the parameter from the tool schema.
        [FromServices] ITranslator translator,
        // Explicit keyed DI: resolved via [FromKeyedServices].
        [FromKeyedServices("premium")] IModelProvider model,
        // A plain record — no attribute needed. The AI fills it in per call.
        TranslationOptions options,
        // Direct CancellationToken support — never appears in the schema.
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Print what DI resolved and what the model filled in, so the sample
        // demonstrates parameter binding even when the LLM paraphrases the
        // tool result.
        Console.WriteLine();
        Console.WriteLine($"  [translate tool called]");
        Console.WriteLine($"    text        = {text.Replace("\"", "\\\"")}");
        Console.WriteLine($"    translator  = {translator.GetType().Name}   (from [FromServices])");
        Console.WriteLine($"    model       = {model.Name}   (from [FromKeyedServices(\"premium\")])");
        Console.WriteLine($"    options     = {options}   (from the AI)");
        Console.WriteLine();

        var translated = translator.Translate(text);
        return options.Verbose
            ? $"[model: {model.Name}] {text} => {translated}"
            : translated;
    }
}

