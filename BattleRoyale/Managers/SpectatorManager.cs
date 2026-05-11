using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace BattleRoyale
{
    public static class SpectatorManager
    {
        private static ManualLogSource _log;
        private static int _targetIndex;

        // Client-side free-fly camera state (written by SpectatorCameraPatch)
        public static float   FlyYaw;
        public static float   FlyPitch;
        public static Vector3 FlyPosition;

        public static void Init(ManualLogSource log) => _log = log;

        // Server-side: put player into spectator mode (when Player object is available)
        public static void EnterSpectatorMode(Player player)
        {
            if (player == null) return;
            Traverse.Create(player).Method("SetVisible", false).GetValue();
            EnterSpectatorModeByName(player.GetPlayerName());
        }

        // Server-side: put player into spectator mode by name (dedicated server path)
        public static void EnterSpectatorModeByName(string name)
        {
            _log?.LogInfo($"[SpectatorManager] EnterSpectatorMode: {name}");
            var state = MatchManager.Instance?.State;
            if (state != null && !state.SpectatorPlayers.Contains(name))
                state.SpectatorPlayers.Add(name);

            ClientSync.BroadcastEnterSpectator(name);
        }

        // Server-side: remove player from spectator mode by name
        public static void ExitSpectatorModeByName(string name)
        {
            _log?.LogInfo($"[SpectatorManager] ExitSpectatorMode: {name}");
            var state = MatchManager.Instance?.State;
            if (state != null)
                state.SpectatorPlayers.Remove(name);

            foreach (var p in Player.GetAllPlayers())
            {
                if (p.GetPlayerName() == name)
                {
                    Traverse.Create(p).Method("SetVisible", true).GetValue();
                    break;
                }
            }

            ClientSync.BroadcastExitSpectator(name);
        }

        // Client-side: cycle to next alive player and teleport camera to them
        public static void NextTarget()
        {
            var players = GetAlivePlayers();
            if (players.Count == 0) return;
            _targetIndex = (_targetIndex + 1) % players.Count;
            TeleportCameraToPlayer(players[_targetIndex]);
        }

        // Client-side: cycle to previous alive player
        public static void PrevTarget()
        {
            var players = GetAlivePlayers();
            if (players.Count == 0) return;
            _targetIndex = (_targetIndex - 1 + players.Count) % players.Count;
            TeleportCameraToPlayer(players[_targetIndex]);
        }

        // Returns name of currently watched player, or null if free-flying
        public static string CurrentTargetName()
        {
            var players = GetAlivePlayers();
            if (players.Count == 0) return null;
            if (_targetIndex >= players.Count) _targetIndex = 0;
            return players[_targetIndex].GetPlayerName();
        }

        private static List<Player> GetAlivePlayers()
        {
            var result = new List<Player>();
            foreach (var p in Player.GetAllPlayers())
            {
                if (!ClientSync.SpectatorList.Contains(p.GetPlayerName()))
                    result.Add(p);
            }
            return result;
        }

        private static void TeleportCameraToPlayer(Player target)
        {
            if (target == null) return;
            FlyPosition = target.transform.position
                          + target.transform.forward * -3f
                          + Vector3.up * 2f;
            FlyYaw   = target.transform.eulerAngles.y;
            FlyPitch = 15f;
            _log?.LogInfo($"[SpectatorManager] Camera → {target.GetPlayerName()} at {FlyPosition}");
        }
    }
}
