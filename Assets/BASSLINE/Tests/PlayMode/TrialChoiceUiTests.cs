using System;
using System.Collections;
using System.Linq;
using BASSLINE.Bootstrap;
using BASSLINE.Core;
using BASSLINE.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BASSLINE.Tests
{
    public sealed class TrialChoiceUiTests
    {
        FixtureHud hud;
        ProductionScreenView View=>hud.GetComponentsInChildren<ProductionScreenView>(true).Single(v=>v.gameObject.activeInHierarchy);
        string[] Labels=>View.ActionLabels.Where((t,i)=>View.Actions[i].gameObject.activeSelf).Select(t=>t.text).ToArray();
        void Click(string label){var view=View;int index=Array.FindIndex(view.ActionLabels,t=>t.text==label);Assert.That(index,Is.GreaterThanOrEqualTo(0),label);Assert.That(view.Actions[index].gameObject.activeSelf&&view.Actions[index].interactable,Is.True,label);view.Actions[index].onClick.Invoke();}

        [UnityTest] public IEnumerator ClueSelectionKeepsKnowledgePrivateAndComparesWithoutManualRules()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;
            var runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;runtime.UseIsolatedTestStorage();
            hud=UnityEngine.Object.FindAnyObjectByType<FixtureHud>();hud.enabled=false;
            MansionUiTestEntry.Start(hud);
            foreach(string pause in runtime.World.Capture().PauseOwners)runtime.Pause(pause,false);
            // Explicit UI-only receipt fixture; physical testimony admission is covered by full-flow tests.
            runtime.Knowledge.Observe("CH_01",new KnownRecord{Kind="Visual",SubjectId="CH_04",Predicate="AtPlace",Value="R_HALL",PlaceId="R_HALL",Source="CH_01",Text="UI 검사용 직접 위치 관측",IdentityConfirmed=true,FromTick=runtime.World.Tick,ToTick=runtime.World.Tick+1,Supports=new[]{"관측한 순간의 위치"}},runtime.World.Tick);
            runtime.Knowledge.Observe("CH_02",new KnownRecord{Kind="Visual",SubjectId="CH_03",Predicate="AtPlace",Value="R_HALL",PlaceId="R_HALL",Source="CH_02",Text="UI_PRIVATE_SENTINEL",IdentityConfirmed=true,FromTick=runtime.World.Tick,ToTick=runtime.World.Tick+1},runtime.World.Tick);
            string recordId=runtime.ConfigureTrialUiReview();string knowledge=JsonUtility.ToJson(runtime.Knowledge.Capture());long tick=runtime.World.Tick;
            hud.Open(23);Click(Labels.Single(x=>x.EndsWith(" 포커스",StringComparison.Ordinal)));Click("이 말을 뒷받침할 단서가 있어");
            Click("이전 관측도 보기");
            var records=runtime.ReadNotebook().Records.OrderByDescending(r=>r.ReceivedTick).ToArray();
            int recordIndex=Array.FindIndex(records,r=>r.Id==recordId);
            Assert.That(View.RecordRowButtons.Length,Is.EqualTo(records.Length));
            Assert.That(Labels,Does.Not.Contain("다음 자료"));
            Assert.That(View.RecordRowButtons.SelectMany(b=>b.GetComponentsInChildren<TMPro.TMP_Text>()).Any(t=>t.text.Contains("UI_PRIVATE_SENTINEL")),Is.False);
            View.RecordRowButtons[recordIndex].onClick.Invoke();
            Assert.That(runtime.ReadUiState().SelectedEvidence,Is.EqualTo(new[]{recordId}));
            View.RecordRowButtons[recordIndex].onClick.Invoke();Assert.That(runtime.ReadUiState().SelectedEvidence,Is.Empty);
            View.RecordRowButtons[recordIndex].onClick.Invoke();
            Assert.That(Labels,Does.Not.Contain("비교 방법 고르기"));
            Click("단서 자세히");Assert.That(View.Context.text,Does.Not.Contain("UI_PRIVATE_SENTINEL"));
            Assert.That(runtime.ReadTrial().Focused,Is.True);Assert.That(runtime.CaptureSession().Proceedings.Court.Submissions,Is.Empty);
            var saved=JsonUtility.FromJson<PlayerUiSnapshot>(JsonUtility.ToJson(runtime.ReadUiState()));
            hud.Open(38);hud.Back();runtime.StoreUiState(saved);hud.SyncPauseView();
            Assert.That(runtime.ReadUiState().SelectedEvidence,Is.EqualTo(new[]{recordId}));
            Assert.That(runtime.ReadTrial().Focused,Is.True);
            Assert.That(JsonUtility.ToJson(runtime.Knowledge.Capture()),Is.EqualTo(knowledge));Assert.That(runtime.World.Tick,Is.EqualTo(tick));
            Click("이 단서로 이야기하기");
            var submission=runtime.CaptureSession().Proceedings.Court.Submissions.Single();
            Assert.That(submission.Payload,Does.Contain("|Support|LR03|"));Assert.That(submission.Result.ResultType,Is.EqualTo("Support"),"The player selects an actual clue; the system compares its location and time");
            Assert.That(submission.Result.CitedRefs,Is.EqualTo(new[]{recordId}));Assert.That(submission.Result.AReadCount,Is.Zero);
            Assert.That(hud.CurrentScreen,Is.EqualTo(23));Assert.That(runtime.ReadTrial().Focused,Is.False);
            Assert.That(JsonUtility.ToJson(runtime.Knowledge.Capture()),Is.EqualTo(knowledge));Assert.That(runtime.World.Tick,Is.EqualTo(tick));
        }
    }
}
