using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Bassline.Unity
{
    public sealed class BasslineGame : MonoBehaviour
    {
        public WorldState world;
        public Content content;
        public MansionView mansion;
        bool title=true,notebook,pause,autoDebate=true,votePanel;
        int page,selectedPerson=1,target=1,selectedEvidence,first=-1,second=-1,linkType;
        string message="",talk="",seedText="1709";
        int talking=-1;
        float simulationTimer,debateTimer;
        Vector2 scroll,detailScroll,logScroll,speechScroll;
        Font font;
        GUIStyle text,small,heading,display,button,muted;
        Texture2D circle;
        readonly Color ink=new Color(.035f,.052f,.066f),paper=new Color(.9f,.88f,.81f),gold=new Color(.79f,.67f,.44f),teal=new Color(.39f,.75f,.72f),faded=new Color(.5f,.56f,.59f);
        string SavePath { get { return Path.GetFullPath(Path.Combine(Application.dataPath,"..","Saves","session.json")); } }
        bool PreliminaryPending {get {return world.phase==Phase.Investigation && world.HasRule("A8") && !world.preliminaryConfirmed;}}
        bool Explore {get {return !title && !notebook && !pause && talking<0 && !PreliminaryPending && (world.phase==Phase.Daily || world.phase==Phase.Investigation);}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if(FindAnyObjectByType<BasslineGame>()==null) new GameObject("BASSLINE").AddComponent<BasslineGame>(); }
        void Awake()
        {
            Application.targetFrameRate=60;
            content=JsonUtility.FromJson<Content>(Resources.Load<TextAsset>("Content").text);
            world=WorldFactory.Create(content,1709);
            mansion=gameObject.AddComponent<MansionView>();mansion.Initialize();mansion.RenderRoom(world,content);
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},24);
            circle=new Texture2D(64,64,TextureFormat.RGBA32,false);
            for(int y=0;y<64;y++) for(int x=0;x<64;x++) circle.SetPixel(x,y,Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))<31?Color.white:Color.clear);
            circle.Apply();
            var args=Environment.GetCommandLineArgs();if(args.Contains("--qa")) StartCoroutine(QaCapture(args));
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.Escape)) {if(notebook) notebook=false;else if(talking>=0) talking=-1;else if(!title) pause=!pause;}
            if(Input.GetKeyDown(KeyCode.Tab) && !title) {notebook=!notebook;talking=-1;}
            if(Input.GetKeyDown(KeyCode.F5) && !title) Save();
            if(Input.GetKeyDown(KeyCode.T) && !title && world.phase==Phase.Investigation && !PreliminaryPending) BeginTrial();
            Cursor.lockState=Explore?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!Explore;
            mansion.Control(Explore);
            if(Explore)
            {
                if(Input.GetKeyDown(KeyCode.E) && mansion.focused!=null) Interact(mansion.focused);
                simulationTimer+=Time.deltaTime;
                if(simulationTimer>=8) {simulationTimer=0;Advance();}
                if(Input.GetKeyDown(KeyCode.Space)) Advance();
            }
            if(!title && !pause && !notebook && world.phase==Phase.Trial && !votePanel && autoDebate)
            {
                debateTimer+=Time.deltaTime;
                if(debateTimer>=7) {debateTimer=0;DialogueDirector.Next(world,content);}
            }
        }
        void Advance()
        {
            var before=world.phase;WorldSimulation.Advance(world,content);mansion.RenderOccupants(world,content);
            if(before!=world.phase) {talking=-1;message=world.notice;Save(false);} else if(world.tick%5==0) Save(false);
        }
        void BeginTrial()
        {
            GameFlow.StartTrial(world,content);notebook=false;talking=-1;votePanel=false;target=world.Living.First(p=>p.id!=0).id;debateTimer=0;Save(false);
        }
        void Interact(Interaction item)
        {
            if(item.kind=="door")
            {if(WorldSimulation.Move(world,0,item.id)) {Advance();mansion.RenderRoom(world,content);talking=-1;}}
            if(item.kind=="npc") {talking=item.id;selectedPerson=item.id;talk=content.Person(item.id).quote;}
            if(item.kind=="evidence")
            {
                KnowledgeSystem.Learn(world,0,item.evidenceId,0,true);var e=world.incident.evidence.Single(x=>x.id==item.evidenceId);
                message=e.title+"을 기록했습니다.";mansion.RenderOccupants(world,content);Save(false);
            }
            if(item.kind=="curator") {page=3;notebook=true;}
        }
        public void Save(bool notify=true)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));string temp=SavePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(world,true));
                if(File.Exists(SavePath)) File.Replace(temp,SavePath,SavePath+".bak");else File.Move(temp,SavePath);
                if(notify) message="현재 장과 확보한 기록을 저장했습니다.";
            } catch(Exception ex) {message="저장하지 못했습니다: "+ex.Message;Debug.LogError(ex);}
        }
        void Load()
        {
            try
            {
                var loaded=JsonUtility.FromJson<WorldState>(File.ReadAllText(SavePath));
                if(loaded==null || loaded.schema!=1 || loaded.characters.Count!=18 || loaded.rooms.Count!=28 || loaded.randomState==0) throw new InvalidDataException("지원하지 않는 저장 형식");
                if(loaded.characters.Any(p=>p.room<0||p.room>=28) || loaded.characters.Select(p=>p.id).Distinct().Count()!=18) throw new InvalidDataException("잘못된 세계 상태");
                world=loaded;title=false;pause=false;notebook=false;talking=-1;first=second=-1;votePanel=false;mansion.RenderRoom(world,content);message="저장된 기록을 불러왔습니다.";
            } catch(Exception ex) {message="불러오지 못했습니다: "+ex.Message;}
        }
        void NewGame()
        {
            int seed;if(!int.TryParse(seedText,out seed)) seed=1709;world=WorldFactory.Create(content,seed);title=false;pause=false;notebook=false;talking=-1;first=second=-1;message="";mansion.RenderRoom(world,content);Save(false);
        }
        void Styles()
        {
            if(text!=null) return;
            text=new GUIStyle(GUI.skin.label){font=font,fontSize=20,wordWrap=true,richText=false};text.normal.textColor=paper;
            small=new GUIStyle(text){fontSize=15};muted=new GUIStyle(small);muted.normal.textColor=faded;
            heading=new GUIStyle(text){fontSize=34,fontStyle=FontStyle.Bold};display=new GUIStyle(heading){fontSize=78};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=19,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(18,14,8,8),wordWrap=true};button.normal.textColor=paper;button.hover.textColor=Color.white;
        }
        void Box(float x,float y,float w,float h,Color color) {Color old=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=old;}
        void Label(float x,float y,float w,float h,string value,GUIStyle style=null) {GUI.Label(new Rect(x,y,w,h),value,style??text);}
        bool Button(float x,float y,float w,float h,string value,bool active=false)
        {
            var old=GUI.backgroundColor;GUI.backgroundColor=active?new Color(.3f,.55f,.55f):new Color(.13f,.2f,.23f);bool click=GUI.Button(new Rect(x,y,w,h),value,button);GUI.backgroundColor=old;return click;
        }
        void Disk(float x,float y,float w,float h,Color color) {Color old=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(x,y,w,h),circle);GUI.color=old;}
        Color PersonColor(int id) {if(id<0)return teal;ColorUtility.TryParseHtmlString("#"+content.Person(id).color,out Color color);return color;}
        void Portrait(int id,float x,float y,float w,float h,bool speaking=false)
        {
            Color accent=PersonColor(id);Box(x,y,w,h,new Color(.09f,.13f,.16f));Box(x,y,w,3,accent);
            if(id>=0 && !world.Person(id).alive) {Label(x+9,y+h*.36f,w-18,80,"빈 자리",small);return;}
            float cx=x+w*.5f;
            if(id<0)
            {
                Box(cx-w*.27f,y+h*.5f,w*.54f,h*.5f,new Color(.04f,.07f,.09f));
                Disk(cx-w*.3f,y+h*.14f,w*.6f,h*.37f,new Color(.23f,.51f,.58f));
                Box(cx-w*.12f,y+h*.28f,w*.2f,h*.05f,gold);Box(cx+w*.06f,y+h*.25f,w*.08f,h*.1f,gold);
                Box(cx-w*.025f,y+h*.52f,w*.05f,h*.28f,gold);
            }
            else
            {
                bool longHair=new[]{3,10,11,13,15,16}.Contains(id);
                Color hair=id==10?new Color(.55f,.35f,.19f):id==16?new Color(.68f,.58f,.39f):id==11?new Color(.44f,.29f,.31f):new Color(.08f,.1f,.14f);
                if(longHair) Disk(cx-w*.28f,y+h*.12f,w*.56f,h*.51f,hair);
                Disk(cx-w*.19f,y+h*.16f,w*.38f,h*.32f,new Color(.75f,.64f,.58f));
                Disk(cx-w*.22f,y+h*.1f,w*.44f,h*.2f,hair);
                Box(cx-w*.19f,y+h*.21f,w*.1f,h*.08f,hair);
                Box(cx-w*.06f,y+h*.44f,w*.12f,h*.11f,new Color(.68f,.56f,.5f));
                Disk(cx-w*.37f,y+h*.5f,w*.74f,h*.62f,accent*.78f);
                Box(cx-w*.32f,y+h*.65f,w*.64f,h*.35f,accent*.78f);
                Box(cx-w*.055f,y+h*.52f,w*.11f,h*.48f,new Color(.75f,.74f,.67f));
                Box(cx-w*.12f,y+h*.31f,w*.05f,Mathf.Max(2,h*.008f),ink);Box(cx+w*.07f,y+h*.31f,w*.05f,Mathf.Max(2,h*.008f),ink);
                Box(cx-w*.04f,y+h*.39f,w*.08f,Mathf.Max(1,h*(speaking?.012f:.004f)),new Color(.38f,.24f,.23f));
                if(id==10) {Disk(cx-w*.18f,y+h*.55f,w*.14f,h*.045f,gold);Disk(cx+w*.04f,y+h*.55f,w*.14f,h*.045f,gold);}
                if(id==1||id==15) Box(cx-w*.34f,y+h*.71f,w*.68f,h*.025f,accent);
            }
            Box(x,y+h-5,w,5,speaking?teal:accent*.6f);
        }
        void OnGUI()
        {
            Styles();float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1600*scale)/2,(Screen.height-900*scale)/2,0),Quaternion.identity,new Vector3(scale,scale,1));
            if(title) {Title();return;}
            if(world.phase==Phase.Trial) Trial(); else if(world.phase==Phase.Daily || world.phase==Phase.Investigation) Hud();else PhaseScreen();
            if(talking>=0 && !notebook) Conversation();
            if(notebook) Notebook();
            if(PreliminaryPending) PreliminaryPanel();
            if(pause) PauseMenu();
        }
        void Title()
        {
            Box(0,0,800,900,new Color(.025f,.04f,.05f,.96f));Box(0,0,1600,8,gold);
            Label(82,90,600,36,"A MANSION OF EIGHTEEN CHOICES",small);
            Label(74,158,700,115,"BASSLINE",display);Label(84,278,550,50,"베이스라인",heading);
            Box(86,352,82,3,gold);Label(84,388,560,118,"같은 사람. 다른 선택.\n당신이 밝힌 진실은, 모두의 판단이 될 수 있을까?",text);
            if(Button(84,548,440,60,"새로운 입장  →")) NewGame();
            GUI.enabled=File.Exists(SavePath);if(Button(84,620,440,54,"기록 이어가기"))Load();GUI.enabled=true;
            Label(86,710,150,30,"회차 시드",small);seedText=GUI.TextField(new Rect(220,706,180,34),seedText,12,text);
            Label(86,776,630,55,"Unity 6 · 시스템 프로토타입 0.1\n1인칭 탐색 / 독립적인 지식 / 사회적 재판",muted);
            Label(1140,734,340,55,"18명이 입장했다.\n남은 자리는 세 개.",heading);
            if(!string.IsNullOrEmpty(message)) Label(84,850,900,40,message,small);
        }
        string PhaseName() {return world.phase==Phase.Daily?"자유행동":world.phase==Phase.Investigation?"사건 조사":world.phase==Phase.Trial?"계약 재판":"관장의 공표";}
        void Header()
        {
            Box(0,0,1600,82,ink);Box(32,24,5,34,gold);Label(53,19,290,45,"BASSLINE",heading);
            Label(397,29,650,35,"LOOP "+world.loop.ToString("00")+"  /  CHAPTER "+world.chapter.ToString("00")+"     "+PhaseName(),small);
            Label(1180,29,370,35,world.Living.Count+" / 18 생존    ·    "+world.Time,small);
        }
        void Hud()
        {
            Header();Box(38,113,360,110,new Color(.03f,.055f,.07f,.9f));Label(58,131,330,40,world.rooms[world.Person(0).room].name,heading);
            Label(60,182,320,27,world.phase==Phase.Daily?"대화하고, 변화를 지켜보세요.":"현장 · 기록 보관실 · 도서실 · 작업실",small);
            Box(795,446,10,2,paper);Box(799,442,2,10,paper);
            if(mansion.focused!=null) {Box(440,643,720,62,new Color(.025f,.05f,.06f,.95f));Label(464,659,680,35,"E   "+mansion.focused.label,text);}
            Box(0,820,1600,80,ink);Label(40,835,1080,30,"WASD  이동    마우스  시점    E  상호작용    Tab  조사수첩    Space  시간 진행    F5  저장",small);
            Label(40,872,1420,24,message,muted);
            if(world.phase==Phase.Investigation && Button(1270,830,280,48,"T  재판 시작")) BeginTrial();
            if(world.phase==Phase.Daily) Label(1180,830,370,48,"8초마다 세계 시간이 흐릅니다.",muted);
        }
        void Conversation()
        {
            Box(0,230,1600,670,new Color(.02f,.04f,.05f,.97f));Portrait(talking,70,270,330,550,true);
            Label(450,294,900,55,content.Person(talking).name,heading);Label(452,357,900,40,content.Person(talking).job,muted);
            Label(450,425,1040,245,talk,text);
            if(world.phase==Phase.Daily)
            {
                if(Button(450,697,365,58,"이야기를 듣고 도움 제안")) {talk=SocialInteractionSystem.Talk(world,content,talking,true);Advance();}
                if(Button(831,697,365,58,"계약에 관해 묻기")) {talk=SocialInteractionSystem.Talk(world,content,talking,false);Advance();}
            }
            else if(Button(450,697,620,58,"확인한 단서와 증언을 요청")) {talk=SocialInteractionSystem.Talk(world,content,talking,false);Save(false);}
            if(Button(1240,781,290,56,"대화 마치기")) talking=-1;
        }
        void PhaseScreen()
        {
            Box(0,0,1600,900,ink);Header();Portrait(-1,80,210,350,570,true);
            Label(495,135,1030,52,world.phase==Phase.Announcement?"이번 장의 규칙":world.phase==Phase.FinalHearing?"네 자리의 계약 심리":world.phase==Phase.Loop?"새벽의 종소리":world.phase==Phase.PlayerOut?"당신의 자리가 비었습니다":"판결과 남겨진 자리",heading);
            if(world.phase==Phase.Announcement)
            {
                float y=228;
                foreach(string id in world.rules) {var r=content.Rule(id);Label(500,y,990,44,id+"  "+r.name,heading);Label(500,y+60,990,112,r.description);Label(500,y+178,990,42,r.conditions+" · 이번 장 종료 시 권한 만료",muted);y+=240;}
                Label(500,700,1000,95,"기본 정산: 정답이면 범인 퇴장. 오답이면 범인은 탈출하고 무고자 1명이 퇴장합니다.\n5명에서 마지막 세 자리를 보존하며, 4명은 최종 심리로 전환됩니다.\n투표 동률: 1회 재투표 후에도 같으면 동률 후보 중 공개 추첨합니다.",small);
                if(Button(1040,810,490,58,"확인했습니다. 일상 시작  →")) {GameFlow.StartDaily(world);Save(false);}
            }
            else if(world.phase==Phase.Verdict)
            {
                Label(500,227,990,200,world.trial.verdict,heading);
                float y=456;Label(500,y,1000,35,world.HasRule("A9")?"최종 득표와 익명 해명":"최종 득표 · 기본 익명 투표",small);y+=44;
                logScroll=GUI.BeginScrollView(new Rect(500,y,1010,240),logScroll,new Rect(0,0,975,world.trial.ballots.Count*112));
                var tally=world.trial.ballots.GroupBy(b=>b.target).OrderByDescending(g=>g.Count()).ToList();
                for(int i=0;i<tally.Count;i++)Label(0,i*38,960,35,content.Person(tally[i].Key).name+" · "+tally[i].Count()+"표",small);
                var anonymousReasons=world.trial.ballots.Select(b=>b.reason).OrderBy(r=>r,StringComparer.Ordinal).ToList();
                if(world.HasRule("A9")) for(int i=0;i<anonymousReasons.Count;i++)Label(0,tally.Count*38+i*70,960,65,"익명 해명: "+anonymousReasons[i],muted);
                GUI.EndScrollView();
                if(Button(1060,814,470,58,"남겨진 기록을 안고 계속  →")) {GameFlow.Continue(world,content);Save(false);}
            }
            else if(world.phase==Phase.FinalHearing)
            {
                Label(500,235,960,95,"자발적 계약 포기 · 임시 시나리오",heading);
                Label(500,363,960,260,content.Person(world.hearingVolunteer).name+"이 계약 보상을 포기하고 자신의 퇴장을 요청했습니다.\n\n새로운 범죄나 계약 위반은 만들어내지 않습니다. 공개된 신청과 당사자의 의사를 확인하는 심리입니다. 정식 최종 심리의 사건과 대사는 추후 제작할 콘텐츠입니다.");
                if(Button(890,801,640,62,"신청 기록과 의사를 확인하고 심리 종료")){GameFlow.ConcludeHearing(world,content);Save(false);}
            }
            else
            {
                Label(500,247,930,150,world.phase==Phase.Loop?"세 사람만이 남았습니다.\n저택은 다시 입장일을 맞습니다.":"이번 삶에서 더는 발언할 수 없습니다.\n기록을 남기고 새로운 회차를 시작할 수 있습니다.",heading);
                Label(500,447,930,160,"플레이어의 이전 재판 기록은 보존됩니다.\n다른 참가자들의 기억, 감정, 관계와 계약은 처음으로 돌아갑니다.");
                if(Button(930,805,600,62,"기록을 남기고 입장일로  →")){world=GameFlow.ResetLoop(world,content);first=second=-1;mansion.RenderRoom(world,content);Save(false);}
            }
            if(Button(45,826,320,45,"Tab  기록 열람")){notebook=true;page=world.phase==Phase.Verdict?4:3;}
        }
        void Trial()
        {
            Box(0,0,1600,900,ink);Header();
            var last=world.trial.speeches.LastOrDefault();int speaker=last==null?-1:last.speaker;
            for(int i=0;i<18;i++)
            {
                float x=42+i*84;Portrait(i,x,103,74,104,speaker==i);Label(x,212,82,30,content.Person(i).name,small);
                if(world.Person(i).alive && i!=0 && GUI.Button(new Rect(x,103,74,140),GUIContent.none,GUIStyle.none))target=i;
            }
            Box(43,252,1510,2,new Color(.2f,.28f,.3f));Portrait(speaker,62,284,330,425,true);
            Label(422,281,940,54,speaker<0?"관장":content.Person(speaker).name,heading);
            Label(423,342,1080,36,last==null?"":ActName(last.act)+"   /   "+(last.reason??""),muted);
            var speechStyle=new GUIStyle(text){fontSize=25};string spoken=last==null?"":last.text;
            float speechHeight=Mathf.Max(205,speechStyle.CalcHeight(new GUIContent(spoken),1030));
            speechScroll=GUI.BeginScrollView(new Rect(422,397,1065,220),speechScroll,new Rect(0,0,1030,speechHeight));Label(0,0,1030,speechHeight,spoken,speechStyle);GUI.EndScrollView();
            Label(423,632,1015,55,"개입 대상: "+content.Person(target).name+"   ·   논점 "+(world.trial.topic+1)+"   ·   발언 "+world.trial.turn+" / "+DialogueDirector.MaxTurns,small);
            if(Button(1300,283,236,45,autoDebate?"자동 토론  II":"자동 토론  ▶"))autoDebate=!autoDebate;
            if(Button(423,703,166,56,"질문"))message=InterruptSystem.Act(world,content,SpeechAct.Question,target);
            if(Button(599,703,166,56,"동의"))message=InterruptSystem.Act(world,content,SpeechAct.Agree,target);
            if(Button(775,703,186,56,"발언 요청"))message=InterruptSystem.Act(world,content,SpeechAct.Request,target);
            if(Button(971,703,240,56,"반박 / 논점 변경")){notebook=true;page=0;}
            if(Button(1221,703,308,56,"관찰 · 다음 발언"))DialogueDirector.Next(world,content);
            Box(0,789,1600,111,new Color(.07f,.1f,.12f));Label(46,807,1080,55,message,small);
            if(Button(46,855,265,36,"Tab  수첩 / 대화 기록")){notebook=true;page=2;}
            GUI.enabled=world.trial.turn>=3;
            if(Button(1222,809,309,61,"최종 투표  →"))
            {
                if(world.HasRule("A8") && !world.events.Any(e=>e.kind=="PreliminaryOpened" && e.id>=world.chapterEventStart))
                    foreach(var b in world.preliminary) world.Record("PreliminaryOpened",content.Person(b.voter).name+"의 사전 의심표: "+content.Person(b.target).name+" (최종 집계 제외)",b.voter,visible:true);
                votePanel=true;
            }GUI.enabled=true;
            if(votePanel) VotingPanel();
        }
        string ActName(SpeechAct act)
        {
            switch(act){case SpeechAct.Claim:return "가설 제시";case SpeechAct.Rebuttal:return "반박";case SpeechAct.Reveal:return "자료 공개";case SpeechAct.Defend:return "방어";case SpeechAct.Mediate:return "중재";case SpeechAct.Withhold:return "공개 보류";case SpeechAct.Request:return "발언 요청";case SpeechAct.Agree:return "동의";case SpeechAct.ChangeTopic:return "논점 변경";default:return "질문";}
        }
        void VotingPanel()
        {
            Box(185,200,1230,590,new Color(.075f,.11f,.14f));Label(226,227,1120,55,world.trial.tieRound>0?"동률 재투표 · 다시 동률이면 공개 추첨":"최종 투표",heading);
            Label(227,290,1120,50,world.disenfranchised.Contains(0)?"이번 장에는 민혁의 투표권이 없습니다. 선택 버튼으로 다른 참가자들의 집계를 진행합니다.":"결론을 선택하세요. 다른 참가자들은 자신의 지식과 관계에 따라 독립적으로 투표합니다.",small);
            var people=world.Living.Where(x=>world.trial.tieCandidates.Count==0 || world.trial.tieCandidates.Contains(x.id)).ToList();
            for(int i=0;i<people.Count;i++) if(Button(230+i%4*280,367+i/4*67,266,54,content.Person(people[i].id).name))
            {VotingSystem.Cast(world,content,people[i].id);votePanel=world.phase==Phase.Trial;Save(false);break;}
            if(Button(1110,728,250,44,"토론으로 돌아가기"))votePanel=false;
        }
        void PreliminaryPanel()
        {
            Box(170,210,1260,560,new Color(.045f,.08f,.1f));Label(219,238,1120,56,"A8 · 조사 전 의심표를 봉인합니다",heading);
            Label(220,312,1120,70,"현재 의심하는 한 사람을 선택하세요. 이 표는 최종 투표 전에 공개되며 집계에는 포함되지 않습니다.\n조사 뒤에는 다른 사람에게 투표할 수 있습니다.",small);
            var people=world.Living;
            for(int i=0;i<people.Count;i++)if(Button(220+i%4*283,401+i/4*66,268,55,content.Person(people[i].id).name))
            {var ballot=world.preliminary.Single(b=>b.voter==0);ballot.target=people[i].id;ballot.reason="플레이어가 조사 전에 제출한 의심표";world.preliminaryConfirmed=true;Save(false);}
        }
        void Notebook()
        {
            Box(18,90,1564,796,new Color(.035f,.059f,.075f));Box(18,90,1564,3,teal);
            Label(48,111,560,56,"민혁의 조사수첩",heading);
            if(world.phase==Phase.Investigation && !PreliminaryPending && Button(1060,112,295,45,"T  조사를 마치고 재판")) BeginTrial();
            string[] names={"단서 연결","참가자","발언 · 기록","지도 · 규칙","이전 회차"};
            for(int i=0;i<names.Length;i++) if(Button(50+i*258,181,246,47,names[i],page==i)){page=i;scroll=detailScroll=logScroll=Vector2.zero;}
            if(Button(1380,112,163,45,"닫기  ×"))notebook=false;
            if(page==0) EvidencePage();else if(page==1) PeoplePage();else if(page==2) LogPage();else if(page==3) RulesPage();else ArchivePage();
            Label(49,851,1480,30,message,small);
        }
        Evidence[] Known() {return world.incident==null?Array.Empty<Evidence>():world.incident.evidence.Where(e=>world.Person(0).Knows(e.id)).ToArray();}
        void EvidencePage()
        {
            var known=Known();
            Label(54,250,450,32,"확보한 단서  "+known.Length,small);
            if(known.Length==0){Label(55,325,1250,110,"아직 조사한 자료가 없습니다.\n사건 후 현장의 자료와 기록 보관실, 도서실, 작업실을 살피고 참가자에게 증언을 요청하세요.");return;}
            selectedEvidence=Mathf.Clamp(selectedEvidence,0,known.Length-1);
            scroll=GUI.BeginScrollView(new Rect(51,301,440,516),scroll,new Rect(0,0,411,known.Length*76));
            for(int i=0;i<known.Length;i++) if(Button(0,i*76,405,66,(known[i].kind==EvidenceKind.Testimony?"진술  ":"자료  ")+known[i].title,i==selectedEvidence))selectedEvidence=i;
            GUI.EndScrollView();
            var selected=known[selectedEvidence];Label(533,253,970,53,selected.title,heading);
            bool verified=world.Person(0).knowledge.Any(k=>k.evidenceId==selected.id && k.verified);
            Label(535,315,975,33,(selected.kind==EvidenceKind.Testimony?"개인 진술 · 틀리거나 숨긴 내용이 있을 수 있음":verified?"원본 확인 · 공식 기록과 물적 흔적":"전언 · 원본 확인 전")+"   /   "+world.rooms[selected.room].name,muted);
            detailScroll=GUI.BeginScrollView(new Rect(535,365,980,230),detailScroll,new Rect(0,0,945,Mathf.Max(220,text.CalcHeight(new GUIContent(selected.text),940))));Label(0,0,940,1000,selected.text);GUI.EndScrollView();
            if(Button(535,612,440,44,"연결 A에 놓기"))first=selectedEvidence;
            if(Button(993,612,514,44,"연결 B에 놓기"))second=selectedEvidence;
            Label(535,670,967,40,"A: "+(first>=0&&first<known.Length?known[first].title:"미선택")+"    ↔    B: "+(second>=0&&second<known.Length?known[second].title:"미선택"),small);
            string[] types={"뒷받침","모순","출처","가설 배제"};
            for(int i=0;i<types.Length;i++)if(Button(535+i*243,711,232,42,types[i],linkType==i))linkType=i;
            if(Button(535,771,971,56,world.phase==Phase.Trial?(linkType==1?"이 모순으로 반박하기":"이 연결로 새 논점 제시"):"두 단서의 관계 검토"))
            {
                if(first<0||second<0||first>=known.Length||second>=known.Length){message="단서 A와 B를 선택하세요.";return;}
                var link=EvidenceGraph.Connect(world.incident,known[first].id,known[second].id,(LinkKind)linkType);
                if(world.phase==Phase.Trial)
                {message=InterruptSystem.Act(world,content,linkType==1?SpeechAct.Rebuttal:SpeechAct.ChangeTopic,target,known[first].id,known[second].id,(LinkKind)linkType);if(link!=null)notebook=false;}
                else message=link==null?"이 연결을 입증할 근거가 부족합니다.":link.explanation;
            }
        }
        void PeoplePage()
        {
            for(int i=0;i<18;i++) if(Button(52+i%3*155,257+i/3*81,146,68,content.Person(i).name+(world.Person(i).alive?"":"\n빈 자리"),selectedPerson==i)) selectedPerson=i;
            var d=content.Person(selectedPerson);Portrait(selectedPerson,558,270,260,445);
            Label(859,267,650,55,d.name,heading);Label(861,334,650,45,d.age+"세 · "+d.gender+" · "+d.job,small);
            Label(861,402,620,111,d.tagline);Label(861,527,620,100,"“"+d.quote+"”");
            var relationship=world.Person(selectedPerson).Relation(0);
            Label(860,693,650,110,"민혁에 대한 태도: "+(relationship.trust>15?"신뢰가 쌓이고 있다":relationship.trust<0?"경계하고 있다":"아직 알아가는 중")+"\n"+(world.Person(selectedPerson).alive?"현재 살아 있는 참가자":"남겨진 좌석"),small);
        }
        void LogPage()
        {
            var entries=world.events.Where(e=>e.publicEvent && e.id>=world.chapterEventStart).Reverse().ToList();
            Label(53,255,1420,33,"직접 들었거나 공동으로 공개된 기록만 표시합니다. NPC의 비공개 지식과 실제 범인은 표시하지 않습니다.",muted);
            logScroll=GUI.BeginScrollView(new Rect(52,307,1483,518),logScroll,new Rect(0,0,1440,entries.Sum(e=>Mathf.Max(70,small.CalcHeight(new GUIContent(e.text),1290)+24))));float y=0;
            foreach(var e in entries){float h=Mathf.Max(70,small.CalcHeight(new GUIContent(e.text),1290)+24);Label(0,y,95,36,(8+e.tick/60).ToString("00")+":"+(e.tick%60).ToString("00"),muted);Label(108,y,1290,h,e.text,small);Box(108,y+h-12,1290,1,new Color(.15f,.2f,.23f));y+=h;}GUI.EndScrollView();
        }
        void RulesPage()
        {
            Label(55,253,715,45,"연결된 저택",heading);var room=world.rooms[world.Person(0).room];Label(56,310,700,40,"현재: "+room.name,small);
            float h=world.rooms.Count*40;scroll=GUI.BeginScrollView(new Rect(53,362,720,466),scroll,new Rect(0,0,683,h));
            for(int i=0;i<world.rooms.Count;i++){var r=world.rooms[i];Label(0,i*40,185,36,r.name,small);Label(190,i*40,490,36,string.Join(" · ",r.links.Select(id=>world.rooms[id].name)),muted);}GUI.EndScrollView();
            Label(835,253,690,45,"공표된 규칙",heading);float y=321;
            foreach(string id in world.rules){var r=content.Rule(id);Label(837,y,665,47,id+"  "+r.name,heading);Label(837,y+60,665,126,r.description,small);y+=206;}
            Label(837,747,662,90,"공식 시설 기록은 위조되지 않습니다.\n각 단서는 기록이 관찰한 시각과 범위만 증명합니다.\n규칙 카탈로그: 기존 40개 중 12개 작동 / 신규 12개는 제안",muted);
        }
        void ArchivePage()
        {
            Label(55,255,1420,44,"이전 재판의 기록 · NPC에게 자동으로 공유되지 않습니다.",small);
            scroll=GUI.BeginScrollView(new Rect(54,314,1470,510),scroll,new Rect(0,0,1420,Mathf.Max(490,world.archive.Count*230)));
            if(world.archive.Count==0) Label(0,45,1350,100,"아직 끝난 재판이 없습니다. 판결이 내려지면 기록을 남깁니다.");
            for(int i=0;i<world.archive.Count;i++){var a=world.archive[i];Label(0,i*230,1400,50,"회차 "+a.loop+" · "+a.chapter+"장 — "+a.title,heading);Label(0,i*230+61,1400,160,a.verdict,small);}GUI.EndScrollView();
        }
        void PauseMenu()
        {
            Box(470,224,660,473,new Color(.035f,.06f,.075f));Label(520,259,550,60,"잠시 멈춘 저택",heading);
            if(Button(520,354,560,57,"계속하기"))pause=false;
            if(Button(520,426,560,57,"현재 기록 저장"))Save();
            if(Button(520,498,560,57,"저장된 기록 불러오기"))Load();
            if(Button(520,570,560,57,"저장하고 종료")){Save(false);Application.Quit();}
        }
        IEnumerator QaCapture(string[] args)
        {
            int index=Array.IndexOf(args,"--qa");string destination=index+1<args.Length?args[index+1]:Path.Combine(Application.dataPath,"../QA");Directory.CreateDirectory(destination);
            yield return new WaitForSeconds(2);ScreenCapture.CaptureScreenshot(Path.Combine(destination,"01-title.png"));yield return new WaitForSeconds(1);
            NewGame();GameFlow.StartDaily(world);yield return new WaitForSeconds(1);ScreenCapture.CaptureScreenshot(Path.Combine(destination,"02-explore.png"));yield return new WaitForSeconds(1);
            for(int tries=0;tries<20;tries++)
            {
                world=WorldFactory.Create(content,1709+tries);GameFlow.StartDaily(world);
                for(int t=0;t<2000 && world.incident==null;t++)WorldSimulation.Advance(world,content);
                if(world.phase==Phase.Investigation)break;
            }
            if(world.phase!=Phase.Investigation)throw new InvalidOperationException("QA case generation failed");
            foreach(var e in world.incident.evidence.Where(e=>e.kind!=EvidenceKind.Testimony))KnowledgeSystem.Learn(world,0,e.id,0,true);
            mansion.RenderRoom(world,content);notebook=true;page=0;yield return new WaitForSeconds(1);ScreenCapture.CaptureScreenshot(Path.Combine(destination,"03-notebook.png"));yield return new WaitForSeconds(1);
            notebook=false;GameFlow.StartTrial(world,content);autoDebate=false;target=world.Living.First(p=>p.id!=0).id;
            var clues=world.incident.evidence.Where(e=>e.route=="B").ToArray();InterruptSystem.Act(world,content,SpeechAct.ChangeTopic,target,clues[0].id,clues[1].id,LinkKind.Supports);
            yield return new WaitForSeconds(1);ScreenCapture.CaptureScreenshot(Path.Combine(destination,"04-trial.png"));yield return new WaitForSeconds(1);
            VotingSystem.Resolve(world,content,world.incident.culprit);yield return new WaitForSeconds(1);ScreenCapture.CaptureScreenshot(Path.Combine(destination,"05-verdict.png"));yield return new WaitForSeconds(1);
            Save(false);var saved=JsonUtility.ToJson(world);Load();if(saved!=JsonUtility.ToJson(world))throw new InvalidOperationException("Unity JSON save/load changed state");
            File.WriteAllText(Path.Combine(destination,"smoke-test.txt"),"PASS: title, first-person world, evidence notebook, public argument, adjudication, Unity JsonUtility save/load.\n");Application.Quit(0);
        }
    }
}
