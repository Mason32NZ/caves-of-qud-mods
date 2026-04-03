using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace M32_QoL
{
    /// <summary>
    /// Stacked Inventory feature - automatically merges compatible item stacks
    /// when opening inventory or trade screens.
    /// </summary>
    public static class M32_StackedInventory
    {
        private static int _lastRunTurn = -1;
        private static readonly object _executionLock = new object();
        // Queue for deferred item removal to avoid modifying inventory during event processing
        private static readonly Queue<GameObject> _removalQueue = new Queue<GameObject>();

        /// <summary>
        /// Gets all modification parts from a game object.
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
        /// Checks if two items have identical modifications.
        /// </summary>
        private static bool HaveSameModifications(GameObject a, GameObject b)
        {
            var modsA = GetModifications(a);
            var modsB = GetModifications(b);

            if (modsA.Count != modsB.Count)
                return false;

            if (modsA.Count == 0 && modsB.Count == 0)
                return true;

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
                
                if (invA != null || invB != null)
                {
                    bool aIsLiquidContainer = volA != null;
                    bool bIsLiquidContainer = volB != null;
                    
                    // Non-liquid containers cannot stack
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

                if (renderA?.ColorString != renderB?.ColorString)
                    return false;

                if (renderA?.DetailColor != renderB?.DetailColor)
                    return false;

                if (renderA?.Tile != renderB?.Tile)
                    return false;

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
        /// Processes the deferred removal queue. Called from a safe context.
        /// </summary>
        private static void ProcessRemovalQueue()
        {
            if (_removalQueue.Count == 0) return;
            
            Inventory inv = The.Player?.GetPart<Inventory>();
            if (inv == null) return;

            int removed = 0;
            while (_removalQueue.Count > 0)
            {
                GameObject item = _removalQueue.Dequeue();
                if (item != null && item.InInventory == The.Player)
                {
                    try
                    {
                        inv.RemoveObject(item, null);
                        removed++;
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"[M32_QoL] StackedInventory: Failed to remove {item.DisplayNameStripped}: {ex.Message}");
                    }
                }
            }

            if (removed > 0)
                UnityEngine.Debug.Log($"[M32_QoL] StackedInventory: Removed {removed} merged items");
        }

        /// <summary>
        /// Consolidates compatible item stacks in the player's inventory.
        /// Items are queued for removal and processed on the next safe tick.
        /// </summary>
        public static void StackInventory()
        {
            if (!M32_Options.EnableStackedInventory)
                return;

            lock (_executionLock)
            {
                // Only run once per game turn
                int currentTurn = (int)(The.Game?.Turns ?? 0);
                if (currentTurn == _lastRunTurn)
                {
                    // Process any pending removals from previous turn
                    ProcessRemovalQueue();
                    return;
                }
                
                _lastRunTurn = currentTurn;

                try
                {
                    Inventory inv = The.Player?.GetPart<Inventory>();
                    if (inv == null) return;

                    // Clear any stale items from queue
                    _removalQueue.Clear();

                    List<GameObject> items = inv.GetObjectsDirect()
                        .Where(i => i != null && !i.IsInvalid())
                        .ToList();

                    if (items.Count < 2) return;

                    // Sort items by display name to ensure consistent grouping (defensive sorting)
                    items.Sort((a, b) => {
                        try
                        {
                            string nameA = a?.DisplayNameStripped ?? "";
                            string nameB = b?.DisplayNameStripped ?? "";
                            return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
                        }
                        catch
                        {
                            // Maintain original order if comparison fails
                            return 0;
                        }
                    });

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
                            groups.Add(new List<GameObject> { item });
                    }

                    // Collect items to merge and queue for removal
                    int totalMerged = 0;

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
                            if (secondary.InInventory != The.Player) continue;

                            totalCount += secondary.Count;
                            _removalQueue.Enqueue(secondary);
                            totalMerged++;
                        }

                        primary.Count = totalCount;
                    }

                    if (totalMerged > 0)
                    {
                        UnityEngine.Debug.Log($"[M32_QoL] StackedInventory: Merged {totalMerged} stacks, queued for removal");
                    }

                    // Process removals immediately since we're outside of event handlers
                    ProcessRemovalQueue();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[M32_QoL] StackedInventory: Error during stack operation: {ex.Message}");
                }
            }
        }
    }

    #region Harmony Patches

    /// <summary>
    /// Patch InventoryAndEquipmentStatusScreen.Show() to stack when opening inventory.
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

            var method = AccessTools.Method(type, "Show") ?? AccessTools.Method(type, "Open");
            return method;
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            // Use a small delay to ensure we're outside of the event processing
            M32_StackedInventory.StackInventory();
        }
    }

    /// <summary>
    /// Patch TradeScreen to stack BEFORE Zone Trade patches.
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

            var method = AccessTools.Method(type, "Show") ?? AccessTools.Method(type, "Open");
            return method;
        }

        [HarmonyPrefix]
        public static void Prefix()
        {
            M32_StackedInventory.StackInventory();
        }
    }

    #endregion
}
