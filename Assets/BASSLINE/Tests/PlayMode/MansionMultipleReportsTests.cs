using System;
using System.Collections;
using System.IO;
using System.Linq;
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
    public sealed class MansionMultipleReportsTests
    {
        MansionRuntime runtime; string storage;
        sealed class Stationary : IMansionPhysics
        {
            public MansionWorld World;
            public Point3 Move(string id,Point3 delta)=>World.Resident(id).Position;
            public bool DoorClear(string id)=>true;
            public void DoorPose(string id,double amount){}
        }
        [UnityTest,Timeout(180000)] public IEnumerator TestOnlyRecordedOutcomesReportInReverseOrderAndRestoreInspection()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;
            runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;
            storage=runtime.UseIsolatedTestStorage();
            var hud=UnityEngine.Object.FindAnyObjectByType<FixtureHud>();
            if(hud.CurrentScreen==38)hud.GetComponentsInChildren<ProductionScreenView>(true).Single(v=>v.gameObject.activeInHierarchy).Actions[0].onClick.Invoke();
            hud.enabled=false;foreach(var owner in runtime.World.Capture().PauseOwners)runtime.Pause(owner,false);
            runtime.Routines=Array.Empty<ResidentRoutine>();
            // Author only initial bodies and two recorded outcomes. This is a reporting fixture,
            // not proof of autonomous production causes, player travel, or court seating.
            Place("CH_02",new Vector3(-4,0,-4));Place("CH_03",new Vector3(4,0,-4));
            runtime.Presenter.Capsule.enabled=false;runtime.Presenter.transform.position=new Vector3(0,0,-3);runtime.Presenter.Capsule.enabled=true;
            var session=runtime.CaptureSession();session.Proceedings.PresenterPosition=MansionRuntime.P(runtime.Presenter.transform.position);
            var nodes=runtime.Layout.NavigationNodes.Select(n=>new MansionNode{Id=n.Id,Room=n.RoomId,Position=MansionRuntime.P(n.Position)}).ToArray();
            var edges=runtime.Layout.NavigationEdges.Select(e=>new MansionEdge{From=nodes[e.From].Id,To=nodes[e.To].Id,Door=runtime.Layout.Connection(e.ConnectionId)?.DoorId??""}).ToArray();
            var world=new MansionWorld(session.World,nodes,edges);var physics=new Stationary{World=world};
            var cases=new[]{OutcomeSettings("CASE_REPORT_A","CH_04","CH_02",1),OutcomeSettings("CASE_REPORT_B","CH_05","CH_03",2)};
            var snapshots=cases.Select(s=>new MansionIncidentSnapshot{Settings=s,Stage="ResultCommitted",FatalityCap=2,TracePresent=true,CauseTick=world.Tick,DueTick=world.Tick+s.DelayTicks,CausePosition=world.Resident(s.TargetId).Position}).ToArray();
            foreach(var c in snapshots){Assert.That(world.RegisterCase(c.Settings),Is.EqualTo("Registered"));Assert.That(world.ReserveCaseOutcome(c.Settings.Id),Is.True);c.CauseEvent="M_EVENT_"+world.CommitCaseCause(c.Settings.Id,c.DueTick);}
            foreach(var c in snapshots){world.Step(default,false,physics);c.ResultEvent="M_EVENT_"+world.CommitCaseResult(c.Settings.Id);c.ResultTick=world.Tick;c.ResultPosition=world.Resident(c.Settings.TargetId).Position;world.SealCaseOutcomes();}
            session.World=world.Capture();session.CaseCollectionVersion=1;session.Incidents=snapshots;session.Incident=snapshots[0];session.OptionalObjects|=17;
            LoadFixture(session,"two-outcomes.dat");
            Assert.That(runtime.World.CaseBook.AdjudicatedCaseId,Is.EqualTo(cases[0].Id));
            Assert.That(runtime.ReadNotebook().Records.Any(r=>r.Predicate=="ConfirmedDeath"),Is.False);

            Discover("CH_03");
            Assert.That(runtime.CaptureSession().Incidents[0].DiscoveryTick,Is.EqualTo(-1),"Clicking B must not discover hidden A.");
            Report();
            Assert.That(runtime.CaptureSession().Proceedings.InspectionCaseId,Is.EqualTo(cases[1].Id));
            RoundTrip("during-second-inspection.dat");Place("CH_01",new Vector3(0,0,-7));
            yield return UntilConfirmed(cases[1].Id);
            var first=runtime.CaptureSession().Proceedings;
            Assert.That(first.PublicSchedule.Publications.Single().DeathId,Does.Contain(cases[1].Id));
            Assert.That(first.ConveneAt-first.FirstAnnouncementTick,Is.EqualTo(216000));

            Discover("CH_02");Report();Place("CH_01",new Vector3(0,0,-7));
            yield return UntilConfirmed(cases[0].Id);
            RoundTrip("both-confirmed.dat");var completed=runtime.CaptureSession();
            Assert.That(completed.Proceedings.PendingReportCases,Is.Empty);
            Assert.That(completed.Proceedings.InspectionCaseId,Is.Empty);
            Assert.That(completed.Proceedings.PublicSchedule.Revision,Is.EqualTo(2));
            Assert.That(completed.Proceedings.PublicSchedule.Publications.Select(n=>n.DeathId),Is.EqualTo(new[]{"DEATH_CASE_REPORT_B_CH_03","DEATH_CASE_REPORT_A_CH_02"}));
            Assert.That(completed.Proceedings.ConveneAt,Is.GreaterThanOrEqualTo(first.ConveneAt));
            Assert.That(runtime.World.CaseBook.AdjudicatedCaseId,Is.EqualTo(cases[0].Id),"Report order cannot change the earliest result's adjudication.");
            Assert.That(runtime.World.Events.Count(e=>e.Type=="DeathConfirmed"),Is.EqualTo(2));
            Assert.That(completed.Proceedings.ImportedReceipts.Distinct().Count(),Is.EqualTo(completed.Proceedings.ImportedReceipts.Length));
            var reports=runtime.ReadNotebook().Records.Where(r=>r.Kind=="OfficialReport").ToArray();
            Assert.That(reports.Select(r=>r.SubjectId),Is.EquivalentTo(new[]{"CH_02","CH_03"}));
            Assert.That(reports.Select(r=>r.ProvenanceKey).Distinct().Count(),Is.EqualTo(2));
        }
        MansionCaseSettings OutcomeSettings(string id,string actor,string victim,int delay)
        {
            var node=runtime.Layout.NavigationNodes[runtime.Layout.NearestNode(runtime.Bodies.Single(b=>b.ActorId==victim).transform.position)];
            return new MansionCaseSettings{Enabled=true,ExplicitTestSession=true,Id=id,ActorId=actor,TargetId=victim,ObjectId=runtime.World.Objects.First().Id,ToolNode=node.Id,ContactNode=node.Id,ChapterStartTick=0,DecisionReason="TestOnly recorded outcome",IntentSpeech="TestOnly",RejectedAlternatives=new[]{"Withdraw","AskForHelp"},ApproachTick=0,EarliestCauseTick=0,OpportunityEndTick=1000,MinimumIntentTicks=1,ContactTicks=1,DelayTicks=delay};
        }
        void LoadFixture(MansionSessionSnapshot snapshot,string name)
        {
            string json=JsonUtility.ToJson(snapshot),path=Path.Combine(storage,name);File.WriteAllText(path,AtomicSaveStore.Hash(json)+"\n"+json);runtime.LoadFrom(path);
        }
        void RoundTrip(string name)
        {
            string path=Path.Combine(storage,name);runtime.SaveTo(path);string before=JsonUtility.ToJson(runtime.CaptureSession());runtime.LoadFrom(path);Assert.That(JsonUtility.ToJson(runtime.CaptureSession()),Is.EqualTo(before));
        }
        void Place(string id,Vector3 position)
        {
            var body=runtime.Bodies.Single(b=>b.ActorId==id);body.Capsule.enabled=false;body.transform.position=position;body.Capsule.enabled=true;
            var actor=runtime.World.Resident(id);actor.Position=MansionRuntime.P(position);actor.Node=runtime.Layout.NavigationNodes[runtime.Layout.NearestNode(position)].Id;actor.Phase="Performing";actor.Activity="TestOnly_Wait";actor.ActivityTicks=360000;actor.Destination="";actor.Path=Array.Empty<string>();actor.PathCursor=0;Physics.SyncTransforms();
        }
        void Look(string id)
        {
            var target=id=="PRES_YUSTI"?runtime.Presenter:runtime.Bodies.Single(b=>b.ActorId==id);
            var player=runtime.Bodies.Single(b=>b.ActorId=="CH_01");var delta=target.transform.position+Vector3.up*target.Height*.8f-(player.transform.position+Vector3.up*player.Height*.88f);
            runtime.SetLook(Quaternion.LookRotation(delta).eulerAngles.y,-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg);Physics.SyncTransforms();
        }
        void Discover(string id){Place("CH_01",runtime.Bodies.Single(b=>b.ActorId==id).transform.position+Vector3.back*1.3f);Look(id);Assert.That(runtime.Interact(id),Does.Contain("유스티"));}
        void Report(){Place("CH_01",runtime.Presenter.transform.position+Vector3.back*1.3f);Look("PRES_YUSTI");Assert.That(runtime.Interact("PRES_YUSTI"),Does.Contain("접수"));}
        IEnumerator UntilConfirmed(string id)
        {
            for(int i=0;i<18000;i++){runtime.AdvanceOne();if(i%60==0&&runtime.CaptureSession().Incidents.Single(c=>c.Settings.Id==id).ConfirmationTick>=0)yield break;if(i%300==0)yield return null;}
            Assert.Fail("Presenter did not physically confirm "+id+": "+JsonUtility.ToJson(runtime.CaptureSession().Proceedings));
        }
    }
}
