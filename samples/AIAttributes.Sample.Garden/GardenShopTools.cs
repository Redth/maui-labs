using AIAttributes.Sample.Garden.Services;
using AIAttributes.Sample.Garden.Tools;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden;

/// <summary>
/// Source-generated tool context that merges all tool sources into one.
/// Use <c>GardenShopTools.Default.Tools</c> to get the full tool list.
/// </summary>
[AIToolSource(typeof(ProductCatalog))]
[AIToolSource(typeof(ShoppingListTools))]
[AIToolSource(typeof(OrderArchiveTools))]
public partial class GardenShopTools : AIToolContext { }
