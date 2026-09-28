using System;
namespace BASSLINE.Core
{
    // Pending spoken invitation, saved with the existing conversation cursor.
    // This payload is never exposed through the player playback view.
    [Serializable] public sealed class AppointmentSpeech
    {
        public string Stage="",PlaceId="",AppointmentId="",Outcome="";
        public int Revision;public long StartTick,Duration=6000;
        public AppointmentSpeech Copy()=>(AppointmentSpeech)MemberwiseClone();
    }
    public interface IPlayerAppointmentConversationPort
    {
        string AskAboutAppointment(string actorId);
    }
}
