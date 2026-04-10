# Caves of Qud Modding API Reference

**Source**: Decompiled Assembly-CSharp.dll + Official Wiki + Trial-and-Error  
**Target**: CoQ Build 2.0.2xx+

---

## CRITICAL PITFALLS

| Pitfall | Wrong | Correct |
|---------|-------|---------|
| PlayerMutator method | `Mutate()` | `mutate()` (lowercase!) |
| GameObject.Value type | `int` | `double` |
| Inventory.AddObject params | `(item, false)` | `(item, null)` — second is IEvent |
| Inventory.RemoveObject params | `(item, false)` | `(item, null)` |
| GameObject.ID | `.id` | `.ID` (uppercase) |
| TradeScreen close method | `"OnExit"` | `"Hide"` or `"Cleanup"` |
| Blueprint "Base_NPC" | Valid | **Invalid** — use actual blueprint names |
| `[HasOptionFlagUpdate]` | On any class | **Must be on static class** |
| WantEvent returns false | HandleEvent works | **HandleEvent never called** |
| `IsModified()` | Exists | **Doesn't exist** — use `HasPart<Modification>()` |
| Modification part | `HasPart("Modification")` | **Wrong** — mods are separate parts (`ModSharp`, `ModHeavy`, etc.) |
| DroppedByPlayer | Turn count | **Flag (value=1)** — track turn yourself |
| LiquidVolume.MaxVolume < 0 | Container | **Puddle on ground** — not a container |
| `GameObject.DisplayName` | Always safe | **Can throw** — triggers `DescriptionBuilder.descOrderComparison` which crashes on null dictionary keys. Wrap in try-catch or use `Blueprint` instead |
| `GameObject.DisplayNameStripped` | Always safe | **Can throw** — same bug as `DisplayName` |
| Waterskin has `Inventory` | Has container part | **No Inventory part** — only has `LiquidVolume`. Use `a.LiquidVolume != null` to detect liquid containers, NOT `a.GetPart<Inventory>()` |
| `LiquidVolume.SameAs()` | Compares liquids | **Always returns false** — use `LiquidVolume.LiquidSameAs()` for liquid comparison |
| Items with `MagazineAmmoLoader` | Only have ammo | **Also have `EnergyCellSocket`** — energy weapons like laser rifles have BOTH parts. Check both when processing items |

---

## Mod File Structure

```
MyMod/
├── manifest.json      # Mod identity
├── Commands.xml       # Keybinds
├── Options.xml        # Settings
├── *.cs               # C# code (any filename)
└── Textures/          # Tile images
```

---

## Adding Code to Player

**Use both** to cover new games and loaded saves:

```csharp
// New games
[PlayerMutator]
public class MyMod_PlayerMutator : IPlayerMutator
{
    public void mutate(GameObject player)  // lowercase!
    {
        MyMod_HarmonyInitializer.Init();
        player.RequirePart<MyMod_Part>();
    }
}

// Loaded saves
[HasCallAfterGameLoaded]
public class MyMod_LoadGameHandler
{
    [CallAfterGameLoaded]
    public static void AfterLoaded()
    {
        if (The.Player == null) return;
        MyMod_HarmonyInitializer.Init();
        The.Player.RequirePart<MyMod_Part>();
    }
}
```

---

## Part Types

| Type | Use |
|------|-----|
| `IScribedPart` | **Recommended** — easy field migration |
| `IPart` | General part |
| `IPlayerPart` | Follows player across body changes |
| `IActivePart` | Only active under conditions |
| `IZonePart` | Attached to zones |

---

## Event System

### WantEvent + HandleEvent (Preferred)

```csharp
public override bool WantEvent(int ID, int cascade)
{
    return base.WantEvent(ID, cascade)
        || ID == CommandEvent.ID
        || ID == BeforeTakeActionEvent.ID
        || ID == AutoexploreObjectEvent.ID;  // MUST return true for HandleEvent
}

public override bool HandleEvent(CommandEvent E)
{
    if (E.Command == "MyModCommand")
    {
        // handle it
        return false;  // stop propagation
    }
    return base.HandleEvent(E);  // continue propagation
}
```

