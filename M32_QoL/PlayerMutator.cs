using XRL;
using XRL.World;

namespace M32_QoL
{
    /// <summary>
    /// Mutator that adds mod parts to newly created players.
    /// </summary>
    [PlayerMutator]
    public class M32_PlayerMutator : IPlayerMutator
    {
        public void mutate(GameObject player)
        {
            M32_HarmonyInitializer.Init();
            player.RequirePart<M32_ZoneTradePart>();
            player.RequirePart<M32_AutoLootPart>();
            player.RequirePart<M32_ConsolidateLiquidsPart>();
            player.RequirePart<M32_UnloadAllPart>();
        }
    }
}