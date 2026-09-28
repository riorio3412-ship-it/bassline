using System.Collections.Generic;
using BL23.Game.Characters;
using UnityEngine;

namespace BL23.Game.Physicality
{
    /// <summary>Lazy, joint-limited PhysX proxy. The render skeleton never owns dynamic rigidbodies.
    /// Animation owns bones normally; this component owns them only while down and during recovery blending.</summary>
    [DefaultExecutionOrder(300)]
    public sealed class PhysicalRagdoll : MonoBehaviour
    {
        sealed class Link
        {
            public Transform Bone;
            public Rigidbody Body;
            public Collider Collider;
            public Quaternion SavedRotation;
            public Vector3 SavedPosition;
        }
        readonly List<Link> _links = new List<Link>();
        ActorRig _rig; Transform _root; PhysicalCharacter _owner;
        float _blend; bool _paused;
        readonly List<Vector3> _velocities = new List<Vector3>(), _angular = new List<Vector3>();
        public bool Active { get; private set; }
        public Vector3 PelvisPosition => _links.Count > 0 ? _links[0].Body.position : transform.position;
        public float Speed => _links.Count > 0 ? _links[0].Body.linearVelocity.magnitude : 0;
        public int BodyCount => _links.Count;

        public void Bind(ActorRig rig, PhysicalCharacter owner) { _rig = rig; _owner = owner; }

        void Build()
        {
            if (_root != null || _rig == null) return;
            _root = new GameObject("Physical body " + _rig.ActorId).transform;
            _root.SetParent(transform.parent, false);
            var map = new Dictionary<HBone, Link>();
            Add(HBone.Hips, HBone.Spine, null, 12, .13f, map);
            Add(HBone.Chest, HBone.Neck, HBone.Hips, 18, .14f, map);
            Add(HBone.Head, HBone.Head, HBone.Chest, 5, .105f, map);
            Add(HBone.UpperArmL, HBone.LowerArmL, HBone.Chest, 2.8f, .052f, map);
            Add(HBone.LowerArmL, HBone.HandL, HBone.UpperArmL, 1.8f, .043f, map);
            Add(HBone.UpperArmR, HBone.LowerArmR, HBone.Chest, 2.8f, .052f, map);
            Add(HBone.LowerArmR, HBone.HandR, HBone.UpperArmR, 1.8f, .043f, map);
            Add(HBone.UpperLegL, HBone.LowerLegL, HBone.Hips, 8.5f, .073f, map);
            Add(HBone.LowerLegL, HBone.FootL, HBone.UpperLegL, 4.2f, .06f, map);
            Add(HBone.UpperLegR, HBone.LowerLegR, HBone.Hips, 8.5f, .073f, map);
            Add(HBone.LowerLegR, HBone.FootR, HBone.UpperLegR, 4.2f, .06f, map);
            var own = GetComponentsInChildren<Collider>(true);
            foreach (var a in _links)
            {
                foreach (var b in _links) if (a != b) Physics.IgnoreCollision(a.Collider, b.Collider);
                foreach (var c in own) if (c != null) Physics.IgnoreCollision(a.Collider, c);
                var player = Session.I?.Player;
                if (_owner != null && _owner.IsPlayer && player?.CC != null) Physics.IgnoreCollision(a.Collider, player.CC);
            }
        }

