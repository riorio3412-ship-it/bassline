using System;
using System.Linq;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
using BASSLINE.World.Mansion;
using BASSLINE.Knowledge;
using BASSLINE.Save;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        const string ReadingActivity="ReadHeldDocument";
        const int ReadingRaise=60,ReadingLower=60;
        ResidentReading[] residentReadings=Array.Empty<ResidentReading>();
        MansionReadableBook[] readingBooks=Array.Empty<MansionReadableBook>();
        MansionReadableBook ReadingBook(string id)=>readingBooks.FirstOrDefault(b=>b&&b.GetComponent<FixtureObjectBody>().ObjectId==id);
        ResidentReading ReadingForItem(string id)=>residentReadings.FirstOrDefault(r=>r.Running&&r.ItemId==id);
        bool AlreadyRead(string actor,MansionReadableBook book)=>Knowledge.For(actor).Records().Any(r=>r.Direct&&r.Kind=="Document"&&r.SubjectId==book.GetComponent<FixtureObjectBody>().ObjectId&&r.Predicate=="ReadDocument"&&r.Value==book.Revision&&r.Text==book.Content);
        bool TryPlanResidentReading(ResidentRoutine routine,ResidentState actor)
        {
            // The preference is a desire to read this item, never access to its hidden contents.
            if(string.IsNullOrEmpty(routine.ReadingItemId)||actor.HeldObject!=routine.ReadingItemId||ResidentToolBusy(actor.Id)||!ResidentCanUseTool(actor.Id))return false;
            var book=ReadingBook(actor.HeldObject);if(!book||AlreadyRead(actor.Id,book))return false;
            if(residentReadings.Any(r=>r.ActorId==actor.Id&&r.Phase=="Cancelled"&&World.Tick-r.LastTick<1800))return false;
            return World.Plan(actor.Id,actor.Node,ReadingActivity,book.ReadingTicks+ReadingRaise+ReadingLower+90)=="Accepted";
        }
        static string ReadingPhase(ResidentReading r,int ticks)=>ticks<ReadingRaise?"Raising":ticks<ReadingRaise+r.RequiredTicks?"Reading":ticks<ReadingRaise+r.RequiredTicks+ReadingLower?"Lowering":"Completed";
        static void ReadingPose(ResidentReading r,int ticks,out Vector3 position,out Quaternion rotation)
        {
            float amount=ticks<=ReadingRaise?Mathf.SmoothStep(0,1,ticks/(float)ReadingRaise):ticks<=ReadingRaise+r.RequiredTicks?1:1-Mathf.SmoothStep(0,1,(ticks-ReadingRaise-r.RequiredTicks)/(float)ReadingLower);
            position=Vector3.Lerp(V(r.StartPosition),V(r.TargetPosition),amount);
            rotation=Quaternion.Slerp(Quaternion.LookRotation(V(r.StartForward),V(r.StartUp)),Quaternion.LookRotation(V(r.TargetForward),V(r.TargetUp)),amount);
        }
        bool PoseResidentReading(ResidentReading r,int ticks)
        {
            var rig=Arm(r.ActorId);var book=ReadingBook(r.ItemId);if(!rig||!book)return false;
            ReadingPose(r,ticks,out var position,out var rotation);
            bool reachable=rig.Pose(position,rotation*Quaternion.Euler(0,-90,0));rig.Curl(1);
            book.transform.SetPositionAndRotation(position,rotation);
            var body=bodies[r.ActorId];var eye=body.transform.position+Vector3.up*body.Height*.88f;
            float amount=ticks<=ReadingRaise?Mathf.Clamp01(ticks/(float)ReadingRaise):ticks<=ReadingRaise+r.RequiredTicks?1:1-Mathf.Clamp01((ticks-ReadingRaise-r.RequiredTicks)/(float)ReadingLower);
            var delta=book.ReadPoint-eye;if(delta.sqrMagnitude>.0001f)body.Head.rotation=Quaternion.Slerp(body.transform.rotation,Quaternion.LookRotation(delta,Vector3.up),amount);
            return reachable;
        }
        bool ReadingPathClear(ResidentReading r,int from,int to)
        {
            var book=ReadingBook(r.ItemId);if(!book||!(book.GetComponent<FixtureObjectBody>().Collider is BoxCollider box)||book.transform.lossyScale!=Vector3.one)return false;
            ReadingPose(r,from,out var a,out var qa);ReadingPose(r,to,out var b,out var qb);
            var segment=new ToolPressState{ActorId=r.ActorId,ToolId=r.ItemId,StartPosition=P(a),ContactPosition=P(b),StartForward=P(qa*Vector3.forward),StartUp=P(qa*Vector3.up),ContactForward=P(qb*Vector3.forward),ContactUp=P(qb*Vector3.up)};
            return ToolPressPathClear(segment,0,PressReachTicks,box);
        }
        void BeginResidentReadings()
        {
            if(World.Paused||World.Tick%30!=0)return;
            foreach(var actor in World.Residents.Where(a=>a.Id!="CH_01"&&a.Phase=="Performing"&&a.Activity==ReadingActivity)){
                if(!ResidentCanUseTool(actor.Id)||ResidentToolBusy(actor.Id))continue;
                var book=ReadingBook(actor.HeldObject);if(!book||AlreadyRead(actor.Id,book)||actor.ActivityTicks<book.ReadingTicks+ReadingRaise+ReadingLower+1)continue;
                var item=World.Object(actor.HeldObject);if(item.Location!="Hand"||item.Owner!=actor.Id)continue;
                long end=World.Tick+actor.ActivityTicks;if(residentReadings.Any(r=>r.ActorId==actor.Id&&r.ActivityEndTick==end))continue;
                var body=bodies[actor.Id];var eye=body.transform.position+Vector3.up*body.Height*.88f;
                var target=body.transform.TransformPoint(new Vector3(.11f,body.Height*.72f,.34f));
                var normal=(eye-target).normalized;var upPage=Vector3.ProjectOnPlane(Vector3.up,normal).normalized;if(upPage.sqrMagnitude<.5f)continue;
                var rotation=Quaternion.LookRotation(upPage,normal);
                var r=new ResidentReading{ActorId=actor.Id,ItemId=item.Id,Revision=book.Revision,Content=book.Content,Node=actor.Node,StartedTick=World.Tick,LastTick=World.Tick,ActivityEndTick=end,RequiredTicks=book.ReadingTicks,BodyYaw=actor.Yaw,ActorPosition=actor.Position,StartPosition=P(book.transform.position),StartForward=P(book.transform.forward),StartUp=P(book.transform.up),TargetPosition=P(target),TargetForward=P(rotation*Vector3.forward),TargetUp=P(rotation*Vector3.up),Position=P(book.transform.position)};
                if(string.IsNullOrWhiteSpace(r.Revision)||string.IsNullOrWhiteSpace(r.Content)||!ReadingPathClear(r,0,ReadingRaise))continue;
                // Temporary posing only checks reach/clearance; it never grants a read receipt.
                bool reachable=PoseResidentReading(r,ReadingRaise)&&PressArmSpaceClear(new ToolPressState{ActorId=actor.Id,ToolId=item.Id});
                book.transform.SetPositionAndRotation(V(r.StartPosition),Quaternion.LookRotation(V(r.StartForward),V(r.StartUp)));PoseRestArms(actor.Id);body.Head.rotation=body.transform.rotation;
                if(!reachable)continue;
                residentReadings=residentReadings.Where(old=>old.ActorId!=actor.Id).Concat(new[]{r}).ToArray();World.Emit("ResidentReadingStarted",actor.Id,item.Id,r.Revision);
            }
        }
        bool BookReadableBy(ResidentReading r,MansionReadableBook book)
        {
            if(book.Revision!=r.Revision||book.Content!=r.Content||!book.TryReadPoints(out var points))return false;
            var body=bodies[r.ActorId];var eye=body.transform.position+Vector3.up*body.Height*.88f;
            if(Vector3.Dot(book.ReadNormal,(eye-book.ReadPoint).normalized)<.35f)return false;
            foreach(var point in points){
                var delta=point-eye;if(delta.sqrMagnitude<.0001f||delta.magnitude>.9f||Vector3.Dot(body.Head.forward,delta.normalized)<.6f)return false;
                int count=Physics.RaycastNonAlloc(eye,delta.normalized,rayHits,Mathf.Max(0,delta.magnitude-.002f),~0,QueryTriggerInteraction.Ignore);
                if(count==rayHits.Length)return false;
                for(int i=0;i<count;i++)if(!rayHits[i].transform.IsChildOf(body.transform))return false;
            }
            return true;
        }
        void AdvanceResidentReadings()
        {
            if(World.Paused)return;
            foreach(var r in residentReadings.Where(x=>x.Running)){
                var actor=World.Resident(r.ActorId);var item=World.Object(r.ItemId);var book=ReadingBook(r.ItemId);
                if(!book||book.Revision!=r.Revision||book.Content!=r.Content||!ResidentCanUseTool(r.ActorId)||actor.Phase!="Performing"||actor.Activity!=ReadingActivity||actor.Node!=r.Node||World.Tick+actor.ActivityTicks!=r.ActivityEndTick||World.Tick>=r.ActivityEndTick||World.Tick!=r.LastTick+1||actor.Position.Distance(r.ActorPosition)>.025||Mathf.Abs(Mathf.DeltaAngle((float)r.BodyYaw,(float)actor.Yaw))>5||actor.HeldObject!=r.ItemId||item.Location!="Hand"||item.Owner!=r.ActorId){CancelResidentReading(r,"ReadingInterrupted");continue;}
                int next=r.ElapsedTicks+1;
                if(!ReadingPathClear(r,r.ElapsedTicks,next)||!PoseResidentReading(r,next)||!PressArmSpaceClear(new ToolPressState{ActorId=r.ActorId,ToolId=r.ItemId})){CancelResidentReading(r,"ReadingSpaceBlocked");continue;}
                Physics.SyncTransforms();
                if(next>ReadingRaise&&next<=ReadingRaise+r.RequiredTicks){
                    if(!BookReadableBy(r,book)){CancelResidentReading(r,"PageNotReadable");continue;}
                    r.ReadingTicks++;
                }
                r.ElapsedTicks=next;r.LastTick=World.Tick;r.Phase=ReadingPhase(r,next);ReadingPose(r,next,out var position,out _);r.Position=P(position);item.Position=r.Position;
                if(r.ReadingTicks==r.RequiredTicks&&r.ReadAt<0){
                    r.ReadAt=World.Tick;
                    r.RecordId=Knowledge.Observe(r.ActorId,new KnownRecord{Kind="Document",Source=r.ActorId,SubjectId=r.ItemId,IdentityConfirmed=true,Predicate="ReadDocument",Value=r.Revision,Text=r.Content,ProvenanceKey="BOOK_READ_L"+World.Loop+"_"+r.ActorId+"_"+r.StartedTick,FromTick=r.StartedTick+ReadingRaise+1,ToTick=World.Tick+1,PlaceId=PlaceOf(position),Position=r.Position,Supports=new[]{"직접 바라보며 읽은 이 페이지의 문장"},DoesNotEstablish=new[]{"글에 적힌 내용이 세계의 사실이라는 보장", "책의 다른 페이지", "다른 사람이 읽었는지"}},World.Tick);
                    World.Emit("ResidentReadDocument",r.ActorId,r.ItemId,r.RecordId);
                }
                if(r.Phase=="Completed")World.Emit("ResidentReadingEnded",r.ActorId,r.ItemId,"ReadAndLowered");
            }
        }
        void CancelResidentReading(ResidentReading r,string reason)
        {
            if(!r.Running)return;r.Phase="Cancelled";r.Reason=reason;PoseRestArms(r.ActorId);bodies[r.ActorId].Head.rotation=bodies[r.ActorId].transform.rotation;
            // Keep custody and any already completed reading. No whole-page receipt for partial reading.
            World.Emit("ResidentReadingEnded",r.ActorId,r.ItemId,reason);
        }
        void CaptureResidentReadings(MansionSessionSnapshot s)
        {
            s.OptionalObjects|=134217728;s.ResidentReadings=residentReadings.Select(r=>r.Copy()).ToArray();
            foreach(var r in s.ResidentReadings.Where(x=>x.Running)){
                var actor=s.World.Residents.Single(a=>a.Id==r.ActorId);var item=s.World.Objects.Single(o=>o.Id==r.ItemId);
                if(r.LastTick!=s.World.Tick||!actor.Alive||!actor.Present||actor.Phase!="Performing"||actor.Activity!=ReadingActivity||actor.Node!=r.Node||s.World.Tick+actor.ActivityTicks!=r.ActivityEndTick||actor.HeldObject!=r.ItemId||item.Location!="Hand"||item.Owner!=r.ActorId){r.Phase="Cancelled";r.Reason="CheckpointAfterInterruption";}
            }
        }
        void ValidateResidentReadings(MansionSessionSnapshot s,KnowledgeLedger knowledge)
        {
            void Invalid(){throw new System.IO.InvalidDataException("주민의 책 읽기와 실제 읽은 기록이 맞지 않습니다.");}
            if(s.ResidentReadings==null||s.ResidentReadings.Any(r=>r==null)||s.ResidentReadings.Select(r=>r.ActorId).Distinct().Count()!=s.ResidentReadings.Length)Invalid();
            foreach(var r in s.ResidentReadings){
                var book=ReadingBook(r.ItemId);
                if(r.ActorId=="CH_01"||!bodies.ContainsKey(r.ActorId)||!book||book.Content!=r.Content||book.Revision!=r.Revision||r.RequiredTicks!=book.ReadingTicks||!nodes.Any(n=>n.Id==r.Node)||r.StartedTick<0||r.LastTick<r.StartedTick||r.LastTick>s.World.Tick||r.LastTick-r.StartedTick!=r.ElapsedTicks||r.ActivityEndTick<=r.StartedTick+ReadingRaise+r.RequiredTicks+ReadingLower||r.ElapsedTicks<0||r.ElapsedTicks>ReadingRaise+r.RequiredTicks+ReadingLower||r.ReadingTicks!=Math.Min(r.RequiredTicks,Math.Max(0,r.ElapsedTicks-ReadingRaise))||double.IsNaN(r.BodyYaw)||double.IsInfinity(r.BodyYaw)||new[]{r.ActorPosition,r.StartPosition,r.StartForward,r.StartUp,r.TargetPosition,r.TargetForward,r.TargetUp,r.Position}.Any(p=>!p.Finite()))Invalid();
                if(!new[]{"Raising","Reading","Lowering","Completed","Cancelled"}.Contains(r.Phase)||r.Phase!="Cancelled"&&r.Phase!=ReadingPhase(r,r.ElapsedTicks)||r.ReadAt< -1||r.RecordId==null||r.Reason==null)Invalid();
                foreach(var pair in new[]{new[]{r.StartForward,r.StartUp},new[]{r.TargetForward,r.TargetUp}})if(Mathf.Abs(V(pair[0]).sqrMagnitude-1)>.01f||Mathf.Abs(V(pair[1]).sqrMagnitude-1)>.01f||Mathf.Abs(Vector3.Dot(V(pair[0]),V(pair[1])))>.01f)Invalid();
                ReadingPose(r,r.ElapsedTicks,out var expected,out _);if(Vector3.Distance(expected,V(r.Position))>.002f)Invalid();
                if(!s.World.Events.Any(e=>e.Type=="ResidentReadingStarted"&&e.Actor==r.ActorId&&e.Target==r.ItemId&&e.Detail==r.Revision&&e.Tick==r.StartedTick))Invalid();
                if(r.ReadAt<0){if(r.RecordId!=""||r.ReadingTicks==r.RequiredTicks)Invalid();}
                else{
                    var receipt=knowledge.For(r.ActorId).Find(r.RecordId);
                    if(r.ReadingTicks!=r.RequiredTicks||r.ReadAt!=r.StartedTick+ReadingRaise+r.RequiredTicks||r.ReadAt>r.LastTick||receipt==null||!receipt.Direct||receipt.Kind!="Document"||receipt.Source!=r.ActorId||receipt.SubjectId!=r.ItemId||receipt.Predicate!="ReadDocument"||receipt.Value!=r.Revision||receipt.Text!=r.Content||receipt.FromTick!=r.StartedTick+ReadingRaise+1||receipt.ToTick!=r.ReadAt+1||receipt.ReceivedTick!=r.ReadAt||!s.World.Events.Any(e=>e.Type=="ResidentReadDocument"&&e.Actor==r.ActorId&&e.Target==r.ItemId&&e.Detail==r.RecordId&&e.Tick==r.ReadAt))Invalid();
                }
                if(!r.Running)continue;
                var actor=s.World.Residents.Single(a=>a.Id==r.ActorId);var item=s.World.Objects.Single(o=>o.Id==r.ItemId);var h=s.ItemExchange.Handoff;
                if(r.LastTick!=s.World.Tick||!actor.Alive||!actor.Present||actor.Phase!="Performing"||actor.Activity!=ReadingActivity||actor.Node!=r.Node||s.World.Tick+actor.ActivityTicks!=r.ActivityEndTick||actor.Position.Distance(r.ActorPosition)>.025||actor.HeldObject!=r.ItemId||item.Location!="Hand"||item.Owner!=r.ActorId||item.Position.Distance(r.Position)>.02||s.World.PauseOwners.Contains("M_COURT")||h.Running&&(h.Giver==r.ActorId||h.Receiver==r.ActorId)||s.ResidentToolWork.Any(w=>w.ActorId==r.ActorId&&w.Motion.Running)||s.ResidentToolPickups.Any(p=>p.ActorId==r.ActorId&&p.Running)||s.ResidentToolReturns.Any(p=>p.ActorId==r.ActorId&&p.Running))Invalid();
            }
        }
    }
}
