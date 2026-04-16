using System;

namespace Microsoft.Maui.AI.Attributes;

/// <summary>
/// Forces a parameter of an <see cref="ExportAIFunctionAttribute"/>-decorated method to be
/// bound from the AI function's argument dictionary (included in the JSON schema exposed to the
/// model), overriding the source generator's inference that interface or abstract parameters
/// are resolved from dependency injection.
/// </summary>
/// <remarks>
/// By default, parameters whose type is an interface or abstract class are treated as
/// dependency-injected and excluded from the tool's schema. Apply this attribute when you want
/// the AI model to supply a value for such a parameter.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class FromArgumentsAttribute : Attribute
{
}
