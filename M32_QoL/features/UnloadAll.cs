using System;
using System.Collections.Generic;

using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace M32_QoL
{
    [Serializable]
    public class M32_UnloadAllPart : IScribedPart
    {
        public override bool AllowStaticRegistration() => true;

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == CommandEvent.ID;
        }

        public override bool HandleEvent(CommandEvent E)
        {
            if (E.Command != "M32QoL_UnloadAll")
                return base.HandleEvent(E);

            if (!M32_Options.EnableUnloadAll)
                return false;

            UnloadAll();
            return false;
        }

        private static void UnloadAll()
        {
            GameObject player = The.Player;
            if (player == null)
                return;

            Inventory inv = player.GetPart<Inventory>();
            if (inv == null)
                return;

            int ammoUnloaded = 0;
            int cellsEjected = 0;

            MessageQueue.AddPlayerMessage("Unloading ammo and ejecting cells...");

            List<GameObject> items = new List<GameObject>(inv.GetObjectsDirect());

            for (int i = 0; i < items.Count; i++)
            {
                GameObject item = items[i];
                if (item == null || item.IsInvalid())
                    continue;

                if (item.Equipped != null)
                    continue;

                MagazineAmmoLoader magLoader = item.GetPart<MagazineAmmoLoader>();
                if (magLoader != null && GameObject.Validate(ref magLoader.Ammo))
                {
                    GameObject ammo = magLoader.Ammo;
                    magLoader.SetAmmo(null);
                    player.ReceiveObject(ammo);
                    ammo.CheckStack();
                    ammoUnloaded++;
                }

                EnergyCellSocket cellSocket = item.GetPart<EnergyCellSocket>();
                if (cellSocket != null && GameObject.Validate(ref cellSocket.Cell))
                {
                    GameObject cell = cellSocket.Cell;
                    cellSocket.SetCell(null);
                    player.ReceiveObject(cell);
                    cell.CheckStack();
                    cellsEjected++;
                }
            }

            if (ammoUnloaded > 0 || cellsEjected > 0)
            {
                MessageQueue.AddPlayerMessage("Unloaded all ammo and removed all cells.");
            }
            else
            {
                MessageQueue.AddPlayerMessage("There is nothing to unload or remove.");
            }
        }
    }
}
