using AIAttributes.Sample.Garden.Services;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden;

/// <summary>
/// Source-generated tool context that merges all tool sources into one.
/// Use <c>GardenShopTools.Default.Tools</c> to get the full tool list.
///
/// Demonstrates: [AIToolSource] composing multiple services — the generator
/// scans each referenced type for [ExportAIFunction] members and emits a
/// single AIToolContext subclass that provides every tool through one .Tools
/// property. Static types (ProductCatalog) and DI-resolved instances
/// (CurrentCart, OrderArchive) are handled transparently.
/// </summary>
[AIToolSource(typeof(ProductCatalog))]
[AIToolSource(typeof(CurrentCart))]
[AIToolSource(typeof(OrderArchive))]
public partial class GardenShopTools : AIToolContext { }
