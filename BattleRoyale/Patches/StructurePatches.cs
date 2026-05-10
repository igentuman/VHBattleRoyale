using HarmonyLib;

namespace BattleRoyale.Patches
{
    [HarmonyPatch(typeof(WearNTear), "Damage")]
    public static class StructurePatches
    {
        [HarmonyPrefix]
        public static void Prefix(ref HitData hit)
        {
            if (MatchManager.Instance?.State?.Phase != MatchPhase.Active) return;

            float mult = Main.Instance.StructureDamageMultiplier;
            hit.m_damage.m_blunt     *= mult;
            hit.m_damage.m_slash     *= mult;
            hit.m_damage.m_pierce    *= mult;
            hit.m_damage.m_chop      *= mult;
            hit.m_damage.m_pickaxe   *= mult;
            hit.m_damage.m_fire      *= mult;
            hit.m_damage.m_frost     *= mult;
            hit.m_damage.m_lightning *= mult;
            hit.m_damage.m_poison    *= mult;
            hit.m_damage.m_spirit    *= mult;
        }
    }
}
