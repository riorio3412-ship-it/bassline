using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
using BASSLINE.Knowledge;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerMapPort,IPlayerMapPosePort
    {
        sealed class MapSign { public Transform Face;public string Room;public string Floor; }
        MapSign[] mapSigns;KnowledgeLedger mapObservedLedger;string priorMapRoom="";Vector3 priorMapPoint;long priorMapTick=-1;
        const string MapPrefix="MAP|";
        string MapKey=>MapPrefix+Layout.MapVersion;

        public string PlaceLabel(string id)
        {
            if(id==null)return "미확인 장소";
            if(id.StartsWith("R_BED_",StringComparison.Ordinal)&&id!="R_BED_COR")return (Layout.Room(id)?.DisplayName??"개인실").Replace(" private bedroom"," 개인실");
            return KoreanPlaceLabels.TryGetValue(id,out var label)?label:Layout.Room(id)?.DisplayName??"미확인 장소";
        }
        static readonly Dictionary<string,string> KoreanPlaceLabels=new Dictionary<string,string>{
            {"R_HALL","중앙 홀"},{"R_VEST","현관"},{"R_EXT","외부 입구"},{"R_CE","동측 회랑"},{"R_CW","서측 회랑"},{"R_CN","북측 회랑"},
            {"R_TRIAL","재판실"},{"R_TRIAL_ENTRY","재판실 입구"},{"R_LIV_COR","생활 구역"},{"R_DINING","식당"},{"R_KITCHEN","주방"},{"R_FOOD","식품 창고"},
            {"R_LOUNGE","휴게실"},{"R_BATH_A","공용 욕실 A"},{"R_BATH_B","공용 욕실 B"},{"R_LIB_COR","도서 · 기록 구역"},{"R_LIBRARY","원형 도서관"},
            {"R_READING","독서실"},{"R_ARCHIVE","기록실"},{"R_CUL_COR","문화 구역"},{"R_MUSIC","음악실"},{"R_ARCADE","오락실"},{"R_THEATER","극장"},{"R_EXHIBIT","전시실"},
            {"R_NAT_COR","자연 구역"},{"R_GREEN","온실"},{"R_GARDEN","실내 정원"},{"R_FLOWER","보라 꽃 회랑"},{"R_WRK_COR","작업 · 정비 구역"},{"R_WORK","작업실"},
            {"R_STORAGE","대형 창고"},{"R_LAUNDRY","세탁실"},{"R_MACHINE","기계실"},{"R_GEN","발전기실"},{"R_MAINT","정비 복도"},{"R_MED_COR","의료 구역"},
            {"R_AID","응급 처치실"},{"R_RECOVERY","회복실"},{"R_AQU_COR","수경 구역"},{"R_POOL","실내 수영장"},{"R_FLOOD","얕은 물 회랑"},{"R_WATER","수질 관리실"},
            {"R_ANX_COR","별관 구역"},{"R_ANNEX","별관 로비"},{"R_CLOSED","폐쇄 객실동"},{"R_MEETING","옛 회의실"},{"R_BED_COR","개인실 복도"},{"R_UP_E","개인실 연결 회랑"},
            {"R_BALCONY","중앙 홀 발코니"},{"R_STAIR","서비스 계단"},{"R_COLD","저온 창고"},{"R_ABANDON","폐기 대기 창고"},{"R_OBSERVE","전망실"},{"R_ASTRO","천체 관측실"},{"R_ROOF","옥상 정원"},
            {"R_GRAND","큰 계단"},{"R_ANNOUNCE","공지 구역"}
        };
        public string PublishedMapText()
        {
            var map=ReadMap();return "직접 관측으로 작성한 지도\n\n"+(map.Places.Count==0?"아직 지도에 기록한 공간이 없습니다.":string.Join("\n",map.Places.Select(p=>p.Floor+" · "+p.Label)))+"\n\n공표 도면 미수신 · 미확인 구역은 표시하지 않습니다.";
        }
        public KnownMapSnapshot ReadMap()=>KnownMapProjection.Create(Knowledge.For("CH_01"),Layout.MapVersion,NameOf);
        public KnownMapPose ReadMapPose()
        {
            var self=World.Resident("CH_01");var room=Layout.RoomAt(V(self.Position));
            var known=room?ReadMap().Places.FirstOrDefault(p=>p.Version==Layout.MapVersion&&p.Id==room.RoomId&&p.Visited):null;
            return new KnownMapPose{Available=known!=null,Floor=known?.Floor??"",X=self.Position.X,Z=self.Position.Z,Yaw=World.Yaw};
        }

        // PRODUCTION PROPOSAL sensor: sample a visible fixed surface, not room-volume entry.
        // The receipt stores one observed landmark point, never the author's room bounds.
        void ObserveMap(FixtureActorBody observer)
        {
            if(observer.ActorId!="CH_01"||!cameraView)return;
            if(mapObservedLedger!=Knowledge){mapObservedLedger=Knowledge;priorMapRoom="";priorMapTick=-1;}
            var room=Layout.RoomAt(observer.transform.position);if(!room)return;
            if(mapSigns==null)CacheMapSigns();
            bool hasSurface=HasVisibleRoomSurface(observer,room);
            if(hasSurface&&!HasMapReceipt(room.RoomId,"MapAreaSeen"))
                MapReceipt(room.RoomId,"MapAreaSeen",room.Floor,room.RoomId,"내부 표면을 직접 확인한 공간",observer.transform.position);
            if(hasSurface&&priorMapRoom!=""&&priorMapRoom!=room.RoomId&&World.Tick-priorMapTick==30&&(observer.transform.position-priorMapPoint).magnitude<=3)
            {
                // Both ends must already be observed; authored connections cannot disclose an unseen exit.
                if(HasMapReceipt(priorMapRoom,"MapAreaSeen")&&!Knowledge.For("CH_01").Records().Any(r=>r.Direct&&r.ProvenanceKey==MapKey&&r.Predicate=="MapPassageUsed"&&((r.SubjectId==priorMapRoom&&r.Value==room.RoomId)||(r.SubjectId==room.RoomId&&r.Value==priorMapRoom))))
                    MapReceipt(priorMapRoom,"MapPassageUsed",room.RoomId,room.RoomId,"직접 이동해 확인한 연결",observer.transform.position);
            }
            priorMapRoom=hasSurface?room.RoomId:"";priorMapPoint=observer.transform.position;priorMapTick=World.Tick;
            Vector3 eye=observer.transform.position+Vector3.up*observer.Height*.88f;
            foreach(var sign in mapSigns)
            {
                if(!sign.Face||!sign.Face.gameObject.activeInHierarchy||HasMapReceipt(sign.Room,"MapNameRead"))continue;
                Vector3 face=sign.Face.position-sign.Face.forward*.025f;
                if(Vector3.Dot(-sign.Face.forward,(eye-face).normalized)<.15f||!Visible("CH_01",face,6,true))continue;
                var lettering=sign.Face.Find("Lettering");var renderer=lettering?lettering.GetComponent<Renderer>():null;if(!renderer||!renderer.enabled)continue;
                MapReceipt(sign.Room,"MapNameRead",sign.Floor,sign.Room,PlaceLabel(sign.Room),face);
            }
            foreach(var door in Layout.Connections)
            {
                if(string.IsNullOrEmpty(door.DoorId)||HasMapReceipt(door.DoorId,"MapDoorSeen"))continue;
                Vector3 point=door.transform.position+Vector3.up*1.25f;
                if(!Visible("CH_01",point,5,true))continue;
                MapReceipt(door.DoorId,"MapDoorSeen",room.Floor,room.RoomId,"직접 확인한 문",point);
            }
        }
        bool HasMapReceipt(string subject,string predicate)
        {
            var record=Knowledge.LastDirect("CH_01",subject,predicate);return record!=null&&record.ProvenanceKey==MapKey;
        }
        void MapReceipt(string subject,string predicate,string value,string place,string text,Vector3 point)
        {
            Knowledge.Observe("CH_01",new KnownRecord{Kind="Visual",SubjectId=subject,Predicate=predicate,Value=value,PlaceId=place,Position=P(point),Text=text,Source="CH_01",ProvenanceKey=MapKey,FromTick=World.Tick,ToTick=World.Tick+1,Supports=new[]{predicate=="MapNameRead"?"읽은 표지의 장소 이름":"직접 확인한 공간 또는 통과 지점"},DoesNotEstablish=new[]{"공간 전체의 실측 윤곽이나 미확인 출입구를 입증하지 않음","문·사람의 현재 상태를 자동 갱신하지 않음"}},World.Tick);
        }
        bool HasVisibleRoomSurface(FixtureActorBody observer,MansionRoom room)
        {
            foreach(float y in new[]{.5f,.35f,.65f})
            {
                var ray=cameraView.ViewportPointToRay(new Vector3(.5f,y));
                foreach(var hit in Physics.RaycastAll(ray,10,~0,QueryTriggerInteraction.Ignore).OrderBy(x=>x.distance))
                {
                    if(hit.collider.transform.IsChildOf(observer.transform))continue;
                    if(hit.collider.GetComponentInParent<MansionRoom>()==room&&!hit.collider.GetComponentInParent<FixtureActorBody>()&&!hit.collider.GetComponentInParent<FixtureObjectBody>())return true;
                    break;
                }
            }
            return false;
        }
        void CacheMapSigns()
        {
            var signs=new List<MapSign>();
            foreach(var room in Layout.Rooms)
                foreach(var t in room.GetComponentsInChildren<Transform>(true))
                    if(t.name=="RoomNumber"||t.name=="Wayfinding"||t.name=="WingSign")signs.Add(new MapSign{Face=t,Room=room.RoomId,Floor=room.Floor});
            foreach(var door in Layout.Connections)
            {
                var a=door.transform.Find("DoorSignA");var b=door.transform.Find("DoorSignB");
                if(a)signs.Add(new MapSign{Face=a,Room=door.RoomB,Floor=Layout.Room(door.RoomA)?.Floor??"?"});
                if(b)signs.Add(new MapSign{Face=b,Room=door.RoomA,Floor=Layout.Room(door.RoomB)?.Floor??"?"});
            }
            mapSigns=signs.ToArray();
        }
    }
}
