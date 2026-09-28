using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    public sealed class TogetherOption { public string Id; public string Label; public int Minutes; public bool Enabled; public string Why; }
    public sealed class TogetherPlan { public string Npc, Id, Activity, Label, RequestId; public int Minutes; public int Furniture = -1; }

    /// <summary>
    /// Spending time with someone (time-on-demand §5.1): a long talk, a cup of tea, a meal at the same table, or joining what
    /// they are doing. It is how the clock moves in company and how people grow closer. Refusals come only from ordinary
    /// causes (tired, late, not close, upset, just spent time together) — the same for everyone, whatever they are hiding.
    /// </summary>
    public sealed partial class Simulation
    {
        /// <summary>The kinds of time together that are possible with this person here and now (disabled ones say why).</summary>
        public List<TogetherOption> TogetherOptions(Actor npc)
        {
            var list = new List<TogetherOption>();
            if (!TogetherEligible(npc, out string why))
            {
                foreach (var a in TogetherActs.All) list.Add(new TogetherOption { Id = a.Id, Label = a.Label, Minutes = a.Minutes, Enabled = false, Why = why });
                return list;
            }
            var me = P; var room = S.Layout.Room(me.Room);
            list.Add(new TogetherOption { Id = "talk_long", Label = "천천히 이야기를 나눈다", Minutes = 30, Enabled = true });
            bool teaHere = room != null && (TogetherActs.TeaRooms.Contains(room.Type) || room.Furniture.Any(fid => S.Layout.Furniture[fid].Type == "TeaCart"));
            list.Add(new TogetherOption { Id = "tea", Label = "차 한잔", Minutes = 30, Enabled = teaHere, Why = teaHere ? null : "차를 마실 만한 곳이 아니다" });
            bool meal = room != null && room.Type == RoomType.Dining && MealTime(out int slot) && (MealStart[slot] + MealLen - S.Minute) >= 5;
            int mealMin = meal && MealTime(out int sl2) ? Math.Max(10, Math.Min(45, MealStart[sl2] + MealLen - S.Minute)) : 30;
            list.Add(new TogetherOption { Id = "meal", Label = "함께 식사하기", Minutes = mealMin, Enabled = meal, Why = meal ? null : "식사 시간에 식당에서만 할 수 있다" });
            string aid = JoinableActivity(npc, out string reqId);
            if (aid != null)
            {
                string kor = Activities.Get(aid)?.Kor ?? aid;
                string label = reqId != null ? $"약속한 {kor}을(를) 함께 한다" : $"{Cast.GivenOf(npc.Id)}의 {kor}을(를) 함께 한다";
                list.Add(new TogetherOption { Id = "join", Label = LineBank.FixParticles(label), Minutes = 30, Enabled = true });
            }
            else list.Add(new TogetherOption { Id = "join", Label = "하는 일을 함께 한다", Minutes = 30, Enabled = false, Why = "지금은 함께할 만한 일을 하고 있지 않다" });
            return list;
        }

        bool TogetherEligible(Actor npc, out string why)
        {
            why = null;
            if (S.Phase != Phase.Daily) { why = "지금은 그럴 때가 아니다"; return false; }
            if (npc == null || npc.IsPlayer || npc.IsButler || !npc.Alive || npc.Status != ActorStatus.Active) { why = "함께할 수 없다"; return false; }
            if (npc.Pose == Pose.Sleep) { why = "자고 있다"; return false; }
            if (npc.TalkingTo != null && npc.TalkingTo != Cast.Player) { why = "다른 사람과 이야기 중이다"; return false; }
            if (npc.Act != null && !npc.Act.Interruptible) { why = "지금은 바빠 보인다"; return false; }
            var me = P; if (me == null || !me.Alive || me.Status != ActorStatus.Active) { why = "지금은 그럴 수 없다"; return false; }
            return true;
        }

        // what someone was doing when 민혁 started talking to them (the conversation puts it down); not saved
        readonly Dictionary<string, (string act, int spot, double clock)> _talkPrev = new Dictionary<string, (string, int, double)>();
        internal void NoteTalkPrev(Actor npc)
        {
            if (npc == null) return;
            if (npc.Act != null && npc.Act.Id != null) _talkPrev[npc.Id] = (npc.Act.Id, npc.Spot >= 0 && npc.Pose != Pose.Stand ? npc.Spot : -1, S.Clock);
            else _talkPrev.Remove(npc.Id);
        }
        /// <summary>Their current activity, or (standing idle right after a conversation in a still world) the one they put down for it.</summary>
        string CurrentOrPrevAct(Actor npc, out int prevSpot)
        {
            prevSpot = -1;
            if (npc?.Act != null) return npc.Act.Id;
            if (npc != null && _talkPrev.TryGetValue(npc.Id, out var pv) && S.Clock - pv.clock < 10) { prevSpot = pv.spot; return pv.act; }
            return null;
        }

        /// <summary>The everyday activity this person is doing that 민혁 could join (null if none); an appointment host's is marked.</summary>
        string JoinableActivity(Actor npc, out string requestId)
        {
            requestId = null;
            var id = CurrentOrPrevAct(npc, out _); if (id == null || !id.StartsWith("life:")) return null;
            var parts = id.Split(':'); if (parts.Length < 2) return null;
            string aid = parts[1];
            if (Activities.Get(aid) == null || aid == "sleep") return null;
            // the appointment they made with 민혁 can always be joined (a walk, a meal, whatever was promised)
            if (parts.Length > 2 && parts[2] == "req")
            {
                var r = (S.Requests ?? new List<Request>()).FirstOrDefault(x => x.From == npc.Id && x.Kind == "invite" && x.Activity == aid && (x.State == "accepted" || x.State == "met"));
                if (r != null) { requestId = r.Id; return aid; }
            }
            if (TogetherActs.NotJoinable.Contains(aid)) return null;
            return aid;
        }

        /// <summary>Why they would say no right now (a line key), or null for yes. Only ordinary causes — never a plan.</summary>
        string TogetherRefusal(Actor npc, string kind)
        {
            var r = S.R(npc.Id, Cast.Player);
            if (npc.Needs.Energy < 0.2f) return "sleepy";
            int m = S.Minute; if ((m >= 23 * 60 || m < 6 * 60) && kind != "talk_long") return "sleepy";
            if (r.Like + r.Trust <= 0.05f || r.Grudge > 0.35f || r.Fear > 0.45f || npc.Needs.Stress > 0.75f) return "invite_no";
            if (S.Flags.TryGetValue("togat:" + npc.Id, out var at) && S.Clock - at < 60) return "talked_recently";
            return null;
        }

        string TogetherWord(Actor npc, string kind)
        {
            switch (kind)
            {
                case "talk_long": return "이야기";
                case "tea": return "차 한잔";
                case "meal": return "식사";
                case "join": { var aid = JoinableActivity(npc, out _); return Activities.Get(aid)?.Kor ?? "그 일"; }
            }
            return "이야기";
        }

        /// <summary>Dialogue: "함께 시간을 보낸다" → kind. Adds 민혁's ask and their answer; on yes, PendingTogether is set and the
        /// answer is keyed "bye" so the conversation closes and the scene can begin.</summary>
        List<Utterance> ChooseTogether(Actor npc, string kind)
        {
            var res = new List<Utterance>(); var me = Cast.Player;
            var o = TogetherOptions(npc).FirstOrDefault(x => x.Id == kind);
            if (o == null || !o.Enabled) { res.Add(U(npc.Id, me, "small_talk")); return res; }
            string word = TogetherWord(npc, kind);
            var slots = new Dictionary<string, string> { { "act", word } };
            res.Add(U(me, npc.Id, "p_invite", slots));
            string refuse = TogetherRefusal(npc, kind);
            if (refuse != null) { res.Add(U(npc.Id, me, refuse, slots, Emotion.Neutral)); S.Log("TogetherRefused", npc.Id, me, data: kind + ":" + refuse); return res; }
            var yes = U(npc.Id, me, "invite_yes", slots, Emotion.Smile);
            yes.Key = "bye";   // the conversation closes on this line; the scene follows
            res.Add(yes);
            string reqId = null; string aid = kind == "join" ? JoinableActivity(npc, out reqId) : null;
            var act = TogetherActs.Get(kind);
            PendingTogether = new TogetherPlan { Npc = npc.Id, Id = kind, Activity = aid ?? act?.Activity, Label = o.Label, RequestId = reqId, Minutes = o.Minutes };
            S.Log("TogetherAgreed", npc.Id, me, data: kind);
            return res;
        }

        /// <summary>Per person per day, time together counts less each time (1, .5, .25, .1). count: this one counts.</summary>
        internal float TogetherDiminish(string npc, bool count)
        {
            string key = "tog:" + npc; int n = 0;
            if (S.Flags.TryGetValue(key, out var v) && (int)v / 100 == S.Day) n = (int)v % 100;
            float mul = n == 0 ? 1f : n == 1 ? 0.5f : n == 2 ? 0.25f : 0.1f;
            if (count) S.Flags[key] = S.Day * 100 + Math.Min(99, n + 1);
            return mul;
        }

        /// <summary>Plans the scene: who sits where (both sit if there are seats near each other), the minutes (a conversation that
        /// just ended is folded in), and 2–3 lines said along the way.</summary>
        public SkipPlan PlanTogether(TogetherPlan tp, out string why)
        {
            why = null;
            if (tp == null) { why = "함께할 사람이 없다"; return null; }
            var npc = S.A(tp.Npc);
            if (!TogetherEligible(npc, out why)) return null;
            if (!CanPassTime(out why)) return null;
            var act = TogetherActs.Get(tp.Id); if (act == null) { why = "함께할 수 없다"; return null; }
            double minutes = Math.Max(5, tp.Minutes) + Math.Max(0, PendingTalk); PendingTalk = 0;
            var p = NewPlan(SkipKind.Together, S.Clock + minutes, tp.Label);
            p.Partner = npc.Id; p.TogetherId = tp.Id; p.Activity = tp.Activity ?? act.Activity; p.RequestId = tp.RequestId; p.Together = tp;
            ChooseTogetherSpots(p, npc);
            p.Lines = TogetherLines(npc, tp);
            return p;
        }

        bool FreeFor(Spot sp, Actor npc) => sp.Occupant == null || sp.Occupant == Cast.Player || npc != null && sp.Occupant == npc.Id;

        void ChooseTogetherSpots(SkipPlan p, Actor npc)
        {
            var me = P; int room = npc.Room; var r = S.Layout.Room(room); if (r == null) return;
            var seats = r.Spots.Select(i => S.Layout.Spots[i]).Where(sp => sp.OnFurniture && SitTags.Contains(sp.Tag) && sp.Tag != "rest" && FreeFor(sp, npc)).OrderBy(sp => sp.Id).ToList();
            if (p.TogetherId == "meal")
            {
                var din = S.Layout.First(RoomType.Dining);
                seats = din == null ? new List<Spot>() : din.Spots.Select(i => S.Layout.Spots[i]).Where(sp => sp.Tag == "sit" && FreeFor(sp, npc)).OrderBy(sp => sp.Id).ToList();
            }
            bool npcSeated = npc.Spot >= 0 && npc.Spot < S.Layout.Spots.Count && S.Layout.Spots[npc.Spot].Occupant == npc.Id && npc.Pose == Pose.Sit;
            if (p.TogetherId == "join")
            {
                // they go back to where they were doing it (the conversation made them stand up), 민혁 takes a seat close by
                CurrentOrPrevAct(npc, out int prevSpot);
                if (npc.Spot >= 0 && S.Layout.Spots[npc.Spot].Occupant == npc.Id) p.PartnerSpot = npc.Spot;
                else if (prevSpot >= 0 && prevSpot < S.Layout.Spots.Count && FreeFor(S.Layout.Spots[prevSpot], npc) && S.Layout.Spots[prevSpot].Occupant != Cast.Player) p.PartnerSpot = prevSpot;
                var at = p.PartnerSpot >= 0 ? S.Layout.Spots[p.PartnerSpot].Pos : npc.Pos;
                var near = seats.Where(sp => sp.Id != p.PartnerSpot && sp.Pos.f == at.f && sp.Pos.DistXZ(at) < 3f).OrderBy(sp => sp.Pos.DistXZ(at)).ThenBy(sp => sp.Id).FirstOrDefault();
                if (near != null) p.Spot = near.Id;
                return;
            }
            if (npcSeated && (p.TogetherId != "meal" || S.Layout.Spots[npc.Spot].Room == S.Layout.First(RoomType.Dining)?.Id))
            {
                p.PartnerSpot = npc.Spot; var ps = S.Layout.Spots[npc.Spot];
                var near = seats.Where(sp => sp.Id != ps.Id && sp.Pos.f == ps.Pos.f && sp.Pos.DistXZ(ps.Pos) < 3f).OrderBy(sp => sp.Pos.DistXZ(ps.Pos)).ThenBy(sp => sp.Id).FirstOrDefault();
                if (near != null) p.Spot = near.Id;
                return;
            }
            // a pair of free seats near each other, the nearest to them
            foreach (var a in seats.Where(sp => sp.Pos.f == npc.Pos.f && sp.Pos.DistXZ(npc.Pos) < (p.TogetherId == "meal" ? 40f : 8f)).OrderBy(sp => sp.Pos.DistXZ(npc.Pos)).ThenBy(sp => sp.Id))
            {
                if (a.Occupant == Cast.Player) continue;
                var b = seats.Where(sp => sp.Id != a.Id && sp.Pos.f == a.Pos.f && sp.Pos.DistXZ(a.Pos) < 3f && (sp.Occupant == null || sp.Occupant == Cast.Player)).OrderBy(sp => sp.Occupant == Cast.Player ? 0 : 1).ThenBy(sp => sp.Pos.DistXZ(me.Pos)).ThenBy(sp => sp.Id).FirstOrDefault();
                if (b == null) continue;
                p.PartnerSpot = a.Id; p.Spot = b.Id; return;
            }
        }

        List<Utterance> TogetherLines(Actor npc, TogetherPlan tp)
        {
            var rng = S.R(Stream.Dialogue); var c = npc.Def; var r = S.R(npc.Id, Cast.Player); var me = Cast.Player;
            var pool = new List<(string key, Dictionary<string, string> slots)>();
            pool.Add(("small_talk", null));
            pool.Add(("talk_wish", null));
            if (c.Likes.Length > 0) pool.Add(("talk_like", new Dictionary<string, string> { { "topic", c.Likes[rng.R(c.Likes.Length)] } }));
            if (tp.Id == "meal") pool.Add(("meal", null));
            if (tp.Id == "join") pool.Add(("doing_act", new Dictionary<string, string> { { "act", Activities.Get(tp.Activity)?.Kor ?? "이것" } }));
            if (r.Trust > 0.35f) pool.Add(("secret_hint", null));
            rng.Shuffle(pool);
            int n = Math.Min(pool.Count, 2 + rng.R(2));
            var lines = new List<Utterance>();
            for (int i = 0; i < n; i++)
            {
                var (key, slots) = pool[i];
                if (key == "talk_like") lines.Add(U(me, npc.Id, "p_ask_like"));
                lines.Add(U(npc.Id, me, key, slots, key == "secret_hint" ? Emotion.Sad : Emotion.Smile, key == "secret_hint" ? Anim.Think : Anim.Talk));
            }
            return lines;
        }

        void PlayerSitSpot(Spot sp)
        {
            var me = P; if (sp == null || me == null) return;
            if (me.Spot >= 0 && me.Spot != sp.Id) ReleaseSpot(me);
            sp.Occupant = Cast.Player; me.Spot = sp.Id; me.Pos = sp.Pos; me.Yaw = sp.Yaw; if (sp.Room >= 0) me.Room = sp.Room;
            me.Pose = Pose.Sit; me.Anim = Anim.Idle; me.Running = false;
        }

        void BeginTogether(SkipPlan p)
        {
            var npc = S.A(p.Partner); var me = P; if (npc == null) return;
            var ta = TogetherActs.Get(p.TogetherId);
            var def = Activities.Get(p.Activity);
            string tag = def != null ? def.Id : "socialize";
            Anim anim = p.TogetherId == "join" ? (def?.Anim ?? Anim.Use) : (ta?.Anim ?? Anim.Talk);
            // the partner: to their seat (if not there already), then the thing itself — for as long as the scene lasts
            var act = new Activity { Id = "social:together:" + Cast.Player, Label = "민혁과 " + (def?.Kor ?? "이야기"), Priority = 5, Interruptible = false };
            npc.TalkingTo = null;
            bool atSpot = p.PartnerSpot >= 0 && npc.Spot == p.PartnerSpot;
            if (p.PartnerSpot >= 0 && !atSpot) act.Steps.Add(GoTo(S.Layout.Spots[p.PartnerSpot].Approach));
            else if (p.PartnerSpot < 0) act.Steps.Add(new ActionStep { Kind = "Face", Actor = Cast.Player });
            act.Steps.Add(Do(tag, (p.Target - S.Clock) + 60, anim, p.PartnerSpot));
            Assign(npc, act);
            // 민혁: the seat next to them, or a step closer
            if (p.Spot >= 0) { PlayerSitSpot(S.Layout.Spots[p.Spot]); p.SeatedByKernel = true; }
            else if (me.Pos.f == npc.Pos.f && me.Pos.DistXZ(npc.Pos) > 2.5f)
            {
                float dx = me.Pos.x - npc.Pos.x, dz = me.Pos.z - npc.Pos.z; float dl = Math.Max(0.01f, (float)Math.Sqrt(dx * dx + dz * dz));
                var q = Snap(new P3(npc.Pos.f, npc.Pos.x + dx / dl * 1.3f, npc.Pos.z + dz / dl * 1.3f));
                if (me.Spot >= 0) PlayerStand();
                me.Pos = q; int rr = S.Layout.RoomAt(q); if (rr >= 0 && rr != me.Room) { int old = me.Room; me.Room = rr; OnEnterRoom(me, old, rr); }
            }
            if (p.Spot < 0) me.Yaw = MathX.AngleDeg(npc.Pos.x - me.Pos.x, npc.Pos.z - me.Pos.z);
            me.Anim = p.TogetherId == "tea" ? Anim.Drink : p.TogetherId == "meal" ? Anim.Eat : p.TogetherId == "join" ? anim : Anim.Listen;
            // the lines are said (and heard) as the scene goes; the ledger keeps them as speech
            foreach (var u in p.Lines) { var sp = S.A(u.Speaker); S.Log("Speech", u.Speaker, u.Listener, room: sp?.Room ?? me.Room, data: u.Key + "|" + u.Text); }
            S.Log("Together", Cast.Player, npc.Id, room: npc.Room, data: p.TogetherId + ":" + p.Activity);
        }

        void EndTogether(SkipPlan p, SkipResult res, float frac)
        {
            var npc = S.A(p.Partner); var me = P; res.Partner = p.Partner;
            if (me != null && me.Anim != Anim.Idle && me.Pose != Pose.Sleep) me.Anim = Anim.Idle;
            if (npc == null) return;
            if (npc.Act != null && npc.Act.Id == "social:together:" + Cast.Player) { Interrupt(npc, 0.3); npc.NextSocial = S.Clock + 40; }
            var ta = TogetherActs.Get(p.TogetherId) ?? TogetherActs.All[0];
            double planned = p.Together != null ? Math.Max(5, p.Together.Minutes) : 30;
            string aid = p.Activity;
            bool hobby = aid != null && npc.Def.Hobbies.Any(h => Activities.HobbyToActivity.TryGetValue(h, out var x) && x == aid);
            float mul = (hobby ? 1.5f : 1f) * MathX.Clamp((float)Math.Sqrt(planned / 30.0), 0.6f, 1.4f) * TogetherDiminish(npc.Id, frac >= 0.5f) * MathX.Clamp01(frac);
            float like = ta.Like * mul, attach = ta.Attach * mul, trust = ta.Trust * mul, respect = (p.TogetherId == "join" && aid != null && TogetherActs.WorkLike.Contains(aid) ? 0.02f : ta.Respect) * mul;
            string kor = p.TogetherId == "talk_long" ? "긴 이야기" : p.TogetherId == "tea" ? "차 한잔" : p.TogetherId == "meal" ? "식사" : Activities.Get(aid)?.Kor ?? "시간";
            if (frac > 0.05f)
            {
                Relations.Change(S, npc.Id, Cast.Player, like: like, trust: trust, respect: respect, attach: attach, memory: frac >= 0.5f ? LineBank.FixParticles($"민혁과 함께 {kor}을(를) 했다") : null);
                Relations.Change(S, Cast.Player, npc.Id, like: like * 0.6f, attach: attach * 0.5f);
                npc.Needs.Social = MathX.Clamp01(npc.Needs.Social + 0.2f * frac); if (me != null) me.Needs.Social = MathX.Clamp01(me.Needs.Social + 0.2f * frac);
            }
            if (p.TogetherId == "tea") { npc.Needs.Stress = MathX.Clamp01(npc.Needs.Stress - 0.05f * frac); if (me != null) me.Needs.Stress = MathX.Clamp01(me.Needs.Stress - 0.05f * frac); }
            if (p.TogetherId == "meal" && me != null && frac >= 0.5f)
            {
                me.Needs.Hunger = 0.05f; me.Needs.LastMeal = S.Clock;
                if (MealTime(out int slot)) { S.Flags[$"ate:{Cast.Player}:{S.Day}:{slot}"] = 1; S.Flags[$"ate:{npc.Id}:{S.Day}:{slot}"] = 1; }
                foreach (var x in S.LivingNpcs.Where(x => x != npc && x.Room == me.Room && x.Pos.DistXZ(me.Pos) < 2.6f && (x.Pose == Pose.Sit || x.Anim == Anim.Eat)).OrderBy(x => x.Id))
                    Relations.Change(S, x.Id, Cast.Player, like: 0.02f * TogetherDiminish(x.Id, false));
            }
            if (frac >= 0.5f) { string bondKey = "bond:" + npc.Id; int b0 = S.Flags.TryGetValue(bondKey, out var bv) ? (int)bv : 0; S.Flags[bondKey] = b0 + 1; }
            S.Flags["togat:" + npc.Id] = S.Clock;
            res.BondReady = BondAvailable(npc)?.Id;
            var words = new List<string>();
            if (like > 0.008f) words.Add($"{Cast.GivenOf(npc.Id)}의 호감이 올랐다");
            if (trust > 0.008f) words.Add("조금 더 믿게 되었다");
            if (attach > 0.008f) words.Add("가까워졌다");
            string head = LineBank.FixParticles(frac >= 0.5f ? $"{Cast.GivenOf(npc.Id)}와(과) {kor}을(를) 함께했다" : $"{Cast.GivenOf(npc.Id)}와(과) {kor}을(를) 하다가 말았다");
            res.Activity = new PlayerActivityResult { Text = head + (words.Count > 0 ? " — " + string.Join(" · ", words) : ""), Joined = npc.Id, Minutes = res.Minutes, Interrupted = frac < 0.8f };
            S.Log("TogetherEnd", Cast.Player, npc.Id, room: npc.Room, data: $"{p.TogetherId}:{frac:0.00}:{like:0.000}");
        }

        /// <summary>Tests: ask for this kind of time together and, on yes, spend it (plan + run). On a refusal the result carries the line.</summary>
        public SkipResult PlayerTogether(Actor npc, string id)
        {
            var lines = ChooseTogether(npc, id);
            var tp = PendingTogether; PendingTogether = null;
            if (tp == null) return new SkipResult { Interrupted = true, Title = lines.LastOrDefault()?.Text, Lines = lines.Select(l => l.Key + "|" + l.Text).ToList() };
            var plan = PlanTogether(tp, out var why);
            if (plan == null) return new SkipResult { Interrupted = true, Title = why };
            var res = RunSkip(plan);
            res.Lines.InsertRange(0, lines.Select(l => l.Key + "|" + l.Text));
            return res;
        }
    }
}
