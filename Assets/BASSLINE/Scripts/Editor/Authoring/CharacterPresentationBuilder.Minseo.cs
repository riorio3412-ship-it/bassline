using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BASSLINE.AuthoringData;
namespace BASSLINE.Authoring
{
    public static partial class CharacterPresentationBuilder
    {
        // Individual workwear silhouette. Metres, +Z front. Cosmetic parts never add physics shapes.
        static void BuildMinseo(FixtureActorBody actor)
        {
            float h=actor.Height;var root=Node(actor.transform,"CharacterProxy",Vector3.zero);
            var cloth=S("Minseo_Shirt","687681");var seam=S("Minseo_Seam","465461");
            var skin=S("Minseo_Skin","CDB29C");var shade=S("Minseo_SkinShade","8C6C60");
            var hair=S("Minseo_Hair","302C2C");var sheen=S("Minseo_HairPlane","484140");
            var tee=S("Minseo_Tee","D7CDBB");var dark=S("Minseo_Trousers","36363C");
            var ink=S("Minseo_Ink","202129");var leather=S("Minseo_Boots","49403A");
            var silver=S("Minseo_Steel","9BA6A8");var iris=S("Minseo_Eyes","64685F");
            Surface(root,"M17_ShirtBody",OpenWorkShirt("M17_ShirtBody",new[]{
                (h*.482f,.194f,.128f,.113f),(h*.52f,.193f,.137f,.112f),(h*.58f,.182f,.140f,.105f),
                (h*.66f,.211f,.148f,.111f),(h*.724f,.244f,.126f,.102f),(h*.765f,.250f,.083f,.081f),
                (h*.794f,.066f,.047f,.058f)},40),cloth);
            Surface(root,"M17_Neck",Loft("M17_Neck",new[]{(h*.767f,.069f,.051f,.055f),(h*.821f,.058f,.048f,.055f),(h*.841f,.052f,.050f,.060f)},24),skin);
            Surface(root,"M17_Face",Loft("M17_Face",new[]{
                (h*.824f,.026f,.068f,.038f),(h*.834f,.054f,.082f,.063f),(h*.856f,.091f,.094f,.093f),
                (h*.882f,.109f,.106f,.104f),(h*.913f,.108f,.106f,.111f),(h*.946f,.099f,.092f,.107f),
                (h*.972f,.066f,.062f,.076f),(h*.981f,.008f,.009f,.014f)},40,0,.5f),skin);
            Surface(root,"M17_Tee",Loft("M17_Tee",new[]{(h*.498f,.177f,.131f,.102f),(h*.58f,.174f,.134f,.099f),(h*.66f,.200f,.142f,.105f),(h*.724f,.234f,.120f,.096f),(h*.76f,.232f,.077f,.075f),(h*.786f,.066f,.057f,.062f)},40,0,.48f),tee);
            Line(root,"M17_CrewNeck",shade,.0012f,new Vector3(-.059f,h*.787f,.034f),new Vector3(-.035f,h*.787f,.053f),new Vector3(0,h*.787f,.059f),new Vector3(.035f,h*.787f,.053f),new Vector3(.059f,h*.787f,.034f));
            foreach(int side in new[]{-1,1}){
                Vector3 V(float x,float y,float z)=>new Vector3(side*x,h*y,z);
                Surface(root,"M17_Collar_"+side,Panel("M17_Collar_"+side,V(.060f,.806f,.049f),V(.128f,.767f,.110f),V(.076f,.737f,.143f),V(.051f,.776f,.110f)),cloth);
                Line(root,"M17_CollarEdge_"+side,seam,.0014f,V(.061f,.806f,.052f),V(.128f,.767f,.114f),V(.076f,.737f,.147f),V(.051f,.776f,.114f));
                Line(root,"M17_Placket_"+side,seam,.0025f,V(.060f,.750f,.140f),V(.071f,.690f,.161f),V(.069f,.585f,.155f),V(.075f,.485f,.138f));
                Surface(root,"M17_ChestPocket_"+side,Panel("M17_ChestPocket_"+side,V(.094f,.705f,.151f),V(.190f,.702f,.139f),V(.186f,.643f,.149f),V(.140f,.631f,.163f),V(.096f,.643f,.164f)),cloth);
                Line(root,"M17_PocketStitch_"+side,seam,.0012f,V(.096f,.700f,.154f),V(.098f,.645f,.167f),V(.140f,.634f,.166f),V(.183f,.646f,.152f),V(.187f,.700f,.143f));
                Surface(root,"M17_PocketFlap_"+side,Panel("M17_PocketFlap_"+side,V(.090f,.710f,.154f),V(.192f,.705f,.144f),V(.188f,.691f,.150f),V(.141f,.680f,.167f),V(.093f,.691f,.165f)),seam);
                P(root,"M17 Pocket button",V(.142f,.690f,.170f),new Vector3(.008f,.008f,.003f),silver);
                Line(root,"M17_ShoulderSeam_"+side,seam,.0014f,V(.132f,.779f,.064f),V(.200f,.773f,.074f),V(.247f,.758f,.084f));
                Line(root,"M17_Hem_"+side,seam,.0013f,V(.075f,.484f,.140f),V(.142f,.482f,.127f),V(.189f,.489f,.069f));
                for(int k=0;k<3;k++)Line(root,"M17_FabricFold_"+side+"_"+k,seam,.0008f,V(.09f,.555f+k*.021f,.148f),V(.14f,.548f+k*.019f,.131f),V(.171f,.544f+k*.02f,.103f));
                P(root,"M17 Ear",V(.113f,.886f,.004f),new Vector3(.035f,.061f,.035f),skin);
                P(root,"M17 Ear fold",V(.122f,.886f,.020f),new Vector3(.014f,.037f,.005f),shade);
                MinseoEye(root,side,h,tee,iris,ink,hair,shade);
                BuildArm(actor,root,side,cloth,skin,cloth);MinseoSleeve(actor,side,cloth,seam,skin,ink,silver);
                var leg=Node(root,side<0?"LegLeft":"LegRight",V(.099f,.536f,0));
                Surface(leg,"M17_Trouser_"+side,Loft("M17_Trouser_"+side,new[]{(0f,.102f,.107f,.098f),(-h*.09f,.096f,.101f,.092f),(-h*.21f,.076f,.082f,.080f),(-h*.255f,.080f,.086f,.081f),(-h*.32f,.070f,.080f,.078f),(-h*.455f,.073f,.080f,.077f),(-h*.471f,.077f,.080f,.079f)},28),dark);
                Line(leg,"M17_TrouserOuterSeam_"+side,ink,.0012f,new Vector3(side*.098f,-.04f,0),new Vector3(side*.077f,-h*.23f,0),new Vector3(side*.073f,-h*.46f,0));
                foreach(float y in new[]{.231f,.261f,.440f})Line(leg,"M17_KneeFold_"+side+"_"+y,ink,.0010f,new Vector3(-.046f,-h*y,.072f),new Vector3(.010f,-h*y+.008f,.088f),new Vector3(.055f,-h*y-.013f,.067f));
                var boot=Node(leg,"Work boot",Vector3.down*h*.536f);
                Surface(boot,"M17_BootSole_"+side,Loft("M17_BootSole_"+side,new[]{(.007f,.079f,.152f,.095f),(.028f,.081f,.154f,.098f)},32,.033f),ink);
                Surface(boot,"M17_Boot_"+side,Loft("M17_Boot_"+side,new[]{(.029f,.077f,.148f,.093f),(.056f,.077f,.145f,.089f),(.084f,.065f,.110f,.079f),(.119f,.060f,.070f,.063f),(.154f,.060f,.060f,.063f)},32,.033f),leather);
                Line(boot,"M17_ToeStitch_"+side,silver,.0008f,new Vector3(-.057f,.052f,.157f),new Vector3(-.030f,.059f,.172f),new Vector3(.030f,.059f,.172f),new Vector3(.057f,.052f,.157f));
                for(int i=0;i<4;i++)Line(boot,"M17_Lace_"+side+"_"+i,ink,.002f,new Vector3(-.030f,.132f-i*.013f,.095f+i*.012f),new Vector3(.029f,.128f-i*.013f,.100f+i*.012f));
            }
            for(int k=0;k<5;k++)P(root,"M17 Placket button",new Vector3(-.071f,h*(.710f-k*.047f),.160f),new Vector3(.008f,.008f,.004f),silver);
            Line(root,"M17_BackYoke",seam,.0012f,new Vector3(-.211f,h*.733f,-.064f),new Vector3(-.11f,h*.720f,-.109f),new Vector3(0,h*.721f,-.116f),new Vector3(.11f,h*.720f,-.109f),new Vector3(.211f,h*.733f,-.064f));
            Line(root,"M17_Mouth",shade,.0015f,new Vector3(-.025f,h*.851f,.100f),new Vector3(-.004f,h*.8515f,.108f),new Vector3(.023f,h*.850f,.102f));
            Surface(root,"M17_Nose",Loft("M17_Nose",new[]{(h*.870f,.006f,.108f,0f),(h*.874f,.010f,.123f,0f),(h*.880f,.006f,.115f,0f),(h*.899f,.003f,.107f,0f)},20),skin);
            MinseoHair(root,h,hair,sheen);
            var thermos=Node(root,"Thermos hip loop",new Vector3(.202f,h*.474f,-.039f));
            Surface(thermos,"M17_Thermos",Loft("M17_Thermos",new[]{(-.105f,.030f,.030f,.030f),(-.096f,.035f,.035f,.035f),(.079f,.035f,.035f,.035f),(.090f,.031f,.031f,.031f)},24),silver);
            Surface(thermos,"M17_ThermosCap",Loft("M17_ThermosCap",new[]{(.087f,.036f,.036f,.036f),(.124f,.036f,.036f,.036f),(.131f,.027f,.027f,.027f)},24),ink);
            Surface(thermos,"M17_ThermosLoop",Loft("M17_ThermosLoop",new[]{(-.075f,.037f,.037f,.037f),(-.045f,.038f,.038f,.038f)},24),leather);
            P(thermos,"Canvas attachment",new Vector3(-.018f,.040f,-.028f),new Vector3(.035f,.190f,.012f),leather,PrimitiveType.Cube);
            CombineStillParts(root,"CH_18");
        }
        static Mesh OpenWorkShirt(string id,(float y,float width,float front,float back)[] rows,int count)
        {
            var v=new List<Vector3>();var t=new List<int>();
            foreach(var row in rows){
                // The front is physically open. A full loft behind the tee caused visible intersections.
                float opening=Mathf.Min(.074f,row.width*.78f),start=Mathf.Asin(opening/row.width);
                for(int j=0;j<=count;j++){
                    float a=Mathf.Lerp(start,2*Mathf.PI-start,j/(float)count),c=Mathf.Cos(a);
                    v.Add(new Vector3(row.width*Mathf.Sin(a),row.y,c>=0?row.front*Mathf.Pow(c,.48f):-row.back*Mathf.Pow(-c,.48f)));
                }
            }
            for(int r=0;r<rows.Length-1;r++)for(int j=0;j<count;j++){int a=r*(count+1)+j,b=a+1,c=a+count+1,d=c+1;t.AddRange(new[]{a,b,c,b,d,c});}
            return MeshAsset(id,v,t);
        }
        static void MinseoEye(Transform root,int side,float h,Material white,Material iris,Material ink,Material hair,Material shade)
        {
            float eye=h*.901f,cx=side*.050f;
            float Z(float x)=>.106f*Mathf.Pow(Mathf.Clamp01(1-x*x/(.109f*.109f)),.25f);
            var contour=Enumerable.Range(0,9).Select(i=>{float t=i/8f,x=cx+side*Mathf.Lerp(-.032f,.031f,t);return new Vector3(x,eye+.008f*Mathf.Sin(t*Mathf.PI)+.0015f*t,Z(x)+.002f);}).Concat(Enumerable.Range(1,7).Select(i=>{float t=1-i/8f,x=cx+side*Mathf.Lerp(-.032f,.031f,t);return new Vector3(x,eye-.007f*Mathf.Sin(t*Mathf.PI)+.0015f*t,Z(x)+.002f);})).ToArray();
            Surface(root,"M17_White_"+side,Panel("M17_White_"+side,contour),white);
            Oval(root,"M17_Iris_"+side,iris,new Vector3(cx,eye+.001f,Z(cx)+.005f),.020f,.015f);
            Oval(root,"M17_Pupil_"+side,ink,new Vector3(cx,eye+.001f,Z(cx)+.006f),.008f,.014f);
            Oval(root,"M17_EyeLight_"+side,white,new Vector3(cx-.003f,eye+.004f,Z(cx)+.007f),.003f,.003f);
            Line(root,"M17_Lid_"+side,ink,.0017f,contour.Take(9).Select(p=>p+Vector3.forward*.002f).ToArray());
            Line(root,"M17_LowerLid_"+side,shade,.0009f,contour.Skip(8).Concat(new[]{contour[0]}).Select(p=>p+Vector3.forward*.002f).ToArray());
            Line(root,"M17_Brow_"+side,hair,.0033f,new Vector3(cx-side*.029f,eye+.026f,Z(cx-side*.029f)+.003f),new Vector3(cx,eye+.027f,Z(cx)+.003f),new Vector3(cx+side*.030f,eye+.022f,Z(cx+side*.030f)+.003f));
        }
        static void MinseoSleeve(FixtureActorBody actor,int side,Material cloth,Material seam,Material skin,Material ink,Material steel)
        {
            var arm=actor.GetComponentsInChildren<ActorArmRig>().Single(r=>r.Side==side);arm.Upper.localPosition=new Vector3(side*.250f,actor.Height*.759f,0);
            void Replace(Transform part,Mesh mesh,Material mat){part.localPosition=Vector3.zero;part.localRotation=Quaternion.identity;part.localScale=Vector3.one;part.GetComponent<MeshFilter>().sharedMesh=mesh;part.GetComponent<Renderer>().sharedMaterial=mat;}
            float upper=arm.UpperLength,lower=arm.ForearmLength;
            Replace(arm.Upper.Find("Upper sleeve"),Loft("M17_UpperSleeve_"+side,new[]{(.009f,.022f,.025f,.025f),(-.010f,.048f,.052f,.052f),(-upper*.20f,.071f,.075f,.073f),(-upper*.67f,.059f,.064f,.061f),(-upper-.018f,.056f,.058f,.056f)},28),cloth);
            Replace(arm.Forearm.Find("Lower sleeve"),Loft("M17_Forearm_"+side,new[]{(.013f,.053f,.055f,.053f),(-lower*.33f,.051f,.055f,.048f),(-lower*.68f,.040f,.043f,.036f),(-lower,.030f,.031f,.027f)},28),skin);
            Replace(arm.Forearm.Find("Cuff"),Loft("M17_RolledSleeve_"+side,new[]{(.010f,.057f,.060f,.058f),(-lower*.23f,.059f,.062f,.060f),(-lower*.32f,.061f,.063f,.060f),(-lower*.38f,.057f,.060f,.057f)},28),cloth);
            Surface(arm.Forearm,"M17_CuffFold_"+side,Loft("M17_CuffFold_"+side,new[]{(-lower*.29f,.062f,.064f,.062f),(-lower*.31f,.062f,.064f,.062f)},28),seam);
            Replace(arm.Wrist.Find("Palm"),Loft("M17_Palm_"+side,new[]{(.009f,.027f,.020f,.015f),(-.023f,.036f,.022f,.017f),(-.050f,.031f,.017f,.014f),(-.057f,.025f,.011f,.010f)},24),skin);
            for(int i=0;i<4;i++){
                float length=i==3?.019f:.023f;
                Replace(arm.FingerRoots[i].Find("Proximal"),Loft("M17_FingerBase_"+side+"_"+i,new[]{(.003f,.0074f,.009f,.006f),(-length,.0064f,.0068f,.006f)},12, .002f),skin);
                Replace(arm.FingerTips[i].Find("Distal"),Loft("M17_FingerTip_"+side+"_"+i,new[]{(.002f,.0065f,.007f,.006f),(-length*.70f,.0055f,.0055f,.0055f),(-length,.0018f,.002f,.002f)},12),skin);
            }
            if(side<0){
                var watch=Node(arm.Wrist,"Digital watch",new Vector3(0,.025f,0));
                Surface(watch,"M17_WatchStrap",Loft("M17_WatchStrap",new[]{(-.009f,.034f,.034f,.030f),(.011f,.034f,.034f,.030f)},28),ink);
                P(watch,"Watch case",new Vector3(0,0,.035f),new Vector3(.042f,.034f,.013f),ink,PrimitiveType.Cube);
                P(watch,"Watch display",new Vector3(0,.001f,.043f),new Vector3(.030f,.021f,.002f),steel,PrimitiveType.Cube);
                // A neutral unlit LCD pattern, not a falsely functional clock.
                foreach(float x in new[]{-.009f,-.003f,.005f,.011f})P(watch,"LCD segment",new Vector3(x,0,.045f),new Vector3(.002f,.010f,.001f),ink,PrimitiveType.Cube);
            }
            arm.Pose(actor.transform.TransformPoint(new Vector3(side*.286f,actor.Height*.47f,.07f)),actor.transform.rotation);
        }
        static void MinseoHair(Transform root,float h,Material hair,Material sheen)
        {
            var v=new List<Vector3>();var tris=new List<int>();const int rings=14,around=40;
            for(int i=0;i<rings;i++)for(int j=0;j<around;j++){
                float a=j*Mathf.PI*2/around,front=Mathf.Max(0,Mathf.Cos(a));float end=Mathf.Lerp(2.36f,1.20f,Mathf.Pow(front,2));float p=.025f+(end-.025f)*i/(rings-1f);
                float r=p>Mathf.PI*.5f?Mathf.Max(.86f,Mathf.Sin(p)):Mathf.Sin(p);
                v.Add(new Vector3(Mathf.Sin(a)*r*.121f,h*.924f+Mathf.Cos(p)*h*.072f,Mathf.Cos(a)*r*.120f-.013f));
            }
            for(int i=0;i<rings-1;i++)for(int j=0;j<around;j++){int a=i*around+j,b=i*around+(j+1)%around,c=a+around,d=b+around;tris.AddRange(new[]{a,c,b,b,c,d});}
            Surface(root,"M17_HairCap",MeshAsset("M17_HairCap",v,tris),hair);
            for(int i=0;i<7;i++){
                float x=-.097f+i*.030f,end=h*(.932f+(i%3)*.009f);
                Lock(root,"M17_Fringe_"+i,hair,new Vector3(x*.4f+.019f,h*.989f,.030f),new Vector3(x+.040f,h*.983f,.118f),new Vector3(x-.015f,h*.953f,.142f),new Vector3(x-.020f,end,.110f),.048f,.010f);
                Lock(root,"M17_HairStroke_"+i,sheen,new Vector3(x*.4f+.022f,h*.990f,.033f),new Vector3(x+.036f,h*.981f,.125f),new Vector3(x-.011f,h*.959f,.151f),new Vector3(x-.021f,end+.018f,.134f),.007f,.001f);
            }
            foreach(int side in new[]{-1,1})for(int i=0;i<3;i++)Lock(root,"M17_SideLock_"+side+"_"+i,hair,new Vector3(side*.066f,h*.977f,-.02f-i*.025f),new Vector3(side*.128f,h*.969f,.005f-i*.036f),new Vector3(side*.135f,h*.935f,.013f-i*.039f),new Vector3(side*.111f,h*(.898f-i*.007f),.019f-i*.041f),.054f,.008f);
        }
    }
}
