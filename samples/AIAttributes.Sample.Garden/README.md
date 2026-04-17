# Garden Shop — per-session state without `IServiceScope`

A single-page MAUI chat app that models a realistic **Unit-of-Work** pattern:

- **Singleton** `ProductCatalog` — browse/search a hard-coded catalog of seeds,
  soil, fertilizer, tools, and equipment.
- **Singleton** `OrderArchive` — past orders and saved drafts. Survives "New Chat".
- **Per-session** `ChatSession` — mutable shopping list owned by the current
  chat. Discarded (as a draft) when you start a new chat.

The left column is the AI chat; the right column shows the current shopping
list on top and the archive (past orders + drafts) below. The two DI lifetimes
are visible on screen at the same time.

## Why this sample exists

Real MAUI / desktop apps rarely use `IServiceScope.CreateScope()` — that's a
web-request idiom. This sample teaches **per-session state in a client app
using only Singleton registrations**, by letting the view model own a plain
`ChatSession` object and publishing it to tools through a narrow
`ICurrentSession` singleton accessor.

## Three tool flavors enabled by `Microsoft.Maui.AI.Attributes`

All tool methods are **`static`** — no instance, no registration-as-type needed.
`GardenShopTools.Default.GetTools()` returns every tool across all three
`[AIToolSource]` groups.

| Group | DI dependencies | Example |
|---|---|---|
| `CatalogTools` | **None** — pure static, no `AIFunctionArguments.Services` touched. | `search_products`, `get_product` |
| `ShoppingListTools` | `[FromServices] ICurrentSession` — reads the currently-published session. | `add_to_list`, `checkout_list` (approval required) |
| `OrderArchiveTools` | `[FromServices] OrderArchive` — singleton. | `list_past_orders`, `reorder` |

The point: **`[FromServices]` transparently wires both durable singletons and
per-session state through the same binding mechanism**, and pure-static tools
need no DI at all.

## How "New Chat" works

When you tap **New Chat**:

1. If the current list has items, it is saved as a **draft** in the archive
   (no prompt — your choice is to always preserve work).
2. The current session's `CancellationTokenSource` is cancelled — any in-flight
   tool call aborts cleanly.
3. A fresh `ChatSession` is created and published through `ICurrentSession`.
   Subsequent tool invocations see the new session.

No `IServiceScope` is disposed anywhere. No scoped services need to exist.

## Approval flow

`checkout_list` and `cancel_list` carry `[ExportAIFunction(ApprovalRequired = true)]`.
They are the explicit commit / discard transitions that move per-session state
into (or out of) the durable archive. The input bar is replaced by an approval
banner until you accept or reject.

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
