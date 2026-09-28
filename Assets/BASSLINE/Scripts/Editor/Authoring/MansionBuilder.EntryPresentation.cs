using UnityEngine;

namespace BASSLINE.Authoring
{
    public static partial class MansionBuilder
    {
        // Overhead and wall-mounted dressing only. PhysicalHash must remain unchanged.
        static void DressEntryHall()
        {
            var hall=layout.Room("R_HALL");
            var decor=Group(hall.transform,"EntryHall_Dressing");
            var brass=Mat("Entry_AgedBrass","9C8053",.42f,.65f);
            var iron=Mat("Entry_BlackIron","20202A",.3f,.45f);
            var wine=Mat("Entry_WovenWine","3D2335",.05f);
            var ivory=Mat("Entry_CandleWax","E8D2A8",.1f);
            var glow=Mat("Entry_CandleLight","FFD99B",.15f,0,2);
            // The original nine point lights remain the room's illumination sources.
            // Their fluorescent bar meshes are replaced by a single hanging fixture.
            foreach(var light in hall.Lights){
                var face=light.transform.Find("Luminaire");
                if(face&&face.TryGetComponent<Renderer>(out var renderer))renderer.enabled=false;
                light.color=new Color(1,.86f,.70f);
            }
            var center=new Vector3(0,5.5f,-3);
            EntryRing(decor,center,1.65f,brass);
            EntryRing(decor,center+Vector3.up*.65f,.75f,brass);
            Cylinder(decor,"CeilingRose",new Vector3(0,10.8f,-3),.38f,.1f,brass,false);
            for(int i=0;i<8;i++){
                float a=i*Mathf.PI/4;
                var edge=center+new Vector3(Mathf.Cos(a)*1.65f,0,Mathf.Sin(a)*1.65f);
                EntryBeam(decor,"RadialArm",center,edge, .045f,iron);
                EntryBeam(decor,"Suspension",edge,new Vector3(0,10.75f,-3),.024f,brass);
                Cylinder(decor,"CandleCup",edge+Vector3.up*.08f,.12f,.04f,brass,false);
                Cylinder(decor,"Candle",edge+Vector3.up*.23f,.045f,.27f,ivory,false);
                var flame=GameObject.CreatePrimitive(PrimitiveType.Sphere);flame.name="WarmFlame";
                flame.transform.SetParent(decor,false);flame.transform.position=edge+Vector3.up*.4f;flame.transform.localScale=new Vector3(.07f,.13f,.07f);
                Object.DestroyImmediate(flame.GetComponent<Collider>());flame.GetComponent<Renderer>().sharedMaterial=glow;flame.isStatic=true;
            }
            // Tall banners frame the north side without obscuring any ground-floor door.
            foreach(float x in new[]{-9.5f,-5.2f,-.9f}){
                var at=new Vector3(x,6.1f,10.83f);
                Box(decor,"WineBanner",at,new Vector3(2.6f,4.4f,.045f),wine,false);
                EntryBeam(decor,"BannerTop",at+new Vector3(-1.45f,2.2f,-.035f),at+new Vector3(1.45f,2.2f,-.035f),.06f,brass);
                foreach(float side in new[]{-1.22f,1.22f})Box(decor,"WovenEdge",at+new Vector3(side,0,-.028f),new Vector3(.028f,4.2f,.012f),brass,false);
                var emblem=at+new Vector3(0,.5f,-.06f);
                var top=emblem+Vector3.up*.65f;var bottom=emblem-Vector3.up*.65f;
                foreach(float side in new[]{-.38f,.38f}){
                    EntryBeam(decor,"DiamondCrest",top,emblem+Vector3.right*side,.035f,brass);
                    EntryBeam(decor,"DiamondCrest",bottom,emblem+Vector3.right*side,.035f,brass);
                }
                EntryBeam(decor,"CrestLine",emblem-Vector3.up*1.35f,emblem+Vector3.up*1.35f,.018f,brass);
            }
        }
        static void EntryRing(Transform parent,Vector3 center,float radius,Material material)
        {
            const int segments=24;
            for(int i=0;i<segments;i++){
                float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                EntryBeam(parent,"ChandelierRing",center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),center+new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius),.07f,material);
            }
        }
        static void EntryBeam(Transform parent,string name,Vector3 start,Vector3 end,float width,Material material)
        {
            var delta=end-start;var beam=Box(parent,name,(start+end)*.5f,new Vector3(width,width,delta.magnitude),material,false);
            beam.transform.rotation=Quaternion.LookRotation(delta);
        }
    }
}
