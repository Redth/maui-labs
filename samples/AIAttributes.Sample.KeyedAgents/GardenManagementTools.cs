using AIAttributes.Sample.KeyedAgents.Services;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.KeyedAgents;

/// <summary>
/// Mutation agent: can add, water, move, and remove plants.
/// Registered via <c>AddAITools&lt;GardenManagementTools&gt;("manage")</c>.
/// </summary>
[AIToolSource(typeof(GardenService))]
public partial class GardenManagementTools : AIToolContext { }
