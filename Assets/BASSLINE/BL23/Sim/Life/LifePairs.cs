using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// F4 — pair scenes and flashpoints (DailyLifeDesign §3.6). Two residents with a tie, free and in one room, in the right
    /// place and time: the scene brews. With 민혁 in the room it waits for him (a Social stop "…목소리가 높아진다" during a
    /// wait; at once when he walks in) and ends in his choice; otherwise it plays NPC-only — the opening lines are said aloud
    /// (overheard through walls, kept in 지난 대화), the scene's NPC outcome applies, and it becomes talk (a rumour).
    /// Cooldown per scene (days); at most two a day in front of 민혁, at most five NPC-only.
    /// Also the staged-scene door for the presentation: LifeOffer / LifePick / LifeStageAbort.
    /// </summary>
    public sealed partial class Simulation
    {
        public const int PairsPerDayForPlayer = 2, PairsPerDayNpc = 5;

        SceneRun PairRun(PairDef p, int room)
        {
            var run = new SceneRun { Scene = p, Kind = "pair", Npc = p.A, Beat = p.First, Room = room };
            run.Ctx["a"] = p.A; run.Ctx["b"] = p.B; run.Ctx["place"] = room.ToString(CultureInfo.InvariantCulture);
            run.Cast.Add(p.A); run.Cast.Add(p.B);
            return run;
        }

        bool PairEligible(PairDef p, int playerRoom, out int room)
        {
            room = -1;
            if (p.First == null) return false;
            if (S.Flags.TryGetValue("lps:" + p.Id, out var last) && S.Day < last + p.Cooldown) return false;
            if (!TimeFits(p.When)) return false;
            var a = S.A(p.A); var b = S.A(p.B);
            bool pairTalk = a != null && b != null && a.TalkingTo == b.Id && b.TalkingTo == a.Id;
            if (!LifeFree(a, pairTalk) || !LifeFree(b, pairTalk)) return false;
            if (a.Room != b.Room || a.Pos.f != b.Pos.f || a.Pos.DistXZ(b.Pos) > 8.5f) return false;
            if (playerRoom >= 0 && a.Room != playerRoom) return false;
            var r = S.Layout.Room(a.Room); if (!RoomFits(p.Where, r)) return false;
            if (!LifeIf(p.If, PairRun(p, a.Room))) return false;
            room = a.Room; return true;
        }

        void PairTick(int mod)
        {
            if (mod < 8 * 60 || mod >= 23 * 60) return;
            var me = S.Player; bool around = LifePlayerAround() && !PlayerInTogether();
            // a scene that brewed in 민혁's room while he was waiting there, and he never came to it: it plays out without him
            foreach (var p in LifeData.Pairs)
            {
                if (!S.Flags.TryGetValue("lpsbrew:" + p.Id, out var at)) continue;
                if (S.Clock - at < 20) continue;
                S.Flags.Remove("lpsbrew:" + p.Id);
                if (PairEligible(p, -1, out int room0) && LF($"lps:npc:{S.Day}") < PairsPerDayNpc) PairNpcOnly(p, room0);
            }
            var cands = new List<(PairDef p, int room)>();
            foreach (var p in LifeData.Pairs) if (!S.Flags.ContainsKey("lpsbrew:" + p.Id) && PairEligible(p, -1, out int room)) cands.Add((p, room));
            if (cands.Count == 0) return;
            if (!LR.Chance(0.4)) return;
            var pick = LR.Weighted(cands, c => c.p.Weight * (c.p.PairKind == "conflict" || c.p.PairKind == "rival" ? 1.1 : 1.0));
            if (around && me.Room == pick.room && LF($"lps:seen:{S.Day}") < PairsPerDayForPlayer)
            {
                S.Flags["lpsbrew:" + pick.p.Id] = S.Clock;
                string title = LineBank.FixParticles($"{S.RoomName(pick.room)}에서 {Cast.GivenOf(pick.p.A)}와(과) {Cast.GivenOf(pick.p.B)}의 목소리가 들린다");
                RaiseStop(StopKind.Approach, StopClass.Social, title, "가 볼까?", pick.p.A, pick.room);
                S.Log("PairBrew", pick.p.A, pick.p.B, room: pick.room, data: pick.p.Id);
                return;
            }
            if (LF($"lps:npc:{S.Day}") >= PairsPerDayNpc) return;
            PairNpcOnly(pick.p, pick.room);
        }

        /// <summary>The scene plays without 민혁: its opening is said aloud, the NPC outcome applies, witnesses remember, it becomes talk.</summary>
        void PairNpcOnly(PairDef p, int room)
        {
            var run = PairRun(p, room);
            var lines = new List<Utterance>();
            foreach (var l in p.First.Lines) if (LifeIf(l.If, run)) lines.Add(LifeU(l, run));
            LifeSayLater(lines, 0.3, 1.1);
            foreach (var fx in p.NpcFx) ApplyFx(fx, run);
            if (p.NpcTie != null) LifeTie(p.NpcTie);
            PairAfter(p, room, false);
            string origin = S.Living.Where(w => w.Room == room && w.Id != p.A && w.Id != p.B && !w.IsPlayer && w.Pose != Pose.Sleep).OrderBy(w => w.Id, StringComparer.Ordinal).Select(w => w.Id).FirstOrDefault();
            if (p.NpcRumour != null) SeedRumour(p.NpcRumour, p.A, p.B, origin ?? (p.PairKind == "conflict" ? p.B : p.A), room, false, p.Id);
            LFinc($"lps:npc:{S.Day}");
            S.Log("PairScene", p.A, p.B, room: room, data: p.Id + ":npc");
        }

        /// <summary>What any pair scene leaves: the cooldown, the bodies turning to each other, witnesses' memory, the table's "yesterday's quarrel".</summary>
        void PairAfter(PairDef p, int room, bool withPlayer)
        {
            S.Flags["lps:" + p.Id] = S.Day; LFinc("lps:total"); LFinc($"lps:day:{S.Day}");
            if (p.PairKind == "conflict" || p.PairKind == "rival") S.Flags[$"lconf:{S.Day}:{p.A}:{p.B}"] = S.Clock;
            foreach (var w in S.Living.Where(w => w.Room == room && w.Pose != Pose.Sleep).OrderBy(w => w.Id, StringComparer.Ordinal))
            {
                S.K(w.Id).Facts.Add($"lifescene:{p.Id}:{S.Day}");
                if (p.PairKind == "conflict" && w.Id != p.A && w.Id != p.B) S.K(w.Id).Facts.Add($"argued:{p.A}:{p.B}");
            }
            if (withPlayer) PK.Facts.Add($"saw-scene:{p.Id}:{S.Day}");
            PairBodies(p, room);
        }

        void PairBodies(PairDef p, int room)
        {
            var a = S.A(p.A); var b = S.A(p.B); if (a == null || b == null) return;
            double mins = Math.Min(8, 1.5 + p.First.Lines.Count * 1.1);
            Anim am = p.PairKind == "conflict" || p.PairKind == "rival" ? Anim.Angry : p.PairKind == "tease" || p.PairKind == "comic" ? Anim.Laugh : Anim.Talk;
            Anim bm = p.PairKind == "conflict" || p.PairKind == "rival" ? Anim.CrossArms : p.PairKind == "tease" || p.PairKind == "comic" ? Anim.Laugh : Anim.Listen;
            foreach (var (x, y, anim) in new[] { (a, b, am), (b, a, bm) })
            {
                if (x.TalkingTo != null && x.TalkingTo != y.Id) continue;
                var act = new Activity { Id = "life:pair:" + y.Id, Label = Cast.GivenOf(y.Id) + "하고 이야기", Priority = 2.6 };
                act.Steps.Add(new ActionStep { Kind = "Face", Actor = y.Id });
                act.Steps.Add(Do("company", mins, anim));
                Assign(x, act);
            }
        }

        // ================================================================== staged scenes for the presentation (LifeSceneUI)
        LifeStage ToStage(SceneRun run, List<Utterance> lines)
        {
            foreach (var u in lines) LifeLineCount(u.Speaker, u.Text);
            var st = new LifeStage { Id = run.Scene.Id, Kind = run.Kind, Title = run.Scene.Title, Room = run.Room, Cast = run.Cast.ToList(), Lines = lines, Note = run.Note, Done = run.Over };
            run.Note = null;
            if (!run.Over) st.Options = VisibleOpts(run).Select(o => OptLabel(o, run)).ToList();
            return st;
        }

        /// <summary>Presentation, every half second while 민혁 is free in daily life: a scene to stage here and now, or null
        /// (a festival or memorial he walked into, a pair scene brewing in his room). Zero clock time.</summary>
        public LifeStage LifeOffer()
        {
            if (!LifePlayerAround() || (_stage != null && !_stage.Over)) return null;
            try
            {
                var fs = FestOffer(); if (fs != null) return fs;
                var me = S.Player; if (me.Room < 0) return null;
                if (LF($"lps:seen:{S.Day}") >= PairsPerDayForPlayer || S.Clock - LF("lps:last", -999) < 45) return null;
                PairDef pick = null; int room = me.Room;
                foreach (var p in LifeData.Pairs)
                    if (S.Flags.ContainsKey("lpsbrew:" + p.Id) && PairEligible(p, me.Room, out _)) { pick = p; break; }
                if (pick == null)
                {
                    var cands = LifeData.Pairs.Where(p => PairEligible(p, me.Room, out _)).ToList();
                    if (cands.Count > 0) pick = LR.Weighted(cands, p => p.Weight);
                }
                if (pick == null) return null;
                return StartPairStage(pick, room);
            }
            catch (Exception e) { Fault("life:offer", e); _stage = null; return null; }
        }

        LifeStage StartPairStage(PairDef p, int room)
        {
            S.Flags.Remove("lpsbrew:" + p.Id);
            var run = PairRun(p, room); run.Cast.Add(Cast.Player);
            _stage = run;
            LFinc($"lps:seen:{S.Day}"); S.Flags["lps:last"] = S.Clock;
            PairAfter(p, room, true);
            S.Log("PairScene", p.A, p.B, room: room, data: p.Id + ":player");
            var lines = RunFrom(run);
            if (run.Over) _stage = null;
            return ToStage(run, lines);
        }

        /// <summary>Presentation: 민혁 chose option i of the staged scene. Returns what follows (lines, maybe another choice).</summary>
        public LifeStage LifePick(int i)
        {
            var run = _stage; if (run == null || run.Over) return null;
            try
            {
                var lines = RunPick(run, i);
                var st = ToStage(run, lines);
                if (run.Over) { StageFinished(run); _stage = null; }
                return st;
            }
            catch (Exception e) { Fault("life:pick", e); _stage = null; return null; }
        }

        /// <summary>Presentation: the staged scene was cut short (a scream, a menu). Nothing more is applied.</summary>
        public void LifeStageAbort() { if (_stage != null) { S.Dev("life stage aborted " + _stage.Scene?.Id); _stage = null; } }

        /// <summary>Is a staged scene waiting for 민혁's choice?</summary>
        public bool LifeStageOpen => _stage != null && !_stage.Over;

        void StageFinished(SceneRun run)
        {
            if (run.Kind == "fest") FestFinished(run);
            if (run.Note != null) S.Emit(GameEventType.Notice, Cast.Player, run.Npc, text: run.Note, key: "life_note");
        }
    }
}
