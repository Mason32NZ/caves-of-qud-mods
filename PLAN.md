# M32's QoL Mod - Implementation Plan

> **IMPORTANT**: Agents working on this project MUST keep this TODO section up to date. Mark items as completed as you finish them. All C# class names MUST use the `M32_` prefix (e.g., `M32_Options`, `M32_PlayerMutator`) for consistency.

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
- [x] Fix merchant detection to avoid detecting 541 NPCs (now requires Commerce part specifically + has inventory + no WontSell tag + same parasang as player)
- [x] Add error handling around trade opening
- [x] Removed 5 merchant limit - now includes all merchants in parasang
- [x] Select primary merchant with best services (highest identify level + repair + recharge)
- [x] Fix merchant detection to include service providers (Argyve and other tinkering NPCs) not just Commerce merchants
- [x] Fix WontSell filter to allow service providers like Argyve who have repair/recharge services
- [x] Exclude player from merchant list
- [x] Fix TradeUI.GetObjects patch to not consolidate player's inventory (only merchant inventories)
- [x] Fix Harmony patch initialization (patches weren't being applied - added M32_HarmonyInitializer with [HasModSensitiveStaticCache])
- [x] Fix merchant detection to only find creatures (not furniture like dressers)
- [x] Change to use TradeUI.ShowTradeScreen() instead of CommandTrade event
- [x] Select primary merchant without vendor services (identify/repair) to avoid vendor popup
- [x] Sort consolidated merchant items alphabetically by name
- [x] Implement Approach 3: Post-trade Reconciliation to return items to correct owners after trade
- [x] Fix UI synchronization issue: Force-close trade UI immediately after TRADE button click to prevent showing stale data
- [ ] Selling to best price merchant (deferred to v1.1)
- [ ] Testing and bug fixes

### Future Features
- [ ] Auto-loot feature
- [ ] Quick travel enhancements
- [ ] Inventory sorting

## Overview
A Caves of Qud quality-of-life mod featuring the **Zone Trade** system for consolidated merchant trading.

## Feature: Zone Trade

### Description
When the player presses Ctrl+T (configurable), open a trade UI with the consolidated inventory of all friendly merchants in the current zone. Works from anywhere in the zone.

### Behavior Specifications

#### Merchant Detection
- **Scope**: All merchants in the current active zone (`The.ActiveZone`)
- **Criteria**:
  1. Has `Commerce` part OR `GenericInventoryRestocker` part
  2. Is NOT hostile toward player (`!IsHostileTowards(The.Player)`)
  3. Is on the same z-level as player
- **Exclusions**: Hostile merchants, merchants on different levels

#### Inventory Consolidation
- All items from valid merchants merged into a single list
- No grouping by merchant - flat item list
- Player can buy any item at the price set by its original merchant

#### Selling Logic
- When player sells items, automatically route to the merchant offering the best price
- Calculate sell price for each merchant and pick the highest

#### Price Calculation
- **Buying**: Use the price from the merchant who owns the item (respects faction reputation)
- **Selling**: Calculate price from all valid merchants, sell to the one offering the best deal

### UI/UX
- Jump directly to consolidated inventory (no merchant selection screen)
- Uses standard TradeScreen with modified inventory source
- Message shown if no friendly merchants available

### Options
- **Single Toggle**: `Option_M32QoL_EnableZoneTrade` (Checkbox, Default: Yes)
- Located in: Options Menu → Mod: M32's QoL
- Disabling prevents the keybind from working

### Keybind
- **Command ID**: `M32QoL_ZoneTrade`
- **Default**: Ctrl+T
- **Category**: Mod: M32's QoL
- **Layer**: Adventure
- Configurable via Keybinds Menu → Mod: M32's QoL

## File Structure

```
M32_QoL/
├── manifest.json          # Mod identity and metadata
├── Commands.xml           # Keybind definitions
├── Options.xml            # Options menu entries
├── ZoneTrade.cs           # Main implementation
└── PLAN.md               # This file
```

## Implementation Details

### C# Classes

#### 1. M32_Options (Static)
```csharp
[HasOptionFlagUpdate(Prefix = "Option_M32QoL_")]
public static class M32_Options
{
    [OptionFlag] public static bool EnableZoneTrade;
}
```

#### 2. M32_PlayerMutator
```csharp
[PlayerMutator]
public class M32_PlayerMutator : IPlayerMutator
{
    public void mutate(GameObject player)
    {
        player.RequirePart<M32_ZoneTradePart>();
    }
}
```

#### 3. M32_LoadGameHandler
```csharp
[HasCallAfterGameLoaded]
public class M32_LoadGameHandler
{
    [CallAfterGameLoaded]
    public static void AfterLoaded()
    {
        if (The.Player == null) return;
        The.Player.RequirePart<M32_ZoneTradePart>();
    }
}
```

#### 4. M32_ZoneTradePart (Main Logic)
```csharp
[Serializable]
public class M32_ZoneTradePart : IScribedPart
{
    // Event handling for CommandEvent
    // Merchant detection logic
    // Virtual merchant creation
    // Trade UI opening
}
```

### Key Methods

#### FindFriendlyMerchants()
- Iterates `The.ActiveZone.GetObjects()`
- Filters by Commerce/Restocker parts
- Checks hostility
- Returns `List<GameObject>` of valid merchants

#### CaptureInventorySnapshot()
- Records all merchant inventories before trade
- Maps each item ID to its owning merchant
- Stores item counts for reference
- Used for post-trade reconciliation

#### OpenZoneTrade()
- Checks if feature enabled
- Finds merchants
- Shows error if none found
- Captures inventory snapshot for reconciliation
- Opens trade with primary merchant (best services)
- Uses `TradeUI.ShowTradeScreen()` for trade

#### ReconcileMerchantInventories() (Approach 3)
- Runs after trade closes
- Compares current item ownership with pre-trade snapshot
- Moves items from wrong merchants back to original owners
- Preserves items bought by player (owned by player, not moved)
- Logs all movements for debugging

### Hybrid Approach: Player Inventory Comparison + Virtual Items

This is the current working approach that combines virtual items for display with player inventory tracking for accurate purchase detection:

1. **Before Trade**: 
   - Capture snapshot of all merchant inventories (item IDs and counts)
   - Capture snapshot of player inventory (blueprint → count)
   - Create VirtualItemRegistry to track mappings

2. **During Trade**: 
   - Create virtual GameObjects representing merged stacks
   - Player trades with virtual items
   - Virtual items are destroyed by game after trade (normal behavior)

3. **After Trade**: 
   - Compare current player inventory to pre-trade snapshot
   - Calculate: `purchased = currentCount - preTradeCount` for each blueprint
   - Use VirtualItemRegistry to determine which merchant items to deduct
   - Deduct in FIFO order from source merchants

**Example:**
```
Before Trade:
  Merchant A: 100 shotgun shells (ID: A1)
  Merchant B: 127 shotgun shells (ID: B1)
  Player: 0 shotgun shells

Virtual Item Created: "shotgun shell x227" (represents A1+B1)

Player buys 50 shells:
  Virtual item destroyed by game
  Player now has: 50 shotgun shells

Cleanup detects:
  - Player purchased 50 shotgun shells (50 - 0 = 50)
  - VirtualItemRegistry shows blueprint "ShotgunShell" came from A1+B1
  - Deduct in FIFO order: 50 from A1
  
Result:
  Merchant A: 50 shells (100 - 50)
  Merchant B: 127 shells (unchanged)
  Player: 50 shells ✓
```

**Key Components:**
- `VirtualItemData` class - Stores virtual item ID, blueprint, original count, and source item mappings
- `VirtualItemRegistry` - Persists even after GameObjects destroyed
- `PlayerInventorySnapshot` - Tracks player inventory before trade
- `CleanupVirtualItems()` - Uses player inventory comparison to detect purchases

**Benefits:**
- Shows merged display (e.g., "x227" instead of separate stacks)
- Accurate purchase tracking via player inventory comparison
- No reliance on destroyed virtual objects
- Properly deducts from correct merchants in FIFO order
- Prevents item duplication

**Notes:**
- Virtual items are destroyed by game during trade (normal behavior)
- Purchase tracking relies on comparing player inventory before/after
- Works correctly even if virtual items don't exist at cleanup time

### UI Synchronization Fix: Force-Close After Trade

**Problem:** After clicking the TRADE button, the game calls `ClearAndSetupTradeUI()` which refreshes the UI. This triggers our `TradeUI.GetObjects` patch, recreating virtual items that were just destroyed, causing purchased items to still appear available (until the UI closes a moment later).

**Failed Attempts:**
1. Call-counter approach - `ClearAndSetupTradeUI()` is called multiple times during initial setup
2. `UpdateViewFromData` patch - reflection to get TradeScreen instance was failing

**Solution:** Patch `TradeUI.PerformOffer` to change the return value from REFRESH/TOP to CLOSE.

**Flow:**
```
User opens Zone Trade
  ↓
TradeScreen opens with consolidated merchant items
  ↓
User clicks TRADE
  ↓
TradeUI.PerformOffer() executes trade → returns REFRESH (normally)
  ↓
PATCH: Changes return value to CLOSE
  ↓
TradeScreen.HandleMenuOption sees CLOSE → calls Hide()
  ↓
CleanupVirtualItems() reconciles merchant inventories
  ↓
Zone Trade state reset
```

See `API.md` section "TradeScreen — Force Close After Trade" for implementation details.

### Error Handling
- No friendly merchants: Show popup "There are no friendly merchants in this area."
- Feature disabled: Keybind does nothing (silent)
- Not on adventure layer: Keybind does nothing

## Dependencies
- None (uses only base game APIs)

## Future Considerations
- Additional QoL features will use same pattern
- Each feature gets its own toggle and keybind
- Modular design for easy expansion

## Lessons Learned

### Harmony Patching Best Practices

1. **Static methods > Async void methods**: When you need to modify behavior, look for static methods that are easier to patch. We tried patching `TradeScreen.HandleMenuOption` (async void) multiple times before realizing we should patch `TradeUI.PerformOffer` (static) instead.

2. **Return value modification > Instance method calls**: Instead of trying to call `Hide()` via reflection on a TradeScreen instance (which failed because `SingletonWindowBase<T>.instance` couldn't be found), we modified the return value of `PerformOffer` from `REFRESH` to `CLOSE`. The game naturally closed the screen based on this return value.

3. **Call counters don't work for initialization**: `ClearAndSetupTradeUI()` is called multiple times during screen initialization, making call-counting unreliable for detecting post-trade state.

4. **Virtual items are destroyed during trade**: The game destroys virtual GameObjects during the trade process (normal behavior). Any tracking must use persistent data structures (like `VirtualItemRegistry`) that survive object destruction.

See `API.md` section "TradeScreen — Force Close After Trade" for the full implementation pattern.

## Testing Checklist
- [x] Mod loads without errors
- [x] Options appear in menu
- [x] Keybind appears and is configurable
- [ ] Zone Trade works with single merchant
- [ ] Zone Trade works with multiple merchants
- [ ] Prices respect faction reputation
- [ ] Selling routes to best price merchant
- [ ] Hostile merchants excluded
- [ ] Merchants on different levels excluded
- [ ] Toggle disables feature
- [x] Works on new games
- [x] Post-trade reconciliation moves items to correct owners
- [x] Items bought by player stay with player
- [x] Reconciliation logs show correct item movements
- [x] Trade UI closes immediately after clicking TRADE (no refresh showing stale data)
- [x] Works on loaded saves
