using System;
using System.Collections.Generic;
using System.Linq;
using XRL;
using XRL.Core;
using XRL.World;
using XRL.World.Parts;

namespace M32_QoL
{
    /// <summary>
    /// Deferred removal command that processes inventory item removals
    /// at the end of the current game segment, after all event processing
    /// has completed. This avoids concurrent modification issues with
    /// other mods that patch Inventory.WantEvent.
    /// </summary>
    public class M32_DeferredRemovalCommand : IActionCommand
    {
        private static readonly M32_DeferredRemovalCommand _instance = new M32_DeferredRemovalCommand();

        private readonly List<string> _pendingItemIds = new List<string>();

        public int Priority => 60000;

        public static void ScheduleRemoval(GameObject item)
        {
            if (item == null) return;

            _instance._pendingItemIds.Add(item.ID);
            if (!The.ActionManager.HasAction<M32_DeferredRemovalCommand>())
                The.ActionManager.EnqueueAction(_instance);
        }

        public void Execute(XRLGame Game, ActionManager Manager)
        {
            if (_pendingItemIds.Count == 0) return;

            Inventory inv = The.Player?.GetPart<Inventory>();
            if (inv == null)
            {
                _pendingItemIds.Clear();
                return;
            }

            int removed = 0;
            foreach (string itemId in _pendingItemIds)
            {
                GameObject item = GameObject.FindByID(itemId);
                if (item == null) continue;
                if (item.InInventory != The.Player) continue;

                try
                {
                    inv.RemoveObject(item, null);
                    removed++;
                }
                catch
                {
                    // Skip items that fail to remove
                }
            }

            if (removed > 0)
                UnityEngine.Debug.Log($"[M32_QoL] StackedInventory: Deferred removal of {removed} merged items");

            _pendingItemIds.Clear();
        }
    }

    /// <summary>
    /// Shared helper methods used across M32_QoL features.
    /// Any functionality that is shared across features should go here.
    /// </summary>
    public static class M32_Helpers
    {
        /// <summary>
        /// Safely compares display names of two items.
        /// Falls back to Blueprint comparison if DisplayName throws
        /// (avoids triggering the game's DescriptionBuilder sort bug).
        /// </summary>
        public static bool SameDisplayName(GameObject a, GameObject b)
        {
            try
            {
                return a.DisplayName == b.DisplayName;
            }
            catch
            {
                return a.Blueprint == b.Blueprint;
            }
        }

        /// <summary>
        /// Safely gets a display name for logging, falling back to Blueprint.
        /// Avoids triggering DescriptionBuilder sort bug on DisplayName/DisplayNameStripped.
        /// </summary>
        public static string SafeGetName(GameObject item)
        {
            try { return item.DisplayName ?? item.Blueprint ?? "null"; }
            catch { return item.Blueprint ?? "null"; }
        }

        /// <summary>
        /// Sorts items by Blueprint for consistent grouping.
        /// Uses Blueprint instead of DisplayName to avoid triggering
        /// the game's internal DescriptionBuilder sort bug.
        /// </summary>
        public static List<GameObject> SortItemsByBlueprint(List<GameObject> items)
        {
            return items.OrderBy(i => i?.Blueprint ?? "").ToList();
        }

        /// <summary>
        /// Detects if a GameObject is a liquid container (has LiquidVolume with MaxVolume >= 0).
        /// MaxVolume less than 0 indicates a ground puddle, not a container.
        /// </summary>
        public static bool IsLiquidContainer(GameObject obj)
        {
            LiquidVolume vol = obj?.LiquidVolume;
            return vol != null && vol.MaxVolume >= 0;
        }

        /// <summary>
        /// Checks if two items have identical tag sets.
        /// Combines blueprint tags AND runtime Property tags.
        /// </summary>
        private static bool HaveSameTags(GameObject a, GameObject b)
        {
            var blueprintA = a?.GetBlueprint();
            var blueprintB = b?.GetBlueprint();

            var tagsA = new HashSet<string>();
            var tagsB = new HashSet<string>();

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

            if (tagsA.Count != tagsB.Count)
                return false;

            return tagsA.SetEquals(tagsB);
        }

        /// <summary>
        /// Checks if two items can be stacked together.
        /// Liquid containers stack by volume, composition, display name, and color.
        /// Non-liquid items stack by Number property, tags, and deep SameAs comparison.
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
                // Never stack items with a cell (battery) inserted in socket
                EnergyCellSocket socketA = a.GetPart<EnergyCellSocket>();
                EnergyCellSocket socketB = b.GetPart<EnergyCellSocket>();
                if ((socketA != null && socketA.Cell != null) ||
                    (socketB != null && socketB.Cell != null))
                    return false;

                // Liquid container stacking logic
                LiquidVolume volA = a.LiquidVolume;
                LiquidVolume volB = b.LiquidVolume;

                if (volA != null || volB != null)
                {
                    // Both must be liquid containers
                    if (volA == null || volB == null)
                        return false;

                    // Must have same volume
                    if (volA.Volume != volB.Volume)
                        return false;

                    // Must have same liquid composition (if non-empty)
                    if (volA.Volume > 0 && !volA.LiquidSameAs(volB))
                        return false;

                    // Must have same display name and render colors
                    if (!SameDisplayName(a, b))
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
                    // Non-liquid items: multiple explicit checks for stacking compatibility

                    // Check Number property (for numbered items like scrolls)
                    if (a.GetIntProperty("Number") != b.GetIntProperty("Number"))
                        return false;

                    // Check tags - items with different tag sets don't stack
                    if (!HaveSameTags(a, b))
                        return false;

                    // Check modifications - items with different mod types don't stack
                    var modsA = a.PartsList != null ? a.PartsList.OfType<IModification>().Select(m => m.GetType().Name).OrderBy(n => n).ToList() : new List<string>();
                    var modsB = b.PartsList != null ? b.PartsList.OfType<IModification>().Select(m => m.GetType().Name).OrderBy(n => n).ToList() : new List<string>();
                    if (modsA.Count != modsB.Count || !modsA.SequenceEqual(modsB))
                        return false;

                    // Check render properties - items with different appearances don't stack
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

                    // Check EnergyCell charge level - cells only stack at same charge
                    EnergyCell cellA = a.GetPart<EnergyCell>();
                    EnergyCell cellB = b.GetPart<EnergyCell>();
                    if (cellA != null && cellB != null && cellA.Charge != cellB.Charge)
                        return false;

                    // DEEP COMPARISON: Use game's built-in SameAs as final safety net
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
    }
}