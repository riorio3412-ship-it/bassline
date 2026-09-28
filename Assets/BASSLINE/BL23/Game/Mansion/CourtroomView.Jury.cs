using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Two things that make the well a court that is attending to you.
    /// · The Stone Jury: on stepped corbels high on the colossal piers (h 84, just under the second arcade) stand hooded figures
    ///   of stone, each some thirteen metres tall, leaning out over the well around the Black Sun. When a trial begins their heads
    ///   are thrown back to the Sun (from the floor they are faceless). Once the well has woken (the first wave of eyes, or the
    ///   verdict) they are found bowed over the court instead, a black hollow under each hood and two points of light in it.
    ///   Nobody ever sees one move: a juror changes only while it is out of the view camera's frustum. One corbel holds only the
    ///   broken stump of a juror that is gone; one juror has lost its head. Unlit stone, darker than the air, rimmed by the Sun.
    /// · The wake rings: when the well wakes, eyes open along every gallery's balustrade in uneven crowds, ring after ring from
    ///   the bottom of the well up toward the Sun (eight steps over four seconds), and close again from the top down.
    /// Both are separate passes over the finished well (their own random streams: nothing else in the well changes).
    /// </summary>
    public sealed partial class CourtroomView
    {
        // ================================================================== the stone jury
        const int JurorCount = 12;
        /// <summary>Where the jurors stand: the corbel top (height above the court floor) and the figure's centre (radius).</summary>
        const float JuryH = 84.45f, JuryR = 14.4f;
        readonly MeshFilter[] _jurorMf = new MeshFilter[JurorCount];
        readonly MeshRenderer[] _jurorR = new MeshRenderer[JurorCount];
        readonly Mesh[] _jurorUp = new Mesh[JurorCount], _jurorCourt = new Mesh[JurorCount];
        readonly bool[] _jurorTurned = new bool[JurorCount];
        readonly Plane[] _juryPlanes = new Plane[6];
        int _jurorsTurned, _jurorsBuilt; bool _juryArmed; float _juryTimer;
        static readonly Color JurorStone = new Color(0.84f, 0.88f, 1f), JurorEye = new Color(1f, 0.76f, 0.5f);

        /// <summary>How many stone jurors stand in the well, and how many have turned to the court (probe / diagnostics).</summary>
        public int JurorsBuilt => _jurorsBuilt;
        public int JurorsTurned => _jurorsTurned;

        /// <summary>A framing of the jury: from the floor on the lift side of the clock, up past the throne-side jurors to the Sun.</summary>
        public bool JuryShot(out Vector3 pos, out Vector3 look, out float fov)
        {
            pos = _c - _jd * 4f + Vector3.up * 1.5f; look = _c + _jd * 11f + Vector3.up * 90f; fov = 40f;
            return _jurorsBuilt > 0;
        }

        void Jury()
        {
            var rnd = new System.Random((int)(_seed % 1000003UL) * 13 + 4441);
            float R() => (float)rnd.NextDouble();
            // the empty seat and the headless juror are on the lift side: the throne's side of the well keeps its jury whole
            int empty = new[] { 4, 7, 8 }[(int)(_seed % 3UL)];
            int headless = new[] { 3, 5, 6, 9 }[(int)((_seed / 3UL) % 4UL)];
            for (int i = 0; i < JurorCount; i++)
            {
                float th = 15f + 30f * i;
                var frame = Matrix4x4.TRS(P(th, PierR, 0f), Quaternion.LookRotation(-Dir(th)), Vector3.one);   // +z toward the axis
                Corbel(_stoneFar[Sec(th)], frame, 0.92f + R() * 0.16f);
                if (i == empty) { EmptySeat(_stoneFar[Sec(th)], frame, rnd); continue; }
                float s = Mathf.Lerp(0.93f, 1.06f, R());
                var root = frame * Matrix4x4.TRS(new Vector3(0f, JuryH, PierR - JuryR), Quaternion.Euler(0f, (R() - 0.5f) * 10f, 0f), Vector3.one * s);
                float folds = R(), peak = Mathf.Lerp(0.6f, 0.9f, R()), twist = (R() - 0.5f) * 10f;
                var up = new MeshBuilder(); var ct = new MeshBuilder();
                // at rest: heads thrown back to the Sun; found later: bowed over the court, the face turned down to the floor
                Figure(up, root, Mathf.Lerp(3f, 7f, R()), -Mathf.Lerp(32f, 44f, R()), twist, i == headless, false, folds, peak);
                Figure(ct, root, Mathf.Lerp(18f, 26f, R()), Mathf.Lerp(46f, 55f, R()), -twist * 0.5f, i == headless, true, folds, peak);
                var mu = up.ToMesh("CourtJuror" + i, out _, false); JurorBake(mu);
                var mc = ct.ToMesh("CourtJurorTurned" + i, out _, false); JurorBake(mc);
                var mr = PutMesh(mu, "CourtJuror" + i, _wellRoot, _mVoid);
                if (mr == null) continue;
                _jurorR[i] = mr; _jurorMf[i] = mr.GetComponent<MeshFilter>(); _jurorUp[i] = mu; _jurorCourt[i] = mc; _jurorsBuilt++;
            }
        }

        /// <summary>A stepped corbel carried out from the pier's second-order capital toward the axis, the juror's standing place.</summary>
        void Corbel(MeshBuilder mb, Matrix4x4 frame, float noise)
        {
            mb.Push(frame);
            Stone(mb, AStone * 1.15f, noise, StoneHue);
            mb.Box(new Vector3(0f, JuryH - 0.25f, 3.03f), new Vector3(3.6f, 0.5f, 3.56f));          // the slab it stands on (z 1.25 .. 4.8)
            Stone(mb, AStone, noise, StoneHue);
            mb.Box(new Vector3(0f, JuryH - 0.8f, 2.8f), new Vector3(3.2f, 0.6f, 3.1f));
            mb.Box(new Vector3(0f, JuryH - 1.45f, 2.43f), new Vector3(2.6f, 0.7f, 2.36f));
            Stone(mb, AStone * 0.8f, noise, StoneHue);
            mb.Box(new Vector3(0f, JuryH - 2.25f, 2.0f), new Vector3(1.9f, 0.9f, 1.5f));
            mb.Pop();
        }

        /// <summary>The seat whose juror is gone: the robe's stump, broken off, and shards of it still standing.</summary>
        void EmptySeat(MeshBuilder mb, Matrix4x4 frame, System.Random rnd)
        {
            float R() => (float)rnd.NextDouble();
            mb.Push(frame * Matrix4x4.Translate(new Vector3(0f, JuryH, PierR - JuryR)));
            Stone(mb, AStone * 0.9f, 1f, StoneHue);
            mb.Lathe(new[] { new Vector2(1.62f, 0f), new Vector2(1.6f, 0.5f), new Vector2(1.5f, 1.1f), new Vector2(1.44f, 1.35f) }, 14, false, true);
            for (int k = 0; k < 5; k++)
            {
                float a = (k / 5f + R() * 0.1f) * Mathf.PI * 2f, hgt = 1.2f + R() * 1.4f;
                mb.Push(new Vector3(Mathf.Cos(a) * 1.15f, 1.3f + hgt * 0.5f - 0.3f, Mathf.Sin(a) * 1.15f), Quaternion.Euler(R() * 18f, R() * 360f, R() * 22f), Vector3.one);
                mb.Box(Vector3.zero, new Vector3(0.62f, hgt, 0.48f)); mb.Pop();
            }
            mb.Pop();
        }

        /// <summary>One hooded juror in its own frame (base at the origin, +z toward the axis). lean pitches the body from the hips
        /// toward the court, head pitches the hood at the neck (negative: thrown back). The hands are hidden in the sleeves.</summary>
        static void Figure(MeshBuilder mb, Matrix4x4 root, float lean, float head, float twist, bool headless, bool eyes, float folds, float peak)
        {
            var stone = new Vector4(0.006f, 0f, 1f, 9f);
            mb.Push(root);
            mb.Set(S.Glow, JurorStone, stone);
            // the robe falling to the corbel, a heavy hem, deep vertical folds
            mb.Lathe(new[] { new Vector2(1.62f, 0f), new Vector2(1.66f, 0.3f), new Vector2(1.58f, 1.5f), new Vector2(1.46f, 3.0f), new Vector2(1.36f, 4.4f), new Vector2(1.3f, 5.4f) }, 16);
            mb.Push(new Vector3(0f, 0.18f, 0f), 0f); mb.Torus(Vector3.zero, 1.63f, 0.13f, 18, 3); mb.Pop();
            for (int k = 0; k < 7; k++)
            {
                float a = (k / 7f + folds) * Mathf.PI * 2f; var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float r = 0.12f + 0.06f * Mathf.Abs(Mathf.Sin(k * 2.3f + folds * 9f));
                mb.Tube(new List<Vector3> { d * 1.6f + Vector3.up * 0.05f, d * 1.49f + Vector3.up * 2.4f, d * 1.33f + Vector3.up * 5.0f }, r, 5);
            }
            // the body above the hips, leaning out over the well
            mb.Push(new Vector3(0f, 5.3f, 0f), Quaternion.Euler(lean, 0f, 0f), Vector3.one);
            mb.Lathe(new[] { new Vector2(1.32f, 0f), new Vector2(1.3f, 1.2f), new Vector2(1.4f, 2.5f), new Vector2(1.52f, 3.35f), new Vector2(1.38f, 3.95f), new Vector2(0.98f, 4.35f), new Vector2(0.62f, 4.6f), new Vector2(0.001f, 4.72f) }, 16);
            mb.Ellipsoid(new Vector3(0f, 3.6f, -0.05f), new Vector3(1.78f, 0.78f, 1.22f), 14, 8);                 // the mantle over the shoulders
            foreach (float sx in new[] { -1f, 1f })
            {
                mb.Tube(new List<Vector3> { new Vector3(sx * 1.42f, 3.45f, 0.05f), new Vector3(sx * 1.5f, 2.55f, 0.35f), new Vector3(sx * 1.25f, 1.7f, 0.8f), new Vector3(sx * 0.62f, 1.3f, 1.18f) }, 0.42f, 8, true);
                mb.Ellipsoid(new Vector3(sx * 0.5f, 1.28f, 1.22f), new Vector3(0.52f, 0.44f, 0.42f), 10, 6);     // the cuffs meeting, hands hidden
            }
            if (headless)
            {
                // the head broken away: a neck stump and a jagged break
                mb.Lathe(new[] { new Vector2(0.66f, 4.5f), new Vector2(0.6f, 4.9f), new Vector2(0.45f, 5.02f), new Vector2(0.001f, 5.06f) }, 10);
                mb.Push(new Vector3(0.18f, 5.05f, -0.1f), Quaternion.Euler(24f, 30f, 14f), Vector3.one); mb.Box(Vector3.zero, new Vector3(0.5f, 0.34f, 0.4f)); mb.Pop();
                mb.Push(new Vector3(-0.22f, 4.98f, 0.16f), Quaternion.Euler(-18f, 70f, -20f), Vector3.one); mb.Box(Vector3.zero, new Vector3(0.36f, 0.26f, 0.3f)); mb.Pop();
            }
            else
            {
                mb.Push(new Vector3(0f, 4.5f, 0.12f), Quaternion.Euler(head, twist, 0f), Vector3.one);
                mb.Ellipsoid(new Vector3(0f, 0.95f, 0f), new Vector3(1.0f, 1.25f, 1.12f), 16, 10);                  // the hood
                mb.Push(new Vector3(0f, 1.5f, -0.3f), Quaternion.Euler(-30f, 0f, 0f), Vector3.one);                  // its soft point, falling back (a cowl, never a cone)
                mb.Lathe(new[] { new Vector2(0.74f, 0f), new Vector2(0.5f, 0.55f * peak), new Vector2(0.24f, 0.95f * peak), new Vector2(0.001f, 1.25f * peak) }, 12);
                mb.Pop();
                mb.Push(new Vector3(0f, 0.88f, 1.0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(1f, 1f, 1.28f)); mb.Torus(Vector3.zero, 0.64f, 0.14f, 20, 5); mb.Pop();   // the cowl's lip
                // no face: a black hollow under the hood
                mb.Set(S.Glow, Color.white, new Vector4(0.0005f, 2f, 1f, 9f));
                mb.Ellipsoid(new Vector3(0f, 0.86f, 0.86f), new Vector3(0.56f, 0.74f, 0.34f), 12, 8);
                if (eyes)
                {
                    // two narrow points of light, slanted a little at their outer ends
                    mb.Set(S.Glow, JurorEye, new Vector4(4.5f, 1f, 1f, 9f));
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        float x = sx * 0.26f, y = 0.94f, z = 1.2f;
                        var l = new Vector3(x - 0.2f, y + (sx < 0 ? 0.03f : 0f), z); var rr = new Vector3(x + 0.2f, y + (sx > 0 ? 0.03f : 0f), z);
                        mb.QuadAuto(l, new Vector3(x, y + 0.075f, z + 0.01f), rr, new Vector3(x, y - 0.05f, z + 0.01f), Vector3.forward);
                    }
                }
                mb.Pop();
            }
            mb.Pop();
            mb.Pop();
        }

        /// <summary>Bake a juror: stone darker than the air around it with a cold rim where the Sun catches the outline, the face a
        /// black hollow, the eyes points of light that carry through the haze. (uv2.y: 0 stone, 1 light, 2 hollow.)</summary>
        void JurorBake(Mesh m)
        {
            var vs = m.vertices; var ns = m.normals; var cs = m.colors32; var xs = new List<Vector4>(vs.Length); m.GetUVs(2, xs);
            if (xs.Count != vs.Length || cs.Length != vs.Length || ns.Length != vs.Length) return;
            var zen = Zenith; var eye = _c + Vector3.up * 3f; var court = _c + Vector3.up * 2f; var air = AerialAir;
            bool aerial = FogStripped && AerialDensity > 0f;
            for (int i = 0; i < vs.Length; i++)
            {
                var p = vs[i]; var n = ns[i]; float kind = xs[i].y; Color hue = cs[i]; float v;
                if (kind > 0.5f && kind < 1.5f) v = xs[i].x;
                else if (kind >= 1.5f) v = 0.0005f;
                else
                {
                    float zl = Mathf.Max(0f, Vector3.Dot(n, (zen - p).normalized));
                    float rim = Mathf.Pow(1f - Mathf.Abs(Vector3.Dot(n, (court - p).normalized)), 3f);
                    v = xs[i].x * (0.8f + 0.4f * H3(p)) + 0.09f * zl * zl + 0.22f * rim * (0.35f + 0.65f * zl);
                    hue = Color.Lerp(hue, new Color(0.75f, 0.82f, 1f), Mathf.Clamp01(rim));
                }
                float mx = Mathf.Max(hue.r, Mathf.Max(hue.g, hue.b)); if (mx > 1e-4f) { hue.r /= mx; hue.g /= mx; hue.b /= mx; }
                if (aerial)
                {
                    // the Sun's own light and the eyes carry through the air; the stone reads a little darker than the haze behind it
                    float d = Vector3.Distance(p, eye), T = Mathf.Exp(-(AerialDensity * d) * (AerialDensity * d));
                    float Tl = Mathf.Pow(T, kind > 0.5f && kind < 1.5f ? 0.4f : 0.45f);
                    var rgb = new Color(hue.r * v * Tl + air.r * (1f - Tl), hue.g * v * Tl + air.g * (1f - Tl), hue.b * v * Tl + air.b * (1f - Tl));
                    v = Mathf.Max(0.0005f, Mathf.Max(rgb.r, Mathf.Max(rgb.g, rgb.b)));
                    hue = new Color(rgb.r / v, rgb.g / v, rgb.b / v);
                }
                hue.a = 1f; cs[i] = hue; xs[i] = new Vector4(v, 0f, 0f, 9f);
            }
            m.colors32 = cs; m.SetUVs(2, xs);
        }

        /// <summary>Per frame (from UpdateWell): once the well has woken, a juror still gazing at the Sun turns to the court the
        /// first time the view camera is not looking at it.</summary>
        void UpdateJury(Camera cam, float dt)
        {
            if (_jurorsBuilt == 0 || cam == null) return;
            if (!_juryArmed) { if (_waveT0 > 0f || _verdictT0 > 0f || _noticeArmed) _juryArmed = true; else return; }
            if (_jurorsTurned >= _jurorsBuilt) return;
            _juryTimer -= dt; if (_juryTimer > 0f) return; _juryTimer = 0.25f;
            GeometryUtility.CalculateFrustumPlanes(cam, _juryPlanes);
            for (int i = 0; i < JurorCount; i++)
            {
                if (_jurorTurned[i] || _jurorR[i] == null || _jurorCourt[i] == null) continue;
                if (GeometryUtility.TestPlanesAABB(_juryPlanes, _jurorR[i].bounds)) continue;
                _jurorMf[i].sharedMesh = _jurorCourt[i]; _jurorTurned[i] = true; _jurorsTurned++;
            }
        }

        void ResetJury()
        {
            _juryArmed = false; _jurorsTurned = 0; _juryTimer = 0f;
            for (int i = 0; i < JurorCount; i++)
            {
                if (_jurorTurned[i] && _jurorMf[i] != null && _jurorUp[i] != null) _jurorMf[i].sharedMesh = _jurorUp[i];
                _jurorTurned[i] = false;
            }
        }

        // ================================================================== the wake rings
        const int WakeBands = 8;
        /// <summary>Band edges (height above the court floor): the rings a look up the well sees, bottom to top.</summary>
        static readonly float[] WakeEdge = { -30f, 22f, 36f, 48f, 58f, 67f, 76f, 85f, 98f };
        readonly MeshRenderer[] _wakeR = new MeshRenderer[WakeBands];
        readonly float[] _wakeShown = new float[WakeBands];
        Material _mWake; MaterialPropertyBlock _wakeMpb; int _wakeEyes;

        static int WakeBand(float h) { for (int b = 0; b < WakeBands; b++) if (h >= WakeEdge[b] && h < WakeEdge[b + 1]) return b; return -1; }

        void WakeRings()
        {
            var rnd = new System.Random((int)(_seed % 1000003UL) * 29 + 1717);
            float R() => (float)rnd.NextDouble();
            var mbs = new MeshBuilder[WakeBands]; for (int b = 0; b < WakeBands; b++) mbs[b] = new MeshBuilder();
            foreach (var (th, k) in _rails)
            {
                if (k < -3) continue;
                float f = TierF(k), h = TierH(k);
                // a slow noise round each ring decides where they crowd along the rail and where it is empty
                float crowd = Mathf.Clamp01((Mathf.PerlinNoise(th * 0.05f + k * 5.3f, k * 1.7f + 0.37f) - 0.3f) / 0.55f);
                int n = Mathf.Min(5, Mathf.FloorToInt(crowd * crowd * 4.2f + R()));
                for (int j = 0; j < n; j++)
                {
                    float off = -3.2f + (j + 0.2f + 0.6f * R()) * 6.4f / n;
                    var at = P(th + off, 36.4f, h + 1.12f * f + 0.1f + R() * 0.12f * f);
                    int b = WakeBand(at.y - _c.y); if (b < 0) continue;
                    var toC = _c - at; toC.y = 0f; toC.Normalize(); var side = Vector3.Cross(Vector3.up, toC);
                    WakePair(mbs[b], at + toC * 0.08f, side, rnd);
                }
            }
            _mWake = new Material(MansionMats.Get(S.Halo)) { name = "CourtWakeEyes" }; _mWake.SetFloat(PInt, 1f);
            _wakeMpb = new MaterialPropertyBlock(); _wakeMpb.SetFloat(PInt, 0f);
            for (int b = 0; b < WakeBands; b++)
            {
                _wakeR[b] = Put(mbs[b], "CourtWakeRing" + b, _wellRoot, _mWake, false, Vector3.zero);
                if (_wakeR[b] != null) _wakeR[b].SetPropertyBlock(_wakeMpb);
                _wakeShown[b] = 0f;
            }
        }

        /// <summary>A pair of eyes just over a balustrade's coping: sized so each is a few pixels at any depth of the well, set
        /// wide enough apart to read as a pair, never as a lamp.</summary>
        void WakePair(MeshBuilder mb, Vector3 at, Vector3 side, System.Random rnd)
        {
            float R() => (float)rnd.NextDouble();
            float d = Vector3.Distance(at, _c + Vector3.up * 2f);
            float s = Mathf.Max(0.15f, 0.0072f * d);
            float val = (0.45f + 0.55f * Mathf.Exp(-(d / 95f) * (d / 95f))) * (0.7f + 0.6f * R());
            var col = Color.Lerp(new Color(1f, 0.78f, 0.5f), new Color(0.97f, 0.9f, 0.8f), R() * 0.5f);
            mb.Set(S.Halo, col, new Vector4(val, 0.04f, 0f, 9f));
            float sp = 0.0042f * d;
            MansionView.HaloQuad(mb, at - side * sp, s); MansionView.HaloQuad(mb, at + side * sp, s);
            _wakeEyes++;
        }

        float WakeLevel(int b, float t)
        {
            if (_waveT0 < 0f) return 0f;
            float lvl = S01((t - _waveT0 - 0.5f * b) / 1.4f);
            if (!_waveHold) lvl *= 1f - S01((t - (_waveT0 + 11.8f + 0.4f * (WakeBands - 1 - b))) / 1.4f);
            float dd = t - _wBlinkT0 - 0.12f * b;
            if (dd >= 0f && dd < 0.25f) lvl *= 1f - Mathf.Sin(dd / 0.25f * Mathf.PI);
            return lvl;
        }

        /// <summary>Per frame (from UpdateWell): the rings follow the well's wave, climbing, and its blinks.</summary>
        void UpdateWake(float t)
        {
            if (_wakeMpb == null) return;
            for (int b = 0; b < WakeBands; b++)
            {
                var r = _wakeR[b]; if (r == null) continue;
                float lvl = WakeLevel(b, t) * _fogK[Mathf.Min(_fogK.Length - 1, b / 2)];
                if (Mathf.Abs(lvl - _wakeShown[b]) < 1e-4f) continue;
                _wakeShown[b] = lvl; _wakeMpb.SetFloat(PInt, lvl); r.SetPropertyBlock(_wakeMpb);
            }
        }

        /// <summary>Probe / diagnostics: the jury and the wake rings in one line.</summary>
        public string JuryState() => $"jurors {_jurorsTurned}/{_jurorsBuilt} armed {_juryArmed} wakeEyes {_wakeEyes} wake {_wakeShown[0]:0.00}/{_wakeShown[3]:0.00}/{_wakeShown[7]:0.00}";
    }
}
