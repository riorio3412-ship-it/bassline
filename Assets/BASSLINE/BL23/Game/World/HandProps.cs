using System.Collections.Generic;
using BL23.Game.Mansion;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game
{
    /// <summary>
    /// Small things in the hand while doing something: a book to read, a cup to drink from, a sheet of paper to write on,
    /// a spoon to stir with, a fork to eat with. Presentation only (not kernel items); used for everyone, and the player
    /// sees their own hands holding them in first person. Nothing is shown if the hand already holds a real item.
    /// </summary>
    public static class HandProps
    {
        static readonly Dictionary<string, (Mesh mesh, Material[] mats)> _cache = new Dictionary<string, (Mesh, Material[])>();

        public static string For(Anim a)
        {
            switch (a)
            {
                case Anim.Read: return "book"; case Anim.Drink: return "cup"; case Anim.Write: return "paper";
                case Anim.Cook: return "spoon"; case Anim.Eat: return "fork";
            }
            return null;
        }

        public static GameObject Attach(Transform hand, string kind)
        {
            if (hand == null || kind == null) return null;
            var (mesh, mats) = Build(kind); if (mesh == null) return null;
            var go = new GameObject("HandProp_" + kind); go.transform.SetParent(hand, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = mats; mr.shadowCastingMode = ShadowCastingMode.Off;
            switch (kind)
            {
                case "book": go.transform.localPosition = new Vector3(0.0f, -0.03f, 0.08f); go.transform.localRotation = Quaternion.Euler(10f, 0, 90f); break;
                case "cup": go.transform.localPosition = new Vector3(0.0f, -0.02f, 0.05f); go.transform.localRotation = Quaternion.identity; break;
                case "paper": go.transform.localPosition = new Vector3(0.0f, -0.02f, 0.1f); go.transform.localRotation = Quaternion.Euler(0, 0, 90f); break;
                default: go.transform.localPosition = new Vector3(0.0f, -0.01f, 0.06f); go.transform.localRotation = Quaternion.Euler(80f, 0, 0); break;
            }
            return go;
        }

        static (Mesh, Material[]) Build(string kind)
        {
            if (_cache.TryGetValue(kind, out var c)) return c;
            var mb = new MeshBuilder();
            switch (kind)
            {
                case "book":
                    mb.Set(S.Leather, new Color(0.42f, 0.08f, 0.1f)); mb.BevelBox(Vector3.zero, new Vector3(0.15f, 0.035f, 0.21f), 0.004f);
                    mb.Set(S.Paper, new Color(0.93f, 0.9f, 0.82f)); mb.Box(new Vector3(0.004f, 0, 0), new Vector3(0.14f, 0.028f, 0.2f));
                    mb.Set(S.Gold, new Color(0.8f, 0.62f, 0.3f)); mb.Box(new Vector3(-0.074f, 0, 0), new Vector3(0.004f, 0.036f, 0.19f));
                    break;
                case "cup":
                    mb.Set(S.Porcelain, new Color(0.95f, 0.93f, 0.9f));
                    mb.Lathe(new[] { new Vector2(0.001f, 0f), new Vector2(0.028f, 0.002f), new Vector2(0.036f, 0.05f), new Vector2(0.04f, 0.075f), new Vector2(0.036f, 0.075f), new Vector2(0.032f, 0.052f), new Vector2(0.001f, 0.012f) }, 16);
                    mb.Set(S.Gold, new Color(0.8f, 0.62f, 0.3f)); mb.Push(new Vector3(0, 0.074f, 0), 0); mb.Torus(Vector3.zero, 0.038f, 0.002f, 16, 3); mb.Pop();
                    break;
                case "paper":
                    mb.Set(S.Paper, new Color(0.94f, 0.91f, 0.84f)); mb.Box(Vector3.zero, new Vector3(0.21f, 0.002f, 0.28f));
                    break;
                case "spoon":
                    mb.Set(S.WoodLight, new Color(0.62f, 0.45f, 0.28f)); mb.Rod(new Vector3(0, 0, -0.1f), new Vector3(0, 0, 0.14f), 0.007f, 6);
                    mb.Ellipsoid(new Vector3(0, 0, 0.17f), new Vector3(0.025f, 0.008f, 0.035f), 8, 4);
                    break;
                case "fork":
                    mb.Set(S.Steel, new Color(0.8f, 0.8f, 0.82f)); mb.Box(new Vector3(0, 0, 0.02f), new Vector3(0.012f, 0.004f, 0.17f));
                    for (int i = 0; i < 3; i++) mb.Box(new Vector3(-0.007f + i * 0.007f, 0, 0.13f), new Vector3(0.002f, 0.003f, 0.04f));
                    break;
                default: return (null, null);
            }
            var mesh = mb.ToMesh("HandProp_" + kind, out var slots);
            var res = (mesh, MansionMats.Materials(slots)); _cache[kind] = res; return res;
        }
    }
}
