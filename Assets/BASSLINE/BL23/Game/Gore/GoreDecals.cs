using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game
{
    /// <summary>
    /// High-resolution blood decals for the kernel's blood traces (TraceFactory.Create hands these types over first):
    /// pools and smears as a 5×5 grid (wet centre → drying rim in vertex colour, gloss 0.9 → 0.35 in UV2.x), drips, prints
    /// and handprints as single quads from the baked gore atlas. BloodSpray / Struggle are empty anchors (GoreScene draws the
    /// marks behind them). The tint's hue is ignored (only its alpha is used): the colour is always the crimson palette.
    /// Own FIFO cap (260) that never evicts a decal of the current case's victim.
    /// </summary>
    public static class GoreDecals
    {
        public const int Cap = 260;
        static readonly LinkedList<GoreDecalView> _all = new LinkedList<GoreDecalView>();
        static Transform _root;
        static readonly GoreAtlas.Batch _b = new GoreAtlas.Batch();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _all.Clear(); _root = null; }

        public static bool Handles(string t)
        {
            switch (t)
            {
                case "BloodPool": case "BloodDrip": case "BloodSmear": case "FootprintBlood": case "Handprint": case "DrainBlood": case "BloodSpray": case "Struggle": return true;
            }
            return false;
        }

        public static GameObject Create(string t, Vector3 pos, Vector3 normal, float size, Color tint)
        {
            if (_root == null) { var r = new GameObject("GoreDecals"); _root = r.transform; }
            if (normal.sqrMagnitude < 1e-6f) normal = Vector3.up;
            normal.Normalize();
            var go = new GameObject(t);
            go.transform.SetParent(_root, true);
            bool vertical = Mathf.Abs(normal.y) < 0.7f;
            float h = Hash01(pos);
            // local +Y = surface normal, local +Z = the cell's +v; runs and prints on walls keep +v = world up
            Vector3 vAxis;
            if (vertical && (t == "BloodDrip" || t == "Handprint" || t == "BloodSmear")) vAxis = Vector3.up;
            else vAxis = Quaternion.AngleAxis(h * 360f, normal) * Vector3.ProjectOnPlane(Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up, normal);
            go.transform.SetPositionAndRotation(pos + normal * 0.004f, Quaternion.LookRotation(vAxis.sqrMagnitude > 1e-6f ? vAxis : Vector3.forward, normal));
            var view = go.AddComponent<GoreDecalView>();
            view.Type = t; view.Size = Mathf.Max(0.05f, size); view.Alpha = Mathf.Clamp01(tint.a <= 0f ? 1f : tint.a); view.Seed = (uint)(h * 1e9f); view.Vertical = vertical;
            if (t != "BloodSpray" && t != "Struggle")
            {
                go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = GoreAtlas.Mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = true;
                mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                view.Build(0f, Vector2.zero, -1f);
            }
            Track(view);
            return go;
        }

        static float Hash01(Vector3 p) => Mathf.Abs(Mathf.Sin(p.x * 12.9898f + p.z * 78.233f + p.y * 37.719f) * 43758.5453f) % 1f;

        static void Track(GoreDecalView v)
        {
            _all.AddLast(v);
            int n = 0; for (var node = _all.First; node != null;) { var next = node.Next; if (node.Value == null) _all.Remove(node); else n++; node = next; }
            if (n <= Cap) return;
            var protectedVictims = CurrentVictims();
            for (var node = _all.First; node != null && n > Cap;)
            {
                var next = node.Next; var d = node.Value;
                if (d != null && d != v && (d.Victim == null || !protectedVictims.Contains(d.Victim))) { Object.Destroy(d.gameObject); _all.Remove(node); n--; }
                node = next;
            }
        }

        static readonly HashSet<string> _victims = new HashSet<string>();
        static HashSet<string> CurrentVictims()
        {
            _victims.Clear();
            var S = GoreWorld.S; if (S == null) return _victims;
            foreach (var inc in S.Incidents.Values) if (inc.Loop == S.Loop && inc.Chapter == S.Chapter && inc.Victim != null) _victims.Add(inc.Victim);
            return _victims;
        }

        // ------------------------------------------------------------------ mesh building (local space: +Y normal, +Z = v)
        internal static GoreAtlas.Batch Scratch => _b;
    }
}
