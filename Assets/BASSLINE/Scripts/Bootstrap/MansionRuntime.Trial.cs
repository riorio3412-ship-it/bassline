using System;
using System.IO;
using System.Linq;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.Trial;
using BASSLINE.Save;
using BASSLINE.Investigation;
using BASSLINE.Knowledge;
using BASSLINE.NPC;
using BASSLINE.World.Mansion;

namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerTrialPort
    {
        TrialDirector court=new TrialDirector();TrialEndgame endgame=new TrialEndgame();
        bool Observer=>!World.Resident("CH_01").Alive||!World.Resident("CH_01").Present;
        string SeatNode(string actor){var seat=Layout.Anchor("SEAT_TRIAL_"+actor.Substring(3));return seat!=null&&seat.ApproachNode>=0?nodes[seat.ApproachNode].Id:"";}
        bool AtCourtSeat(string actor)
        {
            string id=SeatNode(actor);return id!=""&&World.Resident(actor).Position.Distance(nodes.First(n=>n.Id==id).Position)<.85;
        }
        void TryStartCourt()
        {
            if(!CaseBundleConfirmed()||!PresenterReadyForCourt())return;
            var participants=World.Residents.Where(r=>r.Alive&&r.Present).Select(r=>r.Id).ToArray();
            if(participants.Any(id=>!AtCourtSeat(id)))return;
            if(court.Start("TRIAL_L"+World.Loop+"_C"+World.Chapter,Knowledge.LoopId,participants,AtCourtSeat)!="Started")return;
            foreach(var work in residentToolWork)CancelResidentToolWork(work,"CourtStarted");
            foreach(var pickup in residentToolPickups)CancelResidentToolPickup(pickup,"CourtStarted");
            foreach(var returned in residentToolReturns)CancelResidentToolReturn(returned,"CourtStarted");
            foreach(var reading in residentReadings)CancelResidentReading(reading,"CourtStarted");
            CancelWeaponRinse();CancelWeapon();FlushPresenceObservations();EndConversation();CancelInspection();CancelItemExchange();CancelToolPress("재판을 시작하려고 도구 사용을 멈췄어요.");
            EndFamilyDispute("Deferred","CourtStarted");
            foreach(var intent in residentIntents.Intents.Where(ActiveIntent).ToArray())EndResidentIntent(intent,"Cancelled","CourtStarted");
            foreach(var actor in participants)ReceiveChapterRules(actor);
            court.ConfigureVoting(HasChapterRule("CH01")?"R02":"Basic",proceedings.VoteRandomState);proceedings.Phase="Debate";
            World.Pause("M_COURT",true);move=default;running=false;
            foreach(string actor in participants.Where(id=>id!="CH_01"))QueueWitnessSpeech(actor);
            SaveAutomatic("trial-start");
        }
        bool HearCourt(string listener,string speaker)
        {
            return bodies.ContainsKey(listener)&&bodies.ContainsKey(speaker)&&PlaceOf(bodies[listener].transform.position)=="R_TRIAL"&&Visible(listener,SubjectPoint(speaker),24,false);
        }
        void AdvanceCourt()
        {
            if(World.Capture().PauseOwners.Any(p=>p!="M_COURT"))return;
            var before=court.Capture();string speaker=before.Pending.FirstOrDefault()?.Speaker??"";
            court.Step(id=>speaker!=""&&HearCourt(id,speaker));
            AdvanceWitnessExaminations();
            foreach(var speech in court.Capture().Transcript){
                if(proceedings.DeliveredSpeeches.Contains(speech.Speech.Id))continue;
                ReceiveJointSpeech(speech);
                foreach(string entry in proceedings.SpokenRecords.Where(x=>x.StartsWith(speech.Speech.Id+"|",StringComparison.Ordinal)).Distinct()){
                    var parts=entry.Split('|');
                    foreach(string receiver in speech.ReceivedBy.Where(id=>id!=speech.Speech.Speaker))Knowledge.Deliver(speech.Speech.Speaker,receiver,parts[1],World.Tick);
                }
                proceedings.DeliveredSpeeches=proceedings.DeliveredSpeeches.Concat(new[]{speech.Speech.Id}).ToArray();
            }
            AdvanceJointArguments();AdvanceFinalDefense();AdvanceNpcCounters();AdvanceTheoryResponses();
            if(Observer&&!court.Focused){
                var snapshot=court.Capture();
                if(snapshot.Phase=="Debate"&&snapshot.CourtTick%900==0)foreach(string claim in snapshot.ActiveClaimIds)court.RetireClaim(claim);
                snapshot=court.Capture();if(snapshot.Phase=="Debate"&&snapshot.Pending.Length==0&&snapshot.ActiveClaimIds.Length==0)court.OpenVoting();
                if(court.Phase=="Voting"){FillNpcBallots();court.LockVotes();}
                if(court.Phase=="VotesLocked")ContinueTrial();
            }
        }
        string QueueWitnessSpeech(string actor)
        {
            var query=Knowledge.For(actor);var used=proceedings.SpokenRecords.Select(x=>x.Split('|')[1]).ToArray();
            string id="M_SPEECH_"+(++proceedings.SpeechSequence);var draft=new TrialReasoning().DraftSpeech(query,id,used,World.ClockVersion);
            if(draft==null)return "현재 전달할 새 정보가 없습니다.";
            if(HasChapterRule("CH19")&&draft.RecordIds.Any(r=>query.Find(r)!=null&&!query.Find(r).Direct))draft.Text="전해 들은 내용입니다. 제가 직접 확인한 것으로 보지는 말아 주세요. " + draft.Text;
            var claim=new ClaimRecord{Id="CLAIM_"+id,OwnerId=actor,LoopId=query.LoopId,Text=draft.Text,Spans=draft.Spans.Select(sp=>new ClaimSpan{Id=sp.Id,SubjectId=sp.SubjectId,Predicate=sp.Predicate,Value=sp.Value,PlaceId=sp.PlaceId,FromTick=sp.FromTick,ToTick=sp.ToTick,Quantifier=sp.Quantifier}).ToArray()};
            string result=court.QueueSpeech(new SpeechDraft{Id=id,Speaker=actor,Topic=draft.Topic,Text=draft.Text,Claim=claim});
            if(result=="Queued")proceedings.SpokenRecords=proceedings.SpokenRecords.Concat(draft.RecordIds.Select(record=>id+"|"+record)).ToArray();return result;
        }

        public PlayerTrialView ReadTrial()
        {
            var view=new PlayerTrialView{Phase=proceedings.Phase=="Gathering"?"Gathering":"NotStarted",Message=message,Observer=Observer};
            if(court.Phase=="NotStarted")return view;
            var s=court.Capture();string speaker=s.Pending.FirstOrDefault()?.Speaker??"";var c=Observer?court.ReadPublicArchive():court.Read("CH_01",speaker!=""&&HearCourt("CH_01",speaker));var e=endgame.Capture();
            view.Phase=c.Phase=="VerdictTargetLocked"?"Voting":c.Phase;view.CourtTick=c.CourtTick;view.Topic=c.Topic;view.SpeakerId=c.Speaker;view.SpokenText=c.SpokenText;view.VoteRound=c.VoteRound;
            view.Participants=(string[])s.Participants.Clone();view.VoteCandidates=c.VoteCandidates;view.CanVote=c.Phase=="Voting"&&!Observer;view.CanContinue=true;view.Focused=court.Focused;
            view.FocusClaimId=s.Focus?.ClaimId??"";view.FocusSpanId=s.Focus?.SpanId??"";view.AnnouncedTopics=c.AnnouncedTopics;view.History=c.History.Select(h=>NameOf(h.Speech.Speaker)+" · "+h.Speech.Text).ToArray();
            var visibleClaims=c.ActiveClaims.Concat(c.History.Where(h=>h.Speech.Claim?.Id==view.FocusClaimId).Select(h=>h.Speech.Claim)).GroupBy(cl=>cl.Id).Select(g=>g.First());
            view.Claims=visibleClaims.Select(cl=>new TrialClaimView{Id=cl.Id,SpeakerId=cl.OwnerId,Text=cl.Text,Spans=cl.Spans.Select(sp=>new TrialSpanView{Id=sp.Id,Text=ReadableClaim(sp),State=c.Reviews.LastOrDefault(r=>r.ClaimId==cl.Id&&r.SpanId==sp.Id)?.State??"Asserted"}).ToArray()}).ToArray();
            view.Tallies=c.LockedVotes.GroupBy(v=>v.Choice).Select(g=>new TrialTallyView{ActorId=g.Key,Count=g.Count()}).ToArray();
            if(e.Plan==null)return view;
            view.Phase=e.Phase=="RewardApplied"?"Growth":e.Phase=="AwaitingEvaluation"?"Truth":e.Phase=="NextChapterReady"||e.Phase=="LoopReady"?"Completed":e.Phase;
            view.TruthAvailable=true;view.TruthCursor=e.RevealCursor;view.Truth=endgame.ReadTruth().Select(r=>new TrialTruthView{Id=r.Id,Heading=r.Heading,Text=r.Text,Tick=r.Tick,PlaceId=r.PlaceId,HasRecordedPath=r.HasRecordedPath}).ToArray();
            view.Verdict="최종 지목: "+NameOf(e.Plan.Target)+"\n"+(e.Plan.Correct?"판정 대상과 일치합니다.":"판정 대상과 일치하지 않습니다.");view.Executed=e.Plan.Executed;view.Escaped=e.Plan.Escaped;
            view.Observer=!World.Resident("CH_01").Alive||!World.Resident("CH_01").Present;view.Rank=e.Evaluation?.Rank??"";view.Score=e.Evaluation?.Score??0;view.ExperienceAwarded=e.RewardAppliedExperience;view.Residual=e.Plan.Residual;view.SettlementApplied=e.Plan.Applied;view.Transition=endgame.Transition;
            var profile=ReadProfile();view.Level=profile.Level;view.Experience=profile.Experience;view.SkillPoints=profile.SkillPoints;return view;
        }
        string ReadableClaim(ClaimSpan span)
        {
            string value=span.Predicate=="AtPlace"?PlaceLabel(span.Value):NameOf(span.Value);
            string subject=span.SubjectId=="UNKNOWN_ACTOR"?"신원을 확인하지 못한 사람":NameOf(span.SubjectId);
            string detail;
            switch(span.Predicate)
            {
                case "AtPlace":detail="있던 장소 · "+value;break;
                case "PassedDoor":detail="지나간 문 · "+value;break;
                case "HeldObject":detail="들고 있던 물건 · "+value;break;
                case "UsedObject":detail="사용한 물건 · "+value;break;
                case "DoorState":detail="문의 상태 · "+(span.Value=="Locked"?"잠김":span.Value=="Open"?"열림":span.Value=="Closed"?"닫힘":"확인한 기록 참조");break;
                case "HeardSound":detail="들은 소리 · 발언 내용 참조";break;
                case "SaidStatement":detail="실제로 들은 발언";break;
                case "ReceivedInformation":detail="전해 들은 내용";break;
                case "CausedOutcome":detail="행동과 결과의 연결";break;
                default:detail="주장의 내용과 범위";break;
            }
            return subject+" · "+detail+"\n기록 범위: "+WorldTimeLabel.Format(span.FromTick,World.ClockVersion)+" ~ "+WorldTimeLabel.Format(span.ToTick,World.ClockVersion);
        }
        public string EnterTrialFocus(string claim,string span)=>message=Observer?"관찰 중에는 논쟁에 개입할 수 없습니다.":court.EnterFocus(Knowledge.For("CH_01"),claim,span);
        public string CancelTrialFocus()=>message=court.CancelFocus("CH_01");
        public string SubmitTrial(string action,string rule,string[] records)
        {
            if(Observer)return message="관찰 중에는 자료를 제시할 수 없습니다.";
            string request="M_SUBMIT_"+(++proceedings.SubmissionSequence);var own=Knowledge.For("CH_01");
            if(rule=="Auto"){
                var focus=court.Capture().Focus;var heard=court.Read("CH_01",false);
                var claim=heard.History.Select(h=>h.Speech.Claim).FirstOrDefault(cl=>cl!=null&&cl.Id==focus?.ClaimId);
                var span=claim?.Spans.FirstOrDefault(sp=>sp.Id==focus?.SpanId);
                if(span==null)return message="확인할 말을 먼저 골라 주세요.";
                var owned=own.Records();var picked=(records??Array.Empty<string>()).Select(own.Find).ToArray();
                if(picked.Length==0||picked.Any(r=>r==null))return message="직접 모은 단서를 골라 주세요.";
                // One visible clue can refer to several stages already in the notebook.
                // This never searches the world or acquires missing premises for the player.
                records=records.Concat(picked.SelectMany(r=>CausalEvidence.RelatedRecords(owned,r.Id))).Distinct().ToArray();
                rule=PlayerRecordPresentation.ComparisonRule(span.Predicate,span.Quantifier,span.ToTick,records.Select(own.Find).ToArray());
            }
            var results=court.SubmitArgumentEvidence(own,request,action,rule,records);
            if(results.Length==0)results=new[]{court.Submit(own,request,action,rule,records)};
            // A delivered rebuttal is public only to people who actually heard it.
            foreach(var result in results)foreach(string actor in court.Capture().Participants.Where(id=>HearCourt(id,"CH_01")))court.DeliverSubmission(result.RequestId,actor);
            return message=string.Join("\n",results.Select(r=>r.Explanation).Distinct());
        }
        public KnownRecord[] CheckTrialSource(string[] ids)=>Observer?Array.Empty<KnownRecord>():court.CheckSource(Knowledge.For("CH_01"),ids);
        public string RequestTrialTestimony(string witness,string question)
        {
            if(Observer)return message="관찰 중입니다.";
            if(Reconstruction.Phase=="Presenting"||Reconstruction.Phase=="Defense")return message="지금 진행 중인 설명과 답변을 먼저 들어 주세요.";
            string kind=WitnessQuestions.Resolve(question);if(kind=="")return message="확인할 질문을 골라 주세요.";
            string result=court.BeginExamination("CH_01",witness,kind);
            return message=result.StartsWith("EXAM_",StringComparison.Ordinal)?"질문을 전합니다. 답을 들어 봅시다.":result=="QuestionInProgress"?"지금 질문에 대한 답을 먼저 들어 주세요.":result=="WrongWitness"?"이 말을 한 사람에게 물어볼 수 있습니다.":"확인할 진술을 먼저 골라 주세요.";
        }
        public string RetireTrialClaim(string id)=>message=Observer?"관찰 중입니다.":Reconstruction.Phase=="Presenting"||Reconstruction.Phase=="Defense"?"지금 진행 중인 설명과 답변을 먼저 들어 주세요.":court.RetireClaim(id);
        public string OpenTrialVoting()=>message=Observer?"관찰 중입니다.":court.JointArgumentBusy?"함께 설명하기를 마치거나 중단해 주세요.":Reconstruction.Phase!="Editing"?ProceedFromReconstruction(false):court.OpenVoting();
        public string CastTrialVote(string actor)
        {
            if(Observer)return message="관찰 중에는 투표할 수 없습니다.";string result=court.ChooseVote("CH_01",actor);if(result!="Recorded")return message=result;
            FillNpcBallots();return message=court.LockVotes();
        }
        void FillNpcBallots()
        {
            var ballot=court.Capture();
            foreach(string npc in ballot.Participants.Where(id=>id!="CH_01"&&!ballot.Ballots.Any(b=>b.Voter==id))){
                var decision=new TrialReasoning().ChooseVote(Knowledge.For(npc),ballot.VoteCandidates,proceedings.VoteRandomState,court.ReadHeardClaims(npc));
                proceedings.VoteRandomState=decision.NextRandomState;court.ChooseVote(npc,decision.Choice);
            }
        }
        public string ContinueTrial()
        {
            var before=CaptureSession();
            try{
                if(endgame.Phase=="AwaitingVerdict"){
                    string result=court.ResolveVoting();if(result!="VerdictTargetLocked")return message=result;
                    var adjudicated=incidents.Find(World.CaseBook.AdjudicatedCaseId);if(!CaseBundleConfirmed()||adjudicated==null||!adjudicated.CanConvene)return message="사건 확인이 완료되지 않았습니다.";
                    var closed=incidents.All().Where(c=>c.CanConvene).Select(c=>c.Capture()).ToDictionary(c=>c.Settings.Id);
                    var events=World.Events.Where(e=>closed.ContainsKey(e.Detail)&&(e.Type=="IncidentCauseCommitted"||e.Type=="IncidentResultCommitted"||e.Type=="BodyDiscovered"||e.Type=="DeathConfirmed"))
                        .Select(e=>DescribeClosedCaseEvent(e,closed[e.Detail])).ToArray();
                    result=endgame.CommitVerdict(court,new VerdictInput{ClockVersion=World.ClockVersion,CaseId=adjudicated.CaseId,ClosedCaseIds=closed.Keys.ToArray(),CampaignId=proceedings.CampaignId,ChapterId="CHAPTER_"+World.Chapter,ActualCulprit=World.CaseBook.AdjudicatedActorId,LivingResidents=World.Residents.Where(r=>r.Alive&&r.Present).Select(r=>r.Id).ToArray(),ConfirmedDead=closed.Values.Select(c=>c.Settings.TargetId).Distinct().ToArray(),Events=events,DrawSeed=proceedings.VoteRandomState,CaseBundleClosed=true});SaveAutomatic("verdict");return message=result;
                }
                switch(endgame.Phase){
                    case "Verdict":return message=endgame.BeginTruth();
                    case "Truth":
                        var reveal=endgame.Capture();if(reveal.RevealCursor+1<reveal.Reveal.Length)return message=endgame.AdvanceTruth();
                        endgame.FinishTruth();return message=endgame.Evaluate(BuildEvaluation());
                    case "Evaluation":
                        var profile=endgame.PrepareReward(ReadProfile());WriteProfile(profile);string awarded=endgame.AcceptReward(profile);SaveAutomatic("reward");return message=awarded;
                    case "RewardApplied":return message=endgame.ApplySettlement(CommitSettlement);
                    case "Settlement":return message=endgame.CompleteTransition(CommitTransition);
                    case "LoopReady":case "NextChapterReady":return message="정산을 마쳤습니다.";
                    default:return message="Unavailable";
                }
            }catch(Exception error){ApplySession(before);return message="진행을 저장하지 못했습니다: "+error.Message;}
        }
        RevealEvent DescribeClosedCaseEvent(MansionEvent entry,MansionIncidentSnapshot closedCase)
        {
            string heading,text;var position=closedCase.ResultPosition;
            switch(entry.Type)
            {
                case "IncidentCauseCommitted":heading="접촉 발생";text=NameOf(entry.Actor)+"의 소품이 "+NameOf(entry.Target)+"에게 접촉했습니다.";position=closedCase.CausePosition;break;
                case "IncidentResultCommitted":heading="쓰러짐 발생";text=NameOf(entry.Target)+"에게 접촉의 지연 결과가 발생했습니다.";break;
                case "BodyDiscovered":heading="현장 발견";text=NameOf(entry.Actor)+"이 가까이에서 "+NameOf(entry.Target)+"의 쓰러진 상태를 확인했습니다.";position=closedCase.DiscoveryPosition;break;
                case "DeathConfirmed":heading="공식 사망 확인";text="유스티가 현장에서 "+NameOf(entry.Target)+"의 사망을 확인했습니다. 기록 시각은 확인 시각입니다.";position=closedCase.DiscoveryPosition;break;
                default:throw new ArgumentException("Unapproved closed-case reveal event");
            }
            // This projection is called only after the public verdict has been locked above.
            return new RevealEvent{Id="REVEAL_"+entry.Sequence,CaseId=closedCase.Settings.Id,Tick=entry.Tick,Heading=heading,Text=text,PlaceId=PlaceOf(V(position)),RevealApproved=true,HasRecordedPath=false};
        }
        EvaluationInput BuildEvaluation()
        {
            var caseIds=endgame.Capture().Plan.ClosedCaseIds;
            var known=Knowledge.For("CH_01").Records().Where(r=>caseIds.Any(id=>r.ProvenanceKey.StartsWith(id+"_",StringComparison.Ordinal))).ToArray();var ids=known.Select(r=>r.Id).ToArray();
            var results=court.Capture().Submissions.Where(s=>s.Payload.StartsWith("CH_01|",StringComparison.Ordinal)&&s.Result.CitedRefs.Any(ids.Contains)&&new[]{"Support","Contradict","LimitScope"}.Contains(s.Result.ResultType)).ToArray();
            string[] causal=known.Where(r=>r.Predicate=="CausedOutcome"&&results.Any(s=>s.Result.CitedRefs.Contains(r.Id)&&s.Result.ProvenScope.Length>0)).Select(r=>r.Id).ToArray();string[] logic=results.Select(s=>s.Id).ToArray();
            EvaluationObligation Obligation(string id,string area,int weight,string[] refs)=>new EvaluationObligation{Id=id,Area=area,Weight=weight,Alternatives=new[]{new ObligationProof{Id=id+"_PROOF",Completion=refs.Length>0?1:0,Basis=refs}}};
            var independent=results.Where(s=>s.Result.RootGroups.Distinct().Count()>1).Select(s=>s.Id).ToArray();
            return new EvaluationInput{Version="X31_TEST_EVAL_01",Complexity="Tutorial",EssentialCausality=causal.Length>0,IndependentConfirmation=independent.Length>0,PersonalReasonedCorrect=causal.Length>0&&logic.Length>0,
                Obligations=new[]{Obligation("CASE_OBSERVATION","Investigation",200,known.Where(r=>r.Direct&&(r.Predicate=="UsedObject"||r.Predicate=="TracePresent"||PhysicalEvidenceScope.IsSurfaceState(r))).Select(r=>r.Id).ToArray()),Obligation("CAUSAL_CHAIN","Logic",200,causal),Obligation("INDEPENDENT_CONFIRMATION","Verification",150,independent),Obligation("PLAYER_COURT_CONTRIBUTION","Court",200,logic)}};
        }
        bool CommitSettlement(SettlementPlan plan)
        {
            var candidate=CaptureSession();foreach(var actor in candidate.World.Residents.Where(a=>plan.Executed.Contains(a.Id)||plan.Escaped.Contains(a.Id))){
                if(actor.HeldObject!=""){var item=candidate.World.Objects.Single(o=>o.Id==actor.HeldObject);item.Location="World";item.Owner="";if(item.PhysicsVersion==1){item.LinearVelocity=default;item.AngularVelocity=default;item.PhysicsSleeping=false;item.AnchorId="";}else item.Position=actor.Position;AppendEvent(candidate.World,item.PhysicsVersion==1?"ObjectReleased":"ObjectPlaced",actor.Id,item.Id,plan.Id);if(item.PhysicsVersion==1)item.ReleaseEventSequence=candidate.World.Sequence;actor.HeldObject="";}
                if(plan.Executed.Contains(actor.Id))actor.Alive=false;actor.Present=false;actor.Phase="Inactive";actor.Activity="";actor.Path=Array.Empty<string>();actor.PathCursor=0;actor.Destination="";
                AppendEvent(candidate.World,actor.Alive?"ResidentEscaped":"ResidentExecuted",actor.Id,actor.Id,plan.Id);
            }
            AppendEvent(candidate.World,"SettlementCommitted","PRES_YUSTI",plan.Id,plan.Id);
            candidate.Proceedings.Endgame.Plan=plan.Copy();candidate.Proceedings.Endgame.Phase="Settlement";
            foreach(var door in candidate.World.Doors){door.Queue=door.Queue.Where(id=>!plan.Executed.Contains(id)&&!plan.Escaped.Contains(id)).ToArray();if(plan.Executed.Contains(door.Holder)||plan.Escaped.Contains(door.Holder))door.Holder="";}
            Store().Save(SavePath,candidate);World=new MansionWorld(candidate.World,nodes,edges);Present();return true;
        }
        bool CommitTransition(SettlementPlan plan,string transition)
        {
            var candidate=CaptureSession();candidate.Proceedings.Endgame.Plan=plan.Copy();candidate.Proceedings.Endgame.Phase=transition=="Loop"?"LoopReady":"NextChapterReady";
            candidate.Proceedings.Archive=candidate.Proceedings.Archive.Concat(new[]{candidate.Proceedings.Endgame.Copy()}).ToArray();
            if(transition=="Loop"){
                var reset=loopInitial.Copy();reset.ClockVersion=World.ClockVersion;reset.Loop=World.Loop+1;reset.Chapter=1;candidate.World=reset;candidate.ItemExchange=new ItemExchangeSnapshot();candidate.Knowledge=new KnowledgeLedger(bodies.Keys,"LOOP_"+reset.Loop.ToString("00")).Capture();candidate.Social=new SocialLedger(bodies.Keys,Layout.Rooms.Select(r=>r.RoomId)).Capture();candidate.Notebook=new InvestigationNotebook().Capture();candidate.ClockRemainderHex="0000000000000000";
            }else{
                candidate.World.Chapter++;
                // Court seating is an indefinite activity. Release it at the real chapter boundary
                // so living residents can choose their next action from their current physical position.
                foreach(var actor in candidate.World.Residents.Where(a=>a.Alive&&a.Present&&a.Id!="CH_01")){
                    actor.Phase="Idle";actor.Activity="Rest";actor.ActivityTicks=0;actor.Destination="";actor.Path=Array.Empty<string>();actor.PathCursor=0;actor.StuckTicks=0;actor.QueueDoor="";actor.Recovering=false;actor.RecoveryTicks=0;
                }
                foreach(var door in candidate.World.Doors)door.Queue=Array.Empty<string>();
            }
            if(transition=="Loop")candidate.SurfaceTraces=new MansionSurfaceTraces(candidate.World.Loop,candidate.World.Tick).Capture();
            if(transition=="Loop")candidate.ActionJournal=new MansionActionJournal(ActionJournalDefinitions(),candidate.World.Loop,candidate.World.Tick).Capture();
            if(transition=="Loop")candidate.Recordings=EmptyRetiredRecordings(candidate.World.Loop,candidate.World.Tick);
            candidate.DoorObservations=new DoorObservationState{EventCursor=candidate.World.Sequence};
            candidate.PresenceObservations=new PresenceObservationState{Sequence=transition=="Loop"?0:candidate.PresenceObservations.Sequence};
            candidate.ResidentToolWork=Array.Empty<ResidentToolWork>();
            candidate.ResidentToolPickups=Array.Empty<ResidentToolPickup>();
            candidate.ResidentToolReturns=Array.Empty<ResidentToolReturn>();
            candidate.ResidentReadings=Array.Empty<ResidentReading>();
            candidate.WeaponMotion=new WeaponMotionState();candidate.WeaponRinse=new WeaponRinseState();
            if(transition=="Loop")candidate.WeaponResidues=new WeaponResidues(candidate.World.Loop,candidate.World.Tick).Capture();
            candidate.ToolPress=new ToolPressState();candidate.SurfaceReadings=Array.Empty<SurfaceReadingProgress>();
            candidate.ResidentConversations=Array.Empty<ConversationPlaybackState>();candidate.ResidentResponses=Array.Empty<ResidentResponseState>();
            if(transition=="Loop")candidate.FamilyDispute=new FamilyDisputeState{Version=0};
            if(transition=="Loop")candidate.ResidentIntents=new ResidentIntentSnapshot();
            else foreach(var intent in candidate.ResidentIntents.Intents.Where(ActiveIntent)){
                intent.Phase="Cancelled";intent.Outcome="ChapterChanged";intent.CompletedTick=candidate.World.Tick;intent.Plan.State="Cancelled";
                foreach(var speech in new[]{intent.Speech,intent.Reply})if(speech.Phase=="Speaking"){speech.Phase="Interrupted";speech.Committed=true;}
            }
            candidate.World.Cases=new MansionCaseBook(candidate.World.Loop,candidate.World.Chapter,candidate.World.Tick,candidate.World.Residents.Count(r=>r.Alive&&r.Present)).Capture();
            candidate.World.PauseOwners=Array.Empty<string>();candidate.Waiting=new WaitingState();candidate.Conversation=new ConversationPlaybackState{Sequence=candidate.Conversation.Sequence};candidate.Inspection=new InspectionState();candidate.Ui=new PlayerUiSnapshot();candidate.Incident=new MansionIncident().Capture();
            candidate.CaseCollectionVersion=1;candidate.Incidents=Array.Empty<MansionIncidentSnapshot>();
            var nextRules=ChapterRules.Next(proceedings.Rules,candidate.World.Loop,candidate.World.Chapter,candidate.World.Residents.Count(a=>a.Alive&&a.Present),candidate.World.Residents.Count(a=>a.Id!="CH_01"&&a.Alive&&a.Present),ImplementedChapterRules,proceedings.Rules.RandomState);
            candidate.Proceedings=new MansionProceedings{Rules=nextRules,StatementSeals=candidate.World.Loop==World.Loop?candidate.Proceedings.StatementSeals:Array.Empty<MansionStatementSeal>(),CampaignId=proceedings.CampaignId,Archive=candidate.Proceedings.Archive,VoteRandomState=proceedings.VoteRandomState,PresenterPosition=proceedings.PresenterPosition,PresenterNode=proceedings.PresenterNode};
            candidate.OptionalObjects=(candidate.OptionalObjects&~(1|2|4|8|256))|16|32|64|128|1024|2048;candidate.SpeechesWithoutClaim=Array.Empty<string>();
            Store().Save(SavePath,candidate);ApplySession(candidate);return true;
        }
        void SaveAutomatic(string reason)
        {
            // The normal continue slot is the durable transaction boundary; archive copies are supplementary.
            SaveTo(SavePath);
        }
        static void AppendEvent(MansionState state,string type,string actor,string target,string detail){state.Events=state.Events.Concat(new[]{new MansionEvent{Sequence=++state.Sequence,Tick=state.Tick,Type=type,Actor=actor,Target=target,Detail=detail}}).ToArray();}
        string ProfilePath=>Path.Combine(StorageDirectory,"profile-v1.dat");
        TrialProfile ReadProfile()
        {
            if(!File.Exists(ProfilePath))return new TrialProfile();string text=File.ReadAllText(ProfilePath);if(text.Length<65||AtomicSaveStore.Hash(text.Substring(65))!=text.Substring(0,64))throw new InvalidDataException("성장 기록 무결성 오류");return JsonUtility.FromJson<TrialProfile>(text.Substring(65));
        }
        void WriteProfile(TrialProfile profile)
        {
            string text=JsonUtility.ToJson(profile);string path=ProfilePath;Directory.CreateDirectory(Path.GetDirectoryName(path));using(var writer=new FileStream(path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None)){byte[] bytes=System.Text.Encoding.UTF8.GetBytes(AtomicSaveStore.Hash(text)+"\n"+text);writer.Write(bytes,0,bytes.Length);writer.Flush(true);}if(File.Exists(path))File.Replace(path+".tmp",path,path+".previous");else File.Move(path+".tmp",path);
        }
    }
}
