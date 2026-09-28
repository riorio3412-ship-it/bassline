using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>
    /// Presentation physics of one actor (owner: charpolish implementer 4, physics). Step-0 STUB: the public API below
    /// is the contract main code (ActorView hooks) and the other implementers call; bodies are filled in by the physics
    /// implementer. Everything here is presentation only (never feeds the Sim kernel).
    /// Planned: additive hit springs, spring-chain LOD scheduling, ragdoll + get-up, carried-body dangle, solid body
    /// colliders by posture, contact-point memory for region lookups.
    /// </summary>
    [DefaultExecutionOrder(60)]
    [DisallowMultipleComponent]
    public class ActorPhysics : MonoBehaviour, IActorStep
    {
        public int StepOrder => 60;

        /// <summary>A directional hit / shove / bump. strength 0..1 (0.15 bump, 0.4 hit, 0.8 heavy hit, 1 knock-down).</summary>
        public void React(BodyRegion region, Vector3 worldDir, float strength) { }

        /// <summary>The kernel moved this actor a long way in one tick because it was pushed/fell (stairs, rail):
        /// present the fall from fromWorld to toWorld (ragdoll/ballistic), then settle.</summary>
        public void BeginFall(Vector3 fromWorld, Vector3 toWorld) { }

        /// <summary>While true the physics layer owns the root position (ragdoll in flight); ActorView must not move the root.</summary>
        public bool RootLocked => false;

        /// <summary>The solid proxy collider the view adds ("Body" capsule): contacts on it are remembered for region lookups.</summary>
        public void RegisterSolidProxy(Collider c) { }

        /// <summary>Detail tier: 0 = A (near/on screen/focus), 1 = B (on screen, far), 2 = C (hidden/culled).</summary>
        public void SetLod(int tier) { }

        public void Step(float dt) { }

        // ---- charpolish step 0b contract (owner: implementer 4). ActorRig.BeginRagdoll / EndRagdoll / IsRagdoll / PinLimb /
        // SettleOn forward here. Stubs: no ragdoll yet (IsRagdoll stays false).
        public void BeginRagdoll(Vector3 impulse, float blendIn) { }
        public void EndRagdoll(float getUpTime) { }
        public bool IsRagdoll => false;
        /// <summary>True once an active ragdoll has come to rest (hips slower than 5 cm/s for 0.5 s, or 3 s passed).</summary>
        public bool RagdollSettled => false;
        public void PinLimb(HumanLimb limb, Transform target) { }
        public void SettleOn(Collider surface) { }

        /// <summary>Layer of ragdoll bodies (murder-foundation PhysicsLayers.Ragdoll = 10); the Game/Physics adapter may set it at boot.</summary>
        public static int RagdollLayer = 10;
        /// <summary>What ragdoll bodies collide with (default: layer 0, the static world). The adapter may widen it (never to
        /// props that carry kernel evidence).</summary>
        public static int RagdollCollideMask = 1;
        /// <summary>Simultaneously simulating ragdolls (LOD A only); further requests settle at once (snap to the rest pose).</summary>
        public static int MaxActiveRagdolls = 3;
        /// <summary>True when an external driver (the murder-foundation Game/Physics adapter) turns kernel deaths, collapses,
        /// falls and drags into ragdoll calls; the ActorView fallback hooks then stay out of the way.</summary>
        public static bool ExternalFallDriver;

        /// <summary>Share of body mass per bone for ragdoll bodies and carry/drag forces (Dempster segment table; sums to 1 over
        /// the 11 simulated parts: forearm includes the hand, lower leg includes the foot, head includes the neck).</summary>
        public static float MassFraction(HBone b)
        {
            switch (b)
            {
                case HBone.Hips: return 0.142f;
                case HBone.Spine: return 0.139f;
                case HBone.Chest: return 0.216f;
                case HBone.Head: return 0.081f;
                case HBone.UpperArmL: case HBone.UpperArmR: return 0.028f;
                case HBone.LowerArmL: case HBone.LowerArmR: return 0.022f;
                case HBone.UpperLegL: case HBone.UpperLegR: return 0.100f;
                case HBone.LowerLegL: case HBone.LowerLegR: return 0.061f;
            }
            return 0f;
        }
    }
}
