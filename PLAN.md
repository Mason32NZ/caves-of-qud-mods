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

### Stacked Inventory Feature
- [x] Create StackedInventory.cs with stack merging logic
- [x] Add EnableStackedInventory option to Options.xml
- [x] Add OptionFlag to M32_Options class
- [x] Implement CanStackForMerge (reuse Zone Trade logic, only liquid containers can stack)
- [x] Implement StackInventory method
- [x] Add Harmony patch for InventoryScreen.Show() (Postfix)
- [x] Add Harmony patch for TradeScreen.Show() (Prefix to run before Zone Trade)
- [ ] Test with various item types
- [ ] Test compatibility with Zone Trade

### Auto Loot Feature
- [x] Research game's DroppedByPlayer property (built-in int property)
- [x] Create AutoLoot.cs with M32_AutoLootPart
- [x] Add 8 options to Options.xml
- [x] Add option flags to Options.cs
- [x] Implement movement detection (BeforeTakeActionEvent, EnteredCellEvent)
- [x] Implement item pickup on current + adjacent cells
- [x] Implement ShouldAutoLoot() filter
- [x] Implement container content extraction
- [x] Implement auto-explore integration (AutoexploreObjectEvent)
- [x] Add M32_AutoLootPart to PlayerMutator and LoadGameHandler
- [x] Implement ownership check (Skip Owned Items option)
- [x] exclude harvestable objects (plants, etc.)
- [x] exclude garbage/trash items (via Garbage part check)
- [ ] Test liquid handling (containers vs puddles)
- [ ] Test boulder/stone/corpse filtering
- [ ] Test weight checking
- [x] Test dropped item cooldown
- [ ] Test combat toggle
- [ ] Test auto-explore waypoint integration
- [ ] Test ownership check (friendly vs hostile factions)
- [ ] Test harvestable object exclusion (plants, etc.)
- [ ] Test garbage/trash exclusion (Garbage part check)
- [ ] Test performance (9 cells per movement)

### Future Features
- [ ] Quick travel enhancements

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
- Configurable via Keybinds Menu → Mod: M32's QoL

## Feature: Auto Loot

### Description
Automatically picks up nearby items when the player moves. Integrates with auto-explore to add items as waypoints. Highly configurable with multiple options for filtering items and controlling behavior.

### Behavior Specifications

#### Trigger Conditions
- **Movement Detection**: Triggers onevery cell change via `BeforeTakeActionEvent` and `EnteredCellEvent`
- **Combat Mode**: Optional toggle to continue during combat (default: off)
- **Auto-Explore**: Integrates with `AutoexploreObjectEvent` to add items as waypoints

#### Item Detection
- Checks currenttile + 8 adjacent tiles (9 cells total)
- Picks up items in weight order (lowest first)
- Safety limit: Max 10 items per turn to prevent lag

#### Item Filtering (Configurable)
1. **Always Skip**:
   - Items with `NoAutoget` tag/property
   - Items where `IsTakeable()` returns false
   - Items with `Harvestable` part (world decorations like plants, harvestable objects)
     - These are meant to be walked over to harvest resources
     - NOT meant to be picked up as inventory items
   - Items with `Garbage` part (world decorations like garbage, trash)
     - These have `Takeable="true"` but are marked with `<part Name="Garbage" />`
     - They are "gettable terrain" not meantfor inventory
   - Liquid puddles/pools on ground (MaxVolume < 0)

2. **Optional Filters**:
   - Boulders/Stones (Blueprint: SmallBoulder*, MediumBoulder*, LargeBoulder*, "Small Stone", "Large Stone")
   - Corpses (Tag: "DynamicObjectsTable:Corpses")
   - Non-liquid containers (always loot contents, optionally loot empty container)

3. **Weight Check** (Optional, strict):
   - Only pick up if `currentWeight + itemWeight <= maxWeight`
   - Uses `GetCarriedWeight()` and `GetMaxCarriedWeight()`

4. **Dropped Item Cooldown** (Slider: -1 to 100 turns):
   - Uses custom `M32_DroppedTurn` property to track when items were dropped
   - Game sets `DroppedByPlayer=1` flag when player drops items (value is a flag, not turn count)
   - When we first encounter an item with `DroppedByPlayer`, we record the current turn in `M32_DroppedTurn`
   - Subsequent checks use `M32_DroppedTurn` for cooldown calculation
   - `-1` = Never pickup dropped items
   - `0` = Pickup immediately
   - `5` (default) = Wait 5 turns before auto-looting

#### Container Handling
- **Non-liquid containers** (chests, sacks, etc.):
  - Always loot contents first (unless auto-exploring - game handles it)
  - Optionally loot empty container based on setting
- **Liquid containers** (waterskins, vials):
  - Always loot (MaxVolume >= 0 = container, notpuddle)
  - Can contain liquids and be looted

### Options

1. **Enable Auto Loot** (Checkbox, default: Yes)
   - Master toggle for the feature

2. **Auto Loot During Combat** (Checkbox, default: No)
   - Continue looting while enemies nearby
   - Requires master toggle enabled

3. **Check Weight Capacity** (Checkbox, default: Yes)
   - Only loot if won't become over-encumbered
   - Strict weight checking

4. **Loot Empty Containers** (Checkbox, default: Yes)
   - Loot empty non-liquid containers (chests, sacks)
   - Contents are always looted from containers

5. **Loot Corpses** (Checkbox, default: Yes)
   - Auto-loot corpses
   - Uses "DynamicObjectsTable:Corpses" tag

6. **Loot Boulders & Stones** (Checkbox, default: No)
   - Auto-loot boulders and stones (heavy items)
   - Checks blueprint prefix ("Boulder", "Stone", "Rock")

