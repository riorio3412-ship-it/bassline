using System;
using System.IO;
using System.Linq;
using BASSLINE.Core;
using BASSLINE.NPC;
using BASSLINE.Knowledge;
using BASSLINE.World.Mansion;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerAppointmentConversationPort
    {
        string AppointmentWhen(long tick)=>WorldTimeLabel.Format(tick,World.ClockVersion);
        string OfferValue(AppointmentSpeech a)=>"AppointmentOffer:"+a.PlaceId+":"+a.StartTick;
        string OfferText(AppointmentSpeech a)=>AppointmentWhen(a.StartTick)+"에 "+PlaceLabel(a.PlaceId)+"에서 만날까? 잠깐 같이 시간을 보내고 싶어.";
        string ReplyValue(AppointmentSpeech a)=>"AppointmentReply:"+a.AppointmentId+":"+a.Revision+":"+a.Outcome;
        string ReplyText(string actor,AppointmentSpeech a)
        {
            var voice=new ResidentConversation().Profiles().Single(v=>v.ActorId==actor);
            string when=AppointmentWhen(a.StartTick)+", "+PlaceLabel(a.PlaceId);
            bool formal=new[]{"CH_04","CH_06","CH_14","CH_18"}.Contains(actor);
            bool polite=formal||new[]{"CH_03","CH_05","CH_10","CH_12","CH_15","CH_16"}.Contains(actor);
            if(a.Outcome=="Accepted")return voice.Accepted+" "+when+(formal?"에서 뵙겠습니다.":polite?"에서 만나요.":"에서 보자.");
            if(a.Outcome=="CounterOffer")return voice.Negotiating+" "+when+(polite?" 약속은 아직 정하지 말아요. 다른 시간이나 장소로 다시 얘기해 줄래요?":" 약속은 아직 정하지 말자. 다른 시간이나 장소로 다시 얘기해 줄래?");
            return voice.Declined+(polite?" 이번 약속은 잡지 말아 주세요.":" 이번 약속은 잡지 말아 줘.");
        }
        bool CanDiscussAppointment(string actor)=>!World.Paused&&actor!="CH_01"&&bodies.ContainsKey(actor)&&World.Resident(actor).Alive&&World.Resident(actor).Present&&Reach(actor);
        public string Invite(string actor,string place,long delay)
        {
            if(!CanDiscussAppointment(actor)||!InvitationPlaceIds().Contains(place)||delay<3600||delay>24L*60*60*60)return "Unavailable";
            var speech=new AppointmentSpeech{Stage="Offer",PlaceId=place,StartTick=World.Tick+delay};
            string result=BeginConversation("CH_01",OfferText(speech),OfferValue(speech),360,recipient:actor);
            if(result=="Dialogue")conversationPlayback.Appointment=speech;return result;
        }
        public string AskAboutAppointment(string actor)
        {
            if(!CanDiscussAppointment(actor))return "Unavailable";
            var known=Social.For("CH_01",World.Tick).Where(a=>a.Organizer=="CH_01"&&a.Invitee==actor&&a.StartTick>World.Tick&&(a.State=="Proposed"||a.State=="Agreed")).OrderByDescending(a=>a.StartTick).FirstOrDefault();
            if(known==null)return "아직 함께 정한 약속이 없어요.";
            if(known.State=="Agreed")return BeginConversation(actor,AppointmentWhen(known.StartTick)+"에 "+PlaceLabel(known.PlaceId)+"에서 만나기로 했지. 그때 보자.","AppointmentReminder",360);
            var original=Social.Capture().Appointments.Single(a=>a.Id==known.Id&&a.Revision==known.Revision);
            if(string.IsNullOrEmpty(original.ProposalRecordId))return !string.IsNullOrEmpty(original.WrittenRecordId)?TellChangedAppointment(actor):"아직 상대에게 제안이 전해지지 않았어요.";
            return StartAppointmentReply(original);
        }
        string StartAppointmentReply(AppointmentRevision original)
        {
            string actor=original.Invitee;bool conflict=Social.For(actor,World.Tick).Any(a=>a.Id!=original.Id&&a.State=="Agreed"&&a.StartTick<original.StartTick+original.Duration&&original.StartTick<a.StartTick+a.Duration);
            string activity=original.PlaceId=="R_LIBRARY"?"Read":original.PlaceId=="R_DINING"?"Meal":original.PlaceId=="R_GREEN"||original.PlaceId=="R_GARDEN"?"Walk":original.PlaceId=="R_ARCADE"?"Game":"Talk";
            var decision=new ResidentConversation().EvaluateInvitation(actor,activity,original.PlaceId,Social.Experiences(actor),conflict);
            var payload=new AppointmentSpeech{Stage="Reply",AppointmentId=original.Id,Revision=original.Revision,PlaceId=original.PlaceId,StartTick=original.StartTick,Duration=original.Duration,Outcome=original.StartTick<=World.Tick+360?"Declined":decision.Outcome};
            string result=BeginConversation(actor,ReplyText(actor,payload),ReplyValue(payload),360);
            if(result=="Dialogue")conversationPlayback.Appointment=payload;return result;
        }
        bool FinishAppointmentStatement(ConversationPlaybackState speech,string record)
        {
            var a=speech.Appointment;if(a==null||string.IsNullOrEmpty(record))return false;
            string listener=speech.SpeakerId=="CH_01"?speech.RecipientId:"CH_01";
            if(!speech.Listeners.Any(l=>l.ActorId==listener&&l.HeardCharacters==speech.PlannedText.Length))return false;
            if(a.Stage=="Relay"){
                var original=CardRevision(a.AppointmentId,a.Revision);
                Social.Receive(original.Id,original.Revision,speech.RecipientId,World.Tick,original.WrittenRecordId);
                return StartAppointmentReply(original)=="Dialogue";
            }
            if(a.Stage=="Offer"){
                string id=Social.Propose("CH_01",speech.RecipientId,a.PlaceId,a.StartTick,a.Duration,World.Tick);
                if(id=="Unavailable")return false;
                Social.RecordSpokenProposal(id,1,record,World.Tick);
                World.Emit("InvitationReceived","CH_01",speech.RecipientId,id);
                return StartAppointmentReply(Social.Capture().Appointments.Single(x=>x.Id==id))=="Dialogue";
            }
            Social.RecordSpokenReply(a.AppointmentId,a.Revision,record,World.Tick,a.Outcome=="Accepted");
            World.Emit("InvitationAnswered",speech.SpeakerId,"CH_01",a.AppointmentId+":"+a.Outcome);return false;
        }
        void ValidateAppointmentSpeech(ConversationPlaybackState speech,SocialSnapshot social,MansionState world,KnowledgeLedger knowledge)
        {
            // Old non-spoken fixtures are retained. Every new spoken agreement must have both actual utterance receipts.
            foreach(var a in social.Appointments.Where(a=>!string.IsNullOrEmpty(a.ProposalRecordId)||!string.IsNullOrEmpty(a.WrittenRecordId))){
                var offer=knowledge.For(a.Organizer).Find(a.ProposalRecordId);
                if(!string.IsNullOrEmpty(a.ProposalRecordId)&&(offer==null||offer.Source!=a.Organizer||offer.ConversationWith!=a.Invitee||offer.Predicate!="SaidStatement"||offer.Value!="AppointmentOffer:"+a.PlaceId+":"+a.StartTick||a.ProposalReceivedTick!=a.ProposedTick||a.ProposalReceivedTick!=offer.ReceivedTick||a.ProposalReceivedTick>world.Tick||!knowledge.For(a.Invitee).Records().Any(r=>r.RootId==offer.RootId&&r.Predicate=="SaidStatement")))throw new InvalidDataException("끝까지 전한 약속 제안이 아닙니다.");
                if(string.IsNullOrEmpty(a.ReplyRecordId)){
                    if(a.AcceptedBy.Contains(a.Invitee)||a.ConfirmationReceivedBy.Length>0||a.Declined||a.ReplyReceivedTick!=0)throw new InvalidDataException("답을 듣기 전에 확정한 약속입니다.");
                }else{
                    var reply=knowledge.For(a.Invitee).Find(a.ReplyRecordId);
                    string prefix="AppointmentReply:"+a.Id+":"+a.Revision+":";
                    if(reply==null||reply.Source!=a.Invitee||reply.Predicate!="SaidStatement"||!new[]{"Accepted","Declined","CounterOffer"}.Any(o=>reply.Value==prefix+o)||a.ReplyReceivedTick!=reply.ReceivedTick||a.ReplyReceivedTick<(string.IsNullOrEmpty(a.ProposalRecordId)?a.WrittenTick:a.ProposalReceivedTick)||a.ReplyReceivedTick>world.Tick||!knowledge.For(a.Organizer).Records().Any(r=>r.RootId==reply.RootId&&r.Predicate=="SaidStatement")||a.Declined==(reply.Value==prefix+"Accepted")||a.AcceptedBy.Contains(a.Invitee)==a.Declined||a.ConfirmationReceivedBy.Contains(a.Organizer)==a.Declined)throw new InvalidDataException("실제로 들은 약속 답변과 상태가 다릅니다.");
                }
            }
            if(speech.Phase=="Idle")return;
            var p=speech.Appointment;
            bool marked=speech.Value.StartsWith("AppointmentOffer:",StringComparison.Ordinal)||speech.Value.StartsWith("AppointmentReply:",StringComparison.Ordinal)||speech.Value.StartsWith("AppointmentChange:",StringComparison.Ordinal);
            if(p==null){if(marked)throw new InvalidDataException("약속 발화의 내용이 없습니다.");return;}
            if(!marked||!InvitationPlaceIds().Contains(p.PlaceId)||p.StartTick<=speech.StartedTick||p.StartTick>speech.StartedTick+24L*60*60*60||p.Duration!=6000||speech.DurationTicks!=360||speech.EvidenceIds==null||p.Stage!="Relay"&&speech.EvidenceIds.Length!=0)throw new InvalidDataException("약속 발화의 시간·장소가 올바르지 않습니다.");
            if(p.Stage=="Offer"){
                if(speech.SpeakerId!="CH_01"||p.Revision!=0||p.AppointmentId!=""||p.Outcome!=""||speech.Value!=OfferValue(p)||speech.PlannedText!=OfferText(p))throw new InvalidDataException("제안한 약속과 발화가 다릅니다.");
            }else if(p.Stage=="Reply"){
                var a=social.Appointments.SingleOrDefault(x=>x.Id==p.AppointmentId&&x.Revision==p.Revision);
                if(a==null||a.Invitee!=speech.SpeakerId||a.PlaceId!=p.PlaceId||a.StartTick!=p.StartTick||string.IsNullOrEmpty(a.ProposalRecordId)&&string.IsNullOrEmpty(a.WrittenRecordId)||!new[]{"Accepted","Declined","CounterOffer"}.Contains(p.Outcome)||speech.Value!=ReplyValue(p)||speech.PlannedText!=ReplyText(speech.SpeakerId,p))throw new InvalidDataException("듣지 않은 약속에 대한 답변입니다.");
            }else if(p.Stage=="Relay"){
                var a=social.Appointments.SingleOrDefault(x=>x.Id==p.AppointmentId&&x.Revision==p.Revision);
                if(a==null||a.Organizer!=speech.SpeakerId||a.Invitee!=speech.RecipientId||a.PlaceId!=p.PlaceId||a.StartTick!=p.StartTick||string.IsNullOrEmpty(a.WrittenRecordId)||knowledge.For("CH_01").Find(a.WrittenRecordId)==null||a.WrittenTick>speech.StartedTick||speech.EvidenceIds.Length!=1||speech.EvidenceIds[0]!=a.WrittenRecordId||speech.SharedRecordId!=a.WrittenRecordId||speech.PlannedText!=RelayCardText(a)||speech.Value!="AppointmentChange:"+a.Id+":"+a.Revision)throw new InvalidDataException("적거나 읽지 않은 초대장의 내용을 전하려 합니다.");
            }else throw new InvalidDataException("약속 발화 단계가 올바르지 않습니다.");
            if(speech.Phase=="Speaking"&&(world.Residents.Single(r=>r.Id==speech.SpeakerId).Activity!="Talk"||speech.SpeakerId=="CH_01"&&world.Residents.Single(r=>r.Id==speech.RecipientId).Activity!="Listen"))throw new InvalidDataException("약속 대화 참가자의 행동이 다릅니다.");
        }
    }
}
