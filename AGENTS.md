# AGENTS.md - Coding Agent Instructions

## IMPORTANT: Read Before Working on Caves of Qud Mods

**If you are working on a mod for Caves of Qud, you MUST read the `API.md` file first.**

The `API.md` file contains comprehensive documentation about:
- The Caves of Qud modding API
- Critical pitfalls and common mistakes
- GameObject, Part, and Event systems
- XML blueprint structures
- Proper C# patterns for CoQ modding

**This is essential context** before making any changes to CoQ-related code. The API documentation is based on decompiled game source code and wiki information.

---

## C# Code Style for Caves of Qud Mods

### Naming Conventions
- **PascalCase** for: public methods, properties, classes, events
- **camelCase with underscore prefix** for: private fields (e.g., `_lastCell`, `_counter`)
- **ALL_CAPS** for: constants (e.g., `MAX_ITEMS`)
- **ALL class names MUST use the `M32_` prefix** for this mod

### File Organization
- One class per file
- File name matches class name (e.g., `M32_ZoneTradePart.cs`)
- Feature files go in `features/` subdirectory
- Core files (Options, HarmonyInitializer, etc.) go in root `M32_QoL/` directory

### Using Statements
Group imports in this order:
1. `System` namespaces
2. `XRL` namespaces (XRL, XRL.Core, XRL.World, XRL.UI, etc.)
3. Third-party libraries (HarmonyLib)

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
        
        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == CommandEvent.ID;
        }
        
        public void DoSomething()
        {
            // Implementation
        }
    }
}
```

### Part Class Pattern
- Use `IScribedPart` for all new parts (enables field migration)
- Override `AllowStaticRegistration() => true` for player parts
- Use `WantEvent` + `HandleEvent` pattern for events

### Error Handling
- Use try/catch around risky operations
- Log errors to `Player.log` using `UnityEngine.Debug.LogError("[M32_QoL] message")`
- Provide meaningful error messages with context
- Never swallow errors silently

---

## Game Version Compatibility

- This mod is developed for **Caves of Qud Build 2.0.2xx+**
- API patterns may change between game versions
- If the game updates, verify that APIs still work correctly
- Check `Player.log` for deprecation warnings after game updates

---

## Testing Workflow

Since CoQ mods are runtime-compiled, there's no build step. Testing happens in-game:

### Development Cycle
1. **Start the game** with mod enabled
2. **Check Player.log** for compilation errors on load
3. **Test the feature** in-game
4. **Check Player.log** again for runtime errors
5. **Iterate**: Modify code → Reload mod (or restart game) → Test again

### Debugging Tools
- **Player.log**: `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Player.log`
- **In-game console**: `Ctrl+Shift+E` opens the debugging console
- **Logging**: Use `UnityEngine.Debug.Log("[M32_QoL] message")` for debug output

---

## Quick Reference

| Task | Location/Command |
|------|------------------|
| Add new feature option | `Options.xml` → add `[OptionFlag]` to `Options.cs` |
| Add new keybind | `Commands.xml` |
| Add new part | Create `M32_FeaturePart.cs` → Add to `PlayerMutator.cs` + `LoadGameHandler.cs` |
| Add Harmony patch | Create patch class → Call from `HarmonyInitializer.cs` |
| Debug errors | Check `Player.log` for exceptions and stack traces |
| Decompile game code | `ilspy --assembly "Assembly-CSharp.dll" --output "temp/decompiled/"` |
| Check mod loaded | Look for `[M32_QoL] M32_...Part attached to...` in Player.log |

---

## Temp Folder Usage

- **You can freely use the `temp/` folder** for any temporary files, working data, or outputs that should not be committed to version control
- The `temp/` folder is already in `.gitignore` and will not be tracked
- Common uses for the temp folder:
  - Decompiled game files from `ilspy` → `temp/decompiled/`
  - Intermediate build artifacts
  - Test data or scratch files
  - Debugging outputs

---

## Mod Manifest

The `manifest.json` file defines the mod's identity:

```json
{
    "ID": "M32_QoL",
    "Title": "{{C|M32's QoL}}",
    "Description": "Quality of life improvements...",
    "Version": "1.0.0",
    "Author": "M32",
    "Tags": "QoL,UI,Merchants"
}
```

- `ID` must be unique across all mods
- `{{C|...}}` applies color formatting in-game
- Increment `Version` for each release

---

## M32's QoL Mod - Agent Guidelines

When working on the M32's QoL Caves of Qud mod:

### Naming Conventions
- **ALL C# class names MUST use the `M32_` prefix** for consistency:
  - ✅ `M32_Options`, `M32_PlayerMutator`, `M32_ZoneTradePart`
  - ❌ `M32QoLOptions`, `M32QoL_PlayerMutator`, `ZoneTradePart`

### PLAN.md Maintenance
- **You MUST keep the TODO section in `PLAN.md` up to date**
- Mark items as completed (`[x]`) when you finish them
- Add new features/tasks to the TODO as they are identified
- Update implementation details if code structure changes
- Use the TODO section to track progress and remaining work

### File Organization
- All mod files go in `M32_QoL/` directory
- Each feature should have its own toggle in `Options.xml`
- Each feature should have its own keybind in `Commands.xml` (if needed)
- Main logic goes in appropriately named `.cs` files

### API Documentation Maintenance
- **When you fix errors and learn something new about the CoQ API, you MUST update `API.md`**
- Add the corrected information with clear examples showing what works vs. what doesn't
- Use comments like `// CORRECT` and `// WRONG` to make it obvious
- This helps future agents avoid the same mistakes
- Example: If you discover TradeEntry uses constructor parameters not property initializers, add that to the Trade API section

