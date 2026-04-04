using XRL;

namespace M32_QoL
{
    /// <summary>
    /// Shared options class for all M32_QoL features.
    /// Uses HasOptionFlagUpdate to automatically sync with Options.xml
    /// </summary>
    [HasOptionFlagUpdate(Prefix = "Option_M32QoL_")]
    public static class M32_Options
    {
        [OptionFlag]
        public static bool EnableZoneTrade;

        [OptionFlag]
        public static bool EnableStackedInventory;

        [OptionFlag]
        public static bool EnableAutoLoot;

        [OptionFlag]
        public static bool AutoLootInCombat;

        [OptionFlag]
        public static bool AutoLootCheckWeight;

        [OptionFlag]
        public static bool AutoLootEmptyContainers;

        [OptionFlag]
        public static bool AutoLootCorpses;

        [OptionFlag]
        public static bool AutoLootBouldersStones;

        [OptionFlag]
        public static int AutoLootDroppedCooldown;

        [OptionFlag]
        public static bool AutoLootSkipOwned;
    }
}
