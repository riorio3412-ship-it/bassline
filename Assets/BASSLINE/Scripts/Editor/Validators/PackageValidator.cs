using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BASSLINE.Core;

namespace BASSLINE.Authoring
{
    public sealed class Diagnostic
    {
        public string Severity, Code, Source, Id, Message;
        public override string ToString() => Severity + "|" + Code + "|" + Source + "|" + Id + "|" + Message;
    }
    public sealed class PackageData
    {
        public List<CsvRow> Rooms, Characters, Screens, Assets, Connections, Anchors, Seats;
        public static PackageData Load(string root) => new PackageData {
            Rooms = CsvTable.Load(Path.Combine(root, "07_ASSET_MANIFEST/ROOM_MANIFEST.csv")),
            Characters = CsvTable.Load(Path.Combine(root, "07_ASSET_MANIFEST/CHARACTER_MANIFEST.csv")),
            Screens = CsvTable.Load(Path.Combine(root, "07_ASSET_MANIFEST/UI_MANIFEST.csv")),
            Assets = CsvTable.Load(Path.Combine(root, "07_ASSET_MANIFEST/MASTER_ASSET_MANIFEST.csv")),
            Connections = CsvTable.Load(Path.Combine(root, "02_MANSION/BASSLINE_ROOM_CONNECTIONS.csv")),
            Anchors = CsvTable.Load(Path.Combine(root, "02_MANSION/BASSLINE_INTERACTION_ANCHORS.csv")),
            Seats = CsvTable.Load(Path.Combine(root, "02_MANSION/BASSLINE_SEAT_ANCHORS.csv"))
        };
    }
    public static class PackageValidator
    {
        public static List<Diagnostic> Validate(PackageData data, string projectRoot)
        {
            var output = new List<Diagnostic>();
            Action<string,string,string,string,string> add = (severity,code,source,id,message) => output.Add(new Diagnostic { Severity=severity, Code=code, Source=source, Id=id, Message=message });
            Action<string,string,string,string> error = (code,source,id,message) => add("ERROR",code,source,id,message);
            Dictionary<string,CsvRow> Index(List<CsvRow> rows, string key)
            {
                var result = new Dictionary<string,CsvRow>(StringComparer.Ordinal);
                foreach(var row in rows)
                {
                    var id=row[key];
                    try { _ = new StableId(id); } catch(ArgumentException) { error("INVALID_ID",key,id,"Stable ID syntax"); }
                    if(result.ContainsKey(id)) error("DUPLICATE_ID",key,id,"Duplicate at record " + row.RecordNumber);
                    else result.Add(id,row);
                }
                return result;
            }
            var rooms=Index(data.Rooms,"RoomID"); var chars=Index(data.Characters,"CharacterID");
            var screens=Index(data.Screens,"ScreenID"); var assets=Index(data.Assets,"AssetID");
            Index(data.Connections,"ConnectionID"); Index(data.Anchors,"AnchorID"); Index(data.Seats,"SeatID");
            void FK(string key,string id,Dictionary<string,CsvRow> table,string reference,bool optional=false)
            { if(!(optional && reference=="") && !table.ContainsKey(reference)) error("UNKNOWN_FK",key,id,reference); }
            if(data.Rooms.Count!=80 || data.Connections.Count!=79 || data.Seats.Count!=201 || data.Screens.Count!=41 || data.Characters.Count!=18)
                error("CATALOG_COUNT","Package","M01","Expected rooms80/connections79/seats201/screens41/characters18");
            if(data.Characters.Count(x=>x["Role"]=="Player")!=1 || !chars.TryGetValue("CH_01",out var player) || player["Role"]!="Player" || data.Characters.Count(x=>x["Role"]=="AutonomousNPC")!=17)
                error("PARTICIPANT_ROSTER","CharacterID","CH_01","One player and 17 NPCs required; Yusti is not a participant");
            var doorIds=new HashSet<string>(StringComparer.Ordinal);
            foreach(var r in data.Rooms)
            {
                var id=r["RoomID"]; FK("ParentRoomID",id,rooms,r["ParentRoomID"],true);
                foreach(var field in new[]{"X","Y","Z","Width","Depth","CeilingHeight"}) r.Number(field);
                if(r.Number("Width")<=0 || r.Number("Depth")<=0 || r.Number("CeilingHeight")<0) error("DIMENSION","RoomID",id,"Invalid metres");
                if(r["MapVersion"]!="M01") error("MAP_VERSION","RoomID",id,r["MapVersion"]);
                var floors=new Dictionary<string,double>{{"B1",-6},{"F1",0},{"F2",6},{"F3",12}};
                if(!floors.TryGetValue(r["Floor"],out var y) || Math.Abs(y-r.Number("Y"))>0.001) error("FLOOR_Y","RoomID",id,"Expected Y -6/0/6/12m");
            }
            foreach(var c in data.Connections)
            {
                var id=c["ConnectionID"]; FK("RoomA",id,rooms,c["RoomA"]); FK("RoomB",id,rooms,c["RoomB"]);
                if(c["RoomA"]==c["RoomB"]) error("SELF_CONNECTION","ConnectionID",id,"Same room on both ends");
                foreach(var f in new[]{"X","Y","Z","Width","ClearHeight"}) c.Number(f);
                if(c.Number("Width")<=0 || c.Number("ClearHeight")<=0) error("OPENING_SIZE","ConnectionID",id,"Nonpositive opening");
                if(c["Kind"]=="Door")
                {
                    if(c["DoorID"]=="" || !doorIds.Add(c["DoorID"])) error("DOOR_ID","ConnectionID",id,"Missing or duplicate DoorID");
                }
                else if(!new[]{"OpenPassage","ZoneBoundary","Stair"}.Contains(c["Kind"]) || c["DoorID"]!="")
                    error("CONNECTION_KIND","ConnectionID",id,"Non-door connections must not create DoorController");
                if(c["SensorID"]!="") add("WARNING","SENSOR_DEFINITION_REQUIRED","ConnectionID",id,c["SensorID"]);
                if(c["InitialLock"]=="OwnerPermission" && !(c["RoomA"].StartsWith("R_BED_",StringComparison.Ordinal) || c["RoomB"].StartsWith("R_BED_",StringComparison.Ordinal)))
                    error("OWNER_POLICY","ConnectionID",id,"OwnerPermission requires explicit bedroom identity");
                if(rooms.TryGetValue(c["RoomA"],out var a) && rooms.TryGetValue(c["RoomB"],out var b))
                {
                    if(c["Kind"]=="ZoneBoundary")
                    {
                        if(b["ParentRoomID"]!=a["RoomID"] && a["ParentRoomID"]!=b["RoomID"])
                            error("ZONE_PARENT","ConnectionID",id,"Zone boundary requires explicit parent");
                    }
                    else if(c["Kind"]=="Stair")
                    { if(c["Route"]=="") error("STAIR_ROUTE","ConnectionID",id,"Actual flight route required"); }
                    else if(!SharedBoundary(a,b,c)) error("DOOR_BOUNDARY","ConnectionID",id,"Centre/width/height not on shared boundary");
                }
            }
            foreach(var r in data.Characters)
            {
                foreach(var key in new[]{"ModelAssetID","RigAssetID","StandingAssetID"}) FK(key,r["CharacterID"],assets,r[key]);
                if(r.Number("HeightCm")<160 || r.Number("HeightCm")>187) error("BODY_UNITS","CharacterID",r["CharacterID"],"Expected source height in cm");
            }
            foreach(var r in data.Anchors.Concat(data.Seats))
            {
                var seat=r.Fields.ContainsKey("SeatID"); var id=r[seat?"SeatID":"AnchorID"];
                FK("RoomID",id,rooms,r["RoomID"]);
                foreach(var key in new[]{"LocalX","LocalY","LocalZ","Yaw"}) r.Number(key);
                if(rooms.TryGetValue(r["RoomID"],out var room) && (Math.Abs(r.Number("LocalX"))>room.Number("Width")/2 || Math.Abs(r.Number("LocalZ"))>room.Number("Depth")/2))
                    error("LOCAL_BOUNDS","Anchor/Seat",id,"Local coordinate outside room AABB");
                if(seat)
                {
                    FK("OccupantID",id,chars,r["OccupantID"],true);
                    if(r.Number("SeatHeight")<=0 || r.Number("ApproachClearance")<1.2) error("SEAT_CLEARANCE","SeatID",id,"Minimum 1.2m approach required");
                }
                else
                {
                    var slots=r.Number("ParticipantSlots");
                    if(slots<1 || slots!=Math.Floor(slots)) error("SLOT_COUNT","AnchorID",id,"Positive integer required");
                    add("WARNING","SOCKETS_UNRESOLVED","AnchorID",id,"Named sockets require model authoring; interaction remains gated");
                }
            }
            foreach(var r in data.Assets)
            {
                var id=r["AssetID"]; var path=r["AssetPath"];
                if(!path.StartsWith("Assets/BASSLINE/",StringComparison.Ordinal) || path.Contains("..") || path.Contains("\\")) error("ASSET_PATH","AssetID",id,path);
                else if(!File.Exists(Path.Combine(projectRoot,path))) add("WARNING","ASSET_NOT_PRODUCED","AssetID",id,path);
                foreach(var dep in r.List("Dependencies"))
                {
                    // StandingSpriteSet uses CharacterID in the supplied manifest, not AssetID.
                    if(r["Category"]=="StandingSpriteSet" && chars.ContainsKey(dep)) FK("Dependencies",id,assets,"SO_"+dep);
                    else FK("Dependencies",id,assets,dep);
                }
            }
            foreach(var group in data.Assets.GroupBy(x=>x["AssetPath"],StringComparer.OrdinalIgnoreCase).Where(x=>x.Count()>1))
                error("DUPLICATE_PATH","AssetPath",group.Key,"Multiple asset identities map to one path");
            CheckCycles(rooms, r=>new[]{r["ParentRoomID"]}.Where(x=>x!=""), "ROOM_PARENT_CYCLE",error);
            CheckCycles(assets, r=>r.List("Dependencies").Select(x=>r["Category"]=="StandingSpriteSet"&&chars.ContainsKey(x)?"SO_"+x:x), "ASSET_CYCLE",error);
            if(data.Rooms.Count(x=>System.Text.RegularExpressions.Regex.IsMatch(x["RoomID"],@"^R_BED_\d{2}$"))!=18 || data.Seats.Count(x=>x["RoomID"]=="R_TRIAL")!=18)
                error("IDENTITY_COUNT","Package","M01","18 private bedrooms and 18 trial seats required");
            return output.OrderBy(x=>x.Severity,StringComparer.Ordinal).ThenBy(x=>x.Source,StringComparer.Ordinal).ThenBy(x=>x.Id,StringComparer.Ordinal).ThenBy(x=>x.Code,StringComparer.Ordinal).ToList();
        }
        private static bool SharedBoundary(CsvRow a,CsvRow b,CsvRow c)
        {
            const double e=0.011;
            double x=c.Number("X"),z=c.Number("Z"),y=c.Number("Y"),half=c.Number("Width")/2;
            bool Vertical(CsvRow r) => y>=r.Number("Y")-e && (r["GeometryType"]=="Outdoor" || y+c.Number("ClearHeight")<=r.Number("Y")+r.Number("CeilingHeight")+e);
            bool XFace(CsvRow r) => Math.Abs(Math.Abs(x-r.Number("X"))-r.Number("Width")/2)<e && Math.Abs(z-r.Number("Z"))+half<=r.Number("Depth")/2+e;
            bool ZFace(CsvRow r) => Math.Abs(Math.Abs(z-r.Number("Z"))-r.Number("Depth")/2)<e && Math.Abs(x-r.Number("X"))+half<=r.Number("Width")/2+e;
            return Vertical(a)&&Vertical(b)&&((XFace(a)&&XFace(b)&&(a.Number("X")-x)*(b.Number("X")-x)<0)||(ZFace(a)&&ZFace(b)&&(a.Number("Z")-z)*(b.Number("Z")-z)<0));
        }
        private static void CheckCycles(Dictionary<string,CsvRow> table,Func<CsvRow,IEnumerable<string>> edges,string code,Action<string,string,string,string> error)
        {
            var active=new HashSet<string>(); var done=new HashSet<string>();
            void Visit(string id)
            {
                if(done.Contains(id)||!table.ContainsKey(id))return;
                if(!active.Add(id)){error(code,"Graph",id,"Directed dependency cycle");return;}
                foreach(var next in edges(table[id])) Visit(next);
                active.Remove(id);done.Add(id);
            }
            foreach(var id in table.Keys.OrderBy(x=>x,StringComparer.Ordinal))Visit(id);
        }
    }
}
