using System;
using System.Linq;
namespace BASSLINE.Core
{
    [Serializable] public sealed class PresenceSightProgress
    {
        public string Observer="",Subject="",PlaceId="",SeriesId="",IdentityRecordId="",PublishedRecordId="";
        public long StartedTick,LastTick,PublishedThroughTick=-1;
        public Point3 LastPosition;
        public PresenceSightProgress Copy()=>(PresenceSightProgress)MemberwiseClone();
    }
    [Serializable] public sealed class PresenceObservationState
    {
        public long Sequence;
        public PresenceSightProgress[] Sight=Array.Empty<PresenceSightProgress>();
        public PresenceObservationState Copy()=>new PresenceObservationState{Sequence=Sequence,Sight=Sight.Select(s=>s.Copy()).ToArray()};
    }
}
