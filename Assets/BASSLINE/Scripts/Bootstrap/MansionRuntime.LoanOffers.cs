using System;
using System.IO;
using System.Linq;
using BASSLINE.Core;
using BASSLINE.Knowledge;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        ItemLoanTerms[] loanOffers=Array.Empty<ItemLoanTerms>();
        void InitializeLoanOffers()
        {
            // One lender/item per conversation keeps the item menu small. More offers need an explicit picker.
            loanOffers=ObjectBodies.Select(o=>o.Loan!=null&&o.Loan.Complete?o.Loan:o.ObjectId==LoanPen?ItemLoanTerms.Pen():null).Where(t=>t!=null).ToArray();
            if(loanOffers.Select(t=>t.Lender).Distinct().Count()!=loanOffers.Length||loanOffers.Select(t=>t.ItemId).Distinct().Count()!=loanOffers.Length||
                loanOffers.Any(t=>t.Lender=="CH_01"||!bodies.ContainsKey(t.Lender)||!ObjectBodies.Any(o=>o.ObjectId==t.ItemId)))throw new InvalidOperationException("대여 인물과 물건 정의가 중복되거나 없습니다.");
            foreach(var body in ObjectBodies)if(body.Loan!=null&&!string.IsNullOrEmpty(body.Loan.ItemId)&&(!body.Loan.Complete||body.Loan.ItemId!=body.ObjectId))throw new InvalidOperationException("대여 대사 또는 물건 연결이 비어 있습니다: "+body.ObjectId);
        }
        ItemLoanTerms LoanTerms(string actor)=>loanOffers.FirstOrDefault(t=>t.Lender==actor);
        bool IsAuthoredLoan(string actor,string item)=>loanOffers.Any(t=>t.Lender==actor&&t.ItemId==item);
        bool HasAdditionalLoanContent=>ObjectBodies.Any(o=>o.ObjectId!=LoanPen&&o.Loan!=null&&o.Loan.Complete);
        BASSLINE.Save.MansionSessionSnapshot UpgradeAuthoredLoanContent(BASSLINE.Save.MansionSessionSnapshot s)
        {
            if(s?.World==null||(s.OptionalObjects&16777216)!=0||!HasAdditionalLoanContent)return s;
            foreach(var body in ObjectBodies.Where(o=>o.ObjectId!=LoanPen&&o.Loan!=null&&o.Loan.Complete)){
                if(s.World.Objects.Any(o=>o.Id==body.ObjectId))continue;
                var owner=s.World.Residents.FirstOrDefault(r=>r.Id==body.InitialOwner);
                bool held=owner!=null&&owner.Alive&&owner.Present&&owner.HeldObject=="";
                var item=new BASSLINE.World.Mansion.MansionObjectState{Id=body.ObjectId,Name=NameOf(body.ObjectId),Location=held?"Hand":"World",Owner=held?owner.Id:"",Position=held?owner.Position.Plus(new Point3(0,.95,0)):P(body.LoanRestPosition)};
                s.World.Objects=s.World.Objects.Concat(new[]{item}).ToArray();if(held)owner.HeldObject=item.Id;
                AppendEvent(s.World,"ContentObjectAdded","",item.Id,"AuthoredLoansV1; introduced now, no earlier loan or observation");
            }
            s.OptionalObjects|=16777216;return s;
        }
        // Only the owner's complete speech heard at its original time grants permission.
        // A relayed quotation or a later re-delivery cannot renew a loan offer.
        bool HeardLoanSpeech(KnownRecord r,string actor,string value,KnowledgeLedger ledger)
        {
            if(r==null||r.Source!=actor||r.Predicate!="SaidStatement"||r.Value!=value||r.ConversationWith!="CH_01")return false;
            var root=ledger.For(actor).Find(r.RootId);
            return root!=null&&root.Source==actor&&root.Kind=="Speech"&&root.Direct&&root.Predicate==r.Predicate&&root.Value==value&&
                root.ProvenanceKey==r.ProvenanceKey&&root.ReceivedTick==r.ReceivedTick&&r.Parents.Length==1&&r.Parents[0]==root.Id;
        }
        KnownRecord LoanOffer(ItemLoanTerms terms)
        {
            if(terms==null)return null;
            long lastReturn=itemExchange.Loans.Where(l=>l.ItemId==terms.ItemId&&l.Status=="Returned").Select(l=>l.ReturnedTick).DefaultIfEmpty(-1).Max();
            var spoken=Knowledge.For("CH_01").Records();
            long declined=spoken.Where(r=>HeardLoanSpeech(r,terms.Lender,"LoanOfferDeclined:"+terms.ItemId,Knowledge)).Select(r=>r.ReceivedTick).DefaultIfEmpty(-1).Max();
            return spoken.Where(r=>HeardLoanSpeech(r,terms.Lender,"LoanOffer:"+terms.ItemId,Knowledge)&&r.ReceivedTick>Math.Max(lastReturn,declined)&&World.Tick-r.ReceivedTick<=terms.PermissionTicks).OrderByDescending(r=>r.ReceivedTick).FirstOrDefault();
        }
        public string DeclineLoanOffer(string actor)
        {
            var terms=LoanTerms(actor);
            if(World.Paused||terms==null||!ReadItemExchangeChoices(actor).CanDecline||itemExchange.Handoff.Running)return "지금은 거절할 대여 제안이 없어요.";
            return BeginConversation(actor,terms.Declined,"LoanOfferDeclined:"+terms.ItemId,Math.Max(300,terms.Declined.Length*5));
        }
        void ValidateLoanReceipt(ItemLoan loan,BASSLINE.World.Mansion.MansionState world,KnowledgeLedger ledger)
        {
            if(!IsAuthoredLoan(loan.Lender,loan.ItemId)||!world.Objects.Any(o=>o.Id==loan.ItemId))throw new InvalidDataException("정의되지 않은 물건 대여입니다.");
            var offer=ledger.For(loan.Borrower).Find(loan.OfferRecordId);
            if(!HeardLoanSpeech(offer,loan.Lender,"LoanOffer:"+loan.ItemId,ledger))throw new InvalidDataException("직접 들은 대여 허락이 없습니다.");
            if(!world.Events.Any(e=>e.Type=="ObjectHandedOver"&&e.Actor==loan.Lender&&e.Target==loan.ItemId&&e.Detail==loan.Borrower&&e.Tick==loan.StartedTick)||
                loan.Status=="Returned"&&!world.Events.Any(e=>e.Type=="ObjectHandedOver"&&e.Actor==loan.Borrower&&e.Target==loan.ItemId&&e.Detail==loan.Lender&&e.Tick==loan.ReturnedTick))throw new InvalidDataException("대여 또는 반환의 실제 전달이 없습니다.");
        }
    }
}
