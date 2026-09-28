using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
using BASSLINE.World.Mansion;
using BASSLINE.Knowledge;
using BASSLINE.NPC;
using BASSLINE.Investigation;

namespace BASSLINE.Bootstrap
{
    [Serializable] public sealed class ResidentRoutine
    {
        public string ActorId;public string[] Destinations=Array.Empty<string>();public string[] Activities=Array.Empty<string>();
        public string SurfaceToolId="",ReadingItemId="";
        public int DwellTicks=1800;
    }
    public sealed partial class MansionRuntime:MonoBehaviour,IPlayerLifePort,IPlayerMotionPort,IPlayerNotebookPort,IPlayerPlacePort,ISessionPresencePort,IMansionPhysics,IMansionPassagePhysics,IMansionWaypointPhysics,IMansionTrafficPhysics
    {
        public MansionLayout Layout;
        public FixtureActorBody[] Bodies=Array.Empty<FixtureActorBody>();
        public FixtureObjectBody[] ObjectBodies=Array.Empty<FixtureObjectBody>();
        public ResidentRoutine[] Routines=Array.Empty<ResidentRoutine>();
        public bool AutomaticTick=true;
        string GeometryRevision=>Layout.SourceHash+"|"+Layout.BuildVersion;
        public MansionWorld World{get;private set;}
        public KnowledgeLedger Knowledge{get;private set;}
        public SocialLedger Social{get;private set;}
        public InvestigationNotebook Notebook{get;private set;}
        Dictionary<string,FixtureActorBody> bodies;
        Dictionary<string,MansionConnection> doors;
        Dictionary<string,FixtureTarget> targets;
        MansionNode[] nodes;MansionEdge[] edges;
        Point3 move;bool running;string message="";double accumulator;
        Camera cameraView;SimulationMode previousSimulation;
        readonly Collider[] overlap=new Collider[64];readonly RaycastHit[] rayHits=new RaycastHit[64];
        public static Point3 P(Vector3 p)=>new Point3(p.x,p.y,p.z);
        public static Vector3 V(Point3 p)=>new Vector3((float)p.X,(float)p.Y,(float)p.Z);
        void OnEnable(){previousSimulation=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;}
        void OnDisable(){Physics.simulationMode=previousSimulation;}
        void Awake()
        {
            RetireSurveillanceStations();
            bodies=Bodies.ToDictionary(b=>b.ActorId);doors=Layout.Connections.Where(c=>!string.IsNullOrEmpty(c.DoorId)).ToDictionary(c=>c.DoorId);
            targets=FindObjectsByType<FixtureTarget>().ToDictionary(t=>t.StableId);
            InitializeLoanOffers();
            InitializeEverydayRequests();
            readingBooks=ObjectBodies.Select(o=>o.GetComponent<MansionReadableBook>()).Where(b=>b).ToArray();
            cameraView=bodies["CH_01"].GetComponentInChildren<Camera>();
            foreach(var eye in FindObjectsByType<MansionDivineEye>(FindObjectsInactive.Include,FindObjectsSortMode.None))eye.BindPlayerView(cameraView,bodies["CH_01"].transform);
            returnStation=FindAnyObjectByType<CommonReturnStation>();
            nodes=Layout.NavigationNodes.Select(n=>new MansionNode{Id=n.Id,Room=n.RoomId,Position=P(n.Position)}).ToArray();
            edges=Layout.NavigationEdges.Select(e=>new MansionEdge{From=nodes[e.From].Id,To=nodes[e.To].Id,Door=Layout.Connection(e.ConnectionId)?.DoorId??""}).ToArray();
            var initial=new MansionState{ClockVersion=WorldTimeLabel.Morning,MapVersion=Layout.MapVersion,CatalogHash=GeometryRevision,
                Residents=Bodies.Select(b=>new ResidentState{Id=b.ActorId,Position=P(b.transform.position),Node=nodes[Layout.NearestNode(b.transform.position)].Id}).ToArray(),
                Doors=doors.Values.Select(d=>new MansionDoorState{Id=d.DoorId,Locked=d.InitialLock!="Unlocked",OwnerId=d.InitialLock=="OwnerPermission"?"CH_"+d.RoomB.Substring(d.RoomB.Length-2):"",RelockAfterUse=d.InitialLock=="OwnerPermission",EmergencyOpen=d.EmergencyOpen,Open=d.InitialOpen=="Open"?1:0}).ToArray(),
                Objects=ObjectBodies.Select(o=>new MansionObjectState{Id=o.ObjectId,Name=targets[o.ObjectId].PublicName,Position=P(o.transform.position),PhysicsVersion=o.GetComponent<MansionWeapon>()?1:0,Rotation=P(o.transform.eulerAngles),Location=string.IsNullOrEmpty(o.InitialOwner)?"World":"Hand",Owner=o.InitialOwner??""}).ToArray()};
            foreach(var item in initial.Objects.Where(o=>o.Location=="Hand")){var holder=initial.Residents.Single(r=>r.Id==item.Owner);if(holder.HeldObject!="")throw new InvalidOperationException("Initial hand contains multiple objects");holder.HeldObject=item.Id;item.Position=P(bodies[item.Owner].RightHand.position);}
            World=new MansionWorld(initial,nodes,edges);Knowledge=new KnowledgeLedger(bodies.Keys,"LOOP_01");Social=new SocialLedger(bodies.Keys,Layout.Rooms.Select(r=>r.RoomId));Notebook=new InvestigationNotebook();InitializeProceedings();InitializeActionJournals();InitializeResidentPurposes();InitializeFamilyDispute();InitializeIncidentSites();InitializeSurfaceTraces();weaponResidues=new WeaponResidues(World.Loop,World.Tick);doorObservations.EventCursor=World.Capture().Sequence;
            foreach(var b in Bodies){b.Capsule.stepOffset=.32f;b.Capsule.skinWidth=.025f;b.Capsule.minMoveDistance=0;}
            InitializeLooseObjects();Physics.SyncTransforms();Present();PrepareLooseObjects(true);
        }
        void Update()
        {
            if(!AutomaticTick||World==null||World.Paused&&!World.HasPause("M_COURT"))return;
            if(!World.Paused&&conversationPlayback.Phase=="Speaking"&&conversationPlayback.FastForward){AdvanceConversationFastBatch();return;}
            if(waiting.Active){AdvanceWaitingBatch();return;}
            accumulator+=Time.unscaledDeltaTime;int ticks=0;
            while(accumulator>=1d/60&&ticks++<12){accumulator-=1d/60;AdvanceOne();}
            World.PendingTime=accumulator;
        }
        public void AdvanceOne()
        {
            if(World.HasPause("M_COURT")){AdvanceCourt();return;}if(World.Paused)return;
            if(World.Tick%30==0)PlanResidents();
            PrepareLooseObjects();Physics.SyncTransforms();World.Step(move,running,this);PrepareLooseObjects();Physics.SyncTransforms();Physics.Simulate(1f/60);CommitLooseObjects();
            AdvanceWeapon();AdvanceWeaponRinse();AdvanceToolPress();AdvanceItemExchange();AdvanceCommonReturn();AdvanceAppointmentCard();ProcessKnowledge();AdvanceCase();AdvanceConversation();AdvanceResidentConversations();AdvanceFamilyDispute();AdvanceResidentIntents();AdvanceEverydayRequests();TryCompleteLoanLearning();CheckWaiting();AdvanceResidentToolPickups();AdvanceResidentToolWork();AdvanceResidentToolReturns();AdvanceResidentReadings();Present();BeginResidentToolWork();BeginResidentToolReturns();BeginResidentReadings();AdvancePurposeRuleReading();AdvanceSurfaceTraces();AdvancePresenceObservations();AdvanceDoorObservations();AdvanceDoorPassages();AdvanceInspection();
        }
        void PlanResidents()
        {
            PlanResidentMeetings();
            foreach(var routine in Routines){var actor=World.Resident(routine.ActorId);if(!World.CanAct(actor.Id)||RescueControls(actor.Id)||incidents.Controls(actor.Id))continue;
                if(proceedings.Phase=="Gathering"&&HasReceivedCourtSummons(actor.Id)) {string seat=SeatNode(actor.Id);if(seat!=""&&!AtCourtSeat(actor.Id))World.Plan(actor.Id,seat,"WaitForCourt",int.MaxValue);continue;}
                if(FamilyControls(actor.Id)||ReturnTaskControls(actor.Id)||ResponseControls(actor.Id)||IntentControls(actor.Id))continue;
                if(actor.Phase!="Idle")continue;
                var appt=Social.Due(actor.Id,World.Tick);
                if(actor.Id==Collector&&appt==null&&TryPlanReturnVisit())continue;
                string node;string action;int duration;
                if(appt!=null){PlanMeetingVisit(actor,appt);continue;}
                else{if(TryPlanResidentReading(routine,actor))continue;if(routine.Destinations.Length==0)continue;int i=actor.ScheduleCursor%routine.Destinations.Length;node=routine.Destinations[i];action=routine.Activities.Length>i?routine.Activities[i]:"Rest";duration=routine.DwellTicks;}
                if(World.Plan(actor.Id,node,action,duration)!="Accepted"&&appt==null)actor.ScheduleCursor++;
            }
        }
        public void SetMove(double x,double z){if(Math.Abs(x)+Math.Abs(z)>.001){CancelWeaponRinse();CancelWeapon();StopWaiting("움직여서 기다리기를 멈췄어요.");CancelPlayerRescue("MovementRequested");CancelItemExchange();CancelAppointmentCard();CancelToolPress();CancelInspection();}move=new Point3(x,0,z);}
        public void SetRun(bool value){running=value&&!World.Paused;}
        public void SetLook(double yaw,double pitch){if(World.Paused||!new Point3(yaw,pitch,0).Finite())return;if(Math.Abs(yaw-World.Yaw)+Math.Abs(pitch-World.Pitch)>.01)StopWaiting("주변을 살펴보려고 멈췄어요.");World.Yaw=yaw%360;World.Pitch=pitch;ApplyLook();}
        void ApplyLook(){var h=itemExchange.Handoff;double facing=weaponRinse.Running?weaponRinse.Yaw:weaponMotion.Running?weaponMotion.Yaw:toolPress.Running?toolPress.BodyYaw:h.Running&&h.MotionVersion==1?h.PlayerFacing:World.Yaw;bodies["CH_01"].transform.rotation=Quaternion.Euler(0,(float)facing,0);cameraView.transform.localRotation=Quaternion.Euler((float)World.Pitch,(float)(World.Yaw-facing),0);}
        public PlayerLifeView ReadPlayer()=>new PlayerLifeView{Tick=World.Tick,ClockVersion=World.ClockVersion,Yaw=World.Yaw,Pitch=World.Pitch,Paused=World.Paused,NotePause=World.HasPause("K_NOTE"),SettingsPause=World.HasPause("K_SETTINGS"),HeldItem=HeldName(),Activity=World.Resident("CH_01").Activity,Message=RescueFeedback()};
        string HeldName(){string id=World.Resident("CH_01").HeldObject;return id==""?"없음":World.Object(id).Name;}
        public void Pause(string id,bool acquire){if(acquire)StopWaiting("잠깐 멈췄어요.");World.Pause(id,acquire);move=default;running=false;}
        public Point3 Move(string actor,Point3 motion)
            =>MoveActor(actor,motion,true);
        Point3 MoveActor(string actor,Point3 motion,bool allowAvoidance)
        {
            var body=PhysicalBody(actor);var before=body.transform.position;var step=V(motion);
            ResidentState resident=actor!="CH_01"&&actor!="PRES_YUSTI"?World.Resident(actor):null;
            // Flat-floor detours can leave a stair flight while its distant landing is still the target.
            // Keep residents on the same physical segment as the presenter, including after loading.
            if(resident!=null&&resident.QueueDoor==""&&resident.PathCursor>0&&resident.PathCursor<resident.Path.Length
                &&Math.Abs(World.NavigationPoint(resident.Path[resident.PathCursor-1]).Y-World.NavigationPoint(resident.Path[resident.PathCursor]).Y)>.05){
                allowAvoidance=false;resident.Recovering=false;resident.RecoveryTicks=0;
            }
            Point3 intended=resident!=null&&resident.QueueDoor!=""?resident.QueuePosition:resident!=null&&resident.PathCursor<resident.Path.Length?World.NavigationPoint(resident.Path[resident.PathCursor]):P(before+step);
            if(resident!=null&&resident.Recovering){
                var recovery=V(resident.RecoveryPoint)-before;recovery.y=0;
                if(resident.RecoveryTarget.Distance(intended)>.1||recovery.magnitude<.08f||resident.RecoveryTicks>=90){resident.Recovering=false;resident.RecoveryTicks=0;}
                else{step=Vector3.ClampMagnitude(recovery,1.4f/60);resident.RecoveryTicks++;}
            }
            body.Capsule.Move(step+Vector3.down*(2f/60));
            // Local steering uses the same capsule collision as the player; it never changes rooms by assignment.
            if(allowAvoidance&&actor!="CH_01"&&(resident==null||!resident.Recovering)&&step.sqrMagnitude>.00001f&&Vector3.Dot(body.transform.position-before,step.normalized)<step.magnitude*.25f){
                var forward=step;forward.y=0;forward.Normalize();
                // Keep the same passing convention in both travel directions. Actor parity sent opposing
                // residents to the same side and could also push a solitary actor onto a stair-rail corner.
                foreach(float angle in new[]{45f,-45f,90f,-90f,135f,-135f}){
                    var direction=Quaternion.Euler(0,angle,0)*forward;
                    if(!SteeringStepClear(body,direction,.65f))continue;
                    // Preserve the small detour until it is actually walked. Retrying the original
                    // blocked direction every frame can cancel a necessary retreat around a leaf.
                    if(resident!=null){resident.Recovering=true;resident.RecoveryPoint=P(body.transform.position+direction*.65f);resident.RecoveryTarget=intended;resident.RecoveryTicks=0;}
                    body.Capsule.Move(direction*(1.4f/60)+Vector3.down*(2f/60));break;
                }
            }
            return P(body.transform.position);
        }
        bool SteeringStepClear(FixtureActorBody body,Vector3 direction,float distance)
        {
            var start=body.transform.position;
            int count=Physics.CapsuleCastNonAlloc(start+Vector3.up*.32f,start+Vector3.up*(body.Height-.3f),.275f,direction,rayHits,distance,~0,QueryTriggerInteraction.Ignore);
            if(count==rayHits.Length)return false;
            for(int i=0;i<count;i++){
                var hit=rayHits[i];if(hit.collider.transform.IsChildOf(body.transform))continue;
                // A contact behind or parallel to the proposed direction must not prevent backing away.
                if(hit.distance<.02f&&Vector3.Dot(direction,hit.normal)>=-.01f)continue;
                if(!hit.collider.GetComponentInParent<FixtureActorBody>()&&hit.collider.bounds.max.y<=start.y+body.Capsule.stepOffset+.01f)continue;
                return false;
            }
            for(float along=.15f;along<=distance+.1f;along+=.15f){
                var point=start+direction*Mathf.Min(along,distance);bool floor=false;
                count=Physics.RaycastNonAlloc(point+Vector3.up*.38f,Vector3.down,rayHits,.75f,~0,QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++)if(!rayHits[i].collider.GetComponentInParent<FixtureActorBody>()&&rayHits[i].normal.y>.7f&&Math.Abs(rayHits[i].point.y-start.y)<=body.Capsule.stepOffset+.025f){floor=true;break;}
                if(!floor)return false;
            }
            return true;
        }
        public bool DoorClear(string id)
        {
            var door=doors[id];int count=Physics.OverlapBoxNonAlloc(door.transform.position+Vector3.up,.5f*new Vector3(door.Width+1,2,2),overlap,door.transform.rotation,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(overlap[i].GetComponentInParent<FixtureActorBody>())return false;return true;
        }
        public Point3 WaitForPassage(string actor,string id,Point3 approach,Point3 exit)
        {
            var resident=actor=="PRES_YUSTI"?null:World.Resident(actor);var body=PhysicalBody(actor);var start=body.transform.position;
            var entry=V(approach);var forward=(V(exit)-entry).normalized;forward.y=0;
            var side=Vector3.Cross(Vector3.up,forward);int preferred=actor=="PRES_YUSTI"?1:int.Parse(actor.Substring(3))%2==0?1:-1;
            Vector3 goal=V(resident!=null?resident.QueuePosition:proceedings.PresenterQueuePosition);
            string queuedDoor=resident!=null?resident.QueueDoor:proceedings.PresenterQueueDoor;
            bool retained=queuedDoor==id&&QueuePlaceClear(actor,goal)&&QueueWalkClear(body,start,goal);
            if(!retained){
                bool found=false;float best=float.MaxValue;
                // Reserve an actual clear floor position beside the entrance. Leaving the central exit lane
                // free is necessary even for the first opposing FIFO waiter; collision alone cannot yield.
                for(int row=0;row<5;row++)for(int lane=0;lane<4;lane++){
                    float lateral=(.8f+.7f*(lane/2))*((lane%2==0)?preferred:-preferred);
                    var candidate=entry-forward*(.45f+row*.7f)+side*lateral;
                    if(!QueuePlaceClear(actor,candidate)||!QueueWalkClear(body,start,candidate))continue;
                    float score=(candidate-start).sqrMagnitude+row*.12f+lane*.015f;
                    if(score>=best)continue;best=score;goal=candidate;found=true;
                }
                if(!found){
                    // A narrow alcove may require backing away before a side position is directly visible.
                    var retreat=start-forward*.6f;retreat.y=entry.y;
                    if(QueuePlaceClear(actor,retreat)&&QueueWalkClear(body,start,retreat))goal=retreat;
                    else return Move(actor,default);
                }
                if(resident!=null){resident.QueueDoor=id;resident.QueuePosition=P(goal);}
                else{proceedings.PresenterQueueDoor=id;proceedings.PresenterQueuePosition=P(goal);}
            }
            var delta=goal-start;delta.y=0;
            return Move(actor,P(Vector3.ClampMagnitude(delta,1.4f/60)));
        }
        bool QueuePlaceClear(string actor,Vector3 point)
        {
            var body=PhysicalBody(actor);
            int count=Physics.OverlapCapsuleNonAlloc(point+Vector3.up*.32f,point+Vector3.up*(body.Height-.3f),.3f,overlap,~0,QueryTriggerInteraction.Ignore);
            if(count==overlap.Length)return false;
            for(int i=0;i<count;i++)if(!overlap[i].transform.IsChildOf(body.transform))return false;
            foreach(var other in World.Residents){
                if(other.Id==actor||!other.Alive||!other.Present)continue;
                if(other.Position.Distance(P(point))<.72)return false;
                if(other.QueueDoor!=""&&other.QueuePosition.Distance(P(point))<.68)return false;
                // A doorway's exit route may immediately turn along the corridor. A waiting place
                // beside the door axis can still block that route, so keep nearby departures clear.
                if(other.Phase!="Travelling"||other.QueueDoor!=""||other.Position.Distance(P(point))>4.5)continue;
                var prior=V(other.Position);float walked=0;
                for(int index=other.PathCursor;index<other.Path.Length&&walked<4;index++){
                    var next=V(World.NavigationPoint(other.Path[index]));var segment=next-prior;
                    if(Math.Abs(prior.y-point.y)<.2f&&Math.Abs(next.y-point.y)<.2f){
                        float t=segment.sqrMagnitude<.0001f?0:Mathf.Clamp01(Vector3.Dot(point-prior,segment)/segment.sqrMagnitude);
                        if(Vector3.Distance(point,prior+segment*t)<.8f)return false;
                    }
                    walked+=segment.magnitude;prior=next;
                }
            }
            return QueueFloorClear(point);
        }
        bool QueueWalkClear(FixtureActorBody body,Vector3 start,Vector3 end)
        {
            var delta=end-start;delta.y=0;float distance=delta.magnitude;
            if(Math.Abs(start.y-end.y)>.15f||distance>4.5f)return false;
            int count=Physics.CapsuleCastNonAlloc(start+Vector3.up*.32f,start+Vector3.up*(body.Height-.3f),.3f,delta.normalized,rayHits,distance,~0,QueryTriggerInteraction.Ignore);
            if(count==rayHits.Length)return false;
            for(int i=0;i<count;i++)if(!rayHits[i].collider.GetComponentInParent<FixtureActorBody>())return false;
            for(int i=0;i<=Mathf.CeilToInt(distance/.25f);i++)if(!QueueFloorClear(Vector3.Lerp(start,end,distance<.001f?0:Mathf.Min(1,i*.25f/distance))))return false;
            return true;
        }
        bool QueueFloorClear(Vector3 point)
        {
            int count=Physics.RaycastNonAlloc(point+Vector3.up*.15f,Vector3.down,rayHits,.28f,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(!rayHits[i].collider.GetComponentInParent<FixtureActorBody>()&&rayHits[i].normal.y>.95f&&Math.Abs(rayHits[i].point.y-point.y)<.12f)return true;
            return false;
        }
        public void DoorPose(string id,double open)=>doors[id].ApplyOpenAmount((float)open);
        public bool ActorClearedDoor(string id,string actor)
        {
            var body=PhysicalBody(actor);if(!body||!body.gameObject.activeInHierarchy)return true;
            var path=actor=="PRES_YUSTI"?proceedings.PresenterPath:World.Resident(actor).Path;int cursor=actor=="PRES_YUSTI"?proceedings.PresenterCursor:World.Resident(actor).PathCursor;
            if(cursor>0&&cursor<path.Length&&edges.Any(e=>e.From==path[cursor-1]&&e.To==path[cursor]&&e.Door==id))return false;
            var door=doors[id];var point=body.transform.position;
            // Reserve the real route plus two-body clearance, including its endpoints. An axis-aligned
            // rectangle wrongly retains a resident sitting safely beyond a route corner for a whole activity.
            // DoorClear independently prevents a leaf from closing onto anyone beside the route.
            if(door.Route==null||door.Route.Length==0)return false;
            if(door.Route.Length==1)return Vector3.Distance(point,door.Route[0])>.75f;
            for(int i=1;i<door.Route.Length;i++){
                var start=door.Route[i-1];var segment=door.Route[i]-start;
                float t=segment.sqrMagnitude<.0001f?0:Mathf.Clamp01(Vector3.Dot(point-start,segment)/segment.sqrMagnitude);
                if(Vector3.Distance(point,start+segment*t)<=.75f)return false;
            }
            return true;
        }
        public bool CanBypassIntermediate(string actor,Point3 nextPoint)
        {
            var body=PhysicalBody(actor);Vector3 start=body.transform.position,direction=V(nextPoint)-start;direction.y=0;
            if(direction.magnitude>2.5f)return false;
            int count=Physics.CapsuleCastNonAlloc(start+Vector3.up*.32f,start+Vector3.up*(body.Height-.3f),.275f,direction.normalized,rayHits,direction.magnitude,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(!rayHits[i].collider.GetComponentInParent<FixtureActorBody>())return false;
            return count<rayHits.Length;
        }
        bool Visible(string observer,Vector3 point,float range=12,bool cone=true)
        {
            var body=PhysicalBody(observer);if(!body)return false;var origin=body.transform.position+Vector3.up*body.Height*.88f;var delta=point-origin;
            if(delta.magnitude>range)return false;var forward=observer=="CH_01"?cameraView.transform.forward:body.Head.forward;
            if(cone&&Vector3.Dot(forward,delta.normalized)<.45f)return false;
            int count=Physics.RaycastNonAlloc(origin,delta.normalized,rayHits,Mathf.Max(0,delta.magnitude-.08f),~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++){var hit=rayHits[i].collider;if(hit.transform.IsChildOf(body.transform)||hit.bounds.Contains(point))continue;return false;}return count<rayHits.Length;
        }
        bool Reach(string id,float distance=2.4f)
        {
            if(!targets.TryGetValue(id,out var target)||!target.gameObject.activeInHierarchy)return false;
            Vector3 point=target.transform.position;if(bodies.TryGetValue(id,out var body))point+=Vector3.up*body.Height*.6f;
            return Vector3.Distance(bodies["CH_01"].transform.position,point)<distance&&Visible("CH_01",point,distance+.5f,false);
        }
        public string Interact(string id)
        {
            if(World.Paused)return message="WorldPaused";
            if(!World.CanAct("CH_01"))return message="지금은 몸을 움직일 수 없습니다.";
            if(!incidents.All().Any(c=>c.RescuerId=="CH_01"&&c.TargetId==id))CancelPlayerRescue("OtherInteraction");
            if(itemExchange.Handoff.Running)CancelItemExchange();
            if(WritingCard)CancelAppointmentCard();
            StopWaiting("다른 행동을 하려고 멈췄어요.");
            if(toolPress.Running&&id==toolPress.SurfaceId)return message="표면에 도구를 대고 있어요.";
            CancelToolPress();
            
            if(weaponRinse.Running&&id==weaponRinse.StationId)return message="물건을 씻고 있어요. 움직이면 멈춥니다.";
            CancelWeaponRinse();CancelWeapon();
            if(id=="DROP")return message=World.Drop("CH_01");
            bool locking=id.StartsWith("LOCK:",StringComparison.Ordinal);string key=locking?id.Substring(5):id;
            if(!Reach(key))return message="가까이에서 대상을 확인하세요.";
            if(WashStation(key))return message=BeginWeaponRinse(key);
            if(PigmentSurface(key)){if(HeldPigmentTool())return message=BeginToolPress(key);return message="Inspect";}
            if(key==AppointmentDesk.CardId)return message="Inspect";
            if(returnStation&&SurfaceAction(key)!="")return message=InteractReturnStation(key);
            if(key==LoanPen&&World.Object(LoanPen).Location=="Container")return message=BeginSurfaceTransfer("CH_01","Retrieve");
            if(key==LoanPen&&World.Object(LoanPen).Location=="World"&&World.Object(LoanPen).AnchorId==CommonReturnStation.BagFrontId)return message=BeginSurfaceTransfer("CH_01","TakePrivate");
            if(doors.ContainsKey(key))return message=World.UseDoor(key,"CH_01",locking);
            if(key=="PRES_YUSTI")return message=SpeakToPresenter();
            if(bodies.ContainsKey(key))return message=World.PhysicalBand(key)=="Critical"?HelpResident(key):World.Resident(key).Alive?Talk(key):DiscoverBody(key);
            if(ObjectBodies.Any(o=>o.ObjectId==key))return message=World.Pickup(key,"CH_01");
            if(Layout.Anchor(key)!=null)return message="Inspect";
            return message="Unavailable";
        }
        void Present()
        {
            ApplyLook();
            foreach(var b in Bodies){var a=World.Resident(b.ActorId);if(b.ActorId!="CH_01"){
                b.gameObject.SetActive(a.Present);if(!a.Present)continue;
                b.transform.rotation=Quaternion.Euler(0,(float)a.Yaw,0);b.Head.rotation=b.transform.rotation;
                var visual=b.transform.Find("CharacterProxy");if(visual){visual.localRotation=!a.Alive?Quaternion.Euler(80,0,0):World.PhysicalBand(a.Id)=="Critical"?Quaternion.Euler(45,0,0):RescueControls(a.Id)?Quaternion.Euler(20,0,0):Quaternion.identity;float stride=a.Phase=="Travelling"&&World.CanAct(a.Id)?Mathf.Sin(World.Tick*.15f)*18:0;foreach(var leg in new[]{"LegLeft","LegRight"}){var joint=visual.Find(leg);if(joint)joint.localRotation=Quaternion.Euler(leg=="LegLeft"?stride:-stride,0,0);}}
            }}
            PresentArms();
            if(returnStation)returnStation.DrawerVisual.localPosition=ReturnState.DrawerOpen?returnStation.OpenDrawerPosition:returnStation.ClosedDrawerPosition;
            foreach(var o in ObjectBodies){var item=World.Object(o.ObjectId);var press=PressForTool(o.ObjectId);var pickup=PickupForTool(o.ObjectId);var returning=ReturnForTool(o.ObjectId);var returned=ReturnedToolPose(o.ObjectId);var reading=ReadingForItem(o.ObjectId);bool visible=item.Location!="Container"||o.ObjectId==LoanPen&&ReturnState.DrawerOpen;o.gameObject.SetActive(visible);o.Collider.enabled=item.Location=="World"||item.Location=="Container"&&visible;o.transform.position=reading!=null?V(reading.Position):returning!=null?V(returning.ItemPosition):pickup!=null?V(pickup.ItemPosition):press!=null?V(press.Position):SurfaceRunning&&o.ObjectId==LoanPen?V(ReturnState.Motion.Position):itemExchange.Handoff.Running&&itemExchange.Handoff.ItemId==o.ObjectId?V(itemExchange.Handoff.Position):item.Location=="Hand"?bodies[item.Owner].RightHand.position:V(item.Position);
                item.Position=P(o.transform.position);
                if(reading!=null){ReadingPose(reading,reading.ElapsedTicks,out _,out var rotation);o.transform.rotation=rotation;}
                else if(returning!=null){ToolReturnPose(returning,returning.ElapsedTicks,out _,out var rotation);o.transform.rotation=rotation*Quaternion.Euler(0,90,0);}
                else if(returned!=null)o.transform.rotation=Quaternion.LookRotation(V(returned.TargetForward),V(returned.TargetUp));
                else if(pickup!=null){PickupPose(pickup,pickup.ElapsedTicks,out _,out var rotation);o.transform.rotation=rotation*Quaternion.Euler(0,90,0);}
                else if(press!=null){PressPose(press,press.ElapsedTicks,out _,out var rotation);o.transform.rotation=rotation;}
                else if(item.Location=="Hand"&&traceSources.Any(s=>s&&TraceSourceId(s)==o.ObjectId))o.transform.rotation=bodies[item.Owner].RightHand.rotation*Quaternion.Euler(0,90,0);
                else if(item.Location=="Hand"&&(o.ObjectId==LoanPen||o.Loan!=null&&o.Loan.Complete||ReadingBook(o.ObjectId))){
                    var h=itemExchange.Handoff;bool exchanging=h.Running&&h.ItemId==o.ObjectId&&h.MotionVersion==1&&h.Phase!="Approaching";
                    o.transform.rotation=exchanging?Quaternion.LookRotation(V(h.ContactAxis)):bodies[item.Owner].RightHand.rotation*Quaternion.Euler(0,90,0);
                }
                PresentWeapon(o,item);
            }
        }

        public string[] InvitationPlaceIds()=>new[]{"R_HALL","R_DINING","R_LIBRARY","R_GREEN","R_EXHIBIT","R_WORK","R_GARDEN","R_ARCADE"}.Where(id=>Layout.Room(id)!=null).ToArray();
    }
}
