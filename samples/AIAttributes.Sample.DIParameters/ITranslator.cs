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
