using System;
using System.Linq;
using System.Collections.Generic;
using BASSLINE.Core;
namespace BASSLINE.Knowledge
{
    [Serializable] public sealed class MemoryReceipt {public string OwnerId;public KnownRecord Record;}
    [Serializable] public sealed class KnowledgeSnapshot {public string LoopId="K_LOOP_01";public long Sequence;public MemoryReceipt[] Memories=Array.Empty<MemoryReceipt>();}
    /// <summary>Only the trusted perception/delivery adapter writes receipts. Consumers receive an owner-bound query.</summary>
    public sealed class KnowledgeLedger
    {
        readonly HashSet<string> actors;
        readonly List<MemoryReceipt> memories=new List<MemoryReceipt>();
        readonly Dictionary<string,KnownRecord> latestDirect=new Dictionary<string,KnownRecord>();
        static string ObservationKey(string owner,string subject,string predicate)=>owner+"|"+subject+"|"+predicate;
        public KnownRecord LastDirect(string owner,string subject,string predicate)
        {
            if(!actors.Contains(owner))throw new ArgumentException("Unknown knowledge owner");
            return latestDirect.TryGetValue(ObservationKey(owner,subject,predicate),out var record)&&!RetiredSurveillance.IsRecord(record)?record.Copy():null;
        }
        public string LoopId {get;private set;}="K_LOOP_01";
        long sequence;
        public KnowledgeLedger(IEnumerable<string> actorIds,string loopId="K_LOOP_01"){_=new StableId(loopId);actors=new HashSet<string>(actorIds);LoopId=loopId;}
        public IActorKnowledgeQuery For(string owner)
        {
            if(!actors.Contains(owner))throw new ArgumentException("Unknown knowledge owner");
            return new OwnerQuery(this,owner);
        }
        public string Observe(string owner,KnownRecord record,long tick)
        {
            if(!actors.Contains(owner)||record==null||RetiredSurveillance.IsRecord(record)||record.FromTick<0||record.ToTick<=record.FromTick||record.ToTick>tick+1||!record.Position.Finite())throw new ArgumentException("Invalid observation");
            var copy=record.Copy();copy.OriginKind=record.Kind??"";copy.OriginObserver=owner;copy.Id="K_REC_"+(++sequence);copy.RootId=copy.Id;copy.LoopId=LoopId;copy.ReceivedTick=tick;copy.Parents=Array.Empty<string>();copy.Direct=true;
            memories.Add(new MemoryReceipt{OwnerId=owner,Record=copy});latestDirect[ObservationKey(owner,copy.SubjectId,copy.Predicate)]=copy;return copy.Id;
        }
        public string Deliver(string sender,string receiver,string recordId,long tick)
        {
            if(!actors.Contains(sender)||!actors.Contains(receiver)||sender==receiver)return "Unavailable";
            var original=For(sender).Find(recordId);if(original==null||RetiredSurveillance.IsRecord(original)||original.ReceivedTick>tick)return "AccessDenied";
            // Repeated delivery is not a new independent source and does not inflate knowledge.
            var old=memories.FirstOrDefault(x=>x.OwnerId==receiver&&x.Record.RootId==original.RootId);
            if(old!=null)return old.Record.Id;
            var copy=original.Copy();copy.Id="K_REC_"+(++sequence);copy.Kind="Statement";copy.Direct=false;copy.ReceivedTick=tick;copy.Parents=new[]{recordId};copy.Source=sender;
            copy.DoesNotEstablish=copy.DoesNotEstablish.Concat(new[]{"전언의 내용 자체가 세계의 사실임을 확정하지 않음"}).Distinct().ToArray();
            memories.Add(new MemoryReceipt{OwnerId=receiver,Record=copy});return copy.Id;
        }
        public KnownLocationView[] LastConfirmed(string owner)=>For(owner).Records().Where(x=>x.Direct&&x.IdentityConfirmed&&actors.Contains(x.SubjectId)&&x.Predicate=="AtPlace")
            .GroupBy(x=>x.SubjectId).Select(g=>g.OrderByDescending(x=>x.ToTick).ThenByDescending(x=>x.ReceivedTick).First())
            .Select(x=>new KnownLocationView{ActorId=x.SubjectId,PlaceId=x.PlaceId,Tick=x.ToTick-1,Position=x.Position,SourceRecordId=x.Id}).ToArray();
        public KnowledgeSnapshot Capture()=>new KnowledgeSnapshot{LoopId=LoopId,Sequence=sequence,Memories=memories.Select(x=>new MemoryReceipt{OwnerId=x.OwnerId,Record=x.Record.Copy()}).ToArray()};
        public static KnowledgeLedger Restore(KnowledgeSnapshot snapshot,IEnumerable<string> actorIds,long worldTick)
        {
            var ledger=new KnowledgeLedger(actorIds);Validate(snapshot,ledger.actors,worldTick);ledger.LoopId=snapshot.LoopId;ledger.sequence=snapshot.Sequence;
            // Old saves already contain the full immutable parent chain. Recover only
            // that recorded source metadata, never new observations about the world.
            var normalized=new Dictionary<string,KnownRecord>();
            foreach(var memory in snapshot.Memories){
                var copy=memory.Record.Copy();
                if(string.IsNullOrEmpty(copy.OriginKind)){
                    if(copy.Direct){copy.OriginKind=copy.Kind;copy.OriginObserver=memory.OwnerId;}
                    else{var parent=normalized[copy.Parents[0]];copy.OriginKind=parent.OriginKind;copy.OriginObserver=parent.OriginObserver;}
                }
                normalized.Add(copy.Id,copy);ledger.memories.Add(new MemoryReceipt{OwnerId=memory.OwnerId,Record=copy});
            }
            foreach(var receipt in ledger.memories.Where(m=>m.Record.Direct))ledger.latestDirect[ObservationKey(receipt.OwnerId,receipt.Record.SubjectId,receipt.Record.Predicate)]=receipt.Record;return ledger;
        }
        static void Validate(KnowledgeSnapshot s,HashSet<string> actors,long tick)
        {
            if(s==null||string.IsNullOrWhiteSpace(s.LoopId)||s.Memories==null||s.Sequence!=s.Memories.Length)throw new ArgumentException("Invalid knowledge snapshot");
            var seen=new Dictionary<string,KnownRecord>();long next=0;
            foreach(var m in s.Memories){var r=m.Record;
                if(r==null||!actors.Contains(m.OwnerId)||r.Id!="K_REC_"+(++next)||r.LoopId!=s.LoopId||r.FromTick<0||r.ToTick<=r.FromTick||r.ToTick>tick+1||r.ReceivedTick>tick||r.ReceivedTick<r.FromTick||!r.Position.Finite()||r.Parents==null||r.Supports==null||r.DoesNotEstablish==null)throw new ArgumentException("Invalid memory receipt");
                if(r.Direct){if(r.RootId!=r.Id||r.Parents.Length!=0)throw new ArgumentException("Invalid direct root");}
                else if(r.Parents.Length!=1||!seen.TryGetValue(r.Parents[0],out var p)||p.RootId!=r.RootId||(p.ProvenanceKey??"")!=(r.ProvenanceKey??"")||!CausalEvidence.SameBinding(p,r))throw new ArgumentException("Invalid/cyclic source chain");
                bool hasOrigin=!string.IsNullOrEmpty(r.OriginKind)||!string.IsNullOrEmpty(r.OriginObserver);
                if(hasOrigin){
                    if(r.Direct){if(r.OriginKind!=r.Kind||r.OriginObserver!=m.OwnerId)throw new ArgumentException("Changed original observation source");}
                    else{var parent=seen[r.Parents[0]];if(r.OriginKind!=(parent.OriginKind??"")||r.OriginObserver!=(parent.OriginObserver??""))throw new ArgumentException("Changed testimony origin");}
                }
                seen.Add(r.Id,r);
            }
        }
        sealed class OwnerQuery:IActorKnowledgeQuery
        {
            readonly KnowledgeLedger ledger;public string OwnerId{get;}public string LoopId=>ledger.LoopId;
            public long Revision=>ledger.memories.LongCount(x=>x.OwnerId==OwnerId);
            public OwnerQuery(KnowledgeLedger ledger,string owner){this.ledger=ledger;OwnerId=owner;}
            public KnownRecord[] Records()=>ledger.memories.Where(x=>x.OwnerId==OwnerId&&!RetiredSurveillance.IsRecord(x.Record)).Select(x=>x.Record.Copy()).ToArray();
            public KnownRecord Find(string id)=>ledger.memories.FirstOrDefault(x=>x.OwnerId==OwnerId&&x.Record.Id==id)?.Record.Copy();
        }
    }
}


