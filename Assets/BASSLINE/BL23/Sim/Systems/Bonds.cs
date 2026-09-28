using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// Bond stories (Content/BondScenes*.cs): when someone has grown close enough to 민혁, a conversation can turn
    /// personal. Stages 1–3 per loop, stage 4 only after the player has heard stage 3 in an earlier loop.
    /// At most one bond scene per person per day, so closeness is built over days, not in one sitting.
    /// </summary>
    public sealed partial class Simulation
    {
        public int BondStage(string npc) => S.Flags.TryGetValue($"bond:{S.Loop}:{npc}", out var v) ? (int)v : 0;

        public BondScene BondAvailable(Actor npc)
        {
            if (npc == null || npc.IsButler || npc.IsPlayer || !npc.Alive || S.Phase != Phase.Daily) return null;
            if (S.Flags.TryGetValue($"bondday:{npc.Id}", out var day) && (int)day == S.Day) return null;
            int next = BondStage(npc.Id) + 1; if (next > 4) return null;
            if (next == 4 && !S.Profile.Nodes.Contains("bond3:" + npc.Id)) return null;
            var r = S.R(npc.Id, Cast.Player);
            bool ok = next == 1 ? r.Like >= 0.12f
                    : next == 2 ? r.Like >= 0.26f && r.Attach >= 0.06f
                    : next == 3 ? r.Like >= 0.4f && r.Trust >= 0.28f && r.Attach >= 0.14f
                    : r.Like >= 0.48f && r.Trust >= 0.38f;
            if (!ok || r.Grudge > 0.35f) return null;
            return BondScenes.All.FirstOrDefault(s => s.Npc == npc.Id && s.Stage == next);
        }

        public BondScene BondPending(Actor npc)
        {
            if (npc == null || !S.Flags.TryGetValue("bondpending:" + npc.Id, out var sv)) return null;
            int stage = (int)sv; return BondScenes.All.FirstOrDefault(s => s.Npc == npc.Id && s.Stage == stage);
        }

        /// <summary>The scene's opening lines; its choices then become the conversation's options.</summary>
        List<Utterance> BondOpen(Actor npc)
        {
            var res = new List<Utterance>(); var sc = BondAvailable(npc); if (sc == null) return res;
            S.Flags["bondpending:" + npc.Id] = sc.Stage;
            foreach (var l in sc.Lines) res.Add(BondU(npc, l));
            S.Log("BondScene", Cast.Player, npc.Id, data: sc.Id);
            return res;
        }

        List<Utterance> BondPick(Actor npc, string arg)
        {
            var res = new List<Utterance>(); var sc = BondPending(npc); if (sc == null) return res;
            int i = int.TryParse(arg, out var ii) ? ii : 0; if (i < 0 || i >= sc.Choices.Count) i = 0;
            var ch = sc.Choices[i];
            res.Add(new Utterance { Speaker = Cast.Player, Listener = npc.Id, Key = "bond", Text = LineBank.FixParticles(ch.Label), Emotion = Emotion.Neutral, Gesture = Anim.Talk });
            foreach (var l in ch.Reply) res.Add(BondU(npc, l));
            Relations.Change(S, npc.Id, Cast.Player, like: ch.Like, trust: ch.Trust, attach: ch.Attach, memory: sc.Title);
            Relations.Change(S, Cast.Player, npc.Id, like: Math.Max(0, ch.Like) * 0.5f, attach: Math.Max(0, ch.Attach) * 0.5f);
            var k = S.K(Cast.Player);
            if (!string.IsNullOrEmpty(ch.Reveal))
            {
                foreach (var rv in ch.Reveal.Split(';')) if (rv.Length > 0) k.Facts.Add(rv.StartsWith("note:") ? "bondnote:" + npc.Id + ":" + rv.Substring(5) : rv);
            }
            S.Flags.Remove("bondpending:" + npc.Id);
            S.Flags[$"bond:{S.Loop}:{npc.Id}"] = sc.Stage; S.Flags[$"bondday:{npc.Id}"] = S.Day;
            if (sc.Stage >= 3) S.Profile.Nodes.Add("bond3:" + npc.Id);
            if (sc.Stage >= 4) S.Profile.Nodes.Add("bond4:" + npc.Id);
            S.Emit(GameEventType.Relationship, npc.Id, Cast.Player, text: "bond:" + sc.Stage, data: sc.Title);
            return res;
        }

        Utterance BondU(Actor npc, BondLine l)
        {
            string who = l.Who == BondScenes.Me ? Cast.Player : l.Who ?? npc.Id;
            return new Utterance { Speaker = who, Listener = who == Cast.Player ? npc.Id : Cast.Player, Key = "bond", Text = LineBank.FixParticles(l.Text), Emotion = l.Emo, Gesture = Anim.Talk, Seconds = Math.Max(2.5, l.Text.Length * 0.08) };
        }
    }
}
