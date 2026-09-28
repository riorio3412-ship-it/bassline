using UnityEngine;
using static BL23.Game.Characters.MotionKit;

namespace BL23.Game.Characters
{
    /// <summary>Paired prolonged kills (motion track): strangling, ligatures, smothering, drowning. The victim's body is aligned to
    /// the attacker, both are IK-coupled at the contact points (hands on the throat, a cord behind the neck, a fist in the hair, a
    /// pillow on the face, the victim's hands clawing at the attacker's forearms), and the struggle the violence track feeds each
    /// tick drives thrashing, kicking, sinking to the knees and finally limp limbs, with a gasping face that reddens then pales.
    /// Presentation only (the kernel decides the outcome and the timing).</summary>
    public partial class ActorAnimator
    {
        sealed class PairedState
        {
            public PairedKind Kind;
            public ActorAnimator A, V;
            public float T, Struggle = 1f, Shown = 1f, LimpT, Surface = -1f, Sink;
            public float SeedA, SeedV;
        }
        PairedState _pairedRef;
        float _pairAlignW;
        readonly ActorPose _pp = new ActorPose();

        /// <summary>Start a paired act. Call it on either actor; the partner is configured with the other role. struggle01 = how
        /// hard the victim fights now (1 = thrashing, 0 = limp).</summary>
        public void BeginPaired(PairedKind kind, PairedRole role, ActorAnimator partner, float struggle01 = 1f)
        {
            Init();
            if (partner == null || partner == this || kind == PairedKind.None) return;
            partner.Init();
            var a = role == PairedRole.Attacker ? this : partner;
            var v = role == PairedRole.Attacker ? partner : this;
            if (a._pairedRef != null) a.EndPaired();
            if (v._pairedRef != null) v.EndPaired();
            var st = new PairedState { Kind = kind, A = a, V = v, Struggle = Clamp01(struggle01), Shown = Clamp01(struggle01), SeedA = a._seed, SeedV = v._seed + 3.7f };
            st.Surface = kind == PairedKind.Drown ? 0.55f : kind == PairedKind.Smother ? 0f : -1f;
            foreach (var x in new[] { a, v })
            {
                x._pairedRef = st; x._pairAlignW = 0f;
                x._atk.Active = false; x._stag.Active = false; x._hit.Active = false; x._startle.Active = false; x._def.Active = false;
                x._gActive = false; x._aActive = false; x._trans = null;
                x.BlendFromCurrentBones(0.3f);
                if (x._physicalActions != null) x._physicalActions.Cancel();
            }
            if (v._rig != null && v._rig.Face != null) { v._rig.Face.PushTransient(Expr.Choke, 1f, 0.6f); }
            if (a._rig != null && a._rig.Face != null) a._rig.Face.PushTransient(Expr.Angry, 1f, 1.5f);
        }

        /// <summary>Victim struggle intensity 0..1 each tick (from the kernel assault state). 0 = unconscious / limp.</summary>
        public void SetPairedIntensity(float struggle01) { if (_pairedRef != null) _pairedRef.Struggle = Clamp01(struggle01); }

        /// <summary>Height of the surface the act happens at, in metres above the attacker's feet: the water surface for Drown (bath
        /// 0.55, basin 0.85, fountain 0.5), the bed top for Smother (0 = on the floor).</summary>
        public void SetPairedSurface(float metres) { if (_pairedRef != null) _pairedRef.Surface = Mathf.Max(0f, metres); }

        /// <summary>End the paired act on both actors (release, interruption or death); each blends back to its kernel state
        /// (a dead or unconscious victim collapses from where it was held).</summary>
        public void EndPaired()
        {
            var st = _pairedRef;
            if (st == null) return;
            foreach (var x in new[] { st.A, st.V })
            {
                if (x == null) continue;
                x._pairedRef = null;
                x.BlendFromCurrentBones(x == st.V ? 0.45f : 0.35f);
                if (x._rig != null)
                {
                    bool deadOrOut = x == st.V && (x._dead >= 0 || !x._conscious || st.Shown < 0.05f);
                    x._rig.SetStrain(0f, deadOrOut ? 0f : 0f, deadOrOut ? Mathf.Max(0.35f, st.LimpT > 0f ? 0.8f : 0.4f) : 0f);
                }
            }
        }

