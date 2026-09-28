using System.Linq;
using UnityEngine;
using UnityEditor;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
namespace BASSLINE.Authoring
{
    public static class MansionTraceToolsBuilder
    {
        // Workshop marking supplies. Contact leaves the same marks during innocent use.
        public static FixtureObjectBody Build(MansionLayout layout)
        {
            var room=layout.Room("R_WORK");var table=room.GetComponentsInChildren<MansionProp>().Single(p=>p.ObjectId=="OBJ_R_WORK_Workbench0");
            var material=table.GetComponentInChildren<Renderer>().sharedMaterial;
            const string path="Assets/BASSLINE/Art/Materials/M_SurfacePigment.mat";
            var pigment=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!pigment){pigment=new Material(Shader.Find("Universal Render Pipeline/Lit"));pigment.SetFloat("_Smoothness",0);AssetDatabase.CreateAsset(pigment,path);}
            var front=table.GetComponentsInChildren<Collider>().Select(c=>c.bounds.min.z).DefaultIfEmpty(table.transform.position.z-.5f).Min();
            var top=table.GetComponentsInChildren<Collider>().Select(c=>c.bounds.max.y).DefaultIfEmpty(table.transform.position.y+1).Max();
            var board=new GameObject("M_WORK_MARKING_BOARD");board.transform.SetParent(room.transform,false);board.transform.position=table.transform.position+new Vector3(0,top-table.transform.position.y+.14f,front-table.transform.position.z-.025f);
            var target=board.AddComponent<FixtureTarget>();target.StableId=board.name;target.PublicName="작업대 표식판";
            var surface=board.AddComponent<MansionTraceSurface>();var box=board.AddComponent<BoxCollider>();box.size=new Vector3(.4f,.22f,.025f);surface.ContactSurface=box;surface.TraceMaterial=pigment;
            Part(board.transform,new Vector3(.4f,.22f,.025f),material);
            var tool=new GameObject("M_BLUE_MARKING_CHALK");tool.transform.position=table.transform.position+new Vector3(.28f,top-table.transform.position.y+.04f,-.15f);
            var body=tool.AddComponent<FixtureObjectBody>();body.ObjectId=tool.name;var collider=tool.AddComponent<BoxCollider>();collider.size=new Vector3(.04f,.06f,.06f);body.Collider=collider;
            var toolTarget=tool.AddComponent<FixtureTarget>();toolTarget.StableId=tool.name;toolTarget.PublicName="홈이 난 푸른 표식 분필";
            Part(tool.transform,collider.size,material);
            var source=tool.AddComponent<MansionTraceSource>();var face=new GameObject("Pigment contact face");face.transform.SetParent(tool.transform,false);face.transform.localPosition=new Vector3(-.0205f,0,0);face.transform.localRotation=Quaternion.Euler(0,-90,0);
            source.Face=face.AddComponent<MansionTracePatternVisual>();source.Face.Build(new SurfacePattern{Mask=0xB55D,Colour=1,SizeMm=60},pigment);
            return body;
        }
        static void Part(Transform parent,Vector3 scale,Material material){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(parent,false);go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(go.GetComponent<Collider>());}
    }
}
