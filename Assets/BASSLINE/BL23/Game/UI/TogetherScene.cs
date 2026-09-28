using System;
using System.Collections;
using BL23.Sim;
using UnityEngine;
using Pose = BL23.Sim.Pose;

namespace BL23.Game
{
    /// <summary>
    /// "함께 시간을 보낸다": once someone has said yes in conversation (tea, a long talk, a meal, joining what they are
    /// doing), the conversation closes and the time is spent together — 민혁 takes the place the kernel gave (sits down, or
    /// walks the last step), the view stays with the other person (the mouse still looks about), both do the thing, and a
    /// few lines pass between them while the clock runs (the time director shows the lines, the dial and the result card).
    /// When it ends the view is free again; if a personal story is ready to be told, the conversation opens by itself.
    /// </summary>
    public sealed class TogetherScene : MonoBehaviour
    {
        static TogetherScene _i;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (_i != null) return; var go = new GameObject("BL23 TogetherScene"); DontDestroyOnLoad(go); _i = go.AddComponent<TogetherScene>(); }

        TimeDirector _dir; SkipPlan _plan; string _partner, _label; int _room = -1;

        void OnDestroy() { Unhook(); if (_i == this) _i = null; }
        void Unhook() { if (_dir != null) { _dir.Began -= OnBegan; _dir.Ended -= OnEnded; } _dir = null; }

        void Update()
        {
            var s = Session.I; if (s == null || s.Sim == null) { Unhook(); return; }
            var dir = TimeLink.Dir;
            if (dir != _dir) { Unhook(); _dir = dir; if (_dir != null) { _dir.Began += OnBegan; _dir.Ended += OnEnded; } }
            if (_dir == null) return;
            var tp = TimeLink.PendingTogether; if (tp == null) { _waitFor = null; return; }
            // after the conversation has closed (not on the frame of its last key), once nothing else holds the screen
            if ((s.Dialogue?.Active ?? false) || Time.frameCount == DialogueUI.ClosedFrame || _dir.Active || (s.Cine?.Busy ?? false) || s.IsPaused("menu"))
            {
                // something else still holds the screen (the table talk at a meal, a lapse): say so instead of leaving the player
                // wondering (once, after a second)
                if (!(s.Dialogue?.Active ?? false))
                {
                    if (_waitFor != tp) { _waitFor = tp; _waitSince = Time.unscaledTime; _waitNoted = false; }
                    else if (!_waitNoted && Time.unscaledTime - _waitSince > 1f) { _waitNoted = true; Hud.I?.SysNote(LineBank.FixParticles($"{Cast.GivenOf(tp.Npc)}와(과) 함께할 준비를 한다…"), 2.5f); }
                }
                return;
            }
            if (s.S.Phase != Phase.Daily) { TimeLink.PendingTogether = null; return; }
            var ps = s.Sim.PendingStop;
            if (ps != null && ps.Class == StopClass.Critical) { TimeLink.PendingTogether = null; Hud.I?.Toast("지금은 함께할 때가 아니다", Pal.TextDim, 2.2f); return; }
            if (!_dir.CanStart(TimeDirector.Style.Scene)) return;   // (planning draws the lines and folds the talk in: only when it will start)
            Begin(s, tp);
        }

        TogetherPlan _waitFor; float _waitSince; bool _waitNoted;

        void Begin(Session s, TogetherPlan tp)
        {
            TimeLink.PendingTogether = null; _waitFor = null;
            double talk0 = s.Sim.PendingTalk;
            SkipPlan plan = null; string why = null;
            try { plan = s.Sim.PlanTogether(tp, out why); } catch (Exception e) { Debug.LogException(e); }
            if (plan == null) { Hud.I?.Toast(why ?? "지금은 함께할 수 없다", Pal.TextDim, 2.4f); return; }
            if (string.IsNullOrEmpty(plan.Label)) plan.Label = tp.Label;
            if (!_dir.Start(plan, TimeDirector.Style.Scene))
            {
                s.Sim.PendingTalk = Math.Max(s.Sim.PendingTalk, talk0);   // the conversation's minutes are not lost
                Hud.I?.Toast("지금은 함께할 수 없다", Pal.TextDim, 2f);
            }
        }

        void OnBegan(SkipPlan p)
        {
            if (p == null || p.Kind != SkipKind.Together) return;
            var s = Session.I; var pc = s?.Player; var me = s?.S.Player; if (pc == null || me == null) return;
            _plan = p; _partner = p.Partner; _label = p.Label; _room = me.Room;
            // the place the kernel gave: a seat (sit down) or a spot beside the other person (the last step)
            if (TimeLink.PlayerOnSpot(out var sp) && me.Pose == Pose.Sit) { if (!pc.Seated) pc.EnterSeat(sp); }
            else
            {
                var w = s.World.ToWorld(me.Pos); var feet = pc.FeetPosition;
                if (new Vector2(w.x - feet.x, w.z - feet.z).magnitude > 0.15f) pc.ScriptedWalkTo(w, 1.0f);
            }
            var pv = s.World.ViewOf(_partner);
            if (pv != null) pc.SceneLook(pv.Rig != null && pv.Rig.EyeAnchor != null ? pv.Rig.EyeAnchor : pv.transform);
        }

        void OnEnded(SkipPlan p, SkipResult r)
        {
            if (p == null || p != _plan) return;
            var s = Session.I; _plan = null;
            s?.Player?.SceneLook(null);
            if (s == null) return;
            var stop = r?.Stop ?? p.Stop;
            if (stop != null && stop.Kind == StopKind.PartnerLeft && _partner != null)
                Backlog.Add(_partner, $"(자리를 떴다 — 함께 {(_label ?? "시간을 보내던")} 중에)", s.S.Clock, s.S.RoomName(_room));
            if (!string.IsNullOrEmpty(r?.BondReady) && _partner != null && (stop == null || stop.Kind == StopKind.Target)) StartCoroutine(OpenBond(_partner));
        }

        /// <summary>A personal story is ready: the conversation opens a moment after the result card.</summary>
        IEnumerator OpenBond(string npc)
        {
            float until = Time.unscaledTime + 1.2f;
            while (Time.unscaledTime < until) yield return null;
            for (float waited = 0f; waited < 6f; waited += Time.unscaledDeltaTime)
            {
                var s = Session.I; if (s == null) yield break;
                bool free = !(s.Dialogue?.Active ?? false) && !(s.Cine?.Busy ?? false) && !TimeLink.TimeActive && !(s.Menu?.Open ?? false) && s.Player != null && s.Player.Controlling;
                if (free)
                {
                    var a = s.S.A(npc); if (a == null || !a.Alive || a.Room != s.S.Player.Room) yield break;
                    s.Dialogue.Open(a); yield break;
                }
                yield return null;
            }
        }
    }
}
