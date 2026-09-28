using System;
using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game.Physicality
{
    /// <summary>
    /// Reusable, exclusive physical grip for player or NPC hands. Apply MoveGrip in FixedUpdate.
    /// A force at the actual contact point preserves weight, pendulum motion, wall contact and torque.
    /// </summary>
    public sealed class PhysicalGrab : IDisposable
    {
        public Rigidbody Body { get; private set; }
        public Vector3 WorldGrip => Body != null ? Body.transform.TransformPoint(_localGrip) : Vector3.zero;
        public float ForceLimit = 650f;
        public float MaximumMass = 30f;
        public float BreakDistance = 2.6f;
        public bool Active => Body != null;

        static readonly Dictionary<Rigidbody, PhysicalGrab> Owners = new Dictionary<Rigidbody, PhysicalGrab>();
        readonly List<Collider> _ignored = new List<Collider>();
        Collider _ownerCollider;
        Vector3 _localGrip;
        float _oldAngularDamping, _strain;

        public static bool IsHeld(Rigidbody body) => body != null && Owners.ContainsKey(body);

        public bool TryAcquire(Rigidbody body, Vector3 worldGripPoint, Collider ownerCollider = null)
        {
            if (body == null || body.isKinematic || !body.gameObject.activeInHierarchy || body.mass > MaximumMass
                || Owners.ContainsKey(body) || !PropPhysicsProfile.Finite(worldGripPoint)) return false;
            Release(Vector3.zero);
            Body = body;
            Owners[body] = this;
            _localGrip = body.transform.InverseTransformPoint(worldGripPoint);
            _oldAngularDamping = body.angularDamping;
            _ownerCollider = ownerCollider;
            _strain = 0f;
            body.angularDamping = Mathf.Max(body.angularDamping, 1.2f);
            if (ownerCollider != null)
            {
                foreach (var collider in body.GetComponentsInChildren<Collider>())
                {
                    if (collider.attachedRigidbody != body || collider == ownerCollider || Physics.GetIgnoreCollision(collider, ownerCollider)) continue;
                    Physics.IgnoreCollision(collider, ownerCollider, true);
                    _ignored.Add(collider);
                }
            }
            body.WakeUp();
            return true;
        }

        /// <returns>False after a lost grip, ownership change, obstruction or excessive strain.</returns>
        public bool MoveGrip(Vector3 target, Vector3 targetVelocity, float fixedDeltaTime)
        {
            if (Body == null || Body.isKinematic || !Body.gameObject.activeInHierarchy
                || !PropPhysicsProfile.Finite(target) || !PropPhysicsProfile.Finite(targetVelocity)
                || !PropPhysicsProfile.Finite(fixedDeltaTime) || fixedDeltaTime <= 0f
                || !PropPhysicsProfile.Finite(ForceLimit) || !PropPhysicsProfile.Finite(BreakDistance) || BreakDistance <= 0f)
            {
                Release(Vector3.zero);
                return false;
            }
            var point = WorldGrip;
            var error = target - point;
            if (error.sqrMagnitude > BreakDistance * BreakDistance)
            {
                Release(Vector3.zero);
                return false;
            }
            // A backward-Euler PD controller is stable across fixed timestep changes. Force capping preserves
            // inertia and contact resistance, so an obstructed object cannot be dragged through a wall.
            float omega = Mathf.Lerp(12f, 7f, Mathf.InverseLerp(.5f, MaximumMass, Body.mass));
            float kp = omega * omega, kd = 2f * omega;
            float denominator = 1f + kd * fixedDeltaTime + kp * fixedDeltaTime * fixedDeltaTime;
            var relativeVelocity = Vector3.ClampMagnitude(targetVelocity, 8f) - Body.GetPointVelocity(point);
            var acceleration = (error * kp + relativeVelocity * (kd + kp * fixedDeltaTime)) / denominator;
            var force = Vector3.ClampMagnitude(Body.mass * (acceleration - Physics.gravity), Mathf.Clamp(ForceLimit, 0f, 2000f));
            Body.AddForceAtPosition(force, point, ForceMode.Force);
            _strain = error.magnitude > .85f ? _strain + fixedDeltaTime : Mathf.Max(0f, _strain - fixedDeltaTime * 2f);
            if (_strain > .75f) { Release(Vector3.zero); return false; }
            return true;
        }

        /// <summary>Release with additional N*s, retaining the object's existing linear and angular momentum.</summary>
        public void Release(Vector3 impulse)
        {
            var body = Body;
            // ReferenceEquals deliberately distinguishes a destroyed Unity object from a real null dictionary key.
            if (!ReferenceEquals(body, null)) Owners.Remove(body);
            if (_ownerCollider != null)
                foreach (var collider in _ignored)
                    if (collider != null) Physics.IgnoreCollision(collider, _ownerCollider, false);
            _ignored.Clear();
            _ownerCollider = null;
            if (body != null)
            {
                body.angularDamping = _oldAngularDamping;
                if (!body.isKinematic && PropPhysicsProfile.Finite(impulse))
                    body.AddForce(Vector3.ClampMagnitude(impulse, 120f), ForceMode.Impulse);
            }
            Body = null;
            _strain = 0f;
        }

        public void Dispose() => Release(Vector3.zero);
    }
}
