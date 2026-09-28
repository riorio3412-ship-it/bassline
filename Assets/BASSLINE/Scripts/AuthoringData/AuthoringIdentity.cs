using UnityEngine;

namespace BASSLINE.AuthoringData
{
    [DisallowMultipleComponent]
    public sealed class AuthoringIdentity : MonoBehaviour
    {
        public string StableId;
        public string Kind;
        public string AssetStatus="FunctionalProxy";
        public string MapVersion="M01";
        public string DefinitionPath;
        public bool SocketsResolved;
    }
}
