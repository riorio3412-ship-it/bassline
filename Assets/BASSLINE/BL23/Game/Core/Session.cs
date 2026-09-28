using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using BL23.Game.Audio;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// One running campaign. Drives the kernel at 10Hz from real time (pausable by tokens), routes kernel events to
    /// presentation, and switches between exploration / trial / verdict / reveal / loop-reset presentations.
    /// </summary>
    public sealed class Session : MonoBehaviour
    {
        public static Session I;
        public Simulation Sim; public GameState S => Sim.S;
        public WorldPresenter World; public PlayerController Player; public Hud Hud; public DialogueUI Dialogue; public NoteUI Note; public TrialDirectorUI Trial; public RevealPlayer Reveal;
        public MenuUI Menu; public AnnouncementUI Announcer; public CinematicUI Cine;
        readonly HashSet<string> _pause = new HashSet<string>();
        public bool Paused => _pause.Count > 0;
        public float Speed = 1f;          // time acceleration while waiting
        float _acc; Phase _lastPhase = Phase.Boot; int _loopSeen = -1; int _chapterSeen = -1;
        public bool Headless;             // automated probe: skip cinematics timing
        public string LastSaveNote;

        // --- time-on-demand (begin) ----------------------------------------------------------------------------------------
        /// <summary>OnDemand (players): daily life stands still until time is spent (T, time with someone, a conversation, an
        /// emergency, walking with a companion). Continuous: the house runs in real time (probes unless -bl23timeflow ondemand).</summary>
        public TimeFlowMode TimeFlow = TimeFlowMode.Continuous;
        /// <summary>Runs every skip (never call it Time: that would hide UnityEngine.Time).</summary>
        public TimeDirector TimeDir;
        float _lastStepAt = -99f, _perceiveAt, _emergCheckAt, _followNoteAt = -99f;
        string _emergWhy; bool _inTail, _following; double _tailFrom; string _followerId;
        /// <summary>No kernel tick for 0.4 real seconds (a stopped clock, a conversation, a pause): people live in place.
        /// Any tick counts, whoever ran it (the running house, a skip, a conversation's minutes in the continuous flow).</summary>
        public bool WorldStill { get { WatchTick(); return Time.unscaledTime - _lastStepAt > 0.4f; } }
        public void MarkStepped() { _lastStepAt = Time.unscaledTime; }
        long _seenTick = long.MinValue;
        void WatchTick() { if (Sim != null && S.Tick != _seenTick) { _seenTick = S.Tick; _lastStepAt = Time.unscaledTime; } }
        public bool OnDemandDaily => TimeFlow == TimeFlowMode.OnDemand && S != null && S.Phase == Phase.Daily;
        /// <summary>Why the clock runs on its own right now (OnDemand daily): an emergency the player knows about.</summary>
        public string EmergencyWhy => _emergWhy;
        public bool EmergencyTail => _inTail;
        public bool FollowingNow => _following;

        static TimeFlowMode ResolveTimeFlow()
        {
            var b = GameBoot.I; string v = null; try { v = b != null ? b.ArgValue("-bl23timeflow") : null; } catch (Exception) { }
            if (v != null && v.Equals("ondemand", StringComparison.OrdinalIgnoreCase)) return TimeFlowMode.OnDemand;
            if (v != null && v.Equals("continuous", StringComparison.OrdinalIgnoreCase)) return TimeFlowMode.Continuous;
            if (b != null && b.HasArg("-bl23probe")) return TimeFlowMode.Continuous;   // other workflows' verifications expect a running house
            return TimeFlowMode.OnDemand;
        }

        /// <summary>Does the house run in real time this frame (else it stands still until time is spent)?</summary>
        public bool RunsRealTime() => TimeFlow == TimeFlowMode.Continuous || S.Phase != Phase.Daily || Speed > 1.5f || _emergWhy != null || _inTail || _following;

        /// <summary>The HUD chip (OnDemand daily only): 시간 멈춤 / 함께 다니는 중 / 긴급 / 곧 시간이 멈춘다. Null otherwise.</summary>
        public string ClockChip()
        {
            if (TimeFlow != TimeFlowMode.OnDemand || S.Phase != Phase.Daily || (TimeDir != null && TimeDir.Active)) return null;
            if (_emergWhy != null) return $"<color=#E07A6A>긴급</color> — {_emergWhy} · 시간이 흐른다";
            if (_inTail) return "곧 시간이 멈춘다";
            if (_following) return LineBank.FixParticles($"시간이 흐른다 — {(_followerId != null ? Cast.GivenOf(_followerId) : "누군가")}와(과) 함께 다니는 중");
            if (Speed > 1.5f) return null;
            return "시간 멈춤 · <color=#D6AD62>T</color> 시간 보내기";
        }

        void UpdateClockState()
        {
            if (TimeFlow != TimeFlowMode.OnDemand || S.Phase != Phase.Daily) { _emergWhy = null; _inTail = false; _following = false; return; }
            if (Time.unscaledTime < _emergCheckAt) return;
            _emergCheckAt = Time.unscaledTime + 0.1f;
            string why = null; try { why = Sim.EmergencyWhy(); } catch (Exception e) { Debug.LogException(e); }
            if (why != null) { _emergWhy = why; _inTail = false; }
            else if (_emergWhy != null) { _emergWhy = null; _inTail = true; _tailFrom = S.Clock; }
            if (_inTail) { bool calm = false; try { calm = Sim.CalmEnough(); } catch (Exception) { calm = true; } if (calm || S.Clock - _tailFrom >= 30) _inTail = false; }
            bool fol = false; try { fol = Sim.FollowerActive; } catch (Exception) { }
            if (fol && !_following && Time.unscaledTime - _followNoteAt > 60f) { _followNoteAt = Time.unscaledTime; Hud?.SysNote("함께 다니는 동안에는 시간이 흐른다", 3f); }
            _following = fol;
            _followerId = fol ? S.LivingNpcs.Where(x => x.Following == Cast.Player).Select(x => x.Id).FirstOrDefault() : null;
        }

        /// <summary>The exact running loop of the continuous house (speed cap, the legacy alarm break).</summary>
        void LegacyStep(Phase ph)
        {
            _acc += Time.deltaTime * Speed;
            int n = 0, cap = Speed > 1.5f ? 2000 : 6;
            while (_acc >= SimTime.Dt && n < cap) { Sim.Step(); _acc -= SimTime.Dt; n++; if (S.Phase != ph) break; if (Speed > 1.5f && Sim.PlayerAlarm) { Sim.PlayerAlarm = false; Speed = 1; WaitStopped(); break; } }
            if (n > 0) MarkStepped();
            if (n >= cap) _acc = 0;
        }

        /// <summary>A wait broken by something: one toast that names it ("기다림을 멈췄다 — 비명 소리가 들렸다 …"); the notice that
        /// caused it is not toasted a second time.</summary>
        void WaitStopped()
        {
            string cause = null;
            var list = S.Out;
            if (list != null)
                for (int i = list.Count - 1; i >= 0 && cause == null; i--)
                {
                    var e = list[i];
                    if (e.Type == GameEventType.Discovery) cause = e.Text == "body" ? "시신이 발견됐다" : "누군가 쓰러졌다";
                    else if (e.Type == GameEventType.Announcement && e.Key != "y_intro") cause = "유스티의 방송";
                    else if (e.Type == GameEventType.Notice && (e.Actor == Cast.Player || e.Actor == null) && !string.IsNullOrEmpty(e.Text) && e.Key != "exam") { cause = e.Text; _noticeShown = e.Text; }
                }
            if (cause == null && LastCause != null && Time.unscaledTime - LastCauseAt < 2f) cause = LastCause;
            Hud?.Toast(cause != null ? "기다림을 멈췄다 — " + cause : "기다림을 멈췄다", Pal.Magenta);
        }
        string _noticeShown;   // a notice already told inside the "기다림을 멈췄다" toast

        /// <summary>Still daily life: no ticks; the player still sees (a body, an attack) and queued lapses start.</summary>
        void Frozen()
        {
            _acc = 0;
            if (Time.unscaledTime >= _perceiveAt)
            {
                _perceiveAt = Time.unscaledTime + 0.2f;
                if (S.Player != null && S.Player.Alive) { try { Sim.PlayerPerceive(); } catch (Exception e) { Debug.LogException(e); } }
            }
            TimeDir?.AutoStart();
        }

        /// <summary>The kernel moved the player (a swallowed room, the house putting people out): the body follows.</summary>
        void FollowKernelMove(P3 pushed)
        {
            var a = S.Player; if (a == null || !a.Alive || a.CarriedBy != null || Player == null || !Player.Controlling || Player.Scripted) return;
            if (a.Pos.f != pushed.f || a.Pos.DistXZ(pushed) > 0.5f) Player.Teleport(a.Pos, a.Yaw, Player.Pitch);
        }

        /// <summary>Stops raised outside a skip (someone at the door, a scream while time runs): a banner. Every kind in still
        /// daily life (OnDemand), only the urgent ones where the clock runs anyway; never for a discovery (it has its film).</summary>
        void TakeStops()
        {
            if (TimeDir != null && TimeDir.Active) return;   // the director reads the stops of its own skip
            TimeStop st = null; try { st = Sim.TakeStop(); } catch (Exception) { }
            if (st == null || st.Kind == StopKind.Discovery || Headless || TimeDir == null) return;
            bool show = st.Class == StopClass.Critical || (TimeFlow == TimeFlowMode.OnDemand && S.Phase == Phase.Daily);
            if (show) TimeDir.UI.Banner(st);
        }
        bool BannerJustShown(StopKind k, string actor = null) =>
            TimeDir != null && Time.unscaledTime - TimeDir.UI.LastBannerAt < 1f && TimeDir.UI.LastBannerKind == k && (actor == null || TimeDir.UI.LastBannerActor == null || TimeDir.UI.LastBannerActor == actor);

        GameEvent _queuedAnn; string _autoSaveWhy; bool _tutPending; float _aftermathAt = -1f; string _aftermathKey;
        float _titleCardUntil = -1f; string _queuedApproach;   // the investigation's title card: an approach prompt waits for it
        void FlushApproach()
        {
            if (_queuedApproach == null || Time.unscaledTime < _titleCardUntil) return;
            var who = _queuedApproach; _queuedApproach = null;
            if (S.Flags.ContainsKey("approach:" + who)) Hud?.Approach(who);   // (still coming to talk)
        }
        // --- time-on-demand (end) ------------------------------------------------------------------------------------------

        public void Pause(string token) { _pause.Add(token); }
        public void Resume(string token) { _pause.Remove(token); }
        public bool IsPaused(string token) => _pause.Contains(token);

        public static Session Create(Simulation sim)
        {
            var go = new GameObject("BL23 Session"); DontDestroyOnLoad(go);
            var s = go.AddComponent<Session>(); I = s; s.Sim = sim;
            sim.OnFault = (what, e) => Debug.LogError("[BL23 kernel fault] " + what + ": " + e);
            // --- time-on-demand: chosen per session (new game and every load); the kernel flag is not saved
            s.TimeFlow = ResolveTimeFlow(); sim.OnDemand = s.TimeFlow == TimeFlowMode.OnDemand;
            Debug.Log("[BL23] time flow " + s.TimeFlow);
            s.Build();
            return s;
        }

        void Build()
        {
            UIKit.EnsureEventSystem(); Backlog.Clear(); Guide.Clear();
            // each piece is built even if another one throws, so a single presentation fault can't take the session down
            void Safe(Action a) { try { a(); } catch (Exception e) { Debug.LogException(e); } }
            World = gameObject.AddComponent<WorldPresenter>(); Safe(() => World.Init(this));
            Safe(() => Player = PlayerController.Create(this));
            Hud = gameObject.AddComponent<Hud>(); Safe(() => Hud.Init(this));
            Dialogue = gameObject.AddComponent<DialogueUI>(); Safe(() => Dialogue.Init(this));
            Note = gameObject.AddComponent<NoteUI>(); Safe(() => Note.Init(this));
            Announcer = gameObject.AddComponent<AnnouncementUI>(); Safe(() => Announcer.Init(this));
            Trial = gameObject.AddComponent<TrialDirectorUI>(); Safe(() => Trial.Init(this));
            Reveal = gameObject.AddComponent<RevealPlayer>(); Safe(() => Reveal.Init(this));
            Menu = gameObject.AddComponent<MenuUI>(); Safe(() => Menu.Init(this));
            Cine = gameObject.AddComponent<CinematicUI>(); Safe(() => Cine.Init(this));
            TimeDir = gameObject.AddComponent<TimeDirector>(); Safe(() => TimeDir.Init(this));   // --- time-on-demand
            _loopSeen = S.Loop; _chapterSeen = S.Chapter;
        }

        public void Teardown()
        {
            Sfx.StopAllLoops(0.2f); BacklogUI.Close(); CaseReport.Close();
            SaveStore.Flush();   // an autosave still being written finishes before the session goes
            if (Player != null) Destroy(Player.gameObject);
            World?.Teardown();
            foreach (var c in GetComponentsInChildren<Canvas>(true)) Destroy(c.gameObject);
            Hud?.Destroy(); Dialogue?.Destroy(); Note?.Destroy(); Announcer?.Destroy(); Trial?.Destroy(); Reveal?.Destroy(); Menu?.Destroy(); Cine?.Destroy();
            TimeDir?.Destroy();   // --- time-on-demand
            Destroy(gameObject); if (I == this) I = null;
        }

        void Update()
        {
            if (Sim == null) return;
            var ph = S.Phase;
            // --- time-on-demand (begin): who moves the clock this frame — a running skip (the director steps the kernel
            // itself), the running house (continuous, investigation, emergencies, a companion), or nobody (still daily life)
            bool exploring = ph == Phase.Daily || ph == Phase.Investigation || ph == Phase.Assembly || ph == Phase.Prologue;
            if (TimeDir != null && TimeDir.Active) { _acc = 0; TimeDir.Tick(); }
            else if (exploring && !Paused)
            {
                Player?.PushPose(); var pushed = S.Player != null ? S.Player.Pos : default(P3);
                UpdateClockState();
                if (RunsRealTime()) LegacyStep(ph);
                else Frozen();
                FollowKernelMove(pushed);
            }
            else _acc = 0;
            WatchTick();
            TakeStops();
            if (_queuedAnn != null && !(TimeDir?.Lapsing ?? false)) { var qa = _queuedAnn; _queuedAnn = null; Announcer.Show(qa.Text, qa.Key); }
            if (_autoSaveWhy != null && !(TimeDir?.Active ?? false)) { var why = _autoSaveWhy; _autoSaveWhy = null; AutoSave(why); }
            FlushApproach();
            PollAutoSave();
            if (_tutPending && !(Cine?.Busy ?? false) && !(TimeDir?.Active ?? false) && Player != null && Player.Controlling)
            {
                _tutPending = false;
                Hud?.Toast("일상에서는 시간이 멈춰 있다 — T로 시간을 보내거나, 누군가와 함께 시간을 보내면 흐른다", Pal.Gold, 7f);
                if (!AutoProbe.Active) { try { PlayerPrefs.SetInt("bl23_tut_time", 1); PlayerPrefs.Save(); } catch (Exception) { } }   // a probe never marks the player's profile
            }
            // --- time-on-demand (end)
            DrainEvents();
            if (S.Phase != _lastPhase) OnPhase(_lastPhase, S.Phase);
            World?.Sync(Time.deltaTime);
            UpdateMusic();
            CheckTableTalk();
            QolKeys();
            if (_reportPending && (S.Phase != Phase.Assembly || (!Dialogue.Active && !(Cine?.Busy ?? false) && !(Menu?.Open ?? false))))
            {
                _reportPending = false;
                if (S.Phase == Phase.Assembly) { try { CaseReport.Show(this); } catch (Exception e) { Debug.LogException(e); } }
            }
        }

        // ------------------------------------------------------------------ global keys: H 지난 대화 · F5 빠른 저장 · F9 빠른 불러오기
        void QolKeys()
        {
            if (Input.GetKeyDown(KeyCode.H) && !(Menu?.Open ?? false) && Time.frameCount != BacklogUI.ClosedFrame) BacklogUI.Toggle(this);
            if (Input.GetKeyDown(KeyCode.F5))
            {
                if (!CanQuickSave()) { Hud?.SysNote("지금은 저장할 수 없다"); return; }
                if (WriteSave(Settings.QuickPath, SaveLabel(), true)) { Hud?.Toast("빠르게 저장했다 — F9로 불러오기", Pal.Good, 2f); Hud?.SaveMark("빠른 저장"); }
            }
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (!CanQuickSave()) { Hud?.SysNote("지금은 저장할 수 없다"); return; }
                var info = SaveStore.Info(Settings.QuickPath);
                if (!info.Exists || info.Corrupt) { Hud?.Toast("빠른 저장 기록이 없다 — F5로 저장하기", Pal.TextDim, 2f); return; }
                Menu.ConfirmOnly("빠른 불러오기", "빠른 저장을 불러올까?\n<size=80%><color=#9A8E7C>" + MenuUI.SlotLine(info) + "\n저장하지 않은 부분은 사라진다.</color></size>", "불러오기", () =>
                {
                    var st = LoadPath(Settings.QuickPath, out var note);
                    if (st == null) { Hud?.Toast(note ?? "불러올 수 없다", Pal.Blood, 3f); return; }
                    GameBoot.I.StartFromState(st, note);
                });
            }
        }

        /// <summary>Quick save / load work in free exploration only (not in court, cinematics, conversations or menus).</summary>
        public bool CanQuickSave()
        {
            var ph = S.Phase;
            if (ph != Phase.Daily && ph != Phase.Investigation && ph != Phase.Assembly) return false;
            if ((Trial?.Active ?? false) || (Reveal?.Active ?? false) || (Cine?.Busy ?? false) || (Dialogue?.Active ?? false) || (Menu?.Open ?? false) || BacklogUI.Open || CaseReport.Open) return false;
            if (TimeDir != null && TimeDir.Active) return false;   // --- time-on-demand: never in the middle of a skip
            if (S.Player == null || !S.Player.Alive || Player == null || !Player.Controlling || Player.Scripted) return false;
            return true;
        }

        // ------------------------------------------------------------------ events → presentation
        void DrainEvents()
        {
            var list = S.Out; if (list == null || list.Count == 0) return;
            var batch = list.ToList(); list.Clear();
            foreach (var e in batch)
            {
                try { Route(e); } catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        void Route(GameEvent e)
        {
            World?.OnEvent(e);
            var p = S.Player;
            switch (e.Type)
            {
                case GameEventType.Speech:
                    if (e.Actor != Cast.Player && p != null && e.Pos.f == p.Pos.f && e.Pos.DistXZ(p.Pos) < (e.Value > 0 ? 24 : 12) && !Dialogue.Active)
                    {
                        if (!(TimeDir?.Lapsing ?? false)) Hud.Overheard(e.Actor, e.Text, e.Pos.DistXZ(p.Pos));   // --- time-on-demand: fast-forwarded talk goes to 지난 대화 only
                        Backlog.Add(e.Actor, e.Text, S.Clock, S.RoomName(e.Room >= 0 ? e.Room : p.Room));
                    }
                    break;
                case GameEventType.Announcement:
                    if (TimeDir != null && TimeDir.Lapsing) _queuedAnn = e;   // --- time-on-demand: an ordinary bell during a lapse shows after it (the latest one)
                    else Announcer.Show(e.Text, e.Key);
                    if (e.Key != "y_intro") Backlog.Add(Cast.Butler, e.Text, S.Clock, "저택 방송");
                    Cause("유스티의 방송");
                    break;
                case GameEventType.Notice:
                    if (e.Actor == Cast.Player || e.Actor == null)
                    {
                        string nt = e.Text;
                        // "옷에 붉은 얼룩" — whose? A sighting names the person it is about.
                        if (string.IsNullOrEmpty(e.Key) && !string.IsNullOrEmpty(e.Target) && e.Target != Cast.Player && S.A(e.Target) != null && nt != null && !nt.Contains(Cast.GivenOf(e.Target))) nt = Cast.GivenOf(e.Target) + " — " + nt;
                        // --- time-on-demand: the banner already said it (a scream heard, an attack seen)
                        bool said = (e.Key == "heard" && BannerJustShown(StopKind.Heard)) || (e.Key == "attack" && BannerJustShown(StopKind.Attack));
                        if (_noticeShown != null && e.Text == _noticeShown) { said = true; _noticeShown = null; }   // already in the "기다림을 멈췄다 — …" toast
                        if (!said) Hud.Toast(nt, e.Key == "attack" || e.Key == "attacked" || e.Key == "player_dead" ? Pal.Blood : Pal.Cyan);
                        if (e.Key != "exam") Cause(nt);
                    }
                    if (e.Key == "approach" && !BannerJustShown(StopKind.Approach, e.Actor)) { if (Time.unscaledTime < _titleCardUntil) _queuedApproach = e.Actor; else Hud.Approach(e.Actor); }   // --- time-on-demand: not over the '수사' title card
                    if (e.Key == "request_met") { Hud.Toast(LineBank.FixParticles("약속을 지켰다 — " + Cast.GivenOf(e.Actor) + "와(과) 함께"), Pal.Gold, 4f); Sfx.Play("chime", null, 0.4f); }
                    if (e.Key == "request_missed") Hud.Toast(LineBank.FixParticles(Cast.GivenOf(e.Actor) + "와(과)의 약속을 지키지 못했다"), Pal.TextDim, 4f);
                    break;
                case GameEventType.Evidence: Hud.EvidenceToast(e.Text, e.Key, e.Data);   // one toast per find (sound included), batched in the HUD
                    if (S.Phase == Phase.Daily && e.Key == "new" && e.Actor == Cast.Player) MusicDirector.I?.Surprise();   // 저택에서 예상치 못한 걸 발견했을 때 (owner's folder)
                    break;
                case GameEventType.Discovery: if (e.Text != "body") MusicDirector.I?.Stinger("discovery"); /* [discovery film] a found body gets the film's own composed sting (CinematicUI) */ Hud.Discovery(e.Target, e.Text == "body"); if (e.Text == "body") { Cine.Discovery(e.Target); _discoveryAt = Time.unscaledTime; } Cause(e.Text == "body" ? "시신이 발견됐다" : "누군가 쓰러졌다"); break;
                case GameEventType.RuleStart: Hud.Toast("새 규칙 — " + e.Text, Pal.Gold); break;
                case GameEventType.Relationship: if (!string.IsNullOrEmpty(e.Text) && e.Actor != Cast.Player && e.Target == Cast.Player) Hud.Toast(Cast.GivenOf(e.Actor) + "의 기억에 남았다 — " + e.Text, Pal.A(Pal.Gold, 0.9f), 2.2f); break;
                case GameEventType.Verdict: break;
            }
        }

        /// <summary>The last thing that could have broken a wait (a notice, a broadcast, a discovery), with when it came.</summary>
        public string LastCause; public float LastCauseAt = -999f; public int LastCauseFrame = -1;
        void Cause(string text) { if (string.IsNullOrEmpty(text)) return; LastCause = text; LastCauseAt = Time.unscaledTime; LastCauseFrame = Time.frameCount; }

        // ------------------------------------------------------------------ phases
        bool _reportPending;
        bool _firstPhase = true;   // the first phase seen by a new session (a loaded save) does not autosave over the rotation
        void OnPhase(Phase from, Phase to)
        {
            _lastPhase = to; bool fresh = _firstPhase && from == Phase.Boot; _firstPhase = false;
            switch (to)
            {
                case Phase.Prologue: Cine.Prologue(); break;
                case Phase.Daily:
                    Player.SetControl(true);
                    if (S.Loop != _loopSeen) { _loopSeen = S.Loop; World.Rebuild(); Player.Respawn(); Cine.LoopStartCard(); }
                    if (S.Chapter != _chapterSeen) { _chapterSeen = S.Chapter; Cine.ChapterCard(); }
                    if (!fresh) AutoSave("챕터 시작");
                    // --- time-on-demand (begin): a new day starts with people going about their morning (not on a load);
                    // the first still day of a profile says how time works
                    if (TimeFlow == TimeFlowMode.OnDemand)
                    {
                        // a chapter that begins at night (a late trial): the night is passed, everyone to bed, until the 7:00 bell
                        if (!fresh) { int mm = S.Minute; if (mm >= 20 * 60 || mm < 5 * 60) TimeDir?.QueueNight("그날 밤, 모두 말없이 방으로 돌아갔다"); else TimeDir?.QueueSettle(); }
                        bool seen = false; try { seen = PlayerPrefs.GetInt("bl23_tut_time", 0) == 1; } catch (Exception) { }
                        if (!seen) _tutPending = true;
                    }
                    // --- time-on-demand (end)
                    break;
                case Phase.Investigation:
                    {
                        var inc = S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Confirmed).OrderBy(i => i.ConfirmClock).FirstOrDefault();
                        Cine.InvestigationCard(inc != null ? Cast.NameOf(inc.Victim) : "", inc != null ? S.RoomName(inc.FoundRoom) : "");
                        _titleCardUntil = Headless ? -1f : Time.unscaledTime + 4.7f;   // --- time-on-demand: someone coming to talk waits until the card has gone
                        Hud.Toast("수사 시작 — 남은 시간 " + Mathf.Max(0, Mathf.RoundToInt((float)(S.Ch.InvestigationEnd - S.Clock))) + "분", Pal.Gold, 4); if (!fresh) AutoSave("수사 시작"); break;   // --- time-on-demand: plain minutes
                    }
                case Phase.Assembly:
                    Hud.Toast("심판이 열린다 — 중앙 홀 승강기로 가자", Pal.Gold, 5); if (!fresh) AutoSave("심판 소집");
                    _reportPending = true;   // 심판 전 정리: shown as soon as nothing else holds the screen
                    break;
                case Phase.Trial: Player.SetControl(false); Trial.Begin(); break;
                case Phase.Verdict: Trial.ShowVerdict(); break;
                case Phase.LoopEpilogue: Cine.LoopEnd(); break;
            }
        }

        /// <summary>Called by the trial/verdict/reveal presentation when everything has been shown.</summary>
        public void FinishChapterPresentation()
        {
            Settlements.AfterReveal(Sim);
            if (S.Phase == Phase.LoopEpilogue) { Cine.LoopEnd(); return; }
            World.AfterTrial();
            Player.Respawn();
            Player.SetControl(true);
        }

        public void StartNextLoop()
        {
            Settlements.NextLoop(Sim);
            S.Phase = Phase.Daily; _lastPhase = Phase.Boot;
        }

        float _discoveryAt = -999f;
        /// <summary>Walking into the dining room at breakfast or dinner while several people eat together starts a short
        /// table conversation (once per meal).</summary>
        void CheckTableTalk()
        {
            if (Headless || S.Phase != Phase.Daily || Cine == null || Cine.Busy || Dialogue.Active || Paused || S.Player == null || !Player.Controlling) return;
            var room = S.Layout.Room(S.Player.Room); if (room == null || room.Type != RoomType.Dining) return;
            int m = S.Minute; string meal = m >= 7 * 60 && m < 10 * 60 ? "breakfast" : m >= 12 * 60 && m < 14 * 60 ? "lunch" : m >= 18 * 60 && m < 21 * 60 ? "dinner" : null; if (meal == null) return;
            string key = $"tabletalk:{S.Day}:{meal}"; if (S.Flags.ContainsKey(key)) return;
            var diners = S.LivingNpcs.Where(x => x.Room == room.Id && x.Status == ActorStatus.Active && (x.Anim == Anim.Eat || x.Pose == BL23.Sim.Pose.Sit) && x.TalkingTo == null).OrderBy(x => x.Id).Select(x => x.Id).ToList();
            if (diners.Count < 3) return;
            S.Flags[key] = S.Clock;
            Cine.TableTalk(diners.Take(7).ToList(), meal);   // up to seven at the table: the long table scenes (pact, the house's pushes)
        }
        void UpdateMusic()
        {
            var md = MusicDirector.I; if (md == null) return;
            if (Trial != null && Trial.Active) return; // trial UI drives music itself
            if (Reveal != null && Reveal.Active) return;
            if (Cine != null && Cine.Busy) return;
            MusicState want;
            switch (S.Phase)
            {
                case Phase.Prologue: want = MusicState.Prologue; break;
                case Phase.Investigation: want = S.Clock > S.Ch.InvestigationEnd - 20 ? MusicState.InvestigationLate : MusicState.Investigation; break;
                case Phase.Assembly: want = MusicState.InvestigationLate; break;
                case Phase.Daily:
                    {
                        int m = S.Minute; var room = S.Layout.Room(S.Player?.Room ?? -1);
                        if (room != null && RoomInfo.IsMystery(room.Type)) want = MusicState.Mystery;
                        else if (S.Settlements.Any(x => x.Loop == S.Loop && x.Chapter == S.Chapter - 1) && S.Clock - S.Ch.ChapterStartClock < 90 && AftermathFresh()) want = MusicState.Aftermath;   // --- time-on-demand: also ends after 3 real minutes (the clock may stand still)
                        else if (m < 6 * 60 || m >= 22 * 60) want = MusicState.Night;
                        else if (m < 11 * 60) want = MusicState.DailyMorning;
                        else if (m < 18 * 60) want = MusicState.DailyDay;
                        else want = MusicState.DailyEvening;
                        if (S.Player != null && S.Player.Needs.Fear > 0.6f) want = MusicState.Tension;
                        // a shared meal: the dining room is full and people are eating together
                        else if (room != null && room.Type == RoomType.Dining && S.LivingNpcs.Count(x => x.Room == room.Id && x.Anim == Anim.Eat) >= 3) want = MusicState.Gathering;
                        if (BL23.Game.DialogueUI.BondScene) want = MusicState.Bond;
                        { var tp = TimeLink.Dir != null ? TimeLink.Dir.Current : null; if (tp != null && !string.IsNullOrEmpty(tp.Partner)) want = MusicState.Bond; }   // npc들이랑 놀 때 (owner's folder): time spent with someone
                        if (Time.unscaledTime - _discoveryAt < 30f) want = MusicState.BodyDiscovery;   // until the bell and the investigation take over
                        break;
                    }
                default: return;
            }
            if (md.Current != want) md.SetState(want);
        }

        // --- time-on-demand: the aftermath music of a chapter lasts at most three real minutes
        bool AftermathFresh()
        {
            string key = S.Loop + ":" + S.Chapter;
            if (_aftermathKey != key) { _aftermathKey = key; _aftermathAt = Time.unscaledTime; }
            return Time.unscaledTime - _aftermathAt < 180f;
        }

        // ------------------------------------------------------------------ save / load
        // manual slots 1..6 · rotating autosaves auto1..auto3 (the oldest is overwritten) · quick.sav (F5/F9) · legacy autosave.sav (read only)
        public bool Save(int slot, string label) => WriteSave(Settings.SlotPath(slot), label ?? SaveLabel(), false);

        bool WriteSave(string path, string label, bool quiet)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                SaveStore.Write(path, S, label);
                Debug.Log($"[BL23] save {System.IO.Path.GetFileName(path)} \"{label}\" {sw.ElapsedMilliseconds} ms");
                if (!quiet) Hud?.Toast("저장했다", Pal.Good, 1.5f);
                return true;
            }
            catch (Exception e) { Debug.LogException(e); Hud?.Toast("저장하지 못했다 — " + e.Message, Pal.Blood, 4); return false; }
        }

        /// <summary>Transitions only (chapter start, waking, investigation start, assembly): overwrites the oldest of auto1..3.</summary>
        public void AutoSave(string why)
        {
            if (S == null || S.Player == null || !S.Player.Alive) return;
            if (TimeDir != null && TimeDir.Active) { _autoSaveWhy = why; return; }   // --- time-on-demand: written when the skip ends
            int pick = 1; DateTime oldest = DateTime.MaxValue;
            for (int i = 1; i <= 3; i++)
            {
                var info = SaveStore.Info(Settings.AutoPath(i));
                if (!info.Exists || info.Corrupt) { pick = i; break; }
                if (info.Saved < oldest) { oldest = info.Saved; pick = i; }
            }
            // the state becomes JSON on this frame; compressing, hashing and writing run on a worker (no second-long stutter as a
            // case opens or the court is summoned). The quill shows when the file is safely on disk.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                SaveStore.WriteInBackground(Settings.AutoPath(pick), S, SaveLabel());
                _autoPending = $"auto{pick} ({why})"; _autoMainMs = sw.ElapsedMilliseconds;
            }
            catch (Exception e) { Debug.LogException(e); Hud?.Toast("자동 저장하지 못했다 — " + e.Message, Pal.Blood, 4); }
        }
        string _autoPending; long _autoMainMs;
        /// <summary>A background autosave finished: log it (main-thread and worker time) and show the quill.</summary>
        void PollAutoSave()
        {
            if (_autoPending == null || !SaveStore.TakeResult(out bool ok, out long ms, out string note)) return;
            var what = _autoPending; _autoPending = null;
            if (ok) { Debug.Log($"[BL23] autosave {what} {_autoMainMs} ms (main) · {ms} ms (written in the background)"); Hud?.SaveMark(); }
            else { Debug.LogWarning($"[BL23] autosave {what} failed: {note}"); Hud?.Toast("자동 저장하지 못했다 — " + note, Pal.Blood, 4); }
        }

        /// <summary>"3일째 오후 1:02 · 도서실 · 수사 중".</summary>
        public string SaveLabel()
        {
            string ph = S.Phase == Phase.Daily ? "일상" : S.Phase == Phase.Investigation ? "수사 중" : S.Phase == Phase.Assembly ? "심판 직전" : S.Phase == Phase.Trial ? "심판"
                      : S.Phase == Phase.Verdict ? "판결" : S.Phase == Phase.Prologue ? "첫날 밤" : "루프의 끝";
            return $"{ClockFmt.Stamp(S.Clock)} · {S.RoomName(S.Player?.Room ?? -1)} · {ph}";
        }

        public static GameState LoadState(int slot, out string note) => SaveStore.Read(Settings.SlotPath(slot), out note);
        public static GameState LoadPath(string path, out string note) => SaveStore.Read(path, out note);

        /// <summary>Every save file the player can load, newest first is not implied (menu order): autos, quick, legacy auto, slots.</summary>
        public static List<(string name, string path)> SaveFiles(bool includeManual = true)
        {
            var l = new List<(string, string)>();
            for (int i = 1; i <= 3; i++) l.Add(($"자동 {i}", Settings.AutoPath(i)));
            l.Add(("빠른 저장", Settings.QuickPath));
            if (SaveStore.Info(Settings.SlotPath(0)).Exists) l.Add(("이전 자동 저장", Settings.SlotPath(0)));
            if (includeManual) for (int s = 1; s <= 6; s++) l.Add(($"슬롯 {s}", Settings.SlotPath(s)));
            return l;
        }

        /// <summary>The newest valid save of any kind (이어하기), or null.</summary>
        public static string NewestSave()
        {
            string best = null; DateTime bt = DateTime.MinValue;
            foreach (var (_, path) in SaveFiles()) { var info = SaveStore.Info(path); if (info.Exists && !info.Corrupt && info.Saved > bt) { bt = info.Saved; best = path; } }
            return best;
        }
    }
}
