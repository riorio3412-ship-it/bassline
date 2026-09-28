using System;
using System.Collections;
using System.Linq;
using BL23.Game.Audio;
using BL23.Game.Characters;
using BL23.Game.Mansion;
using BL23.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace BL23.Game
{
    /// <summary>
    /// Entry point. Creates itself after the first scene loads (no scene wiring needed), shows the title (a real 3D room
    /// of a dream-mansion with the three modeled students), and starts / loads / leaves campaigns.
    /// </summary>
    public sealed class GameBoot : MonoBehaviour
    {
        public static GameBoot I;
        public const string Version = "BL23 v1.0";
        Canvas _c; RectTransform _menu, _sub; Camera _cam; MansionView _titleView; float _camYaw = 160, _camDist = 6.8f; Vector3 _focus; Transform _backdrop; float _orbit; Vector3 _center;
        string[] _args = new string[0];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (I != null || FindAnyObjectByType<GameBoot>() != null) return;
            new GameObject("BL23 Boot").AddComponent<GameBoot>();
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this; DontDestroyOnLoad(gameObject);
            try { _args = Environment.GetCommandLineArgs(); } catch (Exception) { }
            // any stray scene cameras/lights from the legacy project must not fight ours
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (cam.gameObject.scene.IsValid() && cam.transform.root != transform) cam.gameObject.SetActive(false);
            try { Sfx.Init(); } catch (Exception e) { Debug.LogException(e); }
            MusicDirector.Ensure();
            Settings.Apply();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        void Start()
        {
            ShowTitle();
            if (HasArg("-bl23probe")) gameObject.AddComponent<AutoProbe>().Run(ArgValue("-bl23probe") ?? "full");
        }

        public bool HasArg(string a) => _args.Any(x => string.Equals(x, a, StringComparison.OrdinalIgnoreCase));
        public string ArgValue(string a) { int i = Array.FindIndex(_args, x => string.Equals(x, a, StringComparison.OrdinalIgnoreCase)); return i >= 0 && i + 1 < _args.Length && !_args[i + 1].StartsWith("-") ? _args[i + 1] : null; }

        // ---------------------------------------------------------------- title
        public void ShowTitle()
        {
            HideTitle();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            BuildBackdrop();
            UIKit.EnsureEventSystem();
            _c = UIKit.Root("Title", 70); var t = _c.transform;
            UIKit.Img(t, "Vignette", new Color(0, 0, 0, 0.25f), Vector2.zero, Vector2.one);
            var band = UIKit.Slant(t, "Band", new Vector2(0, 0.52f), new Vector2(0.62f, 0.86f), new Vector2(40, 0), Vector2.zero, Pal.A(Pal.Ink, 0.78f), Pal.A(Pal.MagentaDim, 0.5f), Pal.A(Pal.Gold, 0.85f), 60);
            band.Glow = 0.25f;
            UIKit.Text(band.transform, "Logo", "BASSLINE", 150, new Color(0.96f, 0.9f, 0.76f), TextAlignmentOptions.MidlineLeft, Fonts.Display, Vector2.zero, Vector2.one, new Vector2(90, 40), new Vector2(0, 0));
            UIKit.Text(band.transform, "Sub", "같은 사람들, 다른 진실 — 꿈꾸는 저택의 심판", 34, Pal.Gold, TextAlignmentOptions.BottomLeft, Fonts.Serif, Vector2.zero, Vector2.one, new Vector2(100, 22), new Vector2(0, 0));
            _menu = UIKit.Rect(t, "Menu", new Vector2(0, 0), new Vector2(0.36f, 0.5f), new Vector2(80, 80), new Vector2(0, -20));
            int i = 0;
            if (Session.NewestSave() != null) MenuBtn(i++, "이어하기", Continue);   // the newest valid save of any kind
            MenuBtn(i++, "새 게임", () => ConfirmNew());
            MenuBtn(i++, "불러오기", OpenLoad);
            MenuBtn(i++, "설정", OpenSettings);
            MenuBtn(i++, "종료", Application.Quit);
            UIKit.Text(t, "Ver", Version + " · 음악: 제작자 자작곡 · 글꼴: SIL OFL", 18, Pal.A(Pal.Text, 0.55f), TextAlignmentOptions.BottomRight, Fonts.Body, new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(0, 16), new Vector2(-30, 50));
            _sub = UIKit.Rect(t, "SubPanel", new Vector2(0.4f, 0.08f), new Vector2(0.96f, 0.5f));
            // v0.31 saves use an unrelated world model: they are kept untouched (never overwritten) and the player is told so
            try
            {
                var legacy = System.IO.Path.Combine(Application.persistentDataPath, "MansionM01", "session-v1.dat");
                if (System.IO.File.Exists(legacy))
                    UIKit.Text(t, "Legacy", "이전 버전(v0.31) 저장 파일이 있습니다. BL23은 세계 구조가 달라 이어서 할 수 없지만, 기존 파일은 지우지 않고 그대로 둡니다.", 18, Pal.A(Pal.Gold, 0.8f), TextAlignmentOptions.BottomLeft, Fonts.Body, new Vector2(0, 0), new Vector2(0.6f, 0), new Vector2(40, 16), new Vector2(0, 70));
            }
            catch (Exception) { }
            MusicDirector.I?.SetState(MusicState.Title);
        }

        void MenuBtn(int i, string label, Action a)
        {
            var b = UIKit.Button(_menu, label, a, new Vector2(0, 1), new Vector2(1, 1), new Vector2(i * 22, -(i + 1) * 84), new Vector2(i * 22, -i * 84 - 12), 38, 22, Fonts.Title);
        }

        void HideTitle()
        {
            if (_c) Destroy(_c.gameObject); _c = null;
            if (_backdrop) Destroy(_backdrop.gameObject); _backdrop = null;
            if (_cam) Destroy(_cam.gameObject); _cam = null;
        }

        void ClearSub() { if (_sub) UIKit.Clear(_sub); }

        void ConfirmNew()
        {
            ClearSub();
            bool anySave = Session.SaveFiles().Any(f => SaveStore.Info(f.path).Exists);
            var p = UIKit.Slant(_sub, "P", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Pal.A(Pal.Panel2, 0.95f), Pal.A(Pal.Ink, 0.95f), Pal.A(Pal.Gold, 0.8f), 30);
            UIKit.Text(p.transform, "T", $"새 캠페인\n<size=23><color=#9A8E7C>루프 기준 인원 {Settings.FloorPreset}명 (설정에서 4명·5명 중에 고를 수 있다)\n자동 저장은 챕터 시작·아침·수사 시작·심판 직전에 세 칸을 번갈아 쓴다.</color>"
                + (anySave ? "\n<color=#D6AD62>새 게임을 시작하면 자동 저장 칸을 차례로 덮어쓴다. 직접 저장한 슬롯은 그대로 남는다.</color>" : "") + "</size>", 40, Pal.Text, TextAlignmentOptions.TopLeft, Fonts.Title, Vector2.zero, Vector2.one, new Vector2(50, 20), new Vector2(-30, -30));
            UIKit.Button(p.transform, "시작하기", () => NewGame(0), new Vector2(0.06f, 0), new Vector2(0.46f, 0), new Vector2(0, 40), new Vector2(0, 110), 32);
            UIKit.Button(p.transform, "시드 지정: " + (SeedText ?? "무작위"), () => { SeedText = SeedText == null ? "20260926" : null; ConfirmNew(); }, new Vector2(0.5f, 0), new Vector2(0.94f, 0), new Vector2(0, 40), new Vector2(0, 110), 24);
        }
        string SeedText;

        void OpenLoad()
        {
            ClearSub();
            var p = UIKit.Slant(_sub, "P", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Pal.A(Pal.Panel2, 0.95f), Pal.A(Pal.Ink, 0.95f), Pal.A(Pal.Gold, 0.8f), 30);
            // two columns: 자동 · 빠른 저장 | 수동 슬롯
            int li = 0, ri = 0;
            foreach (var (name, path) in Session.SaveFiles())
            {
                var info = SaveStore.Info(path); bool manual = name.StartsWith("슬롯"); string pth = path; int row = manual ? ri++ : li++;
                string label = $"<color=#D6AD62>{name}</color>  " + (info.Exists ? (info.Corrupt ? "손상됨" : $"{info.Label}\n<size=74%><color=#9A8E7C>{MenuUI.SlotLine(info)}</color></size>") : "<color=#9A8E7C>비어 있음</color>");
                var b = UIKit.Button(p.transform, label, () => LoadPath(pth), manual ? new Vector2(0.5f, 1) : new Vector2(0, 1), manual ? new Vector2(1, 1) : new Vector2(0.5f, 1),
                    new Vector2(manual ? 10 : 30, -24 - (row + 1) * 66), new Vector2(manual ? -30 : -10, -24 - row * 66 - 6), 18);
                b.Label.textWrappingMode = TextWrappingModes.NoWrap; b.Label.overflowMode = TextOverflowModes.Ellipsis;
                if (!info.Exists || info.Corrupt) { b.Interactable = false; b.Paint(); }
            }
        }

        void OpenSettings()
        {
            ClearSub();
            var p = UIKit.Slant(_sub, "P", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Pal.A(Pal.Panel2, 0.95f), Pal.A(Pal.Ink, 0.95f), Pal.Cyan, 30);
            int i = 0;
            // two columns of five so every row fits the panel
            void Row(string label, Action a) { bool right = i >= 5; int r = i % 5; UIKit.Button(p.transform, label, a, right ? new Vector2(0.5f, 1) : new Vector2(0, 1), right ? new Vector2(1, 1) : new Vector2(0.5f, 1), new Vector2(right ? 10 : 30, -30 - (r + 1) * 64), new Vector2(right ? -30 : -10, -30 - r * 64 - 8), 20); i++; }
            Row($"음악 {(int)(Settings.Music * 100)}%  (클릭: +10%)", () => { Settings.Music = Settings.Music >= 0.99f ? 0 : Settings.Music + 0.1f; Settings.Apply(); OpenSettings(); });
            Row($"효과음 {(int)(Settings.SfxVol * 100)}%", () => { Settings.SfxVol = Settings.SfxVol >= 0.99f ? 0 : Settings.SfxVol + 0.1f; Settings.Apply(); OpenSettings(); });
            Row($"대사 효과음 {(int)(Settings.Voice * 100)}%", () => { Settings.Voice = Settings.Voice >= 0.99f ? 0 : Settings.Voice + 0.1f; Settings.Apply(); OpenSettings(); });
            Row($"마우스 감도 {Settings.Sensitivity:0.0}", () => { Settings.Sensitivity = Settings.Sensitivity >= 4.9f ? 0.6f : Settings.Sensitivity + 0.4f; OpenSettings(); });
            Row($"잔혹 표현: {(Settings.Gore == 2 ? "강함" : Settings.Gore == 1 ? "기본" : "완화")}", () => { Settings.Gore = (Settings.Gore + 2) % 3; OpenSettings(); });
            Row($"루프 기준 인원: {Settings.FloorPreset}명", () => { Settings.FloorPreset = Settings.FloorPreset == 4 ? 5 : 4; OpenSettings(); });
            Row($"시점 기본값: {(Settings.ThirdPerson ? "3인칭" : "1인칭")}", () => { Settings.ThirdPerson = !Settings.ThirdPerson; OpenSettings(); });
            Row($"UI 크기: {Mathf.RoundToInt(Settings.UIScale * 100)}%", () => { Settings.UIScale = Settings.UIScale < 1.1f ? 1.25f : Settings.UIScale < 1.4f ? 1.5f : 1f; Settings.Apply(); OpenSettings(); });
            Row("도움: " + new[] { "끔", "보통", "친절" }[Settings.Assist], () => { Settings.Assist = (Settings.Assist + 1) % 3; OpenSettings(); });
            Row("심판 제한 시간: " + new[] { "끔", "60초", "30초" }[Settings.TrialTime], () => { Settings.TrialTime = (Settings.TrialTime + 1) % 3; OpenSettings(); });
        }

        // ---------------------------------------------------------------- backdrop: one dream-room of the mansion
        void BuildBackdrop()
        {
            _backdrop = new GameObject("TitleBackdrop").transform;
            Layout L = null;
            try { L = LayoutGenerator.Generate(0xB4551E1UL, 1, new RngSet { CampaignSeed = 0xB4551E1UL }); } catch (Exception e) { Debug.LogException(e); }
            MansionView mv = null;
            if (L != null)
            {
                try { mv = MansionView.Build(L, _backdrop); } catch (Exception e) { Debug.LogException(e); }
                if (mv == null) mv = FallbackMansion.Build(L, _backdrop);
                _titleView = mv;
                var hall = L.Rooms.FirstOrDefault(r => r.Type == RoomType.GrandHall && r.Floor == 0) ?? L.Rooms.First();
                _center = mv.ToWorld(new P3(hall.Floor, hall.Rect.CX, hall.Rect.CZ));
                // find the most open view across the hall (the grand stair and pillars must not fill the frame)
                Physics.SyncTransforms();
                float bestScore = -1; _camYaw = 160; _camDist = 6.8f;
                for (int a = 0; a < 360; a += 12)
                {
                    var dir = Quaternion.Euler(0, a, 0) * Vector3.forward; var from = _center + Vector3.up * 1.5f;
                    float dist = Physics.SphereCast(from, 0.45f, dir, out var hit, 14f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 14f;
                    // the path the characters stand on must be clear too
                    bool nearClear = !Physics.CheckSphere(_center + dir * 2.2f + Vector3.up * 1f, 0.6f, ~0, QueryTriggerInteraction.Ignore);
                    float score = Mathf.Min(dist, 9f) + (nearClear ? 3f : 0f);
                    if (score > bestScore) { bestScore = score; _camYaw = a; _camDist = Mathf.Clamp(dist - 0.8f, 3.5f, 7.5f); }
                }
                var toCam = Quaternion.Euler(0, _camYaw, 0) * Vector3.forward; var side = Vector3.Cross(Vector3.up, toCam);
                _focus = _center + toCam * (_camDist * 0.42f);
                // the three modeled students, posed between the camera and the hall
                string[] ids = { "P01", "P02", "P04" }; float[] offs = { -1.1f, 0.2f, 1.3f }; float[] back = { 0f, 0.7f, 0.35f };
                for (int k = 0; k < ids.Length; k++)
                {
                    var def = Cast.Get(ids[k]); if (def == null) continue;
                    var holder = new GameObject("Title_" + ids[k]).transform; holder.SetParent(_backdrop, false);
                    var pos = _focus + side * offs[k] - toCam * back[k];
                    if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out var gh, 4f, ~0, QueryTriggerInteraction.Ignore)) pos.y = gh.point.y;
                    holder.position = pos;
                    holder.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(toCam, Vector3.up)) * Quaternion.Euler(0, (k - 1) * -14f, 0);
                    ActorRig rig = null;
                    try { rig = ActorFactory.Create(def, holder); } catch (Exception e) { Debug.LogException(e); }
                    if (rig == null) rig = FallbackActor.Create(def, holder);
                    rig?.SetExpression(k == 0 ? Expr.Neutral : k == 1 ? Expr.Smirk : Expr.Blank);
                    rig?.Anim?.PlayGesture(k == 0 ? Gesture.LookAround : k == 1 ? Gesture.CrossArms : Gesture.Think, 999);
                }
            }
            var camGo = new GameObject("TitleCamera", typeof(Camera)); _cam = camGo.GetComponent<Camera>(); _cam.fieldOfView = 42; _cam.depth = 5;
            var d = camGo.AddComponent<UniversalAdditionalCameraData>(); d.renderPostProcessing = true; d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            BL23.Game.Mansion.MansionAtmosphere.SetupCamera(camGo.GetComponent<Camera>());
            _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = new Color(0.03f, 0.01f, 0.06f);
            camGo.transform.SetParent(_backdrop, false);
            if (_titleView != null) _titleView.ViewCamera = _cam;
        }

        /// <summary>Quitting waits for an autosave that is still being written in the background.</summary>
        void OnApplicationQuit() { try { SaveStore.Flush(); } catch (System.Exception) { } }

        void Update()
        {
            if (_cam == null || _backdrop == null) return;
            _orbit += Time.deltaTime * 3.5f;
            // a slow sway around the open view, looking at the three students with the hall behind them
            var p = _center + Quaternion.Euler(0, _camYaw + Mathf.Sin(_orbit * 0.05f) * 7f, 0) * new Vector3(0, 0, _camDist) + Vector3.up * (1.55f + Mathf.Sin(_orbit * 0.08f) * 0.12f);
            _cam.transform.position = p; _cam.transform.LookAt(_focus + Vector3.up * 1.25f + Vector3.up * Mathf.Sin(_orbit * 0.03f) * 0.1f);
        }

        // ---------------------------------------------------------------- campaigns
        public void NewGame(ulong seed)
        {
            if (seed == 0) seed = SeedText != null && ulong.TryParse(SeedText, out var s) ? s : (ulong)DateTime.Now.Ticks;
            HideTitle();
            Session.I?.Teardown();
            try { ActorFactory.Preload(); } catch (Exception e) { Debug.LogException(e); }
            var sim = Simulation.NewCampaign(seed, Settings.FloorPreset, Version);
            Session.Create(sim);
        }

        void Continue() { var p = Session.NewestSave(); if (p != null) LoadPath(p); }

        void LoadPath(string path)
        {
            var st = Session.LoadPath(path, out var note);
            if (st == null) { ClearSub(); UIKit.Text(_sub, "E", note ?? "불러올 수 없다", 28, Pal.Blood, TextAlignmentOptions.Center, Fonts.Bold); return; }
            StartFromState(st, note);
        }

        public void StartFromState(GameState st, string note)
        {
            HideTitle();
            Session.I?.Teardown();
            var sim = Simulation.FromState(st);
            var s = Session.Create(sim);
            if (!string.IsNullOrEmpty(note)) s.Hud?.Toast(note, Pal.Gold, 4);
        }

        public void ToTitle()
        {
            Session.I?.Teardown();
            ShowTitle();
        }
    }
}