        public PairedKind PairedActive => _pairedRef != null ? _pairedRef.Kind : PairedKind.None;
        public PairedRole PairedRoleNow => _pairedRef != null && _pairedRef.V == this ? PairedRole.Victim : PairedRole.Attacker;
        public ActorAnimator PairedPartner => _pairedRef == null ? null : _pairedRef.A == this ? _pairedRef.V : _pairedRef.A;

        /// <summary>Where the victim's root stands relative to the attacker's root for a paired kind (attacker-local position in
        /// metres for a 1.75 m attacker, and yaw in degrees). The animator aligns the bodies itself; the kernel may use this to
        /// place them.</summary>
        public static void PairedPlacement(PairedKind kind, out Vector3 victimLocalPos, out float victimLocalYaw)
        {
            switch (kind)
            {
                case PairedKind.Strangle: victimLocalPos = new Vector3(0f, 0f, 0.53f); victimLocalYaw = 180f; break;
                case PairedKind.LigatureFront: victimLocalPos = new Vector3(0f, 0f, 0.55f); victimLocalYaw = 180f; break;
                case PairedKind.GarroteRear: victimLocalPos = new Vector3(0f, 0f, 0.38f); victimLocalYaw = 0f; break;
                case PairedKind.Smother: victimLocalPos = new Vector3(0.66f, 0f, 0.46f); victimLocalYaw = 90f; break;
                case PairedKind.Drown: victimLocalPos = new Vector3(0f, 0f, 0.4f); victimLocalYaw = 0f; break;
                default: victimLocalPos = Vector3.zero; victimLocalYaw = 0f; break;
            }
        }

        // ================================================================ evaluation
        /// <summary>Paired pose (after every other layer: the act overrides them). Attacker and victim each pose their own body;
        /// the contact IK runs in LateIK when both are posed.</summary>
        void EvaluatePaired(float dt, ActorPoses.Dims d)
        {
            var st = _pairedRef;
            if (st == null) return;
            if (st.A == null || st.V == null || st.A._pairedRef != st || st.V._pairedRef != st) { _pairedRef = null; return; }
            bool victim = st.V == this;
            if (victim)
            {
                // the victim owns the clock (one tick per frame)
                st.T += dt;
                st.Shown = Mathf.MoveTowards(st.Shown, st.Struggle, dt * (st.Struggle < st.Shown ? 0.5f : 1.5f));
                if (st.Shown < 0.04f) st.LimpT += dt; else st.LimpT = 0f;
                // sinking: the fight goes out of the legs as the struggle weakens (standing kinds)
                float wantSink = Clamp01((0.55f - st.Shown) / 0.45f);
                st.Sink = Mathf.MoveTowards(st.Sink, wantSink, dt * 0.35f);
                FaceStrain(st);
            }
            _pairAlignW = Mathf.MoveTowards(_pairAlignW, 1f, dt / 0.4f);
            float sc = Height / 1.75f;
            var p = _pp; p.CopyFrom(_base);
            float s = st.Shown;
            float t = st.T;
            float seize = Win(t, 0f, 0.45f);
            switch (st.Kind)
            {
                case PairedKind.Smother:
                    if (victim) VictimSmother(p, d, s, t, sc); else AttackerSmother(p, d, s, t, sc, st);
                    break;
                case PairedKind.Drown:
                    if (victim) VictimDrown(p, d, s, t, sc, st); else AttackerDrown(p, d, s, t, sc, st);
                    break;
                default:
                    if (victim) VictimStanding(p, d, s, t, sc, st); else AttackerStanding(p, d, s, t, sc, st);
                    break;
            }
            if (victim) AlignVictim(p, st);
            _base.Overlay(p, Ease(Mathf.Min(_pairAlignW * 1.5f, 1f)), null, true);
        }