7. **Dropped Item Cooldown** (Slider, -1 to 100, default: 5)
   - Don't auto-loot items you dropped within X turns
   - `-1` = Never pickup dropped items
   - Uses game's `DroppedByPlayer` property

8. **Skip Owned Items** (Checkbox, default: Yes)
   - Don't auto-loot items that belong to others (would be considered stealing)
   - Checks `item.IsOwned()` and `item.Owner` property
   - Uses `GetAllegianceLevel()` to determine factionrelationship
   - **Option C behavior**: Skip owned items EXCEPT if owner is hostile
     - If `AllegianceLevel.Member` (friendly/allied) → Skip (don't steal from friends)
     - If `AllegianceLevel.None` (no relationship/unknown) → Allow (per user requirement)
     - If hostile/negative → Allow (loot from enemies)
   - Containers: If container is owned, skip the entire container

### Technical Implementation

#### Cell Tracking
- Tracks `_lastCell` and `_lastMoveTurn` to prevent duplicate triggers
- Only triggers once per move action

#### Performance Optimization
- Safety limit of 10 items per turn
- Skips combat check if feature disabled
- Uses existing game systems (no custom tracking)

#### Auto-Explore Integration
- Uses `AutoexploreObjectEvent` with `E.Command = "Autoget"`
- Game's existing auto-explore handles pathfinding to items
- **Note**: Removed `AutoAct` check for container contents (AutoAct not available in game version 1.0.4)
- Container content extraction during auto-explore may be redundant, but harmless
- Ownership check still prevents stealing from friendly factions

### Known Issues / Notes
- Performance acceptable at 9 cells per move (may revisit if issues)
- Uses custom `M32_DroppedTurn` property for dropped item cooldown
  - Game's `DroppedByPlayer` is a flag (value=1), not a turn count
  - We record the turn when we first encounter a dropped item
  - Cooldown calculated using our `M32_DroppedTurn` property
- Liquid puddles detected via `LiquidVolume.MaxVolume < 0`
- `AutoAct` class not available in some game versions - removed related checks
- Harvestable objects (plants, etc.) excluded via `Harvestable` part check
  - These are world decorations meant to be walked over, not picked up
- Garbage/trash items excluded via `Garbage` part check
  - These have `Takeable="true"` but are marked with `<part Name="Garbage" />`
  - They are "gettable terrain" not meantfor inventory

## File Structure

```
M32_QoL/
├── manifest.json               # Mod identity and metadata
├── Commands.xml                # Keybind definitions
├── Options.xml                 # Options menu entries
├── Options.cs                  # Shared options class (M32_Options)
├── HarmonyInitializer.cs       # Harmony patch initialization
├── PlayerMutator.cs             # New game initialization
├── LoadGameHandler.cs           # Save game loading handler
├── features/                    # Feature implementations
│   ├── AutoLoot.cs             # Auto Loot implementation
│   ├── StackedInventory.cs     # Stacked Inventory implementation
│   └── ZoneTrade.cs            # Zone Trade implementation
└── PLAN.md                     # This file
```

## Implementation Details

### C# Classes

#### 1. M32_Options (Static)
```csharp
[HasOptionFlagUpdate(Prefix = "Option_M32QoL_")]
public static class M32_Options
{
    [OptionFlag] public static bool EnableZoneTrade;
        [OptionFlag] public static bool EnableStackedInventory;
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

---

## Feature: Stacked Inventory

### Description
Automatically merges compatible item stacks in the player's inventory when opening the inventory screen or trade screen. Reduces UI clutter and scrolling.

### Behavior Specifications

#### Trigger Conditions
- **When enabled**: Runs automatically when player opens:
  1. Inventory screen (via 'i' key or other methods)
  2. Trade screen (before Zone Trade processes)

#### Stack Compatibility Rules
Same rules as Zone Trade:
- Same blueprint (item type)
- Not Unique items
- Not Data Disks (unique data per disk)
- Not Unidentified Artifacts
- No liquid contents
- Same modification status (both have mods or neither) - modifications compared by type (e.g., Sharp vs Heavy)
- **Only LIQUID containers can stack** (if empty). Non-liquid containers (chests, bags, etc.) NEVER stack.
- Same Number property (for numbered items like scrolls)
- Same visual properties (ColorString, DetailColor, Tile, RenderString)
- Items are sorted alphabetically before grouping to ensure consistent merge behavior

#### Implementation Details
- **Silent operation**: No UI feedback to player
- **No stack limit**: Unlimited stack size
- **Debug logging**: Logs merged stack counts to Player.log for development
- **Execution order**: Runs as Prefix on TradeScreen (before Zone Trade)
- **Safety**: Uses locking to prevent concurrent execution; validates items before removal

### Options
- **Toggle**: `Option_M32QoL_EnableStackedInventory` (Checkbox, Default: Yes)
- Located in: Options Menu → Mod: M32's QoL
- Disabling prevents automatic stacking

### Keybind
- None required (automatic feature)

### Testing Checklist
- [ ] Stacked Inventory works when opening inventory
- [ ] Stacked Inventory runs before Zone Trade when opening trade
- [ ] Compatible items are merged
- [ ] Incompatible items are not merged
- [ ] Non-liquid containers (chests, bags) do NOT stack
- [ ] Empty liquid containers (waterskins) CAN stack
- [ ] Liquid containers with contents do NOT stack
- [ ] Items with different modifications do NOT stack
- [ ] Data Disks are not stacked
- [ ] Unidentified artifacts are not stacked
- [ ] Items are sorted alphabetically before stacking
- [ ] Toggle disables feature
- [ ] No errors in Player.log
- [ ] Works with loaded saves
- [ ] Works on new games
