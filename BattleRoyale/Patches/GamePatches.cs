using HarmonyLib;

namespace BattleRoyale.Patches
{
    [HarmonyPatch(typeof(Game), "Start")]
    public static class GamePatches
    {
        [HarmonyPostfix]
        public static void Start_Postfix()
        {
            Main.Log?.LogInfo("[GamePatches] Game.Start postfix fired");

            if (ZNet.instance == null)
            {
                Main.Log?.LogWarning("[GamePatches] ZNet.instance is null - skipping init");
                return;
            }

            bool isServer = ZNet.instance.IsServer();
            Main.Log?.LogInfo($"[GamePatches] IsServer={isServer}, IsDedicated={ZNet.instance.IsDedicated()}");

            if (!isServer)
            {
                Main.Log?.LogInfo("[GamePatches] Calling OnClientReady");
                Main.Instance?.OnClientReady();
            }

            if (isServer)
            {
                Main.Log?.LogInfo("[GamePatches] Calling OnServerReady");
                Main.Instance?.OnServerReady();
            }
        }
    }

    // ZRoutedRpc is created in ZNet.Start — register handlers here, not in Game.Start
    [HarmonyPatch(typeof(ZNet), "Start")]
    public static class ZNetPatches
    {
        [HarmonyPostfix]
        public static void Start_Postfix()
        {
            Main.Log?.LogInfo($"[ZNetPatches] ZNet.Start postfix — ZRoutedRpc.instance={(ZRoutedRpc.instance == null ? "NULL" : "OK")}");
            ClientSync.RegisterRpcs();
        }
    }
}
