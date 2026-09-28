using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.World.Mansion
{
    [Serializable] public sealed class MansionCaseReceipt
    {
        public string Id,Observer;public KnownRecord Record;
        public MansionCaseReceipt Copy()=>new MansionCaseReceipt{Id=Id,Observer=Observer,Record=Record.Copy()};
    }
    [Serializable] public sealed class MansionCaseWitness
    {
        public string Observer;public bool ActorIdentified,TargetIdentified,Continuous=true;
        public string CauseReceiptId="";
        public MansionCaseWitness Copy()=>(MansionCaseWitness)MemberwiseClone();
    }
    [Serializable] public sealed class MansionIncidentSnapshot
    {
        public MansionCaseSettings Settings;public string Stage="Inactive",Reason="",CauseEvent="",ResultEvent="",Reporter="";
        public long LastTick=-1,CauseTick=-1,DueTick=-1,ResultTick=-1,DiscoveryTick=-1,ReportTick=-1,ConfirmationTick=-1,IntentStartTick=-1;
        public int ContactProgress,FatalityCap;public bool TracePresent,Reservation; // TracePresent is retained for the explicit X31 fixture only.
        public IncidentPhysicalTrace[] PhysicalTraces=Array.Empty<IncidentPhysicalTrace>();
        public long RiskNoticeTick=-1,RiskContinuedTick=-1;
        public string ActivationId="";
        public Point3 CausePosition,ContactPoint,ResultPosition,DiscoveryPosition;
        public MansionCaseWitness[] Witnesses=Array.Empty<MansionCaseWitness>();public string[] IntentListeners=Array.Empty<string>();
        public MansionRiskWitness[] RiskWitnesses=Array.Empty<MansionRiskWitness>();
        public MansionCaseReceipt[] Receipts=Array.Empty<MansionCaseReceipt>();
        public IncidentPlanState Plan;
        public IncidentAdmissionReceipt Admission;
        public string Rescuer="",RiskResolvedBy="",RiskResolutionEvent="";
        public long RescueStartedTick=-1,RescueLastTick=-1,RiskResolvedTick=-1;
        public int RescueProgress;
        public Point3 RescuerPosition,RescuePosition;
        public MansionIncidentSnapshot Copy(){var c=(MansionIncidentSnapshot)MemberwiseClone();c.Settings=Settings?.Copy();c.PhysicalTraces=(PhysicalTraces??Array.Empty<IncidentPhysicalTrace>()).Select(t=>t.Copy()).ToArray();if(c.Settings!=null&&!c.Settings.ExplicitTestSession)c.TracePresent=false;c.Plan=string.IsNullOrEmpty(Plan?.OwnerId)?null:Plan.Copy();c.Admission=string.IsNullOrEmpty(Admission?.CaseId)?null:Admission.Copy();c.Witnesses=Witnesses.Select(x=>x.Copy()).ToArray();c.RiskWitnesses=(RiskWitnesses??Array.Empty<MansionRiskWitness>()).Select(x=>x.Copy()).ToArray();c.IntentListeners=(string[])IntentListeners.Clone();c.Receipts=Receipts.Select(x=>x.Copy()).ToArray();return c;}
    }
    /// <summary>Opt-in X31 test recipe. Choices use the actor's own B, execution uses physical contact and custody.
    /// Hidden case state is never a public view. No automatic killer assignment, replacement victim, teleport, or knowledge broadcast.</summary>
    public sealed partial class MansionIncident
    {
        MansionIncidentSnapshot state=new MansionIncidentSnapshot();
        public bool IsConfigured=>state.Settings!=null;
        public string CaseId=>state.Settings?.Id??"";
        public string ActorId=>state.Settings?.ActorId??"";
        public string TargetId=>state.Settings?.TargetId??"";
        public string Stage=>state.Stage;
        public Point3 ReportedPosition=>state.Receipts.Where(r=>r.Observer==state.Reporter&&r.Record.ReceivedTick<=state.ReportTick&&(r.Record.Value=="Collapsed"||r.Record.Predicate=="CausedOutcome")).OrderByDescending(r=>r.Record.ReceivedTick).Select(r=>r.Record.Position).DefaultIfEmpty(state.DiscoveryPosition).First();
        public bool HasPendingResult=>state.Reservation;
        public bool CanConvene=>state.ConfirmationTick>=0&&!state.Reservation;
        public bool Controls(string actor)=>state.Settings!=null&&state.Settings.ActorId==actor&&new[]{"ApproachingTool","AcquiringTool","ApproachingTarget","Intent","Contact"}.Contains(state.Stage);
        public string Configure(MansionWorld world,MansionCaseSettings settings)
        {
            if(state.Stage!="Inactive")return "AlreadyConfigured";
            if(settings==null||!settings.Enabled)return "Disabled";
            if(!settings.ExplicitTestSession)return "OwnerBoundPlanRequired";
            ValidateSettings(settings);
            if(!world.Residents.Any(x=>x.Id==settings.ActorId&&x.Alive&&x.Present)||!world.Residents.Any(x=>x.Id==settings.TargetId&&x.Alive&&x.Present)||!world.Objects.Any(x=>x.Id==settings.ObjectId)||world.FindPath(world.Resident(settings.ActorId).Node,settings.ToolNode,Array.Empty<string>()).Length==0||world.FindPath(settings.ToolNode,settings.ContactNode,Array.Empty<string>()).Length==0)return "InvalidRecipeReferences";
            string registered=world.RegisterCase(settings);if(registered!="Registered")return registered;
            state=new MansionIncidentSnapshot{Settings=settings.Copy(),Stage="Planned",FatalityCap=settings.ChapterStartingResidents<=6?1:2};return "Configured";
        }
        static void ValidateSettings(MansionCaseSettings s)
        {
            if(s.PlayerInitiated){ValidatePlayerSettings(s);return;}
            if(s.ExplicitTestSession){if(s.Template!="X31")throw new ArgumentException("Unknown explicit fixture");}
            else{
                IncidentExecutionDefinition.Validate(s.Execution?.Definition);var d=s.Execution.Definition;
                if(d.ReviewStatus!="Reviewed"||s.Template!=d.Id||s.ContactTicks!=d.ContactTicks||s.MinimumIntentTicks!=d.IntentTicks||s.DelayTicks!=d.DelayTicks||s.IntentSpeech!=d.IntentLine||s.ToolNode!=s.Execution.ToolNode||s.ContactNode!=s.Execution.ContactNode)throw new ArgumentException("Execution differs from authored definition");
            }
            if(s.ActorId==s.TargetId||s.ActorId=="CH_01"||string.IsNullOrWhiteSpace(s.DecisionReason)||string.IsNullOrWhiteSpace(s.IntentSpeech)||!s.RejectedAlternatives.Contains("Withdraw")||!s.RejectedAlternatives.Contains("AskForHelp")||s.ContactTicks<1||s.DelayTicks<1||s.MinimumIntentTicks<1||s.KnowledgeFreshnessTicks<1||s.ChapterStartingResidents<4||s.ChapterStartingResidents>18||s.ChapterStartTick<0||s.ApproachTick<s.ChapterStartTick||s.EarliestCauseTick<s.ApproachTick||s.OpportunityEndTick<s.EarliestCauseTick)throw new ArgumentException("Invalid incident timing or alternatives");
            foreach(var id in new[]{s.Id,s.ActorId,s.TargetId,s.ObjectId,s.ToolNode,s.ContactNode})_=new StableId(id);
        }
        public void Step(MansionWorld world,IActorKnowledgeQuery actorKnowledge,IMansionIncidentPhysics physics)
        {
            if(state.Settings==null||world.Paused||world.Tick==state.LastTick)return;
            if(world.Tick<state.LastTick)throw new InvalidOperationException("Restore the incident with the matching world snapshot");
            if(state.CauseTick>=0&&state.LastTick>=0&&world.Tick!=state.LastTick+1)foreach(var witness in state.Witnesses)witness.Continuous=false;
            if(state.LastTick>=0&&world.Tick!=state.LastTick+1&&state.CauseTick<0){state.ContactProgress=0;AbandonRiskNotice(world);Release(world);if(state.Stage=="Intent"){state.Stage="ApproachingTarget";state.IntentStartTick=-1;state.IntentListeners=Array.Empty<string>();}}
            state.LastTick=world.Tick;
            var s=state.Settings;
            if(state.Stage=="CauseCommitted"){AdvanceResult(world,physics);return;}
            if(new[]{"Inactive","Cancelled","RiskResolved","ResultCommitted","Discovered","Reported","Confirmed"}.Contains(state.Stage))return;
            if(actorKnowledge==null||actorKnowledge.OwnerId!=s.ActorId)throw new InvalidOperationException("Planner requires its owner-bound knowledge");
            if(!RecheckExecution(world,actorKnowledge,physics))return;
            if(world.Tick>s.OpportunityEndTick){Cancel(world,"OpportunityExpired");return;}
            var actor=world.Resident(s.ActorId);var target=world.Resident(s.TargetId);
            if(!world.CanAct(actor.Id)||!world.CanAct(target.Id)){Cancel(world,"ParticipantUnavailable");return;}
            bool Known(string id)=>actorKnowledge.Records().Any(r=>r.SubjectId==id&&r.Direct&&r.IdentityConfirmed&&r.Predicate=="AtPlace"&&r.ToTick<=world.Tick+1&&world.Tick-r.ToTick<=s.KnowledgeFreshnessTicks);
            if(state.Stage=="Planned"){
                if(world.Tick<s.ApproachTick||!Known(s.ObjectId)){state.Reason="ObjectNotObserved";return;}
                if(world.Plan(s.ActorId,s.ToolNode,"Examine",60)!="Accepted"){state.Reason="RouteOrActivityUnavailable";return;}
                state.Stage="ApproachingTool";state.Reason="";world.Emit("CaseIntentSelected",s.ActorId,s.Id,s.DecisionReason);return;
            }
            if(state.Stage=="ApproachingTool"){
                if(actor.Node!=s.ToolNode||actor.Phase=="Travelling")return;state.Stage="AcquiringTool";
            }
            if(state.Stage=="AcquiringTool"){
                var item=world.Object(s.ObjectId);
                bool alreadyHeld=actor.HeldObject==s.ObjectId&&item.Location=="Hand"&&item.Owner==s.ActorId;
                if(!alreadyHeld){if(!Known(s.ObjectId)||!physics.CanReach(s.ActorId,s.ObjectId,2.5)||item.Location!="World"||actor.HeldObject!=""){state.Reason="ToolUnavailable";return;}if(world.Pickup(s.ObjectId,s.ActorId)!="Committed")return;}
                if(world.Plan(s.ActorId,s.ContactNode,"Wait",s.ContactTicks+s.MinimumIntentTicks)!="Accepted"){state.Reason="ContactRouteUnavailable";return;}
                state.Stage="ApproachingTarget";state.Reason="";return;
            }
            if(state.Stage=="ApproachingTarget"){
                if(actor.Node!=s.ContactNode||actor.Phase=="Travelling"||world.Tick<s.EarliestCauseTick-s.MinimumIntentTicks-s.ContactTicks)return;
                if(!Known(s.TargetId)||!physics.CanSee(s.ActorId,s.TargetId)||!physics.Identifies(s.ActorId,s.TargetId)){state.Reason="TargetNotObserved";RecordPlanCheck(world,actorKnowledge,"BeforeCause","Wait",state.Reason);return;}
                state.Stage="Intent";state.IntentStartTick=world.Tick;state.IntentListeners=world.Residents.Where(r=>r.Alive&&r.Present&&physics.ReceivesSpeech(r.Id,s.ActorId)).Select(r=>r.Id).ToArray();world.Emit("CaseIntentUtteranceStarted",s.ActorId,s.Id,s.IntentSpeech);return;
            }
            if(state.Stage=="Intent"){
                state.IntentListeners=state.IntentListeners.Where(id=>physics.ReceivesSpeech(id,s.ActorId)).ToArray();
                if(world.Tick-state.IntentStartTick<s.MinimumIntentTicks)return;
                world.Emit("CaseIntentUtteranceCompleted",s.ActorId,s.Id,s.IntentSpeech);
                foreach(var id in state.IntentListeners){bool identified=physics.Identifies(id,s.ActorId);Observe(world,id,"SaidStatement",identified?s.ActorId:"UNKNOWN_ACTOR",s.IntentSpeech,"실제로 들은 발언: "+s.IntentSpeech,state.IntentStartTick,world.Tick+1,identified,actor.Position,new[]{"수신한 발언 원문"},new[]{"발언만으로 실제 실행·결과는 확정하지 않음"});}
                state.Stage="Contact";
            }
            if(state.Stage!="Contact")return;
            if(world.Tick<s.EarliestCauseTick-s.ContactTicks+1)return;
            var held=world.Object(s.ObjectId);
            if(!Known(s.TargetId)||actor.HeldObject!=s.ObjectId||held.Location!="Hand"||held.Owner!=s.ActorId||!physics.HasContact(s.ActorId,s.TargetId,s.ObjectId,out var contact)){
                state.ContactProgress=0;AbandonRiskNotice(world);Release(world);state.Reason="ContactOpportunityLost";RecordPlanCheck(world,actorKnowledge,"BeforeCause","Wait",state.Reason);return;
            }
            state.ContactPoint=contact;if(!MaintainRiskNotice(world,physics))return;
            if(state.ContactProgress==0&&!Reserve(world)){Cancel(world,"FatalityCapacityUnavailable");return;}
            state.Reason="";state.ContactPoint=contact;if(++state.ContactProgress<s.ContactTicks)return;
            if(!AdmitCause(world,actorKnowledge,physics))return;
            state.CauseTick=world.Tick;state.DueTick=world.Tick+s.DelayTicks;state.CausePosition=target.Position;state.TracePresent=s.ExplicitTestSession;state.Stage="CauseCommitted";
            RecordPlanCheck(world,actorKnowledge,"BeforeCause","Committed","");state.Plan.State="CauseCommitted";
            state.CauseEvent="M_EVENT_"+world.CommitCaseCause(s.Id,state.DueTick);
            state.Witnesses=world.Residents.Where(r=>r.Alive&&r.Present&&physics.CanSee(r.Id,s.ActorId)&&physics.CanSee(r.Id,s.TargetId)&&physics.CanSee(r.Id,s.ObjectId)).Select(r=>new MansionCaseWitness{Observer=r.Id,ActorIdentified=physics.Identifies(r.Id,s.ActorId),TargetIdentified=physics.Identifies(r.Id,s.TargetId)}).ToArray();
            foreach(var w in state.Witnesses)Observe(world,w.Observer,"UsedObject",w.ActorIdentified?s.ActorId:"UNKNOWN_ACTOR",s.ObjectId,ContactObservation,world.Tick,world.Tick+1,w.ActorIdentified,state.ContactPoint,new[]{"그 순간 보인 접촉과 소품"},new[]{"접촉만으로 결과나 숨은 의도는 입증하지 않음"});
            CaptureWitnessCauses(world);
        }
        bool Reserve(MansionWorld world)
        {
            if(state.Reservation)return true;
            state.Reservation=world.ReserveCaseOutcome(state.Settings.Id);return state.Reservation;
        }
        void Release(MansionWorld world){if(!state.Reservation)return;world.ReleaseCaseOutcome(state.Settings.Id);state.Reservation=false;}
        void AdvanceResult(MansionWorld world,IMansionIncidentPhysics physics)
        {
            var s=state.Settings;foreach(var w in state.Witnesses)if(!Eligible(world,w.Observer)||!physics.CanSee(w.Observer,s.TargetId)||!physics.CanSee(w.Observer,s.ActorId))w.Continuous=false;
            ObserveCriticalCondition(world,physics);
            if(AdvanceRescue(world,physics))return;
            if(world.Tick<state.DueTick)return;
            var victim=world.Resident(s.TargetId);if(!victim.Alive)throw new InvalidOperationException("Unregistered simultaneous outcome for reserved target");
            long resultSequence=world.CommitCaseResult(s.Id);
            state.ResultTick=world.Tick;state.ResultPosition=victim.Position;state.Stage="ResultCommitted";state.ResultEvent="M_EVENT_"+resultSequence;state.Reservation=false;
            if(state.Plan!=null)state.Plan.State="ResultCommitted";
            CaptureWitnessOutcomes(world,physics);
            foreach(var resident in world.Residents.Where(r=>r.Alive&&r.Present&&physics.CanSee(r.Id,s.TargetId))){var witness=state.Witnesses.FirstOrDefault(w=>w.Observer==resident.Id&&w.Continuous&&w.ActorIdentified&&w.TargetIdentified);bool continuous=witness!=null;
                Observe(world,resident.Id,continuous?"CausedOutcome":"AtPlace",continuous?s.ActorId:physics.Identifies(resident.Id,s.TargetId)?s.TargetId:"UNKNOWN_ACTOR",continuous?s.Template+"_Contact_Then_Collapse":"Collapsed",continuous?ContinuousObservation:"인물이 쓰러지는 모습을 보았다.",continuous?state.CauseTick:world.Tick,world.Tick+1,continuous||physics.Identifies(resident.Id,s.TargetId),victim.Position,continuous?new[]{"연속 관측한 신원·접촉·쓰러짐"}:new[]{"관측 순간의 쓰러짐"},new[]{"유스티 사망 확인·숨은 의도·관측 밖 행동은 별도"});}
        }
        void Cancel(MansionWorld world,string reason)
        {
            if(state.CauseTick>=0||state.Stage=="Cancelled")return;AbandonRiskNotice(world);Release(world);ReleasePlanMovement(world);state.Stage="Cancelled";state.Reason=reason;state.ContactProgress=0;if(state.Plan!=null){state.Plan.State="Withdrawn";state.Plan.Reason=reason;}world.Emit("IncidentCancelled",state.Settings.ActorId,state.Settings.Id,reason);
        }
        public string Withdraw(MansionWorld world,string actor,string reason)
        {
            if(world.Paused||state.Settings==null||actor!=state.Settings.ActorId||state.CauseTick>=0||string.IsNullOrWhiteSpace(reason))return "Unavailable";Cancel(world,reason);return "Withdrawn";
        }
        public string Discover(MansionWorld world,string observer,IMansionIncidentPhysics physics)
        {
            if(world.Paused||state.ResultTick<0||!Eligible(world,observer)||!physics.CanReach(observer,state.Settings.TargetId,2.5)||!physics.CanSee(observer,state.Settings.TargetId))return "Unavailable";
            string target=state.Settings.TargetId;bool identity=physics.Identifies(observer,target);
            var prior=state.Receipts.LastOrDefault(r=>r.Observer==observer&&r.Record.Value=="Collapsed");
            if(prior==null||prior.Record.Position.Distance(world.Resident(target).Position)>.05||identity&&!prior.Record.IdentityConfirmed)Observe(world,observer,"AtPlace",identity?target:"UNKNOWN_ACTOR","Collapsed","가까이에서 쓰러진 인물의 상태를 확인했다.",world.Tick,world.Tick+1,identity,world.Resident(target).Position,new[]{"관측한 위치와 외관 상태"},new[]{"원인·범인·사망 시각은 확인하지 않음"});
            if(state.DiscoveryTick<0){state.DiscoveryTick=world.Tick;state.DiscoveryPosition=world.Resident(target).Position;state.Stage="Discovered";world.Emit("BodyDiscovered",observer,target,state.Settings.Id);}return "Discovered";
        }
        public string Report(MansionWorld world,string observer,bool actuallyDeliveredToYusti)
        {
            if(world.Paused||state.DiscoveryTick<0||!Eligible(world,observer)||!actuallyDeliveredToYusti||!state.Receipts.Any(r=>r.Observer==observer&&(r.Record.Value=="Collapsed"||r.Record.Predicate=="CausedOutcome")))return "Unavailable";
            if(state.ReportTick<0){state.ReportTick=world.Tick;state.Reporter=observer;state.Stage="Reported";}world.Emit("BodyReportReceived",observer,state.Settings.TargetId,state.Settings.Id);return "Reported";
        }
        public string Confirm(MansionWorld world,bool yustiActuallyCompletedInspection)
        {
            if(world.Paused||state.ReportTick<0||!yustiActuallyCompletedInspection||state.Reservation)return "Unavailable";
            if(state.ConfirmationTick>=0)return "Confirmed";state.ConfirmationTick=world.Tick;state.Stage="Confirmed";world.Emit("DeathConfirmed","YUSTI",state.Settings.TargetId,state.Settings.Id);return "Confirmed";
        }
        public string ReceiveAnnouncement(MansionWorld world,string receiver,bool actuallyReceived)
        {
            if(state.ConfirmationTick<0||!actuallyReceived||!Eligible(world,receiver))return "Unavailable";
            if(state.Receipts.Any(r=>r.Observer==receiver&&r.Record.Kind=="OfficialReport"))return "Received";
            Observe(world,receiver,"ConfirmedDeath",state.Settings.TargetId,"Dead","유스티 확인: 사망 신원과 발견 장소. 원인·범인·사망 시각은 보고서에 포함되지 않습니다.",state.ConfirmationTick,state.ConfirmationTick+1,true,state.DiscoveryPosition,new[]{"확인된 신원·사망·발견 장소"},new[]{"범인·원인·고의·사망 시각 미확정"},"OfficialReport");return "Received";
        }
        public string InspectTrace(MansionWorld world,string observer,IMansionIncidentPhysics physics)
        {
            if(state.Settings==null||!state.Settings.ExplicitTestSession)return "PhysicalInspectionRequired";
            if(world.Paused||!state.TracePresent||!Eligible(world,observer)||!physics.CanReach(observer,state.Settings.ObjectId,2.5)||!physics.CanSee(observer,state.Settings.ObjectId))return "Unavailable";
            if(!state.Receipts.Any(r=>r.Observer==observer&&r.Record.Predicate=="TracePresent"))Observe(world,observer,"TracePresent",state.Settings.ObjectId,TraceValue,TraceObservation,world.Tick,world.Tick+1,true,world.Object(state.Settings.ObjectId).Position,new[]{"관측 당시 소품의 표식"},new[]{"과거 소지자·접촉 시각·고의를 단독 확정하지 않음"});return "Observed";
        }
        static bool Eligible(MansionWorld world,string actor)=>world.Residents.Any(r=>r.Id==actor&&r.Alive&&r.Present);
        void Observe(MansionWorld world,string owner,string predicate,string subject,string value,string text,long from,long to,bool identity,Point3 position,string[] supports,string[] limits,string kind="Visual")
        {
            string id=state.Settings.Id+"_RECEIPT_"+(state.Receipts.Length+1);state.Receipts=state.Receipts.Concat(new[]{new MansionCaseReceipt{Id=id,Observer=owner,Record=new KnownRecord{ProvenanceKey=kind=="Visual"?"WITNESS_L"+world.Loop+"_"+CaseId+"_"+owner:state.Settings.Id+"_"+owner+"_"+predicate,LoopId="LOOP_"+world.Loop.ToString("00"),Kind=kind,SubjectId=subject,Predicate=predicate,Value=value,Text=text,Source=owner,FromTick=from,ToTick=to,ReceivedTick=world.Tick,IdentityConfirmed=identity,Position=position,Supports=supports,DoesNotEstablish=limits}}}).ToArray();
            if(state.Settings.PlayerInitiated&&(predicate=="UsedObject"||predicate=="CausedOutcome")){
                var record=state.Receipts.Last().Record;var witness=state.Witnesses.FirstOrDefault(w=>w.Observer==owner);
                record.OutcomeTarget=witness?.TargetIdentified==true?state.Settings.TargetId:"";
                if(predicate=="UsedObject")record.CausalStage="PhysicalStrike";
            }
        }
        public MansionCaseReceipt[] ReceiptsFor(string actor)=>state.Receipts.Where(r=>r.Observer==actor).Select(r=>r.Copy()).ToArray();
        public MansionCasePublicView Read(string actor)
        {
            var records=state.Receipts.Where(r=>r.Observer==actor).Select(r=>r.Record).ToArray();var official=records.FirstOrDefault(r=>r.Kind=="OfficialReport");var observed=records.LastOrDefault(r=>r.Value=="Collapsed"||r.Predicate=="CausedOutcome");
            if(official!=null)return new MansionCasePublicView{State="Confirmed",VictimId=official.SubjectId,Text=official.Text,Discovered=true,Confirmed=true,ObservedTick=official.ReceivedTick,ConfirmationTick=official.FromTick};
            if(observed!=null)return new MansionCasePublicView{State="Observed",VictimId=observed.Predicate=="CausedOutcome"?"":observed.SubjectId,Text=observed.Text,Discovered=true,ObservedTick=observed.ReceivedTick};return new MansionCasePublicView();
        }
        public MansionIncidentSnapshot Capture()=>state.Copy();
        public static MansionIncident Restore(MansionIncidentSnapshot snapshot,MansionWorld world)
        {
            if(snapshot==null)throw new ArgumentException("Missing incident snapshot");var s=snapshot.Copy();if(s.Settings==null){if(s.Stage!="Inactive"||s.Receipts.Length>0||s.PhysicalTraces.Length>0||s.CauseTick>=0)throw new ArgumentException("Invalid inactive incident");return new MansionIncident{state=s};}ValidateSettings(s.Settings);if(!s.Settings.ExplicitTestSession&&!s.Settings.PlayerInitiated&&(s.Plan==null||s.Plan.Origin!="Deliberated"||s.Plan.OwnerId!=s.Settings.ActorId||s.Plan.TargetId!=s.Settings.TargetId||s.Settings.Execution.MapVersion!=world.Capture().MapVersion))throw new ArgumentException("Missing production execution plan");
            if(!new[]{"Planned","ApproachingTool","AcquiringTool","ApproachingTarget","Intent","Contact","CauseCommitted","RiskResolved","ResultCommitted","Discovered","Reported","Confirmed","Cancelled"}.Contains(s.Stage)||s.LastTick>world.Tick||s.ContactProgress<0||s.ContactProgress>s.Settings.ContactTicks||s.FatalityCap!=(s.Settings.ChapterStartingResidents<=6?1:2)||!s.CausePosition.Finite()||!s.ResultPosition.Finite()||!s.ContactPoint.Finite()||!s.DiscoveryPosition.Finite())throw new ArgumentException("Invalid incident progress");
            bool caused=s.CauseTick>=0,finished=s.ResultTick>=0,resolved=s.Stage=="RiskResolved";if(s.Settings.ExplicitTestSession&&caused!=s.TracePresent||caused&&(!world.Events.Any(e=>"M_EVENT_"+e.Sequence==s.CauseEvent&&e.Type=="IncidentCauseCommitted"&&e.Tick==s.CauseTick&&e.Detail==s.Settings.Id)||s.DueTick!=s.CauseTick+s.Settings.DelayTicks)||finished&&(!caused||s.ResultTick<s.DueTick||world.Resident(s.Settings.TargetId).Alive||!world.Events.Any(e=>"M_EVENT_"+e.Sequence==s.ResultEvent&&e.Type=="IncidentResultCommitted"&&e.Tick==s.ResultTick))||caused&&!finished&&!resolved&&!s.Reservation||finished&&s.Reservation||resolved&&(s.DiscoveryTick>=0||s.ReportTick>=0||s.ConfirmationTick>=0)||s.ConfirmationTick>=0&&s.ReportTick<0||s.ReportTick>=0&&s.DiscoveryTick<0)throw new ArgumentException("Invalid cause/result/report chain");
            if(s.Receipts.Select(r=>r.Id).Distinct().Count()!=s.Receipts.Length||s.Receipts.Any(r=>!world.Residents.Any(a=>a.Id==r.Observer)||r.Record.FromTick<0||r.Record.ToTick<=r.Record.FromTick||r.Record.ToTick>world.Tick+1||r.Record.ReceivedTick>world.Tick||!r.Record.Position.Finite()))throw new ArgumentException("Invalid witness receipts");world.CheckRestoredCase(s);if(s.Settings.PlayerInitiated)ValidatePlayerCause(s,world);ValidatePhysicalTraceLinks(s);ValidateRescue(s,world);ValidateRiskNotice(s,world);ValidateRiskWitnesses(s,world);return new MansionIncident{state=s};
        }
    }
}
