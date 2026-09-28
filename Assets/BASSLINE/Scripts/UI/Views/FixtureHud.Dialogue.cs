using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.UI
{
    public sealed partial class FixtureHud
    {
        int dialogueMenu;
        string lastSpeechPhase="",lastHeardText="";
        void RefreshConversationView()
        {
            if(CurrentScreen!=3||!(Source is IPlayerConversationPlaybackPort playback))return;
            var speech=playback.ReadConversationPlayback();
            if(speech.Phase==lastSpeechPhase&&speech.HeardText==lastHeardText)return;
            lastSpeechPhase=speech.Phase;lastHeardText=speech.HeardText;Render();
        }
        void SyncDialoguePause(){port?.Pause("K_DIALOGUE_READ",stack.Contains(3)&&!((Source as IPlayerConversationPlaybackPort)?.ReadConversationPlayback().Speaking??false));}
        void AdvanceDialogue()
        {
            if(CurrentScreen!=3)return;
            if((Source as IPlayerConversationPlaybackPort)?.ReadConversationPlayback().Speaking==true){((IPlayerConversationPlaybackPort)Source).FinishListeningQuickly();return;}
            View(3).RevealDialogue();
        }
        void RenderDialogue(ProductionScreenView view,KnownRecord[] records,KnownRecord selected,Action<string,Action> button,ref string context)
        {
            view.DialogueAdvance=AdvanceDialogue;view.DialogueHasChoices=true;view.DialogueRecordPreview=false;
            var playback=(Source as IPlayerConversationPlaybackPort)?.ReadConversationPlayback();
            if(playback!=null&&playback.Speaking){
                bool speaking=playback.SpeakerId=="CH_01";string finish=speaking?"끝까지 말하기":"끝까지 듣기";
                context=(speaking?"말하고 있어요.":"듣고 있어요.")+"\n"+Controls.Label("Interact")+" / 대사 클릭\n"+finish;
                button(finish,()=>((IPlayerConversationPlaybackPort)Source).FinishListeningQuickly());
                button("잠깐 멈추기",()=>Open(38));button("그만 이야기하기",()=>{dialogueMenu=0;Back();});return;
            }
            context="천천히 골라도 괜찮아요.\n지금은 시간이 멈춰 있어요.";
            if(playback?.Phase=="Interrupted"){
                context=playback.EndReason+"\n들었던 내용은 노트에 남아요.";
                button("주위 살펴보기",Back);button("들었던 말 보기",()=>{recordCategory=2;Open(10);});return;
            }
            if(selectedTarget=="PRES_YUSTI")button("들었던 안내 보기",()=>{recordCategory=3;Open(10);});
            else if(dialogueMenu==6&&Source is IPlayerFamilyDisputePort family){
                button("지금 어떻게 생각해?",()=>status=DialogueCommand(()=>family.DiscussFamilyDispute(selectedTarget,"Ask")));
                button("내가 함께 들어 줄게",()=>status=DialogueCommand(()=>family.DiscussFamilyDispute(selectedTarget,"Mediate")));
                button("믿을 만한 사람에게 말해 봐",()=>status=DialogueCommand(()=>family.DiscussFamilyDispute(selectedTarget,"Disclose")));
                button("지금은 거리를 두자",()=>status=DialogueCommand(()=>family.DiscussFamilyDispute(selectedTarget,"Distance")));
                button("다른 이야기로",()=>dialogueMenu=0);
            }else if(dialogueMenu==0&&Source is IPlayerIncidentConversationPort incident&&incident.CanDiscussIncident(selectedTarget)){
                button("무슨 일이 있었어?",()=>status=DialogueCommand(()=>incident.AskAboutIncident(selectedTarget,false)));
                button("직접 본 거야?",()=>status=DialogueCommand(()=>incident.AskAboutIncident(selectedTarget,true)));
                button("내 입장 말하기",()=>dialogueMenu=5);
                button("다른 이야기",()=>dialogueMenu=1);
            }else if(dialogueMenu==5&&Source is IPlayerIncidentConversationPort position){
                context="상대에게 실제로 하는 말이에요.\n들은 사람은 이 말을 기억하지만, 말한 내용이 사실로 확정되지는 않아요.";
                button("내가 공격했어.",()=>{status=DialogueCommand(()=>position.StateIncidentPosition(selectedTarget,true));dialogueMenu=0;});
                button("내가 공격한 게 아니야.",()=>{status=DialogueCommand(()=>position.StateIncidentPosition(selectedTarget,false));dialogueMenu=0;});
                button("말하지 않고 돌아가기",()=>dialogueMenu=0);
            }else if(dialogueMenu==0){
                if(Source is IPlayerFamilyDisputePort familyTopic&&familyTopic.CanDiscussFamilyDispute(selectedTarget))button("두 사람의 이야기",()=>dialogueMenu=6);
                string appointment=KnownAppointmentSummary(selectedTarget);
                if(appointment!="")context+="\n\n"+appointment;
                if(Source is IPlayerAppointmentCardPort cardPort&&cardPort.HasWrittenAppointmentChange(selectedTarget))button("카드에 적은 약속 전하기",()=>status=DialogueCommand(()=>cardPort.TellChangedAppointment(selectedTarget)));
                else if(Source is IPlayerAppointmentConversationPort appointmentPort&&notebook.ReadNotebook().Appointments.Any(a=>a.Organizer=="CH_01"&&a.Invitee==selectedTarget&&a.State=="Proposed"&&a.StartTick>port.ReadPlayer().Tick))button("아까 말한 약속, 괜찮아?",()=>status=DialogueCommand(()=>appointmentPort.AskAboutAppointment(selectedTarget)));
                if(Source is IPlayerLoanDiscussionPort discussion&&discussion.ReadLoanDiscussion(selectedTarget).Available)button("펜이 사라진 일",()=>dialogueMenu=3);
                if(Source is IPlayerEverydayRequestPort requests){var request=requests.ReadEverydayRequest(selectedTarget);
                    if(request.CanDeliver)button(request.DeliverLabel,()=>CloseForAction(()=>status=requests.DeliverEverydayRequest(selectedTarget)));
                    else if(request.CanAccept||request.CanCancel||request.CanHearReceipt)button("부탁한 일 이야기",()=>dialogueMenu=4);}
                if(Source is IPlayerItemExchangePort loanPort){var loanChoice=loanPort.ReadItemExchangeChoices(selectedTarget);
                    if(loanChoice.CanReturn)button(loanChoice.ReturnLabel,()=>CloseForAction(()=>status=loanPort.ReturnBorrowedItem(selectedTarget)));
                    else if(loanChoice.Available&&loanChoice.HasOutstandingLoan)button(loanChoice.TopicLabel,()=>dialogueMenu=2);}
                if(Source is IPlayerSmallTalkPort small){
                    button("안부 묻기",()=>{status=DialogueCommand(()=>small.SmallTalk(selectedTarget,1));});
                    button("좋아하는 것 묻기",()=>{status=DialogueCommand(()=>small.SmallTalk(selectedTarget,0));});
                }
                button("다른 이야기",()=>{dialogueMenu=1;SelectFirst();});
            }else if(dialogueMenu==1){
                if(Source is IPlayerConversationPort conversation)button("최근에 본 일 묻기",()=>{status=DialogueCommand(()=>conversation.AskRecentObservation(selectedTarget));dialogueMenu=0;});
                button("만날 시간 정하기",()=>Open(4));
                if(Source is IPlayerResidentMeetingsPort meetings)button("누구 만나기로 했어?",()=>{status=DialogueCommand(()=>meetings.AskResidentPlans(selectedTarget));dialogueMenu=0;});
                button("내가 본 일 말하기",()=>{shareRecordId="";Open(5);});
                if(Source is IPlayerSmallTalkPort small)button("불편한 점 묻기",()=>{status=DialogueCommand(()=>small.SmallTalk(selectedTarget,2));dialogueMenu=0;});
                if(Source is IPlayerItemExchangePort exchange&&exchange.ReadItemExchangeChoices(selectedTarget).Available)button("물건 이야기",()=>dialogueMenu=2);
                if(Source is IPlayerEverydayRequestPort requests&&requests.ReadEverydayRequest(selectedTarget).Available)button("도와줄 일 있어?",()=>{dialogueMenu=4;var request=requests.ReadEverydayRequest(selectedTarget);if(request.CanAsk)status=DialogueCommand(()=>requests.AskEverydayRequest(selectedTarget));});
                button("처음 이야기로",()=>dialogueMenu=0);
            }else if(dialogueMenu==3&&Source is IPlayerLoanDiscussionPort discussion){
                var choices=discussion.ReadLoanDiscussion(selectedTarget);
                if(choices.CanExplain)button("확인한 내용을 설명하기",()=>status=DialogueCommand(()=>discussion.SpeakAboutLoan(selectedTarget,"Explain")));
                if(choices.CanWithdraw)button("내가 단정했어. 미안해.",()=>status=DialogueCommand(()=>discussion.SpeakAboutLoan(selectedTarget,"Withdraw")));
                if(Source is IPlayerItemExchangePort pen&&pen.ReadItemExchangeChoices(selectedTarget).CanAskMissing)button(selectedTarget=="CH_06"?"펜 돌려받았어?":"반납대에 있던 펜 봤어?",()=>status=DialogueCommand(()=>pen.AskAboutMissingPen(selectedTarget)));
                if(choices.CanAccuse)button("민서가 훔쳤다고 말하기",()=>status=DialogueCommand(()=>discussion.SpeakAboutLoan(selectedTarget,"Accuse")));
                button("다른 이야기로",()=>dialogueMenu=0);button("그만 이야기하기",()=>{dialogueMenu=0;Back();});
            }else if(dialogueMenu==4&&Source is IPlayerEverydayRequestPort requests){
                var request=requests.ReadEverydayRequest(selectedTarget);context=request.Title+"\n\n"+request.Hint;
                if(request.CanAsk)button("어떤 부탁이야?",()=>status=DialogueCommand(()=>requests.AskEverydayRequest(selectedTarget)));
                if(request.CanAccept)button("도와줄게",()=>status=DialogueCommand(()=>requests.AnswerEverydayRequest(selectedTarget,"Accept")));
                if(request.CanDefer)button("나중에 이야기하자",()=>status=DialogueCommand(()=>requests.AnswerEverydayRequest(selectedTarget,"Defer")));
                if(request.CanDecline)button("이번에는 어려울 것 같아",()=>status=DialogueCommand(()=>requests.AnswerEverydayRequest(selectedTarget,"Decline")));
                if(request.CanDeliver)button(request.DeliverLabel,()=>CloseForAction(()=>status=requests.DeliverEverydayRequest(selectedTarget)));
                if(request.CanCancel)button("부탁받은 일을 그만둘게",()=>status=DialogueCommand(()=>requests.AnswerEverydayRequest(selectedTarget,"Cancel")));
                if(request.CanHearReceipt)button("이야기 듣기",()=>status=DialogueCommand(()=>requests.AskEverydayRequest(selectedTarget)));
                button("다른 이야기로",()=>dialogueMenu=0);button("그만 이야기하기",()=>{dialogueMenu=0;Back();});
            }else if(Source is IPlayerItemExchangePort exchange){
                var choices=exchange.ReadItemExchangeChoices(selectedTarget);
                if(choices.Hint!="")context+="\n\n"+choices.Hint;
                if(choices.CanReceive)button(choices.ReceiveLabel,()=>CloseForAction(()=>status=exchange.ReceivePen(selectedTarget)));
                if(choices.CanReturn)button(choices.ReturnLabel,()=>CloseForAction(()=>status=exchange.ReturnBorrowedItem(selectedTarget)));
                if(choices.CanAskPrivateNote)button("이 메모, 반납 장소 얘기야?",()=>status=DialogueCommand(()=>exchange.AskAboutPrivateNote(selectedTarget)));
                if(choices.CanBorrow){
                    if(!choices.HasOutstandingLoan&&!choices.CanReceive&&!choices.CanDecline)button(choices.BorrowLabel,()=>status=DialogueCommand(()=>exchange.AskToBorrowPen(selectedTarget)));
                    button("어디에 돌려주면 돼?",()=>status=DialogueCommand(()=>exchange.AskWhereToReturn(selectedTarget)));
                }
                if(choices.CanDecline)button("이번에는 안 빌릴게",()=>{status=DialogueCommand(()=>exchange.DeclineLoanOffer(selectedTarget));dialogueMenu=0;});
                if(choices.CanAskMissing)button(selectedTarget=="CH_06"?"펜 돌려받았어?":"반납대에 있던 펜 봤어?",()=>status=DialogueCommand(()=>exchange.AskAboutMissingPen(selectedTarget)));
                button("다른 이야기로",()=>dialogueMenu=1);
                button("그만 이야기하기",()=>{dialogueMenu=0;Back();});
            }
            if(dialogueMenu==0){button("대화 다시 보기",()=>{recordCategory=2;Open(10);});button("그만 이야기하기",Back);}
        }
        string shareRecordId="";
        void RenderSharePicker(ProductionScreenView view,KnownRecord[] records,Action<string,Action> button,ref string title,ref string body,ref string context)
        {
            var items=records.Where(r=>PersonalRecordFilter.Includes(r,0)&&r.Predicate!="WelcomeRead"&&r.Source!=selectedTarget).ToArray();
            var chosen=items.FirstOrDefault(r=>r.Id==shareRecordId);
            title="어떤 이야기를 할까?";
            body=items.Length==0?"아직 전할 이야기가 없어요. 주변을 살펴보거나 다른 사람과 이야기해 보세요.":"";
            context=chosen==null?ActorLabel(selectedTarget)+"에게 할 이야기를 왼쪽에서 골라 주세요.\n\n내용을 확인하고 ‘이 내용을 말하기’를 누르면 상대에게 전해요.":ActorLabel(selectedTarget)+"에게 전할 내용\n\n“"+chosen.Text+"”\n\n"+(chosen.Direct?"내가 직접 확인한 내용":"이야기를 들려준 사람: "+ActorLabel(chosen.Source))+"\n"+Place(chosen.PlaceId)+" · "+TimeLabel(chosen.FromTick);
            if(chosen!=null)button("이 내용을 말하기",()=>{
                string result=DialogueCommand(()=>notebook.Share(selectedTarget,chosen.Id));
                status=result.StartsWith("K_REC_",StringComparison.Ordinal)?ActorLabel(selectedTarget)+"에게 이야기를 전했어요.":DisplayStatus(result);
                if(result.StartsWith("K_REC_",StringComparison.Ordinal)){Back();dialogueMenu=0;}
            });
            button("말하지 않고 돌아가기",Back);
            view.SetRecordRows(items.Select(r=>new RecordRow{Id=r.Id,Heading=TimeLabel(r.FromTick)+" · "+(r.Predicate=="SaidStatement"?ActorLabel(r.Source):Place(r.PlaceId)),Text=r.Text}).ToArray(),shareRecordId,id=>{shareRecordId=id;Render();});
        }
    }
}
