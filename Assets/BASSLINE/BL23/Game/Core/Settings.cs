using System;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>Player settings (PlayerPrefs). Presentation-only options never change world truth.</summary>
    public static class Settings
    {
        public static float Master { get => PlayerPrefs.GetFloat("bl23.master", 0.9f); set => PlayerPrefs.SetFloat("bl23.master", value); }
        public static float Music { get => PlayerPrefs.GetFloat("bl23.music", 0.75f); set => PlayerPrefs.SetFloat("bl23.music", value); }
        public static float SfxVol { get => PlayerPrefs.GetFloat("bl23.sfx", 0.85f); set => PlayerPrefs.SetFloat("bl23.sfx", value); }
        public static float Voice { get => PlayerPrefs.GetFloat("bl23.voice", 0.6f); set => PlayerPrefs.SetFloat("bl23.voice", value); }
        public static float Sensitivity { get => PlayerPrefs.GetFloat("bl23.sens", 2.2f); set => PlayerPrefs.SetFloat("bl23.sens", value); }
        public static bool InvertY { get => PlayerPrefs.GetInt("bl23.invy", 0) == 1; set => PlayerPrefs.SetInt("bl23.invy", value ? 1 : 0); }
        public static int Gore { get => PlayerPrefs.GetInt("bl23.gore", 2); set => PlayerPrefs.SetInt("bl23.gore", value); } // 0 완화 1 기본 2 강함
        public static int FloorPreset { get => PlayerPrefs.GetInt("bl23.floor", 4); set => PlayerPrefs.SetInt("bl23.floor", value); }
        public static bool HeadBob { get => PlayerPrefs.GetInt("bl23.bob", 1) == 1; set => PlayerPrefs.SetInt("bl23.bob", value ? 1 : 0); }
        public static bool CrouchToggle { get => PlayerPrefs.GetInt("bl23.ctoggle", 1) == 1; set => PlayerPrefs.SetInt("bl23.ctoggle", value ? 1 : 0); }
        public static float UIScale { get => PlayerPrefs.GetFloat("bl23.uiscale", 1f); set => PlayerPrefs.SetFloat("bl23.uiscale", value); }
        public static float TextSpeed { get => PlayerPrefs.GetFloat("bl23.textspeed", 1f); set => PlayerPrefs.SetFloat("bl23.textspeed", value); }
        public static bool ThirdPerson { get => PlayerPrefs.GetInt("bl23.tps", 0) == 1; set => PlayerPrefs.SetInt("bl23.tps", value ? 1 : 0); }
        public static float Fov { get => PlayerPrefs.GetFloat("bl23.fov", 72f); set => PlayerPrefs.SetFloat("bl23.fov", value); }
        public static int Quality { get => PlayerPrefs.GetInt("bl23.quality", 2); set => PlayerPrefs.SetInt("bl23.quality", value); }
        /// <summary>도움: 0 끔 · 1 보통 (다음 할 일 한 줄) · 2 친절 (다음 할 일 + 흔적 표시 + 조준 고리).</summary>
        public static int Assist { get => Mathf.Clamp(PlayerPrefs.GetInt("bl23.assist", 1), 0, 2); set => PlayerPrefs.SetInt("bl23.assist", Mathf.Clamp(value, 0, 2)); }
        /// <summary>재판 결정 순간의 제한 시간: 0 끔 · 1 60초 · 2 30초.</summary>
        public static int TrialTime { get => Mathf.Clamp(PlayerPrefs.GetInt("bl23.trialtime", 0), 0, 2); set => PlayerPrefs.SetInt("bl23.trialtime", Mathf.Clamp(value, 0, 2)); }
        /// <summary>대화 자동 진행 (V).</summary>
        public static bool AutoAdvance { get => PlayerPrefs.GetInt("bl23.autoadv", 0) == 1; set => PlayerPrefs.SetInt("bl23.autoadv", value ? 1 : 0); }
        /// <summary>재판 자동 진행 (재판 중 V).</summary>
        public static bool TrialAuto { get => PlayerPrefs.GetInt("bl23.trialauto", 1) == 1; set => PlayerPrefs.SetInt("bl23.trialauto", value ? 1 : 0); }
        /// <summary>첫날 화면 왼쪽 아래의 조작 안내 줄.</summary>
        public static bool FirstDayKeys { get => PlayerPrefs.GetInt("bl23.firstkeys", 1) == 1; set => PlayerPrefs.SetInt("bl23.firstkeys", value ? 1 : 0); }
        public static string SaveDirOverride;   // automated probe: isolated saves
        public static string SaveDir => SaveDirOverride ?? System.IO.Path.Combine(Application.persistentDataPath, "BL23");
        /// <summary>Manual slots 1..6; slot 0 is the legacy single autosave (still readable).</summary>
        public static string SlotPath(int slot) => System.IO.Path.Combine(SaveDir, slot == 0 ? "autosave.sav" : $"slot{slot}.sav");
        /// <summary>Rotating autosaves auto1..auto3 (the oldest is overwritten).</summary>
        public static string AutoPath(int i) => System.IO.Path.Combine(SaveDir, $"auto{Mathf.Clamp(i, 1, 3)}.sav");
        public static string QuickPath => System.IO.Path.Combine(SaveDir, "quick.sav");

        public static void Apply()
        {
            try { Audio.MusicDirector.Ensure().SetVolume(Master, Music, SfxVol); } catch (Exception) { }
            Audio.VoiceBabble.Volume = Voice * 0.45f;
            UIKit.ApplyScale();
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 120;
        }
    }
}
