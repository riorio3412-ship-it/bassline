using UnityEngine;
using UnityEngine.UI;
namespace BASSLINE.UI
{
    public sealed partial class ProductionScreenView
    {
        void EnsureScrollbars()
        {
            foreach(var scroll in new[]{BodyScroll,ContextScroll}){
                if(!scroll||scroll.verticalScrollbar)continue;
                var track=Place(scroll.transform,"Reading scroll",new Vector2(1,0),Vector2.one);track.pivot=new Vector2(1,.5f);track.sizeDelta=new Vector2(12,0);
                var background=track.gameObject.AddComponent<Image>();background.color=new Color(.12f,.17f,.2f,.8f);
                var handle=Place(track,"Handle",Vector2.zero,Vector2.one);var fill=handle.gameObject.AddComponent<Image>();fill.color=Teal;
                var bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=fill;bar.direction=Scrollbar.Direction.BottomToTop;
                var nav=bar.navigation;nav.mode=Navigation.Mode.None;bar.navigation=nav;
                scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;scroll.scrollSensitivity=32;
                scroll.viewport.offsetMax=new Vector2(-20,scroll.viewport.offsetMax.y);
            }
        }
        void ScrollWithKeyboard()
        {
            int direction=Input.GetKeyDown(KeyCode.PageDown)?-1:Input.GetKeyDown(KeyCode.PageUp)?1:0;if(direction==0)return;
            var scroll=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)?ContextScroll:BodyScroll;
            if(!scroll||!scroll.content)return;
            float height=scroll.content.rect.height-scroll.viewport.rect.height;
            if(height>0)scroll.verticalNormalizedPosition=Mathf.Clamp01(scroll.verticalNormalizedPosition+direction*scroll.viewport.rect.height*.8f/height);
        }
    }
}
