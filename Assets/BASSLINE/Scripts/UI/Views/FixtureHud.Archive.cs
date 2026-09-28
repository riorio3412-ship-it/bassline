using System;
using System.Linq;
using System.Text;
using BASSLINE.Core;
using UnityEngine;
namespace BASSLINE.UI
{
    public sealed partial class FixtureHud
    {
        const int ArchivePageSize=2;
        string archiveLoopId="",archiveCaseId="",archiveCompareId="";int archiveRecordPage;
        IPlayerArchivePort Archive=>Source as IPlayerArchivePort;
        ArchiveSnapshot ReadArchiveView()=>Archive?.ReadArchive()??new ArchiveSnapshot("CH_01","",Array.Empty<ArchiveCaseView>());

        void NormalizeArchiveSelection(ArchiveSnapshot data)
        {
            archiveLoopId=archiveLoopId??"";archiveCaseId=archiveCaseId??"";archiveCompareId=archiveCompareId??"";
            if(!data.Cases.Any(c=>c.LoopId==archiveLoopId))archiveLoopId=data.Cases.FirstOrDefault()?.LoopId??"";
            if(!data.Cases.Any(c=>c.Id==archiveCaseId&&c.LoopId==archiveLoopId)){archiveCaseId=data.Cases.FirstOrDefault(c=>c.LoopId==archiveLoopId)?.Id??"";archiveRecordPage=0;}
            if(!data.Cases.Any(c=>c.Id==archiveCompareId))archiveCompareId="";
            int count=data.Cases.FirstOrDefault(c=>c.Id==archiveCaseId)?.Records.Count??0;
            archiveRecordPage=Math.Max(0,Math.Min(archiveRecordPage,Math.Max(0,(count-1)/ArchivePageSize)));
        }
        partial void CaptureArchiveSelection(PlayerUiSnapshot state)
        {
            NormalizeArchiveSelection(ReadArchiveView());state.ArchiveLoopId=archiveLoopId;state.ArchiveCaseId=archiveCaseId;
            state.ArchiveCompareId=archiveCompareId;state.ArchiveRecordPage=archiveRecordPage;
        }
        partial void RestoreArchiveSelection(PlayerUiSnapshot state)
        {
            archiveLoopId=state.ArchiveLoopId??"";archiveCaseId=state.ArchiveCaseId??"";archiveCompareId=state.ArchiveCompareId??"";
            archiveRecordPage=state.ArchiveRecordPage;NormalizeArchiveSelection(ReadArchiveView());
        }
        static string ArchiveNames(System.Collections.Generic.IReadOnlyList<string> names)=>names.Count==0?"없음":string.Join(" · ",names);
        static string ArchiveNumber(string id,string prefix,string suffix)
            =>id!=null&&id.StartsWith(prefix,StringComparison.Ordinal)&&int.TryParse(id.Substring(prefix.Length),out int number)?number+suffix:"지난 사건";
        static string ArchiveSummary(ArchiveCaseView entry)
            =>ArchiveNumber(entry.LoopId,"LOOP_","회차")+" · "+ArchiveNumber(entry.ChapterId,"CHAPTER_","번째 사건")+"\n"+(entry.IsCurrentLoop?"이번 회차에서 끝난 사건":"지난 회차에서 끝난 사건")
                +"\n\n표결로 지목한 사람  "+entry.JudgmentTarget+"\n판정 결과  "+(entry.Correct?"일치":"불일치")
                +"\n사건 피해  "+ArchiveNames(entry.ConfirmedDead)+"\n처형  "+ArchiveNames(entry.Executed)+"\n탈출  "+ArchiveNames(entry.Escaped)
                +"\n잔류  "+entry.Residual+"명\n평가  "+entry.Rank+" · "+entry.Score+"점\n받은 경험치  "+entry.ExperienceAwarded;
        static string ArchiveRecord(ArchiveRecordView record,int clockVersion)
            =>WorldTimeLabel.Format(record.Tick,clockVersion)+" · "+record.Heading+"\n"+record.Text+(record.Place.Length==0?"":"\n장소  "+record.Place)
                +"\n판결 후 공개된 전말"+(record.HasRecordedPath?"":"");

