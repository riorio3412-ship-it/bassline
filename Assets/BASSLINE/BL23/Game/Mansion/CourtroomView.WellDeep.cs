using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Two quiet presences in the well (optional layer; both are silhouettes, never lit, never close):
    /// the Leviathan, Yusti's fish at the wrong scale (38 m), which now and then crosses the zenith between the court and the
    /// Black Sun, visible only where it eclipses the corona or the haze; and the Trial Below, three small copies of this court
    /// hanging tilted in the abyss, each with its own candle specks, seen only when you look straight down past the parapet.
    /// </summary>
    public sealed partial class CourtroomView
    {
        Transform _fish, _fishTail; Material _mBelow, _mFish, _mFishRim;
        float _fishT0 = -1f, _fishNext = -1f, _fishDir = 1f, _fishU; int _fishPasses;
        const float FishH = 94f, FishSpeed = 2.4f, FishRun = 44f, FishOffset = 3f;

        /// <summary>The leviathan's chord: at h 94 (under the upper veil), perpendicular to the judge's axis, 3 m off it toward the lift.</summary>
        Vector3 FishLine(float x) => _c - _jd * FishOffset + _rt * x + Vector3.up * FishH;

        /// <summary>The leviathan drifts on its side, so from the floor it shows the one outline everyone knows: a deep body, a
        /// sail of a dorsal fin, pelvic and anal fins, a forked tail that sweeps. It is drawn as a true black that multiplies
        /// whatever lies behind it (the corona, the haze, the tracery) down to ~1.5 %, after every other layer of the zenith, so no
        /// haze can grey it. It swims just under the upper veil of haze (so the whole outline has light behind it, not only where
        /// it crosses the corona), and a faint cold rim, as if the eclipse caught its edge, keeps the outline whole over dark stone.</summary>
        void Leviathan()
        {
            var park = FishLine(-70f);
            _fish = Pivot("Leviathan", park);
            _mFish = new Material(MansionMats.Get(S.GlowAdd)) { name = "LeviathanShadow" };
            _mFish.SetTexture("_MainTex", Texture2D.whiteTexture);
            _mFish.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.Zero); _mFish.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcColor);
            _mFish.SetFloat("_Cull", 0f); _mFish.SetColor("_Color", new Color(0.985f, 0.985f, 0.985f, 1f));
            int q = _mIris != null ? _mIris.renderQueue : 3012;
            foreach (var m in new[] { _mHaze, _mPuffs, _mShafts, _mBand[0], _mBand[1], _mBand[2], _mBand[3], _mCorona, _mPupil }) if (m != null) q = Mathf.Max(q, m.renderQueue);
            _mFishRim = new Material(MansionMats.Get(S.GlowAdd)) { name = "LeviathanRim" };
            _mFishRim.SetTexture("_MainTex", Texture2D.whiteTexture); _mFishRim.SetFloat("_Cull", 0f); _mFishRim.SetColor("_Color", Color.white);
            _mFishRim.renderQueue = q + 1;   // the rim first, then the black over it: only the edge outside the outline survives
            _mFish.renderQueue = q + 2;
            var rimCol = new Color(0.78f, 0.84f, 1f);
            // side profile in local x (snout +) / y (dorsal +); the plane faces the floor
            var body = new VQ(); FishBody(body, 1f, Color.white, 1f);
            PutMesh(body.ToMesh("Leviathan"), "Leviathan", _fish, _mFish);
            var bodyRim = new VQ(); FishBody(bodyRim, 0.12f, rimCol, 1.045f);
            PutMesh(bodyRim.ToMesh("LeviathanRim"), "LeviathanRim", _fish, _mFishRim);
            // the forked tail on its own pivot at the peduncle
            _fishTail = new GameObject("LeviathanTail").transform; _fishTail.SetParent(_fish, false); _fishTail.localPosition = new Vector3(-12.6f, 0f, 0f); _fishTail.localRotation = Quaternion.identity;
            var tail = new VQ(); FishTail(tail, 1f, Color.white, 1f);
            PutMesh(tail.ToMesh("LeviathanTail"), "LeviathanTail", _fishTail, _mFish);
            var tailRim = new VQ(); FishTail(tailRim, 0.12f, rimCol, 1.06f);
            PutMesh(tailRim.ToMesh("LeviathanTailRim"), "LeviathanTailRim", _fishTail, _mFishRim);
            _fish.rotation = FishRot(1f);
        }

        /// <summary>The body's outline (stations snout to peduncle, dorsal and ventral lines) and its fins, grown about the body's
        /// centre by 'grow' (the rim copy).</summary>
        static void FishBody(VQ q, float val, Color col, float grow)
        {
            var c0 = new Vector2(2f, 0.3f);
            Vector3 G(float x, float y) { var p = c0 + (new Vector2(x, y) - c0) * grow; return new Vector3(p.x, p.y, 0f); }
            float[] xs = { 19.5f, 18.2f, 15.5f, 12f, 8f, 4f, 0f, -4f, -8f, -11f, -13.2f };
            float[] up = { 0.4f, 2.7f, 4.5f, 5.8f, 6.7f, 7.1f, 6.9f, 6.0f, 4.4f, 2.9f, 2.1f };
            float[] dn = { -0.7f, -2.6f, -4.2f, -5.3f, -5.9f, -6.0f, -5.5f, -4.5f, -3.3f, -2.3f, -1.9f };
            for (int i = 0; i + 1 < xs.Length; i++)
                q.Quad(G(xs[i], dn[i]), G(xs[i], up[i]), G(xs[i + 1], up[i + 1]), G(xs[i + 1], dn[i + 1]), col, col, col, col, val, val, val, val, Vector3.forward);
            void Fin(params Vector2[] p) { for (int i = 1; i + 1 < p.Length; i++) q.Tri(G(p[0].x, p[0].y), G(p[i].x, p[i].y), G(p[i + 1].x, p[i + 1].y), col, col, col, val, val, val, Vector3.forward); }
            Fin(new Vector2(7f, 6.2f), new Vector2(3.5f, 11.6f), new Vector2(0.5f, 12.4f), new Vector2(-3.5f, 10.2f), new Vector2(-7.5f, 7.6f), new Vector2(-6f, 5.2f));   // dorsal sail
            Fin(new Vector2(-2.5f, -4.8f), new Vector2(-6.5f, -8.8f), new Vector2(-9.5f, -8.2f), new Vector2(-9.2f, -3.6f));                                         // anal fin
            Fin(new Vector2(6.5f, -5.4f), new Vector2(3.2f, -10.4f), new Vector2(1.6f, -9.6f), new Vector2(3.2f, -5.6f));                                           // pelvic fin
            Fin(new Vector2(11.5f, -2.8f), new Vector2(6.8f, -8.2f), new Vector2(5.6f, -7.2f), new Vector2(8.6f, -2.2f));                                           // pectoral, hanging below the flank
        }

        /// <summary>The forked tail, in the tail pivot's frame (origin at the peduncle), grown about its middle by 'grow'.</summary>
        static void FishTail(VQ q, float val, Color col, float grow)
        {
            var c0 = new Vector2(-5.5f, 0f);
            var tp = new[] { new Vector2(0f, 0f), new Vector2(-0.6f, 2.4f), new Vector2(-6.5f, 7.8f), new Vector2(-11.2f, 10.4f), new Vector2(-8.4f, 4.2f), new Vector2(-6.6f, 0f),
                             new Vector2(-8.4f, -4.2f), new Vector2(-11.2f, -10.4f), new Vector2(-6.5f, -7.8f), new Vector2(-0.6f, -2.4f) };
            for (int i = 0; i < tp.Length; i++) tp[i] = c0 + (tp[i] - c0) * grow;
            for (int i = 1; i + 1 < tp.Length; i++) q.Tri(tp[0], tp[i], tp[i + 1], col, col, col, val, val, val, Vector3.forward);
            q.Tri(tp[0], tp[tp.Length - 1], tp[1], col, col, col, val, val, val, Vector3.forward);
        }

        /// <summary>On its side: local x along the travel, the profile plane facing straight down at the court.</summary>
        Quaternion FishRot(float dir) { var travel = _rt * dir; return Quaternion.LookRotation(Vector3.up, Vector3.Cross(Vector3.up, travel)); }

        void UpdateFish(float t, float dt)
        {
            if (_fish == null) return;
            if (_fishT0 < 0f)
            {
                if (_fishNext > 0f && t >= _fishNext && (_depth >= 0.35f || _fishForced)) { _fishT0 = t; _fishU = 0f; _fishDir = _fishPasses % 2 == 0 ? 1f : -1f; _fishPasses++; _fishForced = false; }
                else return;
            }
            _fishU += dt;   // it does not stop for the verdict: it simply goes on, out of the frame
            float x = -FishRun * _fishDir + _fishDir * FishSpeed * _fishU;
            if (_fishU * FishSpeed > FishRun * 2f)
            {
                _fishT0 = -1f; _fishNext = t + 80f + Hash(t * 0.73f) * 60f;
                _fish.position = FishLine(-70f); return;
            }
            _fish.position = FishLine(x);
            _fish.rotation = FishRot(_fishDir);
            if (_fishTail != null) _fishTail.localRotation = Quaternion.Euler(0f, 0f, 9f * Mathf.Sin(_fishU * Mathf.PI * 2f / 6.5f));   // the sweep, in the plane the floor sees
        }
        bool _fishForced;

        /// <summary>Re-time the leviathan so its head crosses the well's axis s seconds from now (at least 16 s).</summary>
        void FishCross(float s)
        {
            if (_fish == null) return;
            s = Mathf.Max(16f, s);
            float headAt = (FishRun - 19f) / FishSpeed;     // seconds from the start of a pass to the head reaching the axis
            _fishT0 = -1f; _fishNext = Time.unscaledTime + s - headAt; _fishForced = true;
            _fish.position = FishLine(-70f);
        }

        // ---- the trial below: forced-perspective copies of this court in the abyss -----------------------------------
        void TrialBelow()
        {
            var mb = new MeshBuilder(); var hb = new MeshBuilder();
            var copies = new (float th, float r, float h, float s)[] { (130f, 24f, -26f, 0.5f), (-100f, 27f, -38f, 0.34f), (70f, 30f, -50f, 0.22f) };
            var eye = _c + Vector3.up * 4f; float rho = FogStripped ? AerialDensity : 0.026f;   // the same air the bake (or the real fog) applies
            float T(float d) => Mathf.Exp(-(rho * d) * (rho * d));
            foreach (var (th, r, h, s) in copies)
            {
                var o = P(th, r, h); float d = Vector3.Distance(eye, o);
                float k = Mathf.Clamp(T(d / s) / Mathf.Max(T(d), 1e-4f), 0.02f, 1f);
                var toC = _c - o; toC.y = 0; var axis = Vector3.Cross(Vector3.up, toC.normalized);
                var rot = Quaternion.AngleAxis(-10f, axis) * Quaternion.LookRotation(Dir(th + 180f));
                mb.Push(Matrix4x4.TRS(o, rot, Vector3.one * s));
                Lum(mb, 0.03f * k, StoneHue); mb.Disc(Vector3.zero, 10f, 36, true);
                Lum(mb, 0.02f * k, new Color(1f, 0.8f, 0.6f)); mb.Lathe(new[] { new Vector2(9.85f, 2.6f), new Vector2(9.85f, 0f) }, 36);
                Lum(mb, 0.025f * k, new Color(1f, 0.85f, 0.7f));
                for (int i = 0; i < 18; i++) { float a = i / 18f * Mathf.PI * 2f; mb.Box(new Vector3(Mathf.Sin(a) * 6.2f, 0.55f, Mathf.Cos(a) * 6.2f), new Vector3(0.9f, 1.1f, 0.9f)); }
                Lum(mb, 0.03f * k, new Color(1f, 0.8f, 0.6f)); mb.Box(new Vector3(0f, 2.2f, 8.55f), new Vector3(2.6f, 4.4f, 1.2f));
                Lum(mb, 0.012f * k, StoneHue); mb.Lathe(new[] { new Vector2(0.6f, -9f), new Vector2(6f, -4f), new Vector2(10f, 0f) }, 18);
                mb.Pop();
                // candle specks: the only thing about them you would notice
                float hv = 0.3f * T(d / s);
                hb.Set(S.Halo, new Color(1f, 0.72f, 0.45f), new Vector4(Mathf.Max(0.045f, hv), 0.1f, 0f, 9f));
                var m = Matrix4x4.TRS(o, rot, Vector3.one * s);
                for (int i = 0; i < 18; i += 2) { float a = i / 18f * Mathf.PI * 2f; MansionView.HaloQuad(hb, m.MultiplyPoint3x4(new Vector3(Mathf.Sin(a) * 6.2f, 1.3f, Mathf.Cos(a) * 6.2f)), 0.9f * s); }
                MansionView.HaloQuad(hb, m.MultiplyPoint3x4(new Vector3(0f, 4.8f, 8.55f)), 1.4f * s);
            }
            Put(mb, "CourtTrialBelow", _wellRoot, _mVoid, true, Vector3.zero);
            _mBelow = new Material(MansionMats.Get(S.Halo)) { name = "CourtTrialBelowCandles" };
            Put(hb, "CourtTrialBelowCandles", _wellRoot, _mBelow, false, Vector3.zero);
        }
    }
}
