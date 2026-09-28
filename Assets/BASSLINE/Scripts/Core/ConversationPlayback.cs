using System;
using System.Linq;
namespace BASSLINE.Core
{
    // Authority-only save data. Player consumers receive ConversationPlaybackView, never PlannedText.
    [Serializable] public sealed class ConversationListener
    {
        public string ActorId="",HeardText=""; public int HeardCharacters; public bool Gap;public long FirstHeardTick=-1,LastHeardTick=-1;
        public ConversationListener Copy()=>(ConversationListener)MemberwiseClone();
    }
    [Serializable] public sealed class ConversationPlaybackState
    {
        public long Sequence,StartedTick; public string Id="",Phase="Idle",SpeakerId="",PlannedText="",Value="",SharedRecordId="",RelationshipKey="",EndReason="";
        public int DurationTicks,ElapsedTicks,EmittedCharacters; public bool Committed,FastForward;
        public string ResumePhase="",ResumeActivity="";public int ResumeActivityTicks;
        public string RecipientId="";public string[] EvidenceIds=Array.Empty<string>();
        public string ListenerResumePhase="",ListenerResumeActivity="";public int ListenerResumeTicks;
        public ConversationListener[] Listeners=Array.Empty<ConversationListener>();
        public AppointmentSpeech Appointment;
        public ConversationPlaybackState Copy(){var s=(ConversationPlaybackState)MemberwiseClone();s.Listeners=Listeners.Select(l=>l.Copy()).ToArray();s.EvidenceIds=(string[])(EvidenceIds??Array.Empty<string>()).Clone();s.Appointment=Appointment?.Copy();return s;}
    }
    public sealed class ConversationPlaybackView
    {
        public string SpeakerId="",Phase="Idle",HeardText="",EndReason="";
        public bool Speaking=>Phase=="Speaking";
    }
    public interface IPlayerConversationPlaybackPort
    {
        ConversationPlaybackView ReadConversationPlayback();
        void FinishListeningQuickly();
        void EndConversation();
    }
}
