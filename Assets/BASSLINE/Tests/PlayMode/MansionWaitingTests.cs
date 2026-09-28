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
    public sealed class MansionWaitingTests
    {
        MansionRuntime runtime;
        [UnitySetUp] public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");
            runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;runtime.UseIsolatedTestStorage();
            UnityEngine.Object.FindAnyObjectByType<FixtureHud>().enabled=false;
            foreach(var pause in runtime.World.Capture().PauseOwners)runtime.Pause(pause,false);
            runtime.SetMove(0,0);
        }
        KnownRecord Notice(string subject)=>new KnownRecord{Kind="OfficialReport",SubjectId=subject,Predicate="CourtSummons",Value="FixtureNotice",Text="TestOnly local notice",Source="PRES_YUSTI",PlaceId="R_HALL",FromTick=runtime.World.Tick,ToTick=runtime.World.Tick+1,IdentityConfirmed=true};
        [UnityTest] public IEnumerator WaitingMatchesOrdinaryTicksAndPhysicallyMovesResidents()
        {
            runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);long start=runtime.World.Tick;
            var before=runtime.World.Residents.ToDictionary(a=>a.Id,a=>a.Position);
            for(int i=0;i<300;i++)runtime.AdvanceOne();
            var expected=runtime.CaptureSession();runtime.LoadFrom(runtime.SavePath);
            runtime.StartWaiting(300);float maximumStep=0;
            while(runtime.IsWaiting){
                var positions=runtime.World.Residents.ToDictionary(a=>a.Id,a=>a.Position);
                runtime.AdvanceWaitingBatch(1);
                foreach(var actor in runtime.World.Residents)maximumStep=Math.Max(maximumStep,(float)positions[actor.Id].Distance(actor.Position));
                if(runtime.World.Tick%120==0)yield return null;
            }
            var actual=runtime.CaptureSession();
            Assert.That(actual.World.Tick,Is.EqualTo(start+300));
            Assert.That(actual.World.Residents.Any(a=>a.Id!="CH_01"&&a.Position.Distance(before[a.Id])>.5),Is.True,"Waiting must run physical NPC life");
            foreach(var actor in actual.World.Residents){var e=expected.World.Residents.Single(a=>a.Id==actor.Id);Assert.That(actor.Position.Distance(e.Position),Is.LessThan(.005),actor.Id);Assert.That(actor.Phase,Is.EqualTo(e.Phase));}
            // PhysX may return sub-millimetre float differences after controller restoration.
            // Keep every receipt, provenance, time and claim exact; compare only positions with tolerance.
            Assert.That(actual.Knowledge.Memories.Length,Is.EqualTo(expected.Knowledge.Memories.Length));
            for(int i=0;i<actual.Knowledge.Memories.Length;i++){
                var a=actual.Knowledge.Memories[i];var e=expected.Knowledge.Memories[i];
                Assert.That(a.Record.Position.Distance(e.Record.Position),Is.LessThan(.0001),"Receipt position "+a.Record.Id);
                e.Record.Position=a.Record.Position;
            }
            Assert.That(JsonUtility.ToJson(actual.Knowledge),Is.EqualTo(JsonUtility.ToJson(expected.Knowledge)));
            Assert.That(JsonUtility.ToJson(actual.Social),Is.EqualTo(JsonUtility.ToJson(expected.Social)));
            Assert.That(maximumStep,Is.LessThan(.4f));
        }
        [UnityTest] public IEnumerator WaitingIgnoresRemoteNoticeAndStopsAtOwnReceiptAfterLoad()
        {
            runtime.StartWaiting(3000);
            runtime.Knowledge.Observe("CH_02",Notice("REMOTE"),runtime.World.Tick);
            runtime.AdvanceWaitingBatch(30);Assert.That(runtime.IsWaiting,Is.True,"Another person's private receipt must not wake the player");
            long target=runtime.ReadWaiting().TargetTick;runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);
            Assert.That(runtime.IsWaiting,Is.True);Assert.That(runtime.ReadWaiting().TargetTick,Is.EqualTo(target));
            runtime.Knowledge.Observe("CH_01",Notice("OWN"),runtime.World.Tick);long heard=runtime.World.Tick;
            runtime.AdvanceWaitingBatch(600);
            Assert.That(runtime.IsWaiting,Is.False);Assert.That(runtime.World.Tick,Is.EqualTo(heard),"Stop on the received boundary before further ticks");
            yield return null;
        }
        [UnityTest] public IEnumerator WaitingUsesReceivedAppointmentAndCanBeStoppedByMovementOrNote()
        {
            long now=runtime.World.Tick;
            string room=runtime.Layout.RoomAt(runtime.Bodies.Single(b=>b.ActorId=="CH_01").transform.position).RoomId;
            string id=runtime.Social.Propose("CH_02","CH_01",room,now+5*60*60,6000,now);
            runtime.Social.Receive(id,1,"CH_01");runtime.Social.Accept(id,1,"CH_01",true);
            runtime.WaitForNextAppointment();Assert.That(runtime.ReadWaiting().TargetTick,Is.EqualTo(now+4*60*60));
            runtime.Social.Propose("CH_02","CH_01",room,now+10*60*60,6000,now,id);
            runtime.AdvanceWaitingBatch(30);Assert.That(runtime.IsWaiting,Is.True,"An unreceived revision cannot change the player's waiting");
            runtime.Social.Receive(id,2,"CH_01");runtime.AdvanceWaitingBatch(30);Assert.That(runtime.IsWaiting,Is.False);
            runtime.StartWaiting(600);runtime.SetMove(.2,0);Assert.That(runtime.IsWaiting,Is.False);
            runtime.SetMove(0,0);runtime.StartWaiting(600);runtime.Pause("K_NOTE",true);long stopped=runtime.World.Tick;runtime.AdvanceWaitingBatch();
            Assert.That(runtime.IsWaiting,Is.False);Assert.That(runtime.World.Tick,Is.EqualTo(stopped));runtime.Pause("K_NOTE",false);
            yield return null;
        }
    }
}
