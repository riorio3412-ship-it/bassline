using System.Collections;
using System.Linq;
using BASSLINE.Bootstrap;
using BASSLINE.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BASSLINE.Tests
{
    public sealed class DialogueChoiceNavigationTests
    {
        [UnityTest] public IEnumerator DialogueChoicesAndSharePreviewReturnWithoutSendingOrClosing()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;
            var runtime=Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;
            var hud=Object.FindAnyObjectByType<FixtureHud>();
            MansionUiTestEntry.Start(hud);
            hud.enabled=false;
            // Initial test staging only; the actual interaction must pass the production range/LOS port.
            var resident=runtime.Bodies.Single(b=>b.ActorId=="CH_04");var player=runtime.Bodies.Single(b=>b.ActorId=="CH_01");
            resident.Capsule.enabled=false;resident.transform.position=new Vector3(-4,0,-4);resident.Capsule.enabled=true;runtime.World.Resident("CH_04").Position=MansionRuntime.P(resident.transform.position);
            player.Capsule.enabled=false;player.transform.position=resident.transform.position+Vector3.back*1.3f;player.Capsule.enabled=true;runtime.World.Resident("CH_01").Position=MansionRuntime.P(player.transform.position);runtime.SetLook(0,0);Physics.SyncTransforms();
            hud.Primary("CH_04");Assert.That(hud.CurrentScreen,Is.EqualTo(3));MansionUiTestEntry.FinishSpeech(runtime,hud);
            var view=hud.GetComponentsInChildren<ProductionScreenView>(true).Single(v=>v.ScreenId=="UI_03");
            Assert.That(view.Actions.Count(b=>b.gameObject.activeSelf),Is.EqualTo(5));
            Assert.That(view.ActionLabels.Where((t,i)=>view.Actions[i].gameObject.activeSelf).Select(t=>t.text),Does.Contain("안부 묻기"));
            string before=JsonUtility.ToJson(runtime.Knowledge.Capture());
            Click(view,"다른 이야기");Click(view,"내가 본 일 말하기");Assert.That(hud.CurrentScreen,Is.EqualTo(5));
            var picker=hud.GetComponentsInChildren<ProductionScreenView>(true).Single(v=>v.ScreenId=="UI_05");
            Assert.That(picker.RecordRowButtons.Length,Is.GreaterThan(0));picker.RecordRowButtons[0].onClick.Invoke();
            Assert.That(picker.Context.text,Does.Contain("전할 내용"));Assert.That(picker.ActionLabels.Any(t=>t.text=="이 내용을 말하기"),Is.True);
            Assert.That(JsonUtility.ToJson(runtime.Knowledge.Capture()),Is.EqualTo(before),"Selecting a story previews it without sending it.");
            Click(picker,"말하지 않고 돌아가기");hud.Back();Assert.That(hud.CurrentScreen,Is.EqualTo(3));
            Click(view,"대화 다시 보기");Assert.That(hud.CurrentScreen,Is.EqualTo(10));Assert.That(runtime.World.Paused,Is.True);
            var journal=hud.GetComponentsInChildren<ProductionScreenView>(true).Single(v=>v.ScreenId=="UI_10");
            Assert.That(journal.Body.text,Does.Contain(runtime.ReadConversationPlayback().HeardText));
            var labels=journal.ActionLabels.Where((t,i)=>journal.Actions[i].gameObject.activeSelf).Select(t=>t.text).ToArray();
            Assert.That(labels,Does.Not.Contain("다음 기록"));Assert.That(labels,Does.Not.Contain("증거 상세"));Assert.That(labels,Does.Not.Contain("분류 · 대화"));
            hud.Back();Assert.That(hud.CurrentScreen,Is.EqualTo(3));Assert.That(runtime.World.Paused,Is.True);
            Click(view,"그만 이야기하기");Assert.That(hud.CurrentScreen,Is.EqualTo(1));
        }
        static void Click(ProductionScreenView view,string text){int index=System.Array.FindIndex(view.ActionLabels,t=>t.text==text&&t.transform.parent.gameObject.activeSelf);Assert.That(index,Is.GreaterThanOrEqualTo(0),text);view.Actions[index].onClick.Invoke();}
    }
}
