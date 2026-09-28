using System;
using System.IO;
using System.Linq;
using BASSLINE.Core;
using BASSLINE.Knowledge;
using BASSLINE.World.Mansion;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerLoanDiscussionPort
    {
        LoanStatement[] LoanStatements=>itemExchange.Statements??Array.Empty<LoanStatement>();
        bool NeedsLoanCorrection(string loan,string actor)=>LoanStatements.Any(s=>s.LoanId==loan&&s.Kind=="Accuse"&&s.HeardBy.Contains(actor))&&!LoanStatements.Any(s=>s.LoanId==loan&&(s.Kind=="Withdraw"||s.Kind=="Explain")&&s.HeardBy.Contains(actor));
        ItemLoan DiscussionLoan(string actor)=>itemExchange.Loans.LastOrDefault(l=>l.ItemId==LoanPen&&NeedsLoanCorrection(l.Id,actor))??itemExchange.Loans.LastOrDefault(l=>l.ItemId==LoanPen);
        KnownRecord[] LoanExplanationEvidence(ItemLoan loan)
        {
            if(loan==null||loan.ItemId!=LoanPen||!returnStation)return Array.Empty<KnownRecord>();
            var known=Knowledge.For("CH_01").Records().Where(r=>r.Direct&&r.ReceivedTick>=loan.StartedTick).ToArray();
            var book=known.LastOrDefault(r=>RelevantCleanupRecord(r,loan));
            var item=known.LastOrDefault(r=>r.SubjectId==LoanPen&&r.Predicate=="ObjectFeature"&&r.Value=="BluePenCapWear"&&r.Position.Distance(P(returnStation.DrawerPoint.position))<.5);
            return book!=null&&item!=null?new[]{book,item}:Array.Empty<KnownRecord>();
        }
        bool RelevantCleanupRecord(KnownRecord record,ItemLoan loan)
        {
            string prefix="StoredPen:"+LoanPen+":";
            return record!=null&&record.Direct&&record.SubjectId==BASSLINE.AuthoringData.CommonReturnStation.BookId&&record.Predicate=="CleanupRecord"&&record.Value!=null&&record.Value.StartsWith(prefix,StringComparison.Ordinal)&&long.TryParse(record.Value.Substring(prefix.Length),out var collected)&&collected>=loan.StartedTick&&collected<=record.FromTick;
        }
        bool ValidLoanEvidence(ItemLoan loan,string[] ids,KnowledgeLedger knowledge,long tick)
        {
            if(loan==null||loan.ItemId!=LoanPen||loan.Lender!=PenOwner||ids==null||ids.Length!=2||ids.Distinct().Count()!=2||!returnStation)return false;
            var records=ids.Select(id=>knowledge.For("CH_01").Find(id)).ToArray();
            return records.All(r=>r!=null&&r.Direct&&r.ReceivedTick>=loan.StartedTick&&r.ReceivedTick<=tick)&&records.Any(r=>RelevantCleanupRecord(r,loan))&&records.Any(r=>r.SubjectId==LoanPen&&r.Predicate=="ObjectFeature"&&r.Value=="BluePenCapWear"&&r.Position.Distance(P(returnStation.DrawerPoint.position))<.5);
        }
        public LoanDiscussionChoices ReadLoanDiscussion(string actor)
        {
            var loan=DiscussionLoan(actor);if(loan==null||!NearbyLiving(actor))return new LoanDiscussionChoices();
            bool relevant=actor==PenOwner||actor==Collector||NeedsLoanCorrection(loan.Id,actor);
            bool placed=Knowledge.For("CH_01").Records().Any(r=>r.Direct&&r.SubjectId==LoanPen&&r.Predicate=="SurfaceObjectTransfer"&&r.Value=="Place"&&r.FromTick>=loan.StartedTick);
            bool any=LoanStatements.Any(s=>s.LoanId==loan.Id);
            return new LoanDiscussionChoices{Available=relevant&&(placed||any),CanAccuse=relevant&&placed&&loan.Status=="Borrowed"&&!LoanStatements.Any(s=>s.LoanId==loan.Id&&s.Kind=="Accuse"),
                CanWithdraw=relevant&&NeedsLoanCorrection(loan.Id,actor),CanExplain=relevant&&LoanExplanationEvidence(loan).Length==2&&!LoanStatements.Any(s=>s.LoanId==loan.Id&&s.Kind=="Explain"&&s.HeardBy.Contains(actor))};
        }
        public string SpeakAboutLoan(string actor,string topic)
        {
            var choice=ReadLoanDiscussion(actor);var loan=DiscussionLoan(actor);
            if(World.Paused||!choice.Available||loan==null)return "가까이에서 이야기해 주세요.";
            bool allowed=topic=="Accuse"?choice.CanAccuse:topic=="Withdraw"?choice.CanWithdraw:topic=="Explain"&&choice.CanExplain;
            if(!allowed)return "지금 할 수 있는 이야기부터 골라 주세요.";
            var refs=topic=="Explain"?LoanExplanationEvidence(loan):Array.Empty<KnownRecord>();
            string text=topic=="Accuse"?"내가 공용 반납대에 놓은 태겸의 펜 말이야.\n민서가 그 펜을 훔쳤어.":topic=="Withdraw"?"민서가 훔쳤다고 단정한 건 내 잘못이야.\n확인하지 못한 걸 사실처럼 말했어. 미안해.":
                "장부엔 정리함에 옮겼다고 적혀 있었어.\n거기서 같은 흠집이 있는 펜도 봤고.\n"+(LoanStatements.Any(s=>s.LoanId==loan.Id&&s.Kind=="Accuse")?"훔쳤다는 말은 내가 잘못했어. 미안해.":"태겸이 돌려받은 건 아니었던 거야.");
            return BeginConversation("CH_01",text,"LoanDiscussion:"+topic+":"+loan.Id,Math.Max(360,text.Length*5),recipient:actor,evidenceIds:refs.Select(r=>r.Id).ToArray());
        }
        bool FinishLoanStatement(ConversationPlaybackState speech,string root)
        {
            if(!speech.Value.StartsWith("LoanDiscussion:",StringComparison.Ordinal)||root=="")return false;
            var parts=speech.Value.Split(':');if(parts.Length!=3)return false;
            string kind=parts[1],loan=parts[2];
            var heard=speech.Listeners.Where(l=>l.HeardCharacters==speech.PlannedText.Length).Select(l=>l.ActorId).ToArray();
            var statement=new LoanStatement{Id=speech.Id,LoanId=loan,Kind=kind,Recipient=speech.RecipientId,RecordId=root,Tick=World.Tick,HeardBy=heard,EvidenceIds=(string[])speech.EvidenceIds.Clone()};
            bool collectorNeedsCorrection=NeedsLoanCorrection(loan,Collector);
            itemExchange.Statements=LoanStatements.Concat(new[]{statement}).ToArray();
            World.Emit("LoanDiscussionSpoken","CH_01",loan,kind);
            // EvidenceIds are the speaker's supporting sources, not a transfer of whole documents.
            // Listeners receive only the statement actually spoken through FinishConversation.
            if(heard.Contains(Collector)){
                if(kind=="Accuse")Social.Experience(Collector,"CH_01","UnfoundedAccusation",loan,World.Tick,-2,-1);
                else if(collectorNeedsCorrection){Social.Experience(Collector,"CH_01","ClaimCorrected",loan,World.Tick,1,0);Social.Experience("CH_01",Collector,"ClaimCorrected",loan,World.Tick);}
            }
            // Only a listener who heard the entire statement can answer its complete meaning.
            if(!heard.Contains(speech.RecipientId))return false;
            string response;
            if(kind=="Accuse")response=speech.RecipientId==Collector?"훔쳤다고요? 그렇게 단정하지 말아 주세요.\n지금은 더 이야기하고 싶지 않아요.\n정리함과 장부는 직접 보셔도 돼요.":"제가 아직 돌려받지 못한 건 맞습니다.\n하지만 민서 씨가 훔쳤다고 할 수는 없어요.\n어디서 무엇을 직접 확인했는지부터 이야기해 주세요.";
            else if(kind=="Withdraw")response=speech.RecipientId==Collector?"바로잡아 줘서 고마워요.\n그래도 속상했던 일이 바로 없어지진 않아요.\n다음엔 먼저 물어봐 주세요.":"알겠습니다. 먼저 한 말과 지금 바로잡은 말은\n구분해서 기억하겠습니다.\n아직 모르는 부분은 함께 확인하죠.";
            else response=speech.RecipientId==Collector?"직접 확인해 줘서 고마워요. 공용 정리함에 보관했다는 것과 주인에게 돌려줬다는 건 다른 일이에요.":speech.RecipientId==PenOwner?"자세히 말해 주셔서 고맙습니다. 펜을 반납대에 놓으셨다는 말과 제가 받았다는 말이 달랐던 거군요.":"장부에서 읽은 것과 직접 본 것을 구분해 주니 이해가 돼. 처음 들었던 말도 바로잡을게.";
            return BeginConversation(speech.RecipientId,response,"LoanDiscussionReply:"+kind,Math.Max(360,response.Length*5))=="Dialogue";
        }
        void ValidateLoanDiscussion(ItemExchangeSnapshot snapshot,MansionState world,KnowledgeLedger knowledge)
        {
            var statements=snapshot.Statements??Array.Empty<LoanStatement>();
            if(statements.Any(s=>s==null)||statements.Select(s=>s.Id).Distinct().Count()!=statements.Length)throw new InvalidDataException("펜 대화 기록이 중복되거나 비어 있습니다.");
            foreach(var s in statements){
                var loan=snapshot.Loans.SingleOrDefault(l=>l.Id==s.LoanId);var record=knowledge.For("CH_01").Find(s.RecordId);
                if(loan==null||s.Tick<loan.StartedTick||s.Tick>world.Tick||!new[]{"Accuse","Withdraw","Explain"}.Contains(s.Kind)||!bodies.ContainsKey(s.Recipient)||s.Recipient=="CH_01"||s.HeardBy==null||s.EvidenceIds==null||s.HeardBy.Distinct().Count()!=s.HeardBy.Length||s.HeardBy.Any(a=>a=="CH_01"||!bodies.ContainsKey(a))||record==null||record.Source!="CH_01"||record.Predicate!="SaidStatement"||record.ProvenanceKey!=s.Id||record.Value!="LoanDiscussion:"+s.Kind+":"+s.LoanId||!world.Events.Any(e=>e.Type=="LoanDiscussionSpoken"&&e.Actor=="CH_01"&&e.Target==s.LoanId&&e.Detail==s.Kind&&e.Tick==s.Tick))throw new InvalidDataException("실제로 발화한 펜 대화가 아닙니다.");
                foreach(var listener in s.HeardBy)if(!knowledge.For(listener).Records().Any(r=>r.Source=="CH_01"&&r.Predicate=="SaidStatement"&&r.ProvenanceKey==s.Id))throw new InvalidDataException("펜 대화를 듣지 않은 사람입니다.");
                if(s.Kind=="Explain"?!ValidLoanEvidence(loan,s.EvidenceIds,knowledge,record.FromTick):s.EvidenceIds.Length!=0)throw new InvalidDataException("설명한 펜의 근거가 없습니다.");
            }
        }
        void ValidateLoanSpeech(ConversationPlaybackState s,ItemExchangeSnapshot snapshot,MansionState world,KnowledgeLedger knowledge)
        {
            if(s.Phase=="Idle"||s.SpeakerId!="CH_01"||s.Appointment!=null||!s.Value.StartsWith("LoanDiscussion:",StringComparison.Ordinal))return;
            var parts=s.Value.Split(':');var loan=parts.Length==3?snapshot.Loans.SingleOrDefault(l=>l.Id==parts[2]):null;
            if(loan==null||!new[]{"Accuse","Withdraw","Explain"}.Contains(parts[1])||s.EvidenceIds==null||s.StartedTick<loan.StartedTick)throw new InvalidDataException("펜에 관한 발화 상태가 올바르지 않습니다.");
            if(parts[1]=="Explain"?!ValidLoanEvidence(loan,s.EvidenceIds,knowledge,s.StartedTick):s.EvidenceIds.Length!=0)throw new InvalidDataException("발화 전에는 확인하지 않은 근거입니다.");
            if(s.Phase=="Speaking"&&(world.Residents.Single(r=>r.Id=="CH_01").Activity!="Talk"||world.Residents.Single(r=>r.Id==s.RecipientId).Activity!="Listen"))throw new InvalidDataException("발화와 대화 참가자의 행동 상태가 다릅니다.");
        }
    }
}
