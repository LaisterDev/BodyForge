using HarmonyLib;
using UnityEngine;

namespace BodyForge
{
    internal static class CameraGuard
    {
        private static bool _locked;

        public static bool ShouldLock => LiveBridge.IsEditorActive && Menu.IsVisible();

        public static void UpdateLog(bool locked)
        {
            if (locked == _locked) return;
            _locked = locked;
            BodyForgePlugin.LogInfo(locked
                ? "camera locked while the pause menu and BodyForge editor are active"
                : "camera lock released");
        }
    }

    [HarmonyPatch(typeof(PlayerController), "LateUpdate")]
    public static class PlayerLookGuardPatch
    {
        public static bool Prefix(PlayerController __instance)
        {
            if (!CameraGuard.ShouldLock) return true;
            Player player = __instance.GetComponent<Player>();
            if (player != null) player.SetMouseLook(Vector2.zero);
            return false;
        }
    }

    [HarmonyPatch(typeof(GameCamera), "LateUpdate")]
    public static class GameCameraGuardPatch
    {
        public static bool Prefix()
        {
            bool locked = CameraGuard.ShouldLock;
            CameraGuard.UpdateLog(locked);
            return !locked;
        }
    }
}