        void FaceStrain(PairedState st)
        {
            if (_rig == null) return;
            float s = st.Shown;
            bool alive = _dead < 0;
            float gasp = alive && s > 0.04f ? 0.55f + 0.45f * s : 0f;
            float flush = alive ? Clamp01(st.T / 14f) * (s > 0.1f ? 1f : 0.6f) : 0f;
            float pale = Clamp01(st.LimpT / 6f) * 0.9f + (alive ? 0f : 0.6f);
            if (st.Kind == PairedKind.Drown) flush *= 0.6f;
            _rig.SetStrain(gasp, Mathf.Max(0f, flush - pale), pale);
            if (st.A != null && st.A._rig != null) st.A._rig.SetStrain(0f, 0.25f * Clamp01(st.T / 8f) * (0.4f + 0.6f * s), 0f);
        }

        /// <summary>Moves the victim's body so its root sits at the paired placement relative to the attacker (presentation only).</summary>
        void AlignVictim(ActorPose p, PairedState st)
        {
            var a = st.A;
            PairedPlacement(st.Kind, out var pos, out var yaw);
            float asc = a.Height / 1.75f;
            pos *= asc;
            if (st.Kind == PairedKind.Smother && st.Surface > 0f) pos.y = st.Surface;
            Vector3 w = a.transform.TransformPoint(pos);
            Quaternion rw = a.transform.rotation * Quaternion.Euler(0f, yaw, 0f);
            Vector3 off = transform.InverseTransformPoint(w);
            Quaternion rot = Quaternion.Inverse(transform.rotation) * rw;
            float k = Ease(_pairAlignW);
            MotionKit.ShiftRoot(p, off * k, Quaternion.Slerp(Quaternion.identity, rot, k));
        }

        // ---------------------------------------------------------------- standing kinds: strangle, garrote, ligature
        void VictimStanding(ActorPose p, ActorPoses.Dims d, float s, float t, float sc, PairedState st)
        {
            float seed = st.SeedV;
            float thrash = s * (0.6f + 0.4f * Mathf.Abs(Noise(t * 0.7f, seed)));
            float sink = st.Sink;
            bool rear = st.Kind == PairedKind.GarroteRear;
            float limp = Win(st.LimpT, 0f, 1.2f);
            // legs: buckling toward a kneel as the struggle weakens; stamping / kicking while strong
            if (sink < 0.5f)
            {
                MotionKit.Stance(p, d, new Vector3(-0.04f, 0f, 0.02f) * sc, new Vector3(0.04f, 0f, -0.03f) * sc, new Vector3(0f, -0.04f * sc - 0.22f * sc * sink * 2f, rear ? -0.04f * sc : 0.03f * sc));
                float kL = Mathf.Max(0f, Noise(t * 2.6f, seed + 1f)) * thrash, kR = Mathf.Max(0f, Noise(t * 2.4f, seed + 2f)) * thrash;
                p.FootL += new Vector3(0f, 0.12f * kL, (rear ? -0.08f : 0.1f) * kL) * sc;
                p.FootR += new Vector3(0f, 0.12f * kR, (rear ? -0.06f : 0.12f) * kR) * sc;
                // on the toes: pulled up by the neck
                float toes = s * 0.7f;
                p.FootRotL = ActorPose.E(22f * toes, -6f, 0f); p.FootRotR = ActorPose.E(22f * toes, 6f, 0f);
                p.HipsOffset += new Vector3(0f, 0.03f * toes * sc, 0f);
            }
            else
            {
                var kneel = _rx2; kneel.Reset();
                ActorPoses.PostureBase(kneel, d, Posture.Kneel, _time, "");
                float k2 = Ease((sink - 0.5f) * 2f);
                p.Blend(p, kneel, k2);
            }
            // torso: twisting and arching against the hold, then slack
            AddBody(p, HBone.Hips, 0f, Noise(t * 1.6f, seed + 3f) * 14f * thrash, Noise(t * 1.9f, seed + 4f) * 8f * thrash);
            AddBody(p, HBone.Spine, (rear ? -12f : -6f) * s + 14f * limp, Noise(t * 1.8f, seed + 5f) * 12f * thrash, Noise(t * 2.1f, seed + 6f) * 8f * thrash);
            AddBody(p, HBone.Chest, (rear ? -10f : -4f) * s + 10f * limp, Noise(t * 2.2f, seed + 7f) * 8f * thrash, 0f);
            // neck forced back (manual strangle pushes the head back; the cord pulls it back), head lolls when limp
            AddBody(p, HBone.Neck, -18f * (1f - limp) + 20f * limp, Noise(t * 3f, seed + 8f) * 10f * thrash, 12f * limp);
            AddBody(p, HBone.Head, -14f * (1f - limp) + 18f * limp, Noise(t * 3.3f, seed + 9f) * 14f * thrash, 10f * limp);
            // arms: up at the attacker's forearms (IK in LateIK); limp arms hang
            float up = (1f - limp) * Win(t, 0f, 0.3f);
            p.SetArm(true, Mathf.Lerp(8f, 72f, up), ArmIdleOut + 10f * up, 30f * up, Mathf.Lerp(12f, 88f, up), 20f, 0f);
            p.SetArm(false, Mathf.Lerp(8f, 70f, up), ArmIdleOut + 10f * up, 30f * up, Mathf.Lerp(14f, 90f, up), 20f, 0f);
            p.SetHand(true, 0.55f * up + 0.3f, 0.6f * up + 0.25f, 0.7f * up + 0.3f); p.SetHand(false, 0.55f * up + 0.3f, 0.6f * up + 0.25f, 0.7f * up + 0.3f);
        }

