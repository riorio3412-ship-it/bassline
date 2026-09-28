using System;
using System.IO;
using System.Linq;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.World.Mansion;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerItemExchangePort
    {
        const string LoanPen="M_TAEGYEOM_PEN",PenOwner="CH_06";
        const int HandoffCommitTicks=90,HandoffEndTicks=120;
        ItemExchangeSnapshot itemExchange=new ItemExchangeSnapshot();
        BASSLINE.Save.MansionSessionSnapshot UpgradeLoanContent(BASSLINE.Save.MansionSessionSnapshot snapshot)
        {
            if(snapshot?.World==null||(snapshot.OptionalObjects&128)!=0)return snapshot;
            var definition=ObjectBodies.FirstOrDefault(o=>o.ObjectId==LoanPen);
            if(definition&&!snapshot.World.Objects.Any(o=>o.Id==LoanPen)){
                var owner=snapshot.World.Residents.FirstOrDefault(r=>r.Id==PenOwner);
                bool held=owner!=null&&owner.Alive&&owner.Present&&owner.HeldObject=="";
                var item=new MansionObjectState{Id=LoanPen,Name=NameOf(LoanPen),Location=held?"Hand":"World",Owner=held?PenOwner:"",Position=held?owner.Position.Plus(new Point3(0,.95,0)):P(definition.transform.position)};
                snapshot.World.Objects=snapshot.World.Objects.Concat(new[]{item}).ToArray();if(held)owner.HeldObject=LoanPen;
                AppendEvent(snapshot.World,"ContentObjectAdded","",LoanPen,"Version0.12; added at load, not a historical loan");
            }
            snapshot.OptionalObjects|=128;return snapshot;
        }
        ItemLoan OpenLoan(string actor)=>itemExchange.Loans.LastOrDefault(l=>l.Lender==actor&&l.Borrower=="CH_01"&&l.Status=="Borrowed");
        // Permission and the conversation menu survive a passer-by crossing the sight line.
        // Actual transfer still requires capsule approach, arm clearance and swept item contact.
        bool NearbyLiving(string actor)=>actor!="CH_01"&&bodies.ContainsKey(actor)&&World.Resident(actor).Alive&&World.Resident(actor).Present&&Vector3.Distance(bodies["CH_01"].transform.position,bodies[actor].transform.position)<=2.4f&&SpeechPathOpen("CH_01",actor);
        public ItemExchangeChoices ReadItemExchangeChoices(string actor)
        {
            var loan=OpenLoan(actor);var terms=LoanTerms(actor);var offer=LoanOffer(terms);bool near=NearbyLiving(actor);
            bool known=terms!=null&&(loan!=null||offer!=null||World.Resident(actor).HeldObject==terms.ItemId&&CanSee("CH_01",terms.ItemId));
            string hint=loan!=null?(World.Resident("CH_01").HeldObject==loan.ItemId?"상대의 손이 비어 있으면 직접 돌려줄 수 있어요.":"빌린 물건을 손에 들고 돌아오면 직접 돌려줄 수 있어요."):
                offer!=null?(World.Resident("CH_01").HeldObject!=""?"받으려면 먼저 손에 든 물건을 내려놓아 주세요.":World.Resident(actor).HeldObject!=terms.ItemId?"상대가 물건을 손에 들고 있을 때 받을 수 있어요. 나중에 다시 이야기해도 괜찮아요.":"빌리기로 했다면 ‘받기’를 눌러 주세요. 지금 받지 않아도 괜찮아요."):"필요한 물건은 먼저 빌려도 되는지 물어보세요.";
            return new ItemExchangeChoices{Available=near&&(known||actor==Collector),CanBorrow=near&&known,CanAskMissing=near&&(actor==Collector||actor==PenOwner&&itemExchange.Loans.Any(l=>l.ItemId==LoanPen)),HasOutstandingLoan=loan!=null,
                CanReceive=near&&terms!=null&&offer!=null&&World.Resident(actor).HeldObject==terms.ItemId&&World.Resident("CH_01").HeldObject==""&&loan==null,
                CanReturn=near&&loan!=null&&World.Resident("CH_01").HeldObject==loan.ItemId&&World.Resident(actor).HeldObject=="",
                CanAskPrivateNote=near&&actor==PenOwner&&PrivateNoteRecord()!=null,
                CanDecline=near&&loan==null&&offer!=null,
                ReceiveLabel=(terms?.ShortName??"물건")+" 받기",ReturnLabel="빌린 "+(terms?.ShortName??"물건")+" 돌려주기",BorrowLabel=terms?.RequestLabel??"빌려도 될까?",TopicLabel=loan!=null?"빌린 "+(terms?.ShortName??"물건")+" 이야기":"물건 이야기",Hint=hint};
        }
        public string AskToBorrowPen(string actor)
        {
            var terms=LoanTerms(actor);
            if(World.Paused||terms==null||!NearbyLiving(actor)||itemExchange.Handoff.Running)return "가까이에서 빌려도 되는지 물어보세요.";
            if(OpenLoan(actor)!=null)return BeginConversation(actor,terms.Reminder,"LoanReminder:"+terms.ItemId,Math.Max(360,terms.Reminder.Length*5));
            if(World.Resident(actor).HeldObject!=terms.ItemId)return BeginConversation(actor,terms.Unavailable,"LoanUnavailable:"+terms.ItemId,Math.Max(360,terms.Unavailable.Length*5));
            if(World.Resident("CH_01").HeldObject!="")return BeginConversation(actor,"네, 괜찮습니다. 먼저 손에 든 물건을 내려놓으시겠어요? 떨어뜨릴까 봐요.","LoanHandsFull",480);
            return BeginConversation(actor,terms.Offer,"LoanOffer:"+terms.ItemId,Math.Max(600,terms.Offer.Length*5));
        }
        public string AskWhereToReturn(string actor)
        {
            var terms=LoanTerms(actor);
            if(World.Paused||terms==null||!NearbyLiving(actor)||itemExchange.Handoff.Running)return "가까이에서 빌려준 사람에게 물어보세요.";
            return BeginConversation(actor,terms.ReturnInstruction,"LoanReturnInstruction:"+terms.ItemId,Math.Max(540,terms.ReturnInstruction.Length*5));
        }
        public string AskAboutMissingPen(string actor)
        {
            if(World.Paused||!NearbyLiving(actor))return "가까이에서 물어보세요.";
            if(actor==PenOwner){
                bool returned=Knowledge.For(actor).Records().Where(r=>r.Predicate=="HandedObject"&&r.Value==LoanPen).OrderByDescending(r=>r.ReceivedTick).FirstOrDefault()?.SubjectId=="CH_01";
                return BeginConversation(actor,returned?"직접 돌려주신 펜은 받았습니다. 고맙습니다.":"아직 직접 돌려받지는 못했습니다. 어디에 놓으셨나요? 다른 사람이 들고 있었다는 것만으로 가져갔다고 단정하지는 말아 주세요.","LoanOwnerReceipt",660);
            }
            if(actor!=Collector)return "이 사람에게 물어볼 이야기가 아니에요.";
            if(itemExchange.Loans.Any(l=>NeedsLoanCorrection(l.Id,actor)))return BeginConversation(actor,"아직 그 말 때문에 마음이 불편해요.\n지금은 대답하고 싶지 않아요.\n공용 정리함과 장부는 직접 보셔도 돼요.","CleanupDeclined",660);
            var known=Knowledge.For(actor).Records().Where(r=>r.SubjectId==LoanPen&&r.Predicate=="SurfaceObjectTransfer"&&(r.Value=="Collect"||r.Value=="Store")).OrderByDescending(r=>r.ReceivedTick).FirstOrDefault();
            string text=known==null?"제가 직접 옮긴 펜은 아직 없어요. 공용 반납대 옆 안내부터 확인해 볼까요? 정리함과 장부는 누구나 볼 수 있어요.":known.Value=="Collect"?"공용 반납대에서 푸른 펜을 집었어요. 지금 정리함으로 옮기는 중이에요. 주인에게 돌려드린 건 아니고요.":"반납대에 남아 있던 푸른 펜을 공용 정리함에 넣었어요. 뚜껑 옆에 흠집이 있었죠. 장부와 원래 펜을 같이 확인해 주세요.";
            return BeginConversation(actor,text,"CleanupExplanation",720,"",known?.Id??"");
        }
        public string ReceivePen(string actor)
        {
            var choice=ReadItemExchangeChoices(actor);var terms=LoanTerms(actor);var offer=LoanOffer(terms);
            if(!choice.CanReceive||offer==null)return "먼저 빌려도 되는지 물어보고, 손을 비워 주세요.";
            return BeginItemHandoff(terms.ItemId,actor,"CH_01","Borrow",offer.Id);
        }
        public string ReturnBorrowedItem(string actor)
        {
            var loan=OpenLoan(actor);if(loan==null||!ReadItemExchangeChoices(actor).CanReturn)return "빌린 물건을 들고 빌려준 사람에게 가까이 가 주세요. 상대의 손도 비어 있어야 합니다.";
            return BeginItemHandoff(loan.ItemId,"CH_01",actor,"Return",loan.OfferRecordId);
        }
        string BeginItemHandoff(string item,string giver,string receiver,string purpose,string offer,string requestId="")
        {
            string resident=giver=="CH_01"?receiver:giver;
            if(World.Paused||toolPress.Running||ResidentToolBusy(giver)||ResidentToolBusy(receiver)||!World.CanAct(giver)||!World.CanAct(receiver)||RescueControls(giver)||RescueControls(receiver)||SurfaceRunning||itemExchange.Handoff.Running||!NearbyLiving(resident)||World.Resident(giver).HeldObject!=item||World.Resident(receiver).HeldObject!="")return "지금은 건넬 수 없어요.";
            EndConversation();StopWaiting("물건을 건네려고 멈췄어요.");move=default;running=false;
            // Clear residual walking velocity before reaching for the item.
            World.Pause("K_HANDOFF_PREPARE",false);
            var a=World.Resident(resident);long sequence=++itemExchange.Sequence;
            var h=new ItemHandoff{Id="HANDOFF_L"+World.Loop+"_"+sequence,Phase="Approaching",MotionVersion=1,PlayerFacing=World.Yaw,ItemId=item,Giver=giver,Receiver=receiver,Purpose=purpose,OfferRecordId=offer,StartedTick=World.Tick,
                Position=P(bodies[giver].RightHand.position),StartPosition=P(bodies[giver].RightHand.position),ResidentId=resident,ResumePhase=a.Phase,ResumeActivity=a.Activity,ResumeActivityTicks=a.ActivityTicks,RequestId=requestId};
            h.Witnesses=World.Residents.Where(r=>r.Alive&&r.Present&&r.Id!=giver&&r.Id!=receiver&&SeesHandoff(r.Id,h)).Select(r=>r.Id).ToArray();
            itemExchange.Handoff=h;a.Phase="Performing";a.Activity="HandOver";a.ActivityTicks=int.MaxValue;
            World.Emit("ItemHandoffStarted",giver,item,receiver);return "물건을 건네고 있어요.";
        }
        bool SeesHandoff(string observer,ItemHandoff h)=>SeesPassagePerson(observer,bodies[h.Giver],out bool giver)&&giver&&SeesPassagePerson(observer,bodies[h.Receiver],out bool receiver)&&receiver&&Visible(observer,V(h.Position));
        bool HandoffPathClear(ItemHandoff h,Vector3 next)
        {
            var body=ObjectBodies.Single(o=>o.ObjectId==h.ItemId);
            // Disabled held-item colliders have empty world bounds; use authored size for the sweep.
            float radius=body.Collider is BoxCollider box?Vector3.Scale(box.size*.5f,box.transform.lossyScale).magnitude:.3f;
            bool Blocks(Collider c)=>!c.transform.IsChildOf(body.transform)&&!c.transform.IsChildOf(bodies[h.Giver].transform)&&!c.transform.IsChildOf(bodies[h.Receiver].transform);
            int count=Physics.OverlapSphereNonAlloc(next,radius,overlap,~0,QueryTriggerInteraction.Ignore);
            if(count==overlap.Length)return false;for(int i=0;i<count;i++)if(Blocks(overlap[i]))return false;
            var delta=next-V(h.Position);if(delta.sqrMagnitude<1e-9f)return true;
            count=Physics.SphereCastNonAlloc(V(h.Position),radius,delta.normalized,rayHits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            if(count==rayHits.Length)return false;for(int i=0;i<count;i++)if(Blocks(rayHits[i].collider))return false;return true;
        }
        void AdvanceItemExchange()
        {
            var h=itemExchange.Handoff;if(!h.Running)return;
            var from=World.Resident(h.Giver);var to=World.Resident(h.Receiver);var resident=World.Resident(h.ResidentId);
            bool custody=h.Committed?to.HeldObject==h.ItemId&&World.Object(h.ItemId).Owner==h.Receiver:from.HeldObject==h.ItemId&&World.Object(h.ItemId).Owner==h.Giver&&to.HeldObject=="";
            if(!World.CanAct(h.Giver)||!World.CanAct(h.Receiver)||RescueControls(h.Giver)||RescueControls(h.Receiver)||resident.Activity!="HandOver"||!custody||!RequestHandoffAllowed(h)||Vector3.Distance(bodies[h.Giver].transform.position,bodies[h.Receiver].transform.position)>2.6f){CancelItemExchange("물건을 건네던 동작이 멈췄어요.");return;}
            if(h.Phase=="Approaching"){AdvanceHandoffApproach(h);h.Witnesses=h.Witnesses.Where(id=>SeesHandoff(id,h)).ToArray();return;}
            int elapsed=h.ElapsedTicks+1;float progress=Mathf.Clamp01((float)elapsed/HandoffCommitTicks);
            var next=Vector3.Lerp(V(h.StartPosition),bodies[h.Receiver].RightHand.position,Mathf.SmoothStep(0,1,progress));
            if(h.MotionVersion==1&&(!PoseHandoffArms(h,elapsed,out next)||!ArmSpaceClear(h))){CancelItemExchange("팔을 뻗을 공간이 부족해 멈췄어요. 조금 자리를 바꿔 주세요.");return;}
            if(!HandoffPathClear(h,next)){CancelItemExchange("사이에 물체가 있어 건네지 못했어요. 조금 자리를 바꿔 주세요.");return;}
            h.ElapsedTicks=elapsed;h.Position=P(next);World.Object(h.ItemId).Position=h.Position;
            h.Witnesses=h.Witnesses.Where(id=>SeesHandoff(id,h)).ToArray();
            if(!h.Committed&&elapsed>=HandoffCommitTicks){
                if(World.HandOver(h.ItemId,h.Giver,h.Receiver)!="Committed"){CancelItemExchange("상대의 손이 비어 있지 않아 멈췄어요.");return;}
                World.Object(h.ItemId).Position=h.Position;h.Committed=true;h.Phase="Receiving";CommitLoanAndReceipt(h);
            }
            if(elapsed>=HandoffEndTicks){h.Phase="Completed";ReleaseHandoffResident(h);h.Reason=h.Purpose=="Borrow"?KoreanText.AsObject(NameOf(h.ItemId))+" 받았어요. 다 쓰면 "+NameOf(h.Giver)+"에게 돌려주세요.":NameOf(h.Receiver)+"에게 "+KoreanText.AsObject(NameOf(h.ItemId))+(h.Purpose=="Request"?" 건네서 부탁을 마쳤어요.":" 돌려줬어요.");message=h.Reason;}
        }
        void CommitLoanAndReceipt(ItemHandoff h)
        {
            if(h.Purpose=="Borrow"){
                itemExchange.Loans=itemExchange.Loans.Concat(new[]{new ItemLoan{Id=h.Id,ItemId=h.ItemId,Lender=h.Giver,Borrower=h.Receiver,OfferRecordId=h.OfferRecordId,StartedTick=World.Tick}}).ToArray();
                World.Emit("LoanStarted",h.Giver,h.ItemId,h.Receiver);
            }else if(h.Purpose=="Return"){var loan=itemExchange.Loans.Last(l=>l.ItemId==h.ItemId&&l.Lender==h.Receiver&&l.Borrower==h.Giver&&l.Status=="Borrowed");loan.Status="Returned";loan.ReturnedTick=World.Tick;World.Emit("LoanReturned",h.Giver,h.ItemId,h.Receiver);}
            else if(h.Purpose=="Request")CompleteEverydayRequest(h);
            string text=NameOf(h.Giver)+" → "+NameOf(h.Receiver)+"\n"+KoreanText.AsObject(NameOf(h.ItemId))+" 직접 건네는 과정을 확인했다.";
            foreach(string observer in h.Witnesses.Concat(new[]{h.Giver,h.Receiver}).Distinct()){
                Knowledge.Observe(observer,new KnownRecord{Kind="Visual",Predicate="HandedObject",SubjectId=h.Giver,Value=h.ItemId,Source=observer,ProvenanceKey=h.Id+"_TRANSFER",Text=text,IdentityConfirmed=true,Position=h.Position,PlaceId=PlaceOf(bodies[h.Giver].transform.position),FromTick=h.StartedTick,ToTick=World.Tick+1,
                    Supports=new[]{"직접 확인한 물건 전달과 전달 상대"},DoesNotEstablish=new[]{"소지만으로 소유권·절도·숨은 의도를 확정하지 않음"}},World.Tick);
            }
            if(h.Purpose=="Return"){
                Social.Experience(h.Giver,h.Receiver,"ItemReturned",h.Id,World.Tick,1,1);Social.Experience(h.Receiver,h.Giver,"ItemReturned",h.Id,World.Tick,1,1);
            }
        }
        void ReleaseHandoffResident(ItemHandoff h)
        {
            if(!bodies.ContainsKey(h.ResidentId))return;var actor=World.Resident(h.ResidentId);
            if(actor.Alive&&actor.Present&&actor.Activity=="HandOver"){actor.Phase=h.ResumePhase;actor.Activity=h.ResumeActivity;actor.ActivityTicks=h.ResumeActivityTicks;}
        }
        public void CancelItemExchange()=>CancelItemExchange("물건을 건네던 동작을 멈췄어요.");
        void CancelItemExchange(string reason)
        {
            if(PlayerSurfaceRunning)CancelSurfaceTransfer(reason);
            var h=itemExchange.Handoff;if(!h.Running)return;h.Phase="Cancelled";h.Reason=reason+(h.Committed?" 물건은 이미 받은 사람이 가지고 있어요.":" 물건은 원래 사람이 가지고 있어요.");
            ReleaseHandoffResident(h);World.Emit("ItemHandoffInterrupted",h.Giver,h.ItemId,h.Committed?"AfterCommit":"BeforeCommit");message=h.Reason;
        }
        public ItemExchangeView ReadItemExchange()
        {
            var m=ReturnState.Motion;
            if(m.Actor=="CH_01"&&(m.Running||m.StartedTick>itemExchange.Handoff.StartedTick))return new ItemExchangeView{Running=m.Running,StateKey=m.Id+"|"+m.Phase,Progress=m.Ticks/120f,Text=m.Running?"펜을 옮기고 있어요.\n움직이거나 Esc를 누르면 멈춥니다.":m.Reason};
            var h=itemExchange.Handoff;string item=h.ItemId==""?"물건을":KoreanText.AsObject(NameOf(h.ItemId));
            return new ItemExchangeView{Running=h.Running,StateKey=h.Id+"|"+h.Phase,Progress=Mathf.Clamp01((float)h.ElapsedTicks/HandoffEndTicks),Text=h.Running?(h.Phase=="Approaching"?item+" 건네려고 가까이 다가오고 있어요.":h.Committed?(h.Receiver=="CH_01"?item+" 받았어요.":NameOf(h.Receiver)+"에게 "+item+(h.Purpose=="Request"?" 건네줬어요.":" 돌려줬어요.")):item+" 건네고 있어요.")+"\n움직이거나 Esc를 누르면 멈춥니다.":h.Reason};
        }
        void ValidateItemExchange(ItemExchangeSnapshot s,MansionState world,BASSLINE.Knowledge.KnowledgeLedger knowledge)
        {
            ValidateCommonReturn(s?.CommonReturn,world);
            if(s==null||s.Sequence<0||s.Handoff==null||s.Loans==null||s.Loans.Any(l=>l==null)||s.Loans.Select(l=>l.Id).Distinct().Count()!=s.Loans.Length)throw new InvalidDataException("물건 대여 기록이 올바르지 않습니다.");
            ValidateLoanDiscussion(s,world,knowledge);
            ValidateEverydayRequests(s,world,knowledge);
            ValidateTaskJournal(s);
            foreach(var loan in s.Loans){
                if(loan.Borrower!="CH_01"||loan.Id==null||!loan.Id.StartsWith("HANDOFF_L"+world.Loop+"_",StringComparison.Ordinal))throw new InvalidDataException("다른 회차의 대여 기록입니다.");
                var offer=knowledge.For(loan.Borrower).Find(loan.OfferRecordId);
                if(!IsAuthoredLoan(loan.Lender,loan.ItemId)||loan.Borrower!="CH_01"||!new[]{"Borrowed","Returned"}.Contains(loan.Status)||loan.StartedTick<0||loan.StartedTick>world.Tick||loan.Status=="Borrowed"&&loan.ReturnedTick!=-1||loan.Status=="Returned"&&(loan.ReturnedTick<loan.StartedTick||loan.ReturnedTick>world.Tick)||offer==null||offer.Source!=loan.Lender||offer.Predicate!="SaidStatement"||offer.Value!="LoanOffer:"+loan.ItemId||offer.ReceivedTick>loan.StartedTick)throw new InvalidDataException("대여 원문 또는 반환 시각이 올바르지 않습니다.");
                ValidateLoanReceipt(loan,world,knowledge);
            }
            foreach(var group in s.Loans.GroupBy(l=>l.ItemId)){
                var ordered=group.OrderBy(l=>l.StartedTick).ToArray();
                for(int i=1;i<ordered.Length;i++)if(ordered[i-1].Status!="Returned"||ordered[i-1].ReturnedTick>=ordered[i].StartedTick||knowledge.For(ordered[i].Borrower).Find(ordered[i].OfferRecordId).ReceivedTick<=ordered[i-1].ReturnedTick)throw new InvalidDataException("같은 물건의 대여가 겹치거나 허락을 다시 받지 않았습니다.");
            }
            var h=s.Handoff;if(h.Phase=="Idle")return;
            string lender=h.Purpose=="Borrow"?h.Giver:h.Receiver;
            bool request=h.Purpose=="Request";bool authored=request?everydayRequests.Any(d=>d.Id==h.RequestId&&d.Requester==lender&&d.ItemId==h.ItemId):IsAuthoredLoan(lender,h.ItemId);
            if(!new[]{"Approaching","Reaching","Receiving","Completed","Cancelled"}.Contains(h.Phase)||!authored||!new[]{"Borrow","Return","Request"}.Contains(h.Purpose)||h.Giver!=(h.Purpose=="Borrow"?lender:"CH_01")||h.Receiver!=(h.Purpose=="Borrow"?"CH_01":lender)||h.ResidentId!=lender||h.StartedTick<0||h.StartedTick>world.Tick||h.ElapsedTicks<0||h.ElapsedTicks>HandoffEndTicks||h.StartedTick+h.ApproachTicks+h.ElapsedTicks>world.Tick||h.Committed!=(h.ElapsedTicks>=HandoffCommitTicks)||!h.Position.Finite()||!h.StartPosition.Finite()||h.Witnesses==null||h.Witnesses.Any(id=>!bodies.ContainsKey(id))||h.Witnesses.Distinct().Count()!=h.Witnesses.Length)throw new InvalidDataException("물건 전달 동작 상태가 올바르지 않습니다.");
            if(h.MotionVersion<0||h.MotionVersion>1||h.ApproachTicks<0||h.ApproachTicks>180||h.MotionVersion==0&&(h.ApproachTicks!=0||h.Phase=="Approaching")||h.Phase=="Approaching"&&(h.ElapsedTicks!=0||h.Committed)||!new Point3(h.PlayerFacing,0,0).Finite()||!h.ContactPosition.Finite()||!h.ContactAxis.Finite()||!h.ReceiverStartPosition.Finite()||!h.GiverEndPosition.Finite()||!h.ReceiverEndPosition.Finite())throw new InvalidDataException("물건 전달 자세가 올바르지 않습니다.");
            if(h.MotionVersion==1&&h.Phase!="Approaching"&&h.Phase!="Cancelled"&&(h.ApproachTicks==0||Math.Abs(h.ContactAxis.Distance(default)-1)>.001||h.ContactPosition.Distance(h.StartPosition)>1.5||h.ContactPosition.Distance(h.ReceiverEndPosition)>1.5))throw new InvalidDataException("물건을 건네는 손 위치가 올바르지 않습니다.");
            var permission=knowledge.For("CH_01").Find(h.OfferRecordId);
            if(s.Sequence<1||h.Id!="HANDOFF_L"+world.Loop+"_"+s.Sequence||!HeardLoanSpeech(permission,lender,request?"EverydayOffer:"+h.RequestId:"LoanOffer:"+h.ItemId,knowledge)||permission.ReceivedTick>h.StartedTick||h.Phase=="Reaching"&&h.Committed||h.Phase=="Receiving"&&!h.Committed||h.Phase=="Completed"&&h.ElapsedTicks!=HandoffEndTicks)throw new InvalidDataException("전달 동작의 순서 또는 허락 원문이 잘못되었습니다.");
            if(request&&(h.Running||h.Committed)&&!(s.Requests??Array.Empty<EverydayRequestState>()).Any(r=>r.Id==h.RequestId&&r.ItemId==h.ItemId&&r.Requester==h.Receiver&&r.OfferRecordId==h.OfferRecordId&&r.Decision=="Accept"&&r.DecisionTick<=h.StartedTick&&(h.Committed?r.Status=="Completed"&&r.TransferId==h.Id&&r.CompletedTick==h.StartedTick+h.ApproachTicks+HandoffCommitTicks:r.Status=="Accepted")))throw new InvalidDataException("실제로 수락한 부탁의 전달이 아닙니다.");
            if(h.Committed&&!world.Events.Any(e=>e.Type=="ObjectHandedOver"&&e.Actor==h.Giver&&e.Target==h.ItemId&&e.Detail==h.Receiver&&e.Tick==h.StartedTick+h.ApproachTicks+HandoffCommitTicks))throw new InvalidDataException("물건을 받은 실제 사건 기록이 없습니다.");
            if(h.Purpose=="Borrow"&&h.Committed&&!s.Loans.Any(l=>l.Id==h.Id&&l.StartedTick==h.StartedTick+h.ApproachTicks+HandoffCommitTicks)||h.Purpose=="Return"&&h.Running&&!s.Loans.Any(l=>l.ItemId==h.ItemId&&l.Borrower==h.Giver&&l.Lender==h.Receiver&&(h.Committed?l.Status=="Returned"&&l.ReturnedTick==h.StartedTick+h.ApproachTicks+HandoffCommitTicks:l.Status=="Borrowed")))throw new InvalidDataException("전달 동작에 해당하는 대여 또는 반환 기록이 없습니다.");
            if(h.Running){var holder=world.Residents.Single(a=>a.Id==(h.Committed?h.Receiver:h.Giver));var item=world.Objects.Single(o=>o.Id==h.ItemId);if(holder.HeldObject!=h.ItemId||item.Location!="Hand"||item.Owner!=holder.Id||world.Residents.Single(a=>a.Id==h.ResidentId).Activity!="HandOver")throw new InvalidDataException("물건 전달과 실제 소지 상태가 다릅니다.");}
        }
    }
}
