using System.Collections.Generic;
using BL23.Game.Mansion;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// Violence track effects, all procedural and short-lived: muzzle flash and powder smoke, a faint tracer, wood splinters
    /// and plaster dust at an impact, a fine blood mist at a wound, water splashes and bubbles for a drowning, bullet holes
    /// (decals oriented on the real surface), and rope rings / a gag on bound bodies. Particles use the mansion's shipped
    /// particle material (MansionMats.Particle), decals the shared TraceFactory atlas.
    /// </summary>
    public static class ViolenceFx
    {
        static Transform _root;
        static Transform Root { get { if (_root == null) _root = new GameObject("ViolenceFx").transform; return _root; } }

        static Material Mat(bool additive) => MansionMats.Particle(additive ? "ViolFxAdd" : "ViolFxAlpha", MansionMats.Proc("SoftDot"), additive);

        /// <summary>A one-shot particle burst.</summary>
        public static ParticleSystem Burst(Vector3 pos, Vector3 dir, int count, Color color, Vector2 size, Vector2 speed, Vector2 life, float gravity, float coneDeg, bool additive, float spreadRadius = 0.02f)
        {
            if (count <= 0) return null;
            var go = new GameObject("vfx"); go.SetActive(false); go.transform.SetParent(Root, false);
            go.transform.position = pos; go.transform.rotation = dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir.normalized) : Quaternion.identity;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y); main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = color; main.gravityModifier = gravity; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = Mathf.Max(8, count);
            var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = coneDeg; sh.radius = spreadRadius;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = Mat(additive); r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            go.SetActive(true); ps.Play();
            Object.Destroy(go, life.y + 0.6f);
            return ps;
        }

        // ================================================================== guns
        public static void MuzzleFlash(Vector3 muzzle, Vector3 dir, bool big)
        {
            var go = new GameObject("muzzle_flash"); go.transform.SetParent(Root, false); go.transform.position = muzzle + dir.normalized * 0.05f;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.72f, 0.38f); l.range = big ? 9f : 6.5f; l.intensity = big ? 9f : 6f; l.shadows = LightShadows.None;
            Object.Destroy(go, 0.06f);
            Burst(muzzle, dir, big ? 14 : 9, new Color(1f, 0.78f, 0.42f, 1f), new Vector2(0.06f, 0.2f), new Vector2(1.5f, 5f), new Vector2(0.03f, 0.08f), 0f, big ? 16f : 11f, true);
            Burst(muzzle, dir, 3, new Color(1f, 0.9f, 0.7f, 1f), new Vector2(0.28f, 0.42f), new Vector2(0.1f, 0.4f), new Vector2(0.03f, 0.05f), 0f, 2f, true);
            Smoke(muzzle, dir, big ? 16 : 10);
        }
        public static void Smoke(Vector3 pos, Vector3 dir, int n) => Burst(pos, dir + Vector3.up * 0.3f, n, new Color(0.62f, 0.6f, 0.58f, 0.32f), new Vector2(0.16f, 0.42f), new Vector2(0.2f, 0.9f), new Vector2(1.1f, 2.2f), -0.04f, 22f, false, 0.04f);

        /// <summary>A faint streak along the path (reads as a shot without looking like a laser).</summary>
        public static void Tracer(Vector3 a, Vector3 b)
        {
            var go = new GameObject("tracer"); go.transform.SetParent(Root, false);
            var lr = go.AddComponent<LineRenderer>(); lr.positionCount = 2; lr.SetPosition(0, a); lr.SetPosition(1, b);
            lr.startWidth = 0.012f; lr.endWidth = 0.004f; lr.sharedMaterial = Mat(true); lr.startColor = new Color(1f, 0.85f, 0.6f, 0.55f); lr.endColor = new Color(1f, 0.85f, 0.6f, 0.05f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false; lr.useWorldSpace = true;
            Object.Destroy(go, 0.05f);
        }

        /// <summary>Where a projectile struck a surface: dust / splinters out of the hole along the normal.</summary>
        public static void ImpactPuff(Vector3 pos, Vector3 normal, BL23.Sim.Mat mat)
        {
            bool wood = mat == BL23.Sim.Mat.Wood || mat == BL23.Sim.Mat.Paper;
            bool glass = mat == BL23.Sim.Mat.Glass || mat == BL23.Sim.Mat.Ceramic;
            var c = wood ? new Color(0.42f, 0.28f, 0.16f, 1f) : glass ? new Color(0.85f, 0.92f, 1f, 0.9f) : new Color(0.8f, 0.76f, 0.7f, 0.9f);
            Burst(pos + normal * 0.01f, normal, wood ? 14 : 10, c, new Vector2(0.012f, 0.035f), new Vector2(1.2f, 3.5f), new Vector2(0.3f, 0.8f), 1.4f, 35f, false);
            Burst(pos + normal * 0.02f, normal, 6, new Color(c.r, c.g, c.b, 0.35f), new Vector2(0.08f, 0.2f), new Vector2(0.2f, 0.7f), new Vector2(0.4f, 1f), -0.02f, 40f, false);
        }
        public static void BloodMist(Vector3 pos, Vector3 dir, float amount)
        {
            if (Settings.Gore <= 0) amount *= 0.3f;
            Burst(pos, dir, Mathf.RoundToInt(8 + 18 * amount), new Color(0.42f, 0.02f, 0.03f, 0.95f), new Vector2(0.015f, 0.05f), new Vector2(1.2f, 3.2f), new Vector2(0.25f, 0.6f), 1.2f, 22f, false);
            Burst(pos, dir, 4, new Color(0.35f, 0.02f, 0.03f, 0.35f), new Vector2(0.08f, 0.16f), new Vector2(0.3f, 0.9f), new Vector2(0.2f, 0.45f), 0.2f, 30f, false);
        }

        /// <summary>A bullet hole on the real surface: a cracked rim and a dark core (oriented by a raycast from the muzzle).</summary>
        public static GameObject BulletHole(Vector3 pos, Vector3 normal, bool shotgun)
        {
            GameObject go = null;
            try
            {
                go = TraceFactory.Create("Crack", pos, normal, shotgun ? 0.22f : 0.085f, new Color(0.1f, 0.09f, 0.08f, 0.9f));
                var core = TraceFactory.Create("Dent", pos + normal * 0.001f, normal, shotgun ? 0.08f : 0.03f, new Color(0.02f, 0.02f, 0.02f, 1f));
                if (core != null && go != null) core.transform.SetParent(go.transform, true);
                if (shotgun && go != null)
                    for (int i = 0; i < 6; i++)
                    {
                        var off = Quaternion.AngleAxis(i * 60f + 17f, normal) * Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up).normalized * (0.05f + 0.02f * (i % 3));
                        var p = TraceFactory.Create("Dent", pos + off + normal * 0.001f, normal, 0.02f, new Color(0.03f, 0.03f, 0.03f, 1f)); if (p != null) p.transform.SetParent(go.transform, true);
                    }
            }
            catch (System.Exception) { }
            return go;
        }

        // ================================================================== water
        public static void Splash(Vector3 surface, float strength)
        {
            strength = Mathf.Clamp01(strength);
            Burst(surface, Vector3.up, Mathf.RoundToInt(10 + 34 * strength), new Color(0.78f, 0.88f, 0.95f, 0.75f), new Vector2(0.02f, 0.07f), new Vector2(1.2f, 3.6f * (0.5f + strength)), new Vector2(0.35f, 0.8f), 1.3f, 38f, false, 0.12f);
            Burst(surface + Vector3.up * 0.02f, Vector3.up, 5, new Color(0.9f, 0.95f, 1f, 0.28f), new Vector2(0.18f, 0.4f), new Vector2(0.2f, 0.7f), new Vector2(0.25f, 0.5f), 0.4f, 55f, false, 0.08f);
        }
        public static void Bubbles(Vector3 at, int n) => Burst(at, Vector3.up, n, new Color(0.9f, 0.97f, 1f, 0.7f), new Vector2(0.012f, 0.035f), new Vector2(0.15f, 0.5f), new Vector2(0.3f, 0.7f), -0.35f, 25f, true, 0.06f);

        // ================================================================== bindings on a body
        static readonly Dictionary<string, Mesh> _ring = new Dictionary<string, Mesh>();
        static Mesh Ring(float R, float r, Color c)
        {
            string key = R.ToString("0.000") + "/" + r.ToString("0.000") + "/" + ColorUtility.ToHtmlStringRGB(c);
            if (_ring.TryGetValue(key, out var m) && m != null) return m;
            MansionMats.Init();
            var mb = new MeshBuilder(); mb.Set(S.Linen, c);   // MansionLit tints by vertex colour
            mb.Torus(Vector3.zero, R, r, 14, 5); mb.Torus(new Vector3(0, r * 1.6f, 0), R * 0.97f, r * 0.9f, 14, 5);
            m = mb.ToMesh("BindRing", out _); _ring[key] = m; return m;
        }
        public static Color BindColor(string material)
        {
            switch (material)
            {
                case "Rope": return new Color(0.62f, 0.5f, 0.32f); case "CurtainCord": return new Color(0.55f, 0.08f, 0.12f);
                case "ExtensionCord": return new Color(0.08f, 0.08f, 0.09f); case "Scarf": return new Color(0.38f, 0.22f, 0.52f);
                case "Tape": return new Color(0.2f, 0.2f, 0.22f);
            }
            return new Color(0.6f, 0.55f, 0.45f);
        }
        /// <summary>A ring of cord round a bone (a wrist, an ankle, the mouth), kept aligned to the limb in LateUpdate.</summary>
        public static Transform BindRing(Transform bone, Transform toward, float R, float r, Color c, Vector3 localOffset)
        {
            if (bone == null) return null;
            var go = new GameObject("bind_ring"); go.transform.SetParent(bone, false); go.transform.localPosition = localOffset;
            go.AddComponent<MeshFilter>().sharedMesh = Ring(R, r, c);
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = MansionMats.Get(S.Linen);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var al = go.AddComponent<BindAlign>(); al.Toward = toward;
            return go.transform;
        }
    }

    /// <summary>Keeps a binding ring's axis along its limb (the bone's own axes differ between rigs).</summary>
    public sealed class BindAlign : MonoBehaviour
    {
        public Transform Toward;
        void LateUpdate()
        {
            var p = transform.parent; if (p == null) return;
            Vector3 axis = Toward != null ? (Toward.position - p.position) : p.up;
            if (axis.sqrMagnitude < 1e-6f) axis = p.up;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
        }
    }
}
