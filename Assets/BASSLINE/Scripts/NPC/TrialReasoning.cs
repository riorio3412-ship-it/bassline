using System;
using System.Linq;
using System.Collections.Generic;
using BASSLINE.Core;
namespace BASSLINE.NPC
{
    /// <summary>Own B and actually heard public C only. No world state, hidden incident, culprit field, or future speech input.</summary>
    public sealed class TrialReasoning
    {
        static readonly HashSet<string> ClaimPredicates=new HashSet<string>{"AtPlace","PassedDoor","HeldObject","UsedObject","DoorState","HeardSound","SaidStatement","ReceivedInformation","CausedOutcome"};
        static KnownRecord[] Records(IActorKnowledgeQuery query)
        {
            if(query==null)throw new ArgumentNullException(nameof(query));
            return query.Records().Where(r=>r!=null&&r.LoopId==query.LoopId&&r.FromTick>=0&&r.ToTick>r.FromTick&&!string.IsNullOrWhiteSpace(r.Id)).ToArray();
        }
        public NpcTrialSpeech DraftSpeech(IActorKnowledgeQuery query,string speechId,string[] alreadySpokenRecordIds,int clockVersion=WorldTimeLabel.Legacy)
        {
            _=new StableId(speechId);var spoken=new HashSet<string>(alreadySpokenRecordIds??Array.Empty<string>());
            var record=Records(query).Where(r=>ClaimPredicates.Contains(r.Predicate)&&!spoken.Contains(r.Id)).OrderByDescending(r=>Priority(r.Predicate)).ThenBy(r=>r.ReceivedTick).ThenBy(r=>r.Id,StringComparer.Ordinal).FirstOrDefault();
            if(record==null)return null;
            string origin=record.Direct?"직접 관측한 범위만 말하겠습니다.":"전달받은 진술입니다. 내용의 사실 여부를 직접 확인한 것은 아닙니다.";
            string limits=record.DoesNotEstablish.Length>0?string.Join(" / ",record.DoesNotEstablish):"관측하지 않은 시각과 숨은 의도는 확인하지 못했습니다.";
            string text=origin+" "+record.Text+" [기록 범위 "+WorldTimeLabel.Format(record.FromTick,clockVersion)+" ~ "+WorldTimeLabel.Format(record.ToTick,clockVersion)+"] "+limits;
            return new NpcTrialSpeech{Id=speechId,SpeakerId=query.OwnerId,Topic=Topic(record.Predicate),Text=text,RecordIds=new[]{record.Id},Spans=new[]{new NpcTrialSpan{Id=speechId+"_SPAN_01",Predicate=record.Predicate,SubjectId=record.SubjectId,Value=record.Value,PlaceId=record.PlaceId,FromTick=record.FromTick,ToTick=record.ToTick}}};
        }
        static int Priority(string predicate){switch(predicate){case "CausedOutcome":return 5;case "UsedObject":return 4;case "SaidStatement":return 3;case "AtPlace":return 2;default:return 1;}}
        static string Topic(string predicate){switch(predicate){case "CausedOutcome":return "행동과 결과의 관측 범위";case "UsedObject":case "HeldObject":return "소지와 실제 사용의 구별";case "SaidStatement":return "실제로 수신한 발언";case "AtPlace":return "관측한 시각과 위치";case "HeardSound":return "들은 소리와 확인하지 못한 신원";default:return "기록의 출처와 범위";}}
        /// <summary>Proposes a response; the integration still submits these refs to LogicResolver before any review changes.</summary>
        public NpcTrialResponse RespondToClaim(IActorKnowledgeQuery query,NpcHeardClaim claim)
        {
            if(claim==null||claim.ReceiverId!=query?.OwnerId||claim.LoopId!=query.LoopId||claim.Span==null)return null;var span=claim.Span;
            var facts=Records(query).Where(r=>r.Direct&&r.SubjectId==span.SubjectId&&r.Predicate==span.Predicate).ToArray();
            bool Covers(KnownRecord r)=>r.FromTick<=span.FromTick&&r.ToTick>=span.ToTick;
            bool Overlaps(KnownRecord r)=>r.FromTick<span.ToTick&&span.FromTick<r.ToTick;
            var counter=facts.FirstOrDefault(r=>r.IdentityConfirmed&&r.Value!=span.Value&&Covers(r));
            if(counter!=null&&span.Predicate=="AtPlace")return Response("Rebut","LR03",claim,counter,"제가 관측한 같은 시각·신원의 위치는 이 부분과 다릅니다.");
            var match=facts.FirstOrDefault(r=>r.Value==span.Value&&Covers(r)&&(!span.SubjectId.StartsWith("CH_",StringComparison.Ordinal)||r.IdentityConfirmed));
            if(match!=null&&span.Quantifier=="Particular")return Response("Support","LR01",claim,match,"제가 가진 관측은 이 대상·시각·범위 안에서 지지합니다.");
            var partial=facts.FirstOrDefault(r=>r.Value==span.Value&&Overlaps(r));
            if(partial!=null)return Response("LimitScope","LR01",claim,partial,"제 관측 시각 밖까지 확대할 근거는 없습니다.");
            return null;
        }
        static NpcTrialResponse Response(string action,string rule,NpcHeardClaim claim,KnownRecord record,string text)=>new NpcTrialResponse{Action=action,RuleId=rule,ClaimId=claim.Id,SpanId=claim.Span.Id,Explanation=text,RecordIds=new[]{record.Id}};
        static string Root(KnownRecord r)=>string.IsNullOrEmpty(r.ProvenanceKey)?r.RootId??r.Id:r.ProvenanceKey;
        /// <summary>Mandatory ballot on available evidence, never a guilt probability. A stable saved random state breaks equal support.
        /// Passing the same state and knowledge reproduces the same ballot; persist Choice and NextRandomState with the vote.</summary>
        public NpcVoteDecision ChooseVote(IActorKnowledgeQuery query,string[] eligibleCandidates,uint savedRandomState,NpcHeardClaim[] heardClaims=null)
        {
            var candidates=(eligibleCandidates??Array.Empty<string>()).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            // Every admitted participant still votes in a run-off, even when only other people are nominated.
            // Admission belongs to TrialDirector; the candidate pool is not the voter roster.
            if(query==null||candidates.Length==0)throw new ArgumentException("An owner-bound voter and at least one candidate are required");_=new StableId(query.OwnerId);foreach(var id in candidates)_=new StableId(id);
            var own=Records(query);var heard=(heardClaims??Array.Empty<NpcHeardClaim>()).Where(c=>c!=null&&c.ReceiverId==query.OwnerId&&c.LoopId==query.LoopId&&c.Span!=null&&c.Span.SubjectId!=null&&c.Review!="Contradicted"&&c.Review!="UnsupportedScope"&&c.Review!="CorrectedBySpeaker").ToArray();
            var tiers=new Dictionary<string,int>();var bases=new Dictionary<string,string[]>();var explanations=new Dictionary<string,string>();
            foreach(string candidate in candidates){
                bool Countered(string place,long from,long to)=>!string.IsNullOrEmpty(place)&&own.Any(r=>r.Direct&&r.IdentityConfirmed&&r.SubjectId==candidate&&r.Predicate=="AtPlace"&&r.Value!=place&&r.FromTick<=from&&r.ToTick>=to);
                var causality=own.Where(r=>r.SubjectId==candidate&&r.Predicate=="CausedOutcome"&&r.IdentityConfirmed&&(r.Direct||!Countered(r.PlaceId,r.FromTick,r.ToTick))).ToArray();
                var direct=causality.Where(r=>r.Direct).ToArray();var contact=own.Where(r=>r.SubjectId==candidate&&r.Predicate=="UsedObject"&&r.IdentityConfirmed&&r.Direct).ToArray();
                var publicCausality=heard.Where(c=>c.Span.SubjectId==candidate&&c.Span.Predicate=="CausedOutcome"&&c.Review=="SupportedWithinScope"&&!Countered(c.Span.PlaceId,c.Span.FromTick,c.Span.ToTick)).ToArray();
                int roots=causality.Select(Root).Distinct().Count();
                if(direct.Length>0){tiers[candidate]=4;bases[candidate]=causality.Select(r=>r.Id).ToArray();explanations[candidate]="직접 관측한 행동·결과 연결에 근거한 지목입니다. 관측 밖 의도는 별도 판단이 필요합니다.";}
                else if(roots>=2){tiers[candidate]=3;bases[candidate]=causality.Select(r=>r.Id).ToArray();explanations[candidate]="서로 다른 근원에서 받은 행동·결과 진술에 근거합니다. 전언을 직접 관측으로 바꾸지는 않습니다.";}
                else if(causality.Length>0||publicCausality.Length>0){tiers[candidate]=2;bases[candidate]=causality.Select(r=>r.Id).Concat(publicCausality.Select(c=>c.Id)).ToArray();explanations[candidate]="받은 인과 진술을 조건부로 받아들인 지목입니다. 독립 확인이 부족합니다.";}
                else if(contact.Length>0){tiers[candidate]=1;bases[candidate]=contact.Select(r=>r.Id).ToArray();explanations[candidate]="실제 사용 관측만 있는 불완전한 지목입니다. 소지·사용만으로 사망 원인이나 고의를 확정할 수 없습니다.";}
                else{tiers[candidate]=0;bases[candidate]=Array.Empty<string>();explanations[candidate]="지목을 뒷받침할 인과 자료가 없습니다. 기권 불가 규칙에 따른 불확실한 선택이며 유죄 단정이 아닙니다.";}
            }
            int strongest=tiers.Values.Max();var equal=candidates.Where(c=>tiers[c]==strongest).ToArray();uint next=savedRandomState==0?1:savedRandomState;int draws=0,index=0;
            if(equal.Length>1){next^=next<<13;next^=next>>17;next^=next<<5;draws=1;index=(int)(next%(uint)equal.Length);}
            string choice=equal[index];return new NpcVoteDecision{VoterId=query.OwnerId,Choice=choice,Explanation=explanations[choice],Uncertain=true,NextRandomState=next,DrawCount=draws,Basis=bases[choice],EquallySupportedCandidates=equal};
        }
    }
}
