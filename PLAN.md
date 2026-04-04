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

### Stacked Inventory Feature
- [x] Create StackedInventory.cs with stack merging logic
- [x] Add EnableStackedInventory option to Options.xml
- [x] Add OptionFlag to M32_Options class
- [x] Implement CanStackForMerge (reuse Zone Trade logic, only liquid containers can stack)
- [x] Implement StackInventory method
- [x] Add Harmony patch for InventoryScreen.Show() (Postfix)
- [x] Add Harmony patch for TradeScreen.Show() (Prefix to run before Zone Trade)

### Auto Loot Feature
- [x] Research game's DroppedByPlayer property (built-in int property)
- [x] Create AutoLoot.cs with M32_AutoLootPart
- [x] Add 8 options to Options.xml (now 9 options including EnableAutoLoot master toggle)
- [x] Add option flags to Options.cs
- [x] Implement movement detection (BeforeTakeActionEvent, EnteredCellEvent)
- [x] Implement item pickup on current + adjacent cells (9 cells total)
- [x] Implement ShouldAutoLoot() filter with exclusions
- [x] Implement container content extraction
- [x] Implement auto-explore integration (AutoexploreObjectEvent)
- [x] Add M32_AutoLootPart to PlayerMutator and LoadGameHandler
- [x] Implement ownership check (Avoid Stealing option using GetAllegianceLevel)
- [x] Exclude harvestable objects (plants, etc. via Harvestable part check)
- [x] Exclude garbage/trash items (via Garbage part check)
- [x] Implement weight capacity checking (Check Weight Capacity option)
- [x] Implement dropped item cooldown (using M32_DroppedTurn custom property)
- [x] Implement combat toggle (Loot During Combat option)

### Consolidate Liquids Feature
- [x] Add EnableConsolidateLiquids option to Options.xml
- [x] Add EnableConsolidateLiquids option flag to Options.cs
- [x] Create ConsolidateLiquids.cs with M32_ConsolidateLiquidsPart
- [x] Implement GetMixtureKey() with GCD-based ratio normalization
- [x] Implement PourLiquid() using Split() and MixWith()
- [x] Implement ConsolidateLiquids() main logic
- [x] Add neutron flux exclusion (liquid ID: "neutronflux")
- [x] Add command handler via HandleEvent pattern (M32_ConsolidateLiquidsPart)
- [x] Add Ctrl+L keybind to Commands.xml
- [x] Register M32_ConsolidateLiquidsPart in LoadGameHandler
- [x] Add player messages ("Consolidating liquids..." / "Consolidation of liquids complete!")
- [x] Refactor and clean up code (constants, simplified conditionals)

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
- **Master Toggle**: `Option_M32QoL_EnableZoneTrade` - "{{M|Feature: Zone Trade}} (Requires Keybind - Check Control Mappings under 'Mod: M32's QoL')" (Checkbox, Default: Yes)
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
- **Movement Detection**: Triggers on every cell change via `BeforeTakeActionEvent` and `EnteredCellEvent`
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
      - They are "gettable terrain" not meant for inventory
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
  - Always loot (MaxVolume >= 0 = container, not puddle)
  - Can contain liquids and be looted

### Options

1. **Enable Auto Loot** (`Option_M32QoL_EnableAutoLoot`) - "{{M|Feature: Auto Loot}}" (Checkbox, Default: Yes)
   - Master toggle for the feature

2. **Loot During Combat** (`Option_M32QoL_AutoLootInCombat`) - "Loot During Combat" (Checkbox, Default: Yes, Requires: EnableAutoLoot)
   - Continue looting while enemies nearby

