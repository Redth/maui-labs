namespace AIAttributes.Sample.DIParameters;

public sealed class PigLatinTranslator : ITranslator
{
    public string Translate(string text) =>
        string.Join(' ', text.Split(' ').Select(w =>
            w.Length > 1 ? w[1..] + w[0] + "ay" : w));
}
