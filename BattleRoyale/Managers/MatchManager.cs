using System;
using BepInEx.Logging;
using UnityEngine;

namespace BattleRoyale
{
    public interface IMatchSystem
    {
        MatchState State { get; }
        void Start(System.Collections.Generic.IReadOnlyCollection<string> playerNames = null);
        void Tick(float dt);
        void End(string winnerName);
        void RecordKill(string killerName, string victimName, Vector3 position);
    }

    public class MatchManager : IMatchSystem
    {
        private static MatchManager _instance;
        public static MatchManager Instance => _instance;

        public MatchState State { get; private set; }
        private ManualLogSource _log;

        public static void Init(ManualLogSource log)
        {
            _instance = new MatchManager
            {
                _log = log,
                State = new MatchState { Phase = MatchPhase.Lobby }
            };
        }

        public void Start(System.Collections.Generic.IReadOnlyCollection<string> playerNames = null)
        {
            _log.LogInfo($"[MatchManager] Start() called, current Phase={State.Phase}");
            if (State.Phase == MatchPhase.Active)
            {
                _log.LogWarning("[MatchManager] Match already active");
                return;
            }

            State = new MatchState
            {
                MatchId = Guid.NewGuid().ToString("N"),
                Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue),
                Phase = MatchPhase.Active,
                StartTime = DateTime.UtcNow
            };

            if (playerNames != null)
            {
                foreach (var name in playerNames)
                    State.AlivePlayers.Add(name);
            }
            else
            {
                foreach (var player in Player.GetAllPlayers())
                    State.AlivePlayers.Add(player.GetPlayerName());
                // Dedicated server fallback: Player objects don't exist server-side
                if (State.AlivePlayers.Count == 0 && ZNet.instance != null)
                {
                    foreach (var peer in ZNet.instance.GetPeers())
                        if (!string.IsNullOrEmpty(peer.m_playerName))
                            State.AlivePlayers.Add(peer.m_playerName);
                }
            }

            State.InitialPlayerCount = State.AlivePlayers.Count;

            WipeAllInventories();
            SetAllPlayerSkills();
            TeleportPlayersToStart();

            BREventBus.Emit(new MatchStartedEvent
            {
                MatchId = State.MatchId,
                Seed = State.Seed,
                PlayerCount = State.AlivePlayers.Count,
                StartedAt = new DateTimeOffset(State.StartTime).ToUnixTimeSeconds()
            });

            _log.LogInfo($"[MatchManager] Match started: {State.MatchId}, seed: {State.Seed}, players ({State.AlivePlayers.Count}): [{string.Join(", ", State.AlivePlayers)}]");
        }

        public void Tick(float dt)
        {
            if (State.Phase != MatchPhase.Active) return;

            int count;
            lock (State.AlivePlayers)
                count = State.AlivePlayers.Count;

            if (count == 0)
                End("nobody");
            else if (count == 1 && State.InitialPlayerCount > 1)
                End(State.AlivePlayers[0]);
        }

        public void End(string winnerName)
        {
            if (State.Phase != MatchPhase.Active) return;

            State.Phase = MatchPhase.Ended;
            State.WinnerName = winnerName;

            float duration = (float)(DateTime.UtcNow - State.StartTime).TotalSeconds;

            Broadcast($"BATTLE ROYALE ENDED - Winner: {winnerName}");

            BREventBus.Emit(new MatchEndedEvent
            {
                MatchId = State.MatchId,
                WinnerName = winnerName,
                DurationSeconds = duration,
                EndedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });

            int surviving;
            System.Collections.Generic.List<string> survivors;
            lock (State.AlivePlayers)
            {
                surviving = State.AlivePlayers.Count;
                survivors = new System.Collections.Generic.List<string>(State.AlivePlayers);
            }
            _log.LogInfo($"[MatchManager] Match ended. Winner: {winnerName}, Duration: {duration:F0}s, initial: {State.InitialPlayerCount}, survivors: {surviving} [{string.Join(", ", survivors)}]");
        }

