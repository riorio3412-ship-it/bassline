using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
using BASSLINE.Save;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        DoorObservationState doorObservations=new DoorObservationState();
        bool SeesDoorLeaf(string observer,Transform pivot,Collider leaf,out float amount,Quaternion closed,float openAngle)
        {
            amount=0;
            if(!pivot||!leaf||!leaf.enabled||!leaf.gameObject.activeInHierarchy||Mathf.Abs(openAngle)<1)return false;
            if(!pivot.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.sharedMaterial&&(observer!="CH_01"||(cameraView.cullingMask&(1<<r.gameObject.layer))!=0)))return false;
            var body=PhysicalBody(observer);if(!body||!World.CanAct(observer))return false;
            Vector3 point=leaf.bounds.center,origin=observer=="CH_01"?cameraView.transform.position:body.transform.position+Vector3.up*body.Height*.88f;
            Vector3 delta=point-origin,forward=observer=="CH_01"?cameraView.transform.forward:body.Head.forward;
            if(delta.magnitude>5||delta.sqrMagnitude<.0001f||Vector3.Dot(forward,delta.normalized)<.6f)return false;
            if(observer=="CH_01"){
                var viewport=cameraView.WorldToViewportPoint(point);
                if(viewport.z<cameraView.nearClipPlane||viewport.z>cameraView.farClipPlane||viewport.x<0||viewport.x>1||viewport.y<0||viewport.y>1)return false;
            }
            int count=Physics.RaycastNonAlloc(origin,delta.normalized,rayHits,delta.magnitude+.01f,~0,QueryTriggerInteraction.Ignore);
            if(count==rayHits.Length)return false;
            var first=Enumerable.Range(0,count).Select(i=>rayHits[i]).Where(h=>!h.transform.IsChildOf(body.transform)).OrderBy(h=>h.distance).FirstOrDefault();
            if(!first.collider||!first.transform.IsChildOf(pivot))return false;
            amount=Mathf.Clamp01(Quaternion.Angle(closed,pivot.localRotation)/Mathf.Abs(openAngle));return true;
        }
        bool ReadVisibleDoorPose(string observer,string id,out double amount,out string state)
        {
            amount=0;state="";
            if(!doors.TryGetValue(id,out var door)||!door.gameObject.activeInHierarchy||!SeesDoorLeaf(observer,door.LeafPivot,door.LeafCollider,out float a,door.ClosedRotation,door.OpenAngle))return false;
            float b=a;
            if(door.SecondLeafPivot&&!SeesDoorLeaf(observer,door.SecondLeafPivot,door.SecondLeafCollider,out b,door.SecondClosedRotation,door.OpenAngle))return false;
            amount=(a+b)*.5;state=a<=.02f&&b<=.02f?"Closed":a>=.95f&&b>=.95f?"Open":"Ajar";return true;
        }
        static string DoorStateLabel(string state)=>state=="Closed"?"닫혀 있다":state=="Open"?"열려 있다":"일부 열려 있다";
        string ObserveDoorState(string observer,string door,string state,long tick)
            =>Knowledge.Observe(observer,new KnownRecord{Kind="Visual",Source=observer,SubjectId=door,Predicate="DoorState",Value=state,ProvenanceKey="DOOR_OBSERVATION_L"+World.Loop+"_"+observer+"_"+door,Text=NameOf(door)+" — 문짝이 "+DoorStateLabel(state)+".",PlaceId=PlaceOf(doors[door].transform.position),Position=P(doors[door].transform.position),FromTick=tick,ToTick=tick+1,Supports=new[]{"지금 눈으로 본 문의 열림 상태"},DoesNotEstablish=new[]{"잠금장치의 상태", "이전 시각의 상태", "문을 움직인 사람", "실제 통과자나 사건의 원인"}},World.Tick);
        void AdvanceDoorObservations()
        {
            if(World.Paused)return;
            var next=new List<DoorSightProgress>();
            foreach(var actor in World.Residents.Where(a=>World.CanAct(a.Id))){
                foreach(var door in doors.Values){
                    if(Vector3.Distance(bodies[actor.Id].transform.position,door.transform.position)>6)continue;
                    if(!ReadVisibleDoorPose(actor.Id,door.DoorId,out double amount,out string state))continue;
                    var prior=doorObservations.Sight.FirstOrDefault(s=>s.Observer==actor.Id&&s.DoorId==door.DoorId&&s.LastTick==World.Tick-1);
                    var current=new DoorSightProgress{Observer=actor.Id,DoorId=door.DoorId,LastTick=World.Tick,Opening=amount,State=state,LastRecordTick=prior?.LastRecordTick??-1};
                    var last=Knowledge.LastDirect(actor.Id,door.DoorId,"DoorState");
                    if(last==null||last.Value!=state||World.Tick-last.FromTick>=600){ObserveDoorState(actor.Id,door.DoorId,state,World.Tick);current.LastRecordTick=World.Tick;}
                    else current.LastRecordTick=last.FromTick;
                    if(prior!=null){
                        current.Motion=amount-prior.Opening>.002?"Opening":prior.Opening-amount>.002?"Closing":"";
                        if(current.Motion!=""&&current.Motion!=prior.Motion)Knowledge.Observe(actor.Id,new KnownRecord{Kind="Visual",Source=actor.Id,SubjectId=door.DoorId,Predicate="DoorMotion",Value=current.Motion,ProvenanceKey="DOOR_OBSERVATION_L"+World.Loop+"_"+actor.Id+"_"+door.DoorId,Text=NameOf(door.DoorId)+" — 문이 "+(current.Motion=="Opening"?"열리는":"닫히는")+" 움직임을 직접 보았다.",PlaceId=PlaceOf(door.transform.position),Position=P(door.transform.position),FromTick=prior.LastTick,ToTick=World.Tick+1,Supports=new[]{"시야가 이어진 동안 본 문짝의 움직임"},DoesNotEstablish=new[]{"누가 조작했는지", "잠금 조작 여부", "누가 통과했는지", "살인과의 인과관계"}},World.Tick);
                    }
                    next.Add(current);
                }
            }
            doorObservations.Sight=next.ToArray();
            // Locate only new committed events; never copy or rescan the full world history each tick.
            var events=World.Events;int low=0,high=events.Count;
            while(low<high){int mid=low+(high-low)/2;if(events[mid].Sequence<=doorObservations.EventCursor)low=mid+1;else high=mid;}
            for(int i=low;i<events.Count;i++){
                var e=events[i];
                if(e.Type!="DoorResistanceObserved"||!bodies.ContainsKey(e.Actor)||!doors.ContainsKey(e.Target))continue;
                Knowledge.Observe(e.Actor,new KnownRecord{Kind="Touch",Source=e.Actor,SubjectId=e.Target,Predicate="DoorAttempt",Value="WouldNotOpen",ProvenanceKey="DOOR_OBSERVATION_L"+World.Loop+"_"+e.Actor+"_"+e.Target,Text=NameOf(e.Target)+" — 직접 열어 보려 했지만 열리지 않았다.",PlaceId=PlaceOf(doors[e.Target].transform.position),Position=P(doors[e.Target].transform.position),FromTick=e.Tick,ToTick=e.Tick+1,Supports=new[]{"내가 열어 보려 했을 때 통행할 수 없었던 결과"},DoesNotEstablish=new[]{"잠긴 정확한 시각", "누가 잠갔는지", "문 너머 인물", "사건 당시에도 열리지 않았는지"}},World.Tick);
            }
            doorObservations.EventCursor=World.EventSequence;
        }
        void ValidateDoorObservations(MansionSessionSnapshot session)
        {
            var s=session.DoorObservations;
            if(s==null||s.EventCursor<0||s.EventCursor>session.World.Sequence||s.Sight==null||s.Sight.Any(v=>v==null)||s.Sight.Select(v=>v.Observer+"|"+v.DoorId).Distinct().Count()!=s.Sight.Length)throw new System.IO.InvalidDataException("문 관찰 상태가 올바르지 않습니다.");
            foreach(var v in s.Sight)if(!bodies.ContainsKey(v.Observer)||!doors.ContainsKey(v.DoorId)||v.LastTick<0||v.LastTick>session.World.Tick||v.LastRecordTick<0||v.LastRecordTick>v.LastTick||double.IsNaN(v.Opening)||double.IsInfinity(v.Opening)||v.Opening<0||v.Opening>1||!new[]{"Closed","Open","Ajar"}.Contains(v.State)||!new[]{"","Opening","Closing"}.Contains(v.Motion))throw new System.IO.InvalidDataException("문 관찰의 시각이나 대상이 일치하지 않습니다.");
        }
    }
}
