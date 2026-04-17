# Garden Shop AI Chat

A single-page MAUI chat app that demonstrates **`Microsoft.Maui.AI.Attributes`**
with a garden-shop shopping assistant.

- **`ProductCatalog`** — browse/search a hard-coded catalog of seeds, soil,
  fertilizer, tools, and equipment.
- **`OrderArchive`** — committed orders. Survives "New Chat".
- **`Cart`** — mutable shopping list owned by the current chat session.
  Cleared when you start a new chat.

The left column is the AI chat; the right column shows the current shopping
list on top and past orders below.

## Tool groups

All tool methods are **`static`**. `GardenShopTools.Default.Tools` returns
every tool across all three `[AIToolSource]` groups.

| Group | DI dependencies | Tools |
|---|---|---|
| `CatalogTools` | None — pure static. | `search_products`, `get_product` |
| `ShoppingListTools` | `[FromServices] CurrentCart` — the active session. | `add_to_list`, `checkout_list` (approval required) |
| `OrderArchiveTools` | `[FromServices] OrderArchive` — singleton. | `list_past_orders`, `reorder` |

## Approval flow

`checkout_list` and `cancel_list` carry `[ExportAIFunction(ApprovalRequired = true)]`.
They move per-session state into (or out of) the durable archive. The input bar
is replaced by an approval banner until you accept or reject.

## Build & run

```bash
dotnet build samples/AIAttributes.Sample.Garden -f net10.0-maccatalyst
```

Configure user secrets (shared across AI.Attributes samples):

```bash
dotnet user-secrets --id ai-attributes-secrets set "AI:Endpoint" "<your-endpoint>"
dotnet user-secrets --id ai-attributes-secrets set "AI:ApiKey" "<your-key>"
dotnet user-secrets --id ai-attributes-secrets set "AI:DeploymentName" "<your-deployment>"
```