        void AttackerStanding(ActorPose p, ActorPoses.Dims d, float s, float t, float sc, PairedState st)
        {
            float seed = st.SeedA;
            float sink = st.Sink;
            bool rear = st.Kind == PairedKind.GarroteRear;
            float effort = 0.4f + 0.6f * s;
            // braced stance: one foot back, knees bent; following the victim down as it sinks
            MotionKit.Stance(p, d, new Vector3(-0.05f, 0f, rear ? -0.06f : 0.08f) * sc, new Vector3(0.06f, 0f, rear ? -0.22f : -0.16f) * sc, new Vector3(0f, (-0.05f - 0.3f * sink) * sc, (rear ? -0.08f : 0.04f) * sc));
            AddBody(p, HBone.Hips, 6f + 20f * sink, Noise(t * 1.4f, seed) * 5f * s, 0f);
            AddBody(p, HBone.Spine, (rear ? -8f : 4f) + 18f * sink, Noise(t * 1.7f, seed + 1f) * 6f * s, Noise(t * 1.3f, seed + 2f) * 5f * s);
            AddBody(p, HBone.Chest, (rear ? -6f : 3f) + 10f * sink, 0f, 0f);
            AddBody(p, HBone.Neck, -6f);
            AddBody(p, HBone.Head, rear ? 10f : 4f, 0f, rear ? 12f : 0f);
            // arms forward toward the throat (the LateIK lands the hands), shoulders raised with the pressure
            p.SetArm(false, 72f + 10f * sink, rear ? 8f : -4f, 40f, rear ? 90f : 42f, 30f, -20f);
            p.SetArm(true, 72f + 10f * sink, rear ? 8f : -4f, 40f, rear ? 90f : 42f, 30f, -20f);
            BlendShoulder(p, false, 1f, 8f * effort + Noise(t * 9f, seed + 3f) * 2f, 10f); BlendShoulder(p, true, 1f, 8f * effort + Noise(t * 9f, seed + 4f) * 2f, 10f);
            p.SetHand(true, 0.75f, 0.8f, 0.85f); p.SetHand(false, 0.75f, 0.8f, 0.85f);
        }

