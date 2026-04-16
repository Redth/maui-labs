using AIAttributes.Sample.KeyedAgents.Services;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.KeyedAgents;

// Two independent tool contexts — a "browse" agent that only knows how to
// search the catalog, and a "manage" agent that can mutate the user's garden.
//
// Registering each one with a DI key gives the app two isolated tool sets
// it can resolve per page / per scenario using
// sp.GetKeyedServices<AITool>("browse") or "manage".

[AIToolSource(typeof(PlantCatalogService))]
public partial class CatalogTools : AIToolContext { }

[AIToolSource(typeof(GardenService))]
public partial class GardenManagementTools : AIToolContext { }
