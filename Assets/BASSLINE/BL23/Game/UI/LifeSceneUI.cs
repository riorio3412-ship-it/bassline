using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Audio;
using BL23.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Pose = BL23.Sim.Pose;

namespace BL23.Game
{
    /// <summary>
    /// DAILY LIFE on screen (Sim/Life). Three jobs, all presentation:
    ///  1. Staged scenes — two residents flaring up in 민혁's room, the Morning/Evening Table, a festival he walked into, the
    ///     memorial: a calm camera from speaker to speaker, gothic subtitles, then his one choice (numbered buttons) and what
    ///     follows. The kernel decides everything (LifeOffer / LifeTable / LifePick); this only shows it.
    ///  2. Conversations that should open by themselves — a heart event at the appointment, the moment at the end of time
    ///     spent together — and the resident saying what they came to say ("lifego") right after the greeting.
    ///  3. The end of "함께 시간을 보낸다" (TimeDirector.Ended) → LifeAfterTogether.
    /// Self-starting like TogetherScene; the table hooks in through CinematicUI.TableTalk (one line).
    /// </summary>
    public sealed class LifeSceneUI : MonoBehaviour
    {
        static LifeSceneUI _i;
        public static LifeSceneUI I => _i;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (_i != null) return; var go = new GameObject("BL23 LifeSceneUI"); DontDestroyOnLoad(go); _i = go.AddComponent<LifeSceneUI>(); }

        /// <summary>A staged daily-life scene is on screen.</summary>
        public static bool Playing => _i != null && _i._playing;

        Canvas _c; SlantPanel _box, _titlePanel, _notePanel; TextMeshProUGUI _name, _text, _cont, _title, _note, _keys; RectTransform _opts; Camera _cam;
        readonly List<UIKit.Btn> _btns = new List<UIKit.Btn>(); int _sel = -1; int _picked = -1;
        bool _playing; float _nextPoll; TimeDirector _dir;
        string _autoGoNpc; int _autoGoOpen = -1;
        readonly Dictionary<string, string> _autoOpened = new Dictionary<string, string>();
        (string npc, float at)? _openAfter;

        Session S0 => Session.I;

