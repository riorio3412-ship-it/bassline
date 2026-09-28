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
    public sealed class ResidentMeetingTests
    {
        MansionRuntime runtime;FixtureHud hud;Vector3 playerStart;
        [UnitySetUp]public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;
            runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;runtime.UseIsolatedTestStorage();hud=UnityEngine.Object.FindAnyObjectByType<FixtureHud>();MansionUiTestEntry.Start(hud);hud.enabled=false;
            playerStart=runtime.Bodies.Single(b=>b.ActorId=="CH_01").transform.position;
            runtime.Routines=new[]{new ResidentRoutine{ActorId="CH_02"},new ResidentRoutine{ActorId="CH_07"}};
            foreach(var actor in runtime.World.Residents)actor.NextSocialTick=60000;
            foreach(var id in new[]{"CH_02","CH_07"})runtime.World.Resident(id).NextSocialTick=0;
            Place("CH_02",new Vector3(-4,0,-4),0);Place("CH_07",new Vector3(-4,0,-2.7f),180);
        }
        void Place(string id,Vector3 position,float yaw=0)
        {
            var body=runtime.Bodies.Single(b=>b.ActorId==id);body.Capsule.enabled=false;body.transform.position=position;body.transform.rotation=Quaternion.Euler(0,yaw,0);body.Capsule.enabled=true;
            var actor=runtime.World.Resident(id);actor.Position=MansionRuntime.P(position);actor.Node=runtime.Layout.NavigationNodes[runtime.Layout.NearestNode(position)].Id;actor.Yaw=yaw;Physics.SyncTransforms();
        }
        void To(long tick){Assert.That(runtime.World.Paused,Is.False);while(runtime.World.Tick<tick)runtime.AdvanceOne();}
        void NearPlayer()=>Place("CH_01",new Vector3(-2.5f,0,-3.4f),270);
        ConversationPlaybackState[] Speeches()=>runtime.CaptureSession().ResidentConversations;
        ConversationPlaybackState BeginPair()
        {
            for(int cycle=1;cycle<=2;cycle++){
                foreach(var id in new[]{"CH_02","CH_07"})Assert.That(runtime.World.Plan(id,runtime.World.Resident(id).Node,"Rest",60),Is.EqualTo("Accepted"));
                for(int t=0;t<600&&new[]{"CH_02","CH_07"}.Any(id=>runtime.World.Resident(id).CompletedActivities<cycle);t++)runtime.AdvanceOne();
                foreach(var id in new[]{"CH_02","CH_07"})Assert.That(runtime.World.Resident(id).CompletedActivities,Is.GreaterThanOrEqualTo(cycle));
            }
            foreach(var id in new[]{"CH_02","CH_07"}){var actor=runtime.World.Resident(id);actor.Yaw=id=="CH_02"?0:180;runtime.Bodies.Single(b=>b.ActorId==id).transform.rotation=Quaternion.Euler(0,(float)actor.Yaw,0);}Physics.SyncTransforms();
            for(int t=0;t<240&&!Speeches().Any(s=>s.Phase=="Speaking");t++)runtime.AdvanceOne();
            var offer=Speeches().Single(s=>s.Phase=="Speaking");Assert.That(offer.Appointment.Stage,Is.EqualTo("Offer"));return offer;
        }
        [UnityTest]public IEnumerator ResidentsProposeAndReplyWithoutThePlayerAndSaveMidReply()
        {
            var offer=BeginPair();Assert.That(runtime.Social.Capture().Appointments,Is.Empty);To(offer.StartedTick+359);Assert.That(runtime.Social.Capture().Appointments,Is.Empty);To(offer.StartedTick+360);
            Assert.That(runtime.Social.For("CH_02",runtime.World.Tick).Single().State,Is.EqualTo("Proposed"));var reply=Speeches().Single(s=>s.Phase=="Speaking");To(reply.StartedTick+120);runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);To(reply.StartedTick+360);
            var a=runtime.Social.Capture().Appointments.Single();Assert.That(a.Organizer,Is.EqualTo("CH_02"));Assert.That(a.Invitee,Is.EqualTo("CH_07"));Assert.That(runtime.Social.For("CH_07",runtime.World.Tick).Single().State,Is.EqualTo("Agreed"));Assert.That(runtime.Social.For("CH_01",runtime.World.Tick),Is.Empty);
            Assert.That(runtime.Knowledge.For("CH_01").Records().Any(r=>r.Value!=null&&r.Value.StartsWith("AppointmentOffer:")),Is.False);Assert.That(runtime.ReadAmbientSpeech().HeardText,Is.Empty);Assert.That(runtime.World.Paused,Is.False);
            Assert.That(runtime.Knowledge.For("CH_02").Find(a.ProposalRecordId).ConversationWith,Is.EqualTo("CH_07"));runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);Assert.That(runtime.Social.Capture().Appointments.Length,Is.EqualTo(1));yield return null;
        }
        [UnityTest]public IEnumerator ArrivingDuringSpeechRevealsOnlyTheAudibleSuffixAndKeepsTheWorldRunning()
        {
            var offer=BeginPair();To(offer.StartedTick+150);NearPlayer();To(offer.StartedTick+230);var heard=runtime.ReadAmbientSpeech();Assert.That(heard.HeardText,Does.StartWith("… "));Assert.That(heard.HeardText,Is.Not.EqualTo(offer.PlannedText));
            hud.enabled=true;yield return null;hud.enabled=false;Assert.That(hud.CurrentScreen,Is.EqualTo(1));Assert.That(hud.AmbientCaption.transform.parent.gameObject.activeSelf,Is.True);Assert.That(runtime.World.Paused,Is.False);
            To(offer.StartedTick+360);var records=runtime.Knowledge.For("CH_01").Records().Where(r=>r.Value==offer.Value).ToArray();Assert.That(records.Length,Is.EqualTo(1));Assert.That(records[0].Predicate,Is.EqualTo("HeardFragment"));Assert.That(records[0].Text,Does.StartWith("… "));yield return null;
        }
        [UnityTest]public IEnumerator PlayerCanInterruptAnOngoingResidentConversationWithoutCreatingAnAgreement()
        {
            NearPlayer();var offer=BeginPair();To(offer.StartedTick+120);Assert.That(runtime.Talk("CH_02"),Is.EqualTo("Dialogue"));Assert.That(Speeches().Any(s=>s.Phase=="Speaking"),Is.False);Assert.That(runtime.Social.Capture().Appointments,Is.Empty);
            Assert.That(runtime.Knowledge.For("CH_07").Records().Single(r=>r.Value==offer.Value).Predicate,Is.EqualTo("HeardFragment"));runtime.EndConversation();runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);yield return null;
        }
        [UnityTest]public IEnumerator ChangedHearingConditionsStopTheActualExchangeAndPausePreservesItsCursor()
        {
            var offer=BeginPair();To(offer.StartedTick+90);runtime.Pause("K_TEST",true);for(int i=0;i<60;i++)runtime.AdvanceOne();Assert.That(Speeches().Single(s=>s.Phase=="Speaking").ElapsedTicks,Is.EqualTo(90));runtime.Pause("K_TEST",false);
            runtime.AnnouncementRoomIsSilent=room=>room=="R_HALL";runtime.AdvanceOne();Assert.That(Speeches().Single().Phase,Is.EqualTo("Interrupted"));Assert.That(runtime.Social.Capture().Appointments,Is.Empty);runtime.AnnouncementRoomIsSilent=null;yield return null;
        }
        [UnityTest]public IEnumerator ParticipantsActuallyReachSeparatePlacesAndMeetWithoutOneReservedNodeBlockingTheOther()
        {
            var offer=BeginPair();To(offer.StartedTick+720);var a=runtime.Social.For("CH_02",runtime.World.Tick).Single();Assert.That(a.State,Is.EqualTo("Agreed"));
            To(a.StartTick-7200+2400);Assert.That(runtime.World.Resident("CH_02").Destination,Is.Not.EqualTo(runtime.World.Resident("CH_07").Destination));
            To(a.StartTick+120);Assert.That(runtime.Social.For("CH_02",runtime.World.Tick).Single().State,Is.EqualTo("Met"));Assert.That(runtime.Social.For("CH_07",runtime.World.Tick).Single().State,Is.EqualTo("Met"));yield return null;
        }
        [UnityTest]public IEnumerator AskingForPlansSharesOnlyThatResidentsReceivedOriginalAndTamperedReplyIsRejected()
        {
            var offer=BeginPair();To(offer.StartedTick+400);var invalid=runtime.CaptureSession();invalid.ResidentConversations.Single(s=>s.Phase=="Speaking").Appointment.PlaceId="R_GREEN";string json=JsonUtility.ToJson(invalid);File.WriteAllText(runtime.SavePath,AtomicSaveStore.Hash(json)+"\n"+json);Assert.Throws<InvalidDataException>(()=>runtime.LoadFrom(runtime.SavePath));
            To(offer.StartedTick+720);NearPlayer();Assert.That(runtime.AskResidentPlans("CH_02"),Is.EqualTo("Dialogue"));for(int t=0;t<600&&runtime.ReadConversationPlayback().Speaking;t++)runtime.AdvanceOne();Assert.That(runtime.ReadConversationPlayback().HeardText,Does.Contain("유시온"));runtime.EndConversation();
            var a=runtime.Social.Capture().Appointments.Single();Assert.That(runtime.Knowledge.For("CH_01").Records().Any(r=>r.RootId==a.ProposalRecordId),Is.True);Assert.That(runtime.Social.For("CH_01",runtime.World.Tick),Is.Empty);yield return null;
        }
    }
}
