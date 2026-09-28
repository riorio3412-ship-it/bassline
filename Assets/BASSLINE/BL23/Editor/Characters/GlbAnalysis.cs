using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Characters;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Front-silhouette analysis of a scanned character (2D occupancy grid in XY): torso runs, arm tracking
    /// (arms-down or T-pose), crotch, neck, shoulders; arm segmentation by flood fill with a shoulder cut.
    /// </summary>
    public sealed class GlbAnalysis
    {
        public const float Cell = 0.004f;
        public int NX, NY; public float X0;
        public bool[] Occ;
        public int[] RunL, RunR;      // central run extent per row (cells from centre column)
        public float H;
        public bool TPose;
        public GlbRigger.Landmarks LM;
        public bool[,] ArmMask;       // [side, cell]
        bool[] _armL, _armR;

        public int Col(float x) => Mathf.Clamp(Mathf.FloorToInt((x - X0) / Cell), 0, NX - 1);
        public int Row(float y) => Mathf.Clamp(Mathf.FloorToInt(y / Cell), 0, NY - 1);
        public float X(int c) => X0 + (c + 0.5f) * Cell;
        public float Y(int r) => (r + 0.5f) * Cell;
        bool O(int c, int r) => c >= 0 && r >= 0 && c < NX && r < NY && Occ[r * NX + c];

        public static GlbAnalysis Run(List<Vector3> V, List<int> T, float H)
        {
            var a = new GlbAnalysis { H = H };
            a.X0 = -1.1f; a.NX = Mathf.CeilToInt(2.2f / Cell); a.NY = Mathf.CeilToInt((H + 0.02f) / Cell);
            a.Occ = new bool[a.NX * a.NY];
            // rasterise triangles (front projection)
            for (int t = 0; t < T.Count; t += 3)
            {
                Vector3 p0 = V[T[t]], p1 = V[T[t + 1]], p2 = V[T[t + 2]];
                a.FillTri(p0, p1, p2);
            }
            a.Analyse(V);
            return a;
        }

        void FillTri(Vector3 a, Vector3 b, Vector3 c)
        {
            float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
            float minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
            int c0 = Col(minX), c1 = Col(maxX), r0 = Row(minY), r1 = Row(maxY);
            if (c1 - c0 > 60 || r1 - r0 > 60) return;
            for (int r = r0; r <= r1; r++)
                for (int cc = c0; cc <= c1; cc++)
                {
                    Vector2 p = new Vector2(X(cc), Y(r));
                    if (InTri(p, a, b, c, Cell * 0.75f)) Occ[r * NX + cc] = true;
                }
        }

        static bool InTri(Vector2 p, Vector3 a, Vector3 b, Vector3 c, float pad)
        {
            Vector2 A = a, B = b, C = c;
            float d1 = Edge(p, A, B), d2 = Edge(p, B, C), d3 = Edge(p, C, A);
            float area = Mathf.Abs(Edge(C, A, B));
            if (area < 1e-12f) return (p - A).sqrMagnitude < pad * pad;
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            if (!(neg && pos)) return true;
            // near an edge
            return SegD(p, A, B) < pad || SegD(p, B, C) < pad || SegD(p, C, A) < pad;
        }
        static float Edge(Vector2 p, Vector2 a, Vector2 b) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        static float SegD(Vector2 p, Vector2 a, Vector2 b) { Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-12f, ab.sqrMagnitude)); return (p - (a + ab * t)).magnitude; }

        void Analyse(List<Vector3> V)
        {
            var lm = new GlbRigger.Landmarks { H = H };
            LM = lm;
            float S = H / 1.75f;
            int cx = Col(0f);
            RunL = new int[NY]; RunR = new int[NY];
            for (int r = 0; r < NY; r++)
            {
                int gap = 0, last = cx;
                for (int c = cx; c < NX; c++) { if (O(c, r)) { last = c; gap = 0; } else if (++gap >= 3) break; }
                RunR[r] = last - cx;
                gap = 0; last = cx;
                for (int c = cx; c >= 0; c--) { if (O(c, r)) { last = c; gap = 0; } else if (++gap >= 3) break; }
                RunL[r] = cx - last;
            }
            float HalfW(int r) => Mathf.Max(RunL[r], RunR[r]) * Cell;
            // ---- T-pose test: horizontally connected arms around 0.8H
            float maxRun = 0f;
            for (int r = Row(0.7f * H); r < Row(0.86f * H); r++) maxRun = Mathf.Max(maxRun, Mathf.Min(RunL[r], RunR[r]) * Cell);
            TPose = maxRun > 0.33f * S;

            // ---- neck (narrowest central run in the upper body)
            int nb = Row(0.8f * H); float nw = 9f;
            for (int r = Row(0.78f * H); r < Row(0.92f * H); r++)
            {
                float w = (RunL[r] + RunR[r]) * Cell;
                if (w > 0.03f && w < nw) { nw = w; nb = r; }
            }
            lm.NeckY = Y(nb);
            float headH = H - lm.NeckY;
            lm.ChinY = lm.NeckY + headH * 0.16f;

            // ---- crotch: scanning down from 0.6H, highest row with an empty centre column (legs apart)
            int crotch = -1;
            for (int r = Row(0.6f * H); r > Row(0.3f * H); r--)
            {
                bool centre = O(cx, r) || O(cx - 1, r) || O(cx + 1, r);
                if (!centre) { crotch = r; break; }
            }
            lm.CrotchY = crotch >= 0 && Y(crotch) > 0.4f * H ? Y(crotch) : 0.465f * H;
            lm.HipY = lm.CrotchY + 0.035f * S;
            lm.AnkleY = 0.045f * H;
            lm.KneeY = lm.AnkleY + (lm.HipY - lm.AnkleY) * 0.49f;
            lm.WaistY = lm.HipY + (H - lm.HipY) * 0.22f;

            // ---- arms
            lm.Report += TPose ? " [T-pose]" : " [arms-down]";
            if (TPose) ArmsTPose(V, lm, S); else ArmsDown(V, lm, S);
            lm.ChestY = lm.WaistY + (lm.ShoulderY - lm.WaistY) * 0.45f;
            lm.ShoulderX = (Mathf.Abs(lm.ShoulderL.x) + Mathf.Abs(lm.ShoulderR.x)) * 0.5f;

            // ---- head / face
            lm.HeadCenter = new Vector3(0, (lm.ChinY + H) * 0.5f, 0);
            var headBand = V.Where(v => v.y > lm.ChinY && v.y < H - 0.04f && Mathf.Abs(v.x) < 0.08f).ToList();
            if (headBand.Count > 0) lm.HeadCenter.z = (headBand.Min(v => v.z) + headBand.Max(v => v.z)) * 0.5f;
            float eyeY = lm.ChinY + (H - lm.ChinY) * 0.42f;
            var faceBand = V.Where(v => Mathf.Abs(v.y - eyeY) < 0.01f && Mathf.Abs(v.x) < 0.03f).ToList();
            lm.EyeCenter = new Vector3(0, eyeY, (faceBand.Count > 0 ? faceBand.Max(v => v.z) : 0.1f) - 0.01f);
            var chestBand = V.Where(v => Mathf.Abs(v.y - lm.ChestY) < 0.01f && Mathf.Abs(v.x) < 0.05f).ToList();
            lm.TorsoHalfD = chestBand.Count > 0 ? (chestBand.Max(v => v.z) - chestBand.Min(v => v.z)) * 0.5f : 0.1f;
            lm.TorsoHalfW = HalfW(Row(lm.WaistY));

            // ---- legs
            var foot = V.Where(v => v.y < 0.05f * S).ToList();
            float hipX = 0.052f * H;
            var legPts = V.Where(v => Mathf.Abs(v.y - lm.KneeY) < 0.02f && Mathf.Abs(v.x) < 0.2f * S).ToList();
            for (int s = 0; s < 2; s++)
            {
                float sx = s == 0 ? -1f : 1f;
                var fs = foot.Where(v => v.x * sx > 0.005f).ToList();
                Vector3 ac = fs.Count > 0 ? new Vector3((fs.Min(v => v.x) + fs.Max(v => v.x)) * 0.5f, lm.AnkleY, 0) : new Vector3(sx * hipX, lm.AnkleY, 0);
                float toeZ = fs.Count > 0 ? fs.Max(v => v.z) : 0.15f;
                float heelZ = fs.Count > 0 ? fs.Min(v => v.z) : -0.05f;
                ac.z = heelZ + 0.055f * S;
                float toeX = fs.Count > 0 ? fs.Where(v => v.z > toeZ - 0.03f).Select(v => v.x).DefaultIfEmpty(ac.x).Average() : ac.x;
                Vector3 hip = new Vector3(sx * Mathf.Max(hipX, Mathf.Abs(ac.x) * 0.85f), lm.HipY, ac.z * 0.3f);
                Vector3 kc = new Vector3(Mathf.Lerp(hip.x, ac.x, 0.5f), lm.KneeY, ac.z * 0.5f + 0.015f);
                if (s == 0) { lm.HipL = hip; lm.KneeL = kc; lm.AnkleL = ac; lm.ToeL = new Vector3(toeX, 0.01f, toeZ); }
                else { lm.HipR = hip; lm.KneeR = kc; lm.AnkleR = ac; lm.ToeR = new Vector3(toeX, 0.01f, toeZ); }
            }
            lm.HipX = (Mathf.Abs(lm.HipL.x) + Mathf.Abs(lm.HipR.x)) * 0.5f;
            lm.Report += $" crotch {lm.CrotchY:F2} hip {lm.HipY:F2} waist {lm.WaistY:F2} chest {lm.ChestY:F2} shoulderY {lm.ShoulderY:F2} neck {lm.NeckY:F2} chin {lm.ChinY:F2} shX {lm.ShoulderX:F2} hipX {lm.HipX:F2}";

            // ---- arm masks
            _armL = FloodArm(0); _armR = FloodArm(1);
        }

        void ArmsDown(List<Vector3> V, GlbRigger.Landmarks lm, float S)
        {
            int cx = Col(0f);
            for (int s = 0; s < 2; s++)
            {
                int dir = s == 0 ? -1 : 1;
                // find the arm run beside the torso at each row, scanning down from just below the neck
                var centres = new List<Vector2>();
                int armpit = -1, prevLo = -1, prevHi = -1;
                for (int r = Row(lm.NeckY - 0.1f * S); r > Row(0.28f * H); r--)
                {
                    int edge = s == 0 ? RunL[r] : RunR[r];
                    // first occupied run beyond the torso edge
                    int c = cx + dir * (edge + 3);
                    int lo = -1, hi = -1;
                    for (int k = 0; k < 90; k++, c += dir)
                    {
                        if (O(c, r)) { if (lo < 0) lo = c; hi = c; }
                        else if (lo >= 0) break;
                    }
                    if (lo < 0)
                    {
                        if (armpit >= 0) break; else continue;
                    }
                    if (armpit >= 0 && prevLo >= 0)
                    {
                        // must overlap the previous row's run
                        int a0 = Mathf.Min(lo, hi), a1 = Mathf.Max(lo, hi), b0 = Mathf.Min(prevLo, prevHi), b1 = Mathf.Max(prevLo, prevHi);
                        if (a1 < b0 - 2 || a0 > b1 + 2) break;
                    }
                    if (armpit < 0) armpit = r;
                    prevLo = lo; prevHi = hi;
                    centres.Add(new Vector2((X(lo) + X(hi)) * 0.5f, Y(r)));
                }
                if (centres.Count < 5)
                {
                    lm.Report += $" arm{s}:TRACK-FAIL";
                    centres.Clear();
                    float y0 = 0.72f * H;
                    for (int i = 0; i < 20; i++) centres.Add(new Vector2(dir * (0.2f * S + i * 0.006f), y0 - i * 0.02f));
                    armpit = Row(y0);
                }
                Vector2 top = centres[0], tip2 = centres[centres.Count - 1];
                float armpitY = Y(armpit);
                lm.ShoulderY = Mathf.Max(lm.ShoulderY, armpitY + 0.05f * S);
                // arm axis from a linear fit of the upper 60 % of the track
                int nFit = Mathf.Max(3, (int)(centres.Count * 0.6f));
                float sy = 0, sx2 = 0, syy = 0, sxy = 0;
                for (int i = 0; i < nFit; i++) { sy += centres[i].y; sx2 += centres[i].x; syy += centres[i].y * centres[i].y; sxy += centres[i].x * centres[i].y; }
                float den = nFit * syy - sy * sy;
                float slope = Mathf.Abs(den) > 1e-9f ? (nFit * sxy - sx2 * sy) / den : 0f; // dx/dy
                float icpt = (sx2 - slope * sy) / nFit;
                float shoulderY = Mathf.Clamp(0.81f * H, armpitY + 0.04f * S, lm.ChinY - 0.04f * S);
                int shRow = Row(shoulderY);
                float silW = (s == 0 ? RunL[shRow] : RunR[shRow]) * Cell;
                float shX = dir * Mathf.Max(0.1f * S, silW - 0.045f * S);
                Vector3 shoulder = new Vector3(shX, shoulderY, 0);
                var shBand = V.Where(v => Mathf.Abs(v.y - shoulderY) < 0.02f && Mathf.Abs(v.x - shX) < 0.03f).ToList();
                if (shBand.Count > 0) shoulder.z = (shBand.Min(v => v.z) + shBand.Max(v => v.z)) * 0.5f;
                Vector3 tip = new Vector3(tip2.x, tip2.y, 0);
                var tipBand = V.Where(v => Mathf.Abs(v.y - tip.y) < 0.03f && Mathf.Abs(v.x - tip.x) < 0.04f).ToList();
                if (tipBand.Count > 0) tip.z = (tipBand.Min(v => v.z) + tipBand.Max(v => v.z)) * 0.5f;
                // enforce a plausible arm length
                Vector3 d = (tip - shoulder).normalized;
                float len = (tip - shoulder).magnitude;
                float want = Mathf.Clamp(len, 0.40f * H, 0.44f * H);
                if (Mathf.Abs(want - len) > 0.005f) { lm.Report += $" arm{s}:len {len:F2}->{want:F2}"; tip = shoulder + d * want; }
                SetArm(lm, s, shoulder, tip, centres, V);
            }
            lm.ShoulderY = (lm.ShoulderL.y + lm.ShoulderR.y) * 0.5f;
        }

        void ArmsTPose(List<Vector3> V, GlbRigger.Landmarks lm, float S)
        {
            for (int s = 0; s < 2; s++)
            {
                float sx = s == 0 ? -1f : 1f;
                var upper = V.Where(v => v.y > 0.6f * H && v.x * sx > 0).ToList();
                var ext = upper.OrderByDescending(v => v.x * sx).Take(200).ToList();
                Vector3 tip = ext.Aggregate(Vector3.zero, (a, b) => a + b) / ext.Count;
                tip.x = ext[0].x;
                // arm vertical centre near the shoulder
                float probeX = sx * 0.3f * S;
                var col = V.Where(v => Mathf.Abs(v.x - probeX) < 0.01f && v.y > 0.65f * H).ToList();
                float armY = col.Count > 0 ? (col.Min(v => v.y) + col.Max(v => v.y)) * 0.5f : 0.8f * H;
                float armBottom = col.Count > 0 ? col.Min(v => v.y) : armY - 0.06f;
                // torso half width just under the arm
                int r = Row(armBottom - 0.04f * S);
                float torsoHalf = (s == 0 ? RunL[r] : RunR[r]) * Cell;
                Vector3 shoulder = new Vector3(sx * Mathf.Max(0.1f * S, torsoHalf - 0.025f * S), armY, 0);
                var shBand = V.Where(v => Mathf.Abs(v.y - armY) < 0.03f && Mathf.Abs(v.x - shoulder.x) < 0.02f).ToList();
                if (shBand.Count > 0) shoulder.z = (shBand.Min(v => v.z) + shBand.Max(v => v.z)) * 0.5f;
                var tipBand = V.Where(v => Mathf.Abs(v.x - tip.x) < 0.04f && v.y > 0.6f * H).ToList();
                if (tipBand.Count > 0) { tip.z = (tipBand.Min(v => v.z) + tipBand.Max(v => v.z)) * 0.5f; tip.y = (tipBand.Min(v => v.y) + tipBand.Max(v => v.y)) * 0.5f; }
                lm.ShoulderY = armY;
                SetArm(lm, s, shoulder, tip, null, V);
            }
            lm.ShoulderY = (lm.ShoulderL.y + lm.ShoulderR.y) * 0.5f;
        }

        void SetArm(GlbRigger.Landmarks lm, int s, Vector3 shoulder, Vector3 tip, List<Vector2> centres, List<Vector3> V)
        {
            Vector3 d = (tip - shoulder).normalized;
            float handLen = 0.1f * H;
            Vector3 wrist = tip - d * handLen;
            Vector3 elbow = Vector3.Lerp(shoulder, wrist, 0.53f);
            if (centres != null)
            {
                // follow the tracked silhouette centreline for the elbow / wrist x
                Vector2 Near(float y) { var best = centres[0]; foreach (var c in centres) if (Mathf.Abs(c.y - y) < Mathf.Abs(best.y - y)) best = c; return best; }
                var e2 = Near(elbow.y); var w2 = Near(wrist.y);
                if (Mathf.Abs(e2.y - elbow.y) < 0.03f) elbow.x = e2.x;
                if (Mathf.Abs(w2.y - wrist.y) < 0.03f) wrist.x = w2.x;
            }
            Vector3 ZAt(Vector3 p) { var band = V.Where(v => (new Vector2(v.x, v.y) - new Vector2(p.x, p.y)).sqrMagnitude < 0.02f * 0.02f).ToList(); if (band.Count > 0) p.z = (band.Min(v => v.z) + band.Max(v => v.z)) * 0.5f; return p; }
            elbow = ZAt(elbow); wrist = ZAt(wrist);
            float ang = Vector3.Angle(tip - shoulder, Vector3.down);
            if (s == 0) { lm.ShoulderL = shoulder; lm.ElbowL = elbow; lm.WristL = wrist; lm.TipL = tip; lm.ArmAngleL = ang; }
            else { lm.ShoulderR = shoulder; lm.ElbowR = elbow; lm.WristR = wrist; lm.TipR = tip; lm.ArmAngleR = ang; }
            lm.Report += $" arm{s}: sh {shoulder.x:F2},{shoulder.y:F2} tip {tip.x:F2},{tip.y:F2} ang {ang:F0}";
        }

        /// <summary>Flood fill from the fingertip over the silhouette, blocked by the shoulder cut plane and (below the armpit) by the torso run.</summary>
        bool[] FloodArm(int s)
        {
            var lm = LM;
            Vector3 sh = s == 0 ? lm.ShoulderL : lm.ShoulderR, tip = s == 0 ? lm.TipL : lm.TipR;
            Vector2 sh2 = sh, d2 = ((Vector2)(tip - sh)).normalized;
            float cut = 0.0f;
            var mask = new bool[NX * NY];
            var q = new Queue<int>();
            int start = Row(tip.y) * NX + Col(tip.x);
            // find an occupied start near the tip
            for (int rad = 0; rad < 10 && !Occ[start]; rad++)
                for (int dy = -rad; dy <= rad && !Occ[start]; dy++)
                    for (int dx = -rad; dx <= rad; dx++)
                    {
                        int c = Col(tip.x) + dx, r = Row(tip.y) + dy;
                        if (O(c, r)) { start = r * NX + c; break; }
                    }
            if (!Occ[start]) return mask;
            int cx = Col(0f);
            float armpitY = sh.y - 0.06f * H / 1.75f;
            q.Enqueue(start); mask[start] = true;
            while (q.Count > 0)
            {
                int k = q.Dequeue();
                int r = k / NX, c = k % NX;
                for (int n = 0; n < 4; n++)
                {
                    int rr = r + (n == 0 ? 1 : n == 1 ? -1 : 0), cc = c + (n == 2 ? 1 : n == 3 ? -1 : 0);
                    if (!O(cc, rr)) continue;
                    int kk = rr * NX + cc;
                    if (mask[kk]) continue;
                    Vector2 p = new Vector2(X(cc), Y(rr));
                    if (Vector2.Dot(p - sh2, d2) < cut) continue;
                    if (!TPose && Y(rr) < armpitY)
                    {
                        int off = cc - cx;
                        if ((off <= 0 && -off <= RunL[rr]) || (off >= 0 && off <= RunR[rr])) continue;
                    }
                    mask[kk] = true; q.Enqueue(kk);
                }
            }
            return mask;
        }

        /// <summary>Signed distance (m) along the arm from the shoulder cut; arm vertices have it &gt; 0 and lie in the flood mask.</summary>
        public bool IsArm(Vector3 v, int s, out float along)
        {
            var lm = LM;
            Vector3 sh = s == 0 ? lm.ShoulderL : lm.ShoulderR, tip = s == 0 ? lm.TipL : lm.TipR;
            Vector3 d = (tip - sh).normalized;
            along = Vector3.Dot(v - sh, d);
            float len = (tip - sh).magnitude;
            if (along < -0.01f || along > len + 0.04f) return false;
            float S = H / 1.75f;
            Vector3 el = s == 0 ? lm.ElbowL : lm.ElbowR, wr = s == 0 ? lm.WristL : lm.WristR;
            float dist = Mathf.Min(CharMesher.SegDist(v, sh, el), Mathf.Min(CharMesher.SegDist(v, el, wr), CharMesher.SegDist(v, wr, tip)));
            var mask = s == 0 ? _armL : _armR;
            int k = Row(v.y) * NX + Col(v.x);
            if (mask[k] && dist < 0.17f * S) return true;
            // hands / forearms overlapping the coat in the front view: pure 3D proximity to the arm axis
            float r = Mathf.Lerp(0.085f, 0.06f, Mathf.Clamp01(along / len)) * S;
            return along > 0.12f * S && dist < r;
        }
    }
}
