using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Audio;
using BL23.Game.Characters;
using BL23.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BL23.Game
{
    /// <summary>
    /// Real-time 3D conversation. The camera cuts between shots built from where the two actors actually stand
    /// (establishing two-shot, over-the-shoulder, reverse, reaction close-up), kept inside the room so it never reveals
    /// other spaces. Options are BASSLINE's own (life / investigation), not a copied wheel.
    /// </summary>
    public sealed class DialogueUI : MonoBehaviour
    {
        Session _s; GameState S => _s.S; Canvas _c; public bool Active; Actor _npc;
        /// <summary>True while a bond scene (a personal story) is being told: the music turns intimate.</summary>
        public static bool BondScene;
        Camera _cam; Vector3 _camPos, _camLook; Vector3 _wantPos, _wantLook; float _cut; Vector3 _posVel, _lookVel; float _wantFov = 38f, _fovVel;
        SlantPanel _box; TextMeshProUGUI _name, _text, _cont; RectTransform _opts; List<UIKit.Btn> _btns = new List<UIKit.Btn>();
        readonly Queue<Utterance> _queue = new Queue<Utterance>(); Utterance _cur; string[] _pages; int _page; float _typed; bool _waiting; string _lastShot;
        List<DialogueOption> _options; DialogueOption _sub; int _subPage; bool _more; int _sel = -1; TextMeshProUGUI _keys, _filed; SlantPanel _filedPanel; float _autoAt = -1f, _filedUntil; int _openFrame = -1;
        /// <summary>The frame a conversation ended on (the E that ended it must not start another).</summary>
        public static int ClosedFrame = -1;
        const int SubPage = 6;
        // daily small talk that folds behind "더 이야기하기 ▶" when the list grows past nine (presentation only)
        static readonly HashSet<string> MoreIds = new HashSet<string> { "likes", "gossip", "compliment", "tease", "casual", "confide", "apologize", "confess", "warn" };
        // questions whose answer does not change within a day: asked again, they say so (presentation memory)
        static readonly HashSet<string> AskIds = new HashSet<string> { "likes", "confide", "q_suspect" };
        static readonly HashSet<string> _asked = new HashSet<string>();

        public void Init(Session s)
        {
            _s = s; _c = UIKit.Root("Dialogue", 30); var t = _c.transform;
            // same language as the trial box: dark lacquer, a gilt rule, parchment type
            _box = UIKit.Slant(t, "Box", new Vector2(0, 0), new Vector2(1, 0), new Vector2(150, 36), new Vector2(-150, 262), Pal.A(Pal.Panel2, 0.995f), Pal.A(Pal.Ink, 0.995f), Pal.A(Pal.Gold, 0.8f), 0);   // (linear blending: 0.95 reads as ~75% on screen)
            _box.Glow = 0.06f;
            var namePanel = UIKit.Slant(_box.transform, "NamePanel", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -6), new Vector2(360, 46), Pal.A(Pal.Ink, 0.98f), Pal.A(Pal.Panel2, 0.98f), Pal.A(Pal.Gold, 0.9f), 0);
            _name = UIKit.Text(namePanel.transform, "Name", "", 28, Pal.Text, TextAlignmentOptions.Center, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-10, 0));
            _name.characterSpacing = 3;
            _text = UIKit.Text(_box.transform, "Text", "", 31, Pal.Text, TextAlignmentOptions.TopLeft, Fonts.SerifLight, Vector2.zero, Vector2.one, new Vector2(80, 30), new Vector2(-90, -66));
            _text.lineSpacing = 8;
            _cont = UIKit.Text(_box.transform, "Cont", "◆", 18, Pal.A(Pal.Gold, 0.9f), TextAlignmentOptions.BottomRight, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(0, 18), new Vector2(-50, 0));
            _keys = UIKit.Text(_box.transform, "Keys", "", 16, Pal.A(Pal.TextDim, 0.85f), TextAlignmentOptions.BottomRight, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(80, 10), new Vector2(-80, 0));
            _keys.textWrappingMode = TextWrappingModes.NoWrap;
            // a clue filed during the conversation: one quiet line above the box (the HUD toast is not shown for it)
            _filedPanel = UIKit.Slant(_box.transform, "FiledPanel", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-560, 8), new Vector2(-40, 46), Pal.A(Pal.Ink, 0.95f), Pal.A(Pal.Panel2, 0.95f), Pal.A(Pal.Gold, 0.8f), 0); _filedPanel.EdgeLeftOnly = true;
            _filed = UIKit.Text(_filedPanel.transform, "Filed", "", 19, Pal.A(Pal.Gold, 0.95f), TextAlignmentOptions.MidlineLeft, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-10, 0));
            _filedPanel.gameObject.SetActive(false);
            _filed.textWrappingMode = TextWrappingModes.NoWrap; _filed.overflowMode = TextOverflowModes.Ellipsis;
            _opts = UIKit.Rect(t, "Options", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-760, 300), new Vector2(-80, 940));
            _c.enabled = false;
            var camGo = new GameObject("ConversationCamera", typeof(Camera)); DontDestroyOnLoad(camGo);
            _cam = camGo.GetComponent<Camera>(); _cam.enabled = false; _cam.nearClipPlane = 0.05f; _cam.fieldOfView = 38;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            BL23.Game.Mansion.MansionAtmosphere.SetupCamera(camGo.GetComponent<Camera>());
        }

        public void Destroy() { if (_c) UnityEngine.Object.Destroy(_c.gameObject); if (_cam) UnityEngine.Object.Destroy(_cam.gameObject); }

        public void Open(Actor npc)
        {
            if (Active) return;
            if (!_s.Sim.CanTalk(npc, out var why)) { Hud.I?.Toast(why, Pal.TextDim, 1.5f); return; }
            Active = true; _npc = npc; _s.Pause("dialogue"); _openFrame = Time.frameCount;
            _s.Sim.BeginTalk(npc);
            _c.enabled = true; _cam.enabled = true; _s.Player.Cam.enabled = false;
            var me = _s.World.ViewOf(Cast.Player); if (me != null) me.ForceVisible = true;   // conversations (and the trial) are where 민혁 is seen on screen
            MusicDirector.I?.Duck(true);
            _lastShot = null; ShotEstablish(true);
            bool first = !S.K(Cast.Player).Facts.Contains("met:" + npc.Id);
            var opening = new Utterance { Speaker = npc.Id, Listener = Cast.Player, Key = first ? "intro_self" : "greet", Emotion = Emotion.Neutral };
            opening.Text = _s.Sim.Render(npc.Id, Cast.Player, npc.IsButler ? "y_idle" : (first ? "intro_self" : (S.R(npc.Id, Cast.Player).Opinion < -0.3 ? "greet_cold" : S.R(npc.Id, Cast.Player).Opinion > 0.45 ? "greet_close" : (S.Minute < 11 * 60 ? "greet_morning" : "greet")))) ?? "…";
            if (first) { S.K(npc.Id).Facts.Add("met:" + Cast.Player); S.K(Cast.Player).Facts.Add("met:" + npc.Id); }
            var lines = new List<Utterance> { opening };
            // they came with an invitation or a favour: they say it straight away
            var rq = _s.Sim.OfferFrom(npc); if (rq != null) lines.Add(new Utterance { Speaker = npc.Id, Listener = Cast.Player, Key = "request", Text = rq.Text, Emotion = Emotion.Smile, Gesture = Anim.Talk });
            Enqueue(lines);
        }

        public void Close()
        {
            if (!Active) return;
            Active = false; BondScene = false; ClosedFrame = Time.frameCount; _c.enabled = false; _cam.enabled = false; _s.Player.Cam.enabled = true; if (_filedPanel != null) _filedPanel.gameObject.SetActive(false);
            if (_key != null) { _key.enabled = false; _rim.enabled = false; _keyK = 0; }
            var me = _s.World.ViewOf(Cast.Player); if (me != null) me.ForceVisible = false;
            _s.Sim.EndTalk(_npc); _npc = null; _s.Resume("dialogue"); VoiceBabble.Stop(); MusicDirector.I?.Duck(false);
            _sub = null; _subPage = 0; _more = false; _lastSpoken = null; _lastSpokenBy = null;
            ClearOptions();
        }
        string _lastSpoken, _lastSpokenBy;   // the NPC's last line, kept in the box while you choose what to say next

        void Enqueue(List<Utterance> us) { foreach (var u in us) _queue.Enqueue(u); NextLine(); }

        void NextLine()
        {
            ClearOptions();
            if (_queue.Count == 0) { _cur = null; ShowOptions(); return; }
            _cur = _queue.Dequeue(); if (_cur.Key == "bond") BondScene = true; _pages = LineBank.Pages(_cur.Text); if (_pages.Length == 0) _pages = new[] { "…" }; _page = 0;
            Backlog.Add(_cur.Speaker, _cur.Text, S.Clock, S.RoomName(_npc != null ? _npc.Room : -1));
            StartPage();
        }

        void StartPage()
        {
            _typed = 0; _waiting = false; _autoAt = -1f; RefreshKeys();
            var sp = _cur.Speaker; _name.text = Cast.NameOf(sp);
            _text.text = _pages[_page]; _text.maxVisibleCharacters = 0;
            if (sp != Cast.Player) { _lastSpoken = _pages[_page]; _lastSpokenBy = sp; }
            VoiceBabble.Speak(sp, _pages[_page]);
            var v = _s.World.ViewOf(sp); v?.Talk(Mathf.Clamp(_pages[_page].Length * 0.06f, 0.6f, 5f));
            SpeechGestures.Perform(v, _pages[_page], _cur.Emotion, _cur.Gesture == Anim.Point);
            // reactions: the listener reacts to strong emotions
            var lv = _s.World.ViewOf(_cur.Listener);
            SpeechGestures.React(lv, _cur.Emotion, _cur.Gesture == Anim.Point);
            ChooseShot(sp);
        }

        void Update()
        {
            if (!Active) return;
            UpdateCamera(Time.deltaTime);
            if (_filedPanel != null && _filedPanel.gameObject.activeSelf && Time.unscaledTime > _filedUntil) _filedPanel.gameObject.SetActive(false);
            if (BacklogUI.Open || Time.frameCount == BacklogUI.ClosedFrame || Time.frameCount == _openFrame) return;   // reading 지난 대화: the conversation waits (and the E that opened it is not an advance)
            if (Input.GetKeyDown(KeyCode.V)) { Settings.AutoAdvance = !Settings.AutoAdvance; _autoAt = -1f; RefreshKeys(); UISfx.Confirm(); }   // the key bar shows the state
            float wheel = Input.mouseScrollDelta.y;
            if (_cur != null)
            {
                if (wheel > 0.1f) { BacklogUI.Show(_s); return; }
                bool fast = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                float speed = 38f * Settings.TextSpeed;
                _typed += Time.deltaTime * speed; if (fast) _typed = 9999;
                int n = Mathf.Min(_pages[_page].Length, (int)_typed); _text.maxVisibleCharacters = n;
                _waiting = n >= _pages[_page].Length; _cont.enabled = _waiting && ((int)(Time.time * 3) % 2 == 0);
                bool adv = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
                // auto-advance (V) and Ctrl fast-forward: a finished page moves on by itself
                if (_waiting && !adv && (fast || Settings.AutoAdvance))
                {
                    if (_autoAt < 0) _autoAt = Time.unscaledTime + (fast ? 0.12f : Mathf.Clamp(0.9f + _pages[_page].Length * 0.045f, 1.2f, 4.5f) / Mathf.Max(0.3f, Settings.TextSpeed));
                    if (Time.unscaledTime >= _autoAt) adv = true;
                }
                if (adv)
                {
                    if (!_waiting) { _typed = 9999; }
                    else if (_page + 1 < _pages.Length) { _page++; StartPage(); }
                    else { _s.Sim.Spoken(_cur); var ended = _cur.Key == "bye"; NextLine(); if (ended && _queue.Count == 0) Close(); }
                }
                return;
            }
            // --- choosing: 1–9 (0 = tenth) · ↑/↓ + Enter · mouse wheel pages a long list (up at the first page: 지난 대화)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_sub != null) { _sub = null; _subPage = 0; ShowOptions(); }
                else if (_more) { _more = false; ShowOptions(); }
                else Choose(new DialogueOption { Id = "bye" });
                return;
            }
            if (wheel < -0.1f && _sub != null && (_subPage + 1) * SubPage < _sub.Sub.Count) { _subPage++; UISfx.Page(); ShowOptions(); return; }
            if (wheel > 0.1f) { if (_sub != null && _subPage > 0) { _subPage--; UISfx.Page(); ShowOptions(); } else BacklogUI.Show(_s); return; }
            for (int i = 0; i < Mathf.Min(10, _numbered.Count); i++)
                if (Input.GetKeyDown(i == 9 ? KeyCode.Alpha0 : KeyCode.Alpha1 + i) || Input.GetKeyDown(i == 9 ? KeyCode.Keypad0 : KeyCode.Keypad1 + i)) { _numbered[i].Click(); return; }
            if (_btns.Count > 0)
            {
                if (Input.GetKeyDown(KeyCode.DownArrow)) Select(_sel < 0 ? 0 : (_sel + 1) % _btns.Count);
                else if (Input.GetKeyDown(KeyCode.UpArrow)) Select(_sel < 0 ? _btns.Count - 1 : (_sel - 1 + _btns.Count) % _btns.Count);
                else if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && _sel >= 0 && _sel < _btns.Count) _btns[_sel].Click();
            }
        }

        void Select(int i)
        {
            for (int k = 0; k < _btns.Count; k++) if (_btns[k]) { _btns[k].Hover = k == i; _btns[k].Paint(); }
            _sel = i; UISfx.Hover();
        }

        /// <summary>A clue was filed while talking: "◆ 수첩에 적었다 — 민서의 증언" above the box for 3 s (instead of a HUD toast).</summary>
        public void Filed(string title, bool key)
        {
            if (_filed == null || string.IsNullOrEmpty(title)) return;
            string t = (key ? "<color=#D6AD62>◆ 중요 단서</color>" : "수첩에 적었다") + " — " + title + "  <size=80%><color=#9A8E7C>Tab</color></size>";
            _filed.text = t; float w = Mathf.Min(900f, _filed.GetPreferredValues(t).x + 40f);
            var rt = _filedPanel.rectTransform; rt.offsetMin = new Vector2(-40 - w, 8); rt.offsetMax = new Vector2(-40, 46);
            _filedPanel.gameObject.SetActive(true); _filedUntil = Time.unscaledTime + 3f;
        }

        void RefreshKeys()
        {
            if (_keys == null) return;
            _keys.text = (Settings.AutoAdvance ? "<color=#D6AD62>V 자동 진행 켬</color>" : "V 자동 진행") + "  ·  Ctrl 빨리 넘기기  ·  휠 위로 지난 대화";
        }

        // ------------------------------------------------------------------ options
        readonly List<UIKit.Btn> _numbered = new List<UIKit.Btn>();
        string AskKey(string opt) => $"{_npc?.Id}:{opt}:{S.Day}";

        void ShowOptions()
        {
            ClearOptions(); _text.text = ""; _name.text = Cast.NameOf(_npc.Id); _cont.enabled = false; RefreshKeys();
            ShotOptions();
            if (_sub != null && _sub.Sub != null)
            {
                // a long list (people to warn about, cards to show) in pages of six: 이전 / 다음 ▶ rows and the mouse wheel
                var list = _sub.Sub; int pages = Mathf.Max(1, (list.Count + SubPage - 1) / SubPage); _subPage = Mathf.Clamp(_subPage, 0, pages - 1);
                var rows = new List<(string label, Action a, bool num)>();
                foreach (var (id, label) in list.Skip(_subPage * SubPage).Take(SubPage)) { var arg = id; rows.Add((label, () => { var o = _sub; _sub = null; _subPage = 0; Choose(o, arg); }, true)); }
                if (_subPage > 0) rows.Add(("<color=#9A8E7C>이전</color>", () => { _subPage--; UISfx.Page(); ShowOptions(); }, false));
                if (_subPage < pages - 1) rows.Add(($"<color=#D6AD62>다음 ▶</color>  <size=75%><color=#9A8E7C>{_subPage + 1}/{pages}</color></size>", () => { _subPage++; UISfx.Page(); ShowOptions(); }, false));
                rows.Add(("<color=#9A8E7C>돌아가기  Esc</color>", () => { _sub = null; _subPage = 0; ShowOptions(); }, false));
                Layout(rows);
                // the box asks the question the list answers ("누구를 조심하라고 할까?"), fully shown (the typewriter's count is reset)
                _text.maxVisibleCharacters = 99999;
                _text.text = $"{SubQuestion(_sub)}{(pages > 1 ? $"  <size=80%><color=#9A8E7C>{_subPage + 1}/{pages}쪽 · 휠로 넘기기</color></size>" : "")}";
                return;
            }
            _options = _s.Sim.Options(_npc);
            var main = _options; var extra = new List<DialogueOption>();
            if (main.Count > 9) { extra = main.Where(o => MoreIds.Contains(o.Id)).ToList(); if (extra.Count >= 2) main = main.Where(o => !MoreIds.Contains(o.Id)).ToList(); else extra.Clear(); }
            if (extra.Count == 0) _more = false;
            var shown = _more ? extra : main;
            var rowsMain = new List<(string label, Action a, bool num)>(); var enabled = new List<bool>();
            foreach (var o in shown)
            {
                var opt = o;
                bool done = o.Done || (AskIds.Contains(o.Id) && _asked.Contains(AskKey(o.Id)));
                string label = (o.Investigation ? "<color=#D6AD62>◆</color> " : "") + (done ? $"<color=#B8AC98>{o.Label}</color>  <size=78%><color=#9A8E7C>· 이미 물었다</color></size>" : o.Label);
                if (!o.Enabled && !string.IsNullOrEmpty(o.Why)) label += $"  <size=78%><color=#9A8E7C>— {o.Why}</color></size>";
                rowsMain.Add((label, () => { if (opt.Sub != null && opt.Sub.Count > 0) { _sub = opt; _subPage = 0; ShowOptions(); } else Choose(opt); }, true)); enabled.Add(o.Enabled);
            }
            if (!_more && extra.Count > 0) { int at = rowsMain.Count; var bye = shown.FindIndex(o => o.Id == "bye" || o.Id == "follow" || o.Id == "unfollow"); if (bye >= 0) at = bye; rowsMain.Insert(at, ("더 이야기하기 ▶", () => { _more = true; ShowOptions(); }, true)); enabled.Insert(at, true); }
            if (_more) { rowsMain.Add(("<color=#9A8E7C>돌아가기  Esc</color>", () => { _more = false; ShowOptions(); }, false)); enabled.Add(true); }
            Layout(rowsMain);
            for (int i = 0; i < _btns.Count && i < enabled.Count; i++) if (!enabled[i]) { _btns[i].Interactable = false; _btns[i].Paint(); }
            // the box keeps the last thing said (so the choice has its context) and, dim below it, how they feel about you
            _text.maxVisibleCharacters = 99999;
            string last = _lastSpoken != null && _lastSpokenBy == _npc.Id ? $"“{_lastSpoken}”\n" : "";
            _text.text = last + "<size=85%><color=#9A8E7C>" + RelationLine() + "</color></size>";
        }

        /// <summary>What a sub-list is choosing, as a question in the box.</summary>
        static string SubQuestion(DialogueOption o)
        {
            switch (o?.Id)
            {
                case "warn": return "누구를 조심하라고 할까?";
                case "show": return "어떤 단서를 보여 줄까?";
                case "giveback": return "무엇을 돌려줄까?";
                case "gift": return "무엇을 선물할까?";
                case "together": return "무엇을 함께 할까?";
                case "invite": return "무엇을 같이 하자고 할까?";
            }
            return o?.Label ?? "";
        }

        /// <summary>Numbered rows ("1  …"), compressed when the list is long so it never runs into the text box.</summary>
        void Layout(List<(string label, Action a, bool num)> rows)
        {
            float step = Mathf.Min(62f, 640f / Mathf.Max(1, rows.Count)); int n = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i]; string key = r.num && n < 10 ? (n == 9 ? "0" : (n + 1).ToString()) : null;
                var b = AddBtn(i, (key != null ? $"<color=#9A8E7C>{key}</color>   " : "      ") + r.label, r.a, step);
                if (r.num && n < 10) { _numbered.Add(b); n++; }
            }
            _sel = -1;
        }

        string RelationLine()
        {
            var r = S.R(_npc.Id, Cast.Player); var mem = S.R(Cast.Player, _npc.Id).Memory.LastOrDefault() ?? r.Memory.LastOrDefault();
            string tone = r.Opinion > 0.5 ? "당신을 편하게 대한다" : r.Opinion > 0.2 ? "당신에게 호감이 있다" : r.Opinion < -0.3 ? "당신을 경계하고 있다" : "아직 당신을 어색해한다";
            if (r.Tags.Contains("lover")) tone = "당신에게 마음을 주었다"; else if (r.Tags.Contains("friend")) tone = "당신을 친구로 여긴다";
            return LineBank.FixParticles($"{Cast.GivenOf(_npc.Id)}은(는) {tone}.") + (mem != null ? $"  (마음에 남은 일: {mem.Substring(Math.Min(mem.Length, mem.IndexOf(' ') + 1))})" : "");
        }

        UIKit.Btn AddBtn(int i, string label, Action a, float step = 62f)
        {
            var b = UIKit.Button(_opts, label, a, new Vector2(0, 1), new Vector2(1, 1), new Vector2(i * 10, -step - i * step), new Vector2(0, -8 - i * step), step >= 50 ? 24 : 20, 18, Fonts.Bold);
            b.Label.textWrappingMode = TextWrappingModes.NoWrap; b.Label.overflowMode = TextOverflowModes.Ellipsis;
            _btns.Add(b); return b;
        }

        void ClearOptions() { foreach (var b in _btns) if (b) UnityEngine.Object.Destroy(b.gameObject); _btns.Clear(); _numbered.Clear(); _sel = -1; }

        void Choose(DialogueOption o, string arg = null)
        {
            if (o.Id == "bye" && _npc == null) { Close(); return; }
            _more = false;
            if (_npc != null && AskIds.Contains(o.Id)) _asked.Add(AskKey(o.Id));
            var lines = _s.Sim.Choose(_npc, o.Id, arg ?? o.Arg);
            if (o.Id == "bye" && lines.Count == 0) { Close(); return; }
            if (o.Id == "bye") { foreach (var l in lines) if (l.Key != "bye" && l.Speaker != Cast.Player) l.Key = "bye"; if (lines.Count > 0) lines[lines.Count - 1].Key = "bye"; }
            Enqueue(lines);
        }

        // ------------------------------------------------------------------ probe helpers (AutoProbe.Qol)
        /// <summary>Probe only: say every queued line at once (as if E were pressed through them).</summary>
        public void ProbeFinishLines()
        {
            int guard = 0;
            while (Active && _cur != null && guard++ < 80) { _s.Sim.Spoken(_cur); var ended = _cur.Key == "bye"; NextLine(); if (ended && _queue.Count == 0) { Close(); return; } }
        }
        /// <summary>Probe only: pick an option by id (false when it is not offered).</summary>
        public bool ProbeChoose(string id)
        {
            if (!Active || _npc == null) return false;
            var o = _s.Sim.Options(_npc).FirstOrDefault(x => x.Id == id); if (o == null) return false;
            Choose(o); return true;
        }
        /// <summary>Probe only: open the "더 이야기하기" group (false when the list is short enough not to fold).</summary>
        public bool ProbeMore() { if (!Active || _cur != null) return false; _more = true; ShowOptions(); return _more; }
        /// <summary>Probe only: open an option's sub-list at a page.</summary>
        public bool ProbeSub(string id, int page)
        {
            if (!Active || _cur != null || _npc == null) return false;
            var o = _s.Sim.Options(_npc).FirstOrDefault(x => x.Id == id && x.Sub != null && x.Sub.Count > 0); if (o == null) return false;
            _sub = o; _subPage = page; ShowOptions(); return true;
        }
        public bool ShowingOptions => Active && _cur == null;

        // ------------------------------------------------------------------ camera director
        void ChooseShot(string speaker)
        {
            if (_cur == null) return;
            bool strong = _cur.Emotion == Emotion.Angry || _cur.Emotion == Emotion.Fear || _cur.Emotion == Emotion.Sad || _cur.Emotion == Emotion.Surprised || _cur.Key == "secret_share" || _cur.Key == "love_confess" || _cur.Key == "confess";
            if (speaker == Cast.Player && _lastShot != null) return;   // your own lines keep the current framing (no ping-pong)
            string shot = speaker == Cast.Player ? "reverse" : strong ? "close" : (_lastShot == "ots" && _page == 0 && UnityEngine.Random.value < 0.35f ? "two" : "ots");
            if (shot == _lastShot && _page > 0) return;
            _lastShot = shot;
            switch (shot) { case "reverse": ShotOTS(Cast.Player, _npc.Id); break; case "close": ShotClose(speaker); break; case "two": ShotEstablish(false); break; default: ShotOTS(_npc.Id, Cast.Player); break; }
        }

        Vector3 Head(string id) => _s.World.ViewOf(id)?.HeadPos ?? Vector3.zero;

        void ShotOTS(string subject, string over)
        {
            var a = Head(subject); var b = Head(over); var axis = (a - b); axis.y = 0; var dir = axis.normalized; var right = Vector3.Cross(Vector3.up, dir);
            // try both shoulders and a tighter framing; take the first one with a clear line to the face
            // (people count too: a bystander such as Yusti standing in the line must never hide the speaker)
            var cands = new[] { b - dir * 0.75f + right * 0.42f, b - dir * 0.75f - right * 0.42f, b - dir * 0.35f + right * 0.55f, b - dir * 0.35f - right * 0.55f,
                                a - dir * 1.3f + right * 0.3f, a - dir * 1.3f - right * 0.3f, a - dir * 1.1f + right * 0.75f + Vector3.up * 0.25f, a - dir * 1.1f - right * 0.75f + Vector3.up * 0.25f };
            var look = a + Vector3.down * 0.05f; var pos = cands[0] + Vector3.up * 0.1f;
            _occSubject = subject; _occOver = over;
            foreach (var c in cands) { var p = c + Vector3.up * 0.1f; if (!Occluded(p, look)) { pos = p; break; } }
            Aim(pos, look, subject);
        }

        void ShotClose(string subject)
        {
            var v = _s.World.ViewOf(subject); var a = Head(subject); var fwd = v != null ? v.transform.forward : Vector3.forward; var rt = v != null ? v.transform.right : Vector3.right;
            _occSubject = subject; _occOver = null;
            var cands = new[] { a + fwd * 0.95f + rt * 0.18f, a + fwd * 0.95f - rt * 0.18f, a + fwd * 0.8f + rt * 0.45f, a + fwd * 0.8f - rt * 0.45f, a + fwd * 0.7f + Vector3.up * 0.2f };
            var pos = cands[0] + Vector3.up * 0.02f;
            foreach (var c in cands) { var p = c + Vector3.up * 0.02f; if (!Occluded(p, a)) { pos = p; break; } }
            Aim(pos, a, subject, 32);
        }

        void ShotEstablish(bool snap)
        {
            var a = Head(_npc.Id); var b = Head(Cast.Player); var mid = (a + b) / 2; var axis = (a - b); axis.y = 0; var side = Vector3.Cross(Vector3.up, axis.normalized);
            float d = Mathf.Max(2.2f, axis.magnitude * 1.6f);
            _occSubject = _npc.Id; _occOver = Cast.Player;
            var cands = new[] { mid + side * d, mid - side * d, mid + side * d * 0.7f + axis.normalized * 0.8f, mid - side * d * 0.7f + axis.normalized * 0.8f, mid + side * d * 0.7f - axis.normalized * 0.8f, mid - side * d * 0.7f - axis.normalized * 0.8f };
            var pos = cands[0] + Vector3.up * 0.25f;
            foreach (var c in cands) { var p = c + Vector3.up * 0.25f; if (!Blocked(mid, p) && !ActorBlocks(p, a) && !ActorBlocks(p, b)) { pos = p; break; } }
            Aim(pos, mid + Vector3.down * 0.25f, null, 42); if (snap) { _camPos = _wantPos; _camLook = _wantLook; _cam.fieldOfView = _wantFov; _posVel = _lookVel = Vector3.zero; }
        }

        string _occSubject, _occOver;
        /// <summary>Is someone's body between the lens and the face? Bystanders are capsules (feet to head); the shoulder we
        /// shoot over only counts with its head, so an over-the-shoulder frame stays allowed.</summary>
        bool ActorBlocks(Vector3 cam, Vector3 face)
        {
            var seg = face - cam; float len = seg.magnitude; if (len < 0.05f) return false; var dir = seg / len;
            foreach (var a in S.Actors.Values)
            {
                if (a == null || a.Id == _occSubject) continue;
                var v = _s.World.ViewOf(a.Id); if (v == null || !v.gameObject.activeInHierarchy) continue;
                var head = v.HeadPos; var feet = v.transform.position;
                bool over = a.Id == _occOver; float radius = over ? 0.13f : 0.27f;
                for (int k = over ? 4 : 0; k <= 4; k++)
                {
                    var p = Vector3.Lerp(feet + Vector3.up * 0.35f, head, k / 4f);
                    float t = Vector3.Dot(p - cam, dir); if (t < 0.1f || t > len - 0.2f) continue;
                    if ((cam + dir * t - p).sqrMagnitude < radius * radius) return true;
                }
            }
            return false;
        }

        void ShotOptions()
        {
            ShotOTS(_npc.Id, Cast.Player);
            // the numbered options fill the right ~40% of the screen: frame the face left of centre so it never sits behind them
            var fwd = _wantLook - _wantPos; float dist = fwd.magnitude; fwd.y = 0;
            if (fwd.sqrMagnitude < 1e-4f) return;
            fwd.Normalize(); _wantLook += Vector3.Cross(Vector3.up, fwd) * dist * 0.2f;
        }

        void Aim(Vector3 pos, Vector3 look, string subject, float fov = 38)
        {
            // keep the lens inside this room and out of walls; fall back to a safer, closer framing
            var room = S.Layout.Room(_npc.Room);
            if (room != null) { float y = pos.y; var r = room.Rect.Inset(0.25f); pos.x = Mathf.Clamp(pos.x, r.x0, r.x1); pos.z = Mathf.Clamp(pos.z, r.z0, r.z1); pos.y = y; }
            if (Blocked(look, pos)) { var dir = (pos - look).normalized; float dist = Vector3.Distance(pos, look); if (Physics.Raycast(look, dir, out var h, dist, ~0, QueryTriggerInteraction.Ignore)) pos = h.point - dir * 0.15f; }
            _wantPos = pos; _wantLook = look; _wantFov = fov; _cut = 1f;
            // no hard cuts: the lens eases to the new framing (see UpdateCamera); only the opening shot snaps
        }

        /// <summary>Anything solid or any large visible mesh (even decor without colliders) between the lens and the face.</summary>
        bool Occluded(Vector3 cam, Vector3 face)
        {
            if (Blocked(face, cam)) return true;
            if (ActorBlocks(cam, face)) return true;
            var ray = new Ray(cam, (face - cam).normalized); float dist = Vector3.Distance(cam, face) - 0.35f;
            var root = _s.World.Mansion != null ? _s.World.Mansion.transform : null; if (root == null) return false;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                var bnd = r.bounds; if (bnd.size.y < 1.2f || bnd.size.x > 6f || bnd.size.z > 6f) continue;   // columns, statues, tall decor — not floors/walls
                if (bnd.IntersectRay(ray, out var t) && t > 0.05f && t < dist) return true;
            }
            return false;
        }

        bool Blocked(Vector3 from, Vector3 to)
        {
            var d = to - from; if (!Physics.Raycast(from, d.normalized, out var h, d.magnitude, ~0, QueryTriggerInteraction.Ignore)) return false;
            return h.collider.GetComponentInParent<ActorRig>() == null && h.collider.GetComponentInParent<PlayerController>() == null;
        }

        void UpdateCamera(float dt)
        {
            // subtle handheld drift keeps shots alive without new information
            float t = Time.time; var drift = new Vector3(Mathf.Sin(t * 0.37f), Mathf.Sin(t * 0.29f + 1), 0) * 0.012f;
            // calm cinematography: ease toward the wanted framing (about a second), never whip
            _camPos = Vector3.SmoothDamp(_camPos, _wantPos, ref _posVel, 0.85f, 2.2f, Mathf.Max(0.0001f, dt));
            _camLook = Vector3.SmoothDamp(_camLook, _wantLook, ref _lookVel, 0.65f, 3f, Mathf.Max(0.0001f, dt));
            _cam.fieldOfView = Mathf.SmoothDamp(_cam.fieldOfView, _wantFov, ref _fovVel, 0.9f, 20f, Mathf.Max(0.0001f, dt));
            _cam.transform.position = _camPos + drift; _cam.transform.rotation = Quaternion.LookRotation((_camLook - _camPos).normalized, Vector3.up);
            PortraitLights(dt);
        }

        // ---- portrait lighting: a soft warm key from the lens side and a cool rim on whoever is being looked at
        Light _key, _rim; float _keyK;
        void PortraitLights(float dt)
        {
            if (_key == null)
            {
                _key = new GameObject("TalkKeyLight").AddComponent<Light>(); _key.type = LightType.Spot; _key.spotAngle = 42f; _key.innerSpotAngle = 14f; _key.range = 6f; _key.color = new Color(1f, 0.88f, 0.76f); _key.shadows = LightShadows.None;
                _rim = new GameObject("TalkRimLight").AddComponent<Light>(); _rim.type = LightType.Point; _rim.range = 2.2f; _rim.color = new Color(0.62f, 0.72f, 1f); _rim.shadows = LightShadows.None;
                DontDestroyOnLoad(_key.gameObject); DontDestroyOnLoad(_rim.gameObject);
            }
            string who = _cur != null && _cur.Speaker != null ? _cur.Speaker : _npc?.Id;
            var v = who != null ? _s.World.ViewOf(who) : null;
            _keyK = Mathf.MoveTowards(_keyK, Active && v != null ? 1f : 0f, dt * 2f);
            if (v != null)
            {
                var head = v.HeadPos; var toCam = _camPos - head; toCam.y = 0; if (toCam.sqrMagnitude < 1e-4f) toCam = Vector3.forward; toCam.Normalize(); var right = Vector3.Cross(Vector3.up, toCam);
                _key.transform.position = Vector3.Lerp(_key.transform.position, head + toCam * 1.6f + right * 0.8f + Vector3.up * 0.6f, 1f - Mathf.Exp(-5f * dt)); _key.transform.rotation = Quaternion.LookRotation(head + Vector3.down * 0.25f - _key.transform.position);
                _rim.transform.position = Vector3.Lerp(_rim.transform.position, head - toCam * 0.6f - right * 0.3f + Vector3.up * 0.3f, 1f - Mathf.Exp(-5f * dt));
            }
            _key.intensity = 3.2f * _keyK; _rim.intensity = 1.1f * _keyK; _key.enabled = _rim.enabled = _keyK > 0.01f;
        }
    }
}
