using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BASSLINE.AuthoringData;
namespace BASSLINE.Authoring
{
    public static partial class CharacterPresentationBuilder
    {
        // BL22 P02: 163 cm, white curls, very pale eyes, burgundy cardigan.
        // Face and clothing are individual meshes. Existing custody, actor capsule and hand rigs remain authoritative.
        static void BuildJinwoo(FixtureActorBody actor)
        {
            float h=actor.Height;var root=Node(actor.transform,"CharacterProxy",Vector3.zero);
            var knit=S("Jinwoo_Cardigan","683641");var rib=S("Jinwoo_Rib","4D2B35");var fold=S("Jinwoo_Fold","542E38");
            var skin=S("Jinwoo_Skin","E2CBBF");var lip=S("Jinwoo_Lip","966C69");
            var hair=S("Jinwoo_WhiteHair","E8E3DD");var hairShade=S("Jinwoo_HairShade","B0A9AA");var highlight=S("Jinwoo_HairLight","F2EEE5");
            var shirt=S("Jinwoo_Shirt","E0D9CA");var seam=S("Jinwoo_Seam","AEA39A");
            var trousers=S("Jinwoo_Trousers","38353D");var ink=S("Jinwoo_Ink","29252E");var iris=S("Jinwoo_PaleIris","ADAEB3");
            var canvas=S("Jinwoo_Canvas","37373D");var rubber=S("Jinwoo_Rubber","BEB6A5");
            Surface(root,"J19_ShirtBody",Loft("J19_ShirtBody",new[]{(h*.484f,.162f,.108f,.095f),(h*.55f,.159f,.114f,.094f),(h*.64f,.155f,.117f,.088f),(h*.70f,.173f,.112f,.087f),(h*.763f,.207f,.069f,.071f),(h*.799f,.053f,.035f,.048f)},40,0,.48f),shirt);
            Surface(root,"J19_Cardigan",JinwooCardigan(h),knit);
            Surface(root,"J19_Neck",Loft("J19_Neck",new[]{(h*.789f,.055f,.041f,.049f),(h*.835f,.044f,.040f,.049f),(h*.865f,.043f,.044f,.055f)},28),skin);
            Surface(root,"J20_ShirtCollarBand",Loft("J20_ShirtCollarBand",new[]{(h*.794f,.057f,.046f,.054f),(h*.814f,.053f,.044f,.055f)},32),shirt);
            Surface(root,"J19_Face",Loft("J19_Face",new[]{(h*.854f,.018f,.067f,.031f),(h*.863f,.042f,.083f,.055f),(h*.880f,.075f,.095f,.079f),(h*.905f,.099f,.105f,.095f),(h*.934f,.104f,.106f,.102f),(h*.958f,.093f,.087f,.094f),(h*.978f,.065f,.060f,.073f),(h*.985f,.008f,.009f,.014f)},44,0,.5f),skin);
            foreach(int side in new[]{-1,1}){
                Vector3 V(float x,float y,float z)=>new Vector3(side*x,h*y,z);
                Surface(root,"J19_Collar_"+side,Panel("J19_Collar_"+side,V(.047f,.813f,.039f),V(.091f,.771f,.101f),V(.047f,.748f,.124f),V(.019f,.790f,.077f)),shirt);
                Line(root,"J19_CollarSeam_"+side,seam,.0008f,V(.048f,.812f,.043f),V(.089f,.772f,.105f),V(.047f,.751f,.128f));
                Line(root,"J19_KnitEdge_"+side,rib,.010f,V(.049f,.802f,.045f),V(.086f,.769f,.099f),V(.068f,.735f,.128f),V(.034f,.689f,.141f),V(.006f,.640f,.137f),V(.004f,.552f,.135f),V(.023f,.476f,.122f));
                Line(root,"J19_InnerEdge_"+side,fold,.0018f,V(.042f,.800f,.052f),V(.077f,.768f,.112f),V(.060f,.733f,.140f),V(.027f,.687f,.147f),V(.008f,.638f,.148f),V(.010f,.552f,.146f),V(.029f,.479f,.132f));
                for(int k=0;k<4;k++)Line(root,"J19_WaistFold_"+side+"_"+k,fold,.0012f,V(.054f,.514f+k*.022f,.136f),V(.109f,.501f+k*.024f,.125f),V(.153f,.512f+k*.025f,.092f));
                Line(root,"J19_ShoulderSeam_"+side,fold,.0012f,V(.087f,.790f,.060f),V(.166f,.778f,.078f),V(.210f,.763f,.065f));
                P(root,"J19 Ear",V(.108f,.908f,.001f),new Vector3(.032f,.054f,.033f),skin);
                P(root,"J19 Ear fold",V(.118f,.908f,.017f),new Vector3(.012f,.031f,.004f),lip);
                JinwooEye(root,side,h,shirt,iris,ink,hairShade,lip);
                BuildArm(actor,root,side,knit,skin,shirt);JinwooSleeve(actor,side,knit,rib,skin,shirt);
                var leg=Node(root,side<0?"LegLeft":"LegRight",V(.082f,.537f,0));
                Surface(leg,"J19_Trouser_"+side,Loft("J19_Trouser_"+side,new[]{(0f,.084f,.091f,.085f),(-h*.09f,.081f,.087f,.079f),(-h*.22f,.060f,.066f,.064f),(-h*.27f,.066f,.068f,.066f),(-h*.34f,.054f,.063f,.061f),(-h*.46f,.057f,.068f,.062f),(-h*.478f,.060f,.072f,.064f)},28),trousers);
                Line(leg,"J19_PantsCrease_"+side,ink,.0008f,new Vector3(.008f,-h*.07f,.096f),new Vector3(-.006f,-h*.20f,.070f),new Vector3(.003f,-h*.30f,.075f),new Vector3(.008f,-h*.45f,.074f));
                foreach(float y in new[]{.241f,.276f,.438f})Line(leg,"J19_PantsFold_"+side+"_"+y,ink,.0010f,new Vector3(-.037f,-h*y,.059f),new Vector3(.011f,-h*y+.007f,.077f),new Vector3(.046f,-h*y-.011f,.055f));
                var shoe=Node(leg,"Canvas shoe",Vector3.down*h*.537f);
                Surface(shoe,"J19_ShoeSole_"+side,Loft("J19_ShoeSole_"+side,new[]{(.005f,.067f,.141f,.081f),(.024f,.070f,.144f,.084f),(.032f,.068f,.141f,.081f)},32,.035f),rubber);
                Surface(shoe,"J19_Canvas_"+side,Loft("J19_Canvas_"+side,new[]{(.032f,.065f,.135f,.078f),(.060f,.063f,.123f,.075f),(.082f,.048f,.092f,.061f),(.111f,.043f,.054f,.051f)},32,.035f),canvas);
                Surface(shoe,"J19_ToeCap_"+side,JinwooToeCap(side),rubber);
                Line(shoe,"J19_SoleStripe_"+side,ink,.0014f,new Vector3(-.061f,.020f,.099f),new Vector3(-.048f,.020f,.160f),new Vector3(0,.020f,.180f),new Vector3(.048f,.020f,.160f),new Vector3(.061f,.020f,.099f));
                for(int i=0;i<4;i++)Line(shoe,"J19_Lace_"+side+"_"+i,shirt,.0019f,new Vector3(-.025f,.102f-i*.009f,.080f+i*.013f),new Vector3(.024f,.099f-i*.009f,.085f+i*.013f));
            }
            for(int k=0;k<4;k++){float y=.620f-k*.041f;P(root,"J19 Cardigan button",new Vector3(-.005f,h*y,.147f),new Vector3(.012f,.012f,.005f),ink);P(root,"J19 Button center",new Vector3(-.005f,h*y,.150f),new Vector3(.004f,.004f,.001f),seam);}
            // Ribbed hem lies on the actual cardigan surface, not a flat floating band.
            for(int i=0;i<32;i++){
                float a=.15f+i*(Mathf.PI*2-.30f)/31,cs=Mathf.Cos(a);float depth=cs>=0?.123f:-.108f;
                float x=Mathf.Sin(a)*.177f,z=depth*Mathf.Pow(Mathf.Abs(cs),.48f);
                Line(root,"J19_HemRib_"+i,rib,.0011f,new Vector3(x,h*.477f,z),new Vector3(x*.98f,h*.505f,z+.001f));
            }
            Line(root,"J19_ShirtOpening",seam,.0012f,new Vector3(-.008f,h*.787f,.084f),new Vector3(.004f,h*.748f,.133f),new Vector3(.013f,h*.696f,.130f));
            Surface(root,"J19_Nose",Loft("J19_Nose",new[]{(h*.895f,.005f,.104f,0f),(h*.899f,.009f,.119f,0f),(h*.905f,.005f,.112f,0f),(h*.922f,.003f,.106f,0f)},20),skin);
            Line(root,"J19_Mouth",lip,.00125f,new Vector3(-.023f,h*.879f,.099f),new Vector3(-.011f,h*.8755f,.105f),new Vector3(.006f,h*.876f,.107f),new Vector3(.023f,h*.880f,.100f));
            Line(root,"J19_LowerLip",skin,.0010f,new Vector3(-.009f,h*.873f,.107f),new Vector3(.005f,h*.872f,.109f),new Vector3(.015f,h*.874f,.106f));
            var curls=Node(root,"White curls",Vector3.down*h*.00655f);
            JinwooCurls(curls,h,hair,hairShade,highlight);
            ConformJinwooFeatures(root,root.Find("J19_Face").GetComponent<MeshFilter>().sharedMesh);
            CombineStillParts(root,"CH_02");
        }
        static Mesh JinwooCardigan(float h)
        {
            var rows=new[]{(.475f,.180f,.120f,.106f,.024f),(.508f,.177f,.128f,.105f,.012f),(.55f,.168f,.130f,.101f,.004f),(.64f,.164f,.132f,.096f,.006f),(.69f,.179f,.129f,.095f,.034f),(.735f,.207f,.114f,.087f,.068f),(.769f,.215f,.077f,.076f,.086f),(.801f,.058f,.041f,.053f,.036f)};
            var v=new List<Vector3>();var t=new List<int>();const int count=48;
            foreach(var row in rows){float opening=Mathf.Asin(row.Item5/row.Item2);for(int j=0;j<=count;j++){
                float a=j*Mathf.PI*2/count;
                // The neckline changes only the front opening. Keep side/back ring
                // samples aligned so widening the V does not twist side triangles.
                a+=opening*Mathf.Clamp01(1-a/(Mathf.PI/3))-opening*Mathf.Clamp01(1-(Mathf.PI*2-a)/(Mathf.PI/3));
                float c=Mathf.Cos(a);v.Add(new Vector3(row.Item2*Mathf.Sin(a),row.Item1*h,c>=0?row.Item3*Mathf.Pow(c,.48f):-row.Item4*Mathf.Pow(-c,.48f)));
            }}
            for(int r=0;r<rows.Length-1;r++)for(int j=0;j<count;j++){int a=r*(count+1)+j,b=a+1,c=a+count+1,d=c+1;t.AddRange(new[]{a,b,c,b,d,c});}
            return MeshAsset("J19_Cardigan",v,t);
        }
        static Mesh JinwooToeCap(int side)
        {
            var v=new List<Vector3>();var t=new List<int>();const int columns=20;
            var rows=new[]{(.032f,.067f,.138f),(.051f,.066f,.130f),(.065f,.061f,.119f)};
            foreach(var row in rows)for(int j=0;j<=columns;j++){
                float a=Mathf.Lerp(-1.02f,1.02f,j/(float)columns);
                v.Add(new Vector3(Mathf.Sin(a)*row.Item2,row.Item1,.035f+Mathf.Cos(a)*row.Item3));
            }
            for(int r=0;r<rows.Length-1;r++)for(int j=0;j<columns;j++){int a=r*(columns+1)+j,b=a+1,c=a+columns+1;t.AddRange(new[]{a,b,c,b,c+1,c});}
            return MeshAsset("J19_ToeCap_"+side,v,t);
        }
        static void JinwooEye(Transform root,int side,float h,Material white,Material iris,Material ink,Material brow,Material lip)
        {
            float eye=h*.922f,cx=side*.048f;float Z(float x)=>.106f*Mathf.Pow(Mathf.Clamp01(1-x*x/(.103f*.103f)),.25f);
            var contour=Enumerable.Range(0,10).Select(i=>{float t=i/9f,x=cx+side*Mathf.Lerp(-.033f,.032f,t);return new Vector3(x,eye+.009f*Mathf.Sin(t*Mathf.PI)+.0035f*t,Z(x)+.002f);}).Concat(Enumerable.Range(1,8).Select(i=>{float t=1-i/9f,x=cx+side*Mathf.Lerp(-.033f,.032f,t);return new Vector3(x,eye-.008f*Mathf.Sin(t*Mathf.PI)+.0035f*t,Z(x)+.002f);})).ToArray();
            Surface(root,"J19_White_"+side,Panel("J19_White_"+side,contour),white);
            Oval(root,"J19_Iris_"+side,iris,new Vector3(cx,eye+.001f,Z(cx)+.005f),.023f,.017f);
            Oval(root,"J19_Pupil_"+side,ink,new Vector3(cx,eye+.001f,Z(cx)+.006f),.007f,.013f);
            Oval(root,"J19_Glint_"+side,white,new Vector3(cx-.004f,eye+.005f,Z(cx)+.007f),.0035f,.0035f);
            Line(root,"J19_UpperLid_"+side,ink,.00155f,contour.Take(10).Select(p=>p+Vector3.forward*.002f).ToArray());
            Line(root,"J19_LowerLid_"+side,lip,.00075f,contour.Skip(9).Concat(new[]{contour[0]}).Select(p=>p+Vector3.forward*.002f).ToArray());
            Line(root,"J19_Brow_"+side,brow,.0022f,new Vector3(cx-side*.028f,eye+.024f,Z(cx-side*.028f)+.003f),new Vector3(cx,eye+.028f,Z(cx)+.003f),new Vector3(cx+side*.029f,eye+.024f,Z(cx+side*.029f)+.003f));
        }
        static void JinwooSleeve(FixtureActorBody actor,int side,Material knit,Material rib,Material skin,Material shirt)
        {
            var arm=actor.GetComponentsInChildren<ActorArmRig>().Single(r=>r.Side==side);arm.Upper.localPosition=new Vector3(side*.214f,actor.Height*.767f,0);
            void Replace(Transform part,Mesh mesh,Material mat){part.localPosition=Vector3.zero;part.localRotation=Quaternion.identity;part.localScale=Vector3.one;part.GetComponent<MeshFilter>().sharedMesh=mesh;part.GetComponent<Renderer>().sharedMaterial=mat;}
            float lower=arm.ForearmLength;JinwooDeformingSleeve(arm,knit);
            Replace(arm.Forearm.Find("Cuff"),Loft("J19_KnitCuff_"+side,new[]{(-lower+.038f,.042f,.045f,.042f),(-lower+.004f,.037f,.039f,.036f)},28),rib);
            Surface(arm.Forearm,"J19_ShirtCuff_"+side,Loft("J19_ShirtCuff_"+side,new[]{(-lower+.008f,.035f,.037f,.034f),(-lower-.003f,.034f,.036f,.033f)},28),shirt);
            for(int k=0;k<18;k++){float a=k*Mathf.PI*2/18;Line(arm.Forearm,"J19_CuffRib_"+side+"_"+k,knit,.0010f,new Vector3(Mathf.Sin(a)*.041f,-lower+.031f,Mathf.Cos(a)*.044f),new Vector3(Mathf.Sin(a)*.038f,-lower+.009f,Mathf.Cos(a)*.040f));}
            Replace(arm.Wrist.Find("Palm"),Loft("J19_Palm_"+side,new[]{(.007f,.026f,.018f,.014f),(-.022f,.033f,.021f,.016f),(-.049f,.029f,.016f,.013f),(-.055f,.024f,.011f,.009f)},24),skin);
            for(int i=0;i<4;i++){float length=i==3?.019f:.023f;Replace(arm.FingerRoots[i].Find("Proximal"),Loft("J19_FingerBase_"+side+"_"+i,new[]{(.003f,.0074f,.008f,.006f),(-length,.0064f,.0065f,.006f)},12,.002f),skin);Replace(arm.FingerTips[i].Find("Distal"),Loft("J19_FingerTip_"+side+"_"+i,new[]{(.002f,.0065f,.007f,.006f),(-length*.75f,.0055f,.0055f,.0055f),(-length,.0018f,.002f,.002f)},12),skin);}
            arm.Pose(actor.transform.TransformPoint(new Vector3(side*.255f,actor.Height*.47f,.07f)),actor.transform.rotation);
        }
        static void JinwooCurls(Transform root,float h,Material hair,Material shade,Material light)
        {
            var v=new List<Vector3>();var tris=new List<int>();const int around=44,rings=14;
            for(int i=0;i<rings;i++)for(int j=0;j<around;j++){
                float a=j*Mathf.PI*2/around,front=Mathf.Max(0,Mathf.Cos(a)),end=Mathf.Lerp(2.30f,1.17f,front*front),p=.025f+(end-.025f)*i/(rings-1f);
                float r=p>Mathf.PI*.5f?Mathf.Max(.84f,Mathf.Sin(p)):Mathf.Sin(p);
                v.Add(new Vector3(Mathf.Sin(a)*r*.119f,h*.944f+Mathf.Cos(p)*h*.050f,Mathf.Cos(a)*r*.118f-.015f));
            }
            for(int i=0;i<rings-1;i++)for(int j=0;j<around;j++){int a=i*around+j,b=i*around+(j+1)%around,c=a+around,d=b+around;tris.AddRange(new[]{a,c,b,b,c,d});}
            Surface(root,"J19_HairCap",MeshAsset("J19_HairCap",v,tris),shade);
            for(int i=0;i<9;i++){
                float x=-.110f+i*.0275f,phase=i%3,end=.934f+phase*.004f;
                float sweep=i<4?-.028f:.032f;
                var a=new Vector3(x*.48f+.008f,h*(.987f+(i%2)*.003f),.029f);
                var b=new Vector3(x+sweep,h*(.994f-phase*.002f),.129f);
                var c=new Vector3(x-sweep*1.15f,h*(.948f+phase*.006f),.158f);
                var d=new Vector3(x+sweep*.45f,h*end,.120f);
                Lock(root,"J19_FrontCurl_"+i,hair,a,b,c,d,.048f,.012f);
                Lock(root,"J19_CurlLight_"+i,light,a+new Vector3(.002f,.002f,.003f),b+Vector3.forward*.013f,c+Vector3.forward*.013f,d+new Vector3(0,.011f,.006f),.005f,.0012f);
            }
            foreach(int side in new[]{-1,1})for(int i=0;i<6;i++){
                float z=.060f-i*.037f;
                Lock(root,"J19_SideCurl_"+side+"_"+i,hair,new Vector3(side*.064f,h*.980f,z*.6f),new Vector3(side*.155f,h*.980f,z+.030f),new Vector3(side*.104f,h*.935f,z-.023f),new Vector3(side*(.128f-(i%2)*.009f),h*(.914f+i*.003f),z),.047f,.011f);
                Lock(root,"J19_SideLight_"+side+"_"+i,light,new Vector3(side*.068f,h*.982f,z*.6f),new Vector3(side*.163f,h*.979f,z+.031f),new Vector3(side*.111f,h*.947f,z-.021f),new Vector3(side*.139f,h*.934f,z+.006f),.007f,.0015f);
            }
            for(int i=0;i<7;i++){
                float x=-.088f+i*.029f;
                Lock(root,"J19_BackCurl_"+i,hair,new Vector3(x*.4f,h*.990f,-.015f),new Vector3(x+.024f,h*.982f,-.142f),new Vector3(x-.032f,h*.939f,-.136f),new Vector3(x+.009f,h*.910f,-.104f),.048f,.009f);
            }
            Lock(root,"J19_CrownCurl",hair,new Vector3(-.035f,h*.980f,-.009f),new Vector3(-.063f,h*1.008f,.008f),new Vector3(.033f,h*1.002f,.030f),new Vector3(.048f,h*.974f,.060f),.035f,.007f);
        }
    }
}

