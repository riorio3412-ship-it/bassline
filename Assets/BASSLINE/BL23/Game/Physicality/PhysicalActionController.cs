using System;
using BL23.Game.Characters;
using UnityEngine;

namespace BL23.Game.Physicality
{
    /// <summary>
    /// Shared contact-driven actions layered over ActorAnimator. The simulation owns inventory/damage;
    /// callbacks occur at hand contact, and callers can reconcile authoritative state on cancellation.
    /// No animation clips, imported Avatar, personality, or scene authoring are required.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhysicalActionController : MonoBehaviour
    {
        public PhysicalActionStyle Style = new PhysicalActionStyle();
        public LayerMask ContactLayers = ~0;
        public bool EnableFootPlacement = true;
        public PhysicalActionKind CurrentAction { get; private set; }
        public bool Busy => CurrentAction != PhysicalActionKind.None;
        public bool ContactMade { get; private set; }
        public bool WeaponContactActive { get; private set; }
        public event Action<PhysicalActionKind> ActionStarted;
        public event Action<PhysicalActionKind> ContactReached;
        public event Action<PhysicalActionKind, bool> ActionFinished;
        public event Action<bool> WeaponContactWindowChanged;

        ActorRig _rig;
        ActorAnimator _anim;
        PhysicalCharacter _body;
        bool _left;
        Transform _target;
        Collider[] _targetColliders;
        PhysicalActionController _receiver;
        bool _receiverLeft;
        Vector3 _point, _normal, _from;
        float _clock, _duration, _hold;
        int _generation;
        Action _contact;
        Action<bool> _finished;
        ActionAnim _swing;
        readonly Vector3[] _handPoint = new Vector3[2];
        readonly float[] _handWeight = new float[2];
        readonly Transform[] _carried = new Transform[2];
        readonly float[] _carryMass = new float[2];
        readonly float[] _feetOffset = new float[2];
        readonly RaycastHit[] _hits = new RaycastHit[24];
        Vector3 _painDirection, _painPoint, _startlePoint;
        float _pain, _startle, _lastDt;

        public void Bind(ActorRig rig)
        {
            _rig = rig;
            if (_rig == null) return;
            _rig.Init();
            _anim = _rig.Anim;
            _body = _rig.GetComponentInParent<PhysicalCharacter>();
            if (Style == null) Style = new PhysicalActionStyle();
        }

        void Awake() { Bind(GetComponent<ActorRig>()); }
        void OnDisable() { Cancel(); }

        public bool CanUseHand(bool left) => _anim != null && _anim.CanApplyPhysicalActions && _anim.ArmAvailable(left) &&
            _rig != null && _rig.HasBone(left ? HBone.UpperArmL : HBone.UpperArmR) &&
            _rig.HasBone(left ? HBone.LowerArmL : HBone.LowerArmR) && _rig.HasBone(left ? HBone.HandL : HBone.HandR);
        public void SetCarriedObject(Transform item, bool left, float mass = 1f)
        {
            int side = left ? 0 : 1;
            _carried[side] = item;
            _carryMass[side] = Mathf.Max(0.01f, mass);
        }

        public bool Reach(Vector3 worldPoint, bool left = false, float duration = 0.7f)
            => Start(PhysicalActionKind.Reach, worldPoint, null, left, duration, null, null);

        public bool BeginPickup(Transform target, bool left, Action onContact, Action<bool> onFinished = null)
        {
            if (target == null) return false;
            return Start(PhysicalActionKind.PickUp, target.position, target, left, Style.ReachSeconds, onContact, onFinished);
        }

        public bool BeginPlace(Vector3 worldPoint, bool left, Action onRelease, Action<bool> onFinished = null)
            => Start(PhysicalActionKind.Place, worldPoint, null, left, Style.PlaceSeconds, onRelease, onFinished);

        public bool BeginHandover(PhysicalActionController receiver, bool left, Action onContact, Action<bool> onFinished = null, bool receiverLeft = false)
        {
            if (receiver == null || receiver == this || !receiver.CanUseHand(receiverLeft) || receiver.Busy || !CanUseHand(left)) return false;
            Vector3 meet = (Hand(left).position + receiver.Hand(receiverLeft).position) * 0.5f;
            meet.y = Mathf.Max(transform.position.y, receiver.transform.position.y) + Mathf.Min(Height, receiver.Height) * 0.62f;
            if (Vector3.Distance(transform.position, receiver.transform.position) > 1.5f) return false;
            if (!Start(PhysicalActionKind.Give, meet, null, left, Style.HandoverSeconds, onContact, onFinished)) return false;
            _receiver = receiver;
            _receiverLeft = receiverLeft;
            receiver.Start(PhysicalActionKind.Receive, meet, null, receiverLeft, Style.HandoverSeconds, null, null);
            return true;
        }

        public bool Brace(Vector3 point, Vector3 normal, bool left = false, float hold = 0.6f)
        {
            if (Busy && CurrentAction == PhysicalActionKind.Brace && left == _left)
            {
                _point = point; _normal = normal; _hold = Mathf.Max(_hold, _clock + hold); return true;
            }
            if (!Start(PhysicalActionKind.Brace, point, null, left, 0.18f + Mathf.Max(0.1f, hold), null, null)) return false;
            _normal = normal.sqrMagnitude > 0.01f ? normal.normalized : Vector3.up;
            _hold = _duration;
            return true;
        }

        /// <summary>Only the contact window is exposed; this class never applies a second simulation hit.</summary>
        public bool Swing(ActionAnim action, Vector3 targetPoint, bool playAnimation = true, bool left = false)
        {
            if (!Start(PhysicalActionKind.Swing, targetPoint, null, left, Style.SwingSeconds, null, null)) return false;
            _swing = action;
            if (playAnimation) _anim.PlayAction(action, _duration);
            return true;
        }

        public void ReactToImpact(Vector3 worldPoint, Vector3 direction, float severity)
        {
            if (_anim == null || !_anim.CanApplyPhysicalActions) return;
            float strength = Mathf.Clamp01(severity);
            _pain = Mathf.Max(_pain, strength * Style.PainResponse);
            _painDirection = direction.sqrMagnitude > 0.0001f ? transform.InverseTransformDirection(direction.normalized) : Vector3.back;
            _painPoint = transform.InverseTransformPoint(worldPoint);
            if (strength > 0.35f && CurrentAction != PhysicalActionKind.Brace) Cancel();
        }

        public void Startle(Vector3 source, float strength = 0.6f)
        {
            if (_anim == null || !_anim.CanApplyPhysicalActions) return;
            _startle = Mathf.Max(_startle, Mathf.Clamp01(strength) * Style.StartleResponse);
            _startlePoint = source;
        }

        public void Cancel()
        {
            if (!Busy) return;
            Finish(false);
        }

        bool Start(PhysicalActionKind kind, Vector3 point, Transform target, bool left, float duration,
            Action contact, Action<bool> finished)
        {
            if (_rig == null) Bind(GetComponent<ActorRig>());
            if (!CanUseHand(left) || Hand(left) == null) return false;
            Cancel();
            _generation++;
            CurrentAction = kind; ContactMade = false;
            _left = left; _target = target; _point = point; _normal = Vector3.zero;
            _from = Hand(left).position; _clock = 0f;
            _targetColliders = target != null ? target.GetComponentsInChildren<Collider>() : null;
            _duration = Mathf.Max(0.15f, duration); _hold = _duration;
            _contact = contact; _finished = finished; _receiver = null;
            ActionStarted?.Invoke(kind);
            return true;
        }

        void Finish(bool completed)
        {
            PhysicalActionKind kind = CurrentAction;
            _generation++;
            Action<bool> callback = _finished;
            PhysicalActionController receiver = _receiver;
            _contact = null; _finished = null; _target = null; _targetColliders = null; _receiver = null;
            CurrentAction = PhysicalActionKind.None;
            SetWeaponWindow(false);
            if (!completed && receiver != null && receiver.CurrentAction == PhysicalActionKind.Receive) receiver.Cancel();
            callback?.Invoke(completed);
            ActionFinished?.Invoke(kind, completed);
        }

        float Height => _rig != null && _rig.Height > 0.5f ? _rig.Height : 1.75f;
        Transform Hand(bool left) => _rig == null ? null : ((left ? _rig.HandAnchorL : _rig.HandAnchorR) ?? _rig.Bone(left ? HBone.HandL : HBone.HandR));
        static float Ease(float value) { value = Mathf.Clamp01(value); return value * value * (3f - 2f * value); }
        Vector3 CarryPoint(bool left, float mass)
        {
            float heavy = Mathf.InverseLerp(1f, 18f, mass);
            return transform.TransformPoint(new Vector3((left ? -1f : 1f) * Style.CarrySide,
                Height * (0.59f - heavy * 0.08f), Style.CarryForward));
        }

        /// <summary>Called by ActorAnimator immediately before applying its canonical pose.</summary>
        public void PreparePose(float dt, ActorPose pose)
        {
            if (!isActiveAndEnabled || _rig == null || _anim == null) return;
            _lastDt = PhysicalCharacter.WorldPaused ? 0f : Mathf.Clamp(dt, 0f, 0.1f);
            if (!_anim.CanApplyPhysicalActions) { Cancel(); _handWeight[0] = _handWeight[1] = 0f; return; }
            _clock += _lastDt;
            _pain = Mathf.MoveTowards(_pain, 0f, _lastDt * 1.45f);
            _startle = Mathf.MoveTowards(_startle, 0f, _lastDt * 1.75f);
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                bool carrying = _carried[side] != null && CanUseHand(left);
                float want = carrying ? 0.8f : 0f;
                if (carrying && !(Busy && left == _left)) _handPoint[side] = CarryPoint(left, _carryMass[side]);
                if (Busy && left == _left) want = 1f;
                _handWeight[side] = Mathf.MoveTowards(_handWeight[side], want, _lastDt * Style.ContactResponse);
                if (carrying) pose.Grip(left);
            }
            // A heavy item recruits the unoccupied second hand without altering inventory ownership.
            for (int side = 0; side < 2; side++)
            {
                int other = 1 - side;
                bool otherLeft = other == 0;
                if (_carried[side] == null || _carryMass[side] < 5f || _carried[other] != null ||
                    (Busy && _left == otherLeft) || !CanUseHand(otherLeft)) continue;
                _handPoint[other] = CarryPoint(otherLeft, _carryMass[side]);
                _handWeight[other] = Mathf.MoveTowards(_handWeight[other], 0.85f, _lastDt * Style.ContactResponse);
                pose.Grip(otherLeft);
            }
            if (Busy && !CanUseHand(_left)) Cancel();
            if (Busy) PrepareAction(pose);
            if (_pain > 0.01f)
            {
                float upper = Mathf.InverseLerp(Height * 0.5f, Height * 0.85f, _painPoint.y);
                float head = Mathf.InverseLerp(Height * 0.8f, Height * 0.95f, _painPoint.y);
                float lateralContact = Mathf.Clamp(_painPoint.x / (Height * 0.15f), -1f, 1f);
                // Both the contact lever arm and signed force direction change the recoil.
                pose.R[(int)HBone.Hips] = pose.R[(int)HBone.Hips] * ActorPose.Body(-_painDirection.z * 8f * _pain * (1f - upper), lateralContact * 6f * _pain * (1f - upper));
                pose.R[(int)HBone.Spine] = pose.R[(int)HBone.Spine] * ActorPose.Body(-_painDirection.z * 15f * _pain, lateralContact * _painDirection.z * 12f * _pain, -_painDirection.x * 13f * _pain);
                pose.R[(int)HBone.Chest] = pose.R[(int)HBone.Chest] * ActorPose.Body(10f * _pain * upper, _painDirection.x * 9f * _pain * upper);
                pose.R[(int)HBone.Head] = pose.R[(int)HBone.Head] * ActorPose.Body(-_painDirection.z * 18f * _pain * head, -_painDirection.x * 22f * _pain * head);
                // Free hand guards the injured area; a brace or a held prop keeps its priority.
                bool left = _painPoint.x >= 0f;
                int side = left ? 0 : 1;
                if (!Busy && _carried[side] == null && CanUseHand(left))
                {
                    _handPoint[side] = transform.TransformPoint(_painPoint) - transform.forward * 0.035f;
                    _handWeight[side] = Mathf.Clamp01(_pain * 0.85f);
                    pose.SoftHand(left);
                }
            }
            if (_startle > 0.01f)
            {
                pose.R[(int)HBone.Neck] = pose.R[(int)HBone.Neck] * ActorPose.Body(-8f * _startle);
                for (int side = 0; side < 2; side++)
                {
                    bool left = side == 0;
                    pose.SetShoulder(left, _startle * 10f);
                    if (_carried[side] == null && !Busy && CanUseHand(left))
                    {
                        _handPoint[side] = transform.TransformPoint(new Vector3(left ? -0.25f : 0.25f, Height * 0.78f, 0.24f));
                        _handWeight[side] = Mathf.Clamp01(_startle);
                        pose.OpenHand(left);
                    }
                }
                Vector3 source = transform.InverseTransformPoint(_startlePoint);
                pose.R[(int)HBone.Head] = pose.R[(int)HBone.Head] * ActorPose.Body(0f, Mathf.Clamp(Mathf.Atan2(source.x, source.z) * Mathf.Rad2Deg, -35f, 35f) * _startle);
            }
        }

