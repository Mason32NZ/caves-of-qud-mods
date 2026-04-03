using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace M32_QoL
{
    public static class M32_StackedInventory
    {
        // Track last clean time to prevent running too frequently
        private static int _lastCleanTurn = -1;
        private static int _lastCleanTick = -1;
        private static readonly object _cleanLock = new object();

        /// <summary>
        /// Gets all modification parts from a game object.
        /// Modifications are parts that implement IModification interface.
        /// </summary>
        private static List<IPart> GetModifications(GameObject obj)
        {
            if (obj == null) return new List<IPart>();
            
            try
            {
                return obj.PartsList
                    .Where(p => p is IModification)
                    .ToList();
            }
            catch
            {
                return new List<IPart>();
            }
        }

        /// <summary>
        /// Checks if two items have the same modifications.
        /// Returns true if both have no mods, or both have identical mods.
        /// </summary>
        private static bool HaveSameModifications(GameObject a, GameObject b)
        {
            var modsA = GetModifications(a);
            var modsB = GetModifications(b);

            // Different number of mods = can't stack
            if (modsA.Count != modsB.Count)
                return false;

            // Both have no mods = can stack
            if (modsA.Count == 0 && modsB.Count == 0)
                return true;

            // Compare modification types by part name
            var modNamesA = modsA.Select(m => m.GetType().Name).OrderBy(n => n).ToList();
            var modNamesB = modsB.Select(m => m.GetType().Name).OrderBy(n => n).ToList();

            return modNamesA.SequenceEqual(modNamesB);
        }

        /// <summary>
        /// Checks if two items can be stacked together.
        /// Only liquid containers can stack (if empty). Non-liquid containers never stack.
        /// </summary>
        public static bool CanStackForMerge(GameObject a, GameObject b)
        {
            if (a == null || b == null) return false;
            if (a == b) return false;
            if (a.Blueprint != b.Blueprint) return false;
            if (a.HasTag("Unique") || b.HasTag("Unique")) return false;

            // Never stack Data Disks (each has unique data)
            if (a.Blueprint == "DataDisk" || b.Blueprint == "DataDisk") return false;

            // Never stack Unidentified Artifacts
            if (a.HasTag("Unidentified") || b.HasTag("Unidentified")) return false;

            try
            {
                // Check liquid volumes - items with liquids don't stack
                LiquidVolume volA = a.LiquidVolume;
                LiquidVolume volB = b.LiquidVolume;
                if (volA != null && volA.Volume > 0) return false;
                if (volB != null && volB.Volume > 0) return false;

                // Check modifications - items with different mod types should not stack
                if (!HaveSameModifications(a, b))
                    return false;

                // Check containers - only LIQUID containers can stack (and must be empty)
                Inventory invA = a.GetPart<Inventory>();
                Inventory invB = b.GetPart<Inventory>();
                
                // If item has Inventory part (is a container)
                if (invA != null || invB != null)
                {
                    // Only allow stacking for liquid containers (have LiquidVolume part)
                    bool aIsLiquidContainer = volA != null;
                    bool bIsLiquidContainer = volB != null;
                    
                    // If either is a non-liquid container, don't stack
                    if ((invA != null && !aIsLiquidContainer) || (invB != null && !bIsLiquidContainer))
                        return false;
                    
                    // Both are liquid containers - check if empty
                    bool aHasContents = invA != null && invA.GetObjectCountDirect() > 0;
                    bool bHasContents = invB != null && invB.GetObjectCountDirect() > 0;
                    if (aHasContents || bHasContents) return false;
                }

                // Check Number property (for numbered items like scrolls)
                if (a.GetIntProperty("Number") != b.GetIntProperty("Number"))
                    return false;

                // Check visual properties - items with different appearances don't stack
                Render renderA = a.GetPart<Render>();
                Render renderB = b.GetPart<Render>();

                // Check foreground color (e.g., wooden vs iron vs painted)
                if (renderA?.ColorString != renderB?.ColorString)
                    return false;

                // Check detail/background color
                if (renderA?.DetailColor != renderB?.DetailColor)
                    return false;

                // Check tile image (different images for different item types)
                if (renderA?.Tile != renderB?.Tile)
                    return false;

                // Check render string (ASCII character used for rendering)
                if (renderA?.RenderString != renderB?.RenderString)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Consolidates compatible item stacks in the player's inventory.
        /// </summary>
        public static void StackInventory()
        {
            if (!M32_Options.EnableStackedInventory)
                return;

            // Use lock to prevent concurrent execution
            lock (_cleanLock)
            {
                // Safety check: only run once per game turn to prevent spam
                int currentTurn = (int)(The.Game?.Turns ?? 0);
                if (currentTurn == _lastCleanTurn)
                {
                    // Already ran this turn, skip
                    return;
                }
                _lastCleanTurn = currentTurn;
                _lastCleanTick = Environment.TickCount;

                UnityEngine.Debug.Log("[M32_QoL] StackedInventory: Running inventory stack check...");

                try
                {
                    Inventory inv = The.Player?.GetPart<Inventory>();
                    if (inv == null) return;

                    // Get all inventory items (not equipped) - filter out nulls and invalid items
                    List<GameObject> items = inv.GetObjectsDirect()
                        .Where(i => i != null && !i.IsInvalid())
                        .ToList();

                    if (items.Count < 2) return;

                    // Group items by stack compatibility
                    List<List<GameObject>> groups = new List<List<GameObject>>();

                    foreach (GameObject item in items)
                    {
                        if (item == null || item.IsInvalid()) continue;

                        bool addedToGroup = false;
                        foreach (var group in groups)
                        {
                            if (group.Count > 0 && CanStackForMerge(item, group[0]))
                            {
                                group.Add(item);
                                addedToGroup = true;
                                break;
                            }
                        }

                        if (!addedToGroup)
                        {
                            groups.Add(new List<GameObject> { item });
                        }
                    }

                    // Collect all items to remove first (defer actual removal to avoid concurrent modification)
                    List<(GameObject secondary, GameObject primary, int count)> itemsToProcess = new List<(GameObject, GameObject, int)>();

                    foreach (var group in groups)
                    {
                        if (group.Count < 2) continue;

                        GameObject primary = group[0];
                        if (primary == null || primary.IsInvalid()) continue;

                        int totalCount = primary.Count;

                        for (int i = 1; i < group.Count; i++)
                        {
                            GameObject secondary = group[i];
                            if (secondary == null || secondary.IsInvalid()) continue;

                            // Validate that the item is still in player's inventory
                            if (secondary.InInventory != The.Player)
                            {
                                UnityEngine.Debug.LogWarning($"[M32_QoL] StackedInventory: Item {secondary.DisplayNameStripped} is no longer in player inventory, skipping");
                                continue;
                            }

                            totalCount += secondary.Count;
                            itemsToProcess.Add((secondary, primary, secondary.Count));
                        }

                        // Update primary count
                        if (itemsToProcess.Count > 0 && itemsToProcess.Any(x => x.primary == primary))
                        {
                            primary.Count = totalCount;
                        }
                    }

                    // Now remove all secondary items at once (outside of grouping loop)
                    int totalMerged = 0;
                    int stacksConsolidated = 0;
                    HashSet<GameObject> processedPrimaries = new HashSet<GameObject>();

                    foreach (var (secondary, primary, count) in itemsToProcess)
                    {
                        try
                        {
                            if (secondary.InInventory == The.Player)
                            {
                                inv.RemoveObject(secondary, null);
                                totalMerged++;
                                processedPrimaries.Add(primary);
                            }
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogWarning($"[M32_QoL] StackedInventory: Failed to remove {secondary.DisplayNameStripped}: {ex.Message}");
                        }
                    }

                    stacksConsolidated = processedPrimaries.Count;

                    if (totalMerged > 0)
                    {
                        UnityEngine.Debug.Log($"[M32_QoL] StackedInventory: Merged {totalMerged} stacks into {stacksConsolidated} stacks");
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[M32_QoL] StackedInventory: Error during stack operation: {ex.Message}");
                    UnityEngine.Debug.LogError($"[M32_QoL] StackedInventory: Stack trace: {ex.StackTrace}");
                }
            }
        }
    }

    #region Harmony Patches

    /// <summary>
    /// Patch InventoryAndEquipmentStatusScreen.Show() to stack when opening inventory.
    /// Uses Postfix to run after the screen opens.
    /// </summary>
    [HarmonyPatch]
    public static class StackedInventory_InventoryScreen_Patch
    {
        [HarmonyTargetMethod]
        public static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Qud.UI.InventoryAndEquipmentStatusScreen");
            if (type == null)
            {
                UnityEngine.Debug.LogError("[M32_QoL] StackedInventory: Could not find Qud.UI.InventoryAndEquipmentStatusScreen");
                return null;
            }

            // Try Show first, then Open
            var method = AccessTools.Method(type, "Show");
            if (method == null)
                method = AccessTools.Method(type, "Open");

            return method;
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            M32_StackedInventory.StackInventory();
        }
    }

    /// <summary>
    /// Patch TradeScreen to stack BEFORE Zone Trade patches.
    /// Uses Prefix to ensure it runs first.
    /// </summary>
    [HarmonyPatch]
    public static class TradeScreen_Show_StackedInventory_Patch
    {
        [HarmonyTargetMethod]
        public static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Qud.UI.TradeScreen");
            if (type == null)
            {
                UnityEngine.Debug.LogError("[M32_QoL] StackedInventory: Could not find Qud.UI.TradeScreen");
                return null;
            }

            var method = AccessTools.Method(type, "Show");
            if (method == null)
                method = AccessTools.Method(type, "Open");

            return method;
        }

        [HarmonyPrefix]
        public static void Prefix()
        {
            // Run as Prefix to ensure it runs BEFORE Zone Trade's patches
            M32_StackedInventory.StackInventory();
        }
    }

    #endregion
}
