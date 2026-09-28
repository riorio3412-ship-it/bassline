using System.Collections.Generic;
using BL23.Game.Mansion;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using MS = BL23.Game.Mansion.S;

namespace BL23.Game
{
    /// <summary>
    /// Blood on a weapon's edge. Every 0.5 s: each kernel item flagged Bloody (a knife that went in, a cleaver, a saw) gets a
    /// <see cref="GoreBladeBlood"/> on its view; one that has been washed (or is gone) loses it. Added next to GoreScene.
    /// </summary>
    public sealed class GoreItemBlood : MonoBehaviour
    {
        float _next;
        readonly Dictionary<string, GoreBladeBlood> _on = new Dictionary<string, GoreBladeBlood>();
        readonly List<string> _drop = new List<string>();

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            var S = GoreWorld.S; var W = GoreWorld.W;
            _drop.Clear();
            foreach (var kv in _on)
            {
                var it = S?.I(kv.Key);
                if (kv.Value == null || it == null || !it.Bloody) { if (kv.Value != null) Destroy(kv.Value); _drop.Add(kv.Key); }
            }
            foreach (var id in _drop) _on.Remove(id);
            if (S == null || W == null || W.Items == null) return;
            try
            {
                foreach (var it in S.Items.Values)
                {
                    if (!it.Bloody || it.Type == "SeveredPart" || _on.ContainsKey(it.Id)) continue;
                    if (!W.Items.TryGetValue(it.Id, out var iv) || iv == null || iv.Visual == null) continue;
                    var bb = iv.Visual.GetComponent<GoreBladeBlood>() ?? iv.Visual.AddComponent<GoreBladeBlood>();
                    bb.ItemId = it.Id; _on[it.Id] = bb;
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Gore] item blood: " + e.Message); }
        }
    }

    /// <summary>
    /// The blood itself, on one item: if the item has a steel blade (the steel submesh of its own mesh is flat — a knife,
    /// a cleaver, a letter opener, a saw), both faces of the blade carry a wet crimson smear, heaviest at the tip where it went
    /// in and thinning toward the hilt, with a few drops near the guard; the edges dry brown. Built in the blade's own space
    /// under the item's mesh, so it goes wherever the item goes (dropped, carried, hidden); follows the mesh renderer's
    /// visibility. Items without a blade (a candlestick, a rope) are left to the item's own tint. Presentation only.
    /// </summary>
    public sealed class GoreBladeBlood : MonoBehaviour
    {
        public string ItemId;
        bool _tried; GameObject _go; Mesh _mesh; MeshRenderer _src, _mine;
        public bool Built => _go != null;

        void Start() { Build(); }

        void LateUpdate()
        {
            if (!_tried) Build();
            if (_mine != null && _src != null && _mine.enabled != _src.enabled) _mine.enabled = _src.enabled;
        }

        void OnDestroy() { if (_go != null) Destroy(_go); if (_mesh != null) Destroy(_mesh); }

        static bool BladeMat(Material m)
        {
            if (m == null) return false;
            var steel = MansionMats.Get(MS.Steel); var chrome = MansionMats.Get(MS.Chrome); var iron = MansionMats.Get(MS.Iron);
            // (an item tinted "blood" by its view carries instances: "Steel (Instance)")
            return m == steel || m == chrome || m == iron || m.name.StartsWith("Steel") || m.name.StartsWith("Chrome") || m.name.StartsWith("Iron");
        }

        void Build()
        {
            _tried = true;
            try { BuildBlade(); } catch (System.Exception e) { Debug.LogWarning("[Gore] blade blood " + ItemId + ": " + e.Message); }
        }

        void BuildBlade()
        {
            if (GoreAtlas.Mat == null) return;
            MansionMats.Init();
            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>(); var m = mf.sharedMesh;
                if (mr == null || m == null || mf.GetComponentInParent<GorePieceTag>() != null || mf.gameObject.name == "GoreBladeBlood") continue;
                var mats = mr.sharedMaterials;
                for (int s = 0; s < m.subMeshCount && s < mats.Length; s++)
                {
                    if (!BladeMat(mats[s])) continue;
                    if (m.isReadable && TryBlade(mf.transform, m, s)) { _src = mr; return; }
                }
            }
        }

