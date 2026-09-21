using System;
using HarmonyLib;

namespace BodyForge
{
    [HarmonyPatch(typeof(FejdStartup), "SetupCharacterPreview")]
    public static class MenuPreviewPatch
    {
        public static void Postfix(FejdStartup __instance, PlayerProfile profile)
        {
            if (profile == null) return;
            try
            {
                Player preview = __instance.GetPreviewPlayer();
                VisEquipment equipment = preview?.GetComponentInChildren<VisEquipment>();
                BoneScalerPatch.ApplyPreview(equipment, ProportionConfig.LoadForProfile(profile.GetFilename()));
            }
            catch (Exception e)
            {
                BodyForgePlugin.LogError("menu preview apply failed: " + e.Message);
            }
        }
    }
}