        void PrepareAction(ActorPose pose)
        {
            if (_target != null && !ContactMade)
            {
                _point = _target.position;
                float nearest = float.PositiveInfinity;
                if (_targetColliders != null) foreach (var collider in _targetColliders)
                {
                    if (collider == null || !collider.enabled || collider.isTrigger) continue;
                    Vector3 candidate = collider.ClosestPoint(_from);
                    float distance = (candidate - _from).sqrMagnitude;
                    if (distance >= nearest) continue;
                    nearest = distance; _point = candidate;
                }
            }
            if (CurrentAction == PhysicalActionKind.PickUp && _target == null && !ContactMade) { Finish(false); return; }
            float phase = _clock / _duration;
            float reach = Ease(phase / 0.55f);
            Vector3 point = _point;
            if (CurrentAction == PhysicalActionKind.Swing)
            {
                float side = _left ? -1f : 1f;
                Vector3 windup = transform.TransformPoint(new Vector3(side * 0.42f, Height * (_swing == ActionAnim.Overhead ? 1.05f : 0.78f), -0.18f));
                Vector3 contact = Vector3.Lerp(CarryPoint(_left, 1f), _point, 0.8f);
                Vector3 follow = _swing == ActionAnim.Stab ? contact : contact - transform.right * side * 0.42f - Vector3.up * 0.18f;
                point = phase < 0.32f ? Vector3.Lerp(_from, windup, Ease(phase / 0.32f)) :
                    phase < 0.62f ? Vector3.Lerp(windup, contact, Ease((phase - 0.32f) / 0.3f)) :
                    Vector3.Lerp(contact, follow, Ease((phase - 0.62f) / 0.38f));
                SetWeaponWindow(phase >= 0.32f && phase <= 0.72f);
                pose.Fist(_left);
            }
            else if (CurrentAction == PhysicalActionKind.Brace)
            {
                point = Vector3.Lerp(_from, point, Ease(_clock / 0.16f));
                pose.OpenHand(_left);
                pose.R[(int)HBone.Chest] = pose.R[(int)HBone.Chest] * ActorPose.Body(10f, 0f, _left ? 5f : -5f);
            }
            else
            {
                if (ContactMade && CurrentAction == PhysicalActionKind.PickUp)
                    point = Vector3.Lerp(_point, CarryPoint(_left, _carryMass[_left ? 0 : 1]), Ease((phase - 0.6f) / 0.4f));
                else point = Vector3.Lerp(_from, point, reach) + transform.up * Mathf.Sin(reach * Mathf.PI) * 0.08f * Style.MotionScale;
                if (CurrentAction == PhysicalActionKind.Place || CurrentAction == PhysicalActionKind.Give)
                { if (ContactMade) pose.OpenHand(_left); else pose.Grip(_left); }
                else { if (ContactMade) pose.Grip(_left); else pose.OpenHand(_left); }
            }
            if (CurrentAction != PhysicalActionKind.Swing)
            {
                float low = Mathf.Clamp01((Height * 0.6f - transform.InverseTransformPoint(point).y) / (Height * 0.5f));
                if (_anim.CurrentPosture == Posture.Stand && low > 0f)
                {
                    // Contact height selects squat depth. Blend toward an absolute pose rather than
                    // adding another crouch on top of an imported/procedural PickUp clip.
                    float weight = _handWeight[_left ? 0 : 1];
                    pose.HipsOffset.y = Mathf.Lerp(pose.HipsOffset.y, -low * Height * 0.35f, weight);
                    pose.R[(int)HBone.Hips] = Quaternion.Slerp(pose.R[(int)HBone.Hips], ActorPose.Body(low * 20f), weight);
                    pose.R[(int)HBone.Spine] = Quaternion.Slerp(pose.R[(int)HBone.Spine], ActorPose.Body(low * 35f), weight);
                    pose.R[(int)HBone.Chest] = Quaternion.Slerp(pose.R[(int)HBone.Chest], ActorPose.Body(low * 25f), weight);
                    if (pose.LegIK < 0.5f)
                    {
                        pose.FootL = _anim.RestPos(HBone.FootL);
                        pose.FootR = _anim.RestPos(HBone.FootR);
                    }
                    pose.LegIK = 1f;
                }
            }
            _handPoint[_left ? 0 : 1] = point;
        }

