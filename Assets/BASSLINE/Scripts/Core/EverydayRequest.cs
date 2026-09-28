using System;
namespace BASSLINE.Core
{
    [Serializable] public sealed class EverydayRequestDefinition
    {
        public string Id="",Revision="",Source="",Requester="",ItemId="",Title="";
        public string Offer="",Accepted="",Deferred="",Declined="",Cancelled="",Received="";
        public int RetryTicks=18000;
    }
    [Serializable] public sealed class EverydayRequestState
    {
        public string Id="",Revision="",Requester="",ItemId="",Status="Offered";
        public string OfferRecordId="",DecisionRecordId="",Decision="",TransferId="";
        public long OfferedTick,DecisionTick=-1,CompletedTick=-1,RetryAt,ClosedTick=-1;
        public EverydayRequestState Copy()=>(EverydayRequestState)MemberwiseClone();
    }
    public sealed class EverydayRequestView
    {
        public bool Available,CanAsk,CanAccept,CanDefer,CanDecline,CanCancel,CanDeliver,CanHearReceipt;
        public string Title="",Hint="",DeliverLabel="물건 건네기";
    }
    public interface IPlayerEverydayRequestPort
    {
        EverydayRequestView ReadEverydayRequest(string actor);
        string AskEverydayRequest(string actor);
        string AnswerEverydayRequest(string actor,string answer);
        string DeliverEverydayRequest(string actor);
    }
}
