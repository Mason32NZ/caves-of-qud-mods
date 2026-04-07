using System;
using System.Collections.Generic;
using System.Linq;
using XRL;
using XRL.Core;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace M32_QoL
{
    /// <summary>
    /// Auto Loot feature - automatically picks up nearby items when moving.
    /// Integrates with auto-explore to add items as waypoints.
    /// </summary>
    [Serializable]
    public class M32_AutoLootPart : IScribedPart
    {
        private Cell _lastCell = null;
        private long _lastMoveTurn = -1;

        public override bool AllowStaticRegistration() => true;

        public override void Attach()
        {
            UnityEngine.Debug.Log($"[M32_QoL] M32_AutoLootPart attached to {ParentObject?.DisplayName}");
            base.Attach();
        }

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade)
                || ID == BeforeTakeActionEvent.ID
                || ID == AutoexploreObjectEvent.ID
                || ID == EnteredCellEvent.ID;
        }

        public override bool HandleEvent(BeforeTakeActionEvent E)
        {
            if (!M32_Options.EnableAutoLoot)
                return base.HandleEvent(E);

            if (The.Player == null || The.Player != ParentObject)
                return base.HandleEvent(E);

            Cell currentCell = The.Player?.GetCurrentCell();
            if (currentCell == null)
                return base.HandleEvent(E);

            // Only trigger on actual cell change and once per move
            long currentTurn = The.Game?.Turns ?? 0;
            if (currentCell == _lastCell || currentTurn == _lastMoveTurn)
                return base.HandleEvent(E);

            _lastCell = currentCell;
            _lastMoveTurn = currentTurn;

            try
            {
                LootNearbyItems(currentCell);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[M32_QoL] AutoLoot error in BeforeTakeActionEvent: {ex}");
            }

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EnteredCellEvent E)
        {
            if (!M32_Options.EnableAutoLoot)
                return base.HandleEvent(E);

            if (The.Player == null || The.Player != ParentObject)
                return base.HandleEvent(E);

            Cell currentCell = E.Cell;
            if (currentCell == null)
                return base.HandleEvent(E);

            _lastCell = currentCell;
            _lastMoveTurn = The.Game?.Turns ?? 0;

            try
            {
                LootNearbyItems(currentCell);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[M32_QoL] AutoLoot error in EnteredCellEvent: {ex}");
            }

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(AutoexploreObjectEvent E)
        {
            if (!M32_Options.EnableAutoLoot)
                return base.HandleEvent(E);

            if (E.Item == null || The.Player == null)
                return base.HandleEvent(E);

            try
            {
                // Check if this object should be auto-looted
                if (ShouldAutoLoot(E.Item, The.Player))
                {
                    // Tell auto-explore to pick up this item
                    E.Command = "Autoget";
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[M32_QoL] AutoLoot error in AutoexploreObjectEvent: {ex}");
            }

            return base.HandleEvent(E);
        }

        private void LootNearbyItems(Cell playerCell)
        {
            if (playerCell == null || The.Player == null)
                return;

            // Check if in combat and combat looting is disabled
            if (!M32_Options.AutoLootInCombat && The.Player.IsInCombat())
                return;

            List<GameObject> itemsToLoot = new List<GameObject>();

            // Check player's tile and adjacent tiles (9 cells total)
            List<Cell> cellsToCheck = new List<Cell> { playerCell };
            cellsToCheck.AddRange(playerCell.GetLocalAdjacentCells());

            foreach (Cell cell in cellsToCheck)
            {
                if (cell == null) continue;

                foreach (GameObject obj in cell.GetObjects())
                {
                    if (obj == null || obj.IsPlayer()) continue;

                    // Check containers first - loot contents (but not from creatures/NPCs)
                    Inventory containerInv = obj.GetPart<Inventory>();
                    if (containerInv != null && obj.LiquidVolume == null && !obj.IsCreature)
                    {
                        // Non-liquid container - handle contents
                        HandleContainerContents(obj, The.Player, itemsToLoot);
                    }

                    // Check if this item should be auto-looted
                    if (ShouldAutoLoot(obj, The.Player))
                    {
                        itemsToLoot.Add(obj);
                    }
                }
            }

            // Sort by weight (lowest first)
            itemsToLoot.Sort((a, b) => {
                try
                {
                    int weightA = a?.Weight ?? 0;
                    int weightB = b?.Weight ?? 0;
                    return weightA.CompareTo(weightB);
                }
                catch
                {
                    return 0;
                }
            });

            // Loot items
            int lootedCount = 0;
            foreach (GameObject item in itemsToLoot)
            {
                if (item == null || item.IsInvalid()) continue;
                if (!CanCarryWeight(The.Player, item)) continue;

                try
                {
                    // Use TakeObject event to pick up the item
                    The.Player.FireEvent(Event.New("CommandTakeObject", "Object", item));
                    lootedCount++;

                    if (lootedCount > 0 && lootedCount % 10 == 0)
                    {
                        // Safety: limit how many items per turn to prevent lag
                        break;
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[M32_QoL] AutoLoot failed to pick up {item?.DisplayNameStripped}: {ex.Message}");
                }
            }

            if (lootedCount > 0)
            {
                UnityEngine.Debug.Log($"[M32_QoL] AutoLoot: Picked up {lootedCount} items");
            }
        }

        private void HandleContainerContents(GameObject container, GameObject player, List<GameObject> itemsToLoot)
        {
            // Check if container is owned and should skip
            if (ShouldSkipOwned(container))
                return;

            Inventory containerInv = container.GetPart<Inventory>();
            if (containerInv == null) return;

            // Get contents
            List<GameObject> contents = containerInv.GetObjectsDirect()
                .Where(i => i != null && !i.IsInvalid())
                .ToList();

            foreach (GameObject item in contents)
            {
                if (!ShouldAutoLoot(item, player)) continue;

                // Check if already in our loot list
                if (itemsToLoot.Contains(item)) continue;

                // Take item out of container
                try
                {
                    containerInv.RemoveObject(item, null);
                    // Add to player's inventory directly
                    player.Inventory?.AddObject(item, null);
                    UnityEngine.Debug.Log($"[M32_QoL] AutoLoot: Took {item.DisplayNameStripped} from {container.DisplayNameStripped}");
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[M32_QoL] AutoLoot failed to take {item?.DisplayNameStripped} from container: {ex.Message}");
                }
            }

            // Optionally loot the empty container
            if (M32_Options.AutoLootEmptyContainers && containerInv.GetObjectCountDirect() == 0)
            {
                if (ShouldAutoLoot(container, player) && !itemsToLoot.Contains(container))
                {
                    itemsToLoot.Add(container);
                }
            }
        }

        private bool ShouldSkipOwned(GameObject item)
        {
            // If option disabled, don't skip
            if (!M32_Options.AutoLootSkipOwned)
                return false;

            // If item not owned, don't skip
            if (item == null || !item.IsOwned())
                return false;

            string owner = item.Owner;
            if (string.IsNullOrEmpty(owner))
                return false;

            try
            {
                // Check player's relationship with the faction
                XRL.World.Parts.Brain.AllegianceLevel level = The.Player.GetAllegianceLevel(owner);

                // If Member (friendly/allied), skip - don't steal from friends
                if (level == XRL.World.Parts.Brain.AllegianceLevel.Member)
                    return true;

                // If None/Unknown or Hostile, allow pickup
                // None = no relationship (allow per user requirement)
                // Hostile = enemy faction (allow to loot from enemies)
                return false;
            }
            catch
            {
                // If we can't determine relationship, allow (user requirement: allow if unknown)
                return false;
            }
        }

        private bool ShouldAutoLoot(GameObject item, GameObject player)
        {
            if (item == null || player == null) return false;
            if (item.IsPlayer()) return false;

            // Check if owned and should skip
            if (ShouldSkipOwned(item))
                return false;

            // Skip if not takeable
            if (!item.IsTakeable()) return false;

            // Skip harvestable objects (plants, trees, etc.)
            // These are world decorations meant to be walked over to harvest, not picked up
            if (item.GetPart("Harvestable") != null)
                return false;

            // Skip garbage/trash items
            // These are world decorations ("gettable terrain") that shouldn't be looted
            // Note: They have Takeable="true" but are marked with a Garbage part
            if (item.GetPart("Garbage") != null)
                return false;

            // Skip items marked as NoAutoget
            if (item.HasTagOrProperty("NoAutoget")) return false;

            // Skip liquid puddles/pools (MaxVolume < 0 means on ground, not in container)
            if (item.LiquidVolume != null && item.LiquidVolume.MaxVolume < 0)
                return false;

            // Check boulders/stones by blueprint name
            if (!M32_Options.AutoLootBouldersStones)
            {
                string bp = item.Blueprint;
                if (!string.IsNullOrEmpty(bp))
                {
                    // Match boulders: SmallBoulder, MediumBoulder, LargeBoulder (and variants like SmallBoulder Grey)
                    // Match stones: Small Stone, Large Stone
                    if (bp.StartsWith("SmallBoulder") || bp.StartsWith("MediumBoulder") || bp.StartsWith("LargeBoulder") ||
                        bp == "Small Stone" || bp == "Large Stone")
                        return false;
                }
            }

            // Check corpses
            if (!M32_Options.AutoLootCorpses)
            {
                if (item.GetInventoryCategory() == "Corpses")
                    return false;
            }

            // Check if dropped recently (using our custom M32_DroppedTurn property)
            // When the game sets DroppedByPlayer flag, we record the turn number in M32_DroppedTurn
            if (M32_Options.AutoLootDroppedCooldown >= -1 && item.HasIntProperty("DroppedByPlayer"))
            {
                // First time seeing this dropped item - record the turn when we discovered it
                // This is an approximation: we assume the item was dropped recently
                if (!item.HasIntProperty("M32_DroppedTurn"))
                {
                    item.SetIntProperty("M32_DroppedTurn", (int)(The.Game?.Turns ?? 0));
                }

                if (M32_Options.AutoLootDroppedCooldown == -1)
                    return false; // Never pickup dropped items

                try
                {
                    int dropTurn = item.GetIntProperty("M32_DroppedTurn");
                    long currentTurn = The.Game?.Turns ?? 0;
                    
                    if (currentTurn - dropTurn <= M32_Options.AutoLootDroppedCooldown)
                        return false; // Still within cooldown period
                }
                catch
                {
                    // If we can't read the property, allow the item
                }
            }

            // Check weight capacity (strict)
            if (M32_Options.AutoLootCheckWeight)
            {
                if (!CanCarryWeight(player, item))
                    return false;
            }

            // Check if in combat and combat looting is disabled
            if (!M32_Options.AutoLootInCombat && player.IsInCombat())
                return false;

            return true;
        }

        private bool CanCarryWeight(GameObject player, GameObject item)
        {
            if (player == null || item == null) return false;

            try
            {
                int currentWeight = player.GetCarriedWeight();
                int maxWeight = player.GetMaxCarriedWeight();
                int itemWeight = item.Weight;

                return currentWeight + itemWeight <= maxWeight;
            }
            catch
            {
                // If we can't check weight, allow the item
                return true;
            }
        }

    }
}