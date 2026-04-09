using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using XRL;
using XRL.Core;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Parts.Skill;

namespace M32_QoL
{
    #region Data Structures

    public class VirtualItemData
    {
        public string VirtualItemId;
        public string Blueprint;
        public int OriginalCount;
        public List<string> SourceItemIds = new List<string>();
        public List<int> SourceItemCounts = new List<int>();
    }

    public static class M32_ZoneTradeState
    {
        public static bool IsZoneTrade = false;
        public static List<GameObject> ZoneMerchants = new List<GameObject>();
        public static Dictionary<string, List<string>> PreTradeInventorySnapshot = new Dictionary<string, List<string>>();
        public static Dictionary<string, int> PreTradeItemCounts = new Dictionary<string, int>();
        public static Dictionary<string, string> ItemOwnerMap = new Dictionary<string, string>();
        public static Dictionary<string, int> PlayerInventorySnapshot = new Dictionary<string, int>();
        public static Dictionary<string, VirtualItemData> VirtualItemRegistry = new Dictionary<string, VirtualItemData>();

        public static void Reset()
        {
            IsZoneTrade = false;
            ZoneMerchants.Clear();
            PreTradeInventorySnapshot.Clear();
            PreTradeItemCounts.Clear();
            ItemOwnerMap.Clear();
            PlayerInventorySnapshot.Clear();
            VirtualItemRegistry.Clear();
        }
    }

    public class MergedItemGroup
    {
        public string Blueprint;
        public List<GameObject> SourceItems = new List<GameObject>();
        public int TotalCount => SourceItems.Sum(i => i?.Count ?? 0);
    }

    #endregion

    #region Main Zone Trade Part

    [Serializable]
    public class M32_ZoneTradePart : IScribedPart
    {
        public override bool AllowStaticRegistration() => true;

        public override void Attach()
        {
            UnityEngine.Debug.Log($"[M32_QoL] M32_ZoneTradePart attached to {M32_Helpers.SafeGetName(ParentObject)}");
            base.Attach();
        }

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == CommandEvent.ID;
        }

        public override bool HandleEvent(CommandEvent E)
        {
            if (E.Command == "M32QoL_ZoneTrade")
            {
                OpenZoneTrade();
                return false;
            }
            return base.HandleEvent(E);
        }

        private void OpenZoneTrade()
        {
            if (!M32_Options.EnableZoneTrade)
                return;

            AddPlayerMessage("Starting Zone Trade...");
            List<GameObject> merchants = FindFriendlyMerchants();

            if (merchants.Count == 0)
            {
                Popup.Show("There are no friendly merchants in this area.");
                return;
            }

            M32_ZoneTradeState.IsZoneTrade = true;
            M32_ZoneTradeState.ZoneMerchants = merchants;
            CaptureInventorySnapshot(merchants);

            UnityEngine.Debug.Log($"[M32_QoL] Zone Trade opened with {merchants.Count} merchants");

            try
            {
                GameObject primaryMerchant = merchants[0];
                int bestIdentifyLevel = Tinkering.GetIdentifyLevel(primaryMerchant);
                bool bestHasRepair = primaryMerchant.GetPart<Tinkering_Repair>() != null;
                bool bestHasRecharge = primaryMerchant.HasSkill("Tinkering_Tinker1");
                int bestScore = bestIdentifyLevel + (bestHasRepair ? 10 : 0) + (bestHasRecharge ? 10 : 0);

                for (int i = 1; i < merchants.Count; i++)
                {
                    var merchant = merchants[i];
                    if (merchant == null) continue;

                    int identifyLevel = Tinkering.GetIdentifyLevel(merchant);
                    bool hasRepair = merchant.GetPart<Tinkering_Repair>() != null;
                    bool hasRecharge = merchant.HasSkill("Tinkering_Tinker1");
                    int merchantScore = identifyLevel + (hasRepair ? 10 : 0) + (hasRecharge ? 10 : 0);

                    if (merchantScore > bestScore)
                    {
                        bestIdentifyLevel = identifyLevel;
                        bestHasRepair = hasRepair;
                        bestHasRecharge = hasRecharge;
                        bestScore = merchantScore;
                        primaryMerchant = merchant;
                    }
                }

                TradeUI.ShowTradeScreen(primaryMerchant, 1f, TradeUI.TradeScreenMode.Trade);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[M32_QoL] Error opening trade: {ex}");
                Popup.Show("Error opening zone trade. Check Player.log for details.");
                M32_ZoneTradeState.Reset();
            }
        }