        void Add(HBone from, HBone to, HBone? parent, float mass, float radius, Dictionary<HBone, Link> map)
        {
            var bone = _rig.Bone(from); var end = _rig.Bone(to);
            if (bone == null || end == null) return;
            var go = new GameObject(from.ToString()); go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(bone.position, bone.rotation);
            var rb = go.AddComponent<Rigidbody>(); rb.mass = mass; rb.isKinematic = true;
            var part = go.AddComponent<PhysicalBodyPart>(); part.Owner = _owner;
            part.Region = from == HBone.Head ? BL23.Sim.BodyRegion.Head : from == HBone.Hips ? BL23.Sim.BodyRegion.Abdomen :
                from == HBone.UpperArmL || from == HBone.LowerArmL ? BL23.Sim.BodyRegion.ArmL :
                from == HBone.UpperArmR || from == HBone.LowerArmR ? BL23.Sim.BodyRegion.ArmR :
                from == HBone.UpperLegL || from == HBone.LowerLegL ? BL23.Sim.BodyRegion.LegL :
                from == HBone.UpperLegR || from == HBone.LowerLegR ? BL23.Sim.BodyRegion.LegR : BL23.Sim.BodyRegion.Chest;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.solverIterations = 12; rb.solverVelocityIterations = 4;
            rb.maxAngularVelocity = 12; rb.maxDepenetrationVelocity = 2;
            rb.linearDamping = .12f; rb.angularDamping = .6f;
            float scale = Mathf.Clamp(_rig.Height / 1.75f, .6f, 1.6f);
            var cgo = new GameObject("Shape"); cgo.transform.SetParent(go.transform, false);
            Vector3 delta = end.position - bone.position;
            if (from == HBone.Head) delta = _rig.transform.up * (.14f * scale);
            cgo.transform.position = bone.position + delta * .5f;
            cgo.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta.sqrMagnitude > .0001f ? delta.normalized : Vector3.up);
            var col = cgo.AddComponent<CapsuleCollider>(); col.radius = radius * scale;
            col.height = Mathf.Max(col.radius * 2, delta.magnitude + col.radius); col.enabled = false;
            var link = new Link { Bone = bone, Body = rb, Collider = col }; _links.Add(link); map[from] = link;
            if (parent.HasValue && map.TryGetValue(parent.Value, out var p))
            {
                var j = go.AddComponent<ConfigurableJoint>(); j.connectedBody = p.Body;
                j.autoConfigureConnectedAnchor = false; j.anchor = Vector3.zero;
                j.connectedAnchor = p.Body.transform.InverseTransformPoint(bone.position);
                j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
                j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Limited;
                bool knee = from == HBone.LowerLegL || from == HBone.LowerLegR;
                bool elbow = from == HBone.LowerArmL || from == HBone.LowerArmR;
                j.lowAngularXLimit = new SoftJointLimit { limit = knee ? -100 : elbow ? -110 : -35 };
                j.highAngularXLimit = new SoftJointLimit { limit = knee || elbow ? 15 : 50 };
                j.angularYLimit = new SoftJointLimit { limit = knee || elbow ? 12 : 40 };
                j.angularZLimit = new SoftJointLimit { limit = knee || elbow ? 12 : 55 };
                j.projectionMode = JointProjectionMode.PositionAndRotation;
                j.projectionDistance = .04f; j.projectionAngle = 15;
                j.enableCollision = false;
            }
        }

        public bool Begin(Vector3 velocity, Vector3 impulse, Vector3 contact)
        {
            if (_rig == null || _rig.Bone(HBone.Hips) == null) return false;
            Build(); if (_links.Count == 0) return false;
            if (!Active)
            {
                foreach (var l in _links)
                {
                    l.Body.position = l.Bone.position; l.Body.rotation = l.Bone.rotation;
                    l.Collider.enabled = true; l.Body.isKinematic = false;
                    l.Body.linearVelocity = Vector3.ClampMagnitude(velocity, 12); l.Body.angularVelocity = Vector3.zero;
                }
                Active = true; _blend = 0;
                if (_rig.Anim != null) _rig.Anim.PhysicsDriven = true;
            }
            Link near = _links[0]; float best = float.MaxValue;
            foreach (var l in _links) { float d = (l.Body.worldCenterOfMass - contact).sqrMagnitude; if (d < best) { best = d; near = l; } }
            if (!_paused) near.Body.AddForceAtPosition(Vector3.ClampMagnitude(impulse, 160), contact, ForceMode.Impulse);
            return true;
        }

        public void SetPaused(bool pause)
        {
            if (!Active || pause == _paused) return;
            _paused = pause;
            if (pause)
            {
                _velocities.Clear(); _angular.Clear();
                foreach (var l in _links) { _velocities.Add(l.Body.linearVelocity); _angular.Add(l.Body.angularVelocity); l.Body.isKinematic = true; }
            }
            else for (int i = 0; i < _links.Count; i++)
            { _links[i].Body.isKinematic = false; _links[i].Body.linearVelocity = _velocities[i]; _links[i].Body.angularVelocity = _angular[i]; }
        }

        public void End(bool blend = true)
        {
            if (!Active) return;
            foreach (var l in _links)
            {
                l.SavedPosition = l.Body.position; l.SavedRotation = l.Body.rotation;
                l.Body.isKinematic = true; l.Collider.enabled = false;
            }
            Active = false; _paused = false; _blend = blend ? 1 : 0;
            if (_rig?.Anim != null) _rig.Anim.PhysicsDriven = false;
        }

        void LateUpdate()
        {
            if (_rig == null) return;
            if (Active)
            {
                foreach (var l in _links) l.Bone.SetPositionAndRotation(l.Body.position, l.Body.rotation);
            }
            else if (_blend > 0)
            {
                if (!PhysicalCharacter.WorldPaused) _blend = Mathf.Max(0, _blend - Time.deltaTime / .75f);
                float w = _blend * _blend * (3 - 2 * _blend);
                foreach (var l in _links)
                    l.Bone.SetPositionAndRotation(Vector3.Lerp(l.Bone.position, l.SavedPosition, w), Quaternion.Slerp(l.Bone.rotation, l.SavedRotation, w));
            }
        }
        void OnDisable() { End(false); }
        void OnDestroy() { if (_root != null) { if (Application.isPlaying) Destroy(_root.gameObject); else DestroyImmediate(_root.gameObject); } }
    }
}
