using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>Runs after PhysicalRagdoll has written the bones (order 310): the protective reach while falling.</summary>
    [DefaultExecutionOrder(310)]
    [DisallowMultipleComponent]
    public sealed class ActorPostRagdoll : MonoBehaviour, IActorStep
    {
        public ActorAnimator Anim;
        public int StepOrder => 310;
        void LateUpdate() { if (Anim != null && Anim.isActiveAndEnabled) Anim.RagdollOverlay(Mathf.Min(Time.deltaTime, 0.1f)); }
        public void Step(float dt) { if (!Application.isPlaying && Anim != null) Anim.RagdollOverlay(dt); }
    }
}
