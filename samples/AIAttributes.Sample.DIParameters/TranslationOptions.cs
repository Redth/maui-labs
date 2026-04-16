namespace AIAttributes.Sample.DIParameters;

/// <summary>
/// Options the AI model supplies as part of the tool call. Even though
/// this type is DI-resolvable, <c>[FromArguments]</c> on the parameter
/// forces it into the tool schema instead of being inferred from DI.
/// </summary>
public sealed record TranslationOptions(bool Verbose = false);
