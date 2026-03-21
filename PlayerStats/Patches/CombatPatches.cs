using System.Collections.Generic;
using HarmonyLib;
using PlayerStats.Data;
using PlayerStats.Managers;
using UnityEngine;

namespace PlayerStats.Patches
{
    [HarmonyPatch]
    public static class CombatPatches
    {
        private static readonly AccessTools.FieldRef<Humanoid, bool> PerfectBlockField =
            AccessTools.FieldRefAccess<Humanoid, bool>("m_perfectBlock");

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

        [HarmonyPatch(typeof(Character), "OnDeath")]
        [HarmonyPostfix]
        public static void OnDeathPostfix(Character __instance)
        {
            if (__instance == Player.m_localPlayer)
            {
                StatsManager.CurrentStats.deaths++;
            }
            else if (__instance.IsMonsterFaction(0f) || __instance.m_faction == Character.Faction.Undead)
            {
                StatsManager.CurrentStats.kills++;

                // Track 2-star mobs
                if (__instance.GetLevel() == 3)
                {
                    string name = Localize(__instance.m_name);
                    StatEntry entry = StatsManager.CurrentStats.killed2StarMobs.Find(e => e.name == name);
                    if (entry == null)
                    {
                        StatsManager.CurrentStats.killed2StarMobs.Add(new StatEntry(name));
                    }
                    else
                    {
                        entry.count++;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
        [HarmonyPostfix]
        public static void OnDamagePostfix(Character __instance, HitData hit)
        {
            if (hit.GetAttacker() == Player.m_localPlayer)
            {
                StatsManager.CurrentStats.totalDamage += hit.GetTotalDamage();
            }

            if (__instance == Player.m_localPlayer && hit.m_hitType == HitData.HitType.Drowning)
            {
                StatsManager.CurrentStats.totalDrowningDamage += hit.GetTotalDamage();
            }
        }

        [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
        [HarmonyPostfix]
        public static void BlockAttackPostfix(Humanoid __instance, bool __result, Character attacker)
        {
            if (__instance == Player.m_localPlayer && __result)
            {
                StatsManager.CurrentStats.blocks++;
                
                // Perfect blocks
                if (PerfectBlockField(__instance))
                {
                    StatsManager.CurrentStats.perfectBlocks++;
                }
            }
        }

        [HarmonyPatch(typeof(Player), "Dodge")]
        [HarmonyPostfix]
        public static void DodgePostfix(Player __instance)
        {
            if (__instance == Player.m_localPlayer)
            {
                StatsManager.CurrentStats.dodges++;
            }
        }
    }
}
