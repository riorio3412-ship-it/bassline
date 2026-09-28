using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.World.Mansion
{
    [Serializable] public sealed class IncidentPhysicalTrace
    {
        public string MarkId="",SurfaceId="",SourceId="";
        public long ContactTick;
        public Point3 ContactPosition,MarkLocalPoint;
        public IncidentPhysicalTrace Copy()=>(IncidentPhysicalTrace)MemberwiseClone();
    }
    public sealed partial class MansionIncident
    {
        // Authority bookkeeping after a real deposit. Never creates or publishes an observation.
        public bool BindPhysicalTrace(MansionWorld world,SurfaceTrace mark,Point3 contact)
        {
            var s=state.Settings;
            if(s==null||s.ExplicitTestSession||world.Paused||state.CauseTick!=world.Tick||mark==null||mark.DepositedTick!=world.Tick||mark.SourceId!=s.ObjectId||mark.SurfaceId!=s.TargetId||!contact.Finite()||contact.Distance(state.ContactPoint)>.12||state.PhysicalTraces.Any(t=>t.MarkId==mark.Id))return false;
            state.PhysicalTraces=state.PhysicalTraces.Concat(new[]{new IncidentPhysicalTrace{MarkId=mark.Id,SurfaceId=mark.SurfaceId,SourceId=mark.SourceId,ContactTick=world.Tick,ContactPosition=contact,MarkLocalPoint=mark.LocalPoint}}).ToArray();
            return true;
        }
        static void ValidatePhysicalTraceLinks(MansionIncidentSnapshot s)
        {
            if(s.PhysicalTraces==null||s.PhysicalTraces.Any(t=>t==null)||s.PhysicalTraces.Select(t=>t.MarkId).Distinct().Count()!=s.PhysicalTraces.Length)throw new ArgumentException("Invalid physical incident trace links");
            foreach(var t in s.PhysicalTraces)if(s.Settings==null||s.Settings.ExplicitTestSession||s.CauseTick<0||string.IsNullOrEmpty(t.MarkId)||t.ContactTick!=s.CauseTick||t.SourceId!=s.Settings.ObjectId||t.SurfaceId!=s.Settings.TargetId||!t.MarkLocalPoint.Finite()||!t.ContactPosition.Finite()||t.ContactPosition.Distance(s.ContactPoint)>.12)throw new ArgumentException("Trace does not belong to an actual incident contact");
        }
    }
}
