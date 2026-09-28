using System;
using System.Linq;
using System.Collections.Generic;
using BASSLINE.Core;
namespace BASSLINE.Investigation
{
    [Serializable] public sealed class ClaimSpan
    {
        public string Id,Predicate,SubjectId,Value,PlaceId,Quantifier="Particular";public long FromTick,ToTick;
        public ClaimSpan Copy()=>(ClaimSpan)MemberwiseClone();
    }
    [Serializable] public sealed class ClaimRecord
    {
        public string Id,OwnerId,LoopId,Text;public int Revision=1;public ClaimSpan[] Spans=Array.Empty<ClaimSpan>();
        public ClaimRecord Copy()=>new ClaimRecord{Id=Id,OwnerId=OwnerId,LoopId=LoopId,Text=Text,Revision=Revision,Spans=Spans.Select(x=>x.Copy()).ToArray()};
    }
    [Serializable] public sealed class LogicResult
    {
        public string RequestId,ResultType,ReasonCode,Explanation;public long KnowledgeRevision;public int AReadCount;
        public string[] ProvenScope=Array.Empty<string>(),UnsupportedSpanIds=Array.Empty<string>(),MissingPremises=Array.Empty<string>(),CitedRefs=Array.Empty<string>(),RootGroups=Array.Empty<string>();
    }
    public sealed class LogicResolver
    {
        static readonly HashSet<string> predicates=new HashSet<string>{"AtPlace","PassedDoor","HeldObject","PlacedObject","UsedObject","DoorState","DoorMotion","DoorAttempt","HeardSound","SaidStatement","ReceivedInformation","CausedOutcome","SurfacePattern","ContactPattern"};
        public LogicResult Resolve(IActorKnowledgeQuery query,string requestId,string ruleId,ClaimRecord claim,string spanId,IEnumerable<string> references)
        {
            if(query==null)throw new ArgumentNullException(nameof(query));
            var result=new LogicResult{RequestId=requestId,KnowledgeRevision=query.Revision,ResultType="NeedPremise",ReasonCode="UnsupportedPremise"};
            if(!Enumerable.Range(1,10).Select(x=>"LR"+x.ToString("00")).Contains(ruleId))return Reason(result,"NeedPremise","UnknownRule","등록되지 않은 추론 규칙입니다.");
            if(claim==null||claim.LoopId!=query.LoopId)return Reason(result,"NeedPremise","WrongLoop","현재 회차의 주장과 자료만 연결할 수 있습니다.");
            var span=claim.Spans.SingleOrDefault(x=>x.Id==spanId);
            if(span==null||!predicates.Contains(span.Predicate)||span.FromTick<0||span.ToTick<=span.FromTick)return Reason(result,"NeedPremise","UnsupportedPredicate","등록된 명제와 유효한 시간 범위를 선택하세요.");
            var refs=(references??Array.Empty<string>()).Distinct().ToArray();var records=refs.Select(query.Find).ToArray();
            if(records.Any(x=>x==null))return Reason(result,"NeedPremise","AccessDenied","현재 인물이 받지 않은 자료입니다.");
            if(records.Any(x=>x.LoopId!=query.LoopId))return Reason(result,"NeedPremise","WrongLoop","이전 회차 자료는 현재 증거가 아닙니다.");
            if(records.Any(RetiredSurveillance.IsRecord))return Reason(result,"NeedPremise","RetiredSurveillance","현재 세계에 존재하지 않는 촬영 자료는 근거로 사용할 수 없습니다.");
            result.CitedRefs=refs;result.RootGroups=records.Select(EvidenceOrigins.IndependenceKey).Distinct().ToArray();
            if(records.Length==0)return Reason(result,"NeedPremise","MissingEvidence","연결할 자료가 없습니다.");
            if(records.All(r=>r.Predicate=="PassedDoor")&&new[]{"UsedObject","CausedOutcome"}.Contains(span.Predicate))return Reason(result,"NeedPremise","PassageIsNotUse","문을 통과한 사실만으로 물건의 사용이나 사건의 원인을 확정할 수 없습니다.");
            if(records.All(r=>PhysicalEvidenceScope.IsDoorObservation(r.Predicate))&&new[]{"PassedDoor","UsedObject","CausedOutcome"}.Contains(span.Predicate))return Reason(result,"NeedPremise","DoorStateIsNotAnActor","문이 열리거나 닫힌 모습, 열어 보려 한 결과만으로 통과자·조작자·사건의 원인을 확정할 수 없습니다.");
            if(PhysicalEvidenceScope.IsDoorObservation(span.Predicate)&&span.Quantifier!="Particular")return Reason(result,"LimitScope","DoorObservationScope","직접 확인한 문과 시각으로 범위를 좁혀 주세요. 다른 시각에도 같은 상태였다는 뜻은 아닙니다.");
            if(records.All(PhysicalEvidenceScope.IsSurfaceState)&&new[]{"UsedObject","CausedOutcome"}.Contains(span.Predicate))return Reason(result,"NeedPremise","SurfaceIsNotAnAction","표면의 무늬만으로 사용한 사람·사용 시각·결과를 확정할 수 없습니다.");
            if(PhysicalEvidenceScope.IsSurfaceState(span.Predicate)&&span.Quantifier!="Particular")return Reason(result,"LimitScope","SurfaceScope","직접 살펴본 표면과 관찰 시점으로 범위를 좁혀 주세요. 같은 무늬의 다른 물건이 없다는 뜻은 아닙니다.");
            if(ruleId=="LR07"&&span.Predicate=="CausedOutcome"&&(span.Value??"").StartsWith("ACT:",StringComparison.Ordinal)){
                if(span.Quantifier!="Particular"){result.UnsupportedSpanIds=new[]{span.Id};return Reason(result,"LimitScope","CausalScope","하나의 작동과 대상·시간 범위로 좁혀 주세요.");}
                if(CausalEvidence.TryLinkWitness(records,span.SubjectId,span.Value,span.FromTick,span.ToTick,out var conditional,out var witnessMissing)){
                    if(conditional)return Reason(result,"Conditional","WitnessActivationTestimony","같은 목격자의 경고·실행·연속 관측 증언을 받아들일 경우 이 결과가 연결됩니다. 전해 들은 내용은 직접 확인한 사실로 바뀌지 않습니다.");
                    result.ProvenScope=new[]{span.Id};return Reason(result,"Support","WitnessActivationChain","직접 읽은 위험 안내와 공개 규칙, 경고 뒤의 동작 유지, 같은 대상까지 이어진 연속 목격이 이 범위에서 연결됩니다. 사적인 동기는 별도입니다.");
                }
                if(CausalEvidence.TryLinkActionJournal(records,span.SubjectId,span.Value,span.FromTick,span.ToTick,out var journalConditional,out var journalMissing)){
                    if(journalConditional)return Reason(result,"Conditional","WitnessAndLocalJournal","행동에 관한 증언을 받아들일 경우, 직접 읽은 장치 기록의 결과와 연결됩니다. 기록판만으로 조작자를 확인한 것은 아닙니다.");
                    result.ProvenScope=new[]{span.Id};return Reason(result,"Support","ObservedActionAndLocalJournal","직접 본 위험 안내·동작 유지·실행을 같은 작동 번호의 장치 결과와 연결했습니다. 사적인 동기는 별도입니다.");
                }
                return Reason(result,"NeedPremise","MissingActivationPremise",records.Any(r=>r.Kind=="DeviceLog")?journalMissing:witnessMissing);
            }
            bool SameSubject(KnownRecord r)=>r.SubjectId==span.SubjectId;
            bool Overlaps(KnownRecord r)=>r.FromTick<span.ToTick&&span.FromTick<r.ToTick;
            bool Covers(KnownRecord r)=>r.FromTick<=span.FromTick&&r.ToTick>=span.ToTick;
            var relevant=records.Where(SameSubject).ToArray();
            if(relevant.Length==0)return Reason(result,"Irrelevant","Irrelevant","선택한 자료는 이 대상에 관한 관측이 아닙니다.");
            if(ruleId=="LR09"){
                var same=relevant.Where(r=>r.Predicate==span.Predicate&&r.Value==span.Value&&Overlaps(r)).ToArray();
                if(same.Select(EvidenceOrigins.IndependenceKey).Distinct().Count()<2)return Reason(result,"NeedPremise","SameRoot","같은 근원의 사본·전언은 독립 확인이 아닙니다.");
                return Reason(result,"Support","IndependentRoots","서로 다른 관측 근원이 있습니다. 근원 수를 진실 확률로 바꾸지 않습니다.");
            }
            if(ruleId=="LR05"){
                if(!relevant.Any(r=>r.Kind=="Statement"))return Reason(result,"Irrelevant","NotHearsay","선택한 자료에 전언이 없습니다.");
                if(span.Predicate!="SaidStatement"&&span.Predicate!="ReceivedInformation")return Reason(result,"Conditional","HearsayOnly","전언을 받았다는 사실과 전언 내용의 사실 여부는 별도입니다.");
                result.ProvenScope=new[]{span.Id};return Reason(result,"Support","DeliveryReceipt","해당 전언의 수신만 지지합니다.");
            }
            if(ruleId=="LR10"||relevant.All(r=>!r.Direct))return Reason(result,"Conditional","ConditionalPremise","이 진술을 받아들일 경우의 결론입니다. 직접 확인으로 승격하지 않습니다.");
            var direct=relevant.Where(r=>r.Direct).ToArray();
            if(ruleId=="LR02"&&span.Quantifier!="Particular"){
                result.UnsupportedSpanIds=new[]{span.Id};return Reason(result,"LimitScope","SensorScope","이 기록의 문·관측 구역 밖 출입은 확인하지 못합니다.");
            }
            if(ruleId=="LR08"&&direct.Any(r=>r.Predicate==span.Predicate&&r.FromTick>=span.ToTick)){
                result.UnsupportedSpanIds=new[]{span.Id};return Reason(result,"LimitScope","LaterState","나중 상태는 과거 상태를 입증하지 않습니다. 과거는 미확인입니다.");
            }
            if(ruleId=="LR04"&&span.Predicate=="UsedObject"&&direct.Any(r=>r.Predicate=="HeldObject"))return Reason(result,"NeedPremise","MissingUse","소지는 실행이나 고의를 입증하지 않습니다.");
            if(ruleId=="LR07"&&!direct.Any(r=>r.Predicate=="CausedOutcome"&&r.Value==span.Value&&r.IdentityConfirmed&&Covers(r)))return Reason(result,"NeedPremise","MissingCausality","행위·공개된 결과 규칙·대상·시각을 잇는 관측이 필요합니다.");
            if(span.Predicate=="AtPlace"&&span.Quantifier=="Particular"){
                var located=direct.Where(r=>r.Predicate=="AtPlace"&&r.IdentityConfirmed&&r.Value==r.PlaceId&&Overlaps(r)).ToArray();
                if(located.Any(a=>located.Any(b=>a.Value!=b.Value&&Math.Max(span.FromTick,Math.Max(a.FromTick,b.FromTick))<Math.Min(span.ToTick,Math.Min(a.ToTick,b.ToTick)))))return Reason(result,"NeedPremise","ConflictingPresence","같은 인물이 같은 시간에 다른 장소에 있었다는 자료가 겹칩니다. 충돌하는 관측을 먼저 확인해야 합니다.");
                var otherPlaces=direct.Where(r=>r.Predicate=="AtPlace"&&r.IdentityConfirmed&&r.Value!=span.Value&&r.Value==r.PlaceId).Select(r=>r.Value).Distinct().ToArray();
                bool here=PresenceEvidence.Covers(direct,span.SubjectId,span.Value,span.FromTick,span.ToTick,out _);
                bool elsewhere=otherPlaces.Any(place=>PresenceEvidence.Covers(direct,span.SubjectId,place,span.FromTick,span.ToTick,out _));
                if(ruleId=="LR03"&&elsewhere){result.UnsupportedSpanIds=new[]{span.Id};return Reason(result,"Contradict","ContinuousPresenceAlibi","선택한 시간 전체에 다른 장소에서 그 인물을 직접 확인한 자료가 끊김 없이 이어집니다.");}
                if(here){result.ProvenScope=new[]{span.Id};return Reason(result,"Support","ContinuousPresence","선택한 장소와 시간 범위를 직접 관측한 자료가 빈 구간 없이 덮습니다. 그 밖의 행동이나 시각은 확정하지 않습니다.");}
            }
            var matches=direct.Where(r=>r.Predicate==span.Predicate&&r.Value==span.Value&&Overlaps(r)).ToArray();
            if((ruleId=="LR03"&&span.Predicate=="AtPlace"||ruleId=="LR06"&&span.Quantifier!="Particular")&&direct.Any(r=>r.Predicate==span.Predicate&&r.Value!=span.Value&&r.IdentityConfirmed&&Covers(r))){
                result.UnsupportedSpanIds=new[]{span.Id};return Reason(result,"Contradict","ScopedCounterexample","신원과 겹친 시간 범위가 확인된 이 부분에 반례가 있습니다. 다른 주장은 유지합니다.");
            }
            if(matches.Any(Covers)&&span.Quantifier=="Particular"){
                if(!PhysicalEvidenceScope.IsSurfaceState(span.Predicate)&&!matches.Any(r=>r.IdentityConfirmed&&Covers(r))&&span.SubjectId.StartsWith("CH_",StringComparison.Ordinal))return Reason(result,"NeedPremise","UnknownIdentity","선택한 시간 범위에 그 인물을 식별할 근거가 부족합니다.");
                result.ProvenScope=new[]{span.Id};return Reason(result,"Support","WithinScope","선택한 관측의 대상·시간·범위 안에서만 지지됩니다.");
            }
            if(matches.Length>0||span.Quantifier!="Particular"){
                result.UnsupportedSpanIds=new[]{span.Id};return Reason(result,"LimitScope","WrongTimeOrScope","관측 밖 시간 또는 ‘유일·항상’의 범위는 확인되지 않았습니다.");
            }
            return Reason(result,"NeedPremise","UnsupportedPremise","반대 사실을 만들 수 없습니다. 이 명제에 해당하는 관측이 더 필요합니다.");
        }
        static LogicResult Reason(LogicResult r,string type,string code,string text){r.ResultType=type;r.ReasonCode=code;r.Explanation=text;if(type=="NeedPremise")r.MissingPremises=new[]{code};return r;}
    }
}

