using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using BASSLINE.AuthoringData;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionSmokeProbe
    {
        static bool AppointmentCardReview=>Array.IndexOf(Environment.GetCommandLineArgs(),"-bassline-appointment-card-review")>=0;
        readonly string[] appointmentCardStages={"appointment_card_blank_scale125_testonly","appointment_card_choices_scale125_testonly","appointment_card_writing_testonly","appointment_card_written_scale125_testonly","appointment_card_relay_scale125_testonly"};
        IEnumerator ReadCardForReview()
        {
            Call(hud,"Primary",AppointmentDesk.CardId);Require(CurrentScreen==14,"Card inspection did not begin.");
            for(int i=0;i<120;i++)runtime.AdvanceOne();
            for(int i=0;i<10&&CurrentScreen!=15;i++)yield return null;
            Require(CurrentScreen==15,"The card's readable text did not open after physical inspection.");
        }
        IEnumerator ReviewAppointmentCard()
        {
            Require(AppointmentReview,"Card review requires the actual spoken appointment review.");driveWorld=false;
            var own=runtime.Social.For("CH_01",runtime.World.Tick).Single();
            var paper=runtime.ObjectBodies.Single(o=>o.ObjectId==AppointmentDesk.CardId).transform;
            bool found=false;
            var approaches=runtime.Layout.NavigationNodes.Where(n=>paper.position.y-n.Position.y>.3f&&paper.position.y-n.Position.y<1.2f&&Vector3.Distance(n.Position,paper.position)<2f).OrderBy(n=>Vector3.Distance(n.Position,paper.position));
            foreach(var node in approaches){
                var position=node.Position;
                if(!PlayerCapsuleClear(position))continue;
                SetTestOnlyPlayerPosition(position,runtime.Layout.NearestNode(position));
                if(!runtime.ReadAppointmentCard().Nearby)continue;
                placements.Add(new PlacementReceipt{Purpose="TestOnly physical card framing; not a travelled route",ActorId="CH_01",To=position,Tick=runtime.World.Tick,PhysicalReachAccepted=true});found=true;break;
            }
            Require(found,"No obstacle-free reachable card approach.");
            var aim=Quaternion.LookRotation(paper.position-player.Head.position).eulerAngles;runtime.SetLook(aim.y,Mathf.DeltaAngle(0,aim.x));runtime.AdvanceOne();
            yield return ReadCardForReview();yield return CaptureAtBothResolutions(appointmentCardStages[0],15);
            Click("카드에 약속 적기");Click("장소나 시간 바꿔 적기");
            var rows=(Array)ActiveView(4).GetType().GetProperty("RecordRowButtons").GetValue(ActiveView(4));
            var rowEvent=rows.GetValue(Array.IndexOf(runtime.InvitationPlaceIds(),"R_LIBRARY")).GetType().GetProperty("onClick").GetValue(rows.GetValue(Array.IndexOf(runtime.InvitationPlaceIds(),"R_LIBRARY")));Call(rowEvent,"Invoke");
            yield return CaptureAtBothResolutions(appointmentCardStages[1],4);
            Click("이 장소로 고쳐 쓰기");Require(CurrentScreen==1&&runtime.ReadAppointmentCard().Writing&&!runtime.World.Paused,"Writing did not return to the live world.");
            for(int i=0;i<90;i++)runtime.AdvanceOne();yield return CaptureAtBothResolutions(appointmentCardStages[2],1);
            for(int i=0;i<90;i++)runtime.AdvanceOne();
            Require(runtime.Social.For("CH_01",runtime.World.Tick).Single().Revision==2&&runtime.Social.For(own.Invitee,runtime.World.Tick).Single().Revision==1,"Private writing changed the guest's knowledge.");
            yield return ReadCardForReview();yield return CaptureAtBothResolutions(appointmentCardStages[3],15);Call(hud,"Back");
            FramePlayerNearResident(own.Invitee);Call(hud,"Primary",own.Invitee);yield return FinishLoanSpeech();
            Click("카드에 적은 약속 전하기");for(int i=0;i<180;i++)runtime.AdvanceOne();
            Require(runtime.Social.For(own.Invitee,runtime.World.Tick).Single().Revision==1,"The guest received a partial relay.");
            yield return CaptureAtBothResolutions(appointmentCardStages[4],3);yield return FinishLoanSpeech();
            Require(runtime.Social.For(own.Invitee,runtime.World.Tick).Single().Revision==2,"The full relay did not deliver the written change.");
            Click("그만 이야기하기");runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);
            receipt.AppointmentCardVerified=true;driveWorld=true;
        }
    }
}