        // ================================================================== building the UI (lazily, the first time a scene plays)
        void Build()
        {
            if (_c != null) return;
            _c = UIKit.Root("LifeScene", 46);
            var t = _c.transform;
            _box = UIKit.Slant(t, "Box", new Vector2(0, 0), new Vector2(1, 0), new Vector2(170, 34), new Vector2(-170, 236), Pal.A(Pal.Panel2, 0.995f), Pal.A(Pal.Ink, 0.995f), Pal.A(Pal.Gold, 0.8f), 0);
            _box.Glow = 0.06f;
            var np = UIKit.Slant(_box.transform, "NamePanel", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -6), new Vector2(340, 44), Pal.A(Pal.Ink, 0.98f), Pal.A(Pal.Panel2, 0.98f), Pal.A(Pal.Gold, 0.9f), 0);
            _name = UIKit.Text(np.transform, "Name", "", 26, Pal.Text, TextAlignmentOptions.Center, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-10, 0));
            _name.characterSpacing = 3;
            _text = UIKit.Text(_box.transform, "Text", "", 30, Pal.Text, TextAlignmentOptions.TopLeft, Fonts.SerifLight, Vector2.zero, Vector2.one, new Vector2(78, 28), new Vector2(-90, -62));
            _text.lineSpacing = 8;
            _cont = UIKit.Text(_box.transform, "Cont", "◆", 18, Pal.A(Pal.Gold, 0.9f), TextAlignmentOptions.BottomRight, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(0, 16), new Vector2(-46, 0));
            _keys = UIKit.Text(_box.transform, "Keys", "E 넘기기  ·  Esc 건너뛰기  ·  숫자 고르기", 15, Pal.A(Pal.TextDim, 0.8f), TextAlignmentOptions.BottomLeft, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(80, 8), new Vector2(-80, 0));
            _titlePanel = UIKit.Slant(t, "TitlePanel", new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -118), new Vector2(620, -58), Pal.A(Pal.Ink, 0.92f), Pal.A(Pal.Panel2, 0.92f), Pal.A(Pal.Gold, 0.7f), 0);
            _titlePanel.EdgeLeftOnly = true;
            _title = UIKit.Text(_titlePanel.transform, "Title", "", 22, Pal.Gold, TextAlignmentOptions.MidlineLeft, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(22, 0), new Vector2(-12, 0));
            _title.characterSpacing = 2;
            _notePanel = UIKit.Slant(_box.transform, "NotePanel", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-760, 8), new Vector2(-40, 46), Pal.A(Pal.Ink, 0.95f), Pal.A(Pal.Panel2, 0.95f), Pal.A(Pal.Gold, 0.8f), 0);
            _notePanel.EdgeLeftOnly = true;
            _note = UIKit.Text(_notePanel.transform, "Note", "", 19, Pal.A(Pal.Gold, 0.95f), TextAlignmentOptions.MidlineLeft, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-10, 0));
            _note.textWrappingMode = TextWrappingModes.NoWrap; _note.overflowMode = TextOverflowModes.Ellipsis;
            _notePanel.gameObject.SetActive(false);
            _opts = UIKit.Rect(t, "Options", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-780, 270), new Vector2(-90, 760));
            _c.enabled = false;
            var camGo = new GameObject("LifeSceneCamera", typeof(Camera)); DontDestroyOnLoad(camGo);
            _cam = camGo.GetComponent<Camera>(); _cam.enabled = false; _cam.fieldOfView = 38f; _cam.nearClipPlane = 0.05f; _cam.depth = 26;
            var d = camGo.AddComponent<UniversalAdditionalCameraData>(); d.renderPostProcessing = true; d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            try { BL23.Game.Mansion.MansionAtmosphere.SetupCamera(_cam); } catch (Exception e) { Debug.LogException(e); }
        }

        void OnDestroy() { if (_dir != null) _dir.Ended -= OnSkipEnded; if (_i == this) _i = null; }

        // ================================================================== the table (CinematicUI.TableTalk hook)
        /// <summary>CinematicUI.TableTalk → here. True when daily life staged the table (the old table talk is skipped).</summary>
        public static bool TableTalk(Session s, List<string> diners, string meal)
        {
            if (_i == null || s == null || s.Headless || s.Sim == null || _i._playing) return false;
            LifeStage st = null;
            try { st = s.Sim.LifeTable(diners, meal); } catch (Exception e) { Debug.LogException(e); }
            if (st == null) return false;
            _i.StartCoroutine(_i.Play(st));
            return true;
        }

        // ================================================================== the poller
        void Update()
        {
            var s = S0; if (s == null || s.Sim == null) return;
            // the time director of this session: hear when "함께 시간을 보낸다" ends
            var dir = TimeLink.Dir;
            if (!ReferenceEquals(dir, _dir)) { if (_dir != null) _dir.Ended -= OnSkipEnded; _dir = dir; if (_dir != null) _dir.Ended += OnSkipEnded; }
            if (_playing) return;
            AutoGo(s);
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.35f;
            if (s.Headless || !Free(s)) return;
            try { s.Sim.LifeCheckDue(); } catch (Exception e) { Debug.LogException(e); }
            // a conversation that opens by itself: the moment after time together, a heart event at its appointment
            if (_openAfter != null && Time.unscaledTime >= _openAfter.Value.at)
            {
                var who = _openAfter.Value.npc; _openAfter = null;
                if (OpenFor(s, who)) return;
            }
            var me = s.S.Player;
            foreach (var a in s.S.LivingNpcs.Where(x => x.Room == me.Room && x.Pos.f == me.Pos.f).OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                string kind = null; try { kind = s.Sim.LifeGoKind(a.Id); } catch (Exception) { }
                if (kind != "heart" && kind != "hang") continue;
                string mark = kind + ":" + s.S.Day;
                if (_autoOpened.TryGetValue(a.Id, out var m0) && m0 == mark) continue;
                if (a.Pos.DistXZ(me.Pos) > 9f) continue;
                _autoOpened[a.Id] = mark;
                if (OpenFor(s, a.Id)) return;
            }
            // a staged scene here and now
            LifeStage st = null;
            try { st = s.Sim.LifeOffer(); } catch (Exception e) { Debug.LogException(e); }
            if (st != null) StartCoroutine(Play(st));
        }

        static bool Free(Session s)
        {
            if (s.S.Phase != Phase.Daily || s.Paused) return false;
            if ((s.Dialogue?.Active ?? false) || (s.Cine?.Busy ?? false) || TimeLink.TimeActive || (s.Menu?.Open ?? false)) return false;
            if ((s.Trial?.Active ?? false) || (s.Reveal?.Active ?? false) || DiscoveryFilm.Active != null || CaseReport.Open) return false;
            if (s.Player == null || !s.Player.Controlling || s.Player.Scripted) return false;
            if (Time.frameCount == DialogueUI.ClosedFrame) return false;
            var ps = s.Sim.PendingStop; if (ps != null && ps.Class == StopClass.Critical) return false;
            var me = s.S.Player; return me != null && me.Alive && me.Pose != Pose.Sleep;
        }

        bool OpenFor(Session s, string npcId)
        {
            var a = s.S.A(npcId); var me = s.S.Player;
            if (a == null || !a.Alive || me == null || a.Room != me.Room) return false;
            if (!s.Sim.CanTalk(a, out _)) return false;
            s.Dialogue.Open(a);
            return s.Dialogue.Active;
        }

        /// <summary>In a conversation with someone who came to say something: pick "lifego" for them once the greeting is over.</summary>
        void AutoGo(Session s)
        {
            var dlg = s.Dialogue; if (dlg == null || !dlg.Active) { _autoGoNpc = null; return; }
            var npc = s.S.Actors.Values.FirstOrDefault(x => !x.IsPlayer && x.TalkingTo == Cast.Player);
            if (npc == null) return;
            if (_autoGoNpc != npc.Id) { _autoGoNpc = npc.Id; _autoGoOpen = 0; }
            if (_autoGoOpen > 0 || !dlg.ShowingOptions) return;
            bool wants = false; try { wants = s.Sim.LifeGoKind(npc.Id) != null; } catch (Exception) { }
            if (!wants) return;
            _autoGoOpen = 1;
            try { dlg.ProbeChoose("lifego"); } catch (Exception e) { Debug.LogException(e); }
        }

        void OnSkipEnded(SkipPlan p, SkipResult r)
        {
            try
            {
                var s = S0; if (s == null || s.Sim == null || p == null || p.Kind != SkipKind.Together || p.Partner == null) return;
                var stop = r?.Stop ?? p.Stop;
                bool full = stop == null || stop.Kind == StopKind.Target;
                if (s.Sim.LifeAfterTogether(p.Partner, p.TogetherId, p.Activity, full)) _openAfter = (p.Partner, Time.unscaledTime + 1.0f);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ================================================================== playing a staged scene
        IEnumerator Play(LifeStage st)
        {
            var s = S0; if (s == null || st == null) yield break;
            Build();
            _playing = true;
            bool ownBusy = s.Cine != null && !s.Cine.Busy; if (ownBusy) s.Cine.Busy = true;
            s.Pause("life"); s.Player?.SetControl(false);
            _c.enabled = true; _cam.enabled = true;
            _title.text = TitleOf(st); _titlePanel.gameObject.SetActive(!string.IsNullOrEmpty(_title.text));
            var views = st.Cast.Select(id => s.World.ViewOf(id)).Where(v => v != null).ToList();
            var center = Vector3.zero; foreach (var v in views) center += v.HeadPos; if (views.Count > 0) center /= views.Count; else center = s.Player.Cam.transform.position + s.Player.Cam.transform.forward * 2f;
            _camPos = s.Player.Cam.transform.position; _camLook = center; _pv = _lv = Vector3.zero;
            int guard = 0;
            while (st != null && guard++ < 12)
            {
                bool skip = false;
                foreach (var u in st.Lines)
                {
                    if (Aborted(s)) { s.Sim.LifeStageAbort(); goto done; }
                    if (skip) { SafeSpoken(s, u); continue; }
                    yield return Line(s, u, center, st, r => skip = r);
                }
                if (!string.IsNullOrEmpty(st.Note)) { ShowNote(st.Note); Hud.I?.SysNote(st.Note, 3f); }
                if (st.Options == null || st.Options.Count == 0 || st.Done) break;
                // 민혁's choice
                _picked = -1; ShowOptions(st.Options);
                _text.text = ""; _name.text = "민혁"; _cont.enabled = false;
                while (_picked < 0)
                {
                    if (Aborted(s)) { ClearOptions(); s.Sim.LifeStageAbort(); goto done; }
                    OptionKeys();
                    CameraStep(center);
                    yield return null;
                }
                ClearOptions(); UISfx.Confirm();
                LifeStage next = null;
                try { next = s.Sim.LifePick(_picked); } catch (Exception e) { Debug.LogException(e); }
                st = next;
            }
        done:
            yield return Hold(0.4f);
            _notePanel.gameObject.SetActive(false);
            _c.enabled = false; _cam.enabled = false;
            s.Player?.SetControl(true); s.Resume("life");
            if (ownBusy && s.Cine != null) s.Cine.Busy = false;
            _playing = false; _nextPoll = Time.unscaledTime + 1.5f;
        }

        static bool Aborted(Session s)
        {
            if (s.S.Phase != Phase.Daily || DiscoveryFilm.Active != null || (s.Trial?.Active ?? false)) return true;
            var ps = s.Sim.PendingStop; return ps != null && ps.Class == StopClass.Critical;
        }

        static string TitleOf(LifeStage st)
        {
            string k = st.Kind == "table" ? "식탁" : st.Kind == "pair" ? "두 사람" : st.Kind == "fest" ? "모임" : "";
            if (st.Kind == "fest" && st.Title == "추모의 밤") k = "추모";
            return string.IsNullOrEmpty(st.Title) ? k : (k.Length > 0 ? $"{k}  <color=#9A8E7C>—</color>  {st.Title}" : st.Title);
        }

        void SafeSpoken(Session s, Utterance u) { try { s.Sim.Spoken(u); } catch (Exception e) { Debug.LogException(e); } Backlog.Add(u.Speaker, u.Text, s.S.Clock, s.S.RoomName(s.S.Player.Room)); }

        Vector3 _camPos, _camLook, _pv, _lv, _wantPos, _wantLook;

        IEnumerator Line(Session s, Utterance u, Vector3 center, LifeStage st, Action<bool> skipAll)
        {
            var pages = LineBank.Pages(u.Text); if (pages.Length == 0) pages = new[] { "…" };
            bool isMe = u.Speaker == Cast.Player;
            var v = s.World.ViewOf(isMe ? (u.Listener ?? st.Cast.FirstOrDefault(x => x != Cast.Player)) : u.Speaker);
            if (v != null)
            {
                var head = v.HeadPos; var face = v.transform.forward; face.y = 0; if (face.sqrMagnitude < 0.01f) face = (center - head).normalized; face.Normalize();
                _wantPos = isMe ? s.Player.Cam.transform.position : TalkCam(v, head, face, Vector3.Cross(Vector3.up, face));
                _wantLook = head;
            }
            Backlog.Add(u.Speaker, u.Text, s.S.Clock, s.S.RoomName(s.S.Player.Room));
            for (int p = 0; p < pages.Length; p++)
            {
                string page = pages[p];
                _name.text = Cast.NameOf(u.Speaker); _text.text = page; _text.maxVisibleCharacters = 0; _cont.enabled = false;
                if (!isMe && v != null) { VoiceBabble.Speak(u.Speaker, page); v.Talk(Mathf.Clamp(page.Length * 0.06f, 0.8f, 5f)); SpeechGestures.Perform(v, page, u.Emotion, false); }
                float typed = 0; bool done = false; float t0 = Time.unscaledTime;
                while (true)
                {
                    CameraStep(center);
                    bool adv = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
                    bool fast = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    if (Input.GetKeyDown(KeyCode.Escape)) { skipAll(true); _text.maxVisibleCharacters = 99999; break; }
                    typed += Time.unscaledDeltaTime * 38f * Settings.TextSpeed; if (fast) typed = 9999;
                    int n = Mathf.Min(page.Length, (int)typed); _text.maxVisibleCharacters = n; done = n >= page.Length;
                    _cont.enabled = done && ((int)(Time.unscaledTime * 3) % 2 == 0);
                    if (adv && Time.unscaledTime - t0 > 0.15f) { if (!done) typed = 9999; else break; }
                    if (done && (fast || Settings.AutoAdvance) && Time.unscaledTime - t0 > (fast ? 0.1f : Mathf.Clamp(0.9f + page.Length * 0.045f, 1.2f, 4.5f))) break;
                    yield return null;
                }
                if (Input.GetKeyDown(KeyCode.Escape)) break;
            }
            try { s.Sim.Spoken(u); } catch (Exception e) { Debug.LogException(e); }
        }

        IEnumerator Hold(float secs) { for (float t = 0; t < secs; t += Time.unscaledDeltaTime) { CameraStep(_camLook); yield return null; } }

        void CameraStep(Vector3 center)
        {
            if (_cam == null || !_cam.enabled) return;
            float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            if (_wantPos == Vector3.zero) { _wantPos = _camPos; _wantLook = center; }
            _camPos = Vector3.SmoothDamp(_camPos, _wantPos, ref _pv, 0.8f, 3f, dt);
            _camLook = Vector3.SmoothDamp(_camLook, _wantLook, ref _lv, 0.6f, 4f, dt);
            var dir = _camLook - _camPos; if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            _cam.transform.position = _camPos; _cam.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        /// <summary>Where to film someone talking: in front of the face, a little to the side and above, the first spot with a clear line.</summary>
        static Vector3 TalkCam(ActorView v, Vector3 head, Vector3 fwd, Vector3 side)
        {
            var target = head + Vector3.up * 0.02f;
            Vector3[] cands =
            {
                head + fwd * 1.35f + side * 0.3f + Vector3.up * 0.2f,
                head + fwd * 1.25f - side * 0.35f + Vector3.up * 0.3f,
                head + fwd * 1.6f + side * 0.5f + Vector3.up * 0.35f,
                head + fwd * 1.0f + Vector3.up * 0.45f,
                head + fwd * 2.0f + Vector3.up * 0.6f,
            };
            foreach (var c in cands)
            {
                if (Physics.CheckSphere(c, 0.12f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Linecast(c, target, out var hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<ActorView>() != v) continue;
                return c;
            }
            return cands[0];
        }

        // ------------------------------------------------------------------ choices
        void ShowOptions(List<string> labels)
        {
            ClearOptions();
            float step = Mathf.Min(62f, 480f / Mathf.Max(1, labels.Count));
            for (int i = 0; i < labels.Count && i < 9; i++)
            {
                int idx = i;
                var b = UIKit.Button(_opts, $"<color=#9A8E7C>{i + 1}</color>   {labels[i]}", () => { _picked = idx; }, new Vector2(0, 1), new Vector2(1, 1), new Vector2(i * 10, -step - i * step), new Vector2(0, -8 - i * step), step >= 50 ? 24 : 20, 18, Fonts.Bold);
                b.Label.textWrappingMode = TextWrappingModes.NoWrap; b.Label.overflowMode = TextOverflowModes.Ellipsis;
                _btns.Add(b);
            }
            _sel = -1;
        }

        void OptionKeys()
        {
            for (int i = 0; i < _btns.Count; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) { _picked = i; return; }
            if (_btns.Count == 0) return;
            if (Input.GetKeyDown(KeyCode.DownArrow)) Select(_sel < 0 ? 0 : (_sel + 1) % _btns.Count);
            else if (Input.GetKeyDown(KeyCode.UpArrow)) Select(_sel < 0 ? _btns.Count - 1 : (_sel - 1 + _btns.Count) % _btns.Count);
            else if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && _sel >= 0) _picked = _sel;
        }

        void Select(int i) { for (int k = 0; k < _btns.Count; k++) if (_btns[k]) { _btns[k].Hover = k == i; _btns[k].Paint(); } _sel = i; UISfx.Hover(); }
        void ClearOptions() { foreach (var b in _btns) if (b) Destroy(b.gameObject); _btns.Clear(); _sel = -1; }

        void ShowNote(string text)
        {
            if (_note == null || string.IsNullOrEmpty(text)) return;
            string t = "<color=#D6AD62>◆</color> " + text;
            _note.text = t; float w = Mathf.Min(1100f, _note.GetPreferredValues(t).x + 40f);
            var rt = _notePanel.rectTransform; rt.offsetMin = new Vector2(-40 - w, 8); rt.offsetMax = new Vector2(-40, 46);
            _notePanel.gameObject.SetActive(true);
        }
    }
}
