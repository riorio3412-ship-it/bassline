using UnityEngine;
using static BL23.Game.Characters.MotionKit;

namespace BL23.Game.Characters
{
    /// <summary>Ambient life (motion track): weight shifts and personality fidgets in idle, turning a shoulder to let someone pass,
    /// a bump with an apology, cold rooms, comforting someone.</summary>
    public partial class ActorAnimator
    {
        struct PassState { public bool Active; public float Side, T, Dur; }
        PassState _pass;
        struct BumpState { public bool Active; public Vector3 From; public float T; public bool Sorry; }
        BumpState _bump;
        float _cold, _coldShown;
        ActorRig _console; float _consoleUntil = -1f;
        float _shift, _shiftWant, _nextShift = 6f;

        /// <summary>Someone passes close by at otherWorld: turn the shoulders to let them through.</summary>
        public void PassBy(Vector3 otherWorld)
        {
            Init();
            if (!_init || _dead >= 0 || !_conscious || _beingCarried || _pairedRef != null) return;
            Vector3 o = transform.InverseTransformPoint(otherWorld);
            _pass.Active = true; _pass.Side = o.x >= 0f ? 1f : -1f; _pass.T = 0f; _pass.Dur = 1.1f;
            if (Random.value < 0.6f) Glance(otherWorld + Vector3.up * 1.5f, 0.7f);
        }

        /// <summary>Bumped by someone at fromWorld: a small jolt, a look, and (apologize) a brief apologetic gesture.</summary>
        public void Bumped(Vector3 fromWorld, bool apologize = true)
        {
            Init();
            if (!_init || _dead >= 0 || !_conscious || _beingCarried || _pairedRef != null) return;
            Vector3 f = transform.InverseTransformPoint(fromWorld); f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            _bump.Active = true; _bump.From = f.normalized; _bump.T = 0f; _bump.Sorry = apologize;
            Glance(fromWorld + Vector3.up * 1.5f, 1.4f);
            if (_rig != null && _rig.Face != null) _rig.Face.PushTransient(Expr.Surprised, 0.5f, 0.5f);
        }

        /// <summary>How cold the room feels 0..1: arms drawn in, shoulders up, shivering at high values.</summary>
        public void SetCold(float cold01) { _cold = Clamp01(cold01); }

        /// <summary>Comfort 'other' (crying / shaken) for 'seconds': a hand on their shoulder, the head tilted, a look at their face.</summary>
        public void ConsolePartner(ActorRig other, float seconds)
        {
            Init();
            if (other == null) { _console = null; _consoleUntil = -1f; _hand[1].Want = 0f; return; }
            _console = other; _consoleUntil = _time + Mathf.Max(0.5f, seconds);
            var sh = other.Bone(HBone.UpperArmR) ?? other.Bone(HBone.Chest);
            // the nearer of the partner's shoulders
            var l = other.Bone(HBone.UpperArmL);
            if (l != null && sh != null && (l.position - transform.position).sqrMagnitude < (sh.position - transform.position).sqrMagnitude) sh = l;
            if (sh != null) { _hand[1].T = sh; _hand[1].Offset = Vector3.zero; _hand[1].Point = null; _hand[1].Want = 1f; _hand[1].Lean = false; _consoleShoulder = sh; }
            PlayGesture(Gesture.Console, seconds);
        }
        Transform _consoleShoulder;

