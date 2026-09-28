using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using BASSLINE.Bootstrap;
using BASSLINE.AuthoringData;
using BASSLINE.Core;
using BASSLINE.UI;
namespace BASSLINE.Tests
{
    public sealed class MansionPlayModeTests
    {
        MansionRuntime runtime;FixtureHud hud;
        [UnitySetUp] public IEnumerator Setup()
        {
            SceneManager.LoadScene("Mansion_Playable");yield return null;
            runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();Assert.That(runtime,Is.Not.Null);runtime.AutomaticTick=false;
            hud=UnityEngine.Object.FindAnyObjectByType<FixtureHud>();
            MansionUiTestEntry.Start(hud);
            foreach(var owner in runtime.World.Capture().PauseOwners.ToArray())runtime.Pause(owner,false);
        }
        [UnityTest] public IEnumerator M01_AllManifestRoomsAndEighteenResidentsBound()
        {
            var layout=runtime.Layout;Assert.That(layout.Rooms.Length,Is.EqualTo(80));Assert.That(runtime.Bodies.Length,Is.EqualTo(18));
            Assert.That(layout.Rooms.Count(r=>r.RoomId.StartsWith("R_BED_",StringComparison.Ordinal)&&r.RoomId!="R_BED_COR"),Is.EqualTo(18));
            Assert.That(layout.Anchors.Count(a=>a.RoomId=="R_TRIAL"&&a.InteractionType=="Seat"),Is.EqualTo(18));
            foreach(var room in layout.Rooms)Assert.That(room.WalkNode,Is.GreaterThanOrEqualTo(0),room.RoomId);
            foreach(var b in runtime.Bodies)Assert.That(layout.RoomAt(b.transform.position),Is.Not.Null,b.ActorId);
            yield return null;
        }
        void PlacePlayer(Vector3 point)
        {
            var body=runtime.Bodies.Single(b=>b.ActorId=="CH_01");body.Capsule.enabled=false;body.transform.position=point;body.Capsule.enabled=true;runtime.World.Resident("CH_01").Position=MansionRuntime.P(point);runtime.Pause("TEST_RESET",true);runtime.Pause("TEST_RESET",false);Physics.SyncTransforms();
        }
        [UnityTest] public IEnumerator M01_RunDiagonalPauseAndSaveRestore()
        {
            Vector3 start=new Vector3(45,0,0);PlacePlayer(start);runtime.SetMove(1,0);runtime.SetRun(false);
            for(int i=0;i<120;i++)runtime.AdvanceOne();double walk=runtime.World.Resident("CH_01").Position.Distance(MansionRuntime.P(start));
            PlacePlayer(start);runtime.SetMove(1,0);runtime.SetRun(true);for(int i=0;i<120;i++)runtime.AdvanceOne();double run=runtime.World.Resident("CH_01").Position.Distance(MansionRuntime.P(start));
            Assert.That(walk,Is.GreaterThan(4.5));Assert.That(run,Is.GreaterThan(walk*1.5));Assert.That(run,Is.LessThan(9.7));
            long tick=runtime.World.Tick;var position=runtime.World.Resident("CH_01").Position;runtime.Pause("K_NOTE",true);runtime.Pause("K_SETTINGS",true);runtime.Pause("K_NOTE",false);for(int i=0;i<60;i++)runtime.AdvanceOne();Assert.That(runtime.World.Tick,Is.EqualTo(tick));Assert.That(position.Distance(runtime.World.Resident("CH_01").Position),Is.Zero);
            string path=Path.Combine(Application.temporaryCachePath,"mansion-motion-test.dat");
            try{runtime.SaveTo(path);string before=JsonUtility.ToJson(runtime.CaptureSession());runtime.Pause("K_SETTINGS",false);for(int i=0;i<30;i++)runtime.AdvanceOne();runtime.LoadFrom(path);Assert.That(JsonUtility.ToJson(runtime.CaptureSession()),Is.EqualTo(before));}
            finally{foreach(string suffix in new[]{"",".tmp",".previous"})if(File.Exists(path+suffix))File.Delete(path+suffix);}
            TestContext.Progress.WriteLine($"Actual CharacterController 120 ticks: walk {walk:F3}m, run {run:F3}m; nested pause and saved snapshot restored.");yield return null;
        }
        [UnityTest] public IEnumerator M01_SeventeenResidentsMovePhysicallyAndCapture()
        {
            var starts=runtime.Bodies.Where(b=>b.ActorId!="CH_01").ToDictionary(b=>b.ActorId,b=>b.transform.position);var peaks=starts.ToDictionary(p=>p.Key,p=>0f);double maxStep=0;
            var watch=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<3600;i++){
                var previous=runtime.World.Residents.Select(a=>a.Position).ToArray();runtime.AdvanceOne();var after=runtime.World.Residents.Select(a=>a.Position).ToArray();
                foreach(var pair in starts)peaks[pair.Key]=Math.Max(peaks[pair.Key],Vector3.Distance(pair.Value,runtime.Bodies.Single(b=>b.ActorId==pair.Key).transform.position));
                for(int n=0;n<after.Length;n++)maxStep=Math.Max(maxStep,previous[n].Distance(after[n]));if(i%180==0)yield return null;
            }
            foreach(var pair in starts)Assert.That(peaks[pair.Key],Is.GreaterThan(1),pair.Key+" never physically left start");
            Assert.That(maxStep,Is.LessThan(.4),"No room teleport in simulated movement");
            Directory.CreateDirectory("Verification");File.WriteAllText("Verification/mansion-life-snapshot.json",JsonUtility.ToJson(runtime.CaptureSession(),true));
            File.WriteAllText("Verification/mansion-performance.txt",$"Unity={Application.unityVersion}\nDevice={SystemInfo.graphicsDeviceName}\nTicks=3600\nWallSeconds={watch.Elapsed.TotalSeconds}\nMaxActorStep={maxStep}\nManagedBytes={GC.GetTotalMemory(false)}\nPhysics uses actual colliders. This is not a rendered FPS measurement.");
            ScreenCapture.CaptureScreenshot("Verification/mansion-game-view.png");for(int frame=0;frame<6;frame++)yield return null;
        }
        [UnityTest] public IEnumerator M01_RemovedResidentCannotCreateNewVisualMemoryAndDoorPromptDoesNotReadSecretLock()
        {
            var player=runtime.Bodies.Single(b=>b.ActorId=="CH_01");var subject=runtime.Bodies.Single(b=>b.ActorId=="CH_02");
            subject.Capsule.enabled=false;subject.transform.position=player.transform.position+Vector3.forward*1.2f;
            runtime.World.Resident(subject.ActorId).Position=MansionRuntime.P(subject.transform.position);runtime.World.Resident(subject.ActorId).Present=false;subject.gameObject.SetActive(false);Physics.SyncTransforms();
            Assert.That(runtime.Knowledge.LastDirect("CH_01","CH_02","AtPlace"),Is.Null);
            for(int i=0;i<60;i++)runtime.AdvanceOne();
            Assert.That(runtime.Knowledge.LastDirect("CH_01","CH_02","AtPlace"),Is.Null,"An absent resident's stale transform must not look like a visible person.");
            string doorId=runtime.World.Doors.First(d=>d.Locked).Id;string lockedPrompt=runtime.DescribeTarget(doorId).PrimaryAction;
            runtime.World.Door(doorId).Locked=false;Assert.That(runtime.DescribeTarget(doorId).PrimaryAction,Is.EqualTo(lockedPrompt),"A hidden lock state is learned through inspection or an actual use attempt.");
            yield return null;
        }
        [UnityTest] public IEnumerator M01_TwelveMinutesCrowdedLifeReachesRealActivities()
        {
            var residents=runtime.World.Residents.Where(a=>a.Id!="CH_01").ToArray();
            var progress=residents.ToDictionary(a=>a.Id,a=>new ProgressEvidence{Actor=a.Id,LastMeaningfulTick=runtime.World.Tick});
            var nodePositions=runtime.Layout.NavigationNodes.ToDictionary(n=>n.Id,n=>MansionRuntime.P(n.Position));
            long maxDoorHold=0;string longestDoor="",longestHolder="";
            double maxStep=0;for(int tick=0;tick<12*60*60;tick++){
                var previous=runtime.World.Residents.Select(a=>a.Position).ToArray();runtime.AdvanceOne();int index=0;
                foreach(var actor in runtime.World.Residents)maxStep=Math.Max(maxStep,previous[index++].Distance(actor.Position));
                foreach(var actor in residents){
                    var record=progress[actor.Id];string waypoint=actor.PathCursor<actor.Path.Length?actor.Path[actor.PathCursor]:"";
                    double remaining=waypoint==""?0:actor.Position.Distance(nodePositions[waypoint]);
                    if(actor.Phase!="Travelling"||record.Destination!=actor.Destination||record.PathCursor!=actor.PathCursor||remaining<record.BestDistance-.25){
                        record.LastMeaningfulTick=runtime.World.Tick;record.BestDistance=remaining;
                        record.Destination=actor.Destination;record.PathCursor=actor.PathCursor;
                    }
                    // Sideways movement and returning to the same best distance do not reset this clock.
                    record.LongestWithoutProgress=Math.Max(record.LongestWithoutProgress,runtime.World.Tick-record.LastMeaningfulTick);
                }
                foreach(var door in runtime.World.Doors)if(door.Holder!=""&&runtime.World.Tick-door.GrantedTick>maxDoorHold){maxDoorHold=runtime.World.Tick-door.GrantedTick;longestDoor=door.Id;longestHolder=door.Holder;}
                if(tick%600==0)yield return null;
            }
            Directory.CreateDirectory("Verification");File.WriteAllText("Verification/mansion-twelve-minute-life.json",JsonUtility.ToJson(runtime.CaptureSession(),true));
            File.WriteAllText("Verification/mansion-twelve-minute-progress.json",JsonUtility.ToJson(new ProgressReport{Ticks=runtime.World.Tick,MaxPhysicalStep=maxStep,MaxDoorHoldTicks=maxDoorHold,Door=longestDoor,Holder=longestHolder,Actors=progress.Values.ToArray()},true));
            Assert.That(maxStep,Is.LessThan(.4),"Physical motion cannot teleport across rooms");
            Assert.That(maxDoorHold,Is.LessThan(30*60),longestDoor+" was held by "+longestHolder+" without clearing for "+maxDoorHold+" ticks");
            foreach(var actor in runtime.World.Residents.Where(a=>a.Id!="CH_01")){
                Assert.That(progress[actor.Id].LongestWithoutProgress,Is.LessThan(120*60),actor.Id+" made no route/goal progress; sideways motion must not mask persistent blockage");
                Assert.That(actor.CompletedActivities,Is.GreaterThan(0),actor.Id+" stopped in "+actor.Phase+" at "+actor.Node+" toward "+actor.Destination+" stuck ticks "+actor.StuckTicks);
                Assert.That(runtime.World.Events.Any(e=>e.Actor==actor.Id&&e.Type=="ActivityStarted"&&runtime.Layout.NavigationNodes.Any(n=>n.Id==e.Target&&n.RoomId!="R_BED_"+actor.Id.Substring(3))),Is.True,actor.Id+" never reached a real activity outside own bedroom");
            }
        }
        [UnityTest] public IEnumerator M01_RecordedGrandStairCornerRecoversThroughActualCollision()
        {
            runtime.Routines=Array.Empty<ResidentRoutine>();
            // TestOnly initial-condition replay of the failed 12-minute run. No setup placement occurs
            // after simulation starts, and both residents must traverse the original authored route.
            void Replay(string id,Vector3 start,string[] path){
                var body=runtime.Bodies.Single(b=>b.ActorId==id);body.Capsule.enabled=false;body.transform.position=start;body.Capsule.enabled=true;
                var actor=runtime.World.Resident(id);actor.Position=MansionRuntime.P(start);actor.Node=path[0];actor.Path=path;actor.PathCursor=1;actor.Destination=path.Last();actor.Phase="Travelling";actor.Activity="TestOnly_StairRecovery";actor.ActivityTicks=3000;actor.QueueDoor="";
            }
            Replay("CH_05",new Vector3(1.0318704f,5.9654484f,6.7298903f),new[]{"NAV_R_GRAND_STAIR_00_006","NAV_R_GRAND_STAIR_00_007","NAV_R_GRAND_STAIR_00_008"});
            Replay("CH_10",new Vector3(.95764464f,6.025f,7.368184f),new[]{"NAV_R_GRAND_STAIR_00_008","NAV_R_GRAND_STAIR_00_007","NAV_R_GRAND_STAIR_00_006","NAV_R_GRAND_STAIR_00_005"});
            Physics.SyncTransforms();double maxStep=0;
            for(int tick=0;tick<1800;tick++){
                var previous=runtime.World.Residents.Select(a=>a.Position).ToArray();runtime.AdvanceOne();int index=0;
                foreach(var actor in runtime.World.Residents)maxStep=Math.Max(maxStep,previous[index++].Distance(actor.Position));
                if(tick%300==0)yield return null;
            }
            Directory.CreateDirectory("Verification");File.WriteAllText("Verification/mansion-stair-recovery.json",JsonUtility.ToJson(runtime.CaptureSession(),true));
            Assert.That(maxStep,Is.LessThan(.4),"After explicit TestOnly setup every displacement must be physical");
            foreach(string id in new[]{"CH_05","CH_10"})Assert.That(runtime.World.Events.Any(e=>e.Actor==id&&e.Type=="ActivityStarted"&&e.Detail=="TestOnly_StairRecovery"),Is.True,id+" remained at the recorded stair-rail corner");
        }
        [Serializable] sealed class ProgressEvidence
        {
            public string Actor,Destination="";public int PathCursor=-1;public double BestDistance=double.MaxValue;
            public long LastMeaningfulTick,LongestWithoutProgress;
        }
        [Serializable] sealed class ProgressReport
        {
            public long Ticks,MaxDoorHoldTicks;public string Door,Holder;public double MaxPhysicalStep;public ProgressEvidence[] Actors;
        }
    }
}
