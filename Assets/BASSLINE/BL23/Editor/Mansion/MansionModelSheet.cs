using System.IO;
using BL23.Game.Mansion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.EditorTools.Mansion
{
    /// <summary>
    /// Renders every converted CC0 model (Resources/Mansion/Models) from its +Z side (the side furniture presents to
    /// the room) into Shots/Models/&lt;id&gt;.png, so model orientation and proportions can be checked without a build.
    /// Batch: -executeMethod BL23.EditorTools.Mansion.MansionModelSheet.Run [-sheetOut dir] [-sheetOnly a,b,c]
    /// </summary>
    public static class MansionModelSheet
    {
        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        [MenuItem("BL23/Mansion/QA model sheet")]
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MansionMats.Init();
            string outDir = Arg("-sheetOut") ?? Path.Combine(Directory.GetCurrentDirectory(), "Shots", "Models");
            Directory.CreateDirectory(outDir);
            var only = Arg("-sheetOnly");
            Shader.SetGlobalVector("_BL_AmbientParams", new Vector4(0, 0, 1, 0));
            Shader.SetGlobalVector("_BL_CircuitPower0", Vector4.one); Shader.SetGlobalVector("_BL_CircuitPower1", Vector4.one);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.45f, 0.43f, 0.46f);
            var sun = new GameObject("key").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.5f; sun.color = new Color(1f, 0.94f, 0.86f); sun.transform.rotation = Quaternion.Euler(40, 200, 0);
            // floor + a red marker strip on the +Z side (camera side) so the facing is unambiguous
            var mb = new MeshBuilder();
            mb.Set(S.Parquet, new Color(0.8f, 0.78f, 0.75f));
            mb.Quad(new Vector3(-3, 0, -3), new Vector3(-3, 0, 3), new Vector3(3, 0, 3), new Vector3(3, 0, -3));
            mb.Set(S.GlossPaint, new Color(0.8f, 0.1f, 0.1f));
            mb.Box(new Vector3(0, 0.005f, 0.75f), new Vector3(1.2f, 0.01f, 0.05f));
            var floor = new GameObject("floor"); floor.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("floor", out var slots); floor.AddComponent<MeshRenderer>().sharedMaterials = MansionMats.Materials(slots);
            var cam = new GameObject("cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f); cam.allowHDR = false;
            var rt = new RenderTexture(360, 360, 24);
            var models = Resources.LoadAll<MansionModel>("Mansion/Models");
            int n = 0;
            foreach (var m in models)
            {
                if (m == null || m.Mesh == null) continue;
                if (only != null && System.Array.IndexOf(only.Split(','), m.name) < 0) continue;
                var go = Models.Place(m, null, Vector3.zero, 0, new Vector3(1.2f, 1.2f, 1.2f), null, false, Models.Anchor.Bottom, 0f);
                var b = new Bounds(go.transform.position, Vector3.zero);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                float s = Mathf.Max(b.size.x, b.size.y, 0.3f);
                cam.transform.position = new Vector3(0.25f * s, b.center.y + s * 0.35f, 1.9f * s + b.extents.z);
                cam.transform.LookAt(b.center);
                cam.fieldOfView = 40f; cam.targetTexture = rt;
                var req = new RenderPipeline.StandardRequest { destination = rt };
                RenderPipeline.SubmitRenderRequest(cam, req);
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(outDir, m.name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Debug.Log($"[ModelSheet] {m.name} bounds {m.Bounds.size} tris {m.Triangles} parts {m.Parts.Length}");
                Object.DestroyImmediate(go);
                n++;
            }
            Debug.Log($"[ModelSheet] rendered {n} models to {outDir}");
        }
    }
}
