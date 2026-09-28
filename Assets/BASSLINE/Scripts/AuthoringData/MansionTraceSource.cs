using UnityEngine;
namespace BASSLINE.AuthoringData
{
    [RequireComponent(typeof(FixtureObjectBody))]
    public sealed class MansionTraceSource:MonoBehaviour
    {
        public MansionTracePatternVisual Face;
        public bool TransfersPigment=true;
    }
}
