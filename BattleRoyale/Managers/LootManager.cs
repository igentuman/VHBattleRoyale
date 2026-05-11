using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace BattleRoyale
{
    public interface ILootGenerator
    {
        void Generate(int matchSeed, List<LootSpawnPoint> spawnPoints);
        void DespawnAll();
    }

    public class LootManager : ILootGenerator
    {
        private static LootManager _instance;
        public static LootManager Instance => _instance;

        private ManualLogSource _log;
        private int _spawnCount;

        public static void Init(int spawnCount, ManualLogSource log)
        {
            _instance = new LootManager { _log = log, _spawnCount = spawnCount };
            BREventBus.Subscribe<MatchStartedEvent>(e => _instance.Generate(e.Seed, DefaultLootTable.SpawnPoints));
            BREventBus.Subscribe<MatchEndedEvent>(_ => _instance.DespawnAll());
        }

        public void Generate(int matchSeed, List<LootSpawnPoint> spawnPoints)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;

            var rng = new System.Random(matchSeed);
            var pool = new List<LootSpawnPoint>(spawnPoints);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            int count = System.Math.Min(_spawnCount, pool.Count);
            int spawned = 0;

            for (int i = 0; i < count; i++)
            {
                var point = pool[i];
                string prefabName = point.PossiblePrefabs[rng.Next(point.PossiblePrefabs.Length)];
                var prefab = ZNetScene.instance.GetPrefab(prefabName);
                if (prefab == null)
                {
                    _log.LogWarning($"[LootManager] Unknown prefab '{prefabName}' at {point.Position} - skipping");
                    continue;
                }

                ZNetScene.instance.SpawnObject(point.Position, Quaternion.identity, prefab);
                _log.LogInfo($"[LootManager] Spawned '{prefabName}' at {point.Position}");
                spawned++;

                BREventBus.Emit(new LootSpawnedEvent
                {
                    MatchId = MatchManager.Instance?.State?.MatchId ?? "",
                    Position = point.Position,
                    ItemName = prefabName
                });
            }

            _log.LogInfo($"[LootManager] Generate complete: {spawned}/{count} spawned (pool: {pool.Count} pts, requested: {_spawnCount}, seed: {matchSeed})");
        }

        public void DespawnAll()
        {
            // MVP: loot persists until picked up — despawn tracking requires ZDO tagging (Phase 2)
            _log.LogInfo("[LootManager] DespawnAll: not tracked in MVP, loot stays until collected");
        }

    }
}
