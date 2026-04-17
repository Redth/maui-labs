using AIAttributes.Sample.Garden.Services;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden;

/// <summary>
/// Source-generated tool context that merges all tool sources into one.
/// Use <c>GardenShopTools.Default.Tools</c> to get the full tool list.
/// </summary>
[AIToolSource(typeof(ProductCatalog))]
[AIToolSource(typeof(CurrentCart))]
[AIToolSource(typeof(OrderArchive))]
public partial class GardenShopTools : AIToolContext { }
