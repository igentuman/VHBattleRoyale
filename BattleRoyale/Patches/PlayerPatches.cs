using HarmonyLib;
using UnityEngine;

namespace BattleRoyale.Patches
{
    [HarmonyPatch(typeof(Player))]
    public static class PlayerPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("OnDeath")]
        public static void OnDeath_Postfix(Player __instance)
        {
            Main.Log?.LogInfo($"[PlayerPatches] OnDeath: player='{__instance.GetPlayerName()}', pos={__instance.transform.position}, hp={__instance.GetHealth():F1}, IsServer={Main.IsServer}, Phase={MatchManager.Instance?.State?.Phase}");

            // Client-side: immediately reflect the death in the HUD before async RPC arrives.
            if (__instance == Player.m_localPlayer && ClientSync.Phase == MatchPhase.Active)
                ClientSync.OnLocalPlayerDied();

            if (!Main.IsServer) return;
            if (MatchManager.Instance?.State?.Phase != MatchPhase.Active) return;

            string victimName = __instance.GetPlayerName();
            string killerName = "zone";

            // m_lastHit is protected - access via Traverse
            var lastHit = Traverse.Create(__instance).Field<HitData>("m_lastHit").Value;
            var attacker = lastHit?.GetAttacker();
            if (attacker != null)
                killerName = attacker.GetHoverName();

            Main.Log?.LogInfo($"[PlayerPatches] Recording kill: killer='{killerName}', victim='{victimName}'");
            MatchManager.Instance.RecordKill(killerName, victimName, __instance.transform.position);

            int aliveRemaining = MatchManager.Instance.State.AlivePlayers.Count;

            // Don't enter spectator when no alive players remain — match is ending anyway,
            // and entering then immediately exiting spectator blocks RequestRespawn causing black screen.
            if (aliveRemaining > 0)
                SpectatorManager.EnterSpectatorMode(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnSpawned")]
        public static void OnSpawned_Postfix(Player __instance)
        {
            Main.Log?.LogInfo($"[PlayerPatches] OnSpawned: player='{__instance.GetPlayerName()}', IsServer={Main.IsServer}, Phase={MatchManager.Instance?.State?.Phase}");
            if (!Main.IsServer) return;

            if (MatchManager.Instance?.State?.Phase == MatchPhase.Active)
            {
                if (MatchManager.Instance.State.IsSpectator(__instance.GetPlayerName()))
                {
                    Main.Log?.LogInfo($"[PlayerPatches] Match active — skipping inventory wipe for spectator '{__instance.GetPlayerName()}'");
                    return;
                }
                Main.Log?.LogInfo($"[PlayerPatches] Match active — wiping inventory for '{__instance.GetPlayerName()}'");
                __instance.UnequipAllItems();
                __instance.GetInventory().RemoveAll();
                return;
            }

            int total = Player.GetAllPlayers().Count;
            int votes = VoteTracker.VoteCount;
            Main.Log?.LogInfo($"[PlayerPatches] Lobby spawn broadcast: votes={votes}, total={total}");
            VoteTracker.BroadcastSystem(
                $"Battle Royale mod active! Type /brstart in chat to vote. [{votes}/{total} ready]");
        }

        [HarmonyPrefix]
        [HarmonyPatch("UseStamina")]
        public static void UseStamina_Prefix(Player __instance, ref float v)
        {
            if (MatchManager.Instance?.State?.Phase != MatchPhase.Active) return;
            // m_placementGhost is private — access via Traverse
            var ghost = Traverse.Create(__instance).Field<GameObject>("m_placementGhost").Value;
            if (ghost != null)
                v *= Main.Instance.StaminaCostMultiplier;
        }
    }

    [HarmonyPatch(typeof(Game), "RequestRespawn")]
    public static class RespawnPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(float delay, bool afterDeath)
        {
            if (afterDeath && (ClientSync.Phase == MatchPhase.Active || ClientSync.IsSpectator))
            {
                Main.Log?.LogInfo($"[RespawnPatch] Respawn blocked — spectator mode (delay={delay})");
                return false;
            }
            return true;
        }
    }
}
