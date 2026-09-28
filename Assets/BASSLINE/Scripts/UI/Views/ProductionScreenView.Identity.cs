using System.Linq;
using TMPro;
using UnityEngine;

namespace BASSLINE.UI
{
    // Presentation only. Every label and action still comes from the player's read ports.
    public sealed partial class ProductionScreenView
    {
        public static readonly Color Paper = new Color(.92f,.91f,.87f);
        public static readonly Color Muted = new Color(.62f,.70f,.72f);
        public static readonly Color Teal = new Color(.48f,.79f,.79f);
        public static readonly Color Rose = new Color(.65f,.19f,.36f);
        static readonly Color Ink = new Color(.045f,.055f,.075f,.97f);
        TMP_Text eyebrow, footerLabel, contextCaption;
        UnityEngine.UI.RawImage titleArt;
        UnityEngine.UI.Image border, separator, contextShade;
        RectTransform document;
        bool identityReady;

        static RectTransform Place(Transform parent,string name,Vector2 lo,Vector2 hi)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=lo;rect.anchorMax=hi;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }
        UnityEngine.UI.Image Rule(string name,Transform parent,Vector2 lo,Vector2 hi,Color color)
        {
            var rect=Place(parent,name,lo,hi);var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return image;
        }
        TMP_Text Label(string name,Transform parent,Vector2 lo,Vector2 hi,string value,float size,Color color)
        {
            var t=Place(parent,name,lo,hi).gameObject.AddComponent<TextMeshProUGUI>();t.font=Title.font;t.fontSize=size;t.color=color;t.text=value;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.NoWrap;return t;
        }
        void EnsureIdentity()
        {
            if(identityReady||!Title)return;identityReady=true;document=(RectTransform)Title.transform.parent;EnsureScrollbars();
            document.GetComponent<UnityEngine.UI.Image>().color=Ink;
            var dim=Rule("Scene veil",transform,Vector2.zero,Vector2.one,new Color(.015f,.025f,.04f,.62f));dim.transform.SetAsFirstSibling();
            border=Rule("Top hairline",document,new Vector2(.025f,.984f),new Vector2(.975f,.9855f),Teal);
            separator=Rule("Content hairline",document,new Vector2(.025f,.875f),new Vector2(.975f,.876f),new Color(.3f,.39f,.42f,.7f));
            contextShade=Rule("Context paper",document,new Vector2(.64f,.2f),new Vector2(.98f,.855f),new Color(.085f,.10f,.13f,.8f));contextShade.transform.SetAsFirstSibling();
            eyebrow=Label("Chapter eyebrow",document,new Vector2(.028f,.956f),new Vector2(.96f,.982f),"B A S S L I N E   /   "+Section(),14,Teal);
            footerLabel=Label("Footer folio",document,new Vector2(.025f,.006f),new Vector2(.97f,.027f),"기억은 단서가 된다.     ·     ESC  돌아가기",13,Muted);
            contextCaption=Label("Context caption",document,new Vector2(.67f,.819f),new Vector2(.956f,.851f),"잠깐, 알아두기",14,Teal);
            foreach(var t in new[]{Title,Body,Context,Status})t.color=t==Status?Teal:t==Context?Muted:Paper;
            foreach(var b in Actions){
                b.GetComponent<UnityEngine.UI.Image>().color=Color.white;
                var c=b.colors;c.normalColor=new Color(.10f,.13f,.17f);c.highlightedColor=Rose;c.selectedColor=new Color(.39f,.13f,.25f);c.pressedColor=new Color(.28f,.52f,.54f);c.disabledColor=new Color(.1f,.11f,.13f,.5f);c.fadeDuration=.10f;b.colors=c;
                var outline=b.gameObject.AddComponent<UnityEngine.UI.Outline>();outline.effectColor=new Color(.28f,.38f,.41f,.75f);outline.effectDistance=new Vector2(1,-1);
            }
            foreach(var t in ActionLabels)t.color=Paper;
        }
        string Section()
        {
            int.TryParse(ScreenId.Replace("UI_",""),out var n);
            return n<7?"CONVERSATION":n<14?"PERSONAL ARCHIVE":n<23?"INVESTIGATION":n<32?"CLASS TRIAL":n<38?"AFTER THE TRUTH":"SYSTEM";
        }
        static void Anchors(RectTransform r,float x0,float y0,float x1,float y1){r.anchorMin=new Vector2(x0,y0);r.anchorMax=new Vector2(x1,y1);r.offsetMin=r.offsetMax=Vector2.zero;}
        public void EnsureProfilePortrait()
        {
            if(Standing)return;
            Standing=Place(transform,"Known character portrait",new Vector2(.38f,.23f),new Vector2(.64f,.79f)).gameObject.AddComponent<UnityEngine.UI.Image>();Standing.enabled=false;Standing.raycastTarget=false;
        }
        void IdentityLayout()
        {
            EnsureIdentity();if(!identityReady)return;
            bool dialogue=ScreenId=="UI_03", trial=ScreenId=="UI_23"||ScreenId=="UI_29";
            var active=Actions.Where(b=>b&&b.gameObject.activeSelf).ToArray();
            float safeScale=Mathf.Min(fontScale,1.5f);
            foreach(var t in new[]{Body,Context}){t.lineSpacing=10;t.paragraphSpacing=12;}
            Body.fontSize=(dialogue?30:25)*fontScale;Context.fontSize=(dialogue?18:23)*fontScale;Status.fontSize=18*safeScale;
            Title.fontSize=(titleCard?66:dialogue?29:36)*safeScale;
            eyebrow.font=footerLabel.font=contextCaption.font=Title.font;
            contextCaption.gameObject.SetActive(!dialogue&&!titleCard&&!trial);
            contextShade.gameObject.SetActive(!dialogue&&!titleCard&&!trial);
            footerLabel.gameObject.SetActive(!dialogue);separator.gameObject.SetActive(!dialogue&&!titleCard);
            if(ScreenId=="UI_38"&&!titleArt){
                var r=Place(transform,"Manor title artwork",Vector2.zero,Vector2.one);r.SetAsFirstSibling();titleArt=r.gameObject.AddComponent<UnityEngine.UI.RawImage>();
                titleArt.texture=Resources.Load<Texture2D>("BASSLINE/Presentation/TitleManor");titleArt.raycastTarget=false;titleArt.color=Color.white;
            }
            if(titleArt)titleArt.gameObject.SetActive(titleCard);
            if(titleCard){
                Anchors(document,.055f,.065f,.60f,.94f);document.GetComponent<UnityEngine.UI.Image>().color=new Color(.018f,.027f,.04f,.65f);
                Anchors(Title.rectTransform,.045f,.72f,.96f,.85f);Title.characterSpacing=8;
                Anchors(eyebrow.rectTransform,.05f,.866f,.95f,.92f);eyebrow.text="SAME PEOPLE. DIFFERENT TRUTHS.";eyebrow.fontSize=15;
                Anchors((RectTransform)BodyScroll.transform,.05f,.55f,.92f,.70f);
                Anchors((RectTransform)ContextScroll.transform,.05f,.075f,.93f,.15f);Context.fontSize=17;
                Anchors(Status.rectTransform,.05f,.02f,.94f,.07f);footerLabel.text="BASSLINE  /  DEVELOPMENT EDITION  "+Application.version;
                for(int i=0;i<active.Length;i++)Anchors((RectTransform)active[i].transform,.05f,.48f-i*.083f,.75f,.547f-i*.083f);
                foreach(var t in ActionLabels){t.alignment=TextAlignmentOptions.MidlineLeft;t.margin=new Vector4(22,0,8,0);t.fontSize=24*safeScale;}
            }else if(dialogue){
                Title.characterSpacing=1;eyebrow.text="CONVERSATION  /  대화";
                Anchors(document,.04f,.045f,.96f,.355f);document.GetComponent<UnityEngine.UI.Image>().color=new Color(.027f,.038f,.057f,.98f);
                Anchors(Title.rectTransform,.035f,.80f,.71f,.96f);
                Anchors(eyebrow.rectTransform,.73f,.81f,.975f,.96f);eyebrow.fontSize=13;
                Anchors((RectTransform)BodyScroll.transform,.035f,.17f,.78f,.77f);
                Anchors((RectTransform)ContextScroll.transform,.80f,.18f,.976f,.77f);
                Anchors(Status.rectTransform,.035f,.025f,.97f,.145f);
                if(Standing)Anchors(Standing.rectTransform,.63f,.347f,.985f,.97f);
                int columns=DialogueHasChoices?2:3,rows=Mathf.CeilToInt(active.Length/(float)columns);
                float h=.245f*safeScale;
                for(int i=0;i<active.Length;i++){
                    float w=DialogueHasChoices?.276f:.19f,x=DialogueHasChoices?.035f:.035f;
                    float y=1.10f+(rows-1-i/columns)*(h+.055f);
                    Anchors((RectTransform)active[i].transform,x+i%columns*w,y,x+(i%columns+1)*w-.012f,y+h);
                }
                foreach(var t in ActionLabels){t.alignment=TextAlignmentOptions.Center;t.margin=Vector4.zero;t.fontSize=22*safeScale;}
            }else{
                Anchors(document,.035f,.045f,.965f,.955f);document.GetComponent<UnityEngine.UI.Image>().color=Ink;Title.characterSpacing=0;
                Anchors(Title.rectTransform,.029f,.889f,.97f,.946f);eyebrow.text="B A S S L I N E   /   "+Section();eyebrow.fontSize=14;
                Anchors(eyebrow.rectTransform,.028f,.956f,.96f,.982f);
                int columns=active.Length>4?4:Mathf.Max(1,active.Length),rows=Mathf.CeilToInt(active.Length/(float)columns);
                float footer=Mathf.Max(.13f,.095f*rows*safeScale);
                Anchors((RectTransform)BodyScroll.transform,.032f,footer+.135f,.615f,.84f);
                Anchors((RectTransform)ContextScroll.transform,.668f,footer+.135f,.951f,.802f);
                Anchors(contextShade.rectTransform,.64f,footer+.11f,.976f,.858f);
                Anchors(Status.rectTransform,.03f,footer+.035f,.965f,footer+.105f);
                for(int i=0;i<active.Length;i++){
                    float w=.938f/columns,h=(footer-.035f)/rows,y=.045f+(rows-1-i/columns)*h;
                    Anchors((RectTransform)active[i].transform,.03f+i%columns*w,y,.03f+(i%columns+1)*w-.012f,y+h-.014f);
                }
                foreach(var t in ActionLabels){t.alignment=TextAlignmentOptions.Center;t.margin=new Vector4(7,2,7,2);t.fontSize=21*safeScale;}
                if(trial){
                    Anchors(document,.035f,.045f,.965f,.58f);Anchors((RectTransform)BodyScroll.transform,.03f,footer+.15f,.62f,.81f);
                    if(Standing)Anchors(Standing.rectTransform,.66f,.57f,.98f,.98f);
                }
                footerLabel.text="BASSLINE  /  "+Section()+"                                      ESC  돌아가기";
                // Map has a wider main region and a dedicated context column.
                if(ScreenId=="UI_11"){Anchors((RectTransform)ContextScroll.transform,.76f,footer+.135f,.955f,.802f);Anchors(contextShade.rectTransform,.74f,footer+.11f,.976f,.858f);Anchors(contextCaption.rectTransform,.762f,.82f,.96f,.851f);}
                if(ScreenId=="UI_09"&&Standing){Anchors((RectTransform)BodyScroll.transform,.032f,footer+.135f,.35f,.84f);Anchors(Standing.rectTransform,.37f,.23f,.64f,.79f);}
            }
        }
    }
}
