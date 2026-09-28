using System.Collections;
using System.Linq;
using UnityEngine;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionSmokeProbe
    {
        IEnumerator FinishLoanSpeech()
        {
            for(int i=0;i<1800&&runtime.ReadConversationPlayback().Speaking;i++){runtime.AdvanceOne();if(i%30==0)yield return null;}
            Require(runtime.ReadConversationPlayback().Phase=="Reading","Loan speech did not complete.");Call(hud,"Render");
        }
        IEnumerator ReviewLoanExchange()
        {
            driveWorld=false;yield return FindReachableLoanEncounter();driveWorld=true;
            Call(hud,"Primary","CH_06");yield return FinishLoanSpeech();
            Click("다른 이야기");Click("물건 이야기");Click("펜 잠깐 빌려도 될까?");yield return FinishLoanSpeech();
            receipt.LoanExchangeMethod="TestOnly nearby framing, then real conversation buttons, spoken permission, capsule approach and 120 authoritative arm/hand contact ticks in each direction. Not a naturally traversed encounter or full IG01 variant coverage.";
            driveWorld=false;Call(ActiveView(3),"RevealDialogue");yield return CaptureAtBothResolutions("loan_permission_scale125_testonly",3);
            Click("펜 받기");Require(runtime.ReadItemExchange().Running,"Receive button did not start the physical handoff.");
            yield return EnsureHandoffApproach(false);
            for(int i=0;i<60;i++)runtime.AdvanceOne();
            for(int attempt=0;!runtime.ReadItemExchange().Running&&attempt<8;attempt++){
                yield return RetryInterruptedLoan(false);for(int i=0;i<60;i++)runtime.AdvanceOne();
            }
            Require(runtime.ReadItemExchange().Running,"No uninterrupted handoff midpoint after real retries: "+runtime.ReadItemExchange().Text);
            Require(runtime.World.Object("M_TAEGYEOM_PEN").Owner=="CH_06","The giver lost custody before the handoff commit.");
            var camera=Camera.main;var pen=runtime.ObjectBodies.Single(o=>o.ObjectId=="M_TAEGYEOM_PEN");
            var aim=Quaternion.LookRotation(pen.transform.position-camera.transform.position).eulerAngles;float pitch=aim.x>180?aim.x-360:aim.x;runtime.SetLook(aim.y,pitch);
            yield return CaptureAtBothResolutions("loan_handoff_midpoint_testonly_frozen",1);
            for(int i=0;i<60;i++)runtime.AdvanceOne();
            for(int attempt=0;runtime.World.Object("M_TAEGYEOM_PEN").Owner!="CH_01"&&attempt<8;attempt++){
                yield return RetryInterruptedLoan(false);for(int i=0;i<120;i++)runtime.AdvanceOne();
            }
            Require(runtime.World.Object("M_TAEGYEOM_PEN").Owner=="CH_01"&&!runtime.ReadItemExchange().Running,"Physical loan did not finish.");
            runtime.SetLook(runtime.World.Yaw,0);runtime.AdvanceOne();
            yield return CaptureAtBothResolutions("loan_held_firstperson_testonly",1);
            if(PrivateNoteReview)yield return ReviewPrivateNote();
            if(LoanDiscussionReview)yield return ReviewLoanDiscussion();
            driveWorld=true;Call(hud,"Primary","CH_06");yield return FinishLoanSpeech();Click("다른 이야기");Click("물건 이야기");Click("빌린 펜 돌려주기");driveWorld=false;
            yield return EnsureHandoffApproach(true);
            for(int i=0;i<120;i++)runtime.AdvanceOne();
            for(int attempt=0;runtime.World.Object("M_TAEGYEOM_PEN").Owner!="CH_06"&&attempt<8;attempt++){
                yield return RetryInterruptedLoan(true);for(int i=0;i<120;i++)runtime.AdvanceOne();
            }
            Require(runtime.World.Object("M_TAEGYEOM_PEN").Owner=="CH_06"&&runtime.CaptureSession().ItemExchange.Loans.Last().Status=="Returned","The return button did not complete the real transfer.");
            if(PrivateNoteReview&&!LoanDiscussionReview)Require(runtime.CaptureSession().ItemExchange.Learning?.Route=="PrivateNote","Actual private-note route did not leave the learning record after return.");
            Call(hud,"OpenNotePage",10);Click("살펴본 것");yield return CaptureAtBothResolutions("loan_returned_notes_scale125_testonly",10);Call(hud,"Back");
            receipt.LoanExchangeVerified=true;
        }
        IEnumerator EnsureHandoffApproach(bool returning)
        {
            driveWorld=false;
            for(int attempt=0;attempt<8;attempt++){
                for(int i=0;i<181&&runtime.CaptureSession().ItemExchange.Handoff.Phase=="Approaching";i++)runtime.AdvanceOne();
                var h=runtime.CaptureSession().ItemExchange.Handoff;if(h.Phase=="Reaching")yield break;
                Require(h.Phase=="Cancelled"&&!h.Committed&&runtime.World.Object("M_TAEGYEOM_PEN").Owner==(returning?"CH_01":"CH_06"),"Rejected approach changed custody unexpectedly.");
                receipt.LoanApproachRejections=receipt.LoanApproachRejections.Concat(new[]{"tick "+runtime.World.Tick+": "+h.Reason}).ToArray();
                // Follow the real cancellation feedback: let the actor resume walking and frame a
                // different physical encounter. Never move the NPC or remove the blocking collider.
                if(attempt==7)break;
                for(int i=0;i<240;i++)runtime.AdvanceOne();receipt.LoanEncounterWaitTicks+=240;yield return null;
                yield return FindReachableLoanEncounter();Call(hud,"Primary","CH_06");yield return FinishLoanSpeech();Click("다른 이야기");Click("물건 이야기");
                if(!returning&&!runtime.ReadItemExchangeChoices("CH_06").CanReceive){Click("펜 잠깐 빌려도 될까?");yield return FinishLoanSpeech();}
                Click(returning?"빌린 펜 돌려주기":"펜 받기");
            }
            throw new System.InvalidOperationException("No clear handoff approach after 8 real cancellation/reposition attempts: "+string.Join("; ",receipt.LoanApproachRejections));
        }
        IEnumerator RetryInterruptedLoan(bool returning)
        {
            var handoff=runtime.CaptureSession().ItemExchange.Handoff;
            Require(handoff.Phase=="Cancelled"&&!handoff.Committed&&runtime.World.Object("M_TAEGYEOM_PEN").Owner==(returning?"CH_01":"CH_06"),"Unexpected custody after interrupted handoff.");
            receipt.LoanApproachRejections=receipt.LoanApproachRejections.Concat(new[]{"mid-motion tick "+runtime.World.Tick+": "+handoff.Reason}).ToArray();
            for(int i=0;i<240;i++)runtime.AdvanceOne();receipt.LoanEncounterWaitTicks+=240;yield return null;
            yield return FindReachableLoanEncounter();Call(hud,"Primary","CH_06");yield return FinishLoanSpeech();Click("다른 이야기");Click("물건 이야기");
            if(!returning&&!runtime.ReadItemExchangeChoices("CH_06").CanReceive){Click("펜 잠깐 빌려도 될까?");yield return FinishLoanSpeech();}
            Click(returning?"빌린 펜 돌려주기":"펜 받기");yield return EnsureHandoffApproach(returning);
        }
        IEnumerator FindReachableLoanEncounter()
        {
            // A moving resident can be on a stair or behind furniture. Advance the real world;
            // never move the resident, remove colliders, inject knowledge, or waive Reach/LOS.
            for(int attempt=0;attempt<=30;attempt++){
                bool found=false;
                try{FramePlayerNearResident("CH_06");found=true;}
                catch(System.InvalidOperationException e) when(e.Message.StartsWith("No clear, physically reachable resident framing position")){}
                if(found)yield break;
                if(attempt==30)break;
                for(int i=0;i<60;i++)runtime.AdvanceOne();receipt.LoanEncounterWaitTicks+=60;yield return null;
            }
            var actor=runtime.World.Resident("CH_06");
            throw new System.InvalidOperationException("No reachable loan encounter after 1800 real world ticks. CH_06="+JsonUtility.ToJson(actor));
        }
    }
}
