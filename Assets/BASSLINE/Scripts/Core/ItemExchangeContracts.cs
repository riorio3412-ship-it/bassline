using System;
using System.Linq;
namespace BASSLINE.Core
{
    [Serializable] public sealed class ItemLoan
    {
        public string Id="",ItemId="",Lender="",Borrower="",OfferRecordId="",Status="Borrowed";
        public long StartedTick,ReturnedTick=-1;
        public ItemLoan Copy()=>(ItemLoan)MemberwiseClone();
    }
    [Serializable] public sealed class ItemHandoff
    {
        public string Id="",Phase="Idle",ItemId="",Giver="",Receiver="",Purpose="",OfferRecordId="",Reason="";
        public string RequestId="";
        public long StartedTick;public int ElapsedTicks;public bool Committed;
        public Point3 Position,StartPosition;
        public int MotionVersion,ApproachTicks;
        public double PlayerFacing;
        public Point3 ContactPosition,ContactAxis,ReceiverStartPosition,GiverEndPosition,ReceiverEndPosition;
        public string ResidentId="",ResumePhase="",ResumeActivity="";public int ResumeActivityTicks;
        public string[] Witnesses=Array.Empty<string>();
        public bool Running=>Phase=="Approaching"||Phase=="Reaching"||Phase=="Receiving";
        public ItemHandoff Copy(){var h=(ItemHandoff)MemberwiseClone();h.Witnesses=(string[])Witnesses.Clone();return h;}
    }
    [Serializable] public sealed class ItemExchangeSnapshot
    {
        public long Sequence;public ItemHandoff Handoff=new ItemHandoff();public ItemLoan[] Loans=Array.Empty<ItemLoan>();
        public CommonReturnState CommonReturn=new CommonReturnState();
        public LoanStatement[] Statements=Array.Empty<LoanStatement>();
        public LoanLearning Learning;
        public EverydayRequestState[] Requests=Array.Empty<EverydayRequestState>();
        public TaskJournalSettings Journal=new TaskJournalSettings();
        public ItemExchangeSnapshot Copy()=>new ItemExchangeSnapshot{Sequence=Sequence,Handoff=Handoff.Copy(),Loans=Loans.Select(l=>l.Copy()).ToArray(),CommonReturn=(CommonReturn??new CommonReturnState()).Copy(),Statements=(Statements??Array.Empty<LoanStatement>()).Select(s=>s.Copy()).ToArray(),Learning=Learning?.Copy(),Journal=(Journal??new TaskJournalSettings()).Copy(),Requests=(Requests??Array.Empty<EverydayRequestState>()).Select(r=>r.Copy()).ToArray()};
    }
    [Serializable] public sealed class LoanLearning
    {
        public string LoanId="",RecordId="",RewardId="",Route="";public long Tick;public string[] Basis=Array.Empty<string>();
        public LoanLearning Copy(){var l=(LoanLearning)MemberwiseClone();l.Basis=(string[])Basis.Clone();return l;}
    }
    [Serializable] public sealed class LoanStatement
    {
        public string Id="",LoanId="",Kind="",Recipient="",RecordId="";public long Tick;
        public string[] HeardBy=Array.Empty<string>(),EvidenceIds=Array.Empty<string>();
        public LoanStatement Copy(){var s=(LoanStatement)MemberwiseClone();s.HeardBy=(string[])HeardBy.Clone();s.EvidenceIds=(string[])EvidenceIds.Clone();return s;}
    }
    public sealed class LoanDiscussionChoices {public bool Available,CanAccuse,CanWithdraw,CanExplain;}
    public interface IPlayerLoanDiscussionPort
    {
        LoanDiscussionChoices ReadLoanDiscussion(string actor);
        string SpeakAboutLoan(string actor,string topic);
    }
    [Serializable] public sealed class SurfaceTransfer
    {
        public string Id="",Actor="",Mode="",Phase="Idle",ResumePhase="",ResumeActivity="",Reason="";
        public long StartedTick;public int Ticks,ResumeTicks;public bool Committed;
        public Point3 StartGrip,StartItem,Position,Contact;
        public bool Running=>Phase=="Moving";
        public SurfaceTransfer Copy()=>(SurfaceTransfer)MemberwiseClone();
    }
    [Serializable] public sealed class CleanupEntry
    {
        public string Id="",ItemId="",Collector="",Text="";
        public long CollectedTick,StoredTick,WrittenTick;
        public CleanupEntry Copy()=>(CleanupEntry)MemberwiseClone();
    }
    [Serializable] public sealed class CommonReturnState
    {
        public long Sequence,NextVisitTick,CollectedTick=-1,StoredTick=-1;
        public string Task="Idle",ReceiptId="";public int WritingTicks;public bool DrawerOpen;
        public SurfaceTransfer Motion=new SurfaceTransfer();public CleanupEntry[] Entries=Array.Empty<CleanupEntry>();
        public CommonReturnState Copy(){var s=(CommonReturnState)MemberwiseClone();s.Motion=Motion.Copy();s.Entries=Entries.Select(e=>e.Copy()).ToArray();return s;}
    }
    public sealed class ItemExchangeChoices
    {
        public bool Available,CanReceive,CanReturn,CanBorrow,CanAskMissing,HasOutstandingLoan,CanAskPrivateNote,CanDecline;
        public string ReceiveLabel="",ReturnLabel="",BorrowLabel="",TopicLabel="물건 이야기",Hint="";
    }
    public sealed class ItemExchangeView {public bool Running;public string Text="",StateKey="";public float Progress;}
    public interface IPlayerItemExchangePort
    {
        ItemExchangeChoices ReadItemExchangeChoices(string actor);
        // Legacy method names kept for existing callers; terms select the actor's actual authored item.
        string AskToBorrowPen(string actor);
        string DeclineLoanOffer(string actor);
        string AskWhereToReturn(string actor);
        string AskAboutMissingPen(string actor);
        string AskAboutPrivateNote(string actor);
        string ReceivePen(string actor);
        string ReturnBorrowedItem(string actor);
        ItemExchangeView ReadItemExchange();
        void CancelItemExchange();
    }
}
