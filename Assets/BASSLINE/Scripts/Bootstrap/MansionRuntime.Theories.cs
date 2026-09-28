using System;
using System.Linq;
using BASSLINE.Core;

namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerTheoryComparisonPort
    {
        public TheoryComparisonView ReadTheoryComparison()
        {
            if(Observer||court.Phase=="NotStarted")return new TheoryComparisonView();
            var view=court.ReadTheoryComparison("CH_01");var history=court.Read("CH_01",false).History;var own=Knowledge.For("CH_01");
            foreach(var node in view.Theories.SelectMany(t=>t.Premises).Concat(view.ContextCandidates)){
                var claim=history.First(h=>h.Speech.Claim?.Id==node.ClaimId).Speech.Claim;
                node.Text=NameOf(claim.OwnerId)+" · "+ReadableClaim(claim.Spans.Single(s=>s.Id==node.SpanId));
                var cited=node.ProofIds;node.ProofIds=own.Records().Where(r=>cited.Contains(r.Id)||r.Parents.Any(cited.Contains)).Select(r=>r.Id).Distinct().ToArray();
            }
            foreach(var t in view.Theories){
                t.Title=t.Premises.FirstOrDefault(n=>n.Key==t.TargetNode)?.Text??"원진술을 더 확인해야 하는 설명";
                t.Gaps=t.Gaps.Select(key=>t.Premises.FirstOrDefault(n=>n.Key==key)?.Text??key).ToArray();
                t.Counterexamples=t.Counterexamples.Select(key=>view.ContextCandidates.FirstOrDefault(n=>n.Key==key)?.Text??t.Premises.FirstOrDefault(n=>n.Key==key)?.Text??"수신한 반례의 원진술 확인 필요").ToArray();
                t.ConditionalSupporters=t.ConditionalSupporters.Select(NameOf).ToArray();t.DeferredBy=t.DeferredBy.Select(NameOf).ToArray();
            }
            return view;
        }
        string TheoryMessage(string result)=>message=result=="Selected"?"비교할 설명을 골랐습니다.":result=="Queued"?"선택한 설명에 대한 입장을 말합니다.":result=="AlreadyStated"?"이미 같은 입장을 밝혔습니다. 자료가 달라지면 다시 검토할 수 있습니다.":result=="WaitForSpeech"?"진행 중인 발언을 먼저 마쳐 주세요.":result=="Created"?"기존 설명을 남겨두고 수정본을 엽니다.":"실제로 들은 설명과 전제를 먼저 골라 주세요.";
        public string SwitchTheorySide()=>TheoryMessage(CanEditArgument?court.SwitchTheorySide("CH_01"):"Unavailable");
        public string NextComparedTheory()=>TheoryMessage(CanEditArgument?court.NextComparedTheory("CH_01"):"Unavailable");
        public string NextTheoryPremise()=>TheoryMessage(CanEditArgument?court.NextTheoryPremise("CH_01"):"Unavailable");
        public string FocusTheoryPremise()=>message=CanEditArgument?court.FocusTheoryPremise(Knowledge.For("CH_01")):"지금은 검토할 수 없습니다.";
        public string StateTheoryPosition(string position)
        {
            var view=court.ReadTheoryComparison("CH_01");return TheoryMessage(CanEditArgument?court.QueueTheoryPosition("CH_01",view.Focused,position):"Unavailable");
        }
        public string AttachTheoryContext(string claim,string span)
        {
            if(!CanEditArgument)return TheoryMessage("Unavailable");var view=court.ReadTheoryComparison("CH_01");
            var original=court.Read("CH_01",false).History.FirstOrDefault(h=>h.Speech.Claim?.Id==claim)?.Speech.Claim;var node=original?.Spans.FirstOrDefault(s=>s.Id==span);
            return TheoryMessage(node==null?"Unavailable":court.QueueTheoryPosition("CH_01",view.Focused,"Context",claim,span,ReadableClaim(node)));
        }
        public string ReviseComparedTheory()=>TheoryMessage(CanEditArgument?court.ReviseComparedTheory("CH_01"):"Unavailable");
        void AdvanceTheoryResponses()
        {
            var s=court.Capture();if(s.Phase!="Debate"||court.Focused||Reconstruction.Phase!="Editing"||s.CourtTick%360!=0||s.Pending.Any(p=>p.Id.StartsWith("THEORY_POSITION_",StringComparison.Ordinal))||s.Examinations.Any(e=>new[]{"QuestionQueued","AwaitingAnswer","AnswerQueued"}.Contains(e.Phase)))return;
            foreach(string actor in s.Participants.Where(a=>a!="CH_01").OrderBy(a=>a,StringComparer.Ordinal))foreach(var theory in court.ReadTheories(actor).Where(t=>t.Owner!=actor&&t.Lifecycle=="Published")){
                // A response uses only this listener's actual public receipts, never private ballots.
                string stance=theory.Premises.Length>0&&theory.Premises.All(n=>n.Review=="SupportedWithinScope")?"Conditional":new[]{"Collapsed","Contradicted","Weakened"}.Contains(theory.State)?"Defer":"";
                if(stance!=""&&court.QueueTheoryPosition(actor,theory.Id,stance)=="Queued")return;
            }
        }
    }
}
