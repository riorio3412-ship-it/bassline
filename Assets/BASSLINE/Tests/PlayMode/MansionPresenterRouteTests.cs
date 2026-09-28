using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BASSLINE.Bootstrap;
using BASSLINE.Save;
using BASSLINE.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace BASSLINE.Tests
{
    public sealed class MansionPresenterRouteTests
    {
        [UnityTest] public IEnumerator PresenterYieldsBesideDoorForAnOpposingResidentAndResumesAfterLoad()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");
            var runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;
            runtime.UseIsolatedTestStorage();runtime.Routines=Array.Empty<ResidentRoutine>();
            UnityEngine.Object.FindAnyObjectByType<FixtureHud>().enabled=false;
            foreach(var owner in runtime.World.Capture().PauseOwners)runtime.Pause(owner,false);
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var field=typeof(MansionRuntime).GetField("proceedings",flags);
            var progress=(MansionProceedings)field.GetValue(runtime);
            var resident=runtime.World.Resident("CH_07");var residentBody=runtime.Bodies.Single(b=>b.ActorId==resident.Id);
            var residentStart=runtime.Layout.NavigationNodes.Single(n=>n.Id=="NAV_CN_024_1");
            var presenterStart=runtime.Layout.NavigationNodes.Single(n=>n.Id=="NAV_CN_024_2");
            var residentTarget=runtime.Layout.NavigationNodes.Single(n=>n.Id=="NAV_R_CUL_COR_F0_003_002");
            var presenterTarget=runtime.Layout.NavigationNodes.Single(n=>n.Id=="NAV_R_CE_F0_066_003");
            // Author an opposing doorway encounter. Thereafter both use real capsule movement and FIFO grants.
            residentBody.Capsule.enabled=false;residentBody.transform.position=residentStart.Position;residentBody.Capsule.enabled=true;
            resident.Position=MansionRuntime.P(residentStart.Position);resident.Node=residentStart.Id;resident.Phase="Idle";
            runtime.Presenter.Capsule.enabled=false;runtime.Presenter.transform.position=presenterStart.Position;runtime.Presenter.Capsule.enabled=true;
            progress.PresenterPosition=MansionRuntime.P(presenterStart.Position);progress.PresenterNode=presenterStart.Id;
            Physics.SyncTransforms();runtime.AdvanceOne();
            Assert.That(runtime.World.Plan(resident.Id,residentTarget.Id,"Wait",int.MaxValue),Is.EqualTo("Accepted"));
            runtime.World.UseDoor("D_024",resident.Id);runtime.AdvanceOne();
            Assert.That(runtime.World.Door("D_024").Holder,Is.EqualTo(resident.Id));
            Assert.That((bool)typeof(MansionRuntime).GetMethod("PlanPresenter",flags).Invoke(runtime,new object[]{presenterTarget.Id}),Is.True);
            float maxStep=0;
            for(int i=0;i<3600;i++){
                var before=runtime.Presenter.transform.position;var beforeResident=residentBody.transform.position;
                runtime.AdvanceOne();maxStep=Math.Max(maxStep,Math.Max(Vector3.Distance(before,runtime.Presenter.transform.position),Vector3.Distance(beforeResident,residentBody.transform.position)));
                if(i==30){runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);progress=(MansionProceedings)field.GetValue(runtime);resident=runtime.World.Resident("CH_07");}
                if(progress.PresenterCursor==progress.PresenterPath.Length&&resident.Phase=="Performing")break;
                if(i%120==0)yield return null;
            }
            Assert.That(resident.Phase,Is.EqualTo("Performing"),"Resident could not leave the shared door: "+residentBody.transform.position);
            Assert.That(progress.PresenterCursor,Is.EqualTo(progress.PresenterPath.Length),"Presenter blocked at "+runtime.Presenter.transform.position);
            Assert.That(Vector3.Distance(runtime.Presenter.transform.position,presenterTarget.Position),Is.LessThan(.3f));
            Assert.That(maxStep,Is.LessThan(.4f),"No yielding teleport");
        }

        [UnityTest] public IEnumerator PresenterCanDescendTheUpperServiceStairWithRealCollision()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");
            var runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;
            runtime.UseIsolatedTestStorage();runtime.Routines=Array.Empty<ResidentRoutine>();
            var hud=UnityEngine.Object.FindAnyObjectByType<FixtureHud>();hud.enabled=false;
            foreach(var owner in runtime.World.Capture().PauseOwners)runtime.Pause(owner,false);
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var progress=(MansionProceedings)typeof(MansionRuntime).GetField("proceedings",flags).GetValue(runtime);
            var start=runtime.Layout.NavigationNodes.Single(n=>n.Id=="NAV_R_STAIR_STAIR_03_005");
            var target=runtime.Layout.NavigationNodes.Single(n=>n.Id=="NAV_R_STAIR_F2_000_006");
            // Explicit fixture setup only. All subsequent movement uses the ordinary presenter controller.
            var body=runtime.Presenter;body.Capsule.enabled=false;body.transform.position=start.Position;body.Capsule.enabled=true;
            progress.PresenterPosition=MansionRuntime.P(start.Position);progress.PresenterNode=start.Id;Physics.SyncTransforms();
            Assert.That((bool)typeof(MansionRuntime).GetMethod("PlanPresenter",flags).Invoke(runtime,new object[]{target.Id}),Is.True);
            float maxStep=0;
            for(int i=0;i<2400&&progress.PresenterCursor<progress.PresenterPath.Length;i++){
                var before=body.transform.position;runtime.AdvanceOne();maxStep=Math.Max(maxStep,Vector3.Distance(before,body.transform.position));
                if(i%120==0)yield return null;
            }
            string next=progress.PresenterCursor<progress.PresenterPath.Length?progress.PresenterPath[progress.PresenterCursor]:"done";
            var point=next=="done"?target.Position:runtime.Layout.NavigationNodes.Single(n=>n.Id==next).Position;
            Assert.That(progress.PresenterCursor,Is.EqualTo(progress.PresenterPath.Length),"Presenter at "+body.transform.position.ToString("F3")+" next "+next+" "+point.ToString("F3"));
            Assert.That(Vector3.Distance(body.transform.position,target.Position),Is.LessThan(.3f));
            Assert.That(maxStep,Is.LessThan(.4f),"No staircase teleport");
        }
    }
}

