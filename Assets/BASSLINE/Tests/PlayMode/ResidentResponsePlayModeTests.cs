using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BASSLINE.AuthoringData;
using BASSLINE.Bootstrap;
using BASSLINE.Core;
using BASSLINE.Save;
using BASSLINE.UI;
using BASSLINE.World.Mansion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BASSLINE.Tests
{
    public sealed class ResidentResponsePlayModeTests
    {
        const string CaseId="CASE_RESPONSE_TEST";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        MansionRuntime runtime;Vector3 bodyPoint;
        [UnitySetUp]public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;
            runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;runtime.UseIsolatedTestStorage();
            var hud=UnityEngine.Object.FindAnyObjectByType<FixtureHud>();MansionUiTestEntry.Start(hud);hud.enabled=false;
            runtime.Routines=Array.Empty<ResidentRoutine>();
            foreach(var actor in runtime.World.Residents){actor.NextSocialTick=60000;actor.Phase="Performing";actor.Activity="Sleep";actor.ActivityTicks=60000;}
            bodyPoint=runtime.Layout.NavigationNodes[runtime.Layout.NearestNode(new Vector3(-4,0,-4),"R_HALL")].Position;
            Place("CH_02",bodyPoint,180);Place("CH_03",bodyPoint+Vector3.back*1.5f,0);
            Place("CH_01",bodyPoint+Vector3.left*3+Vector3.back*1.5f,90);
            runtime.World.Resident("CH_03").Activity="Rest";runtime.World.Resident("CH_01").Activity="Rest";
            PutPresenter(bodyPoint+Vector3.back*3.5f);
        }
        void Place(string id,Vector3 p,float yaw)
        {
            var b=runtime.Bodies.Single(a=>a.ActorId==id);b.Capsule.enabled=false;b.transform.position=p;b.transform.rotation=Quaternion.Euler(0,yaw,0);b.Capsule.enabled=true;
            var a=runtime.World.Resident(id);a.Position=MansionRuntime.P(p);a.Node=runtime.Layout.NavigationNodes[runtime.Layout.NearestNode(p)].Id;a.Yaw=yaw;Physics.SyncTransforms();
        }
        void PutPresenter(Vector3 p)
        {
            var b=runtime.Presenter;b.Capsule.enabled=false;b.transform.position=p;b.Capsule.enabled=true;
            var proceedings=(MansionProceedings)typeof(MansionRuntime).GetField("proceedings",Private).GetValue(runtime);
            proceedings.PresenterPosition=MansionRuntime.P(p);proceedings.PresenterNode=runtime.Layout.NavigationNodes[runtime.Layout.NearestNode(p)].Id;proceedings.PresenterPath=Array.Empty<string>();proceedings.PresenterCursor=0;Physics.SyncTransforms();
        }
        void Ticks(int count){for(int i=0;i<count;i++)runtime.AdvanceOne();}
        ResidentResponseState Response()=>runtime.CaptureSession().ResidentResponses.Single(s=>s.ActorId=="CH_03");
        // This fixture creates only a completed test outcome through the authority protocol.
        // It tests response/reporting, not ordinary motive selection or lethal action generation.
        void AuthorCompletedTestOutcome()
        {
            string node=runtime.World.Resident("CH_02").Node;
            var settings=new MansionCaseSettings{Enabled=true,ExplicitTestSession=true,Id=CaseId,ActorId="CH_04",TargetId="CH_02",ObjectId="M_NOTEBOOK",ToolNode=node,ContactNode=node,DecisionReason="Explicit response test fixture",IntentSpeech="Test only",RejectedAlternatives=new[]{"Withdraw","AskForHelp"},ChapterStartTick=0,ApproachTick=0,EarliestCauseTick=0,OpportunityEndTick=3600,MinimumIntentTicks=1,ContactTicks=1,DelayTicks=1};
            Assert.That(runtime.World.RegisterCase(settings),Is.EqualTo("Registered"));Assert.That(runtime.World.ReserveCaseOutcome(CaseId),Is.True);
            long causeTick=runtime.World.Tick;long cause=runtime.World.CommitCaseCause(CaseId,causeTick+1);runtime.World.Step(default,false,runtime);long result=runtime.World.CommitCaseResult(CaseId);
            var snapshot=new MansionIncidentSnapshot{Settings=settings,Stage="ResultCommitted",FatalityCap=2,LastTick=runtime.World.Tick,CauseTick=causeTick,DueTick=causeTick+1,ResultTick=runtime.World.Tick,CauseEvent="M_EVENT_"+cause,ResultEvent="M_EVENT_"+result,TracePresent=true,CausePosition=MansionRuntime.P(bodyPoint),ContactPoint=MansionRuntime.P(bodyPoint),ResultPosition=MansionRuntime.P(bodyPoint)};
            typeof(MansionRuntime).GetField("incidents",Private).SetValue(runtime,MansionIncidentCollection.Restore(new[]{snapshot},runtime.World));
        }
        void Until(Func<bool> predicate,int maximum)
        {
            for(int i=0;i<maximum&&!predicate();i++)runtime.AdvanceOne();
            Assert.That(predicate(),Is.True,"tick="+runtime.World.Tick+" task="+string.Join(";",runtime.CaptureSession().ResidentResponses.Select(t=>JsonUtility.ToJson(t)))+" actor="+JsonUtility.ToJson(runtime.World.Resident("CH_03"))+" proceedings="+JsonUtility.ToJson(runtime.CaptureSession().Proceedings));
        }
        [UnityTest]public IEnumerator NearbyNpcChecksReportsAndTriggersPhysicalInspectionWithoutPlayerInput()
        {
            AuthorCompletedTestOutcome();Ticks(80);
            Assert.That(Response().Phase,Is.EqualTo("Inspecting"));Assert.That(runtime.CaptureSession().Incident.DiscoveryTick,Is.LessThan(0));
            string before=JsonUtility.ToJson(Response());runtime.Pause("K_TEST",true);Ticks(90);Assert.That(JsonUtility.ToJson(Response()),Is.EqualTo(before));
            runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);Assert.That(JsonUtility.ToJson(Response()),Is.EqualTo(before));runtime.Pause("K_TEST",false);
            Until(()=>Response().Phase=="SeekingYusti",180);
            Assert.That(runtime.CaptureSession().Incident.ReportTick,Is.LessThan(0));
            // The presenter starts behind the observer. Seeking must turn or walk, not report remotely.
            Until(()=>Response().Phase=="Reporting",2400);Ticks(90);
            Assert.That(runtime.CaptureSession().Incident.ReportTick,Is.LessThan(0));
            before=JsonUtility.ToJson(Response());runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);Assert.That(JsonUtility.ToJson(Response()),Is.EqualTo(before));
            var invalid=runtime.CaptureSession();invalid.ResidentResponses.Single(t=>t.ActorId=="CH_03").ObservationId="ANOTHER_PERSONS_RECEIPT";
            var error=Assert.Throws<TargetInvocationException>(()=>typeof(MansionRuntime).GetMethod("ValidateSession",Private).Invoke(runtime,new object[]{invalid}));Assert.That(error.InnerException,Is.TypeOf<System.IO.InvalidDataException>());
            Until(()=>Response().Phase=="Reported",400);
            var reported=runtime.CaptureSession();Assert.That(reported.Incident.Reporter,Is.EqualTo("CH_03"));Assert.That(reported.Proceedings.PendingReportCases,Is.EquivalentTo(new[]{CaseId}));
            Assert.That(runtime.Knowledge.For("CH_05").Records().Any(r=>r.Value=="Collapsed"||r.Value=="BodyReport:CH_02"),Is.False);
            Until(()=>runtime.CaptureSession().Incident.ConfirmationTick>=0,2400);
            Assert.That(runtime.World.Events.Any(e=>e.Type=="DeathConfirmed"),Is.True);Assert.That(runtime.CaptureSession().Proceedings.PendingReportCases,Is.Empty);
            runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);yield return null;
        }
        [UnityTest]public IEnumerator InterruptedReportKeepsOnlyAudibleFragmentAndResumesFromOwnKnowledge()
        {
            AuthorCompletedTestOutcome();Until(()=>runtime.CaptureSession().ResidentResponses.Any(t=>t.Phase=="Reporting"),3000);Ticks(120);
            runtime.AnnouncementRoomIsSilent=room=>room=="R_HALL";Ticks(1);
            Assert.That(Response().Phase,Is.EqualTo("SeekingYusti"));Assert.That(runtime.CaptureSession().Incident.ReportTick,Is.LessThan(0));
            Assert.That(runtime.Knowledge.For("CH_03").Records().Any(r=>r.Predicate=="HeardFragment"&&r.Value=="BodyReport:CH_02"),Is.True);
            runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);Ticks(400);Assert.That(runtime.CaptureSession().Incident.ReportTick,Is.LessThan(0));
            runtime.AnnouncementRoomIsSilent=null;Until(()=>Response().Phase=="Reported",3000);
            Assert.That(runtime.World.Events.Count(e=>e.Type=="BodyReportReceived"&&e.Actor=="CH_03"),Is.EqualTo(1));yield return null;
        }
        [UnityTest]public IEnumerator WallBlocksDiscoveryAndUnknownPresenterPositionIsNotASecretSearchTarget()
        {
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Test body visibility blocker";wall.transform.position=bodyPoint+Vector3.back*.75f+Vector3.up;wall.transform.localScale=new Vector3(2,2.2f,.15f);Physics.SyncTransforms();
            PutPresenter(runtime.Layout.Room("R_WORK").WalkPoint);AuthorCompletedTestOutcome();Ticks(200);
            Assert.That(runtime.CaptureSession().ResidentResponses,Is.Empty);Assert.That(runtime.CaptureSession().Incident.DiscoveryTick,Is.LessThan(0));
            UnityEngine.Object.Destroy(wall);yield return null;Ticks(180);
            Assert.That(Response().Phase,Is.EqualTo("SeekingYusti"));Ticks(600);
            Assert.That(runtime.Knowledge.For("CH_03").Records().Any(r=>r.SubjectId=="PRES_YUSTI"),Is.False);
            Assert.That(runtime.CaptureSession().Incident.ReportTick,Is.LessThan(0));
            var a=runtime.World.Resident("CH_03");Assert.That(runtime.Layout.RoomAt(MansionRuntime.V(a.Position)).RoomId,Is.EqualTo("R_HALL"));
            if(a.Destination!="")Assert.That(runtime.Layout.NavigationNodes.Single(n=>n.Id==a.Destination).RoomId,Is.EqualTo("R_HALL"));
            runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);yield return null;
        }
    }
}
