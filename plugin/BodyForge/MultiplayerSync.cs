using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace BodyForge
{
    public static class MultiplayerSync
    {
        private const string ZdoKeyV1 = "BodyForge.Proportions.V1";
        private const string ZdoKeyV2 = "BodyForge.Proportions.V2";
        private const int MaxPayloadBytes = 16384;
        private const int MaxBones = 128;
        private const int MaxAnatomySliders = 64;
        private const float MinScale = 0.05f;
        private const float MaxScale = 10f;
        private const double PollSeconds = 0.25;

        private static readonly Dictionary<int, string> LastPayload = new Dictionary<int, string>();
        private static readonly Dictionary<int, HashSet<string>> AppliedBones = new Dictionary<int, HashSet<string>>();
        private static double _nextPoll;

        public static void Tick()
        {
            if (Time.realtimeSinceStartupAsDouble < _nextPoll) return;
            _nextPoll = Time.realtimeSinceStartupAsDouble + PollSeconds;

            foreach (VisEquipment equipment in VisEquipment.Instances)
            {
                if (equipment == null || !equipment.m_isPlayer) continue;
                ZNetView view = GetView(equipment);
                if (view == null || !view.IsValid() || view.IsOwner()) continue;
                ApplyRemote(equipment, view);
            }
        }

        public static void PublishLocal(ProportionConfig config)
        {
            string legacyPayload = Serialize(config, false);
            string payload = Serialize(config, true);
            foreach (VisEquipment equipment in VisEquipment.Instances)
            {
                if (equipment == null || !equipment.m_isPlayer) continue;
                ZNetView view = GetView(equipment);
                if (view == null || !view.IsValid() || !view.IsOwner()) continue;
                ZDO zdo = view.GetZDO();
                if (zdo == null) continue;
                bool changed = false;
                if (zdo.GetString(ZdoKeyV1, "") != legacyPayload)
                {
                    zdo.Set(ZdoKeyV1, legacyPayload);
                    changed = true;
                }
                if (zdo.GetString(ZdoKeyV2, "") != payload)
                {
                    zdo.Set(ZdoKeyV2, payload);
                    changed = true;
                }
                if (changed)
                    BodyForgePlugin.LogInfo($"published multiplayer proportions ({config?.Bones?.Count ?? 0} bones, {config?.Anatomy?.Count ?? 0} anatomy sliders)");
            }
        }

        private static void ApplyRemote(VisEquipment equipment, ZNetView view)
        {
            ZDO zdo = view.GetZDO();
            if (zdo == null) return;
            string payload = zdo.GetString(ZdoKeyV2, "");
            if (string.IsNullOrEmpty(payload)) payload = zdo.GetString(ZdoKeyV1, "");
            if (string.IsNullOrEmpty(payload)) return;

            int id = equipment.GetInstanceID();
            if (LastPayload.TryGetValue(id, out string previous) && previous == payload) return;
            LastPayload[id] = payload;

            try
            {
                ProportionConfig config = Parse(payload);
                ResetPreviouslyApplied(equipment, id);
                BoneScalerPatch.ApplyRemote(equipment, config);
                if (config.Bones.Count > 0) AppliedBones[id] = new HashSet<string>(config.Bones.Keys);
            }
            catch (Exception e)
            {
                BodyForgePlugin.LogWarn("ignored invalid remote proportions: " + e.Message);
            }
        }

        private static ProportionConfig Parse(string payload)
        {
            if (Encoding.UTF8.GetByteCount(payload) > MaxPayloadBytes)
                throw new FormatException("payload exceeds size limit");
            MiniJson.Node root = MiniJson.Parse(payload);
            if ((int)(root.Get("protocol")?.AsNumber(-1) ?? -1) != 1)
                throw new FormatException("unsupported protocol");
            ProportionConfig config = ProportionConfig.LoadJson(payload);
            if (config.Bones.Count > MaxBones)
                throw new FormatException("too many bones");
            if (config.Anatomy.Count > MaxAnatomySliders)
                throw new FormatException("too many anatomy sliders");
            foreach (KeyValuePair<string, ProportionConfig.BoneSpec> entry in config.Bones)
            {
                if (entry.Key.Length == 0 || entry.Key.Length > 64)
                    throw new FormatException("invalid bone name");
                float[] scale = entry.Value.Scale;
                for (int i = 0; i < scale.Length; i++)
                {
                    if (float.IsNaN(scale[i]) || float.IsInfinity(scale[i]))
                        throw new FormatException("non-finite scale");
                    scale[i] = Mathf.Clamp(scale[i], MinScale, MaxScale);
                }
            }
            foreach (KeyValuePair<string, float> entry in config.Anatomy)
            {
                if (entry.Key.Length == 0 || entry.Key.Length > 64)
                    throw new FormatException("invalid anatomy slider name");
                if (float.IsNaN(entry.Value) || float.IsInfinity(entry.Value))
                    throw new FormatException("non-finite anatomy value");
            }
            return config;
        }

        private static string Serialize(ProportionConfig config, bool includeAnatomy)
        {
            MiniJson.Node root = MiniJson.Node.MakeObject();
            root.Object["protocol"] = MiniJson.Node.MakeNumber(1);
            root.Object["schemaVersion"] = MiniJson.Node.MakeNumber(includeAnatomy ? 2 : 1);
            MiniJson.Node bones = MiniJson.Node.MakeObject();
            if (config?.Bones != null)
            {
                foreach (KeyValuePair<string, ProportionConfig.BoneSpec> entry in config.Bones)
                {
                    MiniJson.Node spec = MiniJson.Node.MakeObject();
                    MiniJson.Node scale = MiniJson.Node.MakeArray();
                    scale.Array.Add(MiniJson.Node.MakeNumber(entry.Value.Scale[0]));
                    scale.Array.Add(MiniJson.Node.MakeNumber(entry.Value.Scale[1]));
                    scale.Array.Add(MiniJson.Node.MakeNumber(entry.Value.Scale[2]));
                    spec.Object["scale"] = scale;
                    bones.Object[entry.Key] = spec;
                }
            }
            root.Object["bones"] = bones;
            if (includeAnatomy)
            {
                MiniJson.Node anatomy = MiniJson.Node.MakeObject();
                foreach (KeyValuePair<string, float> entry in config.Anatomy)
                    anatomy.Object[entry.Key] = MiniJson.Node.MakeNumber(entry.Value);
                root.Object["anatomy"] = anatomy;
            }
            return MiniJson.Serialize(root);
        }

        private static void ResetPreviouslyApplied(VisEquipment equipment, int id)
        {
            if (!AppliedBones.TryGetValue(id, out HashSet<string> names)) return;
            Dictionary<string, Transform> boneMap =
                AccessTools.Field(typeof(VisEquipment), "m_boneMap").GetValue(equipment) as Dictionary<string, Transform>;
            if (boneMap != null)
                foreach (string name in names)
                    if (boneMap.TryGetValue(name, out Transform bone)) bone.localScale = Vector3.one;
            AppliedBones.Remove(id);
        }

        private static ZNetView GetView(VisEquipment equipment)
        {
            return AccessTools.Field(typeof(VisEquipment), "m_nview").GetValue(equipment) as ZNetView;
        }
    }
}
