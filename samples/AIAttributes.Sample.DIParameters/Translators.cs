namespace AIAttributes.Sample.DIParameters;

/// <summary>
/// A translator service. Resolved by inferred DI — the generator sees the
/// parameter type is an interface and injects it from the service provider
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