        // ---------------------------------------------------------------- smother (victim on its back)
        void VictimSmother(ActorPose p, ActorPoses.Dims d, float s, float t, float sc)
        {
            float seed = _pairedRef.SeedV;
            float limp = Win(_pairedRef.LimpT, 0f, 1.5f);
            float thrash = s * (0.55f + 0.45f * Mathf.Abs(Noise(t * 0.8f, seed)));
            p.Reset();
            ActorPoses.PostureBase(p, d, Posture.LieBack, _time, "");
            // back arches, hips buck, head fights side to side under the pillow
            AddBody(p, HBone.Hips, -8f * thrash * Mathf.Max(0f, Noise(t * 2f, seed + 1f)), Noise(t * 1.5f, seed + 2f) * 10f * thrash, 0f);
            AddBody(p, HBone.Spine, -8f * thrash, Noise(t * 1.7f, seed + 3f) * 10f * thrash, 0f);
            AddBody(p, HBone.Chest, -6f * thrash, 0f, Noise(t * 2f, seed + 4f) * 8f * thrash);
            p.R[(int)HBone.Neck] = ActorPose.Body(4f, Noise(t * 2.5f, seed + 5f) * 12f * thrash + 18f * limp, 0f);
            p.R[(int)HBone.Head] = ActorPose.Body(0f, Noise(t * 2.7f, seed + 6f) * 10f * thrash + 12f * limp, 0f);
            // legs kick and pedal, then fall still
            float kl = Noise(t * 2.8f, seed + 7f), kr = Noise(t * 3.1f, seed + 8f);
            p.SetLegFK(true, 20f + 40f * Mathf.Max(0f, kl) * thrash, 6f + 8f * thrash, 30f + 70f * Mathf.Abs(kl) * thrash, -20f);
            p.SetLegFK(false, 18f + 42f * Mathf.Max(0f, kr) * thrash, 6f + 8f * thrash, 28f + 72f * Mathf.Abs(kr) * thrash, -20f);
            // arms up at the attacker's forearms (IK), then dropping to the sides
            float up = (1f - limp) * Win(t, 0f, 0.35f);
            p.SetArm(true, Mathf.Lerp(0f, 120f, up), Mathf.Lerp(20f, 8f, up), 20f * up, Mathf.Lerp(12f, 80f, up), 20f, 0f);
            p.SetArm(false, Mathf.Lerp(4f, 118f, up), Mathf.Lerp(24f, 8f, up), 20f * up, Mathf.Lerp(14f, 84f, up), 20f, 0f);
            p.SetHand(true, 0.3f + 0.5f * up, 0.25f + 0.55f * up, 0.3f + 0.6f * up); p.SetHand(false, 0.3f + 0.5f * up, 0.25f + 0.55f * up, 0.3f + 0.6f * up);
            // a flat hand slaps the floor / mattress in between
            if (s > 0.3f && Noise(t * 0.9f, seed + 9f) > 0.35f) p.SetArm(false, 30f, 60f, 0f, 30f, 40f, -20f);
        }

        void AttackerSmother(ActorPose p, ActorPoses.Dims d, float s, float t, float sc, PairedState st)
        {
            float seed = st.SeedA;
            bool bed = st.Surface > 0.2f;
            if (!bed)
            {
                // kneeling beside the victim, the weight over straight arms
                var kneel = _rx2; kneel.Reset();
                ActorPoses.PostureBase(kneel, d, Posture.Kneel, _time, "");
                p.CopyFrom(kneel);
                AddBody(p, HBone.Hips, 25f, 0f, 0f);
                AddBody(p, HBone.Spine, 22f + Noise(t * 1.6f, seed) * 4f * s, Noise(t * 1.3f, seed + 1f) * 5f * s, 0f);
                AddBody(p, HBone.Chest, 12f, 0f, 0f);
                p.HipsOffset += new Vector3(0f, 0.05f * sc, 0.06f * sc);
            }
            else
            {
                // standing at the bed, leaning over it
                MotionKit.Stance(p, d, new Vector3(-0.04f, 0f, 0.02f) * sc, new Vector3(0.05f, 0f, -0.18f) * sc, new Vector3(0f, -0.06f * sc, 0.02f * sc));
                AddBody(p, HBone.Hips, 30f, 0f, 0f);
                AddBody(p, HBone.Spine, 22f + Noise(t * 1.6f, seed) * 4f * s, Noise(t * 1.3f, seed + 1f) * 5f * s, 0f);
                AddBody(p, HBone.Chest, 10f, 0f, 0f);
            }
            AddBody(p, HBone.Neck, -12f);
            p.SetArm(false, 70f, 4f, 30f, 20f, 50f, -50f); p.SetArm(true, 70f, 4f, 30f, 20f, 50f, -50f);
            BlendShoulder(p, false, 1f, 6f * (0.4f + 0.6f * s), 12f); BlendShoulder(p, true, 1f, 6f * (0.4f + 0.6f * s), 12f);
            p.SetHand(true, 0.35f, 0.3f, 0.35f); p.SetHand(false, 0.35f, 0.3f, 0.35f);
        }

