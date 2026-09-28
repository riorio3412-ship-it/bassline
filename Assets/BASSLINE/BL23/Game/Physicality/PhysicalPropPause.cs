using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game.Physicality
{
    /// <summary>One pause coordinator per Session. Preserves prop momentum through UI pause and simulated time skips.</summary>
    [DefaultExecutionOrder(-900)]
    public sealed class PhysicalPropPause : MonoBehaviour
    {
        struct Snapshot
        {
            public bool Kinematic, Sleeping;
            public Vector3 Linear, Angular, Position;
            public Quaternion Rotation;
        }

        static PhysicalPropPause _instance;
        readonly HashSet<PhysicalProp> _props = new HashSet<PhysicalProp>();
        readonly Dictionary<PhysicalProp, Snapshot> _snapshots = new Dictionary<PhysicalProp, Snapshot>();
        bool _paused;

        internal static void Register(PhysicalProp prop)
        {
            if (prop == null || prop.Body == null || Session.I == null) return;
            if (_instance == null)
                _instance = Session.I.GetComponent<PhysicalPropPause>() ?? Session.I.gameObject.AddComponent<PhysicalPropPause>();
            _instance._props.Add(prop);
            if (_instance._paused || PhysicalCharacter.WorldPaused) _instance.Freeze(prop);
        }

        internal static void Unregister(PhysicalProp prop)
        {
            if (_instance == null) return;
            _instance._props.Remove(prop); _instance._snapshots.Remove(prop);
        }

        internal static bool OriginalKinematic(PhysicalProp prop)
        {
            if (_instance != null && _instance._snapshots.TryGetValue(prop, out var state)) return state.Kinematic;
            return prop.Body != null && prop.Body.isKinematic;
        }

        internal static void SetRepairedOwnership(PhysicalProp prop, bool kinematic)
        {
            if (_instance == null || !PhysicalCharacter.WorldPaused) return;
            _instance.Freeze(prop);
            if (!_instance._snapshots.TryGetValue(prop, out var state)) return;
            state.Kinematic = kinematic; state.Sleeping = false;
            state.Linear = Vector3.zero; state.Angular = Vector3.zero;
            _instance._snapshots[prop] = state;
        }

        void FixedUpdate()
        {
            bool pause = PhysicalCharacter.WorldPaused;
            if (pause)
            {
                // The existing room wake manager can toggle sleeping bodies during a paused Update.
                // Enforce the snapshot in FixedUpdate before the next physics simulation.
                foreach (var prop in _props) Freeze(prop);
            }
            else if (_paused || _snapshots.Count > 0) Restore();
            _paused = pause;
        }

        void Freeze(PhysicalProp prop)
        {
            if (prop == null || prop.Body == null) return;
            var body = prop.Body;
            if (!_snapshots.ContainsKey(prop))
            {
                _snapshots[prop] = new Snapshot { Kinematic = body.isKinematic, Sleeping = body.IsSleeping(),
                    Linear = body.isKinematic ? Vector3.zero : body.linearVelocity,
                    Angular = body.isKinematic ? Vector3.zero : body.angularVelocity,
                    Position = body.position, Rotation = body.rotation };
            }
            if (!body.isKinematic) body.isKinematic = true;
        }

        void Restore()
        {
            foreach (var pair in _snapshots)
            {
                var prop = pair.Key;
                if (prop == null || prop.Body == null) continue;
                var body = prop.Body;
                if (prop.Broken && !HasCollision(body)) continue;
                var item = body.GetComponentInParent<ItemView>();
                // A simulated time skip may have moved this item into a hand or drawer while paused.
                if (item != null && (item.Held || item.InContainer)) { body.isKinematic = true; continue; }
                var state = pair.Value;
                body.isKinematic = state.Kinematic;
                if (!state.Kinematic)
                {
                    bool relocated = (body.position - state.Position).sqrMagnitude > .0004f
                        || Quaternion.Angle(body.rotation, state.Rotation) > 1f;
                    body.linearVelocity = relocated ? Vector3.zero : state.Linear;
                    body.angularVelocity = relocated ? Vector3.zero : state.Angular;
                    if (state.Sleeping) body.Sleep(); else body.WakeUp();
                }
            }
            _snapshots.Clear();
        }

        static bool HasCollision(Rigidbody body)
        {
            foreach (var collider in body.GetComponentsInChildren<Collider>()) if (collider.enabled && !collider.isTrigger) return true;
            return false;
        }

        void OnDisable() { Restore(); _paused = false; }
        void OnDestroy() { if (_instance == this) _instance = null; }
    }
}
