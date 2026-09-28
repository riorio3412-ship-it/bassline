using BASSLINE.Core;
using UnityEngine;
namespace BASSLINE.UI
{
    public sealed partial class FixtureHud
    {
        void UpdateAmbientCaption()
        {
            if(!AmbientCaption)return;
            var heard=(Source as IPlayerResidentMeetingsPort)?.ReadAmbientSpeech();
            bool show=CurrentScreen==1&&!port.ReadPlayer().Paused&&heard!=null&&heard.HeardText!="";
            AmbientCaption.transform.parent.gameObject.SetActive(show);
            if(!show){PositionInteractionHint(.12f);return;}
            AmbientCaption.font=KoreanFont;AmbientCaption.fontSize=(Theme?Theme.Body:26)*(Controls?.FontScale??1);
            AmbientCaption.richText=false;
            string text=heard.HeardText;if(text.Length>96)text="… "+text.Substring(text.Length-96);
            AmbientCaption.text=ActorLabel(heard.SpeakerId)+"  “"+text+"”";
            var panel=(RectTransform)AmbientCaption.transform.parent;
            var root=(RectTransform)panel.parent;
            float required=AmbientCaption.GetPreferredValues(AmbientCaption.text,Mathf.Max(100,AmbientCaption.rectTransform.rect.width),Mathf.Infinity).y+24;
            float height=Mathf.Max(.09f,required/Mathf.Max(1,root.rect.height));
            panel.anchorMax=new Vector2(panel.anchorMax.x,.025f+height);
            PositionInteractionHint(Mathf.Max(.12f,.035f+height));
        }
        void LateUpdate()
        {
            if(!InteractionBackdrop||!InteractionHint)return;
            InteractionBackdrop.enabled=InteractionHint.isActiveAndEnabled&&!string.IsNullOrWhiteSpace(InteractionHint.text);
            var back=InteractionBackdrop.rectTransform;var text=InteractionHint.rectTransform;
            back.anchorMin=text.anchorMin;back.anchorMax=text.anchorMax;
            back.offsetMin=text.offsetMin-new Vector2(16,8);back.offsetMax=text.offsetMax+new Vector2(16,8);
        }
        void PositionInteractionHint(float bottom){if(!InteractionHint)return;var rect=InteractionHint.rectTransform;rect.anchorMin=new Vector2(rect.anchorMin.x,bottom);rect.anchorMax=new Vector2(rect.anchorMax.x,bottom+.06f);}
    }
}