        /// <summary>
        /// A flat piece of metal (thinnest side under 35 % of its width, 5–120 cm long): its own broad faces, copied 0.8 mm
        /// proud, carry the blood — so the stain has exactly the blade's outline (never a rectangle hanging past a tapered
        /// point). Heaviest at the tip, thinning toward the hilt; the atlas smear gives it an organic edge; two or three drops
        /// near the guard.
        /// </summary>
        bool TryBlade(Transform t, Mesh m, int sub)
        {
            var b = m.GetSubMesh(sub).bounds;
            var e = b.extents; float[] ex = { e.x, e.y, e.z };
            int thin = 0, lng = 0; for (int i = 1; i < 3; i++) { if (ex[i] < ex[thin]) thin = i; if (ex[i] > ex[lng]) lng = i; }
            if (thin == lng) return false;
            int mid = 3 - thin - lng;
            if (ex[thin] > 0.35f * ex[mid] || ex[lng] * 2f < 0.05f || ex[lng] * 2f > 1.2f) return false;
            Vector3 Ax(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
            var tip = Ax(lng); if (b.center[lng] - m.bounds.center[lng] < 0f) tip = -tip;   // the blade sits toward its tip
            float L = ex[lng], W = ex[mid];
            var V = m.vertices; var tris = m.GetTriangles(sub);
            var rnd = new System.Random((int)(GoreWorld.Hash(ItemId + "|blade") & 0x7FFFFFFF));
            var cell = GoreAtlas.Cell(GoreAtlas.Smear);
            var batch = new GoreAtlas.Batch();
            var wet = GorePalette.Wet;
            float reach = 0.55f + 0.25f * (float)rnd.NextDouble();   // how far back from the tip the blood goes (fraction of the blade)
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                Vector3 a = V[tris[i]], bb = V[tris[i + 1]], c = V[tris[i + 2]];
                var fn = Vector3.Cross(bb - a, c - a); if (fn.sqrMagnitude < 1e-12f) continue; fn.Normalize();
                if (Mathf.Abs(fn[thin]) < 0.8f) continue;                          // only the broad faces, not the edges
                int b0 = batch.V.Count;
                foreach (var p in new[] { a, bb, c })
                {
                    float along = Vector3.Dot(p - b.center, tip) / Mathf.Max(1e-4f, L);   // −1 hilt end … +1 tip
                    float fromTip = (1f - along) * 0.5f;                                   // 0 at the tip … 1 at the hilt end
                    float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((fromTip - reach * 0.55f) / (reach * 0.45f)));
                    float across = (p[mid] - b.center[mid]) / Mathf.Max(1e-4f, W);          // −1 … +1
                    batch.V.Add(p + fn * 0.0008f); batch.N.Add(fn);
                    var tg = Vector3.Cross(fn, Vector3.Cross(tip, fn)).normalized; batch.T.Add(new Vector4(tg.x, tg.y, tg.z, -1f));
                    // the smear cell is heavy at −u: −u lies at the tip
                    batch.UV.Add(new Vector2(Mathf.Lerp(cell.xMin, cell.xMax, Mathf.Clamp01(fromTip / Mathf.Max(0.2f, reach))), Mathf.Lerp(cell.yMin, cell.yMax, across * 0.5f + 0.5f)));
                    batch.X.Add(new Vector4(0.88f, 0f, 1.2f, 0f));
                    batch.C.Add(GorePalette.Check(new Color(wet.r, wet.g, wet.b, 0.96f * k)));
                }
                batch.I.Add(b0); batch.I.Add(b0 + 1); batch.I.Add(b0 + 2);
                batch.Quads++;
            }
            if (batch.Empty) return false;
            // a few drops toward the guard, on both faces
            var dry = Color.Lerp(GorePalette.Wet, GorePalette.DryRim, 0.55f);
            for (int f = -1; f <= 1; f += 2)
            {
                var n = Ax(thin) * f; var plane = b.center + n * (ex[thin] + 0.0009f); var v = Vector3.Cross(n, tip);
                int drops = 1 + rnd.Next(2);
                for (int k = 0; k < drops; k++)
                {
                    var p = plane - tip * (L * (0.35f + 0.4f * (float)rnd.NextDouble())) + v * (W * ((float)rnd.NextDouble() - 0.5f) * 0.6f);
                    float s = W * (0.22f + 0.2f * (float)rnd.NextDouble());
                    batch.Quad(p, n, tip, s, s, GoreAtlas.Droplets, new Color(dry.r, dry.g, dry.b, 0.85f), new Vector3(0.6f, 0f, 1f), 0f);
                }
            }
            _mesh = batch.ToMesh("GoreBladeBlood_" + ItemId); _mesh.RecalculateBounds();
            _go = new GameObject("GoreBladeBlood"); _go.transform.SetParent(t, false);
            _go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _mine = _go.AddComponent<MeshRenderer>(); _mine.sharedMaterial = GoreAtlas.Mat;
            _mine.shadowCastingMode = ShadowCastingMode.Off; _mine.lightProbeUsage = LightProbeUsage.BlendProbes; _mine.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return true;
        }
    }
}
