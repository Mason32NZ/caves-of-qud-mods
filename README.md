# M32's QoL

Quality of life improvements for Caves of Qud. Spend less time managing your inventory and looking for loot, and more time exploring the caves of Qud.

**Trade with all** merchants at once, **auto loot** as you go, auto stack things for a **clean inventory**, **consolidate liquids**, and **unload all ammo and cells** at once!

## Features

### Zone Trade `[Shift+T]`

Trade with all friendly merchants in your current zone at once. No more walking between vendors to find who has what you need (love too all my metamorphic polygel farmers). One keypress opens a consolidated trade window with every nearby merchant's inventory.

- Automatically selects the merchant with the best services (identify, repair, recharge) as the host merchant
- Closes trade window after trade for all those fast bois out there (and totally not to avoid weird bugs)

(Note: You must use the keybind while **NOT** in a menu or user interface for it to work)

### Consolidate Liquids `[Shift+L]`

Merge liquids of the same type into their largest containers. Keeps your inventory organized and maximizes container efficiency.

- Smart ratio-based matching to support mixed liquids
- Uses the containers the liquids are already using to avoid bad things from happening
- Safely excludes neutron flux (because a thousand suns is pretty heavy)

(Note: You must use the keybind while **NOT** in a menu or user interface for it to work)

### Unload All `[Shift+U]`

Unload all projectiles from ranged weapons and eject all energy cells from powered items in your inventory. Perfect for when you're about to sell your loot or want to consolidate your ammo and cells.

- Unloads ammo (slugs, shells, arrows, rockets, etc.) from all unequipped ranged weapons
- Ejects energy cells (batteries) from all unequipped powered items
- Skips equipped items — your currently wielded weapons and worn gear stay loaded
- Unloaded items go directly to your inventory

(Note: You must use the keybind while **NOT** in a menu or user interface for it to work)

### Stacked Inventory `[Automatic]`

Automatically stacks compatible items when you open your inventory or trade screen. Reduces scrolling and keeps similar items together.

### Auto Loot `[Automatic]`

Automatically picks up nearby items when you move. Highly configurable to fit your playstyle.

- Loots items from current tile plus the adjacent tiles
- Works when you move so you don't have to think about it, also integrates with the auto-explore system
- Highly configurable to suit your preferences
- Supports individual item filtering via the `autoloot.json` file in the mod folder

## Keybinds

- `Shift+T`: Open Zone Trade with nearby merchants
- `Shift+L`: Consolidate liquids in inventory
- `Shift+U`: Unload all ammo and cells from unequipped items

⚠️ You may need to **change your keybinds** if there are conflicts ⚠️

All keybinds are configurable in **Options → Controls → Control Mapping → Mod: M32's QoL**.

## Compatibility

- Developed for **Caves of Qud version 1.0.4 (build 2.0.210.24)** using the **Modern UI**
- Should be compatible with other mods that don't heavily modify trade or inventory screens
- Uses Harmony patches — may conflict with other mods patching the same methods

## Developer's Thoughts

Let me know if you run into any issues, it has only been lightly tested. I will try to fix any bugs you find if possible. I will consider new feature requests or changes but no guarantees.

I made this over a few weekends using [opencode](https://opencode.ai/) (Models used: Kimi K2.5, MiniMax M2.7, GLM 5) to play around with agentic programming. I'm a full stack developer by trade and an old man has to get with the times 😂. If you would like to support me and help me burn more money on tokens, you can [click here](https://www.paypal.com/ncp/payment/6Z5HEV6NXBPEY) to donate 💸🔥.