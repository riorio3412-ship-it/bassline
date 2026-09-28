using System;
using System.IO;
using System.Linq;
using BASSLINE.AuthoringData;
using BASSLINE.Core;
using BASSLINE.Knowledge;
using BASSLINE.Trial;
using BASSLINE.World.Mansion;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        const string ReturnLesson="LIFE_IG01_RETURN_MEANING";
        const string ReturnLessonReward="LIFE_IG01_RETURN_MEANING_V1";
        long learningCheckedRevision=-1,learningCheckedReturn=-1,learningRetryAt;
        KnownRecord PrivateNoteRecord()=>Knowledge.For("CH_01").Records().LastOrDefault(r=>r.Direct&&r.Source=="CH_01"&&r.SubjectId==CommonReturnStation.PrivateNoteId&&r.Predicate=="PrivateReturnNote"&&r.Value=="TaegyeomReminder");
        string ReadPrivateReturnNote()=>Knowledge.Observe("CH_01",new KnownRecord{Kind="Document",Source="CH_01",SubjectId=CommonReturnStation.PrivateNoteId,Predicate="PrivateReturnNote",Value="TaegyeomReminder",ProvenanceKey=CommonReturnStation.PrivateNoteId,Text=CommonReturnStation.PrivateNoteText,Position=P(returnStation.PrivateNote.position),PlaceId="R_WORK",FromTick=World.Tick,ToTick=World.Tick+1,Supports=new[]{"가까이에서 읽은 메모의 원문"},DoesNotEstablish=new[]{"누구에게 한 지시인지는 글만으로 확정할 수 없음"}},World.Tick);
        public string AskAboutPrivateNote(string actor)
        {
            var note=PrivateNoteRecord();
            if(World.Paused||actor!=PenOwner||!NearbyLiving(actor)||itemExchange.Handoff.Running||note==null)return "메모를 읽은 뒤 태겸에게 가까이 가서 물어보세요.";
            // Authored explanation of the owner's own reminder, never knowledge of an unseen placement.
            return BeginConversation(actor,"제가 챙길 순서를 적은 메모예요.\n빌려 간 펜은 제게 직접 주세요.\n다른 사람에게 맡기라는 뜻은 아니에요.","LoanPrivateNoteClarification:"+LoanPen,780);
        }
        LoanLearning ReturnLearningProof(ItemExchangeSnapshot exchange,MansionState world,KnowledgeLedger knowledge,ItemLoan loan,long tick)
        {
            if(loan==null||loan.ItemId!=LoanPen||loan.Lender!=PenOwner||loan.Status!="Returned"||loan.ReturnedTick>tick)return null;
            var records=knowledge.For("CH_01").Records().Where(r=>r.ReceivedTick<=tick).ToArray();
            var returned=records.LastOrDefault(r=>r.Direct&&r.Source=="CH_01"&&r.Predicate=="HandedObject"&&r.SubjectId=="CH_01"&&r.Value==LoanPen&&r.ReceivedTick==loan.ReturnedTick);
            if(returned==null||!world.Events.Any(e=>e.Type=="LoanReturned"&&e.Actor=="CH_01"&&e.Target==LoanPen&&e.Tick==loan.ReturnedTick))return null;
            var statements=(exchange.Statements??Array.Empty<LoanStatement>()).Where(s=>s.LoanId==loan.Id&&s.Tick<=tick).ToArray();
            var corrections=statements.Where(s=>s.Kind=="Withdraw"||s.Kind=="Explain").ToArray();
            if(statements.Where(s=>s.Kind=="Accuse").Any(a=>a.HeardBy.Any(listener=>!corrections.Any(c=>c.Tick>a.Tick&&c.HeardBy.Contains(listener)))))return null;
            bool HeardOwner(KnownRecord r){var root=knowledge.For(PenOwner).Find(r.RootId);return r.Source==PenOwner&&r.Predicate=="SaidStatement"&&r.ReceivedTick>=loan.StartedTick&&root!=null&&root.ReceivedTick==r.ReceivedTick&&root.ProvenanceKey==r.ProvenanceKey;}
            KnownRecord Said(string value)=>records.LastOrDefault(r=>HeardOwner(r)&&r.Value==value);
            KnownRecord Placed(string mode,string anchor)=>records.LastOrDefault(r=>r.Direct&&r.Source=="CH_01"&&r.SubjectId==LoanPen&&r.Predicate=="SurfaceObjectTransfer"&&r.Value==mode&&r.FromTick>=loan.StartedTick&&r.ReceivedTick<=loan.ReturnedTick&&world.Events.Any(e=>e.Type=="ObjectPlacedAt"&&e.Actor=="CH_01"&&e.Target==LoanPen&&e.Tick==r.ReceivedTick&&e.Detail==anchor));
            var note=records.LastOrDefault(r=>r.Direct&&r.Source=="CH_01"&&r.SubjectId==CommonReturnStation.PrivateNoteId&&r.Predicate=="PrivateReturnNote"&&r.Value=="TaegyeomReminder");
            var clarification=Said("LoanPrivateNoteClarification:"+LoanPen);
            var privatePlace=Placed("PlacePrivate",CommonReturnStation.BagFrontId);
            var instruction=Said("LoanReturnInstruction:"+LoanPen);
            var publicPlace=Placed("Place",CommonReturnStation.TrayId);
            var notReceived=records.LastOrDefault(r=>HeardOwner(r)&&r.Value=="LoanOwnerReceipt"&&r.ReceivedTick<loan.ReturnedTick);
            var explanation=statements.LastOrDefault(s=>s.Kind=="Explain"&&s.HeardBy.Contains(s.Recipient)&&ValidLoanEvidence(loan,s.EvidenceIds,knowledge,s.Tick));
            string route;string[] basis;
            if(note!=null&&privatePlace!=null&&clarification!=null&&note.ReceivedTick<=clarification.FromTick){route="PrivateNote";basis=new[]{note.Id,privatePlace.Id,clarification.Id};}
            else if(instruction!=null&&instruction.ReceivedTick<=loan.ReturnedTick){route="ClearInstruction";basis=new[]{instruction.Id};}
            else if(publicPlace!=null&&notReceived!=null){route="PublicReturn";basis=new[]{publicPlace.Id,notReceived.Id};}
            else if(publicPlace!=null&&explanation!=null){route="PublicExplanation";basis=new[]{publicPlace.Id,explanation.RecordId}.Concat(explanation.EvidenceIds).ToArray();}
            else return null;
            return new LoanLearning{LoanId=loan.Id,RewardId=ReturnLessonReward,Route=route,Tick=tick,Basis=basis.Concat(new[]{returned.Id}).Concat(corrections.Select(c=>c.RecordId)).Distinct().ToArray()};
        }
        void TryCompleteLoanLearning()
        {
            if(itemExchange.Learning!=null||itemExchange.Handoff.Running||World.Tick<learningRetryAt)return;
            var loan=itemExchange.Loans.LastOrDefault(l=>l.ItemId==LoanPen&&l.Status=="Returned");if(loan==null)return;
            long revision=Knowledge.For("CH_01").Revision;
            if(revision==learningCheckedRevision&&loan.ReturnedTick==learningCheckedReturn)return;
            learningCheckedRevision=revision;learningCheckedReturn=loan.ReturnedTick;
            var proof=ReturnLearningProof(itemExchange,World.Capture(),Knowledge,loan,World.Tick);if(proof==null)return;
            try{
                var profile=ReadProfile();var next=TrialEndgame.PrepareReturnLearning(profile);
                if(next.Revision!=profile.Revision)WriteProfile(next);
            }catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException||ex is ArgumentException){
                learningCheckedRevision=-1;learningRetryAt=World.Tick+600;
                message="펜은 돌려줬어요. 배운 점은 저장 공간을 확인한 뒤 다시 기록할게요.";return;
            }
            proof.RecordId=Knowledge.Observe("CH_01",new KnownRecord{Kind="Reflection",Source="CH_01",SubjectId=LoanPen,Predicate="ReturnMeaningLearned",Value=ReturnLesson,ProvenanceKey=ReturnLessonReward,Text="물건을 놓아둔 것과 주인이 돌려받은 것은 다르다.\n애매한 메모는 뜻을 물어보고, 빌린 물건은 받았는지 확인하자.",Position=World.Resident("CH_01").Position,PlaceId=PlaceOf(bodies["CH_01"].transform.position),FromTick=World.Tick,ToTick=World.Tick+1,Supports=new[]{"이번에 직접 겪고 확인한 반납 경험"},DoesNotEstablish=new[]{"보지 못한 물건의 위치나 다른 사람의 속마음은 알 수 없음"}},World.Tick);
            itemExchange.Learning=proof;message="태겸에게 펜을 돌려줬어요. 배운 점이 노트에 남았어요.";
        }
        void ValidateLoanLearning(ItemExchangeSnapshot exchange,MansionState world,KnowledgeLedger knowledge)
        {
            var receipt=exchange?.Learning;if(receipt==null)return;
            var loan=exchange.Loans.SingleOrDefault(l=>l.Id==receipt.LoanId);
            if(receipt.Tick<0||receipt.Tick>world.Tick||receipt.Basis==null)throw new InvalidDataException("배운 점의 기록 시각이 올바르지 않습니다.");
            var expected=ReturnLearningProof(exchange,world,knowledge,loan,receipt.Tick);var record=knowledge.For("CH_01").Find(receipt.RecordId);
            if(expected==null||receipt.RewardId!=ReturnLessonReward||receipt.Route!=expected.Route||!receipt.Basis.SequenceEqual(expected.Basis)||record==null||!record.Direct||record.Kind!="Reflection"||record.Source!="CH_01"||record.SubjectId!=LoanPen||record.Predicate!="ReturnMeaningLearned"||record.Value!=ReturnLesson||record.ProvenanceKey!=ReturnLessonReward||record.ReceivedTick!=receipt.Tick)throw new InvalidDataException("직접 확인한 경험과 배운 점의 근거가 다릅니다.");
        }
    }
}
