using System;
using System.Collections;
using System.Linq;
using UnityEngine;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionSmokeProbe
    {
        static bool AppointmentReview=>Array.IndexOf(Environment.GetCommandLineArgs(),"-bassline-appointment-review")>=0;
        readonly string[] appointmentStages={"appointment_places_scale125_testonly","appointment_offer_scale125_testonly","appointment_reply_scale125_testonly","appointment_agreed_scale125_testonly"};
        IEnumerator ReviewAppointmentConversation()
        {
            driveWorld=false;Click("다른 이야기");Click("만날 시간 정하기");
            var rows=(Array)ActiveView(4).GetType().GetProperty("RecordRowButtons").GetValue(ActiveView(4));
            int index=Array.IndexOf(runtime.InvitationPlaceIds(),"R_DINING");Require(index>=0&&rows.Length==runtime.InvitationPlaceIds().Length,"Invitation venues are not directly selectable.");
            var onClick=rows.GetValue(index).GetType().GetProperty("onClick").GetValue(rows.GetValue(index));Call(onClick,"Invoke");
            yield return CaptureAtBothResolutions(appointmentStages[0],4);
            var labels=(Array)Field(ActiveView(4),"ActionLabels");string choice=labels.Cast<object>().Select(t=>(string)t.GetType().GetProperty("text").GetValue(t)).First(t=>t.StartsWith("5분 뒤"));Click(choice);
            Require(CurrentScreen==3&&runtime.ReadConversationPlayback().SpeakerId=="CH_01"&&!runtime.World.Paused,"The invitation did not return to a running player conversation.");
            int before=runtime.Social.For("CH_01",runtime.World.Tick).Length;
            for(int i=0;i<180;i++)runtime.AdvanceOne();Require(runtime.Social.For("CH_01",runtime.World.Tick).Length==before,"An unfinished player offer created an appointment.");
            yield return CaptureAtBothResolutions(appointmentStages[1],3);
            for(int i=0;i<360;i++)runtime.AdvanceOne();Require(runtime.Social.For("CH_01",runtime.World.Tick).Last().State=="Proposed","The reply was confirmed before it finished.");
            yield return CaptureAtBothResolutions(appointmentStages[2],3);
            for(int i=0;i<180;i++)runtime.AdvanceOne();Require(runtime.Social.For("CH_01",runtime.World.Tick).Last().State=="Agreed"&&runtime.World.HasPause("K_DIALOGUE_READ"),"The spoken reply did not confirm the meeting and pause for reading.");
            yield return CaptureAtBothResolutions(appointmentStages[3],3);
            receipt.AppointmentConversationVerified=true;driveWorld=true;
        }
    }
}
