using System;
using System.Collections;
using System.IO;
using System.Linq;
using BASSLINE.AuthoringData;
using BASSLINE.Bootstrap;
using BASSLINE.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace BASSLINE.Tests
{
    public sealed partial class CommonReturnPlayModeTests
    {
        const string Pen="M_TAEGYEOM_PEN";MansionRuntime runtime;FixtureHud hud;CommonReturnStation station;
        [UnitySetUp]public IEnumerator Setup(){yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;
            runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;runtime.UseIsolatedTestStorage();hud=UnityEngine.Object.FindAnyObjectByType<FixtureHud>();MansionUiTestEntry.Start(hud);hud.enabled=false;station=UnityEngine.Object.FindAnyObjectByType<CommonReturnStation>();Assert.That(station,Is.Not.Null);
            foreach(var a in runtime.World.Residents.Where(a=>a.Id!="CH_01")){a.Phase="Performing";a.Activity="Rest";a.ActivityTicks=int.MaxValue;}
            Place("CH_06",new Vector3(-4,0,-4));Place("CH_01",new Vector3(-4,0,-5.3f));runtime.SetLook(0,0);Ticks(1);
        }
        void Place(string actor,Vector3 position){var body=runtime.Bodies.Single(b=>b.ActorId==actor);body.Capsule.enabled=false;body.transform.position=position;body.Capsule.enabled=true;var a=runtime.World.Resident(actor);a.Position=MansionRuntime.P(position);a.Node=runtime.Layout.NavigationNodes[runtime.Layout.NearestNode(position)].Id;a.Path=Array.Empty<string>();a.PathCursor=0;a.Destination="";Physics.SyncTransforms();}
        void Ticks(int n){for(int i=0;i<n;i++)runtime.AdvanceOne();}
        void Click(string text){var view=hud.GetComponentsInChildren<ProductionScreenView>(true).Single(v=>v.gameObject.activeInHierarchy&&v.ScreenId=="UI_"+hud.CurrentScreen.ToString("00"));int n=Array.FindIndex(view.ActionLabels,l=>l.text==text);Assert.That(n,Is.GreaterThanOrEqualTo(0),text);view.Actions[n].onClick.Invoke();}
        void Borrow(){hud.Primary("CH_06");MansionUiTestEntry.FinishSpeech(runtime,hud);Click("다른 이야기");Click("물건 이야기");Click("펜 잠깐 빌려도 될까?");MansionUiTestEntry.FinishSpeech(runtime,hud);Click("펜 받기");for(int i=0;i<310&&runtime.ReadItemExchange().Running;i++)Ticks(1);Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_01"));}
        void FrameSurface(Transform target){var p=target.position-new Vector3(.20f,0,.40f);p.y=runtime.Layout.Room("R_WORK").FloorCenter.y;Place("CH_01",p);runtime.SetLook(0,20);Ticks(1);}
        void PlaceOnTray(){FrameSurface(station.TrayPoint);Assert.That(runtime.Interact(CommonReturnStation.TrayId),Is.EqualTo("펜을 옮기고 있어요."));Ticks(120);Assert.That(runtime.World.Object(Pen).AnchorId,Is.EqualTo(CommonReturnStation.TrayId),runtime.ReadItemExchange().Text);}
        void StartCollector(){var p=runtime.Layout.NavigationNodes.Single(n=>n.Id==station.TrayNode).Position;Place("CH_18",p);var a=runtime.World.Resident("CH_18");a.Phase="Idle";a.Activity="Rest";a.StuckTicks=0;}
        [UnityTest]public IEnumerator PlacingIsNotReturningAndItsHalfFinishedPoseSurvivesSave()
        {
            Borrow();FrameSurface(station.TrayPoint);Assert.That(runtime.Interact(CommonReturnStation.TrayId),Is.EqualTo("펜을 옮기고 있어요."));Ticks(45);
            string path=Path.Combine(runtime.UseIsolatedTestStorage(),"surface-save.json");runtime.SaveTo(path);Ticks(50);Assert.That(runtime.World.Object(Pen).Location,Is.EqualTo("World"));runtime.LoadFrom(path);
            Assert.That(runtime.CaptureSession().ItemExchange.CommonReturn.Motion.Ticks,Is.EqualTo(45));Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_01"));Ticks(75);
            Assert.That(runtime.World.Object(Pen).AnchorId,Is.EqualTo(CommonReturnStation.TrayId));Assert.That(runtime.CaptureSession().ItemExchange.Loans.Single().Status,Is.EqualTo("Borrowed"));Assert.That(runtime.World.Events.Count(e=>e.Type=="ObjectPlacedAt"&&e.Target==Pen),Is.EqualTo(1));
            Assert.That(runtime.Knowledge.For("CH_06").Records().Any(r=>r.Predicate=="SurfaceObjectTransfer"),Is.False);yield return null;
        }
        [UnityTest]public IEnumerator CollectorWalksWithOriginalPenAndWritesOnlyAfterStorageThenPlayerRetrievesIt()
        {
            Borrow();PlaceOnTray();Place("CH_01",new Vector3(-4,0,-5.3f));StartCollector();bool sawCarry=false,savedCarry=false;Vector3 previous=runtime.Bodies.Single(b=>b.ActorId=="CH_18").transform.position;int carriedTicks=0;
            for(int i=0;i<2400&&runtime.CaptureSession().ItemExchange.CommonReturn.Entries.Length==0;i++){
                Ticks(1);var a=runtime.World.Resident("CH_18");var position=runtime.Bodies.Single(b=>b.ActorId=="CH_18").transform.position;
                Assert.That(Vector3.Distance(position,previous),Is.LessThan(.05f),"Collector must physically walk; no teleport to drawer.");previous=position;
                if(a.HeldObject==Pen){sawCarry=true;carriedTicks++;Assert.That(runtime.CaptureSession().ItemExchange.CommonReturn.Entries,Is.Empty);}
                if(a.HeldObject==Pen&&a.Phase=="Travelling"){
                    Assert.That(runtime.World.Object(Pen).Position.Distance(MansionRuntime.P(runtime.Bodies.Single(b=>b.ActorId=="CH_18").RightHand.position)),Is.LessThan(.001));
                    if(!savedCarry&&carriedTicks>80){string moving=Path.Combine(runtime.UseIsolatedTestStorage(),"carrying-save.json");runtime.SaveTo(moving);runtime.LoadFrom(moving);savedCarry=true;}
                }
                if(i%120==0)yield return null;
            }
            var state=runtime.CaptureSession().ItemExchange.CommonReturn;
            Assert.That(sawCarry,Is.True,state.Task+" / "+state.Motion.Phase+" / "+state.Motion.Reason);Assert.That(savedCarry,Is.True);Assert.That(carriedTicks,Is.GreaterThan(120));Assert.That(state.Entries.Length,Is.EqualTo(1),state.Task+" / "+runtime.World.Resident("CH_18").Activity+" / "+state.Motion.Reason);
            Assert.That(runtime.World.Object(Pen).Location,Is.EqualTo("Container"));Assert.That(runtime.ObjectBodies.Single(o=>o.ObjectId==Pen).gameObject.activeSelf,Is.False);
            Assert.That(runtime.ReadNotebook().Records.Any(r=>r.Predicate=="CleanupRecord"),Is.False,"Remote player must read the physical book.");
            Assert.That(runtime.Examine(CommonReturnStation.BookId),Is.EqualTo("Unavailable"));
            FrameSurface(station.DrawerPoint);Assert.That(runtime.Interact(CommonReturnStation.DrawerId),Does.Contain("열었어요"));Assert.That(runtime.ObjectBodies.Single(o=>o.ObjectId==Pen).gameObject.activeSelf,Is.True);
            Assert.That(runtime.Examine(Pen),Is.EqualTo("Pending"));Ticks(120);Assert.That(runtime.ReadInspection().State,Is.EqualTo("Completed"));
            Assert.That(runtime.Interact(Pen),Is.EqualTo("펜을 옮기고 있어요."));Ticks(120);Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_01"));
            var book=UnityEngine.Object.FindObjectsByType<FixtureTarget>().Single(t=>t.StableId==CommonReturnStation.BookId);FrameSurface(book.transform);Assert.That(runtime.Examine(CommonReturnStation.BookId),Is.EqualTo("Pending"));Ticks(120);
            Assert.That(runtime.ReadNotebook().Records.Last(r=>r.Predicate=="CleanupRecord").Text,Does.Contain("공용 정리함에 보관"));Assert.That(runtime.CaptureSession().Incidents,Is.Empty);
            string path=Path.Combine(runtime.UseIsolatedTestStorage(),"stored-save.json");runtime.SaveTo(path);runtime.LoadFrom(path);Assert.That(runtime.CaptureSession().ItemExchange.CommonReturn.Entries.Length,Is.EqualTo(1));
            Place("CH_06",new Vector3(-4,0,-4));Place("CH_01",new Vector3(-4,0,-5.3f));runtime.SetLook(0,0);Ticks(1);
            hud.Primary("CH_06");MansionUiTestEntry.FinishSpeech(runtime,hud);Click("빌린 펜 이야기");Click("빌린 펜 돌려주기");for(int i=0;i<310&&runtime.ReadItemExchange().Running;i++)Ticks(1);
            Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_06"));Assert.That(runtime.CaptureSession().ItemExchange.Loans.Single().Status,Is.EqualTo("Returned"));Assert.That(runtime.CaptureSession().Incidents,Is.Empty);
        }
        [UnityTest]public IEnumerator ObstacleStopsTheHandBeforePlacementAndRemoteDrawerAccessIsRejected()
        {
            Borrow();Assert.That(runtime.Interact(CommonReturnStation.DrawerId),Does.Contain("가까이"));FrameSurface(station.TrayPoint);runtime.Interact(CommonReturnStation.TrayId);Ticks(20);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=station.TrayPoint.position+new Vector3(0,.25f,-.15f);wall.transform.localScale=new Vector3(1,.6f,.08f);Physics.SyncTransforms();
            Ticks(110);Assert.That(runtime.CaptureSession().ItemExchange.CommonReturn.Motion.Phase,Is.EqualTo("Cancelled"));Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_01"));Assert.That(runtime.World.Events.Any(e=>e.Type=="ObjectPlacedAt"),Is.False);UnityEngine.Object.Destroy(wall);yield return null;
        }
        [UnityTest]public IEnumerator InterruptedPlacementDoesNotDuplicateOrCompleteLoan()
        {
            Borrow();FrameSurface(station.TrayPoint);runtime.Interact(CommonReturnStation.TrayId);Ticks(30);runtime.CancelItemExchange();Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_01"));
            runtime.Interact(CommonReturnStation.TrayId);Ticks(95);runtime.CancelItemExchange();Assert.That(runtime.World.Object(Pen).Owner,Is.Empty);Assert.That(runtime.World.Object(Pen).AnchorId,Is.EqualTo(CommonReturnStation.TrayId));Assert.That(runtime.CaptureSession().ItemExchange.Loans.Single().Status,Is.EqualTo("Borrowed"));yield return null;
        }
    }
}
