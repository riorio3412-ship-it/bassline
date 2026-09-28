using System;
namespace BASSLINE.Core
{
    // Persisted action progress, never a public view or a substitute for an observation.
    [Serializable] public sealed class ResidentResponseState
    {
        public string ActorId="",CaseId="",TargetId="",Phase="Inspecting",ObservationId="";
        public string PresenterRecordId="",SearchRoom="";
        public long StartedTick,NextPlanTick;
        public int InspectionTicks,ScanSteps;
        public ConversationPlaybackState Speech=new ConversationPlaybackState();
        public ResidentResponseState Copy(){var c=(ResidentResponseState)MemberwiseClone();c.Speech=Speech.Copy();return c;}
    }
}
