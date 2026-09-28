using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BL23.Game.Characters;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Arms of the auto-rigged GLB scans:
    ///  1. membership: which vertices belong to each arm (front-silhouette flood + capsule + mesh connectivity from the
    ///     arm core, so fingers are never left on the legs and coat panels never ride on the arm);
    ///  2. arm skin weights from chain coordinates (smooth shoulder / elbow / wrist blends);
    ///  3. bake-time repose: the T / A-pose arms are rotated down to a relaxed rest pose (slight abduction that clears
    ///     the body, a little forward swing, soft elbow, palms turned toward the thighs) with dual-quaternion blending,
    ///     so the runtime idle / walk poses are small rotations away from the bind pose and the shoulders keep volume.
    /// </summary>
    public static class GlbArms
    {
        public sealed class Arm
        {
            public int Side;                         // 0 = left (-X), 1 = right (+X)
            public Vector3 S, E, W, T;               // shoulder (upper arm joint), elbow, wrist, fingertip (original pose)
            public float LUA, LFA, LH;
            public Vector3 S2, E2, W2, T2;           // reposed joints
            public Quaternion Rua = Quaternion.identity, Rfa = Quaternion.identity, Rh = Quaternion.identity;
            public float Twist;                      // pronation about the forearm axis (deg)
            public float Angle, Carry, Shift;        // rest abduction (deg), carrying angle (deg), shoulder shift (m)
            public Vector3 FaDir2 => (W2 - E2).normalized;
            public float Len => LUA + LFA + LH;
        }

        public sealed class Params
        {
            public int[] Side;       // -1 = not arm, else arm side
            public float[] AS;       // projection on the upper arm direction from the shoulder joint (m)
            public float[] DE, DW;   // signed chain distance from the elbow / wrist (m)
            public float[] Dist;     // distance to the arm polyline (m)
            public float[] ShoulderCap; // 0..1 torso vertices around the shoulder that follow the arm a little in the repose
            public bool TPose; public float[] TorsoHalf = new float[2]; // T-pose scans: torso side x under the armpits
            public Arm[] Arms;
        }

        static void Chain(Arm a, Vector3 p, out float along, out float dist, out float aS)
        {
            Vector3 dUA = (a.E - a.S).normalized;
            aS = Vector3.Dot(p - a.S, dUA);
            // closest point on S-E-W-T(+ext)
            Vector3 tipExt = a.T + (a.T - a.W).normalized * 0.06f;
            Vector3[] pts = { a.S, a.E, a.W, tipExt };
            float best = float.MaxValue; along = 0; float acc = 0;
            for (int i = 0; i < 3; i++)
            {
                Vector3 ab = pts[i + 1] - pts[i]; float l = ab.magnitude;
                float t = Mathf.Clamp01(Vector3.Dot(p - pts[i], ab) / Mathf.Max(1e-8f, l * l));
                float d = (p - (pts[i] + ab * t)).magnitude;
                if (d < best) { best = d; along = acc + t * l; }
                acc += l;
            }
            dist = best;
        }

        public static Arm[] MakeArms(GlbRigger.Landmarks lm)
        {
            var arms = new Arm[2];
            for (int s = 0; s < 2; s++)
            {
                var a = new Arm { Side = s };
                a.S = s == 0 ? lm.ShoulderL : lm.ShoulderR; a.E = s == 0 ? lm.ElbowL : lm.ElbowR;
                a.W = s == 0 ? lm.WristL : lm.WristR; a.T = s == 0 ? lm.TipL : lm.TipR;
                a.LUA = (a.E - a.S).magnitude; a.LFA = (a.W - a.E).magnitude; a.LH = (a.T - a.W).magnitude;
                arms[s] = a;
            }
            return arms;
        }

        /// <summary>
        /// Re-derives the arm chain from the arm's own vertices: centreline from 1 cm slices along the arm's principal
        /// axis, fingertip at the distal end, wrist at the narrowest section 7-15 cm from the tip, shoulder (gleno-humeral
        /// centre) ~4.5 cm down the centreline from the top of the arm and a little medial, elbow at the anatomical 56 %
        /// of shoulder-wrist.
        /// </summary>
        static void RefineJoints(Arm a, List<Vector3> V, int[] side, float S, System.Text.StringBuilder log)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i < V.Count; i++) if (side[i] == a.Side) pts.Add(V[i]);
            if (pts.Count < 200) return;
            Vector3 mean = Vector3.zero; foreach (var p in pts) mean += p; mean /= pts.Count;
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (var p in pts) { var d = p - mean; xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z; yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z; }
            Vector3 dir = (a.T - a.S).normalized;
            for (int it = 0; it < 50; it++)
            {
                dir = new Vector3(xx * dir.x + xy * dir.y + xz * dir.z, xy * dir.x + yy * dir.y + yz * dir.z, xz * dir.x + yz * dir.y + zz * dir.z).normalized;
            }
            if (Vector3.Dot(dir, a.T - a.S) < 0) dir = -dir;
            float pmin = float.MaxValue, pmax = float.MinValue;
            foreach (var p in pts) { float t = Vector3.Dot(p - mean, dir); pmin = Mathf.Min(pmin, t); pmax = Mathf.Max(pmax, t); }
            const float step = 0.01f;
            int ns = Mathf.Max(2, Mathf.CeilToInt((pmax - pmin) / step) + 1);
            var mn = new Vector3[ns]; var mx = new Vector3[ns]; var cnt = new int[ns];
            for (int k = 0; k < ns; k++) { mn[k] = Vector3.one * 9f; mx[k] = -Vector3.one * 9f; }
            foreach (var p in pts)
            {
                int k = Mathf.Clamp((int)((Vector3.Dot(p - mean, dir) - pmin) / step), 0, ns - 1);
                mn[k] = Vector3.Min(mn[k], p); mx[k] = Vector3.Max(mx[k], p); cnt[k]++;
            }
            var cen = new Vector3[ns]; var rad = new float[ns];
            for (int k = 0; k < ns; k++)
            {
                if (cnt[k] < 3) { cen[k] = k > 0 ? cen[k - 1] + dir * step : mean + dir * pmin; rad[k] = k > 0 ? rad[k - 1] : 0.05f; continue; }
                cen[k] = (mn[k] + mx[k]) * 0.5f;
                var e = mx[k] - mn[k];
                // extent perpendicular to the arm
                rad[k] = 0.5f * (Vector3.ProjectOnPlane(e, dir).magnitude) / 1.414f;
            }
            // smooth the centreline
            var cs = (Vector3[])cen.Clone();
            for (int k = 0; k < ns; k++) { Vector3 s = Vector3.zero; int n = 0; for (int d = -2; d <= 2; d++) { int j = k + d; if (j < 0 || j >= ns) continue; s += cen[j]; n++; } cs[k] = s / n; }
            Vector3 At(float proj) { float f = Mathf.Clamp((proj - pmin) / step - 0.5f, 0, ns - 1); int k0 = Mathf.FloorToInt(f), k1 = Mathf.Min(ns - 1, k0 + 1); return Vector3.Lerp(cs[k0], cs[k1], f - k0); }
            // tip: distal end of the centreline
            Vector3 tip = At(pmax - 0.004f) + dir * 0.004f;
            // wrist: narrowest section 7..15 cm from the tip (glove / cuff boundary)
            float pw = pmax - 0.105f * S, best = float.MaxValue;
            for (float pr = pmax - 0.15f * S; pr <= pmax - 0.07f * S; pr += 0.005f)
            {
                int k = Mathf.Clamp((int)((pr - pmin) / step), 0, ns - 1);
                if (cnt[k] < 3) continue;
                if (rad[k] < best - 0.002f) { best = rad[k]; pw = pr; }
            }
            // shoulder (gleno-humeral centre)
            float ps; Vector3 sh;
            if (Mathf.Abs(dir.y) > 0.6f)
            {
                // arms down: the joint sits ~5.5 cm under the top of the shoulder surface straight above the arm root
                Vector3 a0 = At(pmin + 0.03f * S);
                float topY = float.MinValue;
                for (int i = 0; i < V.Count; i++)
                {
                    var p = V[i];
                    if (Mathf.Abs(p.x - a0.x) > 0.03f * S || Mathf.Abs(p.z - a0.z) > 0.05f * S) continue;
                    if (p.y < a0.y - 0.02f || p.y > a0.y + 0.12f * S) continue;
                    topY = Mathf.Max(topY, p.y);
                }
                if (topY == float.MinValue) topY = a0.y + 0.03f * S;
                sh = new Vector3(a0.x - Mathf.Sign(a0.x) * 0.012f * S, topY - 0.055f * S, a0.z);
                ps = Vector3.Dot(sh - mean, dir);
            }
            else
            {
                // T-pose: on the centreline just inside the arm root, raised toward the top of the arm: lowering the arm
                // then bends the shoulder line around a small radius instead of swinging the deltoid out (puffy shoulders)
                ps = pmin + 0.015f * S;
                sh = At(ps);
                // pivot inside the torso side (the gleno-humeral centre sits under the deltoid, not at the sleeve seam)
                float r0 = rad[Mathf.Clamp((int)((ps - pmin) / step), 0, ns - 1)];
                sh.x -= Mathf.Sign(sh.x) * 0.6f * r0;
                sh.y += 0.15f * r0;
                ps = Vector3.Dot(sh - mean, dir);
            }
            Vector3 wr = At(pw);
            float pe = ps + 0.56f * (pw - ps);
            Vector3 el = At(pe);
            log?.AppendLine($"    arm{a.Side} refine: len {(pmax - pmin):F3} shoulder {sh} elbow {el} wrist {wr} tip {tip} (wrist {(pmax - pw) * 100f:F1} cm from tip, min r {best * 100f:F1} cm)");
            a.S = sh; a.E = el; a.W = wr; a.T = tip;
            a.LUA = (a.E - a.S).magnitude; a.LFA = (a.W - a.E).magnitude; a.LH = (a.T - a.W).magnitude;
        }

        public static Params Classify(List<Vector3> V, int[] weld, List<int>[] adj, int weldCount, GlbAnalysis an, GlbRigger.Landmarks lm, System.Text.StringBuilder log, string id)
        {
            float S = lm.H / 1.75f;
            var P = new Params { Arms = MakeArms(lm) };
            int n = V.Count;
            P.Side = new int[n]; P.AS = new float[n]; P.DE = new float[n]; P.DW = new float[n]; P.Dist = new float[n]; P.ShoulderCap = new float[n];
            for (int pass = 0; pass < 3; pass++)
            {
                var cand = new int[n];
                var core = new bool[n];
                Parallel.For(0, n, i =>
                {
                    cand[i] = -1;
                    var p = V[i];
                    for (int s = 0; s < 2; s++)
                    {
                        var a = P.Arms[s];
                        float sx = s == 0 ? -1f : 1f;
                        if (p.x * sx < Mathf.Abs(a.S.x) * 0.55f) continue;
                        Chain(a, p, out float along, out float dist, out float aS);
                        if (aS < -0.012f) continue;
                        float lenArm = a.LUA + a.LFA;
                        float rCap = along < lenArm ? Mathf.Lerp(0.105f, 0.08f, along / lenArm) * S : 0.11f * S;
                        if (dist > rCap) continue;
                        bool lower = along > a.LUA + a.LFA * 0.45f;
                        bool flood = an.IsArm(p, s, out _);
                        if (!lower && !flood) continue;
                        cand[i] = s;
                        if (dist < 0.05f * S && along > a.LUA * 0.35f && along < lenArm) core[i] = flood || lower;
                        break;
                    }
                });
                // connectivity on the welded graph from the arm core, through candidates of the same side
                var wl = new int[weldCount]; var wcore = new bool[weldCount];
                for (int k = 0; k < weldCount; k++) wl[k] = -2;
                for (int i = 0; i < n; i++)
                {
                    int k = weld[i];
                    if (wl[k] == -2) wl[k] = cand[i]; else if (wl[k] != cand[i]) wl[k] = -1;
                    if (core[i]) wcore[k] = true;
                }
                var reached = new int[weldCount];
                for (int k = 0; k < weldCount; k++) reached[k] = -1;
                var q = new Queue<int>();
                for (int k = 0; k < weldCount; k++) if (wcore[k] && wl[k] >= 0) { reached[k] = wl[k]; q.Enqueue(k); }
                while (q.Count > 0)
                {
                    int k = q.Dequeue();
                    foreach (int j in adj[k])
                    {
                        if (reached[j] >= 0 || wl[j] != reached[k]) continue;
                        reached[j] = reached[k]; q.Enqueue(j);
                    }
                }
                // adopt small non-arm islands hanging off an arm (thumb tips, cuff buttons, glove seams...)
                {
                    var comp = new int[weldCount];
                    for (int k = 0; k < weldCount; k++) comp[k] = -1;
                    var members = new List<int>();
                    int adopted = 0;
                    for (int k0 = 0; k0 < weldCount; k0++)
                    {
                        if (reached[k0] >= 0 || comp[k0] >= 0 || wl[k0] == -2) continue;
                        members.Clear(); q.Enqueue(k0); comp[k0] = k0;
                        int touchL = 0, touchR = 0;
                        while (q.Count > 0)
                        {
                            int k = q.Dequeue(); members.Add(k);
                            foreach (int j in adj[k])
                            {
                                if (reached[j] == 0) touchL++; else if (reached[j] == 1) touchR++;
                                if (reached[j] >= 0 || comp[j] >= 0) continue;
                                comp[j] = k0; q.Enqueue(j);
                            }
                        }
                        if (members.Count < weldCount / 200 && (touchL > 0 || touchR > 0))
                        {
                            int side = touchL >= touchR ? 0 : 1;
                            foreach (int k in members) { adopted++; reached[k] = side; }
                        }
                    }
                    if (pass == 2) log.AppendLine($"[{id}] arm islands adopted: {adopted} welded verts");
                }
                // detached mesh islands (straps, epaulettes, buttons, cuffs modelled as separate shells): the whole island
                // follows the label of the nearest vertex of the main body
                {
                    var wpos = new Vector3[weldCount]; var has = new bool[weldCount];
                    for (int i = 0; i < n; i++) { wpos[weld[i]] = V[i]; has[weld[i]] = true; }
                    var comp = new int[weldCount]; for (int k = 0; k < weldCount; k++) comp[k] = -1;
                    var sizes = new List<int>(); var lists = new List<List<int>>();
                    for (int k0 = 0; k0 < weldCount; k0++)
                    {
                        if (!has[k0] || comp[k0] >= 0) continue;
                        int cid = lists.Count; var l = new List<int>(); comp[k0] = cid; q.Enqueue(k0);
                        while (q.Count > 0) { int k = q.Dequeue(); l.Add(k); foreach (int j in adj[k]) if (comp[j] < 0) { comp[j] = cid; q.Enqueue(j); } }
                        lists.Add(l);
                    }
                    int big = lists.Max(l => l.Count), moved = 0;
                    for (int c = 0; c < lists.Count; c++)
                    {
                        var l = lists[c];
                        if (l.Count > big / 20) continue;
                        Vector3 ctr = Vector3.zero; foreach (int k in l) ctr += wpos[k]; ctr /= l.Count;
                        float bd = 0.06f * S; int bl = -3;
                        for (int k = 0; k < weldCount; k++)
                        {
                            if (!has[k] || comp[k] == c || lists[comp[k]].Count <= big / 20) continue;
                            float d = (wpos[k] - ctr).sqrMagnitude;
                            if (d < bd * bd) { bd = Mathf.Sqrt(d); bl = reached[k]; }
                        }
                        if (bl == -3) continue;
                        foreach (int k in l) { if (reached[k] != bl) moved++; reached[k] = bl; }
                    }
                    if (pass == 2) log.AppendLine($"[{id}] detached islands: {lists.Count - 1}, relabelled {moved} welded verts");
                }
                // T-pose scans: everything lateral of the shoulder joint plane inside the arm capsule rides on the arm
                // (epaulettes, shoulder straps and sleeve-head panels stitched to the torso would otherwise stay up)
                P.TPose = an.TPose;
                if (an.TPose)
                {
                    int grabbed = 0;
                    for (int s = 0; s < 2; s++)
                    {
                        var a = P.Arms[s]; var xs = new List<float>();
                        for (int i = 0; i < n; i++) { var p = V[i]; if (reached[weld[i]] >= 0 || p.x * (s == 0 ? -1f : 1f) <= 0 || p.y > a.S.y - 0.24f * S || p.y < a.S.y - 0.42f * S) continue; xs.Add(Mathf.Abs(p.x)); }
                        xs.Sort(); P.TorsoHalf[s] = xs.Count > 20 ? xs[(int)(xs.Count * 0.95f)] : Mathf.Abs(a.S.x) - 0.01f;
                    }
                    for (int i = 0; i < n; i++)
                    {
                        int k = weld[i];
                        if (reached[k] >= 0) continue;
                        var p = V[i];
                        for (int s = 0; s < 2; s++)
                        {
                            var a = P.Arms[s];
                            if (p.x * (s == 0 ? -1f : 1f) <= 0) continue;
                            Chain(a, p, out float along, out float dist, out float aS);
                            if (Mathf.Abs(p.x) < P.TorsoHalf[s] + 0.01f || p.y < a.S.y - 0.24f * S) continue;
                            // hair locks hanging beside the neck sit above the shoulder joint and medial of it: never arm (the repose would
                            // swing them out into wings once no cape widens the torso)
                            if (p.y > a.S.y + 0.03f * S && Mathf.Abs(p.x) < Mathf.Abs(a.S.x) + 0.04f * S) continue;
                            float lenArm = a.LUA + a.LFA;
                            float rCap = 0.2f * S;
                            if (dist > rCap) continue;
                            reached[k] = s; grabbed++; break;
                        }
                    }
                    if (pass == 2) log.AppendLine($"[{id}] T-pose: {grabbed} verts lateral of the shoulder joined the arms (torso half {P.TorsoHalf[0]:F3} / {P.TorsoHalf[1]:F3}, shoulder {P.Arms[0].S} / {P.Arms[1].S})");
                }
                for (int i = 0; i < n; i++) P.Side[i] = reached[weld[i]];
                if (pass < 2) { RefineJoints(P.Arms[0], V, P.Side, S, pass == 1 ? log : null); RefineJoints(P.Arms[1], V, P.Side, S, pass == 1 ? log : null); }
            }
            // chain coordinates
            Parallel.For(0, n, i =>
            {
                int s = P.Side[i];
                var p = V[i];
                if (s < 0)
                {
                    // torso vertices around the shoulder cap
                    for (int t = 0; t < 2; t++)
                    {
                        var a = P.Arms[t];
                        float sx = t == 0 ? -1f : 1f;
                        if (p.x * sx < Mathf.Abs(a.S.x) * 0.4f) continue;
                        Chain(a, p, out _, out _, out float aS2);
                        float d = (p - a.S).magnitude;
                        if (d > 0.13f * S || p.y < a.S.y - 0.05f * S) continue;
                        float cap = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.07f * S, -0.005f, aS2)) * Mathf.Clamp01((0.13f * S - d) / (0.05f * S));
                        P.ShoulderCap[i] = Mathf.Max(P.ShoulderCap[i], cap);
                        P.AS[i] = aS2;
                    }
                    return;
                }
                var arm = P.Arms[s];
                Chain(arm, p, out float along, out float dist, out float aS);
                P.AS[i] = aS; P.Dist[i] = dist;
                P.DE[i] = along - arm.LUA;
                P.DW[i] = along - arm.LUA - arm.LFA;
            });
            // the refined chain replaces the silhouette landmarks (skeleton, torso weights and the repose use it)
            { var l = P.Arms[0]; var r = P.Arms[1]; lm.ShoulderL = l.S; lm.ElbowL = l.E; lm.WristL = l.W; lm.TipL = l.T; lm.ShoulderR = r.S; lm.ElbowR = r.E; lm.WristR = r.W; lm.TipR = r.T; lm.ShoulderX = (Mathf.Abs(l.S.x) + Mathf.Abs(r.S.x)) * 0.5f; lm.ShoulderY = (l.S.y + r.S.y) * 0.5f; }
            int nl = P.Side.Count(x => x == 0), nr = P.Side.Count(x => x == 1);
            log.AppendLine($"[{id}] arm membership L {nl} R {nr} verts; elbow L {P.Arms[0].E} wrist L {P.Arms[0].W} tip L {P.Arms[0].T}");
            return P;
        }

        static KeyValuePair<int, float> KV(HBone b, float w) => new KeyValuePair<int, float>((int)b, w);

        /// <summary>Skin weights of an arm vertex (original pose chain coordinates).</summary>
        /// <summary>T-pose scans: how far a vertex lies outside the torso side (it belongs to the arm / armpit, whatever its aS).</summary>
        public static float LateralK(Params P, int s, Vector3 p, float S) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.015f * S, 0.05f * S, Mathf.Abs(p.x) - P.TorsoHalf[s]));

        public static BoneWeight ArmWeight(Params P, int i, float S, Vector3 p)
        {
            int s = P.Side[i]; bool L = s == 0;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.015f, 0.09f * S, P.AS[i]));
            if (P.TPose) k = Mathf.Max(k, LateralK(P, s, p, S));
            float b1 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.035f * S, 0.035f * S, P.DE[i]));
            float b2 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.018f * S, 0.02f * S, P.DW[i]));
            var l = new List<KeyValuePair<int, float>>
            {
                KV(L ? HBone.UpperArmL : HBone.UpperArmR, k * (1f - b1)),
                KV(L ? HBone.LowerArmL : HBone.LowerArmR, k * b1 * (1f - b2)),
                KV(L ? HBone.HandL : HBone.HandR, k * b1 * b2),
                KV(L ? HBone.ShoulderL : HBone.ShoulderR, (1f - k) * 0.6f),
                KV(HBone.UpperChest, (1f - k) * 0.4f),
            };
            return CharMesher.ToBW(l);
        }

        // ------------------------------------------------------------------ repose
        static Quaternion Twist(Vector3 axis, float deg) => Quaternion.AngleAxis(deg, axis);

        /// <summary>
        /// Rest pose per arm: the upper arm hangs almost vertically (small abduction) against the torso side, shifted
        /// out just enough that the sleeve does not sink into the body; the forearm gets a carrying angle that clears the
        /// hips / coat; a little forward swing and a soft elbow.
        /// </summary>
        public static void SolveTargets(Params P, List<Vector3> V, GlbRigger.Landmarks lm, System.Text.StringBuilder log, string id,
                                        float upperDeg = 5f, float fwdDeg = 3f, float elbowDeg = 10f)
        {
            float S = lm.H / 1.75f;
            const float bin = 0.01f;
            int nb = Mathf.CeilToInt(lm.H / bin) + 1;
            var rows = new List<Vector2>[2, nb];
            for (int s = 0; s < 2; s++) for (int b = 0; b < nb; b++) rows[s, b] = new List<Vector2>();
            for (int i = 0; i < V.Count; i++)
            {
                if (P.Side[i] >= 0) continue;
                var p = V[i];
                int b = Mathf.Clamp((int)(p.y / bin), 0, nb - 1);
                rows[p.x < 0 ? 0 : 1, b].Add(new Vector2(Mathf.Abs(p.x), p.z));
            }
            var shift = new float[2]; var carry = new float[2]; var upper = new float[2];
            foreach (var a in P.Arms)
            {
                var rad = new float[24];
                var lists = new List<float>[24];
                for (int k = 0; k < 24; k++) lists[k] = new List<float>();
                float len = a.Len;
                for (int i = 0; i < V.Count; i++)
                {
                    if (P.Side[i] != a.Side) continue;
                    float along = P.DE[i] + a.LUA;
                    int k = Mathf.Clamp((int)(along / len * 24f), 0, 23);
                    lists[k].Add(P.Dist[i]);
                }
                for (int k = 0; k < 24; k++) { var l = lists[k].OrderBy(x => x).ToList(); rad[k] = l.Count > 10 ? l[(int)(l.Count * 0.85f)] : 0.045f * S; }
                float sx = a.Side == 0 ? -1f : 1f;
                // max penetration of the arm samples in [k0, k1) into the body with the given pose
                float Pen(int k0, int k1, float allow)
                {
                    float worst = 0f;
                    for (int k = k0; k < k1; k++)
                    {
                        float t = (k + 0.5f) / 24f * len;
                        Vector3 q = t < a.LUA ? Vector3.Lerp(a.S2, a.E2, t / a.LUA) : t < a.LUA + a.LFA ? Vector3.Lerp(a.E2, a.W2, (t - a.LUA) / a.LFA) : Vector3.Lerp(a.W2, a.T2, Mathf.Clamp01((t - a.LUA - a.LFA) / a.LH));
                        int b = Mathf.Clamp((int)(q.y / bin), 0, nb - 1);
                        float inner = Mathf.Abs(q.x) - rad[k] + allow;
                        for (int bb = Mathf.Max(0, b - 1); bb <= Mathf.Min(nb - 1, b + 1); bb++)
                            foreach (var bp in rows[a.Side, bb])
                                if (Mathf.Abs(bp.y - q.z) < rad[k] * 0.8f && bp.x > inner) worst = Mathf.Max(worst, bp.x - inner);
                    }
                    return worst;
                }
                int kUA0 = Mathf.CeilToInt(a.LUA * 0.6f / len * 24f), kUA1 = Mathf.FloorToInt(a.LUA * 0.95f / len * 24f);
                int kFA0 = Mathf.CeilToInt(a.LUA / len * 24f);
                // search (upper-arm abduction, shoulder shift, carrying angle), smallest abduction first: the elbow may
                // press ~1.5 cm into the torso (sleeve against the body), forearm / hand ~1.2 cm into hips or coat
                bool found = false; float bu = 12f, bd = 0.025f * S, bc = 15f;
                for (float au = upperDeg; au <= 16.01f && !found; au += 1f)
                    for (float dd = 0f; dd <= 0.0251f * S && !found; dd += 0.005f * S)
                    {
                        Pose(a, au, 4f, dd, fwdDeg, elbowDeg, sx);
                        if (Pen(kUA0, kUA1 + 1, -0.025f * S) > 0.001f) continue;
                        for (float cc = 4f; cc <= 15.01f; cc += 1f)
                        {
                            Pose(a, au, cc, dd, fwdDeg, elbowDeg, sx);
                            if (Pen(kFA0, 24, -0.012f * S) <= 0.0005f) { bu = au; bd = dd; bc = cc; found = true; break; }
                        }
                    }
                upper[a.Side] = bu; shift[a.Side] = bd; carry[a.Side] = bc;
                var dbg = new System.Text.StringBuilder();
                for (float au = 5f; au <= 14.01f; au += 3f) { Pose(a, au, 4f, 0f, fwdDeg, elbowDeg, sx); float pu = Pen(kUA0, kUA1 + 1, -0.015f * S); Pose(a, au, 10f, 0.01f, fwdDeg, elbowDeg, sx); dbg.Append($" A{au:F0}: upperPen {pu * 100f:F1}cm forePen(c10,d1) {Pen(kFA0, 24, -0.012f * S) * 100f:F1}cm;"); }
                log.AppendLine($"[{id}] arm{a.Side} found={found} rad(UA mid) {rad[kUA0] * 100f:F1}cm rad(hand) {rad[22] * 100f:F1}cm {dbg}");
            }
            float dm = Mathf.Max(shift[0], shift[1]), cm = Mathf.Max(carry[0], carry[1]); upperDeg = Mathf.Max(upper[0], upper[1]);
            foreach (var a in P.Arms) { a.Angle = upperDeg; a.Carry = cm; a.Shift = dm; Pose(a, upperDeg, cm, dm, fwdDeg, elbowDeg, a.Side == 0 ? -1f : 1f); }
            log.AppendLine($"[{id}] repose: upper arm {upperDeg:F1} deg, shoulder shift {dm * 100f:F1} cm, carrying angle {cm:F1} deg");
        }

        static void Pose(Arm a, float A, float carryDeg, float shift, float fwdDeg, float elbowDeg, float sx)
        {
            Vector3 dUA0 = (a.E - a.S).normalized, dFA0 = (a.W - a.E).normalized, dH0 = (a.T - a.W).normalized;
            Vector3 dUA = new Vector3(sx * Mathf.Sin(A * Mathf.Deg2Rad), -Mathf.Cos(A * Mathf.Deg2Rad), Mathf.Tan(fwdDeg * Mathf.Deg2Rad)).normalized;
            Vector3 fwdPerp = Vector3.ProjectOnPlane(Vector3.forward, dUA).normalized;
            Vector3 outPerp = Vector3.ProjectOnPlane(new Vector3(sx, 0, 0), dUA).normalized;
            Vector3 dFA = (dUA + fwdPerp * Mathf.Tan(elbowDeg * Mathf.Deg2Rad) + outPerp * Mathf.Tan(carryDeg * Mathf.Deg2Rad)).normalized;
            a.Rua = Quaternion.FromToRotation(dUA0, dUA);
            a.Rfa = Quaternion.FromToRotation(a.Rua * dFA0, dFA) * a.Rua;
            a.Rh = Quaternion.FromToRotation(a.Rfa * dH0, dFA) * a.Rfa;
            a.S2 = a.S + new Vector3(sx * shift, -shift * 0.25f, 0f);
            a.E2 = a.S2 + a.Rua * (a.E - a.S); a.W2 = a.E2 + a.Rfa * (a.W - a.E); a.T2 = a.W2 + a.Rh * (a.T - a.W);
        }

        /// <summary>Pronation so the palm faces the thigh (thumb forward), from the hand's shape.</summary>
        public static void SolveTwist(Params P, List<Vector3> V, System.Text.StringBuilder log, string id, GlbHands.Hand[] hands = null)
        {
            foreach (var a in P.Arms)
            {
                Vector3 ax = a.FaDir2;
                var pts = new List<Vector3>();
                for (int i = 0; i < V.Count; i++)
                    if (P.Side[i] == a.Side && P.DW[i] > 0.01f) pts.Add(a.Rh * (V[i] - a.W));
                if (pts.Count < 50) { a.Twist = 0; continue; }
                // principal width axis in the plane perpendicular to the forearm
                Vector3 b1 = Vector3.ProjectOnPlane(Vector3.right, ax).normalized, b2 = Vector3.Cross(ax, b1);
                double xx = 0, xy = 0, yy = 0; Vector2 mean = Vector2.zero;
                var p2 = pts.Select(p => new Vector2(Vector3.Dot(p, b1), Vector3.Dot(p, b2))).ToList();
                foreach (var p in p2) mean += p; mean /= p2.Count;
                foreach (var p in p2) { var d = p - mean; xx += d.x * d.x; xy += d.x * d.y; yy += d.y * d.y; }
                double ang = 0.5 * Math.Atan2(2 * xy, xx - yy);
                Vector3 w = (b1 * (float)Math.Cos(ang) + b2 * (float)Math.Sin(ang)).normalized;
                // thumb side: the proximal half of the hand sticks out further on the thumb side (third moment)
                double skew = 0; float hl = a.LH;
                foreach (var p in pts)
                {
                    float h = Vector3.Dot(p, ax);
                    if (h > hl * 0.55f) continue;
                    float c = Vector3.Dot(p, w) - Vector3.Dot(mean.x * b1 + mean.y * b2, w);
                    skew += c * c * c;
                }
                Vector3 thumb = skew >= 0 ? w : -w;
                if (hands != null && hands[a.Side] != null && hands[a.Side].Ok) thumb = Vector3.ProjectOnPlane(a.Rh * hands[a.Side].ThumbDir, ax).normalized;
                Vector3 target = Vector3.ProjectOnPlane(Vector3.forward + (a.Side == 0 ? Vector3.right : Vector3.left) * 0.15f, ax).normalized;
                a.Twist = Mathf.Clamp(Vector3.SignedAngle(thumb, target, ax), -130f, 130f);
                log.AppendLine($"[{id}] arm{a.Side} hand twist {a.Twist:F0} deg (width axis {w}, thumb {thumb})");
            }
        }

        /// <summary>Rest-pose position of a point that rides rigidly on the hand.</summary>
        public static Vector3 HandToRest(Arm a, Vector3 p) => a.W2 + Quaternion.AngleAxis(a.Twist, a.FaDir2) * (a.Rh * (p - a.W));

        // dual quaternion helpers (x,y,z,w in Vector4)
        static Vector4 Q(Quaternion q) => new Vector4(q.x, q.y, q.z, q.w);
        static Vector4 QMul(Vector4 a, Vector4 b) => new Vector4(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
            a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);

        struct DQ { public Vector4 R, D; }
        static DQ Make(Quaternion r, Vector3 t) { var rr = Q(r); return new DQ { R = rr, D = QMul(new Vector4(t.x, t.y, t.z, 0), rr) * 0.5f }; }

        /// <summary>Applies the repose to all vertices / normals and updates the landmarks' arm joints.</summary>
        public static void Apply(Params P, List<Vector3> V, List<Vector3> N, GlbRigger.Landmarks lm, float S)
        {
            int n = V.Count;
            Parallel.For(0, n, i =>
            {
                int s = P.Side[i];
                float cap = P.ShoulderCap[i];
                if (s < 0 && cap <= 0f) return;
                if (s < 0)
                {
                    // torso shoulder cap: follow the upper arm a little
                    int side = V[i].x < 0 ? 0 : 1;
                    var a0 = P.Arms[side];
                    float w0 = (P.TPose ? 0.45f : 0.3f) * cap;
                    var dqI = Make(Quaternion.identity, Vector3.zero);
                    var dqU = Make(a0.Rua, a0.S2 - a0.Rua * a0.S);
                    Blend(V, N, i, new[] { dqI, dqU }, new[] { 1f - w0, w0 });
                    return;
                }
                var a = P.Arms[s];
                float wS = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.02f, 0.11f * S, P.AS[i]));
                if (P.TPose) wS = Mathf.Min(wS, 1f - LateralK(P, s, V[i], S));
                float b1 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.03f * S, 0.03f * S, P.DE[i]));
                float b2 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.015f * S, 0.015f * S, P.DW[i]));
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(P.DE[i] / Mathf.Max(0.01f, a.LFA)));
                Vector3 ax = a.FaDir2;
                var qFa = Twist(ax, a.Twist * u) * a.Rfa;
                var qH = Twist(ax, a.Twist) * a.Rh;
                var dq = new[]
                {
                    Make(Quaternion.identity, Vector3.zero),
                    Make(a.Rua, a.S2 - a.Rua * a.S),
                    Make(qFa, a.E2 - qFa * a.E),
                    Make(qH, a.W2 - qH * a.W),
                };
                float wr = 1f - wS;
                var w = new[] { wS, wr * (1f - b1), wr * b1 * (1f - b2), wr * b1 * b2 };
                Blend(V, N, i, dq, w);
            });
            for (int s = 0; s < 2; s++)
            {
                var a = P.Arms[s];
                if (s == 0) { lm.ShoulderL = a.S2; lm.ElbowL = a.E2; lm.WristL = a.W2; lm.TipL = a.T2; lm.ArmAngleL = a.Angle; }
                else { lm.ShoulderR = a.S2; lm.ElbowR = a.E2; lm.WristR = a.W2; lm.TipR = a.T2; lm.ArmAngleR = a.Angle; }
            }
        }

        static void Blend(List<Vector3> V, List<Vector3> N, int i, DQ[] dq, float[] w)
        {
            Vector4 r = Vector4.zero, d = Vector4.zero;
            Vector4 r0 = Vector4.zero; bool first = true;
            for (int k = 0; k < dq.Length; k++)
            {
                if (w[k] <= 1e-5f) continue;
                if (first) { r0 = dq[k].R; first = false; }
                float sgn = Vector4.Dot(dq[k].R, r0) < 0 ? -1f : 1f;
                r += dq[k].R * (w[k] * sgn); d += dq[k].D * (w[k] * sgn);
            }
            float len = r.magnitude;
            if (len < 1e-8f) return;
            r /= len; d /= len;
            var conj = new Vector4(-r.x, -r.y, -r.z, r.w);
            var tq = QMul(d, conj) * 2f;
            var rot = new Quaternion(r.x, r.y, r.z, r.w);
            V[i] = rot * V[i] + new Vector3(tq.x, tq.y, tq.z);
            N[i] = rot * N[i];
        }

        /// <summary>Arm idle abduction for the animator so the idle arm sits on the rest pose.</summary>
        public static float IdleOut(Params P) => Mathf.Max(3f, P.Arms[0].Angle - 1f);
    }
}
