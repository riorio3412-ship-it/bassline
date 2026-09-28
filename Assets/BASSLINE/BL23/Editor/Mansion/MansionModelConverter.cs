using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BL23.Game.Mansion;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.EditorTools.Mansion
{
    /// <summary>
    /// Converts downloaded Poly Haven glTF (1k) models into MansionModel assets (mesh + material descriptors) so the
    /// runtime needs no glTF importer. Textures are copied next to the model under Art/Mansion/Models/&lt;id&gt;.
    /// Batch: -executeMethod BL23.EditorTools.Mansion.MansionModelConverter.ConvertAll -gltfSrc C:/path/to/models
    /// </summary>
    public static class MansionModelConverter
    {
        const string ArtDir = "Assets/BASSLINE/BL23/Art/Mansion/Models";
        const string ResDir = "Assets/BASSLINE/BL23/Resources/Mansion/Models";

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        [MenuItem("BL23/Mansion/Convert glTF models")]
        public static void ConvertAll()
        {
            string src = Arg("-gltfSrc") ?? "C:/Users/리오/BL23Lab/dl/models";
            Directory.CreateDirectory(ArtDir); Directory.CreateDirectory(ResDir);
            var dirs = Directory.GetDirectories(src);
            // pass 1: copy textures and import them
            var jobs = new List<(string id, string gltf, JObject json)>();
            foreach (var d in dirs)
            {
                string id = Path.GetFileName(d);
                string gltf = Path.Combine(d, id + ".gltf");
                if (!File.Exists(gltf)) continue;
                try
                {
                    var json = JObject.Parse(File.ReadAllText(gltf));
                    var images = json["images"] as JArray;
                    var used = new HashSet<string>();
                    if (images != null) foreach (var im in images) { var uri = (string)im["uri"]; if (uri != null) used.Add(Uri.UnescapeDataString(uri)); }
                    string outDir = ArtDir + "/" + id;
                    Directory.CreateDirectory(outDir);
                    foreach (var u in used)
                    {
                        string from = Path.Combine(d, u);
                        string to = Path.Combine(outDir, Path.GetFileName(u));
                        if (File.Exists(from) && !File.Exists(to)) File.Copy(from, to);
                    }
                    jobs.Add((id, gltf, json));
                }
                catch (Exception e) { Debug.LogError("[ModelConverter] " + id + ": " + e.Message); }
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // pass 2: build meshes
            int ok = 0;
            foreach (var (id, gltf, json) in jobs)
            {
                try { if (Convert(id, gltf, json)) ok++; }
                catch (Exception e) { Debug.LogError("[ModelConverter] " + id + ": " + e); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[ModelConverter] converted {ok}/{jobs.Count} models");
        }

        static readonly string[] FabricHints = { "fabric", "cloth", "cushion", "seat", "velvet", "upholster", "pillow", "sofa", "armchair_01_fabric", "textile", "bed_sheet", "sheet", "blanket", "mattress", "curtain" };

        static bool Convert(string id, string gltfPath, JObject j)
        {
            string dir = Path.GetDirectoryName(gltfPath);
            var buffers = (JArray)j["buffers"]; var views = (JArray)j["bufferViews"]; var accessors = (JArray)j["accessors"];
            var bins = new List<byte[]>();
            foreach (var b in buffers) bins.Add(File.ReadAllBytes(Path.Combine(dir, Uri.UnescapeDataString((string)b["uri"]))));
            var meshes = (JArray)j["meshes"]; var nodes = (JArray)j["nodes"];
            var materials = j["materials"] as JArray;

            // gather primitive instances through the node tree
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
            var bySub = new SortedDictionary<int, List<int>>();
            int sceneIdx = j["scene"] != null ? (int)j["scene"] : 0;
            var roots = (JArray)j["scenes"][sceneIdx]["nodes"];
            void Walk(int ni, Matrix4x4 parent)
            {
                var n = (JObject)nodes[ni];
                Matrix4x4 local = Matrix4x4.identity;
                if (n["matrix"] is JArray m)
                {
                    for (int c = 0; c < 4; c++) for (int r = 0; r < 4; r++) local[r, c] = (float)m[c * 4 + r];
                }
                else
                {
                    var t = n["translation"] is JArray ta ? new Vector3((float)ta[0], (float)ta[1], (float)ta[2]) : Vector3.zero;
                    var q = n["rotation"] is JArray ra ? new Quaternion((float)ra[0], (float)ra[1], (float)ra[2], (float)ra[3]) : Quaternion.identity;
                    var s = n["scale"] is JArray sa ? new Vector3((float)sa[0], (float)sa[1], (float)sa[2]) : Vector3.one;
                    local = Matrix4x4.TRS(t, q, s);   // math is handedness-agnostic here: we stay in glTF space
                }
                var world = parent * local;
                if (n["mesh"] != null)
                {
                    var mesh = (JObject)meshes[(int)n["mesh"]];
                    foreach (JObject prim in (JArray)mesh["primitives"])
                    {
                        var attr = (JObject)prim["attributes"];
                        if (attr["POSITION"] == null) continue;
                        var pos = ReadVec3(accessors, views, bins, (int)attr["POSITION"]);
                        var nor = attr["NORMAL"] != null ? ReadVec3(accessors, views, bins, (int)attr["NORMAL"]) : null;
                        var uv = attr["TEXCOORD_0"] != null ? ReadVec2(accessors, views, bins, (int)attr["TEXCOORD_0"]) : null;
                        int[] idx = prim["indices"] != null ? ReadIndices(accessors, views, bins, (int)prim["indices"]) : Enumerable.Range(0, pos.Length).ToArray();
                        int mat = prim["material"] != null ? (int)prim["material"] : 0;
                        int baseV = verts.Count;
                        var nm = world.inverse.transpose;
                        bool flip = world.determinant < 0;
                        for (int i = 0; i < pos.Length; i++)
                        {
                            var p = world.MultiplyPoint3x4(pos[i]);
                            var nn = nor != null ? nm.MultiplyVector(nor[i]).normalized : Vector3.up;
                            verts.Add(new Vector3(-p.x, p.y, p.z));           // glTF RH -> Unity LH (mirror X)
                            norms.Add(new Vector3(-nn.x, nn.y, nn.z));
                            uvs.Add(uv != null ? new Vector2(uv[i].x, 1f - uv[i].y) : Vector2.zero);
                        }
                        if (!bySub.TryGetValue(mat, out var list)) bySub[mat] = list = new List<int>();
                        for (int i = 0; i < idx.Length; i += 3)
                        {
                            // mirroring flips winding -> swap (and flip again for negative-scale nodes)
                            if (!flip) { list.Add(baseV + idx[i]); list.Add(baseV + idx[i + 2]); list.Add(baseV + idx[i + 1]); }
                            else { list.Add(baseV + idx[i]); list.Add(baseV + idx[i + 1]); list.Add(baseV + idx[i + 2]); }
                        }
                    }
                }
                if (n["children"] is JArray ch) foreach (var c in ch) Walk((int)c, world);
            }
            foreach (var r in roots) Walk((int)r, Matrix4x4.identity);
            if (verts.Count == 0) return false;

            var um = new Mesh { name = id };
            if (verts.Count > 65000) um.indexFormat = IndexFormat.UInt32;
            um.SetVertices(verts); um.SetNormals(norms); um.SetUVs(0, uvs);
            var mats = bySub.Keys.ToList();
            um.subMeshCount = mats.Count;
            for (int s = 0; s < mats.Count; s++) um.SetTriangles(bySub[mats[s]], s, true);
            um.RecalculateBounds();
            um.RecalculateTangents();
            um.UploadMeshData(false);

            var model = ScriptableObject.CreateInstance<MansionModel>();
            model.name = id; model.SourceId = id; model.Author = "Poly Haven (CC0)";
            model.Mesh = um; model.Bounds = um.bounds;
            model.Triangles = bySub.Values.Sum(l => l.Count) / 3;
            var parts = new List<MansionModel.Part>();
            string outDir = ArtDir + "/" + id;
            foreach (int mi in mats)
            {
                var part = new MansionModel.Part();
                var mj = materials != null && mi < materials.Count ? (JObject)materials[mi] : null;
                part.Name = mj != null ? (string)mj["name"] ?? ("mat" + mi) : "mat" + mi;
                if (mj != null)
                {
                    var pbr = mj["pbrMetallicRoughness"] as JObject;
                    if (pbr != null)
                    {
                        if (pbr["baseColorFactor"] is JArray bc) part.BaseColor = new Color((float)bc[0], (float)bc[1], (float)bc[2], (float)bc[3]).gamma;
                        part.Metallic = pbr["metallicFactor"] != null ? (float)pbr["metallicFactor"] : 1f;
                        part.Roughness = pbr["roughnessFactor"] != null ? (float)pbr["roughnessFactor"] : 1f;
                        part.BaseMap = Tex(j, pbr["baseColorTexture"], outDir);
                        part.MaskMap = Tex(j, pbr["metallicRoughnessTexture"], outDir);
                    }
                    part.NormalMap = Tex(j, mj["normalTexture"], outDir);
                    string alpha = (string)mj["alphaMode"];
                    part.AlphaClip = alpha == "MASK" || alpha == "BLEND";
                    part.DoubleSided = mj["doubleSided"] != null && (bool)mj["doubleSided"];
                }
                string lname = (part.Name + " " + (part.BaseMap != null ? part.BaseMap.name : "")).ToLowerInvariant();
                part.Recolor = FabricHints.Any(h => lname.Contains(h));
                parts.Add(part);
            }
            model.Parts = parts.ToArray();
            string path = ResDir + "/" + id + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<MansionModel>(path);
            if (old != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(model, path);
            AssetDatabase.AddObjectToAsset(um, model);
            EditorUtility.SetDirty(model);
            Debug.Log($"[ModelConverter] {id}: {verts.Count} verts, {model.Triangles} tris, {parts.Count} parts, bounds {model.Bounds.size}, fabric parts: {string.Join(",", parts.Where(p => p.Recolor).Select(p => p.Name))}");
            return true;
        }

        static Texture2D Tex(JObject j, JToken texRef, string outDir)
        {
            if (texRef == null || texRef["index"] == null) return null;
            var tex = (JObject)j["textures"][(int)texRef["index"]];
            if (tex["source"] == null) return null;
            var img = (JObject)j["images"][(int)tex["source"]];
            var uri = (string)img["uri"]; if (uri == null) return null;
            string p = outDir + "/" + Path.GetFileName(Uri.UnescapeDataString(uri));
            return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }

        // ------------------------------------------------------------------ accessor readers
        static (byte[] data, int offset, int stride) View(JArray acc, JArray views, List<byte[]> bins, int ai, int compSize, int comps)
        {
            var a = (JObject)acc[ai];
            var v = (JObject)views[(int)a["bufferView"]];
            int off = (v["byteOffset"] != null ? (int)v["byteOffset"] : 0) + (a["byteOffset"] != null ? (int)a["byteOffset"] : 0);
            int stride = v["byteStride"] != null ? (int)v["byteStride"] : compSize * comps;
            return (bins[(int)v["buffer"]], off, stride);
        }
        static Vector3[] ReadVec3(JArray acc, JArray views, List<byte[]> bins, int ai)
        {
            int count = (int)acc[ai]["count"]; var (d, off, stride) = View(acc, views, bins, ai, 4, 3);
            var r = new Vector3[count];
            for (int i = 0; i < count; i++) { int o = off + i * stride; r[i] = new Vector3(BitConverter.ToSingle(d, o), BitConverter.ToSingle(d, o + 4), BitConverter.ToSingle(d, o + 8)); }
            return r;
        }
        static Vector2[] ReadVec2(JArray acc, JArray views, List<byte[]> bins, int ai)
        {
            int count = (int)acc[ai]["count"]; int ct = (int)acc[ai]["componentType"];
            int cs = ct == 5126 ? 4 : ct == 5123 ? 2 : 1;
            var (d, off, stride) = View(acc, views, bins, ai, cs, 2);
            bool norm = acc[ai]["normalized"] != null && (bool)acc[ai]["normalized"];
            var r = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                int o = off + i * stride;
                if (ct == 5126) r[i] = new Vector2(BitConverter.ToSingle(d, o), BitConverter.ToSingle(d, o + 4));
                else if (ct == 5123) r[i] = new Vector2(BitConverter.ToUInt16(d, o) / (norm ? 65535f : 1f), BitConverter.ToUInt16(d, o + 2) / (norm ? 65535f : 1f));
                else r[i] = new Vector2(d[o] / (norm ? 255f : 1f), d[o + 1] / (norm ? 255f : 1f));
            }
            return r;
        }
        static int[] ReadIndices(JArray acc, JArray views, List<byte[]> bins, int ai)
        {
            int count = (int)acc[ai]["count"]; int ct = (int)acc[ai]["componentType"];
            int cs = ct == 5125 ? 4 : ct == 5123 ? 2 : 1;
            var (d, off, stride) = View(acc, views, bins, ai, cs, 1);
            var r = new int[count];
            for (int i = 0; i < count; i++)
            {
                int o = off + i * stride;
                r[i] = ct == 5125 ? (int)BitConverter.ToUInt32(d, o) : ct == 5123 ? BitConverter.ToUInt16(d, o) : d[o];
            }
            return r;
        }
    }
}
