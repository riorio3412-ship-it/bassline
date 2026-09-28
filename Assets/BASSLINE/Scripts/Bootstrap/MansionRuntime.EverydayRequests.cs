using System;
using System.IO;
using System.Linq;
using BASSLINE.AuthoringData;
using BASSLINE.Core;
using BASSLINE.Knowledge;
using BASSLINE.World.Mansion;
using UnityEngine;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerEverydayRequestPort
    {
        EverydayRequestDefinition[] everydayRequests=Array.Empty<EverydayRequestDefinition>();
        EverydayRequestState[] Requests=>itemExchange.Requests??Array.Empty<EverydayRequestState>();
        void InitializeEverydayRequests()
        {
            everydayRequests=FindObjectsByType<MansionEverydayRequest>().Select(a=>a.Definition).ToArray();
            foreach(var d in everydayRequests){
                if(d==null||new[]{d.Id,d.Revision,d.Source,d.Requester,d.ItemId,d.Title,d.Offer,d.Accepted,d.Deferred,d.Declined,d.Cancelled,d.Received}.Any(string.IsNullOrWhiteSpace)||d.Id.Contains(":")||d.RetryTicks<60||d.Requester=="CH_01"||!bodies.ContainsKey(d.Requester)||!ObjectBodies.Any(o=>o.ObjectId==d.ItemId))throw new InvalidOperationException("생활 부탁의 인물·물건·대사가 비어 있습니다.");
            }
            if(everydayRequests.Select(d=>d.Id).Distinct().Count()!=everydayRequests.Length||everydayRequests.Select(d=>d.Requester).Distinct().Count()!=everydayRequests.Length)throw new InvalidOperationException("한 대화에 여러 부탁이 중복되어 있습니다.");
        }
        EverydayRequestDefinition RequestFor(string actor)=>everydayRequests.FirstOrDefault(d=>d.Requester==actor);
        EverydayRequestState RequestState(string id)=>Requests.FirstOrDefault(r=>r.Id==id);
        public EverydayRequestView ReadEverydayRequest(string actor)
        {
            var d=RequestFor(actor);if(d==null||!NearbyLiving(actor))return new EverydayRequestView();
            var r=RequestState(d.Id);bool retry=r==null||new[]{"Deferred","Declined","Cancelled"}.Contains(r.Status)&&World.Tick>=r.RetryAt;
            bool accepted=r?.Status=="Accepted",offered=r?.Status=="Offered";
            bool deliver=accepted&&World.Resident("CH_01").HeldObject==d.ItemId&&World.Resident(actor).HeldObject==""&&!itemExchange.Loans.Any(l=>l.ItemId==d.ItemId&&l.Status=="Borrowed");
            return new EverydayRequestView{Available=true,Title=d.Title,CanAsk=retry,CanAccept=offered,CanDefer=offered,CanDecline=offered,CanCancel=accepted,CanDeliver=deliver,CanHearReceipt=r?.Status=="Completed",
                DeliverLabel=KoreanText.AsObject(NameOf(d.ItemId))+" 건네기",
                Hint=accepted?(deliver?"물건을 직접 건네면 부탁을 마칠 수 있어요.":"부탁받은 물건을 손에 들고 돌아오세요. 상대의 손도 비어 있어야 해요.\n어려우면 그만두겠다고 말해도 괜찮아요."):offered?"도울지 천천히 골라 주세요. 거절하거나 나중으로 미뤄도 괜찮아요.":r?.Status=="Completed"?"직접 물건을 건네서 부탁을 마쳤어요.":retry?"어떤 부탁인지 먼저 들어 보세요.":"지금은 이 부탁을 다시 맡지 않아도 괜찮아요."};
        }
        public string AskEverydayRequest(string actor)
        {
            var d=RequestFor(actor);if(World.Paused||d==null||!NearbyLiving(actor))return "가까이에서 이야기해 주세요.";
            var r=RequestState(d.Id);
            if(r?.Status=="Completed")return BeginConversation(actor,d.Received,"EverydayThanks:"+d.Id,Math.Max(360,d.Received.Length*5));
            if(r?.Status=="Accepted")return BeginConversation(actor,"아직 부탁을 맡아 주고 있는 거지?\n무리하지 않아도 돼. 어려워지면 이야기해 줘.","EverydayReminder:"+d.Id,480);
            if(!ReadEverydayRequest(actor).CanAsk)return "이미 들은 부탁에서 답을 골라 주세요.";
            // The requester only expresses a wish; the offer supplies no unseen location or owner history.
            if(World.Resident(actor).HeldObject==d.ItemId)return BeginConversation(actor,"지금 필요한 물건은 내 손에 있어.\n마음 써 줘서 고마워.","EverydayNotNeeded:"+d.Id,360);
            return BeginConversation(actor,d.Offer,"EverydayOffer:"+d.Id,Math.Max(480,d.Offer.Length*5));
        }
        public string AnswerEverydayRequest(string actor,string answer)
        {
            var d=RequestFor(actor);var view=ReadEverydayRequest(actor);var r=d==null?null:RequestState(d.Id);
            bool allowed=answer=="Accept"?view.CanAccept:answer=="Defer"?view.CanDefer:answer=="Decline"?view.CanDecline:answer=="Cancel"&&view.CanCancel;
            if(World.Paused||r==null||!allowed||itemExchange.Handoff.Running)return "지금 답할 수 있는 부탁을 골라 주세요.";
            string line=answer=="Accept"?"알았어. 찾으면 직접 가져다줄게.":answer=="Defer"?"지금은 다른 일을 하고 있어. 나중에 다시 이야기하자.":answer=="Decline"?"미안하지만 이번에는 도와주기 어려울 것 같아.":"아까 부탁받은 일, 계속하기 어려울 것 같아. 그만둘게.";
            return BeginConversation("CH_01",line,"EverydayReply:"+answer+":"+d.Id,Math.Max(360,line.Length*5),recipient:actor,evidenceIds:new[]{r.OfferRecordId});
        }
        bool FinishEverydayStatement(ConversationPlaybackState speech,string root)
        {
            if(root==""||!speech.Value.StartsWith("Everyday",StringComparison.Ordinal))return false;
            var d=RequestFor(speech.SpeakerId=="CH_01"?speech.RecipientId:speech.SpeakerId);if(d==null)return false;
            if(speech.Value=="EverydayOffer:"+d.Id){
                var offer=Knowledge.For("CH_01").Records().LastOrDefault(r=>r.ProvenanceKey==speech.Id&&HeardLoanSpeech(r,d.Requester,speech.Value,Knowledge));
                if(offer==null)return false;
                var old=RequestState(d.Id);if(old!=null&&!new[]{"Deferred","Declined","Cancelled"}.Contains(old.Status))return false;
                itemExchange.Requests=Requests.Where(r=>r.Id!=d.Id).Concat(new[]{new EverydayRequestState{Id=d.Id,Revision=d.Revision,Requester=d.Requester,ItemId=d.ItemId,OfferRecordId=offer.Id,OfferedTick=World.Tick}}).ToArray();
                World.Emit("EverydayRequestOffered",d.Requester,d.Id,speech.Id);return false;
            }
            if(speech.SpeakerId!="CH_01"||!speech.Value.StartsWith("EverydayReply:",StringComparison.Ordinal)||!speech.Listeners.Any(l=>l.ActorId==d.Requester&&l.HeardCharacters==speech.PlannedText.Length))return false;
            var parts=speech.Value.Split(':');var request=RequestState(d.Id);
            if(parts.Length!=3||request==null||parts[2]!=d.Id||speech.EvidenceIds.Length!=1||speech.EvidenceIds[0]!=request.OfferRecordId)return false;
            string answer=parts[1];if(answer=="Cancel"?request.Status!="Accepted":request.Status!="Offered")return false;
            string status=answer=="Accept"?"Accepted":answer=="Defer"?"Deferred":answer=="Decline"?"Declined":answer=="Cancel"?"Cancelled":"";if(status=="")return false;
            request.Status=status;request.Decision=answer;request.DecisionRecordId=root;request.DecisionTick=World.Tick;request.RetryAt=answer=="Accept"?0:World.Tick+d.RetryTicks;
            World.Emit("EverydayRequestAnswered","CH_01",d.Id,answer);
            string response=answer=="Accept"?d.Accepted:answer=="Defer"?d.Deferred:answer=="Decline"?d.Declined:d.Cancelled;
            return BeginConversation(d.Requester,response,"EverydayAcknowledged:"+answer+":"+d.Id,Math.Max(360,response.Length*5))=="Dialogue";
        }
        public string DeliverEverydayRequest(string actor)
        {
            var d=RequestFor(actor);if(d==null||!ReadEverydayRequest(actor).CanDeliver)return "부탁받은 물건을 들고 가까이 가 주세요. 상대의 손도 비어 있어야 해요.";
            var request=RequestState(d.Id);
            return BeginItemHandoff(d.ItemId,"CH_01",actor,"Request",request.OfferRecordId,d.Id);
        }
        bool RequestHandoffAllowed(ItemHandoff h)=>h.Purpose!="Request"||Requests.Any(r=>r.Id==h.RequestId&&r.Requester==h.Receiver&&r.ItemId==h.ItemId&&r.OfferRecordId==h.OfferRecordId&&(h.Committed?r.Status=="Completed"&&r.TransferId==h.Id:r.Status=="Accepted"));
        void CompleteEverydayRequest(ItemHandoff h)
        {
            var r=RequestState(h.RequestId);r.Status="Completed";r.CompletedTick=World.Tick;r.TransferId=h.Id;
            World.Emit("EverydayRequestCompleted",h.Receiver,r.Id,h.Id);
            Social.Experience(h.Receiver,"CH_01","EverydayHelp",r.Id+"|"+r.OfferRecordId,World.Tick,1,1);
            Social.Experience("CH_01",h.Receiver,"EverydayHelp",r.Id+"|"+r.OfferRecordId,World.Tick,0,1);
            Knowledge.Observe(h.Receiver,new KnownRecord{Kind="Touch",Source=h.Receiver,SubjectId="CH_01",Predicate="RequestFulfilled",Value=r.Id,ProvenanceKey=h.Id+"_REQUEST",Text=NameOf("CH_01")+"에게 부탁했던 "+KoreanText.AsObject(NameOf(h.ItemId))+" 직접 받았다.",IdentityConfirmed=true,Position=h.Position,PlaceId=PlaceOf(bodies[h.Receiver].transform.position),FromTick=World.Tick,ToTick=World.Tick+1,Supports=new[]{"직접 요청하고 받은 이번 물건"},DoesNotEstablish=new[]{"물건을 찾은 경로나 보지 못한 이동은 알 수 없음"}},World.Tick);
        }
        void AdvanceEverydayRequests()
        {
            foreach(var r in Requests.Where(r=>r.Status=="Offered"||r.Status=="Accepted")){
                var actor=World.Resident(r.Requester);
                if(actor.Alive&&actor.Present)continue;
                r.Status="Interrupted";r.ClosedTick=World.Tick;World.Emit("EverydayRequestInterrupted",r.Requester,r.Id,"RequesterUnavailable");
                if(itemExchange.Handoff.Running&&itemExchange.Handoff.Purpose=="Request"&&itemExchange.Handoff.RequestId==r.Id)CancelItemExchange("물건을 건네던 동작을 이어갈 수 없어 멈췄어요.");
                // No knowledge receipt, remote notification, relationship penalty or item recovery.
            }
        }
        void ValidateEverydayRequests(ItemExchangeSnapshot exchange,MansionState world,KnowledgeLedger ledger)
        {
            var requests=exchange.Requests;if(requests==null)throw new InvalidDataException("생활 부탁 저장이 없습니다.");
            if(requests.Any(r=>r==null)||requests.Select(r=>r.Id).Distinct().Count()!=requests.Length)throw new InvalidDataException("생활 부탁 기록이 중복되거나 비었습니다.");
            foreach(var r in requests){
                var d=everydayRequests.FirstOrDefault(x=>x.Id==r.Id);var offer=ledger.For("CH_01").Find(r.OfferRecordId);
                if(d==null||r.Revision!=d.Revision||r.ItemId!=d.ItemId||r.Requester!=d.Requester||!new[]{"Offered","Accepted","Deferred","Declined","Cancelled","Completed","Interrupted"}.Contains(r.Status)||!HeardLoanSpeech(offer,d.Requester,"EverydayOffer:"+d.Id,ledger)||r.OfferedTick!=offer.ReceivedTick||r.OfferedTick>world.Tick||r.RetryAt<0)throw new InvalidDataException("부탁한 인물 또는 들은 원문이 다릅니다.");
                if(!world.Events.Any(e=>e.Type=="EverydayRequestOffered"&&e.Actor==r.Requester&&e.Target==r.Id&&e.Detail==offer.ProvenanceKey&&e.Tick==r.OfferedTick))throw new InvalidDataException("부탁을 실제로 들은 사건이 없습니다.");
                if(r.DecisionTick>=0){
                    var answer=ledger.For("CH_01").Find(r.DecisionRecordId);
                    if(!new[]{"Accept","Defer","Decline","Cancel"}.Contains(r.Decision)||answer==null||answer.Source!="CH_01"||answer.Kind!="Speech"||!answer.Direct||answer.Predicate!="SaidStatement"||answer.Value!="EverydayReply:"+r.Decision+":"+r.Id||answer.ConversationWith!=r.Requester||answer.ReceivedTick!=r.DecisionTick||r.DecisionTick<r.OfferedTick||r.DecisionTick>world.Tick||!ledger.For(r.Requester).Records().Any(k=>k.RootId==answer.Id&&k.Source=="CH_01"&&k.Predicate=="SaidStatement"&&k.ReceivedTick==r.DecisionTick)||!world.Events.Any(e=>e.Type=="EverydayRequestAnswered"&&e.Actor=="CH_01"&&e.Target==r.Id&&e.Detail==r.Decision&&e.Tick==r.DecisionTick))throw new InvalidDataException("상대가 실제로 들은 부탁 답변이 없습니다.");
                }else if(r.DecisionTick!=-1||r.DecisionRecordId!=""||r.Decision!=""||r.Status!="Offered"&&r.Status!="Interrupted")throw new InvalidDataException("부탁을 수락하기 전 상태가 잘못되었습니다.");
                if((r.Status=="Accepted"||r.Status=="Completed")&&r.Decision!="Accept"||r.Status=="Deferred"&&r.Decision!="Defer"||r.Status=="Declined"&&r.Decision!="Decline"||r.Status=="Cancelled"&&r.Decision!="Cancel"||r.Status=="Offered"&&r.DecisionTick!=-1)throw new InvalidDataException("부탁 상태와 답변이 다릅니다.");
                bool postponed=new[]{"Deferred","Declined","Cancelled"}.Contains(r.Status);
                if(r.RetryAt!=(postponed?r.DecisionTick+d.RetryTicks:0)||r.Decision=="Cancel"&&!world.Events.Any(e=>e.Type=="EverydayRequestAnswered"&&e.Actor=="CH_01"&&e.Target==r.Id&&e.Detail=="Accept"&&e.Tick>=r.OfferedTick&&e.Tick<r.DecisionTick))throw new InvalidDataException("부탁을 미루거나 그만둔 순서가 잘못되었습니다.");
                if(r.Status=="Completed"){
                    if(r.CompletedTick<r.DecisionTick||r.CompletedTick>world.Tick||string.IsNullOrEmpty(r.TransferId)||!world.Events.Any(e=>e.Type=="ObjectHandedOver"&&e.Actor=="CH_01"&&e.Target==r.ItemId&&e.Detail==r.Requester&&e.Tick==r.CompletedTick)||!world.Events.Any(e=>e.Type=="EverydayRequestCompleted"&&e.Actor==r.Requester&&e.Target==r.Id&&e.Detail==r.TransferId&&e.Tick==r.CompletedTick))throw new InvalidDataException("부탁한 물건을 실제로 받은 기록이 없습니다.");
                }else if(r.CompletedTick!=-1||r.TransferId!="")throw new InvalidDataException("끝나지 않은 부탁에 완료 기록이 있습니다.");
                if(r.Status=="Interrupted"?(r.ClosedTick<r.OfferedTick||r.ClosedTick>world.Tick||!world.Events.Any(e=>e.Type=="EverydayRequestInterrupted"&&e.Actor==r.Requester&&e.Target==r.Id&&e.Tick==r.ClosedTick)):r.ClosedTick!=-1)throw new InvalidDataException("부탁 중단 시각이 잘못되었습니다.");
            }
        }
        void ValidateEverydaySpeech(ConversationPlaybackState speech,ItemExchangeSnapshot exchange,KnowledgeLedger ledger)
        {
            if(speech.Phase=="Idle"||!speech.Value.StartsWith("EverydayReply:",StringComparison.Ordinal))return;
            var parts=speech.Value.Split(':');var r=parts.Length==3?(exchange.Requests??Array.Empty<EverydayRequestState>()).FirstOrDefault(x=>x.Id==parts[2]):null;
            if(r==null||speech.SpeakerId!="CH_01"||speech.RecipientId!=r.Requester||!new[]{"Accept","Defer","Decline","Cancel"}.Contains(parts[1])||speech.EvidenceIds==null||speech.EvidenceIds.Length!=1||speech.EvidenceIds[0]!=r.OfferRecordId||r.OfferedTick>speech.StartedTick||ledger.For("CH_01").Find(r.OfferRecordId)==null)throw new InvalidDataException("부탁 답변과 들은 요청이 맞지 않습니다.");
            if(speech.Phase=="Speaking"&&(parts[1]=="Cancel"?r.Status!="Accepted":r.Status!="Offered"))throw new InvalidDataException("이미 닫힌 부탁에 답하고 있습니다.");
        }
    }
}
