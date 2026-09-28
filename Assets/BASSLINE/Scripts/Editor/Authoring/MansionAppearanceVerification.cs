using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BASSLINE.AuthoringData;
using BASSLINE.Save;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BASSLINE.Authoring
{
    // A structural regression receipt for a visual-only scene revision, not an art approval.
    public static class MansionAppearanceVerification
    {
        public static void UpgradeAndVerify()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Use an isolated batch editor for this comparison.");
            EditorSceneManager.OpenScene(ProductionGameSetup.ScenePath);
            var previous=UnityEngine.Object.FindAnyObjectByType<MansionLayout>();
            string before=PhysicalHash(previous),revision=previous.SourceHash+"|"+previous.BuildVersion;
            File.WriteAllText("Verification/mansion-appearance-baseline.txt",before+"\n"+revision);
            ProductionGameSetup.Build();
            var current=UnityEngine.Object.FindAnyObjectByType<MansionLayout>();string after=PhysicalHash(current);
            var receipt=new CompatibilityReceipt{Before=before,After=after,RevisionBefore=revision,RevisionAfter=current.SourceHash+"|"+current.BuildVersion,
                Result=before==after&&revision==current.SourceHash+"|"+current.BuildVersion?"PASS_EXISTING_SCENE_PHYSICAL_COMPATIBILITY":"FAIL"};
            File.WriteAllText("Verification/mansion-appearance-compatibility.json",JsonUtility.ToJson(receipt,true));
            if(receipt.Result=="FAIL")throw new InvalidOperationException("Appearance rebuild changed prior scene physics or save geometry revision.");
        }

        public static string PhysicalHash(MansionLayout value)
        {
            var lines=new List<string>{value.SourceHash,value.BuildVersion,value.MapVersion};
            foreach(var room in value.Rooms)lines.Add("room|"+room.RoomId+"|"+room.WingId+"|"+room.Floor+"|"+room.ParentRoomId+"|"+room.GeometryType+"|"+JsonUtility.ToJson(room.Bounds)+"|"+V(room.FloorCenter)+"|"+F(room.CeilingHeight)+"|"+V(room.WalkPoint)+"|"+room.WalkNode);
            foreach(var node in value.NavigationNodes)lines.Add("node|"+JsonUtility.ToJson(node));
            foreach(var edge in value.NavigationEdges)lines.Add("edge|"+JsonUtility.ToJson(edge));
            foreach(var c in value.Connections)lines.Add("door|"+c.ConnectionId+"|"+c.DoorId+"|"+c.RoomA+"|"+c.RoomB+"|"+c.Kind+"|"+c.InitialLock+"|"+c.InitialOpen+"|"+c.EmergencyOpen+"|"+c.PassageToken+"|"+F(c.Width)+"|"+F(c.ClearHeight)+"|"+V(c.NormalAToB)+"|"+c.EntryNodeA+"|"+c.EntryNodeB+"|"+F(c.OpenAngle)+"|"+F(c.OpenAmount)+"|"+string.Join(";",c.Route.Select(V)));
            foreach(var a in value.Anchors) {
                lines.Add("anchor|"+a.AnchorId+"|"+a.RoomId+"|"+a.InteractionType+"|"+a.FurnitureId+"|"+a.ParticipantSlots+"|"+F(a.ApproachClearance)+"|"+V(a.ApproachPoint)+"|"+a.ApproachNode+"|"+a.DefinitionRevision+"|"+a.SocketsResolved+"|"+Matrix(a.transform)+"|"+Matrix(a.LookTarget)+"|"+Matrix(a.LeftHand)+"|"+Matrix(a.RightHand)+"|"+Matrix(a.LeftFoot)+"|"+Matrix(a.RightFoot));
            }
            foreach(var collider in value.GetComponentsInChildren<Collider>(true)) {
                string shape="";
                if(collider is BoxCollider box)shape=V(box.center)+"|"+V(box.size);
                else if(collider is SphereCollider sphere)shape=V(sphere.center)+"|"+F(sphere.radius);
                else if(collider is CapsuleCollider capsule)shape=V(capsule.center)+"|"+F(capsule.radius)+"|"+F(capsule.height)+"|"+capsule.direction;
                else if(collider is MeshCollider mesh)shape=mesh.convex+"|"+mesh.cookingOptions+"|"+AtomicSaveStore.Hash(string.Join(";",mesh.sharedMesh.vertices.Select(V))+"|"+string.Join(",",mesh.sharedMesh.triangles));
                else throw new NotSupportedException("Include this collider in physical comparison: "+collider.GetType().Name);
                lines.Add("collider|"+PathOf(collider.transform,value.transform)+"|"+collider.GetType().Name+"|"+collider.enabled+"|"+collider.isTrigger+"|"+collider.gameObject.activeSelf+"|"+collider.gameObject.layer+"|"+F(collider.contactOffset)+"|"+Matrix(collider.transform)+"|"+shape);
            }
            lines.Sort(StringComparer.Ordinal);return AtomicSaveStore.Hash(string.Join("\n",lines));
        }
        static string PathOf(Transform t,Transform root)=>t==root?t.name:PathOf(t.parent,root)+"/"+t.name;
        static string Matrix(Transform t)=>t?string.Join(",",Enumerable.Range(0,16).Select(i=>F(t.localToWorldMatrix[i]))):"none";
        // Serialization and matrix recomposition can differ below a micron; geometry tolerance is 10 microns.
        static string F(float value)=>value.ToString("F5",CultureInfo.InvariantCulture);
        static string V(Vector3 value)=>F(value.x)+","+F(value.y)+","+F(value.z);
        [Serializable] sealed class CompatibilityReceipt {public string Before,After,RevisionBefore,RevisionAfter,Result;}
    }
}
