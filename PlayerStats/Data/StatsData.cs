using System;
using System.Collections.Generic;

namespace PlayerStats.Data
{
    [Serializable]
    public class StatsData
    {
        // Combat
        public int kills;
        public int deaths;
        public int dodges;
        public int blocks;
        public int perfectBlocks;
        public float totalDamage;
        public float totalDrowningDamage;

        // Movement
        public float runDistance;
        public float walkDistance;
        public float airDistance;
        public float sailDistance;
        public float traveledDistance;
        public float swimDistance;
        public int jumps;

        // Time
        public float timeInBase;
        public float timeOutOfBase;

        // Gathering
        public int treesChopped;
        public int minedCount;

        // Misc
        public int buildingPartsPlaced;
        public int fishesCaught;

        // Complex Data (2-star mobs)
        public List<StatEntry> killed2StarMobs = new List<StatEntry>();
        public List<StatEntry> spawned2StarAroundPlayer = new List<StatEntry>();
    }
}
