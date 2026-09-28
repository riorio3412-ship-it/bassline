using System;
using System.Collections;
using System.Linq;
using UnityEngine;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionSmokeProbe
    {
        static bool ResidentMeetingsReview=>Array.IndexOf(Environment.GetCommandLineArgs(),"-bassline-resident-meetings-review")>=0;
        IEnumerator ReviewResidentMeetings()
        {
            driveWorld=false;
            runtime.Routines=new[]{new ResidentRoutine{ActorId="CH_02"},new ResidentRoutine{ActorId="CH_07"}};
            foreach(var actor in runtime.World.Residents)actor.NextSocialTick=runtime.World.Tick+60000;
            foreach(var id in new[]{"CH_02","CH_07"}){
                var position=id=="CH_02"?new Vector3(-4,0,-4):new Vector3(-4,0,-2.7f);
                var body=runtime.Bodies.Single(b=>b.ActorId==id);body.Capsule.enabled=false;body.transform.position=position;body.Capsule.enabled=true;
                var actor=runtime.World.Resident(id);actor.Position=MansionRuntime.P(position);actor.Node=runtime.Layout.NavigationNodes[runtime.Layout.NearestNode(position)].Id;actor.NextSocialTick=0;
                placements.Add(new PlacementReceipt{Purpose="TestOnly NPC encounter framing; actual Rest completion, visual perception and speech; no knowledge injection",ActorId=id,To=position,Tick=runtime.World.Tick});
            }
            Physics.SyncTransforms();
            for(int cycle=1;cycle<=2;cycle++){
                foreach(var id in new[]{"CH_02","CH_07"})Require(runtime.World.Plan(id,runtime.World.Resident(id).Node,"Rest",60)=="Accepted","Rest activity rejected.");
                for(int tick=0;tick<600&&new[]{"CH_02","CH_07"}.Any(id=>runtime.World.Resident(id).CompletedActivities<cycle);tick++)runtime.AdvanceOne();
            }
            foreach(var id in new[]{"CH_02","CH_07"}){var actor=runtime.World.Resident(id);actor.Yaw=id=="CH_02"?0:180;runtime.Bodies.Single(b=>b.ActorId==id).transform.rotation=Quaternion.Euler(0,(float)actor.Yaw,0);}Physics.SyncTransforms();
            for(int tick=0;tick<240&&!runtime.CaptureSession().ResidentConversations.Any(s=>s.Phase=="Speaking");tick++)runtime.AdvanceOne();
            var offer=runtime.CaptureSession().ResidentConversations.Single(s=>s.Phase=="Speaking");
            while(runtime.World.Tick<offer.StartedTick+140)runtime.AdvanceOne();
            var approach=new Vector3(-2.5f,0,-3.4f);Require(PlayerCapsuleClear(approach),"Overheard scene approach is obstructed.");SetTestOnlyPlayerPosition(approach,runtime.Layout.NearestNode(approach));runtime.SetLook(270,8);
            while(runtime.World.Tick<offer.StartedTick+290)runtime.AdvanceOne();
            Require(runtime.ReadAmbientSpeech().HeardText.StartsWith("… ")&&!runtime.World.Paused&&CurrentScreen==1,"Overhearing disclosed unheard words or interrupted exploration.");
            yield return CaptureAtBothResolutions("resident_overheard_suffix_testonly",1);
            while(runtime.World.Tick<offer.StartedTick+600)runtime.AdvanceOne();
            yield return CaptureAtBothResolutions("resident_spoken_reply_testonly",1);
            while(runtime.World.Tick<offer.StartedTick+720)runtime.AdvanceOne();
            Require(runtime.Social.For("CH_07",runtime.World.Tick).Single().State=="Agreed"&&runtime.Social.For("CH_01",runtime.World.Tick).Length==0,"Autonomous meeting was not agreed privately.");
            Call(hud,"Primary","CH_02");yield return FinishLoanSpeech();Click("다른 이야기");
            yield return CaptureAtBothResolutions("resident_plan_choices_testonly",3);
            Click("누구 만나기로 했어?");yield return FinishLoanSpeech();
            Require(runtime.ReadConversationPlayback().HeardText.Contains("유시온"),"Plan question did not disclose the resident's own appointment.");
            yield return CaptureAtBothResolutions("resident_plan_answer_testonly",3);
            Click("그만 이야기하기");runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);
            Require(captures.Count==12&&captures.All(c=>c.PngVerified),"Resident meeting review captures are incomplete.");
            receipt.ResidentMeetingsVerified=true;receipt.Status="PASS_SMOKE_ONLY";
        }
    }
}
