using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>Probe shots for the clue model v2-lite: the notebook's 단서 tab (사건 개요, a body card, a witness sheet, 기타),
    /// the 동선 board, 인물, 일정·소지품, 지도 pins, and the court's clue picker (발언 되짚기, a preview, a result plate).</summary>
    public sealed partial class AutoProbe
    {
        /// <summary>A line in the probe log from presentation code (the court's decision moments).</summary>
        public static void Note(string s) { if (_i != null) _i.Log(s); }

        partial void CluesHook(string at, List<IEnumerator> run)
        {
            if (at == "investigation") run.Add(CluesInvestigation());
            else if (at == "trial") run.Add(CluesTrial(20f));
            else if (at == "trialtour") run.Add(CluesTrial(120f));
        }

        IEnumerator CluesInvestigation()
        {
            var inc = CaseProgress.Current(S);
            if (inc == null) { Log("clues: no current case"); yield break; }
            // fill the notebook like a thorough player: the found room's visible traces, then 사건 이야기 with the five nearest
            int traces = 0, spoken = 0;
            foreach (var t in S.Traces.Where(t => t.Room == inc.FoundRoom && !t.Cleaned && t.Visibility <= 1).OrderBy(t => t.Id, StringComparer.Ordinal).Take(8).ToList())
            {
                try { Ses.Sim.PlayerExamine(t); traces++; } catch (Exception e) { Log("clues: examine failed " + e.Message); }
            }
            foreach (var npc in S.LivingNpcs.Where(a => !a.IsButler).OrderBy(a => a.Pos.Dist(S.Player.Pos)).ThenBy(a => a.Id, StringComparer.Ordinal).Take(5).ToList())
            {
                try { foreach (var u in Ses.Sim.Choose(npc, "q_case") ?? new List<Utterance>()) { Ses.Sim.Spoken(u); spoken++; } }
                catch (Exception e) { Log("clues: q_case failed " + e.Message); }
            }
            yield return Wait(0.4f);
            var cards = CaseBoard.Cards(Ses.Sim);
            int inCase = cards.Count(v => v.InCase), key = cards.Count(v => v.Key);
            int firm = 0; try { firm = CaseBoard.FirmCount(Ses.Sim); } catch (Exception) { }
            Log($"clues: examined {traces} traces, {spoken} lines; cards {cards.Count} (사건 {inCase}, 중요 {key}, 기타 {cards.Count - inCase}), 개요 {firm}/4");
            foreach (var v in cards.Take(24)) Log($"   card [{v.KindLabel}{(v.Key ? " 중요" : "")}{(v.InCase ? "" : " 기타")}] {v.Title} — {v.Line} ({v.When})");

            Ses.Note.ProbeSelect(null); yield return Wait(0.8f); yield return ShotCo("clue_overview");
            var body = cards.FirstOrDefault(v => v.Ev != null && v.Ev.Kind == EvKind.Body);
            if (body != null) { Ses.Note.ProbeSelect(body.Id); yield return Wait(0.6f); yield return ShotCo("clue_body"); }
            var sheet = cards.Where(v => v.Ev != null && v.Ev.Kind == EvKind.Testimony).OrderByDescending(v => v.Bullets?.Count ?? 0).FirstOrDefault();
            if (sheet != null) { Ses.Note.ProbeSelect(sheet.Id); yield return Wait(0.6f); yield return ShotCo("clue_witness"); }
            if (cards.Any(v => !v.InCase)) { Ses.Note.ProbeOther(true); yield return Wait(0.6f); yield return ShotCo("clue_other_open"); Ses.Note.ProbeOther(false); }
            Ses.Note.OpenTab("timeline"); yield return Wait(0.8f); yield return ShotCo("note_board");
            Ses.Note.OpenTab("people"); yield return Wait(0.6f); yield return ShotCo("note_people");
            Ses.Note.OpenTab("schedule"); yield return Wait(0.6f); yield return ShotCo("note_schedule");
            Ses.Note.ProbeMapRoom(inc.FoundRoom); yield return Wait(0.8f); yield return ShotCo("note_map_pins");
            Ses.Note.Close(); yield return Wait(0.4f);
        }

        IEnumerator CluesTrial(float wait)
        {
            var tr = Ses.Trial; if (tr == null) yield break;
            // a quiet moment with at least two statements heard
            yield return Until(() => S.Phase != Phase.Trial || (tr.ProbeReady && CluePicker.ReviewClaims(S.Trial).Count >= 2), wait, "claims for the picker");
            if (S.Phase != Phase.Trial || !tr.ProbeReady) { Log("clues: court never idle for the picker"); yield break; }
            var arsenal = TrialGames.Arsenal(Ses.Sim);
            Log($"clues: picker with {CluePicker.ReviewClaims(S.Trial).Count} statements, {arsenal.Count} cards; top titles: {string.Join(" / ", arsenal.Take(6).Select(b => b.Title))}");
            tr.ProbePicker(); yield return Wait(0.9f);
            if (!tr.PickerOpen) { Log("clues: picker did not open"); yield break; }
            yield return ShotCo("trial_picker");
            tr.ProbePickerCard(); yield return Wait(0.6f); yield return ShotCo("trial_picker_preview");
            if (tr.ProbePickerCommit()) { Log("clues: presented a working card (result plate)"); yield return Wait(3.2f); yield break; }
            Log("clues: no card breaks a heard statement yet — waiting for one"); tr.ProbeCloseFocus(); yield return Wait(0.5f);
            // the core court move (present a card → the result plate) must be seen at least once: keep listening while the debate
            // runs (a decision moment with a working card presents it by itself) and use the first statement a card can break
            float t0 = Time.realtimeSinceStartup;
            while (S.Phase == Phase.Trial && Time.realtimeSinceStartup - t0 < 45f)
            {
                if (tr.ProbeReady && tr.ProbeBreakable())
                {
                    tr.ProbePicker(); yield return Wait(0.6f);
                    if (tr.PickerOpen) { tr.ProbePickerCard(); yield return Wait(0.5f); if (tr.ProbePickerCommit()) { Log($"clues: presented a working card after {Time.realtimeSinceStartup - t0:0}s (result plate)"); yield return Wait(3.2f); yield break; } tr.ProbeCloseFocus(); }
                }
                yield return Wait(0.5f);
            }
            Log("clues: still no card broke a statement in this trial");
        }
    }
}
