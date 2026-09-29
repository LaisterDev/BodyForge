#if BODYFORGE_DEV
using System;
using System.IO;
using UnityEngine;

namespace BodyForge
{
    // Records topology metadata for both vanilla body models. No vertices,
    // textures, materials, or other game assets are copied to disk.
    public static class AnatomyMeshInventory
    {
        private static bool _captured;

        public static void Capture(VisEquipment equipment)
        {
            if (_captured || equipment == null || !equipment.m_isPlayer || equipment.m_models == null) return;
            try
            {
                MiniJson.Node root = MiniJson.Node.MakeObject();
                root.Object["schemaVersion"] = MiniJson.Node.MakeNumber(1);
                root.Object["gameVersion"] = MiniJson.Node.MakeString(Version.CurrentVersion.ToString());
                MiniJson.Node models = MiniJson.Node.MakeArray();
                for (int index = 0; index < equipment.m_models.Length; index++)
                {
                    Mesh mesh = equipment.m_models[index]?.m_mesh;
                    if (mesh == null) continue;
                    MiniJson.Node model = MiniJson.Node.MakeObject();
                    model.Object["index"] = MiniJson.Node.MakeNumber(index);
                    model.Object["profile"] = MiniJson.Node.MakeString(index == 0 ? "male" : index == 1 ? "female" : "model-" + index);
                    model.Object["mesh"] = MiniJson.Node.MakeString(mesh.name);
                    model.Object["readable"] = MiniJson.Node.MakeBool(mesh.isReadable);
                    model.Object["vertexCount"] = MiniJson.Node.MakeNumber(mesh.vertexCount);
                    model.Object["triangleCount"] = MiniJson.Node.MakeNumber(mesh.isReadable ? mesh.triangles.Length / 3 : -1);
                    model.Object["subMeshCount"] = MiniJson.Node.MakeNumber(mesh.subMeshCount);
                    model.Object["boundsCenter"] = VectorNode(mesh.bounds.center);
                    model.Object["boundsSize"] = VectorNode(mesh.bounds.size);
                    models.Array.Add(model);
                }
                root.Object["models"] = models;
                Directory.CreateDirectory(ProportionConfig.TokenFolder);
                string path = Path.Combine(ProportionConfig.TokenFolder, "anatomy-mesh-inventory.json");
                File.WriteAllText(path, MiniJson.Serialize(root) + "\n");
                _captured = true;
                BodyForgePlugin.LogInfo("anatomy mesh inventory written: " + path);
            }
            catch (Exception error)
            {
                BodyForgePlugin.LogWarn("anatomy mesh inventory failed: " + error.Message);
            }
        }

        private static MiniJson.Node VectorNode(Vector3 value)
        {
            MiniJson.Node array = MiniJson.Node.MakeArray();
            array.Array.Add(MiniJson.Node.MakeNumber(value.x));
            array.Array.Add(MiniJson.Node.MakeNumber(value.y));
            array.Array.Add(MiniJson.Node.MakeNumber(value.z));
            return array;
        }
    }
}
#endif
