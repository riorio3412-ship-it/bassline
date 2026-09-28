using UnityEngine;
using TMPro;
namespace BASSLINE.AuthoringData
{
    [RequireComponent(typeof(FixtureObjectBody))]
    public sealed class MansionReadableBook:MonoBehaviour
    {
        public string Revision="",Content="";public TextMeshPro Page;
        public int ReadingTicks=>System.Math.Max(600,(Content??"").Length*12);
        public Vector3 ReadPoint=>Page?Page.transform.position:transform.position;
        public Vector3 ReadNormal=>Page?-Page.transform.forward:transform.up;
        public bool TryReadPoints(out Vector3[] points)
        {
            points=System.Array.Empty<Vector3>();return isActiveAndEnabled&&!string.IsNullOrWhiteSpace(Revision)&&ReadableGlyphs.TryGetPoints(Page,Content,out points);
        }
    }
}
