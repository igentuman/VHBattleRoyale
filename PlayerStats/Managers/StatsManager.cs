using System.IO;
using BepInEx;
using PlayerStats.Data;
using UnityEngine;

namespace PlayerStats.Managers
{
    public static class StatsManager
    {
        private static string StatsFolder => Path.Combine(Paths.ConfigPath, "PlayerStats", "Stats");
        private static StatsData _currentStats;

        public static StatsData CurrentStats
        {
            get
            {
                if (_currentStats == null)
                {
                    LoadStats();
                }
                return _currentStats;
            }
        }

        public static void LoadStats()
        {
            string filePath = GetFilePath();
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                _currentStats = JsonUtility.FromJson<StatsData>(json);
            }
            else
            {
                _currentStats = new StatsData();
            }
        }

        public static void SaveStats()
        {
            if (_currentStats == null) return;
            
            string filePath = GetFilePath();
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(_currentStats, true);
            File.WriteAllText(filePath, json);
        }

        public static void ClearStats()
        {
            _currentStats = null;
        }

        private static string GetFilePath()
        {
            string characterName = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : "Default";
            string worldName = ZNet.instance != null ? ZNet.instance.GetWorldName() : "Default";
            return Path.Combine(StatsFolder, $"stats_{characterName}_{worldName}.json");
        }
    }
}
