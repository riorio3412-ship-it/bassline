using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>
    /// Region-accurate wounds: decal patches parented to the hit bone, spreading blood / bruise masks on the
    /// clothing shader (8 slots, bind-pose space), bone deformation for crush damage and automatic limp arms.
    /// </summary>
    public class ActorWounds : MonoBehaviour
    {
        public sealed class Wound
        {
            public BodyRegion Region;
            public DamageType Type;
            public int Severity;
            public bool Postmortem;
            public Vector3 RestPoint;     // actor rest space
            public Vector3 RestNormal;
            public Transform Decal;
            public int Slot = -1;
            public float TargetRadius;
            public float Age;
        }

        public readonly List<Wound> List = new List<Wound>();
        public IEnumerable<Wound> PostmortemWounds { get { foreach (var w in List) if (w.Postmortem) yield return w; } }

        ActorRig _rig;
        readonly Vector4[] _slots = new Vector4[8];
        readonly float[] _slotGrow = new float[8];
        readonly float[] _slotTarget = new float[8];
        int _nextSlot;
        static readonly int[] IdSlot =
        {
            Shader.PropertyToID("_Wound0"), Shader.PropertyToID("_Wound1"), Shader.PropertyToID("_Wound2"), Shader.PropertyToID("_Wound3"),
            Shader.PropertyToID("_Wound4"), Shader.PropertyToID("_Wound5"), Shader.PropertyToID("_Wound6"), Shader.PropertyToID("_Wound7")
        };
        static readonly int IdBloodCol = Shader.PropertyToID("_BloodColor");

        // cached bind-pose surface samples
        Vector3[] _sv, _sn; int[] _sb; bool _sampled;
        readonly List<Transform> _crushed = new List<Transform>();

        public void Bind(ActorRig rig) { _rig = rig; }

        public void Clear()
        {
            foreach (var w in List) if (w.Decal != null) Destroy(w.Decal.gameObject);
            List.Clear();
            for (int i = 0; i < 8; i++) { _slots[i] = Vector4.zero; _slotGrow[i] = 0; _slotTarget[i] = 0; }
            _nextSlot = 0;
            PushSlots();
            foreach (var t in _crushed) if (t != null) t.localScale = Vector3.one;
            _crushed.Clear();
            if (_rig != null && _rig.Anim != null) _rig.Anim.ClearAutoInjury();
        }

        public void Add(BodyRegion r, DamageType t, int severity, Vector3 localPoint, bool postmortem)
        {
            if (_rig == null) _rig = GetComponent<ActorRig>();
            severity = Mathf.Clamp(severity, 1, 5);
            HBone hb = _rig.RegionBone(r);
            Transform bone = _rig.Bone(hb);
            var rest = _rig.RestBoneToRoot;
            // --- resolve the rest-space point
            // localPoint = actor-local point (rig.transform.InverseTransformPoint(hit)). It is interpreted both in the current
            // pose (live hits) and in the standing rest frame (replayed / stored wounds); the reading that lands closer to the
            // region's bone wins. Vector3.zero (or a point far from the region) picks a sensible default spot for that region.
            Vector3 restP = DefaultPoint(r);
            if (localPoint.sqrMagnitude > 1e-8f)
            {
                Vector3 a0 = rest[(int)hb].GetColumn(3);
                Vector3 segEnd = BoneSegEnd(hb, a0);
                Vector3 world = transform.TransformPoint(localPoint);
                Vector3 candA = rest[(int)hb].MultiplyPoint3x4(bone.InverseTransformPoint(world));
                Vector3 candB = localPoint;
                float dA = SegDist(candA, a0, segEnd), dB = SegDist(candB, a0, segEnd);
                float lim = 0.3f * _rig.Height / 1.75f;
                if (Mathf.Min(dA, dB) < lim) restP = dA <= dB ? candA : candB;
            }
            Vector3 restN;
            SnapToSurface(r, ref restP, out restN);
            var w = new Wound { Region = r, Type = t, Severity = severity, Postmortem = postmortem, RestPoint = restP, RestNormal = restN };

            // --- blood / bruise mask
            float bloodR = 0f, bruiseR = 0f;
            Vector3 bloodC = restP;
            switch (t)
            {
                case DamageType.Cut:
                    bloodR = 0.045f + 0.035f * severity;
                    if (r == BodyRegion.Neck) { bloodR = 0.1f + 0.06f * severity; bloodC += new Vector3(0, -0.06f * severity, 0.02f); }
                    break;
                case DamageType.Stab:
                    bloodR = 0.05f + 0.04f * severity;
                    if (r == BodyRegion.Abdomen || r == BodyRegion.Chest || r == BodyRegion.Back) { bloodR = 0.07f + 0.05f * severity; bloodC += new Vector3(0, -0.03f * severity, 0); }
                    break;
                case DamageType.Blunt:
                    bruiseR = 0.035f + 0.025f * severity;
                    if (severity >= 3 || r == BodyRegion.Head) bloodR = 0.025f + 0.02f * severity;
                    break;
                case DamageType.Crush:
                    bruiseR = 0.06f + 0.03f * severity; bloodR = 0.04f + 0.03f * severity;
                    break;
                case DamageType.Burn:
                    bruiseR = 0.05f + 0.03f * severity;
                    break;
                case DamageType.Shock:
                    bruiseR = 0.02f + 0.01f * severity;
                    break;
                case DamageType.Choke:
                    bruiseR = 0.045f;
                    break;
                case DamageType.Fall:
                    bruiseR = 0.05f + 0.02f * severity; if (r == BodyRegion.Head) bloodR = 0.04f + 0.02f * severity;
                    break;
                case DamageType.Drown:
                    _rig.SetWet(true);
                    break;
            }
            if (postmortem) bloodR *= 0.35f;
            if (bloodR > 0f) { w.Slot = Alloc(); _slots[w.Slot] = new Vector4(bloodC.x, bloodC.y, bloodC.z, 0.001f); _slotTarget[w.Slot] = bloodR; _slotGrow[w.Slot] = postmortem ? 99f : 0f; }
            if (bruiseR > 0f)
            {
                int s = Alloc();
                if (t == DamageType.Choke)
                {
                    // ring of finger bruises around the neck front
                    _slots[s] = new Vector4(restP.x - 0.03f, restP.y, restP.z, -bruiseR);
                    int s2 = Alloc();
                    _slots[s2] = new Vector4(restP.x + 0.03f, restP.y, restP.z, -bruiseR);
                }
                else _slots[s] = new Vector4(restP.x, restP.y, restP.z, -bruiseR);
            }
            if (postmortem)
                foreach (var m in _rig.Materials) m.SetColor(IdBloodCol, new Color(0.22f, 0.03f, 0.03f));
            PushSlots();

            // --- decal
            string tex = null; float dw = 0, dh = 0;
            switch (t)
            {
                case DamageType.Cut: tex = severity >= 3 || r == BodyRegion.Neck ? "gash" : "cut"; dw = 0.035f + 0.018f * severity; dh = dw * 0.45f; if (r == BodyRegion.Neck) dw *= 1.5f; break;
                case DamageType.Stab: tex = "stab"; dw = 0.02f + 0.006f * severity; dh = dw * 1.4f; break;
                case DamageType.Burn: case DamageType.Shock: tex = "burn"; dw = dh = 0.04f + 0.02f * severity; break;
                case DamageType.Crush: tex = "gash"; dw = 0.04f + 0.01f * severity; dh = dw * 0.6f; break;
                case DamageType.Blunt: if (severity >= 3) { tex = "cut"; dw = 0.03f; dh = 0.015f; } break;
            }
            if (tex != null)
            {
                var go = new GameObject("Wound_" + r + "_" + t);
                go.transform.SetParent(bone, false);
                Matrix4x4 inv = rest[(int)hb].inverse;
                Vector3 lp = inv.MultiplyPoint3x4(restP + restN * 0.0035f);
                Vector3 ln = inv.MultiplyVector(restN).normalized;
                Vector3 up = r == BodyRegion.Neck ? inv.MultiplyVector(Vector3.up) : inv.MultiplyVector(Quaternion.AngleAxis(Random.Range(-40f, 40f), restN) * Vector3.up);
                go.transform.localPosition = lp;
                go.transform.localRotation = Quaternion.LookRotation(ln, up);
                go.transform.localRotation *= Quaternion.Euler(0, 0, r == BodyRegion.Neck ? 90f : Random.Range(60f, 120f));
                var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = ToonRuntime.Patch(dw, dh, 0.0015f);
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = ToonRuntime.DecalMat(ToonRuntime.WoundTex(postmortem ? "splash" : tex));
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                w.Decal = go.transform;
            }

            // --- deformation / behaviour
            if (t == DamageType.Crush) Crush(hb, severity);
            if (t == DamageType.Blunt && r == BodyRegion.Head && severity >= 3) Crush(HBone.Head, 1);
            if (!postmortem && _rig.Anim != null && severity >= 2 && (t == DamageType.Stab || t == DamageType.Cut || t == DamageType.Crush || t == DamageType.Blunt && severity >= 3))
            {
                bool l = r == BodyRegion.ShoulderL || r == BodyRegion.ArmL || r == BodyRegion.HandL && t == DamageType.Crush;
                bool rr = r == BodyRegion.ShoulderR || r == BodyRegion.ArmR || r == BodyRegion.HandR && t == DamageType.Crush;
                if (l || rr) _rig.Anim.AddAutoInjury(l, rr);
            }
            List.Add(w);
        }

        void Crush(HBone hb, int severity)
        {
            Transform b = _rig.Bone(hb);
            float k = Mathf.Clamp01(severity / 4f);
            Vector3 sc;
            switch (hb)
            {
                case HBone.Head: sc = new Vector3(1f + 0.06f * k, 1f - 0.12f * k, 1f - 0.05f * k); break;
                case HBone.Chest: sc = new Vector3(1f + 0.05f * k, 1f - 0.04f * k, 1f - 0.22f * k); break;
                case HBone.HandL: case HBone.HandR: sc = new Vector3(1f + 0.25f * k, 1f - 0.2f * k, 1f - 0.45f * k); break;
                case HBone.FootL: case HBone.FootR: sc = new Vector3(1f + 0.3f * k, 1f - 0.45f * k, 1f + 0.05f * k); break;
                default: sc = new Vector3(1f + 0.12f * k, 1f - 0.1f * k, 1f - 0.2f * k); break;
            }
            b.localScale = sc;
            _crushed.Add(b);
            // undo the scale on skeleton children so the rest of the body keeps its shape
            for (int i = 0; i < b.childCount; i++)
            {
                var c = b.GetChild(i);
                bool skel = false;
                foreach (var bb in _rig.Bones) if (bb == c) { skel = true; break; }
                if (!skel) continue;
                c.localScale = new Vector3(1f / sc.x, 1f / sc.y, 1f / sc.z);
                _crushed.Add(c);
            }
        }

        int Alloc()
        {
            int s = _nextSlot % 8;
            _nextSlot++;
            return s;
        }

        void PushSlots()
        {
            if (_rig == null) return;
            foreach (var m in _rig.Materials)
                for (int i = 0; i < 8; i++) m.SetVector(IdSlot[i], _slots[i]);
        }

        void Update()
        {
            bool dirty = false;
            for (int i = 0; i < 8; i++)
            {
                if (_slots[i].w <= 0f || _slotTarget[i] <= 0f) continue;
                if (_slots[i].w < _slotTarget[i])
                {
                    _slotGrow[i] += Time.deltaTime;
                    // blood soaks outward quickly at first, then slowly (strong readable stain within ~10 s)
                    float g = _slotGrow[i] >= 99f ? 1f : 1f - Mathf.Exp(-_slotGrow[i] / 3.5f);
                    float r = Mathf.Lerp(_slotTarget[i] * 0.35f, _slotTarget[i], g);
                    if (Mathf.Abs(r - _slots[i].w) > 0.0005f) { _slots[i].w = r; dirty = true; }
                }
            }
            if (dirty) PushSlots();
        }

        /// <summary>Instantly completes blood spreading (e.g. for bodies found later).</summary>
        public void Settle()
        {
            for (int i = 0; i < 8; i++) if (_slotTarget[i] > 0f && _slots[i].w > 0f) _slots[i].w = _slotTarget[i];
            PushSlots();
        }

        Vector3 BoneSegEnd(HBone hb, Vector3 a0)
        {
            var an = _rig.Anim;
            switch (hb)
            {
                case HBone.Head: return _rig.transform.InverseTransformPoint(_rig.HeadTopAnchor.position);
                case HBone.Neck: return an.RestPos(HBone.Head);
                case HBone.Chest: return an.RestPos(HBone.Neck);
                case HBone.Spine: return an.RestPos(HBone.Chest);
                case HBone.UpperArmL: return an.RestPos(HBone.LowerArmL);
                case HBone.UpperArmR: return an.RestPos(HBone.LowerArmR);
                case HBone.LowerArmL: return an.RestPos(HBone.HandL);
                case HBone.LowerArmR: return an.RestPos(HBone.HandR);
                case HBone.HandL: return _rig.HandTipRestL;
                case HBone.HandR: return _rig.HandTipRestR;
                case HBone.UpperLegL: return an.RestPos(HBone.LowerLegL);
                case HBone.UpperLegR: return an.RestPos(HBone.LowerLegR);
                case HBone.FootL: return an.RestPos(HBone.ToeL);
                case HBone.FootR: return an.RestPos(HBone.ToeR);
            }
            return a0 + Vector3.up * 0.1f;
        }

        static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a; float l2 = ab.sqrMagnitude;
            float t = l2 > 1e-10f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2) : 0f;
            return (p - (a + ab * t)).magnitude;
        }

        Vector3 DefaultPoint(BodyRegion r)
        {
            var a = _rig.Anim;
            Vector3 J(HBone b) => a.RestPos(b);
            float s = _rig.Height / 1.75f;
            switch (r)
            {
                case BodyRegion.Head: return Vector3.Lerp(J(HBone.Head), _rig.transform.InverseTransformPoint(_rig.HeadTopAnchor.position), 0.62f) + new Vector3(0.02f, 0, 0.1f * s);
                case BodyRegion.Neck: return Vector3.Lerp(J(HBone.Neck), J(HBone.Head), 0.55f) + new Vector3(-0.012f, 0, 0.06f * s);
                case BodyRegion.Chest: return J(HBone.Chest) + new Vector3(-0.04f, 0.1f * s, 0.15f * s);
                case BodyRegion.Abdomen: return J(HBone.Spine) + new Vector3(0.03f, -0.02f, 0.15f * s);
                case BodyRegion.Back: return J(HBone.Chest) + new Vector3(0.03f, 0.06f * s, -0.15f * s);
                case BodyRegion.ShoulderL: return J(HBone.UpperArmL) + new Vector3(0, 0.02f, 0.05f * s);
                case BodyRegion.ShoulderR: return J(HBone.UpperArmR) + new Vector3(0, 0.02f, 0.05f * s);
                case BodyRegion.ArmL: return Vector3.Lerp(J(HBone.LowerArmL), J(HBone.HandL), 0.4f) + new Vector3(0, 0, 0.05f);
                case BodyRegion.ArmR: return Vector3.Lerp(J(HBone.LowerArmR), J(HBone.HandR), 0.4f) + new Vector3(0, 0, 0.05f);
                case BodyRegion.HandL: return Vector3.Lerp(J(HBone.HandL), _rig.HandTipRestL, 0.35f) + new Vector3(-0.03f, 0, 0);
                case BodyRegion.HandR: return Vector3.Lerp(J(HBone.HandR), _rig.HandTipRestR, 0.35f) + new Vector3(0.03f, 0, 0);
                case BodyRegion.LegL: return Vector3.Lerp(J(HBone.UpperLegL), J(HBone.LowerLegL), 0.45f) + new Vector3(0, 0, 0.09f * s);
                case BodyRegion.LegR: return Vector3.Lerp(J(HBone.UpperLegR), J(HBone.LowerLegR), 0.45f) + new Vector3(0, 0, 0.09f * s);
                case BodyRegion.FootL: return J(HBone.FootL) + new Vector3(0, 0.02f, 0.07f * s);
                case BodyRegion.FootR: return J(HBone.FootR) + new Vector3(0, 0.02f, 0.07f * s);
            }
            return J(HBone.Chest);
        }

        static HBone[] RegionBones(BodyRegion r)
        {
            switch (r)
            {
                case BodyRegion.Head: return new[] { HBone.Head };
                case BodyRegion.Neck: return new[] { HBone.Neck, HBone.Head, HBone.Chest };
                case BodyRegion.Chest: case BodyRegion.Back: return new[] { HBone.Chest, HBone.Spine };
                case BodyRegion.Abdomen: return new[] { HBone.Spine, HBone.Hips, HBone.Chest };
                case BodyRegion.ShoulderL: return new[] { HBone.UpperArmL, HBone.ShoulderL };
                case BodyRegion.ShoulderR: return new[] { HBone.UpperArmR, HBone.ShoulderR };
                case BodyRegion.ArmL: return new[] { HBone.UpperArmL, HBone.LowerArmL };
                case BodyRegion.ArmR: return new[] { HBone.UpperArmR, HBone.LowerArmR };
                case BodyRegion.HandL: return new[] { HBone.HandL };
                case BodyRegion.HandR: return new[] { HBone.HandR };
                case BodyRegion.LegL: return new[] { HBone.UpperLegL, HBone.LowerLegL };
                case BodyRegion.LegR: return new[] { HBone.UpperLegR, HBone.LowerLegR };
                case BodyRegion.FootL: return new[] { HBone.FootL, HBone.ToeL, HBone.LowerLegL };
                case BodyRegion.FootR: return new[] { HBone.FootR, HBone.ToeR, HBone.LowerLegR };
            }
            return new[] { HBone.Chest };
        }

        void Sample()
        {
            _sampled = true;
            var sv = new List<Vector3>(); var sn = new List<Vector3>(); var sb = new List<int>();
            foreach (var smr in _rig.Skins)
            {
                if (smr == null || smr.sharedMesh == null || !smr.sharedMesh.isReadable) continue;
                var mesh = smr.sharedMesh;
                var v = mesh.vertices; var n = mesh.normals; var bw = mesh.boneWeights;
                var bones = smr.bones;
                // map mesh bone index -> HBone index
                var map = new int[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    map[i] = -1;
                    for (int k = 0; k < _rig.Bones.Length; k++) if (_rig.Bones[k] == bones[i]) { map[i] = (int)ActorSkeleton.Core((HBone)k); break; }
                }
                int step = Mathf.Max(1, v.Length / 25000);
                for (int i = 0; i < v.Length; i += step)
                {
                    int hb = bw.Length > i && bw[i].boneIndex0 < map.Length ? map[bw[i].boneIndex0] : -1;
                    if (hb < 0) continue;
                    sv.Add(v[i]); sn.Add(n.Length > i ? n[i] : Vector3.up); sb.Add(hb);
                }
            }
            _sv = sv.ToArray(); _sn = sn.ToArray(); _sb = sb.ToArray();
        }

        void SnapToSurface(BodyRegion r, ref Vector3 p, out Vector3 n)
        {
            n = Vector3.forward;
            if (!_sampled) Sample();
            if (_sv == null || _sv.Length == 0) return;
            var bones = RegionBones(r);
            float best = float.MaxValue; int bi = -1;
            for (int i = 0; i < _sv.Length; i++)
            {
                bool ok = false;
                foreach (var b in bones) if (_sb[i] == (int)b) { ok = true; break; }
                if (!ok) continue;
                float d = (_sv[i] - p).sqrMagnitude;
                // prefer front/outward facing samples for back/front regions
                if (r == BodyRegion.Back && _sn[i].z > 0) d += 0.01f;
                if ((r == BodyRegion.Chest || r == BodyRegion.Abdomen || r == BodyRegion.Neck) && _sn[i].z < 0) d += 0.01f;
                if (d < best) { best = d; bi = i; }
            }
            if (bi >= 0) { p = _sv[bi]; n = _sn[bi].normalized; }
        }
    }
}
