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
        bool SeesPassageBodyPoint(string observer,FixtureActorBody subject,Vector3 point)
        {
            var eye=bodies[observer];Vector3 origin=observer=="CH_01"?cameraView.transform.position:eye.transform.position+Vector3.up*eye.Height*.88f;
            Vector3 delta=point-origin,forward=observer=="CH_01"?cameraView.transform.forward:eye.Head.forward;
            if(delta.sqrMagnitude<.0001f||delta.magnitude>6||Vector3.Dot(forward,delta.normalized)<.6f)return false;
            if(observer=="CH_01"){
                var viewport=cameraView.WorldToViewportPoint(point);
                if(viewport.z<cameraView.nearClipPlane||viewport.z>cameraView.farClipPlane||viewport.x<0||viewport.x>1||viewport.y<0||viewport.y>1)return false;
            }
            int count=Physics.RaycastNonAlloc(origin,delta.normalized,rayHits,delta.magnitude+.01f,~0,QueryTriggerInteraction.Ignore);
            if(count==rayHits.Length)return false;
            var hit=Enumerable.Range(0,count).Select(i=>rayHits[i]).Where(h=>!h.transform.IsChildOf(eye.transform)).OrderBy(h=>h.distance).FirstOrDefault();
            return hit.collider&&hit.transform.IsChildOf(subject.transform);
        }
        bool SeesPassagePerson(string observer,FixtureActorBody subject,out bool identity)
        {
            identity=false;
            if(!subject||!subject.Head||!subject.Capsule||!subject.Capsule.enabled||!subject.gameObject.activeInHierarchy)return false;
            if(!subject.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.sharedMaterial&&(observer!="CH_01"||(cameraView.cullingMask&(1<<r.gameObject.layer))!=0)))return false;
            Vector3 chest=subject.transform.position+Vector3.up*subject.Height*.65f;
            if(!SeesPassageBodyPoint(observer,subject,chest))return false;
            var viewer=bodies[observer];Vector3 eye=observer=="CH_01"?cameraView.transform.position:viewer.transform.position+Vector3.up*viewer.Height*.88f;
            identity=Vector3.Distance(eye,subject.Head.position)<=4&&Vector3.Dot(subject.Head.forward,(eye-subject.Head.position).normalized)>.35f&&SeesPassageBodyPoint(observer,subject,subject.Head.position);
            return true;
        }
        bool SeesPassageFrame(string observer,MansionConnection door)
        {
            Vector3 header=door.transform.position+Vector3.up*door.ClearHeight;
            if(observer=="CH_01"){
                var viewport=cameraView.WorldToViewportPoint(header);
                if(viewport.z<cameraView.nearClipPlane||viewport.z>cameraView.farClipPlane||viewport.x<0||viewport.x>1||viewport.y<0||viewport.y>1)return false;
            }
            return Visible(observer,header,6,true);
        }
        bool PassageZone(FixtureActorBody body,MansionConnection door,Vector3 point,out int side)
        {
            side=0;
            if(door.Kind!="Door"||door.NormalAToB.sqrMagnitude<.9f||door.ClearHeight<body.Height||!P(point).Finite())return false;
            Vector3 normal=door.NormalAToB.normalized,delta=point-door.transform.position;
            float radius=body.Capsule.radius*Mathf.Max(Mathf.Abs(body.transform.lossyScale.x),Mathf.Abs(body.transform.lossyScale.z));
            float distance=Vector3.Dot(delta,normal),lateral=Mathf.Abs(Vector3.Dot(delta,Vector3.Cross(Vector3.up,normal)));
            if(Mathf.Abs(distance)>1.5f||Mathf.Abs(delta.y)>.2f||lateral+radius>door.Width*.5f+.02f)return false;
            float clearance=radius+.12f;side=distance>clearance?1:distance< -clearance?-1:0;return true;
        }
        void AdvanceDoorPassages()
        {
            if(World.Paused)return;
            var next=new List<DoorPassageProgress>();
            var present=World.Residents.Where(r=>r.Present).ToArray();
            foreach(var observer in present.Where(r=>World.CanAct(r.Id))){
                foreach(var subject in present.Where(r=>r.Id!=observer.Id)){
                    var body=bodies[subject.Id];
                    if(Vector3.Distance(bodies[observer.Id].transform.position,body.transform.position)>6||!SeesPassagePerson(observer.Id,body,out bool identity))continue;
                    foreach(var door in doors.Values){
                        if(!door.gameObject.activeInHierarchy||!PassageZone(body,door,body.transform.position,out int side)||!SeesPassageFrame(observer.Id,door))continue;
                        var prior=doorObservations.Passages.FirstOrDefault(p=>p.Observer==observer.Id&&p.Subject==subject.Id&&p.DoorId==door.DoorId&&p.LastTick==World.Tick-1);
                        // A load, spawn, occlusion or discontinuous relocation cannot become a crossing.
                        if(prior!=null&&prior.LastPosition.Distance(P(body.transform.position))>.3)prior=null;
                        var current=prior?.Copy()??new DoorPassageProgress{Observer=observer.Id,Subject=subject.Id,DoorId=door.DoorId,StartedTick=World.Tick,OriginSide=side};
                        current.LastTick=World.Tick;current.LastPosition=P(body.transform.position);
                        if(identity&&!current.IdentitySeen){
                            current.IdentityRecordId=Knowledge.Observe(observer.Id,new KnownRecord{Kind="Visual",Source=observer.Id,SubjectId=subject.Id,IdentityConfirmed=true,Predicate="AtPlace",Value=PlaceOf(body.transform.position),PlaceId=PlaceOf(body.transform.position),Position=P(body.transform.position),ProvenanceKey="DOOR_OBSERVATION_L"+World.Loop+"_"+observer.Id+"_"+door.DoorId,FromTick=World.Tick,ToTick=World.Tick+1,Text=NameOf(subject.Id)+"의 얼굴을 "+NameOf(door.DoorId)+" 가까이에서 확인했다.",Supports=new[]{"직접 얼굴을 확인한 인물과 현재 위치"},DoesNotEstablish=new[]{"출입 완료", "문의 조작", "관측 전후 행동"}},World.Tick);
                            current.IdentitySeen=true;
                        }
                        float signed=Vector3.Dot(body.transform.position-door.transform.position,door.NormalAToB.normalized);
                        if(current.OriginSide!=0){
                            if(signed*current.OriginSide>=0)current.CrossedTick=-1;
                            else if(current.CrossedTick<0)current.CrossedTick=World.Tick;
                        }
                        if(current.OriginSide==0&&side!=0){current.OriginSide=side;current.StartedTick=World.Tick;current.CrossedTick=-1;}
                        else if(prior!=null&&side!=0&&side==current.OriginSide){current.StartedTick=World.Tick;current.CrossedTick=-1;}
                        else if(prior!=null&&side!=0&&side==-current.OriginSide){
                            if(current.CrossedTick>=0)ObservePassage(current);
                            current.OriginSide=side;current.StartedTick=World.Tick;current.CrossedTick=-1;
                        }
                        next.Add(current);
                    }
                }
            }
            doorObservations.Passages=next.ToArray();
        }
        void ObservePassage(DoorPassageProgress p)
        {
            bool identified=p.IdentitySeen;string actor=identified?p.Subject:"UNKNOWN_ACTOR";
            var door=doors[p.DoorId];
            Knowledge.Observe(p.Observer,new KnownRecord{Kind="Visual",Source=p.Observer,SubjectId=actor,IdentityConfirmed=identified,Predicate="PassedDoor",Value=p.DoorId,ProvenanceKey="DOOR_OBSERVATION_L"+World.Loop+"_"+p.Observer+"_"+p.DoorId,
                Text=KoreanText.AsSubject(identified?NameOf(p.Subject):"신원을 확인하지 못한 인물")+" "+NameOf(p.DoorId)+" 한쪽에서 반대편으로 완전히 넘어가는 과정을 계속 보았다.",Position=P(door.transform.position),PlaceId=PlaceOf(door.transform.position),FromTick=p.CrossedTick,ToTick=p.CrossedTick+1,
                Supports=new[]{identified?"연속해서 지켜본 인물의 신원과 이 문의 통과":"연속해서 지켜본 신원 미확인 인물의 문 통과", "문턱을 지나는 것을 본 시각"},DoesNotEstablish=new[]{"이 문을 여닫거나 잠근 사람", "다른 문을 이용한 사람", "관측 전후의 출입", "유일한 출입자", "사용한 물건·고의·사건 결과"}},World.Tick);
        }
        void ValidateDoorPassages(MansionSessionSnapshot s,BASSLINE.Knowledge.KnowledgeLedger knowledge)
        {
            var entries=s.DoorObservations.Passages;
            if(entries==null||entries.Any(p=>p==null)||entries.Select(p=>p.Observer+"|"+p.Subject+"|"+p.DoorId).Distinct().Count()!=entries.Length)throw new System.IO.InvalidDataException("출입을 지켜보던 상태가 올바르지 않습니다.");
            foreach(var p in entries){
                if(p.Observer==p.Subject||!bodies.ContainsKey(p.Observer)||!bodies.ContainsKey(p.Subject)||!doors.ContainsKey(p.DoorId)||p.StartedTick<0||p.LastTick<p.StartedTick||p.LastTick>s.World.Tick||p.CrossedTick< -1||p.CrossedTick>=0&&(p.CrossedTick<p.StartedTick||p.CrossedTick>p.LastTick||p.OriginSide==0)||p.OriginSide< -1||p.OriginSide>1||!p.LastPosition.Finite()||!PassageZone(bodies[p.Subject],doors[p.DoorId],V(p.LastPosition),out _))throw new System.IO.InvalidDataException("출입 관찰의 대상·공간·시각이 일치하지 않습니다.");
                float signed=Vector3.Dot(V(p.LastPosition)-doors[p.DoorId].transform.position,doors[p.DoorId].NormalAToB.normalized);
                if(p.CrossedTick>=0&&signed*p.OriginSide>=0)throw new System.IO.InvalidDataException("문턱을 지난 관측과 마지막 위치가 다릅니다.");
                if(p.IdentitySeen){
                    var fact=knowledge.For(p.Observer).Find(p.IdentityRecordId);
                    if(fact==null||fact.Source!=p.Observer||!fact.Direct||fact.Kind!="Visual"||!fact.IdentityConfirmed||fact.SubjectId!=p.Subject||fact.Predicate!="AtPlace"||fact.FromTick>p.LastTick||fact.ProvenanceKey!="DOOR_OBSERVATION_L"+s.World.Loop+"_"+p.Observer+"_"+p.DoorId)throw new System.IO.InvalidDataException("출입 중 신원을 확인한 직접 관측이 없습니다.");
                }else if(!string.IsNullOrEmpty(p.IdentityRecordId))throw new System.IO.InvalidDataException("신원 미확인 관찰에 신원 자료가 붙어 있습니다.");
            }
        }
    }
}
