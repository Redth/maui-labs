namespace AIAttributes.Sample.DIParameters;

/// <summary>
/// A keyed service. Pulled from DI using <c>[FromKeyedServices("premium")]</c>
/// or <c>[FromKeyedServices("free")]</c>.
/// </summary>
public interface IModelProvider
{
    string Name { get; }
}
