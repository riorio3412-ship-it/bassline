using System;
using System.Collections;
using System.IO;
using System.Linq;
using BASSLINE.Bootstrap;
using BASSLINE.Save;
using BASSLINE.Trial;
using BASSLINE.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BASSLINE.Tests
{
    public sealed class CompletedArchiveUiTests
    {
        [Serializable] sealed class CompletedFixture{public TrialEndgameSnapshot[] Archive;public string CampaignId;public int Chapter,Loop,ClockVersion;}
        [UnityTest] public IEnumerator ChapterTransitionPersistsItsSelectedRulesInsteadOfMigratingThemAway()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;
            var runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;runtime.UseIsolatedTestStorage();
            var hud=UnityEngine.Object.FindAnyObjectByType<FixtureHud>();MansionUiTestEntry.Start(hud);hud.enabled=false;
            var fixture=JsonUtility.FromJson<CompletedFixture>(File.ReadAllText(Path.Combine(Application.dataPath,"BASSLINE/Tests/Fixtures/CompletedArchive.json")));
            var completed=fixture.Archive[0];var plan=completed.Plan.Copy();
            // Persistence-only fixture from a completed physical run; not a new verdict or reward test.
            foreach(string id in plan.ConfirmedDead){runtime.World.Resident(id).Alive=false;}
            foreach(string id in plan.Executed){runtime.World.Resident(id).Alive=false;runtime.World.Resident(id).Present=false;}
            foreach(string id in plan.Escaped)runtime.World.Resident(id).Present=false;
            var waiting=runtime.World.Residents.First(a=>a.Alive&&a.Present&&a.Id!="CH_01");string waitingId=waiting.Id;var waitingPosition=waiting.Position;
            waiting.Phase="Performing";waiting.Activity="WaitForCourt";waiting.ActivityTicks=int.MaxValue;
            var members=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(MansionRuntime).GetField("endgame",members).SetValue(runtime,TrialEndgame.Restore(completed));
            Assert.That((bool)typeof(MansionRuntime).GetMethod("CommitTransition",members).Invoke(runtime,new object[]{plan,"Chapter"}),Is.True);
            var rules=runtime.CaptureSession().Proceedings.Rules;Assert.That(rules.Chapter,Is.EqualTo(2));Assert.That(rules.Active.Length,Is.EqualTo(1));string expected=JsonUtility.ToJson(rules);
            runtime.LoadFrom(runtime.SavePath);Assert.That(JsonUtility.ToJson(runtime.CaptureSession().Proceedings.Rules),Is.EqualTo(expected));Assert.That(runtime.ReadConversationPlayback().Phase,Is.EqualTo("Idle"));
            Assert.That(runtime.World.Resident(waitingId).Phase,Is.EqualTo("Idle"));Assert.That(runtime.World.Resident(waitingId).Position.Distance(waitingPosition),Is.LessThan(.0001));
            for(int i=0;i<60;i++)runtime.AdvanceOne();
            Assert.That(runtime.World.Events.Any(e=>e.Type=="ActivityRequested"&&e.Actor==waitingId&&e.Detail!="WaitForCourt"),Is.True,"The next chapter must resume physical life planning.");
        }
        [UnityTest] public IEnumerator CompletedArchiveOpensAfterHudStartupAndSurvivesSaveLoad()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;
            var runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;runtime.UseIsolatedTestStorage();
            var hud=UnityEngine.Object.FindAnyObjectByType<FixtureHud>();
            MansionUiTestEntry.Start(hud);
            hud.enabled=false;
            // The completed archive is a compact fixture extracted from the real full-flow v4 run,
            // after verdict/reward/atomic settlement. No current-case truth is copied into player B.
            var completed=JsonUtility.FromJson<CompletedFixture>(File.ReadAllText(Path.Combine(Application.dataPath,"BASSLINE/Tests/Fixtures/CompletedArchive.json")));
            var snapshot=runtime.CaptureSession();snapshot.Proceedings.Archive=completed.Archive;snapshot.Proceedings.CampaignId=completed.CampaignId;
            snapshot.Proceedings.Rules=new BASSLINE.World.Mansion.ChapterRulePlan{Loop=completed.Loop,Chapter=completed.Chapter};snapshot.World.Chapter=completed.Chapter;snapshot.World.ClockVersion=completed.ClockVersion;
            snapshot.World.Cases=new BASSLINE.World.Mansion.MansionCaseBook(snapshot.World.Loop,snapshot.World.Chapter,snapshot.World.Tick,snapshot.World.Residents.Count(r=>r.Alive&&r.Present)).Capture();
            Assert.That(snapshot.World.Loop,Is.EqualTo(completed.Loop));
            string payload=JsonUtility.ToJson(snapshot);File.WriteAllText(runtime.SavePath,AtomicSaveStore.Hash(payload)+"\n"+payload);runtime.LoadFrom(runtime.SavePath);
            string knowledge=JsonUtility.ToJson(runtime.Knowledge.Capture());long tick=runtime.World.Tick;
            hud.OpenNotePage(13);
            var view=hud.GetComponentsInChildren<ProductionScreenView>(true).Single(v=>v.ScreenId=="UI_13");
            Assert.That(view.Body.text,Does.Contain("표결로 지목한 사람"));
            Assert.That(runtime.ReadArchive().Cases.Single().Records.Count,Is.GreaterThan(0));
            Assert.That(JsonUtility.ToJson(runtime.Knowledge.Capture()),Is.EqualTo(knowledge));Assert.That(runtime.World.Tick,Is.EqualTo(tick));
            runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);hud.SyncPauseView();
            Assert.That(hud.CurrentScreen,Is.EqualTo(13));Assert.That(runtime.ReadArchive().Cases.Count,Is.EqualTo(1));
        }
    }
}
