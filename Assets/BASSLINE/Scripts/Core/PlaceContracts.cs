using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
namespace BASSLINE.Core
{
    public interface IPlayerPlacePort
    {
        string PlaceLabel(string id);
        string PublishedMapText();
        string[] InvitationPlaceIds();
    }
    public interface ISessionPresencePort { bool HasSave { get; } }
    public interface IPlayerMapPort { KnownMapSnapshot ReadMap(); }
    // Self navigation is separate from other people's historical receipts.
    public interface IPlayerMapPosePort { KnownMapPose ReadMapPose(); }
    public sealed class KnownMapPose
    {
        public string Floor=""; public double X,Z,Yaw; public bool Available;
    }

    // Read models contain only received B records. No live world, room bounds, or door references.
    public sealed class KnownMapPlace
    {
        public string Id {get;} public string Label {get;} public string Floor {get;} public string Version {get;}
        public double X {get;} public double Z {get;} public bool Visited {get;} public string ReceiptId {get;} public long Tick {get;}
        public KnownMapPlace(string id,string label,string floor,string version,double x,double z,bool visited,string receipt,long tick){Id=id;Label=label;Floor=floor;Version=version;X=x;Z=z;Visited=visited;ReceiptId=receipt;Tick=tick;}
    }
    public sealed class KnownMapConnection
    {
        public string From {get;} public string To {get;} public string Version {get;} public string ReceiptId {get;} public long Tick {get;}
        public KnownMapConnection(string from,string to,string version,string receipt,long tick){From=from;To=to;Version=version;ReceiptId=receipt;Tick=tick;}
    }
    public sealed class KnownMapDoor
    {
        public string Id {get;} public string PlaceId {get;} public string Floor {get;} public string Version {get;}
        public string State {get;} public string ReceiptId {get;} public long Tick {get;} public double X {get;} public double Z {get;}
        public KnownMapDoor(string id,string place,string floor,string version,double x,double z,string state,string receipt,long tick){Id=id;PlaceId=place;Floor=floor;Version=version;X=x;Z=z;State=state;ReceiptId=receipt;Tick=tick;}
    }
    public sealed class KnownMapPerson
    {
        public string Id {get;} public string Label {get;} public string PlaceId {get;} public string ReceiptId {get;} public long Tick {get;}
        public double X {get;} public double Z {get;}
        public KnownMapPerson(string id,string label,string place,double x,double z,string receipt,long tick){Id=id;Label=label;PlaceId=place;X=x;Z=z;ReceiptId=receipt;Tick=tick;}
    }
    public sealed class KnownMapSnapshot
    {
        public string Owner {get;} public string LoopId {get;} public string CurrentVersion {get;} public long Revision {get;}
        public IReadOnlyList<KnownMapPlace> Places {get;} public IReadOnlyList<KnownMapConnection> Connections {get;}
        public IReadOnlyList<KnownMapDoor> Doors {get;} public IReadOnlyList<KnownMapPerson> People {get;}
        public bool HasPublishedPlan=>false;
        public KnownMapSnapshot(string owner,string version,long revision,IEnumerable<KnownMapPlace> places,IEnumerable<KnownMapConnection> connections,IEnumerable<KnownMapDoor> doors,IEnumerable<KnownMapPerson> people,string loopId="")
        {
            Owner=owner;LoopId=loopId;CurrentVersion=version;Revision=revision;
            Places=Array.AsReadOnly(places.ToArray());Connections=Array.AsReadOnly(connections.ToArray());Doors=Array.AsReadOnly(doors.ToArray());People=Array.AsReadOnly(people.ToArray());
        }
    }
}
namespace BASSLINE.Core
{
    // Pure projection from a bound B query. This function has no authoring or world-state input.
    public static class KnownMapProjection
    {
        const string Prefix="MAP|";
        static string Version(KnownRecord r)=>r.ProvenanceKey.Substring(Prefix.Length);
        public static KnownMapSnapshot Create(IActorKnowledgeQuery source,string currentVersion,Func<string,string> knownPersonName)
        {
            var records=source.Records();var map=records.Where(r=>r.Direct&&r.Kind=="Visual"&&r.ProvenanceKey!=null&&r.ProvenanceKey.StartsWith(Prefix,StringComparison.Ordinal)).ToArray();
            var places=new List<KnownMapPlace>();
            foreach(var group in map.Where(r=>r.Predicate=="MapAreaSeen"||r.Predicate=="MapNameRead").GroupBy(r=>Version(r)+"|"+r.SubjectId).OrderBy(g=>g.Min(r=>r.ReceivedTick)).ThenBy(g=>g.Key,StringComparer.Ordinal))
            {
                var visited=group.Where(r=>r.Predicate=="MapAreaSeen").OrderBy(r=>r.FromTick).FirstOrDefault();
                var name=group.Where(r=>r.Predicate=="MapNameRead").OrderByDescending(r=>r.FromTick).FirstOrDefault();var position=visited??name;
                string label=name?.Text??"확인한 공간 "+(places.Count+1);
                places.Add(new KnownMapPlace(position.SubjectId,label,position.Value,Version(position),position.Position.X,position.Position.Z,visited!=null,position.Id+(name!=null&&name!=position?" · "+name.Id:""),position.FromTick));
            }
            var connections=map.Where(r=>r.Predicate=="MapPassageUsed"&&places.Any(p=>p.Id==r.SubjectId&&p.Version==Version(r))&&places.Any(p=>p.Id==r.Value&&p.Version==Version(r)))
                .GroupBy(r=>Version(r)+"|"+string.Join("|",new[]{r.SubjectId,r.Value}.OrderBy(x=>x,StringComparer.Ordinal))).Select(g=>g.OrderBy(r=>r.FromTick).First())
                .Select(r=>new KnownMapConnection(r.SubjectId,r.Value,Version(r),r.Id,r.FromTick)).ToArray();
            var doors=new List<KnownMapDoor>();
            foreach(var seen in map.Where(r=>r.Predicate=="MapDoorSeen").GroupBy(r=>Version(r)+"|"+r.SubjectId).Select(g=>g.OrderByDescending(r=>r.FromTick).First()))
            {
                var state=Version(seen)==currentVersion?records.Where(r=>r.Direct&&r.SubjectId==seen.SubjectId&&r.Predicate=="DoorState").OrderByDescending(r=>r.FromTick).FirstOrDefault():null;
                doors.Add(new KnownMapDoor(seen.SubjectId,seen.PlaceId,seen.Value,Version(seen),seen.Position.X,seen.Position.Z,state?.Value??"Uninspected",state?.Id??seen.Id,state?.FromTick??seen.FromTick));
            }
            var people=records.Where(r=>r.Direct&&r.IdentityConfirmed&&r.Predicate=="AtPlace"&&r.SubjectId!=null&&r.SubjectId.StartsWith("CH_",StringComparison.Ordinal)&&r.SubjectId!=source.OwnerId)
                .GroupBy(r=>r.SubjectId).Select(g=>g.OrderByDescending(r=>r.FromTick).ThenByDescending(r=>r.ReceivedTick).First())
                .Select(r=>new KnownMapPerson(r.SubjectId,knownPersonName?.Invoke(r.SubjectId)??r.SubjectId,r.PlaceId,r.Position.X,r.Position.Z,r.Id,r.FromTick)).ToArray();
            return new KnownMapSnapshot(source.OwnerId,currentVersion,source.Revision,places,connections,doors,people,source.LoopId);
        }
    }
}
