using System;
using System.Linq;
using System.IO;
using BASSLINE.Core;
using BASSLINE.Trial;
using BASSLINE.Save;
using BASSLINE.Knowledge;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerJointArgumentPort
    {
        public JointArgumentView ReadJointArgument()
        {
            var view=new JointArgumentView();if(Observer||court.Phase=="NotStarted")return view;
            var s=court.Capture();var own=Knowledge.For("CH_01");var j=s.JointArguments.LastOrDefault(x=>x.Owner=="CH_01");
            view.Choices=own.Records().Where(r=>!r.Direct&&r.Parents.Length>0&&r.Source!="CH_01"&&s.Participants.Contains(r.Source)).GroupBy(r=>r.RootId).Select(g=>g.First()).Select(r=>new JointProofChoice{Id=r.Id,Speaker=NameOf(r.Source),Text=r.Text}).ToArray();
            if(j==null)return view;view.Phase=j.Phase;view.Disclosure=j.Disclosure;
            view.Parts=j.Parts.Select(p=>new JointPartView{RecordId=p.SharedRecordId,Speaker=NameOf(p.Actor),Text=own.Find(p.SharedRecordId)?.Text??"수신했던 자료",State=p.State=="Spoke"&&!s.Transcript.Any(h=>h.Speech.Id==p.SpeechId&&h.ReceivedBy.Contains("CH_01"))||p.State=="Withdrawn"&&!s.Transcript.Any(h=>h.Speech.Id==p.WithdrawalId&&h.ReceivedBy.Contains("CH_01"))?"Unheard":p.State}).ToArray();return view;
        }
        string JointMessage(string result)=>message=result=="Added"?"함께 설명할 자료에 넣었어요.":result=="Removed"?"함께 설명할 목록에서 뺐어요. 수첩의 자료는 남아요.":result=="Changed"?"설명 순서와 공개 범위를 바꿨어요.":result=="Requested"?"자료를 전해 준 사람에게 동의를 구합니다. 답을 들어 주세요.":result=="Started"?"동의한 부분을 순서대로 설명합니다.":result=="Stopped"?"함께 설명하기를 멈췄어요. 이미 말한 부분과 가진 자료는 남아요.":result=="Limit"?"한 번에 자료 세 개까지 함께 설명할 수 있어요.":result=="SameRoot"?"같은 근원의 자료가 이미 들어 있어요.":result=="NotShared"?"다른 참여자에게 실제로 전달받은 자료를 골라 주세요.":"진행 중인 말을 마치거나 함께 설명하기를 중단해 주세요.";
        public string ToggleJointProof(string recordId)=>JointMessage(CanEditArgument?court.ToggleJointProof(Knowledge.For("CH_01"),recordId):"Unavailable");
        public string MoveJointProofFirst(string recordId)=>JointMessage(CanEditArgument?court.MoveJointProofFirst("CH_01",recordId):"Unavailable");
        public string ToggleJointDisclosure()=>JointMessage(CanEditArgument?court.ToggleJointDisclosure("CH_01"):"Unavailable");
        public string RequestJointConsent()=>JointMessage(CanEditArgument?court.RequestJointConsent("CH_01"):"Unavailable");
        public string StartJointArgument()=>JointMessage(CanEditArgument?court.StartJointArgument("CH_01"):"Unavailable");
        public string StopJointArgument()=>JointMessage(!Observer?court.StopJointArgument("CH_01"):"Unavailable");
        void AdvanceJointArguments()
        {
            if(court.Phase!="Debate"||court.Focused||Reconstruction.Phase!="Editing")return;
            var s=court.Capture();var j=s.JointArguments.FirstOrDefault(x=>new[]{"Requesting","Speaking"}.Contains(x.Phase));if(j==null)return;
            if(j.Phase=="Requesting"){
                if(j.Parts.Any(p=>p.State=="RequestQueued"||p.State=="AnswerQueued"))return;
                var waiting=j.Parts.FirstOrDefault(p=>p.State=="AwaitingAnswer");
                if(waiting!=null){
                    // Consent is decided using this participant's own B, after the request was received.
                    var own=Knowledge.For(waiting.Actor);var record=own.Records().FirstOrDefault(r=>r.RootId==waiting.RootId);
                    string decision=record==null?"Refused":!record.Direct&&j.Disclosure=="FullRecord"?"Deferred":"Agreed";
                    string answerText=decision=="Refused"?"지금 제 기록에서는 그 자료를 확인할 수 없어요. 함께 설명하기는 어렵겠어요.":decision=="Deferred"?"전해 들은 내용이라 전체 내용을 대신 확인해 드리기는 어려워요. 제가 알고 있는 범위만 설명하는 것으로 다시 요청해 주세요.":j.Disclosure=="ScopeOnly"?"좋아요. 제가 아는 시간 범위와 한계만 설명하겠습니다. 구체적인 내용은 공개하지 않을게요.":"좋아요. 전에 전달한 그 자료와 한계를 함께 설명하겠습니다. 그 밖의 내용까지 동의한 것은 아니에요.";
                    court.AnswerJointRequest(own,j.Id,waiting.Id,decision,record==null?"MissingRecord":decision=="Deferred"?"HearsayOnly":"SharedRecord",answerText);return;
                }
                var next=j.Parts.FirstOrDefault(p=>p.State=="NotAsked");if(next==null)return;
                int order=Array.IndexOf(j.Parts,next)+1;
                // Do not quote the evidence while requesting permission to disclose it.
                string request=NameOf(next.Actor)+", 전에 제게 전달한 자료 중 제가 고른 것을 "+order+"번째로 함께 설명해 줄 수 있나요? "+(j.Disclosure=="ScopeOnly"?"시간 범위와 한계만 말하고, 구체적인 내용은 공개하지 않는 것으로요.":"그 자료의 내용과 한계까지만 공개하는 것으로요.");
                court.QueueJointRequest(j.Owner,next.Id,request);return;
            }
            if(j.Parts.Any(p=>p.State=="Speaking"||p.State=="Withdrawing")||j.ClosingId!="")return;
            var part=j.Parts.FirstOrDefault(p=>p.State=="Agreed");
            if(part==null){
                var heard=s.Transcript.Where(h=>h.ReceivedBy.Contains(j.Owner)&&j.Parts.Any(p=>p.SpeechId==h.Speech.Id)).ToArray();
                court.QueueJointClosing(j.Owner,heard.Length==0?"함께 설명을 요청했지만 제가 끝까지 들은 부분은 없습니다. 가진 자료로 다시 확인하겠습니다.":"지금 들은 "+heard.Length+"개의 설명을 나란히 살펴보겠습니다. 함께 말했다고 사실이 더 확실해지는 것은 아닙니다. 같은 근원에서 나온 자료는 하나로 보고, 각 자료가 확인하는 범위 안에서 판단하겠습니다.");return;
            }
            var query=Knowledge.For(part.Actor);var source=query.Find(part.SourceRecordId);
            if(source==null||source.RootId!=part.RootId){court.WithdrawJointParticipation(part.Actor,j.Id,part.Id,"MissingRecord");return;}
            string text=source==null?"이 자료는 지금 제 기록에서 확인할 수 없습니다.":"제가 "+(source.Direct?"직접 확인한":"전해 들은")+" 자료입니다. "+(j.Disclosure=="FullRecord"?source.Text+" ":"")+"이 기록의 시간 범위는 "+WorldTimeLabel.Format(source.FromTick,World.ClockVersion)+"부터 "+WorldTimeLabel.Format(source.ToTick,World.ClockVersion)+"까지입니다. "+(j.Disclosure=="FullRecord"&&source.DoesNotEstablish.Length>0?"확인하지 못한 부분은 "+string.Join(" · ",source.DoesNotEstablish)+"입니다.":"이 시간 밖의 일이나 다른 사람의 의도까지 확인한 것은 아닙니다.");
            if(court.QueueJointContribution(query,j.Id,part.Id,text)=="Queued"&&j.Disclosure=="FullRecord")
                proceedings.SpokenRecords=proceedings.SpokenRecords.Concat(new[]{part.Id+"_EXPLANATION|"+part.SourceRecordId}).Distinct().ToArray();
        }
        void ReceiveJointSpeech(PublicSpeech speech)
        {
            if(!speech.Speech.Id.StartsWith("JOINT_",StringComparison.Ordinal))return;
            foreach(string listener in speech.ReceivedBy){
                // This proves that these words were heard, never the factual content of an unfinished statement.
                Knowledge.Observe(listener,new KnownRecord{Kind=speech.Speech.Id.EndsWith("_FRAGMENT",StringComparison.Ordinal)?"HeardFragment":"CourtSpeech",SubjectId=speech.Speech.Speaker,Predicate="SaidStatement",Value=speech.Speech.Id,PlaceId="R_TRIAL",Text=speech.Speech.Text,Source=speech.Speech.Speaker,ProvenanceKey="COURT_UTTERANCE_"+speech.Speech.Id,FromTick=World.Tick,ToTick=World.Tick+1,Position=World.Resident(listener).Position,IdentityConfirmed=true,Supports=new[]{"이 말을 실제로 들었다는 것"},DoesNotEstablish=new[]{"발언 내용의 사실 여부", "끝까지 말하지 않은 내용"}},World.Tick);
            }
        }
        static void ValidateJointSharing(MansionSessionSnapshot snapshot,KnowledgeLedger ledger)
        {
            var courtState=snapshot.Proceedings.Court;
            foreach(var joint in courtState.JointArguments??Array.Empty<JointArgumentState>())foreach(var part in joint.Parts){
                var shared=ledger.For(joint.Owner).Find(part.SharedRecordId);
                if(shared==null||shared.Direct||shared.Source!=part.Actor||shared.RootId!=part.RootId||shared.Parents.Length==0)throw new InvalidDataException("공동 설명에 실제로 공유받지 않은 자료가 있습니다.");
                var own=ledger.For(part.Actor);
                if(!shared.Parents.Any(id=>own.Find(id)?.RootId==part.RootId))throw new InvalidDataException("공동 설명 자료의 전달 경로가 맞지 않습니다.");
                if(part.SourceRecordId!=""&&own.Find(part.SourceRecordId)?.RootId!=part.RootId)throw new InvalidDataException("설명자가 동의한 원본 자료가 없습니다.");
                var speech=courtState.Transcript.Select(h=>h.Speech).Concat(courtState.Pending).Concat(courtState.DeferredSpeeches).FirstOrDefault(h=>h.Id==part.SpeechId);
                if(speech?.Claim!=null){var record=own.Find(part.SourceRecordId);var span=speech.Claim.Spans.SingleOrDefault();
                    if(record==null||span==null||joint.Disclosure!="FullRecord"||span.SubjectId!=record.SubjectId||span.Predicate!=record.Predicate||span.Value!=record.Value||span.PlaceId!=record.PlaceId||span.FromTick!=record.FromTick||span.ToTick!=record.ToTick)throw new InvalidDataException("동의한 자료 밖의 내용이 공동 설명에 포함되었습니다.");
                }
            }
            foreach(string mapping in snapshot.Proceedings.SpokenRecords.Where(x=>x.StartsWith("JOINT_",StringComparison.Ordinal))){
                var pair=mapping.Split('|');
                if(pair.Length!=2||!courtState.JointArguments.Any(j=>j.Disclosure=="FullRecord"&&j.Parts.Any(p=>p.SpeechId==pair[0]&&p.SourceRecordId==pair[1])))throw new InvalidDataException("공동 설명의 공개 범위와 자료 전달이 맞지 않습니다.");
            }
        }
    }
}
