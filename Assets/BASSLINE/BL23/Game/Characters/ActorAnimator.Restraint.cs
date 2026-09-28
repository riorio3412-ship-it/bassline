using UnityEngine;
using static BL23.Game.Characters.MotionKit;

namespace BL23.Game.Characters
{
    /// <summary>Restraints (motion track): bound wrists (in front / behind the back), bound ankles (feet together, hopping), a gag,
    /// straining against the bonds. Rope loops and the gag band are drawn on the body (RestraintVisuals).</summary>
    public partial class ActorAnimator
    {
        RestraintFlags _restraint;
        float _restrainStruggle, _restrainW, _hopPhase;
        RestraintVisuals _restraintVis;

        /// <summary>Bindings on this actor (None = free). Movement becomes a hop (ankles bound, standing); bound hands cannot gesture
        /// or use PhysicalActionController.</summary>
        public void SetRestraint(RestraintFlags flags)
        {
            Init();
            if (flags == _restraint) return;
            _restraint = flags;
            if (_restraintVis == null && flags != RestraintFlags.None) _restraintVis = RestraintVisuals.Ensure(_rig);
            if (_restraintVis != null) _restraintVis.Set(flags);
            if ((flags & (RestraintFlags.WristsFront | RestraintFlags.WristsBack)) != 0) { _gActive = false; if (_physicalActions != null) _physicalActions.Cancel(); }
        }
        /// <summary>How hard the bound actor strains against the bonds now (0..1).</summary>
        public void SetRestraintStruggle(float struggle01) { _restrainStruggle = Clamp01(struggle01); }
        public RestraintFlags Restraint => _restraint;
        bool WristsBound => (_restraint & (RestraintFlags.WristsFront | RestraintFlags.WristsBack)) != 0;
        bool AnklesBound => (_restraint & RestraintFlags.Ankles) != 0;

