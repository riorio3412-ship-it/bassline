using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using BASSLINE.AuthoringData;

namespace BASSLINE.Bootstrap
{
    /// <summary>
    /// Explicit standalone visual probe, never installed in an ordinary game session.
    /// Run a graphics-enabled player with -bassline-mansion-smoke -bassline-output ABSOLUTE_DIRECTORY.
    /// The receipt distinguishes ordinary physical locomotion from the later TestOnly dialogue framing.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed partial class MansionSmokeProbe : MonoBehaviour
    {
        const string Flag = "-bassline-mansion-smoke";
        const string ControlsKey = "BASSLINE.Controls.v2";
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        string outputDirectory, runId, previousControls;
        bool hadControls, preferencesBackedUp, cleanedUp, previousBackground, previousAutomatic;
        int previousVsync, previousTargetFrameRate;
        MansionRuntime runtime;
        MonoBehaviour hud;
        FixtureActorBody player;
        Receipt receipt;
        readonly List<CaptureReceipt> captures = new List<CaptureReceipt>();
        readonly List<PerformanceReceipt> performance = new List<PerformanceReceipt>();
        readonly List<ButtonReceipt> buttons = new List<ButtonReceipt>();
        readonly List<PlacementReceipt> placements = new List<PlacementReceipt>();
        readonly List<string> runtimeErrors = new List<string>();
        int[] movementPath = Array.Empty<int>();
        int movementCursor;
        bool driveWorld, driveMovement;
        double tickRemainder;
        Vector3 movementStart, previousPhysicalPosition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), Flag) < 0 && Array.IndexOf(Environment.GetCommandLineArgs(), SpaceFlag) < 0) return;
            if (FindAnyObjectByType<MansionSmokeProbe>()) return;
            var probe = new GameObject("TestOnly_MansionVisualSmoke").AddComponent<MansionSmokeProbe>();
            DontDestroyOnLoad(probe.gameObject);
        }

        IEnumerator Start()
        {
            runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
            outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Verification", "MansionSmoke", runId));
            receipt = new Receipt { Status = "RUNNING", RunId = runId, UnityVersion = Application.unityVersion,
                Platform = Application.platform.ToString(), GraphicsDevice = SystemInfo.graphicsDeviceName,
                GraphicsApi = SystemInfo.graphicsDeviceType.ToString(), Cpu = SystemInfo.processorType,
                Scope = "Standalone rendered Game View smoke; NOT full progression, route coverage, final art approval, or a performance benchmark.",
                MovementMethod = "SetMove/SetRun -> actual MansionRuntime.AdvanceOne -> CharacterController.Move; real elapsed-frame 60 Hz stepping in probe LateUpdate.",
                DialogueMethod = "TestOnly player framing near a living resident, then physical Reach/LOS and FixtureHud.Primary -> own-B speech delivery. No knowledge injected." };
            Application.logMessageReceived += OnLog;
            // A manual IEnumerator runner catches failures from nested capture/wait stages as well.
            var work = new Stack<IEnumerator>(); work.Push(Run());
            while (work.Count > 0)
            {
                object yielded = null; bool moved = false; Exception failure = null;
                try { moved = work.Peek().MoveNext(); if (moved) yielded = work.Peek().Current; }
                catch (Exception e) { failure = e; }
                if (failure != null) { receipt.Status = "FAIL"; receipt.Detail = failure.ToString(); break; }
                if (!moved) { (work.Pop() as IDisposable)?.Dispose(); continue; }
                if (yielded is IEnumerator nested) { work.Push(nested); continue; }
                yield return yielded;
            }
            driveWorld = false;
            while (work.Count > 0) { try { (work.Pop() as IDisposable)?.Dispose(); } catch { /* Retain the original failure. */ } }
            try { Cleanup(); }
            catch (Exception e) { receipt.Status = "FAIL"; receipt.Detail += "\nCleanup: " + e; }
            receipt.Captures = captures.ToArray(); receipt.Performance = performance.ToArray();
            receipt.Buttons = buttons.ToArray(); receipt.TestOnlyPlacements = placements.ToArray();
            receipt.RuntimeErrors = runtimeErrors.ToArray(); receipt.FinalTick = runtime ? runtime.World.Tick : -1;
            if (runtimeErrors.Count > 0) { receipt.Status = "FAIL"; receipt.Detail += "\nRuntime logged errors; see RuntimeErrors."; }
            try { Directory.CreateDirectory(outputDirectory); File.WriteAllText(Path.Combine(outputDirectory, "mansion-smoke.json"), JsonUtility.ToJson(receipt, true)); }
            catch (Exception e) { receipt.Status = "FAIL"; Debug.LogError("Mansion smoke could not write receipt: " + e); }
            Application.logMessageReceived -= OnLog;
            Debug.Log("Mansion visual smoke " + receipt.Status + ": " + outputDirectory);
            if (!Application.isEditor) Application.Quit(receipt.Status == "PASS_SMOKE_ONLY" ? 0 : 1);
            else Destroy(gameObject);
        }

        IEnumerator Run()
        {
            var args = Environment.GetCommandLineArgs(); int outputIndex = Array.IndexOf(args, "-bassline-output");
            bool spaceReview=Array.IndexOf(args,SpaceFlag)>=0;
            if (outputIndex >= 0)
            {
                Require(outputIndex + 1 < args.Length && Path.IsPathRooted(args[outputIndex + 1]), "-bassline-output requires an absolute directory.");
                outputDirectory = Path.GetFullPath(args[outputIndex + 1]);
            }
            Directory.CreateDirectory(outputDirectory);
            receipt.OutputDirectory = outputDirectory;
            File.WriteAllText(Path.Combine(outputDirectory, "mansion-smoke.json"), JsonUtility.ToJson(receipt, true));
            Require(!Application.isEditor, "This visual probe requires a standalone player; Editor Game View resolution is not controlled here.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "A graphics-enabled standalone player is required; -nographics cannot capture Game View.");
            previousBackground = Application.runInBackground; previousVsync = QualitySettings.vSyncCount;
            previousTargetFrameRate = Application.targetFrameRate;
            hadControls = PlayerPrefs.HasKey(ControlsKey); previousControls = PlayerPrefs.GetString(ControlsKey, ""); preferencesBackedUp = !spaceReview;
            File.WriteAllText(Path.Combine(outputDirectory, "controls-before-smoke.json"), JsonUtility.ToJson(new PreferenceBackup { Existed = hadControls, Value = previousControls }, true));
            // Known starting presentation only, restored on every handled exit. Buttons subsequently apply 125% through the real settings flow.
            if(!spaceReview)PlayerPrefs.SetString(ControlsKey, "{\"UiScale\":1,\"FontScale\":1,\"HasDisplayOverride\":false,\"VSync\":1}");
            receipt.PreferencesUntouched=spaceReview;
            Application.runInBackground = true;
            yield return SceneManager.LoadSceneAsync("Mansion_Playable", LoadSceneMode.Single);
            yield return null; yield return null;
            runtime = FindAnyObjectByType<MansionRuntime>();
            Require(runtime && runtime.Layout && runtime.World != null, "Mansion_Playable runtime/layout is missing.");
            hud = FindObjectsByType<MonoBehaviour>().SingleOrDefault(x => x.GetType().FullName == "BASSLINE.UI.FixtureHud");
            Require(hud != null && ReferenceEquals(Field(hud, "Source"), runtime), "Mansion player HUD or source binding is missing.");
            player = runtime.Bodies.Single(b => b.ActorId == "CH_01");
            Require(player.Capsule && player.Capsule.enabled && !player.Capsule.isTrigger, "The real player CharacterController is unavailable.");
            Require(Camera.main && Camera.main.targetTexture == null, "Main Game View camera is missing or renders to a texture.");
            previousAutomatic = runtime.AutomaticTick; runtime.AutomaticTick = false; driveWorld = true;
#if UNITY_EDITOR || DEBUG
            runtime.UseIsolatedTestStorage();
#endif
            receipt.Scene = SceneManager.GetActiveScene().name; receipt.RoomCount = runtime.Layout.Rooms.Length;
            receipt.ResidentCount = runtime.Bodies.Length;
            receipt.Pipeline = GraphicsSettings.defaultRenderPipeline ? GraphicsSettings.defaultRenderPipeline.GetType().Name : "Built-in";
            Require(receipt.Pipeline == "UniversalRenderPipelineAsset", "Expected URP is not active.");
            Require(CurrentScreen == 38, "Expected the actual title UI_38 on scene entry.");
            if(spaceReview){yield return ReviewMansionSpace();yield break;}
            yield return CaptureAtBothResolutions("title", 38);
            Click("새로 시작"); yield return null;
            yield return CaptureAtBothResolutions("welcome",38);Click("문을 열고 들어가기");yield return null;
            Require(CurrentScreen == 1 && !runtime.World.Paused, "The actual title button did not enter live exploration.");
            receipt.TitleButtonEnteredPlay = true;
            if(ResidentMeetingsReview){yield return ReviewResidentMeetings();yield break;}
            yield return null; Require(!((Behaviour)Field(hud, "Clock")).enabled, "Exploration clock should be hidden by default.");

            var destination = runtime.Layout.Room("R_HALL");
            Require(destination != null, "Main hall room is absent.");
            movementPath = runtime.Layout.FindPath(runtime.Layout.NearestNode(player.transform.position), destination.WalkNode,
                id => { var connection = runtime.Layout.Connection(id); return !connection || string.IsNullOrEmpty(connection.DoorId) || !runtime.World.Door(connection.DoorId).Locked; });
            Require(movementPath.Length > 1, "No physical path from the real player spawn to the main hall.");
            movementStart = previousPhysicalPosition = player.transform.position;
            receipt.MovementStart = movementStart; receipt.MovementStartTick = runtime.World.Tick;
            driveMovement = true;
            yield return SetResolution(1280, 720);
            yield return MeasureFrames("physical_exploration_1280x720", 6);
            yield return Capture("exploration", 1, 1280, 720);
            yield return SetResolution(1920, 1080);
            yield return MeasureFrames("physical_exploration_1920x1080", 6);
            driveMovement = false; runtime.SetMove(0, 0); runtime.SetRun(false);
            yield return null;
            receipt.MovementEnd = player.transform.position; receipt.MovementEndTick = runtime.World.Tick;
            receipt.MovementDisplacement = Vector3.Distance(movementStart, player.transform.position);
            receipt.MovementReachedHall = runtime.Layout.RoomAt(player.transform.position)?.RoomId == "R_HALL";
            receipt.MovementPathNodesReached = movementCursor; receipt.MovementPathNodeCount = movementPath.Length;
            Require(receipt.MovementDisplacement > 1 && receipt.MovementEndTick > receipt.MovementStartTick + 60,
                "Physical movement did not advance sufficiently; capture is not a locomotion pass.");
            Require(PlayerCapsuleClear(player.transform.position), "Physical movement ended overlapping a fixed obstacle.");
            receipt.PhysicalMovementVerified = true;
            yield return Capture("exploration", 1, 1920, 1080);

            if(Array.IndexOf(args,"-bassline-startup-only")>=0){
                receipt.Scope="Standalone title, new-game entry and real physical movement only. No full feature, story, final art or save compatibility claim.";
                if(Array.IndexOf(args,"-bassline-entry-interaction")>=0){
                    driveWorld=false;
                    string nearby=FramePlayerNearResident();
                    yield return null;yield return null;
                    var hint=Field(hud,"InteractionHint");
                    string hintText=(string)hint.GetType().GetProperty("text").GetValue(hint);
                    Require(hintText.Contains("말 걸기")&&hintText.Contains("["),"Context prompt did not display its primary key and conversation action: "+hintText);
                    var backing=(UnityEngine.UI.Image)Field(hud,"InteractionBackdrop");
                    Require(backing&&backing.enabled,"Context prompt backing is absent.");
                    yield return Capture("interaction_hint_testonly",1,1920,1080);
                    Call(hud,"Primary",nearby);driveWorld=true;
                    Require(CurrentScreen==3,"Conversation did not open from the actual interaction action.");
                    for(int t=0;t<1200&&runtime.ReadConversationPlayback().Speaking;t++){runtime.AdvanceOne();if(t%30==0)yield return null;}
                    Require(runtime.ReadConversationPlayback().Phase=="Reading","Conversation did not finish into readable choices.");
                    yield return Capture("conversation_testonly",3,1920,1080);
                    receipt.Scope+=" Also checked contextual interaction key/backing and one actual resident conversation using explicitly test-only nearby player framing.";
                }
                if(Array.IndexOf(args,"-bassline-family-review")>=0)yield return ReviewFamilyDispute();
                if(Array.IndexOf(args,"-bassline-weapon-review")>=0)yield return ReviewWeapons();
                if(Array.IndexOf(args,"-bassline-weapon-residue-review")>=0)yield return ReviewWeaponResidue();
                if(Array.IndexOf(args,"-bassline-player-attack-review")>=0)yield return ReviewPlayerAttack();
                if(Array.IndexOf(args,"-bassline-incident-dialogue-review")>=0)yield return ReviewIncidentDialogue();
                if(Array.IndexOf(args,"-bassline-trial-picker-review")>=0){
                    for(int attempt=0;attempt<12&&!runtime.Knowledge.For("CH_01").Records().Any(r=>r.Direct&&r.Predicate=="AtPlace"&&r.IdentityConfirmed&&r.SubjectId.StartsWith("CH_",StringComparison.Ordinal));attempt++){
                        FramePlayerNearResident("",true);
                        for(int t=0;t<10;t++){runtime.AdvanceOne();yield return null;}
                    }
                    yield return ReviewTrialChoices();
                    receipt.Scope+=" Also reviewed the evidence list and real resolver submission using an explicitly test-only court UI fixture; not natural incident or court progression.";
                }
                receipt.Status="PASS_SMOKE_ONLY";receipt.Detail="Title/new-game entry and physical exploration started successfully; requested scoped interaction checks passed.";
                yield break;
            }

            Call(hud, "ToggleSettings"); Require(CurrentScreen == 38, "Pause menu did not open.");
            Click("설정"); Require(CurrentScreen == 39, "Settings button did not open UI_39.");
            // These are the same live UnityEvent listeners invoked by pointer/keyboard activation.
            for (int i = 0; i < 3; i++) Click("범주 변경");
            for (int i = 0; i < 5; i++) Click("값 +");
            receipt.UiScale = UiScale;
            Require(Mathf.Abs(receipt.UiScale - 1.25f) < .001f, "The real settings buttons did not apply 125% UI scale.");
            yield return CaptureAtBothResolutions("settings_scale125", 39);
            Click("뒤로"); Click("계속하기"); Require(CurrentScreen == 1, "Settings return route failed.");

            Call(hud, "ToggleNote"); Require(CurrentScreen == 7 && runtime.ReadPlayer().NotePause, "NOTE did not acquire the live pause token.");
            long noteTick = runtime.World.Tick;
            yield return CaptureAtBothResolutions("note_scale125", 7);
            Require(runtime.World.Tick == noteTick, "NOTE captures advanced the paused world.");
            receipt.NotePauseVerified = true;
            string archiveKnowledge = JsonUtility.ToJson(runtime.Knowledge.Capture());
            Click("지난 회차"); Require(CurrentScreen == 13, "Notebook archive action did not open UI_13.");
            Require(runtime.ReadArchive().Cases.Count == 0, "New session should have no completed archive cases.");
            yield return CaptureAtBothResolutions("archive_empty_scale125", 13);
            Require(runtime.World.Tick == noteTick && JsonUtility.ToJson(runtime.Knowledge.Capture()) == archiveKnowledge,
                "Reading the archive changed world time or personal knowledge.");
            Click("뒤로"); Call(hud, "ToggleNote"); Require(CurrentScreen == 7, "Archive return route failed.");
            Click("지도"); Require(CurrentScreen == 11, "NOTE map button did not open MAP.");
            var observedMap=runtime.ReadMap();
            Require(observedMap.Owner=="CH_01"&&!observedMap.HasPublishedPlan,"Map must use player-owned observations without an unreceived plan.");
            Require(observedMap.Places.Count>0&&observedMap.Places.Count<runtime.Layout.Rooms.Length,"Physical exploration did not produce a limited known map.");
            receipt.MapKnownPlaces=observedMap.Places.Count;
            receipt.MapKnownLayers=observedMap.Places.Select(p=>p.Version+"/"+p.Floor).Concat(observedMap.Doors.Select(d=>d.Version+"/"+d.Floor)).Distinct().Count();
            Click("확대");Click("축소");
            if(receipt.MapKnownLayers>1){Click("다음 층");Click("이전 층");receipt.MapMultipleFloorControlsVerified=true;}
            Click("평면 보기");Click("입체 보기");Click("내 위치");Click("다음 표시");
            receipt.MapKnowledgeAndControlsVerified=true;
            yield return CaptureAtBothResolutions("map_scale125", 11);
            Click("뒤로"); Require(CurrentScreen == 1 && !runtime.World.Paused, "MAP return route failed.");

            Call(hud,"ToggleNote");Click("약속");Require(CurrentScreen==12,"Schedule did not open");
            yield return CaptureAtBothResolutions("schedule_waiting_scale125",12);
            Click("5분 보내기");Require(CurrentScreen==1&&runtime.IsWaiting&&!runtime.World.Paused,"Wait button did not resume the world and begin waiting");
            long waitStart=runtime.World.Tick;
            for(int i=0;i<12;i++){runtime.AdvanceWaitingBatch(60);yield return null;}
            Require(runtime.World.Tick>waitStart,"Waiting did not advance ordinary world ticks");
            yield return CaptureAtBothResolutions("waiting_scale125",1);
            yield return MeasureWaitingAcceleration();
            runtime.StopWaiting("화면 검사를 위해 기다리기를 멈췄어요.");Require(!runtime.IsWaiting,"Waiting did not stop");
            receipt.WaitingScreensVerified=true;

            bool jinwooReview=Array.IndexOf(Environment.GetCommandLineArgs(),"-bassline-jinwoo-art-review")>=0;
            string residentId="";
            for(int attempt=0;attempt<=30;attempt++){
                try{residentId=FramePlayerNearResident(jinwooReview?"CH_02":"CH_04");}
                catch(InvalidOperationException e) when(e.Message.StartsWith("No clear, physically reachable resident framing position")){}
                if(residentId!=""||attempt==30)break;
                for(int i=0;i<60;i++)runtime.AdvanceOne();yield return null;
            }
            Require(residentId!="","No physical dialogue encounter after waiting for the resident's real movement.");
            if(jinwooReview){
                var actor=runtime.Bodies.Single(b=>b.ActorId=="CH_02");
                Require(actor.GetComponentsInChildren<MeshFilter>().Any(f=>f.sharedMesh&&f.sharedMesh.name=="J19_Trouser_-1"),"Jinwoo individual model is absent from the actual scene.");
                Require(actor.GetComponentsInChildren<SkinnedMeshRenderer>().Count(r=>r.sharedMesh&&r.sharedMesh.name.StartsWith("J20_SkinnedSleeve_"))==2,"Continuous skinned sleeves are absent from the player.");
                yield return CaptureAtBothResolutions("jinwoo_world_model_testonly",1);
                bool previousDrive=driveWorld;driveWorld=false;
                var arms=actor.GetComponentsInChildren<ActorArmRig>();var bones=arms.SelectMany(a=>new[]{a.Upper,a.Forearm,a.Wrist,a.ElbowClothingGuide}).Where(b=>b).ToArray();var rotations=bones.Select(b=>b.localRotation).ToArray();var positions=bones.Select(b=>b.localPosition).ToArray();
                try{
                    foreach(var arm in arms)arm.Pose(actor.transform.TransformPoint(new Vector3(arm.Side*.22f,actor.Height*.72f,.29f)),actor.transform.rotation);
                    yield return CaptureAtBothResolutions("jinwoo_sleeve_pose_testonly",1);
                    receipt.JinwooSkinPoseRendered=true;
                }finally{for(int i=0;i<bones.Length;i++){bones[i].localRotation=rotations[i];bones[i].localPosition=positions[i];}driveWorld=previousDrive;}
            }
            int before = runtime.Knowledge.For("CH_01").Records().Length;
            Call(hud, "Primary", residentId);
            Require(CurrentScreen == 3 && runtime.DescribeTarget(residentId).Available, "Physical dialogue interaction did not open UI_03.");
            for(int speechTick=0;speechTick<1200&&runtime.ReadConversationPlayback().Speaking;speechTick++){runtime.AdvanceOne();if(speechTick%30==0)yield return null;}
            Require(runtime.ReadConversationPlayback().Phase=="Reading","Speech did not reach a stable reading pause: "+runtime.ReadConversationPlayback().Phase+" / "+runtime.ReadConversationPlayback().EndReason+"; player="+player.transform.position+" speaker="+runtime.Bodies.Single(b=>b.ActorId==residentId).transform.position);
            var received = runtime.Knowledge.For("CH_01").Records().Where(r => r.Source == residentId && r.Kind == "Statement").OrderByDescending(r => r.ReceivedTick).FirstOrDefault();
            Require(received != null && runtime.Knowledge.For("CH_01").Records().Length > before, "Dialogue produced no speech receipt in the player's own B ledger.");
            receipt.DialogueActor = residentId; receipt.DialogueReceiptId = received.Id; receipt.DialogueSource = received.Source;
            receipt.DialoguePhysicalReach = true; receipt.DialoguePlayerLedgerVerified = true;
            for(int portraitFrame=0;portraitFrame<5;portraitFrame++)yield return null;
            VerifyPortrait();
            if(jinwooReview){
                var portrait=Field(hud,"portrait");var texture=Field(portrait,"texture") as Texture2D;
                Require(texture&&texture.name=="CH_02_v019","Jinwoo dialogue did not use the corrected standing sprite.");
                var pixels=texture.GetPixels32();Require(pixels.Count(p=>p.a<8)>pixels.Length*.4f&&pixels.Count(p=>p.a>240)>pixels.Length*.15f,"Jinwoo standing sprite alpha is invalid.");
                receipt.JinwooArtConnected=true;
            }
            Call(ActiveView(3), "RevealDialogue");
            yield return CaptureAtBothResolutions("dialogue_scale125_testonly_framing", 3);
            Require(runtime.ReceivesSpeech("CH_01",residentId), "Resident became inaudible before the final dialogue capture.");
            // Freeze only the explicit layout review so screenshot timing cannot move the staged speaker away.
            driveWorld=false;string beforeSharePreview=JsonUtility.ToJson(runtime.Knowledge.Capture());
            Click("다른 이야기");yield return CaptureAtBothResolutions("dialogue_choices_scale125_testonly_frozen",3);
            Click("내가 본 일 말하기");
            var storyRows=(Array)ActiveView(5).GetType().GetProperty("RecordRowButtons").GetValue(ActiveView(5));
            Require(storyRows.Length>0,"No personally known story was available to preview.");
            var rowEvent=storyRows.GetValue(0).GetType().GetProperty("onClick").GetValue(storyRows.GetValue(0));Call(rowEvent,"Invoke");
            yield return CaptureAtBothResolutions("dialogue_share_preview_scale125_testonly_frozen",5);
            Require(JsonUtility.ToJson(runtime.Knowledge.Capture())==beforeSharePreview,"Share preview sent a record without choosing to send it.");
            Click("말하지 않고 돌아가기");Click("처음 이야기로");driveWorld=true;
            if(AppointmentReview)yield return ReviewAppointmentConversation();
            Click("그만 이야기하기"); Require(CurrentScreen == 1, "Dialogue close button did not return to exploration.");
            if(AppointmentCardReview)yield return ReviewAppointmentCard();
            Call(hud,"OpenNotePage",9);yield return CaptureAtBothResolutions("people_scale125",9);Call(hud,"Back");
            Call(hud,"OpenNotePage",10);yield return CaptureAtBothResolutions("records_scale125",10);Call(hud,"Back");
            Call(hud,"Open",38);Click("저장/로드");yield return CaptureAtBothResolutions("save_slots_scale125",40);Call(hud,"Back");Call(hud,"Back");
            yield return ReviewLoanExchange();
            yield return ReviewTrialChoices();
            var expectedStages=new[]{"title","welcome","exploration","settings_scale125","note_scale125","archive_empty_scale125","map_scale125","schedule_waiting_scale125","waiting_scale125","dialogue_scale125_testonly_framing","dialogue_choices_scale125_testonly_frozen","dialogue_share_preview_scale125_testonly_frozen","people_scale125","records_scale125","save_slots_scale125","loan_permission_scale125_testonly","loan_handoff_midpoint_testonly_frozen","loan_held_firstperson_testonly","loan_returned_notes_scale125_testonly","trial_evidence_testonly","trial_choices_first_testonly","trial_choices_other_testonly"};
            if(AppointmentCardReview)expectedStages=expectedStages.Concat(appointmentCardStages).ToArray();
            if(AppointmentReview)expectedStages=expectedStages.Concat(appointmentStages).ToArray();
            if(LoanDiscussionReview)expectedStages=expectedStages.Concat(discussionStages).ToArray();
            if(PrivateNoteReview)expectedStages=expectedStages.Concat(privateNoteStages).ToArray();
            if(jinwooReview)expectedStages=expectedStages.Concat(new[]{"jinwoo_world_model_testonly","jinwoo_sleeve_pose_testonly"}).ToArray();
            if(MinseoArtReview){Require(LoanDiscussionReview,"Minseo art review requires the loan discussion review.");expectedStages=expectedStages.Concat(new[]{"minseo_world_model_testonly"}).ToArray();}
            Require(captures.Count==expectedStages.Length*2&&captures.All(c=>c.PngVerified)&&expectedStages.All(name=>captures.Count(c=>c.Name==name&&c.PngWidth==1280&&c.PngHeight==720)==1&&captures.Count(c=>c.Name==name&&c.PngWidth==1920&&c.PngHeight==1080)==1),"One or more required scene/UI stages or resolutions are missing.");
            receipt.Status = "PASS_SMOKE_ONLY";
        }

        IEnumerator MeasureWaitingAcceleration()
        {
            bool previousDrive=driveWorld;driveWorld=false;
            long startTick=runtime.World.Tick;double began=Time.realtimeSinceStartupAsDouble;
            while(runtime.IsWaiting&&Time.realtimeSinceStartupAsDouble-began<5){runtime.AdvanceWaitingBatch();yield return null;}
            receipt.WaitingMeasuredTicks=runtime.World.Tick-startTick;
            receipt.WaitingMeasuredSeconds=Time.realtimeSinceStartupAsDouble-began;
            receipt.WaitingWorldSecondsPerSecond=receipt.WaitingMeasuredSeconds>0?receipt.WaitingMeasuredTicks/60d/receipt.WaitingMeasuredSeconds:0;
            driveWorld=previousDrive;
        }

        IEnumerator ReviewTrialChoices()
        {
#if UNITY_EDITOR || DEBUG
            driveWorld=false;
            string before=JsonUtility.ToJson(runtime.Knowledge.Capture());long tick=runtime.World.Tick;
            string evidenceId=runtime.ConfigureTrialUiReview();
            receipt.TrialUiFixture="TestOnly public-utterance fixture using one already owned observation. Bypasses physical court admission for screen review only; not a progression result.";
            Call(hud,"Open",23);
            var labels=(Array)Field(ActiveView(23),"ActionLabels");
            string focus=labels.Cast<Component>().Select(t=>(string)t.GetType().GetProperty("text").GetValue(t)).Single(t=>t.EndsWith(" 포커스",StringComparison.Ordinal));
            Click(focus);Click("이 말을 뒷받침할 단서가 있어");
            Click("이전 관측도 보기");
            var owned=runtime.ReadNotebook().Records.OrderByDescending(r=>r.ReceivedTick).ToArray();int index=Array.FindIndex(owned,r=>r.Id==evidenceId);
            Require(index>=0,"UI review evidence is not player-owned");
            var evidenceView=ActiveView(25);
            var rows=(UnityEngine.UI.Button[])evidenceView.GetType().GetProperty("RecordRowButtons").GetValue(evidenceView);
            Require(rows.Length==owned.Length,"Full observation view omitted owned records");rows[index].onClick.Invoke();
            yield return CaptureAtBothResolutions("trial_evidence_testonly",25);
            Require(runtime.ReadTrial().Focused,"Choosing a clue unexpectedly submitted it");
            Click("이 단서로 이야기하기");
            var submission=runtime.CaptureSession().Proceedings.Court.Submissions.Single();
            Require(submission.Payload.Contains("|Support|LR03|")&&submission.Result.ResultType=="Support","Direct matching location must be compared without a manual rule selection");
            Require(submission.Result.AReadCount==0,"Comparison read hidden world state");
            Require(JsonUtility.ToJson(runtime.Knowledge.Capture())==before&&runtime.World.Tick==tick,"Clue selection changed personal knowledge or world time");
            receipt.TrialChoicesVerified=true;
#else
            throw new InvalidOperationException("Trial UI review requires the development player");
#endif
        }

        void LateUpdate()
        {
            if (!driveWorld || !runtime || runtime.World == null) return;
            try
            {
                // Background capture may lose OS focus. Use the HUD's existing resume reconciliation, and disclose this adaptation.
                if (runtime.World.HasPause("K_WINDOW_FOCUS")) { Call(hud, "SyncPauseView"); receipt.BackgroundFocusRecoveries++; }
                if (runtime.World.Paused) { tickRemainder = 0; return; }
                tickRemainder += Time.unscaledDeltaTime;
                int count = 0;
                while (tickRemainder >= 1d / 60 && count++ < 12)
                {
                    tickRemainder -= 1d / 60;
                    if (driveMovement) SetPhysicalRouteInput(); else { runtime.SetMove(0, 0); runtime.SetRun(false); }
                    runtime.AdvanceOne(); receipt.ActualWorldSteps++;
                    if (driveMovement) { receipt.PhysicalDistanceTravelled += Vector3.Distance(previousPhysicalPosition, player.transform.position); previousPhysicalPosition = player.transform.position; }
                }
            }
            catch (Exception e) { driveWorld = false; runtimeErrors.Add("Probe LateUpdate: " + e); }
        }

        void SetPhysicalRouteInput()
        {
            while (movementCursor < movementPath.Length && Vector3.Distance(player.transform.position,
                runtime.Layout.NavigationNodes[movementPath[movementCursor]].Position) < .14f) movementCursor++;
            if (movementCursor >= movementPath.Length) { runtime.SetMove(0, 0); runtime.SetRun(false); return; }
            foreach (var door in runtime.Layout.Connections.Where(d => !string.IsNullOrEmpty(d.DoorId)))
                if (runtime.World.Door(door.DoorId).Open < .98 && runtime.DescribeTarget(door.DoorId).Available)
                {
                    string result = runtime.Interact(door.DoorId);
                    if (result == "문이 열립니다.") receipt.PhysicalDoorRequests++;
                }
            Vector3 delta = runtime.Layout.NavigationNodes[movementPath[movementCursor]].Position - player.transform.position;
            delta.y = 0; var direction = delta.normalized;
            runtime.SetLook(Quaternion.LookRotation(direction.sqrMagnitude > .001f ? direction : player.transform.forward).eulerAngles.y, 0);
            runtime.SetRun(false); runtime.SetMove(direction.x, direction.z);
        }

        IEnumerator SetResolution(int width, int height)
        {
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            double deadline = Time.realtimeSinceStartupAsDouble + 12;
            do { yield return null; } while ((Screen.width != width || Screen.height != height) && Time.realtimeSinceStartupAsDouble < deadline);
            Require(Screen.width == width && Screen.height == height, "Requested Game View resolution unavailable: " + width + "x" + height + "; actual " + Screen.width + "x" + Screen.height);
            for (int i = 0; i < 4; i++) yield return null;
            Canvas.ForceUpdateCanvases();
        }

        IEnumerator CaptureAtBothResolutions(string label, int expectedScreen)
        {
            yield return SetResolution(1280, 720); yield return Capture(label, expectedScreen, 1280, 720);
            yield return SetResolution(1920, 1080); yield return Capture(label, expectedScreen, 1920, 1080);
        }

        IEnumerator Capture(string label, int expectedScreen, int width, int height)
        {
            Require(CurrentScreen == expectedScreen, "Wrong UI before capture " + label + ": " + CurrentScreen);
            for (int i = 0; i < 3; i++) yield return null;
            if (expectedScreen == 3) Call(ActiveView(3), "RevealDialogue");
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            Require(Screen.width == width && Screen.height == height, "Resolution changed before capture " + label);
            string path = Path.Combine(outputDirectory, runId + "_" + label + "_" + width + "x" + height + ".png");
            var capture = new CaptureReceipt { Name = label, Path = path, Width = Screen.width, Height = Screen.height,
                ScreenId = CurrentScreen, UiScale = UiScale, Frame = Time.frameCount, Tick = runtime.World.Tick,
                PlayerPosition = player.transform.position, CameraPosition = Camera.main.transform.position,
                CameraEuler = Camera.main.transform.eulerAngles, TestOnlyFraming = placements.Count > 0 };
            captures.Add(capture);
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                // A resolution switch can initially yield an all-black swap-chain image.
                // Read the actual rendered frame and wait for visible content before accepting evidence.
                yield return new WaitForEndOfFrame();
                var frame = ScreenCapture.CaptureScreenshotAsTexture();
                // Hidden Windows swap chains may be entirely black. Use the same live camera
                // and canvases in an explicit SRP render target for deterministic visual QA.
                // This fallback is disclosed in the receipt; it is not a visible-window FPS claim.
                if(frame.GetPixels32().All(p=>p.r<8&&p.g<8&&p.b<8)){
                    Destroy(frame);frame=RenderHiddenReview(width,height);capture.RenderMethod="Live scene + live Canvas / SRP render target";
                }
                bool visible = false;
                try
                {
                    var pixels = frame.GetPixels32(); int min = 255, max = 0;
                    for (int i = 0; i < pixels.Length; i += Math.Max(1, pixels.Length / 4096))
                    {
                        int value = (pixels[i].r + pixels[i].g + pixels[i].b) / 3;
                        min = Math.Min(min, value); max = Math.Max(max, value);
                    }
                    visible = max > 30 && max - min > 20;
                    if (visible) File.WriteAllBytes(path, frame.EncodeToPNG());
                }
                finally { Destroy(frame); }
                if (!visible) { yield return null; continue; }
                if (ReadPngDimensions(path, out int pngWidth, out int pngHeight))
                {
                    capture.PngWidth = pngWidth; capture.PngHeight = pngHeight;
                    if (pngWidth == width && pngHeight == height) { capture.PngVerified = true; capture.Bytes = new FileInfo(path).Length; break; }
                    throw new InvalidOperationException("Screenshot PNG has incorrect dimensions: " + path + " / " + pngWidth + "x" + pngHeight);
                }
            }
            Require(capture.PngVerified && capture.Bytes > 100, "ScreenCapture failed to create a valid PNG: " + path);
        }
        Texture2D RenderHiddenReview(int width,int height)
        {
            var camera=Camera.main;var active=RenderTexture.active;float aspect=camera.aspect;
            var canvases=FindObjectsByType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var previousCameras=canvases.Select(c=>c.worldCamera).ToArray();var previousDistances=canvases.Select(c=>c.planeDistance).ToArray();
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);target.Create();
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try{
                camera.aspect=width/(float)height;
                foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.02f;}
                Canvas.ForceUpdateCanvases();RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();return texture;
            }catch{Destroy(texture);throw;}
            finally{
                RenderTexture.active=active;camera.aspect=aspect;
                for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=previousCameras[i];canvases[i].planeDistance=previousDistances[i];}
                Canvas.ForceUpdateCanvases();target.Release();Destroy(target);
            }
        }

        IEnumerator MeasureFrames(string label, double duration)
        {
            yield return null;
            var sample = new PerformanceReceipt { Stage = label, Width = Screen.width, Height = Screen.height,
                VSync = QualitySettings.vSyncCount, TargetFrameRate = Application.targetFrameRate,
                AllocatedBytesBefore = Profiler.GetTotalAllocatedMemoryLong(), ManagedBytesBefore = Profiler.GetMonoUsedSizeLong(),
                WorkingSetBytesBefore = WorkingSet(), TickBefore = runtime.World.Tick };
            performance.Add(sample);
            var intervals = new List<double>(); double start = Time.realtimeSinceStartupAsDouble, previous = start;
            int startFrame = Time.frameCount;
            while (Time.realtimeSinceStartupAsDouble - start < duration)
            {
                yield return null;
                double now = Time.realtimeSinceStartupAsDouble; intervals.Add((now - previous) * 1000); previous = now;
                sample.PeakAllocatedBytes = Math.Max(sample.PeakAllocatedBytes, Profiler.GetTotalAllocatedMemoryLong());
            }
            sample.Seconds = Time.realtimeSinceStartupAsDouble - start;
            sample.RenderedFrameIntervals = Time.frameCount - startFrame;
            sample.FramesPerSecond = sample.RenderedFrameIntervals / sample.Seconds;
            sample.MeanFrameMilliseconds = intervals.Average(); intervals.Sort();
            sample.P95FrameMilliseconds = intervals[Math.Min(intervals.Count - 1, (int)Math.Ceiling(intervals.Count * .95) - 1)];
            sample.MaxFrameMilliseconds = intervals[intervals.Count - 1];
            sample.AllocatedBytesAfter = Profiler.GetTotalAllocatedMemoryLong(); sample.ReservedBytesAfter = Profiler.GetTotalReservedMemoryLong();
            sample.ManagedBytesAfter = Profiler.GetMonoUsedSizeLong(); sample.WorkingSetBytesAfter = WorkingSet(); sample.TickAfter = runtime.World.Tick;
            Require(sample.RenderedFrameIntervals > 2 && sample.TickAfter > sample.TickBefore, "No real frames/world progress during " + label);
        }

        string FramePlayerNearResident(string requestedActor="",bool requireIdentifiedFace=false)
        {
            var original = player.transform.position;
            foreach (var body in runtime.Bodies.Where(b => b.ActorId != "CH_01" && (requestedActor==""||b.ActorId==requestedActor) && runtime.World.Resident(b.ActorId).Alive && runtime.World.Resident(b.ActorId).Present).OrderBy(b => b.ActorId))
            {
                var candidates = runtime.Layout.NavigationNodes.Select((node, index) => new { node, index, distance = Vector3.Distance(node.Position, body.transform.position) })
                    .Where(c => c.distance > 1.15f && c.distance < 1.8f && Mathf.Abs(c.node.Position.y - body.transform.position.y) < .25f)
                    .OrderBy(c => Mathf.Abs(c.distance - 1.5f)).Take(36);
                foreach (var candidate in candidates)
                {
                    if (!PlayerCapsuleClear(candidate.node.Position)) continue;
                    var placement = new PlacementReceipt { Purpose = "TestOnly dialogue framing only; does not count as travelled distance or a completed route.",
                        ActorId = "CH_01", From = player.transform.position, To = candidate.node.Position, NearbyResident = body.ActorId,
                        NodeId = candidate.node.Id, Tick = runtime.World.Tick };
                    placements.Add(placement);
                    SetTestOnlyPlayerPosition(candidate.node.Position, candidate.index);
                    Vector3 delta = body.transform.position + Vector3.up * body.Height * .72f - Camera.main.transform.position;
                    Vector3 angles = Quaternion.LookRotation(delta).eulerAngles;
                    runtime.SetLook(angles.y, Mathf.DeltaAngle(0, angles.x));
                    if (!runtime.DescribeTarget(body.ActorId).Available) continue;
                    if(requireIdentifiedFace){
                        var sightArgs=new object[]{"CH_01",body,false};
                        bool seen=(bool)runtime.GetType().GetMethod("SeesPassagePerson",Members).Invoke(runtime,sightArgs);
                        if(!seen||!(bool)sightArgs[2])continue;
                    }
                    placement.PhysicalReachAccepted = true;
                    return body.ActorId;
                }
            }
            SetTestOnlyPlayerPosition(original, runtime.Layout.NearestNode(original));
            throw new InvalidOperationException("No clear, physically reachable resident framing position was found. No dialogue capture asserted.");
        }

        void SetTestOnlyPlayerPosition(Vector3 position, int nodeIndex)
        {
            player.Capsule.enabled = false; player.transform.position = position; player.Capsule.enabled = true;
            var authority = runtime.World.Resident("CH_01"); authority.Position = MansionRuntime.P(position);
            authority.Node = runtime.Layout.NavigationNodes[nodeIndex].Id;
            Physics.SyncTransforms();
        }

        bool PlayerCapsuleClear(Vector3 position)
        {
            foreach (var collider in Physics.OverlapCapsule(position + Vector3.up * .32f,
                position + Vector3.up * (player.Height - .3f), .255f, ~0, QueryTriggerInteraction.Ignore))
                if (!collider.transform.IsChildOf(player.transform)) return false;
            return Physics.Raycast(position + Vector3.up * .35f, Vector3.down, .6f, ~0, QueryTriggerInteraction.Ignore);
        }

        int CurrentScreen => (int)hud.GetType().GetProperty("CurrentScreen", Members).GetValue(hud);
        void VerifyPortrait()
        {
            var portrait = Field(hud, "portrait");
            receipt.PortraitDiagnostics=(string)Field(portrait,"diagnostics");
            var texture = (Texture2D)Field(portrait, "texture");
            var surface = (Component)Field(portrait, "image");
            Require(texture!=null,"Standing portrait snapshot has not completed");
            {
                File.WriteAllBytes(Path.Combine(outputDirectory,runId+"_dialogue_portrait_texture.png"),texture.EncodeToPNG());
                var pixels=texture.GetPixels32(); int opaque=0,lit=0;
                foreach(var pixel in pixels){if(pixel.a>10)opaque++;if(pixel.a>10&&Math.Max(pixel.r,Math.Max(pixel.g,pixel.b))>20)lit++;}
                receipt.PortraitOpaquePixels=opaque;receipt.PortraitLitPixels=lit;
                receipt.PortraitSurfaceActive=surface.gameObject.activeInHierarchy;
                var rect=(RectTransform)surface.transform;receipt.PortraitRect=rect.rect.ToString();
                receipt.PortraitRendered=opaque>pixels.Length/30&&lit>pixels.Length/30;
                Require(receipt.PortraitSurfaceActive&&receipt.PortraitRendered,"Standing portrait lacks visible pixels: opaque="+opaque+", lit="+lit+", rect="+receipt.PortraitRect);
            }

        }
        float UiScale => (float)Field(hud.GetType().GetProperty("Controls", Members).GetValue(hud), "UiScale");
        MonoBehaviour ActiveView(int screen) => FindObjectsByType<MonoBehaviour>().Single(x =>
            x.GetType().FullName == "BASSLINE.UI.ProductionScreenView" && x.gameObject.activeInHierarchy && (string)Field(x, "ScreenId") == "UI_" + screen.ToString("00"));

        void Click(string label)
        {
            int from = CurrentScreen; var view = ActiveView(from); var actions = (Array)Field(view, "Actions"); var labels = (Array)Field(view, "ActionLabels");
            Component found = null;
            for (int i = 0; i < actions.Length; i++)
            {
                var button = (Component)actions.GetValue(i); var text = labels.GetValue(i);
                if (!button.gameObject.activeInHierarchy || !(bool)button.GetType().GetProperty("interactable").GetValue(button)) continue;
                if ((string)text.GetType().GetProperty("text").GetValue(text) != label) continue;
                Require(found == null, "Ambiguous live UI button: " + label); found = button;
            }
            Require(found != null, "Live UI button not found: " + label + " on UI_" + from);
            var rect = found.transform as RectTransform;
            Require(rect && rect.rect.width > 0 && rect.rect.height > 0, "Live UI button has no visible rectangle: " + label);
            object onClick = found.GetType().GetProperty("onClick").GetValue(found);
            onClick.GetType().GetMethod("Invoke", Type.EmptyTypes).Invoke(onClick, null);
            buttons.Add(new ButtonReceipt { Label = label, FromScreen = from, ToScreen = CurrentScreen, ObjectName = found.name,
                Activation = "Existing UnityEngine.UI.Button.onClick.Invoke (semantic activation; no OS input synthesized)", Tick = runtime.World.Tick });
            // Render is the same callback used by live button listeners; ensure state-dependent row listeners refresh before another semantic click.
            Call(hud, "Render"); Canvas.ForceUpdateCanvases();
        }

        static object Field(object instance, string name) => instance.GetType().GetField(name, Members)?.GetValue(instance)
            ?? throw new MissingFieldException(instance.GetType().FullName, name);
        static object Call(object instance, string method, params object[] args)
        {
            var target = instance.GetType().GetMethods(Members).SingleOrDefault(m => m.Name == method && m.GetParameters().Length == args.Length);
            if (target == null) throw new MissingMethodException(instance.GetType().FullName, method);
            return target.Invoke(instance, args);
        }
        static void Require(bool condition, string detail) { if (!condition) throw new InvalidOperationException(detail); }
        static long WorkingSet() { try { using (var process = System.Diagnostics.Process.GetCurrentProcess()) { process.Refresh(); long bytes=process.WorkingSet64; return bytes>0?bytes:-1; } } catch { return -1; } }
        static bool ReadPngDimensions(string path, out int width, out int height)
        {
            width = height = 0;
            try
            {
                if (!File.Exists(path)) return false;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length < 44) return false; var header = new byte[24]; if (stream.Read(header, 0, header.Length) != header.Length) return false;
                    if (header[0] != 137 || header[1] != 80 || header[2] != 78 || header[3] != 71 || header[12] != 73 || header[13] != 72 || header[14] != 68 || header[15] != 82) return false;
                    // A header can appear before the asynchronous encoder finishes writing the image.
                    // Require the final zero-length IEND chunk as well, so partial files never count as captures.
                    stream.Seek(-12, SeekOrigin.End); var tail = new byte[12]; if (stream.Read(tail, 0, 12) != 12) return false;
                    if (tail[0] != 0 || tail[1] != 0 || tail[2] != 0 || tail[3] != 0 || tail[4] != 73 || tail[5] != 69 || tail[6] != 78 || tail[7] != 68) return false;
                    width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                    height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23]; return width > 0 && height > 0;
                }
            }
            catch (IOException) { return false; }
        }

        void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                if (runtimeErrors.Count < 40) runtimeErrors.Add(type + ": " + message + "\n" + trace);
        }
        void Cleanup()
        {
            if (cleanedUp) return; cleanedUp = true; driveWorld = false;
            if (runtime) { runtime.SetMove(0, 0); runtime.SetRun(false); runtime.AutomaticTick = previousAutomatic; }
            if (preferencesBackedUp)
            {
                if (hadControls) PlayerPrefs.SetString(ControlsKey, previousControls); else PlayerPrefs.DeleteKey(ControlsKey);
                PlayerPrefs.Save(); Application.runInBackground = previousBackground;
                QualitySettings.vSyncCount = previousVsync; Application.targetFrameRate = previousTargetFrameRate;
                receipt.ControlsPreferencesRestored = PlayerPrefs.HasKey(ControlsKey) == hadControls && (!hadControls || PlayerPrefs.GetString(ControlsKey) == previousControls);
            }
        }
        void OnApplicationQuit() { Cleanup(); }

        [Serializable] sealed class PreferenceBackup { public bool Existed; public string Value; }
        [Serializable] sealed class Receipt
        {
            public string Status, RunId, OutputDirectory, Scope, Detail, UnityVersion, Platform, GraphicsDevice, GraphicsApi, Cpu, Pipeline, Scene, MovementMethod, DialogueMethod;
            public int RoomCount, ResidentCount, ActualWorldSteps, PhysicalDoorRequests, BackgroundFocusRecoveries, MovementPathNodesReached, MovementPathNodeCount;
            public long MovementStartTick, MovementEndTick, FinalTick;
            public Vector3 MovementStart, MovementEnd;
            public float MovementDisplacement, PhysicalDistanceTravelled, UiScale;
            public bool ResidentMeetingsVerified,AppointmentCardVerified,AppointmentConversationVerified,TrialChoicesVerified,LoanExchangeVerified,LoanDiscussionVerified,MinseoArtConnected,PrivateNoteVerified,JinwooArtConnected,JinwooSkinPoseRendered;public string TrialUiFixture,LoanExchangeMethod;public int LoanEncounterWaitTicks;
            public string[] LoanApproachRejections=Array.Empty<string>();
            public long WaitingMeasuredTicks;public double WaitingMeasuredSeconds,WaitingWorldSecondsPerSecond;
            public bool TitleButtonEnteredPlay, PhysicalMovementVerified, MovementReachedHall, NotePauseVerified, DialoguePhysicalReach, DialoguePlayerLedgerVerified, ControlsPreferencesRestored, WaitingScreensVerified;
            public string DialogueActor, DialogueReceiptId, DialogueSource;
            public int PortraitOpaquePixels,PortraitLitPixels;public bool PortraitSurfaceActive,PortraitRendered;public string PortraitRect,PortraitDiagnostics;
            public int MapKnownPlaces,MapKnownLayers;public bool MapKnowledgeAndControlsVerified,MapMultipleFloorControlsVerified;
            public bool PreferencesUntouched;
            public CaptureReceipt[] Captures; public PerformanceReceipt[] Performance; public ButtonReceipt[] Buttons; public PlacementReceipt[] TestOnlyPlacements; public string[] RuntimeErrors;
        }
        [Serializable] sealed class CaptureReceipt
        {
            public string Name, Path,RenderMethod="Game View swap chain"; public int Width, Height, PngWidth, PngHeight, ScreenId, Frame; public float UiScale;
            public long Bytes, Tick; public bool PngVerified, TestOnlyFraming; public Vector3 PlayerPosition, CameraPosition, CameraEuler;
        }
        [Serializable] sealed class PerformanceReceipt
        {
            public string Stage; public int Width, Height, VSync, TargetFrameRate, RenderedFrameIntervals;
            public double Seconds, FramesPerSecond, MeanFrameMilliseconds, P95FrameMilliseconds, MaxFrameMilliseconds;
            public long TickBefore, TickAfter, AllocatedBytesBefore, AllocatedBytesAfter, ReservedBytesAfter, PeakAllocatedBytes,
                ManagedBytesBefore, ManagedBytesAfter, WorkingSetBytesBefore, WorkingSetBytesAfter;
        }
        [Serializable] sealed class ButtonReceipt { public string Label, ObjectName, Activation; public int FromScreen, ToScreen; public long Tick; }
        [Serializable] sealed class PlacementReceipt { public string Purpose, ActorId, NearbyResident, NodeId; public Vector3 From, To; public long Tick; public bool PhysicalReachAccepted; }
    }
}
