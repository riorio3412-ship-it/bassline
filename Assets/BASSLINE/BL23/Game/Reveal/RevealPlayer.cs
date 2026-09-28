using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Audio;
using BL23.Game.Characters;
using BL23.Game.Cinema;
using BL23.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace BL23.Game
{
    /// <summary>
    /// Read-only 3D reveal: replays what actually happened (recorded frames + ledger captions) with the real actors in the
    /// real mansion. Nothing here is generated after the fact — positions, strikes and lies all come from the recording.
    /// It opens with 그날 밤의 재구성 (Cinema.CaseRecap: the case retold as leaded-glass windows of live shots), then the full
    /// film, directed: every recorded moment gets its own shot (the hand, the eyes, a low angle, a view from the ceiling),
    /// quiet stretches get coverage, and when the two people who matter are apart, a roundel shows the other one.
    /// </summary>
    public sealed class RevealPlayer : MonoBehaviour
    {
        Session _s; GameState S => _s.S; Canvas _c; Camera _cam; public bool Active;
        /// <summary>True while 그날 밤의 재구성 plays (before the film).</summary>
        public bool RecapActive { get; private set; }
        TextMeshProUGUI _header, _clock, _caption, _log, _help, _speedT; RectTransform _bar, _cursor, _marks;
        List<ReplaySegment> _segs; ReplaySegment _seg; int _segIdx; List<(long tick, double clock, string text, string actor)> _script; int _shownIdx;
        double _t; float _speedSel = 1f; bool _auto = true, _paused, _skipSeg, _skipAll; string _focus; List<string> _focusList = new List<string>();
        float _orbit, _dist = 3.6f, _captionAt; Action _done;
        Vector3 _camPos, _camLook;
        ReplayStage _stage; PaneMontage _montage;

        public void Init(Session s)
        {
            _s = s; _c = UIKit.Root("Reveal", 40); var t = _c.transform;
            // letterbox bars: the reveal is a film of what really happened, not a HUD
            UIKit.Img(t, "LetterTop", Pal.A(Pal.Ink, 0.94f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -96), Vector2.zero);
            UIKit.Img(t, "LetterBot", Pal.A(Pal.Ink, 0.94f), new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 150));
            UIKit.Img(t, "RuleTop", Pal.A(Pal.Gold, 0.55f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -97), new Vector2(0, -96));
            UIKit.Img(t, "RuleBot", Pal.A(Pal.Gold, 0.55f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 150), new Vector2(0, 151));
            UIKit.Text(t, "Kicker", "진 상", 18, Pal.A(Pal.Gold, 0.9f), TextAlignmentOptions.MidlineLeft, Fonts.Serif, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -64), new Vector2(200, -30)).characterSpacing = 18;
            _header = UIKit.Text(t, "H", "", 28, Pal.Text, TextAlignmentOptions.MidlineLeft, Fonts.Serif, new Vector2(0, 1), new Vector2(0.62f, 1), new Vector2(150, -70), new Vector2(0, -24));
            _clock = UIKit.Text(t, "Clock", "", 28, Pal.Text, TextAlignmentOptions.MidlineRight, Fonts.Serif, new Vector2(0.62f, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(-56, -24));
            _speedT = UIKit.Text(t, "Speed", "", 17, Pal.A(Pal.Gold, 0.85f), TextAlignmentOptions.TopRight, Fonts.Body, new Vector2(0.62f, 1), new Vector2(1, 1), new Vector2(0, -126), new Vector2(-56, -104));
            // chronicle: the last few recorded moments, quiet, right side
            var logP = UIKit.Slant(t, "LogPanel", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-560, -470), new Vector2(-40, -140), Pal.A(Pal.Ink, 0.62f), Pal.A(Pal.Panel, 0.62f), Pal.A(Pal.Gold, 0.45f), 0);
            _log = UIKit.Text(logP.transform, "Log", "", 17, Pal.Text, TextAlignmentOptions.BottomLeft, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(24, 16), new Vector2(-20, -16));
            _log.lineSpacing = 12; _log.overflowMode = TextOverflowModes.Truncate;
            // subtitle: inside the lower letterbox, like a film subtitle
            _caption = UIKit.Text(t, "Cap", "", 27, Pal.Text, TextAlignmentOptions.Center, Fonts.Serif, new Vector2(0.12f, 0), new Vector2(0.88f, 0), new Vector2(0, 62), new Vector2(0, 146));
            _caption.lineSpacing = 6;
            _bar = UIKit.Rect(t, "Bar", new Vector2(0.12f, 0), new Vector2(0.88f, 0), new Vector2(0, 44), new Vector2(0, 47));
            UIKit.Img(_bar, "Bg", Pal.A(Pal.TextDim, 0.25f), Vector2.zero, Vector2.one);
            _marks = UIKit.Rect(_bar, "Marks", Vector2.zero, Vector2.one);
            _cursor = UIKit.Img(_bar, "Cursor", Pal.Gold, new Vector2(0, -2.5f), new Vector2(0, 3.5f), new Vector2(-1.5f, 0), new Vector2(1.5f, 0)).rectTransform;
            _help = UIKit.Text(t, "Help", "Space 멈춤   ←/→ 기록 이동   1–5 속도   A 자동   Tab 인물   Q/E 회전   휠 거리   C 연출/자유   Enter 다음 사건   Esc 끝", 15, Pal.A(Pal.TextDim, 0.8f), TextAlignmentOptions.Bottom, Fonts.Body, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 10), new Vector2(0, 34));
            BuildInset(t);
            _c.enabled = false;
            var camGo = new GameObject("RevealCamera", typeof(Camera)); DontDestroyOnLoad(camGo); _cam = camGo.GetComponent<Camera>(); _cam.enabled = false; _cam.fieldOfView = 50; _cam.depth = 20;
            var d = camGo.AddComponent<UniversalAdditionalCameraData>(); d.renderPostProcessing = true;
            BL23.Game.Mansion.MansionAtmosphere.SetupCamera(camGo.GetComponent<Camera>());
            _cam.nearClipPlane = 0.03f; CineDof.Register(_cam);
        }
        public void Destroy()
        {
            if (_c) UnityEngine.Object.Destroy(_c.gameObject); if (_cam) { CineDof.Forget(_cam); UnityEngine.Object.Destroy(_cam.gameObject); }
            if (_insetCam) { CineDof.Forget(_insetCam); UnityEngine.Object.Destroy(_insetCam.gameObject); } if (_insetRT) { _insetRT.Release(); UnityEngine.Object.Destroy(_insetRT); }
            _montage?.Destroy(); _montage = null;
        }

        public void Play(Action done) { _done = done; StartCoroutine(PlayCo()); }

        IEnumerator PlayCo()
        {
            Active = true; _skipAll = false; _s.Player.SetControl(false);
            _segs = Replay.BuildSegments(_s.Sim).Where(sg => sg.Frames.Count > 1 && sg.LayoutHash == S.Layout.Hash).ToList();
            MusicDirector.I?.SetState(MusicState.Reveal);
            if (_segs.Count == 0)
            {
                _c.enabled = true;
                _header.text = "진상 — 다시 볼 기록이 없다"; _caption.text = "이번 사건에는 누군가 살인을 꾸민 기록이 없다. 판결은 발표된 대로 확정된다.";
                _log.text = ""; _clock.text = ""; _speedT.text = "";
                yield return HoldOrSkip(3f);
                Finish(); yield break;
            }
            foreach (var v in _s.World.Actors.Values) { v.ReplayDriven = true; v.ForceVisible = true; }
            // 그날 밤의 재구성: the case retold once, as windows of live shots
            if (!_s.Headless)
            {
                var target = TrialSystem.TargetIncident(S); var seg = _segs.FirstOrDefault(x => target != null && x.Incident == target.Id) ?? _segs[0];
                bool watchFilm = true;
                RecapActive = true;
                yield return RecapCo(seg, r => watchFilm = r);
                RecapActive = false;
                if (!watchFilm) { Finish(); yield break; }
            }
            _c.enabled = true; _cam.enabled = true;
            for (_segIdx = 0; _segIdx < _segs.Count && !_skipAll; _segIdx++)
            {
                yield return PlaySegment(_segs[_segIdx]);
            }
            Finish();
        }

        IEnumerator RecapCo(ReplaySegment seg, Action<bool> film)
        {
            if (_montage == null) _montage = PaneMontage.Create(transform, 45);
            _c.enabled = false; _cam.enabled = false; SetInset(false);
            var st = S.Settlements.LastOrDefault(); var inc = S.Incidents.TryGetValue(seg.Incident, out var ii) ? ii : null;
            bool you = st != null && st.Correct && !st.Exception && inc?.Culprit != null && inc.Culprit != inc.Victim;
            var stage = new ReplayStage(_s, seg); stage.Begin();
            CaseRecap recap = null;
            try { recap = new CaseRecap(_s, stage, _montage, you); } catch (Exception e) { Debug.LogException(e); }
            if (recap != null && recap.BeatCount > 0)
            {
                var run = Safe(recap.Play(false)); while (run.MoveNext()) yield return run.Current;
            }
            stage.Release(); _montage.Hide();
            // the full film is there for whoever wants to look closer
            if (AutoProbe.Active) { film(true); yield break; }
            var ask = UIKit.Rect(_c.transform, "Ask", Vector2.zero, Vector2.one);
            _c.enabled = true; foreach (Transform ch in _c.transform) if (ch != ask) ch.gameObject.SetActive(false);
            UIKit.Img(ask, "Black", new Color(0.012f, 0.009f, 0.008f, 1f), Vector2.zero, Vector2.one);
            var tt = Goth.Engraved(ask, "T", "기록은 처음부터 끝까지 남아 있다", 40); tt.rectTransform.anchorMin = new Vector2(0, 0.52f); tt.rectTransform.anchorMax = new Vector2(1, 0.62f);
            var kb = Goth.KeyBar(ask, "E|처음부터 따라가 본다 · Esc|끝낸다", 20); kb.anchorMin = kb.anchorMax = new Vector2(0.5f, 0.42f); kb.pivot = new Vector2(0.5f, 0.5f); kb.anchoredPosition = Vector2.zero;
            float t0 = Time.unscaledTime; bool? pick = null;
            while (pick == null)
            {
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0)) pick = true;
                if (Input.GetKeyDown(KeyCode.Escape)) pick = false;
                if (Time.unscaledTime - t0 > 12f) pick = false;
                yield return null;
            }
            UnityEngine.Object.Destroy(ask.gameObject); foreach (Transform ch in _c.transform) ch.gameObject.SetActive(true); SetInset(false);
            film(pick.Value);
        }

        /// <summary>Runs a nested coroutine tree; an exception ends it (logged) instead of freezing the reveal.</summary>
        static IEnumerator Safe(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(root);
            while (stack.Count > 0)
            {
                var top = stack.Peek(); bool moved; object cur;
                try { moved = top.MoveNext(); cur = moved ? top.Current : null; }
                catch (Exception e) { Debug.LogException(e); yield break; }
                if (!moved) { stack.Pop(); continue; }
                if (cur is IEnumerator nested) { stack.Push(nested); continue; }
                yield return cur;
            }
        }

        void Finish()
        {
            _c.enabled = false; _cam.enabled = false; Active = false; SetInset(false); _montage?.Hide();
            _stage?.Release(); _stage = null;
            _s.World.ForceResync();
            var d = _done; _done = null; d?.Invoke();
        }

        IEnumerator HoldOrSkip(float secs)
        {
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < (_s.Headless ? 0.05f : secs)) { if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E)) break; yield return null; }
        }

        IEnumerator PlaySegment(ReplaySegment seg)
        {
            _seg = seg; _script = Replay.Script(S, seg); _shownIdx = -1; _skipSeg = false; _paused = false;
            _stage = new ReplayStage(_s, seg); _stage.Begin(); _stage.OnEvent += OnStageEvent;
            var inc = _stage.Inc;
            string victim = inc?.Victim, culprit = inc?.Culprit;
            _focusList = new List<string>(); if (culprit != null) _focusList.Add(culprit); if (victim != null) _focusList.Add(victim);
            foreach (var e in seg.Events) { if (e.Actor != null && !_focusList.Contains(e.Actor) && Cast.Get(e.Actor) != null) _focusList.Add(e.Actor); }
            _focus = _focusList.FirstOrDefault() ?? Cast.Player;
            _header.text = (_segs.Count > 1 ? $"<size=70%><color=#9A8E7C>{_segIdx + 1} / {_segs.Count}</color></size>   " : "") + $"{Cast.NameOf(victim)}의 죽음"
                + (culprit != null && culprit != victim ? $"   <size=75%><color=#C04A55>손을 댄 사람 — {Cast.NameOf(culprit)}</color></size>" : culprit == victim ? "   <size=75%><color=#9A8E7C>스스로 / 사고</color></size>" : "");
            BuildMarks();
            _t = seg.Frames[0].Tick; _log.text = ""; _caption.text = "";
            _caption.text = culprit != null && culprit != victim ? $"{Cast.NameOf(culprit)}의 실제 행동을 처음부터 따라간다." : "실제로 있었던 일을 따라간다.";
            _captionAt = Time.unscaledTime; _stage.Seek(_t);
            _shot = null; _shotEvt = null; _manualUntil = -1; _coverIdx = 0;
            yield return null;
            Coverage(true); SnapCam();
            long end = seg.Frames[seg.Frames.Count - 1].Tick; int probeShots = 0; float probeAt = Time.unscaledTime + 2.5f;
            while (_t < end && !_skipSeg && !_skipAll)
            {
                Controls();
                float spd = 0;
                if (!_paused)
                {
                    spd = _speedSel; if (_auto) spd *= AutoFactor();
                    _t = Math.Min(end, _t + Time.unscaledDeltaTime * SimTime.PerSecond * spd * (_s.Headless ? 400 : 1));
                }
                _stage.Rate = spd; _stage.Frozen = _paused; _stage.Sounds = !_s.Headless && spd < 3f;
                _stage.Advance(_t); _curSpeed = spd;
                Captions(); UpdateCam(Time.unscaledDeltaTime); UpdateInset(Time.unscaledDeltaTime); UpdateUi(end);
                if (AutoProbe.Active && probeShots < 3 && Time.unscaledTime > probeAt && _shot != null) { probeShots++; probeAt = Time.unscaledTime + 4.5f; AutoProbe.Shot($"reveal_cine_{probeShots}"); }
                yield return null;
            }
            _stage.OnEvent -= OnStageEvent;
            if (!_skipAll) { _caption.text = "<color=#9A8E7C>— 기록은 여기서 끝난다 —</color>"; _caption.alpha = 1; _captionAt = Time.unscaledTime; yield return HoldOrSkip(1.8f); }
            _stage.Release();
        }

        // ---------------------------------------------------------------- captions
        void Captions()
        {
            int idx = -1; for (int i = 0; i < _script.Count; i++) if (_script[i].tick <= _t) idx = i;
            if (idx == _shownIdx) return;
            bool forward = idx > _shownIdx;
            _shownIdx = idx;
            if (idx >= 0)
            {
                var c = _script[idx]; _caption.text = $"<size=62%><color=#9CC4B2>{ClockFmt.Vague(c.clock)}</color></size>\n{c.text}"; _captionAt = Time.unscaledTime;
                if (c.actor != null && _focusList.Contains(c.actor) && _auto) _focus = c.actor;
                Sfx.Play("ui_page", null, 0.35f);
                // a recorded moment gets its own shot
                if (forward && _curSpeed < 3.5f) { var e = _seg.Events.LastOrDefault(x => x.Tick == c.tick && (x.Actor == c.actor || c.actor == null)); if (e != null) Direct(e); }
            }
            // chronicle: earlier lines fade; the current one sits at the bottom in parchment
            var lines = new List<string>(); int from = Math.Max(0, idx - 5);
            for (int i = from; i <= idx; i++)
            {
                int age = idx - i; string col = age == 0 ? "#F2E8D4" : age == 1 ? "#B8AC98" : "#7E7466";
                lines.Add($"<color={col}>" + (age == 0 ? "<color=#D6AD62>◆</color> " : "   ") + _script[i].text + "</color>");
            }
            _log.text = string.Join("\n", lines);
        }

        void OnStageEvent(LedgerEvent e)
        {
            if (e.Type == "BodySeen" && _stage != null && _stage.Sounds) MusicDirector.I?.Stinger("discovery", 0.6f);
            // the weapon in the hand from the moment it is taken until it is put away
            var inc = _stage?.Inc; if (inc == null || inc.Culprit == null || string.IsNullOrEmpty(inc.WeaponType) || ItemCatalog.Get(inc.WeaponType) == null) return;
            if (e.Actor == inc.Culprit && e.Type == "PickUp" && (e.Item == inc.Weapon || (e.Data ?? "").StartsWith(inc.WeaponType))) _stage.HoldProp(inc.Culprit, inc.WeaponType, inc.Weapon);
            else if (e.Actor == inc.Culprit && (e.Type == "HideItem" || e.Type == "PlantWeapon" || e.Type == "Burn" || e.Type == "DumpWater" || e.Type == "Bury")) _stage.ClearProp();
        }

        // ---------------------------------------------------------------- pacing
        float AutoFactor()
        {
            // 1× near recorded events, fast through idle stretches (the recording itself is not altered)
            long next = long.MaxValue, prev = long.MinValue;
            foreach (var c in _script) { if (c.tick > _t) { next = Math.Min(next, c.tick); } else prev = Math.Max(prev, c.tick); }
            double dn = next == long.MaxValue ? 1e9 : next - _t, dp = prev == long.MinValue ? 1e9 : _t - prev;
            if (dn < 40 || dp < 60) return 1f;
            if (dn < 150) return 3f;
            return 12f;
        }

        void Controls()
        {
            if (Input.GetKeyDown(KeyCode.Space)) _paused = !_paused;
            if (Input.GetKeyDown(KeyCode.Escape)) _skipAll = true;
            if (Input.GetKeyDown(KeyCode.Return)) _skipSeg = true;
            if (Input.GetKeyDown(KeyCode.A)) _auto = !_auto;
            if (Input.GetKeyDown(KeyCode.Alpha1)) _speedSel = 0.5f; if (Input.GetKeyDown(KeyCode.Alpha2)) _speedSel = 1f; if (Input.GetKeyDown(KeyCode.Alpha3)) _speedSel = 2f;
            if (Input.GetKeyDown(KeyCode.Alpha4)) _speedSel = 4f; if (Input.GetKeyDown(KeyCode.Alpha5)) _speedSel = 8f;
            if (Input.GetKeyDown(KeyCode.RightArrow)) { var n = _script.FirstOrDefault(c => c.tick > _t + 1); if (n.text != null) { _t = Math.Max(_seg.Frames[0].Tick, n.tick - 20); _stage.Seek(_t); _shownIdx = -2; } }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { var p = _script.LastOrDefault(c => c.tick < _t - 40); _t = p.text != null ? Math.Max(_seg.Frames[0].Tick, p.tick - 20) : _seg.Frames[0].Tick; _stage.Seek(_t); _shownIdx = -2; }
            if (Input.GetKeyDown(KeyCode.Tab) && _focusList.Count > 0) { int i = _focusList.IndexOf(_focus); _focus = _focusList[(i + 1) % _focusList.Count]; if (Manual) SnapCam(); else Coverage(true); }
            if (Input.GetKeyDown(KeyCode.C)) { _manualUntil = Manual ? -1 : float.MaxValue; if (!Manual) Coverage(true); else SnapCam(); }
            bool turned = false;
            if (Input.GetKey(KeyCode.Q)) { _orbit -= Time.unscaledDeltaTime * 70; turned = true; } if (Input.GetKey(KeyCode.E)) { _orbit += Time.unscaledDeltaTime * 70; turned = true; }
            if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f) { _dist = Mathf.Clamp(_dist - Input.mouseScrollDelta.y * 0.4f, 1.6f, 9f); turned = true; }
            if (turned && !Manual) { _manualUntil = Time.unscaledTime + 8f; SnapCam(); } else if (turned && _manualUntil < float.MaxValue) _manualUntil = Math.Max(_manualUntil, Time.unscaledTime + 8f);
        }

        // ---------------------------------------------------------------- camera: directed (shots per recorded moment) or free (orbit)
        CineShot _shot; float _shotT0, _shotDur = 6f; LedgerEvent _shotEvt; float _manualUntil = -1; int _coverIdx; float _curSpeed = 1f; string _shotRoomKey;
        bool Manual => Time.unscaledTime < _manualUntil;

        void Cut(ShotSpec spec, float dur)
        {
            if (spec == null || spec.Subject == null && spec.Point == null) return;
            CineShot sh = null; try { sh = CineSolver.Solve(spec); } catch (Exception e) { Debug.LogException(e); }
            if (sh == null) return;
            _shot = sh; _shotT0 = Time.unscaledTime; _shotDur = dur; _shot.Apply(_cam, 0f, 0f);
            if (AutoProbe.Active) Debug.Log($"[CINE] film {spec} -> {sh.Spec} target {(sh.Target != null ? sh.Target() : sh.Base):F2} cam {_cam.transform.position:F2} fwd {_cam.transform.forward:F2} fov {_cam.fieldOfView:F1}");
            _camPos = _cam.transform.position; _camLook = _camPos + _cam.transform.forward * 3f;
            var mv = _s.World?.Mansion; if (mv != null) mv.Cull(_cam.transform.position);
            var r = CineSolver.RoomAt(CineAnchors.Head(spec.Subject ?? _focus)); _shotRoomKey = (spec.Subject ?? "") + "@" + (r?.Id ?? -1);
        }

        /// <summary>Choose the shot for a recorded moment (who does what decides the angle).</summary>
        void Direct(LedgerEvent e)
        {
            if (Manual) return;
            string c = _stage?.Culprit, v = _stage?.Victim; float side = (_coverIdx++ % 2 == 0) ? 1f : -1f; string who = e.Actor ?? _focus;
            if (_s.World.ViewOf(who) == null) who = _focus;
            ShotSpec s;
            switch (e.Type)
            {
                case "AttackBegin": case "Garrote": case "Shove": case "Smother": s = new ShotSpec(ShotKind.Two, who, e.Target, side) { Dutch = true }; break;
                case "Strike": s = new ShotSpec(ShotKind.Hand, who, null, side); break;
                case "Death": s = new ShotSpec(ShotKind.Top, e.Actor ?? v, null, side); break;
                case "Resist": case "Flee": case "Scratched": s = new ShotSpec(ShotKind.Face, who, null, side) { Dutch = true }; break;
                case "BodySeen": s = new ShotSpec(ShotKind.Face, who, null, side); break;
                case "PickUp": case "Wash": case "HideItem": case "PlantWeapon": case "Dose": case "Sedate": case "PoisonPlant": case "Burn": case "DumpWater": case "Bury": case "TrapArmed": case "ShockRigged": s = new ShotSpec(ShotKind.Hand, who, null, side); break;
                case "Invite": case "CourierAsk": case "Handover": case "Lie": s = e.Target != null && _s.World.ViewOf(e.Target) != null ? new ShotSpec(ShotKind.Over, who, e.Target, side) { Rack = true } : new ShotSpec(ShotKind.Face, who, null, side); break;
                case "CarryStart": case "CarryEnd": s = new ShotSpec(ShotKind.Top, who, null, side); break;
                case "SealedRoom": case "LockedRoomMade": case "KeySlide": s = new ShotSpec(ShotKind.Thing, who, null, side) { Point = _s.World.ToWorld(e.Pos) + Vector3.up * 1.0f }; break;
                case "FakeMessage": case "TodShift": case "ColdHide": s = new ShotSpec(ShotKind.Top, e.Target ?? v ?? who, null, side); break;
                case "SealedFound": s = new ShotSpec(ShotKind.Establish, v ?? who); break;
                case "DisguiseOn": case "DisguiseOff": case "ChangeClothes": case "GuiseAs": s = new ShotSpec(ShotKind.Medium, who, null, side); break;
                default: s = new ShotSpec(who == c ? ShotKind.Medium : ShotKind.Face, who, null, side); break;
            }
            _shotEvt = e; Cut(s, e.Type == "Death" || e.Type == "AttackBegin" ? 6.5f : 5f);
        }

        /// <summary>Between recorded moments: coverage of whoever the film is following (wide when the film runs fast).</summary>
        void Coverage(bool force)
        {
            if (Manual) return;
            string id = _focus; if (_s.World.ViewOf(id) == null) return;
            var st = _stage.StateOf(id); float side = (_coverIdx++ % 2 == 0) ? 1f : -1f;
            ShotSpec s;
            if (st.status >= 2) s = new ShotSpec(_coverIdx % 2 == 0 ? ShotKind.Top : ShotKind.Establish, id, null, side);
            else if (_curSpeed >= 3f) s = new ShotSpec(_coverIdx % 3 == 0 ? ShotKind.Top : ShotKind.Establish, id, null, side);
            else if (st.moving) { var k = new[] { ShotKind.Behind, ShotKind.Feet, ShotKind.Medium, ShotKind.Establish }; s = new ShotSpec(k[_coverIdx % k.Length], id, null, side); }
            else { var k = new[] { ShotKind.Medium, ShotKind.Orbit, ShotKind.Face, ShotKind.Low, ShotKind.Establish }; s = new ShotSpec(k[_coverIdx % k.Length], id, null, side); }
            _shotEvt = null; Cut(s, _curSpeed >= 3f ? 4f : 6.5f);
        }

        void Desired(out Vector3 pos, out Vector3 look)
        {
            var v = _s.World.ViewOf(_focus); var head = v != null ? v.HeadPos : Vector3.zero;
            if (v != null && _stage != null && _stage.StateOf(v.Id).status >= 2) head = v.transform.position + Vector3.up * 0.4f;
            var rot = Quaternion.Euler(22, (v != null ? v.transform.eulerAngles.y : 0) + 150 + _orbit, 0);
            var want = head + rot * Vector3.back * _dist + Vector3.up * 0.3f;
            var dir = want - head; float len = dir.magnitude;
            if (Physics.SphereCast(head, 0.2f, dir.normalized, out var hit, len, ~0, QueryTriggerInteraction.Ignore)) want = head + dir.normalized * Mathf.Max(0.5f, hit.distance - 0.1f);
            pos = want; look = head;
        }
        void SnapCam() { Desired(out _camPos, out _camLook); if (Manual || _shot == null) { _cam.transform.position = _camPos; _cam.transform.LookAt(_camLook); } }
        void UpdateCam(float dt)
        {
            if (!Manual && _shot != null)
            {
                float u = (Time.unscaledTime - _shotT0) / Mathf.Max(0.5f, _shotDur);
                _shot.Apply(_cam, Mathf.Min(u, 1.15f), dt);
                // the subject walked out of the room (or the film sped up): new coverage
                bool expired = u >= 1f;
                if (!expired && _shotEvt == null && _shot.Spec?.Subject != null && Time.unscaledTime - _shotT0 > 1.2f)
                {
                    var r = CineSolver.RoomAt(CineAnchors.Head(_shot.Spec.Subject)); if (((_shot.Spec.Subject ?? "") + "@" + (r?.Id ?? -1)) != _shotRoomKey) expired = true;
                }
                if (expired) Coverage(false);
                return;
            }
            CineDof.Off(_cam); _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, 50f, 1f - Mathf.Exp(-3f * dt));
            Desired(out var p, out var l);
            _camPos = Vector3.Lerp(_camPos, p, 1f - Mathf.Exp(-4f * dt)); _camLook = Vector3.Lerp(_camLook, l, 1f - Mathf.Exp(-6f * dt));
            _cam.transform.position = _camPos; _cam.transform.rotation = Quaternion.LookRotation((_camLook - _camPos).normalized);
        }

        // ---------------------------------------------------------------- the roundel: meanwhile, the other one
        RectTransform _inset; PaneGraphic _insetImg; LeadFrame _insetLead; TextMeshProUGUI _insetLabel; CanvasGroup _insetCg; Camera _insetCam; RenderTexture _insetRT; CineShot _insetShot; float _insetT0, _insetK; string _insetWho;
        void BuildInset(Transform t)
        {
            _inset = UIKit.Rect(t, "Inset", new Vector2(0, 1), new Vector2(0, 1)); _inset.pivot = new Vector2(0, 1); _inset.sizeDelta = new Vector2(300, 300); _inset.anchoredPosition = new Vector2(56, -130);
            _insetCg = _inset.gameObject.AddComponent<CanvasGroup>(); _insetCg.alpha = 0; _insetCg.blocksRaycasts = false;
            var sh = UIKit.Rect(_inset, "Shade", Vector2.zero, Vector2.one, new Vector2(-40, -40), new Vector2(40, 40)); var si = sh.gameObject.AddComponent<RawImage>(); si.texture = GTex.Glow; si.color = new Color(0, 0, 0, 0.7f); si.raycastTarget = false;
            var circle = Poly.Circle(Vector2.zero, 138f, 48);
            var img = UIKit.Rect(_inset, "Glass", Vector2.zero, Vector2.one); _insetImg = img.gameObject.AddComponent<PaneGraphic>(); _insetImg.Shape = circle; _insetImg.raycastTarget = false; _insetImg.Vignette = 0.35f;
            var lead = UIKit.Rect(_inset, "Lead", Vector2.zero, Vector2.one); _insetLead = lead.gameObject.AddComponent<LeadFrame>(); _insetLead.Shape = circle; _insetLead.Width = 13f; _insetLead.Bosses = false; _insetLead.raycastTarget = false;
            _insetLabel = Goth.Text(_inset, "Label", "", 18, GPal.A(GPal.Brass, 0.95f), TextAlignmentOptions.Top, true, new Vector2(0, 0), new Vector2(1, 0), new Vector2(-40, -40), new Vector2(40, -8)); _insetLabel.characterSpacing = 3;
        }
        void SetInset(bool on) { if (!on) { _insetWho = null; _insetShot = null; if (_insetCam) _insetCam.enabled = false; } }
        void UpdateInset(float dt)
        {
            string c = _stage?.Culprit, v = _stage?.Victim; string other = null;
            if (c != null && v != null && c != v && !_paused)
            {
                var sc = _stage.StateOf(c); var sv = _stage.StateOf(v);
                if (sc.status == 1 && sv.status == 1)
                {
                    var rc = CineSolver.RoomAt(sc.pos + Vector3.up); var rv = CineSolver.RoomAt(sv.pos + Vector3.up);
                    other = _focus == c ? v : _focus == v ? c : null;
                    var op = other == v ? sv.pos : sc.pos;
                    // only when they are apart, and the other one is somewhere the house is drawing (same floor, not far)
                    if (other != null && (rc == null || rv == null || rc.Id == rv.Id || Mathf.Abs(op.y - _cam.transform.position.y) > 2.5f || Vector3.Distance(op, _cam.transform.position) > 9f)) other = null;
                }
            }
            if (other != null && other != _insetWho)
            {
                if (_insetCam == null)
                {
                    var go = new GameObject("RevealInsetCam", typeof(Camera)); DontDestroyOnLoad(go); _insetCam = go.GetComponent<Camera>(); _insetCam.enabled = false; go.AddComponent<UniversalAdditionalCameraData>();
                    BL23.Game.Mansion.MansionAtmosphere.SetupCamera(_insetCam); _insetCam.depth = 19; _insetCam.cullingMask = ~(1 << 5); _insetCam.nearClipPlane = 0.03f; CineDof.Register(_insetCam);
                    _insetRT = new RenderTexture(384, 384, 24, RenderTextureFormat.ARGB32) { name = "RevealInset" }; _insetRT.Create(); _insetCam.targetTexture = _insetRT; _insetImg.Tex = _insetRT; _insetImg.Refresh();
                }
                try { _insetShot = CineSolver.Solve(new ShotSpec(ShotKind.Face, other, null, 1)); } catch (Exception e) { Debug.LogException(e); _insetShot = null; }
                _insetWho = _insetShot != null ? other : null; _insetT0 = Time.unscaledTime; _insetLabel.text = _insetWho != null ? "그 시각 · " + Cast.GivenOf(_insetWho) : "";
                if (_insetShot != null) Sfx.Play("quill", null, 0.25f);
            }
            if (other == null) _insetWho = null;
            bool show = _insetWho != null && _insetShot != null;
            _insetK = Mathf.MoveTowards(_insetK, show ? 1f : 0f, dt * 2.5f); _insetCg.alpha = _insetK;
            _insetLead.Draw = _insetK; _insetLead.Refresh();
            if (_insetCam != null) _insetCam.enabled = _insetK > 0.01f && _insetShot != null;
            if (_insetCam != null && _insetCam.enabled) _insetShot.Apply(_insetCam, Mathf.Min(1.2f, (Time.unscaledTime - _insetT0) / 7f), dt);
        }

        // ---------------------------------------------------------------- UI
        void BuildMarks()
        {
            UIKit.Clear(_marks); long t0 = _seg.Frames[0].Tick, t1 = _seg.Frames[_seg.Frames.Count - 1].Tick; if (t1 <= t0) return;
            foreach (var c in _script)
            {
                float x = Mathf.Clamp01((float)(c.tick - t0) / (t1 - t0));
                var col = c.text.Contains("숨을 거둔다") ? Pal.Blood : c.text.Contains("달려든다") || c.text.Contains("└") ? Pal.Magenta : c.text.Contains("거짓말") ? Pal.Gold : Pal.A(Pal.Cyan, 0.8f);
                UIKit.Img(_marks, "M", col, new Vector2(x, -1.5f), new Vector2(x, 2.5f), new Vector2(-1, 0), new Vector2(1, 0));
            }
        }

        void UpdateUi(long end)
        {
            long t0 = _seg.Frames[0].Tick; float x = end > t0 ? Mathf.Clamp01((float)((_t - t0) / (end - t0))) : 0; _cursor.anchorMin = new Vector2(x, -0.8f); _cursor.anchorMax = new Vector2(x, 1.8f);
            double ck = _stage != null ? _stage.ClockAt(_t) : _seg.Frames[0].Clock;
            _clock.text = $"{ClockFmt.Day(ck)}일째 · {ClockFmt.Period(ck)}  <size=62%><color=#D6AD62>{ClockFmt.BellShort(ck)}</color></size>";
            _speedT.text = (_paused ? "멈춤" : $"×{_speedSel:0.#}" + (_auto ? " · 자동" : "")) + $"   ·   {Cast.GivenOf(_focus)}의 곁에서" + (Manual ? "   ·   자유 시점" : "");
            // captions fade out instead of vanishing
            float age = Time.unscaledTime - _captionAt; _caption.alpha = age < 6f ? 1f : Mathf.Clamp01(1f - (age - 6f) / 1.2f);
        }
    }
}
