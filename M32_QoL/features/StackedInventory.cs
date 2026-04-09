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
        /// Checks if two items have identical tag sets.
        /// Combines blueprint tags AND runtime Property tags.
        /// </summary>
        private static bool HaveSameTags(GameObject a, GameObject b)
        {
            // Get blueprint tags
            var blueprintA = a?.GetBlueprint();
            var blueprintB = b?.GetBlueprint();

            // Collect all tag keys from both blueprint and runtime properties
            var tagsA = new HashSet<string>();
            var tagsB = new HashSet<string>();

            // Blueprint tags
            if (blueprintA?.Tags != null)
                tagsA.UnionWith(blueprintA.Tags.Keys);
            if (blueprintB?.Tags != null)
                tagsB.UnionWith(blueprintB.Tags.Keys);

            // Runtime properties (using reflection to access private _Property field)
            var propA = typeof(GameObject).GetField("_Property", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(a) as Dictionary<string, string>;
            var propB = typeof(GameObject).GetField("_Property", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(b) as Dictionary<string, string>;

            if (propA != null)
                tagsA.UnionWith(propA.Keys);
            if (propB != null)
                tagsB.UnionWith(propB.Keys);

            // Check if tag key sets are identical
            if (tagsA.Count != tagsB.Count)
                return false;

            return tagsA.SetEquals(tagsB);
        }

        /// <summary>
        /// Checks if two items can be stacked together.
        /// Only liquid containers can stack (if empty). Non-liquid containers never stack.
        /// Uses deep comparison via SameAs() for parts and properties.
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
                // Never stack items that have a cell (battery) socket with cell inserted
                EnergyCellSocket socketA = a.GetPart<EnergyCellSocket>();
                EnergyCellSocket socketB = b.GetPart<EnergyCellSocket>();
                if ((socketA != null && socketA.Cell != null) ||
                    (socketB != null && socketB.Cell != null))
                    return false;

                // Check containers
                LiquidVolume volA = a.LiquidVolume;
                LiquidVolume volB = b.LiquidVolume;
                Inventory invA = a.GetPart<Inventory>();
                Inventory invB = b.GetPart<Inventory>();

                if (invA != null || invB != null)
                {
                    bool aIsLiquidContainer = volA != null;
                    bool bIsLiquidContainer = volB != null;

                    bool aHasContents = invA != null && invA.GetObjectCountDirect() > 0;
                    bool bHasContents = invB != null && invB.GetObjectCountDirect() > 0;

                    // Non-liquid containers with items cannot stack
                    if ((aHasContents && !aIsLiquidContainer) || (bHasContents && !bIsLiquidContainer)) return false;

                    // Liquid containers with different liquids or amounts cannot stack
                    if (volA != null && volB != null)
                    {
                        // Both are liquid containers - check if they have the same liquid and amount
                        // If volumes differ, they cannot stack
                        if (volA.Volume != volB.Volume)
                            return false;

                        // If both have liquid, check composition
                        if (volA.Volume > 0 && !volA.LiquidSameAs(volB))
                            return false;
                    }
                    else if (volA != null || volB != null)
                    {
                        // Only one is a liquid container - cannot stack
                        return false;
                    }

                    // Containers must have same display name and colors to stack
                    if (a.DisplayName != b.DisplayName)
                        return false;

                    Render renderA = a.GetPart<Render>();
                    Render renderB = b.GetPart<Render>();
                    if (renderA?.ColorString != renderB?.ColorString)
                        return false;
                    if (renderA?.DetailColor != renderB?.DetailColor)
                        return false;
                }
                else
                {
                    // Check Number property (for numbered items like scrolls)
                    if (a.GetIntProperty("Number") != b.GetIntProperty("Number"))
                        return false;

                    // Check tags - items with different tag sets don't stack
                    if (!HaveSameTags(a, b))
                        return false;

                    // DEEP COMPARISON: Use game's built-in SameAs for parts and properties
                    if (!a.SameAs(b))
                        return false;
                }

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
