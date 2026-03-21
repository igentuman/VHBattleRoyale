using System.Text;
using PlayerStats.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace PlayerStats.UI
{
    public class StatsSummary : MonoBehaviour
    {
        private static StatsSummary _instance;
        public static StatsSummary Instance => _instance;

        private GameObject _summaryRoot;
        private Text _summaryText;

        private void Awake()
        {
            _instance = this;
            CreateUI();
        }

        private void CreateUI()
        {
            _summaryRoot = new GameObject("PlayerStatsSummary");
            _summaryRoot.transform.SetParent(transform);

            Canvas canvas = _summaryRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 101;

            _summaryRoot.AddComponent<GraphicRaycaster>();

            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(_summaryRoot.transform);
            Image image = bg.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0.85f);
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            GameObject textObj = new GameObject("SummaryText");
            textObj.transform.SetParent(_summaryRoot.transform);
            _summaryText = textObj.AddComponent<Text>();
            _summaryText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _summaryText.fontSize = 24;
            _summaryText.color = Color.white;
            _summaryText.alignment = TextAnchor.MiddleCenter;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.1f, 0.1f);
            textRect.anchorMax = new Vector2(0.9f, 0.9f);
            textRect.sizeDelta = Vector2.zero;

            _summaryRoot.SetActive(false);
        }

        public void Show()
        {
            UpdateText();
            _summaryRoot.SetActive(true);
        }

        public void Hide()
        {
            _summaryRoot.SetActive(false);
        }

        public void Toggle()
        {
            if (_summaryRoot.activeSelf)
                Hide();
            else
                Show();
        }

        private void UpdateText()
        {
            var stats = StatsManager.CurrentStats;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<size=40><color=orange><b>PLAYER STATISTICS SUMMARY</b></color></size>");
            sb.AppendLine();

            sb.AppendLine("<b>--- Combat ---</b>");
            sb.AppendLine($"Total Kills: {stats.kills}");
            sb.AppendLine($"Total Deaths: {stats.deaths}");
            sb.AppendLine($"Dodges: {stats.dodges}");
            sb.AppendLine($"Blocks: {stats.blocks} (Perfect: {stats.perfectBlocks})");
            sb.AppendLine($"Total Damage Dealt: {stats.totalDamage:F0}");
            sb.AppendLine($"Total Drowning Damage: {stats.totalDrowningDamage:F0}");
            sb.AppendLine();

            sb.AppendLine("<b>--- Movement & Time ---</b>");
            sb.AppendLine($"Total Distance Traveled: {stats.traveledDistance:F0}m");
            sb.AppendLine($"Run: {stats.runDistance:F0}m | Walk: {stats.walkDistance:F0}m | Swim: {stats.swimDistance:F0}m");
            sb.AppendLine($"Air: {stats.airDistance:F0}m | Sail: {stats.sailDistance:F0}m");
            sb.AppendLine($"Jumps: {stats.jumps}");
            sb.AppendLine($"Time in Base: {FormatTime(stats.timeInBase)}");
            sb.AppendLine($"Time Exploring: {FormatTime(stats.timeOutOfBase)}");
            sb.AppendLine();

            sb.AppendLine("<b>--- Gathering & Misc ---</b>");
            sb.AppendLine($"Trees Chopped: {stats.treesChopped}");
            sb.AppendLine($"Mined Items: {stats.minedCount}");
            sb.AppendLine($"Buildings Placed: {stats.buildingPartsPlaced}");
            sb.AppendLine($"Fishes Caught: {stats.fishesCaught}");
            sb.AppendLine();

            if (Main.Instance.RevealHiddenStats.Value || stats.killed2StarMobs.Count > 0)
            {
                sb.AppendLine("<b>--- Elite 2-Star Kills ---</b>");
                if (stats.killed2StarMobs.Count == 0) sb.AppendLine("None");
                foreach (var entry in stats.killed2StarMobs)
                {
                    sb.AppendLine($"{entry.name}: {entry.count}");
                }
            }

            _summaryText.text = sb.ToString();
        }

        private string FormatTime(float seconds)
        {
            System.TimeSpan t = System.TimeSpan.FromSeconds(seconds);
            if (t.TotalHours >= 1)
                return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)t.TotalHours, t.Minutes, t.Seconds);
            return string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
        }

        private void Update()
        {
            if (_summaryRoot.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
            }
        }
    }
}
