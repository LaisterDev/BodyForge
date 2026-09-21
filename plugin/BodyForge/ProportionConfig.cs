// ProportionConfig — loads the character's <char>.vhforges.json from
// BepInEx/config/BodyForge/, the same writable folder the framework itself
// uses for all mod settings. Keeping it out of the game's save folders avoids
// the Steam Cloud virtual filesystem (files laid down next to a .fch in
// userdata/.../remote are invisible to the game until IT registers them).
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BodyForge
{
    public sealed class ProportionConfig
    {
        public sealed class BoneSpec
        {
            public bool Enabled = true;
            public float[] Scale = { 1f, 1f, 1f };
        }

        public int SchemaVersion;
        public readonly Dictionary<string, BoneSpec> Bones = new Dictionary<string, BoneSpec>();

        public static ProportionConfig Current { get; private set; }

        private static bool _warnedNoProfile;
        private static bool _warnedNoConfig;
        private static string _currentProfile = "";

        /// <summary>Folder under BepInEx/config where per-character tokens live.
        /// The editor writes here; the plugin reads the same path. Extensible:
        /// future community/port options (cloud mirror keyed per profile) can be
        /// layered on top without touching callers.</summary>
        public static string TokenFolder
        {
            get { return Path.Combine(BepInEx.Paths.ConfigPath, "BodyForge"); }
        }

        /// <summary>Resolves the local player's profile file name and loads its
        /// <char>.vhforges.json if present. Safe to call each Awake:
        /// missing-file warnings are logged only once.</summary>
        public static ProportionConfig Reload()
        {
            try
            {
                PlayerProfile profile = Game.instance?.GetPlayerProfile();
                if (profile == null)
                {
                    if (!_warnedNoProfile)
                    {
                        _warnedNoProfile = true;
                        BodyForgePlugin.LogInfo("no active profile yet — retrying on next spawn");
                    }
                    Current = null;
                    _currentProfile = "";
                    return null;
                }
                _warnedNoProfile = false;

                return LoadForProfile(profile.GetFilename());
            }
            catch (Exception e)
            {
                BodyForgePlugin.LogError("failed to load proportions: " + e.Message);
                Current = null;
                return null;
            }
        }

        public static ProportionConfig LoadForProfile(string profileName)
        {
            try
            {
                if (string.IsNullOrEmpty(profileName)) return null;
                string path = Path.Combine(TokenFolder, profileName + ".vhforges.json");
                if (!File.Exists(path))
                {
                    if (!_warnedNoConfig)
                    {
                        _warnedNoConfig = true;
                        BodyForgePlugin.LogInfo($"no {profileName}.vhforges.json in BepInEx/config/BodyForge — nothing to apply");
                    }
                    Current = null;
                    _currentProfile = profileName;
                    return null;
                }

                Current = LoadJson(File.ReadAllText(path));
                _warnedNoConfig = false;
                if (_currentProfile != profileName)
                    BodyForgePlugin.LogInfo("loaded proportions from " + path);
                _currentProfile = profileName;
            }
            catch (Exception e)
            {
                BodyForgePlugin.LogError("failed to load proportions: " + e.Message);
                Current = null;
            }
            return Current;
        }

        public static ProportionConfig LoadJson(string text)
        {
            MiniJson.Node root = MiniJson.Parse(text);
            ProportionConfig cfg = new ProportionConfig();
            MiniJson.Node ver = root.Get("schemaVersion");
            cfg.SchemaVersion = ver != null ? (int)ver.AsNumber(-1) : -1;
            if (cfg.SchemaVersion != 1)
            {
                throw new FormatException("unsupported schemaVersion " + cfg.SchemaVersion +
                                          " (expected 1)");
            }
            MiniJson.Node bones = root.Get("bones");
            if (bones == null || bones.Type != MiniJson.NodeType.Object)
            {
                throw new FormatException("missing 'bones' object");
            }
            foreach (KeyValuePair<string, MiniJson.Node> kv in bones.Object)
            {
                string boneName = kv.Key;
                MiniJson.Node spec = kv.Value;
                if (spec == null || spec.Type != MiniJson.NodeType.Object) continue;
                if (spec.Get("enabled") != null && !spec.Get("enabled").AsBool(true)) continue;
                MiniJson.Node scale = spec.Get("scale");
                if (scale == null || scale.Type != MiniJson.NodeType.Array || scale.Array.Count != 3) continue;
                float sx = (float)scale.Array[0].AsNumber(1);
                float sy = (float)scale.Array[1].AsNumber(1);
                float sz = (float)scale.Array[2].AsNumber(1);
                cfg.Bones[boneName] = new BoneSpec { Enabled = true, Scale = new[] { sx, sy, sz } };
            }
            return cfg;
        }
    }
}
