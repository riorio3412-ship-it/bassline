using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
namespace BASSLINE.UI
{
    public sealed partial class FixtureHud
    {
        bool showAllTrialRecords,showTrialDetails;
        int claimIndex,spanIndex,evidenceIndex,witnessIndex,voteIndex,trialAction,trialRule;
        static readonly string[] RuleLabels={"대상·시간·범위","기록된 구역","동시 위치","물건 소지와 사용","전해 들은 말","예외가 있는지","행동과 결과","나중의 상태","서로 다른 근원","진술을 전제로"};
        readonly List<string> selectedEvidence=new List<string>();
        IPlayerTrialPort Court=>Source as IPlayerTrialPort;
        static readonly string[] TrialActions={"Rebut","Support","LimitScope","CheckSource","RequestTestimony"};
        static readonly string[] ActionLabels={"반박","뒷받침","범위 제한","출처 확인","증언 요청"};
        partial void CaptureTrialSelection(PlayerUiSnapshot state)
        {
            CaptureReconstructionSelection(state);var c=Court?.ReadTrial();var claim=c?.Claims.ElementAtOrDefault(claimIndex);state.SelectedClaimId=claim?.Id??"";state.SelectedSpanId=claim?.Spans.ElementAtOrDefault(spanIndex)?.Id??"";state.SelectedEvidence=selectedEvidence.ToArray();state.TrialAction=trialAction;state.TrialRule=trialRule;
        }
        partial void RestoreTrialSelection(PlayerUiSnapshot state)
        {
            RestoreReconstructionSelection(state);choosingTrialRule=false;var c=Court?.ReadTrial();claimIndex=Math.Max(0,Array.FindIndex(c?.Claims??Array.Empty<TrialClaimView>(),x=>x.Id==state.SelectedClaimId));spanIndex=Math.Max(0,Array.FindIndex(c?.Claims.ElementAtOrDefault(claimIndex)?.Spans??Array.Empty<TrialSpanView>(),x=>x.Id==state.SelectedSpanId));
            selectedEvidence.Clear();selectedEvidence.AddRange(state.SelectedEvidence??Array.Empty<string>());trialAction=Mathf.Clamp(state.TrialAction,0,4);trialRule=Mathf.Clamp(state.TrialRule,0,9);
        }
        string ActorLabel(string id){var target=FindObjectsByType<FixtureTarget>().FirstOrDefault(x=>x.StableId==id);return target?target.PublicName:id;}
        partial void CheckTrialScreen(int screen,ref bool allowed)
        {
            var c=Court?.ReadTrial();if(c==null||c.Phase=="NotStarted"||c.Phase=="Gathering")return;
            allowed=screen<=31?c.Phase=="Debate"||c.Phase=="Voting"||c.Phase=="Revote":c.TruthAvailable||c.Phase=="Verdict";
        }
        partial void SyncTrialPause()
        {
            if(Court==null)return;var c=Court.ReadTrial();if(c.Focused&&!stack.Any(s=>s==24||s==25||s==29))Court.CancelTrialFocus();
        }
        partial void TickTrial()
        {
            if(Court==null)return;TickReconstruction();var c=Court.ReadTrial();
            if(c.Phase!="NotStarted"&&c.Phase!="Gathering"&&c.Phase!="Completed"&&stack.Contains(3)){
                (Source as IPlayerConversationPlaybackPort)?.EndConversation();foreach(var view in views.Values)view.gameObject.SetActive(false);stack.Clear();ReconcileTokens();SyncCursor();
            }
            if(CurrentScreen==1&&c.Phase!="NotStarted"&&c.Phase!="Gathering"&&c.Phase!="Completed")Open(c.Phase=="Debate"?23:c.CanVote?31:c.Phase=="Verdict"?32:c.TruthAvailable?33:23);
            if(CurrentScreen==23&&Controls.Down("Focus"))OpenFocus(c);
        }
        void OpenFocus(PlayerTrialView c)
        {
            if(c.Claims.Length==0){status="완전히 발화되어 수신한 주장이 없습니다.";return;}
            var claim=c.Claims[Mathf.Clamp(claimIndex,0,c.Claims.Length-1)];if(claim.Spans.Length==0)return;
            string result=Court.EnterTrialFocus(claim.Id,claim.Spans[Mathf.Clamp(spanIndex,0,claim.Spans.Length-1)].Id);status=result;
            if(Court.ReadTrial().Focused)Open(24);
        }
        void ReturnToDebate()
        {
            choosingTrialRule=false;
            foreach(int screen in stack.Where(s=>s>=23&&s<=37).ToArray())View(screen).gameObject.SetActive(false);
            stack.RemoveAll(s=>s>=23&&s<=37);Open(23);
        }
        partial void RenderTrial(int screen,ProductionScreenView view,ref bool handled)
        {
            if(screen<23||screen>37||Court==null)return;handled=true;var c=Court.ReadTrial();
            foreach(var button in view.Actions){button.onClick.RemoveAllListeners();button.gameObject.SetActive(false);}int count=0;
            void Add(string label,Action action){if(count>=view.Actions.Length)return;int n=count++;view.Actions[n].gameObject.SetActive(true);view.ActionLabels[n].text=label;view.Actions[n].onClick.AddListener(()=>{action();audioFeedback.Select();Render();});}
            string title=ScreenDefinitions.Single(x=>x.ScreenId=="UI_"+screen.ToString("00")).Title,body="",context="";
            claimIndex=Mathf.Clamp(claimIndex,0,Math.Max(0,c.Claims.Length-1));var claim=c.Claims.Length==0?null:c.Claims[claimIndex];
            spanIndex=Mathf.Clamp(spanIndex,0,Math.Max(0,(claim?.Spans.Length??0)-1));
            var allEvidence=notebook.ReadNotebook().Records;var evidence=showAllTrialRecords?allEvidence.OrderByDescending(r=>r.ReceivedTick).ToArray():PlayerRecordPresentation.Compact(allEvidence);evidenceIndex=Mathf.Clamp(evidenceIndex,0,Math.Max(0,evidence.Length-1));var selected=evidence.Length==0?null:evidence[evidenceIndex];
            if(screen==27&&RenderJointArgument(Add,out body,out context)){
                title="함께 설명하기";
            }else if(screen==26&&RenderTheoryComparison(Add,out body,out context)){
                title="서로 다른 설명 비교";
            }else if(screen==28&&RenderArgumentChain(Add,out body,out context)){
                title="설명 연결";
            }else if(screen==23||screen==27||screen==28||screen==26){
                body=(string.IsNullOrEmpty(c.SpeakerId)?"공개 발언":ActorLabel(c.SpeakerId))+"\n\n"+c.SpokenText;
                if(body.EndsWith("\n\n",StringComparison.Ordinal))body+=c.History.LastOrDefault()??"아직 수신한 발언이 없습니다.";
                context=claim==null?"완료된 주장이 없습니다.":"주요 주장 "+(claimIndex+1)+" / "+c.Claims.Length+"\n"+ActorLabel(claim.SpeakerId)+"\n"+claim.Text+"\n\n"+string.Join("\n",claim.Spans.Select(s=>s.Text+" · "+PlayerUiText.Review(s.State)));
                if(claim!=null){Add("다음 주장",()=>{claimIndex=(claimIndex+1)%c.Claims.Length;spanIndex=0;});Add(Controls.Label("Focus")+" 포커스",()=>OpenFocus(c));Add("쟁점 정리",()=>status=Court.RetireTrialClaim(claim.Id));}
                if(claim!=null&&claim.Spans.Length>0&&Arguments!=null)Add("설명 연결",()=>{status=Arguments.BeginArgumentChain(claim.Id,claim.Spans[spanIndex].Id);Open(28);});
                Add("함께 설명하기",()=>Open(27));Add("최종 재구성",()=>Open(30));
                if(Arguments!=null&&Arguments.ReadArgumentChain().Nodes.Length>0)Add("연결한 설명 보기",()=>Open(28));
            }else if(screen==24){
                title="이 말에서 무엇이 걸릴까?";body=""+(claim?.Text??"주장을 선택하세요.");
                context=claim==null?"":string.Join("\n",claim.Spans.Select((s,i)=>(i==spanIndex?"▶ ":"  ")+s.Text+" · "+PlayerUiText.Review(s.State)));
                Add("이 말과 다른 단서가 있어",()=>{choosingTrialRule=false;trialAction=0;trialRule=2;Open(25);});Add("이 말을 뒷받침할 단서가 있어",()=>{choosingTrialRule=false;trialAction=1;trialRule=0;Open(25);});Add("그렇게까지 단정할 수 있을까?",()=>{choosingTrialRule=false;trialAction=2;trialRule=0;Open(25);});Add("단서가 어디서 왔는지 보기",()=>{choosingTrialRule=false;trialAction=3;Open(25);});Add("조금 더 물어보기",()=>{trialAction=4;Open(29);});
                Add("다른 부분 짚기",()=>{if(claim==null||claim.Spans.Length<2)return;Court.CancelTrialFocus();spanIndex=(spanIndex+1)%claim.Spans.Length;status=Court.EnterTrialFocus(claim.Id,claim.Spans[spanIndex].Id);});
                Add("취소",()=>{Court.CancelTrialFocus();ReturnToDebate();});
            }else if(screen==25&&choosingTrialRule){
                title="무엇을 따져볼까?";RenderRuleChoices(claim,Add,out body,out context);
            }else if(screen==25){
                title="어떤 단서를 보여줄까?";body=evidence.Length==0?"아직 확보한 자료가 없어요. 직접 살펴보거나 들은 내용만 사용할 수 있어요.":"";
                context=(claim==null?"":"확인할 말\n“"+claim.Text+"”\n\n")+(selected==null?"함께 볼 단서를 골라 주세요.":selected.Text)+"\n\n선택한 단서: "+selectedEvidence.Count+"개";
                if(showTrialDetails&&selected!=null)context+="\n\n알 수 있는 것\n"+string.Join("\n",selected.Supports)+"\n\n아직 모르는 것\n"+string.Join("\n",selected.DoesNotEstablish)+"\n출처: "+ActorLabel(selected.Source);
                Add(trialAction==3?"선택한 자료의 출처 확인":"이 단서로 이야기하기",()=>{
                    if(selectedEvidence.Count==0){status="자료를 선택하세요.";return;}
                    if(trialAction==3){var records=Court.CheckTrialSource(selectedEvidence.ToArray());status=string.Join("\n",records.Select(r=>r.Text+" / "+r.Source+" / 근원 "+r.RootId));}
                    else{status=Court.SubmitTrial(TrialActions[trialAction],"Auto",selectedEvidence.ToArray());if(!Court.ReadTrial().Focused)ReturnToDebate();}
                });
                Add(showTrialDetails?"설명 접기":"단서 자세히",()=>showTrialDetails=!showTrialDetails);
                Add(showAllTrialRecords?"간단히 보기":"이전 관측도 보기",()=>{showAllTrialRecords=!showAllTrialRecords;evidenceIndex=0;});
                if(selectedEvidence.Count>0)Add("선택 지우기",()=>selectedEvidence.Clear());
                Add("취소",Back);
            }else if(screen==29){
                var original=c.Claims.FirstOrDefault(x=>x.Id==c.FocusClaimId);string witness=original?.SpeakerId??"";
                title="조금 더 물어보기";body=ActorLabel(witness)+"\n\n“"+(original?.Text??"확인할 진술을 먼저 골라 주세요.")+"”";context="이 말에서 궁금한 부분을 골라 주세요. 답은 실제 대화로 이어집니다.";
                if(witness!=""&&witness!="CH_01")foreach(string question in WitnessQuestions.Kinds){string selectedQuestion=question;Add(WitnessQuestions.Label(question),()=>{status=Court.RequestTrialTestimony(witness,selectedQuestion);if(!Court.ReadTrial().Focused)ReturnToDebate();});}
                Add("돌아가기",Back);
            }else if(screen==30&&RenderReconstruction(view,Add,out body,out context)){
                title="사건의 흐름 정리";
            }else if(screen==30){
                body=string.Join("\n\n",c.Claims.Select(x=>x.Text+"\n"+string.Join("\n",x.Spans.Select(s=>s.Text+" · "+PlayerUiText.Review(s.State)))));context="확인된 범위와 남은 불확실성을 검토하세요. 불완전한 상태에서도 최종 판단으로 진행할 수 있습니다.";
                Add("논쟁으로",ReturnToDebate);Add("투표 진행",()=>{status=Court.OpenTrialVoting();if(Court.ReadTrial().CanVote)Open(31);});
            }else if(screen==31){
                voteIndex=Mathf.Clamp(voteIndex,0,Math.Max(0,c.VoteCandidates.Length-1));string choice=c.VoteCandidates.Length==0?"":c.VoteCandidates[voteIndex];body="투표 "+c.VoteRound+"차\n\n선택: "+ActorLabel(choice);context="공개 전 다른 인물의 표심은 보이지 않습니다.\n"+string.Join("\n",c.Tallies.Select(t=>ActorLabel(t.ActorId)+" · "+t.Count));
                Add("다음 후보",()=>voteIndex=(voteIndex+1)%Math.Max(1,c.VoteCandidates.Length));Add("투표 확정",()=>{status=Court.CastTrialVote(choice);});Add("결과 진행",()=>{status=Court.ContinueTrial();var next=Court.ReadTrial();if(next.Phase=="Verdict")Open(32);});
            }else{
                body=screen==32?c.Verdict:screen==33?string.Join("\n\n",c.Truth.Take(c.TruthCursor+1).Select(t=>t.Heading+"\n"+t.Text)):screen==34?"사건 평가\n\n"+c.Rank+" · "+c.Score:screen==35?"획득 경험치 "+c.ExperienceAwarded+"\n레벨 "+c.Level+" · 남은 경험치 "+c.Experience:screen==36?"정산\n\n잔류 인원 "+c.Residual+"\n"+PlayerUiText.Transition(c.Transition):"회차 결과\n"+PlayerUiText.Transition(c.Transition);
                context=DisplayStatus(c.Message);Add("계속",()=>{status=Court.ContinueTrial();var next=Court.ReadTrial();int target=next.Phase=="Truth"?33:next.Phase=="Evaluation"?34:next.Phase=="Growth"?35:next.Phase=="Settlement"?36:0;if(target>0&&target!=screen)Open(target);else if(next.Phase=="Completed"||next.Phase=="NotStarted"){foreach(var p in stack.ToArray())View(p).gameObject.SetActive(false);stack.Clear();ReconcileTokens();SyncCursor();}});
            }
            if(screen==25){view.Body.fontSize=(choosingTrialRule?23f:26f)*Controls.FontScale;view.Body.lineSpacing=choosingTrialRule?6:12;}
            Add("메뉴",()=>Open(38));bool bodyChanged=screen==25&&view.Body.text!=body;view.Show(title,body,context,DisplayStatus(status));
            if(screen==25&&!choosingTrialRule){
                var rows=evidence.Select(r=>new RecordRow{Id=r.Id,Heading=(selectedEvidence.Contains(r.Id)?"[선택]  ·  ":"")+(r.Direct?"직접 확인":"전해 들은 말")+"  ·  "+TimeLabel(r.FromTick),Text=r.Text.Length>140?r.Text.Substring(0,140)+"…":r.Text}).ToArray();
                view.SetRecordRows(rows,selected?.Id??"",id=>{
                    evidenceIndex=Math.Max(0,Array.FindIndex(evidence,r=>r.Id==id));
                    if(!selectedEvidence.Remove(id))selectedEvidence.Add(id);
                    audioFeedback.Select();Render();if(view.ContextScroll)view.ContextScroll.verticalNormalizedPosition=1;
                    if(UnityEngine.EventSystems.EventSystem.current&&evidenceIndex<view.RecordRowButtons.Length)
                        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(view.RecordRowButtons[evidenceIndex].gameObject);
                });
            }else if(screen!=30) view.SetRecordRows(Array.Empty<RecordRow>(),"",_=>{});
            Canvas.ForceUpdateCanvases();if(bodyChanged){if(view.BodyScroll)view.BodyScroll.verticalNormalizedPosition=1;if(view.ContextScroll)view.ContextScroll.verticalNormalizedPosition=1;}
        }
    }
}
