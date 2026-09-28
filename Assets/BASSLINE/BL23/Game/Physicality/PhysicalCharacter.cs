using System;
using System.Collections.Generic;
using BL23.Game.Characters;
using BL23.Sim;
using BL23.Sim.Physicality;
using UnityEngine;
using Region = BL23.Sim.BodyRegion;

namespace BL23.Game.Physicality
{
    /// <summary>Common contact/balance motor for the entire cast. Story navigation owns normal motion;
    /// impulses temporarily give PhysX authority, feeding the resulting position back to the story state.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class PhysicalCharacter : MonoBehaviour
    {
        public PhysicalityTuning Tuning = new PhysicalityTuning();
        public PhysicalActionController Actions { get; private set; }
        public PhysicalRagdoll Ragdoll { get; private set; }
        public Rigidbody Body { get; private set; }
        public CapsuleCollider Capsule { get; private set; }
        public PhysicalBody State => _actor?.Physical;
        public bool IsPlayer => _actor != null && _actor.IsPlayer;
        public bool DrivesPosition => Ragdoll != null && Ragdoll.Active || _activeSeconds > 0 && !IsPlayer;
        public bool BlocksActions => (Ragdoll != null && Ragdoll.Active) || State != null && (!State.CanAct || State.Reaction >= PhysicalReaction.Stagger && State.ReactionRemaining > 0);
        public Vector3 PhysicalPosition => Ragdoll != null && Ragdoll.Active ? _ragdollFeet : Body != null ? Body.position : transform.position;
        public float FacingYaw => Ragdoll != null && Ragdoll.Active ? _actor.Yaw : Body.rotation.eulerAngles.y;
        public static bool WorldPaused => Session.I != null && (Session.I.Paused || TimeLink.Lapsing || Session.I.S?.Phase == Phase.Trial || Session.I.S?.Phase == Phase.Verdict || Session.I.S?.Phase == Phase.Execution);
        Actor _actor; ActorView _view; ActorRig _rig;
        float _activeSeconds, _braceSeconds, _fallDelay, _impactCooldown, _startleCooldown, _airSeconds;
        Collider _support; Vector3 _supportLocal, _supportWorld;
        bool _resumeDynamic, _restoredDown;
        Vector3 _pausedVelocity, _ragdollFeet, _lastImpulse, _lastPoint;
        readonly Collider[] _overlaps = new Collider[48];
        readonly RaycastHit[] _hits = new RaycastHit[24];
        readonly Dictionary<int, float> _bumps = new Dictionary<int, float>();

        public void Bind(ActorView view, Actor actor)
        {
            _view = view; _actor = actor; _rig = view.Rig;
            bool fresh = actor.Physical == null;
            if (fresh) actor.Physical = new PhysicalBody();
            actor.PhysicsDriven = false;
            Actions = _rig.GetComponent<PhysicalActionController>() ?? _rig.gameObject.AddComponent<PhysicalActionController>(); Actions.Bind(_rig);
            Actions.ContactReached += OnActionContact;
            Ragdoll = GetComponent<PhysicalRagdoll>() ?? gameObject.AddComponent<PhysicalRagdoll>(); Ragdoll.Bind(_rig, this);
            Body = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
            Body.mass = Tuning.BodyMassKg; Body.isKinematic = true; Body.useGravity = true;
            Body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ; Body.angularDamping = 5;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Body.solverIterations = 10; Body.solverVelocityIterations = 4; Body.maxDepenetrationVelocity = 2;
            Capsule = GetComponent<CapsuleCollider>() ?? gameObject.AddComponent<CapsuleCollider>();
            Capsule.height = Mathf.Clamp(_rig.Height, 1.25f, 2.2f); Capsule.radius = .24f; Capsule.center = Vector3.up * (Capsule.height * .5f + .025f);
            Capsule.enabled = !IsPlayer;
            if (fresh)
            {
                foreach (var w in actor.Body.Wounds)
                {
                    if (w.Sev >= 5) BodySolver.MarkSevered(State, w.Region);
                    else BodySolver.ApplyImpact(State, new ImpactInput { Region = w.Region, Type = w.Type, SeverityHint = w.Sev });
                }
                State.Pain = 0; State.Balance = 1; State.ReactionRemaining = 0; State.Reaction = PhysicalReaction.None;
            }
            SyncLimits();
            _restoredDown = State.Posture == PhysicalPosture.Prone || State.Posture == PhysicalPosture.Falling;
        }

        void SyncLimits()
        {
            var b = _actor.Body;
            BodySolver.ApplyLegacyLimits(State, b.Mobility, b.HandL, b.HandR, _actor.Status == ActorStatus.Unconscious ? 0 : b.Conscious, ! _actor.Alive);
        }

        public void FollowPresentation(Vector3 position)
        {
            if (Body == null || DrivesPosition) return;
            if (Body.isKinematic) Body.position = position;
            Capsule.enabled = !IsPlayer && _actor.Status == ActorStatus.Active && _actor.CarriedBy == null && _actor.StairId < 0 &&
                              (_actor.Pose == BL23.Sim.Pose.Stand || _actor.Pose == BL23.Sim.Pose.Crouch);
        }

        public void ResetMotion()
        {
            if (Ragdoll != null) Ragdoll.End(false);
            _activeSeconds = _braceSeconds = _fallDelay = 0; _restoredDown = false; _resumeDynamic = false;
            if (Body != null) { Body.isKinematic = true; Body.position = transform.position; }
            if (_actor != null) _actor.PhysicsDriven = false;
        }

        public void Shove(Vector3 direction, float impulse, Vector3 point)
        {
            if (_actor == null || WorldPaused || !Finite(direction) || !Finite(point) || float.IsNaN(impulse)) return;
            if (_actor.CarriedBy != null || _actor.StairId >= 0) return;
            direction.y = Mathf.Clamp(direction.y, -.2f, .2f);
            if (direction.sqrMagnitude < .0001f) return;
            Receive(Region.Chest, DamageType.Blunt, 0, direction.normalized * Mathf.Clamp(impulse, 0, 200), point, null, true, 0);
        }

        /// <summary>Walk contact is rate-limited per mover, so frame rate cannot multiply shove strength.</summary>
        public void Bump(Vector3 moverVelocity, Vector3 point, int moverId)
        {
            if (_bumps.TryGetValue(moverId, out float last) && Time.time - last < .28f) return;
            float speed = new Vector2(moverVelocity.x, moverVelocity.z).magnitude;
            if (speed < .35f) return;
            _bumps[moverId] = Time.time;
            Shove(moverVelocity, Mathf.Clamp(speed * 14, 8, 70), point);
        }

        public void OnWound(Region region, DamageType type, int severity, Vector3 localPoint, string source, bool postmortem,
            Vector3 contactDirection = default, float contactImpulse = 0, float contactEnergy = 0, Vector3 normal = default, bool resolvedByPhysics = false)
        {
            if (State == null || _view.ReplayDriven) return;
            if (severity >= 5) BodySolver.MarkSevered(State, region);
            var attacker = Session.I?.World?.ViewOf(source ?? "");
            Vector3 dir = attacker != null ? transform.position - attacker.transform.position : -transform.forward;
            if (dir.sqrMagnitude < .001f) dir = -transform.forward;
            dir.y = .08f;
            Vector3 point = localPoint.sqrMagnitude > .0001f ? _rig.transform.TransformPoint(localPoint) : _rig.Bone(_rig.RegionBone((Characters.BodyRegion)region))?.position ?? transform.position + Vector3.up;
            float impulse = (type == DamageType.Cut || type == DamageType.Stab ? 9 : 18) * Mathf.Max(1, severity);
            if (contactImpulse > 0 && Finite(contactDirection)) { dir = contactDirection; impulse = contactImpulse; }
            Receive(region, type, contactImpulse > 0 ? contactEnergy : 8 + 18 * severity, dir.normalized * impulse, point, source, false, Mathf.Min(4, severity), normal, !resolvedByPhysics);
        }

        void Receive(Region region, DamageType type, float energy, Vector3 impulse, Vector3 point, string source, bool transient, int severity, Vector3 normal = default, bool applyImpulse = true)
        {
            if (_actor == null || !Finite(impulse) || !Finite(point)) return;
            SyncLimits();
            bool supported = FindSupport(impulse, out var support);
            Vector3 localDirection = transform.InverseTransformDirection(impulse.normalized), localContact = transform.InverseTransformPoint(point), localNormal = transform.InverseTransformDirection(normal);
            var result = BodySolver.ApplyImpact(State, new ImpactInput {
                Region = region, Type = type, EnergyJoules = energy, Impulse = impulse.magnitude,
                Leverage = Mathf.Clamp((point.y - transform.position.y) / 1.1f, .65f, 1.7f), Supported = _braceSeconds > 0 && Actions.ContactMade,
                SourceId = source, SeverityHint = severity, TransientOnly = transient, HasContact = true,
                DirectionX = localDirection.x, DirectionY = localDirection.y, DirectionZ = localDirection.z,
                ContactX = localContact.x, ContactY = localContact.y, ContactZ = localContact.z,
                NormalX = localNormal.x, NormalY = localNormal.y, NormalZ = localNormal.z }, Tuning);
            _lastImpulse = impulse; _lastPoint = point;
            if (WorldPaused) { if (State.Dead) _restoredDown = true; return; }
            if (!State.Dead) Actions.ReactToImpact(point, impulse.normalized, Mathf.Clamp01(.2f + severity * .18f));
            if (supported && State.CanBrace && State.Balance < .85f)
            {
                var l = _rig.Bone(HBone.UpperArmL); var r = _rig.Bone(HBone.UpperArmR);
                bool left = Actions.CanUseHand(true) && (!Actions.CanUseHand(false) || l != null && r != null && (l.position - support.point).sqrMagnitude <= (r.position - support.point).sqrMagnitude);
                if (Actions.Brace(support.point, support.normal, left, .65f))
                { Drop(left); _support = support.collider; _supportLocal = _support.transform.InverseTransformPoint(support.point); _supportWorld = support.point; }
                else supported = false;
            }
            if (result.ShouldDropLeft) Drop(true);
            if (result.ShouldDropRight) Drop(false);
            if (Ragdoll.Active) { if (applyImpulse) Ragdoll.Begin(Vector3.zero, impulse, point); }
            else if (IsPlayer) { if (applyImpulse) Session.I?.Player?.ApplyExternalImpulse(impulse / Mathf.Max(1, Body.mass)); }
            else if (_actor.CarriedBy == null)
            {
                Capsule.enabled = true; Body.isKinematic = false; Body.WakeUp();
                if (applyImpulse) Body.AddForceAtPosition(Vector3.ClampMagnitude(impulse, 200), point, ForceMode.Impulse);
                _activeSeconds = Mathf.Max(_activeSeconds, .8f); _actor.PhysicsDriven = true;
            }
            if (State.Posture == PhysicalPosture.Falling) _fallDelay = supported ? .45f : .16f;
            if (severity > 0) _rig.SetExpression(Expr.Pain);
        }

        void Drop(bool left)
        {
            var s = Session.I; if (s == null) return;
            var item = s.S.I(left ? _actor.HandL : _actor.HandR);
            if (item != null) s.Sim.DropItem(_actor, item, _actor.Pos);
        }

        void OnActionContact(PhysicalActionKind kind)
        {
            if (kind != PhysicalActionKind.Brace || _support == null || Ragdoll.Active || !State.CanBrace) return;
            var prop = _support.GetComponentInParent<PhysicalProp>(); if (prop != null && !prop.CanSupport) return;
            _braceSeconds = .65f;
            State.Balance = Mathf.Min(1, State.Balance + .35f);
            if (State.Balance > Tuning.FallThreshold && (State.Posture == PhysicalPosture.Falling || State.Posture == PhysicalPosture.Prone))
            { _fallDelay = 0; State.Posture = PhysicalPosture.Staggering; State.PostureSeconds = 0; }
            if (!Body.isKinematic)
            {
                Vector3 v = Body.linearVelocity;
                Vector3 arrest = new Vector3(v.x, 0, v.z) * Body.mass * .35f;
                Body.AddForce(-arrest, ForceMode.Impulse);
                var rb = _support.attachedRigidbody;
                if (rb != null && !rb.isKinematic) rb.AddForceAtPosition(arrest, _supportWorld, ForceMode.Impulse);
            }
        }

        public void Startle(Vector3 source, float strength)
        {
            if (_actor == null || _startleCooldown > 0 || WorldPaused || _actor.IsPlayer || !_actor.Alive) return;
            if (BodySolver.RequestStartle(State, strength, null, Tuning))
            { Actions.Startle(source, strength); _startleCooldown = 1.5f; }
        }

        public void Collapse()
        {
            if (_actor == null || _actor.CarriedBy != null) return;
            SyncLimits();
            if (_actor.Alive && _actor.Status == ActorStatus.Active)
            { State.Posture = PhysicalPosture.Falling; State.PostureSeconds = 0; State.Balance = 0; }
            _fallDelay = .12f;
        }

        void BeginFall()
        {
            if (Ragdoll.Active || _actor.CarriedBy != null) return;
            Vector3 v = !Body.isKinematic ? Body.linearVelocity : _view.Velocity;
            Body.isKinematic = true; Capsule.enabled = false;
            Actions.Cancel();
            _ragdollFeet = transform.position;
            if (Ragdoll.Begin(v, _lastImpulse * .25f + transform.forward * 5, _lastPoint == Vector3.zero ? transform.position + Vector3.up : _lastPoint))
            {
                _actor.PhysicsDriven = true; _activeSeconds = 0;
                Drop(true); Drop(false);
                if (IsPlayer && Session.I?.Player?.CC != null) Session.I.Player.CC.enabled = false;
            }
            else { Capsule.enabled = !IsPlayer; _actor.PhysicsDriven = false; State.Posture = PhysicalPosture.Prone; _rig.Anim?.PlayAction(ActionAnim.Fall, .8f); }
        }

        void FixedUpdate()
        {
            if (_actor == null || _rig == null || _view.ReplayDriven) return;
            bool paused = WorldPaused;
            Ragdoll.SetPaused(paused);
            if (paused)
            {
                if (!Body.isKinematic) { _pausedVelocity = Body.linearVelocity; _resumeDynamic = true; Body.isKinematic = true; }
                return;
            }
            if (_resumeDynamic) { Body.isKinematic = false; Body.linearVelocity = _pausedVelocity; _resumeDynamic = false; }
            float dt = Time.fixedDeltaTime;
            _impactCooldown = Mathf.Max(0, _impactCooldown - dt); _startleCooldown = Mathf.Max(0, _startleCooldown - dt);
            _braceSeconds = Mathf.Max(0, _braceSeconds - dt);
            if (_support != null && (_support.transform.TransformPoint(_supportLocal) - _supportWorld).sqrMagnitude > .04f) _braceSeconds = 0;
            SyncLimits();
            if (_actor.CarriedBy != null)
            { Ragdoll.End(false); _actor.PhysicsDriven = false; Capsule.enabled = false; Body.isKinematic = true; return; }
            if (_restoredDown) { _restoredDown = false; BeginFall(); }
            if (_fallDelay > 0) { _fallDelay -= dt; if (_fallDelay <= 0) BeginFall(); }
            bool ground = GroundAt(Ragdoll.Active ? Ragdoll.PelvisPosition : transform.position + Vector3.up * .2f, out var groundHit);
            if (_activeSeconds > 0 && !ground && !Ragdoll.Active)
            {
                _airSeconds += dt;
                if (_airSeconds > .22f && State.Posture != PhysicalPosture.Falling)
                { State.Posture = PhysicalPosture.Falling; State.PostureSeconds = 0; _fallDelay = .1f; }
            }
            else _airSeconds = 0;
            BodySolver.Step(State, dt, ground && (!Ragdoll.Active || Ragdoll.Speed < 1.4f), _braceSeconds > 0, Tuning);
            if (State.Posture == PhysicalPosture.Falling && !Ragdoll.Active && _fallDelay <= 0) _fallDelay = .12f;
            if (Ragdoll.Active)
            {
                var pelvis = Ragdoll.PelvisPosition;
                _ragdollFeet = new Vector3(pelvis.x, ground ? groundHit.point.y : pelvis.y - .25f, pelvis.z);
                WritePosition(_ragdollFeet);
                if (IsPlayer && Session.I?.Player != null) Session.I.Player.transform.position = _ragdollFeet;
                if ((State.Posture == PhysicalPosture.Recovering || State.Posture == PhysicalPosture.Standing) && ground && ClearToStand(_ragdollFeet))
                {
                    Body.position = _ragdollFeet + Vector3.up * .035f; transform.position = Body.position;
                    _rig.Anim?.SetPosture(Posture.LieBack); Ragdoll.End(); _rig.Anim?.SetPosture(Posture.Stand);
                    Capsule.enabled = !IsPlayer; _activeSeconds = .35f; _actor.PhysicsDriven = true;
                    if (IsPlayer && Session.I?.Player?.CC != null) { Session.I.Player.transform.position = Body.position; Session.I.Player.CC.enabled = true; }
                }
                return;
            }
            if (_activeSeconds > 0)
            {
                if (!Body.isKinematic)
                {
                    Vector3 lateral = Body.linearVelocity; lateral.y = 0;
                    Body.AddForce(-lateral * Mathf.Lerp(2f, 7f, State.Balance), ForceMode.Acceleration);
                }
                WritePosition(Body.position);
                _activeSeconds = Mathf.Max(0, _activeSeconds - dt);
                if (_activeSeconds <= 0 && State.Posture != PhysicalPosture.Falling)
                {
                    if (!Body.isKinematic && Body.linearVelocity.sqrMagnitude > .1f) _activeSeconds = .15f;
                    else { Body.isKinematic = true; _actor.PhysicsDriven = false; }
                }
            }
            else _actor.PhysicsDriven = false;
        }

        void WritePosition(Vector3 position)
        {
            if (!Finite(position) || Session.I == null) return;
            int floor = _actor.Pos.f;
            foreach (var f in Session.I.S.Layout.Floors)
                if (position.y >= f.BaseY - .3f && Mathf.Abs(position.y - f.BaseY) < Mathf.Abs(position.y - Session.I.S.Layout.FloorY(floor))) floor = f.F;
            _actor.Pos = new P3(floor, position.x, position.z); _actor.Room = Session.I.S.Layout.RoomAt(_actor.Pos);
            if (!Ragdoll.Active) _actor.Yaw = Body.rotation.eulerAngles.y;
            _actor.Speed = 0; _actor.Running = false;
        }

        bool GroundAt(Vector3 p, out RaycastHit best)
        {
            best = default; float distance = float.MaxValue;
            int n = Physics.RaycastNonAlloc(p + Vector3.up * .3f, Vector3.down, _hits, 1.1f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i]; if (Own(h.collider) || h.normal.y < .55f) continue;
                if (h.distance < distance) { distance = h.distance; best = h; }
            }
            return distance < float.MaxValue;
        }

