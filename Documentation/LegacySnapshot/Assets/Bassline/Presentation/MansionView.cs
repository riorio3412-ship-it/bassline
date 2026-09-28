using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bassline.Unity
{
    public sealed class Interaction : MonoBehaviour
    {
        public string kind, label, evidenceId;
        public int id;
    }
    public sealed class MansionView : MonoBehaviour
    {
        public Camera eye;
        public CharacterController controller;
        public Transform player;
        public Interaction focused;
        GameObject architecture,occupants;
        Font font;
        float pitch,yaw;
        readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        public int room=-1;
        public void Initialize()
        {
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},36);
            var go=new GameObject("Minhyuk • First person");player=go.transform;controller=go.AddComponent<CharacterController>();
            controller.height=1.8f;controller.radius=.28f;controller.center=new Vector3(0,.9f,0);
            var cam=new GameObject("Eyes");cam.transform.SetParent(player);cam.transform.localPosition=new Vector3(0,1.66f,0);
            eye=cam.AddComponent<Camera>();eye.fieldOfView=72;eye.nearClipPlane=.06f;eye.farClipPlane=100;
            eye.clearFlags=CameraClearFlags.SolidColor;eye.backgroundColor=new Color(.02f,.035f,.045f);
            eye.gameObject.AddComponent<AudioListener>();
            RenderSettings.ambientLight=new Color(.36f,.39f,.44f);RenderSettings.fog=true;RenderSettings.fogColor=eye.backgroundColor;RenderSettings.fogDensity=.016f;
            var light=new GameObject("Moonlight").AddComponent<Light>();light.type=LightType.Directional;light.color=new Color(.58f,.72f,.83f);light.intensity=.8f;light.transform.rotation=Quaternion.Euler(55,-35,0);
        }
        Material Mat(string hex)
        {
            if(materials.TryGetValue(hex,out var material)) return material;
            ColorUtility.TryParseHtmlString("#"+hex,out Color color);
            material=new Material(Shader.Find("Standard"));material.color=color;material.SetFloat("_Glossiness",.3f);materials[hex]=material;return material;
        }
        GameObject Shape(PrimitiveType type,string name,Vector3 at,Vector3 scale,string color,Transform parent,bool collide=true)
        {
            var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent);g.transform.localPosition=at;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=Mat(color);
            if(!collide) Destroy(g.GetComponent<Collider>());return g;
        }
        void Sign(Transform parent,string text,Vector3 position,float size,Color color,Quaternion rotation)
        {
            var go=new GameObject("Sign "+text);go.transform.SetParent(parent);go.transform.localPosition=position;go.transform.localRotation=rotation;
            var tm=go.AddComponent<TextMesh>();tm.text=text;tm.font=font;tm.characterSize=size;tm.fontSize=40;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.color=color;go.GetComponent<MeshRenderer>().sharedMaterial=font.material;
        }
        public void RenderRoom(WorldState w,Content c,bool resetPosition=true)
        {
            if(architecture!=null) {architecture.SetActive(false);Destroy(architecture);}room=w.Person(0).room;
            architecture=new GameObject("Mansion room "+room);var root=architecture.transform;
            bool hall=room==0||room==9;float size=hall?15:11;
            Shape(PrimitiveType.Cube,"Floor",new Vector3(0,-.15f,0),new Vector3(size*2,.3f,size*2),"343C42",root);
            Shape(PrimitiveType.Cube,"Ceiling",new Vector3(0,6,0),new Vector3(size*2,.3f,size*2),"151D25",root);
            for(int x=-(int)size+1;x<size;x+=2) for(int z=-(int)size+1;z<size;z+=2)
                if((x+z)%4==0) Shape(PrimitiveType.Cube,"Inlaid stone",new Vector3(x,.006f,z),new Vector3(1.98f,.02f,1.98f),"46515A",root,false);
            Shape(PrimitiveType.Cube,"Runner",new Vector3(0,.035f,0),new Vector3(3,.035f,size*2-.4f),"612E43",root,false);
            foreach(int side in Enumerable.Range(0,4))
            {
                float angle=side*90;var rot=Quaternion.Euler(0,angle,0);var center=rot*new Vector3(0,3,size);
                var wall=Shape(PrimitiveType.Cube,"Wall",center,new Vector3(size*2,6,.35f),"202C35",root);wall.transform.localRotation=rot;
                for(int k=-1;k<=1;k++)
                {
                    var pillar=Shape(PrimitiveType.Cylinder,"Column",rot*new Vector3(k*(size-2),2.7f,size-1),new Vector3(.65f,2.7f,.65f),"9B9485",root);
                    Shape(PrimitiveType.Cube,"Capital",pillar.transform.localPosition+Vector3.up*2.5f,new Vector3(1.1f,.3f,1.1f),"B2A88F",root,false);
                }
                var rail=Shape(PrimitiveType.Cube,"Brass cornice",rot*new Vector3(0,4.9f,size-.25f),new Vector3(size*2,.12f,.12f),"AB9066",root,false);rail.transform.localRotation=rot;
            }
            var links=w.rooms[room].links.OrderBy(x=>x).ToList();
            for(int i=0;i<links.Count;i++)
            {
                float angle=360f*i/links.Count;var rotation=Quaternion.Euler(0,angle,0);var p=rotation*new Vector3(0,1.6f,size-2);
                var door=Shape(PrimitiveType.Cube,"Passage to "+links[i],p,new Vector3(2.1f,3.2f,.3f),"132D36",root);door.transform.localRotation=rotation;
                var label=door.AddComponent<Interaction>();label.kind="door";label.id=links[i];label.label=w.rooms[links[i]].name+"으로 이동";
                var frame=Shape(PrimitiveType.Cube,"Lintel",p+Vector3.up*1.65f,new Vector3(2.3f,.16f,.45f),"B7A077",root,false);frame.transform.localRotation=rotation;
                Sign(root,w.rooms[links[i]].name,p+rotation*new Vector3(0,.55f,-.19f),.085f,new Color(.82f,.8f,.67f),rotation);
                Shape(PrimitiveType.Sphere,"Handle",p+rotation*new Vector3(.7f,-.3f,-.23f),Vector3.one*.12f,"D1B477",root,false);
            }
            Shape(PrimitiveType.Cylinder,"Central dais",new Vector3(0,.12f,3.8f),new Vector3(3.4f,.12f,3.4f),"252D34",root);
            Shape(PrimitiveType.Cube,"Reading desk",new Vector3(0,.9f,3.8f),new Vector3(2.7f,.18f,1.4f),"80664E",root);
            Shape(PrimitiveType.Cube,"Desk base",new Vector3(0,.45f,3.8f),new Vector3(1.8f,.9f,.9f),"423A36",root);
            if(room==0 || room==8)
            {
                var bust=Shape(PrimitiveType.Capsule,"The curator",new Vector3(0,1.2f,6),new Vector3(.8f,1.1f,.7f),"10171C",root);
                Shape(PrimitiveType.Sphere,"Aquarium head",new Vector3(0,2.55f,6),Vector3.one*1.1f,"598A99",root,false);
                Shape(PrimitiveType.Cube,"Fish",new Vector3(0,2.55f,5.48f),new Vector3(.3f,.12f,.06f),"E2B568",root,false);
                var interact=bust.AddComponent<Interaction>();interact.kind="curator";interact.label="관장의 안내 듣기";
                Sign(root,"B A S S L I N E",new Vector3(0,4.4f,size-.3f),.23f,new Color(.75f,.66f,.48f),Quaternion.identity);
            }
            if(room==2||room==7) for(int i=-2;i<=2;i++)
            {
                Shape(PrimitiveType.Cube,"Bookcase",new Vector3(i*2.2f,1.5f,-size+1.2f),new Vector3(1.7f,3,.5f),"473D38",root);
                for(int row=0;row<3;row++) for(int b=0;b<5;b++) Shape(PrimitiveType.Cube,"Book",new Vector3(i*2.2f-.65f+b*.32f,.5f+row*.85f,-size+1.52f),new Vector3(.22f,.64f,.24f),b%2==0?"7A5C65":"697B78",root,false);
            }
            if(room==4) for(int i=0;i<6;i++)
            {float a=i*Mathf.PI/3;var p=new Vector3(Mathf.Cos(a)*6,.5f,Mathf.Sin(a)*6);Shape(PrimitiveType.Cylinder,"Planter",p,new Vector3(.9f,.5f,.9f),"81745E",root);Shape(PrimitiveType.Sphere,"Topiary",p+Vector3.up*1.2f,new Vector3(1.6f,2,1.6f),"466B60",root,false);}
            if(room>=10) {Shape(PrimitiveType.Cube,"Bed",new Vector3(-4,.45f,0),new Vector3(2,.9f,3.6f),"726B70",root);Shape(PrimitiveType.Cube,"Pillow",new Vector3(-4,1,1.1f),new Vector3(1.6f,.2f,.8f),"C4C0AE",root,false);}
            var lampGo=new GameObject("Room lantern");lampGo.transform.SetParent(root);lampGo.transform.position=new Vector3(0,4.7f,1);
            var lamp=lampGo.AddComponent<Light>();lamp.type=LightType.Point;lamp.color=new Color(1,.8f,.57f);lamp.intensity=3;lamp.range=25;
            Shape(PrimitiveType.Sphere,"Lantern",lampGo.transform.position,Vector3.one*.6f,"D2B78A",root,false);
            if(resetPosition) {controller.enabled=false;player.position=new Vector3(0,.1f,-4);yaw=0;pitch=0;player.rotation=Quaternion.identity;eye.transform.localRotation=Quaternion.identity;controller.enabled=true;}
            RenderOccupants(w,c);
        }
        public void RenderOccupants(WorldState w,Content c)
        {
            if(occupants!=null) {occupants.SetActive(false);Destroy(occupants);}occupants=new GameObject("People and clues");
            var locals=w.Living.Where(p=>p.id!=0 && p.room==room).ToList();
            for(int i=0;i<locals.Count;i++)
            {
                var p=locals[i];var data=c.Person(p.id);float a=(i+.4f)*Mathf.PI*2/Mathf.Max(5,locals.Count);Vector3 pos=new Vector3(Mathf.Cos(a)*5.8f,0,Mathf.Sin(a)*5.8f);
                var body=Shape(PrimitiveType.Capsule,data.name,pos+Vector3.up*.98f,new Vector3(.55f,.76f,.44f),data.color,occupants.transform);
                var interact=body.AddComponent<Interaction>();interact.kind="npc";interact.id=p.id;interact.label=data.name+" · "+p.action;
                Shape(PrimitiveType.Sphere,"Face",pos+Vector3.up*1.83f,new Vector3(.39f,.47f,.4f),"C2ACA0",occupants.transform,false);
                Shape(PrimitiveType.Sphere,"Hair",pos+new Vector3(0,2.01f,.02f),new Vector3(.45f,.27f,.45f),p.id==10?"956F46":p.id==16?"BAA676":"292B34",occupants.transform,false);
                Sign(occupants.transform,data.name,pos+Vector3.up*2.35f,.065f,new Color(.9f,.9f,.8f),Quaternion.identity);
            }
            if(w.incident!=null && (w.phase==Phase.Investigation || w.phase==Phase.Trial))
            {
                var clues=w.incident.evidence.Where(e=>e.room==room && e.kind!=EvidenceKind.Testimony).ToList();
                for(int i=0;i<clues.Count;i++)
                {
                    var e=clues[i];var obj=Shape(PrimitiveType.Cube,e.title,new Vector3(-.7f+i*.7f,1.04f,3.8f),new Vector3(.52f,.06f,.65f),w.Person(0).Knows(e.id)?"627F7B":"DEC58B",occupants.transform);
                    var interact=obj.AddComponent<Interaction>();interact.kind="evidence";interact.evidenceId=e.id;interact.label=e.title+(w.Person(0).Knows(e.id)?" · 확인함":" · 조사");
                }
                if(w.incident.room==room) foreach(int id in w.incident.victims)
                {
                    var seat=Shape(PrimitiveType.Cube,"Empty place",new Vector3(-3+id%3,.15f,3),new Vector3(.9f,.3f,1.8f),"B1AD9E",occupants.transform);
                    Sign(occupants.transform,"남겨진 자리",seat.transform.position+Vector3.up*.6f,.07f,Color.white,Quaternion.identity);
                }
            }
        }
        public void Control(bool enabled)
        {
            if(!enabled) {focused=null;return;}
            yaw+=Input.GetAxis("Mouse X")*2;pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*2,-75,75);
            player.rotation=Quaternion.Euler(0,yaw,0);eye.transform.localRotation=Quaternion.Euler(pitch,0,0);
            Vector3 delta=(player.right*Input.GetAxisRaw("Horizontal")+player.forward*Input.GetAxisRaw("Vertical")).normalized*(Input.GetKey(KeyCode.LeftShift)?5.2f:3.5f);
            delta.y=-5;controller.Move(delta*Time.deltaTime);
            focused=null;if(Physics.Raycast(eye.transform.position,eye.transform.forward,out RaycastHit hit,4.2f)) focused=hit.collider.GetComponent<Interaction>();
        }
    }
}
