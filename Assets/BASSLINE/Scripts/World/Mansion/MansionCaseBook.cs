using System;
using System.Linq;
using BASSLINE.Core;

namespace BASSLINE.World.Mansion
{
    [Serializable] public sealed class MansionOutcomeEntry
    {
        public string CaseId,ActorId,VictimId,ReleaseReason="";
        public long ReservedTick=-1,CauseTick=-1,CauseSequence,DueTick=-1,ResultTick=-1,ReleasedTick=-1;
        public bool Reserved;
        public MansionOutcomeEntry Copy()=>(MansionOutcomeEntry)MemberwiseClone();
    }
    [Serializable] public sealed class MansionCaseBookSnapshot
    {
        public int Version=1,Loop,Chapter,StartingResidents;
        public long ChapterStartTick,SealedThroughTick=-1;
        public string AdjudicatedCaseId="";
        public MansionOutcomeEntry[] Entries=Array.Empty<MansionOutcomeEntry>();
        public MansionCaseBookSnapshot Copy(){var c=(MansionCaseBookSnapshot)MemberwiseClone();c.Entries=Entries.Select(x=>x.Copy()).ToArray();return c;}
    }

    // Chapter-wide outcome authority. It exposes no player view and never selects an actor's plan.
    // All result callbacks for a tick are admitted before Seal chooses the first result's cause order.
    public sealed class MansionCaseBook
    {
        MansionCaseBookSnapshot state;
        public MansionCaseBook(int loop,int chapter,long startTick,int startingResidents)
        {
            state=new MansionCaseBookSnapshot{Loop=loop,Chapter=chapter,ChapterStartTick=startTick,StartingResidents=startingResidents};Validate(state,long.MaxValue);
        }
        public int Capacity=>state.StartingResidents>=7?2:state.StartingResidents>=4?1:0;
        public int Used=>state.Entries.Count(e=>e.Reserved||e.ResultTick>=0);
        public bool HasPendingOutcome=>state.Entries.Any(e=>e.Reserved);
        public bool HasCriticalRisk(string actor)=>state.Entries.Any(e=>e.VictimId==actor&&e.Reserved&&e.CauseTick>=0);
        public bool HasResolvedRisk(string actor)=>state.Entries.Any(e=>e.VictimId==actor&&e.ReleaseReason=="RiskResolved");
        public string AdjudicatedCaseId=>state.AdjudicatedCaseId;
        public string AdjudicatedActorId=>state.Entries.FirstOrDefault(e=>e.CaseId==state.AdjudicatedCaseId)?.ActorId??"";
        public MansionOutcomeEntry Find(string id)=>state.Entries.FirstOrDefault(e=>e.CaseId==id)?.Copy();
        MansionOutcomeEntry Entry(string id)=>state.Entries.Single(e=>e.CaseId==id);
        public string Register(string id,string actor,string victim)
        {
            foreach(string value in new[]{id,actor,victim})_=new StableId(value);
            if(actor==victim)return "ParticipantUnavailable";
            var old=state.Entries.FirstOrDefault(e=>e.CaseId==id);
            if(old!=null)return old.ActorId==actor&&old.VictimId==victim?"AlreadyRegistered":"ConflictingCase";
            state.Entries=state.Entries.Concat(new[]{new MansionOutcomeEntry{CaseId=id,ActorId=actor,VictimId=victim}}).ToArray();return "Registered";
        }
        public string Reserve(string id,long tick)
        {
            var e=Entry(id);if(tick<state.ChapterStartTick||tick<state.SealedThroughTick||tick<e.ReservedTick)throw new ArgumentOutOfRangeException(nameof(tick));
            if(e.Reserved)return "Reserved";
            if(e.CauseTick>=0||e.ResultTick>=0||e.ReleasedTick>=0)return "ClosedPlan";
            if(Used>=Capacity)return "FatalityCapacityUnavailable";
            var committed=state.Entries.Where(x=>x.CaseId!=id&&(x.Reserved||x.CauseTick>=0&&x.ReleaseReason!="RiskResolved")).ToArray();
            if(committed.Any(x=>x.VictimId==e.VictimId))return "VictimAlreadyReserved";
            // Exclude incompatible causal roles before contact commits. Never undo a real death.
            if(committed.Any(x=>x.ActorId==e.VictimId||x.VictimId==e.ActorId))return "UnsupportedCausalCombination";
            e.Reserved=true;e.ReservedTick=tick;return "Reserved";
        }
        public void CommitCause(string id,long tick,long stableSequence,long dueTick)
        {
            var e=Entry(id);
            if(!e.Reserved||e.CauseTick>=0||tick<e.ReservedTick||tick<state.SealedThroughTick||stableSequence<=0||dueTick<=tick||state.Entries.Any(x=>x.CauseSequence==stableSequence))throw new InvalidOperationException("Invalid reserved cause");
            e.CauseTick=tick;e.CauseSequence=stableSequence;e.DueTick=dueTick;
        }
        public void CommitResult(string id,long tick)
        {
            var e=Entry(id);
            if(!e.Reserved||e.CauseTick<0||e.ResultTick>=0||tick<e.DueTick||tick<=state.SealedThroughTick)throw new InvalidOperationException("Invalid or late outcome callback");
            e.ResultTick=tick;e.Reserved=false;e.ReleasedTick=tick;e.ReleaseReason="ResultCommitted";
        }
        public void Release(string id,long tick,bool riskResolved=false)
        {
            var e=Entry(id);if(e.ResultTick>=0)return;
            if(!e.Reserved)return;
            if(tick<e.ReservedTick||tick<e.CauseTick||tick<state.SealedThroughTick||riskResolved!=(e.CauseTick>=0))throw new InvalidOperationException("A committed risk requires actual resolution");
            e.Reserved=false;e.ReleasedTick=tick;e.ReleaseReason=riskResolved?"RiskResolved":"Withdrawn";
        }
        public void Seal(long tick)
        {
            if(tick<state.SealedThroughTick)throw new InvalidOperationException("Cannot rewind outcome boundary");
            state.SealedThroughTick=tick;
            if(state.AdjudicatedCaseId!="")return;
            var first=state.Entries.Where(e=>e.ResultTick>=0&&e.ResultTick<=tick).OrderBy(e=>e.ResultTick).ThenBy(e=>e.CauseSequence).FirstOrDefault();
            if(first!=null)state.AdjudicatedCaseId=first.CaseId;
        }
        public MansionCaseBookSnapshot Capture()=>state.Copy();
        public static MansionCaseBook Restore(MansionCaseBookSnapshot s,long tick)
        {
            Validate(s,tick);var book=new MansionCaseBook(s.Loop,s.Chapter,s.ChapterStartTick,s.StartingResidents);book.state=s.Copy();return book;
        }
        public static void Validate(MansionCaseBookSnapshot s,long tick)
        {
            if(s==null||s.Version!=1||s.Loop<1||s.Chapter<1||s.StartingResidents<0||s.StartingResidents>18||s.ChapterStartTick<0||s.ChapterStartTick>tick||s.SealedThroughTick< -1||s.SealedThroughTick>tick||s.Entries==null||s.AdjudicatedCaseId==null)throw new ArgumentException("Invalid chapter outcome book");
            int cap=s.StartingResidents>=7?2:s.StartingResidents>=4?1:0;
            if(s.Entries.Any(e=>e==null)||s.Entries.Select(e=>e.CaseId).Distinct().Count()!=s.Entries.Length||s.Entries.Count(e=>e.Reserved||e.ResultTick>=0)>cap)throw new ArgumentException("Invalid shared outcome budget");
            var caused=s.Entries.Where(e=>e.CauseTick>=0).ToArray();
            if(caused.Select(e=>e.CauseSequence).Distinct().Count()!=caused.Length)throw new ArgumentException("Duplicate stable cause sequence");
            foreach(var e in s.Entries){
                foreach(string id in new[]{e.CaseId,e.ActorId,e.VictimId})_=new StableId(id);
                if(e.ActorId==e.VictimId||e.ReservedTick< -1||e.CauseTick< -1||e.DueTick< -1||e.ResultTick< -1||e.ReleasedTick< -1||e.ReleaseReason==null||new[]{e.ReservedTick,e.CauseTick,e.ResultTick,e.ReleasedTick}.Any(t=>t>tick))throw new ArgumentException("Invalid outcome entry");
                if(e.ReservedTick>=0&&e.ReservedTick<s.ChapterStartTick||e.CauseTick>=0&&(e.ReservedTick<0||e.CauseTick<e.ReservedTick||e.CauseSequence<=0||e.DueTick<=e.CauseTick)||e.CauseTick<0&&(e.CauseSequence!=0||e.DueTick!=-1)||e.ResultTick>=0&&(e.CauseTick<0||e.ResultTick<e.DueTick||e.Reserved||e.ReleaseReason!="ResultCommitted"||e.ReleasedTick!=e.ResultTick))throw new ArgumentException("Broken outcome causality");
                if(e.Reserved&&(e.ReservedTick<0||e.ReleasedTick>=0||e.ReleaseReason!="")||!e.Reserved&&e.ReservedTick>=0&&e.ReleasedTick<e.ReservedTick||e.CauseTick>=0&&!e.Reserved&&e.ResultTick<0&&e.ReleaseReason!="RiskResolved")throw new ArgumentException("Invalid reservation lifecycle");
                if(!new[]{"","Withdrawn","RiskResolved","ResultCommitted"}.Contains(e.ReleaseReason)||e.ReservedTick<0&&(e.ReleasedTick>=0||e.ReleaseReason!="")||e.ReleaseReason=="Withdrawn"&&e.CauseTick>=0||e.ReleaseReason=="RiskResolved"&&(e.CauseTick<0||e.ReleasedTick<e.CauseTick)||e.ReleaseReason=="ResultCommitted"&&e.ResultTick<0||e.ReleasedTick>=0&&e.ReleaseReason=="")throw new ArgumentException("Invalid risk release");
            }
            var incompatible=s.Entries.Where(e=>e.Reserved||e.CauseTick>=0&&e.ReleaseReason!="RiskResolved").ToArray();
            if(incompatible.Any(e=>incompatible.Any(x=>x.CaseId!=e.CaseId&&(x.VictimId==e.VictimId||x.ActorId==e.VictimId||x.VictimId==e.ActorId))))throw new ArgumentException("Incompatible causal roles");
            var first=s.Entries.Where(e=>e.ResultTick>=0&&e.ResultTick<=s.SealedThroughTick).OrderBy(e=>e.ResultTick).ThenBy(e=>e.CauseSequence).FirstOrDefault();
            if(s.AdjudicatedCaseId!=(first?.CaseId??""))throw new ArgumentException("Adjudication is not the earliest sealed result");
        }
    }
}
