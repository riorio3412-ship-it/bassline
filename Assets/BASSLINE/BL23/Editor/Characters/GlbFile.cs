using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>Minimal GLB (binary glTF 2.0) reader: triangle primitives, float POSITION/NORMAL/TEXCOORD_0, uint/ushort indices, embedded images.</summary>
    public sealed class GlbFile
    {
        public sealed class Prim
        {
            public string MeshName;
            public int Material = -1;
            public Vector3[] Pos, Nrm;
            public Vector2[] UV;
            public int[] Idx;
        }

        public sealed class Mat
        {
            public string Name;
            public Color BaseColor = Color.white;
            public int BaseColorImage = -1;
            public int NormalImage = -1;
            public float Metallic, Roughness = 1f;
        }

        public readonly List<Prim> Prims = new List<Prim>();
        public readonly List<Mat> Materials = new List<Mat>();
        public readonly List<byte[]> Images = new List<byte[]>();
        JObject _json;
        byte[] _bin;

        public static GlbFile Load(string path)
        {
            var f = new GlbFile();
            var all = File.ReadAllBytes(path);
            uint magic = BitConverter.ToUInt32(all, 0);
            if (magic != 0x46546C67) throw new Exception("not a GLB: " + path);
            int off = 12;
            while (off < all.Length)
            {
                int len = BitConverter.ToInt32(all, off);
                uint type = BitConverter.ToUInt32(all, off + 4);
                if (type == 0x4E4F534A) f._json = JObject.Parse(Encoding.UTF8.GetString(all, off + 8, len));
                else if (type == 0x004E4942) { f._bin = new byte[len]; Buffer.BlockCopy(all, off + 8, f._bin, 0, len); }
                off += 8 + len;
            }
            f.Parse();
            return f;
        }

        void Parse()
        {
            var images = _json["images"] as JArray;
            if (images != null)
                foreach (var im in images)
                {
                    int bv = (int)im["bufferView"];
                    Images.Add(View(bv));
                }
            var textures = _json["textures"] as JArray;
            var mats = _json["materials"] as JArray;
            if (mats != null)
                foreach (var m in mats)
                {
                    var mm = new Mat { Name = (string)m["name"] ?? "" };
                    var pbr = m["pbrMetallicRoughness"];
                    if (pbr != null)
                    {
                        var bcf = pbr["baseColorFactor"] as JArray;
                        if (bcf != null) mm.BaseColor = new Color((float)bcf[0], (float)bcf[1], (float)bcf[2], (float)bcf[3]);
                        var bct = pbr["baseColorTexture"];
                        if (bct != null && textures != null) mm.BaseColorImage = (int)textures[(int)bct["index"]]["source"];
                        if (pbr["metallicFactor"] != null) mm.Metallic = (float)pbr["metallicFactor"];
                        if (pbr["roughnessFactor"] != null) mm.Roughness = (float)pbr["roughnessFactor"];
                    }
                    var nt = m["normalTexture"];
                    if (nt != null && textures != null) mm.NormalImage = (int)textures[(int)nt["index"]]["source"];
                    Materials.Add(mm);
                }
            var meshes = (JArray)_json["meshes"];
            var nodes = (JArray)_json["nodes"];
            foreach (var node in nodes)
            {
                if (node["mesh"] == null) continue;
                var mesh = meshes[(int)node["mesh"]];
                Matrix4x4 mtx = Matrix4x4.identity;
                var ma = node["matrix"] as JArray;
                if (ma != null)
                    for (int i = 0; i < 16; i++) mtx[i % 4, i / 4] = (float)ma[i];
                foreach (var p in (JArray)mesh["primitives"])
                {
                    int mode = p["mode"] != null ? (int)p["mode"] : 4;
                    if (mode != 4) continue;
                    var attr = p["attributes"];
                    var prim = new Prim { MeshName = (string)node["name"] ?? (string)mesh["name"] ?? "mesh" };
                    prim.Pos = Vec3((int)attr["POSITION"]);
                    for (int i = 0; i < prim.Pos.Length; i++) prim.Pos[i] = mtx.MultiplyPoint3x4(prim.Pos[i]);
                    prim.Nrm = attr["NORMAL"] != null ? Vec3((int)attr["NORMAL"]) : null;
                    prim.UV = attr["TEXCOORD_0"] != null ? Vec2((int)attr["TEXCOORD_0"]) : null;
                    prim.Idx = p["indices"] != null ? Indices((int)p["indices"]) : Seq(prim.Pos.Length);
                    if (p["material"] != null) prim.Material = (int)p["material"];
                    Prims.Add(prim);
                }
            }
        }

        static int[] Seq(int n) { var a = new int[n]; for (int i = 0; i < n; i++) a[i] = i; return a; }

        byte[] View(int bv)
        {
            var v = _json["bufferViews"][bv];
            int off = v["byteOffset"] != null ? (int)v["byteOffset"] : 0;
            int len = (int)v["byteLength"];
            var b = new byte[len];
            Buffer.BlockCopy(_bin, off, b, 0, len);
            return b;
        }

        void AccessorInfo(int acc, out int offset, out int count, out int stride, out int comp)
        {
            var a = _json["accessors"][acc];
            var v = _json["bufferViews"][(int)a["bufferView"]];
            offset = (v["byteOffset"] != null ? (int)v["byteOffset"] : 0) + (a["byteOffset"] != null ? (int)a["byteOffset"] : 0);
            count = (int)a["count"];
            stride = v["byteStride"] != null ? (int)v["byteStride"] : 0;
            comp = (int)a["componentType"];
        }

        Vector3[] Vec3(int acc)
        {
            AccessorInfo(acc, out int off, out int count, out int stride, out _);
            if (stride == 0) stride = 12;
            var r = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                int o = off + i * stride;
                r[i] = new Vector3(BitConverter.ToSingle(_bin, o), BitConverter.ToSingle(_bin, o + 4), BitConverter.ToSingle(_bin, o + 8));
            }
            return r;
        }

        Vector2[] Vec2(int acc)
        {
            AccessorInfo(acc, out int off, out int count, out int stride, out _);
            if (stride == 0) stride = 8;
            var r = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                int o = off + i * stride;
                r[i] = new Vector2(BitConverter.ToSingle(_bin, o), BitConverter.ToSingle(_bin, o + 4));
            }
            return r;
        }

        int[] Indices(int acc)
        {
            AccessorInfo(acc, out int off, out int count, out int stride, out int comp);
            var r = new int[count];
            for (int i = 0; i < count; i++)
            {
                if (comp == 5125) r[i] = (int)BitConverter.ToUInt32(_bin, off + i * 4);
                else if (comp == 5123) r[i] = BitConverter.ToUInt16(_bin, off + i * 2);
                else r[i] = _bin[off + i];
            }
            return r;
        }
    }
}
