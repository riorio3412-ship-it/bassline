using System;
using UnityEngine;
using BASSLINE.Core;
namespace BASSLINE.AuthoringData
{
    // Per-character authored purposes. This never grants knowledge or fixes a victim.
    [RequireComponent(typeof(FixtureActorBody))]
    public sealed class MansionResidentPurpose:MonoBehaviour
    {
        public ResidentPurposeDefinition[] Goals=Array.Empty<ResidentPurposeDefinition>();
        public string[] PersonalTaboos=Array.Empty<string>();
        public string RulePlateId="",ObjectId="",ToolNode="",ContactNode="";
    }
}
