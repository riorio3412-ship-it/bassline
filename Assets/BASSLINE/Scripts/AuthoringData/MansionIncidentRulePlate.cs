using System;
using UnityEngine;
using TMPro;
using BASSLINE.Core;
namespace BASSLINE.AuthoringData
{
    [RequireComponent(typeof(FixtureTarget))]
    public sealed class MansionIncidentRulePlate:MonoBehaviour
    {
        public IncidentExecutionDefinition Definition=new IncidentExecutionDefinition();
        public TextMeshPro RuleLabel;
        public Vector3 ReadPosition=>RuleLabel?RuleLabel.transform.position:transform.position;
        public Vector3 ReadNormal=>RuleLabel?-RuleLabel.transform.forward:-transform.forward;
        public int ReadLayer=>RuleLabel?RuleLabel.gameObject.layer:gameObject.layer;
        // This never writes label text or grants knowledge. Hidden author data is not a document.
        public bool HasDisplayedContent()
            =>isActiveAndEnabled&&Definition!=null&&!string.IsNullOrWhiteSpace(Definition.PublicRule)&&!string.IsNullOrWhiteSpace(Definition.Id)&&!string.IsNullOrWhiteSpace(Definition.Revision)
              &&RuleLabel&&RuleLabel.isActiveAndEnabled&&RuleLabel.font&&!RuleLabel.richText&&RuleLabel.text==IncidentRuleReading.DisplayText(Definition);
        public bool TryGetReadPoints(out Vector3[] points)
        {
            points=Array.Empty<Vector3>();
            return HasDisplayedContent()&&ReadableGlyphs.TryGetPoints(RuleLabel,IncidentRuleReading.DisplayText(Definition),out points);
        }
    }
}
