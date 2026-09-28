using System;
namespace BASSLINE.Core
{
    [Serializable] public sealed class AppointmentCardState
    {
        public string AppointmentId="",WritingId="",WritingPlace="",Phase="Idle";
        public int Revision,WritingRevision,ElapsedTicks;public long WritingStart,StartedTick;
        public string ResumePhase="",ResumeActivity="",Message="";public int ResumeTicks;
        public AppointmentCardState Copy()=>(AppointmentCardState)MemberwiseClone();
    }
    public sealed class AppointmentCardView
    {
        public bool Nearby,Writing;public string Text="",AppointmentId="",Message="";public int Revision;
        public AppointmentView[] OwnMeetings=Array.Empty<AppointmentView>();
    }
    public interface IPlayerAppointmentCardPort
    {
        AppointmentCardView ReadAppointmentCard();
        string WriteAppointmentCard(string appointmentId,string placeId,long startTick);
        void CancelAppointmentCard();
        string TellChangedAppointment(string actorId);
        bool HasWrittenAppointmentChange(string actorId);
    }
}
