using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale
{
    public class MobDropEntry
    {
        public string m_itemName;
        public float  m_dropPercent;
        public int    m_dropAmountMin;
        public int    m_dropAmountMax;
    }


    public class LootSpawnPoint
    {
        public Vector3 Position;
        public string[] PossiblePrefabs;
        public float Weight;
    }

    public static class DefaultLootTable
    {
        // Positions are offsets from world origin - tune per map
        public static readonly List<LootSpawnPoint> SpawnPoints = new List<LootSpawnPoint>
        {
            new LootSpawnPoint { Position = new Vector3(  50,  2,   50), Weight = 1f, PossiblePrefabs = new[] { "SwordBronze", "AxeBronze" } },
            new LootSpawnPoint { Position = new Vector3( -50,  2,   50), Weight = 1f, PossiblePrefabs = new[] { "ShieldWood", "ShieldBronzeBuckler" } },
            new LootSpawnPoint { Position = new Vector3(  50,  2,  -50), Weight = 1f, PossiblePrefabs = new[] { "Bow", "ArrowFlint" } },
            new LootSpawnPoint { Position = new Vector3( -50,  2,  -50), Weight = 1f, PossiblePrefabs = new[] { "ArmorBronzeChest", "HelmetBronze" } },
            new LootSpawnPoint { Position = new Vector3( 120,  2,   30), Weight = 1f, PossiblePrefabs = new[] { "ArmorBronzeLegs", "ArmorLeatherLegs" } },
            new LootSpawnPoint { Position = new Vector3( -120, 2,   30), Weight = 1f, PossiblePrefabs = new[] { "Knife_Flint", "Club" } },
            new LootSpawnPoint { Position = new Vector3(  30,  2,  120), Weight = 1f, PossiblePrefabs = new[] { "ArrowBronze", "ArrowFlint" } },
            new LootSpawnPoint { Position = new Vector3( -30,  2,  120), Weight = 1f, PossiblePrefabs = new[] { "Spear_Flint", "Hammer" } },
            new LootSpawnPoint { Position = new Vector3(  80,  2,  -90), Weight = 0.5f, PossiblePrefabs = new[] { "SwordIron", "MaceBronze" } },
            new LootSpawnPoint { Position = new Vector3( -80,  2,  -90), Weight = 0.5f, PossiblePrefabs = new[] { "HelmetIron", "ArmorIronChest" } },
            new LootSpawnPoint { Position = new Vector3( 200,  2,    0), Weight = 1f, PossiblePrefabs = new[] { "Mushroom", "RawMeat" } },
            new LootSpawnPoint { Position = new Vector3(-200,  2,    0), Weight = 1f, PossiblePrefabs = new[] { "Mushroom", "Raspberry" } },
            new LootSpawnPoint { Position = new Vector3(   0,  2,  200), Weight = 1f, PossiblePrefabs = new[] { "Carrot", "RawMeat" } },
            new LootSpawnPoint { Position = new Vector3(   0,  2, -200), Weight = 1f, PossiblePrefabs = new[] { "Honey", "Blueberries" } },
            new LootSpawnPoint { Position = new Vector3( 150,  2,  150), Weight = 0.5f, PossiblePrefabs = new[] { "AtgeirBronze", "BattleaxeCrystal" } },
        };
    }
}
