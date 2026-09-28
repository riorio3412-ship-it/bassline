using System;
using System.Linq;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
using BASSLINE.Save;
using BASSLINE.World.Mansion;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        MansionActionJournalStation[] actionStations=Array.Empty<MansionActionJournalStation>();
        MansionActionJournal actionJournal;
        ActionJournalCoverage[] ActionJournalDefinitions()=>actionStations.Select(s=>new ActionJournalCoverage{SourceId=s.SourceId,DeviceId=s.DeviceId,PlaceId=s.RoomId}).ToArray();
        void InitializeActionJournals()
        {
            actionStations=FindObjectsByType<MansionActionJournalStation>();
            incidentSignals=FindObjectsByType<MansionIncidentDeviceSignal>();
            actionJournal=new MansionActionJournal(ActionJournalDefinitions(),World.Loop,World.Tick);
        }
        MansionActionJournalStation ActionJournalTarget(string id)=>targets.TryGetValue(id,out var t)?t.GetComponent<MansionActionJournalStation>():null;
        void AdvanceActionJournals()=>actionJournal.Step(World,incidents.Capture(),id=>actionStations.Any(s=>s.SourceId==id&&s.isActiveAndEnabled&&s.Powered));
        MansionSessionSnapshot UpgradeActionJournalContent(MansionSessionSnapshot s)
        {
            if(s?.World!=null&&(s.OptionalObjects&16384)==0){s.ActionJournal=new MansionActionJournal(ActionJournalDefinitions(),s.World.Loop,s.World.Tick).Capture();s.OptionalObjects|=16384;}
            return s;
        }
        static string JournalKind(string kind)=>kind=="WarningShown"?"경고 안내 표시":kind=="ContactContinued"?"안내 후 접촉 유지":kind=="Cancelled"?"원인 작동 전 접촉 중단":kind=="Cause"?"원인 작동 확정":kind=="Result"?"연결된 결과 발생":"위험 해소";
        string ReadActionJournal(string id)
        {
            var station=ActionJournalTarget(id);if(!station||!station.isActiveAndEnabled||!CanReach("CH_01",id,2.5)||!CanSee("CH_01",id))return "";
            var own=Knowledge.For("CH_01");var snapshot=actionJournal.Capture();
            string noticeKey=station.SourceId+"_JOURNAL_NOTICE_L"+World.Loop;
            string last=own.Records().FirstOrDefault(r=>r.Direct&&r.ProvenanceKey==noticeKey)?.Id??"";
            if(last=="")last=Knowledge.Observe("CH_01",new KnownRecord{Kind="Document",ProvenanceKey=noticeKey,SubjectId=station.DeviceId,Predicate="DeviceLogCoverage",Value=station.SourceId,Text=MansionActionJournalStation.Notice+" 기록 시작: "+WorldTimeLabel.Format(snapshot.InstalledTick,World.ClockVersion),Source=id,PlaceId=station.RoomId,Position=P(station.transform.position),FromTick=World.Tick,ToTick=World.Tick+1,Supports=new[]{"직접 읽은 장치 기록 범위"},DoesNotEstablish=new[]{"장치 밖 사건", "기록되지 않은 행동의 부재"}},World.Tick);
            int added=0;
            foreach(var entry in actionJournal.Read(station.SourceId)){
                string value=entry.ActivationId+":"+entry.Kind;
                var prior=own.Records().FirstOrDefault(r=>r.Direct&&r.Kind=="DeviceLog"&&r.ProvenanceKey==station.SourceId+"_JOURNAL_L"+World.Loop&&r.Value==value&&r.FromTick==entry.Tick);
                if(prior!=null){last=prior.Id;continue;}
                last=Knowledge.Observe("CH_01",new KnownRecord{Kind="DeviceLog",ActivationId=entry.ActivationId,DeviceId=entry.DeviceId,OutcomeTarget=entry.TargetId,ActionDefinition=entry.DefinitionId,ActionRevision=entry.Revision,CausalStage=entry.Kind,ProvenanceKey=station.SourceId+"_JOURNAL_L"+World.Loop,SubjectId=entry.DeviceId,Predicate="DeviceStateTransition",Value=value,Text=WorldTimeLabel.Format(entry.Tick,World.ClockVersion)+" · "+JournalKind(entry.Kind)+"\n작동 번호: "+entry.ActivationId+"\n대상: "+(string.IsNullOrEmpty(entry.TargetId)?"기록 없음":NameOf(entry.TargetId))+" · 안내 개정: "+entry.Revision,Source=id,PlaceId=entry.PlaceId,Position=P(station.transform.position),FromTick=entry.Tick,ToTick=entry.Tick+1,Supports=new[]{"이 장치가 기록한 상태 변화", "같은 작동 번호에 속한 기록의 선후 관계"},DoesNotEstablish=new[]{"조작한 사람의 신원", "행위자의 사적인 동기", "다른 장치의 작동이나 기록되지 않은 시간"}},World.Tick);added++;
            }
            message=added>0?"장치 기록 "+added+"개를 수첩에 옮겼어요.":"새 기록은 없어요. 이 단말이 기록하는 범위를 수첩에 남겼어요.";return last;
        }
    }
}
