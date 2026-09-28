using System;
using System.Collections;
using System.IO;
using System.Linq;
using BASSLINE.AuthoringData;
using BASSLINE.Bootstrap;
using BASSLINE.Save;
using BASSLINE.UI;
using BASSLINE.World.Mansion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BASSLINE.Tests
{
    // The former recording feature is intentionally retired: gods' eyes are not NPC evidence.
    public sealed class MansionRecordingPlayModeTests
    {
        MansionRuntime runtime;
        [UnitySetUp] public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable"); yield return null;
            runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();
            runtime.AutomaticTick=false; runtime.UseIsolatedTestStorage();
            var hud=UnityEngine.Object.FindAnyObjectByType<FixtureHud>(); MansionUiTestEntry.Start(hud); hud.enabled=false;
            runtime.Routines=Array.Empty<ResidentRoutine>();
            foreach(var actor in runtime.World.Residents)actor.NextSocialTick=60000;
        }
        [UnityTest] public IEnumerator PlayableSceneDoesNotCreateNpcSurveillanceOrRecordingEvidence()
        {
            Assert.That(UnityEngine.Object.FindObjectsByType<MansionRecordingStation>(),Is.Empty);
            var targets=UnityEngine.Object.FindObjectsByType<FixtureTarget>();
            Assert.That(targets.Any(t=>t.StableId==MansionRecordingStation.WorkshopCamera||t.StableId==MansionRecordingStation.WorkshopReader),Is.False);
            Assert.That(typeof(IMansionRecordingPhysics).IsAssignableFrom(typeof(MansionRuntime)),Is.False);
            for(int i=0;i<120;i++)runtime.AdvanceOne();
            Assert.That(runtime.CaptureSession().Recordings.Clips,Is.Empty);
            foreach(var actor in runtime.World.Residents)Assert.That(runtime.Knowledge.For(actor.Id).Records().Any(r=>r.Kind=="Video"),Is.False);
            runtime.SaveTo(runtime.SavePath); runtime.LoadFrom(runtime.SavePath);
            Assert.That(runtime.CaptureSession().Recordings.Clips,Is.Empty);
            yield return null;
        }
        [UnityTest] public IEnumerator LoadingLegacyRecordingPresenceDoesNotRestartARecorder()
        {
            var old=runtime.CaptureSession(); old.OptionalObjects&=~2048; old.Recordings=new MansionRecordingSnapshot();
            string json=JsonUtility.ToJson(old); File.WriteAllText(runtime.SavePath,AtomicSaveStore.Hash(json)+"\n"+json);
            runtime.LoadFrom(runtime.SavePath);
            for(int i=0;i<60;i++)runtime.AdvanceOne();
            var loaded=runtime.CaptureSession().Recordings;
            Assert.That(loaded.Clips,Is.Empty);
            Assert.That(loaded.LastTick,Is.EqualTo(runtime.World.Tick));
            yield return null;
        }
    }
}
