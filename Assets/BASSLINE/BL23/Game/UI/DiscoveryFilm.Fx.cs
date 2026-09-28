using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Audio;
using BL23.Sim;
using UnityEngine;
using MansionMats = BL23.Game.Mansion.MansionMats;

namespace BL23.Game
{
    /// <summary>
    /// The film's small physical things: dust hanging in the candlelight (and almost stopping during the close-ups), the
    /// smoke of the guttering candles, one drop of blood hanging from an edge that finally falls, and a moth that leaves a
    /// candle and settles on the dead hand. All presentation only, all destroyed by Restore.
    /// </summary>
    public sealed partial class DiscoveryFilm
    {
        // blood here follows the gore palette rule: g ≤ 0.35 r, b ≤ 0.25 r (never pink)
        static readonly Color DropCol = new Color(0.30f, 0.012f, 0.02f, 1f);
        static readonly Color ThickCol = new Color(0.14f, 0.005f, 0.01f, 1f);

        readonly List<GameObject> _fx = new List<GameObject>();
        ParticleSystem _motes; float _motesFrom = 1f, _motesTo = 1f, _motesT0 = -1f; float _motesFadeT0 = -1f, _motesFadeDur = 1f;
        ParticleSystem.Particle[] _pbuf;

        static Material _dropMat, _mothMat;

