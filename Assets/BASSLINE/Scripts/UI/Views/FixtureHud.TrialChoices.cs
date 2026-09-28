using System;
using System.Linq;
using BASSLINE.Core;

namespace BASSLINE.UI
{
    public sealed partial class FixtureHud
    {
        bool choosingTrialRule;
        int trialRulePage;
        // Presentation order depends only on the player's chosen action. No case authority,
        // hidden claim data, evidence scoring, or resolver result enters this chooser.
        static readonly int[][] FirstRulePages={new[]{2,5,3,7,0},new[]{0,8,6,4,9},new[]{0,1,7,3,4}};
        static readonly string[] RuleQuestions={
            "같은 사람과 같은 시각을 확인한 기록일까?",
            "기록이 닿는 구역 밖까지 단정한 말일까?",
            "그 시각에 다른 곳에 있었다는 기록이 있을까?",
            "갖고 있었다는 말과 썼다는 말을 구분했을까?",
            "직접 본 일일까, 누군가에게 전해 들은 말일까?",
            "‘항상’ 그렇다는 말에 반대되는 사례가 있을까?",
            "행동부터 결과까지 이어지는 근거가 있을까?",
            "나중의 모습을 과거에도 그랬다고 말한 걸까?",
            "각자 확인한 기록일까, 같은 이야기가 돌았을까?",
            "그 말이 맞다고 가정하면 어디까지 알 수 있을까?"
        };
        void RenderRuleChoices(TrialClaimView claim,Action<string,Action> add,out string body,out string context)
        {
            int[] first=FirstRulePages[Math.Min(2,Math.Max(0,trialAction))];
            int[] choices=trialRulePage==0?first:Enumerable.Range(0,10).Except(first).ToArray();
            body=string.Join("\n\n",choices.Select(i=>RuleLabels[i]+"\n"+RuleQuestions[i]));
            var span=claim?.Spans.ElementAtOrDefault(spanIndex);
            context="지금 살펴보는 말\n"+(span?.Text??claim?.Text??"선택한 주장이 없어요.")+"\n\n고른 자료 "+selectedEvidence.Count+"개\n현재 비교 방법: "+RuleLabels[trialRule]+"\n\n방법을 고르면 자료 화면으로 돌아가요. 제시를 눌러야 선택한 근거를 전달해요.";
            foreach(int index in choices){int choice=index;add(RuleLabels[choice],()=>{trialRule=choice;choosingTrialRule=false;});}
            add(trialRulePage==0?"다른 방법 5개":"처음 방법 5개",()=>trialRulePage=1-trialRulePage);
            add("자료로 돌아가기",()=>choosingTrialRule=false);
        }
    }
}
