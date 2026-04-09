using System;
using System.Collections.Generic;
using System.Linq;
using XRL.World;
using XRL.World.Parts;

namespace M32_QoL
{
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
                    // Non-liquid items: check Number property, tags, then deep SameAs comparison
                    if (a.GetIntProperty("Number") != b.GetIntProperty("Number"))
                        return false;

                    if (!HaveSameTags(a, b))
                        return false;

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