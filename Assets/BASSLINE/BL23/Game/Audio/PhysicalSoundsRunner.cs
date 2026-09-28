using UnityEngine;

namespace BL23.Game.Audio
{
    /// <summary>Hidden host for <see cref="PhysicalSounds"/>: ticks the struggle sound beds on game time (they pause with the game).</summary>
    [DisallowMultipleComponent]
    internal sealed class PhysicalSoundsRunner : MonoBehaviour
    {
        void Update() { PhysicalSounds.Tick(Mathf.Min(Time.deltaTime, 0.1f)); }
    }
}
