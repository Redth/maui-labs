using AIAttributes.Sample.Garden.Services;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden;

/// <summary>
/// All tools this sample exposes to the chat client.
/// Composes the read-only plant catalog with the per-session garden management tools.
/// </summary>
[AIToolSource(typeof(PlantCatalogService))]
[AIToolSource(typeof(GardenService))]
public partial class GardenTools : AIToolContext { }
