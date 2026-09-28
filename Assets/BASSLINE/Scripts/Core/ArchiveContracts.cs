using System;
using System.Collections.Generic;
using System.Linq;
namespace BASSLINE.Core
{
    // Player-only ArchiveMeta. These identifiers are never current Evidence or knowledge receipts.
    public interface IPlayerArchivePort { ArchiveSnapshot ReadArchive(); }

    public sealed class ArchiveRecordView
    {
        public string Id {get;} public string Heading {get;} public string Text {get;} public string Place {get;}
        public long Tick {get;} public bool HasRecordedPath {get;}
        public ArchiveRecordView(string id,string heading,string text,string place,long tick,bool hasRecordedPath)
        { Id=id??"";Heading=heading??"";Text=text??"";Place=place??"";Tick=tick;HasRecordedPath=hasRecordedPath; }
    }

    public sealed class ArchiveCaseView
    {
        public string Id {get;} public string LoopId {get;} public string ChapterId {get;} public string CaseId {get;}
        public string JudgmentTarget {get;} public bool Correct {get;} public string Rank {get;}
        public int Score {get;} public int ExperienceAwarded {get;} public int Residual {get;}
        public bool IsCurrentLoop {get;} public int ClockVersion {get;}
        public IReadOnlyList<string> ConfirmedDead {get;} public IReadOnlyList<string> Executed {get;} public IReadOnlyList<string> Escaped {get;}
        public IReadOnlyList<ArchiveRecordView> Records {get;}
        public ArchiveCaseView(string id,string loopId,string chapterId,string caseId,string judgmentTarget,bool correct,string rank,
            int score,int experienceAwarded,int residual,bool isCurrentLoop,IEnumerable<string> confirmedDead,IEnumerable<string> executed,
            IEnumerable<string> escaped,IEnumerable<ArchiveRecordView> records,int clockVersion=WorldTimeLabel.Legacy)
        {
            ClockVersion=clockVersion;Id=id;LoopId=loopId;ChapterId=chapterId;CaseId=caseId;JudgmentTarget=judgmentTarget;Correct=correct;Rank=rank??"";
            Score=score;ExperienceAwarded=experienceAwarded;Residual=residual;IsCurrentLoop=isCurrentLoop;
            ConfirmedDead=Array.AsReadOnly(confirmedDead.ToArray());Executed=Array.AsReadOnly(executed.ToArray());Escaped=Array.AsReadOnly(escaped.ToArray());
            Records=Array.AsReadOnly(records.ToArray());
        }
    }

    public sealed class ArchiveSnapshot
    {
        public string Owner {get;} public string CurrentLoopId {get;} public string Scope=>"ArchiveMeta";
        public IReadOnlyList<ArchiveCaseView> Cases {get;}
        public ArchiveSnapshot(string owner,string currentLoopId,IEnumerable<ArchiveCaseView> cases)
        { Owner=owner??"";CurrentLoopId=currentLoopId??"";Cases=Array.AsReadOnly(cases.ToArray()); }
    }
}
