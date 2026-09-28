using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using BASSLINE.AuthoringData;
namespace BASSLINE.Authoring
{
    public static partial class CharacterPresentationBuilder
    {
        const string SculptFolder="Assets/BASSLINE/Characters/Meshes/SculptV14";
        static Material S(string name,string color){var m=M("T14_"+name,color);ColorUtility.TryParseHtmlString("#"+color,out var c);m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",.12f);EditorUtility.SetDirty(m);return m;}
        static Mesh MeshAsset(string id,List<Vector3> vertices,List<int> triangles)
        {
            Directory.CreateDirectory(SculptFolder);var mesh=new Mesh{name=id};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=SculptFolder+"/"+id+".asset";var prior=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(prior){prior.Clear();prior.SetVertices(vertices);prior.SetTriangles(triangles,0);prior.RecalculateNormals();prior.RecalculateBounds();UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(prior);return prior;}
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static GameObject Surface(Transform parent,string id,Mesh mesh,Material material)
        {
            var o=new GameObject(id,typeof(MeshFilter),typeof(MeshRenderer));o.transform.SetParent(parent,false);o.GetComponent<MeshFilter>().sharedMesh=mesh;o.GetComponent<MeshRenderer>().sharedMaterial=material;return o;
        }
        // Profile entries: y, half width, front depth, back depth. Rings use metres, not primitive scales.
        static Mesh Loft(string id,(float y,float width,float front,float back)[] rows,int count=32,float centerZ=0,float flatten=1)
        {
            var v=new List<Vector3>();var t=new List<int>();
            foreach(var r in rows)for(int j=0;j<count;j++){
                float a=j*Mathf.PI*2/count,c=Mathf.Cos(a);float z=c>=0?r.front*Mathf.Pow(c,flatten):-r.back*Mathf.Pow(-c,flatten);
                v.Add(new Vector3(r.width*Mathf.Sin(a),r.y,z+centerZ));
            }
            bool up=rows[rows.Length-1].y>rows[0].y;
            for(int r=0;r<rows.Length-1;r++)for(int j=0;j<count;j++){
                int a=r*count+j,b=r*count+(j+1)%count,c=a+count,d=b+count;
                if(up)t.AddRange(new[]{a,b,c,b,d,c});else t.AddRange(new[]{a,c,b,b,c,d});
            }
            for(int end=0;end<2;end++){
                int ring=end==0?0:rows.Length-1,center=v.Count;v.Add(new Vector3(0,rows[ring].y,centerZ));
                for(int j=0;j<count;j++){int a=ring*count+j,b=ring*count+(j+1)%count;bool reverse=(end==0)==up;t.AddRange(reverse?new[]{center,b,a}:new[]{center,a,b});}
            }
            return MeshAsset(id,v,t);
        }
        static Mesh Panel(string id,params Vector3[] contour)
        {
            // Ear clipping preserves notches in lapels and the collar instead of filling their concavity.
            var v=contour.ToList();var triangles=new List<int>();var indices=Enumerable.Range(0,v.Count).ToList();float area=0;
            for(int i=0;i<v.Count;i++){var a=v[i];var b=v[(i+1)%v.Count];area+=a.x*b.y-b.x*a.y;}
            if(area<0)indices.Reverse();int safety=v.Count*v.Count;
            while(indices.Count>2&&safety-->0){bool clipped=false;
                for(int i=0;i<indices.Count;i++){
                    int a=indices[(i+indices.Count-1)%indices.Count],b=indices[i],c=indices[(i+1)%indices.Count];
                    if(Cross2(v[a],v[b],v[c])<=.0000000001f)continue;
                    if(indices.Any(p=>p!=a&&p!=b&&p!=c&&Cross2(v[a],v[b],v[p])>=0&&Cross2(v[b],v[c],v[p])>=0&&Cross2(v[c],v[a],v[p])>=0))continue;
                    triangles.AddRange(new[]{a,b,c});indices.RemoveAt(i);clipped=true;break;
                }if(!clipped)break;
            }
            if(indices.Count>2)throw new InvalidOperationException("Invalid clothing contour: "+id);
            int n=v.Count;v.AddRange(contour.Select(p=>p-Vector3.forward*.002f));var front=triangles.ToArray();
            for(int i=0;i<front.Length;i+=3)triangles.AddRange(new[]{front[i]+n,front[i+2]+n,front[i+1]+n});
            return MeshAsset(id,v,triangles);
        }
        static float Cross2(Vector3 a,Vector3 b,Vector3 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
        static Vector3 Cubic(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t){float s=1-t;return s*s*s*a+3*s*s*t*b+3*s*t*t*c+t*t*t*d;}
        static void Lock(Transform root,string id,Material mat,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float width,float depth)
        {
            const int rings=13,sides=8;var v=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<rings;i++){
                float t=i/(float)(rings-1);var p=Cubic(a,b,c,d,t);var tangent=(Cubic(a,b,c,d,Mathf.Min(1,t+.01f))-Cubic(a,b,c,d,Mathf.Max(0,t-.01f))).normalized;
                var side=Vector3.Cross(Vector3.forward,tangent).normalized;if(side.sqrMagnitude<.01f)side=Vector3.right;var normal=Vector3.Cross(tangent,side).normalized;
                float taper=Mathf.Lerp(.64f,1,Mathf.Sin(t*Mathf.PI));taper*=Mathf.Pow(1-t,.43f);taper=Mathf.Max(.018f,taper);
                for(int j=0;j<sides;j++){float angle=j*Mathf.PI*2/sides;v.Add(p+side*(Mathf.Cos(angle)*width*.5f*taper)+normal*(Mathf.Sin(angle)*depth*taper));}
            }
            for(int i=0;i<rings-1;i++)for(int j=0;j<sides;j++){int a0=i*sides+j,b0=i*sides+(j+1)%sides,c0=a0+sides,d0=b0+sides;triangles.AddRange(new[]{a0,b0,c0,b0,d0,c0});}
            Surface(root,id,MeshAsset(id,v,triangles),mat);
        }
        static void Line(Transform root,string id,Material material,float radius,params Vector3[] points)
        {
            const int sides=6;var v=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<points.Length;i++){
                var tangent=(points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(0,i-1)]).normalized;
                var side=Vector3.Cross(tangent,Vector3.forward).normalized;if(side.sqrMagnitude<.01f)side=Vector3.right;var normal=Vector3.Cross(tangent,side).normalized;
                for(int j=0;j<sides;j++){float a=j*Mathf.PI*2/sides;v.Add(points[i]+radius*(side*Mathf.Cos(a)+normal*Mathf.Sin(a)));}
            }
            for(int i=0;i<points.Length-1;i++)for(int j=0;j<sides;j++){int a=i*sides+j,b=i*sides+(j+1)%sides,c=a+sides,d=b+sides;triangles.AddRange(new[]{a,b,c,b,d,c});}
            Surface(root,id,MeshAsset(id,v,triangles),material);
        }
        static void Oval(Transform root,string id,Material material,Vector3 center,float width,float height)
        {
            Surface(root,id,Panel(id,Enumerable.Range(0,20).Select(i=>center+new Vector3(Mathf.Cos(i*Mathf.PI/10)*width*.5f,Mathf.Sin(i*Mathf.PI/10)*height*.5f,0)).ToArray()),material);
        }
        static void BuildReadableFringe(Transform root,int number,float h,Material hair)
        {
            // Temporary hairstyle replacement for the remaining cast: no vertically doubled capsules.
            for(int j=0;j<5;j++){
                float x=(j-2)*.039f;float lean=number%2==0?.038f:-.038f;float endY=h*.896f+(j%2)*.015f;
                Lock(root,"CH"+number+"_Fringe_"+j,hair,new Vector3(x*.45f,h*.955f,.01f),new Vector3(x+lean,h*.963f,.10f),new Vector3(x+lean,h*.933f,.14f),new Vector3(x*.85f-lean*.2f,endY,.113f),.063f,.014f);
            }
        }
        static void BuildTaegyeom(FixtureActorBody actor)
        {
            float h=actor.Height;var v=Node(actor.transform,"CharacterProxy",Vector3.zero);
            var suit=S("Suit","44434A");var lapel=S("Lapel","3D3C42");var skin=S("Skin","DBC7B9");var hair=S("Hair","3D3538");var sheen=S("HairPlanes","51464A");var shirt=S("Shirt","E3DED5");var tie=S("Tie","69564D");var ink=S("Ink","29252B");var metal=S("Frame","8A827E");var iris=S("Iris","726B65");var lip=S("Lips","98756E");var leather=S("Shoe","302D32");
            Surface(v,"T14_Jacket",Loft("T14_Jacket",new[]{(h*.485f,.197f,.126f,.123f),(h*.54f,.180f,.127f,.118f),(h*.59f,.162f,.120f,.100f),(h*.68f,.185f,.130f,.096f),(h*.741f,.223f,.107f,.093f),(h*.760f,.216f,.074f,.073f),(h*.799f,.060f,.047f,.050f)},40,0,.48f),suit);
            Surface(v,"T14_Neck",Loft("T14_Neck",new[]{(h*.754f,.058f,.045f,.049f),(h*.800f,.051f,.043f,.049f),(h*.837f,.047f,.045f,.055f)},24),skin);
            Surface(v,"T14_Face",Loft("T14_Face",new[]{(h*.823f,.020f,.073f,.042f),(h*.834f,.048f,.086f,.067f),(h*.855f,.078f,.096f,.091f),(h*.882f,.101f,.103f,.105f),(h*.915f,.107f,.104f,.109f),(h*.946f,.096f,.091f,.103f),(h*.968f,.069f,.061f,.073f),(h*.979f,.008f,.009f,.013f)},40,0,.5f),skin);
            Surface(v,"T14_Shirt",Panel("T14_Shirt",new Vector3(-.052f,h*.799f,.060f),new Vector3(-.077f,h*.710f,.147f),new Vector3(-.029f,h*.607f,.145f),new Vector3(-.036f,h*.563f,.143f),new Vector3(.036f,h*.563f,.143f),new Vector3(.029f,h*.607f,.145f),new Vector3(.077f,h*.710f,.147f),new Vector3(.052f,h*.799f,.060f)),shirt);
            Surface(v,"T14_Tie",Panel("T14_Tie",new Vector3(-.012f,h*.764f,.133f),new Vector3(-.015f,h*.710f,.151f),new Vector3(-.021f,h*.584f,.151f),new Vector3(0,h*.558f,.151f),new Vector3(.021f,h*.584f,.151f),new Vector3(.015f,h*.710f,.151f),new Vector3(.012f,h*.764f,.133f)),tie);
            Surface(v,"T14_TieKnot",Panel("T14_TieKnot",new Vector3(-.019f,h*.783f,.103f),new Vector3(-.010f,h*.760f,.136f),new Vector3(.010f,h*.760f,.136f),new Vector3(.019f,h*.783f,.103f),new Vector3(0,h*.793f,.090f)),tie);
            foreach(int side in new[]{-1,1}){
                Vector3 V(float x,float y,float z)=>new Vector3(side*x,h*y,z);
                Surface(v,"T14_Collar_"+side,Panel("T14_Collar_"+side,V(.045f,.811f,.056f),V(.069f,.783f,.103f),V(.039f,.750f,.146f),V(.018f,.790f,.109f)),shirt);
                Surface(v,"T14_Lapel_"+side,Panel("T14_Lapel_"+side,V(.060f,.800f,.060f),V(.165f,.747f,.117f),V(.122f,.724f,.140f),V(.151f,.711f,.146f),V(.020f,.598f,.156f),V(.058f,.726f,.154f)),lapel);
                Line(v,"T14_LapelSeam_"+side,ink,.0010f,V(.060f,.800f,.063f),V(.165f,.747f,.120f),V(.122f,.724f,.143f),V(.151f,.711f,.149f),V(.020f,.598f,.159f));
                Surface(v,"T14_Pocket_"+side,Panel("T14_Pocket_"+side,V(.073f,.570f,.123f),V(.150f,.572f,.112f),V(.153f,.559f,.113f),V(.074f,.555f,.122f)),lapel);
                Line(v,"T14_PocketSeam_"+side,ink,.0014f,V(.075f,.570f,.126f),V(.149f,.572f,.115f));
                P(v,"Ear "+side,V(.111f,.886f,.002f),new Vector3(.034f,.060f,.035f),skin);
                P(v,"Ear fold "+side,V(.118f,.886f,.020f),new Vector3(.015f,.037f,.006f),lip);
                float eye=h*.891f;float cx=side*.049f;
                float EyeZ(float x)=>.1035f*Mathf.Pow(Mathf.Clamp01(1-x*x/(.103f*.103f)),.25f);
                var whiteContour=Enumerable.Range(0,9).Select(i=>{float t=i/8f;float x=cx+side*Mathf.Lerp(-.034f,.032f,t);return new Vector3(x,eye+.009f*Mathf.Sin(t*Mathf.PI)+.004f*t,EyeZ(x)+.002f);}).Concat(Enumerable.Range(1,7).Select(i=>{float t=1-i/8f;float x=cx+side*Mathf.Lerp(-.034f,.032f,t);return new Vector3(x,eye-.007f*Mathf.Sin(t*Mathf.PI)+.004f*t,EyeZ(x)+.002f);})).ToArray();
                Surface(v,"T14_EyeWhite_"+side,Panel("T14_EyeWhite_"+side,whiteContour),shirt);
                Oval(v,"T14_Iris_"+side,iris,new Vector3(cx,eye+.001f,EyeZ(cx)+.005f),.021f,.016f);
                Oval(v,"T14_Pupil_"+side,ink,new Vector3(cx,eye+.001f,EyeZ(cx)+.006f),.007f,.014f);
                Oval(v,"T14_Glint_"+side,shirt,new Vector3(cx-.004f,eye+.004f,EyeZ(cx)+.007f),.003f,.003f);
                Line(v,"T14_UpperLid_"+side,ink,.0016f,whiteContour.Take(9).Select(p=>p+Vector3.forward*.002f).ToArray());
                Line(v,"T14_LowerLid_"+side,lip,.0008f,whiteContour.Skip(8).Concat(new[]{whiteContour[0]}).Select(p=>p+Vector3.forward*.002f).ToArray());
                Line(v,"T14_Brow_"+side,hair,.0019f,new Vector3(cx-side*.029f,eye+.025f,EyeZ(cx-side*.029f)+.003f),new Vector3(cx,eye+.030f,EyeZ(cx)+.003f),new Vector3(cx+side*.029f,eye+.025f,EyeZ(cx+side*.029f)+.003f));
                var rim=new[]{new Vector3(cx-side*.038f,eye+.022f,.128f),new Vector3(cx+side*.030f,eye+.023f,.124f),new Vector3(cx+side*.043f,eye+.009f,.117f),new Vector3(cx+side*.029f,eye-.017f,.124f),new Vector3(cx-side*.029f,eye-.019f,.128f),new Vector3(cx-side*.041f,eye-.006f,.129f),new Vector3(cx-side*.038f,eye+.022f,.128f)};
                Line(v,"T14_Glasses_"+side,metal,.0012f,rim.Select(p=>p-Vector3.forward*.009f).ToArray());
                Line(v,"T14_Temple_"+side,metal,.0012f,V(.091f,.902f,.110f),V(.115f,.901f,.051f),V(.115f,.885f,-.021f));
                BuildArm(actor,v,side,suit,skin,shirt);
                RefineSuitSleeve(actor,side,suit,shirt);
                var leg=Node(v,side<0?"LegLeft":"LegRight",new Vector3(side*.089f,h*.535f,0));
                Surface(leg,"T14_Trouser_"+side,Loft("T14_Trouser_"+side,new[]{(0f,.089f,.097f,.092f),(-h*.10f,.077f,.082f,.080f),(-h*.22f,.065f,.067f,.070f),(-h*.31f,.061f,.068f,.069f),(-h*.45f,.062f,.071f,.073f),(-h*.499f,.067f,.077f,.075f)},24),suit);
                Line(leg,"T14_TrouserCrease_"+side,lapel,.0008f,new Vector3(0,-h*.065f,.098f),new Vector3(0,-h*.20f,.074f),new Vector3(.006f,-h*.34f,.075f),new Vector3(.002f,-h*.49f,.080f));
                var shoe=Node(leg,"Shoe",Vector3.down*h*.535f);
                Surface(shoe,"T14_Sole_"+side,Loft("T14_Sole_"+side,new[]{(.009f,.069f,.144f,.091f),(.024f,.070f,.145f,.092f)},32,.035f),ink);
                Surface(shoe,"T14_Shoe_"+side,Loft("T14_Shoe_"+side,new[]{(.024f,.066f,.138f,.085f),(.048f,.066f,.132f,.081f),(.074f,.054f,.104f,.065f),(.095f,.041f,.057f,.056f)},32,.035f),leather);
                for(int i=0;i<3;i++)Line(shoe,"T14_Lace_"+side+"_"+i,ink,.0017f,new Vector3(-.026f,.085f-i*.006f,.087f+i*.013f),new Vector3(.026f,.085f-i*.006f,.087f+i*.013f));
            }
            Line(v,"T14_GlassesBridge",metal,.0012f,new Vector3(-.012f,h*.896f,.122f),new Vector3(0,h*.900f,.126f),new Vector3(.012f,h*.896f,.122f));
            Surface(v,"T14_Nose",Loft("T14_Nose",new[]{(h*.862f,.006f,.104f,0f),(h*.866f,.009f,.116f,0f),(h*.873f,.005f,.112f,0f),(h*.890f,.003f,.104f,0f)},20),skin);
            Line(v,"T14_Mouth",lip,.0014f,new Vector3(-.023f,h*.845f,.098f),new Vector3(-.002f,h*.846f,.106f),new Vector3(.021f,h*.844f,.099f));
            foreach(float y in new[]{.612f,.562f})P(v,"Jacket button",new Vector3(-.033f,h*y,.160f),new Vector3(.014f,.014f,.005f),ink);
            P(v,"Belt",new Vector3(0,h*.549f,.146f),new Vector3(.079f,.018f,.009f),leather,PrimitiveType.Cube);
            P(v,"Belt buckle",new Vector3(0,h*.549f,.152f),new Vector3(.026f,.021f,.004f),metal,PrimitiveType.Cube);
            BuildCenterPart(v,h,hair,sheen);
            CombineStillParts(v,"CH_06");
        }
        static void RefineSuitSleeve(FixtureActorBody actor,int side,Material suit,Material shirt)
        {
            var arm=actor.GetComponentsInChildren<ActorArmRig>().Single(r=>r.Side==side);
            void Replace(Transform part,Mesh mesh){part.localPosition=Vector3.zero;part.localRotation=Quaternion.identity;part.localScale=Vector3.one;part.GetComponent<MeshFilter>().sharedMesh=mesh;}
            float length=arm.UpperLength;
            Replace(arm.Upper.Find("Upper sleeve"),Loft("T14_UpperSleeve_"+side,new[]{(.002f,.033f,.039f,.039f),(-.013f,.053f,.057f,.057f),(-length*.22f,.061f,.063f,.062f),(-length*.69f,.052f,.056f,.055f),(-length-.010f,.048f,.050f,.050f)},24));
            length=arm.ForearmLength;
            Replace(arm.Forearm.Find("Lower sleeve"),Loft("T14_LowerSleeve_"+side,new[]{(.012f,.048f,.050f,.050f),(-length*.20f,.051f,.054f,.053f),(-length*.73f,.044f,.047f,.045f),(-length+.014f,.042f,.044f,.043f)},24));
            Replace(arm.Forearm.Find("Cuff"),Loft("T14_Cuff_"+side,new[]{(-length+.020f,.043f,.045f,.044f),(-length-.003f,.043f,.044f,.044f)},24));
        }
        static void BuildCenterPart(Transform root,float h,Material hair,Material sheen)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();const int around=40,rings=13;
            for(int i=0;i<rings;i++)for(int j=0;j<around;j++){
                float a=j*Mathf.PI*2/around;float front=Mathf.Max(0,Mathf.Cos(a));float end=Mathf.Lerp(2.45f,.99f,Mathf.Pow(front,2));float p=.025f+(end-.025f)*i/(rings-1f);
                float radius=p>Mathf.PI*.5f?Mathf.Max(.86f,Mathf.Sin(p)):Mathf.Sin(p);
                vertices.Add(new Vector3(Mathf.Sin(a)*radius*.122f,h*.914f+Mathf.Cos(p)*h*.075f,Mathf.Cos(a)*radius*.117f-.010f));
            }
            for(int i=0;i<rings-1;i++)for(int j=0;j<around;j++){int a=i*around+j,b=i*around+(j+1)%around,c=a+around,d=b+around;triangles.AddRange(new[]{a,c,b,b,c,d});}
            Surface(root,"T14_HairCap",MeshAsset("T14_HairCap",vertices,triangles),hair);
            foreach(int side in new[]{-1,1}){
                Vector3 P0(float x,float y,float z)=>new Vector3(side*x,h*y,z);
                Lock(root,"T14_PartInner_"+side,hair,P0(.004f,.976f,.051f),P0(.043f,.980f,.108f),P0(.051f,.945f,.121f),P0(.026f,.911f,.112f),.038f,.006f);
                Lock(root,"T14_PartSweep_"+side,hair,P0(.011f,.983f,.024f),P0(.080f,.987f,.077f),P0(.110f,.954f,.123f),P0(.092f,.890f,.090f),.070f,.008f);
                Lock(root,"T14_PartOuter_"+side,hair,P0(.022f,.979f,-.027f),P0(.113f,.989f,.011f),P0(.142f,.952f,.043f),P0(.120f,.880f,.008f),.074f,.010f);
                Lock(root,"T14_Nape_"+side,hair,P0(.039f,.962f,-.075f),P0(.127f,.956f,-.080f),P0(.125f,.912f,-.108f),P0(.076f,.869f,-.084f),.061f,.010f);
                Lock(root,"T14_HairPlane_"+side,sheen,P0(.022f,.983f,.041f),P0(.080f,.979f,.099f),P0(.090f,.957f,.140f),P0(.102f,.927f,.120f),.012f,.0017f);
            }
        }
    }
}
