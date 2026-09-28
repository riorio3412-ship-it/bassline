using System.Collections.Generic;
using BL23.Game.Characters;
using BL23.Game.Mansion;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game
{
    /// <summary>
    /// The house's moths find the dead. A body that has lain for a while (10 clock-minutes) draws one to three dusty moths:
    /// they drift in slow, uneven loops over it, now and then settle on the chest, a hand or the face, open and close their
    /// wings there for a few seconds, and lift off again. Deterministic per body (its id); drawn only while the body is drawn,
    /// within 14 m of the camera, and never during a replay (the person is alive there). Scaled time: they stop when the
    /// game is paused. Presentation only; added and removed by <see cref="GoreForensics"/>.
    /// </summary>
    [DefaultExecutionOrder(320)]   // after the ragdoll (300): landing spots are this frame's
    public sealed class GoreMoths : MonoBehaviour
    {
        public string ActorId;
        sealed class Moth
        {
            public Transform T, WL, WR; public float Phase, R, H, W, Next, LandAt = -1f, LandUntil = -1f, Seed; public HBone Spot; public Vector3 From;
        }
        ActorView _view; readonly List<Moth> _m = new List<Moth>(); System.Random _rnd; GameObject _root;
        static Material _mat; static Mesh _wing, _wingR;
        public int Count => _m.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _mat = null; _wing = null; _wingR = null; }

        void OnDestroy() { if (_root != null) Destroy(_root); }

        void LateUpdate()
        {
            try { Step(); }
            catch (System.Exception e) { Debug.LogWarning("[Gore] moths " + ActorId + ": " + e.Message); enabled = false; if (_root != null) _root.SetActive(false); }
        }

        void Step()
        {
            if (_view == null) _view = GetComponent<ActorView>();
            var rig = _view != null ? _view.Rig : null; if (rig == null || !rig.HasBone(HBone.Chest)) return;
            var cam = Camera.main;
            bool on = !_view.ReplayDriven && SkinsOn(rig) && cam != null && (cam.transform.position - rig.Bone(HBone.Chest).position).sqrMagnitude < 14f * 14f;
            if (_root != null && _root.activeSelf != on) _root.SetActive(on);
            if (!on) return;
            if (_root == null) Build(rig);
            float t = Time.time, dt = Time.deltaTime;
            var hips = rig.Bone(HBone.Hips).position; var chest = rig.Bone(HBone.Chest).position;
            var centre = (hips + chest) * 0.5f;
            foreach (var m in _m)
            {
                if (m.T == null) continue;
                Vector3 target; bool landed = false;
                if (m.LandAt < 0f && t >= m.Next)
                {
                    // choose where to settle: the chest, a hand, the face
                    m.Spot = PickSpot(rig, m); m.LandAt = t; m.From = m.T.position; m.LandUntil = t + 0.9f + Rr(3f, 6.5f);
                }
                if (m.LandAt >= 0f)
                {
                    var spot = SpotPos(rig, m.Spot, m.Seed);
                    float u = Mathf.Clamp01((t - m.LandAt) / 0.9f); float e = u * u * (3f - 2f * u);
                    if (t < m.LandUntil)
                    {
                        target = Vector3.Lerp(m.From, spot, e) + Vector3.up * (0.08f * Mathf.Sin(u * Mathf.PI));
                        landed = u >= 1f;
                    }
                    else
                    {
                        // lift off, back into the loop
                        m.LandAt = -1f; m.Next = t + Rr(7f, 15f); target = m.T.position + Vector3.up * 0.05f;
                    }
                }
                else
                {
                    float a = t * m.W + m.Phase;
                    target = centre + new Vector3(Mathf.Cos(a) * m.R, m.H + 0.12f * Mathf.Sin(a * 2.3f + m.Seed), Mathf.Sin(a * 0.87f) * m.R * 0.75f)
                           + new Vector3(Mathf.PerlinNoise(t * 1.7f, m.Seed) - 0.5f, (Mathf.PerlinNoise(m.Seed, t * 1.3f) - 0.5f) * 0.6f, Mathf.PerlinNoise(t * 1.5f + 3f, m.Seed) - 0.5f) * 0.18f;
                }
                var prev = m.T.position; var pos = landed ? target : Vector3.Lerp(prev, target, 1f - Mathf.Exp(-6f * dt));
                m.T.position = pos;
                var vel = pos - prev; vel.y *= 0.3f;
                if (!landed && vel.sqrMagnitude > 1e-8f) m.T.rotation = Quaternion.Slerp(m.T.rotation, Quaternion.LookRotation(vel.normalized, Vector3.up), 1f - Mathf.Exp(-8f * dt));
                float flap = landed ? Mathf.Lerp(0.55f, 1f, 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 0.9f + m.Seed)) : Mathf.Lerp(0.18f, 1f, Mathf.Abs(Mathf.Sin(t * Mathf.PI * 17f + m.Seed)));
                if (m.WL != null) m.WL.localScale = new Vector3(flap, 1f, 1f); if (m.WR != null) m.WR.localScale = new Vector3(flap, 1f, 1f);
            }
        }

        static bool SkinsOn(ActorRig rig) { foreach (var s in rig.Skins) if (s != null && s.enabled) return true; return false; }

        float Rr(float a, float b) => a + (float)_rnd.NextDouble() * (b - a);

        static readonly HBone[] Spots = { HBone.Chest, HBone.Chest, HBone.HandL, HBone.HandR, HBone.Head };
        HBone PickSpot(ActorRig rig, Moth m)
        {
            for (int k = 0; k < 4; k++) { var b = Spots[_rnd.Next(Spots.Length)]; if (rig.HasBone(b) && BoneShown(rig, b)) return b; }
            return HBone.Chest;
        }

        /// <summary>A severed part's bones are still there (collapsed, or carrying no mesh): never land on what is gone.</summary>
        bool BoneShown(ActorRig rig, HBone b)
        {
            var t = rig.Bone(b); if (t == null || t.lossyScale.sqrMagnitude < 0.01f) return false;
            var gt = GetComponent<GoreTorso>(); int mask = gt != null ? gt.Mask : 0; if (mask == 0) return true;
            bool Gone(SeverPart p) => (mask & GoreMeshCut.Bit(p)) != 0;
            switch (b)
            {
                case HBone.Head: return !Gone(SeverPart.Head);
                case HBone.HandL: return !Gone(SeverPart.HandL) && !Gone(SeverPart.ArmL);
                case HBone.HandR: return !Gone(SeverPart.HandR) && !Gone(SeverPart.ArmR);
            }
            return true;
        }

        /// <summary>A point just on the surface near a bone: the chest's front, the back of a hand, the cheek.</summary>
        static Vector3 SpotPos(ActorRig rig, HBone b, float seed)
        {
            var t = rig.Bone(b); if (t == null) return rig.transform.position;
            float S = rig.Height > 0.5f ? rig.Height / 1.75f : 1f;
            switch (b)
            {
                case HBone.Head: return rig.HeadCenterWorld() + Vector3.up * 0.1f * S + rig.transform.right * 0.03f * Mathf.Sign(Mathf.Sin(seed));
                case HBone.HandL: case HBone.HandR: return t.position + Vector3.up * 0.035f * S;
                default: return t.position + Vector3.up * 0.13f * S;
            }
        }

        void Build(ActorRig rig)
        {
            _rnd = new System.Random((int)(GoreWorld.Hash((ActorId ?? name) + "|moths") & 0x7FFFFFFF));
            _root = new GameObject("GoreMoths"); _root.transform.SetParent(transform.parent, false);
            if (_mat == null)
            {
                MansionMats.Init();
                _mat = MansionMats.NewLit("GoreMoth", null, 1f, 0.95f);
                _mat.SetColor("_BaseColor", new Color(0.38f, 0.33f, 0.27f, 1f)); if (_mat.HasProperty("_Cull")) _mat.SetFloat("_Cull", 0f);
            }
            if (_wing == null) { _wing = WingMesh(-1f); _wingR = WingMesh(1f); }
            int n = 1 + _rnd.Next(3);
            var chest = rig.Bone(HBone.Chest).position;
            for (int i = 0; i < n; i++)
            {
                var m = new Moth { Phase = Rr(0f, 6.28f), R = Rr(0.3f, 0.6f), H = Rr(0.35f, 0.9f), W = Rr(0.45f, 0.8f) * (_rnd.Next(2) == 0 ? 1f : -1f), Seed = Rr(0f, 100f) };
                m.Next = Time.time + Rr(3f, 12f);
                var go = new GameObject("Moth"); go.transform.SetParent(_root.transform, false); go.transform.position = chest + Vector3.up * m.H;
                go.transform.localScale = Vector3.one * 1.6f;
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name = "Body"; Destroy(body.GetComponent<Collider>());
                body.transform.SetParent(go.transform, false); body.transform.localRotation = Quaternion.Euler(90, 0, 0); body.transform.localScale = new Vector3(0.0045f, 0.007f, 0.0045f);
                var br = body.GetComponent<MeshRenderer>(); br.sharedMaterial = _mat; br.shadowCastingMode = ShadowCastingMode.Off;
                m.WL = Wing(go.transform, _wing, "WingL"); m.WR = Wing(go.transform, _wingR, "WingR");
                m.T = go.transform; _m.Add(m);
            }
        }

        static Transform Wing(Transform parent, Mesh mesh, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = _mat; mr.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>A rounded, dusty forewing and hindwing (a five-point fan), pivoting on the body's axis.</summary>
        static Mesh WingMesh(float s)
        {
            var v = new[] { new Vector3(0, 0, 0.004f), new Vector3(s * 0.012f, 0.0005f, 0.007f), new Vector3(s * 0.022f, 0.001f, 0.001f), new Vector3(s * 0.017f, 0.0005f, -0.008f), new Vector3(s * 0.006f, 0, -0.009f), new Vector3(0, 0, -0.005f) };
            var c = new Color(0.9f, 0.86f, 0.8f, 1f); var col = new[] { c, c * 0.92f, c * 0.8f, c * 0.85f, c * 0.95f, c };
            var m = new Mesh { name = "GoreMothWing" }; m.vertices = v; m.colors = col;
            var uv = new Vector2[v.Length]; for (int i = 0; i < v.Length; i++) uv[i] = new Vector2(v[i].x * 20f + 0.5f, v[i].z * 20f + 0.5f); m.uv = uv;
            m.triangles = s > 0 ? new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5 } : new[] { 0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 5, 4 };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
