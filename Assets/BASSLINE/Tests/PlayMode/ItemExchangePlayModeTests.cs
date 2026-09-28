using System;
using System.Collections;
using System.IO;
using System.Linq;
using BASSLINE.Bootstrap;
using BASSLINE.Core;
using BASSLINE.Save;
using BASSLINE.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace BASSLINE.Tests
{
    public sealed class ItemExchangePlayModeTests
    {
        const string Pen="M_TAEGYEOM_PEN";MansionRuntime runtime;FixtureHud hud;
        [UnitySetUp]public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;
            runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;runtime.UseIsolatedTestStorage();
            hud=UnityEngine.Object.FindAnyObjectByType<FixtureHud>();MansionUiTestEntry.Start(hud);hud.enabled=false;
            foreach(var actor in runtime.World.Residents.Where(a=>a.Id!="CH_01")){actor.Phase="Performing";actor.Activity="Rest";actor.ActivityTicks=int.MaxValue;}
            Place("CH_06",new Vector3(-4,0,-4));Place("CH_01",new Vector3(-4,0,-5.3f));runtime.SetLook(0,0);Ticks(1);
            Assert.That(runtime.World.Resident("CH_06").HeldObject,Is.EqualTo(Pen));
        }
        void Place(string id,Vector3 pos){var b=runtime.Bodies.Single(x=>x.ActorId==id);b.Capsule.enabled=false;b.transform.position=pos;b.Capsule.enabled=true;runtime.World.Resident(id).Position=MansionRuntime.P(pos);Physics.SyncTransforms();}
        void Ticks(int n){for(int i=0;i<n;i++)runtime.AdvanceOne();}
        void Click(string label){var v=hud.GetComponentsInChildren<ProductionScreenView>(true).Single(x=>x.gameObject.activeInHierarchy&&x.ScreenId=="UI_"+hud.CurrentScreen.ToString("00"));int index=Array.FindIndex(v.ActionLabels,t=>t.text==label);Assert.That(index,Is.GreaterThanOrEqualTo(0),label);Assert.That(v.Actions[index].gameObject.activeSelf,Is.True,label);v.Actions[index].onClick.Invoke();}
        void OfferThroughUi()
        {
            hud.Primary("CH_06");MansionUiTestEntry.FinishSpeech(runtime,hud);Click("다른 이야기");Click("물건 이야기");Click("펜 잠깐 빌려도 될까?");
            Assert.That(runtime.ReadItemExchangeChoices("CH_06").CanReceive,Is.False,"Unspoken permission is not sufficient.");
            MansionUiTestEntry.FinishSpeech(runtime,hud);
            Assert.That(runtime.ReadItemExchangeChoices("CH_06").CanReceive,Is.True);
        }
        void FinishApproach(){for(int i=0;i<181&&runtime.CaptureSession().ItemExchange.Handoff.Phase=="Approaching";i++)Ticks(1);Assert.That(runtime.CaptureSession().ItemExchange.Handoff.Phase,Is.EqualTo("Reaching"),runtime.ReadItemExchange().Text);}
        void BeginBorrow(){OfferThroughUi();Click("펜 받기");Assert.That(hud.CurrentScreen,Is.EqualTo(1));Assert.That(runtime.ReadItemExchange().Running,Is.True);FinishApproach();}
        [UnityTest]public IEnumerator EndingAnOfferMidSpeechClosesTheSubmenuWithoutGrantingPermission()
        {
            hud.Primary("CH_06");MansionUiTestEntry.FinishSpeech(runtime,hud);Click("다른 이야기");Click("물건 이야기");Click("펜 잠깐 빌려도 될까?");Ticks(100);
            Click("그만 이야기하기");Assert.That(hud.CurrentScreen,Is.EqualTo(1));Assert.That(runtime.ReadConversationPlayback().Phase,Is.EqualTo("Idle"));Assert.That(runtime.ReadItemExchangeChoices("CH_06").CanReceive,Is.False);
            Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_06"));Assert.That(runtime.CaptureSession().ItemExchange.Loans,Is.Empty);yield return null;
        }
        [UnityTest]public IEnumerator OccupiedHandsReceiveARefusalInsteadOfAHiddenTransfer()
        {
            var book=runtime.ObjectBodies.Single(o=>o.ObjectId=="M_BOOK");book.transform.position=new Vector3(-3.5f,.4f,-5.3f);runtime.World.Object("M_BOOK").Position=MansionRuntime.P(book.transform.position);Physics.SyncTransforms();Assert.That(runtime.Interact("M_BOOK"),Is.EqualTo("Committed"));
            hud.Primary("CH_06");MansionUiTestEntry.FinishSpeech(runtime,hud);Click("다른 이야기");Click("물건 이야기");Click("펜 잠깐 빌려도 될까?");MansionUiTestEntry.FinishSpeech(runtime,hud);
            Assert.That(runtime.ReadConversationPlayback().HeardText,Does.Contain("먼저 손에 든 물건"));Assert.That(runtime.ReadItemExchangeChoices("CH_06").CanReceive,Is.False);
            Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_06"));Assert.That(runtime.World.Resident("CH_01").HeldObject,Is.EqualTo("M_BOOK"));yield return null;
        }
        [UnityTest]public IEnumerator SpokenPermissionPhysicalTransferAndDirectReturnCompleteThroughRealButtons()
        {
            Assert.That(runtime.ReceivePen("CH_06"),Does.Contain("먼저"));BeginBorrow();long start=runtime.World.Tick;
            Ticks(89);Assert.That(runtime.World.Resident("CH_06").HeldObject,Is.EqualTo(Pen));Assert.That(runtime.CaptureSession().ItemExchange.Loans,Is.Empty);
            Ticks(1);Assert.That(runtime.World.Resident("CH_01").HeldObject,Is.EqualTo(Pen));Assert.That(runtime.CaptureSession().ItemExchange.Loans.Single().StartedTick,Is.EqualTo(start+90));
            Ticks(30);Assert.That(runtime.ReadItemExchange().Running,Is.False);
            hud.Primary("CH_06");MansionUiTestEntry.FinishSpeech(runtime,hud);Click("다른 이야기");Click("물건 이야기");Click("어디에 돌려주면 돼?");MansionUiTestEntry.FinishSpeech(runtime,hud);Click("빌린 펜 돌려주기");FinishApproach();Ticks(120);
            Assert.That(runtime.World.Resident("CH_06").HeldObject,Is.EqualTo(Pen));Assert.That(runtime.World.Resident("CH_01").HeldObject,Is.Empty);
            Assert.That(runtime.CaptureSession().ItemExchange.Loans.Single().Status,Is.EqualTo("Returned"));Assert.That(runtime.World.Events.Count(e=>e.Type=="ObjectHandedOver"),Is.EqualTo(2));
            Assert.That(runtime.ReadNotebook().Records.Count(r=>r.Predicate=="HandedObject"&&r.Value==Pen),Is.EqualTo(2));
            Assert.That(runtime.ReadNotebook().Records.Where(r=>r.Predicate=="HandedObject").All(r=>r.PlaceId==runtime.Layout.RoomAt(runtime.Bodies.Single(b=>b.ActorId=="CH_01").transform.position).RoomId),Is.True,"Hand height must not be mistaken for an unknown floor.");
            Assert.That(runtime.Knowledge.For("CH_18").Records().Any(r=>r.Predicate=="HandedObject"),Is.False,"Remote NPCs get no transfer information.");
            Assert.That(runtime.CaptureSession().Incidents,Is.Empty,"A peaceful loan must not open a lethal case.");yield return null;
        }
        [UnityTest]public IEnumerator CancellationBeforeAndAfterCommitKeepsExactlyOnePhysicalHolder()
        {
            BeginBorrow();Ticks(35);runtime.CancelItemExchange();Assert.That(runtime.World.Resident("CH_06").HeldObject,Is.EqualTo(Pen));Assert.That(runtime.CaptureSession().ItemExchange.Loans,Is.Empty);
            Assert.That(runtime.ReceivePen("CH_06"),Does.Contain("건네"));FinishApproach();Ticks(95);runtime.SetMove(1,0);
            Assert.That(runtime.ReadItemExchange().Running,Is.False);Assert.That(runtime.World.Resident("CH_01").HeldObject,Is.EqualTo(Pen));Assert.That(runtime.World.Resident("CH_06").HeldObject,Is.Empty);
            Assert.That(runtime.CaptureSession().ItemExchange.Loans.Length,Is.EqualTo(1));Assert.That(runtime.World.Events.Count(e=>e.Type=="ObjectHandedOver"),Is.EqualTo(1));yield return null;
        }
        [UnityTest]public IEnumerator SweptItemCannotPassThroughSolidBarrier()
        {
            BeginBorrow();var barrier=GameObject.CreatePrimitive(PrimitiveType.Cube);barrier.transform.position=new Vector3(-4,.95f,-4.5f);barrier.transform.localScale=new Vector3(2,1,.1f);Physics.SyncTransforms();
            try{Ticks(120);Assert.That(runtime.CaptureSession().ItemExchange.Handoff.Phase,Is.EqualTo("Cancelled"));Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_06"));Assert.That(runtime.CaptureSession().ItemExchange.Loans,Is.Empty);}
            finally{UnityEngine.Object.Destroy(barrier);}yield return null;
        }
        [UnityTest]public IEnumerator SavingBeforeAndAfterCommitResumesWithoutDuplicateReceipts()
        {
            BeginBorrow();Ticks(45);runtime.Pause("TEST_PAUSE",true);runtime.SaveTo(runtime.SavePath);Ticks(80);Assert.That(runtime.CaptureSession().ItemExchange.Handoff.ElapsedTicks,Is.EqualTo(45));
            runtime.Pause("TEST_PAUSE",false);Ticks(75);var expected=runtime.ReadNotebook().Records.Where(r=>r.Predicate=="HandedObject").Select(r=>r.Id+"|"+r.Text+"|"+r.ReceivedTick).ToArray();
            runtime.LoadFrom(runtime.SavePath);hud.SyncPauseView();Assert.That(runtime.World.HasPause("TEST_PAUSE"),Is.True);runtime.Pause("TEST_PAUSE",false);Ticks(50);Assert.That(runtime.CaptureSession().ItemExchange.Handoff.Committed,Is.True);
            runtime.SaveTo(runtime.SavePath);Ticks(25);runtime.LoadFrom(runtime.SavePath);hud.SyncPauseView();Ticks(25);
            CollectionAssert.AreEqual(expected,runtime.ReadNotebook().Records.Where(r=>r.Predicate=="HandedObject").Select(r=>r.Id+"|"+r.Text+"|"+r.ReceivedTick).ToArray());
            Assert.That(runtime.World.Events.Count(e=>e.Type=="ObjectHandedOver"),Is.EqualTo(1));Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_01"));yield return null;
        }
        [UnityTest]public IEnumerator DroppingThePenDoesNotPretendTheLenderReceivedItAndRemoteReturnIsRejected()
        {
            BeginBorrow();Ticks(120);Assert.That(runtime.Interact("DROP"),Is.EqualTo("Committed"));
            Assert.That(runtime.ReadItemExchangeChoices("CH_06").CanReturn,Is.False);Assert.That(runtime.CaptureSession().ItemExchange.Loans.Single().Status,Is.EqualTo("Borrowed"));
            Assert.That(runtime.Interact(Pen),Is.EqualTo("Committed"));Place("CH_06",new Vector3(7,0,-7));Assert.That(runtime.ReturnBorrowedItem("CH_06"),Does.Contain("가까이"));
            Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_01"));yield return null;
        }
        [UnityTest]public IEnumerator InvalidCommitCursorIsRejectedWithoutChangingTheLiveSession()
        {
            BeginBorrow();Ticks(45);var snapshot=runtime.CaptureSession();snapshot.ItemExchange.Handoff.ElapsedTicks=95;string payload=JsonUtility.ToJson(snapshot);string path=Path.Combine(Path.GetDirectoryName(runtime.SavePath),"invalid-handoff.dat");File.WriteAllText(path,AtomicSaveStore.Hash(payload)+"\n"+payload);
            long tick=runtime.World.Tick;Assert.Throws<InvalidDataException>(()=>runtime.LoadFrom(path));Assert.That(runtime.World.Tick,Is.EqualTo(tick));Assert.That(runtime.CaptureSession().ItemExchange.Handoff.ElapsedTicks,Is.EqualTo(45));Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_06"));yield return null;
        }
        [UnityTest]public IEnumerator OldSaveAddsNewPropAtLoadWithoutInventingHistoricalLoans()
        {
            var old=runtime.CaptureSession();old.OptionalObjects&=~128;old.World.Objects=old.World.Objects.Where(o=>o.Id!=Pen).ToArray();old.World.Residents.Single(r=>r.Id=="CH_06").HeldObject="";
            string payload=JsonUtility.ToJson(old);string path=Path.Combine(Path.GetDirectoryName(runtime.SavePath),"old-content.dat");File.WriteAllText(path,AtomicSaveStore.Hash(payload)+"\n"+payload);
            runtime.LoadFrom(path);Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_06"));Assert.That(runtime.CaptureSession().ItemExchange.Loans,Is.Empty);Assert.That(runtime.World.Events.Last().Type,Is.EqualTo("ContentObjectAdded"));
            runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);Assert.That(runtime.World.Events.Count(e=>e.Type=="ContentObjectAdded"),Is.EqualTo(1));yield return null;
        }
        [UnityTest]public IEnumerator HandsMeetOnThePenWithoutStretchingBonesOrDetachingTheOwnedProp()
        {
            BeginBorrow();var pen=runtime.ObjectBodies.Single(o=>o.ObjectId==Pen);
            var player=runtime.Bodies.Single(b=>b.ActorId=="CH_01");var lender=runtime.Bodies.Single(b=>b.ActorId=="CH_06");
            foreach(var body in new[]{player,lender})Assert.That(body.RightHand.GetComponentInParent<BASSLINE.AuthoringData.ActorArmRig>(),Is.Not.Null,"Grip must belong to the rendered articulated arm.");
            for(int i=1;i<=120;i++){
                Ticks(1);Assert.That(runtime.CaptureSession().ItemExchange.Handoff.Phase,Is.Not.EqualTo("Cancelled"),runtime.ReadItemExchange().Text);
                var owner=runtime.World.Object(Pen).Owner=="CH_01"?player:lender;
                Assert.That(Vector3.Distance(pen.transform.position,owner.RightHand.position),Is.LessThan(.041f),"The held pen must stay touching its physical holder at tick "+i);
                foreach(var arm in new[]{player,lender}.SelectMany(b=>b.GetComponentsInChildren<BASSLINE.AuthoringData.ActorArmRig>())){
                    Assert.That(Vector3.Distance(arm.Upper.position,arm.Forearm.position),Is.EqualTo(arm.UpperLength).Within(.0001f));
                    Assert.That(Vector3.Distance(arm.Forearm.position,arm.Wrist.position),Is.EqualTo(arm.ForearmLength).Within(.0001f));
                    Assert.That(arm.Upper.localScale,Is.EqualTo(Vector3.one));Assert.That(arm.Forearm.localScale,Is.EqualTo(Vector3.one));
                }
                if(i>=60&&i<=90){Assert.That(Vector3.Distance(player.RightHand.position,lender.RightHand.position),Is.EqualTo(.08f).Within(.002f));Assert.That(Vector3.Distance(player.RightHand.position,pen.transform.position),Is.LessThan(.041f));}
            }yield return null;
        }
        [UnityTest]public IEnumerator ApproachUsesCapsuleMovementAndRestoresItsActualPositionFromSave()
        {
            OfferThroughUi();Click("펜 받기");var npc=runtime.Bodies.Single(b=>b.ActorId=="CH_06");var player=runtime.Bodies.Single(b=>b.ActorId=="CH_01");var origin=npc.transform.position;var playerOrigin=player.transform.position;
            for(int i=0;i<8;i++){var before=npc.transform.position;Ticks(1);var displacement=npc.transform.position-before;Assert.That(Mathf.Abs(displacement.y),Is.LessThan(.03f),"Capsule ground settling stays within its skin width.");displacement.y=0;Assert.That(displacement.magnitude,Is.LessThan(.0181f),"Approach must be walked at the configured speed.");}
            Assert.That(Vector3.Distance(origin,npc.transform.position),Is.GreaterThan(.1f));Assert.That(Vector3.Distance(playerOrigin,player.transform.position),Is.LessThan(.01f));
            Assert.That(runtime.CaptureSession().ItemExchange.Handoff.Phase,Is.EqualTo("Approaching"));var position=npc.transform.position;var grip=npc.RightHand.position;
            runtime.SaveTo(runtime.SavePath);FinishApproach();Ticks(120);runtime.LoadFrom(runtime.SavePath);hud.SyncPauseView();
            Assert.That(Vector3.Distance(npc.transform.position,position),Is.LessThan(.0001f));Assert.That(Vector3.Distance(npc.RightHand.position,grip),Is.LessThan(.0001f));
            FinishApproach();Ticks(120);Assert.That(runtime.World.Events.Count(e=>e.Type=="ObjectHandedOver"),Is.EqualTo(1));yield return null;
        }
        [UnityTest]public IEnumerator MidReachPoseRestoresExactlyAndLookingAroundDoesNotDetachHands()
        {
            BeginBorrow();Ticks(45);var npc=runtime.Bodies.Single(b=>b.ActorId=="CH_06");var player=runtime.Bodies.Single(b=>b.ActorId=="CH_01");
            var hand=npc.RightHand.position;var elbow=npc.GetComponentsInChildren<BASSLINE.AuthoringData.ActorArmRig>().Single(a=>a.Side==1).Forearm.position;
            runtime.SaveTo(runtime.SavePath);Ticks(75);runtime.LoadFrom(runtime.SavePath);hud.SyncPauseView();
            Assert.That(Vector3.Distance(hand,npc.RightHand.position),Is.LessThan(.0001f));Assert.That(Vector3.Distance(elbow,npc.GetComponentsInChildren<BASSLINE.AuthoringData.ActorArmRig>().Single(a=>a.Side==1).Forearm.position),Is.LessThan(.0001f));
            var facing=player.transform.rotation;runtime.SetLook(45,30);Ticks(1);Assert.That(Quaternion.Angle(facing,player.transform.rotation),Is.LessThan(.001f));
            Ticks(74);Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_01"));Assert.That(runtime.ReadItemExchange().Running,Is.False);yield return null;
        }
        [UnityTest]public IEnumerator BlockedApproachDoesNotTeleportThroughFurniture()
        {
            OfferThroughUi();Click("펜 받기");var npc=runtime.Bodies.Single(b=>b.ActorId=="CH_06");var origin=npc.transform.position;
            var barrier=GameObject.CreatePrimitive(PrimitiveType.Cube);barrier.transform.position=new Vector3(-4,.9f,-4.6f);barrier.transform.localScale=new Vector3(2,1.8f,.12f);Physics.SyncTransforms();
            try{Ticks(181);Assert.That(runtime.CaptureSession().ItemExchange.Handoff.Phase,Is.EqualTo("Cancelled"));Assert.That(npc.transform.position.z,Is.GreaterThan(-4.4f));Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_06"));Assert.That(runtime.CaptureSession().ItemExchange.Loans,Is.Empty);}
            finally{UnityEngine.Object.Destroy(barrier);}yield return null;
        }
        [UnityTest]public IEnumerator PassingResidentDoesNotHidePermissionButStillBlocksPhysicalTransfer()
        {
            OfferThroughUi();Place("CH_18",new Vector3(-4,0,-4.65f));
            Assert.That(runtime.ReadItemExchangeChoices("CH_06").CanReceive,Is.True,"A passer-by must not erase the heard offer or hide the receive action while reading is paused.");
            Click("펜 받기");Ticks(181);
            Assert.That(runtime.CaptureSession().ItemExchange.Handoff.Phase,Is.EqualTo("Cancelled"));Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_06"));Assert.That(runtime.CaptureSession().ItemExchange.Loans,Is.Empty);
            yield return null;
        }
    }
}