### Common Events

| Event | When |
|-------|------|
| `CommandEvent.ID` | Keybind pressed |
| `BeforeTakeActionEvent.ID` | Before player action |
| `EnteredCellEvent.ID` | Player enters cell |
| `AutoexploreObjectEvent.ID` | Autoexplore considers object |
| `AfterGameLoadedEvent.ID` | Save loaded |
| `GetShortDescriptionEvent.ID` | Get item description |

---

## Commands.xml (Keybinds)

```xml
<?xml version="1.0" encoding="utf-8"?>
<commands>
    <command ID="MyMod_Command" DisplayText="Do Thing"
             Category="My Mod" Layer="Adventure" Auto="Down">
        <keyboardBind Modifier="ctrl" Key="t"/>
    </command>
</commands>
```

---

## Options.xml

```xml
<?xml version="1.0" encoding="utf-8"?>
<options>
    <option ID="Option_MyMod_Enable" DisplayText="Enable Feature"
            Category="Mod: My Mod" Type="Checkbox" Default="Yes">
        <helptext>Description.</helptext>
    </option>
    <option ID="Option_MyMod_Amount" DisplayText="Amount"
            Category="Mod: My Mod" Type="Slider"
            Min="0" Max="100" Increment="5" Default="50"
            Requires="Option_MyMod_Enable==Yes"/>
</options>
```

### OptionFlag (Auto-synced)

```csharp
[HasOptionFlagUpdate(Prefix = "Option_MyMod_")]  // static class required
public static class MyModOptions
{
    [OptionFlag] public static bool Enable;
    [OptionFlag] public static int Amount;
    [OptionFlag] public static string Mode;
}
```

---

## AllowStaticRegistration

```csharp
// Re-register on every save load (for player parts)
public override bool AllowStaticRegistration() => true;
```

---

## Serialization

### IScribedPart (Recommended)

```csharp
[Serializable]
public class MyPart : IScribedPart  // Fields auto-serialized with names
{
    public string MyField = "default";  // Can add/remove fields freely
}
```

### IPart (Manual)

```csharp
[Serializable]
public class MyPart : IPart
{
    [NonSerialized] public List<GameObject> _cache;  // Not saved
    
    public override void Write(GameObject Basis, SerializationWriter Writer)
    {
        Writer.Write(myString);
        Writer.Write(myInt);
    }
    
    public override void Read(GameObject Basis, SerializationReader Reader)
    {
        myString = Reader.ReadString();
        myInt = Reader.ReadInt32();
    }
}
```

---

## GameObject API

### Getting/Checking Parts

```csharp
MyPart p = go.GetPart<MyPart>();
bool has = go.HasPart<MyPart>();
bool has = go.HasPart("PartName");
go.RequirePart<MyPart>();  // Add if not present
go.RemovePart<MyPart>();
```

### Identity & State

```csharp
bool isPlayer = go.IsPlayer();
bool isCreature = go.IsCreature;
bool isOK = go.IsOK();
bool isValid = go.IsValid();
bool isOwned = go.IsOwned();
string owner = go.Owner;
bool isHostile = go.IsHostileTowards(other);
Brain.AllegianceLevel level = go.GetAllegianceLevel(factionName);
// Member = friendly/allied, Neutral, None/Unknown, Hostile, Enemy
```

### Properties & Tags

```csharp
string s = go.GetStringProperty("Name");
go.SetStringProperty("Name", "value");
int i = go.GetIntProperty("Number");
go.SetIntProperty("Number", 42);
bool has = go.HasStringProperty("Name");
bool has = go.HasPropertyOrTag("Name");
bool hastag = go.HasTag("TagName");
```

### Inventory

