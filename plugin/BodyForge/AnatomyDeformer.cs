using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BodyForge
{
    public static class AnatomyDeformer
    {
        private const double PollSeconds = 0.25;
        private const float CapeAnchorDrop = 0.05f;

        private static readonly Dictionary<int, ProportionConfig> Configs = new Dictionary<int, ProportionConfig>();
        private static readonly Dictionary<int, string> Signatures = new Dictionary<int, string>();
        private static readonly Dictionary<int, MeshState> Meshes = new Dictionary<int, MeshState>();
        private static readonly Dictionary<int, CapeAnchorState> CapeAnchors = new Dictionary<int, CapeAnchorState>();
        private static double _nextPoll;

        public static void RememberAndApply(VisEquipment equipment, ProportionConfig config)
        {
            if (equipment == null || config == null) return;
            Configs[equipment.GetInstanceID()] = config;
            Apply(equipment, config);
            Signatures[equipment.GetInstanceID()] = Signature(equipment, config);
        }

        public static void Tick()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < _nextPoll) return;
            _nextPoll = now + PollSeconds;
            foreach (VisEquipment equipment in VisEquipment.Instances)
            {
                if (equipment == null || !equipment.m_isPlayer) continue;
                int id = equipment.GetInstanceID();
                if (!Configs.TryGetValue(id, out ProportionConfig config)) continue;
                string signature = Signature(equipment, config);
                if (Signatures.TryGetValue(id, out string previous) && previous == signature) continue;
                Apply(equipment, config);
                Signatures[id] = signature;
            }
        }

        public static void EnsureBodyMesh(VisEquipment equipment)
        {
            if (equipment == null || !Configs.TryGetValue(equipment.GetInstanceID(), out ProportionConfig config)
                || !HasChanges(config) || equipment.m_bodyModel == null) return;
            if (Meshes.TryGetValue(equipment.m_bodyModel.GetInstanceID(), out MeshState state)
                && state.Deformed != null && equipment.m_bodyModel.sharedMesh != state.Deformed)
                equipment.m_bodyModel.sharedMesh = state.Deformed;
        }

        private static void Apply(VisEquipment equipment, ProportionConfig config)
        {
            SkinnedMeshRenderer body = equipment.m_bodyModel;
            if (body == null) return;
            if (!HasChanges(config))
            {
                RestoreAll(equipment);
                ApplyCapeAnchors(equipment, body, -body.transform.root.up * CapeAnchorDrop);
                return;
            }

            int model = equipment.GetModelIndex();
            Mesh bodySource = model >= 0 && model < equipment.m_models.Length
                ? equipment.m_models[model].m_mesh : body.sharedMesh;
            if (bodySource == null || !bodySource.isReadable) return;
            AnatomyProfile profile = AnatomyProfile.ForModel(model);

            Mesh deformedBody = BuildMesh(body, bodySource, body, bodySource.bounds, config, profile);
            Vector3 capeOffset = deformedBody != null
                ? UpperBackOffset(body, bodySource, deformedBody, profile) : Vector3.zero;
            capeOffset -= body.transform.root.up * CapeAnchorDrop;
            if (deformedBody != null) ReplaceMesh(body, bodySource, deformedBody);

            foreach (SkinnedMeshRenderer renderer in equipment.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (IsMagicaCloth(renderer))
                {
                    RestoreRenderer(renderer);
                    ApplyCapeAnchor(renderer, body, capeOffset);
                    continue;
                }
                if (renderer == body) continue;
                Mesh source = renderer == body ? bodySource : OriginalMesh(renderer);
                if (source == null || !source.isReadable || source.vertexCount == 0) continue;
                Mesh deformed = BuildMesh(renderer, source, body, bodySource.bounds, config, profile);
                if (deformed == null) continue;
                ReplaceMesh(renderer, source, deformed);
            }
            CleanupCapeAnchors();
        }

        private static void ApplyCapeAnchors(VisEquipment equipment, SkinnedMeshRenderer body,
                                             Vector3 worldOffset)
        {
            foreach (SkinnedMeshRenderer renderer in equipment.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!IsMagicaCloth(renderer)) continue;
                RestoreRenderer(renderer);
                ApplyCapeAnchor(renderer, body, worldOffset);
            }
            CleanupCapeAnchors();
        }

        private static Vector3 UpperBackOffset(SkinnedMeshRenderer body, Mesh original, Mesh deformed,
                                               AnatomyProfile profile)
        {
            Vector3[] source = original.vertices;
            Vector3[] result = deformed.vertices;
            if (source.Length != result.Length) return Vector3.zero;
            Bounds bounds = original.bounds;
            Vector3 right = body.transform.InverseTransformDirection(body.transform.root.right).normalized;
            Vector3 up = body.transform.InverseTransformDirection(body.transform.root.up).normalized;
            Vector3 forward = body.transform.InverseTransformDirection(body.transform.root.forward).normalized;
            float halfDepth = Mathf.Max(ProjectedExtent(bounds.extents, forward), 0.0001f);
            float height = Mathf.Max(ProjectedExtent(bounds.extents, up) * 2f, 0.0001f);
            Vector3 localOffset = Vector3.zero;
            float totalWeight = 0f;
            for (int i = 0; i < source.Length; i++)
            {
                Vector3 relative = source[i] - bounds.center;
                float vertical = Vector3.Dot(relative, up) / height + 0.5f;
                float lateral = Vector3.Dot(relative, right) / height;
                float posterior = Vector3.Dot(relative, -forward) / halfDepth;
                if (posterior <= 0f) continue;
                float weight = Bell(vertical, profile.UpperBackHeight, profile.UpperBackRange)
                    * Bell(lateral, profile.UpperBackLateralPosition, profile.UpperBackLateralRange)
                    * DepthBell(posterior, profile.UpperBackDepthPosition, profile.UpperBackDepthRange);
                if (weight <= 0.001f) continue;
                localOffset += (result[i] - source[i]) * weight;
                totalWeight += weight;
            }
            return totalWeight > 0f
                ? body.transform.TransformVector(localOffset / totalWeight) : Vector3.zero;
        }

        private static Mesh BuildMesh(SkinnedMeshRenderer renderer, Mesh source, SkinnedMeshRenderer body,
                                      Bounds bodyBounds, ProportionConfig config, AnatomyProfile profile)
        {
            Vector3[] vertices = source.vertices;
            Vector3 right = body.transform.InverseTransformDirection(renderer.transform.root.right).normalized;
            Vector3 up = body.transform.InverseTransformDirection(renderer.transform.root.up).normalized;
            Vector3 forward = body.transform.InverseTransformDirection(renderer.transform.root.forward).normalized;
            float halfDepth = Mathf.Max(ProjectedExtent(bodyBounds.extents, forward), 0.0001f);
            float height = Mathf.Max(ProjectedExtent(bodyBounds.extents, up) * 2f, 0.0001f);
#if BODYFORGE_DEV
            if (renderer == body)
                AnatomyCalibration.Visualize(body, bodyBounds, right, up, forward, halfDepth, height, profile);
#endif
            bool changed = false;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = renderer.transform.TransformPoint(vertices[i]);
                Vector3 point = body.transform.InverseTransformPoint(world);
                Vector3 relative = point - bodyBounds.center;
                float vertical = Vector3.Dot(relative, up) / height + 0.5f;
                float lateral = Vector3.Dot(relative, right);
                float posterior = Vector3.Dot(relative, -forward);
                float anterior = -posterior;
                Vector3 delta = Vector3.zero;

                float bodyHeight = Value(config, "body_height");
                if (Mathf.Abs(bodyHeight) > 0.0001f)
                {
                    float band = Bell(vertical, profile.BodyHeightPosition, profile.BodyHeightRange)
                        * Bell(lateral / height, profile.BodyHeightLateralPosition, profile.BodyHeightLateralRange)
                        * DepthBell(anterior / halfDepth, profile.BodyHeightDepthPosition, profile.BodyHeightDepthRange);
                    delta += up * height * vertical * profile.BodyHeightStrength * bodyHeight * band;
                }

                float bodyWidth = Value(config, "body_width");
                if (Mathf.Abs(bodyWidth) > 0.0001f)
                {
                    float band = Bell(vertical, profile.BodyWidthPosition, profile.BodyWidthRange)
                        * Bell(lateral / height, profile.BodyWidthLateralPosition, profile.BodyWidthLateralRange)
                        * DepthBell(anterior / halfDepth, profile.BodyWidthDepthPosition, profile.BodyWidthDepthRange);
                    delta += right * lateral * profile.BodyWidthStrength * bodyWidth * band;
                }

                float bodyDepth = Value(config, "body_depth");
                if (Mathf.Abs(bodyDepth) > 0.0001f)
                {
                    float band = Bell(vertical, profile.BodyDepthPosition, profile.BodyDepthRange)
                        * Bell(lateral / height, profile.BodyDepthLateralPosition, profile.BodyDepthLateralRange)
                        * DepthBell(anterior / halfDepth, profile.BodyDepthDepthPosition, profile.BodyDepthDepthRange);
                    delta += forward * anterior * profile.BodyDepthStrength * bodyDepth * band;
                }

                float head = Value(config, "head");
                if (Mathf.Abs(head) > 0.0001f)
                {
                    float centerLateral = height * profile.HeadLateralPosition;
                    float centerAnterior = halfDepth * profile.HeadDepthPosition;
                    float verticalOffset = height * (vertical - profile.HeadHeight);
                    float band = Bell(vertical, profile.HeadHeight, profile.HeadRange)
                        * Bell(lateral / height, profile.HeadLateralPosition, profile.HeadLateralRange)
                        * DepthBell(anterior / halfDepth, profile.HeadDepthPosition, profile.HeadDepthRange);
                    Vector3 radial = right * (lateral - centerLateral) + up * verticalOffset
                        + forward * (anterior - centerAnterior);
                    delta += radial * profile.HeadStrength * head * band;
                }

                float jaw = Value(config, "jaw");
                if (Mathf.Abs(jaw) > 0.0001f)
                {
                    float side = Side(lateral);
                    float centerLateral = side * height * profile.JawLateralPosition;
                    float centerAnterior = halfDepth * profile.JawDepthPosition;
                    float verticalOffset = height * (vertical - profile.JawHeight);
                    float band = Bell(vertical, profile.JawHeight, profile.JawRange)
                        * Bell(Mathf.Abs(lateral) / height, profile.JawLateralPosition, profile.JawLateralRange)
                        * DepthBell(anterior / halfDepth, profile.JawDepthPosition, profile.JawDepthRange);
                    Vector3 radial = right * (lateral - centerLateral) + up * verticalOffset
                        + forward * (anterior - centerAnterior);
                    delta += radial * profile.JawStrength * jaw * band;
                }

                float cheeks = Value(config, "cheeks");
                if (Mathf.Abs(cheeks) > 0.0001f)
                {
                    float band = Bell(vertical, profile.CheeksHeight, profile.CheeksRange)
                        * Bell(Mathf.Abs(lateral) / height, profile.CheeksLateralPosition, profile.CheeksLateralRange)
                        * DepthBell(anterior / halfDepth, profile.CheeksDepthPosition, profile.CheeksDepthRange);
                    delta += forward * height * profile.CheeksStrength * cheeks * band;
                }

                float forehead = Value(config, "forehead");
                if (Mathf.Abs(forehead) > 0.0001f)
                {
                    float band = Bell(vertical, profile.ForeheadHeight, profile.ForeheadRange)
                        * Bell(lateral / height, profile.ForeheadLateralPosition, profile.ForeheadLateralRange)
                        * DepthBell(anterior / halfDepth, profile.ForeheadDepthPosition, profile.ForeheadDepthRange);
                    delta += forward * height * profile.ForeheadStrength * forehead * band;
                }

                float nose = Value(config, "nose");
                if (Mathf.Abs(nose) > 0.0001f)
                {
                    float band = Bell(vertical, profile.NoseHeight, profile.NoseRange)
                        * Bell(lateral / height, profile.NoseLateralPosition, profile.NoseLateralRange)
                        * DepthBell(anterior / halfDepth, profile.NoseDepthPosition, profile.NoseDepthRange);
                    delta += forward * height * profile.NoseStrength * nose * band;
                }

                float chin = Value(config, "chin");
                if (Mathf.Abs(chin) > 0.0001f)
                {
                    float band = Bell(vertical, profile.ChinHeight, profile.ChinRange)
                        * Bell(lateral / height, profile.ChinLateralPosition, profile.ChinLateralRange)
                        * DepthBell(anterior / halfDepth, profile.ChinDepthPosition, profile.ChinDepthRange);
                    delta += forward * height * profile.ChinStrength * chin * band;
                }

                float eyes = Value(config, "eyes");
                if (Mathf.Abs(eyes) > 0.0001f)
                {
                    float side = Side(lateral);
                    float centerLateral = side * height * profile.EyesLateralPosition;
                    float centerAnterior = halfDepth * profile.EyesDepthPosition;
                    float verticalOffset = height * (vertical - profile.EyesHeight);
                    float band = Bell(vertical, profile.EyesHeight, profile.EyesRange)
                        * Bell(Mathf.Abs(lateral) / height, profile.EyesLateralPosition, profile.EyesLateralRange)
                        * DepthBell(anterior / halfDepth, profile.EyesDepthPosition, profile.EyesDepthRange);
                    Vector3 radial = right * (lateral - centerLateral) + up * verticalOffset
                        + forward * (anterior - centerAnterior);
                    delta += radial * profile.EyesStrength * eyes * band;
                }

                float mouth = Value(config, "mouth");
                if (Mathf.Abs(mouth) > 0.0001f)
                {
                    float centerLateral = height * profile.MouthLateralPosition;
                    float centerAnterior = halfDepth * profile.MouthDepthPosition;
                    float verticalOffset = height * (vertical - profile.MouthHeight);
                    float band = Bell(vertical, profile.MouthHeight, profile.MouthRange)
                        * Bell(lateral / height, profile.MouthLateralPosition, profile.MouthLateralRange)
                        * DepthBell(anterior / halfDepth, profile.MouthDepthPosition, profile.MouthDepthRange);
                    Vector3 radial = right * (lateral - centerLateral) + up * verticalOffset
                        + forward * (anterior - centerAnterior);
                    delta += radial * profile.MouthStrength * mouth * band;
                }

                float ears = Value(config, "ears");
                if (Mathf.Abs(ears) > 0.0001f)
                {
                    float side = Side(lateral);
                    float centerLateral = side * height * profile.EarsLateralPosition;
                    float centerAnterior = halfDepth * profile.EarsDepthPosition;
                    float verticalOffset = height * (vertical - profile.EarsHeight);
                    float band = Bell(vertical, profile.EarsHeight, profile.EarsRange)
                        * Bell(Mathf.Abs(lateral) / height, profile.EarsLateralPosition, profile.EarsLateralRange)
                        * DepthBell(anterior / halfDepth, profile.EarsDepthPosition, profile.EarsDepthRange);
                    Vector3 radial = right * (lateral - centerLateral) + up * verticalOffset
                        + forward * (anterior - centerAnterior);
                    delta += radial * profile.EarsStrength * ears * band;
                }

                float neck = Value(config, "neck");
                if (Mathf.Abs(neck) > 0.0001f)
                {
                    float centerLateral = height * profile.NeckLateralPosition;
                    float centerAnterior = halfDepth * profile.NeckDepthPosition;
                    float band = Bell(vertical, profile.NeckHeight, profile.NeckRange)
                        * Bell(lateral / height, profile.NeckLateralPosition, profile.NeckLateralRange)
                        * DepthBell(anterior / halfDepth, profile.NeckDepthPosition, profile.NeckDepthRange);
                    Vector3 radial = right * (lateral - centerLateral)
                        + forward * (anterior - centerAnterior);
                    delta += radial * profile.NeckStrength * neck * band;
                }

                float shoulders = Value(config, "shoulders");
                if (Mathf.Abs(shoulders) > 0.0001f)
                {
                    float side = Side(lateral);
                    float centerLateral = side * height * profile.ShouldersLateralPosition;
                    float centerAnterior = halfDepth * profile.ShouldersDepthPosition;
                    float band = Bell(vertical, profile.ShouldersHeight, profile.ShouldersRange)
                        * Bell(Mathf.Abs(lateral) / height, profile.ShouldersLateralPosition, profile.ShouldersLateralRange)
                        * DepthBell(anterior / halfDepth, profile.ShouldersDepthPosition, profile.ShouldersDepthRange);
                    Vector3 radial = right * (lateral - centerLateral)
                        + forward * (anterior - centerAnterior);
                    delta += radial * profile.ShouldersStrength * shoulders * band;
                }

                float chest = Value(config, "chest");
                if (Mathf.Abs(chest) > 0.0001f)
                {
                    float side = Side(lateral);
                    float centerLateral = side * height * profile.ChestLateralPosition;
                    float centerAnterior = halfDepth * profile.ChestDepthPosition;
                    float band = Bell(vertical, profile.ChestHeight, profile.ChestRange)
                        * DepthBell(anterior / halfDepth, profile.ChestDepthPosition, profile.ChestDepthRange)
                        * Bell(Mathf.Abs(lateral) / height, profile.ChestLateralPosition, profile.ChestLateralRange);
                    Vector3 radial = right * (lateral - centerLateral) * profile.ChestWidthRatio
                        + forward * (anterior - centerAnterior) * profile.ChestDepthRatio;
                    delta += radial * profile.ChestStrength * chest * band;
                }

                float waist = Value(config, "waist");
                if (Mathf.Abs(waist) > 0.0001f)
                {
                    float band = Bell(vertical, profile.WaistHeight, profile.WaistRange)
                        * DepthBell(anterior / halfDepth, profile.WaistDepthPosition, profile.WaistDepthRange)
                        * Bell(lateral / height, profile.WaistLateralPosition, profile.WaistLateralRange);
                    Vector3 radial = right * lateral + forward * anterior;
                    delta += radial * profile.WaistStrength * waist * band;
                }

                float upperBack = Value(config, "upper_back");
                if (Mathf.Abs(upperBack) > 0.0001f)
                {
                    float band = Bell(vertical, profile.UpperBackHeight, profile.UpperBackRange)
                        * Bell(lateral / height, profile.UpperBackLateralPosition, profile.UpperBackLateralRange)
                        * DepthBell(posterior / halfDepth, profile.UpperBackDepthPosition, profile.UpperBackDepthRange);
                    delta += -forward * height * profile.UpperBackStrength * upperBack * band;
                }

                float upperArms = Value(config, "upper_arms");
                if (Mathf.Abs(upperArms) > 0.0001f)
                {
                    float centerAnterior = halfDepth * profile.UpperArmsDepthPosition;
                    float verticalOffset = height * (vertical - profile.UpperArmsHeight);
                    float band = Bell(vertical, profile.UpperArmsHeight, profile.UpperArmsRange)
                        * Bell(Mathf.Abs(lateral) / height, profile.UpperArmsLateralPosition, profile.UpperArmsLateralRange)
                        * DepthBell(anterior / halfDepth, profile.UpperArmsDepthPosition, profile.UpperArmsDepthRange);
                    Vector3 radial = up * verticalOffset + forward * (anterior - centerAnterior);
                    delta += radial * profile.UpperArmsStrength * upperArms * band;
                }

                float forearms = Value(config, "forearms");
                if (Mathf.Abs(forearms) > 0.0001f)
                {
                    float centerAnterior = halfDepth * profile.ForearmsDepthPosition;
                    float verticalOffset = height * (vertical - profile.ForearmsHeight);
                    float band = Bell(vertical, profile.ForearmsHeight, profile.ForearmsRange)
                        * Bell(Mathf.Abs(lateral) / height, profile.ForearmsLateralPosition, profile.ForearmsLateralRange)
                        * DepthBell(anterior / halfDepth, profile.ForearmsDepthPosition, profile.ForearmsDepthRange);
                    Vector3 radial = up * verticalOffset + forward * (anterior - centerAnterior);
                    delta += radial * profile.ForearmsStrength * forearms * band;
                }

                float hands = Value(config, "hands");
                if (Mathf.Abs(hands) > 0.0001f)
                {
                    float side = Side(lateral);
                    float centerLateral = side * height * profile.HandsLateralPosition;
                    float centerAnterior = halfDepth * profile.HandsDepthPosition;
                    float verticalOffset = height * (vertical - profile.HandsHeight);
                    float band = Bell(vertical, profile.HandsHeight, profile.HandsRange)
                        * Bell(Mathf.Abs(lateral) / height, profile.HandsLateralPosition, profile.HandsLateralRange)
                        * DepthBell(anterior / halfDepth, profile.HandsDepthPosition, profile.HandsDepthRange);
                    Vector3 radial = right * (lateral - centerLateral) + up * verticalOffset
                        + forward * (anterior - centerAnterior);
                    delta += radial * profile.HandsStrength * hands * band;
                }

                float abdomen = Value(config, "abdomen");
                if (Mathf.Abs(abdomen) > 0.0001f)
                {
                    float depth = DepthBell(anterior / halfDepth, profile.AbdomenDepthPosition, profile.AbdomenDepthRange);
                    float band = Bell(vertical, profile.AbdomenHeight, profile.AbdomenRange)
                        * depth * Bell(lateral / height, profile.AbdomenLateralPosition, profile.AbdomenLateralRange);
                    delta += forward * height * profile.AbdomenStrength * abdomen * band;
                }

                float hips = Value(config, "hips");
                if (Mathf.Abs(hips) > 0.0001f)
                {
                    float band = Bell(vertical, profile.HipsHeight, profile.HipsRange)
                        * DepthBell(posterior / halfDepth, profile.HipsDepthPosition, profile.HipsDepthRange)
                        * Bell(Mathf.Abs(lateral) / height, profile.HipsLateralPosition, profile.HipsLateralRange);
                    float silhouette = Ramp(Mathf.Abs(lateral) / height,
                        profile.HipsInnerRadius, profile.HipsOuterRadius);
                    delta += right * Mathf.Sign(lateral) * height * profile.HipsStrength * hips * band * silhouette;
                }

                float glutes = Value(config, "glutes");
                if (Mathf.Abs(glutes) > 0.0001f)
                {
                    float side = Side(lateral);
                    float lobeCenter = side * height * profile.GlutesLateralPosition;
                    float lobe = Bell(lateral / height, lobeCenter / height, profile.GlutesLateralRange);
                    float depth = DepthBell(posterior / halfDepth, profile.GlutesDepthPosition, profile.GlutesDepthRange);
                    float band = Bell(vertical, profile.GlutesHeight, profile.GlutesRange) * lobe * depth;
                    delta += -forward * height * profile.GlutesStrength * glutes * band;
                }

                float thighs = Value(config, "thighs");
                if (Mathf.Abs(thighs) > 0.0001f)
                {
                    float side = Side(lateral);
                    float centerLateral = side * height * profile.ThighLateralPosition;
                    float centerPosterior = halfDepth * profile.ThighDepthPosition;
                    Vector3 radial = right * (lateral - centerLateral)
                        + (-forward) * (posterior - centerPosterior);
                    float radialDistance = Mathf.Sqrt(
                        Mathf.Pow((lateral - centerLateral) / height, 2f)
                        + Mathf.Pow((posterior - centerPosterior) / height, 2f));
                    float proximity = 1f - Ramp(radialDistance, profile.ThighInnerRadius, profile.ThighLateralRange);
                    float depth = DepthBell(posterior / halfDepth, profile.ThighDepthPosition, profile.ThighDepthRange);
                    float band = Bell(vertical, profile.ThighHeight, profile.ThighRange) * proximity * depth;
                    delta += radial * profile.ThighStrength * thighs * band;
                }

                float knees = Value(config, "knees");
                if (Mathf.Abs(knees) > 0.0001f)
                {
                    float side = Side(lateral);
                    float centerLateral = side * height * profile.KneesLateralPosition;
                    float centerPosterior = halfDepth * profile.KneesDepthPosition;
                    float band = Bell(vertical, profile.KneesHeight, profile.KneesRange)
                        * Bell(Mathf.Abs(lateral) / height, profile.KneesLateralPosition, profile.KneesLateralRange)
                        * DepthBell(posterior / halfDepth, profile.KneesDepthPosition, profile.KneesDepthRange);
                    Vector3 radial = right * (lateral - centerLateral)
                        + (-forward) * (posterior - centerPosterior);
                    delta += radial * profile.KneesStrength * knees * band;
                }

                float calves = Value(config, "calves");
                if (Mathf.Abs(calves) > 0.0001f)
                {
                    float side = Side(lateral);
                    float centerLateral = side * height * profile.CalvesLateralPosition;
                    float centerPosterior = halfDepth * profile.CalvesDepthPosition;
                    float band = Bell(vertical, profile.CalvesHeight, profile.CalvesRange)
                        * Bell(Mathf.Abs(lateral) / height, profile.CalvesLateralPosition, profile.CalvesLateralRange)
                        * DepthBell(posterior / halfDepth, profile.CalvesDepthPosition, profile.CalvesDepthRange);
                    Vector3 radial = right * (lateral - centerLateral)
                        + (-forward) * (posterior - centerPosterior);
                    delta += radial * profile.CalvesStrength * calves * band;
                }

                float feet = Value(config, "feet");
                if (Mathf.Abs(feet) > 0.0001f)
                {
                    float side = Side(lateral);
                    float centerLateral = side * height * profile.FeetLateralPosition;
                    float centerPosterior = halfDepth * profile.FeetDepthPosition;
                    float verticalOffset = height * (vertical - profile.FeetHeight);
                    float band = Bell(vertical, profile.FeetHeight, profile.FeetRange)
                        * Bell(Mathf.Abs(lateral) / height, profile.FeetLateralPosition, profile.FeetLateralRange)
                        * DepthBell(posterior / halfDepth, profile.FeetDepthPosition, profile.FeetDepthRange);
                    Vector3 radial = right * (lateral - centerLateral) + up * verticalOffset
                        + (-forward) * (posterior - centerPosterior);
                    delta += radial * profile.FeetStrength * feet * band;
                }

                float normalizedLateral = lateral / height;
                float normalizedDepth = anterior / halfDepth;
                foreach (AnatomySpec spec in profile.AdditionalSpecs.Values)
                {
                    float amount = Value(config, spec.Name);
                    if (Mathf.Abs(amount) <= 0.0001f) continue;
                    float maskLateral = spec.Bilateral ? Mathf.Abs(normalizedLateral) : normalizedLateral;
                    float band = Bell(vertical, spec.VerticalPosition, spec.VerticalRange)
                        * Bell(maskLateral, spec.LateralPosition, spec.LateralRange)
                        * DepthBell(normalizedDepth, spec.DepthPosition, spec.DepthRange);
                    if (band <= 0f) continue;
                    delta += AdditionalDelta(spec, amount, band, vertical, lateral, anterior,
                        height, halfDepth, right, up, forward);
                }

                if (delta.sqrMagnitude <= 0.000000000001f) continue;
                Vector3 displacedWorld = body.transform.TransformPoint(point + delta);
                vertices[i] = renderer.transform.InverseTransformPoint(displacedWorld);
                changed = true;
            }
            if (!changed) return null;
            Mesh output = UnityEngine.Object.Instantiate(source);
            output.name = source.name + " (BodyForge anatomy)";
            output.vertices = vertices;
            output.RecalculateBounds();
            output.RecalculateNormals();
            return output;
        }

        private static float ProjectedExtent(Vector3 extents, Vector3 axis)
        {
            return Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.y) * extents.y + Mathf.Abs(axis.z) * extents.z;
        }

        private static float Bell(float value, float center, float range)
        {
            float distance = Mathf.Abs(value - center) / Mathf.Max(Mathf.Abs(range), 0.0001f);
            return Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(distance));
        }

        private static float DepthBell(float value, float center, float range)
        {
            return Bell(value, center, range * 2f);
        }

        private static float Ramp(float value, float start, float end)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, value));
        }

        private static float Side(float lateral)
        {
            return lateral < 0f ? -1f : 1f;
        }

        private static Vector3 AdditionalDelta(AnatomySpec spec, float amount, float band,
                                               float vertical, float lateral, float anterior,
                                               float height, float halfDepth, Vector3 right,
                                               Vector3 up, Vector3 forward)
        {
            float side = Side(lateral);
            float centerX = (spec.Bilateral ? side : 1f) * height * spec.LateralPosition;
            float centerY = height * (spec.VerticalPosition - 0.5f);
            float pointY = height * (vertical - 0.5f);
            float centerZ = halfDepth * spec.DepthPosition;
            Vector3 displacement;
            switch (spec.Mode)
            {
                case AnatomyMode.ScaleX:
                    displacement = right * (lateral - centerX);
                    break;
                case AnatomyMode.ScaleY:
                    displacement = up * (pointY - centerY);
                    break;
                case AnatomyMode.ScaleZ:
                    displacement = forward * (anterior - centerZ);
                    break;
                case AnatomyMode.TranslateX:
                    displacement = right * side * height;
                    break;
                case AnatomyMode.TranslateY:
                    displacement = up * height;
                    break;
                case AnatomyMode.TranslateZ:
                    displacement = forward * height;
                    break;
                case AnatomyMode.Posture:
                    displacement = forward * height * (vertical - spec.VerticalPosition);
                    break;
                case AnatomyMode.LengthX:
                    displacement = right * side * Mathf.Max(Mathf.Abs(lateral) - Mathf.Abs(centerX), 0f);
                    break;
                case AnatomyMode.LengthY:
                    displacement = up * Mathf.Sign(pointY - centerY)
                        * Mathf.Max(Mathf.Abs(pointY - centerY) - height * spec.VerticalRange * 0.25f, 0f);
                    break;
                case AnatomyMode.Radial:
                default:
                    displacement = right * (lateral - centerX) + forward * (anterior - centerZ);
                    break;
            }
            return displacement * spec.Strength * amount * band;
        }

        private static float Value(ProportionConfig config, string name)
        {
            float value = config.Anatomy.TryGetValue(name, out float configured) ? configured : 0f;
#if BODYFORGE_DEV
            return AnatomyCalibration.Value(name, value);
#else
            return value;
#endif
        }

        private static bool HasChanges(ProportionConfig config)
        {
#if BODYFORGE_DEV
            return config.HasAnatomyChanges || AnatomyCalibration.Enabled;
#else
            return config.HasAnatomyChanges;
#endif
        }

        private static Mesh OriginalMesh(SkinnedMeshRenderer renderer)
        {
            return Meshes.TryGetValue(renderer.GetInstanceID(), out MeshState state)
                ? state.Original : renderer.sharedMesh;
        }

        private static void ReplaceMesh(SkinnedMeshRenderer renderer, Mesh original, Mesh deformed)
        {
            int id = renderer.GetInstanceID();
            if (Meshes.TryGetValue(id, out MeshState previous) && previous.Deformed != null)
                UnityEngine.Object.Destroy(previous.Deformed);
            Meshes[id] = new MeshState { Renderer = renderer, Original = original, Deformed = deformed };
            renderer.sharedMesh = deformed;
        }

        private static bool IsMagicaCloth(SkinnedMeshRenderer renderer)
        {
            if (renderer == null) return false;
            if (HasMagicaCloth(renderer.gameObject)) return true;
            Transform parent = renderer.transform.parent;
            return parent != null && HasMagicaCloth(parent.gameObject);
        }

        private static bool HasMagicaCloth(GameObject gameObject)
        {
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (component == null) continue;
                Type type = component.GetType();
                if (type.FullName == "MagicaCloth2.MagicaCloth" || type.Name == "MagicaCloth") return true;
            }
            return false;
        }

        private static void ApplyCapeAnchor(SkinnedMeshRenderer renderer, SkinnedMeshRenderer body,
                                            Vector3 worldOffset)
        {
            int id = renderer.GetInstanceID();
            if (worldOffset.sqrMagnitude <= 0.0000000001f)
            {
                RestoreCapeAnchor(id);
                return;
            }

            if (CapeAnchors.TryGetValue(id, out CapeAnchorState state))
            {
                if (state.Renderer != renderer || state.Original == null || state.Proxy == null)
                {
                    RestoreCapeAnchor(id);
                    state = null;
                }
            }

            if (state == null)
            {
                Transform original = FindCapeBone(renderer, body);
                Component cloth = FindMagicaCloth(renderer);
                if (original == null || original.parent == null || cloth == null) return;

                GameObject proxyObject = new GameObject(original.name + " (BodyForge cape anchor)");
                proxyObject.hideFlags = HideFlags.HideAndDontSave;
                Transform proxy = proxyObject.transform;
                proxy.SetParent(original.parent, false);
                proxy.localPosition = original.localPosition;
                proxy.localRotation = original.localRotation;
                proxy.localScale = original.localScale;

                Transform[] bones = renderer.bones;
                int boneIndex = Array.IndexOf(bones, original);
                if (boneIndex < 0 || !ReplaceClothTransform(cloth, original.name, proxy))
                {
                    BodyForgePlugin.LogWarn("cape anchor replacement unavailable for " + renderer.name);
                    UnityEngine.Object.Destroy(proxyObject);
                    return;
                }
                bones[boneIndex] = proxy;
                renderer.bones = bones;
                state = new CapeAnchorState
                {
                    Renderer = renderer,
                    Cloth = cloth,
                    Original = original,
                    Proxy = proxy,
                    BoneIndex = boneIndex,
                    WorldOffset = new Vector3(float.PositiveInfinity, 0f, 0f)
                };
                CapeAnchors[id] = state;
                BodyForgePlugin.LogInfo("cape anchor proxy applied to " + renderer.name);
            }

            bool changed = (state.WorldOffset - worldOffset).sqrMagnitude > 0.0000000001f;
            state.Proxy.localPosition = state.Original.localPosition
                + state.Proxy.parent.InverseTransformVector(worldOffset);
            state.Proxy.localRotation = state.Original.localRotation;
            state.Proxy.localScale = state.Original.localScale;
            state.WorldOffset = worldOffset;
            if (changed) ResetCloth(state.Cloth);
        }

        private static Transform FindCapeBone(SkinnedMeshRenderer renderer, SkinnedMeshRenderer body)
        {
            HashSet<Transform> bodyBones = new HashSet<Transform>(body.bones.Where(bone => bone != null));
            Transform fallback = null;
            foreach (Transform bone in renderer.bones)
            {
                if (bone == null || bodyBones.Contains(bone)) continue;
                if (bone.name.IndexOf("cape", StringComparison.OrdinalIgnoreCase) >= 0) return bone;
                if (fallback == null) fallback = bone;
            }
            return fallback;
        }

        private static Component FindMagicaCloth(SkinnedMeshRenderer renderer)
        {
            Component cloth = FindMagicaCloth(renderer.gameObject);
            return cloth != null || renderer.transform.parent == null
                ? cloth : FindMagicaCloth(renderer.transform.parent.gameObject);
        }

        private static Component FindMagicaCloth(GameObject gameObject)
        {
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (component == null) continue;
                Type type = component.GetType();
                if (type.FullName == "MagicaCloth2.MagicaCloth" || type.Name == "MagicaCloth") return component;
            }
            return null;
        }

        private static bool ReplaceClothTransform(Component cloth, string transformName, Transform replacement)
        {
            try
            {
                MethodInfo method = cloth.GetType().GetMethod("ReplaceTransform",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                    new[] { typeof(Dictionary<string, Transform>) }, null);
                if (method == null) return false;
                var replacements = new Dictionary<string, Transform>
                {
                    { transformName, replacement }
                };
                method.Invoke(cloth, new object[] { replacements });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void ResetCloth(Component cloth)
        {
            if (cloth == null) return;
            try
            {
                MethodInfo method = cloth.GetType().GetMethod("ResetCloth",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                    new[] { typeof(bool) }, null);
                if (method != null) method.Invoke(cloth, new object[] { false });
            }
            catch (Exception)
            {
                // Reflection failure must not interfere with rendering or equipment updates.
            }
        }

        private static void RestoreCapeAnchors(VisEquipment equipment)
        {
            foreach (SkinnedMeshRenderer renderer in equipment.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                RestoreCapeAnchor(renderer.GetInstanceID());
            CleanupCapeAnchors();
        }

        private static void RestoreCapeAnchor(int id)
        {
            if (!CapeAnchors.TryGetValue(id, out CapeAnchorState state)) return;
            CapeAnchors.Remove(id);
            if (state.Renderer != null && state.Original != null)
            {
                Transform[] bones = state.Renderer.bones;
                if (state.BoneIndex >= 0 && state.BoneIndex < bones.Length && bones[state.BoneIndex] == state.Proxy)
                {
                    bones[state.BoneIndex] = state.Original;
                    state.Renderer.bones = bones;
                }
            }
            if (state.Cloth != null && state.Proxy != null && state.Original != null)
            {
                ReplaceClothTransform(state.Cloth, state.Original.name, state.Original);
                ResetCloth(state.Cloth);
            }
            if (state.Proxy != null) UnityEngine.Object.Destroy(state.Proxy.gameObject);
        }

        private static void CleanupCapeAnchors()
        {
            foreach (int id in CapeAnchors.Where(entry => entry.Value.Renderer == null).Select(entry => entry.Key).ToArray())
                RestoreCapeAnchor(id);
        }

        private static void RestoreRenderer(SkinnedMeshRenderer renderer)
        {
            if (renderer == null || !Meshes.TryGetValue(renderer.GetInstanceID(), out MeshState state)) return;
            renderer.sharedMesh = state.Original;
            if (state.Deformed != null) UnityEngine.Object.Destroy(state.Deformed);
            Meshes.Remove(renderer.GetInstanceID());
        }

        private static void RestoreAll(VisEquipment equipment)
        {
            foreach (SkinnedMeshRenderer renderer in equipment.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                RestoreRenderer(renderer);
        }

        private static string Signature(VisEquipment equipment, ProportionConfig config)
        {
            string renderers = string.Join(",", equipment.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer != null).Select(renderer => renderer.GetInstanceID().ToString()));
            string anatomy = string.Join(",", config.Anatomy.OrderBy(entry => entry.Key)
                .Select(entry => entry.Key + ":" + entry.Value.ToString("R")));
            string signature = equipment.GetModelIndex() + "|" + renderers + "|" + anatomy;
#if BODYFORGE_DEV
            signature += "|calibration:" + AnatomyCalibration.Revision;
#endif
            return signature;
        }

        private sealed class MeshState
        {
            public SkinnedMeshRenderer Renderer;
            public Mesh Original;
            public Mesh Deformed;
        }

        private sealed class CapeAnchorState
        {
            public SkinnedMeshRenderer Renderer;
            public Component Cloth;
            public Transform Original;
            public Transform Proxy;
            public int BoneIndex;
            public Vector3 WorldOffset;
        }

        internal sealed class AnatomyProfile
        {
            public readonly Dictionary<string, AnatomySpec> AdditionalSpecs = new Dictionary<string, AnatomySpec>();
            public float BodyHeightPosition;
            public float BodyHeightRange;
            public float BodyHeightLateralPosition;
            public float BodyHeightLateralRange;
            public float BodyHeightDepthPosition;
            public float BodyHeightDepthRange;
            public float BodyHeightStrength;
            public float BodyWidthPosition;
            public float BodyWidthRange;
            public float BodyWidthLateralPosition;
            public float BodyWidthLateralRange;
            public float BodyWidthDepthPosition;
            public float BodyWidthDepthRange;
            public float BodyWidthStrength;
            public float BodyDepthPosition;
            public float BodyDepthRange;
            public float BodyDepthLateralPosition;
            public float BodyDepthLateralRange;
            public float BodyDepthDepthPosition;
            public float BodyDepthDepthRange;
            public float BodyDepthStrength;
            public float HeadHeight;
            public float HeadRange;
            public float HeadLateralPosition;
            public float HeadLateralRange;
            public float HeadDepthPosition;
            public float HeadDepthRange;
            public float HeadStrength;
            public float JawHeight;
            public float JawRange;
            public float JawLateralPosition;
            public float JawLateralRange;
            public float JawDepthPosition;
            public float JawDepthRange;
            public float JawStrength;
            public float CheeksHeight;
            public float CheeksRange;
            public float CheeksLateralPosition;
            public float CheeksLateralRange;
            public float CheeksDepthPosition;
            public float CheeksDepthRange;
            public float CheeksStrength;
            public float ForeheadHeight;
            public float ForeheadRange;
            public float ForeheadLateralPosition;
            public float ForeheadLateralRange;
            public float ForeheadDepthPosition;
            public float ForeheadDepthRange;
            public float ForeheadStrength;
            public float NoseHeight;
            public float NoseRange;
            public float NoseLateralPosition;
            public float NoseLateralRange;
            public float NoseDepthPosition;
            public float NoseDepthRange;
            public float NoseStrength;
            public float ChinHeight;
            public float ChinRange;
            public float ChinLateralPosition;
            public float ChinLateralRange;
            public float ChinDepthPosition;
            public float ChinDepthRange;
            public float ChinStrength;
            public float EyesHeight;
            public float EyesRange;
            public float EyesLateralPosition;
            public float EyesLateralRange;
            public float EyesDepthPosition;
            public float EyesDepthRange;
            public float EyesStrength;
            public float MouthHeight;
            public float MouthRange;
            public float MouthLateralPosition;
            public float MouthLateralRange;
            public float MouthDepthPosition;
            public float MouthDepthRange;
            public float MouthStrength;
            public float EarsHeight;
            public float EarsRange;
            public float EarsLateralPosition;
            public float EarsLateralRange;
            public float EarsDepthPosition;
            public float EarsDepthRange;
            public float EarsStrength;
            public float NeckHeight;
            public float NeckRange;
            public float NeckLateralPosition;
            public float NeckLateralRange;
            public float NeckDepthPosition;
            public float NeckDepthRange;
            public float NeckStrength;
            public float ShouldersHeight;
            public float ShouldersRange;
            public float ShouldersLateralPosition;
            public float ShouldersLateralRange;
            public float ShouldersDepthPosition;
            public float ShouldersDepthRange;
            public float ShouldersStrength;
            public float ChestHeight;
            public float ChestRange;
            public float ChestLateralPosition;
            public float ChestLateralRange;
            public float ChestDepthPosition;
            public float ChestDepthRange;
            public float ChestStrength;
            public float ChestWidthRatio;
            public float ChestDepthRatio;
            public float WaistHeight;
            public float WaistRange;
            public float WaistLateralPosition;
            public float WaistLateralRange;
            public float WaistDepthPosition;
            public float WaistDepthRange;
            public float WaistStrength;
            public float UpperBackHeight;
            public float UpperBackRange;
            public float UpperBackLateralPosition;
            public float UpperBackLateralRange;
            public float UpperBackDepthPosition;
            public float UpperBackDepthRange;
            public float UpperBackStrength;
            public float UpperArmsHeight;
            public float UpperArmsRange;
            public float UpperArmsLateralPosition;
            public float UpperArmsLateralRange;
            public float UpperArmsDepthPosition;
            public float UpperArmsDepthRange;
            public float UpperArmsStrength;
            public float ForearmsHeight;
            public float ForearmsRange;
            public float ForearmsLateralPosition;
            public float ForearmsLateralRange;
            public float ForearmsDepthPosition;
            public float ForearmsDepthRange;
            public float ForearmsStrength;
            public float HandsHeight;
            public float HandsRange;
            public float HandsLateralPosition;
            public float HandsLateralRange;
            public float HandsDepthPosition;
            public float HandsDepthRange;
            public float HandsStrength;
            public float AbdomenHeight;
            public float AbdomenRange;
            public float AbdomenLateralPosition;
            public float AbdomenLateralRange;
            public float AbdomenDepthPosition;
            public float AbdomenDepthRange;
            public float AbdomenStrength;
            public float HipsHeight;
            public float HipsRange;
            public float HipsLateralPosition;
            public float HipsLateralRange;
            public float HipsStrength;
            public float HipsInnerRadius;
            public float HipsOuterRadius;
            public float HipsDepthPosition;
            public float HipsDepthRange;
            public float GlutesHeight;
            public float GlutesRange;
            public float GlutesStrength;
            public float GlutesLateralPosition;
            public float GlutesLateralRange;
            public float GlutesDepthPosition;
            public float GlutesDepthRange;
            public float ThighHeight;
            public float ThighRange;
            public float ThighLateralPosition;
            public float ThighLateralRange;
            public float ThighDepthPosition;
            public float ThighDepthRange;
            public float ThighInnerRadius;
            public float ThighStrength;
            public float KneesHeight;
            public float KneesRange;
            public float KneesLateralPosition;
            public float KneesLateralRange;
            public float KneesDepthPosition;
            public float KneesDepthRange;
            public float KneesStrength;
            public float CalvesHeight;
            public float CalvesRange;
            public float CalvesLateralPosition;
            public float CalvesLateralRange;
            public float CalvesDepthPosition;
            public float CalvesDepthRange;
            public float CalvesStrength;
            public float FeetHeight;
            public float FeetRange;
            public float FeetLateralPosition;
            public float FeetLateralRange;
            public float FeetDepthPosition;
            public float FeetDepthRange;
            public float FeetStrength;

            public static AnatomyProfile ForModel(int model)
            {
                AnatomyProfile profile = model == 1
                    ? new AnatomyProfile
                    {
                        BodyHeightPosition = 0.5f, BodyHeightRange = 1f, BodyHeightLateralPosition = 0f,
                        BodyHeightLateralRange = 1f, BodyHeightDepthPosition = 0f, BodyHeightDepthRange = 1f,
                        BodyHeightStrength = 0.25f,
                        BodyWidthPosition = 0.5f, BodyWidthRange = 1f, BodyWidthLateralPosition = 0f,
                        BodyWidthLateralRange = 1f, BodyWidthDepthPosition = 0f, BodyWidthDepthRange = 1f,
                        BodyWidthStrength = 0.25f,
                        BodyDepthPosition = 0.5f, BodyDepthRange = 1f, BodyDepthLateralPosition = 0f,
                        BodyDepthLateralRange = 1f, BodyDepthDepthPosition = 0f, BodyDepthDepthRange = 1f,
                        BodyDepthStrength = 0.25f,
                        HeadHeight = 0.915f, HeadRange = 0.095f, HeadLateralPosition = 0f,
                        HeadLateralRange = 0.085f, HeadDepthPosition = 0.10f, HeadDepthRange = 0.95f,
                        HeadStrength = 0.28f,
                        JawHeight = 0.855f, JawRange = 0.055f, JawLateralPosition = 0.035f,
                        JawLateralRange = 0.055f, JawDepthPosition = 0.25f, JawDepthRange = 0.75f,
                        JawStrength = 0.30f,
                        CheeksHeight = 0.905f, CheeksRange = 0.045f, CheeksLateralPosition = 0.038f,
                        CheeksLateralRange = 0.045f, CheeksDepthPosition = 0.55f, CheeksDepthRange = 0.5f,
                        CheeksStrength = 0.014f,
                        ForeheadHeight = 0.955f, ForeheadRange = 0.050f, ForeheadLateralPosition = 0f,
                        ForeheadLateralRange = 0.070f, ForeheadDepthPosition = 0.45f, ForeheadDepthRange = 0.55f,
                        ForeheadStrength = 0.012f,
                        NoseHeight = 0.905f, NoseRange = 0.042f, NoseLateralPosition = 0f,
                        NoseLateralRange = 0.030f, NoseDepthPosition = 0.72f, NoseDepthRange = 0.38f,
                        NoseStrength = 0.014f,
                        ChinHeight = 0.855f, ChinRange = 0.035f, ChinLateralPosition = 0f,
                        ChinLateralRange = 0.038f, ChinDepthPosition = 0.58f, ChinDepthRange = 0.45f,
                        ChinStrength = 0.012f,
                        EyesHeight = 0.925f, EyesRange = 0.032f, EyesLateralPosition = 0.032f,
                        EyesLateralRange = 0.030f, EyesDepthPosition = 0.65f, EyesDepthRange = 0.38f,
                        EyesStrength = 0.24f,
                        MouthHeight = 0.875f, MouthRange = 0.028f, MouthLateralPosition = 0f,
                        MouthLateralRange = 0.050f, MouthDepthPosition = 0.68f, MouthDepthRange = 0.34f,
                        MouthStrength = 0.24f,
                        EarsHeight = 0.910f, EarsRange = 0.050f, EarsLateralPosition = 0.082f,
                        EarsLateralRange = 0.028f, EarsDepthPosition = 0.05f, EarsDepthRange = 0.7f,
                        EarsStrength = 0.28f,
                        NeckHeight = 0.82f, NeckRange = 0.075f, NeckLateralPosition = 0f,
                        NeckLateralRange = 0.055f, NeckDepthPosition = 0f, NeckDepthRange = 0.75f,
                        NeckStrength = 0.30f,
                        ShouldersHeight = 0.76f, ShouldersRange = 0.10f, ShouldersLateralPosition = 0.12f,
                        ShouldersLateralRange = 0.085f, ShouldersDepthPosition = 0f, ShouldersDepthRange = 1f,
                        ShouldersStrength = 0.28f,
                        ChestHeight = 0.70f, ChestRange = 0.15f, ChestLateralPosition = 0.052f, ChestLateralRange = 0.085f,
                        ChestDepthPosition = 0f, ChestDepthRange = 1f, ChestStrength = 0.32f,
                        ChestWidthRatio = 0.94f, ChestDepthRatio = 1.06f,
                        WaistHeight = 0.56f, WaistRange = 0.11f, WaistLateralPosition = 0f, WaistLateralRange = 0.18f,
                        WaistDepthPosition = 0f, WaistDepthRange = 1f, WaistStrength = 0.30f,
                        UpperBackHeight = 0.69f, UpperBackRange = 0.15f, UpperBackLateralPosition = 0f,
                        UpperBackLateralRange = 0.18f, UpperBackDepthPosition = 0.60f,
                        UpperBackDepthRange = 0.75f, UpperBackStrength = 0.022f,
                        UpperArmsHeight = 0.70f, UpperArmsRange = 0.065f, UpperArmsLateralPosition = 0.18f,
                        UpperArmsLateralRange = 0.075f, UpperArmsDepthPosition = 0f, UpperArmsDepthRange = 0.75f,
                        UpperArmsStrength = 0.34f,
                        ForearmsHeight = 0.70f, ForearmsRange = 0.055f, ForearmsLateralPosition = 0.265f,
                        ForearmsLateralRange = 0.075f, ForearmsDepthPosition = 0f, ForearmsDepthRange = 0.7f,
                        ForearmsStrength = 0.34f,
                        HandsHeight = 0.70f, HandsRange = 0.065f, HandsLateralPosition = 0.345f,
                        HandsLateralRange = 0.055f, HandsDepthPosition = 0f, HandsDepthRange = 0.75f,
                        HandsStrength = 0.30f,
                        AbdomenHeight = 0.56f, AbdomenRange = 0.12f, AbdomenLateralPosition = 0f,
                        AbdomenLateralRange = 0.18f, AbdomenDepthPosition = 0.55f,
                        AbdomenDepthRange = 0.75f, AbdomenStrength = 0.025f,
                        HipsHeight = 0.48f, HipsRange = 0.14f, HipsLateralPosition = 0.08f,
                        HipsLateralRange = 0.08f, HipsStrength = 0.022f,
                        HipsInnerRadius = 0.035f, HipsOuterRadius = 0.11f,
                        HipsDepthPosition = 0f, HipsDepthRange = 1f,
                        GlutesHeight = 0.475f, GlutesRange = 0.105f, GlutesStrength = 0.026f,
                        GlutesLateralPosition = 0.055f, GlutesLateralRange = 0.075f,
                        GlutesDepthPosition = 0.65f, GlutesDepthRange = 0.8f,
                        ThighHeight = 0.31f, ThighRange = 0.17f, ThighLateralPosition = 0.068f,
                        ThighLateralRange = 0.11f,
                        ThighDepthPosition = 0f, ThighDepthRange = 1f,
                        ThighInnerRadius = 0.055f, ThighStrength = 0.38f,
                        KneesHeight = 0.225f, KneesRange = 0.055f, KneesLateralPosition = 0.067f,
                        KneesLateralRange = 0.055f, KneesDepthPosition = 0f, KneesDepthRange = 0.75f,
                        KneesStrength = 0.30f,
                        CalvesHeight = 0.145f, CalvesRange = 0.085f, CalvesLateralPosition = 0.067f,
                        CalvesLateralRange = 0.060f, CalvesDepthPosition = 0.10f, CalvesDepthRange = 0.8f,
                        CalvesStrength = 0.34f,
                        FeetHeight = 0.045f, FeetRange = 0.055f, FeetLateralPosition = 0.067f,
                        FeetLateralRange = 0.065f, FeetDepthPosition = -0.30f, FeetDepthRange = 1f,
                        FeetStrength = 0.30f
                    }
                    : new AnatomyProfile
                    {
                        BodyHeightPosition = 0.5f, BodyHeightRange = 1f, BodyHeightLateralPosition = 0f,
                        BodyHeightLateralRange = 1f, BodyHeightDepthPosition = 0f, BodyHeightDepthRange = 1f,
                        BodyHeightStrength = 0.25f,
                        BodyWidthPosition = 0.5f, BodyWidthRange = 1f, BodyWidthLateralPosition = 0f,
                        BodyWidthLateralRange = 1f, BodyWidthDepthPosition = 0f, BodyWidthDepthRange = 1f,
                        BodyWidthStrength = 0.25f,
                        BodyDepthPosition = 0.5f, BodyDepthRange = 1f, BodyDepthLateralPosition = 0f,
                        BodyDepthLateralRange = 1f, BodyDepthDepthPosition = 0f, BodyDepthDepthRange = 1f,
                        BodyDepthStrength = 0.25f,
                        HeadHeight = 0.915f, HeadRange = 0.10f, HeadLateralPosition = 0f,
                        HeadLateralRange = 0.09f, HeadDepthPosition = 0.10f, HeadDepthRange = 1f,
                        HeadStrength = 0.30f,
                        JawHeight = 0.855f, JawRange = 0.06f, JawLateralPosition = 0.038f,
                        JawLateralRange = 0.06f, JawDepthPosition = 0.25f, JawDepthRange = 0.78f,
                        JawStrength = 0.32f,
                        CheeksHeight = 0.905f, CheeksRange = 0.05f, CheeksLateralPosition = 0.040f,
                        CheeksLateralRange = 0.048f, CheeksDepthPosition = 0.55f, CheeksDepthRange = 0.52f,
                        CheeksStrength = 0.015f,
                        ForeheadHeight = 0.955f, ForeheadRange = 0.052f, ForeheadLateralPosition = 0f,
                        ForeheadLateralRange = 0.074f, ForeheadDepthPosition = 0.45f, ForeheadDepthRange = 0.58f,
                        ForeheadStrength = 0.013f,
                        NoseHeight = 0.905f, NoseRange = 0.044f, NoseLateralPosition = 0f,
                        NoseLateralRange = 0.032f, NoseDepthPosition = 0.72f, NoseDepthRange = 0.4f,
                        NoseStrength = 0.015f,
                        ChinHeight = 0.855f, ChinRange = 0.038f, ChinLateralPosition = 0f,
                        ChinLateralRange = 0.040f, ChinDepthPosition = 0.58f, ChinDepthRange = 0.48f,
                        ChinStrength = 0.013f,
                        EyesHeight = 0.925f, EyesRange = 0.034f, EyesLateralPosition = 0.034f,
                        EyesLateralRange = 0.032f, EyesDepthPosition = 0.65f, EyesDepthRange = 0.4f,
                        EyesStrength = 0.26f,
                        MouthHeight = 0.875f, MouthRange = 0.030f, MouthLateralPosition = 0f,
                        MouthLateralRange = 0.052f, MouthDepthPosition = 0.68f, MouthDepthRange = 0.36f,
                        MouthStrength = 0.26f,
                        EarsHeight = 0.910f, EarsRange = 0.052f, EarsLateralPosition = 0.086f,
                        EarsLateralRange = 0.030f, EarsDepthPosition = 0.05f, EarsDepthRange = 0.72f,
                        EarsStrength = 0.30f,
                        NeckHeight = 0.82f, NeckRange = 0.08f, NeckLateralPosition = 0f,
                        NeckLateralRange = 0.06f, NeckDepthPosition = 0f, NeckDepthRange = 0.8f,
                        NeckStrength = 0.32f,
                        ShouldersHeight = 0.76f, ShouldersRange = 0.105f, ShouldersLateralPosition = 0.125f,
                        ShouldersLateralRange = 0.09f, ShouldersDepthPosition = 0f, ShouldersDepthRange = 1f,
                        ShouldersStrength = 0.32f,
                        ChestHeight = 0.69f, ChestRange = 0.15f, ChestLateralPosition = 0.052f, ChestLateralRange = 0.09f,
                        ChestDepthPosition = 0f, ChestDepthRange = 1f, ChestStrength = 0.32f,
                        ChestWidthRatio = 1.06f, ChestDepthRatio = 0.94f,
                        WaistHeight = 0.56f, WaistRange = 0.11f, WaistLateralPosition = 0f, WaistLateralRange = 0.18f,
                        WaistDepthPosition = 0f, WaistDepthRange = 1f, WaistStrength = 0.28f,
                        UpperBackHeight = 0.69f, UpperBackRange = 0.15f, UpperBackLateralPosition = 0f,
                        UpperBackLateralRange = 0.19f, UpperBackDepthPosition = 0.60f,
                        UpperBackDepthRange = 0.75f, UpperBackStrength = 0.024f,
                        UpperArmsHeight = 0.70f, UpperArmsRange = 0.07f, UpperArmsLateralPosition = 0.18f,
                        UpperArmsLateralRange = 0.075f, UpperArmsDepthPosition = 0f, UpperArmsDepthRange = 0.78f,
                        UpperArmsStrength = 0.36f,
                        ForearmsHeight = 0.70f, ForearmsRange = 0.06f, ForearmsLateralPosition = 0.265f,
                        ForearmsLateralRange = 0.075f, ForearmsDepthPosition = 0f, ForearmsDepthRange = 0.72f,
                        ForearmsStrength = 0.36f,
                        HandsHeight = 0.70f, HandsRange = 0.07f, HandsLateralPosition = 0.345f,
                        HandsLateralRange = 0.055f, HandsDepthPosition = 0f, HandsDepthRange = 0.78f,
                        HandsStrength = 0.32f,
                        AbdomenHeight = 0.56f, AbdomenRange = 0.12f, AbdomenLateralPosition = 0f,
                        AbdomenLateralRange = 0.18f, AbdomenDepthPosition = 0.55f,
                        AbdomenDepthRange = 0.75f, AbdomenStrength = 0.023f,
                        HipsHeight = 0.47f, HipsRange = 0.14f, HipsLateralPosition = 0.075f,
                        HipsLateralRange = 0.075f, HipsStrength = 0.018f,
                        HipsInnerRadius = 0.035f, HipsOuterRadius = 0.105f,
                        HipsDepthPosition = 0f, HipsDepthRange = 1f,
                        GlutesHeight = 0.465f, GlutesRange = 0.10f, GlutesStrength = 0.022f,
                        GlutesLateralPosition = 0.052f, GlutesLateralRange = 0.07f,
                        GlutesDepthPosition = 0.65f, GlutesDepthRange = 0.8f,
                        ThighHeight = 0.31f, ThighRange = 0.17f, ThighLateralPosition = 0.066f,
                        ThighLateralRange = 0.11f,
                        ThighDepthPosition = 0f, ThighDepthRange = 1f,
                        ThighInnerRadius = 0.055f, ThighStrength = 0.36f,
                        KneesHeight = 0.225f, KneesRange = 0.058f, KneesLateralPosition = 0.067f,
                        KneesLateralRange = 0.058f, KneesDepthPosition = 0f, KneesDepthRange = 0.78f,
                        KneesStrength = 0.32f,
                        CalvesHeight = 0.145f, CalvesRange = 0.09f, CalvesLateralPosition = 0.067f,
                        CalvesLateralRange = 0.064f, CalvesDepthPosition = 0.10f, CalvesDepthRange = 0.82f,
                        CalvesStrength = 0.36f,
                        FeetHeight = 0.045f, FeetRange = 0.058f, FeetLateralPosition = 0.067f,
                        FeetLateralRange = 0.068f, FeetDepthPosition = -0.30f, FeetDepthRange = 1f,
                        FeetStrength = 0.32f
                    };
                AddAdditionalSpecs(profile, model == 1 ? 0.94f : 1f);
                AnatomyProfileCalibration.ApplyTo(profile, model);
#if BODYFORGE_DEV
                AnatomyCalibration.ApplyTo(profile, model);
#endif
                return profile;
            }

            private static void AddAdditionalSpecs(AnatomyProfile profile, float modelScale)
            {
                Add(profile, "weight", AnatomyMode.Radial, .52f, .43f, 0f, .22f, 0f, 1f, .24f * modelScale, false);
                Add(profile, "musculature", AnatomyMode.Radial, .55f, .36f, .10f, .25f, 0f, 1f, .17f * modelScale, true);
                Add(profile, "posture", AnatomyMode.Posture, .55f, .42f, 0f, .22f, 0f, 1f, .18f, false);
                Add(profile, "head_width", AnatomyMode.ScaleX, .92f, .10f, 0f, .09f, .10f, 1f, .30f, false);
                Add(profile, "head_height", AnatomyMode.ScaleY, .92f, .10f, 0f, .09f, .10f, 1f, .30f, false);
                Add(profile, "head_depth", AnatomyMode.ScaleZ, .92f, .10f, 0f, .09f, .10f, 1f, .30f, false);
                Add(profile, "eye_spacing", AnatomyMode.TranslateX, .925f, .032f, .034f, .030f, .65f, .40f, .018f, true);
                Add(profile, "eye_height", AnatomyMode.TranslateY, .925f, .032f, .034f, .030f, .65f, .40f, .012f, true);
                Add(profile, "eye_projection", AnatomyMode.TranslateZ, .925f, .032f, .034f, .030f, .65f, .40f, .012f, true);
                Add(profile, "nose_width", AnatomyMode.ScaleX, .905f, .044f, 0f, .032f, .72f, .40f, .34f, false);
                Add(profile, "nose_height", AnatomyMode.ScaleY, .905f, .044f, 0f, .032f, .72f, .40f, .30f, false);
                Add(profile, "mouth_width", AnatomyMode.ScaleX, .875f, .030f, 0f, .052f, .68f, .36f, .34f, false);
                Add(profile, "mouth_height", AnatomyMode.ScaleY, .875f, .030f, 0f, .052f, .68f, .36f, .30f, false);
                Add(profile, "jaw_width", AnatomyMode.ScaleX, .855f, .060f, 0f, .070f, .25f, .78f, .30f, false);
                Add(profile, "chin_width", AnatomyMode.ScaleX, .855f, .038f, 0f, .040f, .58f, .48f, .32f, false);
                Add(profile, "chin_length", AnatomyMode.TranslateZ, .855f, .038f, 0f, .040f, .58f, .48f, .013f, false);
                Add(profile, "shoulder_width", AnatomyMode.LengthX, .76f, .105f, .125f, .12f, 0f, 1f, .55f, true);
                Add(profile, "chest_projection", AnatomyMode.TranslateZ, .69f, .15f, 0f, .14f, .55f, .50f, .025f, false);
                Add(profile, "belly", AnatomyMode.TranslateZ, .55f, .13f, 0f, .18f, .58f, .60f, .028f, false);
                Add(profile, "leg_length", AnatomyMode.LengthY, .25f, .25f, .067f, .075f, 0f, 1f, .55f, true);
            }

            private static void Add(AnatomyProfile profile, string name, AnatomyMode mode,
                                    float verticalPosition, float verticalRange,
                                    float lateralPosition, float lateralRange,
                                    float depthPosition, float depthRange, float strength, bool bilateral)
            {
                profile.AdditionalSpecs[name] = new AnatomySpec(name, mode, verticalPosition, verticalRange,
                    lateralPosition, lateralRange, depthPosition, depthRange, strength, bilateral);
            }
        }

        internal enum AnatomyMode { Radial, ScaleX, ScaleY, ScaleZ, TranslateX, TranslateY, TranslateZ, Posture, LengthX, LengthY }

        internal sealed class AnatomySpec
        {
            public readonly string Name;
            public readonly AnatomyMode Mode;
            public readonly bool Bilateral;
            public float VerticalPosition;
            public float VerticalRange;
            public float LateralPosition;
            public float LateralRange;
            public float DepthPosition;
            public float DepthRange;
            public float Strength;

            public AnatomySpec(string name, AnatomyMode mode, float verticalPosition, float verticalRange,
                               float lateralPosition, float lateralRange, float depthPosition,
                               float depthRange, float strength, bool bilateral)
            {
                Name = name; Mode = mode; Bilateral = bilateral;
                VerticalPosition = verticalPosition; VerticalRange = verticalRange;
                LateralPosition = lateralPosition; LateralRange = lateralRange;
                DepthPosition = depthPosition; DepthRange = depthRange; Strength = strength;
            }
        }
    }

    [HarmonyPatch(typeof(VisEquipment), "UpdateBaseModel")]
    public static class AnatomyBaseModelPatch
    {
        public static void Postfix(VisEquipment __instance)
        {
            AnatomyDeformer.EnsureBodyMesh(__instance);
        }
    }
}
