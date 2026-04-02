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
        /// Reset the state after trade ends.
        /// </summary>
        public static void Reset()
        {
            IsZoneTrade = false;
            ZoneMerchants.Clear();
        }
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
    }

    /// <summary>
    /// Harmony patches for Zone Trade functionality.
    /// </summary>
    public static class M32_ZoneTradePatches
    {
        /// <summary>
        /// Patch to consolidate merchant inventory when getting objects for trade.
        /// Instead of manually creating TradeEntry objects (which has internal constructor/fields),
        /// we call the original TradeUI.GetObjects() for each merchant and merge the results.
        /// </summary>
        [HarmonyPatch(typeof(TradeUI), nameof(TradeUI.GetObjects))]
        public static class TradeUI_GetObjects_Patch
        {
            public static bool Prefix(
                GameObject Trader,
                List<TradeEntry> ReturnObjects,
                GameObject Other,
                float CostMultiple)
            {
                UnityEngine.Debug.Log($"[M32_QoL] TradeUI.GetObjects Prefix called. Trader={Trader?.DisplayName}, IsZoneTrade={M32_ZoneTradeState.IsZoneTrade}");
                
                if (!M32_ZoneTradeState.IsZoneTrade)
                {
                    UnityEngine.Debug.Log("[M32_QoL] ZoneTrade not active, running original method");
                    return true; // Run original method
                }

                // If single merchant, use normal behavior
                if (M32_ZoneTradeState.ZoneMerchants.Count <= 1)
                {
                    return true; // Run original method
                }

                // Check if this is the player's inventory (right side) - don't consolidate player's items
                if (Trader.IsPlayer())
                {
                    UnityEngine.Debug.Log("[M32_QoL] Getting player's inventory - running original method");
                    return true; // Run original method to get player's normal inventory
                }

                // Temporarily disable ZoneTrade flag to prevent recursion
                M32_ZoneTradeState.IsZoneTrade = false;

                try
                {
                    UnityEngine.Debug.Log($"[M32_QoL] TradeUI.GetObjects patch: Consolidating {M32_ZoneTradeState.ZoneMerchants.Count} merchants");
                    
                    // Call original GetObjects for each merchant and merge results
                    foreach (GameObject merchant in M32_ZoneTradeState.ZoneMerchants)
                    {
                        UnityEngine.Debug.Log($"[M32_QoL] Getting objects from merchant: {merchant.DisplayName}");
                        int countBefore = ReturnObjects.Count;
                        TradeUI.GetObjects(merchant, ReturnObjects, Other, CostMultiple);
                        UnityEngine.Debug.Log($"[M32_QoL] Added {ReturnObjects.Count - countBefore} items from {merchant.DisplayName}");
                    }
                    
                    UnityEngine.Debug.Log($"[M32_QoL] Total trade entries: {ReturnObjects.Count}");
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[M32_QoL] Error in TradeUI.GetObjects patch: {ex}");
                    throw;
                }
                finally
                {
                    // Re-enable flag
                    M32_ZoneTradeState.IsZoneTrade = true;
                }

                return false; // Skip original method (we already called it for each merchant)
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
                    M32_ZoneTradeState.Reset();
                }
            }
        }
    }
}
