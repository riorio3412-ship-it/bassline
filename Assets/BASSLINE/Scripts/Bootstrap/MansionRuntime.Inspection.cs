using System;
using System.Linq;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
using BASSLINE.Save;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        public string Examine(string id)
        {
            if(World.Paused||!World.CanAct("CH_01")||WritingCard||itemExchange.Handoff.Running||PlayerSurfaceRunning||conversationPlayback.Phase=="Speaking"||!Reach(id))return "Unavailable";
            if(inspection.State=="Running"&&inspection.TargetId==id)return "Pending";
            if(doors.ContainsKey(id)&&!ReadVisibleDoorPose("CH_01",id,out _,out _))return "Unavailable";
            var rule=IncidentRulePlate(id);
            if(rule&&!CanReadIncidentRule("CH_01",id))return "Unavailable";
            var own=Knowledge.For("CH_01").Records();
            var focus=SurfaceFeatures(id).Where(c=>!rule&&SeesPattern("CH_01",c.Visual))
                .OrderBy(c=>own.Any(r=>r.Direct&&r.ProvenanceKey==c.Root&&r.Value==c.Visual.Pattern.Key&&r.SubjectId==ObservedSurfaceSubject("CH_01",c.Target))?1:0)
                .ThenBy(c=>Vector3.Distance(cameraView.transform.position,c.Visual.transform.position))
                .ThenBy(c=>c.Root,StringComparer.Ordinal).FirstOrDefault();
            if(!rule&&!focus.Visual&&!Visible("CH_01",SubjectPoint(id),2.9f,true))return "Unavailable";
            CancelWeaponRinse();CancelWeapon();CancelToolPress();CancelPlayerRescue("InspectionRequested");StopWaiting("자세히 살펴보려고 멈췄어요.");
            move=default;running=false;
            inspection=new InspectionState{TargetId=id,State="Running",DurationTicks=rule?IncidentRuleReading.Duration(rule.Definition.PublicRule):120,StartedTick=World.Tick,LastTick=World.Tick,
                FocusMode=rule?"Rule":focus.Visual?"Surface":"Target",FocusRoot=rule?IncidentRuleReading.Root(rule.Definition):focus.Root??"",FocusValue=rule?IncidentRuleReading.ContentKey(rule.Definition):focus.Visual?focus.Visual.Pattern.Key:""};
            return "Pending";
        }
        bool InspectionFocusVisible()
        {
            if(!Reach(inspection.TargetId))return false;
            if(doors.ContainsKey(inspection.TargetId))return ReadVisibleDoorPose("CH_01",inspection.TargetId,out _,out _);
            if(inspection.FocusMode=="Rule"){
                var rule=IncidentRulePlate(inspection.TargetId);
                return rule&&CanReadIncidentRule("CH_01",inspection.TargetId)&&inspection.FocusRoot==IncidentRuleReading.Root(rule.Definition)&&inspection.FocusValue==IncidentRuleReading.ContentKey(rule.Definition);
            }
            if(inspection.FocusMode=="Target"&&IncidentRulePlate(inspection.TargetId))return false;
            if(inspection.FocusMode=="Surface")return SurfaceFeatures(inspection.TargetId).Any(c=>c.Root==inspection.FocusRoot&&c.Visual&&c.Visual.Pattern.Key==inspection.FocusValue&&SeesPattern("CH_01",c.Visual));
            return Visible("CH_01",SubjectPoint(inspection.TargetId),2.9f,true);
        }
        void AdvanceInspection()
        {
            if(inspection.State!="Running"||World.Paused||inspection.LastTick==World.Tick)return;
            if(inspection.LastTick!=World.Tick-1||!World.CanAct("CH_01")||WritingCard||itemExchange.Handoff.Running||PlayerSurfaceRunning||toolPress.Running||conversationPlayback.Phase=="Speaking"||!InspectionFocusVisible()){
                CancelInspection();return;
            }
            inspection.LastTick=World.Tick;
            if(++inspection.ElapsedTicks<inspection.DurationTicks)return;
            CompleteInspection();
        }
        void CompleteInspection()
        {
            string id=inspection.TargetId;string place=ObjectPlace(targets[id].transform.position);string predicate="AtPlace",value=place,description=NameOf(id)+"의 현재 상태와 위치를 가까이서 살펴보았다.";
            string pattern=inspection.FocusMode=="Surface"?ObserveSurfacePatterns("CH_01",id,inspection.FocusRoot):"";
            if(inspection.FocusMode=="Surface"){FinishInspection(pattern);return;}
            if(inspection.FocusMode=="Rule"){FinishInspection(ReadIncidentRule("CH_01",id));return;}
            string residue=InspectWeaponResidue("CH_01",id);if(residue!=""){FinishInspection(residue);return;}
            if(ActionJournalTarget(id)){FinishInspection(ReadActionJournal(id));return;}
            if(id==AppointmentDesk.CardId){FinishInspection(ReadPhysicalAppointmentCard());return;}
            if(id==CommonReturnStation.BookId){FinishInspection(ReadCleanupBook());return;}
            if(id==CommonReturnStation.PrivateNoteId){FinishInspection(ReadPrivateReturnNote());return;}
            if(id==CommonReturnStation.BagFrontId){predicate="PrivateBag";value="TaegyeomBag";description="태겸이라는 이름표가 달린 개인 가방. 앞에는 펜 한 자루를 놓을 만한 자리가 있다. 공용 반납대와는 다른 탁자다.";}
            if(id==CommonReturnStation.TrayId){predicate="PublicNotice";value="CommonReturnUse";description="공용 반납대. 공동 물품을 두는 곳이다. 개인 물건은 주인에게 직접 돌려주라는 안내가 붙어 있다. 남겨진 물품은 정리 담당자가 공용 정리함으로 옮기고 옆의 장부에 적는다. 정리함과 장부는 누구나 열람할 수 있다.";}
            if(doors.ContainsKey(id)){FinishInspection(ReadVisibleDoorPose("CH_01",id,out _,out string visibleState)?ObserveDoorState("CH_01",id,visibleState,World.Tick):"");return;}
            if(id==LoanPen){predicate="ObjectFeature";value="BluePenCapWear";description="푸른 펜의 뚜껑 옆에 짧은 흠집이 있다. 은색 클립이 달려 있다. 외관만으로 이전 소지자나 소유권까지 알 수는 없다.";}
            if(id=="M_KITCHEN_KNIFE"){predicate="ObjectFeature";value="KitchenKnifeAppearance";description="짙은 손잡이에 금속 리벳 세 개가 박힌 주방 칼. 날의 끝이 비스듬히 좁아진다.";}
            if(id=="M_WORK_HAMMER"){predicate="ObjectFeature";value="WorkshopHammerAppearance";description="짙은색 손잡이에 가로로 긴 금속 머리가 달린 망치. 머리 아래에는 금속 고정대가 있다.";}
            if(id=="M_EXHIBIT_DAGGER"){predicate="ObjectFeature";value="ExhibitDaggerAppearance";description="끝이 뾰족한 대칭형 금속 날과 짙은 손잡이. 손잡이 위 가드와 끝 장식은 금빛이다.";}
            FinishInspection(Observe("CH_01",id,predicate,value,description,targets[id].transform.position,new[]{"조사한 현재 시점의 대상 상태"},new[]{"이전 사용자·원인·정확한 변화 시각은 입증하지 않음"},place));
        }
        void FinishInspection(string record)
        {
            inspection.RecordId=record??"";
            inspection.State=string.IsNullOrEmpty(inspection.RecordId)?"Cancelled":"Completed";
        }
        public InspectionState ReadInspection()=>inspection.Copy();
        public void CancelInspection(){if(inspection.State=="Running")inspection.State="Cancelled";}
        void ValidateInspection(MansionSessionSnapshot s)
        {
            var task=s.Inspection;
            if(task==null||!new[]{"Idle","Running","Completed","Cancelled"}.Contains(task.State)||task.DurationTicks<120||task.FocusMode!="Rule"&&task.DurationTicks!=120||task.ElapsedTicks<0||task.ElapsedTicks>task.DurationTicks||!new[]{"Target","Surface","Rule"}.Contains(task.FocusMode)||task.FocusRoot==null||task.FocusValue==null)
                throw new System.IO.InvalidDataException("조사 진행 상태가 올바르지 않습니다.");
            if(task.State!="Running")return;
            if(!targets.ContainsKey(task.TargetId)||task.RecordId!=""||task.StartedTick<0||task.LastTick!=s.World.Tick||task.LastTick-task.StartedTick!=task.ElapsedTicks||task.ElapsedTicks>=task.DurationTicks)
                throw new System.IO.InvalidDataException("조사를 이어가던 시각과 대상이 일치하지 않습니다.");
            if(task.FocusMode=="Rule"){
                var plate=IncidentRulePlate(task.TargetId);
                if(!plate||!plate.HasDisplayedContent()||task.FocusRoot!=IncidentRuleReading.Root(plate.Definition)||task.FocusValue!=IncidentRuleReading.ContentKey(plate.Definition)||task.DurationTicks!=IncidentRuleReading.Duration(plate.Definition.PublicRule))
                    throw new System.IO.InvalidDataException("읽고 있던 안내문 내용이나 개정 번호가 바뀌었습니다.");
                return;
            }
            if(task.FocusMode=="Target"){
                if(task.FocusRoot!=""||task.FocusValue!="")throw new System.IO.InvalidDataException("대상 조사의 초점이 올바르지 않습니다.");
                return;
            }
            bool face=traceSources.Any(t=>t&&TraceSourceId(t)==task.TargetId&&t.Face&&t.Face.Pattern.Key==task.FocusValue&&task.FocusRoot=="PATTERN_FACE_L"+s.World.Loop+"_"+task.TargetId);
            bool mark=s.SurfaceTraces.Marks.Any(m=>m.Id==task.FocusRoot&&m.SurfaceId==task.TargetId&&m.Pattern.Key==task.FocusValue);
            if(!face&&!mark)throw new System.IO.InvalidDataException("살펴보던 무늬와 실제 표면이 일치하지 않습니다.");
        }
    }
}
