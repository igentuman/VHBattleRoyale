using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BattleRoyale.Patches
{
    public static class ChatCommands
    {
        public static void Register()
        {
            new Terminal.ConsoleCommand("brstart", "Force start a Battle Royale match (admin bypass)",
                delegate(Terminal.ConsoleEventArgs args)
                {
                    Main.Log?.LogInfo("[ChatCommands] brstart console command invoked");
                    if (MatchManager.Instance == null)
                    {
                        args.Context?.AddString("BR: MatchManager not initialized.");
                        Main.Log?.LogWarning("[ChatCommands] brstart: MatchManager.Instance is null");
                        return;
                    }
                    if (MatchManager.Instance.State?.Phase == MatchPhase.Active)
                    {
                        args.Context?.AddString("BR: match already active.");
                        return;
                    }
                    VoteTracker.BroadcastSystem("Admin is starting Battle Royale!");
                    MatchManager.Instance.Start();
                    args.Context?.AddString("Battle Royale: match started.");
                }, isCheat: true);

            new Terminal.ConsoleCommand("brstop", "Force stop the current Battle Royale match",
                delegate(Terminal.ConsoleEventArgs args)
                {
                    Main.Log?.LogInfo("[ChatCommands] brstop console command invoked");
                    MatchManager.Instance?.End("admin");
                    args.Context?.AddString("Battle Royale: match stopped by admin.");
                }, isCheat: true);

            new Terminal.ConsoleCommand("brstatus", "Show current Battle Royale match status",
                delegate(Terminal.ConsoleEventArgs args)
                {
                    var state = MatchManager.Instance?.State;
                    if (state == null) { args.Context?.AddString("BR: no match manager initialized."); return; }
                    var players = Player.GetAllPlayers();
                    args.Context?.AddString(
                        $"BR: phase={state.Phase}  alive={state.AlivePlayers.Count}  match={state.MatchId ?? "none"}  Player.GetAllPlayers()={players.Count}");
                    Main.Log?.LogInfo($"[ChatCommands] brstatus: phase={state.Phase}, alive={state.AlivePlayers.Count}, Player.GetAllPlayers()={players.Count}");
                });
        }
    }

    // Intercept chat on the server to process /brstart votes.
    // Patch Talker.RPC_Say (not Chat.OnNewChatMessage) because on a dedicated server
    // Player.m_localPlayer is null and RPC_Say exits before ever calling Chat.
    [HarmonyPatch(typeof(Talker), "RPC_Say")]
    public static class ChatVotePatch
    {
        [HarmonyPrefix]
        public static void Prefix(UserInfo user, string text)
        {
            Main.Log?.LogInfo($"[ChatVotePatch] RPC_Say received: user='{user.Name}' text='{text}' IsServer={Main.IsServer}");

            if (text == "!spectator")
            {
                if (ClientSync.Phase != MatchPhase.Lobby)
                {
                    Main.Log?.LogInfo("[ChatVotePatch] !spectator ignored — not in lobby");
                    return;
                }

                if (!Main.IsServer)
                {
                    // On a listen-server, RPC_Say fires on both the server path (IsServer=true,
                    // handled above) and the client path (IsServer=false). Skip the RPC if the
                    // server already handled it (player is already a spectator).
                    if (ClientSync.SpectatorList.Contains(user.Name))
                    {
                        Main.Log?.LogInfo($"[ChatVotePatch] Client: '{user.Name}' already spectator, skip duplicate RPC");
                        return;
                    }
                    Main.Log?.LogInfo($"[ChatVotePatch] Client sending RequestSpectator for '{user.Name}'");
                    ClientSync.SendRequestSpectator(user.Name);
                    return;
                }

                Main.Log?.LogInfo($"[ChatVotePatch] Server: '{user.Name}' entering spectator mode");
                SpectatorManager.EnterSpectatorModeByName(user.Name);
                return;
            }

            if (text != "!brstart") return;

            if (!Main.IsServer)
            {
                // Dedicated server never fires Talker.RPC_Say - client sends vote via ZRoutedRpc instead
                Main.Log?.LogInfo($"[ChatVotePatch] Client sending VoteStart RPC for '{user.Name}'");
                ClientSync.SendVoteStart(user.Name);
                return;
            }

            // Solo host path: server also has local player, handle directly
            Main.Log?.LogInfo($"[ChatVotePatch] Server-side !brstart from '{user.Name}', MatchManager={(MatchManager.Instance == null ? "NULL" : "OK")}");

            if (MatchManager.Instance == null)
            {
                Main.Log?.LogWarning("[ChatVotePatch] MatchManager.Instance is null — server not initialized yet");
                return;
            }

            if (MatchManager.Instance.State?.Phase == MatchPhase.Active)
            {
                Main.Log?.LogInfo("[ChatVotePatch] Match already active, ignoring vote");
                VoteTracker.BroadcastSystem("Match already in progress.");
                return;
            }

            VoteTracker.AddVote(user.Name);
        }
    }

    public static class VoteTracker
    {
        private static readonly HashSet<string> _votes = new HashSet<string>();

        static VoteTracker()
        {
            BREventBus.Subscribe<MatchStartedEvent>(_ => Clear());
            BREventBus.Subscribe<MatchEndedEvent>(_ => Clear());
        }

        public static void AddVote(string playerName)
        {
            if (MatchManager.Instance == null)
            {
                Main.Log?.LogWarning("[VoteTracker] AddVote called but MatchManager.Instance is null");
                return;
            }

            _votes.Add(playerName);
            int total = Player.GetAllPlayers().Count;
            if (total == 0 && ZNet.instance != null)
                total = ZNet.instance.GetPeers().Count;
            int spectCount = MatchManager.Instance?.State?.SpectatorPlayers.Count ?? 0;
            total = System.Math.Max(1, total - spectCount);
            int count = _votes.Count;

            Main.Log?.LogInfo($"[VoteTracker] Vote from '{playerName}': {count}/{total} ready. Set: [{string.Join(", ", _votes)}]");

            if (count >= total && total > 0)
            {
                Main.Log?.LogInfo("[VoteTracker] All players voted, calling MatchManager.Start()");
                BroadcastSystem("All players ready! Starting Battle Royale...");
                var voters = new System.Collections.Generic.HashSet<string>(_votes);
                _votes.Clear();
                MatchManager.Instance.Start(voters);
            }
            else
            {
                BroadcastSystem($"{playerName} is ready! [{count}/{total}] — type /brstart to vote");
            }
        }

        public static int VoteCount => _votes.Count;

        public static void Clear()
        {
            if (_votes.Count > 0)
                Main.Log?.LogInfo($"[VoteTracker] Clearing {_votes.Count} vote(s): [{string.Join(", ", _votes)}]");
            _votes.Clear();
        }

        public static void BroadcastSystem(string msg)
        {
            Main.Log?.LogInfo($"[VoteTracker] BroadcastSystem: '{msg}'");
            ClientSync.BroadcastSystemMessage(msg);
        }
    }

}
