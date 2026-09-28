using System;
using System.IO;
using System.Linq;
using BASSLINE.Save;
using BASSLINE.World.Mansion;
using UnityEngine;

namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        static string DeathNoticeId(MansionIncidentSnapshot c)=>"DEATH_"+c.Settings.Id+"_"+c.Settings.TargetId;
        static void UpgradeCaseCollection(MansionSessionSnapshot s)
        {
            if(s.CaseCollectionVersion==0){
                s.Incidents=s.Incident.Settings==null?Array.Empty<MansionIncidentSnapshot>():new[]{s.Incident};
                var progress=s.Proceedings;progress.PendingReportCases=s.Incidents.Where(c=>c.ReportTick>=0&&c.ConfirmationTick<0).Select(c=>c.Settings.Id).ToArray();
                progress.InspectionCaseId=progress.Phase=="InspectingBody"?progress.PendingReportCases.FirstOrDefault()??"":"";
                if(s.Incidents.Length==1)progress.ImportedReceipts=progress.ImportedReceipts.Select(id=>s.Incident.Settings.Id+"|"+id).ToArray();
                var schedule=new MansionPublicCaseSchedule();
                if(progress.FirstAnnouncementTick>=0){
                    if(s.Incident.Settings==null||s.Incident.ConfirmationTick<0)throw new InvalidDataException("이전 공표에 대응하는 사건이 없습니다.");
                    schedule.PublishConfirmedDeath(DeathNoticeId(s.Incident),progress.FirstAnnouncementTick);
                    if(schedule.ConveneAt!=progress.ConveneAt)throw new InvalidDataException("이전 공표 일정이 일치하지 않습니다.");
                }
                progress.PublicSchedule=schedule.Capture();s.CaseCollectionVersion=1;
            }
            if(s.CaseCollectionVersion!=1||s.Incidents==null||s.Incidents.Any(c=>c==null||c.Settings==null))throw new InvalidDataException("사건 모음 형식을 읽을 수 없습니다.");
            var first=s.Incidents.FirstOrDefault()??new MansionIncident().Capture();
            if(JsonUtility.ToJson(s.Incident)!=JsonUtility.ToJson(first))throw new InvalidDataException("사건 모음과 호환 기록이 다릅니다.");
            // Keep one in-memory alias for old callers; the collection is authoritative on disk.
            s.Incident=first;
        }
        static void ValidateCaseProgress(MansionSessionSnapshot s,MansionIncidentCollection cases)
        {
            if(s.CaseCollectionVersion!=1||s.Incidents==null||s.Proceedings==null)throw new InvalidDataException("사건 모음이 없습니다.");
            var p=s.Proceedings;var schedule=MansionPublicCaseSchedule.Restore(p.PublicSchedule,s.World.Tick).Capture();
            var confirmed=cases.All().Where(c=>c.CanConvene).Select(c=>c.Capture()).ToArray();
            if(schedule.ConveneAt!=p.ConveneAt||p.FirstAnnouncementTick!=(schedule.Publications.FirstOrDefault()?.Tick??-1)
                ||!schedule.Publications.Select(n=>n.DeathId).OrderBy(id=>id).SequenceEqual(confirmed.Select(DeathNoticeId).OrderBy(id=>id))
                ||schedule.Publications.Any(n=>n.Tick<confirmed.Single(c=>DeathNoticeId(c)==n.DeathId).ConfirmationTick))throw new InvalidDataException("공표와 확인 이력이 일치하지 않습니다.");
            if(p.PendingReportCases==null||p.InspectionCaseId==null||p.PendingReportCases.Distinct().Count()!=p.PendingReportCases.Length
                ||p.PendingReportCases.Any(id=>cases.Find(id)==null||cases.Find(id).Capture().ReportTick<0||cases.Find(id).CanConvene)
                ||p.InspectionCaseId!=""&&!p.PendingReportCases.Contains(p.InspectionCaseId)
                ||(p.Phase=="InspectingBody")!=(p.InspectionCaseId!=""))throw new InvalidDataException("신고 대기열과 현장 확인 상태가 다릅니다.");
        }
    }
}
