using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BASSLINE.Core;
using BASSLINE.Trial;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerArchivePort
    {
        public ArchiveSnapshot ReadArchive()=>ArchiveViewProjection.Create("CH_01",proceedings.CampaignId,Knowledge.LoopId,World.Chapter,
            proceedings.Archive,NameOf,PlaceLabel);
    }

    // Only completed, durable archive snapshots enter this adapter. No WorldFact, current Court/C, or B-query input.
    public static class ArchiveViewProjection
    {
        static int Number(string id,string prefix)
            =>id!=null&&id.StartsWith(prefix,StringComparison.Ordinal)&&int.TryParse(id.Substring(prefix.Length),NumberStyles.None,CultureInfo.InvariantCulture,out int n)&&n>0?n:0;

        public static ArchiveSnapshot Create(string owner,string campaign,string currentLoop,int currentChapter,
            IEnumerable<TrialEndgameSnapshot> archive,Func<string,string> actorLabel=null,Func<string,string> placeLabel=null)
        {
            int loop=Number(currentLoop,"LOOP_");
            if(string.IsNullOrWhiteSpace(owner)||string.IsNullOrWhiteSpace(campaign)||loop==0||currentChapter<1)
                return new ArchiveSnapshot(owner,currentLoop,Array.Empty<ArchiveCaseView>());
            string Person(string id)=>string.IsNullOrWhiteSpace(id)?"기록 없음":actorLabel?.Invoke(id)??id;
            var cases=new List<ArchiveCaseView>();var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var entry in archive??Array.Empty<TrialEndgameSnapshot>())
            {
                var plan=entry?.Plan;if(plan==null||plan.PlayerId!=owner||plan.CampaignId!=campaign||!plan.Applied||!plan.TransitionApplied
                    ||(entry.Phase!="LoopReady"&&entry.Phase!="NextChapterReady")||string.IsNullOrWhiteSpace(plan.CaseId)
                    ||entry.Evaluation==null||string.IsNullOrWhiteSpace(entry.RewardId))continue;
                int caseLoop=Number(plan.LoopId,"LOOP_"),chapter=Number(plan.ChapterId,"CHAPTER_");
                if(caseLoop==0||chapter==0||caseLoop>loop||(caseLoop==loop&&chapter>=currentChapter))continue;
                string id=plan.LoopId+"/"+plan.ChapterId+"/"+plan.CaseId;if(!seen.Add(id))continue;
                var closed=plan.ClosedCaseIds!=null&&plan.ClosedCaseIds.Length>0?plan.ClosedCaseIds:new[]{plan.CaseId};
                var records=(entry.Reveal??Array.Empty<RevealEvent>()).Where(e=>e!=null&&closed.Contains(e.CaseId)&&e.RevealApproved&&!e.Protected)
                    .OrderBy(e=>e.Tick).ThenBy(e=>e.Id,StringComparer.Ordinal)
                    .Select(e=>new ArchiveRecordView(e.Id,e.Heading,e.Text,string.IsNullOrEmpty(e.PlaceId)?"":placeLabel?.Invoke(e.PlaceId)??e.PlaceId,e.Tick,e.HasRecordedPath)).ToArray();
                cases.Add(new ArchiveCaseView(id,plan.LoopId,plan.ChapterId,plan.CaseId,Person(plan.Target),plan.Correct,entry.Evaluation.Rank,
                    entry.Evaluation.Score,entry.RewardAppliedExperience,plan.Residual,caseLoop==loop,
                    (plan.ConfirmedDead??Array.Empty<string>()).Select(Person),(plan.Executed??Array.Empty<string>()).Select(Person),
                    (plan.Escaped??Array.Empty<string>()).Select(Person),records,plan.ClockVersion));
            }
            return new ArchiveSnapshot(owner,currentLoop,cases.OrderByDescending(c=>Number(c.LoopId,"LOOP_")).ThenByDescending(c=>Number(c.ChapterId,"CHAPTER_")).ThenBy(c=>c.CaseId,StringComparer.Ordinal));
        }
    }
}
