using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>Probe shots for the convenience pass: HUD lines, dialogue numbering / paging / backlog, menus and confirmations,
    /// appointments and the compass, look captions, the clue card (one notification per find) and the pre-trial summary.</summary>
    public sealed partial class AutoProbe
    {
        partial void QolHook(string at, List<IEnumerator> run)
        {
            switch (at)
            {
                case "daily": run.Add(QolDaily()); break;
                case "investigation": run.Add(QolInvestigation()); break;
                case "after": run.Add(QolAfter()); break;
            }
        }

        void QolClean() { if (Ses.Note.Open) Ses.Note.Close(); if (Ses.Menu.Open) Ses.Menu.Close(); if (BacklogUI.Open) BacklogUI.Close(); }

        IEnumerator QolDaily()
        {
            Log("qol: daily");
            QolClean();
            yield return Wait(0.8f); yield return ShotCo("hud_legend");
            // conversation conveniences: numbered options, ✔ on a question already asked today, the folded small talk, a paged list, 지난 대화
            var npc = S.LivingNpcs.Where(a => a.Status == ActorStatus.Active && a.PlanId == null && a.TalkingTo == null).OrderBy(a => a.Pos.Dist(S.Player.Pos)).FirstOrDefault();
            if (npc != null)
            {
                var stand = NearIn(npc.Pos, npc.Room, 1.2f); Ses.Player.Teleport(stand, MathX.AngleDeg(npc.Pos.x - stand.x, npc.Pos.z - stand.z)); yield return Wait(0.4f);
                Ses.Dialogue.Open(npc); yield return Wait(1.2f);
                if (Ses.Dialogue.Active)
                {
                    Ses.Dialogue.ProbeFinishLines();
                    if (Ses.Dialogue.ProbeChoose("chat")) { yield return Wait(0.3f); Ses.Dialogue.ProbeFinishLines(); }
                    if (Ses.Dialogue.Active && Ses.Dialogue.ProbeChoose("likes")) { yield return Wait(0.3f); Ses.Dialogue.ProbeFinishLines(); }
                    if (Ses.Dialogue.Active)
                    {
                        yield return Wait(1.0f); yield return ShotCo("dialogue_numbers");
                        if (Ses.Dialogue.ProbeMore()) { yield return Wait(0.8f); yield return ShotCo("dialogue_more"); } else Log("qol: short option list — no 더 이야기하기 group");
                        if (Ses.Dialogue.ProbeSub("warn", 1)) { yield return Wait(0.8f); yield return ShotCo("dialogue_paged"); } else Log("qol: no warn list");
                        BacklogUI.Show(Ses); yield return Wait(0.8f); yield return ShotCo("backlog"); BacklogUI.Close();
                        Log($"qol: backlog lines {Backlog.Lines.Count}");
                        Ses.Dialogue.Close();
                    }
                }
                else Log("qol: could not talk to " + npc.Id);
            }
            yield return Wait(0.5f);
            // save / load with the rotation, a confirmation, the settings rows, the wait menu
            Ses.Menu.OpenSaveLoad(false); yield return Wait(0.8f); yield return ShotCo("menu_saveload");
            var newest = Session.NewestSave(); var ni = newest != null ? SaveStore.Info(newest) : null;
            Ses.Menu.Confirm("불러오기", $"「자동 1」을 불러올까?\n<size=80%><color=#9A8E7C>{ni?.Label ?? "3일째 오후 1:02 · 도서실 · 일상"}\n{MenuUI.SlotLine(ni)}\n저장하지 않은 진행은 사라진다.</color></size>", "불러온다", () => { });
            yield return Wait(0.6f); yield return ShotCo("menu_confirm"); Ses.Menu.Close();
            Ses.Menu.OpenSettings(); yield return Wait(0.8f); yield return ShotCo("settings_rows"); Ses.Menu.Close();
            Ses.Menu.OpenWait(); yield return Wait(0.8f); yield return ShotCo("wait_menu"); Ses.Menu.Close();
            // an invitation accepted: the appointment toast and the compass line (tracked automatically in daily life)
            var host = S.LivingNpcs.Where(a => a.Status == ActorStatus.Active && a != npc && a.PlanId == null).OrderBy(a => a.Pos.Dist(S.Player.Pos)).FirstOrDefault();
            if (host != null && Ses.Sim.ProbeOffer(host) != null)
            {
                Ses.Sim.Choose(host, "req_accept"); yield return Wait(1.6f);
                Log("qol: appointment line: " + (Ses.Hud.ProbeInfo ?? "").Replace("\n", " | "));
                yield return ShotCo("hud_appointment");
            }
            else Log("qol: no invitation could be made");
        }

        IEnumerator QolInvestigation()
        {
            Log("qol: investigation");
            QolClean(); if (Ses.Dialogue.Active) Ses.Dialogue.Close();
            yield return Wait(1.3f);
            Log("qol: case lines: " + (Ses.Hud.ProbeInfo ?? "").Replace("\n", " | "));
            yield return ShotCo("hud_investigation");
            var inc = CaseProgress.Current(S);
            // a plain look: nothing is filed, a caption under the crosshair
            Evidence loose = null; int filedBefore = S.K(Cast.Player).Evidence.Count;
            foreach (var f in S.Layout.Furniture.Where(f => f.Pos.f == S.Player.Pos.f && (inc == null || f.Room != inc.FoundRoom)).OrderBy(f => f.Pos.DistXZ(S.Player.Pos)).Take(12))
            {
                var ev = Ses.Sim.PlayerExamine(f); if (ev != null && ev.Loose) { loose = ev; break; }
            }
            Log($"qol: plain look {(loose != null ? "'" + loose.Title + " — " + loose.Line + "'" : "none found")}; cards filed while looking: {S.K(Cast.Player).Evidence.Count - filedBefore}");
            if (loose != null) { Hud.I?.Look(loose); yield return Wait(0.4f); yield return ShotCo("look_caption"); }
            // a trace examined: the clue card, and no second notification for the same find
            var tr = inc == null ? null : S.Traces.Where(t => t.Room == inc.FoundRoom && !t.Cleaned && t.Visibility <= 1 && t.Type != "PowerResidue").OrderBy(t => t.Id, System.StringComparer.Ordinal).FirstOrDefault();
            if (tr != null)
            {
                var stand = NearIn(tr.Pos, tr.Room, 1.2f); Ses.Player.Teleport(stand, MathX.AngleDeg(tr.Pos.x - stand.x, tr.Pos.z - stand.z), 35); yield return Wait(0.6f);
                var ev = Ses.Sim.PlayerExamine(tr); if (ev != null) Hud.I?.Card(ev);
                yield return Wait(1.7f);   // past the 1.2 s toast batch
                string title = null; try { title = ev != null ? CaseBoard.Describe(Ses.Sim, ev).Title : null; } catch (System.Exception) { }
                Log($"qol: clue card '{title}' — duplicate toasts on screen: {(title != null ? Ses.Hud.ProbeToasts(title) : -1)}");
                yield return ShotCo("clue_card");
            }
            else Log("qol: no visible trace in the found room");
            bool can = Ses.Sim.CanEndInvestigation(out var why, out var left);
            Log($"qol: end investigation allowed={can} why={why} left={left:0}");
            Ses.Menu.OpenWait(); yield return Wait(0.8f); yield return ShotCo("wait_menu_investigation"); Ses.Menu.Close();
            // the summons: 심판 전 정리
            Ses.Speed = 30f; float t0 = Time.realtimeSinceStartup;
            while (S.Phase == Phase.Investigation && Time.realtimeSinceStartup - t0 < 420)
            {
                if (Ses.Speed < 30) Ses.Speed = 30; if (Ses.Dialogue.Active) Ses.Dialogue.Close(); if (Ses.Menu.Open) Ses.Menu.Close();
                yield return null;
            }
            Ses.Speed = 1f;
            yield return Until(() => CaseReport.Open || S.Phase == Phase.Trial, 20, "pretrial summary");
            if (CaseReport.Open) { yield return Wait(0.8f); yield return ShotCo("pretrial_summary"); }
            else Log("qol: no pretrial summary (phase " + S.Phase + ")");
        }

        IEnumerator QolAfter()
        {
            QolClean();
            for (int i = 1; i <= 3; i++) { var info = SaveStore.Info(Settings.AutoPath(i)); Log($"qol: auto{i} {(info.Exists ? info.Label + " @ " + info.Saved.ToString("HH:mm:ss") : "empty")}"); }
            Ses.Menu.OpenSaveLoad(false); yield return Wait(0.8f); yield return ShotCo("menu_saveload_after"); Ses.Menu.Close();
        }
    }
}
