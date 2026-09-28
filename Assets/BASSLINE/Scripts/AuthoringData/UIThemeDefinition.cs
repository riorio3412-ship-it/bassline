using UnityEngine;
namespace BASSLINE.AuthoringData
{
    public sealed class UIThemeDefinition:ScriptableObject
    {
        public string ThemeId="TH_BASSLINE_01",DecisionStatus="PRODUCTION_PROPOSAL",FontFamily="Malgun Gothic",FontApproval="REVIEW_REQUIRED";
        public Color OffWhite=new Color32(231,232,225,255),ColdGray=new Color32(182,190,193,255),DarkBlueGray=new Color32(38,50,60,255),Teal=new Color32(85,120,121,255),MutedRed=new Color32(140,85,90,255),Archive=new Color32(117,109,128,255),Annotation=new Color32(215,199,169,255);
        public int SchemaVersion=1;public float Body=26,Title=36,Caption=22,Clock=22,Claim=30,SafeMargin=48,LineHeight=1.45f;
        public float OpenDuration=.12f,CloseDuration=.10f,FocusDuration=.08f;
    }
}
