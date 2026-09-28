using System.Collections;
using System.Linq;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.NPC;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionSmokeProbe
    {
        IEnumerator ReviewFamilyDispute()
        {
            driveWorld=false;
            FamilyDisputeState State()=>(FamilyDisputeState)Field(runtime,"familyDispute");
            Require(!runtime.CanDiscussFamilyDispute("CH_03"),"Private family memory leaked into player's menu");
            var space=runtime.Layout.NavigationNodes.Select((node,index)=>new{node,index}).FirstOrDefault(n=>PlayerCapsuleClear(n.node.Position+Vector3.right*1.3f)&&PlayerCapsuleClear(n.node.Position+Vector3.forward*.8f)&&PlayerCapsuleClear(n.node.Position-Vector3.forward*.8f)&&runtime.Bodies.Where(b=>b.ActorId!="CH_01").All(b=>Vector3.Distance(b.transform.position,n.node.Position)>7)&&Physics.OverlapSphere(n.node.Position+Vector3.up*1.35f,1.25f,~0,QueryTriggerInteraction.Ignore).All(c=>c.transform.IsChildOf(player.transform)));
            Require(space!=null,"No clear conversation review area");
            SetTestOnlyPlayerPosition(space.node.Position+Vector3.right*1.3f,space.index);runtime.SetLook(270,0);
            foreach(var id in new[]{"CH_03","CH_05"}){
                var body=runtime.Bodies.Single(b=>b.ActorId==id);var r=runtime.World.Resident(id);var pos=space.node.Position+Vector3.forward*(id=="CH_03"?-.8f:.8f);
                placements.Add(new PlacementReceipt{Purpose="TestOnly encounter for authored family dialogue and save interruption; not natural travel",ActorId=id,From=body.transform.position,To=pos,NodeId=space.node.Id,Tick=runtime.World.Tick,PhysicalReachAccepted=true});
                runtime.World.StopResidentActivity(id);body.Capsule.enabled=false;body.transform.position=pos;body.Capsule.enabled=true;r.Position=MansionRuntime.P(pos);r.Node=space.node.Id;r.Yaw=id=="CH_03"?0:180;r.Phase="Performing";r.Activity="Wait";r.ActivityTicks=12000;
            }
            Physics.SyncTransforms();
            for(int t=0;t<500&&State().Speech.Phase!="Speaking";t++){runtime.AdvanceOne();if(t%20==0)yield return null;}
            Require(State().Speech.Phase=="Speaking","Authored opening did not start at actual encounter: "+State().Phase+"/"+State().Step);
            for(int t=0;t<90;t++)runtime.AdvanceOne();
            int before=State().Speech.EmittedCharacters;Require(before>0&&before<State().Speech.PlannedText.Length,"No partial speech cursor");
            Require(runtime.SaveSlot()=="저장 완료","Family speech save failed");Require(runtime.LoadSlot()=="불러오기 완료","Family speech load failed");Call(hud,"SyncPauseView");
            Require(State().Speech.EmittedCharacters==before,"Speech restarted on load");
            Call(runtime,"InterruptFamilyDispute","CH_03");
            Require(FamilyDisputePlanner.Received(runtime.Knowledge.For("CH_05"),"Opening","CH_03",runtime.World.Tick)==null,"Interrupted opening became a complete received statement");
            Require(State().Plans.Length==0,"Partial speech selected a plan");
            for(int t=0;t<3500&&State().Phase!="Completed"&&State().Phase!="Deferred";t++){runtime.AdvanceOne();if(t%20==0)yield return null;}
            Require(State().Phase=="Completed"&&State().Plans.Length==2,"Own-knowledge dialogue/choice/action failed: "+State().Phase+"/"+State().Step+"/"+State().Outcome);
            Require(State().Plans.All(p=>p.Action=="Negotiate"&&p.Alternatives.Length==5),"Default negotiation did not consider all alternatives");
            Require(runtime.CanDiscussFamilyDispute("CH_03"),"Heard conversation did not enable simple topic");
            Require(runtime.Knowledge.For("CH_01").Records().All(r=>r.Predicate!="PersonalHistory"),"Private memory was delivered as evidence");
            yield return Capture("family_ambient_heard",1,1920,1080);
            FramePlayerNearResident("CH_03",true);Call(hud,"Primary","CH_03");yield return FinishLoanSpeech();
            Click("두 사람의 이야기");yield return Capture("family_choices",3,1920,1080);
            Click("내가 함께 들어 줄게");yield return FinishLoanSpeech();
            Require(FamilyDisputePlanner.Received(runtime.Knowledge.For("CH_03"),"Mediate","CH_01",runtime.World.Tick)!=null,"Mediation was not actually heard");
            Require(State().Plans.Last().Action=="AskForHelp"&&State().Plans.Last().TargetId=="CH_01","Received offer did not change the action");
            yield return Capture("family_mediation_reply",3,1920,1080);
            Click("다른 이야기로");Click("그만 이야기하기");
            for(int t=0;t<1500&&State().Phase!="Completed";t++){runtime.AdvanceOne();if(t%20==0)yield return null;}
            Require(State().Phase=="Completed","NPC did not execute the selected request for help");
            Require(runtime.Knowledge.For("CH_01").Records().Any(r=>r.Predicate=="SaidStatement"&&r.Value=="FamilyDispute:AskForHelp"),"Player never heard the chosen help request");
            Require(runtime.SaveSlot()=="저장 완료"&&runtime.LoadSlot()=="불러오기 완료","Completed family action did not save/load");
            receipt.Scope+=" Authored family dialogue: test-only encounter placements, actual speech/partial interruption, mid-speech save/resume, own-knowledge five-option deliberation, real negotiation and mediation/help request via UI. No NPC murder, independent lethal evidence routes, natural encounter travel or full-game completion claim.";
        }
    }
}
