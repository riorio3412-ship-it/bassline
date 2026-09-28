using System;
using System.Collections.Generic;
using BL23.Game.Mansion;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Physicality
{
    /// <summary>Material mechanics in SI units. Independent of meshes, particles and character personality.</summary>
    [Serializable]
    public struct PropPhysicsProfile
    {
        public float StaticFriction, DynamicFriction, Restitution, YieldEnergy, FractureEnergy;

        public static PropPhysicsProfile For(Mat material)
        {
            var p = new PropPhysicsProfile { StaticFriction = .65f, DynamicFriction = .48f,
                Restitution = .06f, YieldEnergy = 18f, FractureEnergy = 120f };
            switch (material)
            {
                case Mat.Glass: p.StaticFriction = .42f; p.DynamicFriction = .3f; p.YieldEnergy = 1.8f; p.FractureEnergy = 5f; break;
                case Mat.Ceramic: p.YieldEnergy = 2.5f; p.FractureEnergy = 8f; break;
                case Mat.Metal: p.StaticFriction = .5f; p.DynamicFriction = .36f; p.YieldEnergy = 65f; p.FractureEnergy = 380f; break;
                case Mat.Stone: p.StaticFriction = .8f; p.DynamicFriction = .65f; p.YieldEnergy = 95f; p.FractureEnergy = 600f; break;
                case Mat.Plastic: p.YieldEnergy = 10f; p.FractureEnergy = 60f; p.Restitution = .15f; break;
                case Mat.Cloth: case Mat.Leather: p.StaticFriction = .85f; p.DynamicFriction = .7f; p.YieldEnergy = 40f; p.FractureEnergy = 220f; break;
                case Mat.Paper: p.YieldEnergy = 1.5f; p.FractureEnergy = 10f; break;
                case Mat.Food: case Mat.Plant: case Mat.Flesh: p.YieldEnergy = 2f; p.FractureEnergy = 15f; break;
                case Mat.Liquid: p.YieldEnergy = float.PositiveInfinity; p.FractureEnergy = float.PositiveInfinity; break;
            }
            return p;
        }

        /// <summary>Energy dissipated along a collision normal; resting forces and tangential sliding do not damage.</summary>
        public static float ImpactEnergy(float normalImpulse, float closingSpeed)
        {
            if (!Finite(normalImpulse) || !Finite(closingSpeed) || normalImpulse <= 0f || closingSpeed < .75f) return 0f;
            return Mathf.Min(100000f, .5f * normalImpulse * closingSpeed);
        }

        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }

    /// <summary>
    /// Shared physical prop model. Unity resolves contact, friction and angular motion; this component owns
    /// material damage and load-bearing state, forwarding the existing 0..3 damage contract to PropMaterial/Sim.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhysicalProp : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public PropPhysicsProfile Profile { get; private set; }
        public bool Broken => _material != null && _material.Damage >= 3;
        public bool AllowsSimulation { get; private set; } = true;
        public bool CanSupport => !Broken && (_furniture != null || (Body != null && Body.mass >= 8f));
        public float AccumulatedDamageEnergy => _damageEnergy;
        public event Action<PhysicalProp> BrokenEvent;
        public event Action<PhysicalProp, float> Impacted;

        PropMaterial _material;
        FurnitureView _furniture;
        readonly List<Collider> _supports = new List<Collider>();
        readonly List<Collider> _collapsedColliders = new List<Collider>();
        float _damageEnergy, _readyAt, _lastImpact = -10f;
        bool _initialized, _collapsed, _beforeCollapseKinematic;
        int _reportedDamage;
        static readonly Dictionary<Mat, PhysicsMaterial> Materials = new Dictionary<Mat, PhysicsMaterial>();

        public static PhysicalProp Ensure(GameObject target)
        {
            if (target == null || target.GetComponentInParent<Characters.ActorRig>() != null) return null;
            var material = target.GetComponent<PropMaterial>();
            if (material == null) return null;
            var prop = target.GetComponent<PhysicalProp>() ?? target.AddComponent<PhysicalProp>();
            prop.Initialize(null, null);
            return prop;
        }

        public static PhysicalProp EnsureFurniture(GameObject target, Furniture furniture, FurnitureDef definition)
        {
            if (target == null || definition == null) return null;
            var prop = target.GetComponent<PhysicalProp>() ?? target.AddComponent<PhysicalProp>();
            prop.Initialize(furniture, definition);
            return prop;
        }

        /// <summary>Optional once-per-world upgrade path for externally spawned or legacy props.</summary>
        public static void InstallSceneProps()
        {
            foreach (var material in FindObjectsByType<PropMaterial>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Ensure(material.gameObject);
        }

        void Initialize(Furniture furniture, FurnitureDef definition)
        {
            if (_initialized) return;
            _material = GetComponent<PropMaterial>();
            if (_material == null) return;
            _initialized = true;
            _furniture = GetComponent<FurnitureView>();
            if (furniture != null) _material.Damage = Mathf.Max(_material.Damage, furniture.Damage);
            definition = definition ?? (_furniture == null ? null : FurnitureCatalog.Get(_furniture.Type));
            Profile = PropPhysicsProfile.For(_material.Mat);
            Body = GetComponent<Rigidbody>();
            bool movable = definition != null && definition.Movable && definition.Mass > 0f && definition.Mass <= 300f
                && definition.Type != "Rug" && definition.Type != "DoorLogger" && definition.Type != "Chandelier";
            if (movable)
            {
                if (furniture != null) InstallFurnitureShape(furniture);
                if (GetComponentsInChildren<Collider>().Length > 0)
                {
                    Body = Body ?? gameObject.AddComponent<Rigidbody>();
                    Body.mass = Mathf.Max(.05f, definition.Mass);
                    // The factory's original loose furniture is already registered with room sleeping.
                    // Newly movable pieces use ordinary PhysX sleep and never become static-batched.
                    if (GetComponent<FurnitureSync>() == null && _furniture != null)
                        gameObject.AddComponent<FurnitureSync>().FurnitureId = _furniture.Id;
                }
            }
            if (Body != null)
            {
                // A non-convex mesh cannot participate in a dynamic compound. Bounds make a conservative,
                // explicitly approximate fallback; authored box/capsule/convex collision is preserved.
                foreach (var collider in GetComponentsInChildren<Collider>())
                {
                    var mesh = collider as MeshCollider;
                    if (mesh == null || mesh.convex || mesh.sharedMesh == null || mesh.isTrigger) continue;
                    if (mesh.attachedRigidbody != Body) continue;
                    var replacement = mesh.gameObject.AddComponent<BoxCollider>();
                    replacement.center = mesh.sharedMesh.bounds.center;
                    replacement.size = mesh.sharedMesh.bounds.size;
                    mesh.enabled = false;
                }
                Body.solverIterations = Mathf.Max(Body.solverIterations, 10);
                Body.solverVelocityIterations = Mathf.Max(Body.solverVelocityIterations, 4);
                Body.maxDepenetrationVelocity = 1.5f;
                Body.maxAngularVelocity = 14f;
                Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                Body.interpolation = RigidbodyInterpolation.Interpolate;
                Body.linearDamping = .12f;
                Body.angularDamping = .35f;
            }
            if (!Materials.TryGetValue(_material.Mat, out var friction) || friction == null)
            {
                friction = new PhysicsMaterial("BL23 physical " + _material.Mat)
                {
                    staticFriction = Profile.StaticFriction, dynamicFriction = Profile.DynamicFriction,
                    bounciness = Profile.Restitution, frictionCombine = PhysicsMaterialCombine.Average,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
                Materials[_material.Mat] = friction;
            }
            foreach (var collider in GetComponentsInChildren<Collider>())
                if (!collider.isTrigger && (Body == null || collider.attachedRigidbody == Body)) collider.sharedMaterial = friction;
            float strength = StrengthScale();
            _damageEnergy = _material.Damage == 0 ? 0f : _material.Damage == 1 ? Profile.YieldEnergy * strength
                : _material.Damage == 2 ? Profile.FractureEnergy * strength * .5f : Profile.FractureEnergy * strength;
            _readyAt = Time.time + 1f;
            _reportedDamage = _material.Damage;
            if (Broken) Collapse();
            PhysicalPropPause.Register(this);
        }

        void Start() { if (!_initialized) Initialize(null, null); }
        void OnDestroy()
        {
            PhysicalPropPause.Unregister(this);
            if (Body != null) PhysicsGrab.LastTouchedByPlayer.Remove(Body);
        }

        public void NotifyDamageChanged()
        {
            if (!_initialized || _material == null) return;
            float minimum = _material.Damage >= 3 ? Profile.FractureEnergy : _material.Damage == 2
                ? Profile.FractureEnergy * .5f : _material.Damage == 1 ? Profile.YieldEnergy : 0f;
            _damageEnergy = _material.Damage < _reportedDamage ? minimum * StrengthScale()
                : Mathf.Max(_damageEnergy, minimum * StrengthScale());
            _reportedDamage = _material.Damage;
            if (_collapsed && !Broken)
            {
                foreach (var collider in _collapsedColliders) if (collider != null) collider.enabled = true;
                _collapsedColliders.Clear(); _collapsed = false; AllowsSimulation = true;
                if (Body != null)
                {
                    Body.isKinematic = _beforeCollapseKinematic || PhysicalCharacter.WorldPaused;
                    PhysicalPropPause.SetRepairedOwnership(this, _beforeCollapseKinematic);
                    if (!Body.isKinematic) Body.WakeUp();
                }
            }
            if (Broken) Collapse();
        }

        float StrengthScale()
        {
            float mass = Body != null ? Body.mass : (_furniture != null ? FurnitureCatalog.Get(_furniture.Type)?.Mass ?? 10f : 1f);
            return Mathf.Max(.15f, Mathf.Sqrt(Mathf.Max(.05f, mass))) * Mathf.Max(.1f, _material.Toughness) * (_material.Fragile ? .65f : 1f);
        }

        /// <summary>Called by PropMaterial exactly once for each collision event.</summary>
        public void HandleCollision(Collision collision)
        {
            if (!_initialized || Broken || Time.time < _readyAt || Time.time - _lastImpact < .08f) return;
            if (Time.time - MansionView.LastBuildTime < 4f || collision.contactCount == 0) return;
            var contact = collision.GetContact(0);
            float speed = 0f;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var c = collision.GetContact(i);
                float closing = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, c.normal));
                if (closing > speed) { speed = closing; contact = c; }
            }
            float impulse = Mathf.Abs(Vector3.Dot(collision.impulse, contact.normal));
            float energy = PropPhysicsProfile.ImpactEnergy(impulse, speed);
            if (energy <= 0f) return;
            _lastImpact = Time.time;
            AddDamageEnergy(contact.point, contact.normal, impulse, energy, 0f);
        }

        /// <summary>Weapon/environment hit. Impulse is N*s; energy is J; sharpness 0..1 favors cuts in soft materials.</summary>
        public void ApplyImpact(Vector3 point, Vector3 impulse, float energyJoules, float sharpness = 0f)
        {
            if (!_initialized || !PropPhysicsProfile.Finite(point) || !PropPhysicsProfile.Finite(impulse)
                || !PropPhysicsProfile.Finite(energyJoules) || !PropPhysicsProfile.Finite(sharpness)) return;
            // Furniture made kinematic by the room sleep manager must wake on a deliberate interaction.
            // Inventory items remain under their holder's kinematic ownership.
            if (Body != null && _furniture != null && !Broken && !PhysicalCharacter.WorldPaused) Body.isKinematic = false;
            if (Body != null && !Body.isKinematic && !PhysicalCharacter.WorldPaused)
                Body.AddForceAtPosition(Vector3.ClampMagnitude(impulse, Mathf.Min(3000f, Body.mass * 35f)), point, ForceMode.Impulse);
            AddDamageEnergy(point, impulse.sqrMagnitude > .001f ? -impulse.normalized : Vector3.up,
                impulse.magnitude, Mathf.Clamp(energyJoules, 0f, 100000f), Mathf.Clamp01(sharpness));
        }

        void AddDamageEnergy(Vector3 point, Vector3 normal, float impulse, float energy, float sharpness)
        {
            if (Broken || energy <= 0f) return;
            float strength = StrengthScale();
            bool cuttable = _material.Mat == Mat.Cloth || _material.Mat == Mat.Leather || _material.Mat == Mat.Paper
                || _material.Mat == Mat.Plant || _material.Mat == Mat.Flesh;
            energy *= cuttable ? 1f + sharpness * 4f : 1f;
            // Below elastic yield energy an object recovers; tiny contacts cannot accumulate infinite damage.
            if (energy < Profile.YieldEnergy * strength * .25f) return;
            _damageEnergy = Mathf.Min(_damageEnergy + energy, Profile.FractureEnergy * strength);
            int damage = _damageEnergy >= Profile.FractureEnergy * strength ? 3
                : _damageEnergy >= Profile.FractureEnergy * strength * .5f ? 2
                : _damageEnergy >= Profile.YieldEnergy * strength ? 1 : 0;
            if (damage > _material.Damage) _material.ApplyDamage(point, normal, impulse, damage - _material.Damage);
            Impacted?.Invoke(this, energy);
        }

        /// <summary>A finite force, never a position/velocity teleport. Heavy objects naturally resist the same push.</summary>
        public void ApplyPush(Vector3 point, Vector3 desiredVelocity, float forceLimit = 450f)
        {
            if (Body == null || Body.isKinematic || !PropPhysicsProfile.Finite(point)
                || !PropPhysicsProfile.Finite(desiredVelocity) || !PropPhysicsProfile.Finite(forceLimit)) return;
            var error = Vector3.ProjectOnPlane(desiredVelocity - Body.GetPointVelocity(point), Vector3.up);
            var force = Vector3.ClampMagnitude(error * Body.mass * 7f, Mathf.Clamp(forceLimit, 0f, 2000f));
            Body.AddForceAtPosition(force, point, ForceMode.Force);
        }

        void Collapse()
        {
            if (_collapsed) return;
            _collapsed = true;
            _beforeCollapseKinematic = PhysicalPropPause.OriginalKinematic(this);
            // Load-bearing legs disappear physically; the existing tabletop/seat falls under gravity.
            // A single-mesh visual keeps its existing appearance; authored fragments can subscribe to BrokenEvent.
            foreach (var support in _supports) DisableForCollapse(support);
            if (Body != null && _supports.Count > 0)
            {
                Body.isKinematic = false;
                Body.WakeUp();
            }
            if (_furniture != null && (_material.Mat == Mat.Glass || _material.Mat == Mat.Ceramic))
            {
                // PropMaterial/FurnitureView already created the shard visuals. Remove the invisible intact barrier.
                foreach (var collider in GetComponentsInChildren<Collider>()) DisableForCollapse(collider);
                if (Body != null) Body.isKinematic = true;
            }
            if (Body != null)
            {
                AllowsSimulation = false;
                foreach (var collider in GetComponentsInChildren<Collider>())
                    if (collider.enabled && !collider.isTrigger && collider.attachedRigidbody == Body) { AllowsSimulation = true; break; }
                if (!AllowsSimulation) Body.isKinematic = true;
            }
            BrokenEvent?.Invoke(this);
        }

        void DisableForCollapse(Collider collider)
        {
            if (collider == null || !collider.enabled) return;
            _collapsedColliders.Add(collider); collider.enabled = false;
        }

        void InstallFurnitureShape(Furniture furniture)
        {
            bool chair = furniture.Type == "Chair";
            bool table = furniture.Type == "CoffeeTable" || furniture.Type == "RoundTable" || furniture.Type == "Desk";
            if (!chair && !table) return;
            // Replace only the factory's single whole-object box. Preserve authored compound/mesh collision.
            var old = GetComponentsInChildren<Collider>();
            if (old.Length != 1 || !(old[0] is BoxCollider) || old[0].gameObject != gameObject) return;
            old[0].enabled = false;
            float width = Mathf.Max(.1f, furniture.W), depth = Mathf.Max(.1f, furniture.D), height = Mathf.Max(.2f, furniture.H);
            float top = chair ? height * .48f : height;
            float thick = Mathf.Clamp(top * .12f, .045f, .09f);
            AddBox(new Vector3(0, top - thick * .5f, 0), new Vector3(width, thick, depth), false);
            float leg = Mathf.Clamp(Mathf.Min(width, depth) * .12f, .04f, .085f);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    AddBox(new Vector3(x * (width * .5f - leg), (top - thick) * .5f, z * (depth * .5f - leg)),
                        new Vector3(leg, top - thick, leg), true);
            if (chair) AddBox(new Vector3(0, (height + top) * .5f, -depth * .5f + thick * .5f),
                new Vector3(width, height - top, thick), false);
        }

        void AddBox(Vector3 center, Vector3 size, bool support)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = center; box.size = size;
            if (support) _supports.Add(box);
        }
    }
}
