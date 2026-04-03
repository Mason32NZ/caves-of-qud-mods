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
    /// <summary>
    /// Initializes Harmony patches when the game starts.
    /// </summary>
    [HasModSensitiveStaticCache]
    public static class M32_HarmonyInitializer
    {
        static M32_HarmonyInitializer()
        {
            UnityEngine.Debug.Log("[M32_QoL] Initializing Harmony patches...");
            try
            {
                var harmony = new Harmony("M32_QoL");
                harmony.PatchAll();
                UnityEngine.Debug.Log("[M32_QoL] Harmony patches applied successfully");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[M32_QoL] Failed to apply Harmony patches: {ex}");
            }
        }

        public static void Init()
        {
            // Static constructor handles initialization
            UnityEngine.Debug.Log("[M32_QoL] HarmonyInitializer.Init() called");
        }
    }

    /// <summary>
    /// Auto-updated options flags. Must be static class with HasOptionFlagUpdate attribute.
    /// </summary>
    [HasOptionFlagUpdate(Prefix = "Option_M32QoL_")]
    public static class M32_Options
    {
        [OptionFlag]
        public static bool EnableZoneTrade;
    }

    /// <summary>
    /// Adds the ZoneTrade part to new games.
    /// </summary>
    [PlayerMutator]
    public class M32_PlayerMutator : IPlayerMutator
    {
        public void mutate(GameObject player)
        {
            UnityEngine.Debug.Log("[M32_QoL] PlayerMutator: Adding M32_ZoneTradePart to player");
            // Ensure Harmony patches are initialized
            M32_HarmonyInitializer.Init();
            player.RequirePart<M32_ZoneTradePart>();
        }
    }

    /// <summary>
    /// Adds the ZoneTrade part to loaded saves.
    /// </summary>
    [HasCallAfterGameLoaded]
    public class M32_LoadGameHandler
    {
        [CallAfterGameLoaded]
        public static void AfterLoaded()
        {
            UnityEngine.Debug.Log("[M32_QoL] LoadGameHandler: AfterLoaded called");
            // Ensure Harmony patches are initialized
            M32_HarmonyInitializer.Init();
            if (The.Player == null)
            {
                UnityEngine.Debug.Log("[M32_QoL] LoadGameHandler: Player is null!");
                return;
            }
            UnityEngine.Debug.Log("[M32_QoL] LoadGameHandler: Adding M32_ZoneTradePart to player");
            The.Player.RequirePart<M32_ZoneTradePart>();
        }
    }

    /// <summary>
    /// Stores data about a virtual item created for merged display.
    /// </summary>
    public class VirtualItemData
    {
        /// <summary>
        /// The virtual item's GameObject ID.
        /// </summary>
        public string VirtualItemId;
        
        /// <summary>
        /// The blueprint of the virtual item (e.g., "SteelArrow").
        /// </summary>
        public string Blueprint;
        
        /// <summary>
        /// The original count this virtual item represented.
        /// </summary>
        public int OriginalCount;
        
        /// <summary>
        /// Ordered list of source item IDs (FIFO - first items are deducted first).
        /// </summary>
        public List<string> SourceItemIds = new List<string>();
        
        /// <summary>
        /// Count of each source item (parallel to SourceItemIds).
        /// </summary>
        public List<int> SourceItemCounts = new List<int>();
    }

    /// <summary>
    /// Stores state for Zone Trade across the trade session.
    /// </summary>
    public static class M32_ZoneTradeState
    {
        /// <summary>
        /// Whether the current trade session is a Zone Trade (multiple merchants).
        /// </summary>
        public static bool IsZoneTrade = false;

        /// <summary>
        /// List of all friendly merchants in the zone during Zone Trade.
        /// </summary>
        public static List<GameObject> ZoneMerchants = new List<GameObject>();

        /// <summary>
        /// Snapshot of merchant inventories before trade (merchant ID -> list of item IDs).
        /// </summary>
        public static Dictionary<string, List<string>> PreTradeInventorySnapshot = new Dictionary<string, List<string>>();

        /// <summary>
        /// Snapshot of item counts before trade (item ID -> count).
        /// </summary>
        public static Dictionary<string, int> PreTradeItemCounts = new Dictionary<string, int>();

        /// <summary>
        /// Track which merchant owns each item (item ID -> merchant ID).
        /// </summary>
        public static Dictionary<string, string> ItemOwnerMap = new Dictionary<string, string>();

        /// <summary>
        /// Snapshot of player inventory before trade (blueprint -> count).
        /// Used to detect what was purchased.
        /// </summary>
        public static Dictionary<string, int> PlayerInventorySnapshot = new Dictionary<string, int>();

        /// <summary>
        /// Registry of all virtual items created for this trade session.
        /// Key: virtual item ID, Value: VirtualItemData with mapping info.
        /// Persists even after virtual GameObjects are destroyed.
        /// </summary>
        public static Dictionary<string, VirtualItemData> VirtualItemRegistry = new Dictionary<string, VirtualItemData>();

        /// <summary>
        /// Reset the state after trade ends.
        /// </summary>
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

    /// <summary>
    /// Represents a group of identical items that can be merged for display.
    /// Tracks the source item IDs for inventory comparison after trade.
    /// </summary>
    public class MergedItemGroup
    {
        /// <summary>
        /// The blueprint ID for items in this group.
        /// </summary>
        public string Blueprint;

        /// <summary>
        /// The representative item name for display.
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// List of source item GameObjects in this group.
        /// </summary>
        public List<GameObject> SourceItems = new List<GameObject>();

        /// <summary>
        /// Total count across all items in the group.
        /// </summary>
        public int TotalCount => SourceItems.Sum(i => i?.Count ?? 0);
    }

    /// <summary>
    /// Main Zone Trade implementation. Allows trading with all friendly merchants in the zone.
    /// </summary>
    [Serializable]
    public class M32_ZoneTradePart : IScribedPart
    {
        public override bool AllowStaticRegistration() => true;

        public override void Attach()
        {
            UnityEngine.Debug.Log($"[M32_QoL] M32_ZoneTradePart attached to {ParentObject?.DisplayName}");
            base.Attach();
        }

        public override bool WantEvent(int ID, int cascade)
        {
            bool wants = base.WantEvent(ID, cascade) || ID == CommandEvent.ID;
            if (ID == CommandEvent.ID)
            {
                UnityEngine.Debug.Log("[M32_QoL] WantEvent: Responding true for CommandEvent");
            }
            return wants;
        }

        public override bool HandleEvent(CommandEvent E)
        {
            UnityEngine.Debug.Log($"[M32_QoL] HandleEvent received command: {E.Command}");
            if (E.Command == "M32QoL_ZoneTrade")
            {
                UnityEngine.Debug.Log("[M32_QoL] ZoneTrade command received, opening trade...");
                OpenZoneTrade();
                return false;
            }
            return base.HandleEvent(E);
        }

        /// <summary>
        /// Main entry point for Zone Trade. Checks if enabled, finds merchants, and opens trade UI.
        /// </summary>
        private void OpenZoneTrade()
        {
            UnityEngine.Debug.Log($"[M32_QoL] OpenZoneTrade called. EnableZoneTrade={M32_Options.EnableZoneTrade}");
            
            // Check if feature is enabled
            if (!M32_Options.EnableZoneTrade)
            {
                UnityEngine.Debug.Log("[M32_QoL] ZoneTrade is disabled, returning");
                return;
            }

            // Find friendly merchants in the zone
            List<GameObject> merchants = FindFriendlyMerchants();
            UnityEngine.Debug.Log($"[M32_QoL] Found {merchants.Count} friendly merchants");

            if (merchants.Count == 0)
            {
                UnityEngine.Debug.Log("[M32_QoL] No merchants found, showing popup");
                Popup.Show("There are no friendly merchants in this area.");
                return;
            }

            // Set up Zone Trade state
            M32_ZoneTradeState.IsZoneTrade = true;
            M32_ZoneTradeState.ZoneMerchants = merchants;
            
            // Capture pre-trade inventory snapshot for reconciliation
            CaptureInventorySnapshot(merchants);
            
            UnityEngine.Debug.Log($"[M32_QoL] Opening trade with {merchants.Count} merchants");

            try
            {
                // Find the best merchant with the highest tier services
                // This gives the player access to the best identify/repair/recharge services available
                GameObject primaryMerchant = merchants[0];
                int bestIdentifyLevel = Tinkering.GetIdentifyLevel(primaryMerchant);
                bool bestHasRepair = primaryMerchant.GetPart<Tinkering_Repair>() != null;
                bool bestHasRecharge = primaryMerchant.HasSkill("Tinkering_Tinker1");
                int bestScore = bestIdentifyLevel + (bestHasRepair ? 10 : 0) + (bestHasRecharge ? 10 : 0);
                
                UnityEngine.Debug.Log($"[M32_QoL] Evaluating {merchants.Count} merchants for best services:");
                UnityEngine.Debug.Log($"[M32_QoL] Merchant 0: {primaryMerchant?.DisplayName} - Identify:{bestIdentifyLevel}, Repair:{bestHasRepair}, Recharge:{bestHasRecharge}, Score:{bestScore}");
                
                for (int i = 1; i < merchants.Count; i++)
                {
                    var merchant = merchants[i];
                    int identifyLevel = Tinkering.GetIdentifyLevel(merchant);
                    bool hasRepair = merchant.GetPart<Tinkering_Repair>() != null;
                    bool hasRecharge = merchant.HasSkill("Tinkering_Tinker1");
                    
                    // Calculate a score for this merchant
                    // Higher identify level = better
                    // Having repair = +10 points
                    // Having recharge = +10 points
                    int merchantScore = identifyLevel + (hasRepair ? 10 : 0) + (hasRecharge ? 10 : 0);
                    
                    UnityEngine.Debug.Log($"[M32_QoL] Merchant {i}: {merchant?.DisplayName} - Identify:{identifyLevel}, Repair:{hasRepair}, Recharge:{hasRecharge}, Score:{merchantScore}");
                    
                    // Prefer merchants with better overall services
                    if (merchantScore > bestScore)
                    {
                        bestIdentifyLevel = identifyLevel;
                        bestHasRepair = hasRepair;
                        bestHasRecharge = hasRecharge;
                        bestScore = merchantScore;
                        primaryMerchant = merchant;
                        UnityEngine.Debug.Log($"[M32_QoL] -> New best merchant selected!");
                    }
                }
                
                UnityEngine.Debug.Log($"[M32_QoL] Opening trade with primary merchant: {primaryMerchant?.DisplayName} (Identify:{bestIdentifyLevel}, Repair:{bestHasRepair}, Recharge:{bestHasRecharge}, Score:{bestScore})");
                
                // Use TradeUI.ShowTradeScreen - if merchant has services, player gets access to them
                TradeUI.ShowTradeScreen(primaryMerchant, 1f, TradeUI.TradeScreenMode.Trade);
                UnityEngine.Debug.Log("[M32_QoL] TradeUI.ShowTradeScreen called successfully");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[M32_QoL] Error opening trade: {ex}");
                Popup.Show("Error opening zone trade. Check Player.log for details.");
                M32_ZoneTradeState.Reset();
            }
        }

        /// <summary>
        /// Captures a snapshot of all merchant inventories AND player inventory before trade.
        /// This is used for post-trade reconciliation and to detect what the player purchased.
        /// </summary>
        private void CaptureInventorySnapshot(List<GameObject> merchants)
        {
            UnityEngine.Debug.Log($"[M32_QoL] Capturing inventory snapshot for {merchants.Count} merchants and player");
            
            M32_ZoneTradeState.PreTradeInventorySnapshot.Clear();
            M32_ZoneTradeState.PreTradeItemCounts.Clear();
            M32_ZoneTradeState.ItemOwnerMap.Clear();
            M32_ZoneTradeState.PlayerInventorySnapshot.Clear();
            M32_ZoneTradeState.VirtualItemRegistry.Clear();
            
            // Capture player inventory snapshot (by blueprint for comparison)
            Inventory playerInv = The.Player?.GetPart<Inventory>();
            if (playerInv != null)
            {
                foreach (GameObject item in playerInv.GetObjectsDirect())
                {
                    if (item != null)
                    {
                        string blueprint = item.Blueprint;
                        int count = item.Count;
                        if (M32_ZoneTradeState.PlayerInventorySnapshot.ContainsKey(blueprint))
                            M32_ZoneTradeState.PlayerInventorySnapshot[blueprint] += count;
                        else
                            M32_ZoneTradeState.PlayerInventorySnapshot[blueprint] = count;
                    }
                }
            }
            UnityEngine.Debug.Log($"[M32_QoL] Player inventory snapshot: {M32_ZoneTradeState.PlayerInventorySnapshot.Count} unique blueprints");
            
            // Capture merchant inventories
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
                        if (item != null)
                        {
                            string itemId = item.ID;
                            itemIds.Add(itemId);
                            M32_ZoneTradeState.PreTradeItemCounts[itemId] = item.Count;
                            M32_ZoneTradeState.ItemOwnerMap[itemId] = merchantId;
                        }
                    }
                }
                
                M32_ZoneTradeState.PreTradeInventorySnapshot[merchantId] = itemIds;
                UnityEngine.Debug.Log($"[M32_QoL] Snapshot for {merchant.DisplayName}: {itemIds.Count} items");
            }
            
            UnityEngine.Debug.Log($"[M32_QoL] Total merchant items in snapshot: {M32_ZoneTradeState.ItemOwnerMap.Count}");
        }

        /// <summary>
        /// Finds all friendly merchants in the same parasang as the player.
        /// Merchants must have Commerce part, be non-hostile, have items to sell,
        /// and be in the same local area (parasang) as the player.
        /// </summary>
        private List<GameObject> FindFriendlyMerchants()
        {
            List<GameObject> merchants = new List<GameObject>();
            Zone zone = The.ActiveZone;

            if (zone == null)
            {
                UnityEngine.Debug.Log("[M32_QoL] FindFriendlyMerchants: zone is null");
                return merchants;
            }

            // Get player's current cell to determine which parasang they're in
            Cell playerCell = The.Player.GetCurrentCell();
            if (playerCell == null)
            {
                UnityEngine.Debug.Log("[M32_QoL] FindFriendlyMerchants: player cell is null");
                return merchants;
            }

            int playerX = playerCell.X;
            int playerY = playerCell.Y;
            
            UnityEngine.Debug.Log($"[M32_QoL] FindFriendlyMerchants: Player at ({playerX}, {playerY}) in zone {zone.ZoneID}");

            foreach (GameObject obj in zone.GetObjects())
            {
                // Skip the player
                if (obj.IsPlayer())
                {
                    continue;
                }

                // Must be a creature (not furniture, walls, etc.)
                if (!obj.IsCreature)
                {
                    continue;
                }

                // Check if this is a merchant or service provider
                bool hasCommerce = obj.HasPart<Commerce>();
                bool hasRepair = obj.GetPart<Tinkering_Repair>() != null;
                bool hasRecharge = obj.HasSkill("Tinkering_Tinker1");
                bool hasTinkering = obj.HasSkill("Tinkering") || obj.GetPart<Tinkering_Repair>() != null;
                bool hasWontSell = obj.HasPropertyOrTag("WontSell");
                
                // Debug log for potential merchants
                if (obj.DisplayName.Contains("Argyve") || obj.DisplayName.Contains("tinker"))
                {
                    UnityEngine.Debug.Log($"[M32_QoL] Checking {obj.DisplayName}: Commerce={hasCommerce}, Repair={hasRepair}, Recharge={hasRecharge}, Tinkering={hasTinkering}, WontSell={hasWontSell}");
                }
                
                // Must have Commerce OR be a service provider (repair/recharge/tinkering)
                if (!hasCommerce && !hasRepair && !hasRecharge && !hasTinkering)
                {
                    continue;
                }

                // Check if merchant is not hostile toward player
                if (obj.IsHostileTowards(The.Player))
                {
                    if (obj.DisplayName.Contains("Argyve")) UnityEngine.Debug.Log($"[M32_QoL] {obj.DisplayName} filtered out: is hostile");
                    continue;
                }

                // Skip if merchant has WontSell property UNLESS they have valuable services
                // Some NPCs like Argyve have WontSell but provide identify/repair/recharge
                if (hasWontSell && !hasRepair && !hasRecharge)
                {
                    if (obj.DisplayName.Contains("Argyve")) UnityEngine.Debug.Log($"[M32_QoL] {obj.DisplayName} filtered out: has WontSell and no services");
                    continue;
                }

                // Must have an inventory with items
                Inventory inv = obj.GetPart<Inventory>();
                if (inv == null || inv.GetObjectCountDirect() == 0)
                {
                    if (obj.DisplayName.Contains("Argyve")) UnityEngine.Debug.Log($"[M32_QoL] {obj.DisplayName} filtered out: no inventory or empty");
                    continue;
                }

                // Check if merchant is in the same parasang as player
                // A parasang is roughly 80x25 cells in Qud
                Cell merchantCell = obj.GetCurrentCell();
                if (merchantCell == null)
                {
                    continue;
                }

                // Calculate parasang coordinates (each parasang is 80x25 cells)
                int playerParasangX = playerX / 80;
                int playerParasangY = playerY / 25;
                int merchantParasangX = merchantCell.X / 80;
                int merchantParasangY = merchantCell.Y / 25;

                // Only include merchants in the same parasang
                if (merchantParasangX != playerParasangX || merchantParasangY != playerParasangY)
                {
                    continue;
                }

                merchants.Add(obj);
            }

            UnityEngine.Debug.Log($"[M32_QoL] FindFriendlyMerchants: Found {merchants.Count} valid merchants in same parasang");
            return merchants;
        }

        /// <summary>
        /// Cleans up after Zone Trade using player inventory comparison (Hybrid Approach).
        /// 
        /// This method:
        /// 1. Captures current player inventory
        /// 2. Compares to pre-trade snapshot to detect what was purchased
        /// 3. Uses virtual item registry to determine which merchants to deduct from
        /// 4. Deducts purchased items from merchant inventories
        /// 5. Destroys any remaining virtual items
        /// </summary>
        public static void CleanupVirtualItems()
        {
            UnityEngine.Debug.Log($"[M32_QoL] Cleaning up Zone Trade using player inventory comparison");
            UnityEngine.Debug.Log($"[M32_QoL] Virtual item registry has {M32_ZoneTradeState.VirtualItemRegistry.Count} entries");
            
            int destroyedVirtualItems = 0;
            int adjustedMerchants = 0;
            int totalItemsBought = 0;
            
            // Step 1: Get current player inventory (post-trade)
            Dictionary<string, int> currentPlayerInventory = new Dictionary<string, int>();
            Inventory playerInv = The.Player?.GetPart<Inventory>();
            if (playerInv != null)
            {
                foreach (GameObject item in playerInv.GetObjectsDirect())
                {
                    if (item != null)
                    {
                        string blueprint = item.Blueprint;
                        int count = item.Count;
                        if (currentPlayerInventory.ContainsKey(blueprint))
                            currentPlayerInventory[blueprint] += count;
                        else
                            currentPlayerInventory[blueprint] = count;
                    }
                }
            }
            
            UnityEngine.Debug.Log($"[M32_QoL] Current player inventory: {currentPlayerInventory.Count} unique blueprints");
            
            // Step 2: Calculate what was purchased (by blueprint)
            Dictionary<string, int> purchasedAmounts = new Dictionary<string, int>();
            foreach (var currentKvp in currentPlayerInventory)
            {
                string blueprint = currentKvp.Key;
                int currentCount = currentKvp.Value;
                
                // Check if player had this before trade
                M32_ZoneTradeState.PlayerInventorySnapshot.TryGetValue(blueprint, out int preTradeCount);
                
                int purchased = currentCount - preTradeCount;
                if (purchased > 0)
                {
                    purchasedAmounts[blueprint] = purchased;
                    UnityEngine.Debug.Log($"[M32_QoL] Player purchased {blueprint}: {purchased} (had {preTradeCount}, now has {currentCount})");
                    totalItemsBought += purchased;
                }
            }
            
            // Step 3: Deduct from merchants using virtual item registry
            foreach (var purchaseKvp in purchasedAmounts)
            {
                string blueprint = purchaseKvp.Key;
                int amountToDeduct = purchaseKvp.Value;
                
                UnityEngine.Debug.Log($"[M32_QoL] Deducting {amountToDeduct} {blueprint} from merchants");
                
                // Find virtual items for this blueprint
                var virtualItemsForBlueprint = M32_ZoneTradeState.VirtualItemRegistry
                    .Where(v => v.Value.Blueprint == blueprint)
                    .ToList();
                
                if (virtualItemsForBlueprint.Count == 0)
                {
                    UnityEngine.Debug.LogWarning($"[M32_QoL] No virtual items found for {blueprint}, cannot deduct");
                    continue;
                }
                
                UnityEngine.Debug.Log($"[M32_QoL] Found {virtualItemsForBlueprint.Count} virtual item groups for {blueprint}");
                
                // Deduct from each virtual item group (FIFO order)
                foreach (var virtualKvp in virtualItemsForBlueprint)
                {
                    if (amountToDeduct <= 0) break;
                    
                    VirtualItemData virtualData = virtualKvp.Value;
                    UnityEngine.Debug.Log($"[M32_QoL] Processing virtual group: {virtualData.SourceItemIds.Count} source items, total {virtualData.SourceItemCounts.Sum()}");
                    
                    // Deduct from source items in FIFO order
                    for (int i = 0; i < virtualData.SourceItemIds.Count && amountToDeduct > 0; i++)
                    {
                        string sourceItemId = virtualData.SourceItemIds[i];
                        int sourceOriginalCount = virtualData.SourceItemCounts[i];
                        
                        GameObject sourceItem = GameObject.FindByID(sourceItemId);
                        if (sourceItem == null)
                        {
                            UnityEngine.Debug.Log($"[M32_QoL] Source item {sourceItemId} not found, assuming already deducted");
                            amountToDeduct -= sourceOriginalCount;
                            continue;
                        }
                        
                        // Check if still with merchant
                        GameObject owner = sourceItem.InInventory;
                        if (owner == null || owner.IsPlayer())
                        {
                            // Item no longer with merchant - already moved to player
                            amountToDeduct -= sourceOriginalCount;
                            continue;
                        }
                        
                        // Calculate how much to deduct from this source item
                        int deductFromThis = Math.Min(amountToDeduct, sourceOriginalCount);
                        
                        Inventory ownerInv = owner.GetPart<Inventory>();
                        if (ownerInv != null)
                        {
                            if (deductFromThis >= sourceItem.Count)
                            {
                                // Remove entire item
                                ownerInv.RemoveObject(sourceItem, null);
                                UnityEngine.Debug.Log($"[M32_QoL] Removed {sourceItem.DisplayNameStripped} from {owner.DisplayName} ({deductFromThis}/{sourceOriginalCount})");
                            }
                            else
                            {
                                // Reduce count
                                sourceItem.Count -= deductFromThis;
                                UnityEngine.Debug.Log($"[M32_QoL] Reduced {sourceItem.DisplayNameStripped} to {sourceItem.Count} (deducted {deductFromThis})");
                            }
                            adjustedMerchants++;
                        }
                        
                        amountToDeduct -= deductFromThis;
                    }
                }
                
                if (amountToDeduct > 0)
                {
                    UnityEngine.Debug.LogWarning($"[M32_QoL] Could not fully deduct {blueprint}, {amountToDeduct} remaining");
                }
            }
            
            // Step 4: Destroy any remaining virtual items
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
            
            UnityEngine.Debug.Log($"[M32_QoL] Cleanup complete: {totalItemsBought} items bought, {adjustedMerchants} merchant adjustments, {destroyedVirtualItems} virtual items destroyed");
        }

        /// <summary>
        /// Reconciles merchant inventories after trade to fix items that accidentally
        /// ended up with the wrong merchant. Only moves items if:
        /// 1. Item is still with a merchant (not with player who bought it)
        /// 2. Item is with a DIFFERENT merchant than its original owner
        /// 
        /// This fixes the case where the trade system puts sold items in the wrong
        /// merchant's inventory, without affecting legitimate player purchases.
        /// </summary>
        public static void ReconcileMerchantInventories()
        {
            UnityEngine.Debug.Log("[M32_QoL] Starting post-trade reconciliation");
            
            if (!M32_ZoneTradeState.IsZoneTrade)
            {
                UnityEngine.Debug.Log("[M32_QoL] Not in Zone Trade, skipping reconciliation");
                return;
            }

            if (M32_ZoneTradeState.ItemOwnerMap.Count == 0)
            {
                UnityEngine.Debug.Log("[M32_QoL] No item ownership data, skipping reconciliation");
                return;
            }

            int movedItems = 0;
            int skippedPlayerItems = 0;
            int alreadyCorrect = 0;
            int errors = 0;

            // Build a map of valid merchants for quick lookup
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

            // For each item that was originally tracked
            foreach (var kvp in M32_ZoneTradeState.ItemOwnerMap)
            {
                string itemId = kvp.Key;
                string originalOwnerId = kvp.Value;

                // Find the item in the current world state
                GameObject item = GameObject.FindByID(itemId);
                if (item == null)
                {
                    // Item was destroyed, fully consumed, or player bought it and it's now gone
                    continue;
                }

                // Find who currently owns this item
                GameObject currentOwner = item.InInventory;
                if (currentOwner == null)
                {
                    // Item is on the ground, in a container, or otherwise not in an inventory
                    continue;
                }

                // Check if item is still with its original owner
                if (currentOwner.ID == originalOwnerId)
                {
                    alreadyCorrect++;
                    continue;
                }

                // Check if current owner is the player (they bought it)
                if (currentOwner.IsPlayer())
                {
                    skippedPlayerItems++;
                    continue;
                }

                // Check if current owner is a merchant (not player, not original owner)
                if (merchantIds.Contains(currentOwner.ID))
                {
                    // Item ended up with the WRONG merchant - fix it
                    if (!merchantMap.TryGetValue(originalOwnerId, out GameObject originalOwner))
                    {
                        UnityEngine.Debug.LogWarning($"[M32_QoL] Original owner {originalOwnerId} no longer exists for {item.DisplayNameStripped}");
                        errors++;
                        continue;
                    }

                    UnityEngine.Debug.Log($"[M32_QoL] Reconciling: {item.DisplayNameStripped} ended up with {currentOwner.DisplayName}, returning to {originalOwner.DisplayName}");
                    
                    Inventory currentInv = currentOwner.GetPart<Inventory>();
                    Inventory originalInv = originalOwner.GetPart<Inventory>();
                    
                    if (currentInv != null && originalInv != null)
                    {
                        // Remove from wrong merchant
                        try
                        {
                            currentInv.RemoveObject(item, null);
                            // Add to correct merchant
                            originalInv.AddObject(item, null);
                            movedItems++;
                            UnityEngine.Debug.Log($"[M32_QoL] Successfully reconciled {item.DisplayNameStripped}");
                        }
                        catch (Exception ex)
                        {
                            errors++;
                            UnityEngine.Debug.LogError($"[M32_QoL] Failed to reconcile {item.DisplayNameStripped}: {ex.Message}");
                        }
                    }
                }
                // If current owner is neither player nor a tracked merchant, leave it be
                // (might have been sold to another NPC not in our zone trade)
            }

            UnityEngine.Debug.Log($"[M32_QoL] Reconciliation complete: {movedItems} items moved, {alreadyCorrect} already correct, {skippedPlayerItems} player purchases preserved, {errors} errors");
        }
    }

    /// <summary>
    /// Harmony patches for Zone Trade functionality.
    /// </summary>
    public static class M32_ZoneTradePatches
    {
        // Cached reflection info for TradeEntry fields (they are internal)
        private static System.Reflection.FieldInfo _tradeEntryGoField;
        
        /// <summary>
        /// Gets the 'GO' field from a TradeEntry via reflection.
        /// TradeEntry has a public field 'GO' that contains the GameObject.
        /// </summary>
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
        
        /// <summary>
        /// Checks if two items can be merged based on their properties.
        /// Items can merge if they have the same blueprint and identical properties.
        /// </summary>
        private static bool CanStackForMerge(GameObject a, GameObject b)
        {
            if (a == null || b == null) return false;
            
            // Must have same blueprint
            if (a.Blueprint != b.Blueprint) return false;
            
            // Check for Unique tag - never merge unique items
            if (a.HasTag("Unique") || b.HasTag("Unique")) return false;
            
            try
            {
                // Check liquid contents - do NOT merge if either has liquids
                LiquidVolume volA = a.LiquidVolume;
                LiquidVolume volB = b.LiquidVolume;
                if (volA != null && volA.Volume > 0) return false;
                if (volB != null && volB.Volume > 0) return false;
                
                // Check modifications - must be identical
                // Check for Modification part (items with mods can't stack with unmodded items)
                bool aHasMods = a.HasPart("Modification");
                bool bHasMods = b.HasPart("Modification");
                if (aHasMods != bHasMods) return false;
                
                // Check containers with items inside
                Inventory invA = a.GetPart<Inventory>();
                Inventory invB = b.GetPart<Inventory>();
                if (invA != null && invA.GetObjectCountDirect() > 0) return false;
                if (invB != null && invB.GetObjectCountDirect() > 0) return false;
                
                // Check various properties that affect stacking
                if (a.GetIntProperty("Number") != b.GetIntProperty("Number"))
                    return false;
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Merges identical items from different merchants into virtual GameObjects with max 1000 per stack.
        /// Items with same blueprint and properties are merged together.
        /// Creates temporary virtual items for display; original items remain with merchants.
        /// </summary>
        private static List<TradeEntry> MergeEntries(List<TradeEntry> entries, GameObject buyer)
        {
            if (entries == null || entries.Count == 0)
                return new List<TradeEntry>();
            
            UnityEngine.Debug.Log($"[M32_QoL] Merging {entries.Count} trade entries");
            
            // Group items that can be merged together
            List<MergedItemGroup> groups = new List<MergedItemGroup>();
            
            foreach (TradeEntry entry in entries)
            {
                GameObject entryObj = GetTradeEntryObject(entry);
                if (entryObj == null) continue;
                
                // Try to find an existing group for this item
                bool addedToGroup = false;
                foreach (var group in groups)
                {
                    if (group.SourceItems.Count > 0)
                    {
                        GameObject groupObj = group.SourceItems[0];
                        if (groupObj != null && CanStackForMerge(entryObj, groupObj))
                        {
                            group.SourceItems.Add(entryObj);
                            addedToGroup = true;
                            break;
                        }
                    }
                }
                
                // If no matching group found, create a new one
                if (!addedToGroup)
                {
                    var newGroup = new MergedItemGroup
                    {
                        Blueprint = entryObj.Blueprint,
                        DisplayName = entryObj.DisplayNameStripped,
                        SourceItems = new List<GameObject> { entryObj }
                    };
                    groups.Add(newGroup);
                }
            }
            
            UnityEngine.Debug.Log($"[M32_QoL] Created {groups.Count} merged groups");
            
            // Create virtual merged items for groups with multiple items
            List<TradeEntry> result = new List<TradeEntry>();
            const int MAX_STACK = 1000;
            int groupIndex = 0;
            
            foreach (var group in groups)
            {
                if (group.SourceItems.Count == 1)
                {
                    // Single item, no merging needed - use original TradeEntry
                    TradeEntry originalEntry = entries.FirstOrDefault(e => GetTradeEntryObject(e) == group.SourceItems[0]);
                    if (originalEntry != null)
                    {
                        result.Add(originalEntry);
                    }
                    continue;
                }
                
                // Multiple items - create virtual merged GameObjects
                var sourceItems = group.SourceItems.Where(x => x != null).ToList();
                int totalCount = sourceItems.Sum(x => x.Count);
                
                UnityEngine.Debug.Log($"[M32_QoL] Creating virtual items for '{group.DisplayName}': {sourceItems.Count} sources, total count {totalCount}");
                
                // Create group key for this batch of virtual items
                string groupKey = $"{group.Blueprint}_{groupIndex}";
                
                // Split into max 1000 count stacks
                int remainingCount = totalCount;
                int stackIndex = 0;
                
                // Track which source items contribute to each virtual stack
                int currentSourceIndex = 0;
                int remainingInCurrentSource = sourceItems[0].Count;
                
                while (remainingCount > 0)
                {
                    int stackCount = Math.Min(remainingCount, MAX_STACK);
                    
                    // Track which source items are used for this virtual stack
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
                    
                    // Create a virtual GameObject by copying the first source item
                    GameObject virtualItem = GameObject.Create(group.Blueprint);
                    if (virtualItem != null)
                    {
                        // Copy important properties from the first source
                        GameObject firstSource = sourceItems.First();
                        
                        // Set the count
                        virtualItem.Count = stackCount;
                        
                        // Copy the Number property (for items like scrolls with variations)
                        int numberProp = firstSource.GetIntProperty("Number");
                        if (numberProp != 0)
                        {
                            virtualItem.SetIntProperty("Number", numberProp);
                        }
                        
                        // Mark as virtual so we can identify it later
                        virtualItem.SetStringProperty("M32_VirtualItem", "true");
                        virtualItem.SetStringProperty("M32_VirtualGroup", groupKey);
                        
                        // Register this virtual item with full data for post-trade tracking
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
                        
                        UnityEngine.Debug.Log($"[M32_QoL] Virtual {virtualId} ({group.DisplayName} x{stackCount}) sources: {string.Join(", ", virtualStackSources)}");
                        
                        // Create a TradeEntry for this virtual item
                        // We need to use reflection since TradeEntry constructor is internal
                        try
                        {
                            Type tradeEntryType = typeof(TradeEntry);
                            var ctor = tradeEntryType.GetConstructor(new[] { typeof(GameObject) });
                            if (ctor != null)
                            {
                                TradeEntry virtualEntry = (TradeEntry)ctor.Invoke(new object[] { virtualItem });
                                result.Add(virtualEntry);
                                UnityEngine.Debug.Log($"[M32_QoL] Created virtual item: {group.DisplayName} x{stackCount} (ID: {virtualId})");
                            }
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogError($"[M32_QoL] Failed to create TradeEntry for virtual item: {ex}");
                            // Fall back to using original entries
                            foreach (var sourceItem in sourceItems)
                            {
                                TradeEntry originalEntry = entries.FirstOrDefault(e => GetTradeEntryObject(e) == sourceItem);
                                if (originalEntry != null && !result.Contains(originalEntry))
                                    result.Add(originalEntry);
                            }
                        }
                    }
                    
                    // Update tracking for next stack
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
                    stackIndex++;
                }
                
                groupIndex++;
            }
            
            UnityEngine.Debug.Log($"[M32_QoL] Merged into {result.Count} display entries");
            return result;
        }

        /// <summary>
        /// Patch to consolidate merchant inventory when getting objects for trade.
        /// Instead of manually creating TradeEntry objects (which has internal constructor/fields),
        /// we call the original TradeUI.GetObjects() for each merchant and merge the results.
        /// Items are sorted alphabetically and stacked when possible.
        /// </summary>
        [HarmonyPatch(typeof(TradeUI), nameof(TradeUI.GetObjects))]
        public static class TradeUI_GetObjects_Patch
        {
            // Flag to prevent recursion when we call TradeUI.GetObjects internally
            private static bool _isConsolidating = false;

            public static bool Prefix(
                GameObject Trader,
                List<TradeEntry> ReturnObjects,
                GameObject Other,
                float CostMultiple)
            {
                UnityEngine.Debug.Log($"[M32_QoL] TradeUI.GetObjects Prefix called. Trader={Trader?.DisplayName}, IsZoneTrade={M32_ZoneTradeState.IsZoneTrade}, IsConsolidating={_isConsolidating}");
                
                // If we're in the middle of consolidating, run original method
                if (_isConsolidating)
                {
                    UnityEngine.Debug.Log("[M32_QoL] Currently consolidating, running original method");
                    return true; // Run original method
                }
                
                // If Zone Trade is not active, run original method
                if (!M32_ZoneTradeState.IsZoneTrade)
                {
                    UnityEngine.Debug.Log("[M32_QoL] ZoneTrade not active, running original method");
                    return true; // Run original method
                }

                // If ZoneMerchants is null or empty, run original method
                if (M32_ZoneTradeState.ZoneMerchants == null || M32_ZoneTradeState.ZoneMerchants.Count == 0)
                {
                    UnityEngine.Debug.Log("[M32_QoL] ZoneMerchants is null or empty, running original method");
                    M32_ZoneTradeState.Reset();
                    return true; // Run original method
                }

                // If single merchant, use normal behavior and reset state
                if (M32_ZoneTradeState.ZoneMerchants.Count == 1)
                {
                    UnityEngine.Debug.Log("[M32_QoL] Only 1 merchant in ZoneMerchants, running original method");
                    M32_ZoneTradeState.Reset();
                    return true; // Run original method
                }

                // Check if this is the player's inventory (right side) - don't consolidate player's items
                if (Trader.IsPlayer())
                {
                    UnityEngine.Debug.Log("[M32_QoL] Getting player's inventory - running original method");
                    return true; // Run original method to get player's normal inventory
                }

                // Set flag to prevent recursion
                _isConsolidating = true;

                try
                {
                    UnityEngine.Debug.Log($"[M32_QoL] TradeUI.GetObjects patch: Consolidating {M32_ZoneTradeState.ZoneMerchants.Count} merchants");
                    
                    // Collect all items from all merchants first
                    List<TradeEntry> allEntries = new List<TradeEntry>();
                    
                    foreach (GameObject merchant in M32_ZoneTradeState.ZoneMerchants)
                    {
                        if (merchant == null) continue;
                        
                        UnityEngine.Debug.Log($"[M32_QoL] Getting objects from merchant: {merchant.DisplayName}");
                        int countBefore = allEntries.Count;
                        TradeUI.GetObjects(merchant, allEntries, Other, CostMultiple);
                        UnityEngine.Debug.Log($"[M32_QoL] Added {allEntries.Count - countBefore} items from {merchant.DisplayName}");
                    }
                    
                    UnityEngine.Debug.Log($"[M32_QoL] Total trade entries before sorting/stacking: {allEntries.Count}");
                    
                    if (allEntries.Count == 0)
                    {
                        UnityEngine.Debug.Log("[M32_QoL] WARNING: No entries collected from any merchant!");
                    }
                    
                    // Sort entries alphabetically by item display name
                    allEntries.Sort((a, b) => {
                        GameObject objA = GetTradeEntryObject(a);
                        GameObject objB = GetTradeEntryObject(b);
                        string nameA = objA?.DisplayNameStripped ?? "";
                        string nameB = objB?.DisplayNameStripped ?? "";
                        return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
                    });
                    
                    // Merge identical items from different merchants
                    // Items with same blueprint and properties are merged, with max 1000 per displayed stack
                    List<TradeEntry> mergedEntries = MergeEntries(allEntries, Other);
                    
                    // Add merged entries to the return list
                    ReturnObjects.AddRange(mergedEntries);
                    
                    UnityEngine.Debug.Log($"[M32_QoL] Total trade entries after merging: {ReturnObjects.Count}");
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[M32_QoL] Error in TradeUI.GetObjects patch: {ex}");
                    throw;
                }
                finally
                {
                    // Clear flag
                    _isConsolidating = false;
                }

                return false; // Skip original method (we already handled it)
            }
        }

        /// <summary>
        /// Patch to reset Zone Trade state when trade screen closes.
        /// </summary>
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

                // Try Hide first, then Cleanup
                return AccessTools.Method(tradeScreenType, "Hide")
                    ?? AccessTools.Method(tradeScreenType, "Cleanup");
            }

            [HarmonyPostfix]
            public static void Postfix()
            {
                if (M32_ZoneTradeState.IsZoneTrade)
                {
                    UnityEngine.Debug.Log("[M32_QoL] Trade screen closing, cleaning up virtual items...");

                    // Clean up virtual items and apply changes to originals
                    M32_ZoneTradePart.CleanupVirtualItems();

                    UnityEngine.Debug.Log("[M32_QoL] Running reconciliation...");

                    // Reconcile merchant inventories before resetting state
                    M32_ZoneTradePart.ReconcileMerchantInventories();

                    // Reset the Zone Trade state
                    M32_ZoneTradeState.Reset();
                    
                    UnityEngine.Debug.Log("[M32_QoL] Zone Trade state reset");
                }
            }
        }

        /// <summary>
        /// Patch to intercept ClearAndSetupTradeUI for logging purposes.
        /// The actual force-close is now handled by TradeUI_PerformOffer_Patch.
        /// </summary>
        [HarmonyPatch]
        public static class TradeScreen_ClearAndSetupTradeUI_Patch
        {
            [HarmonyTargetMethod]
            public static System.Reflection.MethodBase TargetMethod()
            {
                System.Type tradeScreenType = AccessTools.TypeByName("Qud.UI.TradeScreen");
                if (tradeScreenType == null)
                {
                    UnityEngine.Debug.LogError("[M32_QoL] Could not find Qud.UI.TradeScreen type for ClearAndSetupTradeUI patch");
                    return null;
                }
                return AccessTools.Method(tradeScreenType, "ClearAndSetupTradeUI");
            }

            [HarmonyPrefix]
            public static void Prefix()
            {
                if (!M32_ZoneTradeState.IsZoneTrade)
                    return;

                UnityEngine.Debug.Log("[M32_QoL] ClearAndSetupTradeUI called");
            }
        }

        /// <summary>
        /// Patch TradeUI.PerformOffer to change the return value from REFRESH to CLOSE when in Zone Trade.
        /// This prevents the UI from refreshing with stale data after a successful trade.
        /// </summary>
        [HarmonyPatch]
        public static class TradeUI_PerformOffer_Patch
        {
            [HarmonyTargetMethod]
            public static System.Reflection.MethodBase TargetMethod()
            {
                // TradeUI is in XRL.UI namespace
                System.Type tradeUIType = AccessTools.TypeByName("XRL.UI.TradeUI");
                if (tradeUIType == null)
                {
                    UnityEngine.Debug.LogError("[M32_QoL] Could not find XRL.UI.TradeUI type");
                    return null;
                }

                // Find PerformOffer method with the right signature
                var methods = tradeUIType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                foreach (var method in methods)
                {
                    if (method.Name == "PerformOffer" && method.GetParameters().Length == 6)
                    {
                        UnityEngine.Debug.Log("[M32_QoL] Found TradeUI.PerformOffer method");
                        return method;
                    }
                }

                UnityEngine.Debug.LogError("[M32_QoL] Could not find PerformOffer method with 6 parameters");
                return null;
            }

            [HarmonyPostfix]
            public static void Postfix(ref XRL.UI.TradeUI.OfferStatus __result)
            {
                if (!M32_ZoneTradeState.IsZoneTrade)
                    return;

                UnityEngine.Debug.Log($"[M32_QoL] TradeUI.PerformOffer returned: {__result}");

                // If the result is REFRESH or TOP, change it to CLOSE to prevent UI refresh
                if (__result == XRL.UI.TradeUI.OfferStatus.REFRESH || __result == XRL.UI.TradeUI.OfferStatus.TOP)
                {
                    UnityEngine.Debug.Log("[M32_QoL] Changing OfferStatus to CLOSE to prevent stale UI");
                    __result = XRL.UI.TradeUI.OfferStatus.CLOSE;
                }
            }
        }
    }
}