        void EvaluateRestraint(float dt, ActorPoses.Dims d)
        {
            _restrainW = Mathf.MoveTowards(_restrainW, _restraint != RestraintFlags.None ? 1f : 0f, dt * 3f);
            if (_restrainW <= 0.001f || _dead >= 0 || _beingCarried) return;
            float w = Ease(_restrainW);
            float sc = Height / 1.75f;
            float s = _conscious ? _restrainStruggle : 0f;
            float t = _time;
            var p = _rx; p.CopyFrom(_base);
            bool front = (_restraint & RestraintFlags.WristsFront) != 0, back = (_restraint & RestraintFlags.WristsBack) != 0;
            float twist = Noise(t * 3.1f, 1f) * 25f * s, roll = Noise(t * 2.3f, 2f) * 10f * s;
            if (front)
            {
                // wrists together in front of the belly (the LateIK closes the gap), straining = twisting and pulling apart
                p.SetArm(true, 24f + roll, ArmIdleOut - 16f, 48f, 76f + roll, 40f + twist, 0f);
                p.SetArm(false, 24f - roll, ArmIdleOut - 16f, 48f, 76f - roll, 40f - twist, 0f);
                p.SetHand(true, 0.45f, 0.5f, 0.55f); p.SetHand(false, 0.45f, 0.5f, 0.55f);
                BlendShoulder(p, true, 1f, 3f, 12f); BlendShoulder(p, false, 1f, 3f, 12f);
            }
            else if (back)
            {
                // wrists behind the back at the sacrum: shoulders pulled back, chest open
                p.SetArm(true, -30f + roll * 0.5f, ArmIdleOut + 8f, 82f, 44f, 30f + twist, 0f);
                p.SetArm(false, -30f - roll * 0.5f, ArmIdleOut + 8f, 82f, 44f, 30f - twist, 0f);
                p.SetHand(true, 0.4f, 0.45f, 0.5f); p.SetHand(false, 0.4f, 0.45f, 0.5f);
                BlendShoulder(p, true, 1f, -2f, -12f); BlendShoulder(p, false, 1f, -2f, -12f);
                AddBody(p, HBone.Chest, -4f, 0f, 0f);
            }
            if (s > 0.01f)
            {
                // writhing against the bonds
                AddBody(p, HBone.Spine, Noise(t * 1.7f, 3f) * 8f * s, Noise(t * 1.9f, 4f) * 14f * s, Noise(t * 2.1f, 5f) * 8f * s);
                AddBody(p, HBone.Chest, Noise(t * 2.3f, 6f) * 6f * s, Noise(t * 2.2f, 7f) * 10f * s, 0f);
                AddBody(p, HBone.Head, Noise(t * 2.6f, 8f) * 8f * s, Noise(t * 2.9f, 9f) * 14f * s, 0f);
                if (_posture != Posture.Stand)
                {
                    // sitting / lying: the legs kick and push
                    p.R[(int)HBone.LowerLegL] = p.R[(int)HBone.LowerLegL] * Quaternion.AngleAxis(Noise(t * 2.7f, 10f) * 30f * s, Vector3.right);
                    p.R[(int)HBone.LowerLegR] = p.R[(int)HBone.LowerLegR] * Quaternion.AngleAxis(Noise(t * 2.5f, 11f) * 30f * s, Vector3.right);
                }
            }
            if (AnklesBound && _posture == Posture.Stand && p.LegIK > 0.5f)
            {
                // feet together; moving = small hops with both feet (planted while on the ground, swung forward in the air)
                float cx = (d.FootL.x + d.FootR.x) * 0.5f;
                Vector3 fl = new Vector3(cx - 0.055f * sc, d.FootL.y, d.FootL.z), fr = new Vector3(cx + 0.055f * sc, d.FootR.y, d.FootR.z);
                float spd = MoveSpeedRaw;
                if (spd > 0.05f)
                {
                    float hopLen = 0.24f * sc;
                    _hopPhase += dt * spd / hopLen;
                    _hopPhase -= Mathf.Floor(_hopPhase);
                    float u = _hopPhase;
                    float a = hopLen * 0.5f * 0.4f;
                    float z = u < 0.4f ? Mathf.Lerp(a, -a, u / 0.4f) : Mathf.Lerp(-a, a, Ease((u - 0.4f) / 0.6f));
                    float lift = u < 0.4f ? 0f : Mathf.Sin((u - 0.4f) / 0.6f * Mathf.PI) * 0.06f * sc;
                    Vector3 dir = _hopDir; dir.y = 0f; if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward; dir.Normalize();
                    fl += dir * z + Vector3.up * lift; fr += dir * z + Vector3.up * lift;
                    float crouch = u < 0.4f ? Mathf.Sin(u / 0.4f * Mathf.PI) : 0f;
                    p.HipsOffset = new Vector3(p.HipsOffset.x * 0.3f, -0.05f * crouch * sc + lift * 1.1f - 0.02f * sc, p.HipsOffset.z * 0.3f);
                    AddBody(p, HBone.Spine, 6f + 6f * crouch, 0f, 0f);
                    if (!WristsBound) { BlendArm(p, true, 0.8f, 20f + 20f * crouch, ArmIdleOut + 20f, 0f, 40f); BlendArm(p, false, 0.8f, 20f + 20f * crouch, ArmIdleOut + 20f, 0f, 40f); }
                }
                p.FootL = fl; p.FootR = fr;
                p.FootRotL = ActorPose.E(0f, -3f, 0f); p.FootRotR = ActorPose.E(0f, 3f, 0f);
            }
            _base.Overlay(p, w, null, true);
        }

        float MoveSpeedRaw => (_dead < 0 && !_beingCarried && _posture == Posture.Stand && _conscious) ? _hopSpeed : 0f;

        /// <summary>After the pose: the bound wrists meet (front: before the belly; behind: at the small of the back).</summary>
        void SolveRestraintIK()
        {
            if (_restrainW <= 0.01f || !WristsBound || _dead >= 0 || _pairedRef != null) return;
            Transform hl = _rig.Bone(HBone.HandL), hr = _rig.Bone(HBone.HandR), hips = _b[0], chest = _b[(int)HBone.Chest];
            if (hl == null || hr == null) return;
            float sc = Height / 1.75f;
            bool front = (_restraint & RestraintFlags.WristsFront) != 0;
            Vector3 mid = front ? Vector3.Lerp(hips.position, chest.position, 0.25f) + transform.forward * 0.2f * sc + transform.up * 0.02f * sc
                                : Vector3.Lerp(hips.position, chest.position, 0.1f) - transform.forward * 0.16f * sc;
            float s = _restrainStruggle;
            Vector3 apart = transform.right * (0.035f + 0.02f * s * Mathf.Abs(Noise(_time * 3f, 12f))) * sc;
            float w = Ease(_restrainW);
            HandIK(true, mid - apart, null, w);
            HandIK(false, mid + apart, null, w);
        }
    }

}
