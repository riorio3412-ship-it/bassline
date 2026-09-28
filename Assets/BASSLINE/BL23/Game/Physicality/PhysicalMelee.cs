using System;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Physicality
{
    /// <summary>One attack = wind-up, swept contact window, recovery. Contact normal, point and
    /// incoming velocity travel into the saved wound. Solid obstacles stop the sweep before actors.</summary>
    public sealed class PhysicalMelee : MonoBehaviour
    {
        PlayerController _player; Session _session; Item _weapon;
        float _clock; bool _swinging, _resolved; Vector3 _previousTip; bool _haveTip;
        Vector3 _origin, _forward, _right, _up; ActionAnim _motion;
        readonly RaycastHit[] _hits = new RaycastHit[96];
        public bool Swinging => _swinging;
        public void Init(Session session, PlayerController player) { _session = session; _player = player; }

        public bool Begin()
        {
            if (_swinging || _session == null || _player?.Cam == null || PhysicalCharacter.WorldPaused) return false;
            var me = _session.S.Player;
            var physical = _session.World.ViewOf(me.Id)?.Physical;
            if (physical != null && physical.BlocksActions) return false;
            _weapon = _session.Sim.Held(me, d => d != null && d.IsWeapon);
            if (_weapon == null) return false;
            _clock = 0; _resolved = _haveTip = false; _swinging = true;
            var cam = _player.Cam.transform;
            _origin = cam.position; _forward = cam.forward; _right = cam.right; _up = cam.up;
            _motion = _weapon.Def.Dmg == DamageType.Stab ? ActionAnim.Stab : _weapon.Def.Dmg == DamageType.Cut ? ActionAnim.Slash : ActionAnim.Overhead;
            var av = _session.World.ViewOf(me.Id);
            av?.Rig?.Anim?.PlayAction(_motion, .66f);
            physical?.Actions?.Swing(_motion, _origin + _forward * Mathf.Clamp(_weapon.Def.Reach + .65f, .75f, 2.2f), false);
            Audio.Sfx.Play("blade_whoosh", cam.position, .6f);
            return true;
        }

        void FixedUpdate()
        {
            if (!_swinging) return;
            if (PhysicalCharacter.WorldPaused || _session == null || _weapon == null || _weapon.Holder != Cast.Player ||
                _session.Sim.Held(_session.S.Player, d => d != null && d.IsWeapon)?.Id != _weapon.Id || !_session.S.Player.Alive ||
                _session.S.Player.Status != ActorStatus.Active || _session.World.ViewOf(Cast.Player)?.Physical?.BlocksActions == true)
            { Cancel(); return; }
            float old = _clock; _clock += Time.fixedDeltaTime;
            if (_clock >= .66f) { Cancel(); return; }
            if (_clock < .16f || old > .4f || _resolved) return;
            float phase = Mathf.Clamp01((_clock - .16f) / .24f);
            // The arc is captured at wind-up; turning the camera cannot hit somebody behind the swing.
            float reach = Mathf.Clamp(_weapon.Def.Reach + .8f, .85f, 2.4f);
            float x = _motion == ActionAnim.Slash ? Mathf.Lerp(.72f, -.72f, phase) : .03f;
            float y = _motion == ActionAnim.Overhead ? Mathf.Lerp(.6f, -.65f, phase) : -.12f;
            float z = _motion == ActionAnim.Stab ? Mathf.Lerp(.32f, reach, Mathf.Sin(phase * Mathf.PI * .5f)) : reach * (.75f + .25f * Mathf.Sin(phase * Mathf.PI));
            Vector3 tip = _origin + _forward * z + _right * x + _up * y;
            Vector3 basePoint = _origin + _forward * .2f - _up * .12f;
            Vector3 previous = _haveTip ? _previousTip : basePoint;
            Vector3 motion = tip - previous;
            float speed = _haveTip ? motion.magnitude / Mathf.Max(.005f, Time.fixedDeltaTime) : reach / .24f;
            // Across frames (continuous blade tip), and along the blade (catches targets already inside its arc).
            if (!Sweep(previous, tip, basePoint, motion.normalized, speed))
                Sweep(basePoint, tip, basePoint, motion.sqrMagnitude > .0001f ? motion.normalized : _forward, speed);
            _previousTip = tip; _haveTip = true;
        }

        bool Sweep(Vector3 from, Vector3 to, Vector3 shoulder, Vector3 incoming, float speed)
        {
            Vector3 path = to - from; if (path.sqrMagnitude < .00001f) return false;
            int count = Physics.SphereCastNonAlloc(from, .055f, path.normalized, _hits, path.magnitude, ~0, QueryTriggerInteraction.Collide);
            if (count == _hits.Length) { _resolved = true; return true; }
            // Deterministic nearest order; no LINQ allocation inside the contact window.
            Array.Sort(_hits, 0, count, HitComparer.Instance);
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i]; var col = hit.collider;
                if (col == null || col.transform.IsChildOf(_player.transform)) continue;
                var rig = col.GetComponentInParent<ActorRig>();
                var motor = col.GetComponentInParent<PhysicalCharacter>();
                var part = col.GetComponentInParent<PhysicalBodyPart>();
                if (rig == null && motor != null) rig = motor.GetComponent<ActorView>()?.Rig;
                if (rig == null && part?.Owner != null) rig = part.Owner.GetComponent<ActorView>()?.Rig;
                if (rig != null && rig.ActorId == Cast.Player) continue;
                // The broad locomotion capsule must never hide the more precise animated regional hitboxes.
                if (motor != null && col == motor.Capsule) continue;
                if (col.isTrigger && rig == null) continue;
                if (Obstructed(shoulder, hit.point, col)) { _resolved = true; return true; }
                _resolved = true;
                float incidence = Mathf.Abs(Vector3.Dot(incoming, hit.normal));
                float mass = Mathf.Clamp(_weapon.Def.Mass, .15f, 8f);
                speed = Mathf.Clamp(speed, 1.5f, 12f);
                float energy = .5f * mass * speed * speed;
                float impulse = Mathf.Clamp(mass * speed * (1f + incidence), 4f, 125f);
                if (rig != null)
                {
                    var target = _session.S.A(rig.ActorId); if (target == null) return true;
                    var region = part != null ? part.Region : col.isTrigger ? (BL23.Sim.BodyRegion)rig.RegionFromCollider(col) : (BL23.Sim.BodyRegion)rig.NearestRegion(hit.point);
                    if (!_session.S.Flags.ContainsKey("attackconfirm:" + target.Id) && target.Alive && target.Status == ActorStatus.Active)
                    {
                        _session.S.Flags["attackconfirm:" + target.Id] = 1;
                        Hud.I?.Toast(LineBank.FixParticles("한 번 더 휘두르면 " + Cast.GivenOf(target.Id) + "을(를) 정말로 공격한다"), Pal.Blood, 2.5f);
                        _session.World.ViewOf(target.Id)?.Physical?.Startle(_origin, .7f); return true;
                    }
                    var lp = rig.transform.InverseTransformPoint(hit.point);
                    float angleFactor = _weapon.Def.Dmg == DamageType.Stab ? Mathf.Lerp(.35f, 1, incidence) : Mathf.Lerp(.65f, 1, incidence);
                    float scale = angleFactor * Mathf.Clamp(speed / 5f, .55f, 1.2f);
                    _session.Sim.PlayerStrike(target, region, lp.x, lp.y, lp.z, incoming.x, incoming.y, incoming.z, impulse, energy,
                        hit.normal.x, hit.normal.y, hit.normal.z, scale);
                }
                else
                {
                    var prop = col.GetComponentInParent<PhysicalProp>();
                    if (prop == null && hit.rigidbody != null) prop = PhysicalProp.Ensure(hit.rigidbody.gameObject);
                    if (prop != null)
                    {
                        if (prop.Body != null) PhysicsGrab.LastTouchedByPlayer[prop.Body] = Time.time;
                        prop.ApplyImpact(hit.point, incoming * impulse, energy, _weapon.Def.Dmg == DamageType.Cut || _weapon.Def.Dmg == DamageType.Stab ? .85f : 0);
                    }
                }
                return true;
            }
            // A saturated buffer is conservatively a stopped swing, never permission to strike through blockers.
            if (count == _hits.Length) _resolved = true;
            return _resolved;
        }

        bool Obstructed(Vector3 from, Vector3 point, Collider target)
        {
            var ray = point - from;
            var blocking = Physics.RaycastAll(from, ray.normalized, ray.magnitude, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in blocking)
            {
                if (h.collider == target || h.collider.transform.IsChildOf(_player.transform)) continue;
                var rig = h.collider.GetComponentInParent<ActorRig>(); if (rig != null) continue;
                var actor = h.collider.GetComponentInParent<PhysicalCharacter>(); if (actor != null) continue;
                var part = h.collider.GetComponentInParent<PhysicalBodyPart>(); if (part != null) continue;
                if (h.distance < ray.magnitude - .04f) return true;
            }
            return false;
        }
        public void Cancel() { _swinging = false; _weapon = null; _haveTip = false; }
        void OnDisable() { Cancel(); }
        sealed class HitComparer : System.Collections.Generic.IComparer<RaycastHit>
        { public static readonly HitComparer Instance = new HitComparer(); public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance); }
    }
}
