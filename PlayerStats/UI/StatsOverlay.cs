using System.Text;
using PlayerStats.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace PlayerStats.UI
{
    public class StatsOverlay : MonoBehaviour
    {
        private static StatsOverlay _instance;
        public static StatsOverlay Instance => _instance;

        private GameObject _overlayRoot;
        private Text _statsText;
        private float _updateTimer = 0f;
        private const float UpdateInterval = 0.5f;

        private void Awake()
        {
            _instance = this;
            CreateUI();
        }

        private void CreateUI()
        {
            _overlayRoot = new GameObject("PlayerStatsOverlay");
            _overlayRoot.transform.SetParent(transform);

            Canvas canvas = _overlayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = _overlayRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GameObject textObj = new GameObject("StatsText");
            textObj.transform.SetParent(_overlayRoot.transform);

            _statsText = textObj.AddComponent<Text>();
            _statsText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _statsText.fontSize = 20;
            _statsText.color = Color.white;
            _statsText.alignment = TextAnchor.UpperLeft;

            Shadow shadow = textObj.AddComponent<Shadow>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(1, -1);

            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(Main.Instance.OverlayX.Value, Main.Instance.OverlayY.Value);
            rect.sizeDelta = new Vector2(400, 800);
        }

        private void Update()
        {
            if (!Main.Instance.ShowOverlay.Value || Player.m_localPlayer == null)
            {
                _overlayRoot.SetActive(false);
                return;
            }

            _overlayRoot.SetActive(true);

            HandleDragging();

            _updateTimer += Time.deltaTime;
            if (_updateTimer >= UpdateInterval)
            {
                _updateTimer = 0f;
                UpdateText();
            }
        }

        private void HandleDragging()
        {
            if (Input.GetKey(KeyCode.LeftAlt) && (Cursor.visible || (InventoryGui.instance != null && InventoryGui.IsVisible())))
            {
                RectTransform rect = _statsText.GetComponent<RectTransform>();
                Vector2 mousePos;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _overlayRoot.GetComponent<RectTransform>(),
                    Input.mousePosition,
                    null,
                    out mousePos))
                {
                    if (Input.GetMouseButton(0))
                    {
                        Main.Instance.OverlayX.Value = mousePos.x + 960f; 
                        Main.Instance.OverlayY.Value = mousePos.y - 540f; 
                        
                        UpdatePosition();
                    }
                }
            }
        }

        private void UpdatePosition()
        {
            RectTransform rect = _statsText.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(Main.Instance.OverlayX.Value, Main.Instance.OverlayY.Value);
        }

        private void UpdateText()
        {
            UpdatePosition(); 
            var stats = StatsManager.CurrentStats;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<color=orange><b>Player Stats</b></color>");

            if (Main.Instance.ShowKills.Value) sb.AppendLine($"Kills: {stats.kills}");
            if (Main.Instance.ShowDeaths.Value) sb.AppendLine($"Deaths: {stats.deaths}");
            if (Main.Instance.ShowDodges.Value) sb.AppendLine($"Dodges: {stats.dodges}");
            if (Main.Instance.ShowBlocks.Value) sb.AppendLine($"Blocks: {stats.blocks}");
            if (Main.Instance.ShowPerfectBlocks.Value) sb.AppendLine($"Perfect Blocks: {stats.perfectBlocks}");
            if (Main.Instance.ShowTotalDamage.Value) sb.AppendLine($"Total Damage: {stats.totalDamage:F0}");
            if (Main.Instance.ShowTotalDrowningDamage.Value) sb.AppendLine($"Drowning Damage: {stats.totalDrowningDamage:F0}");
            
            sb.AppendLine();
            if (Main.Instance.ShowRunDistance.Value) sb.AppendLine($"Run Distance: {stats.runDistance:F0}m");
            if (Main.Instance.ShowWalkDistance.Value) sb.AppendLine($"Walk Distance: {stats.walkDistance:F0}m");
            if (Main.Instance.ShowAirDistance.Value) sb.AppendLine($"Air Distance: {stats.airDistance:F0}m");
            if (Main.Instance.ShowSailDistance.Value) sb.AppendLine($"Sail Distance: {stats.sailDistance:F0}m");
            if (Main.Instance.ShowTraveledDistance.Value) sb.AppendLine($"Total Traveled: {stats.traveledDistance:F0}m");
            if (Main.Instance.ShowSwimDistance.Value) sb.AppendLine($"Swim Distance: {stats.swimDistance:F0}m");
            if (Main.Instance.ShowJumps.Value) sb.AppendLine($"Jumps: {stats.jumps}");
            
            sb.AppendLine();
            if (Main.Instance.ShowTimeInBase.Value) sb.AppendLine($"Time in Base: {FormatTime(stats.timeInBase)}");
            if (Main.Instance.ShowTimeOutOfBase.Value) sb.AppendLine($"Time out of Base: {FormatTime(stats.timeOutOfBase)}");
            
            sb.AppendLine();
            if (Main.Instance.ShowTreesChopped.Value) sb.AppendLine($"Trees Chopped: {stats.treesChopped}");
            if (Main.Instance.ShowMinedCount.Value) sb.AppendLine($"Mined Items: {stats.minedCount}");
            if (Main.Instance.ShowBuildingPartsPlaced.Value) sb.AppendLine($"Buildings Placed: {stats.buildingPartsPlaced}");
            if (Main.Instance.ShowFishesCaught.Value) sb.AppendLine($"Fishes Caught: {stats.fishesCaught}");

            _statsText.text = sb.ToString();
        }

        private string FormatTime(float seconds)
        {
            System.TimeSpan t = System.TimeSpan.FromSeconds(seconds);
            if (t.TotalHours >= 1)
                return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)t.TotalHours, t.Minutes, t.Seconds);
            return string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
        }
    }
}
