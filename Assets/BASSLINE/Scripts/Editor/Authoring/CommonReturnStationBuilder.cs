using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TMPro;
using BASSLINE.AuthoringData;
using BASSLINE.Bootstrap;
namespace BASSLINE.Authoring
{
    public static class CommonReturnStationBuilder
    {
        public static CommonReturnStation Build(MansionLayout layout)
        {
            var room=layout.Room("R_WORK");var root=new GameObject("Common return furniture");root.transform.SetParent(room.transform,false);
            var s=root.AddComponent<CommonReturnStation>();
            var props=room.GetComponentsInChildren<MansionProp>();
            var trayTable=props.Single(p=>p.ObjectId=="OBJ_R_WORK_Workbench0");var drawerTable=props.Single(p=>p.ObjectId=="OBJ_R_WORK_Workbench3");
            var wood=trayTable.GetComponentInChildren<Renderer>().sharedMaterial;
            var paper=new Material(Shader.Find("Universal Render Pipeline/Lit"));paper.SetColor("_BaseColor",new Color(.77f,.73f,.62f));
            const string path="Assets/BASSLINE/Characters/Materials/ReturnStationPaper.mat";var old=AssetDatabase.LoadAssetAtPath<Material>(path);if(old){UnityEngine.Object.DestroyImmediate(paper);paper=old;}else AssetDatabase.CreateAsset(paper,path);
            Vector3 tray=trayTable.transform.position+new Vector3(.55f,.973f,-.57f);
            s.TrayPoint=Target(root.transform,CommonReturnStation.TrayId,"공용 반납대",tray,new Vector3(.60f,.035f,.24f),wood);
            s.TrayNode=Closest(layout,tray-Vector3.up*.973f,room.RoomId);
            Vector3 drawer=drawerTable.transform.position+new Vector3(.45f,1.04f,-.86f);
            s.DrawerPoint=Target(root.transform,CommonReturnStation.DrawerId,"공용 정리함",drawer,new Vector3(.64f,.12f,.28f),wood);
            s.DrawerVisual=s.DrawerPoint.Find("Body");s.OpenDrawerPosition=s.DrawerVisual.localPosition;s.ClosedDrawerPosition=s.OpenDrawerPosition+Vector3.forward*.31f;
            s.DrawerVisual.Find("Surface").localScale=new Vector3(.64f,.018f,.28f);
            foreach(int side in new[]{-1,1})Shape(s.DrawerVisual,"Side",new Vector3(side*.305f,.06f,0),new Vector3(.03f,.12f,.28f),wood);
            Shape(s.DrawerVisual,"Back",new Vector3(0,.06f,.125f),new Vector3(.64f,.12f,.03f),wood);
            s.DrawerNode=Closest(layout,drawer-Vector3.up*1.04f,room.RoomId);
            var book=Target(root.transform,CommonReturnStation.BookId,"공용 정리 기록",drawerTable.transform.position+new Vector3(-.40f,.969f,-.44f),new Vector3(.38f,.025f,.30f),paper);
            Label(s.TrayPoint,"공용 반납대\n개인 물건은 주인에게",new Vector3(0,.16f,.24f));
            Label(s.DrawerPoint,"공용 정리함\n누구나 열람할 수 있어요",new Vector3(0,.18f,.46f));
            Label(book,"공용 정리 기록",new Vector3(0,.12f,.18f));
            var personalTable=props.Single(p=>p.ObjectId=="OBJ_R_WORK_Workbench1");
            Vector3 personal=personalTable.transform.position+new Vector3(.55f,.973f,-.57f);
            s.BagFrontPoint=Target(root.transform,CommonReturnStation.BagFrontId,"태겸의 가방 앞",personal,new Vector3(.30f,.018f,.23f),wood);
            var bagMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));bagMaterial.SetColor("_BaseColor",new Color(.22f,.19f,.17f));
            const string bagPath="Assets/BASSLINE/Characters/Materials/TaegyeomBag.mat";var priorBag=AssetDatabase.LoadAssetAtPath<Material>(bagPath);if(priorBag){UnityEngine.Object.DestroyImmediate(bagMaterial);bagMaterial=priorBag;}else AssetDatabase.CreateAsset(bagMaterial,bagPath);
            Shape(s.BagFrontPoint,"Personal bag",new Vector3(0,.16f,.30f),new Vector3(.48f,.30f,.18f),bagMaterial);
            s.BagFrontPoint.Find("Personal bag").GetComponent<Collider>().isTrigger=false;
            foreach(int side in new[]{-1,1})Shape(s.BagFrontPoint,"Handle post",new Vector3(side*.10f,.355f,.30f),new Vector3(.027f,.11f,.025f),bagMaterial);
            Shape(s.BagFrontPoint,"Bag handle",new Vector3(0,.405f,.30f),new Vector3(.22f,.025f,.025f),bagMaterial);
            s.PrivateNote=Target(root.transform,CommonReturnStation.PrivateNoteId,"태겸의 메모",personal+new Vector3(-.35f,.01f,.08f),new Vector3(.19f,.008f,.14f),paper);
            Label(s.BagFrontPoint,"권태겸",new Vector3(0,.14f,.198f),.10f,.24f,.30f,true);
            Label(s.PrivateNote,CommonReturnStation.PrivateNoteText,new Vector3(-.08f,.15f,.04f),.23f,.40f,.28f,true);
            return s;
        }
        static string Closest(MansionLayout l,Vector3 point,string room)=>l.NavigationNodes.Where(n=>n.RoomId==room).OrderBy(n=>Vector3.Distance(n.Position,point)).First().Id;
        static Transform Target(Transform parent,string id,string name,Vector3 p,Vector3 size,Material material)
        {
            var root=new GameObject(name);root.transform.SetParent(parent,false);root.transform.position=p;var t=root.AddComponent<FixtureTarget>();t.StableId=id;t.PublicName=name;
            var visual=new GameObject("Body").transform;visual.SetParent(root.transform,false);Shape(visual,"Surface",Vector3.zero,size,material);return root.transform;
        }
        static void Shape(Transform parent,string name,Vector3 point,Vector3 size,Material material)
        {
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name=name;cube.transform.SetParent(parent,false);cube.transform.localPosition=point;cube.transform.localScale=size;cube.GetComponent<Renderer>().sharedMaterial=material;
            // The furniture's existing collision remains solid; these small props are query targets.
            cube.GetComponent<Collider>().isTrigger=true;
        }
        static void Label(Transform parent,string text,Vector3 pos,float height=.23f,float width=.82f,float fontSize=.48f,bool light=false)
        {
            var go=new GameObject("Label");go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localRotation=Quaternion.identity;
            const string boardPath="Assets/BASSLINE/Characters/Materials/ReturnStationSign.mat";
            var boardMaterial=AssetDatabase.LoadAssetAtPath<Material>(boardPath);
            if(!boardMaterial){boardMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));boardMaterial.SetColor("_BaseColor",new Color(.055f,.075f,.083f));AssetDatabase.CreateAsset(boardMaterial,boardPath);}
            if(light)boardMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/BASSLINE/Characters/Materials/ReturnStationPaper.mat");
            var board=GameObject.CreatePrimitive(PrimitiveType.Cube);board.name="Sign backing";board.transform.SetParent(go.transform,false);board.transform.localPosition=new Vector3(0,0,.014f);board.transform.localScale=new Vector3(width,height,.018f);board.GetComponent<Renderer>().sharedMaterial=boardMaterial;UnityEngine.Object.DestroyImmediate(board.GetComponent<Collider>());
            var label=go.AddComponent<TextMeshPro>();label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BASSLINE/UI/Fonts/FONT_Korean_System.asset");label.text=text;label.fontSize=fontSize;label.enableAutoSizing=false;label.textWrappingMode=TextWrappingModes.NoWrap;label.color=light?new Color(.10f,.085f,.07f):new Color(.94f,.91f,.83f);label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(width-.04f,height-.03f);
        }
    }
}
