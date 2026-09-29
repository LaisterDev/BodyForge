using HarmonyLib;
using UnityEngine;

namespace BodyForge
{
    internal static class AppearancePreviewGuard
    {
        private static string _profile = "";
        private static AppearanceState _committed;
        private static AppearanceState _preview;
        private static bool _savingBodyForge;

        internal static void BeginPreview(Player player, string profile)
        {
            if (player == null) return;
            if (_committed == null || _profile != profile)
            {
                _profile = profile;
                _committed = AppearanceState.Capture(player);
            }
        }

        internal static void UpdatePreview(Player player)
        {
            if (player != null) _preview = AppearanceState.Capture(player);
        }

        internal static void BeginCommit()
        {
            _savingBodyForge = true;
        }

        internal static void AcceptCommit()
        {
            if (_preview != null) _committed = _preview.Clone();
        }

        internal static void FinishCommit()
        {
            _savingBodyForge = false;
        }

        internal static bool CancelPreview(Player player, string profile)
        {
            if (player == null || _committed == null || _preview == null || _profile != profile) return false;
            _committed.Apply(player);
            _profile = "";
            _committed = null;
            _preview = null;
            _savingBodyForge = false;
            return true;
        }

        internal static AppearanceState BeforeSave(Player player)
        {
            if (_savingBodyForge || player == null || _committed == null || _preview == null) return null;
            AppearanceState restore = AppearanceState.Capture(player);
            _committed.Apply(player);
            return restore;
        }

        internal static void AfterSave(Player player, AppearanceState restore)
        {
            restore?.Apply(player);
        }

        internal sealed class AppearanceState
        {
            public int Model;
            public string Hair;
            public string Beard;
            public Vector3 Skin;
            public Vector3 HairColor;

            public static AppearanceState Capture(Player player)
            {
                return new AppearanceState
                {
                    Model = player.GetPlayerModel(), Hair = player.GetHair(), Beard = player.GetBeard(),
                    Skin = (Vector3)AccessTools.Field(typeof(Player), "m_skinColor").GetValue(player),
                    HairColor = player.GetHairColor()
                };
            }

            public void Apply(Player player)
            {
                player.SetPlayerModel(Model);
                player.SetHair(Hair);
                player.SetBeard(Beard);
                player.SetSkinColor(Skin);
                player.SetHairColor(HairColor);
            }

            public AppearanceState Clone()
            {
                return (AppearanceState)MemberwiseClone();
            }
        }
    }

    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.SavePlayerData))]
    internal static class AppearancePreviewSavePatch
    {
        private static void Prefix(Player player, out AppearancePreviewGuard.AppearanceState __state)
        {
            __state = AppearancePreviewGuard.BeforeSave(player);
        }

        private static void Postfix(Player player, AppearancePreviewGuard.AppearanceState __state)
        {
            AppearancePreviewGuard.AfterSave(player, __state);
        }
    }
}
