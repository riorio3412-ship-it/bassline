using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>Runs ActorAnimator.LateIK after every animator has written its pose (order 55): IK between two bodies.</summary>
    [DefaultExecutionOrder(55)]
    [DisallowMultipleComponent]
    public sealed class ActorLateIK : MonoBehaviour, IActorStep
    {
        public ActorAnimator Anim;
        public int StepOrder => 55;
        void LateUpdate() { if (Anim != null && Anim.isActiveAndEnabled) Anim.LateIK(Mathf.Min(Time.deltaTime, 0.1f)); }
        public void Step(float dt) { if (!Application.isPlaying && Anim != null) Anim.LateIK(dt); }
    }
}
