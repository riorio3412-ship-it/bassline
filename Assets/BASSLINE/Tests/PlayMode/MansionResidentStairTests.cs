using System;
using System.Collections;
using System.Linq;
using BASSLINE.Bootstrap;
using BASSLINE.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BASSLINE.Tests
{
    public sealed class MansionResidentStairTests
    {
        [UnityTest] public IEnumerator ResidentFinishesGrandStairSegmentAfterSummonsAndReload()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");
            var runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();
            runtime.AutomaticTick=false;runtime.UseIsolatedTestStorage();runtime.Routines=Array.Empty<ResidentRoutine>();
            UnityEngine.Object.FindAnyObjectByType<FixtureHud>().enabled=false;
            foreach(var owner in runtime.World.Capture().PauseOwners)runtime.Pause(owner,false);
            var actor=runtime.World.Resident("CH_15");var body=runtime.Bodies.Single(b=>b.ActorId==actor.Id);
            var start=runtime.Layout.NavigationNodes.Single(n=>n.Id=="NAV_R_GRAND_STAIR_00_000");
            var upper=runtime.Layout.NavigationNodes.Single(n=>n.Id=="NAV_R_GRAND_STAIR_00_002");
            var destination=runtime.Layout.NavigationNodes.Single(n=>n.Id=="NAV_R_HALL_F0_013_015");
            // Reproduce a summons while physically climbing. Only the initial encounter is authored.
            body.Capsule.enabled=false;body.transform.position=start.Position;body.Capsule.enabled=true;
            actor.Position=MansionRuntime.P(start.Position);actor.Node=start.Id;actor.Phase="Idle";Physics.SyncTransforms();
            Assert.That(runtime.World.Plan(actor.Id,upper.Id,"Walk",900),Is.EqualTo("Accepted"));
            bool retargeted=false;float maxStep=0;
            for(int i=0;i<4200;i++){
                var before=body.transform.position;runtime.AdvanceOne();maxStep=Math.Max(maxStep,Vector3.Distance(before,body.transform.position));
                if(!retargeted&&body.transform.position.y>.75f){
                    Assert.That(runtime.World.Plan(actor.Id,destination.Id,"WaitForCourt",int.MaxValue),Is.EqualTo("Accepted"));
                    runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);actor=runtime.World.Resident("CH_15");retargeted=true;
                }
                if(retargeted&&actor.Phase=="Performing")break;
                if(i%120==0)yield return null;
            }
            Assert.That(retargeted,Is.True,"Actor never climbed the first flight: "+body.transform.position);
            string next=actor.PathCursor<actor.Path.Length?actor.Path[actor.PathCursor]:"done";
            Assert.That(actor.Phase,Is.EqualTo("Performing"),"Actor left the physical stair route: "+body.transform.position+" target "+next);
            Assert.That(Vector3.Distance(body.transform.position,destination.Position),Is.LessThan(.3f));
            Assert.That(maxStep,Is.LessThan(.4f),"No staircase teleport");
        }

        [UnityTest] public IEnumerator ResidentDescendsUpperServiceStairWithoutFloorAvoidance()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");
            var runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();
            runtime.AutomaticTick=false;runtime.UseIsolatedTestStorage();runtime.Routines=Array.Empty<ResidentRoutine>();
            UnityEngine.Object.FindAnyObjectByType<FixtureHud>().enabled=false;
            foreach(var owner in runtime.World.Capture().PauseOwners)runtime.Pause(owner,false);
            var actor=runtime.World.Resident("CH_15");var body=runtime.Bodies.Single(b=>b.ActorId==actor.Id);
            var start=runtime.Layout.NavigationNodes.Single(n=>n.Id=="NAV_R_STAIR_STAIR_03_005");
            var destination=runtime.Layout.NavigationNodes.Single(n=>n.Id=="NAV_R_STAIR_F2_000_006");
            body.Capsule.enabled=false;body.transform.position=start.Position;body.Capsule.enabled=true;
            actor.Position=MansionRuntime.P(start.Position);actor.Node=start.Id;actor.Phase="Idle";Physics.SyncTransforms();
            Assert.That(runtime.World.Plan(actor.Id,destination.Id,"Walk",900),Is.EqualTo("Accepted"));
            float maxStep=0;
            for(int i=0;i<3600&&actor.Phase!="Performing";i++){
                var before=body.transform.position;runtime.AdvanceOne();maxStep=Math.Max(maxStep,Vector3.Distance(before,body.transform.position));
                if(i==180){runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);actor=runtime.World.Resident("CH_15");}
                if(i%120==0)yield return null;
            }
            Assert.That(actor.Phase,Is.EqualTo("Performing"),"Resident oscillates beside the stair: "+body.transform.position);
            Assert.That(Vector3.Distance(body.transform.position,destination.Position),Is.LessThan(.3f));
            Assert.That(maxStep,Is.LessThan(.4f),"No staircase teleport");
        }

        [UnityTest] public IEnumerator ResidentWaitsOnGrandStairForTemporaryObstruction()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");
            var runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();
            runtime.AutomaticTick=false;runtime.UseIsolatedTestStorage();runtime.Routines=Array.Empty<ResidentRoutine>();
            UnityEngine.Object.FindAnyObjectByType<FixtureHud>().enabled=false;
            foreach(var owner in runtime.World.Capture().PauseOwners)runtime.Pause(owner,false);
            var actor=runtime.World.Resident("CH_15");var body=runtime.Bodies.Single(b=>b.ActorId==actor.Id);
            var start=runtime.Layout.NavigationNodes.Single(n=>n.Id=="NAV_R_GRAND_STAIR_00_000");
            var destination=runtime.Layout.NavigationNodes.Single(n=>n.Id=="NAV_R_GRAND_STAIR_00_002");
            body.Capsule.enabled=false;body.transform.position=start.Position;body.Capsule.enabled=true;
            actor.Position=MansionRuntime.P(start.Position);actor.Node=start.Id;actor.Phase="Idle";
            var obstacle=new GameObject("TestOnly temporary stair obstruction",typeof(BoxCollider));
            obstacle.transform.position=new Vector3(2.55f,2.34f,2.2f);obstacle.GetComponent<BoxCollider>().size=new Vector3(.8f,2,1.2f);
            Physics.SyncTransforms();Assert.That(runtime.World.Plan(actor.Id,destination.Id,"Walk",900),Is.EqualTo("Accepted"));
            float maxStep=0;
            try{
                for(int i=0;i<3600&&actor.Phase!="Performing";i++){
                    if(i==900){obstacle.SetActive(false);Physics.SyncTransforms();}
                    var before=body.transform.position;runtime.AdvanceOne();maxStep=Math.Max(maxStep,Vector3.Distance(before,body.transform.position));
                    if(i%120==0)yield return null;
                }
                Assert.That(actor.Phase,Is.EqualTo("Performing"),"Resident left the stair instead of waiting: "+body.transform.position);
                Assert.That(Vector3.Distance(body.transform.position,destination.Position),Is.LessThan(.3f));
                Assert.That(maxStep,Is.LessThan(.4f));
            }finally{UnityEngine.Object.Destroy(obstacle);}
        }
    }
}
