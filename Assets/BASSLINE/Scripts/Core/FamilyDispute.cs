using System;
using System.Linq;
namespace BASSLINE.Core
{
    // Private story progress. Views use the observer's received speech, never this state.
    [Serializable] public sealed class FamilyDisputeState
    {
        public int Version=1,Loop=1,Attempts;
        public string Phase="Waiting",Step="Opening",Outcome="",LocationId="",Destination="";
        public long InstalledTick,Deadline,NextPlanTick,Sequence;
        public ConversationPlaybackState Speech=new ConversationPlaybackState();
        public IncidentPlanState[] Plans=Array.Empty<IncidentPlanState>();
        public FamilyDisputeState Copy(){var c=(FamilyDisputeState)MemberwiseClone();c.Speech=Speech.Copy();c.Plans=Plans.Select(p=>p.Copy()).ToArray();return c;}
    }
    public interface IPlayerFamilyDisputePort
    {
        bool CanDiscussFamilyDispute(string actor);
        string DiscussFamilyDispute(string actor,string choice);
    }
}
