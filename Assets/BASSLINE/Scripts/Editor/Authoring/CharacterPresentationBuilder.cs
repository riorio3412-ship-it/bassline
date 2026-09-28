using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using BASSLINE.AuthoringData;
namespace BASSLINE.Authoring
{
    // Replaceable 3D blockout with readable identity silhouettes. The physical actor stays untouched.
    public static partial class CharacterPresentationBuilder
    {
        const string Folder="Assets/BASSLINE/Characters/Materials/PresentationV10";
        static Material M(string name,string hex)
        {
            Directory.CreateDirectory(Folder);string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));ColorUtility.TryParseHtmlString("#"+hex,out var c);m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",.17f);AssetDatabase.CreateAsset(m,path);}return m;
        }
        static Transform Node(Transform parent,string name,Vector3 at)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=at;return t;}
        static GameObject P(Transform parent,string name,Vector3 at,Vector3 scale,Material mat,PrimitiveType primitive=PrimitiveType.Sphere)
        {
            var o=GameObject.CreatePrimitive(primitive);o.name=name;o.transform.SetParent(parent,false);o.transform.localPosition=at;o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(o.GetComponent<Collider>());return o;
        }
        public static void Build(FixtureActorBody actor,int number)
        {
            if(number==2){BuildJinwoo(actor);return;}
            if(number==6){BuildTaegyeom(actor);return;}
            if(number==18){BuildMinseo(actor);return;}
            string[] hair={"", "594A40","DFD8CD","202B39","272730","20252B","352B2A","AD6336","62717D","8B5C42","3A302A","463527","2B2836","242333","222329","9D443E","45364E","C9AB75","34312D"};
            string[] clothes={"","7B6951","65424D","28344D","35343A","9B9D9C","3B4650","75645D","3B4855","625869","D8CDB5","49594F","9B657D","503947","383843","A68A4F","644459","6C9997","728184"};
            var skin=M("Skin","D6BBA3");var cloth=M("Cloth_"+number,clothes[number]);var hairMat=M("Hair_"+number,hair[number]);var ivory=M("Ivory","DCD7C9");var ink=M("Ink","161D27");var metal=M("Brass","A18B61");var eyes=M("Iris_"+number,number==2?"C5BDBD":number==4?"999CA3":"5D5258");
            float h=actor.Height;var v=Node(actor.transform,"CharacterProxy",Vector3.zero);
            bool skirt=number>=12&&number<=17;bool female=number>=11&&number<=17||number==3;
            P(v,"Tailored coat",new Vector3(0,h*.585f,0),new Vector3(female?.34f:.40f,h*.24f,.215f),cloth,PrimitiveType.Capsule);
            P(v,"Shirt front",new Vector3(0,h*.65f,.105f),new Vector3(.15f,h*.135f,.018f),ivory,PrimitiveType.Cube);
            P(v,"Tie",new Vector3(0,h*.66f,.121f),new Vector3(.031f,.16f,.017f),number==5?M("Teal tie","3B777B"):ink,PrimitiveType.Cube);
            P(v,"Waist",new Vector3(0,h*.425f,0),new Vector3(.31f,.19f,.22f),cloth);
            if(skirt)P(v,"Skirt",new Vector3(0,h*.35f,0),new Vector3(.39f,h*.22f,.26f),cloth,PrimitiveType.Cube);
            P(v,"Neck",new Vector3(0,h*.77f,0),new Vector3(.11f,.12f,.10f),skin,PrimitiveType.Cylinder);
            P(v,"Face",new Vector3(0,h*.868f,.024f),new Vector3(.216f,.279f,.198f),skin);
            P(v,"Hair silhouette",new Vector3(0,h*.90f,-.021f),new Vector3(.237f,.235f,.215f),hairMat);
            BuildReadableFringe(v,number,h,hairMat);
            foreach(int side in new[]{-1,1}){
                P(v,"Ear",new Vector3(side*.109f,h*.86f,.015f),new Vector3(.038f,.065f,.035f),skin);
                P(v,"Eye white",new Vector3(side*.046f,h*.875f,.113f),new Vector3(.056f,.029f,.014f),ivory);
                P(v,"Iris",new Vector3(side*.045f,h*.875f,.122f),new Vector3(.025f,.027f,.006f),eyes);
                P(v,"Pupil",new Vector3(side*.045f,h*.875f,.126f),new Vector3(.011f,.023f,.004f),ink);
                P(v,"Lash",new Vector3(side*.047f,h*.885f,.123f),new Vector3(.060f,.005f,.010f),ink,PrimitiveType.Cube);
                BuildArm(actor,v,side,cloth,skin,ivory);
                var leg=Node(v,side<0?"LegLeft":"LegRight",new Vector3(side*.088f,h*.41f,0));
                P(leg,"Leg",new Vector3(0,-h*.16f,0),new Vector3(.12f,h*.19f,.13f),ink,PrimitiveType.Capsule);
                P(leg,"Shoe",new Vector3(0,-h*.385f,.04f),new Vector3(.132f,.084f,.246f),ink);
                if(female||number==4||number==8)P(v,"Side hair",new Vector3(side*.11f,h*.845f,-.007f),new Vector3(.055f,female?.22f:.14f,.105f),hairMat,PrimitiveType.Capsule);
            }
            P(v,"Nose",new Vector3(0,h*.851f,.129f),new Vector3(.018f,.027f,.026f),skin);
            P(v,"Mouth",new Vector3(0,h*.824f,.111f),new Vector3(.033f,.003f,.006f),M("Lip","745853"),PrimitiveType.Cube);
            for(int k=0;k<3;k++)P(v,"Button",new Vector3(0,h*.57f-k*.068f,.116f),Vector3.one*.013f,metal);
            if(number==6||number==13)foreach(int side in new[]{-1,1}){
                P(v,"Glasses top",new Vector3(side*.05f,h*.89f,.135f),new Vector3(.077f,.005f,.01f),metal,PrimitiveType.Cube);
                P(v,"Glasses bottom",new Vector3(side*.05f,h*.862f,.135f),new Vector3(.077f,.005f,.01f),metal,PrimitiveType.Cube);
            }
            if(number==10||number==11)P(v,"Apron",new Vector3(0,h*.48f,.139f),new Vector3(.29f,.5f,.016f),number==10?ivory:cloth,PrimitiveType.Cube);
            if(number==14||number==16)P(v,"Long hair",new Vector3(0,h*.75f,-.14f),new Vector3(.24f,.40f,.05f),hairMat,PrimitiveType.Capsule);
            if(number==17)foreach(int side in new[]{-1,1})P(v,"Twin tail",new Vector3(side*.157f,h*.84f,-.04f),new Vector3(.10f,.32f,.095f),hairMat,PrimitiveType.Capsule);
            if(number==2)for(int i=0;i<8;i++)P(v,"Soft curl",new Vector3(Mathf.Sin(i)*.106f,h*.939f+Mathf.Cos(i)*.028f,Mathf.Cos(i)*.083f),Vector3.one*.08f,hairMat);
            CombineStillParts(v,"CH_"+number.ToString("00"));
        }
        public static void PlayerArms(FixtureActorBody actor)
        {
            var root=Node(actor.transform,"CharacterProxy",Vector3.zero);
            foreach(int side in new[]{-1,1})BuildArm(actor,root,side,M("Cloth_1","7B6951"),M("Skin","D6BBA3"),M("Ivory","DCD7C9"));
        }
        static void BuildArm(FixtureActorBody actor,Transform parent,int side,Material cloth,Material skin,Material cuff)
        {
            float h=actor.Height;var arm=Node(parent,side<0?"ArmLeft":"ArmRight",Vector3.zero);
            var rig=arm.gameObject.AddComponent<ActorArmRig>();rig.Side=side;rig.UpperLength=h*.155f;rig.ForearmLength=h*.14f;
            rig.Upper=Node(arm,"Shoulder",new Vector3(side*.223f,h*.76f,0));
            rig.Forearm=Node(rig.Upper,"Elbow",Vector3.down*rig.UpperLength);
            rig.Wrist=Node(rig.Forearm,"Wrist",Vector3.down*rig.ForearmLength);
            P(rig.Upper,"Upper sleeve",Vector3.down*rig.UpperLength*.5f,new Vector3(.106f,rig.UpperLength*.5f,.114f),cloth,PrimitiveType.Capsule);
            P(rig.Forearm,"Lower sleeve",Vector3.down*rig.ForearmLength*.46f,new Vector3(.091f,rig.ForearmLength*.48f,.098f),cloth,PrimitiveType.Capsule);
            P(rig.Forearm,"Cuff",Vector3.down*(rig.ForearmLength-.016f),new Vector3(.096f,.018f,.093f),cuff,PrimitiveType.Cylinder);
            P(rig.Wrist,"Palm",new Vector3(0,-.025f,0),new Vector3(.067f,.060f,.030f),skin);
            rig.FingerRoots=new Transform[4];rig.FingerTips=new Transform[4];
            for(int i=0;i<4;i++){
                float length=i==3?.019f:.023f;
                var finger=Node(rig.Wrist,"Finger "+i,new Vector3((i-1.5f)*.0145f,-.044f,.012f));
                P(finger,"Proximal",Vector3.down*length*.5f,new Vector3(.012f,length*.5f,.012f),skin,PrimitiveType.Capsule);
                var tip=Node(finger,"Knuckle",Vector3.down*length);
                P(tip,"Distal",Vector3.down*length*.5f,new Vector3(.011f,length*.5f,.011f),skin,PrimitiveType.Capsule);
                rig.FingerRoots[i]=finger;rig.FingerTips[i]=tip;
            }
            rig.Thumb=Node(rig.Wrist,"Thumb joint",new Vector3(side*.030f,-.010f,.012f));
            P(rig.Thumb,"Thumb",Vector3.down*.020f,new Vector3(.016f,.020f,.016f),skin,PrimitiveType.Capsule);rig.Curl(0);
            var old=side>0?actor.RightHand:actor.LeftHand;if(old)Object.DestroyImmediate(old.gameObject);
            rig.Grip=Node(rig.Wrist,side>0?"RightHand":"LeftHand",rig.GripOffset);
            if(side>0)actor.RightHand=rig.Grip;else actor.LeftHand=rig.Grip;
            rig.Pose(actor.transform.TransformPoint(new Vector3(side*.255f,h*.47f,.07f)),actor.transform.rotation);
        }
        public static void Butler(FixtureActorBody actor)
        {
            var root=actor.transform;var black=M("Butler coat","1C2430");var white=M("Butler ivory","E3DFD1");var brass=M("Butler brass","AA9670");var blue=M("Aquarium water","528D98");
            P(root,"Tailcoat",new Vector3(0,.97f,0),new Vector3(.40f,.46f,.23f),black,PrimitiveType.Capsule);
            P(root,"Waistcoat",new Vector3(0,1.15f,.12f),new Vector3(.21f,.31f,.027f),white,PrimitiveType.Cube);
            P(root,"Tie",new Vector3(0,1.24f,.14f),new Vector3(.043f,.14f,.022f),black,PrimitiveType.Cube);
            foreach(int side in new[]{-1,1}){
                P(root,"Tail",new Vector3(side*.09f,.58f,-.065f),new Vector3(.20f,.43f,.12f),black,PrimitiveType.Cube);
                P(root,"Leg",new Vector3(side*.10f,.39f,0),new Vector3(.135f,.34f,.15f),black,PrimitiveType.Capsule);
                P(root,"Shoe",new Vector3(side*.10f,.045f,.065f),new Vector3(.14f,.09f,.25f),black);
                var arm=P(root,"Arm",new Vector3(side*.25f,1.04f,.015f),new Vector3(.11f,.30f,.12f),black,PrimitiveType.Capsule);arm.transform.localRotation=Quaternion.Euler(0,0,side*15);
                P(root,"Glove",new Vector3(side*.28f,.75f,.055f),new Vector3(.083f,.13f,.068f),white);
            }
            P(root,"Aquarium",new Vector3(0,1.61f,0),new Vector3(.34f,.28f,.25f),blue,PrimitiveType.Cube);
            foreach(float y in new[]{1.47f,1.75f}){
                P(root,"Tank rim",new Vector3(0,y,0),new Vector3(.355f,.017f,.27f),brass,PrimitiveType.Cube);
            }
            foreach(int side in new[]{-1,1})P(root,"Tank corner",new Vector3(side*.17f,1.61f,.127f),new Vector3(.012f,.28f,.014f),brass,PrimitiveType.Cube);
            P(root,"Silver fish",new Vector3(.02f,1.62f,.13f),new Vector3(.11f,.045f,.025f),white);
            var tail=P(root,"Fish tail",new Vector3(-.047f,1.62f,.13f),new Vector3(.031f,.055f,.014f),white,PrimitiveType.Cube);tail.transform.localRotation=Quaternion.Euler(0,0,45);
            for(int i=0;i<3;i++)P(root,"Water reflection",new Vector3(-.09f+i*.06f,1.67f+i*.012f,.131f),new Vector3(.034f,.006f,.006f),white,PrimitiveType.Cube);
            CombineStillParts(root,"PRES_YUSTI");
        }
        static void CombineStillParts(Transform root,string id)
        {
            const string meshFolder="Assets/BASSLINE/Characters/Meshes/PresentationV10";Directory.CreateDirectory(meshFolder);
            var joints=new[]{root.Find("LegLeft"),root.Find("LegRight"),root.Find("ArmLeft"),root.Find("ArmRight")}.Where(t=>t).ToArray();
            var renderers=root.GetComponentsInChildren<MeshRenderer>().Where(r=>!joints.Any(j=>r.transform.IsChildOf(j))).ToArray();
            foreach(var group in renderers.GroupBy(r=>r.sharedMaterial)){
                var mesh=new Mesh{name=id+"_"+group.Key.name};mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.CombineMeshes(group.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=root.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray(),true,true);
                string path=meshFolder+"/"+mesh.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(old){old.Clear();old.indexFormat=mesh.indexFormat;old.vertices=mesh.vertices;old.normals=mesh.normals;old.uv=mesh.uv;old.triangles=mesh.triangles;old.bounds=mesh.bounds;Object.DestroyImmediate(mesh);mesh=old;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);
                var node=new GameObject(group.Key.name);node.transform.SetParent(root,false);node.AddComponent<MeshFilter>().sharedMesh=mesh;node.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
            foreach(var renderer in renderers)Object.DestroyImmediate(renderer.gameObject);
        }
    }
}