        private void CaptureInventorySnapshot(List<GameObject> merchants)
        {
            M32_ZoneTradeState.PreTradeInventorySnapshot.Clear();
            M32_ZoneTradeState.PreTradeItemCounts.Clear();
            M32_ZoneTradeState.ItemOwnerMap.Clear();
            M32_ZoneTradeState.PlayerInventorySnapshot.Clear();
            M32_ZoneTradeState.VirtualItemRegistry.Clear();

            Inventory playerInv = The.Player?.GetPart<Inventory>();
            if (playerInv != null)
            {
                foreach (GameObject item in playerInv.GetObjectsDirect())
                {
                    if (item == null) continue;

                    string blueprint = item.Blueprint;
                    int count = item.Count;
                    if (M32_ZoneTradeState.PlayerInventorySnapshot.ContainsKey(blueprint))
                        M32_ZoneTradeState.PlayerInventorySnapshot[blueprint] += count;
                    else
                        M32_ZoneTradeState.PlayerInventorySnapshot[blueprint] = count;
                }
            }

            foreach (GameObject merchant in merchants)
            {
                if (merchant == null) continue;

                string merchantId = merchant.ID;
                List<string> itemIds = new List<string>();

                Inventory inv = merchant.GetPart<Inventory>();
                if (inv != null)
                {
                    foreach (GameObject item in inv.GetObjectsDirect())
                    {
                        if (item == null) continue;

                        string itemId = item.ID;
                        itemIds.Add(itemId);
                        M32_ZoneTradeState.PreTradeItemCounts[itemId] = item.Count;
                        M32_ZoneTradeState.ItemOwnerMap[itemId] = merchantId;
                    }
                }

                M32_ZoneTradeState.PreTradeInventorySnapshot[merchantId] = itemIds;
            }
        }

        private List<GameObject> FindFriendlyMerchants()
        {
            List<GameObject> merchants = new List<GameObject>();
            Zone zone = The.ActiveZone;

            if (zone == null)
                return merchants;

            Cell playerCell = The.Player?.GetCurrentCell();
            if (playerCell == null)
                return merchants;

            int playerX = playerCell.X;
            int playerY = playerCell.Y;
            int playerParasangX = playerX / 80;
            int playerParasangY = playerY / 25;

            foreach (GameObject obj in zone.GetObjects())
            {
                if (obj == null) continue;
                if (obj.IsPlayer()) continue;
                if (!obj.IsCreature) continue;

                bool hasCommerce = obj.HasPart<Commerce>();
                bool hasRepair = obj.GetPart<Tinkering_Repair>() != null;
                bool hasRecharge = obj.HasSkill("Tinkering_Tinker1");
                bool hasTinkering = obj.HasSkill("Tinkering") || hasRepair;
                bool hasWontSell = obj.HasPropertyOrTag("WontSell");

                if (!hasCommerce && !hasRepair && !hasRecharge && !hasTinkering)
                    continue;

                if (obj.IsHostileTowards(The.Player))
                    continue;

                if (hasWontSell && !hasRepair && !hasRecharge)
                    continue;

                Inventory inv = obj.GetPart<Inventory>();
                if (inv == null || inv.GetObjectCountDirect() == 0)
                    continue;

                Cell merchantCell = obj.GetCurrentCell();
                if (merchantCell == null)
                    continue;

                int merchantParasangX = merchantCell.X / 80;
                int merchantParasangY = merchantCell.Y / 25;

                if (merchantParasangX != playerParasangX || merchantParasangY != playerParasangY)
                    continue;

                merchants.Add(obj);
            }

            return merchants;
        }

