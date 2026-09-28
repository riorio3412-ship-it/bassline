using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.UI
{
    public sealed partial class FixtureHud
    {
        bool welcomePanel;int recordCategory,manualSlot=1;string pendingSave="";
        SaveSlotView[] saveLibrary;
        string DialogueCommand(Func<string> command)
        {
            bool held=stack.Contains(3);if(held)port.Pause("K_DIALOGUE_READ",false);
            try{var result=command();return result=="Dialogue"?"":result;}finally{SyncDialoguePause();}
        }
        void RenderWelcome(Action<string,Action> button,ref string title,ref string body,ref string context)
        {
            title="처음, 저택의 문 앞에서";
            body="<color=#80C9CD>01   둘러보기</color>\n마우스로 주위를 보고 WASD로 움직입니다. 가까운 사람이나 물건을 바라보면 가능한 행동이 나타납니다.\n\n<color=#80C9CD>02   말 걸고, 살펴보기</color>\nE로 대화하거나 문을 엽니다. R로 자세히 살펴봅니다. 물건을 들었다면 G로 내려놓습니다.\n\n<color=#80C9CD>03   생각할 시간</color>\nN은 노트, M은 지도입니다. 대화와 노트를 읽는 동안 세계가 멈춥니다. 급하게 읽지 않아도 괜찮습니다.";
            context="현관 안내문\n\n식사와 물은 공용 시설에서 이용하실 수 있습니다. 개인실에는 노크 후 허락을 구해 주십시오.\n\n처음에는 가까운 사람에게 말을 걸어 보세요. 기록은 직접 본 것과 들은 말로 채워집니다.\n\n이동이 불편하면 설정에서 감도와 시야각을 조절할 수 있습니다.";
            button("문을 열고 들어가기",()=>{(Source as IPlayerWelcomePort)?.ReadWelcome();welcomePanel=false;titleMenu=false;Back();});
            button("조작 · 읽기 설정",()=>Open(39));
        }
        int recordVisibleCount=20;bool showEveryObservation,comparingPatterns;string patternLeft="",patternRight="";
        void RenderRecordLibrary(ProductionScreenView view,KnownRecord[] all,Action<string,Action> button,ref string title,ref string body,ref string context)
        {
            if(recordCategory==0)recordCategory=2;
            if(recordCategory!=1)comparingPatterns=false;
            var patterns=all.Where(SurfacePatternComparison.CanCompare).GroupBy(r=>r.ProvenanceKey).Select(g=>g.First()).ToArray();
            if(comparingPatterns){
                title="무늬 비교";body="살펴본 표면 두 개를 골라 주세요.";
                var left=patterns.FirstOrDefault(r=>r.Id==patternLeft);var right=patterns.FirstOrDefault(r=>r.Id==patternRight);
                context=left==null?"첫 번째 표면을 목록에서 골라 주세요.":right==null?"A를 골랐어요. 비교할 두 번째 표면을 골라 주세요.":"A · "+left.Text+"\n\nB · "+right.Text+"\n\n"+SurfacePatternComparison.Describe(left,right);
                view.SetRecordRows(patterns.Select(r=>new RecordRow{Id=r.Id,Heading=(r.Id==patternLeft?"A · ":r.Id==patternRight?"B · ":"")+Place(r.PlaceId)+" · "+TimeLabel(r.FromTick),Text=r.Text}).ToArray(),patternRight!=""?patternRight:patternLeft,id=>{if(id==patternLeft){patternLeft=patternRight;patternRight="";}else if(id==patternRight)patternRight="";else if(patternLeft==""||patternRight!=""){patternLeft=id;patternRight="";}else patternRight=id;Render();});
                button("다시 고르기",()=>{patternLeft=patternRight="";});button("살펴본 목록으로",()=>comparingPatterns=false);return;
            }
            view.SetRecordRows(Array.Empty<RecordRow>(),"",null);
            if(recordCategory==1&&patterns.Length>=2)button("무늬 비교",()=>{comparingPatterns=true;patternLeft=patternRight="";});
            bool withPerson=stack.Contains(3)&&recordCategory==2;
            var displayRecords=recordCategory==1&&!showEveryObservation?PlayerRecordPresentation.Compact(all):all;
            var filtered=displayRecords.Where(r=>PersonalRecordFilter.Includes(r,recordCategory)&&(!withPerson||r.Source==selectedTarget||r.Source=="CH_01"&&r.ConversationWith==selectedTarget)).ToArray();
            title=recordCategory==2?(withPerson?ActorLabel(selectedTarget)+"와 나눈 대화":"나눈 대화"):recordCategory==1?"살펴본 것":"받은 안내";
            body=filtered.Length==0?"아직 여기에 남은 내용이 없어요.":string.Join("\n\n",filtered.Take(recordVisibleCount).Select(r=>"<color=#89CDCF><size=85%>"+TimeLabel(r.FromTick)+" · "+(recordCategory==2?ActorLabel(r.Source):Place(r.PlaceId))+"</size></color>\n"+(recordCategory==2?"“"+r.Text+"”":r.Text)));
            var speech=(Source as IPlayerConversationPlaybackPort)?.ReadConversationPlayback();
            if(withPerson&&speech?.Speaking==true&&speech.HeardText.Length>0)body="<color=#89CDCF>지금까지 들은 말</color>\n“"+speech.HeardText+"”\n\n"+body;
            if(recordCategory==1)button(showEveryObservation?"간단히 보기":"이전 관측도 보기",()=>{showEveryObservation=!showEveryObservation;recordVisibleCount=20;});
            context=(recordCategory==1&&!showEveryObservation?"같은 내용은 묶어서 보여요. 중요한 단서가 먼저 나와요.\n":"")+"마우스 휠이나 Page Up / Down으로 읽을 수 있어요.\n\n읽는 동안 시간은 멈춥니다.";
            if(filtered.Length>recordVisibleCount){context+="\n\n"+filtered.Length+"개 중 "+recordVisibleCount+"개 표시";button("이전 내용 더 보기",()=>recordVisibleCount+=20);}
            if(!stack.Contains(3)){
                if(recordCategory!=2)button("나눈 대화",()=>{recordCategory=2;recordVisibleCount=20;});
                if(recordCategory!=1)button("살펴본 것",()=>{recordCategory=1;recordVisibleCount=20;});
                if(recordCategory!=3)button("받은 안내",()=>{recordCategory=3;recordVisibleCount=20;});
            }
        }
        void RenderSaveLibrary(Action<string,Action> button,ref string body,ref string context)
        {
            if(!(Source is IPlayerSaveLibraryPort library)){body="현재 진행을 저장합니다.";context="이전 저장을 불러오면 현재 진행을 대체합니다.";button("저장",()=>status=port.SaveSlot());button("불러오기",()=>{status=port.LoadSlot();if(status=="불러오기 완료")SyncPauseView();});return;}
            if(saveLibrary==null)saveLibrary=library.ReadSaveSlots();var selected=saveLibrary[manualSlot-1];
            int start=(manualSlot-1)/5*5;
            body=string.Join("\n\n",saveLibrary.Skip(start).Take(5).Select(s=>(s.Slot==manualSlot?"<color=#80C9CD>▶  ":"     ")+"SLOT "+s.Slot.ToString("00")+"   "+s.Description+(s.Slot==manualSlot?"</color>":"")));
            context="수동 저장 10개\n\n세계의 상태, 개인 기억, 약속과 노트를 함께 보관합니다.\n\n슬롯 "+manualSlot+"\n"+selected.Description;
            if(pendingSave!=""){
                context=pendingSave=="save"?"슬롯 "+manualSlot+"을 현재 진행으로 덮어쓸까요?\n이전 파일 한 개는 복구용으로 보존합니다.":"슬롯 "+manualSlot+"을 불러올까요?\n저장하지 않은 현재 진행은 사라집니다.";
                button("확인",()=>{bool loading=pendingSave=="load";pendingSave="";status=loading?library.LoadManual(manualSlot):library.SaveManual(manualSlot);saveLibrary=null;if(loading&&status=="불러오기 완료"){titleMenu=false;welcomePanel=false;SyncPauseView();}});
                button("취소",()=>pendingSave="");return;
            }
            button("이전 슬롯",()=>manualSlot=(manualSlot+8)%10+1);button("다음 슬롯",()=>manualSlot=manualSlot%10+1);
            if(!titleMenu)button("여기에 저장",()=>{if(selected.Exists)pendingSave="save";else{status=library.SaveManual(manualSlot);saveLibrary=null;}});
            if(selected.Exists)button("불러오기",()=>pendingSave="load");
        }
    }
}
