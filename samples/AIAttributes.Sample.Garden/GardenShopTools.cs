using AIAttributes.Sample.Garden.Tools;
using Microsoft.Maui.AI.Attributes;

namespace AIAttributes.Sample.Garden;

/// <summary>
/// All tools this sample exposes to the chat client. Three sources, each
/// teaching a different DI shape:
///
/// <list type="bullet">
///   <item><see cref="CatalogTools"/> — pure static methods, no DI at all.</item>
///   <item><see cref="ShoppingListTools"/> — static methods using
///       <c>[FromServices] ICurrentSession</c> for per-session state.</item>
///   <item><see cref="OrderArchiveTools"/> — static methods using
///       <c>[FromServices] OrderArchive</c> for durable singleton state.</item>
/// </list>
/// </summary>
[AIToolSource(typeof(CatalogTools))]
[AIToolSource(typeof(ShoppingListTools))]
[AIToolSource(typeof(OrderArchiveTools))]
public partial class GardenShopTools : AIToolContext { }
