using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using BASSLINE.AuthoringData;
using BASSLINE.Save;

namespace BASSLINE.Authoring
{
    [Serializable] public sealed class GeneratedRecord { public string Id,Path,SourceHash,OutputHash,MetaHash,Status; }
    [Serializable] public sealed class GeneratedIndex { public List<GeneratedRecord> Records=new List<GeneratedRecord>(); }
    public static class ProductionImporter
    {
        const string IndexPath="Verification/generated-assets.json";
        public static string SourceRoot=>Path.GetFullPath("SourcePackage");
        public static string CatalogHash()=>AtomicSaveStore.Hash(string.Join("\n",Directory.GetFiles(SourceRoot,"*.csv",SearchOption.AllDirectories).OrderBy(x=>x,StringComparer.Ordinal).Select(x=>Path.GetRelativePath(SourceRoot,x).Replace('\\','/')+":"+AtomicSaveStore.Hash(File.ReadAllText(x)))));
        [MenuItem("BASSLINE/Production/1 Validate manifests")]
        public static void Validate()
        {
            var findings=PackageValidator.Validate(PackageData.Load(SourceRoot),Directory.GetCurrentDirectory());
            Directory.CreateDirectory("Verification");File.WriteAllLines("Verification/unity-manifest-diagnostics.txt",findings.Select(x=>x.ToString()));
            if(findings.Any(x=>x.Severity=="ERROR"))throw new InvalidDataException("Manifest validation failed; see Verification/unity-manifest-diagnostics.txt");
            Debug.Log("BASSLINE manifest static validation PASS; physical/art tests NOT RUN. Warnings="+findings.Count);
        }
        public static string FileHash(string path)=>AtomicSaveStore.Hash(Convert.ToBase64String(File.ReadAllBytes(path)));
        public static GeneratedIndex LoadIndex()=>File.Exists(IndexPath)?JsonUtility.FromJson<GeneratedIndex>(File.ReadAllText(IndexPath)):new GeneratedIndex();
        public static void SaveIndex(GeneratedIndex index){Directory.CreateDirectory("Verification");File.WriteAllText(IndexPath,JsonUtility.ToJson(index,true));}
        public static bool CanWrite(GeneratedIndex index,string id,string path,string sourceHash)
        {
            var previous=index.Records.SingleOrDefault(x=>x.Id==id);
            if(previous!=null&&previous.Path!=path)throw new InvalidOperationException("Generated path changed: "+id);
            if(File.Exists(path))
            {
                if(previous==null||FileHash(path)!=previous.OutputHash)throw new InvalidOperationException("Manual modification/unowned output conflict: "+path);
                if(string.IsNullOrEmpty(previous.MetaHash)||!File.Exists(path+".meta")||FileHash(path+".meta")!=previous.MetaHash)throw new InvalidOperationException("Metadata/GUID conflict or missing tracking: "+path);
                return previous.SourceHash!=sourceHash;
            }
            if(previous!=null)throw new InvalidOperationException("Previously generated asset was deleted: "+path);
            if(File.Exists(path+".meta"))throw new InvalidOperationException("Orphan metadata conflict: "+path);
            return true;
        }
        public static void Track(GeneratedIndex index,string id,string path,string sourceHash,string status)
        {
            index.Records.RemoveAll(x=>x.Id==id);index.Records.Add(new GeneratedRecord {Id=id,Path=path,SourceHash=sourceHash,OutputHash=FileHash(path),MetaHash=FileHash(path+".meta"),Status=status});
            index.Records=index.Records.OrderBy(x=>x.Id,StringComparer.Ordinal).ToList();SaveIndex(index);
        }
        static string AssetPath(PackageData data,string id)=>data.Assets.Single(x=>x["AssetID"]==id)["AssetPath"];
        [MenuItem("BASSLINE/Production/2 Import authoring data")]
        public static void Import()
        {
            Validate(); var data=PackageData.Load(SourceRoot);var index=LoadIndex();
            var requests=new List<(string id,string path,string kind,CsvRow row)>();
            requests.AddRange(data.Rooms.Select(r=>(r["RoomID"],AssetPath(data,"SO_"+r["RoomID"]),"Room",r)));
            requests.AddRange(data.Connections.Select(r=>(r["DoorID"]==""?r["ConnectionID"]:r["DoorID"],AssetPath(data,"SO_"+(r["DoorID"]==""?r["ConnectionID"]:r["DoorID"])),"Connection",NormalizeConnection(r))));
            requests.AddRange(data.Characters.Select(r=>(r["CharacterID"],AssetPath(data,"SO_"+r["CharacterID"]),"Character",r)));
            requests.AddRange(data.Screens.Select(r=>(r["ScreenID"],AssetPath(data,"SO_"+r["ScreenID"]),"Screen",r)));
            // Preflight the entire data batch before the first write.
            foreach(var q in requests)CanWrite(index,"SO_"+q.id,q.path,AtomicSaveStore.Hash(CsvTable.Canonical(q.row)));
            foreach(var room in data.Rooms)
            {
                var related=data.Anchors.Concat(data.Seats).Where(x=>x["RoomID"]==room["RoomID"]);
                CanWrite(index,room["Prefab"],AssetPath(data,"ENV_"+room["RoomID"]),AtomicSaveStore.Hash("RoomAuthoring_v1"+CsvTable.Canonical(room)+string.Join("",related.Select(CsvTable.Canonical))));
            }
            foreach(var q in requests)
            {
                var hash=AtomicSaveStore.Hash(CsvTable.Canonical(q.row));if(!CanWrite(index,"SO_"+q.id,q.path,hash))continue;
                Directory.CreateDirectory(Path.GetDirectoryName(q.path));AssetDatabase.Refresh();
                var asset=AssetDatabase.LoadAssetAtPath<ImportedDefinition>(q.path);
                bool create=asset==null;if(create)asset=ScriptableObject.CreateInstance<ImportedDefinition>();
                asset.StableId=q.id;asset.RecordKind=q.kind;asset.SourceFile="SourcePackage (stable ID lookup)";asset.SourceHash=hash;
                asset.DecisionStatus=q.row.Fields.TryGetValue("DecisionStatus",out var status)?status:q.row.Fields.TryGetValue("Status",out status)?status:"PRODUCTION PROPOSAL";
                asset.Fields=q.row.Fields.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>new AuthoringField{Key=x.Key,Value=x.Value}).ToArray();
                if(create)AssetDatabase.CreateAsset(asset,q.path);else EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();Track(index,"SO_"+q.id,q.path,hash,"AuthoringProxy");
            }
            foreach(var room in data.Rooms)
            {
                var path=AssetPath(data,"ENV_"+room["RoomID"]);var anchors=data.Anchors.Where(x=>x["RoomID"]==room["RoomID"]).ToArray();var seats=data.Seats.Where(x=>x["RoomID"]==room["RoomID"]).ToArray();
                var hash=AtomicSaveStore.Hash("RoomAuthoring_v1"+CsvTable.Canonical(room)+string.Join("",anchors.Concat(seats).Select(CsvTable.Canonical)));
                if(!CanWrite(index,room["Prefab"],path,hash))continue;
                var root=new GameObject(room["RoomID"]+"_AuthoringProxy");
                try
                {
                    Identity(root,room["RoomID"],"Room",AssetPath(data,"SO_"+room["RoomID"]));
                    foreach(var name in new[]{"Architecture","DoorBindings","Furniture","GameplayObjects","Decoration","Anchors","Seats","ObservationZones","Lighting","NavSources"})new GameObject(name).transform.SetParent(root.transform,false);
                    foreach(var row in anchors)Place(row,"AnchorID",root.transform.Find("Anchors"),"InteractionAnchor");
                    foreach(var row in seats)Place(row,"SeatID",root.transform.Find("Seats"),"SeatAnchor");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));PrefabUtility.SaveAsPrefabAsset(root,path,out var success);
                    if(!success)throw new IOException("Prefab save failed: "+path);
                    Track(index,room["Prefab"],path,hash,"AuthoringProxy_NoGeometry");
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("BASSLINE authoring import completed; unresolved sockets and room geometry remain gated.");
        }
        public static CsvRow NormalizeConnection(CsvRow row)
        {
            var fields=new Dictionary<string,string>(row.Fields,StringComparer.Ordinal);
            fields["AccessPolicy"]=row["InitialLock"]=="OwnerPermission"?"OwnerPermission":row["InitialLock"]=="FacilityControlled"?"Facility":"Public";
            fields["LockState"]=row["InitialLock"]=="OwnerPermission"?"KeyLocked":row["InitialLock"];
            fields["OwnerCharacterID"]=row["InitialLock"]=="OwnerPermission"?"CH_"+(row["RoomA"].StartsWith("R_BED_",StringComparison.Ordinal)?row["RoomA"]:row["RoomB"]).Substring(6):"";
            return new CsvRow(fields,row.RecordNumber);
        }
        public static AuthoringIdentity Identity(GameObject go,string id,string kind,string path="")
        {var identity=go.AddComponent<AuthoringIdentity>();identity.StableId=id;identity.Kind=kind;identity.DefinitionPath=path;return identity;}
        static void Place(CsvRow row,string idField,Transform parent,string kind)
        {
            var go=new GameObject(row[idField]+"_UnresolvedSockets");go.transform.SetParent(parent,false);
            go.transform.localPosition=new Vector3((float)row.Number("LocalX"),(float)row.Number("LocalY"),(float)row.Number("LocalZ"));
            go.transform.localRotation=Quaternion.Euler(0,(float)row.Number("Yaw"),0);Identity(go,row[idField],kind);
        }
    }
}
