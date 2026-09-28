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
    public sealed class ConversationPlaybackTests
    {
        MansionRuntime runtime;FixtureHud hud;
        [UnitySetUp]public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;
            runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;runtime.UseIsolatedTestStorage();
            hud=UnityEngine.Object.FindAnyObjectByType<FixtureHud>();MansionUiTestEntry.Start(hud);hud.enabled=false;
            Place("CH_04",new Vector3(-4,0,-4));Place("CH_01",new Vector3(-4,0,-5.3f));Place("CH_03",new Vector3(7,0,-7));runtime.SetLook(0,0);Physics.SyncTransforms();
        }
        void Place(string id,Vector3 pos){var b=runtime.Bodies.Single(x=>x.ActorId==id);b.Capsule.enabled=false;b.transform.position=pos;b.Capsule.enabled=true;runtime.World.Resident(id).Position=MansionRuntime.P(pos);Physics.SyncTransforms();}
        void Ticks(int n){for(int i=0;i<n;i++)runtime.AdvanceOne();}
        [UnityTest]public IEnumerator CourtEntryClosesConversationAndReleasesItsReadingToken()
        {
            hud.Primary("CH_04");MansionUiTestEntry.FinishSpeech(runtime,hud);Assert.That(runtime.World.HasPause("K_DIALOGUE_READ"),Is.True);
            runtime.ConfigureTrialUiReview();
            typeof(FixtureHud).GetMethod("TickTrial",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(hud,null);
            Assert.That(hud.CurrentScreen,Is.EqualTo(23));Assert.That(runtime.ReadConversationPlayback().Phase,Is.EqualTo("Idle"));
            Assert.That(runtime.World.HasPause("K_DIALOGUE_READ"),Is.False);Assert.That(runtime.World.HasPause("M_COURT"),Is.True);yield return null;
        }
        [UnityTest]public IEnumerator QuickListeningRunsAllWorldTicksAndPreservesReceiptsAndResidentMovement()
        {
            hud.Primary("CH_04");Ticks(60);runtime.SaveTo(runtime.SavePath);MansionUiTestEntry.FinishSpeech(runtime,hud);
            long normalTick=runtime.World.Tick;var normal=runtime.World.Residents.Select(a=>new{a.Id,a.Position,a.Phase,a.ScheduleCursor}).ToArray();
            var receipts=runtime.Knowledge.Capture().Memories.Select(m=>m.OwnerId+"|"+m.Record.Id+"|"+m.Record.Text+"|"+m.Record.ReceivedTick).ToArray();
            runtime.LoadFrom(runtime.SavePath);hud.SyncPauseView();runtime.FinishListeningQuickly();
            var batch=typeof(MansionRuntime).GetMethod("AdvanceConversationFastBatch",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            for(int frame=0;frame<30&&runtime.ReadConversationPlayback().Speaking;frame++)batch.Invoke(runtime,null);
            Assert.That(runtime.ReadConversationPlayback().Phase,Is.EqualTo("Reading"));Assert.That(runtime.World.Tick,Is.EqualTo(normalTick));
            CollectionAssert.AreEqual(receipts,runtime.Knowledge.Capture().Memories.Select(m=>m.OwnerId+"|"+m.Record.Id+"|"+m.Record.Text+"|"+m.Record.ReceivedTick).ToArray());
            foreach(var expected in normal){var actor=runtime.World.Resident(expected.Id);Assert.That(actor.Position.Distance(expected.Position),Is.LessThan(.002),actor.Id);Assert.That(actor.Phase,Is.EqualTo(expected.Phase));Assert.That(actor.ScheduleCursor,Is.EqualTo(expected.ScheduleCursor));}
            yield return null;
        }
        [UnityTest]public IEnumerator PassingResidentDoesNotBlockNearbyVoiceButSolidBarrierDoes()
        {
            hud.Primary("CH_04");Ticks(30);Place("CH_03",new Vector3(-4,0,-4.65f));
            var other=runtime.World.Resident("CH_03");other.Phase="Performing";other.Activity="Rest";other.ActivityTicks=3000;
            Assert.That(runtime.ReceivesSpeech("CH_01","CH_04"),Is.True);Ticks(30);Assert.That(runtime.ReadConversationPlayback().Speaking,Is.True);
            Assert.That(runtime.DescribeTarget("CH_04").Available,Is.False,"The passing resident obscures the interaction ray in this regression setup.");
            hud.enabled=true;yield return null;
            Assert.That(hud.CurrentScreen,Is.EqualTo(3),"The active HUD must not close a conversation merely because a passerby obscures the speaker.");
            Assert.That(runtime.ReadConversationPlayback().Speaking,Is.True);hud.enabled=false;
            Place("CH_03",new Vector3(-2.5f,0,-4));
            var barrier=GameObject.CreatePrimitive(PrimitiveType.Cube);barrier.name="TestOnly solid acoustic barrier";barrier.transform.position=new Vector3(-4,1,-4.65f);barrier.transform.localScale=new Vector3(2,2,.1f);Physics.SyncTransforms();
            try{Assert.That(runtime.ReceivesSpeech("CH_01","CH_04"),Is.False);Ticks(1);Assert.That(runtime.ReadConversationPlayback().Phase,Is.EqualTo("Interrupted"));}
            finally{UnityEngine.Object.Destroy(barrier);}yield return null;
        }
        [UnityTest]public IEnumerator FutureWordsStayPrivatePauseDoesNotSpendTimeAndLoadingResumesExactly()
        {
            hud.Primary("CH_04");Assert.That(runtime.World.Paused,Is.False);Ticks(120);
            var speech=runtime.ReadConversationPlayback();string heard=speech.HeardText;
            Assert.That(heard.Length,Is.GreaterThan(0));Assert.That(runtime.ReadNotebook().Records.Any(r=>r.Predicate=="SaidStatement"&&r.Source=="CH_04"),Is.False);
            Assert.That(runtime.CaptureSession().Conversation.PlannedText,Does.StartWith(heard));
            long tick=runtime.World.Tick;hud.Open(10);string memories=JsonUtility.ToJson(runtime.Knowledge.Capture());Ticks(180);
            Assert.That(runtime.World.Tick,Is.EqualTo(tick));Assert.That(JsonUtility.ToJson(runtime.Knowledge.Capture()),Is.EqualTo(memories));
            runtime.SaveTo(runtime.SavePath);hud.Back();Ticks(30);runtime.LoadFrom(runtime.SavePath);hud.SyncPauseView();
            Assert.That(runtime.ReadConversationPlayback().HeardText,Is.EqualTo(heard));Assert.That(runtime.CaptureSession().Conversation.ElapsedTicks,Is.EqualTo(120));
            hud.Back();MansionUiTestEntry.FinishSpeech(runtime,hud);
            Assert.That(runtime.World.Tick,Is.EqualTo(360));Assert.That(runtime.World.Paused,Is.True);Ticks(120);Assert.That(runtime.World.Tick,Is.EqualTo(360));
            var completed=runtime.Knowledge.Capture().Memories.Select(m=>m.OwnerId+"|"+m.Record.Id+"|"+m.Record.RootId+"|"+m.Record.Text+"|"+m.Record.ReceivedTick).ToArray();runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);hud.SyncPauseView();Ticks(60);
            CollectionAssert.AreEqual(completed,runtime.Knowledge.Capture().Memories.Select(m=>m.OwnerId+"|"+m.Record.Id+"|"+m.Record.RootId+"|"+m.Record.Text+"|"+m.Record.ReceivedTick).ToArray());hud.Back();Assert.That(runtime.World.Paused,Is.False);yield return null;
        }
        [UnityTest]public IEnumerator LateListenerGetsOnlyAudibleSuffixAndDoesNotAcquireEarlierWords()
        {
            hud.Primary("CH_04");Ticks(180);long joined=runtime.World.Tick;
            Place("CH_03",new Vector3(-2.5f,0,-4));var resident=runtime.World.Resident("CH_03");resident.Phase="Performing";resident.Activity="Rest";resident.ActivityTicks=3000;
            Assert.That(runtime.ReceivesSpeech("CH_03","CH_04"),Is.True);MansionUiTestEntry.FinishSpeech(runtime,hud);
            var record=runtime.Knowledge.For("CH_03").Records().Single(r=>r.ProvenanceKey==runtime.CaptureSession().Conversation.Id);
            Assert.That(record.Predicate,Is.EqualTo("HeardFragment"));Assert.That(record.Text,Does.StartWith("… "));Assert.That(record.FromTick,Is.GreaterThan(joined));
            Assert.That(record.Text,Is.Not.EqualTo(runtime.ReadConversationPlayback().HeardText));yield return null;
        }
        [UnityTest]public IEnumerator InterruptingKeepsOnlyWhatWasHeardAndRejectsCorruptPlaybackCursor()
        {
            hud.Primary("CH_04");Ticks(90);string heard=runtime.ReadConversationPlayback().HeardText;var saved=runtime.CaptureSession();saved.Conversation.EmittedCharacters+=4;
            string text=JsonUtility.ToJson(saved);string path=Path.Combine(Path.GetDirectoryName(runtime.SavePath),"invalid-speech.dat");File.WriteAllText(path,AtomicSaveStore.Hash(text)+"\n"+text);
            string before=JsonUtility.ToJson(runtime.CaptureSession());Assert.Throws<InvalidDataException>(()=>runtime.LoadFrom(path));Assert.That(JsonUtility.ToJson(runtime.CaptureSession()),Is.EqualTo(before));
            hud.Back();Assert.That(runtime.ReadConversationPlayback().Phase,Is.EqualTo("Idle"));Assert.That(runtime.World.Paused,Is.False);
            var record=runtime.ReadNotebook().Records.Single(r=>r.Source=="CH_04"&&r.Predicate=="HeardFragment");Assert.That(record.Text,Is.EqualTo(heard));
            Assert.That(runtime.World.Resident("CH_04").Activity,Is.Not.EqualTo("Talk"));yield return null;
        }
    }
}
