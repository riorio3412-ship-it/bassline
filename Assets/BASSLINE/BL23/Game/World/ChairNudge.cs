using BL23.Game.Physicality;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// Pulls/tucks a chair with a bounded horizontal force. Contacts can stop or redirect the chair.
    /// </summary>
    public sealed class ChairNudge : MonoBehaviour
    {
        Rigidbody _body;
        PhysicalProp _prop;
        Vector3 _origin, _direction, _target;
        float _elapsed, _duration, _followOffset = float.NaN, _followDuration;
        bool _moving;

        static ChairNudge For(GameObject chair)
        {
            var nudge = chair.GetComponent<ChairNudge>() ?? chair.AddComponent<ChairNudge>();
            nudge._prop = PhysicalProp.Ensure(chair);
            nudge._body = chair.GetComponent<Rigidbody>();
            return nudge;
        }

        /// <summary>Sitting down: out by 'pull' metres (along 'back'), then in to 'tuck'.</summary>
        public static void Sit(GameObject chair, Vector3 back, float pull = 0.34f, float tuck = 0.12f)
        {
            if (chair == null || !PropPhysicsProfile.Finite(back)) return;
            var n = For(chair);
            if (n._body == null || (n._prop != null && n._prop.Broken)) return;
            if (!n._moving) n._origin = n._body.position;
            n._direction = Vector3.ProjectOnPlane(back, Vector3.up).normalized;
            n.Move(pull, .5f);
            n._followOffset = Mathf.Clamp(tuck, 0f, .5f); n._followDuration = .65f;
        }

        /// <summary>Getting up: out, then back to where the chair stood.</summary>
        public static void Stand(GameObject chair, Vector3 back, float pull = 0.3f)
        {
            if (chair == null || !PropPhysicsProfile.Finite(back)) return;
            var n = For(chair);
            if (n._body == null || (n._prop != null && n._prop.Broken)) return;
            // The chair may have been moved while occupied. Do not drag it back across the room.
            if (n._direction == Vector3.zero || (n._body.position - n._origin).sqrMagnitude > 1f)
                n._origin = n._body.position;
            n._direction = Vector3.ProjectOnPlane(back, Vector3.up).normalized;
            n.Move(pull, .45f);
            n._followOffset = 0f; n._followDuration = .6f;
        }

        void Move(float offset, float duration)
        {
            _target = _origin + _direction * Mathf.Clamp(offset, -.5f, .5f);
            _duration = Mathf.Max(.1f, duration);
            _elapsed = 0f; _moving = true; enabled = true;
            _body.isKinematic = false; _body.WakeUp();
        }

        void FixedUpdate()
        {
            if (!_moving || _body == null || PhysicalCharacter.WorldPaused) return;
            if ((_prop != null && _prop.Broken) || PhysicalGrab.IsHeld(_body)
                || Vector3.Dot(transform.up, Vector3.up) < .7f)
            { Stop(); return; }
            _elapsed += Time.fixedDeltaTime;
            var delta = Vector3.ProjectOnPlane(_target - _body.position, Vector3.up);
            if (delta.sqrMagnitude > 1.44f) { Stop(); return; }
            var planarVelocity = Vector3.ProjectOnPlane(_body.linearVelocity, Vector3.up);
            var force = Vector3.ClampMagnitude(_body.mass * (delta * 45f - planarVelocity * 14f), 180f);
            _body.AddForce(force, ForceMode.Force);
            // Leave a blocked chair where contact allows. No snap at the end, no perpetual kinematic lock.
            if (_elapsed < _duration) return;
            if (!float.IsNaN(_followOffset))
            {
                float next = _followOffset; _followOffset = float.NaN;
                Move(next, _followDuration); return;
            }
            Stop();
        }

        void Stop() { _moving = false; _followOffset = float.NaN; enabled = false; }
        void OnDisable() { _moving = false; }
    }
}
