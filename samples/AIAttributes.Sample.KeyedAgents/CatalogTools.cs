using AIAttributes.Sample.KeyedAgents.Services;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.KeyedAgents;

/// <summary>
/// Read-only agent: exposes only the plant catalog lookup tools.
/// Registered via <c>AddAITools&lt;CatalogTools&gt;("browse")</c>.
/// </summary>
[AIToolSource(typeof(PlantCatalogService))]
public partial class CatalogTools : AIToolContext { }
