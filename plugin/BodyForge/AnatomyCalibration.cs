#if BODYFORGE_DEV
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BodyForge
{
    internal static class AnatomyCalibration
    {
        private const double PollSeconds = 0.10;
        private static readonly Dictionary<string, float> Parameters = new Dictionary<string, float>();
        private static readonly HashSet<string> AdditionalFields = new HashSet<string>
        {
            "weight", "musculature", "posture", "head_width", "head_height", "head_depth",
            "eye_spacing", "eye_height", "eye_projection", "nose_width", "nose_height",
            "mouth_width", "mouth_height", "jaw_width", "chin_width", "chin_length",
            "shoulder_width", "chest_projection", "belly", "leg_length"
        };
        private static double _nextPoll;
        private static long _lastWriteTicks;
        private static long _lastProfilesWriteTicks;
        private static long _lastSequence = -1;
        private static long _lastHeartbeat;
        private static GameObject _visualization;
        private static Material _lineMaterial;
        private static Animator _poseAnimator;
        private static float _poseAnimatorSpeed;
        private static bool _poseAnimatorEnabled;
        private static readonly List<BoneTransform> PoseBones = new List<BoneTransform>();

        internal static bool Enabled { get; private set; }
        internal static string Field { get; private set; } = "glutes";
        internal static float PreviewValue { get; private set; } = 1f;
        internal static int Model { get; private set; }
        internal static int Revision { get; private set; }

        private static string CommandPath => Path.Combine(ProportionConfig.TokenFolder, "live-dev-command.json");
        private static string SavedProfilesPath => Path.Combine(ProportionConfig.TokenFolder, "anatomy-calibration.saved.json");

        internal static void Tick()
        {
            UpdateReferencePose();
            if (Time.realtimeSinceStartupAsDouble < _nextPoll) return;
            _nextPoll = Time.realtimeSinceStartupAsDouble + PollSeconds;
            try
            {
                if (Enabled && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _lastHeartbeat > 2000)
                {
                    Enabled = false;
                    Revision++;
                    DestroyVisualization();
                    RestoreReferencePose();
                    BodyForgePlugin.LogInfo("anatomy calibration expired; restored saved character state");
                }
                LoadSavedProfiles();
                if (!File.Exists(CommandPath)) return;
                long writeTicks = File.GetLastWriteTimeUtc(CommandPath).Ticks;
                if (writeTicks == _lastWriteTicks) return;
                _lastWriteTicks = writeTicks;
                ApplyCommand(File.ReadAllText(CommandPath));
            }
            catch (Exception e)
            {
                BodyForgePlugin.LogError("anatomy calibration failed: " + e.Message);
            }
        }

        private static void LoadSavedProfiles()
        {
            if (!File.Exists(SavedProfilesPath)) return;
            long writeTicks = File.GetLastWriteTimeUtc(SavedProfilesPath).Ticks;
            if (writeTicks == _lastProfilesWriteTicks) return;
            AnatomyProfileCalibration.Replace(MiniJson.Parse(File.ReadAllText(SavedProfilesPath)));
            _lastProfilesWriteTicks = writeTicks;
            Revision++;
            BodyForgePlugin.LogInfo("saved anatomy calibration profiles loaded");
        }

        private static void ApplyCommand(string text)
        {
            MiniJson.Node root = MiniJson.Parse(text);
            if ((int)(root.Get("schemaVersion")?.AsNumber(-1) ?? -1) != 1
                || root.Get("command")?.AsString("") != "anatomy.calibrate")
                throw new FormatException("invalid anatomy calibration command");
            long sequence = (long)(root.Get("sequence")?.AsNumber(-1) ?? -1);
            if (sequence <= _lastSequence) return;
            string field = root.Get("field")?.AsString("") ?? "";
            if (field != "hips" && field != "glutes" && field != "thighs"
                && field != "chest" && field != "waist" && field != "abdomen"
                && field != "neck" && field != "shoulders" && field != "upper_back"
                && field != "upper_arms" && field != "forearms" && field != "hands"
                && field != "knees" && field != "calves" && field != "feet"
                && field != "head" && field != "jaw" && field != "cheeks"
                && field != "forehead" && field != "nose" && field != "chin"
                && field != "eyes" && field != "mouth" && field != "ears"
                && field != "body_height" && field != "body_width" && field != "body_depth"
                && !AdditionalFields.Contains(field))
                throw new FormatException("unknown anatomy calibration field");

            int model = (int)(root.Get("model")?.AsNumber(-1) ?? -1);
            if (model != 0 && model != 1) throw new FormatException("invalid anatomy calibration model");
            if (root.Get("commit")?.AsBool(false) ?? false)
            {
                AnatomyProfileCalibration.Replace(root.Get("profiles"));
                BodyForgePlugin.LogInfo("anatomy calibration profiles committed for runtime preview");
            }

            Parameters.Clear();
            MiniJson.Node parameters = root.Get("parameters");
            if (parameters != null && parameters.Type == MiniJson.NodeType.Object)
            {
                foreach (KeyValuePair<string, MiniJson.Node> entry in parameters.Object)
                {
                    float value = (float)entry.Value.AsNumber(float.NaN);
                    if (!float.IsNaN(value) && !float.IsInfinity(value)) Parameters[entry.Key] = value;
                }
            }
            Field = field;
            Model = model;
            Enabled = root.Get("enabled")?.AsBool(false) ?? false;
            PreviewValue = Mathf.Clamp((float)(root.Get("previewValue")?.AsNumber(1) ?? 1), -1f, 1f);
            _lastHeartbeat = (long)(root.Get("heartbeat")?.AsNumber(0) ?? 0);
            _lastSequence = sequence;
            Revision++;
            if (!Enabled)
            {
                DestroyVisualization();
                RestoreReferencePose();
            }
            BodyForgePlugin.LogInfo($"anatomy calibration {Field} {(Enabled ? "enabled" : "disabled")}");
        }

        private static void UpdateReferencePose()
        {
            if (!Enabled)
            {
                RestoreReferencePose();
                return;
            }

            Player player = Player.m_localPlayer;
            if (player == null) return;
            Animator animator = player.GetComponentInChildren<Animator>();
            if (animator == null) return;
            if (_poseAnimator == animator) return;

            VisEquipment equipment = player.GetComponentInChildren<VisEquipment>();
            SkinnedMeshRenderer body = equipment != null ? equipment.m_bodyModel : null;
            Mesh mesh = body != null ? body.sharedMesh : null;
            if (mesh == null || mesh.bindposes.Length == 0 || body.bones.Length != mesh.bindposes.Length) return;

            RestoreReferencePose();
            _poseAnimator = animator;
            _poseAnimatorSpeed = animator.speed;
            _poseAnimatorEnabled = animator.enabled;
            animator.speed = 0f;
            animator.enabled = false;

            Transform[] bones = body.bones;
            Matrix4x4[] bindposes = mesh.bindposes;
            Dictionary<Transform, int> indices = new Dictionary<Transform, int>();
            Matrix4x4[] desiredWorld = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                indices[bones[i]] = i;
                desiredWorld[i] = body.transform.localToWorldMatrix * bindposes[i].inverse;
                PoseBones.Add(new BoneTransform(bones[i]));
            }
            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                if (bone == null) continue;
                Matrix4x4 parentWorld = indices.TryGetValue(bone.parent, out int parentIndex)
                    ? desiredWorld[parentIndex]
                    : bone.parent != null ? bone.parent.localToWorldMatrix : Matrix4x4.identity;
                Matrix4x4 local = parentWorld.inverse * desiredWorld[i];
                bone.localPosition = local.GetColumn(3);
                bone.localRotation = local.rotation;
                bone.localScale = local.lossyScale;
            }
            BodyForgePlugin.LogInfo("anatomy calibration reference pose applied");
        }

        private static void RestoreReferencePose()
        {
            if (_poseAnimator == null && PoseBones.Count == 0) return;
            for (int i = 0; i < PoseBones.Count; i++) PoseBones[i].Restore();
            PoseBones.Clear();
            if (_poseAnimator != null)
            {
                _poseAnimator.speed = _poseAnimatorSpeed;
                _poseAnimator.enabled = _poseAnimatorEnabled;
            }
            _poseAnimator = null;
            BodyForgePlugin.LogInfo("anatomy calibration reference pose released");
        }

        private sealed class BoneTransform
        {
            private readonly Transform _transform;
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;
            private readonly Vector3 _scale;

            internal BoneTransform(Transform transform)
            {
                _transform = transform;
                _position = transform.localPosition;
                _rotation = transform.localRotation;
                _scale = transform.localScale;
            }

            internal void Restore()
            {
                if (_transform == null) return;
                _transform.localPosition = _position;
                _transform.localRotation = _rotation;
                _transform.localScale = _scale;
            }
        }

        internal static float Value(string field, float actual)
        {
            if (!Enabled || field != Field) return actual;
            Player player = Player.m_localPlayer;
            VisEquipment equipment = player != null ? player.GetComponentInChildren<VisEquipment>() : null;
            return equipment != null && equipment.GetModelIndex() == Model ? PreviewValue : actual;
        }

        internal static void ApplyTo(AnatomyDeformer.AnatomyProfile profile, int model)
        {
            if (!Enabled || model != Model) return;
            if (profile.AdditionalSpecs.TryGetValue(Field, out AnatomyDeformer.AnatomySpec spec))
            {
                spec.VerticalPosition = Parameter("verticalPosition", spec.VerticalPosition);
                spec.VerticalRange = Parameter("verticalRange", spec.VerticalRange);
                spec.LateralPosition = Parameter("lateralPosition", spec.LateralPosition);
                spec.LateralRange = Parameter("lateralRange", spec.LateralRange);
                spec.DepthPosition = Parameter("depthPosition", spec.DepthPosition);
                spec.DepthRange = Parameter("depthRange", spec.DepthRange);
                spec.Strength = Parameter("strength", spec.Strength);
                return;
            }
            if (Field == "body_height")
            {
                profile.BodyHeightPosition = Parameter("verticalPosition", profile.BodyHeightPosition);
                profile.BodyHeightRange = Parameter("verticalRange", profile.BodyHeightRange);
                profile.BodyHeightLateralPosition = Parameter("lateralPosition", profile.BodyHeightLateralPosition);
                profile.BodyHeightLateralRange = Parameter("lateralRange", profile.BodyHeightLateralRange);
                profile.BodyHeightDepthPosition = Parameter("depthPosition", profile.BodyHeightDepthPosition);
                profile.BodyHeightDepthRange = Parameter("depthRange", profile.BodyHeightDepthRange);
                profile.BodyHeightStrength = Parameter("strength", profile.BodyHeightStrength);
            }
            else if (Field == "body_width")
            {
                profile.BodyWidthPosition = Parameter("verticalPosition", profile.BodyWidthPosition);
                profile.BodyWidthRange = Parameter("verticalRange", profile.BodyWidthRange);
                profile.BodyWidthLateralPosition = Parameter("lateralPosition", profile.BodyWidthLateralPosition);
                profile.BodyWidthLateralRange = Parameter("lateralRange", profile.BodyWidthLateralRange);
                profile.BodyWidthDepthPosition = Parameter("depthPosition", profile.BodyWidthDepthPosition);
                profile.BodyWidthDepthRange = Parameter("depthRange", profile.BodyWidthDepthRange);
                profile.BodyWidthStrength = Parameter("strength", profile.BodyWidthStrength);
            }
            else if (Field == "body_depth")
            {
                profile.BodyDepthPosition = Parameter("verticalPosition", profile.BodyDepthPosition);
                profile.BodyDepthRange = Parameter("verticalRange", profile.BodyDepthRange);
                profile.BodyDepthLateralPosition = Parameter("lateralPosition", profile.BodyDepthLateralPosition);
                profile.BodyDepthLateralRange = Parameter("lateralRange", profile.BodyDepthLateralRange);
                profile.BodyDepthDepthPosition = Parameter("depthPosition", profile.BodyDepthDepthPosition);
                profile.BodyDepthDepthRange = Parameter("depthRange", profile.BodyDepthDepthRange);
                profile.BodyDepthStrength = Parameter("strength", profile.BodyDepthStrength);
            }
            else if (Field == "head")
            {
                profile.HeadHeight = Parameter("verticalPosition", profile.HeadHeight);
                profile.HeadRange = Parameter("verticalRange", profile.HeadRange);
                profile.HeadLateralPosition = Parameter("lateralPosition", profile.HeadLateralPosition);
                profile.HeadLateralRange = Parameter("lateralRange", profile.HeadLateralRange);
                profile.HeadDepthPosition = Parameter("depthPosition", profile.HeadDepthPosition);
                profile.HeadDepthRange = Parameter("depthRange", profile.HeadDepthRange);
                profile.HeadStrength = Parameter("strength", profile.HeadStrength);
            }
            else if (Field == "jaw")
            {
                profile.JawHeight = Parameter("verticalPosition", profile.JawHeight);
                profile.JawRange = Parameter("verticalRange", profile.JawRange);
                profile.JawLateralPosition = Parameter("lateralPosition", profile.JawLateralPosition);
                profile.JawLateralRange = Parameter("lateralRange", profile.JawLateralRange);
                profile.JawDepthPosition = Parameter("depthPosition", profile.JawDepthPosition);
                profile.JawDepthRange = Parameter("depthRange", profile.JawDepthRange);
                profile.JawStrength = Parameter("strength", profile.JawStrength);
            }
            else if (Field == "cheeks")
            {
                profile.CheeksHeight = Parameter("verticalPosition", profile.CheeksHeight);
                profile.CheeksRange = Parameter("verticalRange", profile.CheeksRange);
                profile.CheeksLateralPosition = Parameter("lateralPosition", profile.CheeksLateralPosition);
                profile.CheeksLateralRange = Parameter("lateralRange", profile.CheeksLateralRange);
                profile.CheeksDepthPosition = Parameter("depthPosition", profile.CheeksDepthPosition);
                profile.CheeksDepthRange = Parameter("depthRange", profile.CheeksDepthRange);
                profile.CheeksStrength = Parameter("strength", profile.CheeksStrength);
            }
            else if (Field == "forehead")
            {
                profile.ForeheadHeight = Parameter("verticalPosition", profile.ForeheadHeight);
                profile.ForeheadRange = Parameter("verticalRange", profile.ForeheadRange);
                profile.ForeheadLateralPosition = Parameter("lateralPosition", profile.ForeheadLateralPosition);
                profile.ForeheadLateralRange = Parameter("lateralRange", profile.ForeheadLateralRange);
                profile.ForeheadDepthPosition = Parameter("depthPosition", profile.ForeheadDepthPosition);
                profile.ForeheadDepthRange = Parameter("depthRange", profile.ForeheadDepthRange);
                profile.ForeheadStrength = Parameter("strength", profile.ForeheadStrength);
            }
            else if (Field == "nose")
            {
                profile.NoseHeight = Parameter("verticalPosition", profile.NoseHeight);
                profile.NoseRange = Parameter("verticalRange", profile.NoseRange);
                profile.NoseLateralPosition = Parameter("lateralPosition", profile.NoseLateralPosition);
                profile.NoseLateralRange = Parameter("lateralRange", profile.NoseLateralRange);
                profile.NoseDepthPosition = Parameter("depthPosition", profile.NoseDepthPosition);
                profile.NoseDepthRange = Parameter("depthRange", profile.NoseDepthRange);
                profile.NoseStrength = Parameter("strength", profile.NoseStrength);
            }
            else if (Field == "chin")
            {
                profile.ChinHeight = Parameter("verticalPosition", profile.ChinHeight);
                profile.ChinRange = Parameter("verticalRange", profile.ChinRange);
                profile.ChinLateralPosition = Parameter("lateralPosition", profile.ChinLateralPosition);
                profile.ChinLateralRange = Parameter("lateralRange", profile.ChinLateralRange);
                profile.ChinDepthPosition = Parameter("depthPosition", profile.ChinDepthPosition);
                profile.ChinDepthRange = Parameter("depthRange", profile.ChinDepthRange);
                profile.ChinStrength = Parameter("strength", profile.ChinStrength);
            }
            else if (Field == "eyes")
            {
                profile.EyesHeight = Parameter("verticalPosition", profile.EyesHeight);
                profile.EyesRange = Parameter("verticalRange", profile.EyesRange);
                profile.EyesLateralPosition = Parameter("lateralPosition", profile.EyesLateralPosition);
                profile.EyesLateralRange = Parameter("lateralRange", profile.EyesLateralRange);
                profile.EyesDepthPosition = Parameter("depthPosition", profile.EyesDepthPosition);
                profile.EyesDepthRange = Parameter("depthRange", profile.EyesDepthRange);
                profile.EyesStrength = Parameter("strength", profile.EyesStrength);
            }
            else if (Field == "mouth")
            {
                profile.MouthHeight = Parameter("verticalPosition", profile.MouthHeight);
                profile.MouthRange = Parameter("verticalRange", profile.MouthRange);
                profile.MouthLateralPosition = Parameter("lateralPosition", profile.MouthLateralPosition);
                profile.MouthLateralRange = Parameter("lateralRange", profile.MouthLateralRange);
                profile.MouthDepthPosition = Parameter("depthPosition", profile.MouthDepthPosition);
                profile.MouthDepthRange = Parameter("depthRange", profile.MouthDepthRange);
                profile.MouthStrength = Parameter("strength", profile.MouthStrength);
            }
            else if (Field == "ears")
            {
                profile.EarsHeight = Parameter("verticalPosition", profile.EarsHeight);
                profile.EarsRange = Parameter("verticalRange", profile.EarsRange);
                profile.EarsLateralPosition = Parameter("lateralPosition", profile.EarsLateralPosition);
                profile.EarsLateralRange = Parameter("lateralRange", profile.EarsLateralRange);
                profile.EarsDepthPosition = Parameter("depthPosition", profile.EarsDepthPosition);
                profile.EarsDepthRange = Parameter("depthRange", profile.EarsDepthRange);
                profile.EarsStrength = Parameter("strength", profile.EarsStrength);
            }
            else if (Field == "neck")
            {
                profile.NeckHeight = Parameter("verticalPosition", profile.NeckHeight);
                profile.NeckRange = Parameter("verticalRange", profile.NeckRange);
                profile.NeckLateralPosition = Parameter("lateralPosition", profile.NeckLateralPosition);
                profile.NeckLateralRange = Parameter("lateralRange", profile.NeckLateralRange);
                profile.NeckDepthPosition = Parameter("depthPosition", profile.NeckDepthPosition);
                profile.NeckDepthRange = Parameter("depthRange", profile.NeckDepthRange);
                profile.NeckStrength = Parameter("strength", profile.NeckStrength);
            }
            else if (Field == "shoulders")
            {
                profile.ShouldersHeight = Parameter("verticalPosition", profile.ShouldersHeight);
                profile.ShouldersRange = Parameter("verticalRange", profile.ShouldersRange);
                profile.ShouldersLateralPosition = Parameter("lateralPosition", profile.ShouldersLateralPosition);
                profile.ShouldersLateralRange = Parameter("lateralRange", profile.ShouldersLateralRange);
                profile.ShouldersDepthPosition = Parameter("depthPosition", profile.ShouldersDepthPosition);
                profile.ShouldersDepthRange = Parameter("depthRange", profile.ShouldersDepthRange);
                profile.ShouldersStrength = Parameter("strength", profile.ShouldersStrength);
            }
            else if (Field == "upper_back")
            {
                profile.UpperBackHeight = Parameter("verticalPosition", profile.UpperBackHeight);
                profile.UpperBackRange = Parameter("verticalRange", profile.UpperBackRange);
                profile.UpperBackLateralPosition = Parameter("lateralPosition", profile.UpperBackLateralPosition);
                profile.UpperBackLateralRange = Parameter("lateralRange", profile.UpperBackLateralRange);
                profile.UpperBackDepthPosition = Parameter("depthPosition", profile.UpperBackDepthPosition);
                profile.UpperBackDepthRange = Parameter("depthRange", profile.UpperBackDepthRange);
                profile.UpperBackStrength = Parameter("strength", profile.UpperBackStrength);
            }
            else if (Field == "upper_arms")
            {
                profile.UpperArmsHeight = Parameter("verticalPosition", profile.UpperArmsHeight);
                profile.UpperArmsRange = Parameter("verticalRange", profile.UpperArmsRange);
                profile.UpperArmsLateralPosition = Parameter("lateralPosition", profile.UpperArmsLateralPosition);
                profile.UpperArmsLateralRange = Parameter("lateralRange", profile.UpperArmsLateralRange);
                profile.UpperArmsDepthPosition = Parameter("depthPosition", profile.UpperArmsDepthPosition);
                profile.UpperArmsDepthRange = Parameter("depthRange", profile.UpperArmsDepthRange);
                profile.UpperArmsStrength = Parameter("strength", profile.UpperArmsStrength);
            }
            else if (Field == "forearms")
            {
                profile.ForearmsHeight = Parameter("verticalPosition", profile.ForearmsHeight);
                profile.ForearmsRange = Parameter("verticalRange", profile.ForearmsRange);
                profile.ForearmsLateralPosition = Parameter("lateralPosition", profile.ForearmsLateralPosition);
                profile.ForearmsLateralRange = Parameter("lateralRange", profile.ForearmsLateralRange);
                profile.ForearmsDepthPosition = Parameter("depthPosition", profile.ForearmsDepthPosition);
                profile.ForearmsDepthRange = Parameter("depthRange", profile.ForearmsDepthRange);
                profile.ForearmsStrength = Parameter("strength", profile.ForearmsStrength);
            }
            else if (Field == "hands")
            {
                profile.HandsHeight = Parameter("verticalPosition", profile.HandsHeight);
                profile.HandsRange = Parameter("verticalRange", profile.HandsRange);
                profile.HandsLateralPosition = Parameter("lateralPosition", profile.HandsLateralPosition);
                profile.HandsLateralRange = Parameter("lateralRange", profile.HandsLateralRange);
                profile.HandsDepthPosition = Parameter("depthPosition", profile.HandsDepthPosition);
                profile.HandsDepthRange = Parameter("depthRange", profile.HandsDepthRange);
                profile.HandsStrength = Parameter("strength", profile.HandsStrength);
            }
            else if (Field == "knees")
            {
                profile.KneesHeight = Parameter("verticalPosition", profile.KneesHeight);
                profile.KneesRange = Parameter("verticalRange", profile.KneesRange);
                profile.KneesLateralPosition = Parameter("lateralPosition", profile.KneesLateralPosition);
                profile.KneesLateralRange = Parameter("lateralRange", profile.KneesLateralRange);
                profile.KneesDepthPosition = Parameter("depthPosition", profile.KneesDepthPosition);
                profile.KneesDepthRange = Parameter("depthRange", profile.KneesDepthRange);
                profile.KneesStrength = Parameter("strength", profile.KneesStrength);
            }
            else if (Field == "calves")
            {
                profile.CalvesHeight = Parameter("verticalPosition", profile.CalvesHeight);
                profile.CalvesRange = Parameter("verticalRange", profile.CalvesRange);
                profile.CalvesLateralPosition = Parameter("lateralPosition", profile.CalvesLateralPosition);
                profile.CalvesLateralRange = Parameter("lateralRange", profile.CalvesLateralRange);
                profile.CalvesDepthPosition = Parameter("depthPosition", profile.CalvesDepthPosition);
                profile.CalvesDepthRange = Parameter("depthRange", profile.CalvesDepthRange);
                profile.CalvesStrength = Parameter("strength", profile.CalvesStrength);
            }
            else if (Field == "feet")
            {
                profile.FeetHeight = Parameter("verticalPosition", profile.FeetHeight);
                profile.FeetRange = Parameter("verticalRange", profile.FeetRange);
                profile.FeetLateralPosition = Parameter("lateralPosition", profile.FeetLateralPosition);
                profile.FeetLateralRange = Parameter("lateralRange", profile.FeetLateralRange);
                profile.FeetDepthPosition = Parameter("depthPosition", profile.FeetDepthPosition);
                profile.FeetDepthRange = Parameter("depthRange", profile.FeetDepthRange);
                profile.FeetStrength = Parameter("strength", profile.FeetStrength);
            }
            else if (Field == "chest")
            {
                profile.ChestHeight = Parameter("verticalPosition", profile.ChestHeight);
                profile.ChestRange = Parameter("verticalRange", profile.ChestRange);
                profile.ChestLateralPosition = Parameter("lateralPosition", profile.ChestLateralPosition);
                profile.ChestLateralRange = Parameter("lateralRange", profile.ChestLateralRange);
                profile.ChestDepthPosition = Parameter("depthPosition", profile.ChestDepthPosition);
                profile.ChestDepthRange = Parameter("depthRange", profile.ChestDepthRange);
                profile.ChestStrength = Parameter("strength", profile.ChestStrength);
                profile.ChestWidthRatio = Parameter("widthRatio", profile.ChestWidthRatio);
                profile.ChestDepthRatio = Parameter("depthRatio", profile.ChestDepthRatio);
            }
            else if (Field == "waist")
            {
                profile.WaistHeight = Parameter("verticalPosition", profile.WaistHeight);
                profile.WaistRange = Parameter("verticalRange", profile.WaistRange);
                profile.WaistLateralPosition = Parameter("lateralPosition", profile.WaistLateralPosition);
                profile.WaistLateralRange = Parameter("lateralRange", profile.WaistLateralRange);
                profile.WaistDepthPosition = Parameter("depthPosition", profile.WaistDepthPosition);
                profile.WaistDepthRange = Parameter("depthRange", profile.WaistDepthRange);
                profile.WaistStrength = Parameter("strength", profile.WaistStrength);
            }
            else if (Field == "abdomen")
            {
                profile.AbdomenHeight = Parameter("verticalPosition", profile.AbdomenHeight);
                profile.AbdomenRange = Parameter("verticalRange", profile.AbdomenRange);
                profile.AbdomenLateralPosition = Parameter("lateralPosition", profile.AbdomenLateralPosition);
                profile.AbdomenLateralRange = Parameter("lateralRange", profile.AbdomenLateralRange);
                profile.AbdomenDepthPosition = Parameter("depthPosition", profile.AbdomenDepthPosition);
                profile.AbdomenDepthRange = Parameter("depthRange", profile.AbdomenDepthRange);
                profile.AbdomenStrength = Parameter("strength", profile.AbdomenStrength);
            }
            else if (Field == "hips")
            {
                profile.HipsHeight = Parameter("verticalPosition", profile.HipsHeight);
                profile.HipsRange = Parameter("verticalRange", profile.HipsRange);
                profile.HipsLateralPosition = Parameter("lateralPosition", profile.HipsLateralPosition);
                profile.HipsLateralRange = Parameter("lateralRange", profile.HipsLateralRange);
                profile.HipsStrength = Parameter("strength", profile.HipsStrength);
                profile.HipsInnerRadius = Parameter("innerRadius", profile.HipsInnerRadius);
                profile.HipsOuterRadius = Parameter("outerRadius", profile.HipsOuterRadius);
                profile.HipsDepthPosition = Parameter("depthPosition", profile.HipsDepthPosition);
                profile.HipsDepthRange = Parameter("depthRange", profile.HipsDepthRange);
            }
            else if (Field == "glutes")
            {
                profile.GlutesHeight = Parameter("verticalPosition", profile.GlutesHeight);
                profile.GlutesRange = Parameter("verticalRange", profile.GlutesRange);
                profile.GlutesStrength = Parameter("strength", profile.GlutesStrength);
                profile.GlutesLateralPosition = Parameter("lateralPosition", profile.GlutesLateralPosition);
                profile.GlutesLateralRange = Parameter("lateralRange", profile.GlutesLateralRange);
                profile.GlutesDepthPosition = Parameter("depthPosition", profile.GlutesDepthPosition);
                profile.GlutesDepthRange = Parameter("depthRange", profile.GlutesDepthRange);
            }
            else
            {
                profile.ThighHeight = Parameter("verticalPosition", profile.ThighHeight);
                profile.ThighRange = Parameter("verticalRange", profile.ThighRange);
                profile.ThighLateralPosition = Parameter("lateralPosition", profile.ThighLateralPosition);
                profile.ThighLateralRange = Parameter("lateralRange", profile.ThighLateralRange);
                profile.ThighDepthPosition = Parameter("depthPosition", profile.ThighDepthPosition);
                profile.ThighDepthRange = Parameter("depthRange", profile.ThighDepthRange);
                profile.ThighInnerRadius = Parameter("innerRadius", profile.ThighInnerRadius);
                profile.ThighStrength = Parameter("strength", profile.ThighStrength);
            }
        }

        internal static void Visualize(SkinnedMeshRenderer body, Bounds bounds,
                                       Vector3 right, Vector3 up, Vector3 forward,
                                       float halfDepth, float height,
                                       AnatomyDeformer.AnatomyProfile profile)
        {
            if (!Enabled || body == null) return;
            DestroyVisualization();
            _visualization = new GameObject("BodyForge anatomy calibration");
            _visualization.transform.SetParent(body.transform, false);
            Color color = new Color(1f, 0.2f, 0.05f, 0.9f);
            if (profile.AdditionalSpecs.TryGetValue(Field, out AnatomyDeformer.AnatomySpec spec))
            {
                if (spec.Bilateral)
                    DrawBilateral(bounds, right, up, forward, halfDepth, height,
                        spec.VerticalPosition, spec.VerticalRange, spec.LateralPosition,
                        spec.LateralRange, spec.DepthPosition, spec.DepthRange, color);
                else
                    DrawCentral(bounds, right, up, forward, halfDepth, height,
                        spec.VerticalPosition, spec.VerticalRange, spec.LateralPosition,
                        spec.LateralRange, spec.DepthPosition, spec.DepthRange, color);
                return;
            }
            if (Field == "body_height")
            {
                DrawCentral(bounds, right, up, forward, halfDepth, height,
                    profile.BodyHeightPosition, profile.BodyHeightRange,
                    profile.BodyHeightLateralPosition, profile.BodyHeightLateralRange,
                    profile.BodyHeightDepthPosition, profile.BodyHeightDepthRange, color);
            }
            else if (Field == "body_width")
            {
                DrawCentral(bounds, right, up, forward, halfDepth, height,
                    profile.BodyWidthPosition, profile.BodyWidthRange,
                    profile.BodyWidthLateralPosition, profile.BodyWidthLateralRange,
                    profile.BodyWidthDepthPosition, profile.BodyWidthDepthRange, color);
            }
            else if (Field == "body_depth")
            {
                DrawCentral(bounds, right, up, forward, halfDepth, height,
                    profile.BodyDepthPosition, profile.BodyDepthRange,
                    profile.BodyDepthLateralPosition, profile.BodyDepthLateralRange,
                    profile.BodyDepthDepthPosition, profile.BodyDepthDepthRange, color);
            }
            else if (Field == "head")
            {
                Vector3 center = bounds.center + up * height * (profile.HeadHeight - 0.5f)
                    + right * height * profile.HeadLateralPosition
                    + forward * halfDepth * profile.HeadDepthPosition;
                DrawEllipsoid(center, right, up, -forward,
                    height * profile.HeadLateralRange, height * profile.HeadRange,
                    halfDepth * profile.HeadDepthRange, color);
            }
            else if (Field == "jaw")
            {
                DrawBilateral(bounds, right, up, forward, halfDepth, height,
                    profile.JawHeight, profile.JawRange,
                    profile.JawLateralPosition, profile.JawLateralRange,
                    profile.JawDepthPosition, profile.JawDepthRange, color);
            }
            else if (Field == "cheeks")
            {
                DrawBilateral(bounds, right, up, forward, halfDepth, height,
                    profile.CheeksHeight, profile.CheeksRange,
                    profile.CheeksLateralPosition, profile.CheeksLateralRange,
                    profile.CheeksDepthPosition, profile.CheeksDepthRange, color);
            }
            else if (Field == "forehead")
            {
                DrawCentral(bounds, right, up, forward, halfDepth, height,
                    profile.ForeheadHeight, profile.ForeheadRange,
                    profile.ForeheadLateralPosition, profile.ForeheadLateralRange,
                    profile.ForeheadDepthPosition, profile.ForeheadDepthRange, color);
            }
            else if (Field == "nose")
            {
                DrawCentral(bounds, right, up, forward, halfDepth, height,
                    profile.NoseHeight, profile.NoseRange,
                    profile.NoseLateralPosition, profile.NoseLateralRange,
                    profile.NoseDepthPosition, profile.NoseDepthRange, color);
            }
            else if (Field == "chin")
            {
                DrawCentral(bounds, right, up, forward, halfDepth, height,
                    profile.ChinHeight, profile.ChinRange,
                    profile.ChinLateralPosition, profile.ChinLateralRange,
                    profile.ChinDepthPosition, profile.ChinDepthRange, color);
            }
            else if (Field == "eyes")
            {
                DrawBilateral(bounds, right, up, forward, halfDepth, height,
                    profile.EyesHeight, profile.EyesRange,
                    profile.EyesLateralPosition, profile.EyesLateralRange,
                    profile.EyesDepthPosition, profile.EyesDepthRange, color);
            }
            else if (Field == "mouth")
            {
                DrawCentral(bounds, right, up, forward, halfDepth, height,
                    profile.MouthHeight, profile.MouthRange,
                    profile.MouthLateralPosition, profile.MouthLateralRange,
                    profile.MouthDepthPosition, profile.MouthDepthRange, color);
            }
            else if (Field == "ears")
            {
                DrawBilateral(bounds, right, up, forward, halfDepth, height,
                    profile.EarsHeight, profile.EarsRange,
                    profile.EarsLateralPosition, profile.EarsLateralRange,
                    profile.EarsDepthPosition, profile.EarsDepthRange, color);
            }
            else if (Field == "neck")
            {
                Vector3 center = bounds.center + up * height * (profile.NeckHeight - 0.5f)
                    + right * height * profile.NeckLateralPosition
                    + forward * halfDepth * profile.NeckDepthPosition;
                DrawEllipsoid(center, right, up, -forward,
                    height * profile.NeckLateralRange, height * profile.NeckRange,
                    halfDepth * profile.NeckDepthRange, color);
            }
            else if (Field == "shoulders")
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 center = bounds.center + up * height * (profile.ShouldersHeight - 0.5f)
                        + right * side * height * profile.ShouldersLateralPosition
                        + forward * halfDepth * profile.ShouldersDepthPosition;
                    DrawEllipsoid(center, right, up, -forward,
                        height * profile.ShouldersLateralRange, height * profile.ShouldersRange,
                        halfDepth * profile.ShouldersDepthRange, color);
                }
            }
            else if (Field == "upper_back")
            {
                Vector3 center = bounds.center + up * height * (profile.UpperBackHeight - 0.5f)
                    + right * height * profile.UpperBackLateralPosition
                    - forward * halfDepth * profile.UpperBackDepthPosition;
                DrawEllipsoid(center, right, up, -forward,
                    height * profile.UpperBackLateralRange, height * profile.UpperBackRange,
                    halfDepth * profile.UpperBackDepthRange, color);
            }
            else if (Field == "upper_arms")
            {
                DrawBilateral(bounds, right, up, forward, halfDepth, height,
                    profile.UpperArmsHeight, profile.UpperArmsRange,
                    profile.UpperArmsLateralPosition, profile.UpperArmsLateralRange,
                    profile.UpperArmsDepthPosition, profile.UpperArmsDepthRange, color);
            }
            else if (Field == "forearms")
            {
                DrawBilateral(bounds, right, up, forward, halfDepth, height,
                    profile.ForearmsHeight, profile.ForearmsRange,
                    profile.ForearmsLateralPosition, profile.ForearmsLateralRange,
                    profile.ForearmsDepthPosition, profile.ForearmsDepthRange, color);
            }
            else if (Field == "hands")
            {
                DrawBilateral(bounds, right, up, forward, halfDepth, height,
                    profile.HandsHeight, profile.HandsRange,
                    profile.HandsLateralPosition, profile.HandsLateralRange,
                    profile.HandsDepthPosition, profile.HandsDepthRange, color);
            }
            else if (Field == "knees")
            {
                DrawBilateralPosterior(bounds, right, up, forward, halfDepth, height,
                    profile.KneesHeight, profile.KneesRange,
                    profile.KneesLateralPosition, profile.KneesLateralRange,
                    profile.KneesDepthPosition, profile.KneesDepthRange, color);
            }
            else if (Field == "calves")
            {
                DrawBilateralPosterior(bounds, right, up, forward, halfDepth, height,
                    profile.CalvesHeight, profile.CalvesRange,
                    profile.CalvesLateralPosition, profile.CalvesLateralRange,
                    profile.CalvesDepthPosition, profile.CalvesDepthRange, color);
            }
            else if (Field == "feet")
            {
                DrawBilateralPosterior(bounds, right, up, forward, halfDepth, height,
                    profile.FeetHeight, profile.FeetRange,
                    profile.FeetLateralPosition, profile.FeetLateralRange,
                    profile.FeetDepthPosition, profile.FeetDepthRange, color);
            }
            else if (Field == "chest")
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 center = bounds.center + up * height * (profile.ChestHeight - 0.5f)
                        + right * side * height * profile.ChestLateralPosition
                        + forward * halfDepth * profile.ChestDepthPosition;
                    DrawEllipsoid(center, right, up, -forward,
                        height * profile.ChestLateralRange, height * profile.ChestRange,
                        halfDepth * profile.ChestDepthRange, color);
                }
            }
            else if (Field == "waist")
            {
                Vector3 center = bounds.center + up * height * (profile.WaistHeight - 0.5f)
                    + right * height * profile.WaistLateralPosition
                    + forward * halfDepth * profile.WaistDepthPosition;
                DrawEllipsoid(center, right, up, -forward,
                    height * profile.WaistLateralRange, height * profile.WaistRange,
                    halfDepth * profile.WaistDepthRange, color);
            }
            else if (Field == "abdomen")
            {
                Vector3 center = bounds.center + up * height * (profile.AbdomenHeight - 0.5f)
                    + right * height * profile.AbdomenLateralPosition
                    + forward * halfDepth * profile.AbdomenDepthPosition;
                DrawEllipsoid(center, right, up, forward,
                    height * profile.AbdomenLateralRange, height * profile.AbdomenRange,
                    halfDepth * profile.AbdomenDepthRange, color);
            }
            else if (Field == "glutes")
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 center = bounds.center
                        + up * height * (profile.GlutesHeight - 0.5f)
                        + right * side * height * profile.GlutesLateralPosition
                        - forward * halfDepth * profile.GlutesDepthPosition;
                    DrawEllipsoid(center, right, up, -forward,
                        height * profile.GlutesLateralRange,
                        height * profile.GlutesRange,
                        halfDepth * profile.GlutesDepthRange, color);
                }
            }
            else if (Field == "thighs")
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 center = bounds.center
                        + up * height * (profile.ThighHeight - 0.5f)
                        + right * side * height * profile.ThighLateralPosition
                        - forward * halfDepth * profile.ThighDepthPosition;
                    DrawEllipsoid(center, right, up, -forward,
                        height * profile.ThighLateralRange, height * profile.ThighRange,
                        halfDepth * profile.ThighDepthRange, color);
                }
            }
            else
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 center = bounds.center + up * height * (profile.HipsHeight - 0.5f)
                        + right * side * height * profile.HipsLateralPosition
                        - forward * halfDepth * profile.HipsDepthPosition;
                    DrawEllipsoid(center, right, up, -forward,
                        height * profile.HipsLateralRange, height * profile.HipsRange,
                        halfDepth * profile.HipsDepthRange, color);
                }
            }
        }

        private static float Parameter(string name, float fallback)
        {
            return Parameters.TryGetValue(name, out float value) ? Mathf.Clamp(value, -1f, 1f) : fallback;
        }

        private static void DrawBilateral(Bounds bounds, Vector3 right, Vector3 up, Vector3 forward,
                                          float halfDepth, float height, float verticalPosition,
                                          float verticalRange, float lateralPosition, float lateralRange,
                                          float depthPosition, float depthRange, Color color)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 center = bounds.center + up * height * (verticalPosition - 0.5f)
                    + right * side * height * lateralPosition
                    + forward * halfDepth * depthPosition;
                DrawEllipsoid(center, right, up, -forward,
                    height * lateralRange, height * verticalRange,
                    halfDepth * depthRange, color);
            }
        }

        private static void DrawCentral(Bounds bounds, Vector3 right, Vector3 up, Vector3 forward,
                                        float halfDepth, float height, float verticalPosition,
                                        float verticalRange, float lateralPosition, float lateralRange,
                                        float depthPosition, float depthRange, Color color)
        {
            Vector3 center = bounds.center + up * height * (verticalPosition - 0.5f)
                + right * height * lateralPosition + forward * halfDepth * depthPosition;
            DrawEllipsoid(center, right, up, -forward,
                height * lateralRange, height * verticalRange,
                halfDepth * depthRange, color);
        }

        private static void DrawBilateralPosterior(Bounds bounds, Vector3 right, Vector3 up, Vector3 forward,
                                                   float halfDepth, float height, float verticalPosition,
                                                   float verticalRange, float lateralPosition, float lateralRange,
                                                   float depthPosition, float depthRange, Color color)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 center = bounds.center + up * height * (verticalPosition - 0.5f)
                    + right * side * height * lateralPosition
                    - forward * halfDepth * depthPosition;
                DrawEllipsoid(center, right, up, -forward,
                    height * lateralRange, height * verticalRange,
                    halfDepth * depthRange, color);
            }
        }

        private static void DrawEllipsoid(Vector3 center, Vector3 axisX, Vector3 axisY, Vector3 axisZ,
                                          float radiusX, float radiusY, float radiusZ, Color color)
        {
            radiusX = Mathf.Max(Mathf.Abs(radiusX), 0.00001f);
            radiusY = Mathf.Max(Mathf.Abs(radiusY), 0.00001f);
            radiusZ = Mathf.Max(Mathf.Abs(radiusZ) * 2f, 0.00001f);
            DrawRing(center, axisX, axisY, radiusX, radiusY, color);
            DrawRing(center, axisX, axisZ, radiusX, radiusZ, color);
            DrawRing(center, axisY, axisZ, radiusY, radiusZ, color);

            // Front/rear slices make the volume readable even from a near-frontal camera.
            for (int side = -1; side <= 1; side += 2)
            {
                const float offset = 0.62f;
                float sliceScale = Mathf.Sqrt(1f - offset * offset);
                DrawRing(center + axisZ * radiusZ * offset * side,
                    axisX, axisY, radiusX * sliceScale, radiusY * sliceScale, color);
            }

            Color depthColor = new Color(1f, 0.8f, 0.1f, 1f);
            DrawSegment(center - axisZ * radiusZ, center + axisZ * radiusZ, depthColor, 0.014f);
        }

        private static void DrawRing(Vector3 center, Vector3 axisA, Vector3 axisB,
                                     float radiusA, float radiusB, Color color)
        {
            const int segments = 48;
            GameObject ring = new GameObject("field ring");
            ring.transform.SetParent(_visualization.transform, false);
            LineRenderer line = ring.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segments;
            line.widthMultiplier = 0.008f;
            line.startColor = color;
            line.endColor = color;
            line.sharedMaterial = LineMaterial();
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, center + axisA * Mathf.Cos(angle) * radiusA
                    + axisB * Mathf.Sin(angle) * radiusB);
            }
        }

        private static void DrawSegment(Vector3 start, Vector3 end, Color color, float width)
        {
            GameObject segment = new GameObject("depth axis");
            segment.transform.SetParent(_visualization.transform, false);
            LineRenderer line = segment.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.startColor = color;
            line.endColor = color;
            line.sharedMaterial = LineMaterial();
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private static Material LineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
            if (shader != null) _lineMaterial = new Material(shader);
            return _lineMaterial;
        }

        private static void DestroyVisualization()
        {
            if (_visualization != null) UnityEngine.Object.Destroy(_visualization);
            _visualization = null;
        }
    }
}
#endif
