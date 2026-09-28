using System;
using System.Linq;
using BASSLINE.Core;
using TMPro;
using UnityEngine;
namespace BASSLINE.UI
{
    public sealed partial class FixtureHud
    {
        bool taskNotebook;string selectedTask="";bool taskHistory;
        RectTransform taskHudPanel;TMP_Text taskHudText;float taskHudRefresh;
        void UpdateTaskHud()
        {
            if(!(Source is IPlayerTaskJournalPort tasks))return;
            bool visible=CurrentScreen==1&&!titleMenu&&!port.ReadPlayer().Paused&&(Source as IPlayerItemExchangePort)?.ReadItemExchange().Running!=true;
            if(!visible){if(taskHudPanel)taskHudPanel.gameObject.SetActive(false);taskHudRefresh=0;return;}
            if(Time.unscaledTime<taskHudRefresh)return;taskHudRefresh=Time.unscaledTime+.5f;
            var pinned=tasks.ReadPinnedTask();
            if(pinned==null){if(taskHudPanel)taskHudPanel.gameObject.SetActive(false);return;}
            if(!taskHudPanel){
                var go=new GameObject("Current task",typeof(RectTransform),typeof(UnityEngine.UI.Image));
                var parent=transform.Find("ExplorationHUD");go.transform.SetParent(parent?parent:transform,false);
                taskHudPanel=(RectTransform)go.transform;taskHudPanel.anchorMin=new Vector2(.025f,.975f);taskHudPanel.anchorMax=new Vector2(.38f,.975f);taskHudPanel.pivot=new Vector2(0,1);taskHudPanel.offsetMin=taskHudPanel.offsetMax=Vector2.zero;
                var image=go.GetComponent<UnityEngine.UI.Image>();image.color=new Color(.025f,.035f,.045f,.91f);image.raycastTarget=false;
                var label=new GameObject("Task text",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(go.transform,false);taskHudText=label.GetComponent<TextMeshProUGUI>();
                var rect=taskHudText.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(18,12);rect.offsetMax=new Vector2(-18,-12);
                taskHudText.raycastTarget=false;taskHudText.richText=false;taskHudText.enableAutoSizing=false;taskHudText.textWrappingMode=TextWrappingModes.Normal;taskHudText.alignment=TextAlignmentOptions.TopLeft;taskHudText.color=new Color(.92f,.95f,.93f);
            }
            taskHudPanel.gameObject.SetActive(true);taskHudText.font=KoreanFont;taskHudText.fontSize=22*(Controls?.FontScale??1);taskHudText.lineSpacing=4;
            taskHudText.text=pinned.Title+"\n"+pinned.Instruction+"\n"+Controls.Label("Note")+" · 노트에서 바꾸거나 숨기기";
            var parentRect=(RectTransform)taskHudPanel.parent;float width=Mathf.Max(220,parentRect.rect.width*.355f-36);
            float height=taskHudText.GetPreferredValues(taskHudText.text,width,Mathf.Infinity).y+24;
            taskHudPanel.sizeDelta=new Vector2(0,height);
        }
        void RenderTaskNotebook(ProductionScreenView view,Action<string,Action> button,ref string title,ref string body,ref string context)
        {
            title="맡은 일과 빌린 물건";
            var tasks=Source as IPlayerTaskJournalPort;var journal=tasks?.ReadTaskJournal()??new TaskJournalView();
            bool Active(TaskJournalEntry e)=>e.CanPin||e.Status=="답하기 전";
            var entries=journal.Entries.Where(e=>taskHistory?!Active(e):Active(e)).ToArray();
            var chosen=entries.FirstOrDefault(e=>e.Id==selectedTask)??entries.FirstOrDefault(e=>e.Pinned)??entries.FirstOrDefault();selectedTask=chosen?.Id??"";
            body=entries.Length==0?(taskHistory?"아직 마치거나 접어 둔 일이 없어요.":"지금 맡은 일이나 빌린 물건이 없어요.\n사람들과 이야기하고 주변을 둘러봐도 괜찮아요."):"";
            context=chosen==null?"화면에는 할 일 하나만 표시해요.\n표시를 숨겨도 약속이나 대여는 취소되지 않아요.":chosen.Title+"\n\n"+chosen.Instruction+"\n\n직접 들은 말\n“"+chosen.SourceText+"”";
            if(chosen!=null){
                if(chosen.LastItemPlace!="")context+="\n\n물건을 마지막으로 본 곳\n"+Place(chosen.LastItemPlace)+" · "+TimeLabel(chosen.ItemSeenTick);
                if(chosen.LastActorPlace!="")context+="\n\n"+ActorLabel(chosen.ActorId)+"을 마지막으로 본 곳\n"+Place(chosen.LastActorPlace)+" · "+TimeLabel(chosen.ActorSeenTick);
                if(chosen.LastItemPlace!=""||chosen.LastActorPlace!="")context+="\n그 뒤에 이동했을 수 있어요.";
                if(chosen.CanPin)button(chosen.Pinned?"화면 표시 숨기기":"이 할 일 표시하기",()=>{if(chosen.Pinned){tasks.HidePinnedTask();status="화면 표시만 숨겼어요. 맡은 일은 그대로 남아요.";}else status=tasks.PinTask(chosen.Id);taskHudRefresh=0;});
            }
            if(journal.Hidden&&journal.Entries.Any(e=>e.CanPin))button("할 일 표시 다시 켜기",()=>{tasks.ShowPinnedTask();taskHudRefresh=0;status="할 일 하나를 다시 표시해요.";});
            button(taskHistory?"지금 할 일":"지난 일",()=>{taskHistory=!taskHistory;selectedTask="";});
            view.SetRecordRows(entries.Select(e=>new RecordRow{Id=e.Id,Heading=(e.Pinned?"표시 중 · ":"")+e.Status,Text=e.Title}).ToArray(),selectedTask,id=>{selectedTask=id;Render();});
        }
    }
}
