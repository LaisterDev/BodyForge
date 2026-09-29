using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace BodyForge
{
    internal static class AnatomyProfileCalibration
    {
        private const string ResourceName = "BodyForge.anatomy_calibration.json";

        private static readonly Dictionary<string, string> StandardPrefixes =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "head", "Head" }, { "jaw", "Jaw" }, { "cheeks", "Cheeks" },
                { "forehead", "Forehead" }, { "nose", "Nose" }, { "chin", "Chin" },
                { "eyes", "Eyes" }, { "mouth", "Mouth" }, { "ears", "Ears" },
                { "neck", "Neck" }, { "shoulders", "Shoulders" }, { "chest", "Chest" },
                { "waist", "Waist" }, { "upper_back", "UpperBack" },
                { "upper_arms", "UpperArms" }, { "forearms", "Forearms" },
                { "hands", "Hands" }, { "abdomen", "Abdomen" }, { "hips", "Hips" },
                { "glutes", "Glutes" }, { "thighs", "Thigh" }, { "knees", "Knees" },
                { "calves", "Calves" }, { "feet", "Feet" }
            };

        private static readonly Dictionary<string, string> StandardSuffixes =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "verticalPosition", "Height" }, { "verticalRange", "Range" },
                { "lateralPosition", "LateralPosition" }, { "lateralRange", "LateralRange" },
                { "depthPosition", "DepthPosition" }, { "depthRange", "DepthRange" },
                { "strength", "Strength" }, { "innerRadius", "InnerRadius" },
                { "outerRadius", "OuterRadius" }, { "widthRatio", "WidthRatio" },
                { "depthRatio", "DepthRatio" }
            };

        private static readonly Dictionary<string, string> AdditionalProperties =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "verticalPosition", "VerticalPosition" }, { "verticalRange", "VerticalRange" },
                { "lateralPosition", "LateralPosition" }, { "lateralRange", "LateralRange" },
                { "depthPosition", "DepthPosition" }, { "depthRange", "DepthRange" },
                { "strength", "Strength" }
            };

        private static readonly Dictionary<int, Dictionary<string, Dictionary<string, float>>> Profiles = Load();

        internal static void ApplyTo(AnatomyDeformer.AnatomyProfile profile, int model)
        {
            Dictionary<string, Dictionary<string, float>> values;
            if (profile == null || !Profiles.TryGetValue(model == 1 ? 1 : 0, out values)) return;

            foreach (KeyValuePair<string, Dictionary<string, float>> entry in values)
            {
                AnatomyDeformer.AnatomySpec spec;
                if (profile.AdditionalSpecs.TryGetValue(entry.Key, out spec))
                {
                    ApplyAdditional(spec, entry.Value);
                    continue;
                }

                string prefix;
                if (entry.Key == "body_height") prefix = "BodyHeight";
                else if (entry.Key == "body_width") prefix = "BodyWidth";
                else if (entry.Key == "body_depth") prefix = "BodyDepth";
                else if (!StandardPrefixes.TryGetValue(entry.Key, out prefix)) continue;

                foreach (KeyValuePair<string, float> parameter in entry.Value)
                {
                    string suffix;
                    if (!StandardSuffixes.TryGetValue(parameter.Key, out suffix)) continue;
                    if (entry.Key.StartsWith("body_", StringComparison.Ordinal))
                    {
                        if (parameter.Key == "verticalPosition") suffix = "Position";
                        else if (parameter.Key == "verticalRange") suffix = "Range";
                    }
                    SetFloat(profile, prefix + suffix, parameter.Value);
                }
            }
        }

#if BODYFORGE_DEV
        internal static void Replace(MiniJson.Node root)
        {
            if (root == null || root.Type != MiniJson.NodeType.Object)
                throw new FormatException("invalid calibration profiles");
            var replacement = new Dictionary<int, Dictionary<string, Dictionary<string, float>>>();
            ReadModel(root, "0", 0, replacement);
            ReadModel(root, "1", 1, replacement);
            if (!replacement.ContainsKey(0) || !replacement.ContainsKey(1))
                throw new FormatException("calibration profiles must contain models 0 and 1");
            Profiles.Clear();
            foreach (KeyValuePair<int, Dictionary<string, Dictionary<string, float>>> entry in replacement)
                Profiles[entry.Key] = entry.Value;
        }
#endif

        private static void ApplyAdditional(AnatomyDeformer.AnatomySpec spec,
                                            Dictionary<string, float> values)
        {
            foreach (KeyValuePair<string, float> parameter in values)
            {
                string propertyName;
                if (!AdditionalProperties.TryGetValue(parameter.Key, out propertyName)) continue;
                SetFloat(spec, propertyName, parameter.Value);
            }
        }

        private static void SetFloat(object target, string fieldName, float value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            if (field != null && field.FieldType == typeof(float)) field.SetValue(target, value);
        }

        private static Dictionary<int, Dictionary<string, Dictionary<string, float>>> Load()
        {
            var result = new Dictionary<int, Dictionary<string, Dictionary<string, float>>>();
            try
            {
                Assembly assembly = typeof(AnatomyProfileCalibration).Assembly;
                using (Stream stream = assembly.GetManifestResourceStream(ResourceName))
                {
                    if (stream == null) return result;
                    using (var reader = new StreamReader(stream))
                    {
                        MiniJson.Node root = MiniJson.Parse(reader.ReadToEnd());
                        ReadModel(root, "0", 0, result);
                        ReadModel(root, "1", 1, result);
                    }
                }
            }
            catch (Exception)
            {
                // Embedded calibration is optional at runtime; hardcoded profiles remain authoritative fallback.
            }
            return result;
        }

        private static void ReadModel(MiniJson.Node root, string key, int model,
            Dictionary<int, Dictionary<string, Dictionary<string, float>>> result)
        {
            MiniJson.Node modelNode = root != null ? root.Get(key) : null;
            if (modelNode == null || modelNode.Type != MiniJson.NodeType.Object) return;
            var fields = new Dictionary<string, Dictionary<string, float>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, MiniJson.Node> fieldEntry in modelNode.Object)
            {
                if (fieldEntry.Value == null || fieldEntry.Value.Type != MiniJson.NodeType.Object) continue;
                var parameters = new Dictionary<string, float>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, MiniJson.Node> parameter in fieldEntry.Value.Object)
                {
                    if (parameter.Value == null || parameter.Value.Type != MiniJson.NodeType.Number) continue;
                    double number = parameter.Value.Number;
                    if (double.IsNaN(number) || double.IsInfinity(number)
                        || number > float.MaxValue || number < -float.MaxValue) continue;
                    parameters[parameter.Key] = (float)number;
                }
                if (parameters.Count > 0) fields[fieldEntry.Key] = parameters;
            }
            result[model] = fields;
        }
    }
}
