using System.IO;
using System.Linq;
using BL23.Game.Mansion;
using BL23.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.EditorTools.Mansion
{
    /// <summary>Renders every ItemCatalog item (PropFactory) and every trace type on a grid for visual QA.</summary>
    public static class MansionItemSheet
    {
        [MenuItem("BL23/Mansion/QA item sheet")]
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MansionMats.Init();
            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Shots");
            Directory.CreateDirectory(outDir);
            // floor
            var mb = new MeshBuilder();
            mb.Set(S.Parquet, new Color(0.9f, 0.85f, 0.8f));
            mb.Quad(new Vector3(-2, 0, -2), new Vector3(-2, 0, 12), new Vector3(12, 0, 12), new Vector3(12, 0, -2));
            var floor = new GameObject("floor"); floor.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("floor", out var slots); floor.AddComponent<MeshRenderer>().sharedMaterials = MansionMats.Materials(slots);
            Shader.SetGlobalVector("_BL_AmbientParams", new Vector4(0, 0, 1, 0));
            Shader.SetGlobalVector("_BL_CircuitPower0", Vector4.one); Shader.SetGlobalVector("_BL_CircuitPower1", Vector4.one);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.35f, 0.33f, 0.38f);
            var sun = new GameObject("key").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.6f; sun.color = new Color(1f, 0.92f, 0.85f); sun.transform.rotation = Quaternion.Euler(50, -30, 0); sun.shadows = LightShadows.Soft;
            var items = ItemCatalog.All.ToList();
            int cols = 9;
            for (int i = 0; i < items.Count; i++)
            {
                var go = PropFactory.CreateItem(items[i], "qa_" + items[i].Type);
                go.transform.position = new Vector3((i % cols) * 1.0f, 0.02f, (i / cols) * 1.0f);
                go.transform.rotation = Quaternion.Euler(0, 30, 0);
                var rb = go.GetComponent<Rigidbody>(); if (rb != null) rb.isKinematic = true;
            }
            // traces on the floor
            var types = TraceFactory.Types;
            for (int i = 0; i < types.Length; i++)
                TraceFactory.Create(types[i], new Vector3(i * 0.75f, 0.0f, 8.5f), Vector3.up, 0.6f, TraceFactory.DefaultTint(types[i]));
            var cam = new GameObject("cam").AddComponent<Camera>();
            MansionAtmosphere.SetupCamera(cam);
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            var rt = new RenderTexture(1600, 900, 24);
            void Shot(string name, Vector3 pos, Vector3 look, float fov)
            {
                cam.transform.position = pos; cam.transform.LookAt(look); cam.fieldOfView = fov; cam.targetTexture = rt;
                var req = new RenderPipeline.StandardRequest { destination = rt };
                RenderPipeline.SubmitRenderRequest(cam, req);
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            Shot("Items_A", new Vector3(4f, 2.6f, -2.2f), new Vector3(4f, 0f, 2.2f), 55);
            Shot("Items_B", new Vector3(4f, 2.2f, 1.5f), new Vector3(4f, 0f, 5.5f), 55);
            Shot("Traces", new Vector3(5.3f, 3.0f, 6.0f), new Vector3(5.3f, 0f, 8.5f), 60);
            Debug.Log("[MansionItemSheet] done, items=" + items.Count);
        }
    }
}
