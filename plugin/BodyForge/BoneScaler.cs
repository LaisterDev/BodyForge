// BodyForge — applies per-bone localScale from the character's
// <char>.vhforges.json to the local player's skeleton every time a
// VisEquipment is created (respawn, teleport, world load).
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BodyForge
{
    [HarmonyPatch(typeof(VisEquipment), "Awake")]
    public static class BoneScalerPatch
    {
        private static readonly HashSet<string> MissingWarned = new HashSet<string>();

        public static void Postfix(VisEquipment __instance)
        {
            try
            {
                Apply(__instance);
            }
            catch (Exception e)
            {
                BodyForgePlugin.LogError("apply failed: " + e);
            }
        }

        private static void Apply(VisEquipment ve)
        {
            if (ve == null || !ve.m_isPlayer) return;
            // Local disk configuration is only authoritative for the body this client owns.
            ZNetView nview = AccessTools.Field(typeof(VisEquipment), "m_nview").GetValue(ve) as ZNetView;
            if (nview == null || !nview.IsOwner()) return;

            ProportionConfig cfg = ProportionConfig.Reload();
            if (cfg == null || cfg.Bones == null || cfg.Bones.Count == 0) return;

            ApplyToBoneMap(ve, cfg, true);
            MultiplayerSync.PublishLocal(cfg);
        }

        public static bool ApplyLive(ProportionConfig cfg)
        {
            if (cfg == null || cfg.Bones == null) return false;
            foreach (VisEquipment ve in VisEquipment.Instances)
            {
                if (ve == null || !ve.m_isPlayer) continue;
                ZNetView nview = AccessTools.Field(typeof(VisEquipment), "m_nview").GetValue(ve) as ZNetView;
                if (nview == null || !nview.IsOwner()) continue;
                return ApplyToBoneMap(ve, cfg, false);
            }
            return false;
        }

        public static bool ApplyPreview(VisEquipment ve, ProportionConfig cfg)
        {
            return ApplyToBoneMap(ve, cfg, true);
        }

        public static bool ApplyRemote(VisEquipment ve, ProportionConfig cfg)
        {
            return ApplyToBoneMap(ve, cfg, false);
        }

        private static bool ApplyToBoneMap(VisEquipment ve, ProportionConfig cfg, bool warnMissing)
        {
            if (ve == null || cfg == null || cfg.Bones == null) return false;
            Dictionary<string, Transform> boneMap =
                AccessTools.Field(typeof(VisEquipment), "m_boneMap").GetValue(ve) as Dictionary<string, Transform>;
            if (boneMap == null || boneMap.Count == 0) return false;
            foreach (KeyValuePair<string, ProportionConfig.BoneSpec> kv in cfg.Bones)
            {
                if (!kv.Value.Enabled) continue;
                if (!boneMap.TryGetValue(kv.Key, out Transform bone))
                {
                    if (warnMissing && MissingWarned.Add(kv.Key))
                        BodyForgePlugin.LogWarn($"bone '{kv.Key}' not found in model — ignored");
                    continue;
                }
                float[] sc = kv.Value.Scale;
                bone.localScale = new Vector3(sc[0], sc[1], sc[2]);
            }
            return true;
        }
    }
}
