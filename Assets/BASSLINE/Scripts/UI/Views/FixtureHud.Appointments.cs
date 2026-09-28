using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.UI
{
    public sealed partial class FixtureHud
    {
        void RenderInvitationPicker(ProductionScreenView view,Action<string,Action> button,ref string title,ref string body,ref string context)
        {
            if(selectedTarget==BASSLINE.AuthoringData.AppointmentDesk.CardId){RenderCardWriter(view,button,ref title,ref body,ref context);return;}
            var venues=places?.InvitationPlaceIds()??new[]{"K_H"};
            title="어디서 만날까?";body="";
            if(venues.Length==0){body="지금은 만날 장소를 고를 수 없어요.";return;}
            invitationPlaceIndex=Math.Max(0,Math.Min(invitationPlaceIndex,venues.Length-1));string venue=venues[invitationPlaceIndex];
            context=ActorLabel(selectedTarget)+"에게 제안하기\n\n"+Place(venue)+"\n\n시간을 고르면 대화로 돌아가요.\n상대의 답을 듣고 약속을 정해요.";
            button("5분 뒤 · "+TimeLabel(port.ReadPlayer().Tick+5L*60*60),()=>SpeakInvitation(venue,5L*60*60));
            button("10분 뒤 · "+TimeLabel(port.ReadPlayer().Tick+10L*60*60),()=>SpeakInvitation(venue,10L*60*60));
            view.SetRecordRows(venues.Select(id=>new RecordRow{Id=id,Heading=id==venue?"선택한 장소":"만날 장소",Text=Place(id)}).ToArray(),venue,id=>{invitationPlaceIndex=Array.IndexOf(venues,id);Render();});
        }
        void SpeakInvitation(string place,long delay)
        {
            status=DialogueCommand(()=>notebook.Invite(selectedTarget,place,delay));
            if((Source as IPlayerConversationPlaybackPort)?.ReadConversationPlayback().Speaking==true){dialogueMenu=0;Back();}
        }
        string KnownAppointmentSummary(string actor)
        {
            var a=notebook.ReadNotebook().Appointments.Where(x=>x.Organizer=="CH_01"&&x.Invitee==actor).OrderByDescending(x=>x.StartTick).FirstOrDefault();
            if(a==null)return "";
            return TimeLabel(a.StartTick)+" · "+Place(a.PlaceId)+"\n"+PlayerUiText.Appointment(a.State);
        }
    }
}
