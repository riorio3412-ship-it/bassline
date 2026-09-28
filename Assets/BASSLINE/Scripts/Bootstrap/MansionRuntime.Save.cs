using System;
using System.IO;
using System.Linq;
using System.Globalization;
using UnityEngine;
using BASSLINE.Save;
using BASSLINE.World.Mansion;
using BASSLINE.Knowledge;
using BASSLINE.NPC;
using BASSLINE.Investigation;
using BASSLINE.Trial;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        public bool HasSave=>File.Exists(SavePath);
        string StorageDirectory
        {
            get
            {
#if UNITY_EDITOR || DEBUG
                if(!string.IsNullOrEmpty(isolatedTestStorage))return isolatedTestStorage;
#endif
                return Path.Combine(Application.persistentDataPath,"MansionM01");
            }
        }
#if UNITY_EDITOR || DEBUG
        string isolatedTestStorage;
        // Full-flow Editor tests must never commit automatic saves/rewards to a player's profile.
        // Development players may isolate only the explicit visual smoke command; release builds expose no override.
        public string UseIsolatedTestStorage()
        {
            if(!Application.isEditor&&(!Debug.isDebugBuild||Array.IndexOf(Environment.GetCommandLineArgs(),"-bassline-mansion-smoke")<0))throw new InvalidOperationException("Explicit review session required");
            if(string.IsNullOrEmpty(isolatedTestStorage))
            {
                isolatedTestStorage=Path.Combine(Application.temporaryCachePath,"BASSLINE-M01-tests",Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(isolatedTestStorage);
            }
            return isolatedTestStorage;
        }
#endif
        public string SavePath=>Path.Combine(StorageDirectory,"session-v1.dat");
        MansionSaveStore Store()=>new MansionSaveStore(s=>JsonUtility.ToJson(s),json=>UpgradeLooseObjects(UpgradeWeaponContent(UpgradeAuthoredLoanContent(UpgradeSurfaceTraceContent(UpgradeActionJournalContent(UpgradeRecordingContent(UpgradeAppointmentCardContent(UpgradeLoanContent(DecodeSession(json))))))))),ValidateSession);
        static MansionSessionSnapshot DecodeSession(string json)
        {
            var s=JsonUtility.FromJson<MansionSessionSnapshot>(json);if(s==null||s.Incident==null||s.Proceedings==null)return s;
            if((s.OptionalObjects&262144)==0&&s.World!=null)s.DoorObservations=new BASSLINE.Core.DoorObservationState{EventCursor=s.World.Sequence};
            if((s.OptionalObjects&524288)==0&&s.DoorObservations!=null)s.DoorObservations.Passages=Array.Empty<BASSLINE.Core.DoorPassageProgress>();
            if((s.OptionalObjects&1048576)==0)s.PresenceObservations=new BASSLINE.Core.PresenceObservationState();
            if((s.OptionalObjects&131072)==0&&s.Inspection!=null){
                // Old saves did not retain continuous focus. Keep received records,
                // but do not promote an unobserved interval to a completed inspection.
                if(s.Inspection.State=="Running")s.Inspection.State="Cancelled";
                s.Inspection.FocusMode="Target";s.Inspection.FocusRoot="";s.Inspection.FocusValue="";
                s.Inspection.StartedTick=-1;s.Inspection.LastTick=-1;
            }
            if((s.OptionalObjects&65536)==0)s.ToolPress=new BASSLINE.Core.ToolPressState();
            if((s.OptionalObjects&2097152)==0){s.ResidentToolWork=Array.Empty<BASSLINE.Core.ResidentToolWork>();if(s.ToolPress!=null)s.ToolPress.ActorId="CH_01";}
            if((s.OptionalObjects&4194304)==0)s.ResidentToolPickups=Array.Empty<BASSLINE.Core.ResidentToolPickup>();
            if((s.OptionalObjects&8388608)==0)s.ResidentToolReturns=Array.Empty<BASSLINE.Core.ResidentToolReturn>();
            if((s.OptionalObjects&134217728)==0)s.ResidentReadings=Array.Empty<BASSLINE.Core.ResidentReading>();
            if(s.FamilyDispute==null)s.FamilyDispute=new BASSLINE.Core.FamilyDisputeState{Version=0};
            if(s.Waiting==null)s.Waiting=new BASSLINE.Core.WaitingState();
            if((s.OptionalObjects&64)==0||s.Conversation==null)s.Conversation=new BASSLINE.Core.ConversationPlaybackState();
            if((s.OptionalObjects&1024)==0)s.ResidentConversations=Array.Empty<BASSLINE.Core.ConversationPlaybackState>();
            if((s.OptionalObjects&4096)==0)s.ResidentResponses=Array.Empty<BASSLINE.Core.ResidentResponseState>();
            if((s.OptionalObjects&8192)==0)s.ResidentIntents=new BASSLINE.Core.ResidentIntentSnapshot();
            if((s.OptionalObjects&256)==0)s.Conversation.Appointment=null;
            if((s.OptionalObjects&512)==0&&s.Social!=null)s.Social.Card=new BASSLINE.Core.AppointmentCardState();
            if((s.OptionalObjects&128)==0)s.ItemExchange=new BASSLINE.Core.ItemExchangeSnapshot();
            if((s.OptionalObjects&33554432)==0&&s.ItemExchange!=null){s.ItemExchange.Requests=Array.Empty<BASSLINE.Core.EverydayRequestState>();if(s.ItemExchange.Handoff!=null)s.ItemExchange.Handoff.RequestId="";}
            if((s.OptionalObjects&67108864)==0&&s.ItemExchange!=null)s.ItemExchange.Journal=new BASSLINE.Core.TaskJournalSettings();
            if(s.ItemExchange?.Learning!=null&&string.IsNullOrEmpty(s.ItemExchange.Learning.RecordId))s.ItemExchange.Learning=null;
            // Unity serializes inline null classes as empty objects. Explicit presence bits preserve their meaning.
            // Old files have no chapter book; FromJson still materializes an empty inline object.
            if((s.OptionalObjects&16)==0&&s.World!=null)s.World.Cases=null;
            if((s.OptionalObjects&1)==0)s.Incident.Settings=null;
            if((s.OptionalObjects&2)==0)s.Proceedings.Court.Focus=null;
            if((s.OptionalObjects&4)==0)s.Proceedings.Endgame.Plan=null;
            if((s.OptionalObjects&8)==0)s.Proceedings.Endgame.Evaluation=null;
            foreach(var speech in s.Proceedings.Court.Pending.Concat(s.Proceedings.Court.DeferredSpeeches??Array.Empty<SpeechDraft>()).Concat(s.Proceedings.Court.Transcript.Select(t=>t.Speech)))if(s.SpeechesWithoutClaim.Contains(speech.Id))speech.Claim=null;
            UpgradeCaseCollection(s);
            if(s.World!=null&&(s.WeaponResidues==null||s.WeaponResidues.Version==0)){s.WeaponResidues=new WeaponResidues(s.World.Loop,s.World.Tick).Capture();s.WeaponRinse=new BASSLINE.Core.WeaponRinseState();}
            if(s.Proceedings.StatementSeals==null)s.Proceedings.StatementSeals=Array.Empty<MansionStatementSeal>();
            if(s.Proceedings.Court.Examinations==null)s.Proceedings.Court.Examinations=Array.Empty<BASSLINE.Trial.WitnessExaminationState>();
            if(s.Proceedings.Reconstruction==null||string.IsNullOrEmpty(s.Proceedings.Reconstruction.Phase))s.Proceedings.Reconstruction=new ReconstructionSnapshot();
            if(s.Proceedings.Court.DeferredSpeeches==null)s.Proceedings.Court.DeferredSpeeches=Array.Empty<SpeechDraft>();
            if(s.Proceedings.NpcCounters==null)s.Proceedings.NpcCounters=Array.Empty<BASSLINE.Core.NpcCounterProgress>();
            if(s.Ui!=null){
                if(string.IsNullOrEmpty(s.Ui.ReconstructionPanel))s.Ui.ReconstructionPanel="Entries";
                if(string.IsNullOrEmpty(s.Ui.ReconstructionGroup))s.Ui.ReconstructionGroup="A";
                s.Ui.ReconstructionEntry=s.Ui.ReconstructionEntry??"";s.Ui.ReconstructionRecord=s.Ui.ReconstructionRecord??"";
            }
            if(((s.OptionalObjects&32)==0||s.Proceedings.Rules==null)&&s.World!=null)s.Proceedings.Rules=new ChapterRulePlan{Loop=s.World.Loop,Chapter=s.World.Chapter,StartN=Math.Max(4,s.World.Residents.Count(a=>a.Alive&&a.Present))};
            return s;
        }
        public MansionSessionSnapshot CaptureSession()
        {
            World.PendingTime=accumulator;
            proceedings.Court=court.Capture();proceedings.Endgame=endgame.Capture();
            var cases=incidents.Capture();
            var snapshot=new MansionSessionSnapshot{World=World.Capture(),Knowledge=Knowledge.Capture(),Social=Social.Capture(),Notebook=Notebook.Capture(),CaseCollectionVersion=1,Incidents=cases,Incident=cases.FirstOrDefault()??new MansionIncident().Capture(),Proceedings=proceedings,Waiting=waiting.Copy(),Conversation=conversationPlayback.Copy(),ResidentConversations=residentConversations.Select(c=>c.Copy()).ToArray(),ItemExchange=itemExchange.Copy(),Inspection=inspection.Copy(),Ui=ui.Copy(),ClockRemainderHex=BitConverter.DoubleToInt64Bits(accumulator).ToString("X16",CultureInfo.InvariantCulture)};
            snapshot.OptionalObjects=(snapshot.Incident.Settings!=null?1:0)|(snapshot.Proceedings.Court.Focus!=null?2:0)|(snapshot.Proceedings.Endgame.Plan!=null?4:0)|(snapshot.Proceedings.Endgame.Evaluation!=null?8:0)|(snapshot.World.Cases!=null?16:0)|(snapshot.Conversation.Appointment!=null?256:0)|(ObjectBodies.Any(o=>o.ObjectId==BASSLINE.AuthoringData.AppointmentDesk.CardId)?512:0)|32|64|128|1024;
            // Some automatic checkpoints occur inside a world step, before visual
            // presentation/inspection. Do not serialize that unfinished interval as read.
            if(snapshot.Inspection.State=="Running"&&snapshot.Inspection.LastTick!=snapshot.World.Tick)snapshot.Inspection.State="Cancelled";
            snapshot.OptionalObjects|=131072|262144|524288;snapshot.DoorObservations=doorObservations.Copy();
            snapshot.OptionalObjects|=1048576;snapshot.PresenceObservations=presenceObservations.Copy();
            snapshot.WeaponResidues=weaponResidues.Capture();snapshot.WeaponRinse=weaponRinse.Copy();
            if(snapshot.WeaponRinse.Running&&snapshot.WeaponRinse.LastTick!=snapshot.World.Tick)snapshot.WeaponRinse.Phase="Cancelled";
            snapshot.WeaponMotion=weaponMotion.Copy();snapshot.OptionalObjects|=WeaponContentBit;
            snapshot.ToolPress=toolPress.Copy();snapshot.OptionalObjects|=65536|2097152;
            snapshot.ResidentToolWork=residentToolWork.Select(w=>w.Copy()).ToArray();
            snapshot.OptionalObjects|=4194304;snapshot.ResidentToolPickups=residentToolPickups.Select(p=>p.Copy()).ToArray();
            snapshot.OptionalObjects|=8388608;snapshot.ResidentToolReturns=residentToolReturns.Select(p=>p.Copy()).ToArray();
            if(HasAdditionalLoanContent)snapshot.OptionalObjects|=16777216;
            snapshot.OptionalObjects|=33554432;
            snapshot.OptionalObjects|=67108864;
            CaptureResidentReadings(snapshot);
            foreach(var returned in snapshot.ResidentToolReturns.Where(p=>p.Running)){
                var actor=snapshot.World.Residents.Single(a=>a.Id==returned.ActorId);var item=snapshot.World.Objects.Single(o=>o.Id==returned.ToolId);
                if(!actor.Alive||!actor.Present||actor.Phase!="Performing"||actor.Activity!=returned.Activity||actor.Node!=returned.Node||snapshot.World.Tick+actor.ActivityTicks!=returned.ActivityEndTick||returned.ReleasedTick<0&&(actor.HeldObject!=returned.ToolId||item.Location!="Hand"||item.Owner!=returned.ActorId)){returned.Phase="Cancelled";returned.Reason="CheckpointAfterInterruption";}
            }
            foreach(var pickup in snapshot.ResidentToolPickups.Where(p=>p.Running)){
                var actor=snapshot.World.Residents.Single(a=>a.Id==pickup.ActorId);var item=snapshot.World.Objects.Single(o=>o.Id==pickup.ToolId);
                if(!actor.Alive||!actor.Present||actor.Phase!="Performing"||actor.Activity!=pickup.Activity||actor.Node!=pickup.Node||snapshot.World.Tick+actor.ActivityTicks!=pickup.ActivityEndTick||(pickup.AcquiredTick>=0?(actor.HeldObject!=pickup.ToolId||item.Owner!=pickup.ActorId||item.Location!="Hand"):(actor.HeldObject!=""||item.Location!="World"||item.Position.Distance(pickup.ToolStart)>.003))){pickup.Phase="Cancelled";pickup.Reason="CheckpointAfterInterruption";}
            }
            foreach(var work in snapshot.ResidentToolWork.Where(w=>w.Motion.Running)){
                var actor=snapshot.World.Residents.Single(a=>a.Id==work.ActorId);
                if(!actor.Alive||!actor.Present||actor.Phase!="Performing"||actor.Activity!=work.Activity||actor.Node!=work.Node||snapshot.World.Tick+actor.ActivityTicks!=work.ActivityEndTick||actor.HeldObject!=work.Motion.ToolId){work.Motion.Phase="Cancelled";work.Motion.Reason="CheckpointAfterInterruption";}
            }
            snapshot.SurfaceReadings=surfaceReadings.Select(r=>r.Copy()).ToArray();snapshot.SurfaceTraces=surfaceTraces.Capture();snapshot.OptionalObjects|=32768;
            snapshot.ActionJournal=actionJournal.Capture();snapshot.OptionalObjects|=16384;
            snapshot.Recordings=EmptyRetiredRecordings(World.Loop,World.Tick);snapshot.ResidentResponses=residentResponses.Select(t=>t.Copy()).ToArray();snapshot.OptionalObjects|=2048|4096;
            snapshot.FamilyDispute=familyDispute.Copy();
            snapshot.ResidentIntents=residentIntents.Copy();snapshot.OptionalObjects|=8192;
            snapshot.SpeechesWithoutClaim=snapshot.Proceedings.Court.Pending.Concat(snapshot.Proceedings.Court.DeferredSpeeches).Concat(snapshot.Proceedings.Court.Transcript.Select(t=>t.Speech)).Where(s=>s.Claim==null).Select(s=>s.Id).ToArray();
            return DecodeSession(JsonUtility.ToJson(snapshot));
        }
        void ValidateSession(MansionSessionSnapshot s)
        {
            if(s==null||s.Schema!=2||s.Magic!="BASSLINE_MANSION_SESSION"||s.World==null||s.World.MapVersion!=Layout.MapVersion||s.World.CatalogHash!=GeometryRevision)throw new InvalidDataException("이 저택 버전과 호환되지 않는 저장입니다.");
            MansionWorld.Validate(s.World,nodes.ToDictionary(n=>n.Id));
            ValidateConversation(s.Conversation,s.World.Tick);
            ChapterRules.Validate(s.Proceedings.Rules);
            if(s.Proceedings.Rules.Loop!=s.World.Loop||s.Proceedings.Rules.Chapter!=s.World.Chapter||s.Proceedings.Rules.AnnouncedTick>s.World.Tick)throw new InvalidDataException("규칙과 챕터 상태가 맞지 않습니다.");
            BASSLINE.Core.WaitingPolicy.Validate(s.Waiting??new BASSLINE.Core.WaitingState(),s.World.Tick);
            if(!s.World.Residents.Select(r=>r.Id).OrderBy(x=>x).SequenceEqual(bodies.Keys.OrderBy(x=>x))||!s.World.Doors.Select(d=>d.Id).OrderBy(x=>x).SequenceEqual(doors.Keys.OrderBy(x=>x)))throw new InvalidDataException("저장 ID가 현재 저택과 다릅니다.");
            var knowledge=KnowledgeLedger.Restore(s.Knowledge,bodies.Keys,s.World.Tick);SocialLedger.Restore(s.Social,bodies.Keys,Layout.Rooms.Select(r=>r.RoomId),s.World.Tick);InvestigationNotebook.Restore(s.Notebook,knowledge.For);
            if(!s.World.Objects.Select(o=>o.Id).OrderBy(id=>id).SequenceEqual(ObjectBodies.Select(o=>o.ObjectId).OrderBy(id=>id)))throw new InvalidDataException("저장 물건 ID가 현재 저택과 다릅니다.");
            ValidateItemExchange(s.ItemExchange,s.World,knowledge);
            ValidateLoanSpeech(s.Conversation,s.ItemExchange,s.World,knowledge);
            ValidateEverydaySpeech(s.Conversation,s.ItemExchange,knowledge);
            ValidateAppointmentSpeech(s.Conversation,s.Social,s.World,knowledge);
            ValidateAppointmentCard(s.Social,s.World,knowledge);
            ValidateResidentConversations(s,knowledge);
            ValidateResidentIntents(s,knowledge);ValidateFamilyDispute(s,knowledge);
            ValidateLoanLearning(s.ItemExchange,s.World,knowledge);
            var seals=s.Proceedings.StatementSeals;
            if(seals==null||seals.Any(seal=>seal==null||seal.Loop!=s.World.Loop||seal.Chapter<1||seal.Chapter>s.World.Chapter||!bodies.ContainsKey(seal.ActorId)||string.IsNullOrWhiteSpace(seal.RuleInstanceId)||knowledge.For(seal.ActorId).Find(seal.RecordId)?.Predicate!="SaidStatement")||seals.Select(seal=>seal.Chapter+"|"+seal.ActorId).Distinct().Count()!=seals.Length)throw new InvalidDataException("봉인한 진술 원문이 올바르지 않습니다.");
            if(knowledge.LoopId!="LOOP_"+s.World.Loop.ToString("00"))throw new InvalidDataException("다른 회차의 지식이 포함된 저장입니다.");
            var restoredCases=MansionIncidentCollection.Restore(s.Incidents,new MansionWorld(s.World,nodes,edges));ValidateCaseProgress(s,restoredCases);ValidateResidentResponses(s,restoredCases,knowledge);
            foreach(var incident in restoredCases.Capture())if(incident.Plan!=null){
                BASSLINE.Core.IncidentPlanIntegrity.Validate(incident.Plan,knowledge.For(incident.Settings.ActorId),s.World.Tick);
                if(!incident.Settings.ExplicitTestSession)BASSLINE.Core.IncidentExecutionBinding.Validate(incident.Settings.Execution,incident.Plan,knowledge.For(incident.Settings.ActorId),s.World.Tick);
                if(incident.Plan.TargetId!=incident.Settings.TargetId||!incident.Plan.RequiredResources.Contains(incident.Settings.ObjectId)||incident.Plan.Origin=="ExplicitTestReplay"&&!incident.Settings.ExplicitTestSession)throw new InvalidDataException("사건 실행과 개인 계획이 일치하지 않습니다.");
            }
            foreach(var incident in restoredCases.Capture())if(incident.Admission!=null){
                BASSLINE.Core.IncidentAdmission.ValidateReceipt(incident.Admission,incident.Settings.Id,s.World.Tick);
                if(incident.CauseTick>=0&&(incident.Admission.Status!="Admitted"||incident.Admission.Tick!=incident.CauseTick))throw new InvalidDataException("원인 실행 전에 승인되지 않은 사건입니다.");
            }
            MansionSurfaceTraces.Restore(s.SurfaceTraces,s.World.Loop,s.World.Tick,traceSurfaces.Select(TraceSurfaceId).ToArray(),traceSources.Select(TraceSourceId).ToArray());
            foreach(var incident in s.Incidents)foreach(var link in incident.PhysicalTraces??Array.Empty<IncidentPhysicalTrace>()){
                var mark=s.SurfaceTraces.Marks.FirstOrDefault(m=>m.Id==link.MarkId);
                if(mark==null||mark.SourceId!=link.SourceId||mark.SurfaceId!=link.SurfaceId||mark.DepositedTick!=link.ContactTick||mark.LocalPoint.Distance(link.MarkLocalPoint)>.00001)throw new InvalidDataException("사건 접촉과 실제 표면 흔적이 일치하지 않습니다.");
            }
            ValidateWeaponResidues(s);ValidateWeaponMotion(s);ValidateSurfaceReadings(s);ValidateToolPress(s);ValidateInspection(s);ValidateDoorObservations(s);ValidateDoorPassages(s,knowledge);ValidatePresenceObservations(s,knowledge);ValidateResidentToolWork(s);ValidateResidentToolPickups(s,knowledge);ValidateResidentToolReturns(s);ValidateResidentReadings(s,knowledge);
            // Retired recording payload has no role in the current simulation.
            MansionActionJournal.Restore(s.ActionJournal,ActionJournalDefinitions(),s.World.Loop,s.World.Tick);
            if(s.Proceedings==null||string.IsNullOrWhiteSpace(s.Proceedings.CampaignId)||!s.Proceedings.PresenterPosition.Finite()||!s.Proceedings.PresenterQueuePosition.Finite()||s.Proceedings.PresenterQueueDoor==null||s.Proceedings.PresenterQueueDoor!=""&&!doors.ContainsKey(s.Proceedings.PresenterQueueDoor)||s.Proceedings.PresenterCursor<0||s.Proceedings.PresenterCursor>s.Proceedings.PresenterPath.Length||s.Proceedings.PresenterPath.Any(id=>!nodes.Any(n=>n.Id==id)))throw new InvalidDataException("진행 상태가 잘못되었습니다.");
            TrialDirector.Restore(s.Proceedings.Court);TrialEndgame.Restore(s.Proceedings.Endgame);foreach(var archive in s.Proceedings.Archive)TrialEndgame.Restore(archive);
            ValidateNpcCounters(s,knowledge);ValidateReconstruction(s,knowledge);
            ValidateJointSharing(s,knowledge);
            if(s.Ui==null||s.Ui.Pages==null||s.Ui.Pages.Length>32||s.Ui.Pages.Any(p=>p<2||p>41)||s.Inspection==null||s.Inspection.State=="Running"&&!targets.ContainsKey(s.Inspection.TargetId))throw new InvalidDataException("잘못된 화면 또는 조사 상태입니다.");
            if(s.Ui.SelectedEvidence==null||s.Ui.SelectedEvidence.Any(id=>knowledge.For("CH_01").Find(id)==null)||s.Ui.TrialAction<0||s.Ui.TrialAction>4||s.Ui.TrialRule<0||s.Ui.TrialRule>9)throw new InvalidDataException("확보하지 않은 자료가 선택된 저장입니다.");
            if(!new[]{"Entries","Evidence","Target","Entry","Confirm","SkipConfirm"}.Contains(s.Ui.ReconstructionPanel)||!new[]{"A","B"}.Contains(s.Ui.ReconstructionGroup)||s.Ui.ReconstructionTarget<0||s.Ui.ReconstructionTarget>=18)throw new InvalidDataException("사건 정리 화면의 선택 상태가 잘못되었습니다.");
            if(s.ClockRemainderHex==null||s.ClockRemainderHex.Length!=16||!long.TryParse(s.ClockRemainderHex,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out var bits))throw new InvalidDataException("잘못된 시계 상태입니다.");
            double fraction=BitConverter.Int64BitsToDouble(bits);if(double.IsNaN(fraction)||double.IsInfinity(fraction)||fraction<0||Math.Abs(fraction-s.World.PendingTime)>1e-12)throw new InvalidDataException("시계 상태가 일치하지 않습니다.");
            foreach(var actor in s.World.Residents.Where(a=>a.Present)){
                var p=V(actor.Position);if(Layout.RoomAt(p)==null)throw new InvalidDataException("인물의 위치가 보행 구역을 벗어났습니다: "+actor.Id);
                var b=bodies[actor.Id];
                int count=Physics.OverlapCapsuleNonAlloc(p+Vector3.up*.31f,p+Vector3.up*(b.Height-.3f),.255f,overlap,~0,QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++){
                    var collider=overlap[i];if(collider.GetComponentInParent<BASSLINE.AuthoringData.FixtureActorBody>()||collider.GetComponentInParent<BASSLINE.AuthoringData.FixtureObjectBody>()||collider.GetComponentInParent<BASSLINE.AuthoringData.MansionConnection>())continue;
                    throw new InvalidDataException("저장 위치가 고정 구조물과 겹칩니다: "+actor.Id+" / "+collider.name);
                }
            }
        }
        public void SaveTo(string path)=>Store().Save(path,CaptureSession());
        public void LoadFrom(string path)
        {
            ApplySession(Store().Load(path));
        }
        void ApplySession(MansionSessionSnapshot s)
        {
            var world=new MansionWorld(s.World,nodes,edges);var knowledge=KnowledgeLedger.Restore(s.Knowledge,bodies.Keys,s.World.Tick);var social=SocialLedger.Restore(s.Social,bodies.Keys,Layout.Rooms.Select(r=>r.RoomId),s.World.Tick);var notebook=InvestigationNotebook.Restore(s.Notebook,knowledge.For);
            residentConversations=s.ResidentConversations.Select(c=>c.Copy()).ToList();
            residentResponses=s.ResidentResponses.Select(t=>t.Copy()).ToList();
            residentIntents=s.ResidentIntents.Copy();
            World=world;Knowledge=knowledge;Social=social;familyDispute=s.FamilyDispute.Copy();InitializeFamilyDispute();Notebook=notebook;waiting=s.Waiting??new BASSLINE.Core.WaitingState();inspection=s.Inspection;ui=s.Ui;conversationPlayback=s.Conversation;itemExchange=s.ItemExchange;accumulator=BitConverter.Int64BitsToDouble(long.Parse(s.ClockRemainderHex,NumberStyles.HexNumber,CultureInfo.InvariantCulture));move=default;running=false;
            proceedings=s.Proceedings;incidents=MansionIncidentCollection.Restore(s.Incidents,World);court=TrialDirector.Restore(proceedings.Court);endgame=TrialEndgame.Restore(proceedings.Endgame);
            doorObservations=s.DoorObservations.Copy();
            presenceObservations=s.PresenceObservations.Copy();
            weaponMotion=s.WeaponMotion.Copy();RestoreWeaponResidues(s);
            toolPress=s.ToolPress.Copy();residentToolWork=s.ResidentToolWork.Select(w=>w.Copy()).ToArray();residentToolPickups=s.ResidentToolPickups.Select(p=>p.Copy()).ToArray();residentToolReturns=s.ResidentToolReturns.Select(p=>p.Copy()).ToArray();residentReadings=s.ResidentReadings.Select(p=>p.Copy()).ToArray();RestoreSurfaceTraces(s);
            // No recording producer or public reader is restored.
            actionJournal=MansionActionJournal.Restore(s.ActionJournal,ActionJournalDefinitions(),World.Loop,World.Tick);
            if(Presenter){Presenter.Capsule.enabled=false;Presenter.transform.position=V(proceedings.PresenterPosition);Presenter.Capsule.enabled=true;}
            foreach(var b in Bodies)b.Capsule.enabled=false;
            foreach(var b in Bodies)b.transform.position=V(World.Resident(b.ActorId).Position);
            foreach(var d in World.Doors)DoorPose(d.Id,d.Open);
            foreach(var b in Bodies)b.Capsule.enabled=true;
            ClearInactiveIncidentSignals();
            learningCheckedRevision=-1;learningCheckedReturn=-1;learningRetryAt=0;
            Physics.SyncTransforms();Present();PrepareLooseObjects(true);
        }
        public string SaveSlot(){try{SaveTo(SavePath);return message="저장 완료";}catch(Exception e){return message="저장 실패: "+e.Message;}}
        public string LoadSlot(){try{LoadFrom(SavePath);return message="불러오기 완료";}catch(Exception e){return message="불러오기 실패: "+e.Message;}}
    }
}