        // ---------------------------------------------------------------- drown (kneeling victim, head forced into water)
        void VictimDrown(ActorPose p, ActorPoses.Dims d, float s, float t, float sc, PairedState st)
        {
            float seed = st.SeedV;
            float limp = Win(st.LimpT, 0f, 1.5f);
            float thrash = s * (0.6f + 0.4f * Mathf.Abs(Noise(t * 0.9f, seed)));
            var kneel = _rx2; kneel.Reset();
            ActorPoses.PostureBase(kneel, d, Posture.Kneel, _time, "");
            p.CopyFrom(kneel);
            // how far the head is forced down: toward the water surface, with surfacing attempts while strong
            float surface = st.Surface >= 0f ? st.Surface : 0.55f;
            float lowRim = Clamp01((0.75f - surface) / 0.35f);          // a low bath: bend far forward; a basin: less
            float surfacing = Mathf.Max(0f, Noise(t * 0.55f, seed + 1f)) * s * 0.8f;
            float down = Clamp01(Win(t, 0.15f, 0.9f) * (1f - surfacing));
            AddBody(p, HBone.Hips, 18f + 22f * down * lowRim, 0f, Noise(t * 1.8f, seed + 2f) * 6f * thrash);
            AddBody(p, HBone.Spine, 20f * down + 8f, Noise(t * 1.9f, seed + 3f) * 10f * thrash, Noise(t * 2.2f, seed + 4f) * 6f * thrash);
            AddBody(p, HBone.Chest, 18f * down + 6f, 0f, 0f);
            AddBody(p, HBone.Neck, 22f * down - 10f * surfacing, Noise(t * 3f, seed + 5f) * 12f * thrash, 0f);
            AddBody(p, HBone.Head, 10f * down - 14f * surfacing, Noise(t * 3.4f, seed + 6f) * 16f * thrash, 8f * limp);
            p.HipsOffset += new Vector3(0f, 0.04f * sc * (1f - down), 0.04f * sc * down);
            // hands: pushing on the rim, slapping the water, clawing back at the arm in the hair; limp they sink into the water
            float up = (1f - limp);
            float slapL = Mathf.Max(0f, Noise(t * 3.2f, seed + 7f)), slapR = Mathf.Max(0f, Noise(t * 3.5f, seed + 8f));
            float claw = Mathf.Max(0f, Noise(t * 0.7f, seed + 10f)) * s;
            p.SetArm(true, 70f + 30f * slapL * thrash, ArmIdleOut + 16f, 20f, 38f + 40f * slapL * thrash, 40f, -40f);
            p.SetArm(false, Mathf.Lerp(70f + 30f * slapR * thrash, 150f, claw), Mathf.Lerp(ArmIdleOut + 16f, 20f, claw), 20f, Mathf.Lerp(38f + 40f * slapR * thrash, 120f, claw), 40f, -40f);
            if (limp > 0f) { BlendArm(p, true, limp, 55f, ArmIdleOut + 8f, 10f, 20f, 20f, 20f); BlendArm(p, false, limp, 55f, ArmIdleOut + 8f, 10f, 22f, 20f, 20f); }
            p.SetHand(true, 0.2f * up + 0.3f, 0.2f * up + 0.25f, 0.2f * up + 0.3f); p.SetHand(false, 0.5f * claw + 0.3f, 0.5f * claw + 0.25f, 0.55f * claw + 0.3f);
            // legs kick behind on the floor
            float kl = Mathf.Max(0f, Noise(t * 2.6f, seed + 11f)) * thrash, kr = Mathf.Max(0f, Noise(t * 2.9f, seed + 12f)) * thrash;
            p.R[(int)HBone.LowerLegL] = p.R[(int)HBone.LowerLegL] * Quaternion.AngleAxis(-25f * kl, Vector3.right);
            p.R[(int)HBone.LowerLegR] = p.R[(int)HBone.LowerLegR] * Quaternion.AngleAxis(-25f * kr, Vector3.right);
        }

