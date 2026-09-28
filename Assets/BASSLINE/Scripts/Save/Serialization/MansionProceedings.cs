using System;
using BASSLINE.Core;
using BASSLINE.Trial;
using BASSLINE.World.Mansion;
using BASSLINE.Investigation;
namespace BASSLINE.Save
{
    [Serializable] public sealed class MansionStatementSeal
    {
        public int Loop,Chapter;public string ActorId,RecordId,RuleInstanceId;
    }
    [Serializable] public sealed class MansionProceedings
    {
        public ChapterRulePlan Rules;
        public MansionStatementSeal[] StatementSeals=Array.Empty<MansionStatementSeal>();
        public string CampaignId="",Phase="Living",PresenterNode="",PresenterDestination="";
        public Point3 PresenterPosition,PresenterQueuePosition;public string PresenterQueueDoor="";
        public string[] PresenterPath=Array.Empty<string>(),PresenterKnownLocked=Array.Empty<string>(),ImportedReceipts=Array.Empty<string>(),DeliveredSpeeches=Array.Empty<string>(),SpokenRecords=Array.Empty<string>();
        public int PresenterCursor,InspectionTicks,SpeechSequence,SubmissionSequence;
        public NpcCounterProgress[] NpcCounters=Array.Empty<NpcCounterProgress>();
        public ReconstructionSnapshot Reconstruction=new ReconstructionSnapshot();
        public long FirstAnnouncementTick=-1,ConveneAt=-1;
        public string InspectionCaseId="";
        public string[] PendingReportCases=Array.Empty<string>();
        public MansionPublicScheduleSnapshot PublicSchedule=new MansionPublicScheduleSnapshot();
        public uint VoteRandomState=71863;
        public TrialSnapshot Court=new TrialSnapshot();
        public TrialEndgameSnapshot Endgame=new TrialEndgameSnapshot();
        public TrialEndgameSnapshot[] Archive=Array.Empty<TrialEndgameSnapshot>();
    }
}
