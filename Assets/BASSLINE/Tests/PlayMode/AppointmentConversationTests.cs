using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public sealed class AppointmentConversationTests
    {
        MansionRuntime runtime;FixtureHud hud;
        [UnitySetUp]public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;
            runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;runtime.UseIsolatedTestStorage();
            hud=UnityEngine.Object.FindAnyObjectByType<FixtureHud>();MansionUiTestEntry.Start(hud);hud.enabled=false;
            Place("CH_02",new Vector3(-4,0,-4));Place("CH_01",new Vector3(-4,0,-5.3f));runtime.SetLook(0,0);Physics.SyncTransforms();
        }
        void Place(string id,Vector3 position){var body=runtime.Bodies.Single(x=>x.ActorId==id);body.Capsule.enabled=false;body.transform.position=position;body.Capsule.enabled=true;var actor=runtime.World.Resident(id);actor.Position=MansionRuntime.P(position);actor.Node=runtime.Layout.NavigationNodes[runtime.Layout.NearestNode(position)].Id;Physics.SyncTransforms();}
        void Ticks(int n){for(int i=0;i<n;i++)runtime.AdvanceOne();}
        void Finish(){for(int i=0;i<1000&&runtime.ReadConversationPlayback().Speaking;i++)runtime.AdvanceOne();Assert.That(runtime.ReadConversationPlayback().Phase,Is.EqualTo("Reading"));}
        void Click(int screen,string prefix){var view=hud.GetComponentsInChildren<ProductionScreenView>(true).Single(v=>v.ScreenId=="UI_"+screen.ToString("00"));var action=view.Actions.Select((b,i)=>new{b,t=view.ActionLabels[i].text}).Single(x=>x.b.gameObject.activeSelf&&x.t.StartsWith(prefix,StringComparison.Ordinal));action.b.onClick.Invoke();}
        void RejectForged(Action<BASSLINE.Save.MansionSessionSnapshot> change)
        {
            var snapshot=runtime.CaptureSession();change(snapshot);string json=JsonUtility.ToJson(snapshot);string file=Path.Combine(Path.GetDirectoryName(runtime.SavePath),"forged-appointment.dat");File.WriteAllText(file,AtomicSaveStore.Hash(json)+"\n"+json);
            string before=JsonUtility.ToJson(runtime.CaptureSession());Assert.Throws<InvalidDataException>(()=>runtime.LoadFrom(file));Assert.That(JsonUtility.ToJson(runtime.CaptureSession()),Is.EqualTo(before));
        }
        [UnityTest]public IEnumerator OfferAndReplyMustBothFinishBeforeTheMeetingIsAgreed()
        {
            Assert.That(runtime.Invite("CH_02","R_LIBRARY",18000),Is.EqualTo("Dialogue"));Assert.That(runtime.Social.Capture().Appointments,Is.Empty);
            Assert.That(runtime.ReadConversationPlayback().SpeakerId,Is.EqualTo("CH_01"));Ticks(360);
            var proposal=runtime.Social.For("CH_01",runtime.World.Tick).Single();Assert.That(proposal.State,Is.EqualTo("Proposed"));Assert.That(runtime.Social.Due("CH_02",runtime.World.Tick),Is.Null);
            Assert.That(runtime.ReadConversationPlayback().SpeakerId,Is.EqualTo("CH_02"));Ticks(359);Assert.That(runtime.Social.For("CH_01",runtime.World.Tick).Single().State,Is.EqualTo("Proposed"));Ticks(1);
            Assert.That(runtime.Social.For("CH_01",runtime.World.Tick).Single().State,Is.EqualTo("Agreed"));Assert.That(runtime.Social.For("CH_02",runtime.World.Tick).Single().State,Is.EqualTo("Agreed"));
            var saved=runtime.Social.Capture().Appointments.Single();Assert.That(saved.ProposalReceivedTick,Is.EqualTo(360));Assert.That(saved.ReplyReceivedTick,Is.EqualTo(720));
            Assert.That(runtime.Knowledge.For("CH_01").Find(saved.ProposalRecordId).Predicate,Is.EqualTo("SaidStatement"));Assert.That(runtime.Knowledge.For("CH_02").Find(saved.ReplyRecordId).Predicate,Is.EqualTo("SaidStatement"));
            runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);Assert.That(runtime.Social.Capture().Appointments.Length,Is.EqualTo(1));yield return null;
        }
        [UnityTest]public IEnumerator InterruptedOfferCreatesNoAppointmentAndInterruptedReplyCanBeAskedAgain()
        {
            runtime.Invite("CH_02","R_LIBRARY",18000);Ticks(90);runtime.EndConversation();Assert.That(runtime.Social.Capture().Appointments,Is.Empty);
            runtime.Invite("CH_02","R_LIBRARY",18000);Ticks(420);runtime.EndConversation();Assert.That(runtime.Social.For("CH_01",runtime.World.Tick).Single().State,Is.EqualTo("Proposed"));
            runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);Assert.That(runtime.AskAboutAppointment("CH_02"),Is.EqualTo("Dialogue"));Finish();
            Assert.That(runtime.Social.Capture().Appointments.Length,Is.EqualTo(1));Assert.That(runtime.Social.For("CH_01",runtime.World.Tick).Single().State,Is.EqualTo("Agreed"));yield return null;
        }
        [UnityTest]public IEnumerator MidOfferAndMidReplyResumeWithoutDuplicatingReceiptsAndRejectUnheardAgreement()
        {
            runtime.Invite("CH_02","R_LIBRARY",18000);Ticks(80);runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);Ticks(340);
            RejectForged(s=>{var a=s.Social.Appointments.Single();a.AcceptedBy=new[]{"CH_01","CH_02"};a.ConfirmationReceivedBy=new[]{"CH_01","CH_02"};});
            RejectForged(s=>s.Conversation.Appointment.PlaceId="R_DINING");
            runtime.SaveTo(runtime.SavePath);Finish();string expected=JsonUtility.ToJson(runtime.Social.Capture());var memories=runtime.Knowledge.Capture().Memories;long tick=runtime.World.Tick;
            runtime.LoadFrom(runtime.SavePath);runtime.FinishListeningQuickly();var batch=typeof(MansionRuntime).GetMethod("AdvanceConversationFastBatch",BindingFlags.Instance|BindingFlags.NonPublic);for(int i=0;i<20&&runtime.ReadConversationPlayback().Speaking;i++)batch.Invoke(runtime,null);
            Assert.That(runtime.World.Tick,Is.EqualTo(tick));Assert.That(JsonUtility.ToJson(runtime.Social.Capture()),Is.EqualTo(expected));
            var loaded=runtime.Knowledge.Capture().Memories;Assert.That(loaded.Length,Is.EqualTo(memories.Length));
            for(int i=0;i<loaded.Length;i++){var a=loaded[i];var b=memories[i];Assert.That(a.OwnerId,Is.EqualTo(b.OwnerId));Assert.That(a.Record.Position.Distance(b.Record.Position),Is.LessThan(.00001),"Float-backed physics observation must stay within 10 micrometres.");a.Record.Position=b.Record.Position;Assert.That(JsonUtility.ToJson(a.Record),Is.EqualTo(JsonUtility.ToJson(b.Record)));}yield return null;
        }
        [UnityTest]public IEnumerator ConflictIsSpokenAndDoesNotBecomeAnAgreementOrABrokenPromise()
        {
            string other=runtime.Social.Propose("CH_03","CH_02","R_DINING",18000,6000,0);runtime.Social.Receive(other,1,"CH_02");runtime.Social.Accept(other,1,"CH_02",true);runtime.Social.ReceiveAcceptance(other,1,"CH_03");
            runtime.Invite("CH_02","R_LIBRARY",18000);Ticks(360);Assert.That(runtime.ReadConversationPlayback().HeardText,Is.Empty);Assert.That(runtime.Social.For("CH_01",runtime.World.Tick).Single().State,Is.EqualTo("Proposed"));Finish();
            Assert.That(runtime.ReadConversationPlayback().HeardText,Does.Contain("다른 시간이나 장소"));Assert.That(runtime.Social.For("CH_01",runtime.World.Tick).Single().State,Is.EqualTo("Declined"));
            Assert.That(runtime.Social.For("CH_02",runtime.World.Tick).Single(x=>x.Id==other).State,Is.EqualTo("Agreed"));Assert.That(runtime.Social.Experiences("CH_01").Any(e=>e.Kind=="PromiseBroken"),Is.False);runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);yield return null;
        }
        [UnityTest]public IEnumerator VisibleVenueRowsReturnToDialogueAndFreezeOnlyAfterTheAnswer()
        {
            hud.Primary("CH_02");MansionUiTestEntry.FinishSpeech(runtime,hud);Click(3,"다른 이야기");Click(3,"만날 시간 정하기");Assert.That(hud.CurrentScreen,Is.EqualTo(4));
            var picker=hud.GetComponentsInChildren<ProductionScreenView>(true).Single(v=>v.ScreenId=="UI_04");Assert.That(picker.RecordRowButtons.Length,Is.EqualTo(runtime.InvitationPlaceIds().Length));
            int index=Array.IndexOf(runtime.InvitationPlaceIds(),"R_LIBRARY");var originalButtons=picker.RecordRowButtons;picker.RecordRowButtons[index].onClick.Invoke();
            Assert.That(picker.RecordRowButtons[index].GetComponentInChildren<TMPro.TMP_Text>().text,Does.Contain("선택한 장소"));
            Assert.That(picker.RecordRowButtons.Count(b=>b.GetComponentInChildren<TMPro.TMP_Text>().text.Contains("선택한 장소")),Is.EqualTo(1));CollectionAssert.AreEqual(originalButtons,picker.RecordRowButtons,"Selecting a venue must preserve keyboard-focus targets.");Click(4,"5분 뒤");
            Assert.That(hud.CurrentScreen,Is.EqualTo(3));Assert.That(runtime.World.Paused,Is.False);Assert.That(runtime.ReadConversationPlayback().SpeakerId,Is.EqualTo("CH_01"));
            MansionUiTestEntry.FinishSpeech(runtime,hud);Assert.That(runtime.World.HasPause("K_DIALOGUE_READ"),Is.True);long tick=runtime.World.Tick;Ticks(120);Assert.That(runtime.World.Tick,Is.EqualTo(tick));
            Assert.That(runtime.Social.For("CH_01",tick).Single().PlaceId,Is.EqualTo("R_LIBRARY"));yield return null;
        }
        [UnityTest]public IEnumerator DeadDistantAndBusyTargetsCannotCreateOrOverwriteInvitationSpeech()
        {
            Assert.That(runtime.Invite("CH_01","R_LIBRARY",18000),Is.EqualTo("Unavailable"));Assert.That(runtime.Invite("CH_02","R_LIBRARY",1),Is.EqualTo("Unavailable"));
            runtime.World.Resident("CH_02").Alive=false;Assert.That(runtime.Invite("CH_02","R_LIBRARY",18000),Is.EqualTo("Unavailable"));runtime.World.Resident("CH_02").Alive=true;
            Place("CH_02",new Vector3(-4,0,4));Assert.That(runtime.Invite("CH_02","R_LIBRARY",18000),Is.EqualTo("Unavailable"));Place("CH_02",new Vector3(-4,0,-4));
            runtime.Invite("CH_02","R_LIBRARY",18000);string before=JsonUtility.ToJson(runtime.CaptureSession().Conversation);Assert.That(runtime.Invite("CH_02","R_DINING",36000),Is.EqualTo("Unavailable"));Assert.That(JsonUtility.ToJson(runtime.CaptureSession().Conversation),Is.EqualTo(before));Assert.That(runtime.Social.Capture().Appointments,Is.Empty);yield return null;
        }
        [UnityTest]public IEnumerator AcceptedSpokenAppointmentSendsTheResidentAlongTheActualRoute()
        {
            Assert.That(runtime.Invite("CH_02","R_DINING",3600),Is.EqualTo("Dialogue"));Finish();runtime.EndConversation();
            var npc=runtime.Bodies.Single(b=>b.ActorId=="CH_02");Vector3 start=npc.transform.position;bool arrived=false;
            for(int i=0;i<9000;i++){
                runtime.AdvanceOne();
                if(runtime.World.Tick>=3600&&runtime.Layout.RoomAt(npc.transform.position)?.RoomId=="R_DINING"){arrived=true;break;}
            }
            Assert.That(arrived,Is.True,"Confirmed invitation must drive the existing physical resident planner. "+JsonUtility.ToJson(runtime.World.Resident("CH_02")));Assert.That(Vector3.Distance(start,npc.transform.position),Is.GreaterThan(2));
            Assert.That(runtime.Social.For("CH_01",runtime.World.Tick).Single().State,Is.Not.EqualTo("Met"),"The absent player must not receive a remote meeting confirmation.");yield return null;
        }
    }
}
