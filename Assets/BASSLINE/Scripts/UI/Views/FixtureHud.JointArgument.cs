using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.UI
{
    public sealed partial class FixtureHud
    {
        int jointChoiceIndex;
        IPlayerJointArgumentPort Joint=>Source as IPlayerJointArgumentPort;
        static string JointPartStatus(string state)=>state=="Draft"?"아직 요청하지 않음":state=="Agreed"?"이 범위에 동의함":state=="Refused"?"참여를 거절함":state=="Deferred"?"참여를 미룸":state=="Speaking"?"설명 중":state=="Spoke"?"설명을 들었음":state=="Unheard"?"답변 또는 설명을 끝까지 듣지 못함":state=="Skipped"?"자료를 다시 확인해야 함":state=="Stopped"||state=="Withdrawn"?"중단됨":state=="Withdrawing"?"참여 중단을 말하는 중":"동의를 구하는 중";
        bool RenderJointArgument(Action<string,Action> add,out string body,out string context)
        {
            body=context="";if(Joint==null)return false;var j=Joint.ReadJointArgument();
            bool editing=j.Phase=="None"||j.Phase=="Draft"||j.Phase=="Completed"||j.Phase=="Cancelled";
            jointChoiceIndex=Math.Max(0,Math.Min(jointChoiceIndex,Math.Max(0,j.Choices.Length-1)));var choice=j.Choices.ElementAtOrDefault(jointChoiceIndex);
            body="함께 설명할 순서\n\n"+(j.Parts.Length==0?"다른 참여자가 실제로 전해 준 자료를 골라 주세요.":string.Join("\n\n",j.Parts.Select((p,i)=>(i+1)+". "+p.Speaker+" · "+JointPartStatus(p.State)+"\n"+p.Text)));
            context="공개할 범위: "+(j.Disclosure=="ScopeOnly"?"시간 범위와 한계만":"선택한 자료의 내용과 한계")+"\n\n동의한 부분만 순서대로 설명합니다. 함께 설명해도 자료의 출처와 효력은 그대로입니다. 거절하거나 중단해도 수첩의 자료를 혼자 제시할 수 있습니다.";
            if(editing){
                context+="\n\n"+(choice==null?"함께 설명을 요청할 자료가 없습니다. 먼저 다른 사람에게 자료를 전달받아야 합니다.":"선택한 자료 · "+choice.Speaker+"\n"+choice.Text);
                if(choice!=null){if(j.Choices.Length>1){add("이전 자료",()=>jointChoiceIndex=(jointChoiceIndex+j.Choices.Length-1)%j.Choices.Length);add("다음 자료",()=>jointChoiceIndex=(jointChoiceIndex+1)%j.Choices.Length);}
                    add(j.Phase=="Draft"&&j.Parts.Any(p=>p.RecordId==choice.Id)?"목록에서 빼기":"함께 설명할 자료로",()=>status=Joint.ToggleJointProof(choice.Id));
                    if(j.Phase=="Draft"&&j.Parts.Length>1&&j.Parts.Any(p=>p.RecordId==choice.Id))add("이 자료를 먼저",()=>status=Joint.MoveJointProofFirst(choice.Id));
                }
                if(j.Phase=="Draft"){add("공개 범위 바꾸기",()=>status=Joint.ToggleJointDisclosure());if(j.Parts.Length>0)add("참여 의사 묻기",()=>status=Joint.RequestJointConsent());}
            }else{
                var c=Court.ReadTrial();body+="\n\n"+(string.IsNullOrEmpty(c.SpokenText)?c.History.LastOrDefault()??"":ActorLabel(c.SpeakerId)+"\n"+c.SpokenText);
                if(j.Phase=="Ready"&&j.Parts.Any(p=>p.State=="Agreed"))add("동의한 부분 설명하기",()=>status=Joint.StartJointArgument());
                if(j.Phase=="Speaking"&&c.Claims.Length>=3)add("앞선 주장 정리",()=>status=Court.RetireTrialClaim(c.Claims[0].Id));
                add("함께 설명 중단",()=>status=Joint.StopJointArgument());
            }
            add("논쟁으로",ReturnToDebate);return true;
        }
    }
}
