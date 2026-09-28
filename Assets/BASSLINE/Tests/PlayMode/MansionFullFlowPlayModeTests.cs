using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using BASSLINE.AuthoringData;
using BASSLINE.Bootstrap;
using BASSLINE.Core;
using BASSLINE.UI;

namespace BASSLINE.Tests
{
    public sealed class MansionFullFlowPlayModeTests
    {
        MansionRuntime runtime;
        FixtureActorBody player;
        string storage,phase="Setup";
        double maxPhysicalStep;
        Vector3 contactPoint,announcementVantage;
        Vector3[] witnessApproach=Array.Empty<Vector3>();
        long observationUntil=-1,firstSampledObservationBreakTick=-1;
        Vector3 observerAtBreak,observerForwardAtBreak,actorAtBreak,targetAtBreak;
        bool actorVisibleAtBreak,targetVisibleAtBreak;
        Vector3 routeDestination,routeWaypoint,routeDetour;
        int routeCursor,routeLength;string routeNode="",routeBlocker="";bool routeDetouring;
        bool tracingGrandStair;

        [UnitySetUp] public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");
            // Let the actual HUD Start initialize ports/fonts and its title flow before disabling Update.
            yield return null;
            runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();
            Assert.That(runtime,Is.Not.Null);runtime.AutomaticTick=false;
#if UNITY_EDITOR
            storage=runtime.UseIsolatedTestStorage();
#else
            Assert.Ignore("This explicit authoring fixture requires isolated Editor test storage.");
#endif
            Assert.That(Path.GetFullPath(runtime.SavePath).StartsWith(Path.GetFullPath(storage)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
            var hud=UnityEngine.Object.FindAnyObjectByType<FixtureHud>();
            MansionUiTestEntry.Start(hud);
            hud.enabled=false;
            foreach(string owner in runtime.World.Capture().PauseOwners)runtime.Pause(owner,false);
            player=runtime.Bodies.Single(b=>b.ActorId=="CH_01");
            Assert.That(runtime.CaptureSession().Incident.Settings,Is.Null,"Normal M01 must remain X31 OFF.");
            Directory.CreateDirectory("Verification");File.WriteAllText("Verification/BL22-grand-stair-trace.csv","tick,phase,x,y,z,cursor,next,recovering\n");
        }

        [UnityTest,Timeout(1800000)] public IEnumerator M01_TestOnly_X31_PhysicalReportGatheringVerdictAndDurableSettlement()
        {
            AuthorInitialTestOnlyConditions();
            Phase("Actual contact and delayed result");
            yield return Until(()=>runtime.World.Events.Any(e=>e.Type=="IncidentCauseCommitted"),1800);
            var caused=runtime.CaptureSession().Incident;
            observationUntil=caused.DueTick;
            Assert.That(runtime.World.Resident("CH_02").Alive,Is.True);
            Assert.That(runtime.World.Object("M_NOTEBOOK").Owner,Is.EqualTo("CH_04"));
            Assert.That(runtime.ReadNotebook().Records.Any(r=>r.Predicate=="CausedOutcome"||r.Predicate=="ConfirmedDeath"),Is.False,"A remote player must not receive A.");
            yield return Until(()=>!runtime.World.Resident("CH_02").Alive,900);
            Assert.That(runtime.CaptureSession().Incident.ResultTick-caused.CauseTick,Is.EqualTo(300));
            Assert.That(runtime.World.Events.Count(e=>e.Type=="IncidentCauseCommitted"),Is.EqualTo(1));
            Assert.That(runtime.World.Events.Any(e=>e.Type=="DeathConfirmed"),Is.False,"A result is not an official confirmation.");

            Phase("Physical discovery and witness statement");
            yield return WalkTo(contactPoint,9000,.9f);
            LookAtBody("CH_02");
            Assert.That(runtime.CanSee("CH_01","CH_02"),Is.True,"The player must actually look at the body before discovery. "+Diagnostic());
            Assert.That(runtime.Interact("CH_02"),Does.Contain("유스티"),Diagnostic());
            Assert.That(runtime.CaptureSession().Incident.DiscoveryTick,Is.GreaterThanOrEqualTo(0));
            // The fixture's waiting actor occupies the nearest graph node. Approach around that
            // body on this explicitly authored level-floor route, stopping at the real talk port's range.
            foreach(var waypoint in witnessApproach){
                if(runtime.DescribeTarget("CH_03").Available)break;
                yield return WalkFixtureSegment(waypoint,1800,"CH_03");
            }
            Assert.That(runtime.DescribeTarget("CH_03").Available,Is.True,Diagnostic());
            LookAtBody("CH_03");
            Assert.That(runtime.AskRecentObservation("CH_03"),Is.EqualTo("Dialogue"),Diagnostic());
            yield return Until(()=>!runtime.ReadConversationPlayback().Speaking,1200);runtime.EndConversation();
            Assert.That(runtime.Knowledge.For("CH_03").Records().Any(r=>r.Predicate=="CausedOutcome"&&r.SubjectId=="CH_04"),Is.True,"The fixture witness must actually see contact through collapse.");
            var received=runtime.ReadNotebook().Records.FirstOrDefault(r=>!r.Direct&&r.Source=="CH_03");
            Assert.That(received,Is.Not.Null,"The latest observation must really be spoken, regardless of whether it is causal evidence.");
            Assert.That(received.Direct,Is.False,"A received witness statement must retain its source chain.");
            Assert.That(received.Source,Is.EqualTo("CH_03"));

            Phase("Report delivered to Yusti and real inspection");
            yield return WalkTo(runtime.Presenter.transform.position,9000,1.6f);
            LookAtBody("PRES_YUSTI");
            Assert.That(runtime.Interact("PRES_YUSTI"),Does.Contain("접수"),Diagnostic());
            yield return WalkTo(announcementVantage,9000);
            yield return Until(()=>runtime.World.Events.Any(e=>e.Type=="DeathConfirmed"),9000);
            yield return Until(()=>runtime.ReadNotebook().Records.Any(r=>r.Kind=="OfficialReport"),1800);
            var confirmed=runtime.CaptureSession();
            Assert.That(confirmed.Incident.ReportTick,Is.GreaterThanOrEqualTo(confirmed.Incident.DiscoveryTick));
            Assert.That(confirmed.Incident.ConfirmationTick-confirmed.Incident.ReportTick,Is.GreaterThanOrEqualTo(180));
            Assert.That(confirmed.Proceedings.ConveneAt-confirmed.Proceedings.FirstAnnouncementTick,Is.EqualTo(60*60*60));
            var official=runtime.ReadNotebook().Records.Single(r=>r.Kind=="OfficialReport");
            Assert.That(official.Source,Is.EqualTo("PRES_YUSTI"));
            Assert.That(official.SubjectId,Is.EqualTo("CH_02"));
            Assert.That(official.Predicate,Is.EqualTo("ConfirmedDeath"));
            Assert.That(runtime.HasReceivedCourtSummons("CH_01"),Is.False);
            Assert.That(runtime.ReadTrial().TruthAvailable,Is.False);
            string checkpoint=Path.Combine(storage,"after-confirmation.dat");
            runtime.SaveTo(checkpoint);string exact=JsonUtility.ToJson(runtime.CaptureSession());runtime.LoadFrom(checkpoint);
            Assert.That(JsonUtility.ToJson(runtime.CaptureSession()),Is.EqualTo(exact),"Confirmation and actual recipients must round-trip exactly.");

            Phase("Full announced 60-minute investigation: ordinary ticks, no clock jump");
            long convene=confirmed.Proceedings.ConveneAt;
            while(runtime.World.Tick<convene){
                int batch=(int)Math.Min(600,convene-runtime.World.Tick);
                for(int i=0;i<batch;i++)Step();
                if(runtime.World.Tick%36000<batch)Phase("Investigation: "+((runtime.World.Tick-confirmed.Proceedings.FirstAnnouncementTick)/3600)+" / 60 world minutes");
                Assert.That(runtime.World.Tick,Is.LessThanOrEqualTo(convene));
                if(runtime.World.Tick<convene)Assert.That(runtime.HasReceivedCourtSummons("CH_01"),Is.False,"A schedule is not a summons.");
                yield return null;
            }
            Assert.That(runtime.ReadTrial().TruthAvailable,Is.False);
            Phase("Local summons receipts and physical seats");
            yield return Until(()=>runtime.HasReceivedCourtSummons("CH_01"),30*60*60);
            var summons=runtime.ReadNotebook().Records.Single(r=>r.Predicate=="CourtSummons");
            Assert.That(summons.ReceivedTick,Is.GreaterThanOrEqualTo(convene));
            var seat=runtime.Layout.Anchor("SEAT_TRIAL_01");
            // Court admission freezes the world at the product's <.85 m seat threshold.
            yield return WalkTo(runtime.Layout.NavigationNodes[seat.ApproachNode].Position,20*60*60,.85f);
            yield return Until(()=>runtime.ReadTrial().Phase=="Debate",30*60*60);
            Assert.That(runtime.PresenterReadyForCourt(),Is.True);
            Assert.That(runtime.World.Residents.Where(r=>r.Alive&&r.Present).All(r=>runtime.HasReceivedCourtSummons(r.Id)),Is.True);
            foreach(var actor in runtime.World.Residents.Where(r=>r.Alive&&r.Present)){
                var ownSeat=runtime.Layout.Anchor("SEAT_TRIAL_"+actor.Id.Substring(3));
                Assert.That(actor.Position.Distance(MansionRuntime.P(runtime.Layout.NavigationNodes[ownSeat.ApproachNode].Position)),Is.LessThan(.85),actor.Id);
            }
            Assert.That(runtime.World.HasPause("M_COURT"),Is.True);
            long frozenWorldTick=runtime.World.Tick;

            Phase("Received testimony and normal voting APIs");
            bool votingOpened=false;
            for(int tick=0;tick<30000;tick++){
                Step();var trial=runtime.ReadTrial();
                // Only claims actually visible to the player are acknowledged; never read hidden pending speech.
                foreach(var claim in trial.Claims)runtime.RetireTrialClaim(claim.Id);
                if(runtime.OpenTrialVoting()=="Opened"){votingOpened=true;break;}
                if(tick%300==0)yield return null;
            }
            Assert.That(votingOpened,Is.True,Diagnostic());
            Assert.That(runtime.World.Tick,Is.EqualTo(frozenWorldTick));
            Assert.That(runtime.ReadTrial().TruthAvailable,Is.False);
            received=runtime.ReadNotebook().Records.FirstOrDefault(r=>r.Predicate=="CausedOutcome"&&r.SubjectId=="CH_04");
            Assert.That(received,Is.Not.Null,"The causal testimony must reach the player through actual court speech before choosing that subject.");
            string vote=runtime.CastTrialVote(received.SubjectId);
            Assert.That(vote,Is.EqualTo("VotesLocked"),Diagnostic());
            string verdict=runtime.ContinueTrial();
            if(verdict=="RevoteOpened"){
                var candidates=runtime.ReadTrial().VoteCandidates;
                string choice=candidates.Contains(received.SubjectId)?received.SubjectId:candidates.OrderBy(id=>id,StringComparer.Ordinal).First();
                Assert.That(runtime.CastTrialVote(choice),Is.EqualTo("VotesLocked"),Diagnostic());verdict=runtime.ContinueTrial();
            }
            Assert.That(verdict,Is.EqualTo("VerdictCommitted"),Diagnostic());
            Assert.That(runtime.ReadTrial().TruthAvailable,Is.True);

            Phase("Truth, evaluation and durable idempotent reward");
            for(int i=0;i<32&&runtime.ReadTrial().Phase!="Evaluation";i++)RequireProgress(runtime.ContinueTrial());
            Assert.That(runtime.ReadTrial().Phase,Is.EqualTo("Evaluation"),Diagnostic());
            string beforeReward=Path.Combine(storage,"before-reward.dat");runtime.SaveTo(beforeReward);
            Assert.That(runtime.ContinueTrial(),Is.EqualTo("RewardApplied"),Diagnostic());
            string profile=Path.Combine(storage,"profile-v1.dat");Assert.That(File.Exists(profile),Is.True);
            string firstProfile=File.ReadAllText(profile);runtime.LoadFrom(beforeReward);
            Assert.That(runtime.ContinueTrial(),Is.EqualTo("RewardApplied"),Diagnostic());
            Assert.That(File.ReadAllText(profile),Is.EqualTo(firstProfile),"Reloading an old evaluation must not pay the same reward again.");

            Phase("Atomic settlement and next chapter");
            var decision=runtime.ReadTrial();
            // Residual is committed by ApplySettlement, not during the preceding reward screen.
            int residual=runtime.World.Residents.Count(r=>r.Alive&&r.Present&&!decision.Executed.Contains(r.Id)&&!decision.Escaped.Contains(r.Id));
            Assert.That(runtime.ContinueTrial(),Is.EqualTo("SettlementApplied"),Diagnostic());
            Assert.That(runtime.ReadTrial().Residual,Is.EqualTo(residual));
            Assert.That(runtime.World.Residents.Count(r=>r.Alive&&r.Present),Is.EqualTo(residual));
            foreach(string executed in decision.Executed)Assert.That(runtime.World.Resident(executed).Alive,Is.False);
            foreach(string escaped in decision.Escaped)Assert.That(runtime.World.Resident(escaped).Present,Is.False);
            Assert.That(runtime.World.Events.Count(e=>e.Type=="SettlementCommitted"),Is.EqualTo(1));
            runtime.LoadFrom(runtime.SavePath);
            Assert.That(runtime.ReadTrial().Phase,Is.EqualTo("Settlement"));
            Assert.That(runtime.World.Residents.Count(r=>r.Alive&&r.Present),Is.EqualTo(residual));
            Assert.That(residual,Is.GreaterThan(3),"One 18-person chapter must not trigger a loop reset.");
            Assert.That(runtime.ContinueTrial(),Is.EqualTo("NextChapterReady"),Diagnostic());
            Assert.That(runtime.World.Chapter,Is.EqualTo(2));Assert.That(runtime.World.Loop,Is.EqualTo(1));
            Assert.That(runtime.World.Residents.Count(r=>r.Alive&&r.Present),Is.EqualTo(residual));
            Assert.That(runtime.ReadTrial().Phase,Is.EqualTo("NotStarted"));
            Assert.That(runtime.CaptureSession().Incident.Settings,Is.Null,"The next chapter must not automatically register the test recipe.");
            Assert.That(runtime.World.HasPause("M_COURT"),Is.False);
            Assert.That(maxPhysicalStep,Is.LessThan(.4),"Every post-setup move must use ordinary physical locomotion.");
            runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);
            Assert.That(runtime.World.Chapter,Is.EqualTo(2));
            Phase("Completed-case archive through actual notebook UI");
            string beforeArchive=JsonUtility.ToJson(runtime.Knowledge.Capture());
            var archived=runtime.ReadArchive().Cases.Single();
            Assert.That(archived.CaseId,Is.EqualTo(caused.Settings.Id));
            Assert.That(archived.Records.Count,Is.GreaterThan(0));
            Assert.That(archived.ClockVersion,Is.EqualTo(runtime.World.ClockVersion));
            var hud=UnityEngine.Object.FindAnyObjectByType<FixtureHud>();hud.OpenNotePage(13);
            var archiveView=hud.GetComponentsInChildren<ProductionScreenView>(true).Single(v=>v.ScreenId=="UI_13");
            Assert.That(archiveView.Body.text,Does.Contain("표결로 지목한 사람"));
            Assert.That(archiveView.Body.text,Does.Not.Contain("아직 돌아볼 사건"));
            Assert.That(JsonUtility.ToJson(runtime.Knowledge.Capture()),Is.EqualTo(beforeArchive));
            runtime.SaveTo(runtime.SavePath);runtime.LoadFrom(runtime.SavePath);hud.SyncPauseView();
            Assert.That(hud.CurrentScreen,Is.EqualTo(13));
            Assert.That(runtime.ReadArchive().Cases.Count,Is.EqualTo(1));
            Directory.CreateDirectory("Verification");File.WriteAllText("Verification/mansion-full-flow-testonly.json",JsonUtility.ToJson(runtime.CaptureSession(),true));
            TestContext.Progress.WriteLine("PASS TestOnly M01 X31 complete flow; production X31 remains OFF. Storage="+storage+" maxPhysicalStep="+maxPhysicalStep);
        }

