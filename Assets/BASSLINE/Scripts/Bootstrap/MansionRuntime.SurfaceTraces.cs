using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
using BASSLINE.World.Mansion;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        MansionTraceSource[] traceSources=Array.Empty<MansionTraceSource>();
        MansionTraceSurface[] traceSurfaces=Array.Empty<MansionTraceSurface>();
        MansionSurfaceTraces surfaceTraces;
        SurfaceReadingProgress[] surfaceReadings=Array.Empty<SurfaceReadingProgress>();
        readonly Dictionary<string,MansionTracePatternVisual> traceVisuals=new Dictionary<string,MansionTracePatternVisual>();
        string TraceSourceId(MansionTraceSource s)=>s.GetComponent<FixtureObjectBody>().ObjectId;
        string TraceSurfaceId(MansionTraceSurface s)=>s.GetComponent<FixtureTarget>().StableId;
        void InitializeSurfaceTraces()
        {
            traceSources=FindObjectsByType<MansionTraceSource>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            traceSurfaces=FindObjectsByType<MansionTraceSurface>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            foreach(var source in traceSources)if(source.Face)source.Face.Build(source.Face.Pattern,source.Face.GetComponent<MeshRenderer>().sharedMaterial);
            surfaceTraces=new MansionSurfaceTraces(World.Loop,World.Tick);
        }
        void AdvanceSurfaceTraces()
        {
            if(World.Paused)return;Physics.SyncTransforms();surfaceTraces.BeginTick(World.Loop,World.Tick);
            foreach(var source in traceSources.Where(s=>s&&s.isActiveAndEnabled&&s.TransfersPigment&&s.Face&&s.Face.gameObject.activeInHierarchy)){
                string id=TraceSourceId(source);var item=World.Objects.FirstOrDefault(o=>o.Id==id);
                if(item==null||item.Location!="Hand"||!World.CanAct(item.Owner))continue;
                if(source.transform.lossyScale!=Vector3.one||source.Face.transform.lossyScale!=Vector3.one)continue;
                var face=source.Face;var owner=bodies[item.Owner];Vector3 origin=face.transform.position-face.transform.forward*.01f;
                int count=Physics.RaycastNonAlloc(origin,face.transform.forward,rayHits,.035f,~0,QueryTriggerInteraction.Ignore);if(count==rayHits.Length)continue;
                var hit=Enumerable.Range(0,count).Select(i=>rayHits[i]).Where(h=>!h.transform.IsChildOf(source.transform)&&!h.transform.IsChildOf(owner.transform)).OrderBy(h=>h.distance).FirstOrDefault();
                if(!hit.collider||hit.distance>.012f)continue;
                var surface=traceSurfaces.FirstOrDefault(s=>s&&s.isActiveAndEnabled&&s.ContactSurface==hit.collider&&s.TraceMaterial);
                if(!surface||surface.transform.lossyScale!=Vector3.one||Vector3.Dot(hit.normal,-face.transform.forward)<.85f)continue;
                // Require the whole face on this surface, not a projected print over an edge.
                float radius=face.Pattern.SizeMm*.0005f;bool supported=true;
                foreach(var offset in new[]{new Vector2(-1,-1),new Vector2(-1,1),new Vector2(1,-1),new Vector2(1,1)}){
                    var corner=origin+(face.transform.right*offset.x+face.transform.up*offset.y)*radius;
                    if(!surface.ContactSurface.Raycast(new Ray(corner,face.transform.forward),out var cornerHit,.035f)||cornerHit.distance>.012f||Vector3.Dot(cornerHit.normal,hit.normal)<.98f){supported=false;break;}
                }
                if(!supported)continue;
                var pattern=face.Pattern.Copy();pattern.Mask=SurfacePattern.Mirror(pattern.Mask);
                var tangent=Vector3.ProjectOnPlane(face.transform.up,hit.normal).normalized;
                var mark=surfaceTraces.Deposit(id,TraceSurfaceId(surface),P(surface.transform.InverseTransformPoint(hit.point)),P(surface.transform.InverseTransformDirection(hit.normal).normalized),P(surface.transform.InverseTransformDirection(tangent).normalized),pattern);
                PresentSurfaceMark(mark,surface);
                foreach(var incident in incidents.All())incident.BindPhysicalTrace(World,mark,P(hit.point));
                var press=PressForTool(id);if(press!=null&&press.SurfaceId==mark.SurfaceId){press.MarkId=mark.Id;ObserveToolPressContact(press,source,surface,hit.point);}
            }
            AdvanceResidentSurfaceReading();
        }
        void PresentSurfaceMark(SurfaceTrace mark,MansionTraceSurface surface)
        {
            if(traceVisuals.ContainsKey(mark.Id))return;
            var go=new GameObject(mark.Id);go.transform.SetParent(surface.transform,false);var normal=V(mark.LocalNormal);
            go.transform.localPosition=V(mark.LocalPoint)+normal*.0015f;
            go.transform.localRotation=Quaternion.LookRotation(normal,V(mark.LocalUp));
            var visual=go.AddComponent<MansionTracePatternVisual>();visual.Build(mark.Pattern,surface.TraceMaterial);traceVisuals.Add(mark.Id,visual);
        }
        void RestoreSurfaceTraces(BASSLINE.Save.MansionSessionSnapshot s)
        {
            surfaceReadings=s.SurfaceReadings.Select(r=>r.Copy()).ToArray();
            surfaceTraces=MansionSurfaceTraces.Restore(s.SurfaceTraces,World.Loop,World.Tick,traceSurfaces.Select(TraceSurfaceId).ToArray(),traceSources.Select(TraceSourceId).ToArray());
            foreach(var visual in traceVisuals.Values)if(visual){visual.gameObject.SetActive(false);Destroy(visual.gameObject);}traceVisuals.Clear();
            foreach(var mark in surfaceTraces.Capture().Marks)PresentSurfaceMark(mark,traceSurfaces.Single(x=>TraceSurfaceId(x)==mark.SurfaceId));
        }
        bool SeesPattern(string observer,MansionTracePatternVisual visual)
        {
            if(!visual||!visual.gameObject.activeInHierarchy||!World.CanAct(observer))return false;
            var renderer=visual.GetComponent<MeshRenderer>();if(!renderer||!renderer.enabled||!renderer.sharedMaterial||!visual.GetComponent<MeshFilter>().sharedMesh)return false;
            var body=bodies[observer];Vector3 origin=body.transform.position+Vector3.up*body.Height*.88f,delta=visual.transform.position-origin;
            Vector3 forward=observer=="CH_01"?cameraView.transform.forward:body.Head.forward;
            if(delta.magnitude>1.5f||Vector3.Dot(forward,delta.normalized)<.6f||Vector3.Dot(visual.transform.forward,-delta.normalized)<.2f)return false;
            int count=Physics.RaycastNonAlloc(origin,delta.normalized,rayHits,Mathf.Max(0,delta.magnitude-.002f),~0,QueryTriggerInteraction.Ignore);if(count==rayHits.Length)return false;
            for(int i=0;i<count;i++)if(!rayHits[i].transform.IsChildOf(body.transform))return false;
            return true;
        }
        string ObservedSurfaceSubject(string observer,string target)
            =>!bodies.ContainsKey(target)||observer==target||Identifies(observer,target)?target:"UNKNOWN_SURFACE";
        string ObserveSurfacePatterns(string observer,string target,string requiredRoot="")
        {
            string last="";
            void Receive(MansionTracePatternVisual visual,string root,string predicate,string description)
            {
                if(requiredRoot!=""&&requiredRoot!=root||!SeesPattern(observer,visual))return;
                string observedSubject=ObservedSurfaceSubject(observer,target);
                var prior=Knowledge.For(observer).Records().FirstOrDefault(r=>r.Direct&&r.ProvenanceKey==root&&r.Value==visual.Pattern.Key&&r.SubjectId==observedSubject);
                if(prior!=null){last=prior.Id;return;}
                string surfaceName=observedSubject==target?NameOf(target):"얼굴을 확인하지 못한 인물의 표면";
                last=Knowledge.Observe(observer,new KnownRecord{Kind="Visual",Source=observer,SubjectId=observedSubject,Predicate=predicate,Value=visual.Pattern.Key,ProvenanceKey=root,Text=surfaceName+" — "+description+"\n"+visual.Pattern.Describe(),PlaceId=PlaceOf(visual.transform.position),Position=P(visual.transform.position),FromTick=World.Tick,ToTick=World.Tick+1,IdentityConfirmed=false,Supports=new[]{"살펴본 표면의 색·무늬·폭"},DoesNotEstablish=new[]{"자국이 생긴 시각", "이 물건을 사용한 사람", "살인 실행·고의·결과와의 연결", "동일한 무늬를 가진 다른 물건의 부재"}},World.Tick);
            }
            foreach(var source in traceSources.Where(s=>s&&s.isActiveAndEnabled&&TraceSourceId(s)==target&&s.Face))Receive(source.Face,"PATTERN_FACE_L"+World.Loop+"_"+target,"SurfacePattern","접촉면을 가까이서 살펴봤다.");
            foreach(var mark in surfaceTraces.Capture().Marks.Where(m=>m.SurfaceId==target))if(traceVisuals.TryGetValue(mark.Id,out var visual))Receive(visual,mark.Id,"ContactPattern","표면에 묻은 자국을 가까이서 살펴봤다.");
            return last;
        }
        System.Collections.Generic.IEnumerable<(string Target,string Root,MansionTracePatternVisual Visual)> SurfaceFeatures(string target="")
        {
            foreach(var source in traceSources.Where(s=>s&&s.isActiveAndEnabled&&s.Face&&(target==""||TraceSourceId(s)==target)))
                yield return (TraceSourceId(source),"PATTERN_FACE_L"+World.Loop+"_"+TraceSourceId(source),source.Face);
            foreach(var mark in surfaceTraces.Capture().Marks.Where(m=>target==""||m.SurfaceId==target))
                if(traceVisuals.TryGetValue(mark.Id,out var visual))yield return (mark.SurfaceId,mark.Id,visual);
        }
        void AdvanceResidentSurfaceReading()
        {
            var candidates=SurfaceFeatures().ToArray();
            var pending=new List<SurfaceReadingProgress>();
            foreach(var resident in World.Residents.Where(r=>r.Id!="CH_01"&&World.CanAct(r.Id)&&r.Activity=="Examine"&&r.Phase=="Performing")){
                if(ruleReadersThisTick.Contains(resident.Id)||PlayerTalkingTo(resident.Id)||incidents.Controls(resident.Id))continue;
                var own=Knowledge.For(resident.Id).Records();
                var previous=surfaceReadings.FirstOrDefault(r=>r.Observer==resident.Id&&r.LastTick==World.Tick-1);
                var visible=candidates.Where(c=>SeesPattern(resident.Id,c.Visual)&&!own.Any(r=>r.Direct&&r.ProvenanceKey==c.Root&&r.Value==c.Visual.Pattern.Key&&r.SubjectId==ObservedSurfaceSubject(resident.Id,c.Target))).OrderBy(c=>c.Root==previous?.Root?0:1).ThenBy(c=>Vector3.Distance(bodies[resident.Id].transform.position,c.Visual.transform.position)).ThenBy(c=>c.Root,StringComparer.Ordinal).ToArray();
                if(visible.Length==0)continue;var candidate=visible[0];
                var progress=previous!=null&&previous.Root==candidate.Root&&previous.Pattern==candidate.Visual.Pattern.Key?previous:new SurfaceReadingProgress{Observer=resident.Id,Target=candidate.Target,Root=candidate.Root,Pattern=candidate.Visual.Pattern.Key,StartedTick=World.Tick};
                progress.LastTick=World.Tick;progress.ElapsedTicks++;
                if(progress.ElapsedTicks>=120)ObserveSurfacePatterns(resident.Id,progress.Target,progress.Root);else pending.Add(progress);
            }
            surfaceReadings=pending.ToArray();
        }
        void ValidateSurfaceReadings(BASSLINE.Save.MansionSessionSnapshot s)
        {
            if(s.SurfaceReadings==null||s.SurfaceReadings.Select(r=>r?.Observer).Distinct().Count()!=s.SurfaceReadings.Length)throw new System.IO.InvalidDataException("표면을 살펴보던 상태가 올바르지 않습니다.");
            foreach(var r in s.SurfaceReadings){
                bool face=traceSources.Any(t=>TraceSourceId(t)==r?.Target&&r.Root=="PATTERN_FACE_L"+s.World.Loop+"_"+r.Target&&t.Face&&t.Face.Pattern.Key==r.Pattern);
                bool mark=s.SurfaceTraces.Marks.Any(m=>m.Id==r?.Root&&m.SurfaceId==r.Target&&m.Pattern.Key==r.Pattern);
                if(r==null||r.Observer=="CH_01"||!bodies.ContainsKey(r.Observer)||!face&&!mark||r.StartedTick<0||r.LastTick>s.World.Tick||r.LastTick<r.StartedTick||r.ElapsedTicks!=r.LastTick-r.StartedTick+1||r.ElapsedTicks<1||r.ElapsedTicks>=120)throw new System.IO.InvalidDataException("표면 관찰의 대상이나 시간이 일치하지 않습니다.");
            }
        }
        BASSLINE.Save.MansionSessionSnapshot UpgradeSurfaceTraceContent(BASSLINE.Save.MansionSessionSnapshot s)
        {
            // Existing sessions start collecting from now; no retrospective incident marks.
            if(s?.World!=null&&(s.OptionalObjects&32768)==0){s.SurfaceReadings=Array.Empty<SurfaceReadingProgress>();s.SurfaceTraces=new MansionSurfaceTraces(s.World.Loop,s.World.Tick).Capture();s.OptionalObjects|=32768;}
            return s;
        }
    }
}
