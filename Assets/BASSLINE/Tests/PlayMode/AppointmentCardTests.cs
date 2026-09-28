using System;
using System.Collections;
using System.IO;
using System.Linq;
using BASSLINE.AuthoringData;
using BASSLINE.Bootstrap;
using BASSLINE.Save;
using BASSLINE.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace BASSLINE.Tests
{
    public sealed class AppointmentCardTests
    {
        MansionRuntime runtime;FixtureHud hud;
        [UnitySetUp]public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;
            runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;runtime.UseIsolatedTestStorage();
            hud=UnityEngine.Object.FindAnyObjectByType<FixtureHud>();MansionUiTestEntry.Start(hud);hud.enabled=false;
            Place("CH_02",new Vector3(-4,0,-4));Place("CH_01",new Vector3(-4,0,-5.3f));runtime.SetLook(0,0);
        }
        void Place(string id,Vector3 position){var b=runtime.Bodies.Single(b=>b.ActorId==id);b.Capsule.enabled=false;b.transform.position=position;b.Capsule.enabled=true;var actor=runtime.World.Resident(id);actor.Position=MansionRuntime.P(position);actor.Node=runtime.Layout.NavigationNodes[runtime.Layout.NearestNode(position)].Id;Physics.SyncTransforms();}
        void Ticks(int count){for(int i=0;i<count;i++)runtime.AdvanceOne();}
        void FinishSpeech(){for(int i=0;i<1000&&runtime.ReadConversationPlayback().Speaking;i++)runtime.AdvanceOne();Assert.That(runtime.ReadConversationPlayback().Phase,Is.EqualTo("Reading"));runtime.EndConversation();}
        string Agree(long delay=18000){Assert.That(runtime.Invite("CH_02","R_DINING",delay),Is.EqualTo("Dialogue"));FinishSpeech();return runtime.Social.For("CH_01",runtime.World.Tick).Single().Id;}
        void AtCard()
        {
            var paper=runtime.ObjectBodies.Single(o=>o.ObjectId==AppointmentDesk.CardId).transform;
            var player=runtime.Bodies.Single(b=>b.ActorId=="CH_01");
            foreach(var node in runtime.Layout.NavigationNodes.Where(n=>paper.position.y-n.Position.y>.3f&&paper.position.y-n.Position.y<1.2f&&Vector3.Distance(n.Position,paper.position)<2f).OrderBy(n=>Vector3.Distance(n.Position,paper.position))){
                var pos=node.Position;
                if(Physics.OverlapCapsule(pos+Vector3.up*.32f,pos+Vector3.up*(player.Height-.3f),.255f,~0,QueryTriggerInteraction.Ignore).Any(c=>!c.transform.IsChildOf(player.transform)))continue;
                if(!Physics.Raycast(pos+Vector3.up*.35f,Vector3.down,.6f,~0,QueryTriggerInteraction.Ignore))continue;
                Place("CH_01",pos);if(runtime.ReadAppointmentCard().Nearby)return;
            }
            Assert.Fail("The card must have a reachable approach on its actual floor without intersecting furniture.");
        }
        void NearNpc()
        {
            var npc=runtime.Bodies.Single(b=>b.ActorId=="CH_02");
            foreach(var offset in new[]{Vector3.back,Vector3.left,Vector3.right,Vector3.forward}){
                Place("CH_01",npc.transform.position+offset*1.3f);var direction=npc.Head.position-runtime.Bodies.Single(b=>b.ActorId=="CH_01").Head.position;var angle=Quaternion.LookRotation(direction).eulerAngles;runtime.SetLook(angle.y,Mathf.DeltaAngle(0,angle.x));
                if(runtime.DescribeTarget("CH_02").Available&&runtime.ReceivesSpeech("CH_01","CH_02"))return;
            }Assert.Fail("No physical nearby dialogue approach");
        }
        [UnityTest]public IEnumerator PrivateWritingLeavesTheGuestOnTheOldRouteUntilTheChangeIsActuallySpoken()
        {
            string id=Agree(3600);AtCard();Assert.That(runtime.WriteAppointmentCard(id,"R_LIBRARY",18000),Does.Contain("쓰고"));Ticks(180);
            Assert.That(runtime.Social.For("CH_01",runtime.World.Tick).Single().Revision,Is.EqualTo(2));Assert.That(runtime.Social.For("CH_02",runtime.World.Tick).Single().Revision,Is.EqualTo(1));
            var written=runtime.Social.Capture().Appointments.Single(a=>a.Revision==2);Assert.That(runtime.Knowledge.For("CH_02").Records().Any(r=>r.RootId==written.WrittenRecordId),Is.False);
            var npc=runtime.Bodies.Single(b=>b.ActorId=="CH_02");bool oldArrival=false;
            for(int i=0;i<9000;i++){runtime.AdvanceOne();if(runtime.World.Tick>=3600&&runtime.Layout.RoomAt(npc.transform.position)?.RoomId=="R_DINING"){oldArrival=true;break;}}
            Assert.That(oldArrival,Is.True,"An unread private change must not reroute the invited resident.");Assert.That(runtime.Social.For("CH_01",runtime.World.Tick).Single().State,Is.Not.EqualTo("Met"));
            NearNpc();long sent=runtime.World.Tick;Assert.That(runtime.TellChangedAppointment("CH_02"),Is.EqualTo("Dialogue"));Ticks(359);Assert.That(runtime.Social.For("CH_02",runtime.World.Tick).Single().Revision,Is.EqualTo(1));Ticks(1);
            Assert.That(runtime.Social.For("CH_02",runtime.World.Tick).Single().Revision,Is.EqualTo(2));Assert.That(runtime.Social.For("CH_02",runtime.World.Tick).Single().ReceivedTick,Is.EqualTo(sent+360));FinishSpeech();
            Assert.That(runtime.Social.For("CH_02",runtime.World.Tick).Single().State,Is.EqualTo("Agreed"));Assert.That(runtime.Social.HistoryFor("CH_02",runtime.World.Tick).Length,Is.EqualTo(2));
            runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);Assert.That(runtime.Social.For("CH_02",runtime.World.Tick).Single().PlaceId,Is.EqualTo("R_LIBRARY"));yield return null;
        }
        [UnityTest]public IEnumerator WritingCanBeInterruptedAndSaveResumesWithoutCreatingAnotherVersion()
        {
            string id=Agree();AtCard();runtime.WriteAppointmentCard(id,"R_LIBRARY",24000);Ticks(70);runtime.CancelAppointmentCard();Assert.That(runtime.Social.Capture().Appointments.Length,Is.EqualTo(1));
            runtime.WriteAppointmentCard(id,"R_LIBRARY",24000);Ticks(90);runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);Assert.That(runtime.ReadAppointmentCard().Writing,Is.True);Ticks(90);
            Assert.That(runtime.Social.Capture().Appointments.Length,Is.EqualTo(2));Assert.That(runtime.World.Capture().Events.Count(e=>e.Type=="InvitationWritten"),Is.EqualTo(1));string text=runtime.ReadAppointmentCard().Text;
            runtime.WriteAppointmentCard(id,"R_GREEN",27000);Ticks(40);runtime.SetMove(1,0);Assert.That(runtime.ReadAppointmentCard().Writing,Is.False);Assert.That(runtime.ReadAppointmentCard().Text,Is.EqualTo(text));runtime.SetMove(0,0);
            runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);Assert.That(runtime.Social.Capture().Appointments.Length,Is.EqualTo(2));yield return null;
        }
        [UnityTest]public IEnumerator APartialRelayDoesNotDeliverTheCardAndMidRelaySavePreservesTheBoundary()
        {
            string id=Agree();AtCard();runtime.WriteAppointmentCard(id,"R_LIBRARY",24000);Ticks(180);var original=runtime.Social.Capture().Appointments.Single(a=>a.Revision==2);NearNpc();
            runtime.TellChangedAppointment("CH_02");Ticks(100);runtime.EndConversation();Assert.That(runtime.Knowledge.For("CH_02").Records().Any(r=>r.RootId==original.WrittenRecordId),Is.False);
            runtime.TellChangedAppointment("CH_02");Ticks(100);runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);Ticks(260);Assert.That(runtime.Social.For("CH_02",runtime.World.Tick).Single().Revision,Is.EqualTo(2));FinishSpeech();
            Assert.That(runtime.Knowledge.For("CH_02").Records().Count(r=>r.RootId==original.WrittenRecordId),Is.EqualTo(1));yield return null;
        }
        [UnityTest]public IEnumerator CopyingAnAlreadyDeliveredCardPreservesTheOriginalReceipt()
        {
            string id=Agree();AtCard();runtime.WriteAppointmentCard(id,"R_LIBRARY",24000);Ticks(180);NearNpc();runtime.TellChangedAppointment("CH_02");FinishSpeech();
            Assert.That(runtime.HasWrittenAppointmentChange("CH_02"),Is.False,"Already answered changes should not remain a conversation prompt.");
            var original=runtime.Social.Capture().Appointments.Single(a=>a.Revision==2);AtCard();runtime.WriteAppointmentCard(id,"R_LIBRARY",24000);Ticks(180);
            var copied=runtime.Social.Capture().Appointments.Single(a=>a.Revision==2);Assert.That(copied.WrittenRecordId,Is.EqualTo(original.WrittenRecordId));Assert.That(copied.WrittenTick,Is.EqualTo(original.WrittenTick));
            runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);Assert.That(runtime.Social.For("CH_02",runtime.World.Tick).Single().State,Is.EqualTo("Agreed"));
            var invalid=runtime.CaptureSession();invalid.Social.Appointments.Single(a=>a.Revision==2).PlaceId="R_GREEN";string json=JsonUtility.ToJson(invalid);File.WriteAllText(runtime.SavePath,AtomicSaveStore.Hash(json)+"\n"+json);
            Assert.Throws<InvalidDataException>(()=>runtime.LoadFrom(runtime.SavePath));yield return null;
        }
        [UnityTest]public IEnumerator OlderSaveGainsOnlyABlankCardAndNoHistoricalInvitation()
        {
            var old=runtime.CaptureSession();old.OptionalObjects&=~512;old.Social.Card=null;old.World.Objects=old.World.Objects.Where(o=>o.Id!=AppointmentDesk.CardId).ToArray();string json=JsonUtility.ToJson(old);File.WriteAllText(runtime.SavePath,AtomicSaveStore.Hash(json)+"\n"+json);
            runtime.LoadFrom(runtime.SavePath);Assert.That(runtime.World.Capture().Objects.Count(o=>o.Id==AppointmentDesk.CardId),Is.EqualTo(1));Assert.That(runtime.Social.Capture().Appointments,Is.Empty);Assert.That(runtime.Social.Card.AppointmentId,Is.Empty);
            Assert.That(runtime.World.Capture().Events.Count(e=>e.Type=="ContentObjectAdded"&&e.Target==AppointmentDesk.CardId),Is.EqualTo(1));runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);Assert.That(runtime.World.Capture().Events.Count(e=>e.Type=="ContentObjectAdded"&&e.Target==AppointmentDesk.CardId),Is.EqualTo(1));yield return null;
        }
        void Click(int page,string prefix){var view=hud.GetComponentsInChildren<ProductionScreenView>(true).Single(v=>v.ScreenId=="UI_"+page.ToString("00"));var b=view.Actions.Select((b,i)=>new{b,label=view.ActionLabels[i].text}).Single(x=>x.b.gameObject.activeSelf&&x.label.StartsWith(prefix,StringComparison.Ordinal));b.b.onClick.Invoke();}
        [UnityTest]public IEnumerator PhysicalCardReadAndWriteButtonsRunWithoutAHiddenMenuPause()
        {
            Agree();AtCard();hud.Primary(AppointmentDesk.CardId);Assert.That(hud.CurrentScreen,Is.EqualTo(14));Ticks(120);hud.enabled=true;yield return null;hud.enabled=false;Assert.That(hud.CurrentScreen,Is.EqualTo(15));Click(15,"카드에 약속 적기");Assert.That(hud.CurrentScreen,Is.EqualTo(4));Assert.That(runtime.World.Paused,Is.True);
            Click(4,"장소나 시간 바꿔 적기");var view=hud.GetComponentsInChildren<ProductionScreenView>(true).Single(v=>v.ScreenId=="UI_04");view.RecordRowButtons[Array.IndexOf(runtime.InvitationPlaceIds(),"R_LIBRARY")].onClick.Invoke();Click(4,"이 장소로 고쳐 쓰기");
            Assert.That(hud.CurrentScreen,Is.EqualTo(1));Assert.That(runtime.World.Paused,Is.False);Assert.That(runtime.ReadAppointmentCard().Writing,Is.True);Ticks(180);Assert.That(runtime.ReadAppointmentCard().Text,Does.Contain("원형 도서관"));yield return null;
        }
    }
}
