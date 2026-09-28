using System;
using System.Linq;

namespace BASSLINE.World.Mansion
{
    public sealed partial class MansionWorld
    {
        public MansionCaseBook CaseBook {get;private set;}
        // Current registered contact grammar has an immediately visible critical interval.
        // Future latent hazards must author a separate condition transition before using this path.
        public string PhysicalBand(string actor)
        {
            var resident=Resident(actor);if(!resident.Alive)return "Dead";
            if(CaseBook.HasCriticalRisk(actor))return "Critical";
            return CaseBook.HasResolvedRisk(actor)?"Injured":"Normal";
        }
        public bool CanAct(string actor)=>residents.TryGetValue(actor,out var r)&&r.Alive&&r.Present&&PhysicalBand(actor)!="Critical";
        public void StopResidentActivity(string actor)
        {
            var r=Resident(actor);r.Phase=r.Alive?"Idle":"Inactive";r.Activity="Rest";r.ActivityTicks=0;r.Path=Array.Empty<string>();r.PathCursor=0;r.Destination="";
            r.QueueDoor="";r.Recovering=false;r.RecoveryTicks=0;r.StuckTicks=0;
            foreach(var door in Doors){door.Queue=door.Queue.Where(id=>id!=actor).ToArray();if(door.Holder==actor){door.Holder="";door.GrantedTick=0;}}
            if(actor=="CH_01")state.PlayerVelocity=default;
        }
        bool mayImportLegacyCase;
        void InitializeCaseBook(MansionState initial)
        {
            mayImportLegacyCase=initial.Cases==null;
            CaseBook=initial.Cases==null?new MansionCaseBook(initial.Loop,initial.Chapter,initial.Chapter==1?0:initial.Tick,initial.Residents.Count(r=>r.Alive&&r.Present)):MansionCaseBook.Restore(initial.Cases,initial.Tick);
        }
        public string RegisterCase(BASSLINE.Core.MansionCaseSettings settings)
        {
            if(settings.ChapterStartingResidents!=CaseBook.Capture().StartingResidents)return "ChapterPopulationMismatch";
            return CaseBook.Register(settings.Id,settings.ActorId,settings.TargetId);
        }
        public bool ReserveCaseOutcome(string id)
        {
            var e=CaseBook.Find(id);if(e==null)return false;
            if(e.Reserved)return true;
            if(!Resident(e.ActorId).Alive||!Resident(e.ActorId).Present||!Resident(e.VictimId).Alive||!Resident(e.VictimId).Present||CaseBook.Reserve(id,Tick)!="Reserved")return false;
            Emit("IncidentReservationAcquired",e.ActorId,id,e.VictimId);return true;
        }
        public void ReleaseCaseOutcome(string id,bool riskResolved=false)
        {
            var e=CaseBook.Find(id);if(e==null||!e.Reserved)return;CaseBook.Release(id,Tick,riskResolved);
            Emit("IncidentReservationReleased",e.ActorId,id,riskResolved?"RiskResolved":"");
        }
        public long CommitCaseCause(string id,long dueTick)
        {
            var e=CaseBook.Find(id);long sequence=state.Sequence+1;
            CaseBook.CommitCause(id,Tick,sequence,dueTick);Emit("IncidentCauseCommitted",e.ActorId,e.VictimId,id);
            Drop(e.VictimId);StopResidentActivity(e.VictimId);Resident(e.VictimId).Phase="Incapacitated";Resident(e.VictimId).Activity="NeedsHelp";
            return sequence;
        }
        public long ResolveCaseRisk(string id,string helper)
        {
            var e=CaseBook.Find(id);
            if(Paused||e==null||!e.Reserved||e.CauseTick<0||e.ResultTick>=0||Tick>=e.DueTick||helper==e.VictimId||!CanAct(helper)||!Resident(e.VictimId).Alive||!Resident(e.VictimId).Present)throw new InvalidOperationException("No pending risk can be resolved");
            ReleaseCaseOutcome(id,true);StopResidentActivity(e.VictimId);
            Emit("IncidentRiskResolved",helper,e.VictimId,id);return state.Sequence;
        }
        public long CommitCaseResult(string id)
        {
            var e=CaseBook.Find(id);if(e==null)throw new ArgumentException("Unknown case");
            var victim=Resident(e.VictimId);if(!victim.Alive||!victim.Present)throw new InvalidOperationException("Reserved victim is unavailable");
            // Reject a late/duplicate callback before changing the physical resident state.
            CaseBook.CommitResult(id,Tick);
            Drop(e.VictimId);victim.Alive=false;victim.Phase="Inactive";victim.Activity="";victim.Path=Array.Empty<string>();victim.PathCursor=0;victim.Destination="";
            foreach(var door in Doors){door.Queue=door.Queue.Where(x=>x!=e.VictimId).ToArray();if(door.Holder==e.VictimId)door.Holder="";}
            Emit("IncidentResultCommitted",e.ActorId,e.VictimId,id);return state.Sequence;
        }
        public void SealCaseOutcomes()=>CaseBook.Seal(Tick);
        internal void CheckRestoredCase(MansionIncidentSnapshot incident)
        {
            if(incident.Settings==null)return;
            var settings=incident.Settings;var existing=CaseBook.Find(settings.Id);
            if(existing==null&&mayImportLegacyCase){
                var acquired=Events.LastOrDefault(e=>e.Type=="IncidentReservationAcquired"&&e.Target==settings.Id&&e.Tick>=settings.ChapterStartTick);
                var released=Events.LastOrDefault(e=>e.Type=="IncidentReservationReleased"&&e.Target==settings.Id&&e.Tick>=settings.ChapterStartTick);
                var cause=Events.FirstOrDefault(e=>"M_EVENT_"+e.Sequence==incident.CauseEvent);
                var entry=new MansionOutcomeEntry{CaseId=settings.Id,ActorId=settings.ActorId,VictimId=settings.TargetId,Reserved=incident.Reservation,
                    ReservedTick=acquired?.Tick??-1,CauseTick=incident.CauseTick,CauseSequence=cause?.Sequence??0,DueTick=incident.DueTick,ResultTick=incident.ResultTick,
                    ReleasedTick=incident.ResultTick>=0?incident.ResultTick:released?.Tick??-1,ReleaseReason=incident.ResultTick>=0?"ResultCommitted":released!=null?"Withdrawn":""};
                var upgraded=new MansionCaseBookSnapshot{Loop=Loop,Chapter=Chapter,StartingResidents=settings.ChapterStartingResidents,ChapterStartTick=settings.ChapterStartTick,
                    SealedThroughTick=incident.ResultTick>=0?Tick:Tick-1,AdjudicatedCaseId=incident.ResultTick>=0?settings.Id:"",Entries=new[]{entry}};
                var upgradedBook=MansionCaseBook.Restore(upgraded,Tick);var candidate=Capture();candidate.Cases=upgradedBook.Capture();
                ValidateCaseBook(candidate);CaseBook=upgradedBook;mayImportLegacyCase=false;existing=entry;
            }
            if(existing==null||existing.ActorId!=settings.ActorId||existing.VictimId!=settings.TargetId||existing.CauseTick!=incident.CauseTick||existing.DueTick!=incident.DueTick||existing.ResultTick!=incident.ResultTick||existing.Reserved!=incident.Reservation||existing.CauseTick>=0&&incident.CauseEvent!="M_EVENT_"+existing.CauseSequence||CaseBook.Capture().StartingResidents!=settings.ChapterStartingResidents)throw new ArgumentException("Incident differs from shared outcome book");
            if((existing.ReleaseReason=="RiskResolved")!=(incident.Stage=="RiskResolved")||incident.Stage=="RiskResolved"&&existing.ReleasedTick!=incident.RiskResolvedTick)throw new ArgumentException("Incident risk differs from shared outcome book");
        }
        static void ValidateCaseBook(MansionState s)
        {
            if(s.Cases==null)return;MansionCaseBook.Validate(s.Cases,s.Tick);
            if(s.Cases.Loop!=s.Loop||s.Cases.Chapter!=s.Chapter||s.Cases.StartingResidents<s.Residents.Count(r=>r.Alive&&r.Present))throw new ArgumentException("Outcome book belongs to another chapter");
            foreach(var e in s.Cases.Entries){
                if(!s.Residents.Any(r=>r.Id==e.ActorId)||!s.Residents.Any(r=>r.Id==e.VictimId))throw new ArgumentException("Unknown case participant");
                if(e.Reserved&&s.Residents.Any(r=>r.Id==e.VictimId&&(!r.Alive||!r.Present)))throw new ArgumentException("Reserved victim has no matching outcome");
                if(e.CauseTick>=0&&!s.Events.Any(x=>x.Type=="IncidentCauseCommitted"&&x.Sequence==e.CauseSequence&&x.Tick==e.CauseTick&&x.Actor==e.ActorId&&x.Target==e.VictimId&&x.Detail==e.CaseId))throw new ArgumentException("Case cause is missing from world history");
                if(e.ResultTick>=0&&(s.Residents.Single(r=>r.Id==e.VictimId).Alive||!s.Events.Any(x=>x.Type=="IncidentResultCommitted"&&x.Tick==e.ResultTick&&x.Actor==e.ActorId&&x.Target==e.VictimId&&x.Detail==e.CaseId)))throw new ArgumentException("Case result is missing from world history");
                if(e.ReleaseReason=="RiskResolved"&&(!s.Events.Any(x=>x.Type=="IncidentRiskResolved"&&x.Tick==e.ReleasedTick&&x.Target==e.VictimId&&x.Detail==e.CaseId)||!s.Events.Any(x=>x.Type=="IncidentReservationReleased"&&x.Tick==e.ReleasedTick&&x.Target==e.CaseId&&x.Detail=="RiskResolved")))throw new ArgumentException("Resolved risk is missing from world history");
            }
        }
    }
}
