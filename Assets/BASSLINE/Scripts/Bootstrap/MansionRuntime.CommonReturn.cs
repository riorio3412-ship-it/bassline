using System;
using System.IO;
using System.Linq;
using UnityEngine;
using BASSLINE.AuthoringData;
using BASSLINE.Core;
using BASSLINE.World.Mansion;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        const string Collector="CH_18";
        CommonReturnStation returnStation;
        CommonReturnState ReturnState=>itemExchange.CommonReturn??(itemExchange.CommonReturn=new CommonReturnState());
        bool SurfaceRunning=>ReturnState.Motion.Running;
        bool PlayerSurfaceRunning=>SurfaceRunning&&ReturnState.Motion.Actor=="CH_01";
        bool ReturnTaskControls(string actor)=>actor==Collector&&ReturnState.Task!="Idle";
        string SurfaceAction(string id)
        {
            if(id==CommonReturnStation.TrayId)return World.Resident("CH_01").HeldObject==LoanPen?"펜 내려놓기":"안내 읽기";
            if(id==CommonReturnStation.DrawerId)return ReturnState.DrawerOpen?"정리함 닫기":"정리함 열기";
            if(id==CommonReturnStation.BookId)return "정리 기록 읽기";
            if(id==CommonReturnStation.BagFrontId)return World.Resident("CH_01").HeldObject==LoanPen?"가방 앞에 펜 놓기":"가방 살펴보기";
            if(id==CommonReturnStation.PrivateNoteId)return "메모 읽기";
            return "";
        }
        string InteractReturnStation(string id)
        {
            if(id==CommonReturnStation.BagFrontId)return World.Resident("CH_01").HeldObject==LoanPen?BeginSurfaceTransfer("CH_01","PlacePrivate"):"Inspect";
            if(id==CommonReturnStation.TrayId){
                if(World.Resident("CH_01").HeldObject!=LoanPen)return "Inspect";
                return BeginSurfaceTransfer("CH_01","Place");
            }
            if(id==CommonReturnStation.DrawerId){
                if(SurfaceRunning||ReturnState.Task=="Writing")return "정리 중이에요. 잠시 기다려 주세요.";
                ReturnState.DrawerOpen=!ReturnState.DrawerOpen;Present();
                World.Emit("PublicDrawerOperated","CH_01",id,ReturnState.DrawerOpen?"Open":"Closed");
                return ReturnState.DrawerOpen?"정리함을 열었어요. 안의 물건을 가까이서 살펴볼 수 있어요.":"정리함을 닫았어요.";
            }
            return "Inspect";
        }
        bool TryPlanReturnVisit()
        {
            if(!returnStation||ReturnState.Task!="Idle"||World.Tick<ReturnState.NextVisitTick)return false;
            // A periodic housekeeping visit, independent of a player's unseen placement.
            if(World.Plan(Collector,returnStation.TrayNode,"CheckCommonReturns",180)=="Accepted"){
                ReturnState.Task="Visiting";ReturnState.NextVisitTick=World.Tick+18000;return true;
            }return false;
        }
        static bool SurfaceTaking(string mode)=>mode=="Collect"||mode=="Retrieve"||mode=="TakePrivate";
        static bool SurfaceContainer(string mode)=>mode=="Store"||mode=="Retrieve";
        static string SurfaceAnchor(string mode)=>mode=="PlacePrivate"||mode=="TakePrivate"?CommonReturnStation.BagFrontId:SurfaceContainer(mode)?CommonReturnStation.DrawerId:CommonReturnStation.TrayId;
        Vector3 SurfaceContact(string mode)=>SurfaceAnchor(mode)==CommonReturnStation.BagFrontId?returnStation.BagFrontPoint.position+Vector3.up*.04f:SurfaceContainer(mode)?returnStation.DrawerPoint.position+Vector3.up*.032f:returnStation.TrayPoint.position+Vector3.up*.04f;
        bool AtSurface(string actor,string mode)
        {
            var p=SurfaceContact(mode);var a=bodies[actor];return Vector3.Distance(a.transform.position,p)<1.65f&&Visible(actor,p,2,false);
        }
        string BeginSurfaceTransfer(string actor,string mode)
        {
            if(!returnStation||toolPress.Running||SurfaceRunning||itemExchange.Handoff.Running||World.Paused||!AtSurface(actor,mode))return "반납대 가까이에서 손이 닿는 위치로 다가가 주세요.";
            var a=World.Resident(actor);var item=World.Object(LoanPen);bool taking=SurfaceTaking(mode);
            if(!World.CanAct(actor)||RescueControls(actor)||conversationPlayback.Phase=="Speaking"&&conversationPlayback.SpeakerId==actor)return "지금은 손을 뻗을 수 없어요.";
            if(taking?(a.HeldObject!=""||item.Location!=(SurfaceContainer(mode)?"Container":"World")||item.AnchorId!=SurfaceAnchor(mode)||mode=="Retrieve"&&!ReturnState.DrawerOpen):(a.HeldObject!=LoanPen||item.Owner!=actor))return "지금 옮길 수 있는 펜이 없어요.";
            var arm=Arm(actor);Vector3 contact=SurfaceContact(mode),start=bodies[actor].RightHand.position;
            if(!arm||!arm.Pose(contact,bodies[actor].transform.rotation)){PoseRestArms(actor);return "조금 더 가까이 다가가 주세요.";}
            PoseRestArms(actor);
            if(actor=="CH_01"){EndConversation();StopWaiting("펜을 내려놓으려고 멈췄어요.");move=default;running=false;}
            var m=new SurfaceTransfer{Id="SURFACE_L"+World.Loop+"_"+(++ReturnState.Sequence),Actor=actor,Mode=mode,Phase="Moving",StartedTick=World.Tick,StartGrip=P(start),StartItem=item.Position,Contact=P(contact),Position=taking?item.Position:P(start),ResumePhase=a.Phase,ResumeActivity=a.Activity,ResumeTicks=a.ActivityTicks};
            ReturnState.Motion=m;if(mode=="Store")ReturnState.DrawerOpen=true;
            a.Phase="Performing";a.Activity="SurfaceTransfer";a.ActivityTicks=int.MaxValue;World.Emit("SurfaceTransferStarted",actor,LoanPen,mode);
            return "펜을 옮기고 있어요.";
        }
        Vector3 SurfaceGrip(SurfaceTransfer m,int ticks)
        {
            float t=ticks<=90?Mathf.SmoothStep(0,1,Mathf.Clamp01(ticks/60f)):1-Mathf.SmoothStep(0,1,(ticks-90)/30f);
            return Vector3.Lerp(V(m.StartGrip),V(m.Contact),t);
        }
        void PoseSurfaceTransfer(){var m=ReturnState.Motion;if(!m.Running)return;var rig=Arm(m.Actor);if(!rig)return;rig.Pose(SurfaceGrip(m,m.Ticks),bodies[m.Actor].transform.rotation);bool taking=SurfaceTaking(m.Mode);rig.Curl(taking?Mathf.Clamp01((m.Ticks-50)/40f):1-Mathf.Clamp01((m.Ticks-80)/15f));}
        bool SurfacePathClear(SurfaceTransfer m,Vector3 next)
        {
            var delta=next-V(m.Position);bool Blocks(Collider c)=>!c.transform.IsChildOf(bodies[m.Actor].transform)&&!c.transform.IsChildOf(targets[LoanPen].transform);
            int count=Physics.OverlapSphereNonAlloc(next,.015f,overlap,~0,QueryTriggerInteraction.Ignore);if(count==overlap.Length)return false;
            for(int i=0;i<count;i++)if(Blocks(overlap[i]))return false;
            if(delta.sqrMagnitude<.000001f)return true;
            count=Physics.SphereCastNonAlloc(V(m.Position),.015f,delta.normalized,rayHits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);if(count==rayHits.Length)return false;
            for(int i=0;i<count;i++)if(Blocks(rayHits[i].collider))return false;return true;
        }
        bool SurfaceArmClear(SurfaceTransfer m)
        {
            var rig=Arm(m.Actor);return Clear(rig.Upper.position,rig.Forearm.position,.049f)&&Clear(rig.Forearm.position,rig.Grip.position,.035f);
            bool Clear(Vector3 a,Vector3 b,float radius){int count=Physics.OverlapCapsuleNonAlloc(a,b,radius,overlap,~0,QueryTriggerInteraction.Ignore);if(count==overlap.Length)return false;for(int i=0;i<count;i++){var t=overlap[i].transform;if(!t.IsChildOf(bodies[m.Actor].transform)&&!t.IsChildOf(targets[LoanPen].transform))return false;}return true;}
        }
        void AdvanceCommonReturn()
        {
            if(!returnStation)return;
            var s=ReturnState;var m=s.Motion;var item=World.Object(LoanPen);var collector=World.Resident(Collector);
            if(m.Running){
                var actor=World.Resident(m.Actor);bool taking=SurfaceTaking(m.Mode);
                bool held=taking?m.Committed:!m.Committed;
                bool custody=held?item.Location=="Hand"&&item.Owner==m.Actor&&actor.HeldObject==LoanPen:actor.HeldObject==""&&item.Location==(SurfaceContainer(m.Mode)?"Container":"World")&&item.AnchorId==SurfaceAnchor(m.Mode);
                if(!World.CanAct(m.Actor)||RescueControls(m.Actor)||actor.Activity!="SurfaceTransfer"||!custody||!AtSurface(m.Actor,m.Mode)){CancelSurfaceTransfer("펜을 옮기던 동작이 멈췄어요.");return;}
                int ticks=m.Ticks+1;var grip=SurfaceGrip(m,ticks);var rig=Arm(m.Actor);
                if(!rig||!rig.Pose(grip,bodies[m.Actor].transform.rotation)||!SurfaceArmClear(m)){CancelSurfaceTransfer("팔을 뻗을 공간이 부족해 멈췄어요.");return;}
                var next=held?grip:V(m.Contact);
                if(!SurfacePathClear(m,next)){CancelSurfaceTransfer("사이에 물체가 있어 멈췄어요.");return;}
                m.Ticks=ticks;m.Position=P(next);if(held)item.Position=m.Position;
                if(!m.Committed&&ticks==90){
                    string result=taking?(m.Mode=="Retrieve"?World.Retrieve(m.Actor,LoanPen,CommonReturnStation.DrawerId):World.Pickup(LoanPen,m.Actor)):World.PlaceAt(m.Actor,LoanPen,SurfaceAnchor(m.Mode),m.Contact,m.Mode=="Store");
                    if(result!="Committed"){m.Ticks=89;CancelSurfaceTransfer("펜의 위치가 바뀌어 멈췄어요.");return;}
                    m.Committed=true;item.Position=m.Contact;ObserveSurfaceCommit(m);
                    if(m.Mode=="Collect"){s.CollectedTick=World.Tick;s.StoredTick=-1;s.ReceiptId=m.Id;}
                    if(m.Mode=="Store")s.StoredTick=World.Tick;
                }
                if(ticks==120){m.Phase="Completed";ReleaseSurfaceActor(m);
                    m.Reason=m.Mode=="PlacePrivate"?"태겸의 가방 앞에 놓았어요. 아직 태겸이 받은 것은 아니에요.":m.Mode=="TakePrivate"?"가방 앞에서 펜을 다시 집었어요.":m.Mode=="Place"?"공용 반납대에 놓았어요. 태겸에게 직접 돌려준 것은 아니에요.":m.Mode=="Retrieve"?"정리함에서 펜을 꺼냈어요.":"정리를 마쳤어요.";
                    if(m.Actor=="CH_01")message=m.Reason;
                    if(m.Actor==Collector){if(m.Mode=="Collect")s.Task="Carrying";else if(m.Mode=="Store"){s.Task="Writing";s.WritingTicks=0;collector.Phase="Performing";collector.Activity="WriteCleanupRecord";collector.ActivityTicks=int.MaxValue;}}
                }return;
            }
            if(s.Task=="Idle")return;
            if(!World.CanAct(Collector)||RescueControls(Collector)||incidents.Controls(Collector)){FinishReturnTask();return;}
            if(collector.Activity=="Talk"||collector.Activity=="Listen"||collector.Activity=="HandOver")return;
            if(s.Task=="Visiting"){
                if(collector.Phase=="Travelling")return;
                if(collector.Phase!="Performing"||collector.Activity!="CheckCommonReturns"){FinishReturnTask();return;}
                collector.ActivityTicks=int.MaxValue;
                if(item.Location!="World"||item.AnchorId!=CommonReturnStation.TrayId||collector.HeldObject!=""){FinishReturnTask();return;}
                if(!ApproachReturnSurface("Collect"))return;
                if(BeginSurfaceTransfer(Collector,"Collect")=="펜을 옮기고 있어요.")s.Task="Collecting";
            }else if(s.Task=="Carrying"){
                if(collector.HeldObject!=LoanPen){FinishReturnTask();return;}
                if(collector.Destination!=returnStation.DrawerNode||collector.Activity!="StoreCommonReturn"){
                    if(World.Plan(Collector,returnStation.DrawerNode,"StoreCommonReturn",180)!="Accepted")return;
                }
                if(collector.Phase=="Performing"&&ApproachReturnSurface("Store"))BeginSurfaceTransfer(Collector,"Store");
            }else if(s.Task=="Writing"){
                if(collector.Activity!="WriteCleanupRecord"){FinishReturnTask();return;}
                if(++s.WritingTicks<180)return;
                var entry=new CleanupEntry{Id=s.ReceiptId,ItemId=LoanPen,Collector=Collector,CollectedTick=s.CollectedTick,StoredTick=s.StoredTick,WrittenTick=World.Tick,
                    Text=WorldTimeLabel.Format(s.CollectedTick,World.ClockVersion)+" · 민서\n공용 반납대의 푸른 펜 한 자루를 수거했다. 뚜껑 옆에 흠집, 은색 클립.\n"+WorldTimeLabel.Format(s.StoredTick,World.ClockVersion)+" · 공용 정리함에 보관했다. 주인에게 직접 전달한 것은 아니다."};
                s.Entries=s.Entries.Concat(new[]{entry}).ToArray();World.Emit("CleanupRecordWritten",Collector,LoanPen,entry.Id);s.DrawerOpen=false;FinishReturnTask();
            }
        }
        bool ApproachReturnSurface(string mode)
        {
            var a=World.Resident(Collector);var body=bodies[Collector];Vector3 contact=SurfaceContact(mode),delta=contact-body.transform.position;delta.y=0;
            if(delta.magnitude>.3f){
                // Face with the reaching shoulder, not the centre of the chest. Otherwise a broad
                // actor can hit the furniture capsule limit while the sideways hand remains out of reach.
                var rig=Arm(Collector);float offset=rig?body.transform.InverseTransformPoint(rig.Shoulder).x:0;
                float shoulderTurn=Mathf.Asin(Mathf.Clamp(offset/delta.magnitude,-.85f,.85f))*Mathf.Rad2Deg;
                a.Yaw=Math.Atan2(delta.x,delta.z)*180/Math.PI-shoulderTurn;
            }
            body.transform.rotation=Quaternion.Euler(0,(float)a.Yaw,0);PoseRestArms(Collector);
            bool reach=AtSurface(Collector,mode)&&Arm(Collector).Pose(contact,body.transform.rotation);PoseRestArms(Collector);if(reach)return true;
            if(++a.StuckTicks>240){FinishReturnTask();return false;}
            a.Position=Move(Collector,P(delta.normalized*.012f));return false;
        }
        void FinishReturnTask(){var a=World.Resident(Collector);if(a.Activity=="CheckCommonReturns"||a.Activity=="StoreCommonReturn"||a.Activity=="WriteCleanupRecord"){a.Phase="Idle";a.Activity="Rest";a.ActivityTicks=0;}ReturnState.Task="Idle";ReturnState.NextVisitTick=Math.Max(ReturnState.NextVisitTick,World.Tick+1800);}
        void ReleaseSurfaceActor(SurfaceTransfer m){var a=World.Resident(m.Actor);if(a.Alive&&a.Present&&a.Activity=="SurfaceTransfer"){a.Phase=m.ResumePhase;a.Activity=m.ResumeActivity;a.ActivityTicks=m.ResumeTicks;}}
        void CancelSurfaceTransfer(string reason){var m=ReturnState.Motion;if(!m.Running)return;m.Phase="Cancelled";m.Reason=reason;ReleaseSurfaceActor(m);if(m.Actor==Collector){
            if(m.Mode=="Collect"&&m.Committed||m.Mode=="Store"&&!m.Committed)ReturnState.Task="Carrying";
            else if(m.Mode=="Store"&&m.Committed){ReturnState.Task="Writing";ReturnState.WritingTicks=0;var a=World.Resident(Collector);a.Phase="Performing";a.Activity="WriteCleanupRecord";a.ActivityTicks=int.MaxValue;}
            else FinishReturnTask();
        }World.Emit("SurfaceTransferInterrupted",m.Actor,LoanPen,m.Committed?"AfterCommit":"BeforeCommit");if(m.Actor=="CH_01")message=reason;}
        void ObserveSurfaceCommit(SurfaceTransfer m)
        {
            string what=m.Mode=="PlacePrivate"?"태겸의 개인 가방 앞에 펜을 내려놓았다.":m.Mode=="TakePrivate"?"태겸의 개인 가방 앞에서 펜을 다시 집었다.":m.Mode=="Place"?"공용 반납대에 펜을 내려놓았다.":m.Mode=="Collect"?"공용 반납대에서 펜을 집었다.":m.Mode=="Store"?"공용 정리함에 펜을 보관했다.":"공용 정리함에서 펜을 꺼냈다.";
            foreach(var a in World.Residents.Where(a=>a.Alive&&a.Present&&(a.Id==m.Actor||CanSee(a.Id,m.Actor)&&Identifies(a.Id,m.Actor)&&Visible(a.Id,V(m.Contact)))))
                Knowledge.Observe(a.Id,new KnownRecord{Kind="Visual",Source=a.Id,SubjectId=LoanPen,Predicate="SurfaceObjectTransfer",Value=m.Mode,ProvenanceKey=m.Id,Text=NameOf(m.Actor)+" · "+what,IdentityConfirmed=true,Position=m.Contact,PlaceId="R_WORK",FromTick=m.StartedTick,ToTick=World.Tick+1,Supports=new[]{"직접 본 이번 물건 이동"},DoesNotEstablish=new[]{"소유권·절도·보지 못한 이동·주인의 수령은 확정하지 않음"}},World.Tick);
        }
        string ReadCleanupBook()
        {
            string text=ReturnState.Entries.Length==0?"아직 적힌 정리 기록이 없다. 기록이 없다는 것만으로 물건이 옮겨지지 않았다고 단정할 수는 없다.":string.Join("\n\n",ReturnState.Entries.Select(e=>e.Text));
            return Knowledge.Observe("CH_01",new KnownRecord{Kind="Document",Source="CH_01",SubjectId=CommonReturnStation.BookId,Predicate="CleanupRecord",Value=ReturnState.Entries.Length>0?"StoredPen:"+LoanPen+":"+ReturnState.Entries.Last().CollectedTick:"Empty",ProvenanceKey="CLEANUP_BOOK_"+ReturnState.Entries.Length,Text=text,Position=P(targets[CommonReturnStation.BookId].transform.position),PlaceId="R_WORK",FromTick=World.Tick,ToTick=World.Tick+1,Supports=new[]{"지금 펼쳐 읽은 정리 기록의 내용"},DoesNotEstablish=new[]{"기록의 내용과 원물은 따로 확인해야 함"}},World.Tick);
        }
        void ValidateCommonReturn(CommonReturnState s,MansionState world)
        {
            if(s==null)return; // Older saves predate public housekeeping.
            if(s.Motion==null||s.Entries==null||s.Sequence<0||s.NextVisitTick<0||s.CollectedTick< -1||s.StoredTick< -1||s.CollectedTick>world.Tick||s.StoredTick>world.Tick||s.WritingTicks<0||s.WritingTicks>180||!new[]{"Idle","Visiting","Collecting","Carrying","Writing"}.Contains(s.Task))throw new InvalidDataException("공용 정리 상태가 올바르지 않습니다.");
            foreach(var e in s.Entries)if(e==null||e.ItemId!=LoanPen||e.Collector!=Collector||e.CollectedTick<0||e.StoredTick<e.CollectedTick||e.WrittenTick<e.StoredTick||e.WrittenTick>world.Tick||!world.Events.Any(v=>v.Type=="CleanupRecordWritten"&&v.Actor==Collector&&v.Detail==e.Id&&v.Tick==e.WrittenTick))throw new InvalidDataException("실제로 작성되지 않은 정리 기록입니다.");
            if(s.Entries.Select(e=>e.Id).Distinct().Count()!=s.Entries.Length)throw new InvalidDataException("중복 정리 기록입니다.");
            var m=s.Motion;if(m.Phase=="Idle")return;
            if(s.Sequence<1||m.Id!="SURFACE_L"+world.Loop+"_"+s.Sequence||!returnStation||m.Contact.Distance(P(SurfaceContact(m.Mode)))>.001)throw new InvalidDataException("물건 이동 장소가 올바르지 않습니다.");
            if(!new[]{"Moving","Completed","Cancelled"}.Contains(m.Phase)||!new[]{"Place","Collect","Store","Retrieve","PlacePrivate","TakePrivate"}.Contains(m.Mode)||m.Actor!=(m.Mode=="Collect"||m.Mode=="Store"?Collector:"CH_01")||m.StartedTick<0||m.Ticks<0||m.Ticks>120||m.StartedTick+m.Ticks>world.Tick||m.Committed!=(m.Ticks>=90)||!m.StartGrip.Finite()||!m.StartItem.Finite()||!m.Contact.Finite()||!m.Position.Finite()||m.Phase=="Completed"&&m.Ticks!=120)throw new InvalidDataException("물건을 옮기는 동작이 올바르지 않습니다.");
            if(m.Committed){string type=m.Mode=="Place"||m.Mode=="PlacePrivate"?"ObjectPlacedAt":m.Mode=="Store"?"ObjectStored":m.Mode=="Retrieve"?"ObjectRetrieved":"ObjectPickedUp";if(!world.Events.Any(e=>e.Type==type&&e.Actor==m.Actor&&e.Target==LoanPen&&e.Tick==m.StartedTick+90))throw new InvalidDataException("물건 이동 사건이 없습니다.");}
            if(m.Running){var actor=world.Residents.Single(a=>a.Id==m.Actor);var item=world.Objects.Single(o=>o.Id==LoanPen);bool taking=SurfaceTaking(m.Mode),held=taking?m.Committed:!m.Committed;
                if(actor.Activity!="SurfaceTransfer"||held&&(actor.HeldObject!=LoanPen||item.Owner!=m.Actor||item.Location!="Hand")||!held&&(actor.HeldObject!=""||item.Owner!=""||item.Location!=(SurfaceContainer(m.Mode)?"Container":"World")||item.AnchorId!=SurfaceAnchor(m.Mode)))throw new InvalidDataException("인물의 행동과 물건 이동이 다릅니다.");
            }
        }
    }
}
