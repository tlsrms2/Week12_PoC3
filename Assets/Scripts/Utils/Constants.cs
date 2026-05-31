namespace FactoryDelivery.Utils
{
    /// <summary>
    /// Central repository for all game-wide constants.
    /// Organized by system domain for easy discoverability and maintenance.
    /// </summary>
    public static class Constants
    {
        // ─────────────────────────────────────────────
        //  Grid
        // ─────────────────────────────────────────────

        /// <summary>Width of the game grid in cells.</summary>
        public static int GridWidth = 20;

        /// <summary>Height of the game grid in cells.</summary>
        public static int GridHeight = 20;

        /// <summary>World-space size of a single grid cell.</summary>
        public static float CellSize = 1f;

        // ─────────────────────────────────────────────
        //  Defaults
        // ─────────────────────────────────────────────

        /// <summary>Default cooldown duration (in seconds) for resource production tiles.</summary>
        public static float DefaultResourceCooldown = 5f;

        /// <summary>Maximum upgrade level a tile can reach.</summary>
        public static int MaxTileLevel = 5;

        // ─────────────────────────────────────────────
        //  Block / Day Structure
        // ─────────────────────────────────────────────

        /// <summary>Number of placement blocks available per in-game day.</summary>
        public static int BlocksPerDay = 4;

        /// <summary>Maximum number of slices a player can use per day.</summary>
        public static int MaxSlicesPerDay = 1;

        // ─────────────────────────────────────────────
        //  Economy
        // ─────────────────────────────────────────────

        /// <summary>Base cost for a shop reroll.</summary>
        public static int RerollBaseCost = 10;

        /// <summary>Multiplier applied to the reroll cost after each consecutive reroll.</summary>
        public static float RerollCostMultiplier = 2f;

        // ─────────────────────────────────────────────
        //  Logistics / Workers
        // ─────────────────────────────────────────────

        /// <summary>Base movement speed of worker units (world units per second).</summary>
        public static float WorkerBaseSpeed = 3f;

        /// <summary>Minimum distance workers maintain from each other to avoid overlap.</summary>
        public static float WorkerMinDistance = 0.5f;

        /// <summary>Maximum carrying capacity of worker units.</summary>
        public static int WorkerCapacity = 3;
    }
}