```csharp
Inventory inv = go.GetPart<Inventory>();
IEnumerable<GameObject> items = inv.GetObjects();
IEnumerable<GameObject> items = inv.GetObjectsDirect();  // no validation
int count = inv.GetObjectCountDirect();
GameObject added = inv.AddObject(item, null);  // second param is IEvent
inv.RemoveObject(item, null);
bool has = inv.HasObject(item);
```

### Cell/Zone

```csharp
Cell cell = go.CurrentCell;
Zone zone = go.CurrentZone;
Zone activeZone = The.ActiveZone;
IEnumerable<GameObject> objs = zone.GetObjects();
```

### Combat/Relationships

```csharp
bool hostile = go.IsHostileTowards(other);
Brain.AllegianceLevel level = go.GetAllegianceLevel(faction);
// Member (friendly), Neutral, None (unknown), Hostile, Enemy
```

---

## Item Modifications

**Each mod is a separate part**, not a single "Modification" part:

```csharp
// WRONG
bool hasMods = go.HasPart("Modification");  // Always false!

// CORRECT
var mods = go.PartsList.Where(p => p is IModification).ToList();
bool hasMods = mods.Count > 0;
var modNames = mods.Select(m => m.GetType().Name).ToList();
// Result: ["ModSharp", "ModHeavy"], etc.
```

---

## Non-Lootable World Objects

| Check | Type | Examples |
|-------|------|----------|
| `HasPart("Harvestable")` | Plants, trees, mushrooms | World decorations |
| `HasPart("Garbage")` | Garbage, trash | "Gettable terrain" |
| `HasTagOrProperty("NoAutoget")` | Quest items, special | Game marker |
| `!IsTakeable()` | Rubble, debris | Non-pickable |
| `LiquidVolume.MaxVolume < 0` | Liquid pools | Ground puddles |

---

## Trade API

### Detecting Merchants

```csharp
bool hasCommerce = obj.HasPart<Commerce>();
bool hasRepair = obj.GetPart<Tinkering_Repair>() != null;
bool hasRecharge = obj.HasSkill("Tinkering_Tinker1");
bool hasWontSell = obj.HasPropertyOrTag("WontSell");

if (!obj.IsPlayer() && obj.IsCreature &&
    (hasCommerce || hasRepair || hasRecharge) &&
    !obj.IsHostileTowards(player) &&
    !(hasWontSell && !hasRepair && !hasRecharge) &&
    obj.Inventory != null &&
    obj.Inventory.GetObjectCountDirect() > 0)
{
    // Valid merchant or service provider
}
```

### Opening Trade

```csharp
// PREFERRED - Use TradeUI.ShowTradeScreen directly
TradeUI.ShowTradeScreen(merchant, 1f, TradeUI.TradeScreenMode.Trade);
```

### TradeEntry (Reflection Required)

```csharp
// TradeEntry wraps GameObject, use reflection
FieldInfo goField = typeof(TradeEntry).GetField("GO", 
    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
GameObject item = goField?.GetValue(entry) as GameObject;

// Create TradeEntry
ConstructorInfo ctor = typeof(TradeEntry).GetConstructor(new[] { typeof(GameObject) });
TradeEntry entry = (TradeEntry)ctor.Invoke(new object[] { item });
```

### Force-Close Trade After Transaction

Patch `TradeUI.PerformOffer` return value to prevent UI refresh:

```csharp
[HarmonyPatch]
public static class TradeUI_PerformOffer_Patch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("XRL.UI.TradeUI");
        return AccessTools.Method(t, "PerformOffer", new[] {
            typeof(int), typeof(bool), typeof(GameObject),
            typeof(TradeUI.TradeScreenMode), typeof(List<TradeEntry>[]), typeof(int[][])
        });
    }

    [HarmonyPostfix]
    public static void Postfix(ref TradeUI.OfferStatus __result)
    {
        if (!MyModState.IsZoneTrade) return;
        if (__result == TradeUI.OfferStatus.REFRESH || __result == TradeUI.OfferStatus.TOP)
            __result = TradeUI.OfferStatus.CLOSE;
    }
}
```

