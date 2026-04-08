# M32's QoL Mod - Implementation Plan

> **IMPORTANT**: All C# class names MUST use the `M32_` prefix.

## TODO

### Zone Trade Feature
- [x] Create mod structure (manifest.json, Commands.xml, Options.xml)
- [x] Implement M32_Options class with OptionFlagUpdate
- [x] Implement M32_PlayerMutator for new games
- [x] Implement M32_LoadGameHandler for loaded saves
- [x] Implement M32_ZoneTradePart main logic
- [x] Implement merchant detection (Commerce/Restocker parts, hostility check)
- [x] Implement inventory consolidation via Harmony patch
- [x] Add Ctrl+T keybind configuration
- [x] Add enable/disable toggle in Options menu
- [x] Fix merchant detection (now requires Commerce + inventory + same parasang)
- [x] Select primary merchant with best services
- [x] Include service providers (Argyve, tinkering NPCs)
- [x] Exclude player from merchant list
- [x] Post-trade reconciliation to return items to correct owners
- [x] Force-close UI after trade to prevent stale data

### Stacked Inventory Feature
- [x] Create StackedInventory.cs with stack merging logic
- [x] Add EnableStackedInventory option
- [x] Implement CanStackForMerge (only liquid containers can stack)
- [x] Harmony patch for InventoryScreen.Show()
- [x] Harmony patch for TradeScreen.Show() (Prefix, before Zone Trade)

### Auto Loot Feature
- [x] Create AutoLoot.cs with M32_AutoLootPart
- [x] Add 9 options to Options.xml
- [x] Implement movement detection (BeforeTakeActionEvent, EnteredCellEvent)
- [x] Implement item pickup on current + adjacent cells
- [x] Implement ShouldAutoLoot() filter
- [x] Implement container content extraction
- [x] Implement auto-explore integration (AutoexploreObjectEvent)
- [x] Implement ownership check (GetAllegianceLevel)
- [x] Exclude harvestable objects (Harvestable part)
- [x] Exclude garbage items (Garbage part)
- [x] Implement weight capacity checking
- [x] Implement dropped item cooldown (M32_DroppedTurn property)
- [x] Add autoloot.json whitelist/blacklist support

### Consolidate Liquids Feature
- [x] Create ConsolidateLiquids.cs
- [x] Implement GetMixtureKey() with GCD normalization
- [x] Implement PourLiquid() using Split() and MixWith()
- [x] Add neutron flux exclusion
- [x] Add Ctrl+L keybind

---

## Features

### Zone Trade `[Shift+T]`
Trade with all friendly merchants in the zone at once.

- Finds merchants with Commerce/Tinkering parts in same parasang
- Selects merchant with best services (identify/repair/recharge)
- Consolidates inventories with virtual items for display
- Reconciles post-trade to move items to correct owners

### Stacked Inventory `[Automatic]`
Merge compatible item stacks when opening inventory/trade.

- Same blueprint, modifications, appearance
- Only liquid containers can stack (if empty)
- Runs as Prefix before Zone Trade

### Auto Loot `[Automatic]`
Pick up nearby items when moving.

- Current tile + 8 adjacent cells
- Weight capacity checks
- Ownership checks (don't steal from friendlies)
- Excludes harvestables, garbage, NoAutoget items
- Dropped item cooldown with M32_DroppedTurn tracking
- Excludes creature/NPC inventories
- Excludes butcherable items (corpses)
- Configurable max item weight limit
- Separate toggle for organic limbs (arms, legs, heads, faces)
- Separate toggle for furniture (chests, beds, chairs, etc.)
- `autoloot.json` whitelist/blacklist support (bypasses category filters but respects required checks)
- Always excludes invalid blueprint items (`[invalid blueprint:...]`)

### Consolidate Liquids `[Shift+L]`
Merge same-type liquids into largest containers.

- GCD-based ratio matching
- Excludes neutron flux

---

## File Structure

```
M32_QoL/
├── manifest.json
├── Commands.xml
├── Options.xml
├── Options.cs
├── HarmonyInitializer.cs
├── PlayerMutator.cs
├── LoadGameHandler.cs
├── autoloot.json          # Optional: whitelist/blacklist for auto-loot
└── features/
    ├── AutoLoot.cs
    ├── ConsolidateLiquids.cs
    ├── StackedInventory.cs
    └── ZoneTrade.cs
```

---

## Key Technical Patterns

### DroppedByPlayer Property

Game sets `DroppedByPlayer=1` as a flag, NOT a turn count. Track turns yourself:

```csharp
if (item.HasIntProperty("DroppedByPlayer"))
{
    if (!item.HasIntProperty("M32_DroppedTurn"))
        item.SetIntProperty("M32_DroppedTurn", (int)(The.Game?.Turns ?? 0));
    
    int dropTurn = item.GetIntProperty("M32_DroppedTurn");
    long currentTurn = The.Game?.Turns ?? 0;
    if (currentTurn - dropTurn <= cooldownTurns)
        return false;
}
```

### Multi-Merchant Trade Reconciliation

1. Before trade: Snapshot merchant inventories + player inventory
2. During trade: Virtual items for display consolidation
3. After trade: Compare player inventory, deduct from merchants in FIFO order

### Force-Close Trade After Transaction

Patch `TradeUI.PerformOffer` to return `CLOSE` instead of `REFRESH`:

```csharp
if (__result == TradeUI.OfferStatus.REFRESH || __result == TradeUI.OfferStatus.TOP)
    __result = TradeUI.OfferStatus.CLOSE;
```

### autoloot.json Whitelist/Blacklist

Create `M32_QoL/autoloot.json` for per-item auto-loot control:

```json
{
    "allow": ["Item Name 1", "Item Name 2"],
    "deny": ["Item Name 3"]
}
```

- **`allow`**: Items that bypass category filters (harvestable, butcherable, garbage, boulders, corpses, limbs)
- **`deny`**: Items that are always excluded from auto-loot
- **Required checks still apply**: ownership, takeable, liquid pools, containers, dropped cooldown, weight, combat

---

## Dependencies

None (uses only base game APIs)