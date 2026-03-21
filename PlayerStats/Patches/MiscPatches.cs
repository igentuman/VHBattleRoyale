using System.Collections.Generic;
using HarmonyLib;
using PlayerStats.Data;
using PlayerStats.Managers;
using UnityEngine;

namespace PlayerStats.Patches
{
    [HarmonyPatch]
    public static class MiscPatches
    {
        private static HashSet<ZDOID> _spawnedMobs = new HashSet<ZDOID>();

        private static object _localizationInstance;
        private static System.Reflection.MethodInfo _localizeMethod;

        private static string Localize(string text)
        {
            if (_localizationInstance == null)
            {
                var type = AccessTools.TypeByName("Localization");
                _localizationInstance = AccessTools.Property(type, "instance").GetValue(null);
                _localizeMethod = AccessTools.Method(type, "Localize", new[] { typeof(string) });
            }
            return (string)_localizeMethod.Invoke(_localizationInstance, new object[] { text });
        }

        [HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.Catch))]
        [HarmonyPostfix]
        public static void CatchPostfix(FishingFloat __instance)
        {
            StatsManager.CurrentStats.fishesCaught++;
        }

        [HarmonyPatch(typeof(Character), "Awake")]
        [HarmonyPostfix]
        public static void CharacterAwakePostfix(Character __instance)
        {
            if (__instance.GetLevel() == 3 && Player.m_localPlayer != null)
            {
                float distance = Vector3.Distance(__instance.transform.position, Player.m_localPlayer.transform.position);
                if (distance <= 50f && !_spawnedMobs.Contains(__instance.GetZDOID()))
                {
                    _spawnedMobs.Add(__instance.GetZDOID());
                    string name = Localize(__instance.m_name);
                    
                    StatEntry entry = StatsManager.CurrentStats.spawned2StarAroundPlayer.Find(e => e.name == name);
                    if (entry == null)
                    {
                        StatsManager.CurrentStats.spawned2StarAroundPlayer.Add(new StatEntry(name));
                    }
                    else
                    {
                        entry.count++;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
        [HarmonyPrefix]
        public static void GameLogoutPrefix()
        {
            StatsManager.SaveStats();
            StatsManager.ClearStats();
            _spawnedMobs.Clear();
        }

        [HarmonyPatch(typeof(ZNet), "Save")]
        [HarmonyPostfix]
        public static void ZNetSavePostfix()
        {
            StatsManager.SaveStats();
        }
    }
}
