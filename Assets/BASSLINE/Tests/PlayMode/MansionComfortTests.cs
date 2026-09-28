using System;
using System.Collections;
using System.IO;
using System.Linq;
using BASSLINE.Bootstrap;
using BASSLINE.Core;
using BASSLINE.NPC;
using BASSLINE.Save;
using BASSLINE.UI;
using BASSLINE.World.Mansion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace BASSLINE.Tests
{
    public sealed class MansionComfortTests
    {
        MansionRuntime runtime;FixtureHud hud;string storage;
        [UnitySetUp] public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;
            runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;storage=runtime.UseIsolatedTestStorage();
            hud=UnityEngine.Object.FindAnyObjectByType<FixtureHud>();MansionUiTestEntry.Start(hud);hud.enabled=false;
        }
        ProductionScreenView View=>hud.GetComponentsInChildren<ProductionScreenView>(true).Single(v=>v.gameObject.activeInHierarchy);
        void Click(string label){var v=View;int i=Array.FindIndex(v.ActionLabels,t=>t.text==label);Assert.That(i,Is.GreaterThanOrEqualTo(0),label);Assert.That(v.Actions[i].gameObject.activeSelf,Is.True);v.Actions[i].onClick.Invoke();}
        void StageTalk()
        {
            foreach(var pair in new[]{new{Id="CH_04",P=new Vector3(-4,0,-4)},new{Id="CH_01",P=new Vector3(-4,0,-5.3f)}}){
                var b=runtime.Bodies.Single(x=>x.ActorId==pair.Id);b.Capsule.enabled=false;b.transform.position=pair.P;b.Capsule.enabled=true;runtime.World.Resident(pair.Id).Position=MansionRuntime.P(pair.P);
            }
            runtime.SetLook(0,0);Physics.SyncTransforms();hud.Primary("CH_04");Assert.That(hud.CurrentScreen,Is.EqualTo(3));
        }
        [UnityTest] public IEnumerator DialogueSpeakingUsesWorldTimeThenReadingFreezesAndChoicesRemainUsable()
        {
            Assert.That(runtime.WelcomeRead,Is.True);Assert.That(runtime.World.Paused,Is.False);
            runtime.Knowledge.Observe("CH_02",new KnownRecord{Text="PRIVATE_SENTINEL",SubjectId="CH_03",Predicate="AtPlace",FromTick=runtime.World.Tick,ToTick=runtime.World.Tick+1},runtime.World.Tick);
            StageTalk();MansionUiTestEntry.FinishSpeech(runtime,hud);long tick=runtime.World.Tick;
            for(int i=0;i<120;i++)runtime.AdvanceOne();Assert.That(runtime.World.Tick,Is.EqualTo(tick));
            Click("좋아하는 것 묻기");MansionUiTestEntry.FinishSpeech(runtime,hud);
            Assert.That(View.Body.text,Is.EqualTo(EverydayDialogue.Line("CH_04",0)),"The completed reply must replace the greeting");
            Assert.That(runtime.ReadNotebook().Records.Any(r=>r.Text.Contains("PRIVATE_SENTINEL")),Is.False);
            int experiences=runtime.Social.Experiences("CH_01").Length;
            Click("좋아하는 것 묻기");MansionUiTestEntry.FinishSpeech(runtime,hud);Assert.That(runtime.Social.Experiences("CH_01").Length,Is.EqualTo(experiences));
            Click("대화 다시 보기");Assert.That(runtime.World.Paused,Is.True);hud.Back();Assert.That(runtime.World.Paused,Is.True);
            Click("그만 이야기하기");Assert.That(runtime.World.Paused,Is.False);runtime.AdvanceOne();Assert.That(runtime.World.Tick,Is.EqualTo(tick+2*EverydayDialogue.DurationTicks("CH_04",0)+1));yield return null;
        }
        [UnityTest] public IEnumerator TenSlotsKeepSeparateSnapshotsAndBadLoadLeavesLiveSessionUntouched()
        {
            long first=runtime.World.Tick;Assert.That(runtime.SaveManual(1),Does.Contain("저장했습니다"));
            for(int i=0;i<61;i++)runtime.AdvanceOne();long last=runtime.World.Tick;
            Assert.That(runtime.SaveManual(10),Does.Contain("저장했습니다"));Assert.That(runtime.ReadSaveSlots().Count(s=>s.Exists),Is.EqualTo(2));
            Assert.That(runtime.LoadManual(1),Is.EqualTo("불러오기 완료"));Assert.That(runtime.World.Tick,Is.EqualTo(first));
            Assert.That(runtime.LoadManual(10),Is.EqualTo("불러오기 완료"));Assert.That(runtime.World.Tick,Is.EqualTo(last));
            string before=JsonUtility.ToJson(runtime.CaptureSession());File.WriteAllText(Path.Combine(storage,"manual-02.dat"),"damaged");
            Assert.That(runtime.LoadManual(2),Does.Contain("현재 진행을 유지"));Assert.That(JsonUtility.ToJson(runtime.CaptureSession()),Is.EqualTo(before));
            Assert.That(runtime.SaveManual(0),Does.Contain("저장하지 못"));Assert.That(runtime.SaveManual(11),Does.Contain("저장하지 못"));yield return null;
        }
        [UnityTest] public IEnumerator SealedOriginalSurvivesNewRepliesAndSaveWithoutBroadcastingToOthers()
        {
            var s=runtime.CaptureSession();s.World.Chapter=2;s.World.Cases=new MansionCaseBook(1,2,s.World.Tick,18).Capture();
            s.Proceedings.Rules=ChapterRules.Next(s.Proceedings.Rules,1,2,18,17,new[]{"CH04"},31);
            string payload=JsonUtility.ToJson(s);File.WriteAllText(runtime.SavePath,AtomicSaveStore.Hash(payload)+"\n"+payload);runtime.LoadFrom(runtime.SavePath);
            StageTalk();MansionUiTestEntry.FinishSpeech(runtime,hud);Click("좋아하는 것 묻기");MansionUiTestEntry.FinishSpeech(runtime,hud);Click("그만 이야기하기");
            var sealedState=runtime.CaptureSession();Assert.That(sealedState.Proceedings.StatementSeals.Length,Is.EqualTo(1));
            string original=sealedState.Proceedings.StatementSeals[0].RecordId;string originalText=runtime.Knowledge.For("CH_04").Find(original).Text;
            runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);
            Assert.That(runtime.CaptureSession().Proceedings.StatementSeals[0].RecordId,Is.EqualTo(original));Assert.That(runtime.Knowledge.For("CH_04").Find(original).Text,Is.EqualTo(originalText));
            Assert.That(runtime.Knowledge.For("CH_02").Records().Any(r=>r.RootId==original),Is.False);yield return null;
        }
    }
}