        /// <summary>Called after animation, so contacts never get overwritten by the next pose layer.</summary>
        public void ApplyIK()
        {
            if (!isActiveAndEnabled || _anim == null || !_anim.CanApplyPhysicalActions) return;
            if (EnableFootPlacement && _anim.CurrentPosture == Posture.Stand && _anim.CurrentAction != ActionAnim.Fall)
                PlaceFeet();
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                if (_handWeight[side] < 0.001f || !CanUseHand(left)) continue;
                Vector3 target = _handPoint[side];
                bool active = Busy && left == _left;
                bool brace = active && CurrentAction == PhysicalActionKind.Brace;
                if (active && CurrentAction != PhysicalActionKind.Swing && !brace) target = ClampObstructedReach(left, target);
                ProceduralContactIK.SolveHand(_rig, left, target, brace ? _normal : Vector3.zero,
                    _handWeight[side], brace ? 0.9f : 0f);
            }
            if (!Busy || PhysicalCharacter.WorldPaused) return;
            bool close = Vector3.Distance(Hand(_left).position, _point) <= Style.ContactTolerance;
            if (_receiver != null) close &= Vector3.Distance(_receiver.Hand(_receiverLeft).position, _point) <= Style.ContactTolerance;
            if (!ContactMade && _clock >= (CurrentAction == PhysicalActionKind.Brace ? 0.16f : _duration * 0.55f) && close && CurrentAction != PhysicalActionKind.Swing)
            {
                ContactMade = true;
                Action callback = _contact; _contact = null;
                PhysicalActionKind kind = CurrentAction;
                int generation = _generation;
                callback?.Invoke();
                ContactReached?.Invoke(kind);
                if (generation != _generation) return; // a contact listener may start another action
            }
            float deadline = CurrentAction == PhysicalActionKind.Brace ? _hold : _duration;
            if (_clock >= deadline)
            {
                bool requiresContact = CurrentAction == PhysicalActionKind.PickUp || CurrentAction == PhysicalActionKind.Place || CurrentAction == PhysicalActionKind.Give;
                if (requiresContact && !ContactMade && _clock < deadline + 0.3f) return;
                Finish(!requiresContact || ContactMade);
            }
        }

        void SetWeaponWindow(bool active)
        {
            if (WeaponContactActive == active) return;
            WeaponContactActive = active;
            WeaponContactWindowChanged?.Invoke(active);
        }

        Vector3 ClampObstructedReach(bool left, Vector3 target)
        {
            var upper = _rig.Bone(left ? HBone.UpperArmL : HBone.UpperArmR);
            if (upper == null) return target;
            Vector3 direction = target - upper.position;
            float length = direction.magnitude;
            if (length < 0.05f) return target;
            int count = Physics.SphereCastNonAlloc(upper.position, 0.025f, direction / length, _hits,
                length, ContactLayers, QueryTriggerInteraction.Ignore);
            float best = length;
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.collider == null || hit.distance <= 0f || hit.collider.transform.IsChildOf(transform)) continue;
                var body = hit.collider.GetComponentInParent<PhysicalCharacter>();
                if (body != null && (body == _body || (_receiver != null && body == _receiver._body))) continue;
                if (_receiver != null && hit.collider.transform.IsChildOf(_receiver.transform)) continue;
                if (_target != null && hit.collider.transform.IsChildOf(_target)) continue;
                if (Vector3.Distance(hit.point, target) < 0.13f || hit.distance >= best) continue;
                best = hit.distance;
            }
            return upper.position + direction.normalized * best;
        }

        void PlaceFeet()
        {
            if (Style.FootPlacement <= 0f) return;
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                HBone thigh = left ? HBone.UpperLegL : HBone.UpperLegR;
                HBone shin = left ? HBone.LowerLegL : HBone.LowerLegR;
                HBone ankle = left ? HBone.FootL : HBone.FootR;
                if (!_rig.HasBone(thigh) || !_rig.HasBone(shin) || !_rig.HasBone(ankle)) continue;
                Transform foot = _rig.Bone(ankle);
                Vector3 start = foot.position + Vector3.up * 0.32f;
                int count = Physics.RaycastNonAlloc(start, Vector3.down, _hits, 0.62f, ContactLayers, QueryTriggerInteraction.Ignore);
                float best = float.PositiveInfinity; RaycastHit ground = default;
                for (int i = 0; i < count; i++)
                {
                    var hit = _hits[i];
                    if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<ActorRig>() != null ||
                        hit.collider.GetComponentInParent<PhysicalCharacter>() != null || hit.collider.GetComponentInParent<PhysicalBodyPart>() != null) continue;
                    if (Vector3.Angle(hit.normal, Vector3.up) > Style.MaxFootSlope || hit.distance >= best) continue;
                    best = hit.distance; ground = hit;
                }
                float want = 0f;
                float ankleHeight = Mathf.Max(0.04f, _anim.AnkleH);
                float animatedHeight = transform.InverseTransformPoint(foot.position).y;
                float planted = 1f - Mathf.InverseLerp(ankleHeight + 0.035f, ankleHeight + 0.18f, animatedHeight);
                if (!float.IsPositiveInfinity(best)) want = Mathf.Clamp(ground.point.y + ankleHeight - foot.position.y, -0.16f, 0.22f) * planted;
                _feetOffset[side] = Mathf.Lerp(_feetOffset[side], want, 1f - Mathf.Exp(-Style.ContactResponse * _lastDt));
                Vector3 target = foot.position + Vector3.up * _feetOffset[side];
                ProceduralContactIK.SolveTwoBone(_rig.Bone(thigh), _rig.Bone(shin), foot, target, transform.forward, Style.FootPlacement);
                if (!float.IsPositiveInfinity(best))
                    foot.rotation = Quaternion.Slerp(foot.rotation, Quaternion.FromToRotation(transform.up, ground.normal) * foot.rotation, planted * Style.FootPlacement);
            }
        }
    }
}
