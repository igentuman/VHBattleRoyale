using UnityEngine;

namespace BattleRoyale.UI
{
    public static class SpectatorHud
    {
        private static GUIStyle _titleStyle;
        private static GUIStyle _hintStyle;
        private static bool     _stylesReady;

        private static void InitStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 44,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(0.2f, 0.8f, 1f);

            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 30,
                alignment = TextAnchor.MiddleCenter
            };
            _hintStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        }

        public static void Draw()
        {
            InitStyles();

            float w = 1000f;
            float x = (Screen.width - w) / 2f;

            // Top-center: who we're watching
            string target  = SpectatorManager.CurrentTargetName();
            string topText = target != null ? $"SPECTATING: {target.ToUpper()}" : "FREE CAMERA";
            GUI.Label(new Rect(x, 10f, w, 60f), topText, _titleStyle);

            // Bottom-center: controls hint
            string hint = "[Tab] Next  |  [Shift+Tab] Prev  |  [WASD] Fly  |  [Shift] Fast";
            GUI.Label(new Rect(x, Screen.height - 100f, w, 48f), hint, _hintStyle);

            // Spectator count above hint
            int count = ClientSync.SpectatorList.Count;
            if (count > 0)
            {
                string spectText = count == 1 ? "1 spectator watching" : $"{count} spectators watching";
                GUI.Label(new Rect(x, Screen.height - 148f, w, 48f), spectText, _hintStyle);
            }
        }
    }
}
