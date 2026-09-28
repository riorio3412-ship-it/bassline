using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.World.Mansion
{
    public sealed class ActionJournalCoverage { public string SourceId,DeviceId,PlaceId; }
    [Serializable] public sealed class ActionJournalEntry
    {
        public string Id="",SourceId="",DeviceId="",ActivationId="",Kind="",PlaceId="";
        public long Tick,EventSequence;
        public string TargetId="",DefinitionId="",Revision="";
        public ActionJournalEntry Copy()=>(ActionJournalEntry)MemberwiseClone();
    }
    [Serializable] public sealed class ActionJournalLink
    {
        public string SourceId="",CaseId="",ActivationId="";
        public ActionJournalLink Copy()=>(ActionJournalLink)MemberwiseClone();
    }
    [Serializable] public sealed class MansionActionJournalSnapshot
    {
        public int Loop=1;public long InstalledTick,LastTick=-1,Sequence;
        public string[] Sources=Array.Empty<string>();
        public ActionJournalEntry[] Entries=Array.Empty<ActionJournalEntry>();
        public ActionJournalLink[] Links=Array.Empty<ActionJournalLink>();
        public MansionActionJournalSnapshot Copy()=>new MansionActionJournalSnapshot{Loop=Loop,InstalledTick=InstalledTick,LastTick=LastTick,Sequence=Sequence,Sources=(string[])Sources.Clone(),Entries=Entries.Select(e=>e.Copy()).ToArray(),Links=Links.Select(e=>e.Copy()).ToArray()};
    }
    // Records its coupled device's transitions NOW. This media is not a global case database.
    public sealed class MansionActionJournal
    {
        readonly ActionJournalCoverage[] definitions;
        MansionActionJournalSnapshot state;
        public MansionActionJournal(ActionJournalCoverage[] sources,int loop,long tick)
        {
            if(sources==null||loop<1||tick<0||sources.Any(s=>s==null||string.IsNullOrEmpty(s.SourceId)||string.IsNullOrEmpty(s.DeviceId)||string.IsNullOrEmpty(s.PlaceId))||sources.Select(s=>s.SourceId).Distinct().Count()!=sources.Length)throw new ArgumentException("Invalid local action journal");
            definitions=sources.Select(s=>new ActionJournalCoverage{SourceId=s.SourceId,DeviceId=s.DeviceId,PlaceId=s.PlaceId}).ToArray();
            state=new MansionActionJournalSnapshot{Loop=loop,InstalledTick=tick,Sources=sources.Select(s=>s.SourceId).ToArray()};
        }
        public void Step(MansionWorld world,MansionIncidentSnapshot[] cases,Func<string,bool> powered)
        {
            if(world.Paused||world.Tick==state.LastTick)return;
            if(world.Loop!=state.Loop||world.Tick<state.LastTick||world.Tick<state.InstalledTick)throw new InvalidOperationException("Journal clock mismatch");
            if(state.LastTick>=0&&world.Tick!=state.LastTick+1)state.Links=Array.Empty<ActionJournalLink>();
            state.LastTick=world.Tick;
            foreach(var source in definitions){
                if(!powered(source.SourceId)){state.Links=state.Links.Where(l=>l.SourceId!=source.SourceId).ToArray();continue;}
                foreach(var incident in cases.Where(c=>c.Settings!=null&&!c.Settings.ExplicitTestSession&&!c.Settings.PlayerInitiated&&c.Settings.ObjectId==source.DeviceId)){
                    foreach(var e in world.Events.Where(e=>e.Tick==world.Tick&&e.Detail==incident.Settings.Id&&new[]{"IncidentRiskNotice","IncidentRiskContinued","IncidentContactAbandoned","IncidentCauseCommitted","IncidentResultCommitted","IncidentRiskResolved"}.Contains(e.Type)).OrderBy(e=>e.Sequence)){
                        var link=state.Links.LastOrDefault(l=>l.SourceId==source.SourceId&&l.CaseId==incident.Settings.Id);
                        if(e.Type=="IncidentRiskNotice"){
                            link=new ActionJournalLink{SourceId=source.SourceId,CaseId=incident.Settings.Id,ActivationId=incident.ActivationId};
                            state.Links=state.Links.Where(l=>!(l.SourceId==source.SourceId&&l.CaseId==incident.Settings.Id)).Concat(new[]{link}).ToArray();
                        }
                        // A device switched on midway cannot reconstruct an earlier activation.
                        if(link==null)continue;
                        string kind=e.Type=="IncidentRiskNotice"?"WarningShown":e.Type=="IncidentRiskContinued"?"ContactContinued":e.Type=="IncidentContactAbandoned"?"Cancelled":e.Type=="IncidentCauseCommitted"?"Cause":e.Type=="IncidentResultCommitted"?"Result":"RiskResolved";
                        var prior=state.Entries.Where(r=>r.SourceId==source.SourceId&&r.ActivationId==link.ActivationId).ToArray();
                        if(kind=="Cause"&&!prior.Any(r=>r.Kind=="ContactContinued")||new[]{"Result","RiskResolved"}.Contains(kind)&&!prior.Any(r=>r.Kind=="Cause"))continue;
                        state.Entries=state.Entries.Concat(new[]{new ActionJournalEntry{Id="DEVICE_LOG_L"+state.Loop+"_"+(++state.Sequence),SourceId=source.SourceId,DeviceId=source.DeviceId,PlaceId=source.PlaceId,ActivationId=link.ActivationId,Kind=kind,Tick=e.Tick,EventSequence=e.Sequence,TargetId=incident.Settings.TargetId,DefinitionId=incident.Settings.Execution.Definition.Id,Revision=incident.Settings.Execution.Definition.Revision}}).ToArray();
                        if(new[]{"Cancelled","Result","RiskResolved"}.Contains(kind))state.Links=state.Links.Where(l=>l!=link).ToArray();
                    }
                }
            }
        }
        public ActionJournalEntry[] Read(string source)=>state.Entries.Where(e=>e.SourceId==source).Select(e=>e.Copy()).ToArray();
        public MansionActionJournalSnapshot Capture()=>state.Copy();
        public static MansionActionJournal Restore(MansionActionJournalSnapshot s,ActionJournalCoverage[] definitions,int loop,long tick)
        {
            if(s==null||s.Entries==null||s.Entries.Any(e=>e==null)||s.Links==null||s.Sources==null||s.Loop!=loop||s.InstalledTick<0||s.InstalledTick>tick||s.LastTick>tick||s.LastTick< -1||s.LastTick>=0&&s.LastTick<s.InstalledTick||s.Sequence!=s.Entries.LongLength||s.Entries.Select(e=>e.SourceId+"|"+e.EventSequence).Distinct().Count()!=s.Entries.Length)throw new ArgumentException("Invalid journal snapshot");
            var result=new MansionActionJournal(definitions,loop,s.InstalledTick);
            if(!s.Sources.OrderBy(x=>x).SequenceEqual(result.state.Sources.OrderBy(x=>x)))throw new ArgumentException("Journal installation differs");
            for(int i=0;i<s.Entries.Length;i++){
                var e=s.Entries[i];var d=definitions.FirstOrDefault(x=>x.SourceId==e.SourceId);
                if(d==null||e.Id!="DEVICE_LOG_L"+loop+"_"+(i+1)||e.DeviceId!=d.DeviceId||e.PlaceId!=d.PlaceId||e.Tick<s.InstalledTick||e.Tick>s.LastTick||e.EventSequence<1||string.IsNullOrEmpty(e.ActivationId)||!new[]{"WarningShown","ContactContinued","Cancelled","Cause","Result","RiskResolved"}.Contains(e.Kind))throw new ArgumentException("Invalid journal entry");
                var before=s.Entries.Take(i).Where(p=>p.SourceId==e.SourceId&&p.ActivationId==e.ActivationId).ToArray();
                if(e.Kind!="WarningShown"&&!before.Any(p=>p.Kind=="WarningShown")||before.Any(p=>p.Kind==e.Kind||p.TargetId!=e.TargetId||p.DefinitionId!=e.DefinitionId||p.Revision!=e.Revision||p.Tick>e.Tick||p.EventSequence>=e.EventSequence||new[]{"Cancelled","Result","RiskResolved"}.Contains(p.Kind))||e.Kind=="Cause"&&!before.Any(p=>p.Kind=="ContactContinued")||new[]{"Result","RiskResolved"}.Contains(e.Kind)&&!before.Any(p=>p.Kind=="Cause"))throw new ArgumentException("Journal transition has no predecessor");
            }
            if(s.Links.Any(l=>l==null||string.IsNullOrEmpty(l.CaseId)||!s.Entries.Any(e=>e.SourceId==l.SourceId&&e.ActivationId==l.ActivationId&&e.Kind=="WarningShown")||s.Entries.Any(e=>e.SourceId==l.SourceId&&e.ActivationId==l.ActivationId&&new[]{"Cancelled","Result","RiskResolved"}.Contains(e.Kind)))||s.Links.Select(l=>l.SourceId+"|"+l.CaseId).Distinct().Count()!=s.Links.Length)throw new ArgumentException("Invalid active journal links");
            result.state=s.Copy();return result;
        }
    }
}
