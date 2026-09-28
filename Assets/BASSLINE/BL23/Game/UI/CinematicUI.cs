using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using BL23.Game.Audio;
using BL23.Game.Characters;
using BL23.Sim;
using TMPro;
using UnityEngine;
using Pose = BL23.Sim.Pose;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace BL23.Game
{
    /// <summary>Butler broadcasts (fishbowl speaker overlay). Announcements are heard by everyone awake — not a hint channel.</summary>
    public sealed class AnnouncementUI : MonoBehaviour
    {
        Session _s; Canvas _c; SlantPanel _p; TextMeshProUGUI _t; float _until; string[] _pages; int _page; float _pageAt;
        public void Init(Session s)
        {
            _s = s; _c = UIKit.Root("Announce", 25);
            _p = UIKit.Slant(_c.transform, "Panel", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-460, -214), new Vector2(460, -44), Pal.A(Pal.Panel2, 0.99f), Pal.A(Pal.Ink, 0.99f), Pal.A(Pal.Gold, 0.9f), 0);
            _p.Glow = 0.15f;
            UIKit.Text(_p.transform, "Who", "유스티  ·  저택 방송", 19, Pal.Gold, TextAlignmentOptions.TopLeft, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(44, 12), new Vector2(-20, -14)).characterSpacing = 4;
            _t = UIKit.Text(_p.transform, "T", "", 26, Pal.Text, TextAlignmentOptions.TopLeft, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(48, 14), new Vector2(-40, -48));
            _c.enabled = false;
        }
        public void Destroy() { if (_c) Destroy(_c.gameObject); }

        // --- time-on-demand (begin): at a large UI scale the centred plate would cover the HUD's case lines at the top left
        // (남은 수사 시간 · 다음 — …, x 24..460): it moves right of that column, and narrows if the screen has no room for both
        void Fit()
        {
            var cr = _c.transform as RectTransform; float w = cr != null ? cr.rect.width : 0f; if (w < 900f || _p == null) return;   // (not laid out yet: keep the centred plate)
            const float leftCol = 24f + 436f + 18f, margin = 24f;
            float half = 460f, cx = 0f, left = w * 0.5f - half;
            if (left < leftCol)
            {
                cx = leftCol - left;
                float right = w * 0.5f + cx + half;
                if (right > w - margin) { float over = right - (w - margin); float cut = Mathf.Min(over, (half - 300f) * 2f); half -= cut * 0.5f; cx -= cut * 0.5f; }
            }
            var rt = _p.rectTransform; rt.offsetMin = new Vector2(cx - half, -214); rt.offsetMax = new Vector2(cx + half, -44);
        }
        // --- time-on-demand (end)

        readonly List<(string text, string key)> _defer = new List<(string, string)>();
        public void Show(string text, string key)
        {
            if (key == "y_intro") return; // the prologue has its own staging
            // a body is being found on screen: the broadcast and its bell wait for the discovery film to end (they follow it,
            // one after another), never talking over the close-ups
            if (DiscoveryFilm.Active != null) { if (_defer.Count < 4) _defer.Add((text, key)); return; }
            _pages = LineBank.Pages(text); if (_pages.Length == 0) return; _page = 0; _pageAt = Time.time;
            _t.text = _pages[0]; _c.enabled = true; _on = true; _until = Time.time + 4.5f * _pages.Length + 1;
            Fit();   // --- time-on-demand
            if (key == "y_body" || key == "y_body_multi") Sfx.Play("death_bell", null, 1f); else if (key == "y_trial_summon" || key == "y_hunger") Sfx.Play("bell_toll", null, 0.9f); else if (key == "y_morning") Sfx.Play("morning_bell", null, 0.8f); else if (key == "y_night") Sfx.Play("bell_toll", null, 0.6f); else Sfx.Play("chime", null, 0.8f);
            VoiceBabble.Speak(Cast.Butler, _pages[0]);
        }
        bool _on;
        void Update()
        {
            // broadcasts held back by the discovery film, in order, once the screen is free again
            if (_defer.Count > 0)
            {
                if ((_s.Trial != null && _s.Trial.Active) || (_s.Reveal != null && _s.Reveal.Active)) _defer.Clear();   // the court moved on
                else if (!_on && DiscoveryFilm.Active == null) { var d = _defer[0]; _defer.RemoveAt(0); Show(d.text, d.key); }
            }
            if (!_on) return;
            // the courtroom and the reveal have their own staging; broadcasts wait underneath
            if ((_s.Trial != null && _s.Trial.Active) || (_s.Reveal != null && _s.Reveal.Active)) { _c.enabled = false; _on = false; return; }
            // a modal over the house (심판 전 정리, the menus, the discovery film): the broadcast steps out of sight and keeps
            // its place (its pages and its time wait), so its edge never shows half under the panel
            bool covered = CaseReport.Open || (_s.Menu != null && _s.Menu.Open) || DiscoveryFilm.Active != null;
            if (covered) { _until += Time.deltaTime; _pageAt += Time.deltaTime; if (_c.enabled) _c.enabled = false; return; }
            if (!_c.enabled) _c.enabled = true;
            if (_pages != null && _page + 1 < _pages.Length && Time.time - _pageAt > 4.2f) { _page++; _pageAt = Time.time; _t.text = _pages[_page]; VoiceBabble.Speak(Cast.Butler, _pages[_page]); }
            if (Time.time > _until) { _c.enabled = false; _on = false; }
        }
    }

    /// <summary>Staged moments: prologue speech, chapter/loop cards, waiting/sleeping, loop reset.</summary>
    public sealed class CinematicUI : MonoBehaviour
    {
        Session _s; GameState S => _s.S; Canvas _c; Image _fade; TextMeshProUGUI _big, _small, _sub; SlantPanel _subPanel; Camera _cam;
        public bool Busy;
        /// <summary>The discovery film reuses this cinematic camera (depth 25, post, SetupCamera) and this canvas (bars, title).</summary>
        internal Camera FilmCam => _cam; internal Canvas FilmCanvas => _c;

        public void Init(Session s)
        {
            _s = s; _c = UIKit.Root("Cinematic", 50);
            _fade = UIKit.Img(_c.transform, "Fade", Color.black, Vector2.zero, Vector2.one); _fade.color = new Color(0, 0, 0, 0);
            _big = UIKit.Text(_c.transform, "Big", "", 110, Color.white, TextAlignmentOptions.Center, Fonts.Title, new Vector2(0, 0.45f), new Vector2(1, 0.75f));
            _small = UIKit.Text(_c.transform, "Small", "", 30, Pal.Text, TextAlignmentOptions.Top, Fonts.Serif, new Vector2(0.1f, 0.25f), new Vector2(0.9f, 0.45f));
            _subPanel = UIKit.Slant(_c.transform, "SubPanel", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-780, 40), new Vector2(780, 230), Pal.A(Pal.Panel2, 0.995f), Pal.A(Pal.Ink, 0.995f), Pal.A(Pal.Gold, 0.8f), 0);
            _sub = UIKit.Text(_subPanel.transform, "Sub", "", 30, Pal.Text, TextAlignmentOptions.MidlineLeft, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(60, 20), new Vector2(-60, -20));
            _subPanel.gameObject.SetActive(false);
            var camGo = new GameObject("CinematicCamera", typeof(Camera)); DontDestroyOnLoad(camGo); _cam = camGo.GetComponent<Camera>(); _cam.enabled = false; _cam.fieldOfView = 40; _cam.depth = 25;
            var d = camGo.AddComponent<UniversalAdditionalCameraData>(); d.renderPostProcessing = true;
            BL23.Game.Mansion.MansionAtmosphere.SetupCamera(camGo.GetComponent<Camera>());
        }
        public void Destroy() { if (_c) Destroy(_c.gameObject); if (_cam) Destroy(_cam.gameObject); }

        IEnumerator Fade(float to, float secs) { float from = _fade.color.a; for (float t = 0; t < secs; t += Time.unscaledDeltaTime) { _fade.color = new Color(0, 0, 0, Mathf.Lerp(from, to, t / secs)); yield return null; } _fade.color = new Color(0, 0, 0, to); }

        bool Skip() => Input.GetKeyDown(KeyCode.Escape);
        bool Advance() => Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);

        // ---------------------------------------------------------------- prologue
        public void Prologue() { StartCoroutine(PrologueCo()); }
        IEnumerator PrologueCo()
        {
            Busy = true; _s.Pause("cine"); _s.Player.SetControl(false); _cam.enabled = true;
            MusicDirector.I?.SetState(MusicState.Prologue);
            _fade.color = Color.black;
            _big.text = "BASSLINE"; _small.text = "같은 사람들, 다른 진실";
            yield return new WaitForSecondsRealtime(_s.Headless ? 0.1f : 2.2f);
            _big.text = ""; _small.text = "";
            var hall = S.Layout.Rooms.First(r => r.Type == RoomType.GrandHall && r.Floor == 0);
            var center = _s.World.ToWorld(new P3(0, hall.Rect.CX, hall.Rect.CZ)) + Vector3.up * 1.4f;
            var yusti = _s.World.ViewOf(Cast.Butler);
            StartCoroutine(Fade(0, 2.5f));
            string text = _s.Sim.Render(Cast.Butler, null, "y_intro") ?? "…";
            var pages = LineBank.Pages(text);
            _subPanel.gameObject.SetActive(true);
            float orbit = 0;
            for (int i = 0; i < pages.Length; i++)
            {
                _sub.text = pages[i]; VoiceBabble.Speak(Cast.Butler, pages[i]); yusti?.Talk(3);
                if (i == 3) yusti?.Rig?.Anim?.PlayGesture(Gesture.Bow, 2);
                float t0 = Time.unscaledTime;
                while (true)
                {
                    orbit += Time.unscaledDeltaTime * 0.06f;
                    // alternate: wide orbit over the gathered eighteen, then Yusti's aquarium close-up
                    if (i % 3 == 2 && yusti != null) { var h = yusti.HeadPos; _cam.transform.position = h + yusti.transform.forward * 1.6f + Vector3.up * 0.2f; _cam.transform.LookAt(h); }
                    else { _cam.transform.position = center + new Vector3(Mathf.Sin(orbit + i) * 9f, 3.2f, Mathf.Cos(orbit + i) * 9f); _cam.transform.LookAt(center); }
                    if (_s.Headless && Time.unscaledTime - t0 > 0.05f) break;
                    if (Skip()) { i = pages.Length; break; }
                    if (Advance() && Time.unscaledTime - t0 > 0.25f) break;
                    yield return null;
                }
            }
            _subPanel.gameObject.SetActive(false);
            yield return Fade(1, 0.6f);
            _cam.enabled = false; _s.Player.SetControl(true); _s.Player.Respawn();
            _s.Sim.EndPrologue();
            _big.text = "1일차"; _small.text = "저택을 둘러보고, 사람들과 이야기하세요.\n<size=22><color=#9A8E7C>Tab 수첩 · E 말 걸기·사용 · R 살펴보기 · Esc 메뉴</color></size>";
            yield return Fade(0, 1.2f);
            yield return new WaitForSecondsRealtime(_s.Headless ? 0.1f : 2.5f);
            _big.text = ""; _small.text = "";
            _s.Resume("cine"); Busy = false;
        }

        // ---------------------------------------------------------------- chapter / loop cards
        public void ChapterCard() { StartCoroutine(Card($"CHAPTER {S.Chapter}", S.Ch.Rules.Count > 0 ? "이번 챕터 규칙 — " + string.Join(" · ", S.Ch.Rules.Select(r => r.Name)) : "새 규칙은 없다", 3f)); }
        public void LoopStartCard() { StartCoroutine(Card($"LOOP {S.Loop}", "저택이 다시 지어졌다. 사람들은 그대로인데 방이 다르다.\n<size=22><color=#9A8E7C>이번 루프에서는 권능이 다른 사람에게 넘어갔다.</color></size>", 4f)); }
        /// <summary>A title card is on screen (the aim prompt steps aside so the two never overlap).</summary>
        public bool CardShowing => _big != null && !string.IsNullOrEmpty(_big.text);
        IEnumerator Card(string big, string small, float secs)
        {
            _big.text = big; _small.text = small; Sfx.Play("chime", null, 0.5f);
            yield return new WaitForSecondsRealtime(_s.Headless ? 0.05f : secs);
            _big.text = ""; _small.text = "";
        }

        // ---------------------------------------------------------------- body discovery
        /// <summary>Finding a body: the discovery film (Game/UI/DiscoveryFilm.cs) — first person, the composed sting, the gaze's
        /// close-ups, the return and the title. A body found while something else holds the screen (a conversation, another
        /// cinematic) gets its sting at once and its film as soon as the screen is free (within 20 s). The bell and the
        /// broadcast follow on their own.</summary>
        public void Discovery(string victim)
        {
            if (_s.Headless) return;
            if (Busy || (_s.Dialogue?.Active ?? false)) { if (_pendDisc == null) { _pendDisc = victim; _pendAt = Time.unscaledTime; DiscoveryFilm.PlayStingNow(this); } return; }
            StartCoroutine(DiscoveryCo(victim, false));
        }
        string _pendDisc; float _pendAt;
        void Update()
        {
            if (_pendDisc != null && !Busy && !_s.Headless && !(_s.Dialogue?.Active ?? false) && !(_s.Trial?.Active ?? false))
            { var v = _pendDisc; _pendDisc = null; if (Time.unscaledTime - _pendAt < 20f) StartCoroutine(DiscoveryCo(v, true)); }
        }
        void OnDisable() { DiscoveryFilm.Active?.Restore(); }
        IEnumerator DiscoveryCo(string victim, bool late)
        {
            Busy = true; _s.Pause("cine");
            var film = new DiscoveryFilm(_s, this, victim, late);
            yield return film.Play();      // each step try/catch-guarded; Play never throws
            film.Restore();
            _s.Resume("cine"); Busy = false;
        }

        /// <summary>The investigation begins: a title card with what to do, over the living scene.</summary>
        public void InvestigationCard(string victim, string place)
        {
            if (_s.Headless) return;
            StartCoroutine(Card("수 사", $"<color=#D6AD62>{victim} — {place}</color>\n<size=26><color=#B8AC98>시신을 살펴보고, 그 무렵 일을 물어보자</color></size>" +
                "\n<size=22><color=#9A8E7C>다 되었으면 T로 수사를 마칠 수 있다 (20분 뒤부터)</color></size>", 4.2f));   // --- time-on-demand: how the investigation ends
        }

        // ---------------------------------------------------------------- a shared meal
        /// <summary>Walking into the dining room while people are eating together: a few lines around the table (greetings, the
        /// food, the house, and grief after a death), the view moving calmly from one speaker to the next. Once per meal.</summary>
        public void TableTalk(List<string> diners, string meal)
        {
            if (!Busy && !_s.Headless && diners != null && diners.Count >= 2 && LifeSceneUI.TableTalk(_s, diners, meal)) return;   // --- daily-life: the table topic scene (Game/UI/LifeSceneUI.cs, Sim/Life/LifeTable.cs)
            if (!Busy && !_s.Headless && diners != null && diners.Count >= 2) StartCoroutine(TableTalkCo(diners, meal));
        }
        IEnumerator TableTalkCo(List<string> diners, string meal)
        {
            Busy = true; _s.Pause("cine"); _s.Player.SetControl(false);
            var views = diners.Select(id => _s.World.ViewOf(id)).Where(v => v != null).ToList(); if (views.Count < 2) { _s.Player.SetControl(true); _s.Resume("cine"); Busy = false; yield break; }
            var center = Vector3.zero; foreach (var v in views) center += v.HeadPos; center /= views.Count;
            // who says what: a greeting, the meal, the house; after a death, the ones who were close to the dead speak of it
            var lines = new List<(string who, string text)>(); var rng = new System.Random((int)(S.Clock * 7) + diners.Count);
            var dead = S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Confirmed && S.Clock - i.ConfirmClock < 24 * 60).Select(i => i.Victim).ToList();
            string[] keys = meal == "breakfast" ? new[] { "greet_morning", "meal", "talk_mansion", "small_talk" } : new[] { "meal", "small_talk", "talk_mansion", "talk_like" };
            int n = Math.Min(5, Math.Max(3, views.Count + 1));
            for (int i = 0; i < n; i++)
            {
                var who = diners[(i * 3 + rng.Next(diners.Count)) % diners.Count]; if (lines.Count > 0 && lines[lines.Count - 1].who == who) who = diners[(diners.IndexOf(who) + 1) % diners.Count];
                string key = keys[Math.Min(i, keys.Length - 1)];
                if (dead.Count > 0 && i == 1) { var d = dead[0]; bool close = S.HasRel(who, d) && S.R(who, d).Like > 0.3f; key = close ? "grief_close" : "grief"; }
                var listener = diners[(diners.IndexOf(who) + 1) % diners.Count];
                var text = _s.Sim.Render(who, listener, key, new Dictionary<string, string> { { "t", "@" + (dead.Count > 0 ? dead[0] : listener) }, { "topic", "이 저택" } });
                if (!string.IsNullOrEmpty(text)) lines.Add((who, LineBank.Pages(text).FirstOrDefault() ?? text));
            }
            if (lines.Count == 0) { _s.Player.SetControl(true); _s.Resume("cine"); Busy = false; yield break; }
            _cam.enabled = true; _cam.fieldOfView = 38f; Vector3 camPos = _s.Player.Cam.transform.position, camLook = center, pv = Vector3.zero, lv = Vector3.zero;
            _subPanel.gameObject.SetActive(true);
            foreach (var (who, text) in lines)
            {
                var v = _s.World.ViewOf(who); if (v == null) continue;
                var head = v.HeadPos; var toC = center - head; toC.y = 0; if (toC.sqrMagnitude < 0.01f) toC = -v.transform.forward; toC.Normalize(); var side = Vector3.Cross(Vector3.up, toC);
                // film the face: from where the speaker is looking (seats do not always face the table centre)
                var face = v.transform.forward; face.y = 0; if (face.sqrMagnitude < 0.01f) face = toC; face.Normalize();
                var want = TalkCam(v, head, face, Vector3.Cross(Vector3.up, face));
                _sub.text = $"<size=70%><color=#D6AD62>{Cast.NameOf(who)}</color></size>\n{LineBank.FixParticles(text)}";
                VoiceBabble.Speak(who, text); v.Talk(Mathf.Clamp(text.Length * 0.07f, 1f, 4f)); SpeechGestures.Perform(v, text, Emotion.Neutral, false);
                float dur = Mathf.Clamp(1.6f + text.Length * 0.06f, 2.2f, 5.5f);
                for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
                {
                    camPos = Vector3.SmoothDamp(camPos, want, ref pv, 0.8f, 3f, Time.unscaledDeltaTime); camLook = Vector3.SmoothDamp(camLook, head, ref lv, 0.6f, 4f, Time.unscaledDeltaTime);
                    _cam.transform.position = camPos; _cam.transform.rotation = Quaternion.LookRotation(camLook - camPos);
                    if (Skip()) { t = dur; break; } if (Advance() && t > 0.4f) break;
                    yield return null;
                }
                if (Skip()) break;
            }
            _subPanel.gameObject.SetActive(false); _cam.enabled = false;
            _s.Player.SetControl(true); _s.Resume("cine"); Busy = false;
        }

        /// <summary>Where to film someone talking at the table: across it and a little above the high chair backs, looking down
        /// at the face — the first spot with a clear line of sight (chairs, candles and other diners must not block the face).</summary>
        static Vector3 TalkCam(ActorView v, Vector3 head, Vector3 toC, Vector3 side)
        {
            var target = head + Vector3.up * 0.02f;
            Vector3[] cands =
            {
                head + toC * 1.35f + side * 0.3f + Vector3.up * 0.26f,
                head + toC * 1.25f + side * 0.3f + Vector3.up * 0.42f,
                head + toC * 1.1f - side * 0.45f + Vector3.up * 0.38f,
                head + toC * 1.1f + side * 0.6f + Vector3.up * 0.34f,
                head + toC * 0.85f + Vector3.up * 0.55f,
                head + toC * 1.5f + Vector3.up * 0.6f,
            };
            foreach (var c in cands)
            {
                if (Physics.CheckSphere(c, 0.12f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Linecast(c, target, out var hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<ActorView>() != v) continue;
                return c;
            }
            return cands[0];
        }

        // ---------------------------------------------------------------- waiting / sleeping
        // --- time-on-demand (begin): every passage of time goes through the TimeDirector (Game/Core/TimeDirector.cs)
        /// <summary>Wait a number of minutes (T menu): a first-person time-lapse that stops by itself when something happens.</summary>
        public void Wait(double minutes)
        {
            var td = _s.TimeDir; if (td == null || td.Active || Busy) return;
            SkipPlan p = null; string why = null;
            try { p = _s.Sim.PlanWait(minutes, out why); } catch (Exception e) { Debug.LogException(e); }
            if (p == null) { Hud.I?.Toast(why ?? "지금은 기다릴 수 없다", Pal.TextDim, 2f); return; }
            p.Quiet = S.Phase == Phase.Investigation || TimeDirector.QuietPref;   // investigation: only what is urgent stops it
            if (string.IsNullOrEmpty(p.Label)) p.Label = "기다리는 중";
            td.Start(p);
        }
        /// <summary>E on the own bed: sleep until the morning bell.</summary>
        public void Sleep() { _s.TimeDir?.StartSleep(false); }
        /// <summary>아침까지 잔다 (from anywhere): the screen goes dark, 민혁 is in his own bed, and he sleeps until morning.</summary>
        public void SleepInBed() { _s.TimeDir?.StartSleep(true); }

        /// <summary>Doing something with a piece of furniture. Simple things (open, sit, look, wind the clock) happen at once
        /// with a motion (Interaction.Instant); a pastime (책 읽기 30분) spends clock time (TimeDirector.StartPastime).</summary>
        public void Activity(Furniture f, PlayerAction act)
        {
            if (f == null || act == null || Busy) return;
            if (act.Minutes <= 0) { _s.Player?.Interact?.Instant(f, act); return; }
            _s.TimeDir?.StartPastime(f, act);
        }
        // --- time-on-demand (end)
        /// <summary>The motion for a player action at a piece of furniture: the pose to take and the animation to play.</summary>
        static (Pose pose, Anim anim) ActivityMotion(string actId, Furniture f)
        {
            switch (actId)
            {
                case "sit": return (Pose.Sit, Anim.Idle);
                case "nap": return (Pose.Sleep, Anim.Sleep);
                case "read": return (Pose.Sit, Anim.Read);
                case "play_music": return (Pose.Sit, Anim.Play);
                case "listen": return (Pose.Stand, Anim.Use);
                case "game": return (Pose.Stand, Anim.Play);
                case "chess": return (Pose.Sit, Anim.Play);
                case "cook": return (Pose.Stand, Anim.Cook);
                case "garden": return (Pose.Crouch, Anim.Garden);
                case "pray": return (f.Type == "Altar" ? Pose.Kneel : Pose.Sit, Anim.Pray);
                case "perform": return (Pose.Stand, Anim.Talk);
                case "swim": return (Pose.Stand, Anim.Swim);
                case "craft": return (Pose.Sit, Anim.Craft);
                case "view": return (Pose.Stand, Anim.Examine);
                case "drink": return (Pose.Stand, Anim.Drink);
                case "film": return (Pose.Stand, Anim.Operate);
                case "exercise": return (Pose.Stand, Anim.Exercise);
                case "groom": return (Pose.Stand, Anim.Use);
                case "search": return (f.Type == "Nightstand" || f.Type == "Chest" || f.Type == "Crates" || f.Type == "Barrel" ? Pose.Crouch : Pose.Stand, Anim.Search);
                case "inventory": return (Pose.Stand, Anim.Examine);
                case "wind": return (Pose.Stand, Anim.Use);
                case "fire": return (Pose.Crouch, Anim.Use);
                case "wash": return (Pose.Stand, Anim.Wash);
                case "tea": return (Pose.Stand, Anim.Use);
                case "feed": return (Pose.Stand, Anim.Use);
                case "log": return (Pose.Stand, Anim.Read);
            }
            return (Pose.Stand, Anim.Use);
        }

        static float PitchFor(string actId)
        {
            switch (actId)
            {
                case "read": return 36f; case "chess": return 40f; case "cook": return 34f; case "garden": return 46f; case "craft": return 36f;
                case "play_music": return 24f; case "game": return 20f; case "pray": return 18f; case "listen": return 16f; case "drink": return 6f;
                case "film": return 10f; case "swim": return 8f; case "nap": return -55f; case "sit": return 8f;
                case "search": return 28f; case "inventory": return 8f; case "wind": return 4f; case "fire": return 30f; case "wash": return 38f; case "tea": return 26f; case "feed": return 14f; case "log": return 16f;
            }
            return 4f;
        }

        /// <summary>First person, all the way: 민혁 walks to the spot, turns, pulls the chair out and sits (the view goes down with
        /// the body), then looks at what the hands are doing. Seats only when the spot really is a seat.</summary>
        internal IEnumerator StageActivity(Furniture f, PlayerAction act, System.Action<Spot> held)   // time-on-demand: used by TimeDirector.StartPastime
        {
            var me = S.Player; var view = _s.World.ViewOf(Cast.Player); var pc = _s.Player;
            if (me == null || view == null || pc == null || _s.Headless) { held(null); yield break; }
            var (pose, anim) = ActivityMotion(act.Id, f);
            var spots = S.Layout.Spots.Where(sp => sp.Furniture == f.Id).ToList();
            var spot = spots.FirstOrDefault(sp => sp.Occupant == null && sp.OnFurniture && (sp.Tag == "sit" || sp.Tag == "eat" || sp.Tag == "read" || sp.Tag == "play" || sp.Tag == "pray" || sp.Tag == "sleep" || sp.Tag == "rest"))
                       ?? spots.FirstOrDefault(sp => sp.Occupant == null);
            // only real seats are sat on (a bookshelf's "read" spot is not a chair)
            bool seatFurniture = f.Type == "Chair" || f.Type == "Armchair" || f.Type == "Sofa" || f.Type == "Bench" || f.Type == "BarStool" || f.Type == "Pew" || f.Type == "Lounger" || f.Type == "AudSeat" || f.Type == "WaitBench" || f.Type == "InfirmaryBed" || f.Type == "Bed";
            bool seat = spot != null && spot.OnFurniture && (pose == Pose.Sit || pose == Pose.Sleep) && (seatFurniture || spot.Tag == "sit" || spot.Tag == "eat" || spot.Tag == "sleep" || spot.Tag == "rest");
            if (!seat && (pose == Pose.Sit || pose == Pose.Sleep)) pose = Pose.Stand;
            var stand = spot != null ? spot.Approach : me.Pos;
            float faceYaw = spot != null ? spot.Yaw : MathX.AngleDeg(f.Pos.x - me.Pos.x, f.Pos.z - me.Pos.z);
            pc.BeginScript();
            if (me.Spot >= 0) _s.Sim.PlayerStand();   // --- time-on-demand: a seat is left through the kernel (frees the spot)
            me.Pose = Pose.Stand; me.Anim = Anim.Idle;
            // 1) walk over (the body walks; the view goes with it)
            var from = me.Pos; float dist = stand.f == from.f ? from.DistXZ(stand) : 0f;
            if (dist > 0.15f)
            {
                float walkYaw = MathX.AngleDeg(stand.x - from.x, stand.z - from.z); me.Yaw = walkYaw; pc.ScriptYaw = walkYaw; pc.ScriptPitch = 10f;
                float dur = Mathf.Clamp(dist / 1.35f, 0.3f, 2.5f);
                for (float t = 0; t < dur; t += Time.unscaledDeltaTime) { float k = Mathf.Clamp01(t / dur); me.Pos = new P3(from.f, Mathf.Lerp(from.x, stand.x, k), Mathf.Lerp(from.z, stand.z, k)); yield return null; }
                me.Pos = stand;
            }
            // 2) turn to it
            me.Yaw = faceYaw; pc.ScriptYaw = faceYaw; pc.ScriptPitch = seat ? 26f : 12f;
            yield return new WaitForSecondsRealtime(0.4f);
            // 3) take the seat (the chair is pulled out by the hands in view) or the pose, then do it
            // --- time-on-demand (begin): the kernel seats the player (nearest free spot of this furniture; lying for a nap)
            if (seat)
            {
                var taken = _s.Sim.PlayerSit(f, spot.Pos, pose == Pose.Sleep);
                if (taken != null) { spot = taken; pc.ScriptYaw = taken.Yaw; } else { seat = false; pose = Pose.Stand; }
            }
            // --- time-on-demand (end)
            me.Pose = pose;
            yield return new WaitForSecondsRealtime(seat ? 1.0f : 0.3f);
            me.Anim = anim; pc.ScriptPitch = PitchFor(act.Id);
            for (float t = 0; t < 2.6f; t += Time.unscaledDeltaTime) { if (Advance() && t > 0.6f) break; yield return null; }
            held(seat ? spot : null);
        }

        // --- time-on-demand (begin): getting up goes through the kernel (PlayerStand frees the spot); the time itself is the
        // TimeDirector's (WaitCo / SleepInBedCo / ActivityCo are gone)
        internal IEnumerator UnstageActivity(Spot held)
        {
            var me = S.Player; var pc = _s.Player; if (me == null || pc == null) yield break;
            me.Anim = Anim.Idle;
            if (held != null || me.Spot >= 0) _s.Sim.PlayerStand();
            else me.Pose = Pose.Stand;
            if (!_s.Headless) { pc.ScriptPitch = 6f; yield return new WaitForSecondsRealtime(held != null ? 0.8f : 0.3f); }
            if (pc.Scripted) pc.EndScript(me.Pos, me.Yaw);
        }

        /// <summary>Stand up at once (a pastime cut short by something urgent: no slow rise while a scream is heard).</summary>
        internal void UnstageNow(Spot held)
        {
            var me = S.Player; var pc = _s.Player; if (me == null || pc == null) return;
            me.Anim = Anim.Idle;
            if (held != null || me.Spot >= 0) _s.Sim.PlayerStand(); else me.Pose = Pose.Stand;
            if (pc.Scripted) pc.EndScript(me.Pos, me.Yaw);
        }
        // --- time-on-demand (end)

        // ---------------------------------------------------------------- loop end
        public void LoopEnd() { if (!Busy) StartCoroutine(LoopEndCo()); }
        IEnumerator LoopEndCo()
        {
            Busy = true; _s.Pause("loop"); MusicDirector.I?.SetState(MusicState.LoopReset); Sfx.Play("loop_reset", null, 1f);
            yield return Fade(1, 1.2f);
            _big.text = "LOOP"; _small.text = $"남은 사람은 {S.Survivors}명. 이 저택의 시간은 여기서 끝난다.\n모든 것이 처음으로 돌아간다.";
            yield return new WaitForSecondsRealtime(_s.Headless ? 0.1f : 4f);
            _big.text = ""; _small.text = "";
            _s.StartNextLoop();
            yield return null;
            yield return Fade(0, 1.5f);
            _s.Resume("loop"); Busy = false;
        }
    }
}
