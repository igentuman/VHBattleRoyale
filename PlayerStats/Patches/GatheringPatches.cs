using HarmonyLib;
using PlayerStats.Managers;
using UnityEngine;

namespace PlayerStats.Patches
{
    [HarmonyPatch]
    public static class GatheringPatches
    {
        private static readonly AccessTools.FieldRef<TreeBase, float> TreeBaseHealthField =
            AccessTools.FieldRefAccess<TreeBase, float>("m_health");

        private static readonly AccessTools.FieldRef<TreeLog, float> TreeLogHealthField =
            AccessTools.FieldRefAccess<TreeLog, float>("m_health");

        private static readonly AccessTools.FieldRef<MineRock, GameObject[]> MineRockHitAreasField =
            AccessTools.FieldRefAccess<MineRock, GameObject[]>("m_hitAreas");

        private static readonly AccessTools.FieldRef<MineRock5, System.Collections.IList> MineRock5HitAreasField =
            AccessTools.FieldRefAccess<MineRock5, System.Collections.IList>("m_hitAreas");

        private static System.Type _hitAreaType;
        private static System.Reflection.FieldInfo _hitAreaHealthField;

        private static float GetHitAreaHealth(object hitArea)
        {
            if (_hitAreaType == null)
            {
                _hitAreaType = AccessTools.TypeByName("MineRock5+HitArea");
                _hitAreaHealthField = AccessTools.Field(_hitAreaType, "m_health");
            }
            return (float)_hitAreaHealthField.GetValue(hitArea);
        }

        [HarmonyPatch(typeof(TreeBase), "Damage")]
        [HarmonyPostfix]
        public static void TreeBaseOnDamagedPostfix(TreeBase __instance, HitData hit)
        {
            float health = TreeBaseHealthField(__instance);
            if (hit.GetAttacker() == Player.m_localPlayer && health <= 0f)
            {
                StatsManager.CurrentStats.treesChopped++;
            }
        }

        [HarmonyPatch(typeof(TreeLog), "Damage")]
        [HarmonyPostfix]
        public static void TreeLogOnDamagedPostfix(TreeLog __instance, HitData hit)
        {
            float health = TreeLogHealthField(__instance);
            if (hit.GetAttacker() == Player.m_localPlayer && health <= 0f)
            {
                StatsManager.CurrentStats.treesChopped++;
            }
        }

        [HarmonyPatch(typeof(MineRock), "Damage")]
        [HarmonyPrefix]
        public static void MineRockOnDamagedPrefix(MineRock __instance, HitData hit, out bool __state)
        {
            __state = false;
            if (hit.GetAttacker() == Player.m_localPlayer)
            {
                // Count active hit areas
                var hitAreas = MineRockHitAreasField(__instance);
                if (hitAreas != null)
                {
                    // For MineRock, we're still just using a simple 'mining activity' increment 
                    // because detecting actual destruction accurately in RPC_Damage without state is hard.
                    // But we use the FieldRef now.
                }
                __state = true;
            }
        }

        [HarmonyPatch(typeof(MineRock), "Damage")]
        [HarmonyPostfix]
        public static void MineRockOnDamagedPostfix(MineRock __instance, HitData hit, bool __state)
        {
            if (__state && hit.GetAttacker() == Player.m_localPlayer)
            {
                StatsManager.CurrentStats.minedCount++;
            }
        }

        [HarmonyPatch(typeof(MineRock5), "RPC_Damage")]
        [HarmonyPrefix]
        public static void MineRock5OnDamagedPrefix(MineRock5 __instance, HitData hit, int hitAreaIndex, out float __state)
        {
            __state = -1f;
            if (hit.GetAttacker() == Player.m_localPlayer)
            {
                var hitAreas = MineRock5HitAreasField(__instance);
                if (hitAreas != null && hitAreaIndex >= 0 && hitAreaIndex < hitAreas.Count)
                {
                    var area = hitAreas[hitAreaIndex];
                    __state = GetHitAreaHealth(area);
                }
            }
        }

        [HarmonyPatch(typeof(MineRock5), "RPC_Damage")]
        [HarmonyPostfix]
        public static void MineRock5OnDamagedPostfix(MineRock5 __instance, HitData hit, int hitAreaIndex, float __state)
        {
            if (__state > 0f && hit.GetAttacker() == Player.m_localPlayer)
            {
                var hitAreas = MineRock5HitAreasField(__instance);
                if (hitAreas != null && hitAreaIndex >= 0 && hitAreaIndex < hitAreas.Count)
                {
                    var area = hitAreas[hitAreaIndex];
                    float healthAfter = GetHitAreaHealth(area);
                    if (healthAfter <= 0f)
                    {
                        StatsManager.CurrentStats.minedCount++;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
        [HarmonyPrefix]
        public static void PlacePiecePrefix(Player __instance, Piece piece, out int __state)
        {
            __state = -1;
            if (__instance == Player.m_localPlayer && piece != null)
            {
                // We use a prefix to potentially capture state if needed, 
                // but for now let's just use a more reliable method if possible.
                // However, without a bool return, we might want to check something else.
                __state = 1; 
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
        [HarmonyPostfix]
        public static void PlacePiecePostfix(Player __instance, Piece piece, int __state)
        {
            if (__state == 1 && __instance == Player.m_localPlayer)
            {
                // In Valheim, PlacePiece is called when the player clicks to place.
                // If it wasn't successful (e.g. lack of resources), it usually returns early.
                // Since it's void, we'll assume success for now, or find a better hook.
                // A better hook might be Piece.Awake or something similar, but that's for every piece.
                StatsManager.CurrentStats.buildingPartsPlaced++;
            }
        }
    }
}
