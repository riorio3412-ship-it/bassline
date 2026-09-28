using System.Collections.Generic;
using System.Linq;
using BL23.Game.Characters;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Finger rig for the scanned hands. The scans model separate fingers, so the hand is cut by planes perpendicular to
    /// the hand axis: above the knuckle line the hand surface falls apart into one connected piece per finger. The
    /// lowest cut that still separates four fingers gives the knuckles; the thumb is the short piece on the palm side,
    /// tracked down to where it joins the palm. Every digit gets three bones (thumb: metacarpal, proximal, distal),
    /// weighted along its own joint chain.
    /// </summary>
    public static class GlbHands
    {
        public sealed class Hand
        {
            public bool Ok;
            public int Side;
            public Vector3[,] J = new Vector3[5, 3];  // joints per digit (0 thumb .. 4 little), original pose
            public Vector3[] Tip = new Vector3[5];
            public Vector3 ThumbDir;                 // thumb side, perpendicular to the hand axis (original pose)
            public int[] Label;                      // per vertex: -1 none, 0 thumb, 1 index, 2 middle, 3 ring, 4 little
            public float[] T;                        // per vertex: chain parameter along its digit (0 base joint .. 3 tip)
            public float[] Palm;                     // per vertex: thumb-base (thenar) influence 0..1
            public string Report = "";
        }

        public static Hand[] Segment(List<Vector3> V, int[] weld, List<int>[] adj, int weldCount, GlbArms.Params P, float S)
        {
            var res = new Hand[2];
            for (int s = 0; s < 2; s++) res[s] = SegmentOne(V, weld, adj, weldCount, P, P.Arms[s], S);
            return res;
        }

        sealed class Digit { public int Id; public float TipH, MergeH = float.NaN; public int Size; public List<int> Verts = new List<int>(); public int AbsorbedBy = -1; }

        static Hand SegmentOne(List<Vector3> V, int[] weld, List<int>[] adj, int weldCount, GlbArms.Params P, GlbArms.Arm a, float S)
        {
            var hd = new Hand { Side = a.Side, Label = new int[V.Count], T = new float[V.Count], Palm = new float[V.Count] };
            for (int i = 0; i < V.Count; i++) hd.Label[i] = -1;
            Vector3 ax = (a.T - a.W).normalized;
            float LH = (a.T - a.W).magnitude;
            var wh = new float[weldCount]; var inHand = new bool[weldCount]; var wp = new Vector3[weldCount];
            for (int i = 0; i < V.Count; i++)
            {
                if (P.Side[i] != a.Side || P.DW[i] < -0.02f) continue;
                int k = weld[i]; inHand[k] = true; wp[k] = V[i]; wh[k] = Vector3.Dot(V[i] - a.W, ax);
            }
            var hv = Enumerable.Range(0, weldCount).Where(k => inHand[k] && wh[k] > 0.05f * LH).ToList();
            if (hv.Count < 200) { hd.Report = "too few hand verts"; return hd; }
            int minSize = Mathf.Max(10, hv.Count / 120);
            // ---- merge tree of the height function (tips down): digits are the persistent components
            var uf = new int[weldCount]; for (int k = 0; k < weldCount; k++) uf[k] = -1;
            int Find(int x) { while (uf[x] != x) { uf[x] = uf[uf[x]]; x = uf[x]; } return x; }
            var compDigit = new Dictionary<int, int>();   // root -> digit id (-1 = palm)
            var compSize = new Dictionary<int, int>();
            var digits = new List<Digit>();
            var wlab = new int[weldCount]; for (int k = 0; k < weldCount; k++) wlab[k] = -2;
            foreach (int v in hv.OrderByDescending(k => wh[k]))
            {
                var roots = new HashSet<int>();
                foreach (int j in adj[v]) if (inHand[j] && uf[j] >= 0) roots.Add(Find(j));
                uf[v] = v;
                if (roots.Count == 0)
                {
                    var d = new Digit { Id = digits.Count, TipH = wh[v] };
                    digits.Add(d); compDigit[v] = d.Id; compSize[v] = 1; d.Size = 1; d.Verts.Add(v); wlab[v] = d.Id;
                    continue;
                }
                // significant = palm pieces or digits that already have some size
                var sig = roots.Where(r => compDigit[r] < 0 || compSize[r] >= minSize).ToList();
                int target;
                if (sig.Count >= 2)
                {
                    foreach (int r in sig) { int di = compDigit[r]; if (di >= 0 && float.IsNaN(digits[di].MergeH)) digits[di].MergeH = wh[v]; }
                    target = -1;
                }
                else if (sig.Count == 1) target = compDigit[sig[0]];
                else target = compDigit[roots.OrderByDescending(r => compSize[r]).First()];
                // small pieces are absorbed
                foreach (int r in roots) { int di = compDigit[r]; if (di >= 0 && di != target && compSize[r] < minSize) digits[di].AbsorbedBy = target; }
                int total = 1; foreach (int r in roots) total += compSize[r];
                foreach (int r in roots) { uf[r] = v; compDigit.Remove(r); compSize.Remove(r); }
                compDigit[v] = target; compSize[v] = total;
                wlab[v] = target;
                if (target >= 0) { digits[target].Size++; digits[target].Verts.Add(v); }
            }
            // resolve absorbed digits
            int Resolve(int d) { int guard = 0; while (d >= 0 && digits[d].AbsorbedBy != -1 && guard++ < 50) d = digits[d].AbsorbedBy; return d; }
            foreach (int k in hv) if (wlab[k] >= 0) wlab[k] = Resolve(wlab[k]);
            foreach (var d in digits) d.Verts.Clear();
            foreach (int k in hv) if (wlab[k] >= 0) digits[wlab[k]].Verts.Add(k);
            foreach (var d in digits) if (float.IsNaN(d.MergeH)) d.MergeH = d.Verts.Count > 0 ? d.Verts.Min(k => wh[k]) : d.TipH;
            var real = digits.Where(d => d.AbsorbedBy == -1 && d.Verts.Count >= minSize).OrderByDescending(d => d.Verts.Count * (d.TipH - d.MergeH)).Take(5).ToList();
            if (real.Count == 0) { hd.Report = "no digits"; return hd; }
            Vector3 Cen(IEnumerable<int> l) { Vector3 c = Vector3.zero; int n = 0; foreach (int k in l) { c += wp[k]; n++; } return n > 0 ? c / n : Vector3.zero; }
            // thumb: the digit that joins the palm clearly lower than the others
            Digit thumb = null;
            if (real.Count >= 2)
            {
                var byMerge = real.OrderBy(d => d.MergeH).ToList();
                float others = byMerge.Skip(1).Average(d => d.MergeH);
                if (real.Count >= 5 || others - byMerge[0].MergeH > 0.08f * LH) thumb = byMerge[0];
            }
            var fingers = real.Where(d => d != thumb).ToList();
            Vector3 fcAvg = Cen(fingers.SelectMany(d => d.Verts));
            Vector3 thumbSide = thumb != null ? Vector3.ProjectOnPlane(Cen(thumb.Verts) - fcAvg, ax).normalized : Vector3.ProjectOnPlane(Vector3.forward, ax).normalized;
            hd.ThumbDir = thumbSide;
            float knuckleH = Mathf.Min(fingers.Count > 0 ? fingers.Average(d => d.MergeH) : 0.5f * LH, 0.56f * LH);
            float Lat(int k) => Vector3.Dot(wp[k] - fcAvg, thumbSide);
            // four finger digits across the hand (index first). Separate scanned digits keep their own vertices; fused
            // ones are split at the lateral quantiles of the finger mass.
            var fingerOf = new Dictionary<int, int>(); // welded vert -> 0 index, 1 middle, 2 ring, 3 little
            if (fingers.Count == 4)
            {
                var ordered = fingers.OrderByDescending(d => Lat(d.Verts[d.Verts.Count / 2])).OrderByDescending(d => Cen(d.Verts) == Vector3.zero ? 0f : Vector3.Dot(Cen(d.Verts) - fcAvg, thumbSide)).ToList();
                for (int j = 0; j < 4; j++) foreach (int k in ordered[j].Verts) fingerOf[k] = j;
            }
            else if (fingers.Count > 0)
            {
                var all = fingers.SelectMany(d => d.Verts).ToList();
                var lats = all.Select(Lat).OrderBy(x => x).ToList();
                float q1 = lats[lats.Count / 4], q2 = lats[lats.Count / 2], q3 = lats[lats.Count * 3 / 4];
                foreach (int k in all) { float l = Lat(k); fingerOf[k] = l > q3 ? 0 : l > q2 ? 1 : l > q1 ? 2 : 3; }
            }
            var latC = new float[4];
            for (int g = 0; g < 4; g++) { var vs = fingerOf.Where(kv => kv.Value == g).Select(kv => kv.Key).ToList(); latC[g] = vs.Count > 0 ? vs.Average(Lat) : 0.03f - g * 0.018f; }
            // palm vertices just below the knuckle line join the nearest finger (so knuckles bend smoothly)
            foreach (int k in hv)
            {
                if (fingerOf.ContainsKey(k) || (thumb != null && wlab[k] == thumb.Id)) continue;
                if (wh[k] < knuckleH - 0.022f * S) continue;
                float side = Lat(k); int best = 0; float bd = float.MaxValue;
                for (int g = 0; g < 4; g++) { float dd = Mathf.Abs(side - latC[g]); if (dd < bd) { bd = dd; best = g; } }
                fingerOf[k] = best;
            }
            // per-finger joints: MCP on the knuckle line, PIP / DIP at slices of the finger toward its tip
            for (int g = 0; g < 4; g++)
            {
                var vs = fingerOf.Where(kv => kv.Value == g).Select(kv => kv.Key).ToList();
                Vector3 mcp, tip;
                if (vs.Count < 5) { mcp = a.W + ax * (0.5f * LH) + thumbSide * latC[g]; tip = mcp + ax * 0.45f * LH; }
                else
                {
                    float top = vs.Max(k => wh[k]);
                    mcp = Cen(vs.Where(k => Mathf.Abs(wh[k] - (knuckleH - 0.004f * S)) < 0.004f * S));
                    if (mcp == Vector3.zero) mcp = Cen(vs) - ax * 0.03f * S;
                    tip = Cen(vs.Where(k => wh[k] > top - 0.006f));
                }
                Vector3 chord = tip - mcp; float cl = chord.magnitude; Vector3 cd = cl > 1e-5f ? chord / cl : ax;
                Vector3 Slice(float f)
                {
                    var sl = vs.Where(k => Mathf.Abs(Vector3.Dot(wp[k] - mcp, cd) - f * cl) < 0.004f * S).ToList();
                    return sl.Count > 3 ? Cen(sl) : mcp + chord * f;
                }
                hd.J[g + 1, 0] = mcp; hd.J[g + 1, 1] = Slice(0.45f); hd.J[g + 1, 2] = Slice(0.74f); hd.Tip[g + 1] = tip;
            }
            // thumb geometry
            float palmHalf = 0f;
            foreach (int k in hv) if (wh[k] > 0.1f * LH && wh[k] < 0.35f * LH) palmHalf = Mathf.Max(palmHalf, Vector3.Dot(wp[k] - a.W, thumbSide));
            Vector3 cmc = a.W + ax * (0.14f * LH) + thumbSide * Mathf.Max(0.008f, palmHalf * 0.5f);
            Vector3 tBase = cmc, tTip = cmc + (ax + thumbSide) * 0.03f * S;
            if (thumb != null)
            {
                tBase = Cen(thumb.Verts.Where(k => wh[k] < thumb.MergeH + 0.006f));
                if (tBase == Vector3.zero) tBase = Cen(thumb.Verts);
                tTip = Cen(thumb.Verts.OrderByDescending(k => (wp[k] - cmc).sqrMagnitude).Take(Mathf.Max(3, thumb.Verts.Count / 25)));
            }
            hd.J[0, 0] = cmc; hd.J[0, 1] = thumb != null ? Vector3.Lerp(tBase, tTip, 0.1f) : Vector3.Lerp(cmc, tTip, 0.45f);
            hd.J[0, 2] = Vector3.Lerp(hd.J[0, 1], tTip, 0.55f); hd.Tip[0] = tTip;
            // vertex labels / parameters: T = position along the digit's joint chain (0 base joint .. 1 tip), Seg = segment
            for (int i = 0; i < V.Count; i++)
            {
                if (P.Side[i] != a.Side || P.DW[i] < -0.02f) continue;
                int k = weld[i]; Vector3 p = V[i];
                int digit = -1;
                if (thumb != null && wlab[k] == thumb.Id) digit = 0;
                else if (fingerOf.TryGetValue(k, out int g)) digit = g + 1;
                if (digit >= 0)
                {
                    hd.Label[i] = digit;
                    hd.T[i] = ChainParam(p, hd.J[digit, 0], hd.J[digit, 1], hd.J[digit, 2], hd.Tip[digit]);
                }
                else if (thumb != null)
                {
                    float dd = CharMesher.SegDist(p, cmc, tBase);
                    float side = Vector3.Dot(p - a.W, thumbSide);
                    hd.Palm[i] = Mathf.Clamp01(1f - dd / (0.022f * S)) * Mathf.Clamp01(side / Mathf.Max(0.005f, palmHalf * 0.5f));
                }
            }
            hd.Ok = true;
            hd.Report = $"arm{a.Side}: {real.Count} digits (merge {string.Join(",", real.Select(d => (d.MergeH / LH).ToString("F2")))} LH), thumb {(thumb != null ? thumb.Verts.Count + " verts" : "none")}, separate fingers {fingers.Count}, knuckles {knuckleH / LH:F2} LH";
            return hd;
        }

        /// <summary>Chain parameter of a point on a digit: 0..1 base joint to middle joint, 1..2 to the third joint,
        /// 2..3 to the tip; negative before the base joint (palm side).</summary>
        static float ChainParam(Vector3 p, Vector3 j0, Vector3 j1, Vector3 j2, Vector3 tip)
        {
            Vector3[] pts = { j0, j1, j2, tip };
            float best = float.MaxValue, par = 0f;
            for (int s = 0; s < 3; s++)
            {
                Vector3 ab = pts[s + 1] - pts[s]; float l2 = Mathf.Max(1e-10f, ab.sqrMagnitude);
                float t = Vector3.Dot(p - pts[s], ab) / l2;
                float tc = s == 0 ? Mathf.Min(t, 1f) : s == 2 ? Mathf.Max(t, 0f) : Mathf.Clamp01(t);
                float d = (p - (pts[s] + ab * Mathf.Clamp01(t))).sqrMagnitude;
                if (d < best) { best = d; par = s + tc; }
            }
            return par;
        }

        /// <summary>Adds the finger bones (3 per digit) to the (already smoothed) hand weights.</summary>
        public static void ApplyWeights(BoneWeight[] W, Hand[] hands)
        {
            foreach (var hd in hands)
            {
                if (hd == null || !hd.Ok) continue;
                bool L = hd.Side == 0;
                for (int i = 0; i < W.Length && i < hd.Label.Length; i++)
                {
                    int lab = hd.Label[i];
                    float palm = hd.Palm[i];
                    if (lab < 0 && palm <= 0f) continue;
                    var l = new List<KeyValuePair<int, float>>();
                    var w = W[i];
                    float wf; float[] seg = new float[3];
                    if (lab < 0) { wf = palm * 0.6f; seg[0] = 1f; lab = 0; }
                    else
                    {
                        float t = hd.T[i];
                        // blend into the hand before the base joint; the thumb's metacarpal starts inside the palm
                        wf = lab == 0 ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.3f, 0.25f, t)) : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.35f, 0.12f, t));
                        float b1 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1.15f, t));
                        float b2 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.85f, 2.12f, t));
                        seg[0] = 1f - b1; seg[1] = b1 * (1f - b2); seg[2] = b1 * b2;
                    }
                    if (wf <= 0.001f) continue;
                    void Add(int b, float v) { if (v > 0f) l.Add(new KeyValuePair<int, float>(b, v)); }
                    Add(w.boneIndex0, w.weight0 * (1f - wf)); Add(w.boneIndex1, w.weight1 * (1f - wf));
                    Add(w.boneIndex2, w.weight2 * (1f - wf)); Add(w.boneIndex3, w.weight3 * (1f - wf));
                    for (int s2 = 0; s2 < 3; s2++) Add((int)ActorSkeleton.Digit(L, lab, s2), wf * seg[s2]);
                    W[i] = CharMesher.ToBW(l);
                }
            }
        }

        /// <summary>Writes the (reposed) finger joints into the proportions.</summary>
        public static void ToProportions(BodyProportions P, Hand[] hands, GlbArms.Params arms)
        {
            for (int s = 0; s < 2; s++)
            {
                var hd = hands[s]; var a = arms.Arms[s];
                P.PlaceHand(s == 0);
                if (hd == null || !hd.Ok) continue;
                Vector3 R(Vector3 p) => GlbArms.HandToRest(a, p);
                for (int d = 0; d < 5; d++)
                {
                    for (int k = 0; k < 3; k++) P.Joint[(int)ActorSkeleton.Digit(s == 0, d, k)] = R(hd.J[d, k]);
                    P.FingerTip[s * 5 + d] = R(hd.Tip[d]);
                }
            }
        }
    }
}
