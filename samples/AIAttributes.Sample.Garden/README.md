# Garden Shop AI Chat

A single-page MAUI chat app that demonstrates **`Microsoft.Maui.AI.Attributes`**
with a garden-shop shopping assistant.

## Services

Tool methods live directly on the service classes — no separate "tools" wrappers.

- **`ProductCatalog`** (static) — browse/search a hard-coded catalog of seeds,
  soil, fertilizer, tools, and equipment.
- **`CurrentCart`** (singleton, DI) — the active shopping cart. Manages item
  list, quantities, checkout, and reset on "New Chat".
- **`OrderArchive`** (singleton, DI) — committed orders. Survives "New Chat".

`GardenShopTools` is the `[AIToolSource]`-annotated context that composes all
three services into a single `.Tools` list.

## Feature showcase

| Feature | Where |
|---|---|
| `[ExportAIFunction]` on a **static property** | `ProductCatalog.All` |
| `[ExportAIFunction]` on a **static method** with optional param | `ProductCatalog.SearchProducts` |
| Custom tool name (method ≠ tool name) | `ProductCatalog.FindByName` → `"get_product"` |
| `[ExportAIFunction]` on an **instance method** (DI-resolved) | `CurrentCart.AddToList`, etc. |
| `[ExportAIFunction]` on an **instance property** | `OrderArchive.Orders` |
| `[FromServices]` parameter injection | `CurrentCart.CheckoutList(OrderArchive)` |
| `ApprovalRequired = true` | `CurrentCart.CheckoutList`, `CurrentCart.CancelList` |
| `[AIToolSource]` composing multiple types | `GardenShopTools` |

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
