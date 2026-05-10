using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace BattleRoyale
{
    public static class ClientSync
    {
        private const string RPC_ZoneUpdate     = "BR_ZoneUpdate";
        private const string RPC_MatchStarted  = "BR_MatchStarted";
        private const string RPC_MatchEnded    = "BR_MatchEnded";
        private const string RPC_PlayerKilled  = "BR_PlayerKilled";
        private const string RPC_SystemMessage = "BR_SystemMessage";
        private const string RPC_VoteStart     = "BR_VoteStart";
        private const string RPC_TeleportTo    = "BR_TeleportTo";
        private const string RPC_WipeInventory = "BR_WipeInventory";
        private const string RPC_SetSkills     = "BR_SetSkills";

        public static float    ZoneRadius      { get; private set; } = 500f;
        public static Vector3  ZoneCenter      { get; private set; } = Vector3.zero;
        public static float    ZoneDamage      { get; private set; } = 5f;
        public static float    ZoneNextRadius  { get; private set; } = 500f;
        public static Vector3  ZoneNextCenter  { get; private set; } = Vector3.zero;
        public static int      ZonePhaseNumber { get; private set; } = 1;
        public static int      AliveCount      { get; private set; } = 0;
        public static MatchPhase Phase         { get; private set; } = MatchPhase.Lobby;
        public static string   WinnerName      { get; private set; } = "";

        public static bool IsOutsideZone =>
            Player.m_localPlayer != null &&
            Vector3.Distance(Player.m_localPlayer.transform.position, ZoneCenter) > ZoneRadius;

        public static readonly List<KillFeedEntry> KillFeed = new List<KillFeedEntry>();

        public struct KillFeedEntry
        {
            public string KillerName;
            public string VictimName;
            public float  TimeAdded;
            public int    AliveRemaining;
        }

        private static ManualLogSource _log;

        public static void Init(ManualLogSource log) => _log = log;

        // Call from ZNetScene (or Game.Start) on all peers after ZRoutedRpc is ready
        public static void RegisterRpcs()
        {
            _log?.LogInfo($"[ClientSync] RegisterRpcs called, ZRoutedRpc.instance={(ZRoutedRpc.instance == null ? "NULL" : "OK")}");
            if (ZRoutedRpc.instance == null)
            {
                _log?.LogWarning("[ClientSync] RegisterRpcs: ZRoutedRpc.instance is null, RPC handlers NOT registered");
                return;
            }
            ZRoutedRpc.instance.Register<ZPackage>(RPC_ZoneUpdate,     RpcZoneUpdate);
            ZRoutedRpc.instance.Register<ZPackage>(RPC_MatchStarted,  RpcMatchStarted);
            ZRoutedRpc.instance.Register<ZPackage>(RPC_MatchEnded,    RpcMatchEnded);
            ZRoutedRpc.instance.Register<ZPackage>(RPC_PlayerKilled,  RpcPlayerKilled);
            ZRoutedRpc.instance.Register<ZPackage>(RPC_SystemMessage, RpcSystemMessage);
            ZRoutedRpc.instance.Register<ZPackage>(RPC_VoteStart,     RpcVoteStart);
            ZRoutedRpc.instance.Register<ZPackage>(RPC_TeleportTo,    RpcTeleportTo);
            ZRoutedRpc.instance.Register<ZPackage>(RPC_WipeInventory, RpcWipeInventory);
            ZRoutedRpc.instance.Register<ZPackage>(RPC_SetSkills,     RpcSetSkills);
            _log?.LogInfo("[ClientSync] RPC handlers registered: ZoneUpdate, MatchStarted, MatchEnded, PlayerKilled, SystemMessage, VoteStart, TeleportTo, WipeInventory, SetSkills");
        }

        // Server only: mirror BREventBus events to all clients via ZRoutedRpc
        public static void RegisterServerForwards()
        {
            _log?.LogInfo("[ClientSync] RegisterServerForwards called — subscribing to BREventBus events");
            BREventBus.Subscribe<ZoneUpdatedEvent>(ForwardZoneUpdate);
            BREventBus.Subscribe<MatchStartedEvent>(ForwardMatchStarted);
            BREventBus.Subscribe<MatchEndedEvent>(ForwardMatchEnded);
            BREventBus.Subscribe<PlayerKilledEvent>(ForwardPlayerKilled);
        }

        // ── Server-side forwarders ────────────────────────────────────────────

        private static void ForwardZoneUpdate(ZoneUpdatedEvent e)
        {
            _log?.LogInfo($"[ClientSync] ForwardZoneUpdate: radius={e.Radius}, center={e.Center}, dmg={e.DamagePerSecond}, nextR={e.NextRadius}, phase={e.PhaseNumber}");
            ZoneRadius      = e.Radius;
            ZoneCenter      = e.Center;
            ZoneDamage      = e.DamagePerSecond;
            ZoneNextRadius  = e.NextRadius;
            ZoneNextCenter  = e.NextCenter;
            ZonePhaseNumber = e.PhaseNumber;

            var pkg = new ZPackage();
            pkg.Write(e.Radius);
            pkg.Write(e.Center);
            pkg.Write(e.DamagePerSecond);
            pkg.Write(e.NextRadius);
            pkg.Write(e.NextCenter);
            pkg.Write(e.PhaseNumber);
            ZRoutedRpc.instance?.InvokeRoutedRPC(ZRoutedRpc.Everybody, RPC_ZoneUpdate, pkg);
        }

        private static void ForwardMatchStarted(MatchStartedEvent e)
        {
            _log?.LogInfo($"[ClientSync] ForwardMatchStarted: matchId={e.MatchId}, players={e.PlayerCount}, ZRoutedRpc={(ZRoutedRpc.instance == null ? "NULL" : "OK")}");
            Phase      = MatchPhase.Active;
            AliveCount = e.PlayerCount;
            WinnerName = "";
            lock (KillFeed) KillFeed.Clear();

            var pkg = new ZPackage();
            pkg.Write(e.MatchId);
            pkg.Write(e.PlayerCount);
            ZRoutedRpc.instance?.InvokeRoutedRPC(ZRoutedRpc.Everybody, RPC_MatchStarted, pkg);
        }

        private static void ForwardMatchEnded(MatchEndedEvent e)
        {
            _log?.LogInfo($"[ClientSync] ForwardMatchEnded: winner={e.WinnerName}");
            Phase      = MatchPhase.Ended;
            WinnerName = e.WinnerName;

            var pkg = new ZPackage();
            pkg.Write(e.WinnerName);
            ZRoutedRpc.instance?.InvokeRoutedRPC(ZRoutedRpc.Everybody, RPC_MatchEnded, pkg);
        }

        private static void ForwardPlayerKilled(PlayerKilledEvent e)
        {
            int alive  = MatchManager.Instance?.State?.AlivePlayers.Count ?? 0;
            _log?.LogInfo($"[ClientSync] ForwardPlayerKilled: killer={e.KillerName}, victim={e.VictimName}, alive={alive}");
            AliveCount = alive;
            // Server (and solo host) owns the kill feed here; pure clients get it via RPC
            AddKillFeedEntry(e.KillerName, e.VictimName, alive);

            var pkg = new ZPackage();
            pkg.Write(e.KillerName);
            pkg.Write(e.VictimName);
            pkg.Write(alive);
            ZRoutedRpc.instance?.InvokeRoutedRPC(ZRoutedRpc.Everybody, RPC_PlayerKilled, pkg);
        }

        public static void SendTeleport(string playerName, Vector3 pos)
        {
            if (ZRoutedRpc.instance == null) return;
            _log?.LogInfo($"[ClientSync] SendTeleport: player='{playerName}', pos={pos}");
            var pkg = new ZPackage();
            pkg.Write(playerName);
            pkg.Write(pos);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RPC_TeleportTo, pkg);
        }

        public static void BroadcastWipeInventory()
        {
            if (ZRoutedRpc.instance == null) return;
            _log?.LogInfo("[ClientSync] BroadcastWipeInventory");
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RPC_WipeInventory, new ZPackage());
        }

        public static void BroadcastSetSkills(int level)
        {
            if (ZRoutedRpc.instance == null) return;
            _log?.LogInfo($"[ClientSync] BroadcastSetSkills: level={level}");
            var pkg = new ZPackage();
            pkg.Write(level);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RPC_SetSkills, pkg);
        }

        // ── RPC handlers (run on receiving peers) ────────────────────────────

        private static void RpcZoneUpdate(long sender, ZPackage pkg)
        {
            ZoneRadius      = pkg.ReadSingle();
            ZoneCenter      = pkg.ReadVector3();
            ZoneDamage      = pkg.ReadSingle();
            ZoneNextRadius  = pkg.ReadSingle();
            ZoneNextCenter  = pkg.ReadVector3();
            ZonePhaseNumber = pkg.ReadInt();
            _log?.LogInfo($"[ClientSync] RpcZoneUpdate received: radius={ZoneRadius}, center={ZoneCenter}, dmg={ZoneDamage}, nextR={ZoneNextRadius}, phase={ZonePhaseNumber}");
        }

        private static void RpcMatchStarted(long sender, ZPackage pkg)
        {
            string matchId = pkg.ReadString();
            AliveCount = pkg.ReadInt();
            Phase      = MatchPhase.Active;
            WinnerName = "";
            lock (KillFeed) KillFeed.Clear();
            _log?.LogInfo($"[ClientSync] RpcMatchStarted received: matchId={matchId}, aliveCount={AliveCount}");
        }

        private static void RpcMatchEnded(long sender, ZPackage pkg)
        {
            WinnerName = pkg.ReadString();
            Phase      = MatchPhase.Ended;
            _log?.LogInfo($"[ClientSync] RpcMatchEnded received: winner={WinnerName}");
        }

        private static void RpcPlayerKilled(long sender, ZPackage pkg)
        {
            string killer = pkg.ReadString();
            string victim = pkg.ReadString();
            int    alive  = pkg.ReadInt();
            AliveCount    = alive;
            _log?.LogInfo($"[ClientSync] RpcPlayerKilled received: killer={killer}, victim={victim}, alive={alive}");

            // Server already added kill feed in ForwardPlayerKilled; skip on server to avoid duplicates
            if (!Main.IsServer)
                AddKillFeedEntry(killer, victim, alive);
        }

        // Client-side: send a !brstart vote to the server via ZRoutedRpc
        public static void SendVoteStart(string playerName)
        {
            _log?.LogInfo($"[ClientSync] SendVoteStart: player='{playerName}', ZRoutedRpc={(ZRoutedRpc.instance == null ? "NULL" : "OK")}");
            if (ZRoutedRpc.instance == null)
            {
                _log?.LogWarning("[ClientSync] SendVoteStart: ZRoutedRpc.instance is null — vote not sent");
                return;
            }
            var pkg = new ZPackage();
            pkg.Write(playerName);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RPC_VoteStart, pkg);
        }

        private static void RpcVoteStart(long sender, ZPackage pkg)
        {
            string playerName = pkg.ReadString();
            _log?.LogInfo($"[ClientSync] RpcVoteStart received: player='{playerName}', IsServer={Main.IsServer}");
            if (!Main.IsServer) return;
            Patches.VoteTracker.AddVote(playerName);
        }

        // Server-side: broadcast a chat message to all clients using our own RPC
        // (avoids UserInfo serialization issues with the vanilla "ChatMessage" RPC)
        public static void BroadcastSystemMessage(string msg)
        {
            _log?.LogInfo($"[ClientSync] BroadcastSystemMessage: '{msg}', ZRoutedRpc={(ZRoutedRpc.instance == null ? "NULL" : "OK")}");
            if (ZRoutedRpc.instance == null) return;
            var pkg = new ZPackage();
            pkg.Write(msg);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RPC_SystemMessage, pkg);
        }

        private static void RpcSystemMessage(long sender, ZPackage pkg)
        {
            string msg = pkg.ReadString();
            _log?.LogInfo($"[ClientSync] RpcSystemMessage received: '{msg}'");
            Chat.instance?.AddString("BattleRoyale", msg, Talker.Type.Shout);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void RpcTeleportTo(long sender, ZPackage pkg)
        {
            string targetName = pkg.ReadString();
            Vector3 pos = pkg.ReadVector3();
            var player = Player.m_localPlayer;
            if (player == null || player.GetPlayerName() != targetName)
            {
                _log?.LogInfo($"[ClientSync] RpcTeleportTo: skip — target='{targetName}', local='{player?.GetPlayerName() ?? "null"}'");
                return;
            }
            _log?.LogInfo($"[ClientSync] RpcTeleportTo: teleporting '{targetName}' to {pos}");
            player.TeleportTo(pos, player.transform.rotation, true);
        }

        private static void RpcWipeInventory(long sender, ZPackage pkg)
        {
            _log?.LogInfo("[ClientSync] RpcWipeInventory: wiping local inventory");
            Player.m_localPlayer?.GetInventory().RemoveAll();
        }

        private static void RpcSetSkills(long sender, ZPackage pkg)
        {
            int level = pkg.ReadInt();
            _log?.LogInfo($"[ClientSync] RpcSetSkills: level={level}");
            ApplySkillsToLocalPlayer(level);
        }

        private static readonly System.Reflection.MethodInfo s_getSkillMethod =
            typeof(Skills).GetMethod("GetSkill",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null, new[] { typeof(Skills.SkillType) }, null);

        public static void ApplySkillsToLocalPlayer(int level)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            ApplySkillLevel(player.GetSkills(), level);
        }

        public static void ApplySkillLevel(Skills skills, int level)
        {
            if (s_getSkillMethod == null) return;
            foreach (Skills.SkillType skillType in System.Enum.GetValues(typeof(Skills.SkillType)))
            {
                if (skillType == Skills.SkillType.None || skillType == Skills.SkillType.All) continue;
                var skill = s_getSkillMethod.Invoke(skills, new object[] { skillType }) as Skills.Skill;
                if (skill == null) continue;
                skill.m_level = (float)level;
                skill.m_accumulator = 0f;
            }
        }

        private static void AddKillFeedEntry(string killer, string victim, int alive)
        {
            lock (KillFeed)
            {
                KillFeed.Add(new KillFeedEntry
                {
                    KillerName     = killer,
                    VictimName     = victim,
                    TimeAdded      = Time.time,
                    AliveRemaining = alive
                });
                if (KillFeed.Count > 10)
                    KillFeed.RemoveAt(0);
            }
        }
    }
}
