using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Audio;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>How the game clock moves. Continuous: the house runs in real time (probes, headless tests). OnDemand (players):
    /// daily life stands still until the player spends time — a wait, sleep, a pastime, time with someone, a conversation.</summary>
    public enum TimeFlowMode { Continuous, OnDemand }

    /// <summary>
    /// Runs every passage of time the player asks for (time-on-demand): waits and "~까지" targets from the T menu, sleep,
    /// pastimes, time spent with someone, the minutes a conversation took, the toll after a discovery, the day-start settle.
    /// Every minute is real kernel ticks (Sim.StepSkip), shown as a first-person time-lapse (brass dial, light changing with
    /// the daylight), a fade while asleep, or a short scene with a companion. The kernel decides where a skip stops
    /// (screams, knocks, someone coming to talk, the meal bell); this shows why. Only Session calls Tick.
    /// </summary>
    public sealed class TimeDirector : MonoBehaviour
    {
        public enum Style { Auto, Lapse, Fade, Scene, Hidden }
        const float VisibleRate = 360f;   // ticks per real second of a visible lapse (18 clock min/s in daily life; ActorView keeps up)
        const int LongTicks = 1200;       // longer skips (over an hour of daily life): a veil and a frame budget in between
        const float TailSecs = 0.6f;      // the last stretch of a long skip is visible again, so arrivals look natural
        const float VeilBudgetMs = 35f;   // stepping per frame under the veil (the world is dimmed: ~20 fps is fine there)
        const float FadeBudgetMs = 40f;   // stepping per frame while the screen fades to black
        const float BlackBudgetMs = 180f; // stepping per frame once the screen is fully black (asleep, the world camera draws nothing): a night fits in seconds
        const float VisibleBudgetMs = 20f; // stepping per frame of a visible lapse: the frame stays smooth (a costly house runs a slower lapse, never a hitch)

        Session _s; Simulation Sim => _s.Sim; GameState S => _s.S;
        public TimeUI UI { get; private set; }

        SkipPlan _plan; Style _style;
        bool _ownBusy, _cancellable, _finishNext, _long, _veiled, _fading, _final, _staging, _settlePending;
        float _t0, _tickAcc, _sceneRate = -1f, _dayAt, _dialHideAt = -1f, _holdUntil;
        double _dayHour = -1, _stepMs; int _linesShown, _tollTries;
        // stopped by the player while a murder was in a hot moment: the house runs on (veiled) until the kernel ends it
        bool _cancelHold; float _cancelAt;
        // watchdog: a running skip whose kernel ticks do not advance for a real second is nudged on (and logged)
        float _wdAt; int _stalls;
        // a new chapter that begins at night: the night is passed (the screen goes dark, 민혁 is in his bed, the 7:00 bell)
        string _nightPending;
        Action<SkipPlan, SkipResult, bool> _then;   // a skip with staging (pastime, sleep) ends through its own outro
        readonly List<(string text, Color c, float secs)> _after = new List<(string, Color, float)>();

        /// <summary>A skip is running (only the director steps the kernel).</summary>
        public bool Active => _plan != null;
        /// <summary>A visible time-lapse (not the hidden toll): barks, gestures and overheard lines hold still.</summary>
        public bool Lapsing => _plan != null && _style != Style.Hidden;
        /// <summary>Only mouse look while a skip runs (no walking, no interaction).</summary>
        public bool LocksMovement => _plan != null && _style != Style.Hidden;
        /// <summary>Doors, blows and footsteps are not heard while the house is fast-forwarded (the last 0.6 s are).</summary>
        public bool MuteWorldAudio => Lapsing && !_final;
        public SkipPlan Current => _plan;
        public Style CurrentStyle => _style;
        public float Progress => _plan == null ? 0f : Mathf.Clamp01((float)((S.Clock - _plan.Start) / Math.Max(0.01, _plan.Target - _plan.Start)));
        /// <summary>The last wait that stopped before its target, and where the player stood (the T menu offers 계속 기다리기).</summary>
        public SkipPlan LastInterrupted { get; private set; }
        public P3 LastInterruptedAt { get; private set; }
        public event Action<SkipPlan> Began;
        public event Action<SkipPlan, SkipResult> Ended;
        // probe / diagnostics
        public SkipPlan LastPlan { get; private set; }
        public SkipResult LastResult { get; private set; }
        public float LastRealSecs { get; private set; }
        public string LastLog { get; private set; }
        public int SkipCount { get; private set; }
        public readonly List<float> LapseFrames = new List<float>();
        /// <summary>Why the last Start was refused (probe log): busy, active, staging, a critical stop pending… Null when it started.</summary>
        public string LastRefusal { get; private set; }
        /// <summary>Times the watchdog had to nudge a skip whose ticks stopped advancing (should stay 0).</summary>
        public int Stalls => _stalls;
        /// <summary>The player's stop is being held while a murder's hot moment passes (the house runs on, veiled).</summary>
        public bool CancelHeld => _cancelHold;
        /// <summary>The long middle of a long skip runs under the dark veil (probe: photograph it).</summary>
        public bool Veiled => _veiled;

        /// <summary>T menu toggle "작은 일로는 멈추지 않기" (only screams, appointments and the like stop a wait).</summary>
        public static bool QuietPref
        {
            get { try { return PlayerPrefs.GetInt("bl23.timequiet", 0) == 1; } catch (Exception) { return false; } }
            set { try { PlayerPrefs.SetInt("bl23.timequiet", value ? 1 : 0); PlayerPrefs.Save(); } catch (Exception) { } }
        }

        public void Init(Session s) { _s = s; UI = gameObject.AddComponent<TimeUI>(); UI.Init(s); }
        public void Destroy() { Blackout(false); UI?.Destroy(); }

        // ------------------------------------------------------------------ starting
        /// <summary>Starts a skip. False when one is running, the plan is null, a cinematic holds the screen, or something
        /// urgent is pending (a critical stop).</summary>
        public bool Start(SkipPlan p, Style s = Style.Auto)
        {
            LastRefusal = p == null ? "no plan" : WhyNot(s);
            if (LastRefusal != null) return false;
            return Begin(p, s, false, null);
        }

        /// <summary>Would a skip in this style start now? (Plan first only when it would: planning time with someone draws its lines
        /// and folds the conversation's minutes in.)</summary>
        public bool CanStart(Style s = Style.Auto) => WhyNot(s) == null;

        string WhyNot(Style s)
        {
            if (_plan != null) return "a skip is running";
            if (_staging) return "a pastime or sleep is being staged";
            if (Sim == null) return "no kernel";
            if (s != Style.Hidden && (_s.Cine?.Busy ?? false)) return "a cinematic holds the screen (Cine.Busy)";
            var ps = Sim.PendingStop; if (ps != null && ps.Class == StopClass.Critical) return "a critical stop is pending (" + ps.Kind + ")";
            return null;
        }

        static Style AutoStyle(SkipPlan p) =>
            p.Kind == SkipKind.Sleep || (p.Kind == SkipKind.Pastime && p.Action == "nap") ? Style.Fade : p.Kind == SkipKind.Together ? Style.Scene : Style.Lapse;

        bool Begin(SkipPlan p, Style s, bool busyHeld, Action<SkipPlan, SkipResult, bool> then)
        {
            if (s == Style.Auto) s = AutoStyle(p);
            try { Sim.BeginSkip(p); }
            catch (Exception e) { Debug.LogException(e); return false; }
            _plan = p; _style = s; _then = then; _t0 = Time.unscaledTime; _tickAcc = 0; _stepMs = 0; _sceneRate = -1f; _linesShown = 0;
            _finishNext = false; _veiled = false; _fading = false; _final = false; _dialHideAt = -1f; _cancelHold = false; _wdAt = Time.unscaledTime; LastRefusal = null;
            int tpm = Math.Max(1, Sim.TicksPerMinute);
            _long = s == Style.Lapse && (p.Target - S.Clock) * tpm > LongTicks;
            _cancellable = s != Style.Hidden && p.Kind != SkipKind.Talk && p.Kind != SkipKind.Toll;
            _ownBusy = busyHeld;
            if (!busyHeld && s != Style.Hidden && p.Kind != SkipKind.Talk && p.Kind != SkipKind.Toll && _s.Cine != null) { _s.Cine.Busy = true; _ownBusy = true; }
            if (p.Kind == SkipKind.Wait || p.Kind == SkipKind.Until || p.Kind == SkipKind.Sleep) LastInterrupted = null;
            SkipCount++;
            // how it looks
            switch (s)
            {
                case Style.Lapse:
                    UI.ShowDial(TimeUI.DialMode.Bottom); UI.Effects(p.Kind != SkipKind.Talk && p.Kind != SkipKind.Toll);
                    UI.SetDial(S.Clock, 0f, p.Kind == SkipKind.Talk ? "" : p.Kind == SkipKind.Toll ? "종이 울린다" : p.Kind == SkipKind.Settle ? "하루가 시작된다" : (p.Label ?? "시간이 흐른다"));
                    break;
                case Style.Fade:
                    UI.ShowDial(TimeUI.DialMode.Center); UI.SetDial(S.Clock, 0f, p.Kind == SkipKind.Sleep ? "잠들었다" : (p.Label ?? "잠깐 눈을 붙였다"));
                    MusicDirector.I?.Duck(true);
                    break;
                case Style.Scene:
                    UI.ShowDial(TimeUI.DialMode.TopRight); UI.SetDial(S.Clock, 0f, "");
                    break;
            }
            LapseFrames.Clear();
            try { Began?.Invoke(p); } catch (Exception e) { Debug.LogException(e); }
            return true;
        }

        // ------------------------------------------------------------------ facades
        /// <summary>A pastime at a piece of furniture (책 읽기 30분...): walk over, sit or take the pose, the time-lapse, stand up,
        /// then what came of it.</summary>
        public void StartPastime(Furniture f, PlayerAction act)
        {
            if (f == null || act == null || Active || _staging || (_s.Cine?.Busy ?? false)) return;
            SkipPlan plan = null; string why = null;
            try { plan = Sim.PlanPastime(f, act.Id, out why); } catch (Exception e) { Debug.LogException(e); }
            if (plan == null) { Hud.I?.Toast(why ?? "지금은 할 수 없다", Pal.TextDim, 2.2f); return; }
            if (string.IsNullOrEmpty(plan.Label)) plan.Label = act.Label;
            StartCoroutine(PastimeCo(f, act, plan));
        }

        IEnumerator PastimeCo(Furniture f, PlayerAction act, SkipPlan plan)
        {
            var cine = _s.Cine; _staging = true; cine.Busy = true; _s.Pause("wait");
            Spot held = null;
            IEnumerator stage = null; try { stage = cine.StageActivity(f, act, h => held = h); } catch (Exception e) { Debug.LogException(e); }
            if (stage != null) yield return stage;   // walk over, pull the chair out and sit (PlayerSit), look at the hands
            _s.Resume("wait"); _staging = false;
            bool nap = act.Id == "nap";
            if (!Begin(plan, nap ? Style.Fade : Style.Lapse, true, (p, res, hard) => PastimeOutro(act, held, p, res, hard)))
            {
                yield return cine.UnstageActivity(held);
                ReleaseBusy(true);
            }
        }

        void PastimeOutro(PlayerAction act, Spot held, SkipPlan p, SkipResult res, bool hard)
        {
            if (hard) { UI.FadeTo(0, 0.25f); _s.Cine.UnstageNow(held); ReleaseBusy(true); PastimeReport(act, p, res); return; }
            StartCoroutine(PastimeOutroCo(act, held, p, res));
        }
        IEnumerator PastimeOutroCo(PlayerAction act, Spot held, SkipPlan p, SkipResult res)
        {
            if (UI.FadeAlpha > 0.01f) { UI.FadeTo(0, _s.Headless ? 0.05f : 0.6f); yield return Real(_s.Headless ? 0.06f : 0.6f); }
            yield return _s.Cine.UnstageActivity(held);
            ReleaseBusy(true);
            PastimeReport(act, p, res);
        }
        void PastimeReport(PlayerAction act, SkipPlan p, SkipResult res)
        {
            var a = res?.Activity; var stop = res?.Stop ?? p.Stop; bool cut = res != null ? res.Interrupted : stop != null && stop.Kind != StopKind.Target;
            var lines = EffectLines(a?.Text);   // what happened meanwhile goes to the "그 사이" toasts, not on the card
            string footer = !cut ? null : stop != null && stop.Kind == StopKind.Cancelled ? "도중에 그만두었다" : "도중에 끝났다" + (stop != null && !string.IsNullOrEmpty(stop.Title) ? " — " + stop.Title : "");
            string title = (p.Label ?? act?.Label ?? "") + $"  <size=70%><color=#9A8E7C>{TimeUI.Span(res?.Minutes ?? (S.Clock - p.Start))}</color></size>";
            if (!_s.Headless) UI.ResultCard(title, lines, footer);
            if (a?.LoreTitle != null) Hud.I?.Lore(a.LoreTitle, a.LoreText);
            if (a?.Music != null) MusicDirector.I?.Skip();   // a new record / a new piece: the next of the composer's tracks
            if (a?.ItemMade != null) Sfx.Play("ui_confirm", null, 0.35f);
        }

        /// <summary>Sleep in the own bed until the morning bell (goToBed: from anywhere — the screen goes dark first). card: the line
        /// under the dial instead of "잠들었다" (a night passed after a trial).</summary>
        public void StartSleep(bool goToBed) => StartSleep(goToBed, null);
        public void StartSleep(bool goToBed, string card)
        {
            if (Active || _staging || (_s.Cine?.Busy ?? false) || S.Phase != Phase.Daily) return;
            StartCoroutine(SleepCo(goToBed, card));
        }

        IEnumerator SleepCo(bool goToBed, string card)
        {
            var cine = _s.Cine; _staging = true; cine.Busy = true; _s.Pause("wait");
            float fd = _s.Headless ? 0.05f : 0.5f; UI.FadeTo(1f, fd); yield return Real(fd + 0.02f);
            var bedroom = S.Layout.BedroomOf(Cast.Player);
            var bed = bedroom?.Furniture.Select(id => S.Layout.Furniture[id]).FirstOrDefault(x => x.Type == "Bed");
            if (bed != null && _s.Player != null)
            {
                var spot = S.Layout.Spots.FirstOrDefault(sp => sp.Furniture == bed.Id);
                var at = spot != null ? spot.Approach : bed.Pos;
                if (goToBed || S.Player.Pos.f != at.f || S.Player.Pos.DistXZ(at) > 2.5f) _s.Player.Teleport(at, spot != null ? spot.Yaw : 0f);
            }
            SkipPlan plan = null; string why = null;
            try { plan = Sim.PlanSleep(out why); } catch (Exception e) { Debug.LogException(e); }
            _s.Resume("wait"); _staging = false;
            if (plan == null)
            {
                UI.FadeTo(0, 0.4f); cine.Busy = false;
                Hud.I?.Toast(why ?? "지금은 잘 수 없다", Pal.TextDim, 2.5f); yield break;
            }
            if (string.IsNullOrEmpty(plan.Label)) plan.Label = "잠자리에 든다";
            // the body lies down (kernel pose) and the view goes with it
            _s.Player.BeginScript(); _s.Player.ScriptPitch = -35f;
            if (!Begin(plan, Style.Fade, true, WakeUp)) { EndScriptHere(); UI.FadeTo(0, 0.4f); cine.Busy = false; }
            else if (!string.IsNullOrEmpty(card)) UI.SetDial(S.Clock, 0f, card);
        }

        void WakeUp(SkipPlan p, SkipResult res, bool hard)
        {
            if (hard) { UI.FadeTo(0, 0.25f); EndScriptHere(); ReleaseBusy(true); return; }
            StartCoroutine(WakeCo(p, res));
        }
        IEnumerator WakeCo(SkipPlan p, SkipResult res)
        {
            UI.FadeTo(0, _s.Headless ? 0.05f : 0.6f); yield return Real(_s.Headless ? 0.06f : 0.6f);
            if (_s.Player != null) _s.Player.ScriptPitch = 0f;   // the body sits up, then stands: the view rises with it
            yield return Real(_s.Headless ? 0.05f : 1.0f);
            EndScriptHere(); ReleaseBusy(true);
            bool morning = res != null && !res.Interrupted;
            if (morning) _s.AutoSave("아침");
        }

        void EndScriptHere() { var pc = _s.Player; if (pc != null && pc.Scripted && S.Player != null) pc.EndScript(S.Player.Pos, S.Player.Yaw); }

        /// <summary>The toll after a discovery, even while a cinematic holds the screen (the discovery film calls this
        /// instead of waiting on kernel time). No UI and no lock.</summary>
        public void RunToll()
        {
            if (Active || Sim == null || !Sim.TollPending) return;
            SkipPlan p = null; try { p = Sim.PlanToll(); } catch (Exception e) { Debug.LogException(e); }
            if (p != null) Begin(p, Style.Hidden, false, null);
        }

        /// <summary>The player stops the skip. At once — unless a murder is in a hot moment (OnDemand daily life): then the house runs on,
        /// veiled, until it has passed (the kernel ends the skip then), so a killer is never frozen mid-act for a free tour.</summary>
        public void Cancel()
        {
            var p = _plan; if (p == null || p.Done || _cancelHold) return;
            try { Sim.CancelSkip(p); }
            catch (Exception e) { Debug.LogException(e); try { Sim.AbortSkip(p); } catch (Exception) { p.Done = true; } }
            if (p.Done) { _finishNext = true; return; }
            _cancelHold = true; _cancelAt = Time.unscaledTime;
            Debug.Log($"[BL23] lapse cancel held {p.Kind} at {ClockFmt.HM(S.Clock)} (a murder is in a hot moment: the house runs on until it has passed)");
        }

        /// <summary>A new day begins (after the prologue, a trial, a new loop): once the screen is free, the house runs a
        /// little so people leave the hall and go about their morning.</summary>
        public void QueueSettle() { _settlePending = true; }

        /// <summary>A new chapter begins at night (after a late trial): once the screen is free the night is passed — the screen
        /// goes dark, 민혁 is in his own bed and sleeps until the 7:00 bell, with this line under the dial.</summary>
        public void QueueNight(string card) { _nightPending = card ?? ""; _settlePending = false; }

        /// <summary>Frozen daily life, nothing on screen: the toll, the minutes a conversation took, the day-start settle.</summary>
        public void AutoStart()
        {
            if (Active || _staging || Sim == null || S.Phase != Phase.Daily || _s.Paused || (_s.Dialogue?.Active ?? false) || (_s.Cine?.Busy ?? false)) return;
            if ((_s.Trial?.Active ?? false) || (_s.Reveal?.Active ?? false) || (_s.Menu?.Open ?? false)) return;
            if (_s.Player != null && _s.Player.Scripted) return;
            if (Time.unscaledTime < _holdUntil) return;
            if (S.Out != null && S.Out.Count > 0) return;   // this frame's events first (a discovery starts its film before the toll)
            if (Sim.TollPending)
            {
                // a toll lapse that could not start (a stop pending this very frame) is retried; only lapses that ran count
                if (_tollTries < 3) { if (Start(Sim.PlanToll(), Style.Lapse)) _tollTries++; else _holdUntil = Time.unscaledTime + 0.5f; }
                return;
            }
            _tollTries = 0;
            if (Sim.PendingTalk > 0.01 && Sim.PendingTogether == null)
            {
                // the minutes are spent only when the lapse really starts (a stop pending this very frame: next time)
                SkipPlan tp = null; try { tp = Sim.PlanTalk(Sim.PendingTalk); } catch (Exception e) { Debug.LogException(e); }
                if (tp == null) Sim.PendingTalk = 0;
                else if (Start(tp, Style.Lapse)) Sim.PendingTalk = 0;
                else _holdUntil = Time.unscaledTime + 0.5f;
                return;
            }
            if (_nightPending != null)
            {
                var card = _nightPending.Length > 0 ? _nightPending : null; _nightPending = null; _settlePending = false;
                int m = S.Minute;
                if (m >= 20 * 60 || m < 6 * 60 + 30) { StartSleep(true, card); return; }
                _settlePending = true;   // morning came first (a long skip in between): the day starts as usual
            }
            if (_settlePending)
            {
                _settlePending = false;
                SkipPlan sp = null; try { sp = Sim.PlanSettle(30); } catch (Exception e) { Debug.LogException(e); }
                if (sp != null) Start(sp, Style.Lapse);
            }
        }

        // ------------------------------------------------------------------ running (Session calls this while Active)
        public void Tick()
        {
            var p = _plan; if (p == null) return;
            float now = Time.unscaledTime, dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.1f), el = now - _t0;
            if (_finishNext) { Finish(); return; }
            if (!p.Done && _cancellable && !_cancelHold && el > 0.35f && CancelKey()) { Cancel(); UpdateUi(p, now); return; }   // ends next frame: the key must not also act
            bool hold = _style != Style.Hidden && _s.IsPaused("menu");
            int ticks0 = p.Ticks;
            if (!p.Done && !hold)
            {
                int tpm = Math.Max(1, Sim.TicksPerMinute);
                double left = Math.Max(0.0, (p.Target - S.Clock) * tpm);   // ticks to the target (deferral and settling may run past it)
                int n = 0; float budget = 0f;
                if (_cancelHold)
                {
                    // stopped by the player while a murder was in a hot moment: the house runs on under the veil until it has passed
                    if (_style == Style.Lapse && !_veiled) { _veiled = true; UI.Veil(0.55f); MusicDirector.I?.Duck(true); }
                    n = int.MaxValue; budget = _style == Style.Fade && UI.FadeAlpha >= 0.98f ? BlackBudgetMs : VeilBudgetMs;
                    _final = false;
                }
                else switch (_style)
                {
                    case Style.Hidden: n = int.MaxValue; budget = 25f; break;
                    case Style.Fade:
                        if (UI.FadeAlpha < 0.98f) { if (!_fading) { _fading = true; UI.FadeTo(1f, _s.Headless ? 0.05f : 0.5f); } }
                        else
                        {
                            // black: nothing to draw — the world camera stops drawing the house behind the black and the frame goes to the night
                            bool black = UI.FadeAlpha >= 0.999f && el > 0.3f;
                            Blackout(black);
                            n = int.MaxValue; budget = black ? BlackBudgetMs : FadeBudgetMs;
                        }
                        break;
                    case Style.Scene:
                        if (el < 1f) _tickAcc += 20f * dt;   // the partner walks over at an easy pace
                        else { if (_sceneRate < 0) _sceneRate = Mathf.Max(20f, (float)left / 6f); _tickAcc += _sceneRate * dt; }
                        n = (int)_tickAcc; _tickAcc -= n;
                        break;
                    default:
                        {
                            bool tiny = p.Kind == SkipKind.Talk || p.Kind == SkipKind.Toll;
                            // ticks still to go before the last visible stretch (clock steps are float: this is rarely a whole number)
                            double beyondTail = left - VisibleRate * TailSecs;
                            if (_long && el > 0.5f && beyondTail >= 1.0)
                            {
                                // the long middle: veiled, as fast as a slice of each frame allows, until the last 0.6 s. Always at least one
                                // tick and rounded up (truncating left a fraction of a tick below 1 → nothing ran, a skip stood 10.8 min short)
                                if (!_veiled) { _veiled = true; UI.Veil(0.55f); MusicDirector.I?.Duck(true); }
                                n = Math.Max(1, (int)Math.Ceiling(beyondTail)); budget = VeilBudgetMs;
                            }
                            else
                            {
                                if (_veiled) { _veiled = false; UI.Veil(0f); MusicDirector.I?.Duck(false); }
                                float rampIn = tiny ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(el / 0.3f));
                                float rampOut = left > 0 ? Mathf.Clamp((float)(left / VisibleRate) / 0.4f, 0.35f, 1f) : 1f;
                                _tickAcc += VisibleRate * Mathf.Max(0.08f, rampIn) * rampOut * dt;
                                n = (int)_tickAcc; _tickAcc -= n;
                            }
                            _final = !_veiled && left <= VisibleRate * TailSecs;
                            break;
                        }
                }
                Run(p, n, budget);
            }
            // watchdog: a running skip whose ticks stand still for a real second (not paused by a menu, not waiting for the fade)
            // is nudged on and logged — a frozen dial under a dark veil must never be the player's only way out
            if (hold || p.Done || p.Ticks != ticks0 || (_style == Style.Fade && UI.FadeAlpha < 0.98f && !_cancelHold)) _wdAt = now;
            else if (now - _wdAt > 1f)
            {
                _wdAt = now; _stalls++;
                Debug.Log($"[BL23] lapse STALL {p.Kind} '{p.Label}' ticks={p.Ticks} left={(p.Target - S.Clock):0.00}min style={_style} veiled={_veiled} held={_cancelHold} — nudged");
                Run(p, 1, 0f);
            }
            if (p.Done) { Finish(); return; }
            UpdateUi(p, now);
            if (_style == Style.Lapse && !_veiled && LapseFrames.Count < 20000) LapseFrames.Add(Time.unscaledDeltaTime);
        }

        void Run(SkipPlan p, int n, float budgetMs)
        {
            if (n <= 0) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            double cap = budgetMs > 0 ? budgetMs : VisibleBudgetMs;   // a visible lapse never hitches a frame either
            while (n > 0 && !p.Done)
            {
                int chunk = Math.Min(n, 16), before = p.Ticks;
                try { Sim.StepSkip(p, chunk); }
                catch (Exception e) { Debug.LogException(e); try { Sim.AbortSkip(p); } catch (Exception) { p.Done = true; } break; }
                n -= Math.Max(1, p.Ticks - before);
                if (sw.Elapsed.TotalMilliseconds >= cap) break;
            }
            _stepMs += sw.Elapsed.TotalMilliseconds; _s.MarkStepped();
        }

        void UpdateUi(SkipPlan p, float now)
        {
            if (_style == Style.Hidden) return;
            UI.SetDial(S.Clock, Progress);
            // the light through the windows follows the clock (a full refresh is costly: every ~6 clock minutes at most; not while the
            // screen is black — the skip's end brings the light up to the hour before the black lifts)
            double h = (S.Clock % 1440.0) / 60.0;
            if (_blackCam == null && Math.Abs(h - _dayHour) > 0.1 && now - _dayAt > 0.2f) { _dayHour = h; _dayAt = now; try { _s.World?.Mansion?.RefreshDaylight(); } catch (Exception) { } }
            if (_style == Style.Scene) SceneLines(p);
        }

        void SceneLines(SkipPlan p)
        {
            if (p.Lines == null || p.Lines.Count == 0) return;
            float prog = Progress; int want = prog >= 0.75f ? 3 : prog >= 0.5f ? 2 : prog >= 0.25f ? 1 : 0;
            while (_linesShown < want && _linesShown < p.Lines.Count) Say(p.Lines[_linesShown++]);
        }
        void Say(Utterance u)
        {
            if (u == null || string.IsNullOrEmpty(u.Text)) return;
            var text = LineBank.Pages(u.Text).FirstOrDefault() ?? u.Text;
            UI.SceneLine(u.Speaker, text);
            try { VoiceBabble.Speak(u.Speaker, text); } catch (Exception) { }
            if (u.Speaker != Cast.Player)
            {
                var v = _s.World?.ViewOf(u.Speaker);
                if (v != null) { v.Talk(Mathf.Clamp(text.Length * 0.07f, 1f, 4f)); try { SpeechGestures.Perform(v, text, u.Emotion, false); } catch (Exception) { } }
            }
        }

        static bool CancelKey(Style s)
        {
            if (s == Style.Fade) return Input.anyKeyDown && !Input.GetKeyDown(KeyCode.T);
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E)
                || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D)
                || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow);
        }
        bool CancelKey() => CancelKey(_style);

        // ------------------------------------------------------------------ ending
        void Finish()
        {
            var p = _plan; if (p == null) return;
            if (_blackCam != null)
            {
                // the house is drawn again before the black lifts, in the light of the hour it is now
                Blackout(false); _dayHour = (S.Clock % 1440.0) / 60.0; _dayAt = Time.unscaledTime;
                try { _s.World?.Mansion?.RefreshDaylight(); } catch (Exception) { }
            }
            SkipResult res = null;
            try { res = Sim.EndSkip(p); }
            catch (Exception e) { Debug.LogException(e); }
            if (res == null) res = new SkipResult { Minutes = S.Clock - p.Start, Stop = p.Stop, Interrupted = p.Stop != null && p.Stop.Kind != StopKind.Target };
            var style = _style; _plan = null; _final = false; _finishNext = false; _s.MarkStepped();
            bool held = _cancelHold; float heldSecs = held ? Time.unscaledTime - _cancelAt : 0f; _cancelHold = false;
            var stop = res.Stop ?? p.Stop;
            bool hard = stop != null && (stop.Class == StopClass.Critical || stop.Kind == StopKind.Phase || stop.Kind == StopKind.Moved || stop.Kind == StopKind.PlayerDead || stop.Kind == StopKind.Discovery);
            float real = Time.unscaledTime - _t0;
            LastPlan = p; LastResult = res; LastRealSecs = real;
            LastLog = $"lapse {p.Kind} ticks={p.Ticks} ms={_stepMs:0} ms/tick={(p.Ticks > 0 ? _stepMs / p.Ticks : 0):0.000} style={style} stop={stop?.Kind.ToString() ?? "-"} over={(S.Clock - p.Target):0.0}min real={real:0.00}s min={res.Minutes:0.0}"
                    + (held ? $" cancel-held={heldSecs:0.0}s" : "") + (_stalls > 0 ? $" stalls={_stalls}" : "");
            Debug.Log("[BL23] " + LastLog);
            // presentation off (a critical stop ends everything at once; the fade-in of a sleep is its own outro)
            UI.Veil(0f); UI.Effects(false);
            if (_veiled || style == Style.Fade) MusicDirector.I?.Duck(false);
            _veiled = false;
            bool tiny = p.Kind == SkipKind.Talk || p.Kind == SkipKind.Toll;
            if (tiny && !hard) _dialHideAt = Time.unscaledTime + 1.2f; else UI.HideDial();
            if (style == Style.Scene) UI.HideLine();
            if (_fading && _then == null) UI.FadeTo(0, 0.4f);
            // remember a wait that stopped short (T menu: 계속 기다리기)
            if ((p.Kind == SkipKind.Wait || p.Kind == SkipKind.Until) && stop != null && stop.Kind != StopKind.Target && stop.Kind != StopKind.PlayerDead && stop.Kind != StopKind.Phase && p.Target > S.Clock + 1)
            { LastInterrupted = p; LastInterruptedAt = S.Player != null ? S.Player.Pos : default(P3); }
            var then = _then; _then = null;
            if (then != null) { try { then(p, res, hard); } catch (Exception e) { Debug.LogException(e); ReleaseBusy(true); } }
            else { ReleaseBusy(false); if (p.Kind != SkipKind.Together) FollowKernel(); }   // time together: the scene places the body itself
            Report(p, res, stop, hard);
            try { Ended?.Invoke(p, res); } catch (Exception e) { Debug.LogException(e); }
        }

        // ---- asleep behind the black: the world camera draws nothing (a layer no renderer uses; not an empty mask, so the ears stay
        // on the player's camera), and draws the house again the moment the black begins to lift or the skip ends
        const int BlackLayerMask = 1 << 31;
        Camera _blackCam; int _blackSaved;
        void Blackout(bool on)
        {
            if (on)
            {
                if (_blackCam != null) return;
                var cam = _s.Player != null ? _s.Player.Cam : null; if (cam == null || cam.cullingMask == BlackLayerMask) return;
                _blackCam = cam; _blackSaved = cam.cullingMask; cam.cullingMask = BlackLayerMask;
                return;
            }
            if (_blackCam == null) return;
            if (_blackCam.cullingMask == BlackLayerMask) _blackCam.cullingMask = _blackSaved;
            _blackCam = null;
        }

        void ReleaseBusy(bool force)
        {
            if ((_ownBusy || force) && _s.Cine != null) _s.Cine.Busy = false;
            _ownBusy = false;
        }

        /// <summary>The kernel moved the player during the skip (a swallowed room, a seat left): the body follows.</summary>
        void FollowKernel()
        {
            var pc = _s.Player; var a = S.Player;
            if (pc == null || a == null || !a.Alive || a.CarriedBy != null || !pc.Controlling || pc.Scripted) return;
            var w = _s.World != null ? _s.World.ToWorld(a.Pos) : Vector3.zero; var feet = pc.FeetPosition;
            float d = new Vector2(w.x - feet.x, w.z - feet.z).magnitude;
            if (d > 0.5f || Mathf.Abs(w.y - feet.y) > 1.5f) pc.Teleport(a.Pos, a.Yaw, pc.Pitch);
        }

        void Report(SkipPlan p, SkipResult res, TimeStop stop, bool hard)
        {
            var kind = stop?.Kind ?? StopKind.Target;
            double mins = res.Minutes;
            bool natural = kind == StopKind.Target;
            // why it stopped (a discovery has its own film; a phase change and death speak for themselves)
            if (!natural && stop != null && kind != StopKind.Cancelled && kind != StopKind.Discovery && kind != StopKind.Phase && kind != StopKind.PlayerDead && !string.IsNullOrEmpty(stop.Title) && !_s.Headless)
            {
                string hint = null;
                if (stop.Class != StopClass.Critical && (p.Kind == SkipKind.Wait || p.Kind == SkipKind.Until) && p.Target > S.Clock + 1) hint = "T 계속 기다리기";
                else if (stop.Class != StopClass.Critical && p.Kind == SkipKind.Sleep && S.Phase == Phase.Daily && (S.Minute >= 20 * 60 || S.Minute < 6 * 60 + 30)) hint = "T 다시 잠자리에 들기";
                UI.Banner(stop, hint);
            }
            switch (p.Kind)
            {
                case SkipKind.Talk:
                    if (mins >= 1) UI.SetDial(S.Clock, 1f, $"이야기하는 사이 {TimeUI.Span(mins)}이 흘렀다");
                    break;
                case SkipKind.Wait: case SkipKind.Until:
                    if (natural) After($"{TimeUI.Span(mins)}이 흘렀다 — {TimeUI.Clock12(S.Clock)}", Pal.Text, 3f);
                    else if (kind == StopKind.Cancelled) After("기다리기를 멈췄다 — " + TimeUI.Clock12(S.Clock), Pal.TextDim, 2f);
                    break;
                case SkipKind.Sleep:
                    if (!res.Interrupted) After($"아침이다 — {S.Day}일째 {TimeUI.Clock12(S.Clock)}", Pal.Gold, 3.5f);
                    else if (kind == StopKind.Cancelled) After("잠자리에서 일어났다 — " + TimeUI.Clock12(S.Clock), Pal.TextDim, 2.5f);
                    break;
                case SkipKind.Together:
                    {
                        var lines = EffectLines(res.Activity?.Text);   // "지아와 차 한잔을 함께했다" · "지아의 호감이 올랐다 · 가까워졌다"
                        string footer = !res.Interrupted ? null : kind == StopKind.Cancelled ? "도중에 자리를 떴다" : "도중에 끝났다" + (stop != null && !string.IsNullOrEmpty(stop.Title) ? " — " + LineBank.FixParticles(stop.Title) : "");
                        string title = (p.Label ?? "함께 시간을 보냈다") + $"  <size=70%><color=#9A8E7C>{TimeUI.Span(mins)}</color></size>";
                        if (!_s.Headless) UI.ResultCard(title, lines, footer, 5f);
                        break;
                    }
            }
            // what happened meanwhile (the bell, someone passing) and what was overheard
            if (p.Kind != SkipKind.Talk && p.Kind != SkipKind.Toll && p.Passed != null)
                foreach (var l in p.Passed.Where(x => !string.IsNullOrEmpty(x)).Distinct().Take(2)) After(l.StartsWith("그 사이") ? l : "그 사이: " + l, Pal.TextDim, 3.5f);
            if (res.Overheard > 0) After($"그 사이 들은 이야기 {res.Overheard} — H 지난 대화", Pal.Cyan, 4f);
        }

        /// <summary>A result sentence as card lines: "머리 — 효과 · 효과" becomes the head and the effects under it.</summary>
        static List<string> EffectLines(string text)
        {
            var l = new List<string>(); if (string.IsNullOrEmpty(text)) return l;
            int cut = text.IndexOf(" — ", StringComparison.Ordinal);
            if (cut > 0) { l.Add(text.Substring(0, cut)); l.Add(text.Substring(cut + 3)); } else l.Add(text);
            return l;
        }

        /// <summary>A toast after the skip, once the HUD is back (it hides while a cinematic holds the screen).</summary>
        void After(string text, Color c, float secs) { if (!string.IsNullOrEmpty(text)) _after.Add((LineBank.FixParticles(text), c, secs)); }

        void Update()
        {
            if (_s == null) return;
            if (_dialHideAt > 0 && Time.unscaledTime > _dialHideAt && !Active) { _dialHideAt = -1f; UI.HideDial(); }
            // a menu opened over the little "이야기하는 사이 4분이 흘렀다" pop: the pop goes at once (it never lingers under the T menu)
            if (!Active && _dialHideAt > 0 && (_s.Menu?.Open ?? false)) { _dialHideAt = -1f; UI.HideDial(true); }
            if (_after.Count > 0 && !Active && !_staging && !(_s.Cine?.Busy ?? false) && Hud.I != null)
            {
                foreach (var (text, c, secs) in _after) Hud.I.Toast(text, c, secs);
                _after.Clear();
            }
        }

        static IEnumerator Real(float secs) { float t = Time.unscaledTime; while (Time.unscaledTime - t < secs) yield return null; }
    }
}
