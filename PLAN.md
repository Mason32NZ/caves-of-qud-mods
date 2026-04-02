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

#### CreateVirtualMerchant()
- Creates temporary GameObject
- Adds Commerce part
- Populates inventory from all friendly merchants
- Returns virtual merchant for trading

#### OpenZoneTrade()
- Checks if feature enabled
- Finds merchants
- Shows error if none found
- Creates virtual merchant
- Fires `CommandTrade` event with virtual merchant as target

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

## Testing Checklist
- [ ] Mod loads without errors
- [ ] Options appear in menu
- [ ] Keybind appears and is configurable
- [ ] Zone Trade works with single merchant
- [ ] Zone Trade works with multiple merchants
- [ ] Prices respect faction reputation
- [ ] Selling routes to best price merchant
- [ ] Hostile merchants excluded
- [ ] Merchants on different levels excluded
- [ ] Toggle disables feature
- [ ] Works on new games
- [ ] Works on loaded saves
