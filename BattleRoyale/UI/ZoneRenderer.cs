using UnityEngine;

namespace BattleRoyale.UI
{
    public class ZoneRenderer : MonoBehaviour
    {
        private const int Segments = 128;

        private static readonly Color ColorCurrent = new Color(1f,   0.15f, 0.15f, 0.9f);  // red
        private static readonly Color ColorNext    = new Color(0.2f, 0.5f,  1f,   0.8f);   // blue

        private LineRenderer _currentRing;
        private LineRenderer _nextRing;

        private void Awake()
        {
            _currentRing = CreateRing("ZoneRing_Current", ColorCurrent, 3f);
            _nextRing    = CreateRing("ZoneRing_Next",    ColorNext,    2f);
        }

        private LineRenderer CreateRing(string name, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var lr = go.AddComponent<LineRenderer>();
            lr.loop          = true;
            lr.positionCount = Segments;
            lr.startWidth    = width;
            lr.endWidth      = width;
            lr.useWorldSpace = true;
            lr.material      = new Material(Shader.Find("Sprites/Default"));
            lr.startColor    = color;
            lr.endColor      = color;
            return lr;
        }

        private void Update()
        {
            bool renderEnabled = Main.Instance != null && Main.Instance.RenderZoneCircles;
            bool active = renderEnabled && ClientSync.Phase != MatchPhase.Lobby;
            _currentRing.enabled = active;

            bool showNext = active
                && ClientSync.ZoneNextRadius > 0f
                && Mathf.Abs(ClientSync.ZoneNextRadius - ClientSync.ZoneRadius) > 0.5f;
            _nextRing.enabled = showNext;

            if (!active) return;

            float displayRadius;
            Vector3 displayCenter;

            if (ClientSync.ZoneIsShrinking && ClientSync.ZoneShrinkDuration > 0f)
            {
                float elapsed = ClientSync.ZoneShrinkElapsed + (Time.time - ClientSync.ZoneSyncTime);
                float t = Mathf.Clamp01(elapsed / ClientSync.ZoneShrinkDuration);
                displayRadius = Mathf.Lerp(ClientSync.ZoneShrinkStartRadius, ClientSync.ZoneNextRadius, t);
                displayCenter = Vector3.Lerp(ClientSync.ZoneShrinkStartCenter, ClientSync.ZoneNextCenter, t);
            }
            else
            {
                displayRadius = ClientSync.ZoneRadius;
                displayCenter = ClientSync.ZoneCenter;
            }

            UpdateRing(_currentRing, displayCenter, displayRadius);
            if (showNext)
                UpdateRing(_nextRing, ClientSync.ZoneNextCenter, ClientSync.ZoneNextRadius);
        }

        private static void UpdateRing(LineRenderer ring, Vector3 center, float radius)
        {
            float y = center.y + 2f;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                ring.SetPosition(i, new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    y,
                    center.z + Mathf.Sin(angle) * radius));
            }
        }
    }
}