3. **Avoid Stealing** (`Option_M32QoL_AutoLootSkipOwned`) - "Avoid Stealing" (Checkbox, Default: Yes, Requires: EnableAutoLoot)
   - Don't auto-loot items that belong to others (would be considered stealing)
   - Checks `item.IsOwned()` and `item.Owner` property
   - Uses `GetAllegianceLevel()` to determine faction relationship
   - **Option C behavior**: Skip owned items EXCEPT if owner is hostile
     - If `AllegianceLevel.Member` (friendly/allied) → Skip (don't steal from friends)
     - If `AllegianceLevel.None` (no relationship/unknown) → Allow (per user requirement)
     - If hostile/negative → Allow (loot from enemies)
   - Containers: If container is owned, skip the entire container

4. **Check Weight Capacity** (`Option_M32QoL_AutoLootCheckWeight`) - "Check Weight Capacity" (Checkbox, Default: Yes, Requires: EnableAutoLoot)
   - Only loot if won't become over-encumbered
   - Strict weight checking

5. **Pickup Player Dropped Items After X Turns** (`Option_M32QoL_AutoLootDroppedCooldown`) - (Slider, -1 to 100, Default: 5, Requires: EnableAutoLoot)
   - Don't auto-loot items you dropped within X turns
   - `-1` = Never pickup dropped items
   - `0` = Pickup immediately
   - `5` (default) = Wait 5 turns before auto-looting
   - Uses game's `DroppedByPlayer` property and custom `M32_DroppedTurn` property

6. **Pickup Empty Chests** (`Option_M32QoL_AutoLootEmptyContainers`) - "Pickup Empty Chests" (Checkbox, Default: Yes, Requires: EnableAutoLoot)
   - Loot empty non-liquid containers (chests, sacks)
   - Contents are always looted from containers

7. **Pickup Corpses** (`Option_M32QoL_AutoLootCorpses`) - "Pickup Corpses" (Checkbox, Default: No, Requires: EnableAutoLoot)
   - Auto-loot corpses
   - Uses "DynamicObjectsTable:Corpses" tag

8. **Pickup Boulders and Stones** (`Option_M32QoL_AutoLootBouldersStones`) - "Pickup Boulders and Stones" (Checkbox, Default: No, Requires: EnableAutoLoot)
   - Auto-loot boulders and stones (heavy items)
   - Checks blueprint prefix ("Boulder", "Stone", "Rock")

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
  - They are "gettable terrain" not meant for inventory

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
│   ├── ConsolidateLiquids.cs    # Consolidate Liquids implementation
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

## Lessons Learned

### Harmony Patching (Zone Trade)

We encountered several challenges that required specific solutions:

1. **Patch target selection**: We tried patching `TradeScreen.HandleMenuOption` (async void) multiple times before finding that patching `TradeUI.PerformOffer` (static) was more reliable.

2. **UI state management**: Attempting to get a `TradeScreen` instance via `SingletonWindowBase<T>.instance` reflection failed. Instead, we modified the return value of `PerformOffer`.

3. **Multiple initialization calls**: `ClearAndSetupTradeUI()` is called multiple times during screen setup, making call-counting unreliable for detecting post-trade state.

4. **Virtual item lifecycle**: Virtual GameObjects are destroyed during trade processing, requiring persistent tracking via `VirtualItemRegistry`.

See `API.md` section "Harmony Patching Lessons Learned" for general principles derived from these experiences.

## Future Considerations
- Each feature follows the same pattern: Part class + Events + Options.xml toggle
- Features that need manual activation use Commands.xml keybinds
- Automatic features (like Stacked Inventory) use Harmony patches instead
- Modular design allows easy expansion

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
- **Toggle**: `Option_M32QoL_EnableStackedInventory` - "{{M|Feature: Stacked Inventory}}" (Checkbox, Default: Yes)
- Located in: Options Menu → Mod: M32's QoL
- Disabling prevents automatic stacking

### Keybind
- None required (automatic feature)

---

## Feature: Consolidate Liquids

### Description
When the player presses Ctrl+L, merges liquids of the same type (by ratio) into containers with the largest capacity, filling them first. Works from anywhere in the game.

### Behavior Specifications

#### Liquid Mixing Rules
- **Ratio-based matching**: Only containers with the exact same liquid ratio are consolidated
  - Example: Container A (50 water + 30 wine) consolidates with Container B (25 water + 15 wine)
  - Both have ratio 5:3 (water:wine)
- **GCD normalization**: Uses greatest common divisor to calculate ratio
  - Container with 50 water + 30 wine → ratio "water:5|wine:3"
- **Empty containers ignored**: Only containers with Volume > 0 participate

#### Safety Exclusions
- **NEVER consolidate neutron flux**: Liquid ID "neutronflux" is explicitly excluded
  - Neutron flux explodes if moved between containers
  - Any container containing neutron flux is skipped entirely

#### Fill Algorithm
1. Group containers by ratio-based mixture key
2. For each group with 2+ containers:
   a. Sort by MaxVolume descending (largest first)
   b. Calculate total liquid volume across all containers
   c. Fill containers in order: fill first to max, then move to next
   d. Use Split() and MixWith() for liquid transfer

#### Pour Implementation
- Uses `LiquidVolume.Split(int drams)` to create split volume from source
- Uses `LiquidVolume.MixWith(LiquidVolume)` to combine into target
- Errors logged and skipped (no popup for individual failures)

### Options
- **Toggle**: `Option_M32QoL_EnableConsolidateLiquids` - "{{M|Feature: Consolidate Liquids}} (Requires Keybind - Check Control Mappings under 'Mod: M32's QoL')" (Checkbox, Default: Yes)
- Located in: Options Menu → Mod: M32's QoL
- Disabling prevents the keybind from working

### Keybind
- **Command ID**: `M32QoL_ConsolidateLiquids`
- **Default**: Ctrl+L
- **Category**: Mod: M32's QoL
- Configurable via Keybinds Menu → Mod: M32's QoL

### Command Handler
Uses the same Part-based pattern as ZoneTrade:
```csharp
public override bool HandleEvent(CommandEvent E)
{
    if (E.Command != "M32QoL_ConsolidateLiquids")
        return base.HandleEvent(E);

    if (!M32_Options.EnableConsolidateLiquids)
        return false;

    ConsolidateLiquids(The.Player);
    return false;
}
```

### Logging
- Logs consolidation summary to Player.log (e.g., "Made 5 liquid transfers across 2 groups")
- Shows player messages: "Consolidating liquids..." at start, "Consolidation of liquids complete!" at end
- Logs errors for individual container failures and continues (no popup)

### Technical Implementation

#### Mixture Key Generation
```csharp
// Container with 50 water + 30 wine (proportions 500:300)
// GCD(500, 300) = 100
// Normalized: "water:5|wine:3" (sorted alphabetically)
private static string GetMixtureKey(LiquidVolume vol)
{
    var components = vol.ComponentLiquids
        .Where(kv => kv.Value > 0)
        .OrderBy(kv => kv.Key)
        .ToList();
    
    if (components.Count == 0) return "";
    
    int gcd = components[0].Value;
    foreach (var kv in components)
        gcd = GCD(gcd, kv.Value);
    
    return string.Join("|", components.Select(kv => 
        $"{kv.Key}:{kv.Value / gcd}"));
}
```

#### Pour Liquid Transfer
```csharp
private static bool PourLiquid(GameObject source, GameObject target, int drams)
{
    LiquidVolume sourceVol = source.LiquidVolume;
    LiquidVolume targetVol = target.LiquidVolume;
    
    int availableSpace = targetVol.MaxVolume - targetVol.Volume;
    int toTransfer = Math.Min(drams, availableSpace);
    
    LiquidVolume split = sourceVol.Split(toTransfer);
    bool success = targetVol.MixWith(split);
    
    return success;
}
```

### Known Issues
- Neutron flux cannot be consolidated (by design - would explode)
- Errors on individual containers don't show popup - logged only

### Code Quality
- Uses LOG_PREFIX constant for consistent logging
- Uses UNSAFE_LIQUID constant for the neutronflux exclusion
- Simplified conditionals and early returns throughout
- GCD calculation uses LINQ Aggregate for conciseness
