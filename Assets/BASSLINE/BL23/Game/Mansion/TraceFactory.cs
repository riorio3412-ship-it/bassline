using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Decal-like traces: blood pools/drips/smears, drag marks, bloody & wet footprints, water puddles, scratches,
    /// dents, cracks, fragments, ash, soil. Lit alpha quads (BL23/MansionDecal) offset along the normal with polygon
    /// offset, so there is no z-fighting. Deterministic rotation from the position.
    /// </summary>
    public static class TraceFactory
    {
        static readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
        static Transform _root;
        static readonly Queue<GameObject> _all = new Queue<GameObject>();
        public static int MaxTraces = 400;

        public static readonly string[] Types = { "BloodPool", "BloodDrip", "BloodSmear", "DragMark", "FootprintBlood", "FootprintWet", "Water", "Scratch", "Dent", "Crack", "Fragment", "Ash", "Soil", "PaintDrip", "Handprint" };

        static ProcTex.Decal Cell(string t)
        {
            switch (t)
            {
                case "BloodPool": return ProcTex.Decal.BloodPool;
                case "BloodDrip": return ProcTex.Decal.BloodDrip;
                case "BloodSmear": return ProcTex.Decal.BloodSmear;
                case "DragMark": return ProcTex.Decal.DragMark;
                case "FootprintBlood": return ProcTex.Decal.FootShoe;
                case "FootprintWet": return ProcTex.Decal.FootBare;
                case "Water": return ProcTex.Decal.Water;
                case "Scratch": return ProcTex.Decal.Scratch;
                case "Dent": return ProcTex.Decal.Dent;
                case "Crack": return ProcTex.Decal.Crack;
                case "Fragment": return ProcTex.Decal.Fragments;
                case "Ash": return ProcTex.Decal.Ash;
                case "Soil": return ProcTex.Decal.Soil;
                case "PaintDrip": return ProcTex.Decal.PaintDrip;
                case "Handprint": return ProcTex.Decal.Handprint;
            }
            return ProcTex.Decal.BloodSplat;
        }

        /// <summary>(gloss/wet, emissive, bump, default alpha)</summary>
        static Vector4 Params(string t)
        {
            switch (t)
            {
                case "BloodPool": return new Vector4(0.85f, 0f, 2.5f, 1f);
                case "BloodDrip": return new Vector4(0.7f, 0f, 1.5f, 1f);
                case "BloodSmear": return new Vector4(0.45f, 0f, 1f, 0.95f);
                case "DragMark": return new Vector4(0.35f, 0f, 0.8f, 0.85f);
                case "FootprintBlood": return new Vector4(0.4f, 0f, 0.5f, 0.9f);
                case "FootprintWet": return new Vector4(0.9f, 0f, 0.3f, 0.55f);
                case "Water": return new Vector4(0.95f, 0f, 0.6f, 0.6f);
                case "Scratch": return new Vector4(0.2f, 0f, 1.2f, 0.8f);
                case "Dent": return new Vector4(0.5f, 0f, 3f, 0.7f);
                case "Crack": return new Vector4(0.1f, 0f, 1.5f, 0.9f);
                case "Fragment": return new Vector4(0.9f, 0f, 2f, 0.95f);
                case "Ash": return new Vector4(0.0f, 0f, 0.4f, 0.85f);
                case "Soil": return new Vector4(0.05f, 0f, 1f, 0.95f);
                case "PaintDrip": return new Vector4(0.8f, 0.35f, 1.5f, 1f);
                case "Handprint": return new Vector4(0.5f, 0f, 1f, 0.95f);
            }
            return new Vector4(0.5f, 0, 1, 1);
        }

        static Mesh QuadFor(string t)
        {
            if (_meshes.TryGetValue(t, out var m) && m != null) return m;
            var r = ProcTex.DecalRect(Cell(t));
            var mb = new MeshBuilder();
            var p = Params(t);
            mb.Set(S.Decal, Color.white, new Vector4(p.x, p.y, p.z, 0));
            // unit quad in XY facing +Z (local), v up
            mb.QuadUV(new Vector3(-0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin));
            m = mb.ToMesh("Trace_" + t, out _);
            _meshes[t] = m;
            return m;
        }

        /// <summary>Create a trace at a surface point. normal = surface normal, size in metres, tint (alpha = opacity).</summary>
        public static GameObject Create(string traceType, Vector3 pos, Vector3 normal, float size, Color tint)
        {
            if (BL23.Game.GoreDecals.Handles(traceType)) return BL23.Game.GoreDecals.Create(traceType, pos, normal, size, tint);   // Game/Gore: hi-res blood decals (gore-render)
            if (_root == null) { var r = new GameObject("Traces"); _root = r.transform; }
            if (normal.sqrMagnitude < 1e-6f) normal = Vector3.up;
            normal.Normalize();
            var go = new GameObject(traceType);
            go.transform.SetParent(_root, true);
            // orientation: drips / paint run downward on walls; others random around the normal
            Quaternion rot;
            bool vertical = Mathf.Abs(normal.y) < 0.7f;
            float h = Mathf.Abs(Mathf.Sin(pos.x * 12.9898f + pos.z * 78.233f + pos.y * 37.719f) * 43758.5453f) % 1f;
            if ((traceType == "BloodDrip" || traceType == "PaintDrip" || traceType == "Handprint") && vertical)
                rot = Quaternion.LookRotation(-normal, Vector3.up);
            else
            {
                var up = Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up;
                rot = Quaternion.AngleAxis(h * 360f, normal) * Quaternion.LookRotation(-normal, up);
            }
            go.transform.SetPositionAndRotation(pos + normal * 0.004f, rot);
            float s = Mathf.Max(0.02f, size);
            float aspect = traceType == "DragMark" ? 2.2f : traceType == "BloodDrip" || traceType == "PaintDrip" ? 1.6f : traceType.StartsWith("Footprint") ? 1.0f : 1f;
            go.transform.localScale = new Vector3(s, s * aspect, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = QuadFor(traceType);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MansionMats.Get(S.Decal);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            // per-instance tint through vertex colors would need a mesh per trace; use a property block (few draws)
            var tintMesh = Object.Instantiate(mr.GetComponent<MeshFilter>().sharedMesh);
            var cols = new Color[tintMesh.vertexCount];
            float a = Params(traceType).w * tint.a;
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color(tint.r, tint.g, tint.b, a);
            tintMesh.colors = cols;
            go.GetComponent<MeshFilter>().sharedMesh = tintMesh;
            _all.Enqueue(go);
            while (_all.Count > MaxTraces) { var o = _all.Dequeue(); if (o != null) Object.Destroy(o); }
            return go;
        }

        /// <summary>Default tints for convenience.</summary>
        public static Color DefaultTint(string t)
        {
            switch (t)
            {
                case "BloodPool": case "BloodDrip": case "BloodSmear": case "FootprintBlood": case "Handprint": return new Color(0.32f, 0.01f, 0.02f, 1f);
                case "DragMark": return new Color(0.3f, 0.02f, 0.03f, 0.9f);
                case "FootprintWet": case "Water": return new Color(0.25f, 0.3f, 0.35f, 1f);
                case "Scratch": return new Color(0.85f, 0.8f, 0.72f, 0.8f);
                case "Dent": return new Color(0.2f, 0.2f, 0.22f, 0.8f);
                case "Crack": return new Color(0.08f, 0.07f, 0.07f, 0.9f);
                case "Fragment": return new Color(0.85f, 0.9f, 0.95f, 1f);
                case "Ash": return new Color(0.2f, 0.2f, 0.2f, 0.9f);
                case "Soil": return new Color(0.3f, 0.2f, 0.12f, 1f);
                case "PaintDrip": return new Color(1f, 0.1f, 0.65f, 1f);
            }
            return Color.white;
        }

        /// <summary>Remove all traces (e.g. on loop reset).</summary>
        public static void ClearAll()
        {
            while (_all.Count > 0) { var o = _all.Dequeue(); if (o != null) Object.Destroy(o); }
        }
    }
}