        bool ClearToStand(Vector3 p)
        {
            float radius = Capsule.radius * .9f;
            int n = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * (radius + .1f), p + Vector3.up * (Capsule.height - radius), radius, _overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!Own(_overlaps[i])) return false;
            return n < _overlaps.Length;
        }

        bool FindSupport(Vector3 direction, out RaycastHit best)
        {
            best = default; if (State == null || !State.CanBrace) return false;
            Vector3 shoulder = transform.position + Vector3.up * Mathf.Clamp(Capsule.height * .65f, .75f, 1.25f);
            direction.y = 0; if (direction.sqrMagnitude < .001f) direction = transform.forward; direction.Normalize();
            Vector3[] probes = { direction, (direction + transform.right * .75f).normalized, (direction - transform.right * .75f).normalized };
            float nearest = float.MaxValue;
            foreach (var dir in probes)
            {
                // A top surface must exist within the arm's reachable volume. A wall face is also a useful brace.
                int n = Physics.RaycastNonAlloc(shoulder + dir * .48f + Vector3.up * .18f, Vector3.down, _hits, .7f, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++) ConsiderSupport(_hits[i], shoulder, ref nearest, ref best);
                n = Physics.RaycastNonAlloc(shoulder, dir, _hits, .68f, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++) ConsiderSupport(_hits[i], shoulder, ref nearest, ref best);
            }
            return nearest < float.MaxValue;
        }

        void ConsiderSupport(RaycastHit hit, Vector3 shoulder, ref float nearest, ref RaycastHit best)
        {
            if (Own(hit.collider) || hit.collider.GetComponentInParent<ActorRig>() != null || hit.collider.GetComponentInParent<PhysicalCharacter>() != null) return;
            if (hit.point.y - transform.position.y < .48f) return;
            var prop = hit.collider.GetComponentInParent<PhysicalProp>();
            if (prop != null && !prop.CanSupport) return;
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic && hit.rigidbody.mass < 12) return;
            float dist = (hit.point - shoulder).sqrMagnitude;
            if (dist < nearest && dist < .72f * .72f) { nearest = dist; best = hit; }
        }

        bool Own(Collider c)
        {
            if (c == null) return true;
            if (c.transform.IsChildOf(transform)) return true;
            var part = c.GetComponentInParent<PhysicalBodyPart>(); if (part != null && part.Owner == this) return true;
            return IsPlayer && Session.I?.Player != null && c.transform.IsChildOf(Session.I.Player.transform);
        }

        void OnCollisionEnter(Collision collision) { ReceiveCollision(collision); }
        public void ReceiveCollision(Collision collision, Region? hitRegion = null)
        {
            if (_actor == null || WorldPaused || _impactCooldown > 0 || collision.contactCount == 0) return;
            var other = collision.rigidbody; if (Own(collision.collider)) return;
            if ((other == null || other.isKinematic) && Body.isKinematic && !Ragdoll.Active) return;
            float speed = collision.relativeVelocity.magnitude; if (speed < 1.2f) return;
            var contact = collision.GetContact(0);
            float impulse = Mathf.Min(140, collision.impulse.magnitude);
            if (impulse < 2) return;
            _impactCooldown = .3f;
            Vector3 direction = collision.impulse.sqrMagnitude > .0001f ? collision.impulse.normalized : contact.normal;
            Region region = hitRegion ?? (Region)_rig.NearestRegion(contact.point);
            float ownMass = hitRegion.HasValue ? hitRegion.Value == Region.Head ? 5f : 12f : Body.mass;
            float effectiveMass = other == null || other.isKinematic ? ownMass : other.mass * ownMass / Mathf.Max(.05f, other.mass + ownMass);
            float closingSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            float energy = .5f * effectiveMass * closingSpeed * closingSpeed;
            string source = PhysicsGrab.RecentlyPlayer(other) ? Cast.Player : null;
            var session = Session.I;
            // Upright navigation is kinematic until first contact, so PhysX has not yet moved this actor.
            // Once dynamic (including ragdoll), the collision impulse is already resolved exactly once.
            bool momentumResolved = Ragdoll.Active || !Body.isKinematic;
            if (energy >= 18 && session != null)
            {
                Vector3 lp = _rig.transform.InverseTransformPoint(contact.point); direction.Normalize();
                int severity = energy >= 90 ? 2 : 1;
                var tag = other != null ? other.GetComponentInParent<ItemTag>() : null;
                session.Sim.Strike(source, _actor, region, DamageType.Blunt, severity, tag?.ItemId, "physical contact", lp.x, lp.y, lp.z,
                    direction.x, direction.y, direction.z, impulse, energy, contact.normal.x, contact.normal.y, contact.normal.z, momentumResolved);
                if (source == Cast.Player && !_actor.IsPlayer && _actor.Alive)
                    Relations.Change(session.S, _actor.Id, source, like: -.04f * severity, grudge: .05f * severity, memory: "민혁이 움직인 물건에 맞았다");
            }
            else Receive(region, DamageType.Blunt, energy, direction.normalized * impulse, contact.point, source, true, 0, contact.normal, !momentumResolved);
        }

        static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        void OnDisable()
        {
            if (_actor != null) _actor.PhysicsDriven = false;
            if (Body != null) Body.isKinematic = true;
        }
        void OnDestroy() { if (Actions != null) Actions.ContactReached -= OnActionContact; }
    }

    public sealed class PhysicalBodyPart : MonoBehaviour
    {
        public PhysicalCharacter Owner;
        public Region Region;
        void OnCollisionEnter(Collision contact) { Owner?.ReceiveCollision(contact, Region); }
    }
}
