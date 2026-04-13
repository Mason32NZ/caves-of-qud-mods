# AGENTS.md - Coding Agent Instructions

**Read `API.md` before working on Caves of Qud mods.** It contains critical pitfalls and API patterns.

---

## Naming Conventions

- **ALL class names MUST use `M32_` prefix** (e.g., `M32_Options`, `M32_ZoneTradePart`)
- **PascalCase** for public methods, properties, classes
- **_camelCase** for private fields (e.g., `_lastCell`)
- **ALL_CAPS** for constants

## Code Style

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

using XRL;
using XRL.Core;
using XRL.World;
using XRL.World.Parts;

using HarmonyLib;

namespace M32_QoL
{
    public class M32_ExamplePart : IScribedPart
    {
        private const int MAX_ITEMS = 10;
        private int _counter = 0;
        public override bool AllowStaticRegistration() => true;
        public override bool WantEvent(int ID, int cascade) =>
            base.WantEvent(ID, cascade) || ID == CommandEvent.ID;
    }
}
```

## File Organization

```
M32_QoL/
├── manifest.json, Commands.xml, Options.xml
├── Options.cs, HarmonyInitializer.cs
├── Helpers.cs
├── PlayerMutator.cs, LoadGameHandler.cs
└── features/*.cs
```

## Shared Functionality

Any functionality that is shared across features should go in `Helpers.cs`. This includes:

- `CanStackForMerge()` — item stacking comparison logic; never stacks containers or liquid containers (used by StackedInventory and ZoneTrade)
- `SameDisplayName()` — safe display name comparison that avoids DescriptionBuilder crash
- `SafeGetName()` — safe display name retrieval for logging (avoids DescriptionBuilder crash)
- `SortItemsByBlueprint()` — safe item sorting by Blueprint (avoids DescriptionBuilder crash)
- `IsLiquidContainer()` — liquid container detection (MaxVolume >= 0)
- `HaveSameTags()` — blueprint + runtime tag comparison

## Part Pattern

- Use `IScribedPart` for all new parts
- Override `AllowStaticRegistration() => true` for player parts
- Use `WantEvent` + `HandleEvent` pattern for events

## Testing

No build step — runtime-compiled. Test in-game:

1. Start game with mod enabled
2. Check `Player.log` for errors
3. Test feature in-game
4. Check `Player.log` for runtime errors

**Player.log**: `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Player.log`  
**In-game console**: `Ctrl+Shift+E`

## Quick Reference

| Task | Location |
|------|----------|
| Add feature option | `Options.xml` → `[OptionFlag]` in `Options.cs` |
| Add keybind | `Commands.xml` |
| Add part | Create in `features/` → Add to `PlayerMutator.cs` + `LoadGameHandler.cs` |
| Add Harmony patch | Create patch class → `HarmonyInitializer.cs` applies via `PatchAll()` |
| Add shared logic | Add to `Helpers.cs` (used across features) |
| Debug errors | Check `Player.log` for exceptions |
| Decompile game | `ilspy --assembly "Assembly-CSharp.dll" --output "temp/decompiled/"` |

## Harmony Patches

**Always decompile before patching.** Use `ilspy` to verify method signatures.

```bash
ilspy --assembly "Assembly-CSharp.dll" --output "temp/decompiled/"
```

**Update `API.md`** when you discover new patterns or fix errors. It's a reference, not truth — decompiled code is authoritative.

## Debugging

When user reports issues, **read `Player.log`**. Look for:
- Exceptions with stack traces
- Mod class names or ID
- Error messages

Don't guess — read the log.