using UnityEngine;
namespace BASSLINE.AuthoringData
{
    [RequireComponent(typeof(FixtureObjectBody))]
    public sealed class MansionWeapon:MonoBehaviour
    {
        public string Category="Blade";
        public Vector3 InitialPosition;
        public Vector3 InitialEuler;
    }
}
