using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using PlayerStats.Managers;
using UnityEngine;

namespace PlayerStats
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Main : BaseUnityPlugin
    {
        public const string PluginGuid = "com.igentuman.playerstats";
        public const string PluginName = "Player Stats";
        public const string PluginVersion = "1.0.0";

        private static Main _instance;
        public static Main Instance => _instance;

        private readonly Harmony _harmony = new Harmony(PluginGuid);

        public ConfigEntry<bool> ShowOverlay;
        public ConfigEntry<bool> RevealHiddenStats;
        public ConfigEntry<float> OverlayX;
        public ConfigEntry<float> OverlayY;
        
        // Overlay Toggles
        public ConfigEntry<bool> ShowKills;
        public ConfigEntry<bool> ShowDeaths;
        public ConfigEntry<bool> ShowDodges;
        public ConfigEntry<bool> ShowBlocks;
        public ConfigEntry<bool> ShowPerfectBlocks;
        public ConfigEntry<bool> ShowTotalDamage;
        public ConfigEntry<bool> ShowTotalDrowningDamage;
        public ConfigEntry<bool> ShowRunDistance;
        public ConfigEntry<bool> ShowWalkDistance;
        public ConfigEntry<bool> ShowAirDistance;
        public ConfigEntry<bool> ShowSailDistance;
        public ConfigEntry<bool> ShowTraveledDistance;
        public ConfigEntry<bool> ShowSwimDistance;
        public ConfigEntry<bool> ShowJumps;
        public ConfigEntry<bool> ShowTimeInBase;
        public ConfigEntry<bool> ShowTimeOutOfBase;
        public ConfigEntry<bool> ShowTreesChopped;
        public ConfigEntry<bool> ShowMinedCount;
        public ConfigEntry<bool> ShowBuildingPartsPlaced;
        public ConfigEntry<bool> ShowFishesCaught;

        private float _saveTimer = 0f;
        private const float SaveInterval = 300f; // 5 minutes

        private void Awake()
        {
            _instance = this;

            InitConfig();
            _harmony.PatchAll();
            
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
            
            gameObject.AddComponent<UI.StatsOverlay>();
            gameObject.AddComponent<UI.StatsSummary>();
        }

        private void InitConfig()
        {
            ShowOverlay = Config.Bind("General", "ShowOverlay", true, "Show the stats overlay on screen.");
            RevealHiddenStats = Config.Bind("General", "RevealHiddenStats", false, "Reveal hidden statistics (e.g., 2-star mobs).");
            OverlayX = Config.Bind("General", "OverlayX", 20f, "X position of the overlay (relative to anchor).");
            OverlayY = Config.Bind("General", "OverlayY", -20f, "Y position of the overlay (relative to anchor).");

            ShowKills = Config.Bind("Overlay", "ShowKills", true, "Show total kills in overlay.");
            ShowDeaths = Config.Bind("Overlay", "ShowDeaths", true, "Show total deaths in overlay.");
            ShowDodges = Config.Bind("Overlay", "ShowDodges", true, "Show total dodges in overlay.");
            ShowBlocks = Config.Bind("Overlay", "ShowBlocks", true, "Show total blocks in overlay.");
            ShowPerfectBlocks = Config.Bind("Overlay", "ShowPerfectBlocks", true, "Show total perfect blocks in overlay.");
            ShowTotalDamage = Config.Bind("Overlay", "ShowTotalDamage", true, "Show total damage dealt in overlay.");
            ShowTotalDrowningDamage = Config.Bind("Overlay", "ShowTotalDrowningDamage", true, "Show total drowning damage received in overlay.");
            
            ShowRunDistance = Config.Bind("Overlay", "ShowRunDistance", true, "Show total run distance in overlay.");
            ShowWalkDistance = Config.Bind("Overlay", "ShowWalkDistance", true, "Show total walk distance in overlay.");
            ShowAirDistance = Config.Bind("Overlay", "ShowAirDistance", true, "Show total air distance in overlay.");
            ShowSailDistance = Config.Bind("Overlay", "ShowSailDistance", true, "Show total sail distance in overlay.");
            ShowTraveledDistance = Config.Bind("Overlay", "ShowTraveledDistance", true, "Show total traveled distance in overlay.");
            ShowSwimDistance = Config.Bind("Overlay", "ShowSwimDistance", true, "Show total swim distance in overlay.");
            ShowJumps = Config.Bind("Overlay", "ShowJumps", true, "Show total jumps in overlay.");
            
            ShowTimeInBase = Config.Bind("Overlay", "ShowTimeInBase", true, "Show total time spent in base in overlay.");
            ShowTimeOutOfBase = Config.Bind("Overlay", "ShowTimeOutOfBase", true, "Show total time spent out of base in overlay.");

            ShowTreesChopped = Config.Bind("Overlay", "ShowTreesChopped", true, "Show total trees chopped in overlay.");
            ShowMinedCount = Config.Bind("Overlay", "ShowMinedCount", true, "Show total mined items in overlay.");
            ShowBuildingPartsPlaced = Config.Bind("Overlay", "ShowBuildingPartsPlaced", true, "Show total building parts placed in overlay.");
            ShowFishesCaught = Config.Bind("Overlay", "ShowFishesCaught", true, "Show total fishes caught in overlay.");
        }

        private void Update()
        {
            _saveTimer += Time.deltaTime;
            if (_saveTimer >= SaveInterval)
            {
                _saveTimer = 0f;
                StatsManager.SaveStats();
            }
        }

        private void OnDestroy()
        {
            StatsManager.SaveStats();
            _harmony.UnpatchSelf();
        }
    }
}
