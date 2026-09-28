using System;
using UnityEngine;

namespace BL23.Game.Physicality
{
    /// <summary>Common neutral motor timings. Supply different values per actor later without changing action logic.</summary>
    [Serializable]
    public sealed class PhysicalActionStyle
    {
        [Min(0.15f)] public float ReachSeconds = 0.7f;
        [Min(0.15f)] public float PlaceSeconds = 0.8f;
        [Min(0.2f)] public float HandoverSeconds = 1.1f;
        [Min(0.15f)] public float SwingSeconds = 0.65f;
        [Range(0.5f, 2f)] public float MotionScale = 1f;
        [Range(0.2f, 2f)] public float PainResponse = 1f;
        [Range(0.2f, 2f)] public float StartleResponse = 1f;
        [Range(4f, 30f)] public float ContactResponse = 15f;
        [Range(0.02f, 0.2f)] public float ContactTolerance = 0.14f;
        [Range(0.2f, 0.8f)] public float CarryForward = 0.36f;
        [Range(0.05f, 0.35f)] public float CarrySide = 0.23f;
        [Range(0f, 1f)] public float FootPlacement = 0.8f;
        [Range(0f, 60f)] public float MaxFootSlope = 45f;
    }

    public enum PhysicalActionKind { None, Reach, PickUp, Place, Give, Receive, Brace, Swing }
}
