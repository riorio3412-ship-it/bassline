using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// The circular trial chamber on floor -2. The candle-lit island (dark marble clock floor, oak wainscot, the ring
    /// gallery of watching eyes, 18 podiums, Yusti's raised bench, the lift cage) is the only warm place at the bottom of a
    /// gothic well 76 m across (CourtroomView.Well*.cs): colossal clustered piers, galleries of hooded watchers climbing out
    /// of sight, chains falling into an abyss, one grisaille lancet behind the throne that gives the only cold key light, and
    /// at the zenith an eclipse that behaves like an eye.
    /// SetTension(0..1): the candles gutter harder and the warm light leans toward crimson.
    /// </summary>
    public sealed partial class CourtroomView : MonoBehaviour
    {
        public Transform ButlerAnchor;
        /// <summary>Yusti's seat on the judge's bench (he sits here for the whole trial and speaks only the verdict).</summary>
        public Transform JudgeAnchor => ButlerAnchor;
        /// <summary>The gallery of watching eyes (may be null if the build failed).</summary>
        public CourtroomEyes Eyes { get; private set; }
        public void Watch(Vector3 worldPos) { if (Eyes != null) Eyes.Watch(worldPos); WellWatch(worldPos); }
        public void Stare(float intensity) { if (Eyes != null) Eyes.Stare(intensity); WellStare(intensity); }
        public void Blink() { if (Eyes != null) Eyes.Blink(); WellBlink(); }
        readonly Transform[] _stands = new Transform[18];
        float _tension, _tensionShown;
        MansionView _view;
        MansionView.RoomView _rv;
        public Vector3 Center { get; private set; }
        public float Radius { get; private set; }

        static readonly Color Candle = new Color(1f, 0.8f, 0.58f), Moon = new Color(0.62f, 0.72f, 1f), Crimson = new Color(0.85f, 0.22f, 0.16f);

        /// <summary>A light this view drives itself (created with flicker 0 so MansionView leaves its intensity alone).</summary>
        sealed class Anim { public MansionView.LightRec Rec; public float Phase; public Color Calm; public bool Fire, Noise, Key; }
        readonly List<Anim> _anims = new List<Anim>();
        Anim _key;   // the cold key through the lancet

        /// <summary>Anchor behind podium 'seat' (0..17), facing the centre. Actors stand here.</summary>
        public Transform StandAnchor(int seat) => seat >= 0 && seat < _stands.Length ? _stands[seat] : null;

        /// <summary>0 calm .. 1 climax.</summary>
        public void SetTension(float t)
        {
            _tension = Mathf.Clamp01(t);
            if (_view != null && _view.Atmosphere != null) _view.Atmosphere.SetTension(_tension * 0.85f);
        }

        void AnimLight(MansionView.LightRec rec, float phase, Color calm, bool fire, bool noise, bool key = false)
        {
            if (rec == null || rec.Light == null) return;
            rec.Flicker = 0f;
            var a = new Anim { Rec = rec, Phase = phase, Calm = calm, Fire = fire, Noise = noise, Key = key };
            _anims.Add(a); if (key) _key = a;
        }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            float target = Mathf.Max(_tension, 0.6f * StareLevel);
            _tensionShown = Mathf.MoveTowards(_tensionShown, target, dt * 0.4f);
            UpdateLights(); UpdateFlames();
            try { UpdateWell(dt); } catch (System.Exception e) { if (!_wellErr) { _wellErr = true; Debug.LogException(e); } }
        }
        bool _wellErr;

        void UpdateLights()
        {
            if (_view == null) return;
            float t = Time.time;
            for (int i = 0; i < _anims.Count; i++)
            {
                var a = _anims[i]; var rec = a.Rec;
                if (rec == null || rec.Light == null || !rec.Light.enabled) continue;
                float k = 1f;
                if (a.Noise)
                {
                    // candle flicker: layered noise, rougher as tension rises
                    float ph = a.Phase;
                    float n = Mathf.PerlinNoise(t * (1.6f + _tensionShown * 3.5f) + ph, ph * 3.1f);
                    float gust = Mathf.PerlinNoise(t * 0.35f + ph * 0.7f, 5.3f);
                    k = Mathf.Lerp(0.88f, 1.08f, n) * Mathf.Lerp(1f, Mathf.Lerp(0.55f, 1.15f, gust), _tensionShown);
                }
                if (a.Key) k *= KeyGain(t);
                rec.Light.intensity = rec.Base * _view.LightFactor(rec) * k;
                if (a.Fire) rec.Light.color = Color.Lerp(a.Calm, Crimson, _tensionShown * 0.35f);
            }
        }

        internal static CourtroomView Build(MansionView v, MansionView.RoomView rv)
        {
            var go = new GameObject("Courtroom"); go.transform.SetParent(rv.Root, false);
            var cv = go.AddComponent<CourtroomView>();
            cv._view = v; cv._rv = rv;
            var r = rv.Room; var R = r.Rect; var pal = rv.Pal;
            float fy = rv.FloorY;
            var c = new Vector3(R.CX, fy, R.CZ);
            float rad = Mathf.Min(R.W, R.D) * 0.5f - 0.15f;
            cv.Center = c; cv.Radius = rad;
            Color gilt = new Color(0.78f, 0.6f, 0.3f), oak = new Color(0.34f, 0.22f, 0.15f), oakDark = new Color(0.2f, 0.13f, 0.09f);
            var mb = new MeshBuilder();
            var fx = new MeshBuilder();      // flames (no shadows)
            int seg = 36;

            // ---- floor: dark veined marble disc, brass rings, a clock face of inlaid hour marks, a mosaic fish at the centre
            mb.Set(S.MarbleDark, Color.white, new Vector4(0.25f, 0.05f, 0.2f, 0));
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2, a1 = (i + 1) / (float)seg * Mathf.PI * 2;
                mb.QuadAuto(c, c + new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)) * (rad + 0.2f), c + new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1)) * (rad + 0.2f), c, Vector3.up);
            }
            mb.Set(S.Brass, gilt);
            foreach (float rr in new[] { 1.1f, 1.25f, 4.4f, 4.6f, 7.4f }) { mb.Push(c + Vector3.up * 0.004f, 0); mb.Torus(Vector3.zero, rr, 0.028f, 72, 4); mb.Pop(); }
            // twelve hour marks (long bars, doubled at the quarters) and sixty minute ticks between the brass rings
            for (int k = 0; k < 60; k++)
            {
                float a = k / 60f * Mathf.PI * 2; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                bool hour = k % 5 == 0; float r0 = hour ? 3.3f : 3.95f;
                mb.Bar(c + d * r0 + Vector3.up * 0.006f, c + d * 4.3f + Vector3.up * 0.006f, hour ? (k % 15 == 0 ? 0.09f : 0.05f) : 0.018f, 0.008f);
            }
            // two great hands, stopped at a time nobody set
            mb.Set(S.Iron, new Color(0.12f, 0.1f, 0.09f));
            float hh = (v.Layout.Seed % 12) / 12f * Mathf.PI * 2, mm = (v.Layout.Seed % 60) / 60f * Mathf.PI * 2;
            mb.Bar(c + Vector3.up * 0.01f, c + new Vector3(Mathf.Sin(hh), 0, Mathf.Cos(hh)) * 2.2f + Vector3.up * 0.01f, 0.1f, 0.012f);
            mb.Bar(c + Vector3.up * 0.012f, c + new Vector3(Mathf.Sin(mm), 0, Mathf.Cos(mm)) * 3.1f + Vector3.up * 0.012f, 0.06f, 0.012f);
            // centre medallion: Yusti's fish in mosaic
            mb.Set(S.Mosaic, new Color(0.55f, 0.47f, 0.4f)); mb.Disc(c + Vector3.up * 0.005f, 1.08f, 40, true);
            mb.Set(S.Porcelain, new Color(0.86f, 0.8f, 0.7f)); mb.Push(c + Vector3.up * 0.008f, 30f); mb.Ellipsoid(Vector3.zero, new Vector3(0.55f, 0.006f, 0.26f), 16, 3);
            mb.Tri(new Vector3(0.5f, 0.001f, 0), new Vector3(0.85f, 0.001f, 0.25f), new Vector3(0.85f, 0.001f, -0.25f)); mb.Tri(new Vector3(0.85f, 0.001f, -0.25f), new Vector3(0.85f, 0.001f, 0.25f), new Vector3(0.5f, 0.001f, 0));
            mb.Set(S.Obsidian, Color.white); mb.Sphere(new Vector3(-0.3f, 0.004f, 0.05f), 0.06f, 8, 4, 0.1f); mb.Pop();

            // where the judge's bench stands: the wall opens there (the breach), and the throne sits at the lip of the drop
            var deskF = v.Layout.Furniture.FirstOrDefault(f => f.Room == r.Id && f.Type == "ButlerDesk");
            Vector3 judgeDir = deskF != null ? v.ToWorld(deskF.Pos) - c : Vector3.forward; judgeDir.y = 0; judgeDir = judgeDir.sqrMagnitude > 1e-4f ? judgeDir.normalized : Vector3.forward;
            float judgeAng = Mathf.Atan2(judgeDir.x, judgeDir.z) * Mathf.Rad2Deg;
            // ---- the island's rim (oak wainscot beside the breach and the lift, an open arcade onto the well everywhere else) is
            // built with the island's edge once the frame is known (CourtroomView.Island.cs); above it the gallery, the parapet, the void
            float panelTop = 2.6f, domeBase = r.CeilingH - 2.9f;

            // ---- Yusti's raised bench: carved oak, oxblood velvet high-backed chair, gilt fish
            // the bench stands clear of the wall (the throne's back must not sink into the stone); the old desk prop is not needed
            var desk = deskF;
            if (desk != null) { var dgo = v.FurnitureObject(desk.Id); if (dgo != null) dgo.SetActive(false); }
            Vector3 fwd = -judgeDir;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 benchC = c + judgeDir * (rad - 1.3f);
            float benchH = 1.9f;
            mb.Push(Matrix4x4.TRS(benchC, Quaternion.LookRotation(fwd), Vector3.one));
            mb.Set(S.WoodDark, oakDark); mb.Box(new Vector3(0, benchH / 2, 0), new Vector3(2.6f, benchH, 1.1f));
            mb.Set(S.WoodDark, oak);
            for (int k = 0; k < 4; k++) mb.BevelBox(new Vector3(-0.96f + k * 0.64f, benchH * 0.5f, 0.56f), new Vector3(0.5f, benchH * 0.7f, 0.04f), 0.02f);
            mb.Set(S.Gold, gilt); mb.Box(new Vector3(0, benchH + 0.02f, 0), new Vector3(2.7f, 0.05f, 1.2f)); mb.Box(new Vector3(0, 0.06f, 0.58f), new Vector3(2.62f, 0.05f, 0.03f));
            mb.Set(S.Velvet, new Color(0.36f, 0.04f, 0.07f));
            mb.BevelBox(new Vector3(0, benchH + 0.25f, -0.05f), new Vector3(0.9f, 0.3f, 0.7f), 0.05f);
            mb.BevelBox(new Vector3(0, benchH + 1.35f, -0.38f), new Vector3(1.0f, 2.0f, 0.18f), 0.06f);
            mb.Set(S.WoodDark, oakDark);
            foreach (float sx in new[] { -0.55f, 0.55f }) { mb.Box(new Vector3(sx, benchH + 1.3f, -0.4f), new Vector3(0.12f, 2.3f, 0.22f)); mb.Sphere(new Vector3(sx, benchH + 2.5f, -0.4f), 0.09f, 8, 6); }
            // Yusti's fish as a dark-bronze relief in a roundel on the chair back, just above his head (no floating crest)
            {
                var bronze = new Color(0.36f, 0.26f, 0.15f);
                mb.Push(Matrix4x4.TRS(new Vector3(0, benchH + 2.1f, -0.285f), Quaternion.identity, Vector3.one));
                mb.Set(S.Brass, bronze * 0.8f);
                mb.Push(Vector3.zero, Quaternion.Euler(90f, 0f, 0f), Vector3.one); mb.Torus(Vector3.zero, 0.235f, 0.02f, 32, 6); mb.Pop();
                mb.Push(Matrix4x4.TRS(new Vector3(0.035f, 0f, 0f), Quaternion.identity, Vector3.one * 0.8f));
                mb.Set(S.Brass, bronze);
                mb.Ellipsoid(new Vector3(0.03f, 0f, 0.012f), new Vector3(0.155f, 0.075f, 0.022f), 18, 8);                    // body
                mb.Ellipsoid(new Vector3(0.14f, -0.005f, 0.014f), new Vector3(0.05f, 0.055f, 0.02f), 12, 6);               // head
                mb.Push(new Vector3(0.02f, 0.075f, 0.01f), Quaternion.Euler(0f, 0f, -18f), Vector3.one); mb.Ellipsoid(Vector3.zero, new Vector3(0.075f, 0.035f, 0.009f), 12, 5); mb.Pop();   // dorsal fin
                mb.Push(new Vector3(0.05f, -0.07f, 0.01f), Quaternion.Euler(0f, 0f, 25f), Vector3.one); mb.Ellipsoid(Vector3.zero, new Vector3(0.04f, 0.022f, 0.008f), 10, 4); mb.Pop();    // pelvic fin
                foreach (float s in new[] { 1f, -1f })
                {
                    mb.Push(new Vector3(-0.17f, s * 0.045f, 0.01f), Quaternion.Euler(0f, 0f, s * 38f), Vector3.one);
                    mb.Ellipsoid(new Vector3(-0.045f, 0f, 0f), new Vector3(0.07f, 0.03f, 0.01f), 12, 5); mb.Pop();                                                             // forked tail
                }
                mb.Set(S.Obsidian, Color.white); mb.Sphere(new Vector3(0.155f, 0.012f, 0.032f), 0.011f, 8, 5);
                mb.Pop();
                mb.Pop();
            }
            // side stairs: dark veined stone with a worn brass nosing on every tread (pale marble here read as a block of sandstone)
            mb.Set(S.MarbleDark, new Color(0.3f, 0.28f, 0.27f));
            for (int s = 0; s < 8; s++)
                foreach (float sx in new[] { -1f, 1f })
                {
                    float h = (s + 1) * benchH / 8f;
                    mb.Box(new Vector3(sx * (1.3f + 0.15f + (7 - s) * 0.28f), h / 2, -0.1f), new Vector3(0.28f, h, 0.9f));
                }
            mb.Set(S.Brass, new Color(0.42f, 0.33f, 0.2f));
            for (int s = 0; s < 8; s++)
                foreach (float sx in new[] { -1f, 1f })
                {
                    float h = (s + 1) * benchH / 8f, x = sx * (1.3f + 0.15f + (7 - s) * 0.28f + 0.13f);   // the tread's outer edge (the stairs climb toward the bench)
                    mb.Box(new Vector3(x, h + 0.006f, -0.1f), new Vector3(0.035f, 0.014f, 0.9f));
                }
            // two candelabra on the bench corners
            var bw = new List<Vector3>();
            foreach (float sx in new[] { -1.15f, 1.15f })
            {
                mb.Set(S.Brass, gilt); mb.Push(new Vector3(sx, benchH + 0.04f, 0.35f), 0);
                mb.Lathe(new[] { new Vector2(0.08f, 0), new Vector2(0.03f, 0.05f), new Vector2(0.018f, 0.3f), new Vector2(0.05f, 0.34f) }, 10); mb.Pop();
                FurnitureFactory.Candle(mb, new Vector3(sx, benchH + 0.38f, 0.35f), 0.2f, 0.022f, -2, new Vector3(sx, benchH + 0.59f, 0.35f), bw);
            }
            mb.Pop();
            foreach (var w in bw) MansionView.FlameQuad(fx, benchC + Quaternion.LookRotation(fwd) * w, 0.08f, -2);
            {
                var bb = new Bounds(benchC + Vector3.up * (benchH + 1.2f) * 0.5f, Vector3.zero);
                foreach (float sx in new[] { -1.3f, 1.3f }) foreach (float sz in new[] { -0.55f, 0.55f }) bb.Encapsulate(benchC + right * sx + fwd * sz + Vector3.up * (benchH + 2.5f));
                bb.Encapsulate(benchC);
                v.AddDecorCollider(rv, bb, "JudgeBench", true);
            }
            var ba = new GameObject("ButlerAnchor").transform; ba.SetParent(go.transform, false);
            ba.position = benchC + Vector3.up * (benchH + 0.42f) - fwd * 0.05f; ba.rotation = Quaternion.LookRotation(fwd);
            cv.ButlerAnchor = ba;

            // ---- wrought-iron lift cage in the wall (the trial lift lands here)
            var est = v.Layout.Stairs.FirstOrDefault(s => s.Name == "심판장 승강기" || s.Name == "재판장 승강기");
            Vector3 liftDir = Vector3.zero;
            if (est != null)
            {
                var ep = v.ToWorld(est.B);
                Vector3 toWall = (ep - c); toWall.y = 0; toWall = toWall.sqrMagnitude > 1e-3f ? toWall.normalized : Vector3.back; liftDir = toWall;
                var wallP = c + toWall * (rad - 0.2f);
                mb.Push(Matrix4x4.TRS(wallP, Quaternion.LookRotation(-toWall), Vector3.one));
                mb.Set(S.Iron, new Color(0.08f, 0.07f, 0.07f));
                for (int i = 0; i <= 10; i++) { float x = -1.1f + i * 0.22f; mb.Box(new Vector3(x, 1.5f, 0), new Vector3(0.03f, 3.0f, 0.03f)); }
                mb.Box(new Vector3(0, 3.0f, 0), new Vector3(2.4f, 0.08f, 0.1f)); mb.Box(new Vector3(0, 0.04f, 0), new Vector3(2.4f, 0.08f, 0.1f));
                for (int i = 0; i < 5; i++) { mb.Push(new Vector3(-0.88f + i * 0.44f, 2.7f, 0.02f), Quaternion.Euler(90, 0, 0), Vector3.one); mb.Torus(Vector3.zero, 0.16f, 0.012f, 16, 4); mb.Pop(); }
                mb.Set(S.WoodDark, oakDark); mb.Box(new Vector3(0, 1.6f, -0.12f), new Vector3(2.3f, 3.2f, 0.05f));
                mb.Set(S.Gold, gilt); mb.Box(new Vector3(0, 3.12f, 0.05f), new Vector3(2.5f, 0.06f, 0.04f));
                mb.Pop();
                var cb = new Bounds(wallP + Vector3.up * 1.6f, Vector3.zero);
                var cr = Vector3.Cross(Vector3.up, toWall);
                foreach (float sx in new[] { -1.2f, 1.2f }) foreach (float sz in new[] { -0.15f, 0.05f }) { cb.Encapsulate(wallP + cr * sx + toWall * sz); cb.Encapsulate(wallP + cr * sx + toWall * sz + Vector3.up * 3.2f); }
                v.AddDecorCollider(rv, cb, "ElevatorCage", true);
            }
            if (liftDir == Vector3.zero) liftDir = -judgeDir;

            // ---- stand anchors from the layout (TrialStand furniture, Variant = seat)
            var stands = v.Layout.Furniture.Where(f => f.Room == r.Id && f.Type == "TrialStand").ToList();
            foreach (var f in stands)
            {
                int seat = Mathf.Clamp(f.Variant, 0, 17);
                var sp = v.ToWorld(f.Pos);
                var toC = c - sp; toC.y = 0; toC.Normalize();
                var t = new GameObject("Stand" + seat).transform; t.SetParent(go.transform, false);
                t.position = sp - toC * 0.62f; t.rotation = Quaternion.LookRotation(toC);
                cv._stands[seat] = t;
            }
            for (int s = 0; s < 18; s++)
                if (cv._stands[s] == null)
                {
                    float a = s / 18f * Mathf.PI * 2; var sp = c + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 6.2f;
                    var toC = (c - sp).normalized; var t = new GameObject("Stand" + s).transform; t.SetParent(go.transform, false);
                    t.position = sp - toC * 0.62f; t.rotation = Quaternion.LookRotation(toC); cv._stands[s] = t;
                }

            // ---- the island's edge: parapet with iron candle trees, the judge's breach and promontory, the chandelier on its endless chain
            cv.SetFrame(v, rv, c, rad, judgeDir, liftDir);
            try { cv.Rim(v, mb, panelTop); }
            catch (System.Exception e) { Debug.LogException(e); }
            try { cv.BuildIsland(v, rv, go.transform, mb, fx); }
            catch (System.Exception e) { Debug.LogException(e); }

            // ---- light: one cold key through the far lancet (the only shadowed light), warm candle pools, a steady key on Yusti
            {
                var kp = c + judgeDir * 22f + Vector3.up * 36f;
                var key = v.AddLight(rv, kp, new Color(0.72f, 0.77f, 0.9f), 220f, 70f, LightType.Spot, true, 0f, false, true, false, Quaternion.LookRotation((c - judgeDir * 1.5f + Vector3.up * 0.5f) - kp), 30f);
                if (key?.Light != null) { key.Light.shadowNearPlane = 10f; key.Light.shadowBias = 0.05f; key.Light.shadowNormalBias = 0.4f; }
                cv.AnimLight(key, 0f, Moon, false, false, true);
            }
            for (int k = 0; k < 6; k++)
            {
                float a = (k + 0.5f) / 6f * Mathf.PI * 2;
                var lp = c + new Vector3(Mathf.Sin(a) * (rad - 2.3f), 2.2f, Mathf.Cos(a) * (rad - 2.3f));
                bool warm = k % 2 == 0;
                var l = v.AddLight(rv, lp, warm ? Candle : Moon * 0.8f, warm ? 2.6f : 1.8f, 8f, LightType.Point, false, 0f, fire: warm, moon: !warm);
                if (warm) cv.AnimLight(l, k * 1.3f, Candle, true, true);
            }
            var ys = v.AddLight(rv, ba.position + fwd * 2.5f + Vector3.up * 3f, new Color(1f, 0.82f, 0.62f), 12f, 8f, LightType.Spot, false, 0f, false, false, false, Quaternion.LookRotation((ba.position + Vector3.up * 0.6f) - (ba.position + fwd * 2.5f + Vector3.up * 3f)), 44f);   // wide enough to catch the bronze fish on the chair back
            // a weak cold fill from over the floor onto the podiums nearest the throne: the front row stays readable against the
            // dark bench in the wides (they are otherwise lit only from behind, by the lancet)
            {
                var fp = c - judgeDir * 0.5f + Vector3.up * 5.4f; var ft = c + judgeDir * 6.3f + Vector3.up * 1.4f;
                v.AddLight(rv, fp, new Color(0.66f, 0.74f, 1f), 3.2f, 13f, LightType.Spot, false, 0f, false, true, false, Quaternion.LookRotation(ft - fp), 78f);
            }

            try { cv.DressIslandStone(v.Emit(rv, mb, "CourtShell", go.transform, ShadowCastingMode.On)); }
            catch (System.Exception e) { Debug.LogException(e); }
            cv.CourtFlame(v.Emit(rv, fx, "CourtFlames", go.transform, ShadowCastingMode.Off));
            // colliders: floor + circular wall
            var col = new GameObject("CourtColliders"); col.transform.SetParent(go.transform, false);
            var fb = col.AddComponent<BoxCollider>(); fb.center = c + Vector3.down * 0.15f; fb.size = new Vector3(R.W, 0.3f, R.D);
            for (int i = 0; i < 24; i++)
            {
                float a = (i + 0.5f) / 24f * 360f;
                var wgo = new GameObject("w" + i); wgo.transform.SetParent(col.transform, false);
                wgo.transform.position = c + Quaternion.Euler(0, a, 0) * Vector3.forward * (rad - 0.05f) + Vector3.up * (r.CeilingH * 0.5f);
                wgo.transform.rotation = Quaternion.Euler(0, a, 0);
                var bc = wgo.AddComponent<BoxCollider>(); bc.size = new Vector3(2 * Mathf.PI * rad / 24f + 0.3f, r.CeilingH, 0.5f);
            }
            // ---- the audience: a ring gallery of watching eyes (clear of the bench and the lift)
            try { var jd = benchC - c; jd.y = 0; jd = jd.sqrMagnitude > 1e-4f ? jd.normalized : Vector3.forward; cv.Eyes = CourtroomEyes.Build(v, rv, go.transform, c, rad, panelTop, domeBase, jd, liftDir, c + Vector3.up * 117.9f); }
            catch (System.Exception e) { Debug.LogException(e); }
            // ---- the well around the island (last: a failure here can never cost the anchors, colliders or lights)
            try { cv.BuildWell(v, rv, go.transform); }
            catch (System.Exception e) { Debug.LogException(e); }
            return cv;
        }
    }
}