        void EvaluateAmbient(float dt, ActorPoses.Dims d)
        {
            if (_dead >= 0 || _beingCarried || !_conscious || _pairedRef != null) { _pass.Active = _bump.Active = false; return; }
            float sc = Height / 1.75f;
            // ---- console: look at their face, lean in a little; the hand lands through the hand target IK
            if (_console != null)
            {
                if (_time > _consoleUntil) { _console = null; if (_hand[1].T == _consoleShoulder) _hand[1].Want = 0f; }
                else
                {
                    if (_console.EyeAnchor != null) Glance(_console.EyeAnchor.position, 0.3f);
                    AddBody(_base, HBone.Spine, 5f, 0f, 0f); AddBody(_base, HBone.Head, 6f, 0f, 8f);
                }
            }
            // ---- idle weight shifts: the hips settle over one leg, the other knee softens, then back
            bool idle = _posture == Posture.Stand && _walkW < 0.02f && !_aActive && !_atk.Active && !Reacting && _restraint == RestraintFlags.None;
            if (idle)
            {
                _nextShift -= dt;
                if (_nextShift <= 0f)
                {
                    _nextShift = Random.Range(5f, 12f) * (StyleCalm ? 1.5f : 1f);
                    _shiftWant = _shiftWant > 0.5f ? Random.Range(-1f, -0.4f) : _shiftWant < -0.5f ? Random.Range(0.4f, 1f) : (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.5f, 1f);
                    if (Random.value < 0.25f) _shiftWant = 0f;
                }
            }
            else _shiftWant = 0f;
            _shift = Mathf.MoveTowards(_shift, _shiftWant, dt * 0.9f);
            if (Mathf.Abs(_shift) > 0.01f && _base.LegIK > 0.5f && _posture == Posture.Stand)
            {
                float k = Ease(Mathf.Abs(_shift)) * Mathf.Sign(_shift);     // + = weight on the right leg
                _base.HipsOffset += new Vector3(0.028f * k, -0.008f * Mathf.Abs(k), 0f) * sc;
                AddBody(_base, HBone.Hips, 0f, 2f * k, -4f * k);               // the free hip drops
                AddBody(_base, HBone.Spine, 0f, -1f * k, 3f * k);
                AddBody(_base, HBone.Chest, 0f, 0f, 1.5f * k);
                // the unloaded knee bends: its foot comes a little forward and its heel lifts
                if (k > 0f) { _base.FootL += new Vector3(0f, 0.012f * k, 0.03f * k) * sc; _base.FootRotL = _base.FootRotL * ActorPose.E(8f * k, 0f, 0f); }
                else { _base.FootR += new Vector3(0f, -0.012f * k, -0.03f * k) * sc; _base.FootRotR = _base.FootRotR * ActorPose.E(-8f * k, 0f, 0f); }
            }
            // ---- let someone pass: the shoulder on their side swings back, a slight lean away
            if (_pass.Active)
            {
                _pass.T += dt;
                float u = _pass.T / _pass.Dur;
                if (u >= 1f) _pass.Active = false;
                else
                {
                    float k = Bell(u, 0f, 0.28f, 0.6f, 1f) * _pass.Side;
                    AddBody(_base, HBone.Spine, 0f, 14f * k, -2f * k);
                    AddBody(_base, HBone.Chest, 0f, 20f * k, -2f * k);
                    AddBody(_base, HBone.Head, 0f, -10f * k, 0f);
                    _base.HipsOffset += new Vector3(-0.02f * k, 0f, 0f) * sc;
                    BlendArm(_base, _pass.Side < 0f, Mathf.Abs(k) * 0.6f, 6f, ArmIdleOut - 6f, 20f, 30f);
                }
            }
            // ---- bumped: a jolt away from the contact, then (optionally) a small bow with a raised palm
            if (_bump.Active)
            {
                _bump.T += dt;
                float t = _bump.T;
                if (t > (_bump.Sorry ? 1.7f : 0.6f)) _bump.Active = false;
                else
                {
                    float j = Impulse(t, 0.07f);
                    Vector3 f = _bump.From;
                    AddBody(_base, HBone.Spine, -f.z * 7f * j, 0f, f.x * 6f * j);
                    AddBody(_base, HBone.Chest, -f.z * 5f * j, -f.x * 10f * j, 0f);
                    _base.HipsOffset += new Vector3(-f.x * 0.03f, -0.01f, -f.z * 0.03f) * j * sc;
                    if (_bump.Sorry)
                    {
                        float sorry = Bell(t, 0.35f, 0.6f, 1.2f, 1.6f);
                        AddBody(_base, HBone.Spine, 8f * sorry); AddBody(_base, HBone.Neck, 8f * sorry); AddBody(_base, HBone.Head, 6f * sorry, 0f, 4f * sorry);
                        BlendArm(_base, false, sorry, 42f, ArmIdleOut + 4f, 30f, 96f, 70f, -10f);
                        BlendHand(_base, false, sorry, 0.08f, 0.05f, 0.1f);
                    }
                }
            }
            // ---- cold: arms in, shoulders up, shivers
            _coldShown = Mathf.MoveTowards(_coldShown, _cold, dt * 0.5f);
            if (_coldShown > 0.02f)
            {
                float c = _coldShown;
                float shiver = c > 0.4f ? (c - 0.4f) / 0.6f : 0f;
                BlendShoulder(_base, true, c, 9f * c, 8f); BlendShoulder(_base, false, c, 9f * c, 8f);
                AddBody(_base, HBone.Chest, 4f * c + Noise(_time * 17f, 1f) * 1.8f * shiver, Noise(_time * 15f, 2f) * 1.2f * shiver, 0f);
                AddBody(_base, HBone.Head, 3f * c + Noise(_time * 16f, 3f) * 1.4f * shiver, 0f, 0f);
                if (_walkW < 0.1f && !_aActive && !_gActive && _posture == Posture.Stand && c > 0.45f && !WristsBound)
                {
                    // arms wrapped around the body, hands rubbing the upper arms
                    float wrap = Ease((c - 0.45f) / 0.35f);
                    float rub = Mathf.Sin(_time * 7f) * 4f * shiver;
                    BlendArm(_base, true, wrap, 34f + rub, -22f, 60f, 112f, 20f, 0f);
                    BlendArm(_base, false, wrap, 30f - rub, -20f, 60f, 116f, 20f, 0f);
                    BlendHand(_base, true, wrap, 0.3f, 0.35f, 0.4f); BlendHand(_base, false, wrap, 0.3f, 0.35f, 0.4f);
                }
            }
        }

        bool StyleCalm => _rig != null && (_rig.IdleStyle == "calm" || _rig.IdleStyle == "stiff" || _rig.IdleStyle == "clasp" || _rig.IdleStyle == "behind");

        /// <summary>Idle fidgets chosen by personality (IdleStyle): the formal check their watch and straighten their clothes, the
        /// restless look around and shift their weight, the gentle clasp their hands or nod.</summary>
        Gesture[] FidgetPoolFor(string style)
        {
            switch (style ?? "")
            {
                case "stiff": case "behind": case "politician": case "briefcase": case "clasp": return FormalPool;
                case "slouch": case "pockets": case "swagger": return CasualPool;
                case "cute": case "lily": case "curious": return GentlePool;
                case "notebook": case "clipboard": case "thermos": return WorkPool;
                default: return FidgetPool;
            }
        }
        static readonly Gesture[] FormalPool = { Gesture.CheckWatch, Gesture.AdjustClothes, Gesture.LookAround, Gesture.Nod, Gesture.WeightShift };
        static readonly Gesture[] CasualPool = { Gesture.LookAround, Gesture.Shrug, Gesture.WeightShift, Gesture.AdjustClothes, Gesture.Think };
        static readonly Gesture[] GentlePool = { Gesture.Listen, Gesture.Nod, Gesture.LookAround, Gesture.AdjustClothes, Gesture.WeightShift };
        static readonly Gesture[] WorkPool = { Gesture.Think, Gesture.LookAround, Gesture.CheckWatch, Gesture.Nod };
    }
}
