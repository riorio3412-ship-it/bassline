using System;
using System.Linq;
using BASSLINE.Core;

namespace BASSLINE.UI
{
    public sealed partial class FixtureHud
    {
        int argumentCandidate;
        IPlayerArgumentChainPort Arguments=>Source as IPlayerArgumentChainPort;
        static string ArgumentState(string s)=>s=="NeedsReview"?"전제가 바뀌어 다시 확인 필요":s=="CorrectedBySpeaker"?"말한 사람이 정정함":s=="Unresolved"?"아직 확인되지 않음":PlayerUiText.Review(s);
        bool RenderArgumentChain(Action<string,Action> add,out string body,out string context)
        {
            body=context="";if(Arguments==null)return false;var c=Arguments.ReadArgumentChain();
            if(c.Nodes.Length==0){body="어떤 말에 어떤 전제가 필요한지 정리합니다.";context="논쟁 화면에서 확인할 주장을 고른 뒤 ‘설명 연결’을 눌러 주세요.";add("논쟁으로",ReturnToDebate);return true;}
            var node=c.Nodes.First(n=>n.Key==c.SelectedNode);var root=c.Nodes.First(n=>n.Key==c.TargetNode);
            body="확인할 설명\n"+root.Text+"\n\n"+(node.Key==root.Key?"결론":"선택한 전제")+"\n"+node.Text+"\n"+ArgumentState(node.State)+"\n\n필요한 전제\n"+(node.Requires.Length==0?"연결된 전제가 없습니다.":string.Join("\n\n",node.Requires.Select(k=>{var n=c.Nodes.First(x=>x.Key==k);return n.Text+"\n"+ArgumentState(n.State);})));
            context="설명 "+c.Revision+"차 · "+(c.Phase=="Draft"?"정리 중":c.Phase=="Queued"?"말하는 중":c.Phase=="Withdrawn"?"철회됨":"발언 기록에 남음")+"\n\n연결은 입증과 다릅니다. 전제가 흔들리면 그에 기대는 설명만 다시 확인합니다.\n실제로 들은 관련 반론: "+c.CounterIds.Length;
            if(c.Mode=="ArgumentChain")context+="\n여러 전제에 대한 반론을 함께 검토하고 있습니다.";
            add("다음 전제",()=>{int i=Array.FindIndex(c.Nodes,n=>n.Key==c.SelectedNode);status=Arguments.SelectArgumentNode(c.Nodes[(i+1)%c.Nodes.Length].Key);});
            if(c.Phase=="Draft"){
                var candidates=c.Candidates.Where(n=>n.Key!=node.Key).ToArray();argumentCandidate=Math.Max(0,Math.Min(argumentCandidate,Math.Max(0,candidates.Length-1)));
                if(candidates.Length>0){var choice=candidates[argumentCandidate];context+="\n\n연결할 진술 "+(argumentCandidate+1)+" / "+candidates.Length+"\n"+choice.Text;
                    add("이전 진술",()=>argumentCandidate=(argumentCandidate+candidates.Length-1)%candidates.Length);add("다음 진술",()=>argumentCandidate=(argumentCandidate+1)%candidates.Length);
                    add(node.Requires.Contains(choice.Key)?"이 연결 빼기":"필요한 전제로 연결",()=>status=Arguments.ToggleArgumentPremise(choice.ClaimId,choice.SpanId));}
                if(node.Key!=root.Key)add("이 전제 빼기",()=>status=Arguments.RemoveArgumentNode());
                else if(c.ChainCount>1)add("다른 설명 보기",()=>status=Arguments.NextArgumentChain());
                add("설명하기",()=>{status=Arguments.PublishArgumentChain();if(Arguments.ReadArgumentChain().Phase=="Queued")ReturnToDebate();});
            }else{
                if(Theories!=null)add("다른 설명과 비교",()=>{theoryMore=false;Open(26);});
                if(c.ChainCount>1)add("다른 설명 보기",()=>status=Arguments.NextArgumentChain());
                add("이 부분 확인",()=>{status=Arguments.FocusArgumentNode();var trial=Court.ReadTrial();if(!trial.Focused)return;claimIndex=Array.FindIndex(trial.Claims,x=>x.Id==node.ClaimId);spanIndex=Array.FindIndex(trial.Claims[claimIndex].Spans,x=>x.Id==node.SpanId);Open(24);});
                if(c.Phase=="Published"){add("설명 고치기",()=>status=Arguments.ReviseArgumentChain());add("내 설명 철회",()=>{status=Arguments.WithdrawArgumentChain();ReturnToDebate();});}
            }
            add("논쟁으로",ReturnToDebate);return true;
        }
    }
}