        void RenderArchive(ProductionScreenView view,ref string title,ref string body,ref string context,Action<string,Action> add)
        {
            var data=ReadArchiveView();NormalizeArchiveSelection(data);title="지난 사건 기록";
            view.Title.color=new Color(.459f,.427f,.502f);
            const string boundary="끝난 사건을 돌아보는 기록이에요.\n지난 회차 기록은 이번 사건의 증거로 쓸 수 없어요.";
            if(data.Cases.Count==0){body=Archive==null?"이 시험 장면에는 지난 사건 기록이 없어요.":"아직 돌아볼 사건이 없어요.\n\n사건을 끝내고 다음 챕터로 넘어가면 전말을 다시 볼 수 있어요.";context=boundary;return;}
            var entry=data.Cases.Single(c=>c.Id==archiveCaseId);var loops=data.Cases.Select(c=>c.LoopId).Distinct().ToArray();
            var cases=data.Cases.Where(c=>c.LoopId==archiveLoopId).ToArray();int loopIndex=Array.IndexOf(loops,archiveLoopId),caseIndex=Array.FindIndex(cases,c=>c.Id==archiveCaseId);
            int pages=Math.Max(1,(entry.Records.Count+ArchivePageSize-1)/ArchivePageSize);
            var content=new StringBuilder(ArchiveSummary(entry));content.Append("\n\n전말 ").Append(archiveRecordPage+1).Append(" / ").Append(pages).Append("쪽");
            if(entry.Records.Count==0)content.Append("\n이 사건에 남아 있는 전말 기록이 없어요.");
            foreach(var record in entry.Records.Skip(archiveRecordPage*ArchivePageSize).Take(ArchivePageSize))content.Append("\n\n").Append(ArchiveRecord(record,entry.ClockVersion));body=content.ToString();
            var compared=data.Cases.FirstOrDefault(c=>c.Id==archiveCompareId);
            context=boundary+"\n\n"+(compared==null?"비교할 사건에서 ‘비교에 고정’을 누른 뒤 다른 사건을 선택하세요.\n\n이전 역할이나 판정 결과는 현재 사건의 유죄 근거로 제출할 수 없습니다.":"비교 기준 · "+ArchiveSummary(compared)+"\n\n"+(compared.Id==entry.Id?"현재 사건을 비교 기준으로 고정했습니다. 다른 사건을 선택하세요.":"왼쪽은 선택 사건, 이쪽은 고정한 사건입니다.\n사건마다 확정된 결과와 평가를 비교합니다."));
            void Add(string label,Action action,bool enabled){int index=view.Actions.Count(b=>b.gameObject.activeSelf);add(label,action);if(index<view.Actions.Length)view.Actions[index].interactable=enabled;}
            void Loop(int delta){archiveLoopId=loops[loopIndex+delta];archiveCaseId="";archiveRecordPage=0;}
            void Case(int delta){archiveCaseId=cases[caseIndex+delta].Id;archiveRecordPage=0;}
            Add("이전 회차",()=>Loop(1),loopIndex<loops.Length-1);Add("다음 회차",()=>Loop(-1),loopIndex>0);
            Add("이전 사건",()=>Case(1),caseIndex<cases.Length-1);Add("다음 사건",()=>Case(-1),caseIndex>0);
            Add("이전 기록",()=>archiveRecordPage--,archiveRecordPage>0);Add("다음 기록",()=>archiveRecordPage++,archiveRecordPage<pages-1);
            Add(compared==null?"비교에 고정":"비교 해제",()=>archiveCompareId=compared==null?entry.Id:"",true);
        }
    }
}
