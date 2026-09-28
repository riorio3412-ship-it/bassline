using System;
using System.Collections.Generic;
using System.Linq;
using BASSLINE.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BASSLINE.UI
{
    // A map of receipts, never a renderer of MansionLayout or live actor transforms.
    public sealed class KnownMapView:MonoBehaviour,IBeginDragHandler,IDragHandler,IScrollHandler
    {
        sealed class Marker { public RectTransform Rect,Pin,Leader,Tile;public Vector2 Point;public string Detail;public bool Small; }
        sealed class Link { public RectTransform Rect;public Vector2 A,B; }
        ProductionScreenView screen;KnownMapSnapshot data;TMP_FontAsset font;float fontScale=1,zoom=1;
        RectTransform viewport;TMP_Text legend;Vector2 pan,center;float pixelsPerUnit=10;
        string layer="",selected="";string[] layers=Array.Empty<string>();int selectedIndex=-1;
        readonly List<Marker> markers=new List<Marker>();readonly List<Link> links=new List<Link>();
        readonly List<GameObject> generated=new List<GameObject>();
        int clockVersion;long revision=-1;string loopSignature="";Vector2 previousSize;
        KnownMapPose pose;RectTransform selfArrow;TMP_Text selfLabel;bool initialFloor=true;
        public bool IsPerspective {get;private set;}=true;
        public bool SelfVisible=>selfArrow&&selfArrow.gameObject.activeSelf;
        public int FloorConnectionCount {get;private set;}
        static readonly Color Ink=new Color(.78f,.87f,.86f),Paper=new Color(.10f,.16f,.20f),MapPaper=new Color(.055f,.078f,.10f);
        public string Information {get;private set;}="";
        public int LayerCount=>layers.Length;
        public int MarkerCount=>markers.Count;
        public string SelectedLayer=>layer;
        public void BindPose(KnownMapPose own)
        {
            pose=own;
            if(initialFloor&&pose!=null&&pose.Available&&layers.Contains(data.CurrentVersion+"/"+pose.Floor)){layer=data.CurrentVersion+"/"+pose.Floor;initialFloor=false;Rebuild();}
            PositionSelf();
        }
        public void TogglePerspective(){IsPerspective=!IsPerspective;PositionGraphics();RefreshInformation();}
        public void CenterOnSelf()
        {
            if(pose==null||!pose.Available){CenterMap();return;}
            string ownLayer=data.CurrentVersion+"/"+pose.Floor;if(layer!=ownLayer){layer=ownLayer;Rebuild();}
            pan=Vector2.zero;zoom=1;PositionGraphics();pan=-Project(Point(pose.X,pose.Z));PositionGraphics();RefreshInformation();
        }

        public static KnownMapView Attach(ProductionScreenView view)
        {
            var old=view.GetComponentInChildren<KnownMapView>(true);if(old)return old;
            var go=new GameObject("KnownMap",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.RectMask2D),typeof(KnownMapView));
            go.transform.SetParent(view.BodyScroll.transform.parent,false);var map=go.GetComponent<KnownMapView>();map.screen=view;map.viewport=(RectTransform)go.transform;
            go.GetComponent<UnityEngine.UI.Image>().color=MapPaper;map.viewport.anchorMin=new Vector2(.025f,.27f);map.viewport.anchorMax=new Vector2(.715f,.87f);map.viewport.offsetMin=map.viewport.offsetMax=Vector2.zero;
            view.BodyScroll.gameObject.SetActive(false);var context=(RectTransform)view.ContextScroll.transform;context.anchorMin=new Vector2(.74f,context.anchorMin.y);context.anchorMax=new Vector2(.975f,context.anchorMax.y);
            map.legend=map.Label("MapLegend",map.transform,"",24);map.legend.alignment=TextAlignmentOptions.TopLeft;
            map.legend.rectTransform.anchorMin=new Vector2(.02f,.82f);map.legend.rectTransform.anchorMax=new Vector2(.98f,.98f);map.legend.rectTransform.offsetMin=map.legend.rectTransform.offsetMax=Vector2.zero;
            return map;
        }
        public void Bind(KnownMapSnapshot snapshot,TMP_FontAsset koreanFont,float scale,int clock=WorldTimeLabel.Legacy)
        {
            if(clockVersion!=clock){clockVersion=clock;revision=-1;}
            if(data!=null&&data.LoopId!=snapshot.LoopId){layer="";selected="";selectedIndex=-1;pan=Vector2.zero;zoom=1;initialFloor=true;}
            data=snapshot;font=koreanFont;bool changed=Math.Abs(scale-fontScale)>.001f;fontScale=scale;
            string signature=snapshot.LoopId+"|"+string.Join("|",snapshot.Places.Select(p=>p.ReceiptId));
            var next=snapshot.Places.Select(p=>p.Version+"/"+p.Floor).Concat(snapshot.Doors.Select(d=>d.Version+"/"+d.Floor)).Distinct().OrderBy(x=>x.StartsWith(snapshot.CurrentVersion+"/",StringComparison.Ordinal)?0:1).ThenBy(x=>x,StringComparer.Ordinal).ToArray();
            if(!next.Contains(layer)){layer=next.FirstOrDefault()??"";pan=Vector2.zero;zoom=1;selected="";}
            layers=next;legend.font=font;legend.fontSize=24*Mathf.Min(fontScale,1.5f);
            if(snapshot.Revision!=revision||signature!=loopSignature||changed){revision=snapshot.Revision;loopSignature=signature;Rebuild();}
            RefreshInformation();
        }
        public void CycleFloor(int direction)
        {
            if(layers.Length==0)return;int index=Array.IndexOf(layers,layer);layer=layers[(index+direction+layers.Length)%layers.Length];pan=Vector2.zero;zoom=1;selected="";selectedIndex=-1;Rebuild();
        }
        public void Zoom(float factor){zoom=Mathf.Clamp(zoom*factor,.6f,6);PositionGraphics();RefreshInformation();}
        public void CenterMap(){pan=Vector2.zero;zoom=1;PositionGraphics();RefreshInformation();}
        public void SelectNext(int direction)
        {
            if(markers.Count==0)return;selectedIndex=(selectedIndex+direction+markers.Count)%markers.Count;selected=markers[selectedIndex].Detail;RefreshInformation();
        }
        public void OnBeginDrag(PointerEventData eventData){ }
        public void OnDrag(PointerEventData eventData)
        {
            var canvas=GetComponentInParent<Canvas>();float scale=canvas?canvas.scaleFactor:1;
            pan+=eventData.delta/Mathf.Max(.1f,scale);pan=Vector2.ClampMagnitude(pan,4000);PositionGraphics();
        }
        public void OnScroll(PointerEventData eventData){Zoom(Mathf.Pow(1.15f,eventData.scrollDelta.y));}
        void LateUpdate()
        {
            if(!screen||!viewport)return;
            var original=(RectTransform)screen.BodyScroll.transform;viewport.anchorMin=new Vector2(.025f,original.anchorMin.y);viewport.anchorMax=new Vector2(.715f,.87f);
            if((viewport.rect.size-previousSize).sqrMagnitude>.1f){previousSize=viewport.rect.size;PositionGraphics();}
            legend.transform.SetAsLastSibling();
        }
        void Rebuild()
        {
            foreach(var go in generated){go.SetActive(false);Destroy(go);}generated.Clear();markers.Clear();links.Clear();selfArrow=null;selfLabel=null;FloorConnectionCount=0;if(data==null)return;
            string version=LayerVersion, floor=LayerFloor;var places=data.Places.Where(p=>p.Version==version&&p.Floor==floor).ToArray();
            foreach(var c in data.Connections.Where(c=>c.Version==version))
            {
                var a=data.Places.FirstOrDefault(p=>p.Version==version&&p.Id==c.From);var b=data.Places.FirstOrDefault(p=>p.Version==version&&p.Id==c.To);if(a==null||b==null)continue;
                if(a.Floor!=b.Floor){
                    var here=a.Floor==floor?a:b.Floor==floor?b:null;var other=here==a?b:a;if(here==null)continue;
                    FloorConnectionCount++;AddMarker(Point(here.X,here.Z),FloorLabel(other.Floor)+" 연결","직접 통과한 층 연결\n"+here.Label+" → "+other.Label+"\n"+FloorLabel(here.Floor)+" → "+FloorLabel(other.Floor)+"\n"+TimeLabel(c.Tick),true);continue;
                }
                if(a.Floor!=floor)continue;
                var rect=Graphic("Traversed connection",Ink);links.Add(new Link{Rect=rect,A=Point(a.X,a.Z),B=Point(b.X,b.Z)});
            }
            int number=0;
            foreach(var p in places)
            {
                int n=++number;string detail=n+" · "+p.Label+"\n"+(p.Visited?"공간 내부를 직접 확인":"표지 확인 · 내부 미확인")+"\n"+TimeLabel(p.Tick);
                AddMarker(Point(p.X,p.Z),(p.Label.StartsWith("확인한 공간 ",StringComparison.Ordinal)?"공간 "+n:n+"  "+p.Label)+(p.Visited?"":" (표지)"),detail,false);
            }
            foreach(var d in data.Doors.Where(d=>d.Version==version&&d.Floor==floor))
                AddMarker(Point(d.X,d.Z),"문","문 관측 이력\n"+DoorState(d.State)+"\n관측: "+TimeLabel(d.Tick)+"\n이후 문 상태는 확인하지 않았습니다.",true);
            if(version==data.CurrentVersion)
                foreach(var p in data.People.Where(p=>places.Any(r=>r.Id==p.PlaceId)))
                    AddMarker(Point(p.X,p.Z),"●",p.Label+" · 마지막 확인\n"+PlaceName(p.PlaceId)+"\n"+TimeLabel(p.Tick)+"\n이 점은 현재 위치가 아닙니다.",true);
            var points=markers.Select(m=>m.Point).ToArray();center=points.Length==0?Vector2.zero:new Vector2((points.Min(p=>p.x)+points.Max(p=>p.x))*.5f,(points.Min(p=>p.y)+points.Max(p=>p.y))*.5f);
            selfArrow=Symbol("My position",true,new Color(.08f,.36f,.64f));selfArrow.sizeDelta=new Vector2(22,32);selfLabel=Label("My position label",selfArrow,"나",18);selfLabel.rectTransform.anchoredPosition=new Vector2(0,-30);selfLabel.rectTransform.sizeDelta=new Vector2(50,24);selfLabel.alignment=TextAlignmentOptions.Center;
            PositionGraphics();RefreshInformation();
        }
        void AddMarker(Vector2 point,string label,string detail,bool small)
        {
            var leader=Graphic("Recorded point leader",new Color(Ink.r,Ink.g,Ink.b,.65f));leader.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            var pin=Graphic("Recorded point",Ink);pin.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            var rect=Graphic("Known marker",Paper);var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=rect.GetComponent<UnityEngine.UI.Image>();
            var text=Label("Marker label",rect,label,small?20:25);text.alignment=TextAlignmentOptions.Center;Stretch(text.rectTransform);
            var marker=new Marker{Rect=rect,Pin=pin,Leader=leader,Point=point,Detail=detail,Small=small,Tile=small?null:Symbol("Observed landmark",false,new Color(.49f,.66f,.70f))};markers.Add(marker);
            button.onClick.AddListener(()=>{selected=detail;selectedIndex=markers.IndexOf(marker);RefreshInformation();});
            var outline=rect.gameObject.AddComponent<UnityEngine.UI.Outline>();outline.effectColor=Ink;outline.effectDistance=new Vector2(1,-1);
        }
        RectTransform Graphic(string name,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(transform,false);generated.Add(go);go.GetComponent<UnityEngine.UI.Image>().color=color;var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.43f);return rect;
        }
        RectTransform Symbol(string name,bool arrow,Color shade)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(KnownMapSymbol));go.transform.SetParent(transform,false);generated.Add(go);var symbol=go.GetComponent<KnownMapSymbol>();symbol.Arrow=arrow;symbol.color=shade;symbol.raycastTarget=false;var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.43f);return rect;
        }
        TMP_Text Label(string name,Transform parent,string text,float size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var label=go.GetComponent<TMP_Text>();label.font=font;label.fontSize=size*fontScale;label.text=text;label.color=Ink;label.raycastTarget=false;label.textWrappingMode=TextWrappingModes.Normal;return label;
        }
        void PositionGraphics()
        {
            if(!viewport)return;var points=markers.Select(m=>Perspective(m.Point-center)).ToArray();
            float extentX=points.Length==0?1:Mathf.Max(12,points.Max(p=>p.x)-points.Min(p=>p.x));float extentY=points.Length==0?1:Mathf.Max(12,points.Max(p=>p.y)-points.Min(p=>p.y));
            pixelsPerUnit=Mathf.Min(Mathf.Max(80,viewport.rect.width-150)/extentX,Mathf.Max(80,viewport.rect.height*.67f-100)/extentY);
            foreach(var line in links){Vector2 a=Project(line.A),b=Project(line.B);line.Rect.anchoredPosition=(a+b)*.5f;line.Rect.sizeDelta=new Vector2(Vector2.Distance(a,b),4);line.Rect.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);line.Rect.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;}
            // Declutter only presentation labels. Pins and route endpoints stay on receipt coordinates.
            var occupied=new List<Rect>();var projected=markers.Select(m=>Project(m.Point)).ToArray();
            var area=Rect.MinMaxRect(-viewport.rect.width*.5f+8,-viewport.rect.height*.43f+8,viewport.rect.width*.5f-8,viewport.rect.height*.35f-8);
            for(int i=0;i<markers.Count;i++){
                var marker=markers[i];Vector2 point=projected[i];bool portal=marker.Detail.StartsWith("직접 통과한 층 연결",StringComparison.Ordinal);Vector2 size=(marker.Small?(portal?new Vector2(125,38):new Vector2(44,38)):new Vector2(170,56))*Mathf.Min(fontScale,1.5f);
                if(marker.Tile){marker.Tile.gameObject.SetActive(IsPerspective);marker.Tile.anchoredPosition=point;marker.Tile.sizeDelta=new Vector2(68,44)*Mathf.Clamp(zoom,.8f,1.4f);marker.Tile.SetAsFirstSibling();}
                marker.Pin.anchoredPosition=point;marker.Pin.sizeDelta=Vector2.one*6;
                Vector2 label=FindLabelPosition(point,size,occupied,projected,area);marker.Rect.anchoredPosition=label;marker.Rect.sizeDelta=size;
                occupied.Add(new Rect(label-size*.5f-Vector2.one*7,size+Vector2.one*14));
                var labelBounds=new Rect(label-size*.5f,size);Vector2 edge=new Vector2(Mathf.Clamp(point.x,labelBounds.xMin,labelBounds.xMax),Mathf.Clamp(point.y,labelBounds.yMin,labelBounds.yMax));
                Vector2 delta=edge-point;marker.Leader.anchoredPosition=(point+edge)*.5f;marker.Leader.sizeDelta=new Vector2(delta.magnitude,1.5f);marker.Leader.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
                marker.Leader.gameObject.SetActive(delta.sqrMagnitude>1);
            }
            foreach(var marker in markers)marker.Leader.SetAsFirstSibling();
            foreach(var line in links)line.Rect.SetAsFirstSibling();
            foreach(var marker in markers)marker.Pin.SetAsLastSibling();
            foreach(var marker in markers)marker.Rect.SetAsLastSibling();
            legend.transform.SetAsLastSibling();
            PositionSelf();
        }
        static Vector2 FindLabelPosition(Vector2 point,Vector2 size,List<Rect> occupied,Vector2[] pins,Rect area)
        {
            // Offscreen records remain clipped with their labels while the user pans.
            if(!area.Contains(point))return point+Vector2.right*(size.x*.5f+16);
            var directions=new[]{Vector2.right,Vector2.left,Vector2.up,Vector2.down,new Vector2(1,1),new Vector2(-1,1),new Vector2(1,-1),new Vector2(-1,-1)};
            Vector2 fallback=point+Vector2.right*(size.x*.5f+16);
            for(int ring=0;ring<24;ring++)foreach(var direction in directions){
                var candidate=point+new Vector2(direction.x*(size.x*.5f+16+ring*(size.x+16)),direction.y*(size.y*.5f+16+ring*(size.y+16)));
                var bounds=new Rect(candidate-size*.5f-Vector2.one*5,size+Vector2.one*10);
                if(occupied.Any(r=>r.Overlaps(bounds))||pins.Any(p=>bounds.Contains(p)))continue;
                if(bounds.xMin>=area.xMin&&bounds.xMax<=area.xMax&&bounds.yMin>=area.yMin&&bounds.yMax<=area.yMax)return candidate;
            }
            // Dense zoomed-out clusters can overflow the viewport; never hide another label behind one.
            for(int step=0;step<markersPerOverflowColumn;step++){
                var candidate=fallback+Vector2.down*(step*(size.y+16));var bounds=new Rect(candidate-size*.5f-Vector2.one*5,size+Vector2.one*10);
                if(!occupied.Any(r=>r.Overlaps(bounds))&&!pins.Any(p=>bounds.Contains(p)))return candidate;
            }
            return fallback;
        }
        const int markersPerOverflowColumn=256;
        Vector2 Perspective(Vector2 point)=>IsPerspective?new Vector2((point.x-point.y)*.82f,(point.x+point.y)*.40f):point;
        Vector2 Project(Vector2 point)=>Perspective(point-center)*pixelsPerUnit*zoom+pan;
        void PositionSelf()
        {
            if(!selfArrow)return;bool visible=pose!=null&&pose.Available&&data.CurrentVersion==LayerVersion&&pose.Floor==LayerFloor;selfArrow.gameObject.SetActive(visible);if(!visible)return;
            selfArrow.anchoredPosition=Project(Point(pose.X,pose.Z));float yaw=(float)pose.Yaw*Mathf.Deg2Rad;var facing=Perspective(new Vector2(Mathf.Sin(yaw),Mathf.Cos(yaw)));
            selfArrow.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(facing.y,facing.x)*Mathf.Rad2Deg-90);selfLabel.rectTransform.rotation=Quaternion.identity;selfArrow.SetAsLastSibling();
        }
        string LayerVersion=>layer.Contains("/")?layer.Substring(0,layer.IndexOf('/')):data?.CurrentVersion??"";
        string LayerFloor=>layer.Contains("/")?layer.Substring(layer.IndexOf('/')+1):"";
        string PlaceName(string id)=>data.Places.FirstOrDefault(p=>p.Id==id&&p.Version==data.CurrentVersion)?.Label??"지도에 확인되지 않은 장소";
        void RefreshInformation()
        {
            if(data==null)return;bool old=LayerVersion!=data.CurrentVersion;
            legend.text=(layers.Length==0?"아직 관측한 공간이 없습니다.":(old?"과거 지도 "+LayerVersion+" · ":"")+FloorLabel(LayerFloor)+" · "+(IsPerspective?"입체 보기":"평면 보기")+" · "+Mathf.RoundToInt(zoom*100)+"%")+"\n"+(IsPerspective?"북쪽 ↖":"북쪽 ↑")+"   빈 곳은 아직 미확인";
            var places=data.Places.Where(p=>p.Version==LayerVersion&&p.Floor==LayerFloor).ToArray();
            string names=string.Join("\n",places.Select((p,i)=>(i+1)+"  "+p.Label+(p.Visited?"":" (표지)")));
            string history=old?"":string.Join("\n\n",data.People.Select(p=>p.Label+"\n"+PlaceName(p.PlaceId)+" · "+TimeLabel(p.Tick)+"\n"+p.ReceiptId));
            Information=(selected!=""?selected+"\n\n":"")+(names!=""?"확인한 공간\n"+names+"\n\n":"")+"파란 화살표: 나와 보는 방향\n굵은 선: 직접 통과한 연결\n● 인물의 마지막 확인 위치\n\n드래그: 이동 · 휠: 확대\n공간을 누르면 기록을 볼 수 있어요.\n\n공표 도면 미수신\n입체 표식은 확인한 지점입니다. 방 크기와 모양을 나타내지는 않아요.\n\n"+(history!=""?"인물의 마지막 확인\n"+history:"받은 인물 위치 기록이 없습니다.");
            if(screen&&screen.Context)screen.Context.text=Information;
        }
        static Vector2 Point(double x,double z)=>new Vector2((float)x,(float)z);
        static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        static string FloorLabel(string value)=>value=="B1"?"지하 1층":value=="F1"?"1층":value=="F2"?"2층":value=="F3"?"3층":value;
        static string DoorState(string value)=>value=="Locked"?"당시 잠김":value=="Open"?"당시 열림":value=="Closed"?"당시 닫힘":"문은 보았으나 상태는 조사하지 않음";
        string TimeLabel(long tick)=>WorldTimeLabel.Format(tick,clockVersion);
    }
}
