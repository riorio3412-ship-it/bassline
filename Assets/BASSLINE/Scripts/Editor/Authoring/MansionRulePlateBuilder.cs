using System;
using UnityEngine;
using TMPro;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
namespace BASSLINE.Authoring
{
    public static class MansionRulePlateBuilder
    {
        // Caller must supply a real authored site and readable font. Does not create a case or mark content Reviewed.
        // Rotation follows the existing mansion sign convention: the visible front is local -Z.
        public static MansionIncidentRulePlate Build(Transform parent,string id,string name,IncidentExecutionDefinition definition,Vector3 position,Quaternion rotation,TMP_FontAsset font,Material backing)
        {
            IncidentExecutionDefinition.Validate(definition);
            if(!parent||!font||!backing||string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(name))throw new ArgumentException("A rule plate needs an authored location, name, font and material");
            var root=new GameObject(id);root.transform.SetParent(parent,false);root.transform.position=position;root.transform.rotation=rotation;
            var target=root.AddComponent<FixtureTarget>();target.StableId=id;target.PublicName=name;
            var plate=root.AddComponent<MansionIncidentRulePlate>();plate.Definition=definition.Copy();
            var label=new GameObject("RuleText");label.transform.SetParent(root.transform,false);label.transform.localPosition=new Vector3(0,0,-.021f);
            var text=label.AddComponent<TextMeshPro>();text.font=font;text.richText=false;text.text=IncidentRuleReading.DisplayText(definition);
            text.fontSize=2.6f;text.enableAutoSizing=false;text.alignment=TextAlignmentOptions.TopLeft;text.color=new Color(.95f,.94f,.89f);
            text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Overflow;
            const float width=.9f;float height=Mathf.Max(.24f,text.GetPreferredValues(text.text,width- .06f,Mathf.Infinity).y+.06f);
            text.rectTransform.sizeDelta=new Vector2(width-.06f,height-.06f);text.ForceMeshUpdate();plate.RuleLabel=text;
            var panel=GameObject.CreatePrimitive(PrimitiveType.Cube);panel.name="Backing";panel.transform.SetParent(root.transform,false);panel.transform.localScale=new Vector3(width,height,.03f);panel.GetComponent<Renderer>().sharedMaterial=backing;
            return plate;
        }
    }
}
