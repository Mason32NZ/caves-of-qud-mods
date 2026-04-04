using XRL;
using XRL.World;

namespace M32_QoL
{
    /// <summary>
    /// Handler that adds mod parts when loading an existing save.
    /// </summary>
    [HasCallAfterGameLoaded]
    public class M32_LoadGameHandler
    {
        [CallAfterGameLoaded]
        public static void AfterLoaded()
        {
            M32_HarmonyInitializer.Init();
            if (The.Player == null)
            {
                UnityEngine.Debug.LogError("[M32_QoL] LoadGameHandler: Player is null!");
                return;
            }
            The.Player.RequirePart<M32_ZoneTradePart>();
            The.Player.RequirePart<M32_AutoLootPart>();
            The.Player.RequirePart<M32_ConsolidateLiquidsPart>();
        }
    }
}