using System;
using System.Linq;
namespace BASSLINE.Core
{
    [Serializable] public sealed class DoorSightProgress
    {
        public string Observer="",DoorId="",State="",Motion="";
        public long LastTick,LastRecordTick;
        public double Opening;
        public DoorSightProgress Copy()=>(DoorSightProgress)MemberwiseClone();
    }
    [Serializable] public sealed class DoorPassageProgress
    {
        // Subject is private tracking state, never an identity receipt by itself.
        public string Observer="",Subject="",DoorId="",IdentityRecordId="";
        public long StartedTick,LastTick,CrossedTick=-1;
        public int OriginSide;
        public bool IdentitySeen;
        public Point3 LastPosition;
        public DoorPassageProgress Copy()=>(DoorPassageProgress)MemberwiseClone();
    }
    [Serializable] public sealed class DoorObservationState
    {
        public long EventCursor;
        public DoorPassageProgress[] Passages=Array.Empty<DoorPassageProgress>();
        public DoorSightProgress[] Sight=Array.Empty<DoorSightProgress>();
        public DoorObservationState Copy()=>new DoorObservationState{EventCursor=EventCursor,Sight=Sight.Select(s=>s.Copy()).ToArray(),Passages=(Passages??Array.Empty<DoorPassageProgress>()).Select(p=>p.Copy()).ToArray()};
    }
}
