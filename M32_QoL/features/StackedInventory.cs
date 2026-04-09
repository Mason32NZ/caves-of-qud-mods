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
        private static readonly Queue<GameObject> _removalQueue = new Queue<GameObject>();

        /// <summary>
        /// Consolidates compatible item stacks in the player's inventory.
        /// </summary>
        public static void StackInventory()
        {
            if (!M32_Options.EnableStackedInventory)
                return;

            lock (_executionLock)
            {
                int currentTurn = (int)(The.Game?.Turns ?? 0);
                if (currentTurn == _lastRunTurn)
                {
                    ProcessRemovalQueue();
                    return;
                }

                _lastRunTurn = currentTurn;

                try
                {
                    Inventory inv = The.Player?.GetPart<Inventory>();
                    if (inv == null) return;

                    _removalQueue.Clear();

                    List<GameObject> items = inv.GetObjectsDirect()
                        .Where(i => i != null && !i.IsInvalid())
                        .ToList();

                    if (items.Count < 2) return;

                    // Sort by Blueprint for consistent grouping (avoids DisplayName crash)
                    items = M32_Helpers.SortItemsByBlueprint(items);

                    // Group items by stack compatibility
                    List<List<GameObject>> groups = new List<List<GameObject>>();

                    foreach (GameObject item in items)
                    {
                        if (item == null || item.IsInvalid()) continue;

                        bool addedToGroup = false;
                        foreach (var group in groups)
                        {
                            if (group.Count > 0 && M32_Helpers.CanStackForMerge(item, group[0]))
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

                    ProcessRemovalQueue();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[M32_QoL] StackedInventory: Error during stack operation: {ex.Message}");
                }
            }
        }

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
                        UnityEngine.Debug.LogWarning($"[M32_QoL] StackedInventory: Failed to remove item: {ex.Message}");
                    }
                }
            }

            if (removed > 0)
                UnityEngine.Debug.Log($"[M32_QoL] StackedInventory: Removed {removed} merged items");
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