        public static void CleanupVirtualItems()
        {
            int destroyedVirtualItems = 0;
            int adjustedMerchants = 0;
            int totalItemsBought = 0;

            Dictionary<string, int> currentPlayerInventory = new Dictionary<string, int>();
            Inventory playerInv = The.Player?.GetPart<Inventory>();
            if (playerInv != null)
            {
                foreach (GameObject item in playerInv.GetObjectsDirect())
                {
                    if (item == null) continue;

                    string blueprint = item.Blueprint;
                    int count = item.Count;
                    if (currentPlayerInventory.ContainsKey(blueprint))
                        currentPlayerInventory[blueprint] += count;
                    else
                        currentPlayerInventory[blueprint] = count;
                }
            }

            Dictionary<string, int> purchasedAmounts = new Dictionary<string, int>();
            foreach (var currentKvp in currentPlayerInventory)
            {
                string blueprint = currentKvp.Key;
                int currentCount = currentKvp.Value;

                M32_ZoneTradeState.PlayerInventorySnapshot.TryGetValue(blueprint, out int preTradeCount);

                int purchased = currentCount - preTradeCount;
                if (purchased > 0)
                {
                    purchasedAmounts[blueprint] = purchased;
                    totalItemsBought += purchased;
                }
            }

            foreach (var purchaseKvp in purchasedAmounts)
            {
                string blueprint = purchaseKvp.Key;
                int amountToDeduct = purchaseKvp.Value;

                var virtualItemsForBlueprint = M32_ZoneTradeState.VirtualItemRegistry
                    .Where(v => v.Value.Blueprint == blueprint)
                    .ToList();

                if (virtualItemsForBlueprint.Count == 0)
                    continue;

                foreach (var virtualKvp in virtualItemsForBlueprint)
                {
                    if (amountToDeduct <= 0) break;

                    VirtualItemData virtualData = virtualKvp.Value;

                    for (int i = 0; i < virtualData.SourceItemIds.Count && amountToDeduct > 0; i++)
                    {
                        string sourceItemId = virtualData.SourceItemIds[i];
                        int sourceOriginalCount = virtualData.SourceItemCounts[i];

                        GameObject sourceItem = GameObject.FindByID(sourceItemId);
                        if (sourceItem == null)
                        {
                            amountToDeduct -= sourceOriginalCount;
                            continue;
                        }

                        GameObject owner = sourceItem.InInventory;
                        if (owner == null || owner.IsPlayer())
                        {
                            amountToDeduct -= sourceOriginalCount;
                            continue;
                        }

                        int deductFromThis = Math.Min(amountToDeduct, sourceOriginalCount);

                        Inventory ownerInv = owner.GetPart<Inventory>();
                        if (ownerInv != null)
                        {
                            if (deductFromThis >= sourceItem.Count)
                            {
                                ownerInv.RemoveObject(sourceItem, null);
                            }
                            else
                            {
                                sourceItem.Count -= deductFromThis;
                            }
                            adjustedMerchants++;
                        }

                        amountToDeduct -= deductFromThis;
                    }
                }
            }

            var allObjects = The.ActiveZone?.GetObjects();
            if (allObjects != null)
            {
                foreach (GameObject obj in allObjects.ToList())
                {
                    if (obj != null && obj.HasStringProperty("M32_VirtualItem"))
                    {
                        obj.Destroy();
                        destroyedVirtualItems++;
                    }
                }
            }

            UnityEngine.Debug.Log($"[M32_QoL] Trade complete: {totalItemsBought} items bought, {adjustedMerchants} merchant adjustments, {destroyedVirtualItems} virtual items destroyed");
        }

