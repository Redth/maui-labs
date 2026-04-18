using AIAttributes.Sample.Garden.Services;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden;

/// <summary>
/// Source-generated tool context that merges all tool sources into one.
/// Use <c>GardenShopTools.Default.Tools</c> to get the full tool list.
///
/// Demonstrates three distinct attribute patterns:
/// <list type="bullet">
///   <item><b>Static class</b> — ProductCatalog: tools on a plain static class.</item>
///   <item><b>Instance class</b> — CurrentCart: tools on a DI-registered instance.</item>
///   <item><b>Interface</b> — IOrderArchive: tools declared on the interface so
///     any implementation (InMemoryOrderArchive, PreferencesOrderArchive, …)
///     is AI-capable without changing a single attribute.</item>
/// </list>
/// </summary>
[AIToolSource(typeof(ProductCatalog))]
[AIToolSource(typeof(CurrentCart))]
[AIToolSource(typeof(IOrderArchive))]
public partial class GardenShopTools : AIToolContext { }
