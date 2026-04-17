namespace AIAttributes.Sample.DIParameters;

/// <summary>
/// A translator service. Resolved via DI — the <c>[FromServices]</c> attribute
/// on the parameter tells the generator to inject it from the service provider
/// at invocation time rather than expecting the AI model to supply it.
/// </summary>
public interface ITranslator
{
    string Translate(string text);
}

public sealed class PigLatinTranslator : ITranslator
{
    public string Translate(string text) =>
        string.Join(' ', text.Split(' ').Select(w =>
            w.Length > 1 ? w[1..] + w[0] + "ay" : w));
}
