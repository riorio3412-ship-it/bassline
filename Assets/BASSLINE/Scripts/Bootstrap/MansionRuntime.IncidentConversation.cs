using System;
using System.Linq;
using BASSLINE.Core;
using BASSLINE.NPC;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerIncidentConversationPort
    {
        long IncidentConversationSince=>World.CaseBook.Capture().ChapterStartTick;
        public bool CanDiscussIncident(string actor)
        {
            if(actor=="CH_01"||!bodies.ContainsKey(actor))return false;
            var own=Knowledge.For("CH_01");
            return IncidentConversation.Select(own,IncidentConversationSince)!=null||own.Records().Any(r=>r.ReceivedTick>=IncidentConversationSince&&r.Predicate=="SaidStatement"&&(r.Value??"").StartsWith("Incident",StringComparison.Ordinal));
        }
        public string AskAboutIncident(string actor,bool directOnly)
        {
            if(World.Paused||!CanDiscussIncident(actor)||!Reach(actor))return "Unavailable";
            var own=Knowledge.For(actor);
            var previous=conversationPlayback.SpeakerId==actor?own.Find(conversationPlayback.SharedRecordId):null;
            var known=directOnly&&previous!=null&&IncidentConversation.Relevant(previous)?previous:IncidentConversation.Select(own,IncidentConversationSince);
            string text=IncidentConversation.InVoice(actor,IncidentConversation.Explain(known,NameOf,directOnly));
            return BeginConversation(actor,text,"IncidentObservation",Math.Max(240,text.Length*5),sharedRecord:known?.Id??"");
        }
        public string StateIncidentPosition(string actor,bool admit)
        {
            if(World.Paused||!CanDiscussIncident(actor)||!Reach(actor))return "Unavailable";
            string text=admit?"아까 공격한 사람은 나야.":"내가 공격한 게 아니야.";
            return BeginConversation("CH_01",text,"IncidentPosition:"+(admit?"Admit":"Deny"),Math.Max(180,text.Length*5),recipient:actor);
        }
        bool FinishIncidentPosition(ConversationPlaybackState speech,string root)
        {
            if(speech.SpeakerId!="CH_01"||!(speech.Value??"").StartsWith("IncidentPosition:",StringComparison.Ordinal)||root=="")return false;
            if(!speech.Listeners.Any(l=>l.ActorId==speech.RecipientId&&l.HeardCharacters==speech.PlannedText.Length))return false;
            string text=IncidentConversation.InVoice(speech.RecipientId,IncidentConversation.Reply(Knowledge.For(speech.RecipientId),IncidentConversationSince,speech.Value=="IncidentPosition:Admit"));
            return BeginConversation(speech.RecipientId,text,"IncidentPositionReply",Math.Max(240,text.Length*5))=="Dialogue";
        }
    }
}
