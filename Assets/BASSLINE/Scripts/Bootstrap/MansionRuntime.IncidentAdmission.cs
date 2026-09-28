using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
using BASSLINE.World.Mansion;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IMansionIncidentAdmission
    {
        MansionIncidentSite[] incidentSites=Array.Empty<MansionIncidentSite>();
        void InitializeIncidentSites()
        {
            incidentSites=FindObjectsByType<MansionIncidentSite>();
            rulePlates=FindObjectsByType<MansionIncidentRulePlate>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        }
        IncidentAdmissionInput IMansionIncidentAdmission.ReadCauseAdmission(MansionCaseSettings settings)
        {
            var definition=settings.Execution?.Definition;
            var input=new IncidentAdmissionInput{CaseId=settings.Id,TemplateId=settings.Template,DefinitionRevision=definition?.Revision??"",Tick=World.Tick,ContentReviewed=definition?.ReviewStatus=="Reviewed",ActionRegistered=definition?.ActionId=="ContactOutcome",WithinResultDelayBound=settings.DelayTicks>0&&settings.DelayTicks<=36000};
            var incident=incidents.Find(settings.Id)?.Capture();
            if(definition==null||incident==null||incident.CauseTick>=0||incident.RiskContinuedTick<0||string.IsNullOrEmpty(incident.ActivationId)||!World.CanAct(settings.ActorId)||!World.CanAct(settings.TargetId)||!HasContact(settings.ActorId,settings.TargetId,settings.ObjectId,out _))return input;
            var site=incidentSites.FirstOrDefault(s=>s&&s.isActiveAndEnabled&&s.DeviceId==settings.ObjectId&&s.GetComponent<MansionIncidentRulePlate>().Definition.Id==definition.Id&&s.GetComponent<MansionIncidentRulePlate>().Definition.Revision==definition.Revision);
            if(!site||!nodes.Any(n=>n.Id==site.PublicAccessNode))return input;
            var rule=site.GetComponent<MansionIncidentRulePlate>();string ruleId=rule.GetComponent<FixtureTarget>().StableId;
            if(!rule.isActiveAndEnabled||rule.Definition.PublicRule!=definition.PublicRule||!ReadableAnchor(site.RuleReadNode,ruleId)||AdmissionPath(site.PublicAccessNode,site.RuleReadNode,Array.Empty<string>()).Length==0)return input;
            var actorDisplay=((IMansionWitnessDisplay)this).ReadIncidentDisplay(settings.ActorId,settings.ObjectId);
            if(!DisplayMatches(actorDisplay,incident))return input;
            input.PhysicalOpportunity=true;
            var routes=new List<IncidentEvidenceRoute>();
            foreach(var risk in incident.RiskWitnesses.Where(w=>w.LastTick==World.Tick&&w.Observer!="CH_01"&&w.Observer!=settings.ActorId&&w.Observer!=settings.TargetId&&World.CanAct(w.Observer)&&!string.IsNullOrEmpty(w.ContinuationReceiptId))){
                var warning=incident.Receipts.FirstOrDefault(r=>r.Id==risk.WarningReceiptId&&r.Observer==risk.Observer);
                var continued=incident.Receipts.FirstOrDefault(r=>r.Id==risk.ContinuationReceiptId&&r.Observer==risk.Observer);
                if(warning==null||continued==null||!RecordMatches(warning.Record,incident)||!RecordMatches(continued.Record,incident)||!DisplayMatches(((IMansionWitnessDisplay)this).ReadIncidentDisplay(risk.Observer,settings.ObjectId),incident)||!CanSee(risk.Observer,settings.ActorId)||!CanSee(risk.Observer,settings.TargetId)||!Identifies(risk.Observer,settings.ActorId)||!Identifies(risk.Observer,settings.TargetId))continue;
                var witnessNode=nodes.Where(n=>n.Room==PlaceOf(bodies[risk.Observer].transform.position)&&n.Position.Distance(World.Resident(risk.Observer).Position)<2.5).OrderBy(n=>n.Position.Distance(World.Resident(risk.Observer).Position)).ThenBy(n=>n.Id,StringComparer.Ordinal).FirstOrDefault();
                if(witnessNode==null||!ReadableAnchor(witnessNode.Id,risk.Observer))continue;
                var path=AdmissionPath(site.PublicAccessNode,witnessNode.Id,Array.Empty<string>());if(path.Length==0)continue;
                routes.Add(new IncidentEvidenceRoute{Id="WITNESS_"+risk.Observer,RootIds=new[]{continued.Record.ProvenanceKey},AccessDependencies=PathDoorDependencies(path).Concat(new[]{"PERSON_"+risk.Observer}).ToArray(),AccessNodes=path,ObservationIds=new[]{warning.Id,continued.Id},Responsibilities=(string[])IncidentAdmission.RequiredResponsibilities.Clone(),Available=true,Preserved=false,RequiresPerson=true,RequiresFavor=true});
            }
            // Only the direct-player-witness variant may use the already owned notebook.
            // Hidden events still need their own authored independent physical proof route.
            if(World.CanAct("CH_01")&&incident.RiskWitnesses.Any(w=>w.Observer=="CH_01"&&w.LastTick==World.Tick)&&
                DisplayMatches(((IMansionWitnessDisplay)this).ReadIncidentDisplay("CH_01",settings.ObjectId),incident)){
                var journal=actionJournal?.Capture();
                foreach(var station in site.Journals.Where(s=>s&&s.isActiveAndEnabled&&s.Powered&&s.DeviceId==settings.ObjectId&&actionStations.Contains(s))){
                    string stationId=station.GetComponent<FixtureTarget>().StableId;
                    if(!ReadableAnchor(station.ReaderNode,stationId))continue;
                    // Try a route excluding each witness route's doors, rather than rejecting
                    // a valid alternate approach because the shortest path shared a doorway.
                    foreach(var witnessRoute in routes.Where(r=>r.RequiresPerson).ToArray()){
                        var excluded=witnessRoute.AccessDependencies.Where(x=>x.StartsWith("DOOR_",StringComparison.Ordinal)).Select(x=>x.Substring(5)).ToArray();
                        var path=AdmissionPath(site.PublicAccessNode,station.ReaderNode,excluded);
                        var route=PlayerWitnessAdmission.Read(Knowledge.For("CH_01"),incident,journal,station.SourceId,path,PathDoorDependencies(path),World.Tick);
                        if(route==null)continue;
                        route.Id+="_VIA_"+witnessRoute.Id;
                        routes.Add(route);
                    }
                }
            }
            input.Routes=routes.ToArray();return input;
        }
        static bool DisplayMatches(ObservedActionDisplay display,MansionIncidentSnapshot incident)=>display!=null&&display.ActivationId==incident.ActivationId&&display.TargetId==incident.Settings.TargetId&&display.DefinitionId==incident.Settings.Execution.Definition.Id&&display.Revision==incident.Settings.Execution.Definition.Revision;
        static bool RecordMatches(KnownRecord record,MansionIncidentSnapshot incident)=>record.ActivationId==incident.ActivationId&&record.DeviceId==incident.Settings.ObjectId&&record.OutcomeTarget==incident.Settings.TargetId&&record.ActionDefinition==incident.Settings.Execution.Definition.Id&&record.ActionRevision==incident.Settings.Execution.Definition.Revision;
        bool ReadableAnchor(string nodeId,string subject)
        {
            var node=nodes.FirstOrDefault(n=>n.Id==nodeId);
            if(node==null||!targets.TryGetValue(subject,out var target)||!target.gameObject.activeInHierarchy)return false;
            Vector3 origin=V(node.Position)+Vector3.up*bodies["CH_01"].Height*.88f,point=SubjectPoint(subject),delta=point-origin;
            var plate=target.GetComponent<MansionIncidentRulePlate>();
            if(plate)return RulePlateReadableFrom(plate,origin,(plate.ReadPosition-origin).normalized);
            if(Vector3.Distance(V(node.Position),point)>2.5f)return false;
            int count=Physics.RaycastNonAlloc(origin,delta.normalized,rayHits,Mathf.Max(0,delta.magnitude-.08f),~0,QueryTriggerInteraction.Ignore);
            if(count==rayHits.Length)return false;
            for(int i=0;i<count;i++)if(!rayHits[i].transform.IsChildOf(target.transform)&&!rayHits[i].collider.GetComponentInParent<FixtureActorBody>())return false;
            return true;
        }
        string[] AdmissionPath(string from,string to,string[] excludedDoors)
        {
            if(!nodes.Any(n=>n.Id==from)||!nodes.Any(n=>n.Id==to)||!AdmissionPositionClear(V(nodes.First(n=>n.Id==from).Position),""))return Array.Empty<string>();
            var previous=new Dictionary<string,string>{{from,""}};var queue=new Queue<string>();queue.Enqueue(from);
            while(queue.Count>0){
                string current=queue.Dequeue();if(current==to)break;
                foreach(var edge in edges.Where(e=>e.From==current).OrderBy(e=>e.To,StringComparer.Ordinal)){
                    if(previous.ContainsKey(edge.To))continue;
                    if(edge.Door!=""){
                        var door=World.Doors.FirstOrDefault(d=>d.Id==edge.Door);
                        if(door==null||door.Locked||door.OwnerId!=""||excludedDoors.Contains(edge.Door))continue;
                    }
                    if(!AdmissionSegmentClear(edge.From,edge.To,edge.Door))continue;
                    previous[edge.To]=current;queue.Enqueue(edge.To);
                }
            }
            if(!previous.ContainsKey(to))return Array.Empty<string>();
            var path=new List<string>();for(string cursor=to;cursor!="";cursor=previous[cursor])path.Add(cursor);path.Reverse();return path.ToArray();
        }
        string[] PathDoorDependencies(string[] path)=>path.Zip(path.Skip(1),(a,b)=>edges.First(e=>e.From==a&&e.To==b).Door).Where(id=>!string.IsNullOrEmpty(id)).Select(id=>"DOOR_"+id).Distinct().ToArray();
        bool AdmissionSegmentClear(string from,string to,string doorId)
        {
            Vector3 start=V(nodes.First(n=>n.Id==from).Position),end=V(nodes.First(n=>n.Id==to).Position),delta=end-start;
            if(!AdmissionPositionClear(end,doorId))return false;
            int count=Physics.CapsuleCastNonAlloc(start+Vector3.up*.32f,start+Vector3.up*(bodies["CH_01"].Height-.3f),.275f,delta.normalized,rayHits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            if(count==rayHits.Length)return false;
            for(int i=0;i<count;i++){
                var hit=rayHits[i].collider;if(hit.GetComponentInParent<FixtureActorBody>())continue;
                if(AdmissionDoorLeaf(hit,doorId))continue;
                return false;
            }
            int steps=Math.Max(1,Mathf.CeilToInt(delta.magnitude/.3f));
            for(int step=0;step<=steps;step++){
                var point=Vector3.Lerp(start,end,(float)step/steps);
                int floors=Physics.RaycastNonAlloc(point+Vector3.up*.4f,Vector3.down,rayHits,.8f,~0,QueryTriggerInteraction.Ignore);
                if(floors==rayHits.Length||!Enumerable.Range(0,floors).Any(i=>!rayHits[i].collider.GetComponentInParent<FixtureActorBody>()&&rayHits[i].normal.y>.5f&&Math.Abs(rayHits[i].point.y-point.y)<.35f))return false;
            }
            return true;
        }
        bool AdmissionDoorLeaf(Collider collider,string doorId)=>doorId!=""&&doors.TryGetValue(doorId,out var door)&&(collider==door.LeafCollider||collider==door.SecondLeafCollider);
        bool AdmissionPositionClear(Vector3 point,string doorId)
        {
            int count=Physics.OverlapCapsuleNonAlloc(point+Vector3.up*.32f,point+Vector3.up*(bodies["CH_01"].Height-.3f),.275f,overlap,~0,QueryTriggerInteraction.Ignore);
            if(count==overlap.Length)return false;
            for(int i=0;i<count;i++)if(!overlap[i].GetComponentInParent<FixtureActorBody>()&&!AdmissionDoorLeaf(overlap[i],doorId))return false;
            return true;
        }
    }
}
