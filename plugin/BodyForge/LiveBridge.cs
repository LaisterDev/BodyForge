using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BodyForge
{
    public static class LiveBridge
    {
        private const double PollSeconds = 0.10;
        private static double _nextPoll;
        private static long _lastWriteTicks;
        private static long _lastSequence = -1;
        private static long _lastSavedSequence = -1;
        private static long _lastErrorSequence = -1;
        private static string _lastError = "";
        private static string _lastStatus = "";
        private static long _lastHeartbeat;
        private static MiniJson.Node _hairs;
        private static MiniJson.Node _beards;
        private static long _lastSessionWriteTicks;
        private static bool _editorActive;
        private static long _editorHeartbeat;
        private static string _editorCharacter = "";

        private static string CommandPath => Path.Combine(ProportionConfig.TokenFolder, "live-command.json");
        private static string StatusPath => Path.Combine(ProportionConfig.TokenFolder, "live-status.json");
        private static string SessionPath => Path.Combine(ProportionConfig.TokenFolder, "live-session.json");

        public static bool IsEditorActive
        {
            get
            {
                if (!_editorActive || DateTimeOffset.UtcNow.ToUnixTimeSeconds() - _editorHeartbeat > 2) return false;
                PlayerProfile profile = Game.instance?.GetPlayerProfile();
                return profile != null && string.Equals(_editorCharacter, profile.GetFilename(), StringComparison.OrdinalIgnoreCase);
            }
        }

        public static void Tick()
        {
            if (Time.realtimeSinceStartupAsDouble < _nextPoll) return;
            _nextPoll = Time.realtimeSinceStartupAsDouble + PollSeconds;
            try
            {
                Directory.CreateDirectory(ProportionConfig.TokenFolder);
                ReadSession();
                WriteStatus();
                if (!File.Exists(CommandPath)) return;
                long writeTicks = File.GetLastWriteTimeUtc(CommandPath).Ticks;
                if (writeTicks == _lastWriteTicks) return;
                _lastWriteTicks = writeTicks;
                Apply(File.ReadAllText(CommandPath));
            }
            catch (Exception e)
            {
                BodyForgePlugin.LogError("live bridge failed: " + e.Message);
            }
        }

        private static void ReadSession()
        {
            if (!File.Exists(SessionPath)) return;
            long writeTicks = File.GetLastWriteTimeUtc(SessionPath).Ticks;
            if (writeTicks == _lastSessionWriteTicks) return;
            _lastSessionWriteTicks = writeTicks;
            MiniJson.Node root = MiniJson.Parse(File.ReadAllText(SessionPath));
            if ((int)(root.Get("schemaVersion")?.AsNumber(-1) ?? -1) != 1) return;
            _editorActive = root.Get("active")?.AsBool(false) ?? false;
            _editorHeartbeat = (long)(root.Get("heartbeat")?.AsNumber(0) ?? 0);
            _editorCharacter = root.Get("character")?.AsString("") ?? "";
        }

        private static void Apply(string text)
        {
            MiniJson.Node root = MiniJson.Parse(text);
            if ((int)(root.Get("schemaVersion")?.AsNumber(-1) ?? -1) != 1)
                throw new FormatException("live command schemaVersion must be 1");
            long sequence = (long)(root.Get("sequence")?.AsNumber(-1) ?? -1);
            if (sequence <= _lastSequence) return;

            PlayerProfile profile = Game.instance?.GetPlayerProfile();
            Player player = Player.m_localPlayer;
            if (profile == null || player == null) return;
            string target = root.Get("character")?.AsString("") ?? "";
            if (!string.Equals(target, profile.GetFilename(), StringComparison.OrdinalIgnoreCase)) return;

            MiniJson.Node appearance = root.Get("appearance");
            if (appearance != null && appearance.Type == MiniJson.NodeType.Object)
            {
                player.SetPlayerModel((int)(appearance.Get("model")?.AsNumber(player.GetPlayerModel()) ?? player.GetPlayerModel()));
                player.SetHair(appearance.Get("hair")?.AsString(player.GetHair()) ?? player.GetHair());
                player.SetBeard(appearance.Get("beard")?.AsString(player.GetBeard()) ?? player.GetBeard());
                player.SetSkinColor(ReadVector(appearance.Get("skin"), Vector3.one));
                player.SetHairColor(ReadVector(appearance.Get("haircolor"), player.GetHairColor()));
            }

            ProportionConfig proportions = ProportionConfig.LoadJson(text);
            BoneScalerPatch.ApplyLive(proportions);
            MultiplayerSync.PublishLocal(proportions);
            _lastSequence = sequence;
            if (root.Get("save")?.AsBool(false) ?? false)
            {
                try
                {
                    profile.SavePlayerData(player);
                    if (!profile.Save()) throw new IOException("Valheim profile save returned false");
                    _lastSavedSequence = sequence;
                    _lastError = "";
                    BodyForgePlugin.LogInfo($"character {target} saved by live command {sequence}");
                }
                catch (Exception e)
                {
                    _lastErrorSequence = sequence;
                    _lastError = e.Message;
                    BodyForgePlugin.LogError($"character {target} save failed: {e.Message}");
                }
            }
            BodyForgePlugin.LogInfo($"live update {sequence} applied to {target}");
            WriteStatus(force: true);
        }

        private static Vector3 ReadVector(MiniJson.Node node, Vector3 fallback)
        {
            if (node == null || node.Type != MiniJson.NodeType.Array || node.Array.Count < 3) return fallback;
            return new Vector3(
                (float)node.Array[0].AsNumber(fallback.x),
                (float)node.Array[1].AsNumber(fallback.y),
                (float)node.Array[2].AsNumber(fallback.z));
        }

        private static void WriteStatus(bool force = false)
        {
            PlayerProfile profile = Game.instance?.GetPlayerProfile();
            Player player = Player.m_localPlayer;
            MiniJson.Node root = MiniJson.Node.MakeObject();
            root.Object["schemaVersion"] = MiniJson.Node.MakeNumber(1);
            root.Object["connected"] = MiniJson.Node.MakeBool(profile != null && player != null);
            root.Object["character"] = MiniJson.Node.MakeString(profile?.GetFilename() ?? "");
            root.Object["lastSequence"] = MiniJson.Node.MakeNumber(_lastSequence);
            root.Object["lastSavedSequence"] = MiniJson.Node.MakeNumber(_lastSavedSequence);
            root.Object["lastErrorSequence"] = MiniJson.Node.MakeNumber(_lastErrorSequence);
            root.Object["lastError"] = MiniJson.Node.MakeString(_lastError);
            if (player != null)
            {
                MiniJson.Node appearance = MiniJson.Node.MakeObject();
                appearance.Object["model"] = MiniJson.Node.MakeNumber(player.GetPlayerModel());
                appearance.Object["hair"] = MiniJson.Node.MakeString(player.GetHair());
                appearance.Object["beard"] = MiniJson.Node.MakeString(player.GetBeard());
                Vector3 skin = (Vector3)HarmonyLib.AccessTools.Field(typeof(Player), "m_skinColor").GetValue(player);
                appearance.Object["skin"] = VectorNode(skin);
                appearance.Object["haircolor"] = VectorNode(player.GetHairColor());
                root.Object["appearance"] = appearance;
            }
            long heartbeat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            root.Object["heartbeat"] = MiniJson.Node.MakeNumber(heartbeat);
            if (ObjectDB.instance != null && (_hairs == null || _beards == null))
            {
                _hairs = Names("Hair");
                _beards = Names("Beard");
            }
            root.Object["hairs"] = _hairs ?? MiniJson.Node.MakeArray();
            root.Object["beards"] = _beards ?? MiniJson.Node.MakeArray();
            string text = MiniJson.Serialize(root);
            if (!force && text == _lastStatus && heartbeat == _lastHeartbeat) return;
            _lastStatus = text;
            _lastHeartbeat = heartbeat;
            string temp = StatusPath + ".tmp";
            File.WriteAllText(temp, text);
            if (File.Exists(StatusPath)) File.Delete(StatusPath);
            File.Move(temp, StatusPath);
        }

        private static MiniJson.Node VectorNode(Vector3 value)
        {
            MiniJson.Node array = MiniJson.Node.MakeArray();
            array.Array.Add(MiniJson.Node.MakeNumber(value.x));
            array.Array.Add(MiniJson.Node.MakeNumber(value.y));
            array.Array.Add(MiniJson.Node.MakeNumber(value.z));
            return array;
        }

        private static MiniJson.Node Names(string category)
        {
            MiniJson.Node array = MiniJson.Node.MakeArray();
            List<ItemDrop> items = ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Customization, category);
            foreach (string name in items.Select(x => x.gameObject.name)
                         .Where(x => !x.Contains("_")).Distinct().OrderBy(x => x, StringComparer.Ordinal))
                array.Array.Add(MiniJson.Node.MakeString(name));
            return array;
        }
    }
}
