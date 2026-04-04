using HarmonyLib;
using XRL;

namespace M32_QoL
{
    /// <summary>
    /// Initializes Harmony patches for the mod.
    /// </summary>
    [HasModSensitiveStaticCache]
    public static class M32_HarmonyInitializer
    {
        static M32_HarmonyInitializer()
        {
            try
            {
                var harmony = new Harmony("M32_QoL");
                harmony.PatchAll();
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[M32_QoL] Failed to apply Harmony patches: {ex}");
            }
        }

        public static void Init()
        {
            // Static constructor handles initialization
        }
    }
}