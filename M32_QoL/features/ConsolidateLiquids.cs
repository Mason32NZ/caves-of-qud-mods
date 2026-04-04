using System;
using System.Collections.Generic;
using System.Linq;

using XRL;
using XRL.Core;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Parts.Skill;

namespace M32_QoL
{
    [Serializable]
    public class M32_ConsolidateLiquidsPart : IScribedPart
    {
        private const string LOG_PREFIX = "[M32_QoL] ConsolidateLiquids:";
        private const string UNSAFE_LIQUID = "neutronflux";

        public override bool AllowStaticRegistration() => true;

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == CommandEvent.ID;
        }

        public override bool HandleEvent(CommandEvent E)
        {
            if (E.Command != "M32QoL_ConsolidateLiquids")
                return base.HandleEvent(E);

            if (!M32_Options.EnableConsolidateLiquids)
                return false;

            ConsolidateLiquids(The.Player);
            return false;
        }

        public static void ConsolidateLiquids(GameObject player)
        {
            if (player == null)
                return;

            Inventory inv = player.GetPart<Inventory>();
            if (inv == null)
                return;

            List<GameObject> containers = inv.GetObjects()
                .Where(IsValidLiquidContainer)
                .ToList();

            if (containers.Count < 2)
            {
                UnityEngine.Debug.Log($"{LOG_PREFIX} Not enough liquid containers to consolidate");
                return;
            }

            var groups = containers
                .GroupBy(c => GetMixtureKey(c.LiquidVolume))
                .Where(g => g.Key != "" && g.Count() > 1)
                .ToList();

            if (groups.Count == 0)
            {
                UnityEngine.Debug.Log($"{LOG_PREFIX} No matching liquid groups to consolidate");
                return;
            }

            int totalTransfers = 0;
            int groupsProcessed = 0;

            foreach (var group in groups)
            {
                int transfers = ConsolidateGroup(group.ToList());
                if (transfers > 0)
                {
                    totalTransfers += transfers;
                    groupsProcessed++;
                }
            }

            UnityEngine.Debug.Log($"{LOG_PREFIX} Made {totalTransfers} liquid transfers across {groupsProcessed} groups");
            AddPlayerMessage("Consolidation of liquids complete!");
        }

        private static int ConsolidateGroup(List<GameObject> containers)
        {
            if (containers.Count < 2)
                return 0;

            containers.Sort((a, b) => b.LiquidVolume.MaxVolume.CompareTo(a.LiquidVolume.MaxVolume));

            int remaining = containers.Sum(c => c.LiquidVolume.Volume);
            int targetIndex = 0;
            int transfers = 0;

            while (remaining > 0 && targetIndex < containers.Count - 1)
            {
                GameObject target = containers[targetIndex];
                int spaceInTarget = target.LiquidVolume.MaxVolume - target.LiquidVolume.Volume;

                if (spaceInTarget <= 0)
                {
                    targetIndex++;
                    continue;
                }

                for (int i = containers.Count - 1; i > targetIndex && spaceInTarget > 0; i--)
                {
                    GameObject source = containers[i];
                    if (source.LiquidVolume.Volume <= 0)
                        continue;

                    int toTransfer = Math.Min(source.LiquidVolume.Volume, spaceInTarget);

                    if (toTransfer > 0 && PourLiquid(source, target, toTransfer))
                    {
                        spaceInTarget -= toTransfer;
                        remaining -= toTransfer;
                        transfers++;
                    }
                }

                targetIndex++;
            }

            return transfers;
        }

        private static bool PourLiquid(GameObject source, GameObject target, int drams)
        {
            if (source == null || target == null)
                return false;

            LiquidVolume sourceVol = source.LiquidVolume;
            LiquidVolume targetVol = target.LiquidVolume;

            if (sourceVol == null || targetVol == null || drams <= 0)
                return false;

            int availableSpace = targetVol.MaxVolume - targetVol.Volume;
            if (availableSpace <= 0)
                return false;

            int toTransfer = Math.Min(drams, availableSpace);
            if (toTransfer <= 0)
                return false;

            try
            {
                LiquidVolume split = sourceVol.Split(toTransfer);
                if (split == null || split.Volume <= 0)
                    return false;

                return targetVol.MixWith(split);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"{LOG_PREFIX} Failed to pour liquid: {ex.Message}");
                return false;
            }
        }

        private static bool IsValidLiquidContainer(GameObject obj)
        {
            if (obj == null)
                return false;

            LiquidVolume vol = obj.LiquidVolume;
            if (vol == null || vol.MaxVolume < 0 || vol.Volume <= 0)
                return false;

            if (vol.ComponentLiquids == null || vol.ComponentLiquids.Count == 0)
                return false;

            return !vol.ComponentLiquids.Keys.Contains(UNSAFE_LIQUID);
        }

        private static string GetMixtureKey(LiquidVolume vol)
        {
            if (vol?.ComponentLiquids == null || vol.ComponentLiquids.Count == 0)
                return "";

            var components = vol.ComponentLiquids
                .Where(kv => kv.Value > 0)
                .OrderBy(kv => kv.Key)
                .ToList();

            if (components.Count == 0)
                return "";

            int gcd = components.Aggregate(0, (current, kv) => current == 0 ? kv.Value : GCD(current, kv.Value));

            return string.Join("|", components.Select(kv => $"{kv.Key}:{kv.Value / gcd}"));
        }

        private static int GCD(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }
    }
}
