namespace BL23.Game.Characters
{
    /// <summary>
    /// charpolish step 0 contract (frozen: change only through the integrator).
    /// A per-actor component that runs after <see cref="ActorAnimator"/> has written the pose (hit springs, spring
    /// chains, ragdoll blend, carried-body dangle...). At runtime each component steps itself from LateUpdate (use
    /// [DefaultExecutionOrder] &gt; 50); editor QA (CharacterQA.Settle) calls <see cref="Step"/> directly, in ascending
    /// <see cref="StepOrder"/>, after ActorAnimator.Tick(dt).
    /// </summary>
    public interface IActorStep
    {
        int StepOrder { get; }
        void Step(float dt);
    }
}