        public void RecordKill(string killerName, string victimName, Vector3 position)
        {
            if (State.Phase != MatchPhase.Active) return;

            lock (State.AlivePlayers)
            {
                State.AlivePlayers.Remove(victimName);
            }

            BREventBus.Emit(new PlayerKilledEvent
            {
                MatchId = State.MatchId,
                KillerName = killerName,
                VictimName = victimName,
                Position = position,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });

            int remaining;
            lock (State.AlivePlayers)
                remaining = State.AlivePlayers.Count;

            Broadcast($"{killerName} killed {victimName} — {remaining} players remaining");
            _log.LogInfo($"[MatchManager] Kill: {killerName} → {victimName}, alive: {remaining}");
        }

        private void SetAllPlayerSkills()
        {
            int level = Main.Instance?.StartSkillLevel ?? 0;
            if (level <= 0) return;

            foreach (var player in Player.GetAllPlayers())
            {
                ClientSync.ApplySkillLevel(player.GetSkills(), level);
            }
            _log.LogInfo($"[MatchManager] SetAllPlayerSkills: set local player(s) to {level}, broadcasting to clients");
            ClientSync.BroadcastSetSkills(level);
        }

        private void WipeAllInventories()
        {
            int count = 0;
            foreach (var player in Player.GetAllPlayers())
            {
                player.GetInventory().RemoveAll();
                count++;
            }
            _log.LogInfo($"[MatchManager] WipeAllInventories: wiped {count} local player(s), broadcasting RPC to clients");
            ClientSync.BroadcastWipeInventory();
        }

        private void TeleportPlayersToStart()
        {
            // Teleport local Player objects (listen server / solo host)
            foreach (var player in Player.GetAllPlayers())
            {
                Vector3 pos = FindMeadowsSpawn();
                player.TeleportTo(pos, player.transform.rotation, true);
                _log.LogInfo($"[MatchManager] Teleported (local) {player.GetPlayerName()} to {pos}");
            }
            // Send teleport RPCs for dedicated server clients
            if (ZNet.instance != null && ZNet.instance.IsDedicated())
            {
                foreach (var name in State.AlivePlayers)
                {
                    Vector3 pos = FindMeadowsSpawn();
                    ClientSync.SendTeleport(name, pos);
                    _log.LogInfo($"[MatchManager] Sent teleport RPC for {name} to {pos}");
                }
            }
        }

        private static Vector3 FindMeadowsSpawn()
        {
            const float minRadius = 3500f;
            const float maxRadius = 4500f;
            const int   maxAttempts = 100;

            var wg = WorldGenerator.instance;
            int biomeFail = 0, heightFail = 0;

            for (int i = 0; i < maxAttempts; i++)
            {
                float angle  = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float radius = UnityEngine.Random.Range(minRadius, maxRadius);
                float x      = Mathf.Cos(angle) * radius;
                float z      = Mathf.Sin(angle) * radius;

                if (wg.GetBiome(x, z) != Heightmap.Biome.Meadows) { biomeFail++; continue; }

                float y = wg.GetHeight(x, z);
                if (y <= 28f) { heightFail++; continue; } // skip ocean/shore (sea level ~30)

                var pos = new Vector3(x, y + 1.5f, z);
                _instance?._log.LogInfo($"[MatchManager] FindMeadowsSpawn: found {pos} after {i + 1} attempts (biomeFail={biomeFail}, heightFail={heightFail})");
                return pos;
            }

            // Fallback: random point in radius, no biome guarantee
            float fa = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float fr = UnityEngine.Random.Range(minRadius, maxRadius);
            float fy = wg?.GetHeight(Mathf.Cos(fa) * fr, Mathf.Sin(fa) * fr) ?? 32f;
            var fallback = new Vector3(Mathf.Cos(fa) * fr, fy + 1.5f, Mathf.Sin(fa) * fr);
            _instance?._log.LogWarning($"[MatchManager] FindMeadowsSpawn: ALL {maxAttempts} attempts failed (biomeFail={biomeFail}, heightFail={heightFail}) — using no-biome fallback {fallback}");
            return fallback;
        }

        private void Broadcast(string message)
        {
            _log.LogInfo($"[MatchManager] Broadcast: '{message}'");
            ClientSync.BroadcastSystemMessage(message);
        }
    }
}
