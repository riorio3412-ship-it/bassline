namespace BASSLINE.Core
{
    public sealed class AmbientSpeechView
    {
        public string SpeakerId="",HeardText="";
    }
    public interface IPlayerResidentMeetingsPort
    {
        AmbientSpeechView ReadAmbientSpeech();
        string AskResidentPlans(string actor);
    }
    public sealed class ResidentMeetingIntent
    {
        public string Organizer,Invitee,PlaceId;
        public long StartTick,Duration=6000;
    }
}
