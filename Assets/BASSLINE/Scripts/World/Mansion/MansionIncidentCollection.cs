using System;
using System.Collections.Generic;
using System.Linq;
using BASSLINE.Core;

namespace BASSLINE.World.Mansion
{
    public sealed class MansionKnownCase
    {
        public string Id;public MansionCasePublicView View;
    }
    public sealed class MansionScopedReceipt
    {
        public string CaseId;public MansionCaseReceipt Receipt;
        public string ImportKey=>CaseId+"|"+Receipt.Id;
    }
    // Owns lifecycle instances, not hidden-answer selection.
    // The world book remains the only chapter budget / result-order authority.
    public sealed class MansionIncidentCollection
    {
        readonly List<MansionIncident> entries=new List<MansionIncident>();
        public int Count=>entries.Count;
        public MansionIncident[] All()=>entries.ToArray();
        public bool Controls(string actor)=>entries.Any(e=>e.Controls(actor));
        public MansionIncident Find(string id)=>entries.FirstOrDefault(e=>e.CaseId==id);
        public string Configure(MansionWorld world,MansionCaseSettings settings)
        {
            if(settings==null||!settings.Enabled)return "Disabled";
            if(Find(settings.Id)!=null)return "AlreadyConfigured";
            // One resident cannot execute two competing movement plans at once.
            // This does not reserve the chapter for any one resident.
            if(entries.Any(e=>e.ActorId==settings.ActorId&&e.Capture().CauseTick<0&&e.Stage!="Cancelled"))return "ActorPlanInProgress";
            var next=new MansionIncident();string result=next.Configure(world,settings);
            if(result=="Configured")entries.Add(next);return result;
        }
        public string ConfigurePlanned(MansionWorld world,MansionCaseSettings settings,IncidentPlanState plan,IncidentExecutionBinding binding,IActorKnowledgeQuery own)
        {
            if(settings==null||!settings.Enabled)return "Disabled";
            if(Find(settings.Id)!=null)return "AlreadyConfigured";
            if(entries.Any(e=>e.ActorId==settings.ActorId&&e.Capture().CauseTick<0&&e.Stage!="Cancelled"))return "ActorPlanInProgress";
            var next=new MansionIncident();string result=next.ConfigurePlanned(world,settings,plan,binding,own);
            if(result=="Configured")entries.Add(next);return result;
        }
        public string CommitPlayerStrike(MansionWorld world,string target,string weapon,Point3 contact,IMansionIncidentPhysics physics)
        {
            var next=new MansionIncident();string result=next.CommitPlayerStrike(world,target,weapon,contact,physics);
            if(next.IsConfigured)entries.Add(next);
            return result;
        }
        public void Step(MansionWorld world,Func<string,IActorKnowledgeQuery> ownKnowledge,IMansionIncidentPhysics physics)
        {
            if(world.Paused)return;
            foreach(var entry in entries)entry.Step(world,ownKnowledge(entry.ActorId),physics);
            world.SealCaseOutcomes();
        }
        public MansionKnownCase[] ReadFor(string observer)
            =>entries.Select(e=>new MansionKnownCase{Id=e.CaseId,View=e.Read(observer)}).Where(c=>c.View.Discovered||c.View.Confirmed).ToArray();
        public MansionScopedReceipt[] ReceiptsFor(string observer)
            =>entries.SelectMany(e=>e.ReceiptsFor(observer).Select(r=>new MansionScopedReceipt{CaseId=e.CaseId,Receipt=r})).ToArray();
        public string Discover(MansionWorld world,string observer,string selectedVictim,IMansionIncidentPhysics physics)
        {
            // Match the physically selected body; never substitute another hidden target.
            var selected=entries.FirstOrDefault(e=>e.TargetId==selectedVictim&&e.Capture().ResultTick>=0);
            return selected?.Discover(world,observer,physics)??"Unavailable";
        }
        public MansionIncidentSnapshot[] Capture()=>entries.Select(e=>e.Capture()).ToArray();
        public static MansionIncidentCollection Restore(MansionIncidentSnapshot[] snapshots,MansionWorld world)
        {
            if(snapshots==null||snapshots.Any(s=>s==null||s.Settings==null)||snapshots.Select(s=>s.Settings.Id).Distinct().Count()!=snapshots.Length)throw new ArgumentException("Invalid incident collection");
            var result=new MansionIncidentCollection();
            foreach(var snapshot in snapshots)result.entries.Add(MansionIncident.Restore(snapshot,world));
            if(snapshots.Where(s=>!string.IsNullOrEmpty(s.Rescuer)).GroupBy(s=>s.Rescuer).Any(g=>g.Count()>1))throw new ArgumentException("One helper cannot perform two rescue actions");
            if(world.Residents.Any(r=>r.Activity.StartsWith("Rescue:",StringComparison.Ordinal)&&!snapshots.Any(s=>s.Rescuer==r.Id&&r.Activity=="Rescue:"+s.Settings.Id)))throw new ArgumentException("Rescue activity without matching incident");
            if(!world.CaseBook.Capture().Entries.Select(e=>e.CaseId).OrderBy(id=>id,StringComparer.Ordinal).SequenceEqual(snapshots.Select(s=>s.Settings.Id).OrderBy(id=>id,StringComparer.Ordinal)))throw new ArgumentException("Incident collection does not cover the chapter book");
            if(snapshots.Where(s=>s.CauseTick<0&&s.Stage!="Cancelled").GroupBy(s=>s.Settings.ActorId).Any(g=>g.Count()>1))throw new ArgumentException("Competing movement plans for one resident");
            return result;
        }
    }
}
