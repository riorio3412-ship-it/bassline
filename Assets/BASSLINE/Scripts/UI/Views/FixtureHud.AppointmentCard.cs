using System;
using System.Linq;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
namespace BASSLINE.UI
{
    public sealed partial class FixtureHud
    {
        string cardMeetingId="",cardPlace="";bool choosingCardPlace,wasWritingCard;
        void OpenCardWriter(){cardMeetingId="";cardPlace="";choosingCardPlace=false;Open(4);}
        void RenderCardWriter(ProductionScreenView view,Action<string,Action> button,ref string title,ref string body,ref string context)
        {
            var port=Source as IPlayerAppointmentCardPort;var card=port?.ReadAppointmentCard();title="약속 카드에 적기";body="";
            if(card==null||!card.Nearby){body="카드 가까이에서 다시 확인해 주세요.";return;}
            var meetings=card.OwnMeetings;var chosen=meetings.FirstOrDefault(a=>a.Id==cardMeetingId)??meetings.FirstOrDefault();
            if(chosen==null){body="아직 적을 약속이 없어요.\n누군가와 만날 약속을 먼저 잡아 보세요.";context=card.Text;view.SetRecordRows(Array.Empty<RecordRow>(),"",null);return;}
            cardMeetingId=chosen.Id;
            if(!choosingCardPlace){
                title="어느 약속을 적을까?";
                context=ActorLabel(chosen.Invitee)+"와의 약속\n\n"+TimeLabel(chosen.StartTick)+"\n"+Place(chosen.PlaceId)+"\n\n고쳐 적는 것만으로 상대에게 전해지지는 않아요.";
                button("이대로 적기",()=>CloseForAction(()=>status=port.WriteAppointmentCard(chosen.Id,chosen.PlaceId,chosen.StartTick)));
                button("장소나 시간 바꿔 적기",()=>{choosingCardPlace=true;cardPlace=chosen.PlaceId;});
                view.SetRecordRows(meetings.Select(a=>new RecordRow{Id=a.Id,Heading=(a.Id==chosen.Id?"선택한 약속 · ":"")+ActorLabel(a.Invitee),Text=TimeLabel(a.StartTick)+" · "+Place(a.PlaceId)}).ToArray(),chosen.Id,id=>{cardMeetingId=id;Render();});
            }else{
                var venues=places.InvitationPlaceIds();if(!venues.Contains(cardPlace))cardPlace=chosen.PlaceId;
                title="어디로 바꿔 적을까?";context="고쳐 적을 장소\n"+Place(cardPlace)+"\n\n원래 시각\n"+TimeLabel(chosen.StartTick)+"\n\n다 쓴 뒤 상대에게 바뀐 내용을 이야기해 주세요.";
                button("이 장소로 고쳐 쓰기",()=>CloseForAction(()=>status=port.WriteAppointmentCard(chosen.Id,cardPlace,chosen.StartTick)));
                button("10분 뒤로 바꾸고 쓰기",()=>CloseForAction(()=>status=port.WriteAppointmentCard(chosen.Id,cardPlace,this.port.ReadPlayer().Tick+36000)));
                button("약속 다시 고르기",()=>choosingCardPlace=false);
                view.SetRecordRows(venues.Select(id=>new RecordRow{Id=id,Heading=id==cardPlace?"선택한 장소":"만날 장소",Text=Place(id)}).ToArray(),cardPlace,id=>{cardPlace=id;Render();});
            }
        }
        void TrackCardWriting()
        {
            var card=(Source as IPlayerAppointmentCardPort)?.ReadAppointmentCard();if(card==null)return;
            if(wasWritingCard&&!card.Writing){status=card.Message;feedbackUntil=UnityEngine.Time.unscaledTime+6;}
            wasWritingCard=card.Writing;
        }
    }
}
