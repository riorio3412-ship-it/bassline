using System;
using System.IO;
using System.Linq;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.NPC;
using BASSLINE.Knowledge;
using BASSLINE.World.Mansion;
using BASSLINE.AuthoringData;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerAppointmentCardPort
    {
        const int CardWriteTicks=180;
        bool WritingCard=>Social?.Card?.Phase=="Writing";
        AppointmentRevision CardRevision(string id,int revision)=>Social.Capture().Appointments.SingleOrDefault(a=>a.Id==id&&a.Revision==revision);
        string CardText(AppointmentRevision a)=>"약속 카드\n\n"+NameOf(a.Organizer)+" → "+NameOf(a.Invitee)+"\n"+AppointmentWhen(a.StartTick)+"\n"+PlaceLabel(a.PlaceId)+"에서 만나기\n\n"+(a.Revision==1?"처음 정한 내용":"고쳐 적은 내용")+"\n— "+NameOf(a.Organizer);
        public AppointmentCardView ReadAppointmentCard()
        {
            bool near=targets.ContainsKey(AppointmentDesk.CardId)&&Reach(AppointmentDesk.CardId);
            var state=Social.Card;var a=near?CardRevision(state.AppointmentId,state.Revision):null;
            return new AppointmentCardView{Nearby=near,Writing=WritingCard,Message=state.Message,AppointmentId=a?.Id??"",Revision=a?.Revision??0,
                Text=near?(a==null?"아직 비어 있는 약속 카드예요.":CardText(a)):"가까이에서 약속 카드를 확인해 주세요.",
                OwnMeetings=Social.For("CH_01",World.Tick).Where(x=>x.Organizer=="CH_01"&&x.StartTick>World.Tick+CardWriteTicks&&x.State!="Declined").OrderBy(x=>x.StartTick).ToArray()};
        }
        public string WriteAppointmentCard(string id,string place,long start)
        {
            var player=World.Resident("CH_01");var own=Social.For("CH_01",World.Tick).SingleOrDefault(a=>a.Id==id&&a.Organizer=="CH_01");
            if(World.Paused||toolPress.Running||WritingCard||!World.CanAct("CH_01")||RescueControls("CH_01")||conversationPlayback.Phase=="Speaking"||itemExchange.Handoff.Running||PlayerSurfaceRunning||inspection.State=="Running"||!ReadAppointmentCard().Nearby||own==null||!InvitationPlaceIds().Contains(place)||start<=World.Tick+CardWriteTicks||start>World.Tick+24L*60*60*60)return "지금은 카드를 쓸 수 없어요. 가까이에서 앞으로의 약속을 골라 주세요.";
            var s=Social.Card;s.WritingId=id;s.WritingRevision=own.Revision;s.WritingPlace=place;s.WritingStart=start;s.ElapsedTicks=0;s.StartedTick=World.Tick;s.Phase="Writing";s.Message="약속 카드에 쓰고 있어요.";
            s.ResumePhase=player.Phase;s.ResumeActivity=player.Activity;s.ResumeTicks=player.ActivityTicks;
            player.Phase="Performing";player.Activity="Write";player.ActivityTicks=int.MaxValue;move=default;running=false;StopWaiting("약속 카드를 쓰려고 멈췄어요.");return s.Message;
        }
        public void CancelAppointmentCard()
        {
            if(!WritingCard)return;FinishCardWriting("Interrupted","쓰기를 멈췄어요. 원래 내용은 그대로예요.");
        }
        void FinishCardWriting(string phase,string text)
        {
            var s=Social.Card;var player=World.Resident("CH_01");s.Phase=phase;s.Message=text;
            if(player.Alive&&player.Present&&player.Activity=="Write"){player.Phase=s.ResumePhase;player.Activity=s.ResumeActivity;player.ActivityTicks=s.ResumeTicks;}
        }
        void AdvanceAppointmentCard()
        {
            if(!WritingCard)return;
            var s=Social.Card;var player=World.Resident("CH_01");
            if(!World.CanAct("CH_01")||RescueControls("CH_01")||player.Activity!="Write"||!Reach(AppointmentDesk.CardId)){CancelAppointmentCard();return;}
            if(++s.ElapsedTicks<CardWriteTicks)return;
            var own=Social.For("CH_01",World.Tick).SingleOrDefault(a=>a.Id==s.WritingId&&a.Organizer=="CH_01");
            if(own==null||own.Revision!=s.WritingRevision||s.WritingStart<=World.Tick){FinishCardWriting("Interrupted","그사이 약속이 달라졌어요. 내용을 다시 골라 주세요.");return;}
            bool changed=own.PlaceId!=s.WritingPlace||own.StartTick!=s.WritingStart;int revision=own.Revision;
            if(changed){string id=Social.Propose("CH_01",own.Invitee,s.WritingPlace,s.WritingStart,own.Duration,World.Tick,own.Id);if(id!=own.Id){CancelAppointmentCard();return;}revision++;}
            var a=CardRevision(own.Id,revision);
            // Copying identical contents preserves the original receipt; it must not invalidate a recipient's earlier copy.
            if(!changed&&!string.IsNullOrEmpty(a.WrittenRecordId)){
                s.AppointmentId=a.Id;s.Revision=a.Revision;FinishCardWriting("Completed","같은 약속 내용을 다시 적었어요.");return;
            }
            var paper=targets[AppointmentDesk.CardId].transform;
            string record=Knowledge.Observe("CH_01",new KnownRecord{Kind="Visual",Source="CH_01",SubjectId=AppointmentDesk.CardId,Predicate="WrittenInvitation",Value=a.Id+":"+a.Revision,Text=CardText(a),ProvenanceKey="INVITATION_CARD:"+a.Id+":"+a.Revision,PlaceId=ObjectPlace(paper.position),Position=P(paper.position),FromTick=World.Tick,ToTick=World.Tick+1,Supports=new[]{"이 시각에 카드에 적은 장소와 시각"},DoesNotEstablish=new[]{"상대가 바뀐 내용을 받거나 동의했다는 뜻은 아님"}},World.Tick);
            Social.RecordWrittenInvitation(a.Id,a.Revision,AppointmentDesk.CardId,record,World.Tick);s.AppointmentId=a.Id;s.Revision=a.Revision;
            World.Emit("InvitationWritten","CH_01",a.Id,a.Revision.ToString());FinishCardWriting("Completed",changed?"바뀐 내용을 적었어요. 상대에게도 이야기해 주세요.":"약속 내용을 카드에 적었어요.");
        }
        void ProcessWrittenAppointmentReceipts()
        {
            foreach(var a in Social.Capture().Appointments.Where(a=>!string.IsNullOrEmpty(a.WrittenRecordId)&&!a.ReceivedBy.Contains(a.Invitee))){
                var received=Knowledge.For(a.Invitee).Records().FirstOrDefault(r=>r.RootId==a.WrittenRecordId);
                if(received!=null)Social.Receive(a.Id,a.Revision,a.Invitee,received.ReceivedTick,received.RootId);
            }
        }
        AppointmentRevision KnownWrittenChange(string actor)
        {
            return Social.For("CH_01",World.Tick).Where(a=>a.Organizer=="CH_01"&&a.Invitee==actor&&a.StartTick>World.Tick+360).Select(a=>CardRevision(a.Id,a.Revision)).Where(a=>string.IsNullOrEmpty(a.ReplyRecordId)&&!string.IsNullOrEmpty(a.WrittenRecordId)&&Knowledge.For("CH_01").Find(a.WrittenRecordId)!=null).OrderByDescending(a=>a.WrittenTick).FirstOrDefault();
        }
        public bool HasWrittenAppointmentChange(string actor)=>KnownWrittenChange(actor)!=null;
        public string TellChangedAppointment(string actor)
        {
            var a=KnownWrittenChange(actor);if(a==null||!CanDiscussAppointment(actor))return "가까이에서 내가 적은 약속 내용을 이야기해 주세요.";
            var payload=new AppointmentSpeech{Stage="Relay",AppointmentId=a.Id,Revision=a.Revision,PlaceId=a.PlaceId,StartTick=a.StartTick,Duration=a.Duration};
            string result=BeginConversation("CH_01",RelayCardText(a),"AppointmentChange:"+a.Id+":"+a.Revision,360,sharedRecord:a.WrittenRecordId,recipient:actor,evidenceIds:new[]{a.WrittenRecordId});
            if(result=="Dialogue")conversationPlayback.Appointment=payload;return result;
        }
        string RelayCardText(AppointmentRevision a)=>"약속 카드에 "+AppointmentWhen(a.StartTick)+", "+PlaceLabel(a.PlaceId)+"에서 만나자고 적었어. 이대로 만나도 괜찮을까?";
        string ReadPhysicalAppointmentCard()
        {
            var s=Social.Card;var a=CardRevision(s.AppointmentId,s.Revision);
            if(a!=null)return a.WrittenRecordId;
            var paper=targets[AppointmentDesk.CardId].transform;return Observe("CH_01",AppointmentDesk.CardId,"BlankInvitationCard","Blank","아직 비어 있는 약속 카드. 앞으로의 약속을 적거나 고쳐 쓸 수 있다.",paper.position,new[]{"현재 카드가 비어 있음"},new[]{"과거에 무엇이 적혔는지는 알 수 없음"});
        }
        BASSLINE.Save.MansionSessionSnapshot UpgradeAppointmentCardContent(BASSLINE.Save.MansionSessionSnapshot s)
        {
            if(s?.World==null||(s.OptionalObjects&512)!=0)return s;
            var desk=FindAnyObjectByType<AppointmentDesk>();if(!desk)return s;
            if(!s.World.Objects.Any(o=>o.Id==AppointmentDesk.CardId)){
                s.World.Objects=s.World.Objects.Concat(new[]{new MansionObjectState{Id=AppointmentDesk.CardId,Name="약속 카드",Location="World",Position=P(desk.PaperRestPoint.position)}}).ToArray();
                AppendEvent(s.World,"ContentObjectAdded","",AppointmentDesk.CardId,"Version0.22; blank card added at load, not a historical invitation");
            }
            s.Social.Card=new AppointmentCardState();s.OptionalObjects|=512;return s;
        }
        void ValidateAppointmentCard(SocialSnapshot social,MansionState world,KnowledgeLedger knowledge)
        {
            var s=social.Card;if(s==null||!new[]{"Idle","Writing","Completed","Interrupted"}.Contains(s.Phase)||s.ElapsedTicks<0||s.ElapsedTicks>CardWriteTicks)throw new InvalidDataException("약속 카드의 작성 상태가 올바르지 않습니다.");
            foreach(var a in social.Appointments.Where(a=>!string.IsNullOrEmpty(a.WrittenRecordId))){
                var record=knowledge.For(a.Organizer).Find(a.WrittenRecordId);
                if(a.Organizer!="CH_01"||a.DocumentId!=AppointmentDesk.CardId||!world.Objects.Any(o=>o.Id==a.DocumentId)||record==null||record.Source!=a.Organizer||record.Predicate!="WrittenInvitation"||record.SubjectId!=a.DocumentId||record.Value!=a.Id+":"+a.Revision||record.Text!=CardText(a)||record.ReceivedTick!=a.WrittenTick||a.WrittenTick<a.ProposedTick||a.WrittenTick>world.Tick||!world.Events.Any(e=>e.Type=="InvitationWritten"&&e.Actor==a.Organizer&&e.Target==a.Id&&e.Detail==a.Revision.ToString()&&e.Tick==a.WrittenTick))throw new InvalidDataException("실제로 적은 초대장 원문이 아닙니다.");
                if(string.IsNullOrEmpty(a.ProposalRecordId)&&a.ReceivedBy.Contains(a.Invitee)&&!knowledge.For(a.Invitee).Records().Any(r=>r.RootId==a.WrittenRecordId&&r.ReceivedTick>=a.WrittenTick))throw new InvalidDataException("초대장 변경을 받지 않은 인물입니다.");
            }
            if(!string.IsNullOrEmpty(s.AppointmentId)&&!social.Appointments.Any(a=>a.Id==s.AppointmentId&&a.Revision==s.Revision&&a.DocumentId==AppointmentDesk.CardId&&!string.IsNullOrEmpty(a.WrittenRecordId)))throw new InvalidDataException("카드에 적힌 약속 버전을 찾을 수 없습니다.");
            if(s.Phase=="Writing"){
                var a=social.Appointments.SingleOrDefault(x=>x.Id==s.WritingId&&x.Revision==s.WritingRevision);
                if(a==null||a.Organizer!="CH_01"||!InvitationPlaceIds().Contains(s.WritingPlace)||s.WritingStart<=s.StartedTick+CardWriteTicks||s.WritingStart>s.StartedTick+24L*60*60*60||s.StartedTick<0||s.StartedTick+s.ElapsedTicks>world.Tick||world.Residents.Single(r=>r.Id=="CH_01").Activity!="Write")throw new InvalidDataException("카드를 쓰기 시작한 약속과 진행이 다릅니다.");
            }
        }
    }
}
