using UnityEngine;
namespace BASSLINE.AuthoringData
{
    [RequireComponent(typeof(FixtureTarget))]
    public sealed class MansionIncidentDeviceSignal:MonoBehaviour
    {
        public TextMesh WarningLabel;
        public string ActivationId{get;private set;}="";
        public string TargetId{get;private set;}="";
        public string DefinitionId{get;private set;}="";
        public string Revision{get;private set;}="";
        public void Show(string text,string activation="",string target="",string definition="",string revision="")
        {if(WarningLabel){WarningLabel.text=text;WarningLabel.gameObject.SetActive(true);ActivationId=activation;TargetId=target;DefinitionId=definition;Revision=revision;}}
        public void Clear(){if(WarningLabel)WarningLabel.gameObject.SetActive(false);ActivationId="";TargetId="";DefinitionId="";Revision="";}
    }
}
