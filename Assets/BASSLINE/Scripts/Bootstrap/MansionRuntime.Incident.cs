using System;
using System.Linq;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
using BASSLINE.World.Mansion;
using BASSLINE.Save;

namespace BASSLINE.Bootstrap
{
    // SRC11 P0153–P0156, P0469, P0612–P0615. Public posting is not personal reception.
    // Carrier rounds and their 12 m unobstructed voice radius are reversible production proposals,
    // not a claim that the source mansion contains a whole-building PA network.
    public static class MansionAnnouncementPolicy
    {
        public static bool CanReceive(bool alive,bool present,string activity,bool silent,bool audible)
            =>alive&&present&&!silent&&audible&&activity!="Sleep"&&activity!="Sleeping";
        public static bool IsPublicSearchRoom(string id)
            =>!string.IsNullOrEmpty(id)&&!(id.StartsWith("R_BED_",StringComparison.Ordinal)&&id!="R_BED_COR");
        public static bool HasReceipt(IActorKnowledgeQuery own,string notice,string predicate)
            =>own.Records().Any(r=>r.LoopId==own.LoopId&&r.ProvenanceKey==notice&&r.Predicate==predicate&&r.Source=="PRES_YUSTI");
        public static string NextStop(string[] stops,string previous)
        {
            if(stops==null||stops.Length==0)return "";
            int prior=Array.IndexOf(stops,previous);return stops[(prior+1)%stops.Length];
        }
        public static KnownRecord Notice(string notice,string predicate,string text,string place,Point3 position,long tick)
            =>new KnownRecord{Kind="OfficialAnnouncement",ProvenanceKey=notice,Source="PRES_YUSTI",SubjectId="PRES_YUSTI",Predicate=predicate,Value="R_TRIAL",Text=text,PlaceId=place,Position=position,FromTick=tick,ToTick=tick+1,Supports=new[]{"유스티에게 실제 수신한 공표와 안내"},DoesNotEstablish=new[]{"범인·사망 시각·원인·다른 참가자의 수신 여부는 확정하지 않음"}};
    }
    public sealed partial class MansionRuntime:IMansionIncidentPhysics,IMansionIncidentInterventions
    {
        public FixtureActorBody Presenter;
        // Registration of source X31 is available to explicit test sessions only. Normal play starts disabled.
        MansionIncidentCollection incidents=new MansionIncidentCollection();
        MansionProceedings proceedings=new MansionProceedings();
        MansionState loopInitial;
        // The ability adapter may supply its existing, persisted room-silence state here.
        // This callback neither creates an ability nor changes anyone's knowledge by itself.
        public Func<string,bool> AnnouncementRoomIsSilent;
        string NoticeId=>"OFFICIAL_L"+World.Loop+"_C"+World.Chapter+"_"+proceedings.FirstAnnouncementTick+(proceedings.PublicSchedule.Revision>1?"_R"+proceedings.PublicSchedule.Revision:"");
        public bool HasReceivedCourtSummons(string actorId)
            =>World.Residents.Any(r=>r.Id==actorId&&r.Alive&&r.Present)&&MansionAnnouncementPolicy.HasReceipt(Knowledge.For(actorId),NoticeId,"CourtSummons");
        public bool PresenterReadyForCourt()
            =>Presenter&&Layout.Room("R_TRIAL")!=null&&PlaceOf(Presenter.transform.position)=="R_TRIAL"&&Vector3.Distance(Presenter.transform.position,Layout.Room("R_TRIAL").WalkPoint)<1;
        FixtureActorBody PhysicalBody(string id)=>id=="PRES_YUSTI"?Presenter:bodies.TryGetValue(id,out var b)?b:null;
        Vector3 SubjectPoint(string id)
        {
            var b=PhysicalBody(id);if(b)return b.transform.position+Vector3.up*b.Height*.65f;
            return targets.TryGetValue(id,out var t)?t.transform.position:Vector3.positiveInfinity;
        }
        public bool CanReach(string observer,string subject,double distance)
        {
            var b=PhysicalBody(observer);return b&&targets.ContainsKey(subject)&&Vector3.Distance(b.transform.position,SubjectPoint(subject))<=distance&&Visible(observer,SubjectPoint(subject),(float)distance+.6f,false);
        }
        public bool CanSee(string observer,string subject)=>PhysicalBody(observer)&&targets.ContainsKey(subject)&&Visible(observer,SubjectPoint(subject));
        public bool Identifies(string observer,string subject)=>CanSee(observer,subject)&&Vector3.Distance(PhysicalBody(observer).transform.position,SubjectPoint(subject))<7;
        public bool ReceivesSpeech(string observer,string speaker)
        {
            var actor=World.Residents.FirstOrDefault(r=>r.Id==observer);
            return actor!=null&&World.CanAct(observer)&&(speaker=="PRES_YUSTI"||World.CanAct(speaker))&&MansionAnnouncementPolicy.CanReceive(actor.Alive,actor.Present,actor.Activity,RoomSilent(observer)||RoomSilent(speaker),SpeechPathOpen(observer,speaker));
        }
        bool SpeechPathOpen(string observer,string speaker)
        {
            var listener=PhysicalBody(observer);var source=PhysicalBody(speaker);if(!listener||!source)return false;
            var origin=source.transform.position+Vector3.up*source.Height*.85f;
            var delta=listener.transform.position+Vector3.up*listener.Height*.85f-origin;if(delta.magnitude>8)return false;
            int count=Physics.RaycastNonAlloc(origin,delta.normalized,rayHits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            // A passing person's capsule obscures sight, not an entire nearby spoken sentence.
            for(int i=0;i<count;i++)if(!rayHits[i].collider.GetComponentInParent<FixtureActorBody>())return false;
            return count<rayHits.Length;
        }
        bool RoomSilent(string actor){var body=PhysicalBody(actor);return body&&(AnnouncementRoomIsSilent?.Invoke(PlaceOf(body.transform.position))??false);}
        public bool HasContact(string actor,string target,string item,out Point3 contact)
        {
            contact=default;if(!bodies.ContainsKey(actor)||!bodies.ContainsKey(target)||World.Resident(actor).HeldObject!=item)return false;
            var subject=bodies[target];Vector3 hand=bodies[actor].RightHand.position;
            Vector3 nearest=subject.Capsule.ClosestPoint(hand);
            if(Vector3.Distance(nearest,hand)>.25f||!CanReach(actor,target,1.5))return false;
            contact=P(nearest);return true;
        }
        string IMansionIncidentInterventions.InterruptionFor(string actor,string target)
        {
            if(PlayerTalkingTo(actor)&&CanSee(actor,"CH_01")&&ReceivesSpeech(actor,"CH_01")){ForgetCancelledPlanResume(actor);return "DirectConversationIntervention";}
            if(PlayerTalkingTo(target)&&CanSee(actor,target)&&CanSee(actor,"CH_01"))return "TargetEngagedWithCompanion";
            if(ResidentIsSpeaking(actor)||IntentIsSpeaking(actor)){ForgetCancelledPlanResume(actor);return "OtherConversationInProgress";}
            if(HasReceivedCourtSummons(actor))return "ReceivedCourtSummons";
            return "";
        }
        void ForgetCancelledPlanResume(string actor)
        {
            foreach(var speech in residentConversations.Concat(new[]{conversationPlayback}).Concat(residentIntents.Intents.SelectMany(t=>new[]{t.Speech,t.Reply}))){
                if(speech.Phase=="Idle")continue;
                if(speech.SpeakerId==actor){speech.ResumePhase="Idle";speech.ResumeActivity="Rest";speech.ResumeActivityTicks=0;}
                if(speech.RecipientId==actor){speech.ListenerResumePhase="Idle";speech.ListenerResumeActivity="Rest";speech.ListenerResumeTicks=0;}
            }
        }
        public string ConfigureTestCase(MansionCaseSettings settings)
        {
            if(settings==null||!settings.ExplicitTestSession)return "ExplicitTestSessionRequired";
            if(court.Phase!="NotStarted")return "CourtAlreadyStarted";
            return incidents.Configure(World,settings);
        }
        void InitializeProceedings()
        {
            proceedings.Rules=ChapterRules.Next(null,1,1,18,17,ImplementedChapterRules,32771);
            proceedings.CampaignId="CAMPAIGN_"+Guid.NewGuid().ToString("N");loopInitial=World.Capture();
            if(Presenter){proceedings.PresenterPosition=P(Presenter.transform.position);proceedings.PresenterNode=nodes[Layout.NearestNode(Presenter.transform.position)].Id;}
        }
        void AdvanceCase()
        {
            incidents.Step(World,Knowledge.For,this);AdvanceActionJournals();ClearInactiveIncidentSignals();
            ImportCaseReceipts();AdvanceNearbyRescues();UpdateRescueFeedback();StepPresenter();AdvanceResidentResponses();
            AdvanceReportedCases();
            long lastPublication=proceedings.PublicSchedule.Publications.LastOrDefault()?.Tick??-1;
            if(proceedings.Phase=="Announcement"&&World.Tick-lastPublication>=240)proceedings.Phase="Investigation";
            if(proceedings.Phase=="Investigation"&&World.Tick>=proceedings.ConveneAt)proceedings.Phase="Gathering";
            if(new[]{"Announcement","Investigation","Gathering"}.Contains(proceedings.Phase))AdvanceAnnouncements();
            if(proceedings.Phase=="Gathering")TryStartCourt();
        }
        bool CaseBundleConfirmed()=>incidents.All().Any(c=>c.CanConvene)&&!World.CaseBook.HasPendingOutcome
            &&incidents.All().Where(c=>c.Capture().CauseTick>=0&&!c.RiskResolved).All(c=>c.CanConvene)&&proceedings.PendingReportCases.Length==0;
        void AdvanceReportedCases()
        {
            long lastPublication=proceedings.PublicSchedule.Publications.LastOrDefault()?.Tick??-1;
            if(proceedings.InspectionCaseId==""&&(proceedings.Phase!="Announcement"||World.Tick-lastPublication>=240))StartNextReportedCase();
            var active=incidents.Find(proceedings.InspectionCaseId);if(active==null)return;
            if(Presenter&&CanReach("PRES_YUSTI",active.TargetId,2.5)){
                if(++proceedings.InspectionTicks>=180&&active.Confirm(World,true)=="Confirmed"){
                    var schedule=MansionPublicCaseSchedule.Restore(proceedings.PublicSchedule,World.Tick);
                    schedule.PublishConfirmedDeath(DeathNoticeId(active.Capture()),World.Tick);proceedings.PublicSchedule=schedule.Capture();
                    proceedings.FirstAnnouncementTick=proceedings.PublicSchedule.Publications[0].Tick;proceedings.ConveneAt=schedule.ConveneAt;
                    proceedings.PendingReportCases=proceedings.PendingReportCases.Where(id=>id!=active.CaseId).ToArray();proceedings.InspectionCaseId="";proceedings.InspectionTicks=0;
                    proceedings.Phase="Announcement";World.Emit("OfficialReportPublished","PRES_YUSTI",NoticeId,proceedings.ConveneAt.ToString());
                    foreach(var actor in World.Residents.Where(r=>r.Alive&&r.Present))DeliverOfficialNotice(actor.Id,false);
                }
            }else{
                proceedings.InspectionTicks=0;
                if(World.Tick%60==0&&proceedings.PresenterCursor>=proceedings.PresenterPath.Length)PlanReportedLocation(active);
            }
        }
        void StartNextReportedCase()
        {
            if(proceedings.InspectionCaseId!=""||proceedings.PendingReportCases.Length==0)return;
            var next=incidents.Find(proceedings.PendingReportCases[0]);if(next==null||next.CanConvene)throw new InvalidOperationException("Invalid public report queue");
            proceedings.InspectionCaseId=next.CaseId;proceedings.InspectionTicks=0;proceedings.Phase="InspectingBody";PlanReportedLocation(next);
        }
        bool PlanReportedLocation(MansionIncident reported)
        {
            // Use the received discovery location, not the body's current hidden location.
            int target=Layout.NearestNode(V(reported.ReportedPosition));return target>=0&&PlanPresenter(nodes[target].Id);
        }
        bool HearsAnnouncement(string actorId)
        {
            var actor=World.Resident(actorId);var listener=PhysicalBody(actorId);
            if(!Presenter||!listener||!MansionAnnouncementPolicy.CanReceive(actor.Alive,actor.Present,actor.Activity,RoomSilent(actorId)||RoomSilent("PRES_YUSTI"),true))return false;
            Vector3 origin=Presenter.transform.position+Vector3.up*Presenter.Height*.85f;
            Vector3 end=listener.transform.position+Vector3.up*listener.Height*.85f;var delta=end-origin;
            if(delta.magnitude>12)return false;
            int count=Physics.RaycastNonAlloc(origin,delta.normalized,rayHits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(!rayHits[i].collider.GetComponentInParent<FixtureActorBody>())return false;
            return count<rayHits.Length;
        }
        bool DeliverOfficialNotice(string actorId,bool summons)
        {
            if(!HearsAnnouncement(actorId))return false;
            var published=incidents.All().Where(c=>c.CanConvene&&proceedings.PublicSchedule.Publications.Any(p=>p.DeathId==DeathNoticeId(c.Capture()))).ToArray();
            if(published.Length==0)return false;
            foreach(var confirmed in published)confirmed.ReceiveAnnouncement(World,actorId,true);
            ImportCaseReceipts();
            var own=Knowledge.For(actorId);string predicate=summons?"CourtSummons":"CourtSchedule";
            if(MansionAnnouncementPolicy.HasReceipt(own,NoticeId,predicate))return true;
            string text=summons?"유스티: 이제 재판장으로 모여 주세요.":"유스티: "+(proceedings.PublicSchedule.Revision>1?"추가 사망을 확인했습니다. ":"사망 확인을 마쳤습니다. ")+"소집 예정은 "+WorldTimeLabel.Format(proceedings.ConveneAt,World.ClockVersion)+"입니다. 사망 시각이나 범인이 밝혀진 건 아닙니다.";
            Knowledge.Observe(actorId,MansionAnnouncementPolicy.Notice(NoticeId,predicate,text,PlaceOf(Presenter.transform.position),proceedings.PresenterPosition,World.Tick),World.Tick);
            World.Emit(summons?"CourtSummonsReceived":"OfficialNoticeReceived","PRES_YUSTI",actorId,NoticeId+"|LocalVoice");
            if(summons){
                // A personally received opening announcement may update remembered access, never remote A.
                var opened=World.Events.Where(e=>e.Type=="CourtEntranceOpened"&&e.Detail==NoticeId).Select(e=>e.Target).ToArray();
                World.Resident(actorId).KnownLocked=World.Resident(actorId).KnownLocked.Except(opened).ToArray();
            }
            if(actorId=="CH_01")message=text;
            return true;
        }
        void AdvanceAnnouncements()
        {
            if(!Presenter||World.Tick%60!=0)return;
            bool summons=proceedings.Phase=="Gathering";
            // Facility opening is an actual operation at the door, before its availability is announced.
            if(summons&&!PrepareCourtEntrance())return;
            foreach(var actor in World.Residents.Where(r=>r.Alive&&r.Present))DeliverOfficialNotice(actor.Id,summons);
            if(proceedings.Phase=="Announcement")return;
            if(summons&&World.Residents.Where(r=>r.Alive&&r.Present).All(r=>HasReceivedCourtSummons(r.Id))){
                var trial=Layout.Room("R_TRIAL");
                if(trial&&trial.WalkNode>=0&&!PresenterReadyForCourt()&&(proceedings.PresenterDestination!=nodes[trial.WalkNode].Id||proceedings.PresenterCursor>=proceedings.PresenterPath.Length))PlanPresenter(nodes[trial.WalkNode].Id);
                return;
            }
            if(proceedings.PresenterCursor<proceedings.PresenterPath.Length)return;
            // Fixed public search itinerary uses authored navigation, not the hidden positions of missing people.
            // Public corridor nodes include bedroom thresholds; private rooms are never searched without permission.
            var thresholds=Layout.Connections.Where(c=>!MansionAnnouncementPolicy.IsPublicSearchRoom(c.RoomB)&&c.RoomA=="R_BED_COR"&&c.EntryNodeA>=0).Select(c=>nodes[c.EntryNodeA].Id).ToArray();
            var stops=nodes.Where(n=>MansionAnnouncementPolicy.IsPublicSearchRoom(n.Room))
                .Where(n=>Layout.Room(n.Room)!=null&&Layout.Room(n.Room).WalkNode>=0&&(nodes[Layout.Room(n.Room).WalkNode].Id==n.Id||thresholds.Contains(n.Id)))
                .OrderBy(n=>n.Room=="R_ANNOUNCE"?0:1).ThenBy(n=>n.Room,StringComparer.Ordinal).ThenBy(n=>n.Id,StringComparer.Ordinal).Select(n=>n.Id).Distinct().ToArray();
            var previous=World.Events.LastOrDefault(e=>e.Type=="AnnouncementSearchDestination"&&e.Detail==NoticeId);
            string next=MansionAnnouncementPolicy.NextStop(stops,previous?.Target);
            if(next=="")return;
            World.Emit("AnnouncementSearchDestination","PRES_YUSTI",next,NoticeId);
            if(!PlanPresenter(next))World.Emit("AnnouncementSearchBlocked","PRES_YUSTI",next,NoticeId);
        }
        bool PrepareCourtEntrance()
        {
            var entrance=Layout.Connections.FirstOrDefault(c=>c.InitialLock=="FacilityControlled"&&(c.RoomA=="R_TRIAL"||c.RoomB=="R_TRIAL"));
            if(!entrance)return true;
            if(World.Events.Any(e=>e.Type=="CourtEntranceOpened"&&e.Target==entrance.DoorId&&e.Detail==NoticeId))return true;
            if(!CanReach("PRES_YUSTI",entrance.DoorId,3)){
                int approach=Layout.NearestNode(entrance.transform.position,entrance.RoomA=="R_TRIAL"?entrance.RoomB:entrance.RoomA);
                if(approach>=0&&(proceedings.PresenterDestination!=nodes[approach].Id||proceedings.PresenterCursor>=proceedings.PresenterPath.Length))PlanPresenter(nodes[approach].Id);
                return false;
            }
            var door=World.Door(entrance.DoorId);
            if(door.Locked)World.UseDoor(door.Id,"PRES_YUSTI",true);
            if(door.Locked)return false;
            World.UseDoor(door.Id,"PRES_YUSTI");
            proceedings.PresenterKnownLocked=proceedings.PresenterKnownLocked.Where(id=>id!=door.Id).ToArray();
            World.Emit("CourtEntranceOpened","PRES_YUSTI",door.Id,NoticeId);return true;
        }
        void ImportCaseReceipts()
        {
            foreach(var resident in World.Residents.Where(r=>r.Alive&&r.Present))foreach(var scoped in incidents.ReceiptsFor(resident.Id)){
                if(proceedings.ImportedReceipts.Contains(scoped.ImportKey))continue;
                var record=scoped.Receipt.Record;record.PlaceId=PlaceOf(V(record.Position));
                if(record.Kind=="OfficialReport"){record.Source="PRES_YUSTI";record.ProvenanceKey="OFFICIAL_DEATH_L"+World.Loop+"_C"+World.Chapter+"_"+scoped.CaseId+"_"+record.SubjectId+"_"+record.FromTick;}
                Knowledge.Observe(resident.Id,record,World.Tick);proceedings.ImportedReceipts=proceedings.ImportedReceipts.Concat(new[]{scoped.ImportKey}).ToArray();
            }
        }
        string SpeakToPresenter()
        {
            if(!World.Resident("CH_01").Alive||!World.Resident("CH_01").Present)return "관찰 중에는 새 보고를 받을 수 없습니다.";
            if(!Presenter||!CanReach("CH_01","PRES_YUSTI",3))return "가까이에서 유스티에게 말하세요.";
            ReceiveChapterRules("CH_01");
            int received=0;
            foreach(var known in incidents.ReadFor("CH_01").Where(k=>k.View.Discovered&&!k.View.Confirmed)){
                var reported=incidents.Find(known.Id);if(reported.CanConvene||reported.Report(World,"CH_01",true)!="Reported")continue;
                if(!proceedings.PendingReportCases.Contains(known.Id)){proceedings.PendingReportCases=proceedings.PendingReportCases.Concat(new[]{known.Id}).ToArray();received++;}
            }
            bool notice=DeliverOfficialNotice("CH_01",proceedings.Phase=="Gathering"&&World.Events.Any(e=>e.Type=="CourtEntranceOpened"&&e.Detail==NoticeId));
            if(received>0){StartNextReportedCase();return received==1?"신고 접수했습니다. 현장에서 확인하겠습니다.":"신고 접수했습니다. 차례로 현장을 확인하겠습니다.";}
            if(proceedings.PendingReportCases.Length>0)return "신고는 접수됐습니다. 현장 확인을 진행하고 있습니다.";
            if(notice)return "공식 보고와 소집 안내를 받았습니다.";
            PresenterGreeting();return "Dialogue";
        }
        bool PlanPresenter(string destination)
        {
            var route=World.FindPath(proceedings.PresenterNode,destination,proceedings.PresenterKnownLocked);if(route.Length==0)return false;
            proceedings.PresenterPath=route;proceedings.PresenterCursor=0;proceedings.PresenterDestination=destination;proceedings.PresenterQueueDoor="";return true;
        }
        void StepPresenter()
        {
            if(!Presenter||proceedings.PresenterCursor>=proceedings.PresenterPath.Length)return;
            // Yield outside the physical exit route while another body holds the shared passage.
            // Standing on the endpoint until a grant arrives can prevent that holder from ever leaving.
            if(World.UpcomingPassage(proceedings.PresenterPath,proceedings.PresenterCursor,proceedings.PresenterPosition,out var passage,out var approach,out var exit)
                &&(proceedings.PresenterPosition.Distance(approach)<2.6||proceedings.PresenterQueueDoor==passage.Id)
                &&passage.Holder!="PRES_YUSTI"&&(passage.Holder!=""||passage.Queue.Length>0)){
                if(proceedings.PresenterPosition.Distance(approach)<1.1&&!passage.Queue.Contains("PRES_YUSTI"))World.UseDoor(passage.Id,"PRES_YUSTI");
                proceedings.PresenterPosition=WaitForPassage("PRES_YUSTI",passage.Id,approach,exit);return;
            }
            proceedings.PresenterQueueDoor="";
            string targetId=proceedings.PresenterPath[proceedings.PresenterCursor];
            if(proceedings.PresenterCursor>0){var edge=edges.FirstOrDefault(e=>e.From==proceedings.PresenterPath[proceedings.PresenterCursor-1]&&e.To==targetId);
                if(edge!=null&&edge.Door!=""){
                    var door=World.Door(edge.Door);
                    // Inspection and egress from that inspected private room share the reported access grant.
                    // Later public search rounds cannot use it to enter other people's closed bedrooms.
                    string fromRoom=nodes.First(n=>n.Id==edge.From).Room;
                    bool inspectedRoomExit=!MansionAnnouncementPolicy.IsPublicSearchRoom(fromRoom)&&incidents.All().Any(c=>c.CanConvene&&PlaceOf(V(c.Capture().DiscoveryPosition))==fromRoom);
                    var inspecting=incidents.Find(proceedings.InspectionCaseId);string reportedRoom=inspecting==null?"":PlaceOf(V(inspecting.ReportedPosition));
                    bool reportedAccess=reportedRoom!=""&&(fromRoom==reportedRoom||nodes.First(n=>n.Id==edge.To).Room==reportedRoom);
                    bool emergency=door.EmergencyOpen&&(reportedAccess||inspectedRoomExit);
                    if(emergency)World.UseDoor(door.Id,"PRES_YUSTI",false,true);
                    if(door.Locked){proceedings.PresenterKnownLocked=proceedings.PresenterKnownLocked.Concat(new[]{door.Id}).Distinct().ToArray();proceedings.PresenterPath=Array.Empty<string>();proceedings.PresenterCursor=0;return;}
                    if(door.Holder!="PRES_YUSTI"||door.Open<.999){
                        string result=World.UseDoor(door.Id,"PRES_YUSTI",false,emergency);
                        if(result=="소유자의 허락이 필요합니다."){proceedings.PresenterKnownLocked=proceedings.PresenterKnownLocked.Concat(new[]{door.Id}).Distinct().ToArray();proceedings.PresenterPath=Array.Empty<string>();proceedings.PresenterCursor=0;}
                        return;
                    }
                }
            }
            var target=nodes.First(n=>n.Id==targetId).Position;var delta=target.Minus(proceedings.PresenterPosition);double distance=delta.Distance(default);
            if(distance<.14){proceedings.PresenterNode=targetId;proceedings.PresenterCursor++;return;}
            // Stair treads can briefly halt horizontal progress while the capsule settles downward.
            // Flat-floor side steering oscillates here; keep the verified stair segment and real collision.
            bool stairSegment=proceedings.PresenterCursor>0&&Math.Abs(World.NavigationPoint(proceedings.PresenterPath[proceedings.PresenterCursor-1]).Y-target.Y)>.05;
            proceedings.PresenterPosition=MoveActor("PRES_YUSTI",delta.Scale(Math.Min(1,1.4/60/distance)),!stairSegment);
            Vector3 forward=V(delta);forward.y=0;if(forward.sqrMagnitude>.001f)Presenter.transform.rotation=Quaternion.LookRotation(forward);
        }
        string DiscoverBody(string id)
        {
            string result=incidents.Discover(World,"CH_01",id,this);ImportCaseReceipts();
            return result=="Discovered"?"쓰러진 인물을 확인했습니다. 유스티에게 직접 신고할 수 있습니다.":"확인할 수 없습니다.";
        }
    }
}
