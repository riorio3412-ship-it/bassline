using System;
using UnityEngine;
namespace BASSLINE.AuthoringData
{
    // Sources and access anchors must be placed during authoring, before the simulation starts.
    // Public access does not grant knowledge of any hidden observation.
    [RequireComponent(typeof(MansionIncidentRulePlate))]
    public sealed class MansionIncidentSite:MonoBehaviour
    {
        public string DeviceId="",PublicAccessNode="",RuleReadNode="";
        public MansionActionJournalStation[] Journals=Array.Empty<MansionActionJournalStation>();
    }
}
