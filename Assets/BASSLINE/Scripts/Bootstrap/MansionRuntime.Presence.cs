using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.Save;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        PresenceObservationState presenceObservations=new PresenceObservationState();
        void AdvancePresenceObservations()
        {
            if(World.Paused)return;
            var previous=presenceObservations.Sight.ToDictionary(p=>p.Observer+"|"+p.Subject);
            var next=new List<PresenceSightProgress>();
            var present=World.Residents.Where(r=>r.Present).ToArray();
            foreach(var observer in present.Where(r=>World.CanAct(r.Id)))foreach(var subject in present.Where(r=>r.Id!=observer.Id)){
                var body=bodies[subject.Id];
                if(Vector3.Distance(bodies[observer.Id].transform.position,body.transform.position)>6||!SeesPassagePerson(observer.Id,body,out bool identity))continue;
                string key=observer.Id+"|"+subject.Id,place=PlaceOf(body.transform.position);
                var position=P(body.transform.position);
                previous.TryGetValue(key,out var prior);
                bool continuous=prior!=null&&prior.LastTick==World.Tick-1&&prior.PlaceId==place&&prior.LastPosition.Distance(position)<=.3;
                if(prior!=null){previous.Remove(key);if(!continuous)PublishPresence(prior);}
                var current=continuous?prior.Copy():new PresenceSightProgress{Observer=observer.Id,Subject=subject.Id,PlaceId=place,SeriesId="PRESENCE_L"+World.Loop+"_S"+(++presenceObservations.Sequence),StartedTick=World.Tick};
                current.LastTick=World.Tick;current.LastPosition=position;
                bool newlyIdentified=identity&&string.IsNullOrEmpty(current.IdentityRecordId);
                if(newlyIdentified){
                    current.IdentityRecordId=Knowledge.Observe(observer.Id,new KnownRecord{Kind="Visual",Source=observer.Id,SubjectId=subject.Id,IdentityConfirmed=true,Predicate="AtPlace",Value=place,PlaceId=place,Position=position,ProvenanceKey=current.SeriesId,FromTick=World.Tick,ToTick=World.Tick+1,
                        Text=NameOf(subject.Id)+"의 얼굴을 "+PlaceLabel(place)+"에서 직접 확인했다.",Supports=new[]{"얼굴을 확인한 순간의 신원과 위치"},DoesNotEstablish=new[]{"보지 못한 시간의 위치나 행동"}},World.Tick);
                }
                if(current.PublishedThroughTick<0||newlyIdentified||current.LastTick-current.PublishedThroughTick>=300)PublishPresence(current);
                next.Add(current);
            }
            // Closing a record uses its last visible tick, never the time sight was lost.
            foreach(var ended in previous.Values)PublishPresence(ended);
            presenceObservations.Sight=next.ToArray();
        }
        void PublishPresence(PresenceSightProgress p)
        {
            bool identified=!string.IsNullOrEmpty(p.IdentityRecordId);
            if(p.PublishedThroughTick==p.LastTick){
                var published=Knowledge.For(p.Observer).Find(p.PublishedRecordId);
                if(published!=null&&published.IdentityConfirmed==identified)return;
            }
            string who=identified?NameOf(p.Subject):"신원을 확인하지 못한 인물";
            p.PublishedRecordId=Knowledge.Observe(p.Observer,new KnownRecord{Kind="Visual",Source=p.Observer,SubjectId=identified?p.Subject:"UNKNOWN_ACTOR",IdentityConfirmed=identified,Predicate="AtPlace",Value=p.PlaceId,PlaceId=p.PlaceId,Position=p.LastPosition,ProvenanceKey=p.SeriesId,FromTick=p.StartedTick,ToTick=p.LastTick+1,
                Text=p.StartedTick==p.LastTick?KoreanText.AsObject(who)+" "+PlaceLabel(p.PlaceId)+"에서 보았다.":KoreanText.AsSubject(who)+" "+PlaceLabel(p.PlaceId)+"에 있는 동안 시야를 놓치지 않고 지켜보았다.",
                Supports=new[]{"표시된 관측 시간 동안의 장소",identified?"연속해서 지켜본 몸과 직접 확인한 얼굴의 연결":"같은 몸을 연속해서 지켜본 범위만 확인", "위치는 마지막으로 본 지점"},DoesNotEstablish=new[]{"시야가 끊긴 뒤의 위치", "관측 전후의 행동이나 의도", "장소 안의 정확한 이동 경로"}},World.Tick);
            p.PublishedThroughTick=p.LastTick;
        }
        void FlushPresenceObservations()
        {
            foreach(var p in presenceObservations.Sight)PublishPresence(p);
            presenceObservations.Sight=Array.Empty<PresenceSightProgress>();
        }
        void ValidatePresenceObservations(MansionSessionSnapshot s,BASSLINE.Knowledge.KnowledgeLedger knowledge)
        {
            var state=s.PresenceObservations;
            if(state==null||state.Sequence<0||state.Sight==null||state.Sight.Any(p=>p==null)||state.Sight.Select(p=>p.Observer+"|"+p.Subject).Distinct().Count()!=state.Sight.Length||state.Sight.Select(p=>p.SeriesId).Distinct().Count()!=state.Sight.Length)throw new System.IO.InvalidDataException("연속 목격의 저장 상태가 올바르지 않습니다.");
            string prefix="PRESENCE_L"+s.World.Loop+"_S";
            foreach(var p in state.Sight){
                if(!bodies.ContainsKey(p.Observer)||!bodies.ContainsKey(p.Subject)||p.Observer==p.Subject||p.StartedTick<0||p.LastTick<p.StartedTick||p.LastTick>s.World.Tick||!p.LastPosition.Finite()||PlaceOf(V(p.LastPosition))!=p.PlaceId||string.IsNullOrEmpty(p.SeriesId)||!p.SeriesId.StartsWith(prefix,StringComparison.Ordinal)||!long.TryParse(p.SeriesId.Substring(prefix.Length),out var sequence)||sequence<=0||sequence>state.Sequence||p.PublishedThroughTick<p.StartedTick||p.PublishedThroughTick>p.LastTick)throw new System.IO.InvalidDataException("연속 목격의 대상·장소·시간이 일치하지 않습니다.");
                bool identified=!string.IsNullOrEmpty(p.IdentityRecordId);
                if(identified){
                    var face=knowledge.For(p.Observer).Find(p.IdentityRecordId);
                    if(face==null||!face.Direct||face.Source!=p.Observer||face.Kind!="Visual"||!face.IdentityConfirmed||face.SubjectId!=p.Subject||face.Predicate!="AtPlace"||face.PlaceId!=p.PlaceId||face.Value!=p.PlaceId||face.ProvenanceKey!=p.SeriesId||face.FromTick<p.StartedTick||face.FromTick>p.PublishedThroughTick||face.ToTick!=face.FromTick+1)throw new System.IO.InvalidDataException("연속 목격 중 얼굴을 확인한 원문이 없습니다.");
                }
                var record=knowledge.For(p.Observer).Find(p.PublishedRecordId);
                if(record==null||!record.Direct||record.Source!=p.Observer||record.Kind!="Visual"||record.IdentityConfirmed!=identified||record.SubjectId!=(identified?p.Subject:"UNKNOWN_ACTOR")||record.Predicate!="AtPlace"||record.PlaceId!=p.PlaceId||record.Value!=p.PlaceId||record.ProvenanceKey!=p.SeriesId||record.FromTick!=p.StartedTick||record.ToTick!=p.PublishedThroughTick+1||p.PublishedThroughTick==p.LastTick&&record.Position.Distance(p.LastPosition)>.00001)throw new System.IO.InvalidDataException("연속 목격과 수첩에 남긴 원문이 일치하지 않습니다.");
            }
        }
    }
}
