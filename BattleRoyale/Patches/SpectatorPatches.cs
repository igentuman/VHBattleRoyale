using HarmonyLib;
using UnityEngine;

namespace BattleRoyale.Patches
{
    // Prevent spectators from taking damage
    [HarmonyPatch(typeof(Character), "Damage")]
    public static class SpectatorDamagePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Character __instance)
        {
            if (__instance is Player p && ClientSync.SpectatorList.Contains(p.GetPlayerName()))
                return false;
            return true;
        }
    }

    // Block the inner player-follow calculation so the camera doesn't re-attach.
    // Patching UpdateCamera (called by LateUpdate) is more reliable than trying to
    // replace LateUpdate entirely — other LateUpdate side-effects (listener, shake
    // reset, etc.) still run harmlessly; only the follow math is skipped.
    [HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
    public static class SpectatorCameraBlockPatch
    {
        [HarmonyPrefix]
        public static bool Prefix() => !ClientSync.IsSpectator;
    }

    // Apply free-fly camera AFTER LateUpdate finishes so our transform wins.
    // Using Postfix means we don't fight with whatever LateUpdate's remaining
    // code might set — we simply overwrite position/rotation last.
    [HarmonyPatch(typeof(GameCamera), "LateUpdate")]
    public static class SpectatorCameraPatch
    {
        private const float BaseSpeed     = 10f;
        private const float RunMultiplier = 3f;
        private const float MouseSens     = 2f;

        [HarmonyPostfix]
        public static void Postfix(GameCamera __instance)
        {
            if (!ClientSync.IsSpectator) return;

            float dt = Time.deltaTime;

            // Mouse look
            SpectatorManager.FlyYaw   += MouseSens * Input.GetAxis("Mouse X");
            SpectatorManager.FlyPitch -= MouseSens * Input.GetAxis("Mouse Y");
            SpectatorManager.FlyPitch  = Mathf.Clamp(SpectatorManager.FlyPitch, -89f, 89f);

            var yaw   = Quaternion.Euler(0f, SpectatorManager.FlyYaw, 0f);
            var pitch = Quaternion.Euler(SpectatorManager.FlyPitch, 0f, 0f);
            __instance.transform.rotation = yaw * pitch;

            // WASD + vertical movement
            float speed = BaseSpeed;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                speed *= RunMultiplier;

            var move = Vector3.zero;
            if (Input.GetKey(KeyCode.W))                                       move += Vector3.forward;
            if (Input.GetKey(KeyCode.S))                                       move += Vector3.back;
            if (Input.GetKey(KeyCode.A))                                       move += Vector3.left;
            if (Input.GetKey(KeyCode.D))                                       move += Vector3.right;
            if (Input.GetKey(KeyCode.Space))                                   move += Vector3.up;
            if (Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl)) move += Vector3.down;

            if (move != Vector3.zero)
                SpectatorManager.FlyPosition += __instance.transform.TransformDirection(move.normalized) * speed * dt;

            __instance.transform.position = SpectatorManager.FlyPosition;

            // E — cycle next, Q — cycle prev
            if (Input.GetKeyDown(KeyCode.E)) SpectatorManager.NextTarget();
            if (Input.GetKeyDown(KeyCode.Q)) SpectatorManager.PrevTarget();
        }
    }

    // Block all player-controller physics updates while spectating.
    [HarmonyPatch(typeof(PlayerController), "FixedUpdate")]
    public static class SpectatorInputPatch
    {
        [HarmonyPrefix]
        public static bool Prefix() => !ClientSync.IsSpectator;
    }

    // Also block Player.FixedUpdate for the local player — PlayerController only
    // handles gamepad/input reading; actual character physics run here.
    [HarmonyPatch(typeof(Player), "FixedUpdate")]
    public static class SpectatorPlayerMovePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Player __instance)
        {
            return !(ClientSync.IsSpectator && __instance == Player.m_localPlayer);
        }
    }
}
