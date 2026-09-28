using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using BASSLINE.AuthoringData;
namespace BASSLINE.Authoring
{
    public static class MansionWeaponBuilder
    {
        public static FixtureObjectBody[] Build(MansionLayout layout)
        {
            var steel=Material("WeaponSteel",new Color(.76f,.80f,.84f),.25f,.42f);
            var dark=Material("WeaponGrip",new Color(.13f,.075f,.065f),0,.24f);
            var brass=Material("WeaponBrass",new Color(.54f,.35f,.12f),.75f,.42f);
            var knife=Create("M_KITCHEN_KNIFE","주방 칼","Blade",new Vector3(.055f,.022f,.34f),new Vector3(0,0,.105f));
            Part(knife.transform,"손잡이",new Vector3(0,0,-.015f),new Vector3(.033f,.025f,.12f),dark);
            Blade(knife.transform,"Knife",new[]{new Vector2(-.024f,.045f),new Vector2(.026f,.045f),new Vector2(.026f,.18f),new Vector2(-.015f,.275f)},.004f,steel);
            foreach(float z in new[]{-.048f,0,.03f})Part(knife.transform,"리벳",new Vector3(0,.013f,z),new Vector3(.006f,.002f,.006f),brass);
            Place(knife,layout,"R_KITCHEN","OBJ_R_KITCHEN_CookingCounter",new Vector3(.6f,0,-.35f),0);
            var hammer=Create("M_WORK_HAMMER","작업용 망치","Blunt",new Vector3(.16f,.065f,.38f),new Vector3(0,0,.11f));
            Part(hammer.transform,"나무 손잡이",new Vector3(0,0,.08f),new Vector3(.03f,.032f,.30f),dark);
            Part(hammer.transform,"망치 머리",new Vector3(0,0,.263f),new Vector3(.16f,.065f,.075f),steel);
            Part(hammer.transform,"머리 고정대",new Vector3(0,0,.215f),new Vector3(.045f,.045f,.028f),brass);
            Place(hammer,layout,"R_WORK","OBJ_R_WORK_Workbench1",new Vector3(-.5f,0,-.38f),70);
            var dagger=Create("M_EXHIBIT_DAGGER","장식 단검","Blade",new Vector3(.13f,.028f,.40f),new Vector3(0,0,.12f));
            Part(dagger.transform,"가죽 손잡이",new Vector3(0,0,-.015f),new Vector3(.029f,.027f,.12f),dark);
            Part(dagger.transform,"가드",new Vector3(0,0,.052f),new Vector3(.13f,.024f,.025f),brass);
            Part(dagger.transform,"끝 장식",new Vector3(0,0,-.075f),new Vector3(.046f,.031f,.025f),brass);
            Blade(dagger.transform,"Dagger",new[]{new Vector2(0,.062f),new Vector2(.026f,.10f),new Vector2(0,.315f),new Vector2(-.026f,.10f)},.005f,steel);
            Place(dagger,layout,"R_EXHIBIT","OBJ_R_EXHIBIT_Exhibit0",new Vector3(-.14f,0,-.32f),90,"Plinth");
            return new[]{knife,hammer,dagger};
        }
        static FixtureObjectBody Create(string id,string name,string category,Vector3 size,Vector3 centre)
        {
            var go=new GameObject(id);var b=go.AddComponent<FixtureObjectBody>();b.ObjectId=id;
            var box=go.AddComponent<BoxCollider>();box.size=size;box.center=centre;b.Collider=box;
            var t=go.AddComponent<FixtureTarget>();t.StableId=id;t.PublicName=name;go.AddComponent<MansionWeapon>().Category=category;return b;
        }
        static void Place(FixtureObjectBody b,MansionLayout layout,string room,string furniture,Vector3 offset,float yaw,string surface="")
        {
            var prop=layout.Room(room).GetComponentsInChildren<MansionProp>().Single(x=>x.ObjectId==furniture);
            var colliders=surface!=""?prop.transform.Find(surface).GetComponents<Collider>():prop.GetComponentsInChildren<Collider>().Where(x=>!x.isTrigger).ToArray();
            float top=colliders.Max(x=>x.bounds.max.y);
            b.transform.position=new Vector3(prop.transform.position.x,top+((BoxCollider)b.Collider).size.y*.5f+.004f,prop.transform.position.z)+offset;
            b.transform.rotation=Quaternion.Euler(0,yaw,0);var weapon=b.GetComponent<MansionWeapon>();weapon.InitialPosition=b.transform.position;weapon.InitialEuler=b.transform.eulerAngles;
        }
        static Material Material(string name,Color color,float metallic,float smooth)
        {
            string path="Assets/BASSLINE/Art/Materials/M_"+name+".mat";Directory.CreateDirectory(Path.GetDirectoryName(path));var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);return m;
        }
        static void Part(Transform parent,string name,Vector3 point,Vector3 size,Material material)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=point;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        }
        static void Blade(Transform parent,string name,Vector2[] outline,float thickness,Material material)
        {
            string path="Assets/BASSLINE/Art/Weapons/"+name+".asset";Directory.CreateDirectory(Path.GetDirectoryName(path));var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool create=!mesh;if(create)mesh=new Mesh{name=name};{mesh.Clear();int n=outline.Length;var vertices=new Vector3[n*2];for(int i=0;i<n;i++){vertices[i]=new Vector3(outline[i].x,-thickness*.5f,outline[i].y);vertices[i+n]=new Vector3(outline[i].x,thickness*.5f,outline[i].y);}
                var triangles=new System.Collections.Generic.List<int>();for(int i=1;i<n-1;i++){triangles.AddRange(new[]{0,i+1,i,n,n+i,n+i+1});}for(int i=0;i<n;i++){int j=(i+1)%n;triangles.AddRange(new[]{i,j,j+n,i,j+n,i+n});}for(int t=0;t<triangles.Count;t+=3){int swap=triangles[t+1];triangles[t+1]=triangles[t+2];triangles[t+2]=swap;}mesh.vertices=vertices;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);}
            var g=new GameObject("날");g.transform.SetParent(parent,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
    }
}