### Separation of Concerns: PLAN.md vs API.md

To avoid duplication and keep documentation organized, follow this separation:

**`PLAN.md`** — Project-specific implementation details:
- TODO lists and task tracking
- Feature specifications and behavior descriptions
- Project architecture and file organization
- Testing checklists
- High-level implementation approach (pseudocode/diagrams)
- Lessons learned at the conceptual level
- **Cross-references to API.md** for reusable code patterns

**`API.md`** — General Caves of Qud modding API reference:
- Reusable code patterns and examples
- API documentation for common operations
- Working solutions to common problems
- Critical pitfalls and best practices
- Complete working code examples that can be copied/pasted

**When adding content:**
- If it's a **general API pattern** that could help other mods → put it in `API.md`
- If it's **specific to this mod's implementation** → put it in `PLAN.md`
- If `PLAN.md` needs a code example that's already in `API.md` → use a cross-reference like:  
  "See `API.md` section 'TradeScreen — Force Close After Trade' for implementation"
- **Never duplicate** large code blocks between files — this creates maintenance burden

**Examples:**
- ✅ PLAN.md: "We use the pattern described in API.md's 'Multi-Merchant Trade' section"
- ✅ API.md: Full Harmony patch implementation with complete code example
- ❌ PLAN.md: Copy-pasting the full Harmony patch code that's already in API.md

---

## Harmony / Decompilation / Debugging

**ALWAYS DECOMPILE BEFORE YOU PATCH.** When working on Harmony patches, you MUST use `ilspy` to inspect the actual game code before writing your patch. Do not guess. Do not assume. Decompile and verify.

### When to Use ilspy
- **BEFORE making any Harmony patch** — this is not optional
- **When uncertain about the API** — if you're not 100% sure about a method signature, field name, or class structure, decompile and check
- **To understand internal logic** — game systems often have unexpected behavior that you can only understand by reading the decompiled code
- **To debug patches that aren't working** — if your patch doesn't work as expected, decompile to see what the actual code does

### ilspy Command
```bash
ilspy --assembly "path/to/Assembly-CSharp.dll" --output "temp/decompiled/"
```

**Decompiled output MUST go to `temp/decompiled/`** — use the project's temp folder to keep decompiled files organized and out of version control.

### API.md is NOT Infallible
It is a reference, not the source of truth. The source of truth is the decompiled game code.

**You MUST update `API.md` when you discover:**
- New patterns or APIs not documented in `API.md`
- Information that conflicts with what's in `API.md`
- Better examples or clearer explanations than what exists
- Common pitfalls you encountered that aren't documented

**After using `ilspy`, ask yourself:** Did I learn something new? Is this in `API.md`? Is it accurate? If not, update `API.md` for future agents.

---

## File Access — You Have Full Read Access

**You have read access to ALL files you need.** This includes:

- **Mod files** — Read files in `M32_QoL/` directory anytime you need to understand the current implementation
- **Game files** — Read documents, search for patterns, and refer to existing working code in the codebase
- **Decompiled game assemblies** — Use `ilspy` to decompile and inspect game internals (Assembly-CSharp.dll, etc.)
- **Player.log** — Located at `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Player.log` on Windows

**When making changes:**
1. Read the relevant existing files first
2. Understand the current implementation before modifying
3. Search for similar patterns in existing code
4. Refer to `API.md` for documented patterns

**Do NOT operate in isolation.** Read files. Search for patterns. Understand before you write. And always decompile when working with Harmony patches.

---

## Debugging — Check Player.log When Issues Arise

**When the user reports an issue after testing, you MUST check Player.log.**

The Player.log file at `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Player.log` contains:
- Runtime errors and exceptions
- Stack traces with line numbers
- Debug output from the game
- Mod loading errors
- Harmony patch failures

**After the user tests and reports a problem:**
1. Read the Player.log file
2. Look for exceptions, errors, or warnings related to your changes
3. Search for your class names or mod ID in the log
4. Identify the root cause from the error messages
5. Fix the issue based on what the log tells you

**Do not guess at fixes.** Read the log. Understand what failed. Then fix it.