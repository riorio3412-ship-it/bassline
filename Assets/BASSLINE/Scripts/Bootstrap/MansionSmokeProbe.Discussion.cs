using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using BASSLINE.AuthoringData;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionSmokeProbe
    {
        const string LoanDiscussionFlag="-bassline-loan-discussion-review";
        static bool LoanDiscussionReview=>Array.IndexOf(Environment.GetCommandLineArgs(),LoanDiscussionFlag)>=0;
        static bool MinseoArtReview=>Array.IndexOf(Environment.GetCommandLineArgs(),"-bassline-minseo-art-review")>=0;
        readonly string[] discussionStages={"loan_discussion_choices_testonly","loan_player_statement_testonly","loan_discussion_refusal_testonly","loan_discussion_corrected_testonly","loan_discussion_history_testonly"};
        IEnumerator ReviewLoanDiscussion()
        {
            driveWorld=false;var station=FindAnyObjectByType<CommonReturnStation>();Require(station,"Missing public return station.");
            FrameReturnTray(station);
            Require(runtime.Interact(CommonReturnStation.TrayId)=="펜을 옮기고 있어요.","Could not start actual tray placement.");
            for(int i=0;i<120;i++)runtime.AdvanceOne();
            Require(runtime.World.Object("M_TAEGYEOM_PEN").AnchorId==CommonReturnStation.TrayId,"Tray placement did not commit.");
            bool framed=false;
            for(int attempt=0;attempt<31;attempt++){
                try{FramePlayerNearResident("CH_18");framed=true;}catch(InvalidOperationException e) when(e.Message.StartsWith("No clear, physically reachable resident framing position")){}
                if(framed&&!runtime.CaptureSession().ItemExchange.CommonReturn.Motion.Running)break;
                framed=false;for(int i=0;i<60;i++)runtime.AdvanceOne();yield return null;
            }
            Require(framed,"No clear collector encounter.");
            if(MinseoArtReview){
                var minseo=runtime.Bodies.Single(b=>b.ActorId=="CH_18");
                Require(minseo.GetComponentsInChildren<MeshFilter>().Any(f=>f.sharedMesh&&f.sharedMesh.name=="M17_Trouser_-1"),"Individual Minseo model was not included in the player.");
                Require(minseo.GetComponentsInChildren<Transform>().Any(t=>t.name=="Digital watch"),"Articulated digital watch is missing.");
                yield return CaptureAtBothResolutions("minseo_world_model_testonly",1);
            }
            Call(hud,"Primary","CH_18");yield return FinishLoanSpeech();Click("펜이 사라진 일");
            yield return CaptureAtBothResolutions(discussionStages[0],3);
            if(MinseoArtReview){
                var portrait=Field(hud,"portrait");Require(portrait!=null&&((string)Field(portrait,"diagnostics")).StartsWith("Authored2D=CH_18"),"Minseo dialogue did not use the new authored sprite.");
                var texture=(Texture2D)Field(portrait,"texture");var pixels=texture.GetPixels32();
                Require(pixels.Count(p=>p.a<8)>pixels.Length*.40f&&pixels.Count(p=>p.a>240)>pixels.Length*.15f,"Minseo sprite alpha or visible silhouette is invalid.");
                receipt.MinseoArtConnected=true;
            }
            Click("민서가 훔쳤다고 말하기");for(int i=0;i<150;i++)runtime.AdvanceOne();
            Require(runtime.ReadConversationPlayback().SpeakerId=="CH_01","The player's statement is not being rendered.");
            yield return CaptureAtBothResolutions(discussionStages[1],3);yield return FinishLoanSpeech();
            Click("반납대에 있던 펜 봤어?");yield return FinishLoanSpeech();Require(runtime.ReadConversationPlayback().HeardText.Contains("대답하고 싶지"),"Collector did not react to the actual accusation.");
            yield return CaptureAtBothResolutions(discussionStages[2],3);
            Click("내가 단정했어. 미안해.");yield return FinishLoanSpeech();Require(!runtime.ReadLoanDiscussion("CH_18").CanWithdraw,"The fully heard correction was not applied.");
            yield return CaptureAtBothResolutions(discussionStages[3],3);Click("다른 이야기로");Click("대화 다시 보기");
            var historyBody=Field(ActiveView(10),"Body");var history=(string)historyBody.GetType().GetProperty("text").GetValue(historyBody);
            Require(history.Contains("민서가 그 펜을 훔쳤어."),"Conversation history omitted the player's statement.");
            yield return CaptureAtBothResolutions(discussionStages[4],10);Click("대화로 돌아가기");Click("그만 이야기하기");
            Require(runtime.CaptureSession().Incidents.Length==0,"A nonlethal disagreement created an incident.");
            FrameReturnTray(station);
            Require(runtime.World.Object("M_TAEGYEOM_PEN").AnchorId==CommonReturnStation.TrayId,"The pen moved during this focused dialogue review; inspect actual movement rather than assuming tray custody.");
            runtime.Interact("M_TAEGYEOM_PEN");Require(runtime.World.Object("M_TAEGYEOM_PEN").Owner=="CH_01","Could not retrieve the original pen from its actual position.");
            receipt.LoanDiscussionVerified=true;receipt.LoanExchangeMethod+=" Additional TestOnly player framing at the tray and living collector; actual placement, accusation, refusal and correction. NPCs are not repositioned; no natural route claim.";
            yield return FindReachableLoanEncounter();
        }
        void FrameReturnTray(CommonReturnStation station)
        {
            var position=station.TrayPoint.position-new Vector3(.2f,0,.4f);position.y=runtime.Layout.Room("R_WORK").FloorCenter.y;
            Require(PlayerCapsuleClear(position),"Return tray review position overlaps a physical obstacle.");
            placements.Add(new PlacementReceipt{Purpose="TestOnly public return dialogue review",From=player.transform.position,To=position});
            SetTestOnlyPlayerPosition(position,runtime.Layout.NearestNode(position));runtime.SetLook(0,20);runtime.AdvanceOne();
        }
    }
}