        void AttackerDrown(ActorPose p, ActorPoses.Dims d, float s, float t, float sc, PairedState st)
        {
            float seed = st.SeedA;
            // one knee down behind the victim, leaning over, arms driving down
            p.LegIK = 0f;
            float hipH = 0.07f * sc + d.UpperLeg * 0.95f;
            p.HipsOffset = new Vector3(0f, hipH + d.HipsAboveJoint - d.RestHips.y, -0.06f * sc);
            p.R[0] = ActorPose.Body(10f);
            p.SetLegFK(true, 88f, 8f, 92f, -2f);                 // left foot forward, knee up
            p.SetLegFK(false, 4f, 4f, 96f, 50f);                 // right knee on the floor
            AddBody(p, HBone.Spine, 26f + Noise(t * 1.5f, seed) * 4f * s, Noise(t * 1.3f, seed + 1f) * 6f * s, 0f);
            AddBody(p, HBone.Chest, 14f, 0f, 0f);
            AddBody(p, HBone.Neck, -10f);
            p.SetArm(false, 62f, 6f, 30f, 40f, 40f, -10f);       // fist in the hair (LateIK)
            p.SetArm(true, 60f, 8f, 30f, 30f, 60f, -50f);        // hand between the shoulder blades (LateIK)
            p.SetHand(false, 0.85f, 0.9f, 0.95f); p.SetHand(true, 0.2f, 0.15f, 0.2f);
            BlendShoulder(p, false, 1f, 6f, 10f); BlendShoulder(p, true, 1f, 5f, 10f);
        }

