using HarmonyLib;

namespace PlayerStats.Patches
{
    [HarmonyPatch(typeof(Terminal), "InitTerminal")]
    public static class CommandPatches
    {
        [HarmonyPostfix]
        public static void InitTerminalPostfix()
        {
            new Terminal.ConsoleCommand("revealstats", "Reveal hidden statistics in PlayerStats overlay.", (Terminal.ConsoleEventArgs args) =>
            {
                Main.Instance.RevealHiddenStats.Value = true;
                args.Context.AddString("Hidden statistics revealed.");
            });

            new Terminal.ConsoleCommand("summarystats", "Show the full PlayerStats summary screen.", (Terminal.ConsoleEventArgs args) =>
            {
                if (UI.StatsSummary.Instance != null)
                {
                    UI.StatsSummary.Instance.Toggle();
                    args.Context.AddString("Summary screen toggled.");
                }
            });
        }
    }
}
