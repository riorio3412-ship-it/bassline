using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
namespace BASSLINE.UI
{
    public sealed class RecordRow{public string Id,Heading,Text;}
    public sealed class RecordRowSelection:MonoBehaviour,ISelectHandler
    {
        public ScrollRect Scroll;
        public void OnSelect(BaseEventData data)
        {
            Canvas.ForceUpdateCanvases();var row=(RectTransform)transform;
            float height=Scroll.content.rect.height-Scroll.viewport.rect.height;if(height<=0)return;
            float top=-row.anchoredPosition.y;
            float current=(1-Scroll.verticalNormalizedPosition)*height;
            if(top<current)Scroll.verticalNormalizedPosition=1-Mathf.Clamp01(top/height);
            else if(top+row.rect.height>current+Scroll.viewport.rect.height)Scroll.verticalNormalizedPosition=1-Mathf.Clamp01((top+row.rect.height-Scroll.viewport.rect.height)/height);
        }
    }
    public sealed partial class ProductionScreenView
    {
        readonly List<Button> recordRows=new List<Button>();string rowFingerprint="";
        public Button[] RecordRowButtons=>recordRows.ToArray();
        public void SetRecordRows(RecordRow[] rows,string selected,Action<string> choose)
        {
            string fingerprint=string.Join("|",rows.Select(r=>r.Id))+":"+fontScale;
            Body.gameObject.SetActive(rows.Length==0);
            if(rowFingerprint!=fingerprint){
                rowFingerprint=fingerprint;foreach(var old in recordRows){old.gameObject.SetActive(false);Destroy(old.gameObject);}recordRows.Clear();
                BodyScroll.content.GetComponent<VerticalLayoutGroup>().spacing=10;
                foreach(var row in rows){
                    var go=new GameObject("Known story "+row.Id,typeof(RectTransform),typeof(Image),typeof(Button),typeof(LayoutElement));go.transform.SetParent(BodyScroll.content,false);
                    var button=go.GetComponent<Button>();var image=go.GetComponent<Image>();button.targetGraphic=image;image.color=Color.white;
                    var tgo=new GameObject("Story label",typeof(RectTransform),typeof(TextMeshProUGUI));tgo.transform.SetParent(go.transform,false);
                    var label=tgo.GetComponent<TextMeshProUGUI>();label.font=Title.font;label.fontSize=22*Mathf.Min(fontScale,1.5f);label.color=Paper;label.raycastTarget=false;label.textWrappingMode=TextWrappingModes.Normal;
                    label.text="<color=#89CDCF><size=80%>"+row.Heading+"</size></color>\n"+row.Text;
                    var rect=label.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(16,12);rect.offsetMax=new Vector2(-16,-12);
                    float width=Mathf.Max(280,BodyScroll.viewport.rect.width-32);go.GetComponent<LayoutElement>().preferredHeight=Mathf.Max(100,label.GetPreferredValues(label.text,width,0).y+28);
                    go.AddComponent<RecordRowSelection>().Scroll=BodyScroll;recordRows.Add(button);
                }
            }
            for(int i=0;i<recordRows.Count;i++){
                int index=i;var button=recordRows[i];button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>choose(rows[index].Id));
                // Choice markers can change without changing row identity. Preserve focus and scroll instead of rebuilding buttons.
                var label=button.GetComponentInChildren<TMP_Text>();string text="<color=#89CDCF><size=80%>"+rows[i].Heading+"</size></color>\n"+rows[i].Text;
                if(label.text!=text){label.text=text;float width=Mathf.Max(280,BodyScroll.viewport.rect.width-32);button.GetComponent<LayoutElement>().preferredHeight=Mathf.Max(100,label.GetPreferredValues(text,width,0).y+28);}
                var colors=button.colors;colors.normalColor=rows[i].Id==selected?new Color(.34f,.13f,.22f):new Color(.085f,.12f,.16f);colors.selectedColor=new Color(.26f,.39f,.41f);colors.highlightedColor=new Color(.21f,.29f,.32f);button.colors=colors;
                var nav=button.navigation;nav.mode=Navigation.Mode.Explicit;nav.selectOnUp=recordRows[Math.Max(0,i-1)];nav.selectOnDown=recordRows[Math.Min(recordRows.Count-1,i+1)];nav.selectOnRight=Actions.FirstOrDefault(a=>a.gameObject.activeSelf);button.navigation=nav;
            }
        }
    }
}
