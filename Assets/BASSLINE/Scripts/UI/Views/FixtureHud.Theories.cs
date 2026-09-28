using System;
using System.Linq;
using BASSLINE.Core;

namespace BASSLINE.UI
{
    public sealed partial class FixtureHud
    {
        bool theoryMore;int theoryContextIndex;
        IPlayerTheoryComparisonPort Theories=>Source as IPlayerTheoryComparisonPort;
        static string TheoryStatus(string state)=>state=="Collapsed"?"필수 전제가 반박됨":state=="Contradicted"?"일부 참고 근거가 반박됨":state=="Weakened"?"범위를 좁혀야 함":state=="Strengthened"?"확인된 근거가 있음":state=="Unresolved"?"자료가 부족함":"아직 검토 중";
        string TheoryText(TheoryView theory,string selected)
        {
            if(theory==null)return "비교할 다른 설명이 아직 없습니다.\n\n공개된 진술을 연결해 다른 설명을 제안할 수 있습니다.";
            return (theory.Lifecycle=="Withdrawn"?"철회된 설명\n":"")+theory.Title+"\n\n"+TheoryStatus(theory.State)+" · "+theory.Revision+"차 설명\n\n필요한 전제\n"+string.Join("\n\n",theory.Premises.Select(n=>(n.Key==selected?"▶ ":"")+n.Text+"\n"+ArgumentState(n.State)+" · 연결된 자료 "+n.ProofIds.Length))+"\n\n더 확인할 부분\n"+(theory.Gaps.Length==0?"현재 연결한 전제 안에서는 미확인 부분이 없습니다. 전체 사건의 정답을 뜻하지 않습니다.":string.Join("\n",theory.Gaps))+"\n\n반박된 부분\n"+(theory.Counterexamples.Length==0?"현재 수신한 확실한 반례가 없습니다.":string.Join("\n",theory.Counterexamples))+"\n\n공개된 조건부 지지: "+(theory.ConditionalSupporters.Length==0?"없음":string.Join(", ",theory.ConditionalSupporters))+"\n판단을 미룬 사람: "+(theory.DeferredBy.Length==0?"없음":string.Join(", ",theory.DeferredBy));
        }
        bool RenderTheoryComparison(Action<string,Action> add,out string body,out string context)
        {
            body=context="";if(Theories==null)return false;var c=Theories.ReadTheoryComparison();
            var left=c.Theories.FirstOrDefault(t=>t.Id==c.Left);var right=c.Theories.FirstOrDefault(t=>t.Id==c.Right);var focus=c.Theories.FirstOrDefault(t=>t.Id==c.Focused);
            body="설명 A"+(c.Focused==c.Left?" · 선택됨":"")+"\n\n"+TheoryText(left,c.SelectedNode);
            context="설명 B"+(c.Focused==c.Right&&c.Right!=""?" · 선택됨":"")+"\n\n"+TheoryText(right,c.SelectedNode)+"\n\n"+(c.Mode=="TheoryClash"?"같은 대상·시간 범위에 대해 서로 다른 예측을 하고 있습니다.":"두 설명이 반드시 충돌하는 것은 아닙니다. 필요한 전제부터 확인하세요.")+"\n한 설명이 반박돼도 다른 설명이 자동으로 입증되지는 않습니다.";
            if(focus==null){add("설명 정리로",()=>Open(28));add("논쟁으로",ReturnToDebate);return true;}
            if(theoryMore){
                var choices=c.ContextCandidates.Where(n=>!focus.Premises.Any(p=>p.Key==n.Key)).ToArray();theoryContextIndex=Math.Max(0,Math.Min(theoryContextIndex,Math.Max(0,choices.Length-1)));
                if(choices.Length>0){var selected=choices[theoryContextIndex];context+="\n\n함께 볼 진술\n"+selected.Text;add("다음 참고 진술",()=>theoryContextIndex=(theoryContextIndex+1)%choices.Length);add("참고할 말로 제안",()=>{status=Theories.AttachTheoryContext(selected.ClaimId,selected.SpanId);theoryMore=false;ReturnToDebate();});}
                add("판단 미루기",()=>{status=Theories.StateTheoryPosition("Defer");theoryMore=false;ReturnToDebate();});add("내 지지 거두기",()=>{status=Theories.StateTheoryPosition("WithdrawSupport");theoryMore=false;ReturnToDebate();});
                if(focus.Owner=="CH_01")add("이 설명 고치기",()=>{status=Theories.ReviseComparedTheory();theoryMore=false;Open(28);});
                add("비교로 돌아가기",()=>theoryMore=false);
            }else{
                if(c.Theories.Length>1)add("다른 쪽 선택",()=>status=Theories.SwitchTheorySide());if(c.Theories.Length>2)add("선택한 설명 바꾸기",()=>status=Theories.NextComparedTheory());add("다음 전제",()=>status=Theories.NextTheoryPremise());
                add("이 전제 확인",()=>{status=Theories.FocusTheoryPremise();var trial=Court.ReadTrial();if(!trial.Focused)return;claimIndex=Array.FindIndex(trial.Claims,x=>x.Id==trial.FocusClaimId);spanIndex=Array.FindIndex(trial.Claims[claimIndex].Spans,x=>x.Id==trial.FocusSpanId);Open(24);});
                add("조건부로 받아들이기",()=>{status=Theories.StateTheoryPosition("Conditional");ReturnToDebate();});add("자료·입장 더 보기",()=>theoryMore=true);
            }
            add("논쟁으로",ReturnToDebate);return true;
        }
    }
}
