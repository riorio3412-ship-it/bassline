using System;
using System.Linq;

namespace BASSLINE.World.Mansion
{
    [Serializable] public sealed class MansionPublishedDeath
    {
        public string DeathId;public long Tick;
        public MansionPublishedDeath Copy()=>(MansionPublishedDeath)MemberwiseClone();
    }
    [Serializable] public sealed class MansionPublicScheduleSnapshot
    {
        public MansionPublishedDeath[] Publications=Array.Empty<MansionPublishedDeath>();
        public long ConveneAt=-1;
        public int Revision;
        public bool AdditionalDeathExtensionUsed;
        public MansionPublicScheduleSnapshot Copy(){var copy=(MansionPublicScheduleSnapshot)MemberwiseClone();copy.Publications=Publications.Select(p=>p.Copy()).ToArray();return copy;}
    }
    // Takes completed public publications only. Hidden cause/death/reservation state is not an input.
    public sealed class MansionPublicCaseSchedule
    {
        const long Minute=60*60;
        MansionPublicScheduleSnapshot state=new MansionPublicScheduleSnapshot();
        public long ConveneAt=>state.ConveneAt;
        public int Revision=>state.Revision;
        // DeathId is the announced death, not its perpetrator or potentially shared case.
        public string PublishConfirmedDeath(string deathId,long tick)
        {
            _=new BASSLINE.Core.StableId(deathId);
            if(state.Publications.Any(p=>p.DeathId==deathId))return "AlreadyPublished";
            if(tick<0||state.Publications.Any(p=>p.Tick>tick))throw new ArgumentOutOfRangeException(nameof(tick));
            bool first=state.Publications.Length==0;state.Publications=state.Publications.Concat(new[]{new MansionPublishedDeath{DeathId=deathId,Tick=tick}}).ToArray();
            if(first){state.ConveneAt=tick+60*Minute;state.Revision++;return "Scheduled";}
            if(!state.AdditionalDeathExtensionUsed){state.AdditionalDeathExtensionUsed=true;state.ConveneAt=Math.Max(state.ConveneAt,tick+30*Minute);state.Revision++;return "AdditionalDeathPublished";}
            return "PublishedWithoutExtension";
        }
        public MansionPublicScheduleSnapshot Capture()=>state.Copy();
        public static MansionPublicCaseSchedule Restore(MansionPublicScheduleSnapshot s,long tick)
        {
            if(s==null||s.Publications==null||s.Publications.Any(p=>p==null)||s.Publications.Select(p=>p.DeathId).Distinct().Count()!=s.Publications.Length||s.Revision<0||s.ConveneAt< -1)throw new ArgumentException("Invalid public schedule");
            long previous=-1;foreach(var p in s.Publications){_=new BASSLINE.Core.StableId(p.DeathId);if(p.Tick<0||p.Tick<previous||p.Tick>tick)throw new ArgumentException("Invalid publication order");previous=p.Tick;}
            int count=s.Publications.Length;
            long expected=count==0?-1:s.Publications[0].Tick+60*Minute;
            if(count>1)expected=Math.Max(expected,s.Publications[1].Tick+30*Minute);
            if(s.ConveneAt!=expected||s.Revision!=Math.Min(count,2)||s.AdditionalDeathExtensionUsed!=(count>1))throw new ArgumentException("Invalid publication progression");
            return new MansionPublicCaseSchedule{state=s.Copy()};
        }
    }
}
