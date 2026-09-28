using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// The one authority over the mouse cursor. It runs last every frame and decides lock and visibility from what is on
    /// screen, so no screen can ever be left with a hidden, locked cursor.
    /// (Owner's report 2026-09-27: in a real build the cursor never came back in the 심판, so the vote could not be
    /// cast. The player controller stops updating when it hands control away, and it had left the cursor locked.)
    /// Rules, first match wins:
    ///  1. Pointer screens (the 심판, the replay, conversation, notebook, menu, pause) → free and visible.
    ///  2. Nobody is walking (title, death, hand-over screens) → free and visible.
    ///  3. Scripted first-person moments and cinematics → locked and hidden, unless a clickable choice is up.
    ///  4. Free roam (including passing time, where the mouse still looks around) → locked and hidden.
    /// </summary>
    [DefaultExecutionOrder(10050)]
    public sealed class CursorGuard : MonoBehaviour
    {
        static CursorGuard _i;
        float _nextScan; bool _choiceUp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_i != null) return;
            var go = new GameObject("BL23_CursorGuard"); DontDestroyOnLoad(go); _i = go.AddComponent<CursorGuard>();
        }

        /// <summary>True while something on screen wants the mouse pointer.</summary>
        public static bool PointerWanted { get; private set; }

        void LateUpdate()
        {
            bool free = WantFree();
            PointerWanted = free;
            var mode = free ? CursorLockMode.None : CursorLockMode.Locked;
            if (Cursor.lockState != mode) Cursor.lockState = mode;
            if (Cursor.visible != free) Cursor.visible = free;
        }

        bool WantFree()
        {
            var s = Session.I;
            if (s == null) return true;   // title / boot screens
            try
            {
                if (s.Trial != null && s.Trial.Active) return true;
                if (s.Reveal != null && s.Reveal.Active) return true;
                if ((s.Dialogue != null && s.Dialogue.Active) || (s.Note != null && s.Note.Open) || (s.Menu != null && s.Menu.Open)) return true;
                var pc = s.Player;
                if (pc == null) return true;
                bool cine = s.Cine != null && s.Cine.Busy;
                if (pc.Scripted || cine) return ChoiceUp();
                if (!pc.Controlling) return true;
                if (TimeLink.LocksMovement) return false;
                return s.Paused;
            }
            catch (System.Exception) { return true; }   // never trap the pointer because of a half-built scene
        }

        /// <summary>A clickable button is visible on some canvas (e.g. a choice during a cinematic). Scanned a few times a second.</summary>
        bool ChoiceUp()
        {
            if (Time.unscaledTime < _nextScan) return _choiceUp;
            _nextScan = Time.unscaledTime + 0.25f; _choiceUp = false;
            foreach (var b in FindObjectsByType<UIKit.Btn>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (Shown(b)) { _choiceUp = true; return true; }
            foreach (var b in FindObjectsByType<GBtn>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (Shown(b)) { _choiceUp = true; return true; }
            return false;
        }

        static bool Shown(Component c)
        {
            if (c == null || !c.gameObject.activeInHierarchy) return false;
            var cv = c.GetComponentInParent<Canvas>(); if (cv == null || !cv.enabled) return false;
            for (var t = c.transform; t != null; t = t.parent) { var g = t.GetComponent<CanvasGroup>(); if (g != null && (g.alpha < 0.05f || !g.blocksRaycasts)) return false; }
            return true;
        }
    }
}