        void AuthorInitialTestOnlyConditions()
        {
            // Explicit test authoring happens once before the first measured tick. It is not travel,
            // a normal-mode event, a canonical culprit assignment, or an injected personal memory.
            var layout=runtime.Layout;int contact=layout.NearestNode(new Vector3(-4,0,-4),"R_HALL");
            contactPoint=layout.NavigationNodes[contact].Position;
            int tool=layout.NavigationNodes.Select((n,i)=>new{n,i}).Where(x=>x.n.RoomId=="R_HALL"&&x.i!=contact&&Vector3.Distance(x.n.Position,contactPoint)>1&&Vector3.Distance(x.n.Position,contactPoint)<4)
                .OrderBy(x=>Vector3.Distance(x.n.Position,contactPoint-Vector3.right*2)).First().i;
            Vector3 toolPoint=layout.NavigationNodes[tool].Position,forward=(contactPoint-toolPoint).normalized;forward.y=0;forward.Normalize();Vector3 right=Vector3.Cross(Vector3.up,forward);
            announcementVantage=contactPoint-right*1.4f-forward*.5f;
            witnessApproach=new[]{contactPoint+forward*.7f-right*1.2f,contactPoint-forward*1.4f-right*1.2f,contactPoint-forward*1.4f+right*.1f};
            PlaceInitial("CH_04",toolPoint,tool,forward);
            // Explicit positive-fixture schedule, authored before tick 1. The incident's completed
            // contact activity advances its ordinary schedule cursor once; its next authored stop
            // is therefore this 120-second wait. It then resumes the existing destination list.
            // The v2 negative receipt preserves the valid discontinuous-view outcome when it left.
            var originalRoutine=runtime.Routines.Single(r=>r.ActorId=="CH_04");
            var witnessableRoutine=new ResidentRoutine{ActorId="CH_04",DwellTicks=120*60,
                Destinations=new[]{layout.NavigationNodes[tool].Id,layout.NavigationNodes[contact].Id}.Concat(originalRoutine.Destinations).ToArray(),
                Activities=new[]{"TestOnly_PreContactWait","TestOnly_PostContactWait"}.Concat(originalRoutine.Activities).ToArray()};
            runtime.Routines=runtime.Routines.Select(r=>r.ActorId=="CH_04"?witnessableRoutine:r).ToArray();
            PlaceInitial("CH_02",contactPoint+right*.5f+forward*.38f,contact,-forward);
            Vector3 witness=contactPoint+right*1.8f-forward*1.5f;
            PlaceInitial("CH_03",witness,layout.NearestNode(witness,"R_HALL"),(contactPoint-witness).normalized);
            var item=runtime.World.Object("M_NOTEBOOK");item.Position=MansionRuntime.P(toolPoint+forward*.55f+Vector3.up*.7f);
            runtime.ObjectBodies.Single(o=>o.ObjectId==item.Id).transform.position=MansionRuntime.V(item.Position);
            Physics.SyncTransforms();
            Assert.That(runtime.ConfigureTestCase(new MansionCaseSettings{Enabled=true,ExplicitTestSession=true,Id="CASE_M01_FULLFLOW_TEST",ActorId="CH_04",TargetId="CH_02",ObjectId=item.Id,ToolNode=layout.NavigationNodes[tool].Id,ContactNode=layout.NavigationNodes[contact].Id,DecisionReason="TestOnly X31 인과 연결 시험",IntentSpeech="이 시험에서 가상 소품을 대상에게 접촉시키겠다.",RejectedAlternatives=new[]{"Withdraw","AskForHelp"},ChapterStartTick=runtime.World.Tick,ApproachTick=runtime.World.Tick+60,EarliestCauseTick=runtime.World.Tick+300,OpportunityEndTick=runtime.World.Tick+3600,MinimumIntentTicks=120,ContactTicks=30,DelayTicks=300}),Is.EqualTo("Configured"));
        }
        void PlaceInitial(string id,Vector3 point,int node,Vector3 forward)
        {
            var body=runtime.Bodies.Single(b=>b.ActorId==id);body.Capsule.enabled=false;body.transform.position=point;body.transform.rotation=Quaternion.LookRotation(forward);body.Head.rotation=body.transform.rotation;body.Capsule.enabled=true;
            var actor=runtime.World.Resident(id);actor.Position=MansionRuntime.P(point);actor.Node=runtime.Layout.NavigationNodes[node].Id;actor.Yaw=body.transform.eulerAngles.y;actor.Phase="Performing";actor.Activity="TestOnly_Wait";actor.ActivityTicks=120*60*60;actor.Destination="";
        }
        IEnumerator Until(Func<bool> completed,int ticks)
        {
            for(int i=0;i<ticks;i++){
                if(i%60==0&&completed())yield break;
                Step();if(i%600==0)yield return null;
            }
            Assert.That(completed(),Is.True,Diagnostic());
        }
        IEnumerator WalkTo(Vector3 destination,int ticks,float stopDistance=.3f)
        {
            var layout=runtime.Layout;var path=layout.FindPath(layout.NearestNode(player.transform.position),layout.NearestNode(destination));
            Assert.That(path,Is.Not.Empty,Diagnostic());int cursor=0;
            routeDestination=destination;routeLength=path.Length;routeDetouring=false;routeBlocker="";
            for(int i=0;i<ticks;i++){
                if(Vector3.Distance(player.transform.position,destination)<stopDistance){runtime.SetMove(0,0);runtime.SetRun(false);for(int n=0;n<12;n++)Step();yield break;}
                while(cursor<path.Length&&Vector3.Distance(player.transform.position,layout.NavigationNodes[path[cursor]].Position)<.22f)cursor++;
                if(i%15==0)foreach(var door in layout.Connections.Where(d=>!string.IsNullOrEmpty(d.DoorId)&&runtime.DescribeTarget(d.DoorId).Available))runtime.Interact(door.DoorId);
                Vector3 target=cursor<path.Length?layout.NavigationNodes[path[cursor]].Position:destination;
                routeCursor=cursor;routeNode=cursor<path.Length?layout.NavigationNodes[path[cursor]].Id:"Destination";routeWaypoint=target;
                // Preserve authored edges, including legitimate floor/step transitions. Collision
                // queries here provide diagnostics only; they never invent a side route.
                if(i%30==0)routeBlocker=BlockingCollider(target);
                Vector3 delta=target-player.transform.position;delta.y=0;var direction=delta.normalized;
                if(direction.sqrMagnitude>.01f)runtime.SetLook(Quaternion.LookRotation(direction).eulerAngles.y,0);
                runtime.SetRun(false);runtime.SetMove(direction.x,direction.z);Step();if(i%300==0)yield return null;
            }
            runtime.SetMove(0,0);Assert.Fail("Physical player route failed toward "+destination+". "+Diagnostic());
        }
        IEnumerator WalkFixtureSegment(Vector3 destination,int ticks,string interactionTarget)
        {
            routeDestination=destination;routeWaypoint=destination;routeNode="TestOnly_AuthoredWitnessApproach";routeCursor=0;routeLength=1;routeDetouring=false;
            for(int i=0;i<ticks;i++){
                if(runtime.DescribeTarget(interactionTarget).Available||Vector3.Distance(player.transform.position,destination)<.15f){
                    runtime.SetMove(0,0);for(int n=0;n<12;n++)Step();yield break;
                }
                if(!ClearFixtureSegment(destination,out var blocker)){
                    routeBlocker=blocker;runtime.SetMove(0,0);Step();if(i%300==0)yield return null;continue;
                }
                routeBlocker="";Vector3 delta=destination-player.transform.position;delta.y=0;var direction=delta.normalized;
                runtime.SetLook(Quaternion.LookRotation(direction).eulerAngles.y,0);runtime.SetRun(false);runtime.SetMove(direction.x,direction.z);Step();if(i%300==0)yield return null;
            }
            runtime.SetMove(0,0);Assert.Fail("Collision-checked fixture approach was blocked. "+Diagnostic());
        }
        string BlockingCollider(Vector3 destination)
        {
            Vector3 origin=player.transform.position,delta=destination-origin;delta.y=0;float distance=delta.magnitude;if(distance<.02f)return "";
            var hits=Physics.CapsuleCastAll(origin+Vector3.up*.32f,origin+Vector3.up*(player.Height-.3f),.275f,delta.normalized,distance,~0,QueryTriggerInteraction.Ignore);
            foreach(var hit in hits.OrderBy(h=>h.distance)){
                if(hit.collider.transform.IsChildOf(player.transform))continue;
                var body=hit.collider.GetComponentInParent<FixtureActorBody>();
                return (body?body.ActorId+" / ":"")+hit.collider.name+" at "+hit.point.ToString("F3");
            }
            return "";
        }
        bool ClearFixtureSegment(Vector3 destination,out string blocker)
        {
            blocker=BlockingCollider(destination);if(blocker!="")return false;
            Vector3 origin=player.transform.position,delta=destination-origin;
            if(Math.Abs(delta.y)>.1f){blocker="Fixture route has a height discontinuity";return false;}
            delta.y=0;float distance=delta.magnitude;int samples=Math.Max(1,Mathf.CeilToInt(distance/.2f));
            for(int sample=1;sample<=samples;sample++){
                Vector3 point=origin+delta*(sample/(float)samples);
                foreach(var offset in new[]{Vector3.zero,Vector3.right*.27f,Vector3.left*.27f,Vector3.forward*.27f,Vector3.back*.27f}){
                    if(!Physics.RaycastAll(point+offset+Vector3.up*.2f,Vector3.down,.5f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.normal.y>.7f&&!h.collider.GetComponentInParent<FixtureActorBody>()&&Math.Abs(h.point.y-contactPoint.y)<=.1f&&Math.Abs(h.point.y-origin.y)<=.1f)){
                        blocker="Unsupported footprint/height discontinuity at "+(point+offset).ToString("F3");return false;
                    }
                }
            }
            return true;
        }
        void Step()
        {
            var tracked=runtime.World.Resident("CH_15");
            string next=tracked.PathCursor<tracked.Path.Length?tracked.Path[tracked.PathCursor]:"";
            bool onGrand=tracked.Phase=="Travelling"&&next.StartsWith("NAV_R_GRAND_STAIR",StringComparison.Ordinal);
            if(onGrand&&!tracingGrandStair)File.WriteAllText("Verification/BL22-grand-stair-entry-session.json",JsonUtility.ToJson(runtime.CaptureSession()));
            tracingGrandStair=onGrand;
            if(onGrand&&runtime.World.Tick%120==0){
                var p=tracked.Position;var ci=System.Globalization.CultureInfo.InvariantCulture;
                File.AppendAllText("Verification/BL22-grand-stair-trace.csv",runtime.World.Tick+","+phase+","+p.X.ToString("F4",ci)+","+p.Y.ToString("F4",ci)+","+p.Z.ToString("F4",ci)+","+tracked.PathCursor+","+next+","+tracked.Recovering+"\n");
            }
            var before=runtime.World.Residents.Select(a=>a.Position).ToArray();var presenter=runtime.Presenter.transform.position;
            runtime.AdvanceOne();int index=0;foreach(var actor in runtime.World.Residents)maxPhysicalStep=Math.Max(maxPhysicalStep,before[index++].Distance(actor.Position));
            maxPhysicalStep=Math.Max(maxPhysicalStep,Vector3.Distance(presenter,runtime.Presenter.transform.position));
            if(observationUntil>=runtime.World.Tick&&firstSampledObservationBreakTick<0){
                bool seesActor=runtime.CanSee("CH_03","CH_04"),seesTarget=runtime.CanSee("CH_03","CH_02");
                if(!seesActor||!seesTarget){
                    firstSampledObservationBreakTick=runtime.World.Tick;actorVisibleAtBreak=seesActor;targetVisibleAtBreak=seesTarget;
                    var observer=runtime.Bodies.Single(b=>b.ActorId=="CH_03");observerAtBreak=observer.transform.position;observerForwardAtBreak=observer.Head.forward;
                    actorAtBreak=runtime.Bodies.Single(b=>b.ActorId=="CH_04").transform.position;targetAtBreak=runtime.Bodies.Single(b=>b.ActorId=="CH_02").transform.position;
                }
            }
        }
        void LookAtBody(string id)
        {
            var target=id=="PRES_YUSTI"?runtime.Presenter:runtime.Bodies.Single(b=>b.ActorId==id);
            var camera=player.GetComponentInChildren<Camera>();
            // The route driver only faces its next waypoint. A separate ordinary mouse-look action
            // is required to inspect the person beside it; no visibility or knowledge state is changed.
            Vector3 direction=(target.transform.position+Vector3.up*target.Height*.65f-camera.transform.position).normalized;
            runtime.SetLook(Math.Atan2(direction.x,direction.z)*180/Math.PI,-Math.Asin(Math.Max(-1,Math.Min(1,direction.y)))*180/Math.PI);
            Physics.SyncTransforms();
        }
        [TearDown] public void SaveFailedPhysicalState()
        {
            if(!runtime||TestContext.CurrentContext.Result.Outcome.Status!=NUnit.Framework.Interfaces.TestStatus.Failed)return;
            try{
                var camera=player?player.GetComponentInChildren<Camera>():null;
                var receipt=new FailedPhysicalState{Utc=DateTime.UtcNow.ToString("O"),Editor=Application.unityVersion,Phase=phase,Tick=runtime.World.Tick,
                    Failure=TestContext.CurrentContext.Result.Message,Diagnostic=Diagnostic(),ContactPoint=contactPoint,AnnouncementVantage=announcementVantage,
                    CameraPosition=camera?camera.transform.position:default,CameraForward=camera?camera.transform.forward:default,CameraEuler=camera?camera.transform.eulerAngles:default,
                    CanSeeBody=runtime.CanSee("CH_01","CH_02"),CanReachBody=runtime.CanReach("CH_01","CH_02",2.5),MaxPhysicalStep=maxPhysicalStep,
                    FirstSampledObservationBreakTick=firstSampledObservationBreakTick,ObserverAtBreak=observerAtBreak,ObserverForwardAtBreak=observerForwardAtBreak,ActorAtBreak=actorAtBreak,TargetAtBreak=targetAtBreak,ActorVisibleAtBreak=actorVisibleAtBreak,TargetVisibleAtBreak=targetVisibleAtBreak,
                    RouteDestination=routeDestination,RouteWaypoint=routeWaypoint,RouteCursor=routeCursor,RouteLength=routeLength,RouteNode=routeNode,RouteBlockingCollider=routeBlocker,RouteDetouring=routeDetouring,RouteDetour=routeDetour,
                    Poses=runtime.Bodies.Concat(new[]{runtime.Presenter}).Where(b=>b).Select(b=>new PhysicalPose{ActorId=b.ActorId,Position=b.transform.position,Forward=b.transform.forward,HeadForward=b.Head.forward,RightHand=b.RightHand.position,Height=b.Height}).ToArray(),
                    SessionJson=JsonUtility.ToJson(runtime.CaptureSession())};
                Directory.CreateDirectory("Verification");File.WriteAllText("Verification/mansion-full-flow-failure.json",JsonUtility.ToJson(receipt,true));
                TestContext.Progress.WriteLine("Saved actual failed poses/session: Verification/mansion-full-flow-failure.json");
            }catch(Exception error){TestContext.Progress.WriteLine("Failed to capture physical diagnostics: "+error);}
        }
        [Serializable] sealed class FailedPhysicalState
        {
            public string Utc,Editor,Phase,Failure,Diagnostic,SessionJson;public long Tick;public double MaxPhysicalStep;public bool CanSeeBody,CanReachBody;
            public long FirstSampledObservationBreakTick;public Vector3 ObserverAtBreak,ObserverForwardAtBreak,ActorAtBreak,TargetAtBreak;public bool ActorVisibleAtBreak,TargetVisibleAtBreak;
            public Vector3 RouteDestination,RouteWaypoint,RouteDetour;public int RouteCursor,RouteLength;public string RouteNode,RouteBlockingCollider;public bool RouteDetouring;
            public Vector3 ContactPoint,AnnouncementVantage,CameraPosition,CameraForward,CameraEuler;public PhysicalPose[] Poses;
        }
        [Serializable] sealed class PhysicalPose{public string ActorId;public Vector3 Position,Forward,HeadForward,RightHand;public float Height;}
        void Phase(string value)
        {
            phase=value;string progress="M01 full-flow phase: "+phase+" tick="+runtime.World.Tick;
            TestContext.Progress.WriteLine(progress);Debug.Log(progress);
            Directory.CreateDirectory("Verification");File.WriteAllText("Verification/mansion-full-flow-progress.txt",DateTime.UtcNow.ToString("O")+"\n"+progress+"\n");
        }
        void RequireProgress(string result){Assert.That(result,Is.Not.EqualTo("Unavailable"),Diagnostic());Assert.That(result,Does.Not.Contain("저장하지 못했습니다"),Diagnostic());}
        string Diagnostic()
        {
            var snapshot=runtime.CaptureSession();
            var camera=player?player.GetComponentInChildren<Camera>():null;
            return "Phase="+phase+" tick="+runtime.World.Tick+" player="+(player?player.transform.position.ToString("F3"):"missing")+" cameraForward="+(camera?camera.transform.forward.ToString("F3"):"missing")+" cameraEuler="+(camera?camera.transform.eulerAngles.ToString("F2"):"missing")+" route="+routeCursor+"/"+routeLength+":"+routeNode+" waypoint="+routeWaypoint.ToString("F3")+" blocker="+routeBlocker+" detouring="+routeDetouring+" body="+runtime.Bodies.Single(b=>b.ActorId=="CH_02").transform.position.ToString("F3")+" canSeeBody="+runtime.CanSee("CH_01","CH_02")+" canReachBody="+runtime.CanReach("CH_01","CH_02",2.5)+" incident="+snapshot.Incident.Stage+" reason="+snapshot.Incident.Reason+" proceedings="+snapshot.Proceedings.Phase+" presenterNode="+snapshot.Proceedings.PresenterNode+" presenterDestination="+snapshot.Proceedings.PresenterDestination+" cursor="+snapshot.Proceedings.PresenterCursor+"/"+snapshot.Proceedings.PresenterPath.Length+" message="+runtime.ReadPlayer().Message+" actors="+string.Join(";",runtime.World.Residents.Where(a=>a.Alive&&a.Present).Select(a=>a.Id+":"+a.Phase+":"+a.Node+"→"+a.Destination+" summons="+runtime.HasReceivedCourtSummons(a.Id)));
        }
    }
}


