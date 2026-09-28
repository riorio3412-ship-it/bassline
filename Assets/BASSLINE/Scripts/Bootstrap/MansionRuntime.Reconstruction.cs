using System;
using System.Linq;
using System.IO;
using BASSLINE.Core;
using BASSLINE.Investigation;
using BASSLINE.NPC;
using BASSLINE.Trial;
using BASSLINE.Save;
using BASSLINE.Knowledge;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerReconstructionPort,IPlayerCausalReconstructionPort
    {
        ReconstructionSnapshot Reconstruction=>proceedings.Reconstruction;
        bool CanEditReconstruction=>!Observer&&court.Phase=="Debate"&&!court.Focused&&Reconstruction.Phase=="Editing";
        public ReconstructionView ReadReconstruction()
        {
            var s=Reconstruction;var assessment=FinalReconstruction.Assess(s,Knowledge.For("CH_01"));
            var courtState=court.Capture();var heard=court.Read("CH_01",false).History;
            var defense=s.Defenses.LastOrDefault(d=>d.Revision==s.Revision&&heard.Any(h=>h.Speech.Id==d.SpeechId));
            return new ReconstructionView{Phase=s.Phase,Revision=s.Revision,AccusedId=s.AccusedId,Assessment=assessment.Status,Issues=assessment.Issues.Select(IssueText).ToArray(),IncompleteAccepted=s.AcceptedIncompleteRevision==s.Revision,Published=s.Publications.Any(p=>p.Revision==s.Revision&&heard.Any(h=>h.Speech.Id==p.SpeechId)),CanVote=s.Phase=="Ready"&&!court.Focused&&courtState.Pending.Length==0,Candidates=(string[])courtState.Participants.Clone(),DefenseSpeaker=defense?.ActorId??"",DefenseText=defense?.Text??"",Entries=s.Entries.OrderBy(e=>e.Group).ThenBy(e=>e.FromTick).ThenBy(e=>e.Id,StringComparer.Ordinal).Select(e=>new ReconstructionEntryView{Id=e.Id,Group=e.Group,Role=e.Role,ActorId=e.ActorId,Text=e.Text,PlaceId=e.PlaceId,FromTick=e.FromTick,ToTick=e.ToTick,Assumption=e.Assumption,Status=assessment.EntryStatus[e.Id],RecordIds=(string[])e.RecordIds.Clone()}).ToArray()};
        }
        public string AddReconstructionEvidence(string recordId,string group)
        {
            if(!CanEditReconstruction)return message="지금 설명을 마치거나, 수정하기를 눌러 주세요.";
            string result=FinalReconstruction.Add(Reconstruction,Knowledge.For("CH_01"),recordId,group);
            return message=result.StartsWith("RECON_ENTRY_",StringComparison.Ordinal)?"설명에 자료를 추가했어요.":result=="AlreadyAdded"?"이미 이 사건에 추가한 자료예요.":"지금 이 자료를 추가할 수 없어요.";
        }
        public string ReadCausalConnection(string causeRecordId)=>FinalReconstruction.BuildConnection(Knowledge.For("CH_01"),causeRecordId)?.Text??"";
        public string AddCausalConnection(string causeRecordId,string group)
        {
            if(!CanEditReconstruction)return message="지금 설명을 마치거나, 수정하기를 눌러 주세요.";
            string result=FinalReconstruction.AddConnection(Reconstruction,Knowledge.For("CH_01"),causeRecordId,group);
            return message=result.StartsWith("RECON_ENTRY_",StringComparison.Ordinal)?"행동과 결과를 원래 자료와 함께 연결했어요.":result=="AlreadyAdded"?"이미 추가한 연결이에요.":"행동·위험 안내·작동 규칙·결과를 잇는 자료가 아직 부족해요.";
        }
        public string ChangeReconstructionRole(string entryId,string role)
        {
            var entry=Reconstruction.Entries.FirstOrDefault(e=>e.Id==entryId);if(!CanEditReconstruction||entry==null||!FinalReconstruction.Roles.Contains(role))return message="바꿀 항목을 먼저 골라 주세요.";
            if(entry.Role!=role){entry.Role=role;FinalReconstruction.Changed(Reconstruction);}return message=FinalReconstruction.RoleLabel(role)+"으로 정리했어요.";
        }
        public string SetReconstructionAssumption(string entryId,bool assumption)
        {
            var entry=Reconstruction.Entries.FirstOrDefault(e=>e.Id==entryId);if(!CanEditReconstruction||entry==null)return message="바꿀 항목을 먼저 골라 주세요.";
            if(entry.Assumption!=assumption){entry.Assumption=assumption;FinalReconstruction.Changed(Reconstruction);}return message=assumption?"아직 가정인 부분으로 표시했어요.":"가정 표시를 지웠어요. 자료가 뒷받침하는 범위는 그대로예요.";
        }
        public string RemoveReconstructionEntry(string entryId)
        {
            if(!CanEditReconstruction||!Reconstruction.Entries.Any(e=>e.Id==entryId))return message="삭제할 항목을 먼저 골라 주세요.";
            Reconstruction.Entries=Reconstruction.Entries.Where(e=>e.Id!=entryId).ToArray();FinalReconstruction.Changed(Reconstruction);return message="설명에서 뺐어요. 원래 자료는 수첩에 남아 있어요.";
        }
        public string SetReconstructionTarget(string actorId)
        {
            if(!CanEditReconstruction||!court.Capture().Participants.Contains(actorId))return message="참여 중인 인물을 골라 주세요.";
            if(Reconstruction.AccusedId!=actorId){Reconstruction.AccusedId=actorId;FinalReconstruction.Changed(Reconstruction);}return message=NameOf(actorId)+"의 책임을 묻는 설명으로 정리합니다.";
        }
        public string PublishReconstruction()
        {
            if(court.JointArgumentBusy)return message="함께 설명하기를 마치거나 중단한 뒤 발표해 주세요.";
            var s=Reconstruction;if(!CanEditReconstruction||s.Entries.Length==0||s.AccusedId=="")return message="자료를 추가하고 책임을 물을 인물을 먼저 골라 주세요.";
            var assessment=FinalReconstruction.Assess(s,Knowledge.For("CH_01"));string id="RECON_SPEECH_"+s.Revision;
            string text="제가 정리한 설명을 말할게요. "+string.Join(" ",s.Entries.OrderBy(e=>e.Group).ThenBy(e=>e.FromTick).Select(e=>"사건 "+e.Group+", "+FinalReconstruction.RoleLabel(e.Role)+(e.Assumption?"이라는 가정":"")+": "+e.Text+" 확인한 범위는 "+WorldTimeLabel.Format(e.FromTick,World.ClockVersion)+"부터 "+WorldTimeLabel.Format(e.ToTick,World.ClockVersion)+"까지예요."))+" 이 설명에서 책임을 물을 사람은 "+NameOf(s.AccusedId)+"입니다.";
            if(assessment.Issues.Length>0)text+=" 아직 확인하지 못한 부분도 있어요. "+string.Join(" ",assessment.Issues.Select(IssueText));
            var claim=new ClaimRecord{Id="CLAIM_"+id,OwnerId="CH_01",LoopId=Knowledge.LoopId,Text=text,Spans=s.Entries.Select(e=>new ClaimSpan{Id=id+"_"+e.Id,SubjectId=string.IsNullOrEmpty(e.SubjectId)?e.ActorId:e.SubjectId,Predicate=e.Predicate,Value=e.Value,PlaceId=e.PlaceId,FromTick=e.FromTick,ToTick=e.ToTick,Quantifier="Particular"}).ToArray()};
            if(court.QueueFinalExplanation(new SpeechDraft{Id=id,Speaker="CH_01",Topic="사건 재구성",Text=text,Claim=claim})!="Queued")return message="현재 발언을 정리한 뒤 다시 말해 주세요.";
            s.Publications=s.Publications.Concat(new[]{new ReconstructionPublication{Revision=s.Revision,AccusedId=s.AccusedId,SpeechId=id,ClaimId=claim.Id,KnowledgeRevision=Knowledge.For("CH_01").Revision,CourtTick=court.Capture().CourtTick,Issues=assessment.Issues.Select(IssueText).ToArray(),Entries=s.Entries.Select(e=>e.Copy()).ToArray()}}).ToArray();s.Phase="Presenting";
            proceedings.SpokenRecords=proceedings.SpokenRecords.Concat(s.Entries.SelectMany(e=>e.RecordIds).Distinct().Select(record=>id+"|"+record)).ToArray();return message="정리한 설명을 발표합니다.";
        }
        static string IssueText(string text){int colon=text.IndexOf(": ",StringComparison.Ordinal);return text.StartsWith("RECON_ENTRY_",StringComparison.Ordinal)&&colon>=0?text.Substring(colon+2):text;}
        public string ReviseReconstruction()
        {
            var s=Reconstruction;if(Observer||court.Phase!="Debate"||court.Focused||s.Phase=="Presenting"||s.Phase=="Defense")return message="진행 중인 말을 먼저 들어 주세요.";
            if(s.Phase!="Editing"){s.Phase="Editing";FinalReconstruction.Changed(s);court.ResumeOrdinaryDebate();}return message="설명을 수정할 수 있어요. 이전에 발표한 내용은 대화 기록에 남아요.";
        }
        void AdvanceFinalDefense()
        {
            var s=Reconstruction;var state=court.Capture();if(s.Phase=="Defense"){
                var defense=s.Defenses.LastOrDefault(d=>d.State=="Queued");
                var utterance=defense==null?null:state.Transcript.FirstOrDefault(h=>h.Speech.Id==defense.SpeechId);
                if(utterance==null||court.Focused)return;
                if(defense.Action!=""){
                    var defenseKnowledge=Knowledge.For(defense.ActorId);
                    if(court.EnterFocus(defenseKnowledge,defense.ClaimId,defense.SpanId)!="Focused")return;
                    defense.RequestId="REVIEW_"+defense.Id;
                    var result=court.Submit(defenseKnowledge,defense.RequestId,defense.Action,defense.RuleId,defense.RecordIds);
                    defense.RequestId=result.RequestId;
                    defense.ReviewState=result.ResultType;
                    var explanation=state.Transcript.First(h=>h.Speech.Claim?.Id==defense.ClaimId);
                    foreach(string listener in utterance.ReceivedBy.Intersect(explanation.ReceivedBy))court.DeliverSubmission(result.RequestId,listener);
                    if(court.Focused)court.CancelFocus(defense.ActorId);
                }else defense.ReviewState="UnresolvedGap";
                defense.State="Spoken";s.Phase="Ready";return;
            }
            if(s.Phase!="Presenting")return;
            var publication=s.Publications.Last();var spoken=state.Transcript.FirstOrDefault(h=>h.Speech.Id==publication.SpeechId);if(spoken==null)return;
            string accused=publication.AccusedId;
            if(accused=="CH_01"||!spoken.ReceivedBy.Contains(accused)){s.Phase="Ready";return;}
            var own=Knowledge.For(accused);var spans=spoken.Speech.Claim.Spans;
            NpcTrialResponse counter=null;
            foreach(var span in spans){
                var response=new TrialReasoning().RespondToClaim(own,new NpcHeardClaim{Id=publication.ClaimId,SpeakerId="CH_01",ReceiverId=accused,LoopId=Knowledge.LoopId,Span=new NpcTrialSpan{Id=span.Id,SubjectId=span.SubjectId,Predicate=span.Predicate,Value=span.Value,PlaceId=span.PlaceId,FromTick=span.FromTick,ToTick=span.ToTick,Quantifier=span.Quantifier}});
                if(response!=null&&response.Action!="Support"){counter=response;break;}
            }
            // Internal gaps are in the published explanation. Private facts are consulted only
            // through this person's owner-bound query, never the actual culprit/cause engine.
            string gap=publication.Issues.FirstOrDefault();if(counter==null&&gap==null){s.Phase="Ready";return;}
            var refs=counter?.RecordIds??Array.Empty<string>();string text=counter!=null?counter.Explanation+" "+string.Join(" ",refs.Select(own.Find).Where(r=>r!=null).Select(r=>r.Text)):"그 설명에는 아직 빈 부분이 있어요. "+IssueText(gap)+" 그 부분을 확인하지 않고 제 책임이라고 단정할 수는 없어요.";
            string signature=accused+"|"+string.Join(";",publication.Entries.Select(e=>e.Group+"|"+e.Role+"|"+e.ActorId+"|"+e.Predicate+"|"+e.Value+"|"+e.FromTick+"|"+e.ToTick+"|"+e.Assumption+"|"+string.Join(",",e.RecordIds)).OrderBy(k=>k,StringComparer.Ordinal))+"|"+string.Join(",",refs)+"|"+(counter?.RuleId??gap);
            string id="FINAL_DEFENSE_"+(s.Defenses.Length+1);
            var prior=s.Defenses.LastOrDefault(d=>d.Signature==signature&&d.State!="Queued");
            string speechId=prior?.SpeechId??id;
            // Identical explanations retain the unresolved earlier reply. They do not replay
            // it, silently clear it, or let listeners who missed it acquire its contents.
            if(prior==null&&court.QueueArgumentResponse(new SpeechDraft{Id=id,Speaker=accused,Topic="최종 설명에 대한 반론",Text=text},publication.ClaimId,counter?.SpanId??spans[0].Id)!="Queued")return;
            s.Defenses=s.Defenses.Concat(new[]{new FinalDefenseRecord{Id=id,ActorId=accused,SpeechId=speechId,Signature=signature,Text=text,Revision=publication.Revision,ClaimId=publication.ClaimId,SpanId=counter?.SpanId??spans[0].Id,Action=counter?.Action??"",RuleId=counter?.RuleId??"",RecordIds=(string[])refs.Clone()}}).ToArray();s.Phase="Defense";
            if(prior==null)proceedings.SpokenRecords=proceedings.SpokenRecords.Concat(refs.Select(record=>id+"|"+record)).ToArray();
        }
        public string ProceedFromReconstruction(bool acceptUnresolved)
        {
            var s=Reconstruction;if(Observer)return message="관찰 중입니다.";
            if(s.Phase=="Editing"){
                if(!acceptUnresolved)return message="설명을 발표하지 않고 판단하려면, 확인하지 않은 부분이 남는다는 점을 선택해 주세요.";
                if(court.OpenVoting()!="Opened")return message="아직 진행 중인 발언이 있어요. 논쟁으로 돌아가 말을 들어 주세요.";
                s.AcceptedIncompleteRevision=s.Revision;return message="정리 발표를 생략하고 투표를 시작합니다.";
            }
            if(s.Phase!="Ready")return message="설명과 답변을 먼저 들어 주세요.";
            var assessment=FinalReconstruction.Assess(s,Knowledge.For("CH_01"));bool unresolved=assessment.Issues.Length>0||s.Defenses.Any(d=>d.Revision==s.Revision&&d.State=="Spoken");
            if(unresolved&&!acceptUnresolved)return message="아직 남은 반론이 있어요. 설명을 고치거나, 불확실한 채 판단할지 선택해 주세요.";
            string opened=court.OpenVoting();if(opened!="Opened")return message="아직 진행 중인 발언이 있어요.";
            if(unresolved){s.AcceptedIncompleteRevision=s.Revision;foreach(var defense in s.Defenses.Where(d=>d.Revision==s.Revision&&d.State=="Spoken"))defense.State="Acknowledged";}
            return message="투표를 시작합니다. 설명이 가능하다는 것과 실제 정답인지는 별개입니다.";
        }
        static void ValidateReconstruction(MansionSessionSnapshot session,KnowledgeLedger knowledge)
        {
            var s=session.Proceedings.Reconstruction;var court=session.Proceedings.Court;FinalReconstruction.Validate(s,knowledge.For("CH_01"),court.Participants);
            var speeches=court.Pending.Concat(court.DeferredSpeeches).Concat(court.Transcript.Select(t=>t.Speech)).ToArray();
            foreach(var p in s.Publications){
                var speech=speeches.FirstOrDefault(x=>x.Id==p.SpeechId&&x.Speaker=="CH_01"&&x.Claim?.Id==p.ClaimId);
                if(p.CourtTick>court.CourtTick||speech==null||speech.Claim.Spans.Length!=p.Entries.Length)throw new InvalidDataException("재구성 발표 기록이 맞지 않습니다.");
                foreach(var entry in p.Entries)if(!speech.Claim.Spans.Any(span=>span.Id==p.SpeechId+"_"+entry.Id&&span.SubjectId==entry.ActorId&&span.Predicate==entry.Predicate&&span.Value==entry.Value&&span.PlaceId==entry.PlaceId&&span.FromTick==entry.FromTick&&span.ToTick==entry.ToTick))throw new InvalidDataException("발표와 재구성 자료의 범위가 다릅니다.");
            }
            foreach(var d in s.Defenses){
                if(d.RecordIds.Any(id=>knowledge.For(d.ActorId).Find(id)==null)||!speeches.Any(x=>x.Id==d.SpeechId&&x.Speaker==d.ActorId&&x.Text==d.Text)||!court.Transcript.Any(t=>t.Speech.Claim?.Id==d.ClaimId&&t.Speech.Claim.Spans.Any(span=>span.Id==d.SpanId)&&t.ReceivedBy.Contains(d.ActorId))||d.State!="Queued"&&!court.Transcript.Any(t=>t.Speech.Id==d.SpeechId))throw new InvalidDataException("최종 반론의 자료 또는 발화 상태가 맞지 않습니다.");
                if(d.Action!=""&&d.State!="Queued"&&!court.Submissions.Any(sub=>sub.Id==d.RequestId&&sub.Result.ResultType==d.ReviewState))throw new InvalidDataException("최종 반론의 판정 기록이 없습니다.");
            }
            var current=s.Publications.LastOrDefault(p=>p.Revision==s.Revision);
            if((s.Phase=="Defense"||s.Phase=="Ready")&&(current==null||!court.Transcript.Any(t=>t.Speech.Id==current.SpeechId))||s.Phase=="Presenting"&&(current==null||!court.Pending.Concat(court.Transcript.Select(t=>t.Speech)).Any(t=>t.Id==current.SpeechId)))throw new InvalidDataException("발표가 끝나기 전에 판단 단계로 넘어간 저장입니다.");
        }
    }
}
