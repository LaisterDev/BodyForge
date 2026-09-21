// BodyForge — BoneDumper (development tool).
//
// Dumps every bone of the local player's body model (m_bodyModel.bones) with
// its default localScale into BepInEx/config/bone_dump.json the first time a
// VisEquipment is built. Use it once to generate the authoritative bone
// catalogue that replaces data/bone_catalogue.v1.json (provisional).
using System;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace BodyForge
{
    [BepInPlugin("com.bodyforge.bonedumper", "BodyForge Bone Dumper", "0.1.0")]
    public class BoneDumperPlugin : BaseUnityPlugin
    {
        private static BoneDumperPlugin _instance;
        private static bool _dumped;

        private void Awake()
        {
            _instance = this;
            Harmony harmony = new Harmony("com.bodyforge.bonedumper");
            harmony.PatchAll();
            Log("BoneDumper loaded");
        }

        public static void Log(string msg) => _instance?.Logger?.LogInfo(msg);

        [HarmonyPatch(typeof(VisEquipment), "Awake")]
        private static class DumpPatch
        {
            public static void Postfix(VisEquipment __instance)
            {
                try
                {
                    if (_dumped || __instance == null || !__instance.m_isPlayer) return;
                    ZNetView nview =
                        (ZNetView)typeof(VisEquipment).GetField("m_nview", BindingFlags.NonPublic | BindingFlags.Instance)
                                                      ?.GetValue(__instance);
                    if (nview == null || !nview.IsOwner()) return;

                    SkinnedMeshRenderer model = __instance.m_bodyModel;
                    if (model == null || model.bones == null || model.bones.Length == 0) return;

                    MiniJson.Node root = MiniJson.Node.MakeObject();
                    try
                    {
                        GameVersion v = Version.CurrentVersion;
                        root.Object["game"] = MiniJson.Node.MakeString(
                            $"{v.m_major}.{v.m_minor}.{v.m_patch}");
                    }
                    catch (Exception)
                    {
                        root.Object["game"] = MiniJson.Node.MakeString("?");
                    }
                    try
                    {
                        string profile = Game.instance?.GetPlayerProfile()?.GetFilename() ?? "?";
                        root.Object["player"] = MiniJson.Node.MakeString(profile);
                    }
                    catch (Exception)
                    {
                        root.Object["player"] = MiniJson.Node.MakeString("?");
                    }
                    MiniJson.Node arr = MiniJson.Node.MakeArray();
                    foreach (Transform t in model.bones)
                    {
                        if (t == null) continue;
                        MiniJson.Node entry = MiniJson.Node.MakeObject();
                        entry.Object["name"] = MiniJson.Node.MakeString(t.name);
                        Vector3 s = t.localScale;
                        MiniJson.Node sc = MiniJson.Node.MakeArray();
                        sc.Array.Add(MiniJson.Node.MakeNumber(s.x));
                        sc.Array.Add(MiniJson.Node.MakeNumber(s.y));
                        sc.Array.Add(MiniJson.Node.MakeNumber(s.z));
                        entry.Object["localScale"] = sc;
                        arr.Array.Add(entry);
                    }
                    root.Object["bones"] = arr;

                    string outPath = Path.Combine(Paths.BepInExRootPath, "config", "bone_dump.json");
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                    File.WriteAllText(outPath, MiniJson.Serialize(root));
                    _dumped = true;
                    Log("bone dump written to " + outPath + " (" + arr.Array.Count + " bones)");
                }
                catch (Exception e)
                {
                    Log("bone dump failed: " + e.Message);
                }
            }
        }
    }
}