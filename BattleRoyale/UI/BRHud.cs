using System.Collections.Generic;
using UnityEngine;

namespace BattleRoyale.UI
{
    public class BRHud : MonoBehaviour
    {
        private const float KillFeedDuration = 8f;

        private GUIStyle  _boxStyle;
        private GUIStyle  _labelStyle;
        private GUIStyle  _warnStyle;
        private GUIStyle  _killStyle;
        private GUIStyle  _btnStyle;
        private GUIStyle  _btnVotedStyle;
        private Texture2D _redTex;
        private bool      _stylesReady;
        private Material  _mapLineMaterial;

        private static bool _voted;

        private void Awake()
        {
            BREventBus.Subscribe<MatchEndedEvent>(_ => _voted = false);
            BREventBus.Subscribe<MatchStartedEvent>(_ => _voted = false);
        }

        private void InitStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize  = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _boxStyle.normal.textColor = Color.white;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _labelStyle.normal.textColor = Color.white;

            _warnStyle = new GUIStyle(_labelStyle)
            {
                fontSize = 44
            };
            _warnStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);

            _killStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 30,
                fontStyle = FontStyle.Bold
            };
            _killStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);

            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _btnStyle.normal.textColor = Color.white;

            _btnVotedStyle = new GUIStyle(_btnStyle);
            _btnVotedStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

            _redTex = new Texture2D(1, 1);
            _redTex.SetPixel(0, 0, Color.white);
            _redTex.Apply();
        }

        private void OnGUI()
        {
            InitStyles();

            if (ClientSync.Phase == MatchPhase.Lobby)
            {
                DrawStartButton();
                return;
            }

            if (ClientSync.IsSpectator)
            {
                SpectatorHud.Draw();
                DrawPlayerCount();
                DrawZoneInfo();
                DrawKillFeed();
                DrawZoneCirclesOnMap();
                if (ClientSync.Phase == MatchPhase.Active)
                    DrawTestingButtons();
                return;
            }

            DrawPlayerCount();
            DrawZoneInfo();
            DrawKillFeed();
            DrawZoneCirclesOnMap();

            if (ClientSync.Phase == MatchPhase.Ended)
                DrawMatchEnded();

            if (ClientSync.IsOutsideZone)
                DrawZoneDamageOverlay();

            if (ClientSync.Phase == MatchPhase.Active)
                DrawTestingButtons();
        }

        private void DrawStartButton()
        {
            if (InventoryGui.instance == null || !InventoryGui.IsVisible()) return;
            if (Player.m_localPlayer == null) return;

            const float w = 800f, h = 88f, joinW = 494f, gap = 12f;
            float x = 10f;
            float y = Screen.height - h - 10f;

            if (_voted)
                GUI.Button(new Rect(x, y, w, h), "Vote sent!", _btnVotedStyle);
            else if (GUI.Button(new Rect(x, y, w, h), "Start Match", _btnStyle))
            {
                _voted = true;
                ClientSync.SendVoteStart(Player.m_localPlayer.GetPlayerName());
            }

            float joinY = y - h - 6f;
            bool isSpec = ClientSync.IsSpectator;
            string playerName = Player.m_localPlayer.GetPlayerName();

            if (GUI.Button(new Rect(x, joinY, joinW, h), "Join as Spectator",
                    isSpec ? _btnVotedStyle : _btnStyle) && !isSpec)
            {
                ClientSync.SendRequestSpectator(playerName);
            }

            if (GUI.Button(new Rect(x + joinW + gap, joinY, joinW, h), "Join as Player",
                    isSpec ? _btnStyle : _btnVotedStyle) && isSpec)
            {
                ClientSync.SendRequestJoinPlayer(playerName);
            }
        }

        private void DrawPlayerCount()
        {
            GUI.Box(new Rect(Screen.width - 410, 10, 390, 76),
                $"Players alive: {ClientSync.AliveCount}", _boxStyle);
        }

        private void DrawZoneInfo()
        {
            Vector3 viewPos;
            if (ClientSync.IsSpectator)
                viewPos = SpectatorManager.FlyPosition;
            else if (Player.m_localPlayer != null)
                viewPos = Player.m_localPlayer.transform.position;
            else
                return;

            float dist    = Vector3.Distance(viewPos, ClientSync.ZoneCenter);
            float toEdge  = ClientSync.ZoneRadius - dist;
            bool  outside = toEdge < 0f;

            string line1 = $"Zone {ClientSync.ZonePhaseNumber}/6 - radius: {ClientSync.ZoneRadius:F0} m";
            string line2 = outside
                ? $"OUTSIDE ({-toEdge:F0} m out)  -{ClientSync.ZoneDamage:F1} HP/s"
                : $"To edge: {toEdge:F0} m";

            GUIStyle style = outside ? _warnStyle : _labelStyle;

            float w = 680f;
            float x = (Screen.width - w) / 2f;
            GUI.Box(new Rect(x, 10, w, 120), "", _boxStyle);
            GUI.Label(new Rect(x, 18, w, 44), line1, _labelStyle);
            GUI.Label(new Rect(x, 76, w, 52), line2, style);
        }

        private void DrawZoneCirclesOnMap()
        {
            if (Minimap.instance == null) return;
            if (Minimap.instance.m_mode != Minimap.MapMode.Large) return;
            if (ClientSync.Phase != MatchPhase.Active) return;

            if (_mapLineMaterial == null)
            {
                _mapLineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
                _mapLineMaterial.hideFlags = HideFlags.HideAndDontSave;
                _mapLineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _mapLineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _mapLineMaterial.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
                _mapLineMaterial.SetInt("_ZWrite",   0);
            }

            DrawMapCircle(ClientSync.ZoneCenter,     ClientSync.ZoneRadius,     new Color(1f,   0.15f, 0.15f, 1f));
            DrawMapCircle(ClientSync.ZoneNextCenter, ClientSync.ZoneNextRadius, new Color(0.2f, 0.5f,  1f,   1f));
        }

        private void DrawMapCircle(Vector3 worldCenter, float worldRadius, Color color)
        {
            if (worldRadius < 1f) return;

            var map = Minimap.instance;
            var img = map.m_mapImageLarge;
            var rt  = img.rectTransform;

            // WorldToMapPoint formula (replicates private Minimap method)
            float worldToUV = 1f / (map.m_pixelSize * map.m_textureSize);
            float mx = worldCenter.x * worldToUV + 0.5f;
            float my = worldCenter.z * worldToUV + 0.5f;

            // uvRect is the currently visible portion of the map texture
            UnityEngine.Rect uvRect = img.uvRect;

            float normX = (mx - uvRect.x) / uvRect.width;
            float normY = (my - uvRect.y) / uvRect.height;

            // Local position in RectTransform space
            float localX = rt.rect.xMin + normX * rt.rect.width;
            float localY = rt.rect.yMin + normY * rt.rect.height;

            Vector3 sc3 = rt.TransformPoint(new Vector3(localX, localY, 0f));
            float screenCx = sc3.x;
            float screenCy = sc3.y;

            float uvRadius = worldRadius * worldToUV;
            float screenR  = (uvRadius / uvRect.width) * rt.rect.width * rt.lossyScale.x;
            if (screenR < 2f) return;

            _mapLineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();

            const int segs = 64;
            for (int pass = -1; pass <= 1; pass++)
            {
                GL.Begin(GL.LINE_STRIP);
                GL.Color(color);
                float r = screenR + pass;
                for (int i = 0; i <= segs; i++)
                {
                    float angle = i * Mathf.PI * 2f / segs;
                    GL.Vertex3(screenCx + Mathf.Cos(angle) * r, screenCy + Mathf.Sin(angle) * r, 0f);
                }
                GL.End();
            }

            GL.PopMatrix();
        }

        private void DrawKillFeed()
        {
            float now = Time.time;
            List<ClientSync.KillFeedEntry> snapshot;
            lock (ClientSync.KillFeed)
                snapshot = new List<ClientSync.KillFeedEntry>(ClientSync.KillFeed);

            float y = Screen.height / 2f - (snapshot.Count * 52f) / 2f;

            foreach (var entry in snapshot)
            {
                float age = now - entry.TimeAdded;
                if (age > KillFeedDuration) { y += 52f; continue; }

                float alpha = Mathf.Clamp01(1f - age / KillFeedDuration);
                _killStyle.normal.textColor = new Color(1f, 0.85f, 0.4f, alpha);
                string msg = $"  {entry.KillerName}  killed  {entry.VictimName}  [{entry.AliveRemaining} left]";
                GUI.Label(new Rect(10, y, 1000, 48), msg, _killStyle);
                y += 52f;
            }
        }

        private void DrawMatchEnded()
        {
            string msg = string.IsNullOrEmpty(ClientSync.WinnerName)
                ? "MATCH ENDED"
                : $"WINNER: {ClientSync.WinnerName.ToUpper()}";

            float w = 1000f, h = 140f;
            GUI.Box(new Rect((Screen.width - w) / 2f, Screen.height / 2f - h / 2f - 120f, w, h), "", _boxStyle);
            GUI.Label(new Rect((Screen.width - w) / 2f, Screen.height / 2f - h / 2f - 120f, w, h), msg, _warnStyle);
        }

        private void DrawZoneDamageOverlay()
        {
            float alpha = 0.12f + 0.08f * Mathf.Sin(Time.time * 5f);
            GUI.color = new Color(0.73f, 0f, 0f, 0.38f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _redTex);
            GUI.color = Color.white;
        }

        private void DrawTestingButtons()
        {
            if (Main.Instance == null || !Main.Instance.TestingMode) return;
            if (Player.m_localPlayer == null) return;

            const float w = 340f, h = 64f, gap = 8f;
            float x = 10f;
            float y = Screen.height - h - 10f;
            string playerName = Player.m_localPlayer.GetPlayerName();

            if (ClientSync.IsSpectator)
            {
                if (GUI.Button(new Rect(x, y, w, h), "[TEST] Switch to Player", _btnStyle))
                    ClientSync.SendTestSwitchToPlayer(playerName);
            }
            else
            {
                if (GUI.Button(new Rect(x, y, w, h), "[TEST] Switch to Spectator", _btnStyle))
                    ClientSync.SendTestSwitchToSpectator(playerName);
            }

            if (ClientSync.AliveCount < 2)
            {
                if (GUI.Button(new Rect(x, y - h - gap, w, h), "[TEST] Force End Match", _btnStyle))
                    ClientSync.SendTestForceEnd();
            }
        }
    }
}
