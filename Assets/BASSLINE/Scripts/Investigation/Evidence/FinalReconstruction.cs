using System;
using System.Linq;
using System.Collections.Generic;
using BASSLINE.Core;
namespace BASSLINE.Investigation
{
    [Serializable] public sealed class ReconstructionEntry
    {
        public string SubjectId="",Derivation="";
        public string Id="",Group="A",Role="Observation",ActorId="",Predicate="",Value="",PlaceId="",Text="";
        public long FromTick,ToTick;public bool Assumption;public string[] RecordIds=Array.Empty<string>();
        public ReconstructionEntry Copy(){var c=(ReconstructionEntry)MemberwiseClone();c.SubjectId=SubjectId??"";c.RecordIds=(string[])RecordIds.Clone();return c;}
    }
    [Serializable] public sealed class ReconstructionPublication
    {
        public int Revision;public string AccusedId="",SpeechId="",ClaimId="";public long KnowledgeRevision,CourtTick;
        public ReconstructionEntry[] Entries=Array.Empty<ReconstructionEntry>();public string[] Issues=Array.Empty<string>();
        public ReconstructionPublication Copy(){var c=(ReconstructionPublication)MemberwiseClone();c.Entries=Entries.Select(e=>e.Copy()).ToArray();c.Issues=(string[])Issues.Clone();return c;}
    }
    [Serializable] public sealed class FinalDefenseRecord
    {
        public string Id="",ActorId="",SpeechId="",Signature="",Text="",State="Queued";public int Revision;
        public string ClaimId="",SpanId="",Action="",RuleId="",RequestId="",ReviewState="";
        public string[] RecordIds=Array.Empty<string>();
        public FinalDefenseRecord Copy(){var c=(FinalDefenseRecord)MemberwiseClone();c.RecordIds=(string[])RecordIds.Clone();return c;}
    }
    [Serializable] public sealed class ReconstructionSnapshot
    {
        public string OwnerId="",LoopId="",Phase="Editing",AccusedId="";public int Revision,Sequence,AcceptedIncompleteRevision=-1;
        public ReconstructionEntry[] Entries=Array.Empty<ReconstructionEntry>();
        public ReconstructionPublication[] Publications=Array.Empty<ReconstructionPublication>();
        public FinalDefenseRecord[] Defenses=Array.Empty<FinalDefenseRecord>();
        public ReconstructionSnapshot Copy(){var c=(ReconstructionSnapshot)MemberwiseClone();c.Entries=Entries.Select(e=>e.Copy()).ToArray();c.Publications=Publications.Select(p=>p.Copy()).ToArray();c.Defenses=Defenses.Select(d=>d.Copy()).ToArray();return c;}
    }
    public sealed class ReconstructionAssessment
    {
        public string Status="InsufficientInformation";public string[] Issues=Array.Empty<string>();
        public Dictionary<string,string> EntryStatus=new Dictionary<string,string>();
    }
    // A pre-verdict explanation assembled from owner B. No World, authority result time,
    // actual culprit, archive replay, navigation or scene mutation is available here.
    public static class FinalReconstruction
    {
        public static readonly string[] Roles={"Observation","Cause","Outcome","Discovery","Aftermath"};
        public static string RoleLabel(string role){switch(role){case "Cause":return "원인 행동";case "Outcome":return "결과 관측";case "Discovery":return "발견";case "Aftermath":return "이후 행동";default:return "확인한 사실";}}
        public static string Add(ReconstructionSnapshot s,IActorKnowledgeQuery own,string recordId,string group)
        {
            if(s.Phase!="Editing"||s.Entries.Length>=16||!new[]{"A","B"}.Contains(group))return "Unavailable";
            var r=own.Find(recordId);if(r==null||r.LoopId!=own.LoopId||r.Kind=="ArchiveMeta"||RetiredSurveillance.IsRecord(r))return "UnknownEvidence";
            if(s.OwnerId==""){s.OwnerId=own.OwnerId;s.LoopId=own.LoopId;}
            if(s.OwnerId!=own.OwnerId||s.LoopId!=own.LoopId)return "WrongOwner";
            if(s.Entries.Any(e=>e.Group==group&&e.RecordIds.Contains(recordId)))return "AlreadyAdded";
            var entry=new ReconstructionEntry{Id="RECON_ENTRY_"+(++s.Sequence),Group=group,SubjectId=PhysicalEvidenceScope.ObservationSubject(r),ActorId=!PhysicalEvidenceScope.IsObjectState(r)&&r.IdentityConfirmed?r.SubjectId:"UNKNOWN_ACTOR",Predicate=r.Predicate,Value=r.Value,PlaceId=r.PlaceId??"",Text=r.Text,FromTick=r.FromTick,ToTick=r.ToTick,RecordIds=new[]{r.Id}};
            s.Entries=s.Entries.Concat(new[]{entry}).ToArray();Changed(s);return entry.Id;
        }
        // A connection remains an explanation with original references, never a new
        // direct observation. Only the player's own, actually collected records are read.
        public static ReconstructionEntry BuildConnection(IActorKnowledgeQuery own,string causeId,string[] allowed=null)
        {
            var cause=own.Find(causeId);
            if(cause==null||cause.CausalStage!="Cause"||cause.Predicate!="UsedObject"||cause.LoopId!=own.LoopId)return null;
            var all=own.Records().Where(r=>r.LoopId==own.LoopId&&r.Kind!="ArchiveMeta"&&!RetiredSurveillance.IsRecord(r)).ToArray();
            var ids=allowed??CausalEvidence.RelatedRecords(all,causeId);
            if(!ids.Contains(causeId)||ids.Distinct().Count()!=ids.Length)return null;
            var facts=ids.Select(own.Find).ToArray();
            if(facts.Any(r=>r==null||r.LoopId!=own.LoopId||r.Kind=="ArchiveMeta"||RetiredSurveillance.IsRecord(r)))return null;
            string value=CausalEvidence.OutcomeValue(cause.ActivationId,cause.OutcomeTarget);
            foreach(var result in facts.Where(r=>r.CausalStage=="Result"&&r.ActivationId==cause.ActivationId&&r.DeviceId==cause.DeviceId&&r.OutcomeTarget==cause.OutcomeTarget&&r.ActionDefinition==cause.ActionDefinition&&r.ActionRevision==cause.ActionRevision).OrderBy(r=>r.ToTick).ThenBy(r=>r.Id,StringComparer.Ordinal)){
                bool linked=CausalEvidence.TryLinkWitness(facts,cause.SubjectId,value,cause.FromTick,result.ToTick,out bool conditional,out _);
                if(!linked)linked=CausalEvidence.TryLinkActionJournal(facts,cause.SubjectId,value,cause.FromTick,result.ToTick,out conditional,out _);
                if(!linked)continue;
                return new ReconstructionEntry{Derivation="ObservedActionAndResult",Role="Cause",ActorId=cause.SubjectId,SubjectId=cause.SubjectId,Predicate="CausedOutcome",Value=value,PlaceId=cause.PlaceId??"",FromTick=cause.FromTick,ToTick=result.ToTick,
                    Text="행동과 결과를 연결한 설명\n"+cause.Text+"\n\n연결한 결과\n"+result.Text+"\n\n"+(conditional?"전해 들은 행동이 사실이라는 전제가 남아 있다.":"직접 확인한 행동과 같은 작동 번호의 결과를 연결했다.")+" 사적인 동기나 공식 사망 확인은 별도 자료가 필요하다.",
                    RecordIds=new[]{causeId}.Concat(ids.Where(id=>id!=causeId).OrderBy(id=>id,StringComparer.Ordinal)).ToArray()};
            }
            return null;
        }
        public static string AddConnection(ReconstructionSnapshot s,IActorKnowledgeQuery own,string causeId,string group)
        {
            if(s.Phase!="Editing"||s.Entries.Length>=16||!new[]{"A","B"}.Contains(group))return "Unavailable";
            if(s.OwnerId!=""&&(s.OwnerId!=own.OwnerId||s.LoopId!=own.LoopId))return "WrongOwner";
            var entry=BuildConnection(own,causeId);if(entry==null)return "MissingConnection";
            if(s.Entries.Any(e=>e.Group==group&&e.Derivation==entry.Derivation&&e.ActorId==entry.ActorId&&e.Value==entry.Value&&e.FromTick==entry.FromTick&&e.ToTick==entry.ToTick))return "AlreadyAdded";
            s.OwnerId=own.OwnerId;s.LoopId=own.LoopId;entry.Id="RECON_ENTRY_"+(++s.Sequence);entry.Group=group;
            s.Entries=s.Entries.Concat(new[]{entry}).ToArray();Changed(s);return entry.Id;
        }
        public static void Changed(ReconstructionSnapshot s){s.Revision++;s.AcceptedIncompleteRevision=-1;}
        public static ReconstructionAssessment Assess(ReconstructionSnapshot s,IActorKnowledgeQuery own)
        {
            var issues=new List<string>();var result=new ReconstructionAssessment();bool conflict=false,assumed=false;
            if(s.Entries.Length==0)issues.Add("설명에 쓸 자료가 없습니다.");
            foreach(var e in s.Entries){
                var facts=e.RecordIds.Select(own.Find).Where(r=>r!=null&&r.LoopId==own.LoopId&&r.Kind!="ArchiveMeta"&&!RetiredSurveillance.IsRecord(r)).ToArray();
                string status="Supported";
                bool covered=facts.Any(r=>r.Predicate==e.Predicate&&r.Value==e.Value&&r.FromTick<=e.FromTick&&r.ToTick>=e.ToTick&&(PhysicalEvidenceScope.IsObjectState(e.Predicate)?r.SubjectId==(string.IsNullOrEmpty(e.SubjectId)?r.SubjectId:e.SubjectId):e.ActorId=="UNKNOWN_ACTOR"||r.IdentityConfirmed&&r.SubjectId==e.ActorId));
                bool causal=e.Predicate=="CausedOutcome"&&(e.Value??"").StartsWith("ACT:",StringComparison.Ordinal);
                if(causal){
                    covered=CausalEvidence.TryLinkWitness(facts,e.ActorId,e.Value,e.FromTick,e.ToTick,out bool conditional,out _);
                    if(!covered)covered=CausalEvidence.TryLinkActionJournal(facts,e.ActorId,e.Value,e.FromTick,e.ToTick,out conditional,out _);
                    if(covered&&conditional){status="Partial";issues.Add(e.Id+": 행동은 전해 들은 내용입니다. 결과 기록만으로 그 목격담까지 직접 확인한 사실이 되지는 않습니다.");}
                }
                if(!covered){status="Partial";issues.Add(e.Id+": 선택한 자료가 이 인물·시간 범위 전체를 확인하지는 못합니다.");}
                if(facts.Length>0&&facts.All(r=>!r.Direct)){status="Partial";issues.Add(e.Id+": 전달받은 내용입니다. 원래 관측이 사실이라는 전제가 남아 있습니다.");}
                if(PhysicalEvidenceScope.IsSurfaceState(e.Predicate)&&new[]{"Cause","Outcome","Aftermath"}.Contains(e.Role)){
                    status="Partial";issues.Add(e.Id+": 표면을 확인한 시각은 자국이 생긴 시각이 아닙니다. 자국을 남긴 사람과 행동 순서는 별도 자료가 필요합니다.");
                }
                if(PhysicalEvidenceScope.IsDoorObservation(e.Predicate)&&new[]{"Cause","Aftermath"}.Contains(e.Role)){
                    status="Partial";issues.Add(e.Id+": 문 상태와 움직임만 확인했습니다. 조작한 사람과 사건의 연결은 별도 자료가 필요합니다.");
                }
                if(e.Assumption){assumed=true;status="Partial";issues.Add(e.Id+": 가정으로 남겨 둔 연결입니다.");}
                if(e.Role=="Cause"&&e.Predicate!="CausedOutcome"){status="Partial";issues.Add(e.Id+": "+(e.Predicate=="HeldObject"?"소지만 확인됐습니다. 사용과 결과의 연결이 필요합니다.":"행동이 결과를 일으켰다는 연결은 아직 확인되지 않았습니다."));}
                if(e.Role=="Outcome"&&(e.Value=="Collapsed"||e.Predicate=="ConfirmedDeath")){status="Partial";issues.Add(e.Id+": 관측·사망 확인 시각을 실제 발생 시각으로 단정할 수 없습니다.");}
                if(e.ActorId!="UNKNOWN_ACTOR"&&!string.IsNullOrEmpty(e.PlaceId)&&own.Records().Any(r=>r.Direct&&r.IdentityConfirmed&&r.SubjectId==e.ActorId&&r.Predicate=="AtPlace"&&!string.IsNullOrEmpty(r.PlaceId)&&r.Value==r.PlaceId&&r.Value!=e.PlaceId&&r.FromTick<=e.FromTick&&r.ToTick>=e.ToTick)){
                    status="Contested";conflict=true;issues.Add(e.Id+": 같은 인물과 시각에 다른 장소를 직접 확인한 기록이 있습니다.");
                }
                result.EntryStatus[e.Id]=status;
            }
            foreach(var group in s.Entries.GroupBy(e=>e.Group)){
                if(!group.Any(e=>e.Role=="Cause"))issues.Add("사건 "+group.Key+": 원인 행동을 아직 연결하지 않았습니다.");
                if(!group.Any(e=>e.Role=="Outcome"||e.Role=="Discovery"))issues.Add("사건 "+group.Key+": 결과나 발견 자료를 아직 연결하지 않았습니다.");
                foreach(var cause in group.Where(e=>e.Role=="Cause"))foreach(var after in group.Where(e=>e.Role=="Aftermath"))
                    if(after.ToTick<=cause.FromTick){conflict=true;result.EntryStatus[after.Id]="Contested";issues.Add(after.Id+": ‘이후 행동’으로 놓았지만 선택한 원인 행동보다 앞선 기록입니다.");}
            }
            if(s.AccusedId=="")issues.Add("이번 판단에서 책임을 물을 인물을 아직 고르지 않았습니다.");
            else if(!s.Entries.Any(e=>e.Role=="Cause"&&e.ActorId==s.AccusedId))issues.Add("지목한 인물과 원인 행동을 연결하는 자료가 없습니다.");
            result.Issues=issues.Distinct().ToArray();
            result.Status=conflict?"Contradicted":s.Entries.Length==0?"InsufficientInformation":assumed?"PossibleWithAssumption":issues.Count>0?"InsufficientInformation":"Possible";
            return result;
        }
        public static void Validate(ReconstructionSnapshot s,IActorKnowledgeQuery own,string[] participants)
        {
            if(s==null||s.Entries==null||s.Publications==null||s.Defenses==null||s.Revision<0||s.Sequence<0||s.Entries.Length>16||s.AcceptedIncompleteRevision>s.Revision||!new[]{"Editing","Presenting","Defense","Ready"}.Contains(s.Phase))throw new ArgumentException("Invalid reconstruction state");
            if(s.OwnerId!=""&&(s.OwnerId!=own.OwnerId||s.LoopId!=own.LoopId)||s.AccusedId!=""&&!participants.Contains(s.AccusedId)||s.Entries.Length>0&&s.OwnerId!=own.OwnerId||s.AcceptedIncompleteRevision< -1)throw new ArgumentException("Invalid reconstruction owner or target");
            void Entries(ReconstructionEntry[] entries){
                if(entries==null||entries.Any(e=>e==null)||entries.Select(e=>e.Id).Distinct().Count()!=entries.Length)throw new ArgumentException("Invalid reconstruction entries");
                foreach(var e in entries){
                    if(string.IsNullOrEmpty(e.Id)||!Roles.Contains(e.Role)||!new[]{"A","B"}.Contains(e.Group)||e.FromTick<0||e.ToTick<=e.FromTick||e.RecordIds==null||e.RecordIds.Length==0)throw new ArgumentException("Unknown reconstruction evidence");
                    if(!string.IsNullOrEmpty(e.Derivation)){
                        var rebuilt=BuildConnection(own,e.RecordIds[0],e.RecordIds);
                        if(e.Derivation!="ObservedActionAndResult"||rebuilt==null||e.ActorId!=rebuilt.ActorId||e.SubjectId!=rebuilt.SubjectId||e.Predicate!=rebuilt.Predicate||e.Value!=rebuilt.Value||e.PlaceId!=rebuilt.PlaceId||e.Text!=rebuilt.Text||e.FromTick!=rebuilt.FromTick||e.ToTick!=rebuilt.ToTick||!e.RecordIds.SequenceEqual(rebuilt.RecordIds))throw new ArgumentException("Reconstruction changed its causal sources");
                        continue;
                    }
                    if(e.RecordIds.Length!=1)throw new ArgumentException("An observation has one original source");
                    var fact=own.Find(e.RecordIds[0]);
                    // Only grouping, role and assumption are editable. Restoring a board must
                    // not let its saved display text manufacture a new fact or identity.
                    if(fact==null||fact.Kind=="ArchiveMeta"||fact.LoopId!=own.LoopId||(e.ActorId!=(!PhysicalEvidenceScope.IsObjectState(fact)&&fact.IdentityConfirmed?fact.SubjectId:"UNKNOWN_ACTOR")&&!(PhysicalEvidenceScope.IsObjectState(fact)&&string.IsNullOrEmpty(e.SubjectId)&&fact.IdentityConfirmed&&e.ActorId==fact.SubjectId))||!string.IsNullOrEmpty(e.SubjectId)&&e.SubjectId!=PhysicalEvidenceScope.ObservationSubject(fact)||e.Predicate!=fact.Predicate||e.Value!=fact.Value||e.PlaceId!=(fact.PlaceId??"")||e.Text!=fact.Text||e.FromTick!=fact.FromTick||e.ToTick!=fact.ToTick)throw new ArgumentException("Reconstruction changed its source fact");
                }
            }
            Entries(s.Entries);
            if(s.Publications.Any(p=>p==null)||s.Publications.Select(p=>p.Revision).Distinct().Count()!=s.Publications.Length||s.Defenses.Any(d=>d==null)||s.Defenses.Select(d=>d.Id).Distinct().Count()!=s.Defenses.Length)throw new ArgumentException("Invalid reconstruction revisions");
            foreach(var p in s.Publications){if(p.Revision<1||p.Revision>s.Revision||p.CourtTick<0||p.KnowledgeRevision<0||p.KnowledgeRevision>own.Revision||!participants.Contains(p.AccusedId)||string.IsNullOrEmpty(p.SpeechId)||string.IsNullOrEmpty(p.ClaimId)||p.Issues==null||p.Entries==null||p.Entries.Length==0)throw new ArgumentException("Invalid reconstruction publication");Entries(p.Entries);}
            foreach(var d in s.Defenses)if(!participants.Contains(d.ActorId)||!s.Publications.Any(p=>p.Revision==d.Revision&&p.AccusedId==d.ActorId&&p.ClaimId==d.ClaimId)||!new[]{"Queued","Spoken","Acknowledged"}.Contains(d.State)||d.RecordIds==null||string.IsNullOrEmpty(d.Signature)||string.IsNullOrEmpty(d.SpeechId)||string.IsNullOrEmpty(d.Text)||d.Action!=""&&!new[]{"Rebut","LimitScope"}.Contains(d.Action)||d.Action!=""&&(d.RecordIds.Length==0||!new[]{"LR01","LR03"}.Contains(d.RuleId)))throw new ArgumentException("Invalid final defense");
            if(s.Phase!="Editing"&&!s.Publications.Any(p=>p.Revision==s.Revision&&p.AccusedId==s.AccusedId)||s.Defenses.Count(d=>d.State=="Queued")>1||s.Phase=="Defense"!=s.Defenses.Any(d=>d.State=="Queued"&&d.Revision==s.Revision)||s.AcceptedIncompleteRevision>=0&&s.AcceptedIncompleteRevision!=s.Revision)throw new ArgumentException("Invalid reconstruction phase");
        }
    }
}
