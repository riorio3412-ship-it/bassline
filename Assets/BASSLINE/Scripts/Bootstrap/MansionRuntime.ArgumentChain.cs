using System;
using System.Linq;
using BASSLINE.Core;

namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerArgumentChainPort
    {
        bool CanEditArgument=>!Observer&&court.Phase=="Debate"&&Reconstruction.Phase=="Editing";
        public ArgumentChainView ReadArgumentChain()
        {
            if(Observer||court.Phase=="NotStarted")return new ArgumentChainView();
            var view=court.ReadArgumentChain("CH_01");var history=court.Read("CH_01",false).History;
            foreach(var node in view.Nodes.Concat(view.Candidates)){
                var claim=history.First(h=>h.Speech.Claim?.Id==node.ClaimId).Speech.Claim;
                node.Text=NameOf(claim.OwnerId)+" · "+ReadableClaim(claim.Spans.Single(s=>s.Id==node.SpanId));
                var cited=node.ProofIds;node.ProofIds=Knowledge.For("CH_01").Records().Where(r=>cited.Contains(r.Id)||r.Parents.Any(cited.Contains)).Select(r=>r.Id).Distinct().ToArray();
            }
            return view;
        }
        string ArgumentMessage(string result)=>message=result=="Created"||result=="Selected"?"확인할 말을 골랐습니다.":result=="Linked"?"이 전제가 필요하다고 연결했습니다. 아직 입증한 것은 아닙니다.":result=="Removed"?"연결을 정리했습니다. 원래 진술은 남습니다.":result=="Queued"?"정리한 설명을 말합니다. 실제로 들은 사람에게 전달됩니다.":result=="Withdrawn"?"설명을 철회했습니다. 자료와 이전 기록은 남습니다.":result=="Disconnected"?"결론에 연결되지 않은 전제를 연결하거나 빼 주세요.":result=="Cycle"||result=="SelfDependency"?"자기 자신을 근거로 삼는 연결은 만들 수 없습니다.":result=="NodeLimit"?"한 설명에는 결론과 전제 다섯 개까지 정리할 수 있습니다.":result=="WaitForSpeech"?"진행 중인 설명을 먼저 마쳐 주세요.":"지금은 바꿀 수 없습니다.";
        public string BeginArgumentChain(string claim,string span)=>ArgumentMessage(CanEditArgument?court.BeginArgumentChain("CH_01",claim,span):"Unavailable");
        public string SelectArgumentNode(string key)=>ArgumentMessage(CanEditArgument?court.SelectArgumentNode("CH_01",key):"Unavailable");
        public string NextArgumentChain()=>ArgumentMessage(CanEditArgument?court.NextArgumentChain("CH_01"):"Unavailable");
        public string ToggleArgumentPremise(string claim,string span)=>ArgumentMessage(CanEditArgument?court.ToggleArgumentPremise("CH_01",claim,span):"Unavailable");
        public string RemoveArgumentNode()=>ArgumentMessage(CanEditArgument?court.RemoveArgumentNode("CH_01"):"Unavailable");
        public string ReviseArgumentChain()=>ArgumentMessage(CanEditArgument?court.ReviseArgumentChain("CH_01"):"Unavailable");
        public string WithdrawArgumentChain()=>ArgumentMessage(CanEditArgument?court.WithdrawArgumentChain("CH_01"):"Unavailable");
        public string PublishArgumentChain()=>ArgumentMessage(CanEditArgument?court.PublishArgumentChain("CH_01",(claim,span)=>{
            var original=court.Read("CH_01",false).History.First(h=>h.Speech.Claim?.Id==claim).Speech.Claim;
            return ReadableClaim(original.Spans.Single(s=>s.Id==span));
        }):"Unavailable");
        public string FocusArgumentNode()
        {
            if(!CanEditArgument)return ArgumentMessage("Unavailable");var view=ReadArgumentChain();var node=view.Nodes.FirstOrDefault(n=>n.Key==view.SelectedNode);
            return node==null?ArgumentMessage("Unavailable"):EnterTrialFocus(node.ClaimId,node.SpanId);
        }
    }
}
