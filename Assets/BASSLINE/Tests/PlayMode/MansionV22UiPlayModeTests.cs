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
    public sealed class MansionV22UiPlayModeTests
    {
        [UnityTest] public IEnumerator MinimalHudAndArchivePreserveWorldAndKnowledge()
        {
            SceneManager.LoadScene("Mansion_Playable");yield return null;
            var runtime=Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;
            runtime.UseIsolatedTestStorage();
            var hud=Object.FindAnyObjectByType<FixtureHud>();
            var title=hud.GetComponentsInChildren<ProductionScreenView>(true).Single(v=>v.ScreenId=="UI_38");
            MansionUiTestEntry.Start(hud);
            yield return null;
            Assert.That(runtime.ReadPlayer().ClockVersion,Is.EqualTo(BASSLINE.Core.WorldTimeLabel.Morning));
            Assert.That(hud.Clock.enabled,Is.False,"Exploration has no permanent clock");
            hud.OpenNotePage(11);hud.Back();hud.Open(7);
            var note=hud.GetComponentsInChildren<ProductionScreenView>(true).Single(v=>v.ScreenId=="UI_07");
            Assert.That(note.Actions.Where(b=>b.gameObject.activeSelf).All(b=>b.interactable),Is.True,"Map's disabled controls must not disable the notebook actions");
            string before=JsonUtility.ToJson(runtime.Knowledge.Capture());long tick=runtime.World.Tick;
            hud.Open(13);yield return null;
            var archive=hud.GetComponentsInChildren<ProductionScreenView>(true).Single(v=>v.ScreenId=="UI_13");
            Assert.That(archive.Title.text,Is.EqualTo("지난 사건 기록"));
            Assert.That(archive.Body.text,Does.Contain("아직 돌아볼 사건이 없어요"));
            Assert.That(runtime.ReadArchive().Cases,Is.Empty);
            for(int i=0;i<60;i++)runtime.AdvanceOne();
            Assert.That(runtime.World.Tick,Is.EqualTo(tick));
            Assert.That(JsonUtility.ToJson(runtime.Knowledge.Capture()),Is.EqualTo(before));
            Assert.That(runtime.ReadUiState().Pages.Last(),Is.EqualTo(13));
            hud.Back();Assert.That(hud.CurrentScreen,Is.EqualTo(7));
            Assert.That(note.Context.text,Does.Contain("07:00:"));
            runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);
            Assert.That(runtime.ReadPlayer().ClockVersion,Is.EqualTo(BASSLINE.Core.WorldTimeLabel.Morning));
            Assert.That(runtime.World.Tick,Is.EqualTo(tick));
        }
    }
}