        // ================================================================ contact IK (both bodies posed; runs in LateIK on the attacker)
        void SolvePairedContacts()
        {
            var st = _pairedRef;
            if (st == null || st.A != this || st.V == null) return;
            var a = this; var v = st.V;
            var vr = v._rig; var ar = _rig;
            if (vr == null || ar == null) return;
            float w = Ease(Mathf.Min(_pairAlignW, v._pairAlignW));
            float s = st.Shown, t = st.T;
            Transform vNeck = vr.Bone(HBone.Neck), vHead = vr.Bone(HBone.Head), vChest = vr.Bone(HBone.Chest);
            if (vNeck == null || vHead == null || vChest == null) return;
            float vsc = v.Height / 1.75f;
            Vector3 neck = Vector3.Lerp(vNeck.position, vHead.position, 0.35f);
            Vector3 fwd = vChest.forward, right = vChest.right, up = (vHead.position - vNeck.position).normalized;
            float jit = 0.012f * s;
            switch (st.Kind)
            {
                case PairedKind.Strangle:
                    // hands around the throat, thumbs in front
                    a.HandIK(false, neck + fwd * 0.03f * vsc - right * 0.045f * vsc + Jitter(t, 1f) * jit, null, w);
                    a.HandIK(true, neck + fwd * 0.03f * vsc + right * 0.045f * vsc + Jitter(t, 2f) * jit, null, w);
                    break;
                case PairedKind.LigatureFront:
                    // the cord crosses in front, the hands pull outward beside the neck
                    a.HandIK(false, neck + fwd * 0.06f * vsc - right * 0.1f * vsc + Jitter(t, 1f) * jit, null, w);
                    a.HandIK(true, neck + fwd * 0.06f * vsc + right * 0.1f * vsc + Jitter(t, 2f) * jit, null, w);
                    break;
                case PairedKind.GarroteRear:
                    // hands crossed behind the neck, pulling back
                    a.HandIK(false, neck - fwd * 0.1f * vsc + right * 0.03f * vsc + Jitter(t, 1f) * jit, null, w);
                    a.HandIK(true, neck - fwd * 0.1f * vsc - right * 0.03f * vsc + Jitter(t, 2f) * jit, null, w);
                    break;
                case PairedKind.Smother:
                {
                    // both hands press the pillow onto the face
                    Vector3 face = vHead.position + vHead.forward * 0.12f * vsc;
                    Vector3 across = vHead.right;
                    a.HandIK(false, face + across * 0.1f * vsc + Vector3.up * 0.05f + Jitter(t, 1f) * jit, null, w);
                    a.HandIK(true, face - across * 0.1f * vsc + Vector3.up * 0.05f + Jitter(t, 2f) * jit, null, w);
                    break;
                }
                case PairedKind.Drown:
                {
                    // fist in the hair at the back of the head, the other hand between the shoulder blades
                    Vector3 hair = vHead.position - vHead.forward * 0.07f * vsc + vHead.up * 0.07f * vsc;
                    a.HandIK(false, hair + Jitter(t, 1f) * jit, null, w);
                    a.HandIK(true, vChest.position - vChest.forward * 0.13f * vsc + vChest.up * 0.06f * vsc, null, w * 0.9f);
                    break;
                }
            }
            // the victim's hands claw at the attacker's forearms / the cord / the hand in the hair while conscious
            float claw = (1f - Win(st.LimpT, 0f, 1.2f)) * w;
            if (claw > 0.01f)
            {
                Transform aL = ar.Bone(HBone.LowerArmL), aR = ar.Bone(HBone.LowerArmR), aHL = ar.Bone(HBone.HandL), aHR = ar.Bone(HBone.HandR);
                if (aL != null && aR != null && aHL != null && aHR != null)
                {
                    float slideL = 0.35f + 0.3f * Mathf.Sin(t * 5.3f + st.SeedV), slideR = 0.35f + 0.3f * Mathf.Sin(t * 4.7f + st.SeedV * 2f);
                    Vector3 gl = Vector3.Lerp(aHR.position, aR.position, slideL), gr = Vector3.Lerp(aHL.position, aL.position, slideR);
                    if (st.Kind == PairedKind.GarroteRear || st.Kind == PairedKind.LigatureFront)
                    {
                        // fingers at the cord on the own throat
                        gl = neck + fwd * 0.05f * vsc - right * 0.03f * vsc; gr = neck + fwd * 0.05f * vsc + right * 0.03f * vsc;
                    }
                    if (st.Kind == PairedKind.Drown)
                    {
                        // one hand claws back at the arm holding the hair (only in bursts), the other stays on the rim / water
                        float burst = Mathf.Max(0f, Noise(t * 0.7f, st.SeedV + 10f)) * s;
                        v.HandIK(false, Vector3.Lerp(aHR.position, aR.position, slideL) + Jitter(t, 5f) * 0.03f, null, claw * Ease(burst * 2f));
                    }
                    else
                    {
                        float amp = 0.035f * s;
                        v.HandIK(true, gl + Jitter(t * 1.3f, 3f) * amp, null, claw);
                        v.HandIK(false, gr + Jitter(t * 1.2f, 4f) * amp, null, claw);
                    }
                }
            }
        }

        static Vector3 Jitter(float t, float seed) => new Vector3(Noise(t * 7f, seed), Noise(t * 7.3f, seed + 11f), Noise(t * 6.7f, seed + 23f));
    }
}
