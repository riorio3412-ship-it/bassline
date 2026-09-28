using UnityEngine;
namespace BASSLINE.AuthoringData
{
    // Public furniture, not a source of remote knowledge. Reading requires physical access.
    public sealed class CommonReturnStation:MonoBehaviour
    {
        public const string TrayId="LIFE_RETURN_TRAY",DrawerId="LIFE_RETURN_DRAWER",BookId="LIFE_RETURN_BOOK";
        public const string BagFrontId="LIFE_PRIVATE_BAG_FRONT",PrivateNoteId="LIFE_PRIVATE_RETURN_NOTE";
        public const string PrivateNoteText="펜: 가방 앞.\n작업 끝나면 내가 챙김.\n— 태겸";
        public Transform TrayPoint,DrawerPoint,DrawerVisual,BagFrontPoint,PrivateNote;
        public Vector3 ClosedDrawerPosition,OpenDrawerPosition;
        public string TrayNode,DrawerNode;
    }
}