        // ------------------------------------------------------------------ dust motes
        void MotesStart()
        {
            if (_motes != null) return;
            var go = new GameObject("FilmMotes"); _fx.Add(go);
            go.transform.position = new Vector3(_focus.x, _floorY + 1.2f, _focus.z);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.startLifetime = 40f; main.startSpeed = 0.012f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.0035f, 0.011f); main.maxParticles = 220; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true; main.startColor = new Color(1f, 0.86f, 0.64f, 0.42f); main.gravityModifier = 0f;
            var em = ps.emission; em.enabled = true; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)220) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(4f, 2.4f, 4f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.025f; noise.frequency = 0.3f; noise.scrollSpeed = 0.04f; noise.quality = ParticleSystemNoiseQuality.Low;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.01f), new GradientAlphaKey(1f, 0.95f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MansionMats.Particle("FilmMotes", MansionMats.Proc("SoftDot"), true);
            r.renderMode = ParticleSystemRenderMode.Billboard; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
            _motes = ps;
        }
        void MotesSpeed(float to) { if (_motes == null) return; _motesFrom = _motes.main.simulationSpeed; _motesTo = to; _motesT0 = Time.unscaledTime; }
        void MotesFade(float secs) { if (_motes == null) return; _motesFadeT0 = Time.unscaledTime; _motesFadeDur = Mathf.Max(0.1f, secs); }

        void FxTick()
        {
            float now = Time.unscaledTime;
            GazeTick(now);
            if (_motes != null)
            {
                if (_motesT0 >= 0f)
                {
                    float u = Mathf.Clamp01((now - _motesT0) / 0.35f); var m = _motes.main; m.simulationSpeed = Mathf.Lerp(_motesFrom, _motesTo, u * u * (3f - 2f * u));
                    if (u >= 1f) _motesT0 = -1f;
                }
                if (_motesFadeT0 >= 0f)
                {
                    float k = 1f - Mathf.Clamp01((now - _motesFadeT0) / _motesFadeDur);
                    int n = _motes.particleCount; if (_pbuf == null || _pbuf.Length < n) _pbuf = new ParticleSystem.Particle[Mathf.Max(n, 220)];
                    n = _motes.GetParticles(_pbuf);
                    for (int i = 0; i < n; i++) { var c = _pbuf[i].startColor; c.a = (byte)(107 * k); _pbuf[i].startColor = c; }
                    _motes.SetParticles(_pbuf, n);
                    if (k <= 0f) { _motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); _motesFadeT0 = -1f; }
                }
            }
        }

        // ------------------------------------------------------------------ candle smoke
        void CandleSmoke()
        {
            int n = 0;
            foreach (var (pos, delay, _) in _candles.OrderBy(c => c.delay))
            {
                if (n++ >= 6) break;
                var go = new GameObject("FilmSmoke"); _fx.Add(go); go.transform.position = pos + Vector3.up * 0.04f;
                go.transform.rotation = Quaternion.LookRotation(Vector3.up);
                var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.loop = false; main.playOnAwake = false; main.useUnscaledTime = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startDelay = delay + 0.12f; main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f); main.startColor = new Color(0.52f, 0.5f, 0.48f, 0.26f); main.maxParticles = 16; main.gravityModifier = -0.015f;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)10, (short)14, 1, 0.01f) });
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 9f; sh.radius = 0.004f;
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 4.5f));
                var col = ps.colorOverLifetime; col.enabled = true;
                var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                var nz = ps.noise; nz.enabled = true; nz.strength = 0.05f; nz.frequency = 1.2f; nz.quality = ParticleSystemNoiseQuality.Low;
                var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = MansionMats.Particle("FilmSmoke", MansionMats.Proc("SoftDot"), false);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                ps.Play();
            }
        }

        // ------------------------------------------------------------------ the hanging drop
        Transform _drop; Vector3 _dropPos; float _dropLandY; bool _dropFalling; float _dropV, _dropT0 = -1f;
        Vector3? _poolCentre, _dropAt;

        /// <summary>Where one drop hangs: a bloody weapon's tip lying off the floor, else the edge of a piece of furniture
        /// above the pool (0.25 m up or more, within 0.8 m), else nowhere.</summary>
        void FindDropAnchor()
        {
            _dropAt = null;
            // the pool, from the renderer's hotspots or the kernel's trace
            var hp = Hotspots(_victim).Where(h => h.Kind == GoreKind.Pool).OrderByDescending(h => h.Size).ToList();
            if (hp.Count > 0) _poolCentre = hp[0].Pos;
            else { var t = S.Traces.Where(x => x.Type == "BloodPool" && x.Room == _room && !x.Cleaned && x.Victim == _victim).OrderByDescending(x => x.Clock).FirstOrDefault(); if (t != null) _poolCentre = _s.World.ToWorld(t.Pos); }
            // 1) the weapon, bloody, lying on something
            var it = _inc != null ? S.I(_inc.Weapon) : null;
            if (it != null && it.Bloody && it.Room == _room && it.Holder == null && !it.Hidden && _s.World.Items.TryGetValue(it.Id, out var iv) && iv != null)
            {
                var rs = iv.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    if (b.min.y - _floorY >= 0.12f)
                    {
                        var ax = b.size.x >= b.size.z ? Vector3.right * b.extents.x : Vector3.forward * b.extents.z;
                        var tipA = b.center + ax; var tipB = b.center - ax; var tip = tipA;
                        if (_poolCentre.HasValue && (tipB - _poolCentre.Value).sqrMagnitude < (tipA - _poolCentre.Value).sqrMagnitude) tip = tipB;
                        tip.y = b.min.y - 0.004f; _dropAt = tip; Stamp($"drop at the weapon tip {tip:F2}"); return;
                    }
                }
            }
            // 2) an edge above the pool
            if (!_poolCentre.HasValue) return;
            var pc = _poolCentre.Value; float best = 0.8f;
            foreach (var f in S.Layout.Furniture)
            {
                if (f.Room != _room || f.H < 0.25f || f.H > 1.3f || f.Type == "Rug" || f.Marks.Any(m => m.Contains("넘어져"))) continue;
                var fp = _s.World.ToWorld(f.Pos);
                var local = Quaternion.Euler(0, -f.Yaw, 0) * new Vector3(pc.x - fp.x, 0, pc.z - fp.z);
                bool inside = Mathf.Abs(local.x) < f.W * 0.5f && Mathf.Abs(local.z) < f.D * 0.5f;
                if (inside) { if (f.W * 0.5f - Mathf.Abs(local.x) < f.D * 0.5f - Mathf.Abs(local.z)) local.x = Mathf.Sign(local.x) * f.W * 0.5f; else local.z = Mathf.Sign(local.z) * f.D * 0.5f; }
                else { local.x = Mathf.Clamp(local.x, -f.W * 0.5f, f.W * 0.5f); local.z = Mathf.Clamp(local.z, -f.D * 0.5f, f.D * 0.5f); }
                var edge = fp + Quaternion.Euler(0, f.Yaw, 0) * local;
                float d = new Vector2(edge.x - pc.x, edge.z - pc.z).magnitude;
                if (d > best) continue;
                best = d; edge.y = _floorY + f.H - 0.03f; _dropAt = edge;
            }
            if (_dropAt.HasValue) Stamp($"drop at a furniture edge {_dropAt.Value:F2}");
        }

        void DropSpawn()
        {
            if (!_dropAt.HasValue || _drop != null) return;
            if (_dropMat == null)
            {
                _dropMat = MansionMats.NewLit("FilmDrop", null, 1f, 0.04f);
                _dropMat.SetColor("_BaseColor", DropCol); _dropMat.SetFloat("_Wet", 1f); _dropMat.SetFloat("_Grime", 0f);
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = "FilmDrop";
            var col = go.GetComponent<Collider>(); if (col != null) UnityEngine.Object.Destroy(col);
            var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = _dropMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _drop = go.transform; _fx.Add(go);
            _dropPos = _dropAt.Value; _drop.position = _dropPos; _drop.localScale = new Vector3(0.006f, 0.009f, 0.006f);
            _dropT0 = Time.unscaledTime;
            // where it will land
            _dropLandY = _floorY + 0.003f;
            foreach (var h in Physics.RaycastAll(_dropPos + Vector3.down * 0.02f, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                var av = h.collider.GetComponentInParent<ActorView>(); if (av != null && av.Id == Cast.Player) continue;
                _dropLandY = h.point.y + 0.003f; break;
            }
        }

        void DropRelease() { if (_drop != null) { _dropFalling = true; _dropV = 0f; } }

        void DropTick(bool falling)
        {
            if (_drop == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (!_dropFalling)
            {
                // it swells at the lip, slowly, while the world is suspended
                float k = Mathf.Clamp01((Time.unscaledTime - _dropT0) / 5f);
                float sw = 1f + 0.35f * k;
                _drop.localScale = new Vector3(0.006f * sw, 0.009f * sw * (1f + 0.25f * k), 0.006f * sw);
                _drop.position = _dropPos + Vector3.down * 0.0045f * sw * (1f + 0.25f * k);
                return;
            }
            _dropV += 9.81f * dt; var p = _drop.position; p.y -= _dropV * dt; _drop.position = p;
            float stretch = 1f + Mathf.Clamp01(_dropV / 4f) * 0.8f;
            _drop.localScale = new Vector3(0.0075f / Mathf.Sqrt(stretch), 0.011f * stretch, 0.0075f / Mathf.Sqrt(stretch));
            if (p.y <= _dropLandY)
            {
                var at = new Vector3(p.x, _dropLandY, p.z);
                UnityEngine.Object.Destroy(_drop.gameObject); _drop = null; _dropFalling = false;
                Splash(at);
            }
        }

        void Splash(Vector3 at)
        {
            if (Sfx.Has("amb_drip")) Sfx.PlayEx("amb_drip", at, 0.5f);
            var mat = MansionMats.Particle("FilmRipple", MansionMats.Proc("SoftDot"), false);
            // the ring
            var ring = new GameObject("FilmRipple"); _fx.Add(ring); ring.transform.position = at;
            var mf = ring.AddComponent<MeshFilter>(); var mr = ring.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            var mesh = new Mesh { name = "FilmRipple" }; mf.sharedMesh = mesh;
            var rp = ring.AddComponent<FilmRipple>(); rp.Init(mesh, ThickCol);
            // two or three satellites
            var rnd = new System.Random(Fnv(_victim + "|drop"));
            int n = 2 + rnd.Next(2);
            for (int i = 0; i < n; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, d = 0.02f + (float)rnd.NextDouble() * 0.04f, s = 0.003f + (float)rnd.NextDouble() * 0.004f;
                var q = new GameObject("FilmSatellite"); _fx.Add(q); q.transform.position = at + new Vector3(Mathf.Cos(a) * d, 0.001f, Mathf.Sin(a) * d);
                var qf = q.AddComponent<MeshFilter>(); var qr = q.AddComponent<MeshRenderer>(); qr.sharedMaterial = mat; qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                qf.sharedMesh = FlatQuad(s, new Color(ThickCol.r, ThickCol.g, ThickCol.b, 0.95f));
            }
        }

        static Mesh FlatQuad(float r, Color c)
        {
            var m = new Mesh { name = "FilmSpeck" };
            m.vertices = new[] { new Vector3(-r, 0, -r), new Vector3(-r, 0, r), new Vector3(r, 0, r), new Vector3(r, 0, -r) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.colors = new[] { c, c, c, c }; m.triangles = new[] { 0, 1, 2, 0, 2, 3 }; m.RecalculateBounds(); m.RecalculateNormals();
            return m;
        }

        // ------------------------------------------------------------------ the moth
        Transform _moth, _wingL, _wingR; Vector3 _mothFrom; float _mothT0; const float MothLand = 0.62f;
        void MothStart(Shot sh)
        {
            if (_moth != null) return;
            var hand = sh.Target != null ? sh.Target() : sh.P;
            // from the nearest candle within 5 m (its first 1.2 m of flight), else out of the dark, off the lens's side
            Vector3 from; var cand = _candles.Where(c => Vector3.Distance(c.pos, hand) < 5f).OrderBy(c => Vector3.Distance(c.pos, hand)).Select(c => (Vector3?)c.pos).FirstOrDefault();
            if (cand.HasValue) { var d = cand.Value - hand; from = hand + d.normalized * Mathf.Min(d.magnitude, 1.2f); }
            else { var away = hand - sh.Lens; away.y = 0; away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward; from = hand + Quaternion.AngleAxis(70f, Vector3.up) * away * 1.0f + Vector3.up * 0.45f; }
            _mothFrom = from; _mothT0 = Time.unscaledTime;
            if (_mothMat == null)
            {
                _mothMat = MansionMats.NewLit("FilmMoth", null, 1f, 0.95f);
                _mothMat.SetColor("_BaseColor", new Color(0.36f, 0.31f, 0.25f, 1f)); _mothMat.SetFloat("_Cull", 0f);
            }
            var root = new GameObject("FilmMoth"); _fx.Add(root); _moth = root.transform; _moth.position = from;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name = "Body"; UnityEngine.Object.Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(_moth, false); body.transform.localRotation = Quaternion.Euler(90, 0, 0); body.transform.localScale = new Vector3(0.0045f, 0.007f, 0.0045f);
            body.GetComponent<MeshRenderer>().sharedMaterial = _mothMat;
            _wingL = Wing(-1f); _wingR = Wing(1f);
        }
        Transform Wing(float side)
        {
            var go = new GameObject(side < 0 ? "WingL" : "WingR"); go.transform.SetParent(_moth, false);
            var mf = go.AddComponent<MeshFilter>(); var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = _mothMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // a rounded, dusty forewing + hindwing (5-point fan), pivoting on the body's axis
            float s = side;
            var v = new[] { new Vector3(0, 0, 0.004f), new Vector3(s * 0.012f, 0.0005f, 0.007f), new Vector3(s * 0.022f, 0.001f, 0.001f), new Vector3(s * 0.017f, 0.0005f, -0.008f), new Vector3(s * 0.006f, 0, -0.009f), new Vector3(0, 0, -0.005f) };
            var c = new Color(0.9f, 0.86f, 0.8f, 1f); var col = new[] { c, c * 0.92f, c * 0.8f, c * 0.85f, c * 0.95f, c };
            var m = new Mesh { name = "MothWing" }; m.vertices = v; m.colors = col;
            m.uv = v.Select(x => new Vector2(x.x * 20f + 0.5f, x.z * 20f + 0.5f)).ToArray();
            m.triangles = s > 0 ? new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5 } : new[] { 0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 5, 4 };
            m.RecalculateNormals(); m.RecalculateBounds(); mf.sharedMesh = m;
            return go.transform;
        }
        void MothTick(Shot sh, float u)
        {
            if (_moth == null) return;
            var hand = (sh.Target != null ? sh.Target() : sh.P) + Vector3.up * 0.012f;
            float t = Time.unscaledTime - _mothT0;
            if (u < MothLand)
            {
                float k = u / MothLand; float e = k * k * (3f - 2f * k);
                var mid = (_mothFrom + hand) * 0.5f + Vector3.up * 0.18f;
                var p = (1 - e) * (1 - e) * _mothFrom + 2 * (1 - e) * e * mid + e * e * hand;
                var side = Vector3.Cross(Vector3.up, (hand - _mothFrom).normalized);
                p += side * Mathf.Sin(t * 11f) * 0.03f * (1f - e) + Vector3.up * Mathf.Sin(t * 17f) * 0.015f * (1f - e);
                var dir = p - _moth.position; if (dir.sqrMagnitude > 1e-8f) _moth.rotation = Quaternion.Slerp(_moth.rotation, Quaternion.LookRotation(dir.normalized, Vector3.up), 0.5f);
                _moth.position = p;
                float flap = Mathf.Lerp(0.15f, 1f, Mathf.Abs(Mathf.Sin(t * Mathf.PI * 18f)));
                if (_wingL != null) _wingL.localScale = new Vector3(flap, 1, 1); if (_wingR != null) _wingR.localScale = new Vector3(flap, 1, 1);
            }
            else
            {
                // settled: wings open and close slowly, flat on the skin
                _moth.position = hand;
                var fl = _moth.forward; fl.y = 0; if (fl.sqrMagnitude > 1e-4f) _moth.rotation = Quaternion.Slerp(_moth.rotation, Quaternion.LookRotation(fl.normalized, Vector3.up), 0.2f);
                float open = Mathf.Lerp(0.55f, 1f, 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 1.3f));
                if (_wingL != null) _wingL.localScale = new Vector3(open, 1, 1); if (_wingR != null) _wingR.localScale = new Vector3(open, 1, 1);
            }
        }

        // ------------------------------------------------------------------ the last light on it
        /// <summary>
        /// As the candles gutter, one soft, warm pool of light stays on the body from above (the last candle, or just the eye
        /// refusing to let go): the room sinks into the dark and the body remains the brightest thing in it. It swells in with
        /// the dip, halves under the close-ups' own key light and goes out with the return.
        /// </summary>
        Light _gaze; float _gazeFrom, _gazeTo, _gazeT0 = -1f, _gazeDur = 0.6f;
        const float GazeMax = 0.85f;

        void GazeStart()
        {
            if (_gaze != null || _fallback) return;
            var go = new GameObject("FilmGazeLight"); _fx.Add(go);
            var up = new Vector3(_focus.x, Mathf.Max(_focus.y + 1.35f, _floorY + 1.6f), _focus.z);
            // lean a little toward his side, so the light falls on what he sees
            var toEye = _eye0 - _focus; toEye.y = 0f; if (toEye.sqrMagnitude > 1e-4f) up += toEye.normalized * 0.35f;
            go.transform.position = up; go.transform.rotation = Quaternion.LookRotation((_focus - up).normalized, Vector3.forward);
            _gaze = go.AddComponent<Light>(); _gaze.type = LightType.Spot; _gaze.spotAngle = 74f; _gaze.innerSpotAngle = 30f;
            // no shadow map (the film already pays for its key's): a narrow downward cone that ends short of the walls
            _gaze.range = Mathf.Min(2.4f, Vector3.Distance(up, _focus) + 0.9f); _gaze.shadows = LightShadows.None;
            _gaze.color = new Color(1f, 0.66f, 0.38f); _gaze.intensity = 0f;
            GazeTo(GazeMax, 0.6f);
        }
        void GazeTo(float to, float secs) { if (_gaze == null) return; _gazeFrom = _gaze.intensity; _gazeTo = to; _gazeT0 = Time.unscaledTime; _gazeDur = Mathf.Max(0.01f, secs); }
        void GazeTick(float now)
        {
            if (_gaze == null || _gazeT0 < 0f) return;
            float u = Mathf.Clamp01((now - _gazeT0) / _gazeDur); u = u * u * (3f - 2f * u);
            _gaze.intensity = Mathf.Lerp(_gazeFrom, _gazeTo, u) * (0.96f + 0.04f * Mathf.PerlinNoise(now * 6.1f, 0.77f));
            if (u >= 1f && _gazeTo <= 0f) { _gaze.enabled = false; _gazeT0 = -1f; }
        }

        // ------------------------------------------------------------------ cleanup
        void DestroyFx()
        {
            foreach (var go in _fx) if (go != null) UnityEngine.Object.Destroy(go);
            _fx.Clear(); _motes = null; _drop = null; _moth = null; _wingL = _wingR = null; _gaze = null;
        }
    }

    /// <summary>A ring spreading on the floor where a drop landed (0.6 s), then gone.</summary>
    public sealed class FilmRipple : MonoBehaviour
    {
        Mesh _m; Color _c; float _t0; const int Seg = 32;
        readonly Vector3[] _v = new Vector3[Seg * 2]; readonly Color[] _col = new Color[Seg * 2];
        public void Init(Mesh m, Color c)
        {
            _m = m; _c = c; _t0 = Time.unscaledTime;
            var uv = new Vector2[Seg * 2]; var tri = new int[Seg * 6];
            for (int i = 0; i < Seg; i++)
            {
                uv[i * 2] = new Vector2(0f, 0.5f); uv[i * 2 + 1] = new Vector2(1f, 0.5f);
                int a = i * 2, b = ((i + 1) % Seg) * 2;
                tri[i * 6] = a; tri[i * 6 + 1] = b; tri[i * 6 + 2] = a + 1; tri[i * 6 + 3] = b; tri[i * 6 + 4] = b + 1; tri[i * 6 + 5] = a + 1;
            }
            Build(0f); _m.uv = uv; _m.triangles = tri; _m.RecalculateBounds();
        }
        void Build(float u)
        {
            float r = Mathf.Lerp(0.004f, 0.06f, 1f - (1f - u) * (1f - u)); float w = Mathf.Lerp(0.003f, 0.008f, u);
            float a = (1f - u) * 0.85f; var c = new Color(_c.r, _c.g, _c.b, a);
            for (int i = 0; i < Seg; i++)
            {
                float t = i * Mathf.PI * 2f / Seg; var d = new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t));
                _v[i * 2] = d * Mathf.Max(0f, r - w); _v[i * 2 + 1] = d * (r + w); _col[i * 2] = c; _col[i * 2 + 1] = c;
            }
            _m.vertices = _v; _m.colors = _col;
        }
        void Update()
        {
            float u = Mathf.Clamp01((Time.unscaledTime - _t0) / 0.6f);
            Build(u); _m.RecalculateBounds();
            if (u >= 1f) Destroy(gameObject);
        }
        void OnDestroy() { if (_m != null) Destroy(_m); }
    }
}
