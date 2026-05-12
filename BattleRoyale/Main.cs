using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BattleRoyale.Patches;
using BattleRoyale.UI;
using HarmonyLib;
using UnityEngine;

namespace BattleRoyale
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Main : BaseUnityPlugin
    {
        public const string PluginGuid = "com.igentuman.battleroyale";
        public const string PluginName = "Battle Royale";
        public const string PluginVersion = "1.0.0";

        private static Main _instance;
        public static Main Instance => _instance;

        internal static ManualLogSource Log;

        public static bool IsServer => ZNet.instance != null && ZNet.instance.IsServer();

        // Exposed config values for patches
        public float StructureDamageMultiplier => _cfgStructureDamageMultiplier.Value;
        public float StaminaCostMultiplier => _cfgStaminaCostMultiplier.Value;
        public int StartSkillLevel => _cfgStartSkillLevel.Value;
        public float StartBuffDuration => _cfgStartBuffDuration.Value;
        public bool TestingMode => _cfgTestingMode.Value;
        public float TeleportSpawnRadius => _cfgTeleportSpawnRadius.Value;

        private ConfigEntry<float> _cfgZonePhaseWaitDuration;
        private ConfigEntry<float> _cfgZonePhaseShrinkDuration;
        private ConfigEntry<float> _cfgZoneDamagePerSecond;
        private ConfigEntry<int> _cfgLootSpawnCount;
        private ConfigEntry<float> _cfgStructureDamageMultiplier;
        private ConfigEntry<float> _cfgStaminaCostMultiplier;
        private ConfigEntry<string> _cfgApiBaseUrl;
        private ConfigEntry<bool> _cfgApiEnabled;
        private ConfigEntry<int> _cfgStartSkillLevel;
        private ConfigEntry<float> _cfgStartBuffDuration;
        private ConfigEntry<bool> _cfgTestingMode;
        private ConfigEntry<bool> _cfgRenderZoneCircles;
        private ConfigEntry<float> _cfgTeleportSpawnRadius;

        public bool RenderZoneCircles => _cfgRenderZoneCircles.Value;

        private Harmony _harmony;
        private bool _initialized;
        private bool _clientInitialized;

        private static readonly string[] EmbeddedConfigs = {
            "chest_loot_tables.json",
            "mob_loot_tables.json"
        };

        private void ExtractDefaultConfigs()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var destDir = Path.Combine(Paths.ConfigPath, "BattleRoyale");
            Directory.CreateDirectory(destDir);

            foreach (var file in EmbeddedConfigs)
            {
                var dest = Path.Combine(destDir, file);
                if (File.Exists(dest)) continue;

                using var stream = assembly.GetManifestResourceStream($"BattleRoyale.config.{file}");
                if (stream == null)
                {
                    Logger.LogWarning($"[BattleRoyale] Embedded resource not found: {file}");
                    continue;
                }
                using var reader = new StreamReader(stream);
                File.WriteAllText(dest, reader.ReadToEnd());
                Logger.LogInfo($"[BattleRoyale] Extracted default config: {dest}");
            }
        }

        private void Awake()
        {
            _instance = this;
            Log = Logger;

            ExtractDefaultConfigs();
            InitConfig();
            ChatCommands.Register();
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Logger.LogInfo($"[{PluginName}] v{PluginVersion} loaded");
        }

        private void InitConfig()
        {
            _cfgZonePhaseWaitDuration     = Config.Bind("Zone",      "PhaseWaitDuration",       240f,                   "Seconds to show next zone before it starts shrinking");
            _cfgZonePhaseShrinkDuration   = Config.Bind("Zone",      "PhaseShrinkDuration",      120f,                   "Seconds to complete each zone shrink");
            _cfgZoneDamagePerSecond       = Config.Bind("Zone",      "DamagePerSecond",           1f,                   "Base damage per second outside zone (doubles each phase)");
            _cfgLootSpawnCount            = Config.Bind("Loot",      "SpawnCount",               50,                    "Number of loot items to spawn per match");
            _cfgStructureDamageMultiplier = Config.Bind("Structure", "DamageMultiplier",          2f,                   "Structure damage multiplier during BR match");
            _cfgStaminaCostMultiplier     = Config.Bind("Structure", "StaminaCostMultiplier",     2f,                   "Build stamina cost multiplier during BR match");
            _cfgApiBaseUrl                = Config.Bind("API",       "BaseUrl",  "http://localhost:3000",               "Backend API base URL");
            _cfgApiEnabled                = Config.Bind("API",       "Enabled",                false,                   "Enable API event forwarding (requires backend project)");
            _cfgStartSkillLevel           = Config.Bind("Match",     "StartSkillLevel",           20,                   "Skill level set for all players when match starts (0 = disabled)");
            _cfgStartBuffDuration         = Config.Bind("Match",     "StartBuffDuration",        300f,                   "Seconds all start buffs last (Eikthyr, rested, corpse run, feather fall, no skill drain, sneaky). 0 = disabled");
            _cfgTestingMode               = Config.Bind("Testing",   "TestingMode",             false,                   "Show in-match buttons to switch between spectator/player and force-end match");
            _cfgRenderZoneCircles         = Config.Bind("UI",        "RenderZoneCircles",        true,                    "Render zone boundary circles on the ground");
            _cfgTeleportSpawnRadius       = Config.Bind("Match",     "TeleportSpawnRadius",      4000f,                   "Radius from world center to teleport players on match start (±500 variation). Set to 0 to disable teleportation");
        }

        public void OnServerReady()
        {
            if (_initialized) return;
            _initialized = true;

            var zoneConfig = new ZoneConfig
            {
                PhaseRadii          = new float[] { 5500f, 4000f, 2000f, 1000f, 200f, 1f },
                PhaseWaitDuration   = _cfgZonePhaseWaitDuration.Value,
                PhaseShrinkDuration = _cfgZonePhaseShrinkDuration.Value,
                BaseDamagePerSecond = _cfgZoneDamagePerSecond.Value,
                InitialCenter       = Vector3.zero
            };

            ClientSync.Init(Logger);
            MatchManager.Init(Logger);
            SpectatorManager.Init(Logger);
            ZoneManager.Init(zoneConfig, Logger);
            LootManager.Init(_cfgLootSpawnCount.Value, Logger);
            ApiClient.Init(_cfgApiBaseUrl.Value, _cfgApiEnabled.Value, Logger);
            ClientSync.RegisterServerForwards();

            Logger.LogInfo($"[BattleRoyale] Zone config - phases: [{string.Join(",", zoneConfig.PhaseRadii)}]m, wait: {zoneConfig.PhaseWaitDuration}s, shrink: {zoneConfig.PhaseShrinkDuration}s, baseDmg: {zoneConfig.BaseDamagePerSecond}/s");
            Logger.LogInfo($"[BattleRoyale] Match config — loot: {_cfgLootSpawnCount.Value} items, structDmg: {_cfgStructureDamageMultiplier.Value}x, staminaCost: {_cfgStaminaCostMultiplier.Value}x, API: {(_cfgApiEnabled.Value ? _cfgApiBaseUrl.Value : "disabled")}");
            Logger.LogInfo("[BattleRoyale] All systems initialized");
        }

        public void OnClientReady()
        {
            if (_clientInitialized) return;
            _clientInitialized = true;

            ClientSync.Init(Logger);

            var hudGo = new GameObject("BR_HUD");
            DontDestroyOnLoad(hudGo);
            hudGo.AddComponent<BRHud>();

            var zoneGo = new GameObject("BR_Zone");
            DontDestroyOnLoad(zoneGo);
            zoneGo.AddComponent<ZoneRenderer>();
            zoneGo.AddComponent<ZoneMistEffect>();

            Logger.LogInfo("[BattleRoyale] Client HUD initialized");
        }

        private void Update()
        {
            if (MatchManager.Instance?.State?.Phase != MatchPhase.Active) return;
            ZoneManager.Instance?.Tick(Time.deltaTime);
            MatchManager.Instance.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