---

## LiquidVolume API

```csharp
LiquidVolume vol = go.LiquidVolume;
int volume = vol.Volume;
int maxVol = vol.MaxVolume;  // < 0 = puddle, >= 0 = container
bool isPure = vol.IsPure();
Dictionary<string, int> components = vol.ComponentLiquids;  // {"water": 500}

// Split and combine
LiquidVolume split = sourceVol.Split(drams);
bool success = targetVol.MixWith(split);  // Can fail for unsafe liquids!

// Never mix neutron flux!
if (vol.ComponentLiquids.ContainsKey("neutronflux")) { /* skip */ }
```

---

## Harmony Patching

**Always decompile first**: `ilspy --assembly "Assembly-CSharp.dll" --output "temp/decompiled/"`

### Initialization

```csharp
[HasModSensitiveStaticCache]
public static class MyMod_HarmonyInitializer
{
    static MyMod_HarmonyInitializer()
    {
        var harmony = new Harmony("MyModID");
        harmony.PatchAll();
    }
    
    public static void Init() { }  // Trigger static constructor
}
```

### Lessons Learned

1. **Static methods > Async void** — Patch static methods, not async void
2. **Return value modification > Reflection** — Change return values instead of calling methods via reflection
3. **Call counters unreliable during init** — Methods may be called multiple times during setup
4. **Virtual objects may be destroyed** — Use persistent dictionaries, not object references
5. **Copy inventory before modifying** — `ReceiveObject()` modifies the inventory list while iterating. Use `new List<GameObject>(inv.GetObjectsDirect())` to iterate over a copy

### Patch Patterns

```csharp
// Postfix
[HarmonyPatch(typeof(TargetClass), nameof(TargetClass.Method))]
class MyPatch
{
    [HarmonyPostfix]
    static void Postfix(ref ReturnType __result)
    {
        if (__result == OriginalValue)
            __result = NewValue;
    }
}

// Dynamic target (unknown type at compile time)
[HarmonyPatch]
public static class MyPatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("Qud.UI.SomeScreen");
        return t == null? null : AccessTools.Method(t, "Hide") ?? AccessTools.Method(t, "Cleanup");
    }
    
    [HarmonyPostfix]
    public static void Postfix() { /* after Hide/Cleanup */ }
}
```

---

## AutoexploreObjectEvent

```csharp
// E.Item not E.Object!
public override bool HandleEvent(AutoexploreObjectEvent E)
{
    if (E.Item == null) return base.HandleEvent(E);
    
    if (ShouldAutoLoot(E.Item, The.Player))
        E.Command = "Autoget";  // Tell auto-explore to pick up
    
    return base.HandleEvent(E);
}
```

---

## Logging

```csharp
UnityEngine.Debug.Log("[MyMod] message");
UnityEngine.Debug.LogWarning("[MyMod] warning");
UnityEngine.Debug.LogError("[MyMod] error");

// In-game message
AddPlayerMessage("Hello!");
```

---

## The.* Accessors

```csharp
The.Player      // Current player GameObject
The.Game        // XRLGame instance
The.ActiveZone  // Currently active zone
The.ZoneManager
```

---

## File Locations (Windows)

| Type | Path |
|------|------|
| Player.log | `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Player.log` |
| Mods | `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Mods\` |
| Saves | `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Synced\Saves` |
| DLLs | `[Steam]\steamapps\common\Caves of Qud\CoQ_Data\Managed\Assembly-CSharp.dll` |

---

## Wiki Links

- Overview: https://wiki.cavesofqud.com/wiki/Modding:Overview
- Events: https://wiki.cavesofqud.com/wiki/Modding:Events
- Parts: https://wiki.cavesofqud.com/wiki/Modding:Parts
- Harmony: https://wiki.cavesofqud.com/wiki/Modding:Harmony
- Mod Config: https://wiki.cavesofqud.com/wiki/Modding:Mod_Configuration