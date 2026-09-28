using System.Linq;
using UnityEngine;
using TMPro;
namespace BASSLINE.UI
{
    public sealed partial class ProductionScreenView:MonoBehaviour,UnityEngine.EventSystems.IPointerClickHandler
    {
        public string ScreenId;public TMP_Text Title,Body,Context,Status;public UnityEngine.UI.ScrollRect BodyScroll,ContextScroll;
        public UnityEngine.UI.Button[] Actions;public TMP_Text[] ActionLabels;public UnityEngine.UI.Image Standing;
        float fontScale=1,textSpeed=45,revealed;string lastBody="";bool reducedMotion,titleCard;
        public System.Action DialogueAdvance;
        public bool DialogueHasChoices;
        public bool DialogueRecordPreview;
        public bool DialogueUsesWorldPlayback;
        UnityEngine.UI.Image dialogueBackdrop;
        public bool DialogueRevealed=>Body&&Body.maxVisibleCharacters==int.MaxValue;
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e){if(ScreenId=="UI_03"&&e.button==UnityEngine.EventSystems.PointerEventData.InputButton.Left&&BodyScroll&&RectTransformUtility.RectangleContainsScreenPoint((RectTransform)BodyScroll.transform,e.position,e.pressEventCamera))DialogueAdvance?.Invoke();}
        public void Fonts(TMP_FontAsset font,float scale)
        {
            fontScale=scale;
            foreach(var text in GetComponentsInChildren<TMP_Text>(true)){text.font=font;text.fontSize=(text==Title?36:ScreenId=="UI_03"&&text==Body?30:text==Status||ActionLabels.Contains(text)?22:26)*scale;text.enableAutoSizing=false;text.raycastTarget=false;text.lineSpacing=12;text.textWrappingMode=TextWrappingModes.Normal;}
        }
        public void ConfigurePresentation(PlayerControls controls){textSpeed=controls.TextSpeed;reducedMotion=controls.ReduceMotion;}
        public void ConfigureTitleCard(bool active)
        {
            titleCard=active;var panel=(RectTransform)Title.transform.parent;
            panel.anchorMin=active?new Vector2(.06f,.10f):new Vector2(.025f,.045f);panel.anchorMax=active?new Vector2(.59f,.91f):new Vector2(.975f,.955f);
            Title.fontSize=(active?56:36)*fontScale;
            var body=(RectTransform)BodyScroll.transform;body.anchorMin=active?new Vector2(.06f,.40f):new Vector2(.025f,.18f);body.anchorMax=active?new Vector2(.94f,.79f):new Vector2(.63f,.87f);
            var context=(RectTransform)ContextScroll.transform;context.anchorMin=active?new Vector2(.06f,.23f):new Vector2(.66f,.18f);context.anchorMax=active?new Vector2(.94f,.40f):new Vector2(.975f,.87f);
        }
        public void Show(string title,string body,string context,string status)
        {
            if(ScreenId=="UI_03"&&!dialogueBackdrop){
                var go=new GameObject("Dialogue backdrop",typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(transform,false);go.transform.SetAsFirstSibling();
                dialogueBackdrop=go.GetComponent<UnityEngine.UI.Image>();dialogueBackdrop.raycastTarget=false;dialogueBackdrop.color=new Color(.035f,.065f,.09f,.48f);
                var r=dialogueBackdrop.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
            }
            Title.text=title;Body.text=body;Context.text=context;Status.text=status;
            if(body!=lastBody){BodyScroll.verticalNormalizedPosition=1;lastBody=body;revealed=0;Body.maxVisibleCharacters=ScreenId=="UI_03"&&!DialogueRecordPreview&&!DialogueUsesWorldPlayback&&textSpeed>0&&!reducedMotion?0:int.MaxValue;}
            EnsureIdentity();RefreshActionLayout();
        }
        public void RevealDialogue(){if(Body)Body.maxVisibleCharacters=int.MaxValue;}
        void Update()
        {
            ScrollWithKeyboard();
            if(ScreenId!="UI_03"||!Body||Body.maxVisibleCharacters==int.MaxValue)return;
            if(textSpeed<=0||reducedMotion){RevealDialogue();return;}
            revealed+=Time.unscaledDeltaTime*textSpeed;Body.maxVisibleCharacters=Mathf.FloorToInt(revealed);
            if(revealed>=Body.text.Length)RevealDialogue();
        }
        public void RefreshActionLayout()
        {
            var active=Actions.Where(x=>x&&x.gameObject.activeSelf).ToArray();bool dialogue=ScreenId=="UI_03";
            int columns=dialogue?(DialogueHasChoices?2:3):titleCard?2:Mathf.Min(4,Mathf.Max(1,active.Length));int rows=Mathf.CeilToInt(active.Length/(float)columns);
            float footer=dialogue?.28f:Mathf.Max(.13f,.08f*rows*fontScale);
            if(BodyScroll&&!dialogue&&!titleCard){var r=(RectTransform)BodyScroll.transform;r.anchorMin=new Vector2(r.anchorMin.x,footer+.09f);}
            if(ContextScroll&&!dialogue&&!titleCard){var r=(RectTransform)ContextScroll.transform;r.anchorMin=new Vector2(r.anchorMin.x,footer+.09f);}
            if(titleCard){var body=(RectTransform)BodyScroll.transform;body.anchorMin=new Vector2(.06f,Mathf.Max(.43f,footer+.24f));var context=(RectTransform)ContextScroll.transform;context.anchorMin=new Vector2(.06f,footer+.09f);context.anchorMax=new Vector2(.94f,Mathf.Max(.43f,footer+.24f));}
            if(Status&&!dialogue){var r=Status.rectTransform;r.anchorMin=new Vector2(.025f,footer+.015f);r.anchorMax=new Vector2(.975f,footer+.085f);}
            for(int i=0;i<active.Length;i++)
            {
                var rect=(RectTransform)active[i].transform;float gap=.012f;
                if(dialogue)
                {
                    float height=.24f*Mathf.Max(1,fontScale);float y=1.04f+(rows-1-i/columns)*(height+.035f);
                    float width=DialogueHasChoices?.23f:.18f,start=DialogueHasChoices?.515f:.435f;
                    rect.anchorMin=new Vector2(start+(i%columns)*width,y);rect.anchorMax=new Vector2(start+(i%columns+1)*width-.01f,y+height);
                }
                else
                {
                    float width=.95f/columns;float height=(footer-.035f)/Mathf.Max(1,rows);float y=.018f+(rows-1-i/columns)*height;
                    rect.anchorMin=new Vector2(.025f+(i%columns)*width,y);rect.anchorMax=new Vector2(.025f+(i%columns+1)*width-gap,y+height-gap);
                }
                rect.offsetMin=rect.offsetMax=Vector2.zero;
                var nav=active[i].navigation;nav.mode=UnityEngine.UI.Navigation.Mode.Explicit;
                nav.selectOnLeft=active[(i+active.Length-1)%active.Length];nav.selectOnRight=active[(i+1)%active.Length];nav.selectOnUp=active[Mathf.Max(0,i-columns)];nav.selectOnDown=active[Mathf.Min(active.Length-1,i+columns)];active[i].navigation=nav;
            }
            if(dialogue&&Body)
            {
                var panel=(RectTransform)BodyScroll.transform.parent;panel.anchorMin=new Vector2(.03f,.035f);panel.anchorMax=new Vector2(.97f,.28f);
                BodyScroll.GetComponent<RectTransform>().anchorMin=new Vector2(.025f,.14f);BodyScroll.GetComponent<RectTransform>().anchorMax=new Vector2(DialogueHasChoices?.58f:.74f,.76f);
                ContextScroll.GetComponent<RectTransform>().anchorMin=new Vector2(DialogueHasChoices?.61f:.77f,.14f);ContextScroll.GetComponent<RectTransform>().anchorMax=new Vector2(.975f,.76f);
                Context.fontSize=(DialogueHasChoices?22:18)*fontScale;
                Title.rectTransform.anchorMin=new Vector2(.025f,.77f);Title.rectTransform.anchorMax=new Vector2(.975f,.98f);
                Status.rectTransform.anchorMin=new Vector2(.025f,.015f);Status.rectTransform.anchorMax=new Vector2(.975f,.13f);
                if(Standing){Standing.rectTransform.anchorMin=new Vector2(.03f,.29f);Standing.rectTransform.anchorMax=new Vector2(.38f,.96f);}
            }
            IdentityLayout();
        }
    }
}
