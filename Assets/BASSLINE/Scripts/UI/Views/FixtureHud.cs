using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using BASSLINE.Core;
using BASSLINE.AuthoringData;
namespace BASSLINE.UI
{
 public sealed partial class FixtureHud:MonoBehaviour
 {
  public MonoBehaviour Source;public Camera ViewCamera;public Transform PlayerFacing;public bool StartWithMenu;
  public UnityEngine.UI.Image InteractionBackdrop;
  public TMP_Text Clock,NoteText,InteractionHint,AmbientCaption;public GameObject NotePanel;public TMP_FontAsset KoreanFont;
  public string FontFamily="Malgun Gothic";public UIThemeDefinition Theme;public UIScreenDefinition[] ScreenDefinitions;
  public float MouseSensitivity=2;public bool InvertY;public PlayerControls Controls {get;private set;}
  IPlayerLifePort port;IPlayerNotebookPort notebook;
  readonly Dictionary<int,ProductionScreenView> views=new Dictionary<int,ProductionScreenView>();
  readonly List<int> stack=new List<int>();
  bool note,settings,contextPause,focusPause,ownsFont,runLatched,runningActive,titleMenu;int selectedRecord,invitationPlaceIndex;string selectedTarget="",status="",rebindAction="";float refreshAt,feedbackUntil;
  bool wasWaiting;string lastItemExchangeState="";
  IPlayerMotionPort motion;IPlayerPlacePort places;DialoguePortrait portrait;
  partial void CheckTrialScreen(int screen,ref bool allowed);
  partial void RenderTrial(int screen,ProductionScreenView view,ref bool handled);
  partial void TickTrial();
  partial void SyncTrialPause();
  partial void CaptureTrialSelection(PlayerUiSnapshot state);
  partial void RestoreTrialSelection(PlayerUiSnapshot state);
  partial void CaptureArchiveSelection(PlayerUiSnapshot state);
  partial void RestoreArchiveSelection(PlayerUiSnapshot state);
  public bool NoteOpen=>note;public int CurrentScreen=>stack.Count==0?1:stack[stack.Count-1];
  void Start(){
   port=Source as IPlayerLifePort;notebook=Source as IPlayerNotebookPort;if(port==null||notebook==null)throw new InvalidOperationException("Player read ports missing");
   Controls=PlayerControls.Load();motion=Source as IPlayerMotionPort;places=Source as IPlayerPlacePort;MouseSensitivity=Controls.Sensitivity;InvertY=Controls.InvertY;
   PlayerPresentationSettings.Apply(Controls,transform);audioFeedback=gameObject.AddComponent<PlayerAudioFeedback>();audioFeedback.Initialize(Controls);
   KoreanFont=TMP_FontAsset.CreateFontAsset(Theme?Theme.FontFamily:FontFamily,"Regular",42);if(!KoreanFont)throw new InvalidOperationException("한국어 글꼴을 사용할 수 없습니다.");ownsFont=true;KoreanFont.isMultiAtlasTexturesEnabled=true;
   Clock.font=KoreanFont;if(InteractionHint)InteractionHint.font=KoreanFont;if(ViewCamera)ViewCamera.fieldOfView=Controls.Fov;
   var v=View(7);NotePanel=v.gameObject;NoteText=v.Body;NotePanel.SetActive(false);SyncCursor();if(StartWithMenu){titleMenu=true;Open(38);}
  }
  void OnDestroy(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;if(ownsFont&&KoreanFont){foreach(var t in KoreanFont.atlasTextures)if(t)Destroy(t);if(KoreanFont.material)Destroy(KoreanFont.material);Destroy(KoreanFont);}}
  void OnApplicationFocus(bool focused){
   if(Application.isBatchMode||port==null)return;
   if(!focused&&!focusPause){StopMovement();focusPause=true;port.Pause("K_WINDOW_FOCUS",true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
   else if(focused&&focusPause){focusPause=false;port.Pause("K_WINDOW_FOCUS",false);if(CurrentScreen==1)Open(38);else SyncCursor();}
  }
  public bool WantsLockedCursor=>port!=null&&CurrentScreen==1&&!port.ReadPlayer().Paused&&!focusPause;
  public void SyncCursor(){if(!WantsLockedCursor)StopMovement();Cursor.lockState=WantsLockedCursor?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!WantsLockedCursor;}
  void StopMovement(){port?.SetMove(0,0);motion?.SetRun(false);runLatched=false;runningActive=false;}
  public void ApplyMouseDelta(float x,float y){if(!WantsLockedCursor)return;var p=port.ReadPlayer();port.SetLook(p.Yaw+x*MouseSensitivity*(Controls.InvertX?-1:1),Mathf.Clamp((float)p.Pitch+y*Controls.SensitivityY*(InvertY?1:-1),-75,75));}
  void Update(){
   if(port==null)return;UpdateAmbientCaption();UpdateTaskHud();TrackCardWriting();var waitPort=Source as IPlayerWaitingPort;bool nowWaiting=waitPort?.IsWaiting??false;if(wasWaiting&&!nowWaiting){status=waitPort.ReadWaiting().Reason;feedbackUntil=Time.unscaledTime+5;}wasWaiting=nowWaiting;
   var exchangePort=Source as IPlayerItemExchangePort;var exchangeView=exchangePort?.ReadItemExchange();
   if(exchangeView!=null&&exchangeView.StateKey!=lastItemExchangeState){lastItemExchangeState=exchangeView.StateKey;if(!exchangeView.Running&&exchangeView.Text!=""){status=exchangeView.Text;feedbackUntil=Time.unscaledTime+5;if(CurrentScreen!=1)Render();}}
   var player=port.ReadPlayer();Clock.text=WorldTimeLabel.Format(player.Tick,player.ClockVersion);Clock.enabled=false;TickTrial();
   PlayerPresentationSettings.UpdateCamera(ViewCamera,Controls,CurrentScreen==1&&runningActive&&!player.Paused,Time.unscaledDeltaTime);
   if(rebindAction!=""){foreach(KeyCode key in Enum.GetValues(typeof(KeyCode)))if(Input.GetKeyDown(key)){if(key==KeyCode.Escape){rebindAction="";Render();break;}status=Controls.Rebind(rebindAction,key);if(status=="Applied"){Controls.Save();rebindAction="";}Render();break;}StopMovement();return;}
   if(!titleMenu&&Controls.Down("QuickSave")){status=port.SaveSlot();Render();}if(Controls.Down("QuickLoad")){status=port.LoadSlot();if(status=="불러오기 완료"){titleMenu=false;welcomePanel=false;SyncPauseView();}}
   if(Input.GetKeyDown(KeyCode.Escape)){if(CurrentScreen==1&&wasWritingCard){((IPlayerAppointmentCardPort)Source).CancelAppointmentCard();return;}if(CurrentScreen==1&&exchangeView?.Running==true){exchangePort.CancelItemExchange();return;}if(Source is IPlayerWaitingPort waitingPort&&waitingPort.IsWaiting){waitingPort.StopWaiting("기다리기를 멈췄어요.");return;}Back();return;}
   if(Controls.Down("Note")){ToggleNote();return;}if(Controls.Down("Map")){if(CurrentScreen==11)Back();else OpenNotePage(11);return;}
   // Once speech starts, the playback port owns hearing and interruption. A passing
   // resident can obscure the interaction ray without making the voice inaudible.
   if(CurrentScreen==3&&!(Source is IPlayerConversationPlaybackPort)&&!notebook.DescribeTarget(selectedTarget).Available){status="상대와 거리가 멀어져 대화가 끝났습니다.";Back();}
   if(CurrentScreen==14&&notebook.ReadInspection().State=="Completed"){status="";var recordId=notebook.ReadInspection().RecordId;Back();selectedRecord=Math.Max(0,Array.FindIndex(notebook.ReadNotebook().Records.Reverse().OrderByDescending(x=>x.ReceivedTick).ToArray(),x=>x.Id==recordId));Open(15);}
   if(CurrentScreen!=1){RefreshConversationView();StopMovement();if(InteractionHint)InteractionHint.text="";if(CurrentScreen==3&&Controls.Down("Interact"))AdvanceDialogue();if(Input.GetKeyDown(KeyCode.Tab))NextFocus();if(Time.unscaledTime>=refreshAt){refreshAt=Time.unscaledTime+.5f;Render();}return;}
   if(player.Paused||focusPause){StopMovement();if(InteractionHint)InteractionHint.text="Esc · 일시정지 메뉴";return;}
   bool weaponInputReady=Cursor.lockState==CursorLockMode.Locked;
   if(Cursor.lockState==CursorLockMode.Locked)ApplyMouseDelta(Input.GetAxisRaw("Mouse X"),Input.GetAxisRaw("Mouse Y"));else if(Input.GetMouseButtonDown(0))SyncCursor();
   if(Cursor.lockState!=CursorLockMode.Locked){StopMovement();return;}
   var input=new Vector3((Controls.Held("Right")?1:0)-(Controls.Held("Left")?1:0),0,(Controls.Held("Forward")?1:0)-(Controls.Held("Back")?1:0));var direction=PlayerFacing.TransformDirection(input.normalized);port.SetMove(direction.x,direction.z);
   if(Controls.ToggleRun){if(Controls.Down("Run"))runLatched=!runLatched;}else runLatched=Controls.Held("Run");runningActive=runLatched&&input.sqrMagnitude>0;motion?.SetRun(runningActive);
   if(weaponInputReady&&Input.GetMouseButtonDown(0)&&Source is IPlayerWeaponPort weaponPort&&weaponPort.HeldWeaponId()!=""){status=weaponPort.SwingWeapon();feedbackUntil=Time.unscaledTime+3;}
   if(Controls.Down("Drop")){status=port.Interact("DROP");feedbackUntil=Time.unscaledTime+3;audioFeedback.Select();}
   var target=AimedTarget();if(InteractionHint)InteractionHint.text=(target==null?"":"["+Controls.Label("Interact")+"]  "+target.PrimaryAction+" · "+target.Label+(Controls.ShowControlHints?"     "+Controls.Label("Inspect")+"  자세히":""))+(Time.unscaledTime<feedbackUntil?"\n"+DisplayStatus(status):"");
   if(Source is IPlayerWeaponPort weapons&&InteractionHint&&weapons.HeldWeaponId()!="")InteractionHint.text+="\n"+weapons.HeldWeaponHint()+" · "+(target==null?Controls.Label("Inspect")+" 손에 든 물건 살펴보기 · ":"")+Controls.Label("Drop")+" 내려놓기";
   if(waitPort!=null&&waitPort.IsWaiting&&InteractionHint){var w=waitPort.ReadWaiting();InteractionHint.text="시간 보내는 중 · "+TimeLabel(player.Tick)+" → "+TimeLabel(w.TargetTick)+"\n움직이거나 Esc를 누르면 멈춰요.";}
   if(exchangeView?.Running==true&&InteractionHint)InteractionHint.text=exchangeView.Text+"  "+(exchangeView.Progress*100).ToString("0")+"%";
   if(wasWritingCard&&InteractionHint)InteractionHint.text="약속 카드에 쓰고 있어요. 움직이거나 Esc를 누르면 멈춰요.";
   if(target==null&&Controls.Down("Inspect")&&Source is IPlayerWeaponPort held&&held.HeldWeaponId()!=""){selectedTarget=held.HeldWeaponId();status=notebook.Examine(selectedTarget);if(status=="Pending")Open(14);}
   if(target!=null&&Controls.Down("Interact"))Primary(target.TargetId);if(target!=null&&Controls.Down("Inspect")){selectedTarget=target.TargetId;Open(2);}
  }
  InteractionView AimedTarget()=>InteractionPicker.Pick(ViewCamera,PlayerFacing,notebook.DescribeTarget);
  public void Primary(string id){selectedTarget=id;status=port.Interact(id);wasWaiting=(Source as IPlayerWaitingPort)?.IsWaiting??false;feedbackUntil=Time.unscaledTime+3;audioFeedback.Select();if(status=="Dialogue"){dialogueMenu=0;Open(3);}else if(status=="Inspect"){status=notebook.Examine(id);if(status=="Pending")Open(14);}}
  ProductionScreenView View(int screen){
   if(views.TryGetValue(screen,out var v))return v;var d=ScreenDefinitions.Single(x=>x.ScreenId=="UI_"+screen.ToString("00"));var parent=transform.Find(d.CanvasGroup);if(!parent)parent=transform;
   v=Instantiate(d.Prefab,parent,false).GetComponent<ProductionScreenView>();v.Fonts(KoreanFont,Controls?.FontScale??1);v.ConfigurePresentation(Controls);v.gameObject.SetActive(false);views.Add(screen,v);return v;
  }
  public bool Open(int screen){
   if(screen<2||screen>41||stack.Count>=32)return false;
   if(screen>=23&&screen<=37){bool allowed=false;CheckTrialScreen(screen,ref allowed);if(!allowed){status="아직 시작된 재판 또는 공개된 결과가 없습니다.";Render();return false;}}
   if(screen>=7&&screen<=22&&screen!=14&&!note){port.Pause("K_NOTE",true);note=true;}
   if(screen>=38&&!settings){port.Pause("K_SETTINGS",true);settings=true;}
   if(screen==2&&!contextPause){port.Pause("K_CONTEXT",true);contextPause=true;}
   if(CurrentScreen!=1)View(CurrentScreen).gameObject.SetActive(false);stack.Add(screen);View(screen).gameObject.SetActive(true);if(screen==3)SyncDialoguePause();if(screen==40)saveLibrary=null;StopMovement();SyncTrialPause();SyncCursor();Render();SelectFirst();return true;
  }
  public void OpenNotePage(int screen){
   if(!note)Open(7);while(stack.Count>0&&stack[stack.Count-1]>=7&&stack[stack.Count-1]<=22&&stack[stack.Count-1]!=14){View(stack[stack.Count-1]).gameObject.SetActive(false);stack.RemoveAt(stack.Count-1);}
   stack.Add(screen);View(screen).gameObject.SetActive(true);Render();SyncCursor();SelectFirst();
  }
  public void Back(){
   if(CurrentScreen==7&&taskNotebook){taskNotebook=false;Render();SelectFirst();return;}
   if(CurrentScreen==40&&pendingSave!=""){pendingSave="";Render();return;}
   if(CurrentScreen==3&&dialogueMenu!=0){dialogueMenu=0;Render();SelectFirst();return;}
   if(CurrentScreen==25&&choosingTrialRule){choosingTrialRule=false;Render();SelectFirst();return;}
   if(titleMenu&&CurrentScreen==38)return;
   if(stack.Count==0){if(port.ReadPlayer().Paused)SyncPauseView();if(stack.Count==0)Open(38);return;}
   if(CurrentScreen==14)notebook.CancelInspection();if(CurrentScreen==3)(Source as IPlayerConversationPlaybackPort)?.EndConversation();View(CurrentScreen).gameObject.SetActive(false);stack.RemoveAt(stack.Count-1);ReconcileTokens();if(CurrentScreen!=1)View(CurrentScreen).gameObject.SetActive(true);SyncCursor();Render();SelectFirst();
  }
  void ReconcileTokens(){
   SyncDialoguePause();
   if(note&&!stack.Any(x=>x>=7&&x<=22&&x!=14)){port.Pause("K_NOTE",false);note=false;}
   if(settings&&!stack.Any(x=>x>=38)){port.Pause("K_SETTINGS",false);settings=false;}
   if(contextPause&&!stack.Contains(2)){port.Pause("K_CONTEXT",false);contextPause=false;}SyncTrialPause();
  }
  public void ToggleNote(){
   if(note){foreach(int page in stack.Where(x=>x>=7&&x<=22&&x!=14).ToArray()){View(page).gameObject.SetActive(false);stack.Remove(page);}ReconcileTokens();if(CurrentScreen!=1)View(CurrentScreen).gameObject.SetActive(true);Render();SyncCursor();}
   else Open(7);
  }
  public void ToggleSettings(){
   if(settings){foreach(int page in stack.Where(x=>x>=38).ToArray()){View(page).gameObject.SetActive(false);stack.Remove(page);}ReconcileTokens();if(CurrentScreen!=1)View(CurrentScreen).gameObject.SetActive(true);Render();SyncCursor();}else Open(38);
  }
  public void SyncPauseView(){
   foreach(var v in views.Values)v.gameObject.SetActive(false);stack.Clear();var p=port.ReadPlayer();var saved=notebook.ReadUiState();note=p.NotePause;settings=p.SettingsPause;
   taskNotebook=saved.TaskNotebook;taskHistory=saved.TaskHistory;selectedTask=saved.SelectedTaskId??"";taskHudRefresh=0;
   selectedRecord=Math.Max(0,Array.FindIndex(notebook.ReadNotebook().Records.Reverse().OrderByDescending(x=>x.ReceivedTick).ToArray(),x=>x.Id==saved.SelectedRecordId));selectedTarget=saved.SelectedTarget;RestoreTrialSelection(saved);RestoreArchiveSelection(saved);
   if(saved.Pages.Length>0)stack.AddRange(saved.Pages);else{if(note)stack.Add(7);if(settings)stack.Add(38);}
   SyncDialoguePause();contextPause=stack.Contains(2);port.Pause("K_CONTEXT",contextPause);port.Pause("K_WINDOW_FOCUS",false);focusPause=false;
   note=stack.Any(x=>x>=7&&x<=22&&x!=14);settings=stack.Any(x=>x>=38);port.Pause("K_NOTE",note);port.Pause("K_SETTINGS",settings);
   if(CurrentScreen!=1)View(CurrentScreen).gameObject.SetActive(true);SyncTrialPause();Render();SyncCursor();
  }
  void CloseForAction(Action action){if(stack.Contains(3))(Source as IPlayerConversationPlaybackPort)?.EndConversation();foreach(var v in views.Values)v.gameObject.SetActive(false);stack.Clear();ReconcileTokens();action();SyncCursor();}
  void SelectFirst(){if(CurrentScreen!=1&&EventSystem.current){var b=View(CurrentScreen).RecordRowButtons.Concat(View(CurrentScreen).Actions).FirstOrDefault(x=>x.gameObject.activeSelf&&x.interactable);EventSystem.current.SetSelectedGameObject(b?b.gameObject:null);}}
  void NextFocus(){if(CurrentScreen==1||!EventSystem.current)return;var buttons=View(CurrentScreen).RecordRowButtons.Concat(View(CurrentScreen).Actions).Where(x=>x.gameObject.activeSelf&&x.interactable).ToArray();if(buttons.Length==0)return;int index=Array.FindIndex(buttons,x=>x.gameObject==EventSystem.current.currentSelectedGameObject);EventSystem.current.SetSelectedGameObject(buttons[(index+1)%buttons.Length].gameObject);}
  string TimeLabel(long tick)=>WorldTimeLabel.Format(tick,port.ReadPlayer().ClockVersion);
  string RecordTitle(KnownRecord r)=>TimeLabel(r.FromTick)+" · "+(r.Kind=="Video"?"영상 열람":r.Direct?"직접 확인":"전언")+"\n"+r.Text;
  void Render(){
   SyncDialoguePause();
   var data=notebook.ReadNotebook();var records=data.Records.Reverse().OrderByDescending(x=>x.ReceivedTick).ToArray();selectedRecord=Mathf.Clamp(selectedRecord,0,Math.Max(0,records.Length-1));var selected=records.Length==0?null:records[selectedRecord];
   var uiState=new PlayerUiSnapshot{TaskNotebook=taskNotebook,TaskHistory=taskHistory,SelectedTaskId=selectedTask,Pages=stack.ToArray(),SelectedRecordId=selected?.Id??"",SelectedTarget=selectedTarget};CaptureTrialSelection(uiState);CaptureArchiveSelection(uiState);notebook.StoreUiState(uiState);
   if(CurrentScreen==1)return;var v=View(CurrentScreen);bool trialHandled=false;RenderTrial(CurrentScreen,v,ref trialHandled);if(trialHandled)return;
   var focused=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
   foreach(var b in v.Actions){b.onClick.RemoveAllListeners();b.gameObject.SetActive(false);}int bi=0;
   void Button(string label,Action action){if(bi>=v.Actions.Length)return;int i=bi++;v.ActionLabels[i].text=label;v.Actions[i].gameObject.SetActive(true);v.Actions[i].interactable=true;v.Actions[i].onClick.AddListener(()=>{action();audioFeedback.Select();Render();});}
   string title=ScreenDefinitions.Single(x=>x.ScreenId=="UI_"+CurrentScreen.ToString("00")).Title,body="",context="";
   switch(CurrentScreen){
    case 2:
     var c=notebook.DescribeTarget(selectedTarget);body=c.Label;context="어떻게 할까요?";
     if(Source is IPlayerWeaponPort heldObject&&heldObject.HeldWeaponId()==selectedTarget)Button("내려놓기",()=>CloseForAction(()=>status=port.Interact("DROP")));
     else Button(c.PrimaryAction,()=>CloseForAction(()=>Primary(selectedTarget)));
     Button("관찰하기",()=>CloseForAction(()=>{status=notebook.Examine(selectedTarget);if(status=="Pending")Open(14);}));
     if((selectedTarget.StartsWith("K_DOOR_",StringComparison.Ordinal)||selectedTarget.StartsWith("D_",StringComparison.Ordinal)))Button("잠금 전환",()=>CloseForAction(()=>status=port.Interact("LOCK:"+selectedTarget)));break;
    case 3:
     title=notebook.DescribeTarget(selectedTarget).Label;body=records.FirstOrDefault(x=>(x.Kind=="Statement"||x.Predicate=="SaidStatement")&&x.Source==selectedTarget)?.Text??"아직 들은 얘기가 없어요.";
     var actor=FindObjectsByType<FixtureTarget>().FirstOrDefault(x=>x.StableId==selectedTarget);if(actor&&v.Standing){if(!portrait)portrait=gameObject.AddComponent<DialoguePortrait>();portrait.Show(actor.transform,v.Standing);}
     var playback=(Source as IPlayerConversationPlaybackPort)?.ReadConversationPlayback();
     if(playback!=null&&playback.Phase!="Idle"&&(playback.SpeakerId==selectedTarget||playback.SpeakerId=="CH_01")){body=playback.HeardText.Length==0?"…":playback.HeardText;if(playback.SpeakerId=="CH_01")title=ActorLabel("CH_01");}
     v.DialogueUsesWorldPlayback=playback!=null&&playback.Phase!="Idle";
     RenderDialogue(v,records,selected,Button,ref context);
     if(v.DialogueRecordPreview){title="기록 건네기 · "+title+"에게";body=selected==null?"아직 건넬 기록이 없어요.":RecordTitle(selected);}break;
    case 4:
     RenderInvitationPicker(v,Button,ref title,ref body,ref context);break;
    case 5:RenderSharePicker(v,records,Button,ref title,ref body,ref context);break;
    case 7:
     if(taskNotebook){RenderTaskNotebook(v,Button,ref title,ref body,ref context);break;}
     v.SetRecordRows(Array.Empty<RecordRow>(),"",_=>{});
     title="내 노트";
     body="무엇을 다시 볼까요?\n\n나눈 대화와 살펴본 내용은 자동으로 남아요.\n노트를 닫으면 멈췄던 시간이 다시 흐릅니다.";
     context=TimeLabel(port.ReadPlayer().Tick)+"\n\n들고 있는 물건\n"+port.ReadPlayer().HeldItem+"\n\n"+Controls.Label("Note")+" 또는 Esc로 노트 닫기";
     if(Source is IPlayerTaskJournalPort)Button("맡은 일과 빌린 물건",()=>{taskNotebook=true;taskHistory=false;selectedTask="";});
     Button("나눈 대화",()=>{recordCategory=2;recordVisibleCount=20;OpenNotePage(10);});Button("살펴본 것",()=>{recordCategory=1;recordVisibleCount=20;OpenNotePage(10);});Button("만난 사람",()=>OpenNotePage(9));Button("지도",()=>OpenNotePage(11));Button("약속",()=>OpenNotePage(12));
     if(records.Any(x=>x.Predicate=="CausedOutcome"||x.Predicate=="ContactTrace"||x.Value=="Collapsed"))Button("사건 수첩",()=>OpenNotePage(8));
     Button("지난 회차",()=>OpenNotePage(13));break;
    case 8:var caseRecords=records.Where(x=>x.Predicate=="CausedOutcome"||x.Predicate=="UsedObject"||x.Predicate=="ContactTrace"||x.Value=="Collapsed").ToArray();body=caseRecords.Length==0?"아직 사건으로 묶을 기록이 없어요.":string.Join("\n\n",caseRecords.Select(RecordTitle));context="무슨 일이 있었는지부터 살펴봐요. 누가 왜 그랬는지는 따로 확인해야 해요.";Button("조사 질문",()=>Open(17));Button("보드",()=>Open(20));break;
    case 9:RenderPeople(v,data,Button,ref title,ref body,ref context);break;
    case 10:
     RenderRecordLibrary(v,records,Button,ref title,ref body,ref context);break;
    case 11:
     if(Source is IPlayerMapPort mapSource){
      var map=KnownMapView.Attach(v);map.Bind(mapSource.ReadMap(),KoreanFont,Controls.FontScale,port.ReadPlayer().ClockVersion);if(Source is IPlayerMapPosePort poseSource)map.BindPose(poseSource.ReadMapPose());body="";context=map.Information;
      foreach(var action in v.Actions)action.interactable=true;
      Button("이전 층",()=>map.CycleFloor(-1));Button("다음 층",()=>map.CycleFloor(1));Button("축소",()=>map.Zoom(1/1.3f));Button("확대",()=>map.Zoom(1.3f));Button("내 위치",map.CenterOnSelf);Button(map.IsPerspective?"평면 보기":"입체 보기",map.TogglePerspective);Button("다음 표시",()=>map.SelectNext(1));
      if(map.LayerCount<2){v.Actions[0].interactable=false;v.Actions[1].interactable=false;}if(map.MarkerCount==0)for(int i=2;i<7;i++)v.Actions[i].interactable=false;
     }else{
      body=places?.PublishedMapText()??"알고 있는 시설 도면 · FixtureK\n\n도서실 L ─ 홀 H ─ 북문 N ─ 작업실 W\n                  │             └ 남문 S ┘\n                 온실 G\n\n도면은 위치 관측이 아닙니다.";
      context=data.Locations.Length==0?"마지막 확인 위치가 없습니다.":string.Join("\n\n",data.Locations.Select(x=>x.ActorId+" · "+Place(x.PlaceId)+"\n"+TimeLabel(x.Tick)+" / "+x.SourceRecordId));
     }break;
    case 12:
     body=data.Appointments.Length==0?"아직 수신한 약속이 없습니다.\n가까운 인물과 대화해 약속을 제안할 수 있습니다.":string.Join("\n\n",data.Appointments.Select(x=>TimeLabel(x.StartTick)+" · "+Place(x.PlaceId)+" · "+ActorLabel(x.Organizer=="CH_01"?x.Invitee:x.Organizer)+"\n"+PlayerUiText.Appointment(x.State)));context="여기서 잠깐 시간을 보낼 수 있어요. 움직이거나 Esc를 누르면 멈춰요.\n들은 안내나 새 약속이 생겨도 멈춥니다.";
     if(Source is IPlayerWaitingPort wait){Button("5분 보내기",()=>CloseForAction(()=>status=wait.StartWaiting(5L*60*60)));Button("30분 보내기",()=>CloseForAction(()=>status=wait.StartWaiting(30L*60*60)));if(wait.ReadWaiting().CanWaitForAppointment)Button("약속에 맞춰 기다리기",()=>CloseForAction(()=>status=wait.WaitForNextAppointment()));}break;
    case 13:RenderArchive(v,ref title,ref body,ref context,Button);break;
    case 14:
     var task=notebook.ReadInspection();body=task.State=="Running"?"관찰 중…  "+Mathf.RoundToInt(100f*task.ElapsedTicks/task.DurationTicks)+"%":task.State=="Completed"?"관찰을 마쳤습니다.":"대상에 접근할 수 없어 조사가 중단됐습니다.";context="거리를 벗어나거나 시야가 가려지면 중단합니다.\n노트를 읽는 동안 조사 시간도 멈춥니다.";break;
    case 15:case 16:
     if(selected?.SubjectId==BASSLINE.AuthoringData.AppointmentDesk.CardId){title="약속 카드";body=selected.Text;context="카드에 적힌 내용이에요.\n\n고쳐 쓰더라도 상대에게 전하기 전에는 예전 약속을 알고 있을 수 있어요.";if(Source is IPlayerAppointmentCardPort card&&card.ReadAppointmentCard().Nearby)Button("카드에 약속 적기",OpenCardWriter);break;}
     if(selected?.Kind=="Document"&&(selected.Predicate=="PrivateReturnNote"||selected.Predicate=="CleanupRecord")){
      title=selected.Predicate=="PrivateReturnNote"?"태겸의 메모":"공용 정리 기록";body=selected.Text;
      context=selected.Predicate=="PrivateReturnNote"?"작업실의 가방 앞에서 읽은 메모예요.\n\n뜻이 헷갈리면 태겸에게 물어볼 수 있어요.\n읽은 내용은 노트에도 남아요.":"공용 장부에서 읽은 내용이에요.\n\n적힌 물건은 정리함에서 직접 확인할 수 있어요.\n읽은 내용은 노트에도 남아요.";break;
     }
     body=selected==null?"확인한 관측이 없습니다.":"여기까지는 알 수 있어요\n\n"+string.Join("\n",selected.Supports)+"\n\n"+RecordTitle(selected);
     context=selected==null?"먼저 실제 대상에 접근해 관찰하세요.":"이것만으론 아직 몰라요\n\n"+string.Join("\n",selected.DoesNotEstablish)+"\n\n출처: "+ActorLabel(selected.Source)+"\n\n원문과 전달된 내용은 같은 출처일 수 있습니다.";
     if(selected!=null)Button("사건 수첩에 남기기",()=>{notebook.Hypothesize(selected.Id);status="나중에 확인할 내용으로 남겼어요.";});break;
    case 17:body=selected==null?"아직 비교할 기록이 없어요.":"이 관측의 시간 범위는 어디까지인가?\n직접 확인과 전언은 구분되는가?\n다른 독립 관측이 있는가?";context=selected?.Text??"주변을 살펴보고 이야기를 들어 봐요. 기록이 생기면 같이 정리할 수 있어요.";Button("확인 방법",()=>Open(18));break;
    case 18:body="눈앞의 현장을 살펴보기\n가까운 사람에게 직접 묻기\n이미 모은 기록끼리 비교하기";context="노트를 닫고 실제 공간으로 이동해 확인합니다.";Button("탐색으로",()=>CloseForAction(()=>{}));break;
    case 19:
     body=selected==null?"연결할 자료가 없습니다.":RecordTitle(selected);context="관측 구간 안과 밖의 주장을 비교합니다. 결과는 현재 자료만 사용합니다.";
     if(selected!=null){Button("범위 안 주장",()=>status=notebook.Assess(selected.Predicate,selected.SubjectId,selected.Value,selected.FromTick,selected.ToTick,new[]{selected.Id}));Button("범위 확장",()=>status=notebook.Assess(selected.Predicate,selected.SubjectId,selected.Value,Math.Max(0,selected.FromTick-600),selected.ToTick+600,new[]{selected.Id}));}break;
    case 20:body=string.Join("\n\n",records.Select(RecordTitle));if(body=="")body="보드에 연결할 자료가 없습니다.";context="현재 받은 자료만 표시합니다.";Button("함께 비교하기",()=>Open(19));Button("나중에 확인할 것",()=>Open(21));break;
    case 21:
     title="나중에 확인할 것";
     var hypothesis=data.Hypotheses.LastOrDefault();body=hypothesis==null?"아직 따로 남긴 생각이 없어요.":hypothesis.Text;context=hypothesis==null?"사건을 살펴보다 다시 확인하고 싶은 내용을 여기에 남길 수 있어요.":(hypothesis.Status=="Held"?"나중에 다시 보기":hypothesis.Status=="Withdrawn"?"가능성에서 뺀 생각":"아직 확인하지 않은 생각")+"\n\n이 내용이 맞는지는 현장에서 더 알아봐야 해요.";
     if(selected!=null)Button("이 내용도 남기기",()=>{notebook.Hypothesize(selected.Id);status="확인할 내용으로 남겼어요.";});if(hypothesis!=null){Button("나중에 다시 보기",()=>status=notebook.SetHypothesisStatus(hypothesis.Id,"Held"));Button("가능성에서 빼기",()=>status=notebook.SetHypothesisStatus(hypothesis.Id,"Withdrawn"));}Button("어떻게 알아볼까?",()=>Open(18));break;
    case 22:body="공표된 구조와 현재 관측으로 동선을 검토합니다.";context="현재 실측은 과거 이동 시간의 증명이 아닙니다.";Button("지도",()=>OpenNotePage(11));break;
    case 38:
     v.ConfigureTitleCard(titleMenu&&!welcomePanel);
     if(welcomePanel){RenderWelcome(Button,ref title,ref body,ref context);break;}
     if(titleMenu){title="BASSLINE";body="같은 사람, 다른 진실.\n반복되는 저택에서, 당신이 본 것을 믿으세요.";context=Controls.Label("Forward")+Controls.Label("Left")+Controls.Label("Back")+Controls.Label("Right")+" 이동 · 마우스 시점\n"+Controls.Label("Run")+" 달리기 · "+Controls.Label("Interact")+" 상호작용\n"+Controls.Label("Note")+" 노트 · Esc 메뉴";Button("새로 시작",()=>{welcomePanel=true;});if(Source is ISessionPresencePort presence&&presence.HasSave)Button("이어서 하기",()=>{status=port.LoadSlot();if(status=="불러오기 완료"){titleMenu=false;SyncPauseView();}});Button("저장 불러오기",()=>Open(40));Button("설정",()=>Open(39));Button("게임 종료",()=>Application.Quit());}
     else{body="일시정지\n\n지금은 멈춰 있어요. 천천히 해도 돼요.";context="Esc · 이전 화면으로";Button("계속하기",Back);Button("설정",()=>Open(39));Button("저장/로드",()=>Open(40));Button("접근성",()=>Open(41));Button("게임 종료",()=>Application.Quit());}break;
    case 39:
     RenderSettings(Button,out body,out context);break;
    case 40:
     RenderSaveLibrary(Button,ref body,ref context);break;
    case 41:
     body="한국어 글자 크기 "+Mathf.RoundToInt(Controls.FontScale*100)+"%\n\n관측한 순간의 위치는 이후의 이동을 입증하지 않습니다.\n\n모션 감소 "+(Controls.ReduceMotion?"켜짐":"꺼짐");context="색 없이도 제목·문장·출처로 상태를 구분합니다.\n긴 문장은 스크롤로 읽을 수 있습니다.";Button("글자 −",()=>{Controls.FontScale=Mathf.Max(1,Controls.FontScale-.1f);SaveControls();});Button("글자 +",()=>{Controls.FontScale=Mathf.Min(2,Controls.FontScale+.1f);SaveControls();});Button("모션 감소",()=>{Controls.ReduceMotion=!Controls.ReduceMotion;SaveControls();});break;
    default:body="아직 확인한 내용이 없어요.";context="새로 알게 된 내용이 있으면 여기에 모아 둘게요.";break;
   }
   if(CurrentScreen!=3&&CurrentScreen!=5&&!(titleMenu&&CurrentScreen==38))Button(stack.Contains(3)?"대화로 돌아가기":CurrentScreen==7?(taskNotebook?"노트 첫 화면":"노트 닫기"):"뒤로",Back);v.Show(title,body,context,DisplayStatus(status));Canvas.ForceUpdateCanvases();if(focused&&focused.activeInHierarchy&&EventSystem.current)EventSystem.current.SetSelectedGameObject(focused);
  }
  static string DisplayStatus(string code)=>PlayerUiText.Status(code);
  void SaveControls(){Controls.Validate();MouseSensitivity=Controls.Sensitivity;InvertY=Controls.InvertY;Controls.Save();foreach(var v in views.Values){v.Fonts(KoreanFont,Controls.FontScale);v.ConfigurePresentation(Controls);}PlayerPresentationSettings.Apply(Controls,transform);}
  string Place(string id)=>places?.PlaceLabel(id)??(id=="K_H"?"홀":id=="K_W"?"작업실":id=="K_L"?"도서실":id=="K_G"?"온실":"복도");
 }
}





