using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    public sealed class Utterance
    {
        public string Speaker; public string Listener; public string Key; public string Text; public Prop Prop; public bool Lie; public Emotion Emotion = Emotion.Neutral; public Anim Gesture = Anim.Talk;
        public double Seconds = 3.5;
    }

    public sealed partial class Simulation
    {
        sealed class Convo { public string A, B, Topic, Data; public int Left; public long Next; public bool AFirst = true; public int Room; public string Third; }
        readonly List<Convo> _convos = new List<Convo>();

        // ------------------------------------------------------------------ naming / rendering
        public string CallName(string speaker, string target)
        {
            if (target == null) return "";
            var t = Cast.Get(target); if (t == null) return target;
            if (speaker == Cast.Butler) return t.Name + " 님";
            if (target == Cast.Butler) return "유스티 씨";
            bool casual = speaker != null && S.HasRel(speaker, target) ? S.R(speaker, target).Casual : !(Cast.Get(speaker)?.Speech.PoliteDefault ?? true);
            return casual ? t.Given : t.Given + " 씨";
        }

        public string Render(string speaker, string listener, string key, Dictionary<string, string> slots = null)
        {
            bool casual = listener != null && S.HasRel(speaker, listener) ? S.R(speaker, listener).Casual : !(Cast.Get(speaker)?.Speech.PoliteDefault ?? true);
            if (speaker == Cast.Butler) casual = false;
            var raw = LineContext.Resolve(this, speaker, ref listener, key, slots, ref casual);   // voice resolver: pair/about/situation variants + no-repeat (Content/LineContext.cs)
            if (raw == null) return null;
            var d = new Dictionary<string, string>();
            if (listener != null) d["you"] = CallName(speaker, listener);
            if (slots != null) foreach (var kv in slots) d[kv.Key] = kv.Value != null && kv.Value.StartsWith("@") ? CallName(speaker, kv.Value.Substring(1)) : kv.Value;
            return LineBank.Render(raw, d, casual);
        }

        /// <summary>Speak a keyed line out loud (world-audible). Returns the text.</summary>
        public string Speak(Actor a, string key, string listener = null, Dictionary<string, string> slots = null, Prop prop = null, bool lie = false, bool loud = false)
        {
            if (a == null || !a.Alive || a.Body.Speech < 0.2f) return null;
            string text = Render(a.Id, listener, key, slots) ?? "…";
            a.LastSaid = text; a.LastSaidAt = S.Clock;
            S.Log("Speech", a.Id, listener, room: a.Room, pos: a.Pos, data: key + "|" + text, secret: false);
            S.Emit(GameEventType.Speech, a.Id, listener, text: text, room: a.Room, pos: a.Pos, key: key, value: loud ? 1 : 0);
            // hearers: same room within 14m (or adjacent open) get the statement if it carries a proposition
            foreach (var h in S.Actors.Values)
            {
                if (h == a || !h.Alive || h.Pose == Pose.Sleep) continue;
                if (h.Pos.f != a.Pos.f) continue;
                float d = h.Pos.DistXZ(a.Pos); if (d > (loud ? 22f : 12f)) continue;
                var g = S.Layout.Nav(a.Pos.f);
                if (h.Room != a.Room && !g.Ray(a.Pos.x, a.Pos.z, h.Pos.x, h.Pos.z, door => S.Layout.Doors[door].Open, false, out _)) continue;
                if (prop != null) Learn(h.Id, a.Id, prop, text, lie, listener == h.Id ? false : true);
            }
            if (loud) Sound(SoundKind.Shout, a.Pos, 0.55f, a.Id, a.Id); else Sound(SoundKind.Talk, a.Pos, 0.22f, a.Id, a.Id);
            return text;
        }

        public void Say(Actor a, string key, string place, string target)
        {
            var slots = new Dictionary<string, string>(); if (place != null) slots["place"] = place; if (target != null) slots["t"] = "@" + target;
            Speak(a, key, null, slots, loud: a.IsButler);
        }

        /// <summary>Record that 'who' received a proposition from 'from'. Hearsay keeps the original root (no false independence).</summary>
        public void Learn(string who, string from, Prop p, string text, bool lie, bool overheard)
        {
            var k = S.K(who);
            string root = p.Value != null && p.Value.StartsWith("root:") ? p.Value : "stmt:" + from + ":" + p.Kind + ":" + p.A + ":" + (int)p.T0;
            if (k.Statements.Any(s => s.Speaker == from && s.Prop.Kind == p.Kind && s.Prop.A == p.A && s.Prop.B == p.B && Math.Abs(s.Prop.T0 - p.T0) < 1)) return;
            k.Statements.Add(new Statement { Id = S.NewId("st"), Speaker = from, Listener = who, Clock = S.Clock, Prop = p.Clone(), Hearsay = overheard, Root = root, Text = text, Lie = lie });
            if (k.Statements.Count > 1200) k.Statements.RemoveRange(0, 200);
            if (who == Cast.Player) Evidences.FromStatement(this, who, from, p, text, overheard);
        }

        // ------------------------------------------------------------------ NPC social activity
        Activity SocialActivity(Actor a, Rng rng)
        {
            var cands = S.Living.Where(t => t != a && t.Alive && t.Pose != Pose.Sleep && t.StairId < 0 && t.CarriedBy == null && (t.Act == null || t.Act.Interruptible) && t.TalkingTo == null).ToList();
            // chat with whoever is around; only a close bond (or real loneliness) is worth a trip across the house
            bool lonely = a.Needs.Social < 0.25f;
            var near = cands.Where(x => x.Room == a.Room || (x.Pos.f == a.Pos.f && a.Pos.Dist(x.Pos) < 9f)).ToList();
            if (near.Count == 0)
            {
                bool Close(Actor x) { var r = S.R(a.Id, x.Id); return r.Romance > 0.3f || r.Tags.Contains("friend") || r.Tags.Contains("family") || r.Tags.Contains("lover") || r.Attach > 0.45f; }
                var friend = cands.Where(Close).OrderBy(x => a.Pos.Dist(x.Pos)).FirstOrDefault();
                if (friend != null && rng.Chance(0.25)) near.Add(friend);
                else if (lonely) return GoWherePeopleAre(a, rng);
                else return null;
            }
            cands = near;
            var t = rng.Weighted(cands, x =>
            {
                var r = S.R(a.Id, x.Id);
                double w = 0.4 + r.Opinion * 1.4 + r.Romance * 1.2 + (r.Grudge > 0.35f ? r.Grudge * 0.8 : 0) + (r.Tags.Contains("friend") ? 0.6 : 0);
                w /= 1 + a.Pos.Dist(x.Pos) / 6.0; if (x.Room == a.Room) w *= 2.5; else if (x.Pos.f != a.Pos.f) w *= 0.35;
                if (x.IsPlayer) w *= 0.55; // NPCs sometimes come to talk to Minhyuk too
                return Math.Max(0.02, w);
            });
            var act = new Activity { Id = "social:" + t.Id, Label = Cast.GivenOf(t.Id) + "하고 이야기", Priority = 1.2 };
            act.Steps.Add(new ActionStep { Kind = "Talk", Actor = t.Id, Duration = 0 });
            return act;
        }

        /// <summary>Lonely but nobody nearby: go and sit where people already are (the lounge crowd, a busy tea room) and stay a while.</summary>
        Activity GoWherePeopleAre(Actor a, Rng rng)
        {
            var groups = S.Living.Where(x => x != a && x.Pose != Pose.Sleep && x.Room >= 0).GroupBy(x => x.Room)
                .Select(g => (room: S.Layout.Room(g.Key), n: g.Count())).Where(g => g.room != null && !RoomInfo.IsPassage(g.room.Type) && g.room.Type != RoomType.Bedroom && RoomUsable(a, g.room) && g.n >= 2).ToList();
            if (groups.Count == 0) return null;
            var pick = rng.Weighted(groups, g => g.n / (1 + a.Pos.Dist(new P3(g.room.Floor, g.room.Rect.CX, g.room.Rect.CZ)) / 15.0));
            var defs = Activities.All.Where(d => d.Rooms.Contains(pick.room.Type) && (d.Group || d.Id == "observe" || d.Id == "tea" || d.Id == "read")).ToList();
            var act = defs.Count > 0 ? Simple(a, rng.Pick(defs).Id, pick.room.Id) : null;
            if (act == null) { act = new Activity { Id = "life:company", Label = "사람들 곁에서 휴식", Priority = 1 }; act.Steps.Add(GoTo(RandomPointIn(pick.room, rng))); act.Steps.Add(Do("observe", rng.Range(30, 60), Anim.Idle)); }
            return act;
        }

        void StepTalk(Actor a, ActionStep st)
        {
            var t = S.A(st.Actor);
            if (t == null || !t.Alive || t.Pose == Pose.Sleep) { FailStep(a, "partner gone"); return; }
            if (a.TalkingTo != null) { a.Speed = 0; return; } // conversation in progress
            float d = a.Pos.Dist(t.Pos);
            if (d > 1.35f)
            {
                if (a.Act.Path == null || S.Tick % 15 == 0)
                {
                    var pr = Pathfinder.Find(S.Layout, a.Pos, t.Pos, DoorCostFor(a));
                    if (!pr.Ok) { FailStep(a, "cannot reach partner"); return; }
                    a.Act.Path = pr.Points; a.Act.PathDoors = pr.DoorAtPoint; a.Act.PathStairs = pr.StairAtPoint; a.Act.PathIdx = 1;
                }
                if (S.Clock - a.Act.StepStart > 25) { FailStep(a, "partner kept moving"); return; }
                MoveAlong(a, new ActionStep { Run = false });
                return;
            }
            a.Speed = 0; a.Act.Path = null;
            if (t.IsPlayer)
            {
                // ask the UI to open an NPC-initiated conversation; if the player ignores it, drop after a while
                if (!S.Flags.ContainsKey("approach:" + a.Id)) { S.Flags["approach:" + a.Id] = S.Clock; S.Emit(GameEventType.Notice, a.Id, t.Id, text: Cast.GivenOf(a.Id) + "이(가) 말을 걸어온다", key: "approach"); RaiseStop(StopKind.Approach, StopClass.Social, Cast.GivenOf(a.Id) + "이(가) 말을 걸어온다", "E 대화", a.Id, a.Room); /* time-on-demand */ }
                else if (S.Clock - S.Flags["approach:" + a.Id] > 4) { S.Flags.Remove("approach:" + a.Id); NextStep(a); }
                return;
            }
            if (t.TalkingTo != null || (t.Act != null && !t.Act.Interruptible)) { if (S.Clock - a.Act.StepStart > 6) FailStep(a, "partner busy"); return; }
            // partner accepts or brushes off depending on how they feel
            var rt = S.R(t.Id, a.Id);
            if (rt.Opinion < -0.35f && S.R(Stream.Life).Chance(0.6)) { Speak(t, "busy", a.Id, new Dictionary<string, string> { { "act", t.Act?.Label ?? "생각" } }); Relations.Change(S, a.Id, t.Id, like: -0.02f, memory: "말을 걸었다가 거절당했다"); NextStep(a); return; }
            StartConvo(a, t);
        }

        void StartConvo(Actor a, Actor b)
        {
            if (b.Act != null && b.Act.Interruptible && !b.IsPlayer) { if (b.Act.Id != null && b.Act.Id.StartsWith("life:")) { /* pause */ } EndActivity(b, false); }
            a.TalkingTo = b.Id; b.TalkingTo = a.Id;
            a.Yaw = MathX.AngleDeg(b.Pos.x - a.Pos.x, b.Pos.z - a.Pos.z); b.Yaw = MathX.AngleDeg(a.Pos.x - b.Pos.x, a.Pos.z - b.Pos.z);
            var (topic, third) = ChooseTopic(a, b);
            var rng = S.R(Stream.Life);
            var c = new Convo { A = a.Id, B = b.Id, Topic = topic, Third = third, Left = 2 + rng.R(3), Next = S.Tick + 5, Room = a.Room };
            _convos.Add(c);
            S.Log("Converse", a.Id, b.Id, room: a.Room, data: topic);
        }

        (string topic, string third) ChooseTopic(Actor a, Actor b)
        {
            var rng = S.R(Stream.Life); var r = S.R(a.Id, b.Id); var rb = S.R(b.Id, a.Id); var c = a.Def; var k = S.K(a.Id);
            var opts = new List<(string t, double w, string third)>();
            opts.Add(("small", 1.0, null));
            if (c.Likes.Length > 0) opts.Add(("like", 0.8 + (b.Def.Likes.Intersect(c.Likes).Any() ? 0.8 : 0), null));
            var gossip = Gossip(a, b); if (gossip != null) opts.Add(("gossip", 1.0 + c.P.Sociability, gossip));
            if (r.Like > 0.2f) opts.Add(("compliment", 0.3 + c.P.Sociability * 0.5, null));
            if (r.Like > 0.25f && c.P.Pride > 0.5f || c.Empathy < 60) opts.Add(("tease", 0.35, null));
            if (r.Grudge > 0.3f || r.Jealous > 0.35f || a.Needs.Anger > 0.5f) opts.Add(("argue", r.Grudge * 2 + r.Jealous + a.Needs.Anger + c.P.Aggression, null));
            if (r.Memory.Any(m => m.Contains("말다툼")) && r.Grudge < 0.4f) opts.Add(("apology", 0.6 + c.P.Honesty * 0.5, null));
            if (r.Trust > 0.5f && r.Like > 0.45f) opts.Add(("confide", 0.5, null));
            if (r.Romance > 0.35f) opts.Add(("flirt", r.Romance * 1.5 * c.P.Romance, null));
            if (!r.Casual && r.Like > 0.35f && rb.Like > 0.3f) opts.Add(("casual", 0.9, null));
            if (c.Hobbies.Length > 0 && r.Like > 0.1f) opts.Add(("invite", 0.5 + c.P.Sociability * 0.4, null));
            if (S.Ch.Rules.Any(x => x.Announced && S.Clock - S.Ch.ChapterStartClock < 600)) opts.Add(("rule", 0.5, null));
            var dead = S.Actors.Values.Where(x => !x.Alive && !x.IsButler && k.KnownDead.Contains(x.Id)).ToList();
            if (dead.Count > 0) opts.Add(("grief", 0.9 + a.Needs.Grief * 2, dead[rng.R(dead.Count)].Id));
            var sus = k.Suspicion.Where(kv => kv.Value > 0.4f && kv.Key != b.Id).OrderByDescending(kv => kv.Value).FirstOrDefault();
            if (sus.Key != null && r.Trust > 0.2f) opts.Add(("warn", sus.Value * 1.5, sus.Key));
            if (k.Facts.Contains("threat-plan:" + b.Id)) opts.Add(("threat", 3, null));
            Grammars.TopicOptions(this, a, b, opts);
            var pick = rng.Weighted(opts, o => o.w);
            return (pick.t, pick.third);
        }

        /// <summary>Find a notable third-party observation A could share with B (knowledge propagation, AT-023).</summary>
        string Gossip(Actor a, Actor b)
        {
            var k = S.K(a.Id);
            var s = k.Sightings.Where(x => x.Target != b.Id && x.Target != a.Id && S.Clock - x.T1 < 300 && (x.Bloody || x.Carrying || x.Running || (x.Held != null && ItemCatalog.Get(x.Held)?.IsWeapon == true) || x.Disguise != null) && x.IdConf > 0.45f)
                .OrderByDescending(x => x.T1).FirstOrDefault(x => !S.Flags.ContainsKey($"told:{a.Id}:{b.Id}:{x.Root}"));
            if (s == null) return null;
            S.Flags[$"told:{a.Id}:{b.Id}:{s.Root}"] = 1;
            return s.Root;
        }

        void Conversations()
        {
            for (int i = _convos.Count - 1; i >= 0; i--)
            {
                var c = _convos[i]; var a = S.A(c.A); var b = S.A(c.B);
                bool broken = a == null || b == null || !a.Alive || !b.Alive || a.Pos.Dist(b.Pos) > 3.5f || S.Phase == Phase.Trial || S.Phase == Phase.Assembly;
                if (!broken && S.Tick < c.Next) continue;
                if (broken || c.Left <= 0)
                {
                    if (!broken) ConvoOutcome(c, a, b);
                    if (a != null) { a.TalkingTo = null; if (a.Act != null && a.Act.Cur?.Kind == "Talk") NextStep(a); a.NextSocial = S.Clock + S.R(Stream.Life).Range(30, 90); a.Needs.Social = MathX.Clamp01(a.Needs.Social + 0.25f); }
                    if (b != null) { b.TalkingTo = null; b.NextThink = S.Clock; b.Needs.Social = MathX.Clamp01(b.Needs.Social + 0.2f); }
                    _convos.RemoveAt(i); continue;
                }
                var sp = c.AFirst ? a : b; var li = c.AFirst ? b : a;
                ConvoLine(c, sp, li, c.AFirst);
                c.AFirst = !c.AFirst; c.Left--; c.Next = S.Tick + 38 + S.R(Stream.Life).R(15);
                a.Anim = c.AFirst ? Anim.Listen : Anim.Talk; b.Anim = c.AFirst ? Anim.Talk : Anim.Listen;
            }
        }

        void ConvoLine(Convo c, Actor sp, Actor li, bool isOpener)
        {
            if (c.Topic.StartsWith("g_")) { Grammars.ConvoLine(this, c.Topic, c.Third, sp, li, isOpener); return; }
            if (LifeConvoLine(c.Topic, c.Third, sp, li, isOpener)) return;   // --- daily-life: banter keys (tease/joke/insult/gloat/scared/flirt…) + guardrails (Sim/Life/LifeBanter.cs)
            var slots = new Dictionary<string, string>();
            string key;
            switch (c.Topic)
            {
                case "like": key = isOpener ? "talk_like" : "small_talk"; slots["topic"] = sp.Def.Likes.Length > 0 ? sp.Def.Likes[S.R(Stream.Dialogue).R(sp.Def.Likes.Length)] : "이것저것"; break;
                case "gossip":
                    if (isOpener)
                    {
                        var s = S.K(sp.Id).Sightings.FirstOrDefault(x => x.Root == c.Third);
                        if (s != null)
                        {
                            key = s.Held != null ? "saw_item" : "gossip_saw";
                            slots["t"] = "@" + s.Target; slots["place"] = S.RoomName(s.Room); slots["time"] = ClockFmt.Vague(s.T0); if (s.Held != null) slots["item"] = ItemCatalog.Get(s.Held)?.Kor;
                            var p = new Prop { Kind = s.Held != null ? PropKind.Held : PropKind.AtPlace, A = s.Target, Room = s.Room, Item = s.Held, T0 = s.T0, T1 = s.T1, Value = "root:" + s.Root };
                            Speak(sp, key, li.Id, slots, p); var subj = S.A(s.Target); if (subj != null && subj.Alive && subj.Room == sp.Room && subj.Pose != Pose.Sleep && subj.Pos.Dist(sp.Pos) < 10) { Relations.Change(S, subj.Id, sp.Id, like: -0.08f, grudge: 0.12f, trust: -0.1f, memory: "내 얘기를 뒤에서 하는 걸 들었다"); subj.Needs.Anger = MathX.Clamp01(subj.Needs.Anger + 0.2f); } return;
                        }
                    }
                    key = "small_talk"; break;
                case "compliment": key = isOpener ? "ask_player" : "compliment_react"; break;
                case "tease": key = isOpener ? "small_talk" : "tease_react"; break;
                case "argue": key = isOpener ? "argue_open" : "argue_reply"; slots["t"] = "@" + li.Id; break;
                case "apology": key = isOpener ? "apology" : (S.R(li.Id, sp.Id).Grudge > 0.45f ? "not_forgive" : "forgive"); break;
                case "confide": key = isOpener ? "secret_hint" : "small_talk"; break;
                case "flirt": key = isOpener ? "love_hint" : "small_talk"; break;
                case "casual": key = isOpener ? "casual_offer" : (S.R(li.Id, sp.Id).Like > 0.3f ? "casual_yes" : "casual_no"); break;
                case "invite": key = isOpener ? "invite_ask" : (S.R(li.Id, sp.Id).Like > 0.15f ? "invite_yes" : "invite_no"); slots["act"] = Activities.Get(Activities.HobbyToActivity.TryGetValue(sp.Def.Hobbies.FirstOrDefault() ?? "", out var aid) ? aid : "walk")?.Kor ?? "산책"; slots["place"] = "저기"; break;
                case "rule": key = "react_rule"; break;
                case "grief": key = "grief"; slots["victim"] = "@" + c.Third; break;
                case "warn": key = isOpener ? "warn" : "small_talk"; slots["t"] = "@" + c.Third; break;
                case "threat": key = isOpener ? "warn" : "fear_general"; slots["t"] = "@" + sp.Id; break;
                default: key = "small_talk"; break;
            }
            Speak(sp, key, li.Id, slots);
        }

        void ConvoOutcome(Convo c, Actor a, Actor b)
        {
            var rng = S.R(Stream.Life); var ca = a.Def; var cb = b.Def; float compat = Relations.Compat(ca, cb);
            S.R(a.Id, b.Id).Talks++; S.R(b.Id, a.Id).Talks++; S.R(a.Id, b.Id).LastTalk = S.Clock; S.R(b.Id, a.Id).LastTalk = S.Clock;
            if (c.Topic.StartsWith("g_")) { Grammars.ConvoOutcome(this, c.Topic, c.Third, a, b); return; }
            var witnesses = S.Living.Where(x => x != a && x != b && x.Room == a.Room && x.Pose != Pose.Sleep).ToList();
            switch (c.Topic)
            {
                case "small": Relations.Change(S, a.Id, b.Id, like: 0.02f + compat * 0.02f); Relations.Change(S, b.Id, a.Id, like: 0.02f + compat * 0.02f); break;
                case "like":
                    bool shared = cb.Likes.Intersect(ca.Likes).Any();
                    Relations.Change(S, b.Id, a.Id, like: shared ? 0.07f : 0.02f, memory: shared ? "취향이 같다는 걸 알았다" : null);
                    Relations.Change(S, a.Id, b.Id, like: shared ? 0.06f : 0.02f); break;
                case "gossip": Relations.Change(S, b.Id, a.Id, trust: 0.02f); break;
                case "compliment": Relations.Change(S, b.Id, a.Id, like: 0.05f * (cb.P.Pride > 0.6f ? 1.4f : 1f), memory: "칭찬을 들었다"); break;
                case "tease": { float ok = cb.P.Pride > 0.7f ? -0.05f : 0.03f; Relations.Change(S, b.Id, a.Id, like: ok, grudge: ok < 0 ? 0.03f : 0, memory: ok < 0 ? "놀림받아 기분이 상했다" : "장난을 주고받았다"); break; }
                case "argue":
                    {
                        float heat = 0.05f + a.Def.P.Aggression * 0.06f;
                        Relations.Change(S, a.Id, b.Id, like: -heat, grudge: heat * 0.8f, memory: "말다툼을 했다");
                        Relations.Change(S, b.Id, a.Id, like: -heat, grudge: heat * (witnesses.Count > 0 ? 1.4f : 1f), respect: -0.03f, memory: witnesses.Count > 0 ? "사람들 앞에서 말다툼했다" : "말다툼을 했다");
                        a.Needs.Anger = MathX.Clamp01(a.Needs.Anger + 0.3f); b.Needs.Anger = MathX.Clamp01(b.Needs.Anger + 0.3f); a.Needs.Stress += 0.08f; b.Needs.Stress += 0.1f;
                        foreach (var w in witnesses) { S.K(w.Id).Facts.Add($"argued:{a.Id}:{b.Id}"); Relations.Change(S, w.Id, a.Id, respect: -0.02f); }
                        if (rng.Chance(0.4)) Speak(b, "argue_stormoff", a.Id, new Dictionary<string, string> { { "t", "@" + a.Id } });
                        break;
                    }
                case "apology":
                    {
                        var rb = S.R(b.Id, a.Id);
                        if (rb.Grudge <= 0.45f) { Relations.Change(S, b.Id, a.Id, grudge: -0.15f, like: 0.05f, memory: "사과를 받아들였다"); Relations.Change(S, a.Id, b.Id, grudge: -0.1f); }
                        else Relations.Change(S, b.Id, a.Id, grudge: -0.03f, memory: "사과를 받았지만 용서하지 않았다");
                        break;
                    }
                case "confide":
                    Relations.Change(S, b.Id, a.Id, trust: 0.08f, attach: 0.05f, memory: "속마음을 조금 털어놓았다"); Relations.Change(S, a.Id, b.Id, trust: 0.04f, attach: 0.04f);
                    S.K(b.Id).Facts.Add("confided:" + a.Id); break;
                case "flirt":
                    {
                        var rb = S.R(b.Id, a.Id);
                        if (rb.Romance > 0.3f && rb.Like > 0.35f) { Relations.Change(S, a.Id, b.Id, romance: 0.1f, like: 0.05f, tag: "lover", memory: "마음이 통했다"); Relations.Change(S, b.Id, a.Id, romance: 0.1f, like: 0.05f, tag: "lover", memory: "마음이 통했다"); OnCouple(a, b); }
                        else if (rb.Romance < 0.1f && S.R(a.Id, b.Id).Romance > 0.55f) { Relations.Change(S, a.Id, b.Id, like: -0.03f, tag: "rejected", memory: "마음을 전했다가 거절당했다"); a.Needs.Stress += 0.12f; Relations.Change(S, b.Id, a.Id, fear: 0.02f); }
                        else { Relations.Change(S, b.Id, a.Id, romance: 0.05f * b.Def.P.Romance, like: 0.02f); }
                        break;
                    }
                case "casual":
                    if (S.R(b.Id, a.Id).Like > 0.3f) { S.R(a.Id, b.Id).Casual = true; S.R(b.Id, a.Id).Casual = true; Relations.Change(S, a.Id, b.Id, like: 0.03f, memory: "말을 편하게 하기로 했다"); Relations.Change(S, b.Id, a.Id, like: 0.03f, memory: "말을 편하게 하기로 했다"); }
                    break;
                case "invite":
                    if (S.R(b.Id, a.Id).Like > 0.15f && S.Phase == Phase.Daily)
                    {
                        var hob = ca.Hobbies.Select(h => Activities.HobbyToActivity.TryGetValue(h, out var x) ? x : null).FirstOrDefault(x => x != null && x != "cook");
                        var act = hob != null ? Simple(a, hob) : null;
                        if (act != null)
                        {
                            Assign(a, act);
                            var room = act.Steps.Count > 0 && act.Steps[0].HasTarget ? S.Layout.RoomAt(act.Steps[0].Target) : -1;
                            var act2 = hob != null && room >= 0 ? Simple(b, hob, room) : null;
                            if (act2 != null) { act2.Id = "social:join:" + a.Id; Assign(b, act2); }
                            Relations.Change(S, a.Id, b.Id, like: 0.05f, attach: 0.03f, memory: Activities.Get(hob).Kor + ", 같이 해서 즐거웠다", tag: "colleague");
                            Relations.Change(S, b.Id, a.Id, like: 0.05f, attach: 0.03f, memory: Activities.Get(hob).Kor + ", 같이 해서 즐거웠다", tag: "colleague");
                        }
                    }
                    break;
                case "grief": Relations.Change(S, a.Id, b.Id, attach: 0.04f, trust: 0.02f); Relations.Change(S, b.Id, a.Id, attach: 0.04f, trust: 0.02f); a.Needs.Grief *= 0.85f; b.Needs.Grief *= 0.85f; break;
                case "warn":
                    if (c.Third != null) { var kb = S.K(b.Id); kb.Suspicion[c.Third] = (kb.Suspicion.TryGetValue(c.Third, out var v) ? v : 0) + 0.15f * S.R(b.Id, a.Id).Trust; Relations.Change(S, b.Id, c.Third, fear: 0.05f, trust: -0.05f); }
                    break;
                case "threat": Relations.Change(S, b.Id, a.Id, fear: 0.2f, like: -0.1f, memory: "위협처럼 들리는 말을 들었다"); break;
            }
            // time together builds familiarity for sociable people
            if (ca.P.Sociability > 0.6f) Relations.Change(S, a.Id, b.Id, like: 0.01f);
        }

        void OnCouple(Actor a, Actor b)
        {
            // jealousy for those who carried a torch for either
            foreach (var x in S.Living)
            {
                if (x == a || x == b) continue;
                if (S.R(x.Id, a.Id).Romance > 0.4f && S.K(x.Id).Sightings.Any(s => s.Target == a.Id && S.Clock - s.T1 < 5)) Relations.Change(S, x.Id, b.Id, jealous: 0.25f, memory: "두 사람이 가까워지는 걸 봤다");
                if (S.R(x.Id, b.Id).Romance > 0.4f && S.K(x.Id).Sightings.Any(s => s.Target == b.Id && S.Clock - s.T1 < 5)) Relations.Change(S, x.Id, a.Id, jealous: 0.25f, memory: "두 사람이 가까워지는 걸 봤다");
            }
        }
    }
}
