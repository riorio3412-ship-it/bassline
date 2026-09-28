using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using BASSLINE.AuthoringData;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionSmokeProbe
    {
        static bool PrivateNoteReview=>Array.IndexOf(Environment.GetCommandLineArgs(),"-bassline-private-note-review")>=0;
        readonly string[] privateNoteStages={"private_note_reading_testonly","private_bag_and_note_testonly","private_note_question_testonly","private_note_explanation_testonly"};
        void FramePrivateSurface(Transform target)
        {
            var position=target.position-new Vector3(.2f,0,target.GetComponent<FixtureTarget>().StableId==CommonReturnStation.PrivateNoteId?.55f:.4f);position.y=runtime.Layout.Room("R_WORK").FloorCenter.y;
            Require(PlayerCapsuleClear(position),"Private note review framing overlaps a physical obstacle.");
            placements.Add(new PlacementReceipt{Purpose="TestOnly personal note and bag review",From=player.transform.position,To=position});
            SetTestOnlyPlayerPosition(position,runtime.Layout.NearestNode(position));runtime.SetLook(0,20);runtime.AdvanceOne();
        }
        IEnumerator ReviewPrivateNote()
        {
            driveWorld=false;var station=FindAnyObjectByType<CommonReturnStation>();Require(station&&station.PrivateNote&&station.BagFrontPoint,"Personal bag and note are missing.");
            FramePrivateSurface(station.PrivateNote);Call(hud,"Primary",CommonReturnStation.PrivateNoteId);Require(CurrentScreen==14,"Reading the note did not begin inspection.");
            for(int i=0;i<120;i++)runtime.AdvanceOne();Require(runtime.ReadInspection().State=="Completed","Note inspection did not finish.");
            for(int i=0;i<10&&CurrentScreen!=15;i++)yield return null;
            Require(CurrentScreen==15,"Completed note inspection did not open its readable text.");
            var body=Field(ActiveView(15),"Body");Require(((string)body.GetType().GetProperty("text").GetValue(body)).Contains(CommonReturnStation.PrivateNoteText),"The readable note omitted its actual text.");
            yield return CaptureAtBothResolutions(privateNoteStages[0],15);Call(hud,"Back");
            FramePrivateSurface(station.BagFrontPoint);Require(runtime.Interact(CommonReturnStation.BagFrontId)=="펜을 옮기고 있어요.","Could not start bag-front placement.");
            for(int i=0;i<120;i++)runtime.AdvanceOne();Require(runtime.World.Object("M_TAEGYEOM_PEN").AnchorId==CommonReturnStation.BagFrontId,"Bag placement did not commit.");
            Require(runtime.CaptureSession().ItemExchange.Loans.Last().Status=="Borrowed","Private placement was incorrectly treated as owner receipt.");
            yield return CaptureAtBothResolutions(privateNoteStages[1],1);
            Require(runtime.Interact("M_TAEGYEOM_PEN")=="펜을 옮기고 있어요.","Could not retrieve actual private pen.");for(int i=0;i<120;i++)runtime.AdvanceOne();Require(runtime.World.Object("M_TAEGYEOM_PEN").Owner=="CH_01","Private retrieval did not commit.");
            yield return FindReachableLoanEncounter();Call(hud,"Primary","CH_06");yield return FinishLoanSpeech();Click("빌린 펜 이야기");
            yield return CaptureAtBothResolutions(privateNoteStages[2],3);Click("이 메모, 반납 장소 얘기야?");yield return FinishLoanSpeech();
            Require(runtime.ReadConversationPlayback().HeardText.Contains("제가 챙길 순서"),"Owner clarification is not visible.");yield return CaptureAtBothResolutions(privateNoteStages[3],3);Click("그만 이야기하기");
            receipt.PrivateNoteVerified=true;receipt.LoanExchangeMethod+=" TestOnly personal note/bag framing with physical reading, original-object placement/retrieval and the actual clarification dialogue button.";
            yield return FindReachableLoanEncounter();
        }
    }
}
