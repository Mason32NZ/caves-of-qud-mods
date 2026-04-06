# M32's QoL

Quality of life improvements for Caves of Qud. Spend less time managing your inventory and looking for loot, and more time exploring the caves of Qud.

Trade with all merchants at once, auto loot as you go, auto stack things for a clean inventory, and consolidate liquids!

## Features

### Zone Trade `[Shift+T]`

Trade with **all friendly merchants in your current zone** at once. No more walking between vendors to find who has what you need (love too all my metamorphic polygel farmers). One keypress opens a consolidated trade window with every nearby merchant's inventory.

- Automatically selects the merchant with the best services (identify, repair, recharge) as the host merchant
- Closes trade window after trade for all those fast bois out there (and totally not to avoid weird bugs)

### Consolidate Liquids `[Shift+L]`

Merge liquids of the same type into their largest containers. Keeps your inventory organized and maximizes container efficiency.

- Smart ratio-based matching to support mixed liquids
- Uses the containers the liquids are already using to avoid bad things from happening
- Safely excludes neutron flux (because a thousand suns is pretty heavy)
- Works on all liquid containers in your inventory

### Stacked Inventory `[Automatic]`

Automatically merges compatible item stacks when you open your inventory or trade screen. Reduces scrolling and keeps similar items together.

- Stacks items with identical properties, modifications, and appearance
- Containers with stuff in them don't stack

### Auto Loot `[Automatic]`

Automatically picks up nearby items when you move. Highly configurable to fit your playstyle.

- Loots items from current tile plus the adjacent tiles
- Works when you move so you don't have to think about it, also integrates with the auto-explore system
- Respects ownership (won't steal from friendly factions, optional)
- Weight capacity checks to prevent over-encumbrance (optional)

## Keybinds

- `Shift+T`: Open Zone Trade with nearby merchants
- `Shift+L`: Consolidate liquids in inventory

⚠️ You may need to **change your keybinds** if there are conflicts ⚠️

Both keybinds are configurable in **Options → Controls → Control Mapping → Mod: M32's QoL**.

## Compatibility

- Developed for **Caves of Qud version 1.0.4 (build 2.0.210.24)** using the **Modern UI**
- Should be compatible with other mods that don't heavily modify trade or inventory screens
- Uses Harmony patches — may conflict with other mods patching the same methods

## Developer's Thoughts

Let me know if you run into any issues, it has only been lightly tested. I will try to fix any bugs you find if possible. I will consider new feature requests or changes but no guarantees.

I made this over a few weekends using [opencode](https://opencode.ai/) (Models used: Kimi K2.5, MiniMax M2.7, GLM 5) to play around with agentic programming. I'm a full stack developer by trade and an old man has to get with the times 😂. If you would like to support me and help me burn more money on tokens, you can [click here](https://www.paypal.com/ncp/payment/6Z5HEV6NXBPEY) to donate 💸🔥.