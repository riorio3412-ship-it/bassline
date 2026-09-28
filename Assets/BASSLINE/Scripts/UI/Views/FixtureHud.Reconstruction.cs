using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.UI
{
    public sealed partial class FixtureHud
    {
        string reconstructionPanel="Entries",reconstructionGroup="A",reconstructionEntry="",reconstructionRecord="",lastReconstructionPhase="";
        int reconstructionTarget;bool showAllReconstructionRecords;
        IPlayerReconstructionPort ReconstructionPort=>Source as IPlayerReconstructionPort;
        static readonly string[] ReconstructionRoles={"Observation","Cause","Outcome","Discovery","Aftermath"};
        static string ReconstructionRoleLabel(string role){switch(role){case "Cause":return "원인 행동";case "Outcome":return "결과 관측";case "Discovery":return "발견";case "Aftermath":return "이후 행동";default:return "확인한 사실";}}
        static string ReconstructionStatus(string state){switch(state){case "Supported":return "자료가 뒷받침함";case "Contested":return "충돌하는 자료가 있음";default:return "추가 확인이 필요함";}}
        void CaptureReconstructionSelection(PlayerUiSnapshot s){s.ReconstructionPanel=reconstructionPanel;s.ReconstructionGroup=reconstructionGroup;s.ReconstructionEntry=reconstructionEntry;s.ReconstructionRecord=reconstructionRecord;s.ReconstructionTarget=reconstructionTarget;}
        void RestoreReconstructionSelection(PlayerUiSnapshot s){reconstructionPanel=string.IsNullOrEmpty(s.ReconstructionPanel)?"Entries":s.ReconstructionPanel;reconstructionGroup=s.ReconstructionGroup=="B"?"B":"A";reconstructionEntry=s.ReconstructionEntry??"";reconstructionRecord=s.ReconstructionRecord??"";reconstructionTarget=Math.Max(0,s.ReconstructionTarget);lastReconstructionPhase="";}
        void TickReconstruction()
        {
            if(ReconstructionPort==null)return;string phase=ReconstructionPort.ReadReconstruction().Phase;
            if(phase=="Ready"&&lastReconstructionPhase!=phase&&CurrentScreen==23){reconstructionPanel="Entries";Open(30);}lastReconstructionPhase=phase;
        }
        bool RenderReconstruction(ProductionScreenView view,Action<string,Action> add,out string body,out string context)
        {
            body=context="";var port=ReconstructionPort;if(port==null)return false;
            var r=port.ReadReconstruction();var entries=r.Entries;
            if(r.Phase=="Presenting"||r.Phase=="Defense"||r.Phase=="Ready"||reconstructionPanel=="SkipConfirm"||reconstructionPanel=="Entry")view.SetRecordRows(Array.Empty<RecordRow>(),"",null);
            int index=Math.Max(0,Array.FindIndex(entries,e=>e.Id==reconstructionEntry));var entry=entries.ElementAtOrDefault(index);reconstructionEntry=entry?.Id??"";
            string Describe(ReconstructionEntryView e)=>"사건 "+e.Group+" · "+ReconstructionRoleLabel(e.Role)+(e.Assumption?" (가정)":"")+"\n"+e.Text+"\n"+ReconstructionStatus(e.Status);
            string unresolved=r.Issues.Length==0?"현재 자료와의 충돌은 찾지 못했어요. 실제 정답을 인증한 것은 아니에요.":"아직 확인할 부분\n\n"+string.Join("\n\n",r.Issues);
            if(r.Phase=="Presenting"||r.Phase=="Defense"){
                var c=Court.ReadTrial();body=(c.SpeakerId==""?"":ActorLabel(c.SpeakerId)+"\n\n")+(c.SpokenText==""?"설명과 답변이 이어집니다.":c.SpokenText);context="발언을 들은 뒤 설명을 고치거나 판단으로 넘어갈 수 있어요.";
                add("계속 듣기",ReturnToDebate);return true;
            }
            if(r.Phase=="Ready"){
                body="책임을 물은 인물 · "+ActorLabel(r.AccusedId)+"\n\n"+(r.DefenseText==""?"지금 설명에 대한 추가 반론은 수신하지 못했어요.":ActorLabel(r.DefenseSpeaker)+"의 답변\n“"+r.DefenseText+"”");context=unresolved;
                if(reconstructionPanel=="Confirm"){
                    body="불확실한 부분을 남긴 채 판단할까?\n\n"+body;add("이대로 투표하기",()=>{status=port.ProceedFromReconstruction(true);if(Court.ReadTrial().CanVote)Open(31);});add("돌아가기",()=>reconstructionPanel="Entries");
                }else{
                    add("설명 고치기",()=>{status=port.ReviseReconstruction();reconstructionPanel="Entries";});add("논쟁 다시 듣기",()=>{status=port.ReviseReconstruction();ReturnToDebate();});
                    add("판단하기",()=>{if(r.Issues.Length>0||r.DefenseText!="")reconstructionPanel="Confirm";else{status=port.ProceedFromReconstruction(false);if(Court.ReadTrial().CanVote)Open(31);}});
                }return true;
            }
            if(reconstructionPanel=="SkipConfirm"){
                body="정리 발표 없이 판단할까?\n\n설명을 발표하지 않아도 투표할 수 있어요. 아직 대조하지 못한 자료나 듣지 못한 반론이 남아 있을 수 있어요.";context=unresolved;
                add("이대로 투표하기",()=>{status=port.ProceedFromReconstruction(true);if(Court.ReadTrial().CanVote)Open(31);});add("돌아가기",()=>reconstructionPanel="Entries");return true;
            }
            if(reconstructionPanel=="Evidence"){
                var all=notebook.ReadNotebook().Records;var records=showAllReconstructionRecords?all.Where(x=>x.Kind!="ArchiveMeta").OrderByDescending(x=>x.ReceivedTick).ToArray():PlayerRecordPresentation.Compact(all);int selected=Math.Max(0,Array.FindIndex(records,x=>x.Id==reconstructionRecord));var evidence=records.ElementAtOrDefault(selected);reconstructionRecord=evidence?.Id??"";
                body=evidence==null?"추가할 단서가 없어요.":"";context=(evidence?.Text??"단서를 골라 주세요.")+"\n\n추가할 곳 · 사건 "+reconstructionGroup;
                view.SetRecordRows(records.Select(x=>new RecordRow{Id=x.Id,Heading=Place(x.PlaceId)+" · "+TimeLabel(x.FromTick),Text=x.Text}).ToArray(),reconstructionRecord,id=>{reconstructionRecord=id;Render();});
                if(evidence!=null){add("이 단서로 정리하기",()=>{status=port.AddReconstructionEvidence(evidence.Id,reconstructionGroup);reconstructionPanel="Entries";});add("사건 "+reconstructionGroup+" → 바꾸기",()=>reconstructionGroup=reconstructionGroup=="A"?"B":"A");}
                add(showAllReconstructionRecords?"간단히 보기":"이전 관측도 보기",()=>showAllReconstructionRecords=!showAllReconstructionRecords);
                if(evidence!=null&&Source is IPlayerCausalReconstructionPort causal){
                    string connection=causal.ReadCausalConnection(evidence.Id);
                    if(connection!=""){
                        context+="\n\n이 행동 뒤에 벌어진 일을 함께 설명할 단서가 있어요.\n\n"+connection;
                        add("무슨 결과로 이어졌는지 정리",()=>{status=causal.AddCausalConnection(evidence.Id,reconstructionGroup);reconstructionPanel="Entries";});
                    }
                }
                add("설명으로",()=>reconstructionPanel="Entries");return true;
            }
            if(reconstructionPanel=="Target"){
                reconstructionTarget=Math.Min(reconstructionTarget,Math.Max(0,r.Candidates.Length-1));string target=r.Candidates.ElementAtOrDefault(reconstructionTarget)??"";
                body="누구의 책임을 묻는 설명일까?\n\n"+ActorLabel(target);context="원인 행동과 이후 행동의 주체를 구분해 주세요. 이 선택은 최종 투표와 별개이고 다시 바꿀 수 있어요.";
                view.SetRecordRows(r.Candidates.Select(id=>new RecordRow{Id=id,Heading=ActorLabel(id),Text=id==r.AccusedId?"현재 선택한 인물":"이 인물의 행동 정리하기"}).ToArray(),target,id=>{reconstructionTarget=Array.IndexOf(r.Candidates,id);Render();});
                if(target!=""){add("이 인물 선택",()=>{status=port.SetReconstructionTarget(target);reconstructionPanel="Entries";});}add("돌아가기",()=>reconstructionPanel="Entries");return true;
            }
            body=entry==null?"무슨 일이 있었을까?\n\n직접 살펴본 단서와 들은 말을 골라 사건의 흐름을 정리해 보자.":reconstructionPanel=="Entry"?Describe(entry):"";
            if(reconstructionPanel!="Entry")view.SetRecordRows(entries.Select((e,i)=>new RecordRow{Id=e.Id,Heading=(i+1)+" · "+ReconstructionRoleLabel(e.Role),Text=e.Text+"\n"+ReconstructionStatus(e.Status)}).ToArray(),reconstructionEntry,id=>{reconstructionEntry=id;Render();});
            context="책임을 물을 인물 · "+(r.AccusedId==""?"아직 선택하지 않음":ActorLabel(r.AccusedId))+"\n\n"+unresolved;
            if(reconstructionPanel=="Entry"&&entry!=null){
                add("역할 · "+ReconstructionRoleLabel(entry.Role),()=>status=port.ChangeReconstructionRole(entry.Id,ReconstructionRoles[(Array.IndexOf(ReconstructionRoles,entry.Role)+1)%ReconstructionRoles.Length]));
                add(entry.Assumption?"가정 표시 지우기":"아직 가정으로 두기",()=>status=port.SetReconstructionAssumption(entry.Id,!entry.Assumption));add("설명에서 빼기",()=>{status=port.RemoveReconstructionEntry(entry.Id);reconstructionPanel="Entries";});add("돌아가기",()=>reconstructionPanel="Entries");return true;
            }
            if(entry!=null){add("선택한 내용 고치기",()=>reconstructionPanel="Entry");}
            add("단서 고르기",()=>reconstructionPanel="Evidence");add("책임 인물 고르기",()=>reconstructionPanel="Target");
            if(entry!=null)add("설명 발표하기",()=>{status=port.PublishReconstruction();if(port.ReadReconstruction().Phase=="Presenting")ReturnToDebate();});
            add("발표 없이 판단하기",()=>reconstructionPanel="SkipConfirm");
            add("논쟁으로",ReturnToDebate);return true;
        }
    }
}
