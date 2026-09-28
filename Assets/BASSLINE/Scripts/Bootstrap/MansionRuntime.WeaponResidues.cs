using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
using BASSLINE.World.Mansion;
using BASSLINE.Save;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        WeaponResidues weaponResidues;
        WeaponRinseState weaponRinse=new WeaponRinseState();
        readonly Dictionary<string,GameObject> weaponStains=new Dictionary<string,GameObject>();
        Material residueMaterial;
        MansionWashStation WashStation(string id)=>targets.TryGetValue(id,out var t)?t.GetComponent<MansionWashStation>():null;
        void DepositWeaponResidue(string weapon)
        {
            var incident=incidents.All().LastOrDefault(c=>c.Capture().CauseTick==World.Tick&&c.Capture().Settings.PlayerInitiated&&c.Capture().Settings.ObjectId==weapon);
            if(incident!=null)weaponResidues.Deposit(World,incident.Capture());
        }
        void PresentWeaponResidue(FixtureObjectBody body)
        {
            var marks=weaponResidues.For(body.ObjectId);
            if(marks.Length==0){if(weaponStains.TryGetValue(body.ObjectId,out var old))old.SetActive(false);return;}
            if(!weaponStains.TryGetValue(body.ObjectId,out var stain)||!stain){
                if(!residueMaterial){residueMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));residueMaterial.SetColor("_BaseColor",new Color(.38f,.025f,.035f));residueMaterial.SetFloat("_Smoothness",.23f);}
                stain=new GameObject("표면 얼룩");stain.transform.SetParent(body.transform,false);weaponStains[body.ObjectId]=stain;
                bool hammer=body.GetComponent<MansionWeapon>().Category=="Blunt";
                // Geometry follows the visible contact part, on both sides of the metal.
                foreach(int side in new[]{-1,1})for(int i=0;i<4;i++){
                    var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name="얼룩";g.transform.SetParent(stain.transform,false);g.GetComponent<Collider>().enabled=false;Destroy(g.GetComponent<Collider>());
                    g.transform.localPosition=new Vector3(hammer?(i-1.5f)*.025f:(i%2==0?-.005f:.003f),side*(hammer?.033f:.0034f),hammer?.264f:.115f+i*.027f);
                    g.transform.localScale=hammer?new Vector3(.025f,.0014f,.044f):new Vector3(.019f,.0014f,.031f);
                    g.GetComponent<Renderer>().sharedMaterial=residueMaterial;
                }
            }
            stain.SetActive(true);bool faint=marks.All(m=>m.RinsedTick>=0);
            foreach(Transform patch in stain.transform){var scale=patch.localScale;scale.x=(body.GetComponent<MansionWeapon>().Category=="Blunt"?.025f:.019f)*(faint?.22f:1);patch.localScale=scale;}
        }
        string InspectWeaponResidue(string observer,string weapon)
        {
            var body=ObjectBodies.FirstOrDefault(o=>o.ObjectId==weapon&&o.GetComponent<MansionWeapon>());
            if(!body||!Visible(observer,body.transform.position,2.9f,true))return "";
            var marks=weaponResidues.For(weapon);if(marks.Length==0)return "";
            string appearance=marks.Any(m=>m.RinsedTick<0)?"RedStain":"FaintRedStain";
            // One weapon's stains are one physical source, including rereads after rinsing.
            string root="WEAPON_SURFACE_L"+World.Loop+"_"+weapon;
            var previous=Knowledge.For(observer).Records().LastOrDefault(r=>r.Direct&&r.ProvenanceKey==root);
            if(previous!=null&&previous.Value==appearance)return previous.Id;
            return Knowledge.Observe(observer,new KnownRecord{Kind="Visual",Source=observer,SubjectId=weapon,Predicate="SurfaceResidue",Value=appearance,ProvenanceKey=root,Text=NameOf(weapon)+(appearance=="RedStain"?"의 금속 부분에 붉은 얼룩이 묻어 있다.":"의 금속 부분에 옅은 붉은 자국이 남아 있다.")+" 색만으로 무엇이 묻었는지, 누구에게서 왔는지는 알 수 없다.",Position=P(body.transform.position),PlaceId=PlaceOf(body.transform.position),FromTick=World.Tick,ToTick=World.Tick+1,Supports=new[]{"살펴본 물건에 현재 남아 있는 얼룩"},DoesNotEstablish=new[]{"얼룩의 성분·주인·발생 시각", "사용한 사람·범인", "세척 여부·세척한 사람"}},World.Tick);
        }
        string BeginWeaponRinse(string stationId)
        {
            var station=WashStation(stationId);var weapon=HeldWeapon();
            if(!station||!station.WaterPoint||!weapon)return "씻을 물건을 손에 들어 주세요.";
            if(weaponRinse.Running)return "물건을 씻고 있어요. 움직이면 멈춥니다.";
            if(World.Paused||!World.CanAct("CH_01")||weaponMotion.Running||PlayerSurfaceRunning||WritingCard||itemExchange.Handoff.Running||conversationPlayback.Phase=="Speaking")return "지금은 씻을 수 없어요.";
            var arm=Arm("CH_01");var grip=station.WaterPoint.position-bodies["CH_01"].transform.forward*.18f;
            if(!Reach(stationId)||!arm||Vector3.Distance(arm.Shoulder,grip)>arm.UpperLength+arm.ForearmLength-.03f)return "수도 앞에 더 가까이 다가가 주세요.";
            CancelInspection();CancelToolPress();CancelPlayerRescue("RinseRequested");StopWaiting("물건을 씻으려고 멈췄어요.");move=default;running=false;
            weaponRinse=new WeaponRinseState{Phase="Running",WeaponId=HeldWeaponId(),StationId=stationId,StartedTick=World.Tick,LastTick=World.Tick,Origin=World.Resident("CH_01").Position,StartGrip=P(weapon.transform.position),Grip=P(grip),Yaw=World.Yaw,
                Witnesses=World.Residents.Where(r=>World.CanAct(r.Id)&&(r.Id=="CH_01"||CanSee(r.Id,"CH_01")&&CanSee(r.Id,HeldWeaponId())&&Identifies(r.Id,"CH_01"))).Select(r=>r.Id).ToArray()};
            World.Emit("WeaponRinseStarted","CH_01",weaponRinse.WeaponId,stationId);return "물건을 씻기 시작했어요. 움직이면 멈춥니다.";
        }
        void RinsePose(int elapsed,out Vector3 grip,out Quaternion rotation)
        {
            float t=elapsed<30?Mathf.SmoothStep(0,1,elapsed/30f):elapsed>150?Mathf.SmoothStep(1,0,(elapsed-150)/30f):1;
            grip=Vector3.Lerp(V(weaponRinse.StartGrip),V(weaponRinse.Grip),t);rotation=Quaternion.Euler(0,(float)weaponRinse.Yaw,0)*Quaternion.Slerp(Quaternion.Euler(-35,0,0),Quaternion.identity,t);
        }
        void AdvanceWeaponRinse()
        {
            var w=weaponRinse;if(!w.Running)return;
            var station=WashStation(w.StationId);var weapon=HeldWeapon();
            if(!station||!station.WaterPoint||!weapon||HeldWeaponId()!=w.WeaponId||!World.CanAct("CH_01")||World.Resident("CH_01").Position.Distance(w.Origin)>.04||w.LastTick!=World.Tick-1||!Reach(w.StationId)){CancelWeaponRinse();return;}
            w.Witnesses=w.Witnesses.Where(id=>World.CanAct(id)&&(id=="CH_01"||CanSee(id,"CH_01")&&CanSee(id,w.WeaponId)&&Identifies(id,"CH_01"))).ToArray();
            RinsePose(w.Elapsed,out var from,out var oldRotation);RinsePose(w.Elapsed+1,out var next,out var rotation);
            var box=weapon.GetComponent<FixtureObjectBody>().Collider as BoxCollider;
            if(!box||!WeaponSweep(box,from,oldRotation,next,rotation,out _)||!Arm("CH_01").Pose(next,bodies["CH_01"].transform.rotation)){CancelWeaponRinse();message="물건이 닿아서 씻기를 멈췄어요. 위치를 바꿔 주세요.";return;}
            w.LastTick=World.Tick;w.Elapsed++;
            if(station.Water)station.Water.SetActive(w.Elapsed>=30&&w.Elapsed<150);
            if(w.Elapsed==150){
                World.Emit("WeaponRinsed","CH_01",w.WeaponId,w.StationId);w.CommitSequence=World.Events.Last().Sequence;weaponResidues.Rinse(World,w.WeaponId,w.CommitSequence);
                foreach(string observer in w.Witnesses)Knowledge.Observe(observer,new KnownRecord{Kind="Visual",Source=observer,SubjectId="CH_01",Predicate="RinsedObject",Value=w.WeaponId,IdentityConfirmed=true,ProvenanceKey="RINSE_L"+World.Loop+"_"+w.CommitSequence+"_"+observer,Text=(observer=="CH_01"?"나는":NameOf("CH_01")+"이")+" 수도에서 "+NameOf(w.WeaponId)+"을 씻었다. 씻은 이유나 사용 이력은 이 행동만으로 알 수 없다.",Position=P(station.transform.position),PlaceId=PlaceOf(station.transform.position),FromTick=w.StartedTick+30,ToTick=World.Tick+1,Supports=new[]{"직접 본 물건을 씻는 행동"},DoesNotEstablish=new[]{"세척 전 얼룩의 정체", "공격·사망과의 연관성", "증거를 없애려는 의도"}},World.Tick);
            }
            if(w.Elapsed==180){w.Phase="Completed";message="씻기를 마쳤어요. 남은 자국은 물건을 살펴보면 확인할 수 있어요.";}
        }
        void CancelWeaponRinse()
        {
            if(!weaponRinse.Running)return;weaponRinse.Phase="Cancelled";
            var station=WashStation(weaponRinse.StationId);if(station&&station.Water)station.Water.SetActive(false);
            message="씻기를 멈췄어요.";
        }
        void RestoreWeaponResidues(MansionSessionSnapshot s)
        {
            weaponResidues=WeaponResidues.Restore(s.WeaponResidues,s.World,ObjectBodies.Where(o=>o.GetComponent<MansionWeapon>()).Select(o=>o.ObjectId).ToArray());weaponRinse=s.WeaponRinse.Copy();
            foreach(var station in targets.Values.Select(t=>t.GetComponent<MansionWashStation>()).Where(s=>s&&s.Water))station.Water.SetActive(weaponRinse.Running&&weaponRinse.StationId==station.GetComponent<FixtureTarget>().StableId&&weaponRinse.Elapsed>=30&&weaponRinse.Elapsed<150);
        }
        void ValidateWeaponResidues(MansionSessionSnapshot s)
        {
            WeaponResidues.Restore(s.WeaponResidues,s.World,ObjectBodies.Where(o=>o.GetComponent<MansionWeapon>()).Select(o=>o.ObjectId).ToArray());
            var w=s.WeaponRinse;
            if(w==null||!new[]{"Idle","Running","Cancelled","Completed"}.Contains(w.Phase)||w.Witnesses==null||w.Witnesses.Distinct().Count()!=w.Witnesses.Length||w.Witnesses.Any(id=>!bodies.ContainsKey(id)))throw new System.IO.InvalidDataException("씻기 상태가 올바르지 않습니다.");
            if(w.Phase=="Idle")return;
            if(!WashStation(w.StationId)||!ObjectBodies.Any(o=>o.ObjectId==w.WeaponId&&o.GetComponent<MansionWeapon>())||w.StartedTick<0||w.LastTick>s.World.Tick||w.LastTick-w.StartedTick!=w.Elapsed||w.Elapsed<0||w.Elapsed>180||!w.Origin.Finite()||!w.StartGrip.Finite()||!w.Grip.Finite()||double.IsNaN(w.Yaw)||double.IsInfinity(w.Yaw)||w.Elapsed>=150!=(w.CommitSequence>0)||w.Phase=="Completed"&&w.Elapsed!=180)throw new System.IO.InvalidDataException("씻기 동작의 시간·대상이 맞지 않습니다.");
            if(w.CommitSequence>0&&!s.World.Events.Any(e=>e.Sequence==w.CommitSequence&&e.Type=="WeaponRinsed"&&e.Target==w.WeaponId&&e.Detail==w.StationId&&e.Tick==w.StartedTick+150))throw new System.IO.InvalidDataException("씻기 완료 이력이 없습니다.");
            if(w.Grip.Distance(P(WashStation(w.StationId).WaterPoint.position-Quaternion.Euler(0,(float)w.Yaw,0)*Vector3.forward*.18f))>.002||!s.World.Events.Any(e=>e.Type=="WeaponRinseStarted"&&e.Actor=="CH_01"&&e.Target==w.WeaponId&&e.Detail==w.StationId&&e.Tick==w.StartedTick))throw new System.IO.InvalidDataException("수도의 위치나 씻기 시작 이력이 다릅니다.");
            if(w.Running){var player=s.World.Residents.Single(r=>r.Id=="CH_01");var item=s.World.Objects.Single(o=>o.Id==w.WeaponId);if(!player.Alive||!player.Present||player.HeldObject!=w.WeaponId||item.Location!="Hand"||item.Owner!="CH_01"||player.Position.Distance(w.Origin)>.04||w.Elapsed>=180||w.LastTick!=s.World.Tick||s.WeaponMotion.Running)throw new System.IO.InvalidDataException("씻던 물건과 손 상태가 다릅니다.");}
        }
    }
}
