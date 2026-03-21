using HarmonyLib;
using PlayerStats.Managers;
using UnityEngine;

namespace PlayerStats.Patches
{
    [HarmonyPatch]
    public static class MovementPatches
    {
        private static Vector3 _lastPos = Vector3.zero;
        private static bool _firstUpdate = true;

        [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStat))]
        [HarmonyPostfix]
        public static void IncrementStatPostfix(PlayerStatType stat, float amount)
        {
            var stats = StatsManager.CurrentStats;
            switch (stat)
            {
                case PlayerStatType.DistanceRun:
                    stats.runDistance += amount;
                    break;
                case PlayerStatType.DistanceWalk:
                    stats.walkDistance += amount;
                    break;
                case PlayerStatType.DistanceAir:
                    stats.airDistance += amount;
                    break;
                case PlayerStatType.DistanceSail:
                    stats.sailDistance += amount;
                    break;
                case PlayerStatType.DistanceTraveled:
                    stats.traveledDistance += amount;
                    break;
                case PlayerStatType.TimeInBase:
                    stats.timeInBase += amount;
                    break;
                case PlayerStatType.TimeOutOfBase:
                    stats.timeOutOfBase += amount;
                    break;
            }
        }

        [HarmonyPatch(typeof(Player), "Update")]
        [HarmonyPostfix]
        public static void UpdatePostfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;

            Vector3 currentPos = __instance.transform.position;

            if (_firstUpdate)
            {
                _lastPos = currentPos;
                _firstUpdate = false;
                return;
            }

            if (__instance.IsSwimming())
            {
                float dist = Vector3.Distance(new Vector3(currentPos.x, 0, currentPos.z), new Vector3(_lastPos.x, 0, _lastPos.z));
                if (dist > 0 && dist < 10f) // sanity check for teleporting
                {
                    StatsManager.CurrentStats.swimDistance += dist;
                }
            }
            
            _lastPos = currentPos;
        }

        [HarmonyPatch(typeof(Character), nameof(Character.Jump))]
        [HarmonyPostfix]
        public static void JumpPostfix(Character __instance)
        {
            if (__instance == Player.m_localPlayer)
            {
                StatsManager.CurrentStats.jumps++;
            }
        }
    }
}
