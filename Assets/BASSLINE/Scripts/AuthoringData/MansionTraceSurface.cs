using UnityEngine;
namespace BASSLINE.AuthoringData
{
    [RequireComponent(typeof(FixtureTarget))]
    public sealed class MansionTraceSurface:MonoBehaviour
    {
        public Collider ContactSurface;
        public Material TraceMaterial;
    }
}