        public static void ReconcileMerchantInventories()
        {
            if (!M32_ZoneTradeState.IsZoneTrade)
                return;

            if (M32_ZoneTradeState.ItemOwnerMap.Count == 0)
                return;

            int movedItems = 0;

            HashSet<string> merchantIds = new HashSet<string>();
            Dictionary<string, GameObject> merchantMap = new Dictionary<string, GameObject>();
            foreach (GameObject merchant in M32_ZoneTradeState.ZoneMerchants)
            {
                if (merchant != null)
                {
                    merchantIds.Add(merchant.ID);
                    merchantMap[merchant.ID] = merchant;
                }
            }

            foreach (var kvp in M32_ZoneTradeState.ItemOwnerMap)
            {
                string itemId = kvp.Key;
                string originalOwnerId = kvp.Value;

                GameObject item = GameObject.FindByID(itemId);
                if (item == null)
                    continue;

                GameObject currentOwner = item.InInventory;
                if (currentOwner == null)
                    continue;

                if (currentOwner.ID == originalOwnerId)
                    continue;

                if (currentOwner.IsPlayer())
                    continue;

                if (merchantIds.Contains(currentOwner.ID))
                {
                    if (!merchantMap.TryGetValue(originalOwnerId, out GameObject originalOwner))
                        continue;

                    Inventory currentInv = currentOwner.GetPart<Inventory>();
                    Inventory originalInv = originalOwner.GetPart<Inventory>();

                    if (currentInv != null && originalInv != null)
                    {
                        try
                        {
                            currentInv.RemoveObject(item, null);
                            originalInv.AddObject(item, null);
                            movedItems++;
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogError($"[M32_QoL] Failed to reconcile item: {ex.Message}");
                        }
                    }
                }
            }

            if (movedItems > 0)
            {
                UnityEngine.Debug.Log($"[M32_QoL] Reconciliation complete: {movedItems} items moved to correct merchants");
            }
        }
    }

    #endregion

    #region Harmony Patches

    public static class M32_ZoneTradePatches
    {
        private static System.Reflection.FieldInfo _tradeEntryGoField;

        private static GameObject GetTradeEntryObject(TradeEntry entry)
        {
            if (entry == null) return null;
            if (_tradeEntryGoField == null)
            {
                _tradeEntryGoField = typeof(TradeEntry).GetField("GO",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            }
            return _tradeEntryGoField?.GetValue(entry) as GameObject;
        }

        private static List<TradeEntry> MergeEntries(List<TradeEntry> entries, GameObject buyer)
        {
            if (entries == null || entries.Count == 0)
                return new List<TradeEntry>();

            List<MergedItemGroup> groups = new List<MergedItemGroup>();

            foreach (TradeEntry entry in entries)
            {
                GameObject entryObj = GetTradeEntryObject(entry);
                if (entryObj == null) continue;

                bool addedToGroup = false;
                foreach (var group in groups)
                {
                    if (group.SourceItems.Count > 0)
                    {
                        GameObject groupObj = group.SourceItems[0];
                        if (groupObj != null && M32_Helpers.CanStackForMerge(entryObj, groupObj))
                        {
                            group.SourceItems.Add(entryObj);
                            addedToGroup = true;
                            break;
                        }
                    }
                }

                if (!addedToGroup)
                {
                    var newGroup = new MergedItemGroup
                    {
                        Blueprint = entryObj.Blueprint,
                        SourceItems = new List<GameObject> { entryObj }
                    };
                    groups.Add(newGroup);
                }
            }

            List<TradeEntry> result = new List<TradeEntry>();
            const int MAX_STACK = 1000;
            int groupIndex = 0;

            foreach (var group in groups)
            {
                if (group.SourceItems.Count == 1)
                {
                    TradeEntry originalEntry = entries.FirstOrDefault(e => GetTradeEntryObject(e) == group.SourceItems[0]);
                    if (originalEntry != null)
                    {
                        result.Add(originalEntry);
                    }
                    continue;
                }

                var sourceItems = group.SourceItems.Where(x => x != null).ToList();
                int totalCount = sourceItems.Sum(x => x.Count);
                string groupKey = $"{group.Blueprint}_{groupIndex}";

                int remainingCount = totalCount;
                int currentSourceIndex = 0;
                int remainingInCurrentSource = sourceItems[0]?.Count ?? 0;

                while (remainingCount > 0 && currentSourceIndex < sourceItems.Count)
                {
                    int stackCount = Math.Min(remainingCount, MAX_STACK);

                    var virtualStackSources = new List<string>();
                    int remainingForStack = stackCount;
                    int tempSourceIndex = currentSourceIndex;
                    int tempRemainingInSource = remainingInCurrentSource;

                    while (remainingForStack > 0 && tempSourceIndex < sourceItems.Count)
                    {
                        int takeFromSource = Math.Min(remainingForStack, tempRemainingInSource);
                        if (!virtualStackSources.Contains(sourceItems[tempSourceIndex].ID))
                        {
                            virtualStackSources.Add(sourceItems[tempSourceIndex].ID);
                        }
                        remainingForStack -= takeFromSource;
                        tempRemainingInSource -= takeFromSource;

                        if (tempRemainingInSource <= 0)
                        {
                            tempSourceIndex++;
                            if (tempSourceIndex < sourceItems.Count)
                            {
                                tempRemainingInSource = sourceItems[tempSourceIndex].Count;
                            }
                        }
                    }

                    GameObject virtualItem = GameObject.Create(group.Blueprint);
                    if (virtualItem != null)
                    {
                        GameObject firstSource = sourceItems.First();

                        virtualItem.Count = stackCount;

                        int numberProp = firstSource.GetIntProperty("Number");
                        if (numberProp != 0)
                        {
                            virtualItem.SetIntProperty("Number", numberProp);
                        }

                        virtualItem.SetStringProperty("M32_VirtualItem", "true");
                        virtualItem.SetStringProperty("M32_VirtualGroup", groupKey);

                        string virtualId = virtualItem.ID;
                        var virtualData = new VirtualItemData
                        {
                            VirtualItemId = virtualId,
                            Blueprint = group.Blueprint,
                            OriginalCount = stackCount,
                            SourceItemIds = virtualStackSources,
                            SourceItemCounts = virtualStackSources.Select(id =>
                                M32_ZoneTradeState.PreTradeItemCounts.TryGetValue(id, out int count) ? count : 0
                            ).ToList()
                        };
                        M32_ZoneTradeState.VirtualItemRegistry[virtualId] = virtualData;

                        try
                        {
                            Type tradeEntryType = typeof(TradeEntry);
                            var ctor = tradeEntryType.GetConstructor(new[] { typeof(GameObject) });
                            if (ctor != null)
                            {
                                TradeEntry virtualEntry = (TradeEntry)ctor.Invoke(new object[] { virtualItem });
                                result.Add(virtualEntry);
                            }
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogError($"[M32_QoL] Failed to create TradeEntry for virtual item: {ex}");
                            foreach (var sourceItem in sourceItems)
                            {
                                TradeEntry originalEntry = entries.FirstOrDefault(e => GetTradeEntryObject(e) == sourceItem);
                                if (originalEntry != null && !result.Contains(originalEntry))
                                    result.Add(originalEntry);
                            }
                        }
                    }

                    remainingForStack = stackCount;
                    while (remainingForStack > 0 && currentSourceIndex < sourceItems.Count)
                    {
                        int takeFromSource = Math.Min(remainingForStack, remainingInCurrentSource);
                        remainingForStack -= takeFromSource;
                        remainingInCurrentSource -= takeFromSource;

                        if (remainingInCurrentSource <= 0)
                        {
                            currentSourceIndex++;
                            if (currentSourceIndex < sourceItems.Count)
                            {
                                remainingInCurrentSource = sourceItems[currentSourceIndex].Count;
                            }
                        }
                    }

                    remainingCount -= stackCount;
                }

                groupIndex++;
            }

            return result;
        }

        [HarmonyPatch(typeof(TradeUI), nameof(TradeUI.GetObjects))]
        public static class TradeUI_GetObjects_Patch
        {
            private static bool _isConsolidating = false;

            public static bool Prefix(
                GameObject Trader,
                List<TradeEntry> ReturnObjects,
                GameObject Other,
                float CostMultiple)
            {
                if (_isConsolidating)
                    return true;

                if (!M32_ZoneTradeState.IsZoneTrade)
                    return true;

                if (M32_ZoneTradeState.ZoneMerchants == null || M32_ZoneTradeState.ZoneMerchants.Count == 0)
                {
                    M32_ZoneTradeState.Reset();
                    return true;
                }

                if (M32_ZoneTradeState.ZoneMerchants.Count == 1)
                {
                    M32_ZoneTradeState.Reset();
                    return true;
                }

                if (Trader.IsPlayer())
                    return true;

                _isConsolidating = true;

                try
                {
                    List<TradeEntry> allEntries = new List<TradeEntry>();

                    foreach (GameObject merchant in M32_ZoneTradeState.ZoneMerchants)
                    {
                        if (merchant == null) continue;
                        TradeUI.GetObjects(merchant, allEntries, Other, CostMultiple);
                    }

                    // Sort entries by Blueprint for consistent grouping
                    // Uses Blueprint instead of DisplayName to avoid triggering
                    // the game's internal DescriptionBuilder sort bug
                    allEntries.Sort((a, b) => {
                        try
                        {
                            GameObject objA = GetTradeEntryObject(a);
                            GameObject objB = GetTradeEntryObject(b);
                            string bpA = objA?.Blueprint ?? "";
                            string bpB = objB?.Blueprint ?? "";
                            return string.Compare(bpA, bpB, StringComparison.OrdinalIgnoreCase);
                        }
                        catch
                        {
                            return 0;
                        }
                    });

                    List<TradeEntry> mergedEntries = MergeEntries(allEntries, Other);
                    ReturnObjects.AddRange(mergedEntries);
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[M32_QoL] Error in TradeUI.GetObjects patch: {ex}");
                    throw;
                }
                finally
                {
                    _isConsolidating = false;
                }

                return false;
            }
        }

        [HarmonyPatch]
        public static class TradeScreen_Close_Patch
        {
            [HarmonyTargetMethod]
            public static System.Reflection.MethodBase TargetMethod()
            {
                System.Type tradeScreenType = AccessTools.TypeByName("Qud.UI.TradeScreen");
                if (tradeScreenType == null)
                {
                    return null;
                }

                return AccessTools.Method(tradeScreenType, "Hide")
                    ?? AccessTools.Method(tradeScreenType, "Cleanup");
            }

            [HarmonyPostfix]
            public static void Postfix()
            {
                if (M32_ZoneTradeState.IsZoneTrade)
                {
                    M32_ZoneTradePart.CleanupVirtualItems();
                    M32_ZoneTradePart.ReconcileMerchantInventories();
                    M32_ZoneTradeState.Reset();
                }
            }
        }

        [HarmonyPatch]
        public static class TradeUI_PerformOffer_Patch
        {
            private static System.Reflection.MethodBase _targetMethod;

            [HarmonyTargetMethod]
            public static System.Reflection.MethodBase TargetMethod()
            {
                if (_targetMethod != null)
                    return _targetMethod;

                System.Type tradeUIType = AccessTools.TypeByName("XRL.UI.TradeUI");
                if (tradeUIType == null)
                {
                    UnityEngine.Debug.LogError("[M32_QoL] Could not find XRL.UI.TradeUI type");
                    return null;
                }

                _targetMethod = AccessTools.Method(tradeUIType, "PerformOffer", new[] {
                    typeof(int),
                    typeof(bool),
                    typeof(GameObject),
                    typeof(TradeUI.TradeScreenMode),
                    typeof(List<TradeEntry>[]),
                    typeof(int[][])
                });

                if (_targetMethod == null)
                {
                    UnityEngine.Debug.LogError("[M32_QoL] Could not find PerformOffer method");
                }
                else
                {
                    UnityEngine.Debug.Log("[M32_QoL] Found TradeUI.PerformOffer method");
                }

                return _targetMethod;
            }

            [HarmonyPostfix]
            public static void Postfix(ref XRL.UI.TradeUI.OfferStatus __result)
            {
                if (!M32_ZoneTradeState.IsZoneTrade)
                    return;

                if (__result == XRL.UI.TradeUI.OfferStatus.REFRESH || __result == XRL.UI.TradeUI.OfferStatus.TOP)
                {
                    __result = XRL.UI.TradeUI.OfferStatus.CLOSE;
                }
            }
        }
    }

    #endregion
}