using UnityEngine;

namespace BL23.Game.Audio
{
    /// <summary>Hidden host for <see cref="Sfx"/>: drives loop fades / follow targets on unscaled time.</summary>
    [DisallowMultipleComponent]
    internal sealed class SfxRunner : MonoBehaviour
    {
        void Update() { Sfx.UpdateLoops(Mathf.Min(Time.unscaledDeltaTime, 0.25f)); }
    }
}
