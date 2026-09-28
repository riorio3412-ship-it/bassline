using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    public sealed class DialogueOption { public string Id; public string Label; public bool Enabled = true; public string Why; public string Arg; public List<(string id, string label)> Sub; public bool Investigation; public bool Done; }

    /// <summary>Everything the player can do. Each call is one validated command against the world; results come back as utterances/events.</summary>
    public sealed partial class Simulation
    {
        public Actor P => S.Player;
        public bool PlayerCasualTo(string npc) => S.R(Cast.Player, npc).Casual;

        // ------------------------------------------------------------------ conversation
        public bool CanTalk(Actor npc, out string why)
        {
            why = null;
            if (npc == null || !npc.Alive) { why = "대답이 없다"; return false; }
            if (npc.Status == ActorStatus.Unconscious) { why = "의식이 없다"; return false; }
            if (npc.Pose == Pose.Sleep) { why = "자고 있다"; return false; }
            if (npc.Act != null && !npc.Act.Interruptible) { why = "지금은 말 붙일 틈이 없어 보인다"; return false; }
            return true;
        }

        public void BeginTalk(Actor npc)
        {
            NoteTalkPrev(npc);   // --- time-on-demand: what they were doing (and where), so "함께 한다" can offer to join it
            if (npc.Act != null && npc.Act.Interruptible && !npc.IsButler) EndActivity(npc, false);
            npc.TalkingTo = Cast.Player; npc.Speed = 0;
            npc.Yaw = MathX.AngleDeg(P.Pos.x - npc.Pos.x, P.Pos.z - npc.Pos.z);
            S.Flags.Remove("approach:" + npc.Id);
            S.R(Cast.Player, npc.Id).Talks++; S.R(npc.Id, Cast.Player).Talks++;
            S.Log("Converse", Cast.Player, npc.Id, room: P.Room);
        }

        public void EndTalk(Actor npc)
        {
            if (npc == null) return; npc.TalkingTo = null; npc.NextThink = S.Clock + 0.3; S.R(npc.Id, Cast.Player).LastTalk = S.Clock; S.R(Cast.Player, npc.Id).LastTalk = S.Clock;
            npc.Needs.Social = MathX.Clamp01(npc.Needs.Social + 0.15f);
        }

        public List<DialogueOption> Options(Actor npc)
        {
            var o = new List<DialogueOption>(); var r = S.R(npc.Id, Cast.Player); var rp = S.R(Cast.Player, npc.Id);
            if (npc.IsButler)
            {
                o.Add(new DialogueOption { Id = "b_rules", Label = "규칙을 다시 묻는다" });
                o.Add(new DialogueOption { Id = "b_idle", Label = "가볍게 말을 건다" });
                o.Add(new DialogueOption { Id = "b_hint", Label = "범인이 누군지 묻는다" });
    
                o.Add(new DialogueOption { Id = "bye", Label = "그만 간다" });
                return o;
            }
            // they came to ask something: answer it first
            var req = OfferFrom(npc);
            if (req != null)
            {
                // an invitation that lands on top of one already made says so before you agree (the offer itself is unchanged)
                var clash = req.Kind == "invite" ? InviteClash(req) : null;
                string clashNote = clash != null ? $" — {Cast.GivenOf(clash.From)}{LineBank.Josa(Cast.GivenOf(clash.From), "와")}의 {ClockFmt.Mark(clash.At, false)} 약속과 겹친다" : "";
                o.Add(new DialogueOption { Id = "req_accept", Label = (req.Kind == "invite" ? "그때 가겠다고 한다" : req.Kind == "find" ? "찾아보겠다고 한다" : "쪽지를 받아 둔다") + clashNote });
                if (req.Kind == "invite") o.Add(new DialogueOption { Id = "req_later", Label = "한 시간 뒤라면 괜찮다고 한다" });
                o.Add(new DialogueOption { Id = "req_refuse", Label = "이번에는 어렵다고 한다" });
            }
            // a personal story in progress: its choices are the only thing to say right now
            var pend = BondPending(npc);
            if (pend != null) { for (int i = 0; i < pend.Choices.Count; i++) o.Add(new DialogueOption { Id = "bondpick", Arg = i.ToString(), Label = pend.Choices[i].Label }); return o; }
            if (LifeOptions(npc, o)) return o;   // --- daily-life: a scene's choices / what they came to say / a heart event / rumours (Sim/Life/LifeDialogue.cs)
            var bs = BondAvailable(npc);
            if (bs != null) o.Add(new DialogueOption { Id = "bond", Label = $"「{bs.Title}」 — 천천히 이야기를 나눈다" });
            bool inv = S.Phase == Phase.Investigation || S.Phase == Phase.Assembly;
            if (inv)
            {
                // one question covers where they were, what they saw and what they heard (q_where/q_saw/q_heard stay for tools)
                o.Add(new DialogueOption { Id = "q_case", Label = "그때 어디서 뭘 했는지 묻는다", Investigation = true, Done = CaseBoard.Asked(this, npc.Id) });
                o.Add(new DialogueOption { Id = "q_suspect", Label = "누가 의심스러운지 묻는다", Investigation = true });
                o.Add(new DialogueOption { Id = "q_share", Label = "찾은 게 있는지 묻는다", Investigation = true });
                var evs = CaseBoard.Cards(this).Where(v => v.Key && v.InCase).Select(v => (v.Id, v.Title)).ToList();
                o.Add(new DialogueOption { Id = "show", Label = "단서를 보여 준다", Investigation = true, Enabled = evs.Count > 0, Why = "보여 줄 만한 단서가 없다", Sub = evs });
            }
            else
            {
                o.Add(new DialogueOption { Id = "chat", Label = "이야기를 나눈다" });
                o.Add(new DialogueOption { Id = "likes", Label = "좋아하는 걸 묻는다" });
            }
            if (!inv)
            {
                o.Add(new DialogueOption { Id = "gossip", Label = "요즘 뭘 봤는지 묻는다" });
                o.Add(new DialogueOption { Id = "compliment", Label = "칭찬한다" });
                o.Add(new DialogueOption { Id = "tease", Label = "장난을 건다" });
                // IG01: bring back something that belongs to them (found somewhere, or passed on by someone)
                var theirs = Carried(P).Where(i => i.Owner == npc.Id && i.Def != null && !i.Def.Key && i.Type != "Invitation").Select(i => (i.Id, i.Kor)).ToList();
                if (theirs.Count > 0) o.Add(new DialogueOption { Id = "giveback", Label = "주인에게 돌려준다", Sub = theirs });
                var gifts = Carried(P).Where(i => i.Def != null && !i.Def.Key && i.Def.Tag != "rescue" && i.Owner != npc.Id).Select(i => (i.Id, i.Kor)).ToList();
                o.Add(new DialogueOption { Id = "gift", Label = "선물한다", Enabled = gifts.Count > 0, Why = "줄 만한 물건이 없다", Sub = gifts });
                // --- time-on-demand (begin): in a still daily world, "함께 시간을 보낸다" (time passes together) replaces the old invite
                if (OnDemand && S.Phase == Phase.Daily)
                {
                    var tog = TogetherOptions(npc).Where(t => t.Enabled).Select(t => (t.Id, t.Label + $" · {t.Minutes}분")).ToList();   // how long it takes, in the choice itself
                    o.Add(new DialogueOption { Id = "together", Label = "함께 시간을 보낸다", Enabled = tog.Count > 0, Why = "지금은 함께할 만한 게 없다", Sub = tog });
                }
                else
                {
                    var acts = npc.Def.Hobbies.Select(h => Activities.HobbyToActivity.TryGetValue(h, out var a) ? a : null).Where(a => a != null && a != "cook").Distinct().Select(a => (a, Activities.Get(a).Kor)).ToList();
                    o.Add(new DialogueOption { Id = "invite", Label = "같이 하자고 권한다", Enabled = acts.Count > 0, Sub = acts });
                }
                // --- time-on-demand (end)
                if (!rp.Casual && r.Like > 0.25f) o.Add(new DialogueOption { Id = "casual", Label = "말을 편하게 하자고 한다" });
                if (r.Trust > 0.35f) o.Add(new DialogueOption { Id = "confide", Label = "고민이 있는지 묻는다" });
                if (r.Grudge > 0.1f || rp.Memory.Any(m => m.Contains("다툼") || m.Contains("상처"))) o.Add(new DialogueOption { Id = "apologize", Label = "사과한다" });
                if (r.Like > 0.35f) o.Add(new DialogueOption { Id = "confess", Label = "마음을 전한다" });
            }
            var others = S.Living.Where(x => x != npc && !x.IsPlayer).Select(x => (x.Id, Cast.NameOf(x.Id))).ToList();
            o.Add(new DialogueOption { Id = "warn", Label = "누군가를 조심하라고 한다", Sub = others });
            o.Add(new DialogueOption { Id = npc.Following == Cast.Player ? "unfollow" : "follow", Label = npc.Following == Cast.Player ? "이제 따로 다니자고 한다" : "같이 다니자고 한다" });
            o.Add(new DialogueOption { Id = "bye", Label = "대화를 끝낸다" });
            return o;
        }

        Utterance U(string speaker, string listener, string key, Dictionary<string, string> slots = null, Emotion emo = Emotion.Neutral, Anim gest = Anim.Talk, Prop prop = null, bool lie = false)
        {
            string text = Render(speaker, listener, key, slots) ?? "…";
            return new Utterance { Speaker = speaker, Listener = listener, Key = key, Text = text, Emotion = emo, Gesture = gest, Prop = prop, Lie = lie };
        }

        /// <summary>Apply an option: returns the spoken lines (player first). Effects happen when the lines are spoken (time advances).</summary>
        public List<Utterance> Choose(Actor npc, string opt, string arg = null)
        {
            var res = new List<Utterance>(); var id = npc.Id; var me = Cast.Player; var rng = S.R(Stream.Dialogue); var c = npc.Def;
            var r = S.R(id, me);
            void Say(string key, Dictionary<string, string> sl = null, Emotion e = Emotion.Neutral, Anim g = Anim.Talk, Prop p = null, bool lie = false) => res.Add(U(id, me, key, sl, e, g, p, lie));
            void Me(string key, Dictionary<string, string> sl = null) => res.Add(U(me, id, key, sl));
            { var life = LifeChoose(npc, opt, arg); if (life != null) return life; }   // --- daily-life: life* options and gifts (Gifts.cs) (Sim/Life/LifeDialogue.cs)
            switch (opt)
            {
                case "bond": res.AddRange(BondOpen(npc)); break;
                case "req_accept": case "req_later": Me("p_accept_favor"); res.AddRange(AnswerRequest(npc, opt == "req_later" ? "later" : "accept")); break;
                case "req_refuse": Me("p_refuse_favor"); res.AddRange(AnswerRequest(npc, "refuse")); break;
                case "bondpick": res.AddRange(BondPick(npc, arg)); break;
                case "b_rules": Me("p_ask_about"); res.Add(U(Cast.Butler, me, "y_rules")); break;
                case "b_idle": Me("p_greet"); res.Add(U(Cast.Butler, me, "y_idle")); break;
                case "b_hint": Me("p_ask_suspect"); res.Add(U(Cast.Butler, me, "y_refuse_hint")); break;
                case "b_file":
                    Me("p_ask_about");
                    foreach (var inc in S.Incidents.Values.Where(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Confirmed)) { res.Add(U(Cast.Butler, me, "y_body", new Dictionary<string, string> { { "victim", Cast.NameOf(inc.Victim) }, { "place", S.RoomName(inc.FoundRoom) } })); Evidences.OfficialFile(this, P, inc); }
                    break;
                case "chat":
                    {
                        Me("p_greet");
                        bool gain = ChatGain(id);   // --- time-on-demand: small-talk gains count once per person per clock hour (OnDemand)
                        if (r.Talks <= 1 && !S.K(id).Facts.Contains("met:" + me)) { Say("intro_self"); S.K(id).Facts.Add("met:" + me); S.K(me).Facts.Add("met:" + id); }
                        else if (!gain) Say("talked_recently");
                        else Say(S.Phase == Phase.Investigation ? "investigate_comment" : rng.Chance(0.5) ? "small_talk" : "talk_mansion");
                        if (gain)
                        {
                            Relations.Change(S, id, me, like: 0.03f + Relations.Compat(c, Cast.Get(me)) * 0.02f);
                            Relations.Change(S, me, id, like: 0.02f);
                        }
                        break;
                    }
                case "likes":
                    {
                        Me("p_ask_like");
                        var topic = c.Likes.Length > 0 ? c.Likes[rng.R(c.Likes.Length)] : "이것저것";
                        Say("talk_like", new Dictionary<string, string> { { "topic", topic } }, Emotion.Smile);
                        S.K(me).Facts.Add($"likes:{id}:{topic}");
                        if (ChatGain(id)) Relations.Change(S, id, me, like: 0.04f, memory: "좋아하는 것에 관심을 가져 줬다");   // --- time-on-demand: capped
                        break;
                    }
                case "gossip":
                    {
                        Me("p_ask_saw");
                        var s = S.K(id).Sightings.Where(x => x.Target != me && x.Target != id && S.Clock - x.T1 < 360 && x.IdConf > 0.5f && !x.Dead).OrderByDescending(x => (x.Held != null ? 2 : 0) + (x.Running ? 1 : 0) + (x.Bloody ? 3 : 0) + (x.Carrying ? 3 : 0) + rng.F()).FirstOrDefault();
                        if (s == null || r.Trust < -0.2f) { Say("saw_nothing"); break; }
                        var p = new Prop { Kind = s.Held != null ? PropKind.Held : PropKind.AtPlace, A = s.Target, Room = s.Room, Item = s.Held, T0 = s.T0, T1 = s.T1, Value = "root:" + s.Root };
                        Say(s.Held != null ? "saw_item" : "gossip_saw", new Dictionary<string, string> { { "t", "@" + s.Target }, { "place", S.RoomName(s.Room) }, { "time", ClockFmt.Vague(s.T0) }, { "item", ItemCatalog.Get(s.Held)?.Kor } }, p: p);
                        break;
                    }
                case "compliment":
                    {
                        Me("p_compliment");
                        bool vain = c.P.Pride > 0.6f; bool wary = c.Deceit > 80 || c.P.Honesty < 0.3f;
                        Say("compliment_react", null, vain ? Emotion.Smile : wary ? Emotion.Smirk : Emotion.Smile);
                        if (ChatGain(id)) Relations.Change(S, id, me, like: vain ? 0.07f : wary ? 0.01f : 0.04f, memory: "칭찬을 들었다");   // --- time-on-demand: capped
                        break;
                    }
                case "tease":
                    {
                        Me("p_tease");
                        bool ok = c.P.Pride < 0.7f || r.Like > 0.4f;
                        Say("tease_react", null, ok ? Emotion.Laugh : Emotion.Angry);
                        if (!ok || ChatGain(id)) Relations.Change(S, id, me, like: ok ? 0.05f : -0.06f, grudge: ok ? 0 : 0.04f, memory: ok ? "장난을 주고받았다" : "놀림에 기분이 상했다");   // --- time-on-demand: gains capped
                        break;
                    }
                case "giveback":
                    {
                        var it = S.I(arg); if (it == null || it.Holder != me) { Say("small_talk"); break; }
                        Me("p_gift", new Dictionary<string, string> { { "item", it.Kor } });
                        DropItem(P, it, npc.Pos); it.Holder = null; PickUp(npc, it);
                        { var rq = RequestHandover(npc, it); if (rq != null) res.Add(rq); else Say("return_thanks", null, Emotion.Smile); }
                        foreach (var l in S.Loans.Where(l => l.Item == it.Id && l.State != "resolved"))
                        {
                            // the misunderstanding clears: whoever was suspected gets some of the lost trust back
                            if (l.Suspect != null) Relations.Change(S, id, l.Suspect, grudge: -0.08f, trust: 0.04f, memory: "없어진 줄 알았던 물건이 돌아왔다");
                            l.State = "resolved"; S.Log("ItemFound", id, me, item: it.Id, data: l.Id + " via player");
                        }
                        Relations.Change(S, id, me, like: 0.06f, trust: 0.06f, memory: "잃어버린 물건(" + (it.Def?.Kor ?? it.Kor) + ")을 찾아 줬다");
                        break;
                    }
                case "gift":
                    {
                        var it = S.I(arg); if (it == null || it.Holder != me) { Say("small_talk"); break; }
                        Me("p_gift", new Dictionary<string, string> { { "item", it.Kor } });
                        int score = GiftScore(c, it);
                        string key = score >= 2 ? "gift_love" : score == 1 ? "gift_like" : score == 0 ? "gift_meh" : "gift_hate";
                        Say(key, new Dictionary<string, string> { { "item", it.Kor } }, score >= 1 ? Emotion.Smile : score < 0 ? Emotion.Disgust : Emotion.Neutral);
                        if (score >= 0) { DropItem(P, it, npc.Pos); it.Holder = null; PickUp(npc, it); it.Owner = id; }
                        Relations.Change(S, id, me, like: 0.03f * (score + 1), attach: score >= 2 ? 0.05f : 0, memory: score >= 1 ? it.Kor + ", 선물로 받았다" : score < 0 ? "싫어하는 걸 받았다" : null);
                        break;
                    }
                case "invite":
                    {
                        var def = Activities.Get(arg); if (def == null) break;
                        Me("p_invite", new Dictionary<string, string> { { "act", def.Kor } });
                        bool yes = r.Like + r.Trust > 0.05f && npc.Needs.Energy > 0.2f && !S.IsNight;
                        Say(yes ? "invite_yes" : "invite_no", new Dictionary<string, string> { { "act", def.Kor } }, yes ? Emotion.Smile : Emotion.Neutral);
                        if (yes)
                        {
                            var act = Simple(npc, arg); if (act != null) { act.Id = "social:join:" + me; act.Label = "민혁과 " + def.Kor; Assign(npc, act); S.Flags["invited:" + id] = S.Clock; }
                            Relations.Change(S, id, me, like: 0.03f, attach: 0.02f);
                        }
                        break;
                    }
                case "casual":
                    {
                        Me("p_casual_offer");
                        bool yes = r.Like > 0.3f;
                        Say(yes ? "casual_yes" : "casual_no", null, yes ? Emotion.Smile : Emotion.Neutral);
                        if (yes) { S.R(me, id).Casual = true; S.R(id, me).Casual = true; Relations.Change(S, id, me, like: 0.04f, memory: "말을 편하게 하기로 했다"); Relations.Change(S, me, id, memory: "말을 편하게 하기로 했다"); }
                        break;
                    }
                case "confide":
                    {
                        Me("p_comfort");
                        bool deep = r.Trust > 0.6f && r.Like > 0.5f;
                        Say(deep ? "secret_share" : "secret_hint", null, Emotion.Sad, Anim.Think);
                        Relations.Change(S, id, me, trust: 0.05f, attach: 0.05f, memory: "속마음을 털어놓았다");
                        S.K(me).Facts.Add(deep ? "secret:" + id : "hint:" + id);
                        break;
                    }
                case "apologize":
                    {
                        Me("p_apologize");
                        bool ok = r.Grudge < 0.45f;
                        Say(ok ? "forgive" : "not_forgive", null, ok ? Emotion.Neutral : Emotion.Angry);
                        Relations.Change(S, id, me, grudge: ok ? -0.2f : -0.04f, like: ok ? 0.05f : 0, memory: ok ? "사과를 받아들였다" : "사과를 받았지만 용서하지 않았다");
                        break;
                    }
                case "confess":
                    {
                        Me("p_confess_love");
                        bool yes = r.Romance > 0.3f && r.Like > 0.45f || (r.Like > 0.6f && c.P.Romance > 0.6f);
                        Say(yes ? "love_yes" : "love_no", null, yes ? Emotion.Smile : Emotion.Sad);
                        if (yes) { Relations.Change(S, id, me, romance: 0.3f, attach: 0.2f, tag: "lover", memory: "마음을 받아들였다"); Relations.Change(S, me, id, romance: 0.3f, attach: 0.2f, tag: "lover", memory: "마음이 통했다"); OnCouple(npc, P); }
                        else Relations.Change(S, id, me, like: -0.02f, memory: "고백을 거절했다");
                        break;
                    }
                case "warn":
                    {
                        var t = S.A(arg); if (t == null) break;
                        Me("p_warn", new Dictionary<string, string> { { "t", "@" + t.Id } });
                        float believe = MathX.Clamp01(0.3f + r.Trust - S.R(id, t.Id).Trust * 0.5f);
                        var k = S.K(id); k.Suspicion[t.Id] = (k.Suspicion.TryGetValue(t.Id, out var v) ? v : 0) + believe * 0.4f;
                        Relations.Change(S, id, t.Id, fear: believe * 0.2f, trust: -believe * 0.15f);
                        Say(believe > 0.4f ? "fear_of" : "small_talk", new Dictionary<string, string> { { "t", "@" + t.Id } }, believe > 0.4f ? Emotion.Fear : Emotion.Neutral);
                        if (believe > 0.5f) npc.Needs.Fear = MathX.Clamp01(npc.Needs.Fear + 0.2f);
                        break;
                    }
                case "follow":
                    {
                        Me("p_accompany");
                        bool yes = r.Trust + r.Like > 0.05f || S.Phase == Phase.Investigation;
                        Say(yes ? "accompany_start" : "invite_no", null, yes ? Emotion.Smile : Emotion.Neutral);
                        if (yes) { npc.Following = me; S.Log("Accompany", id, me); }
                        break;
                    }
                case "unfollow": Me("p_stop_accompany"); Say("accompany_end"); npc.Following = null; if (npc.Act?.Id == "follow") EndActivity(npc, false); break;
                // ---- investigation
                case "q_case":
                    {
                        // where were you, did you see anything, did you hear anything — asked as one question. The dice roll in the
                        // same order as asking the three in turn; only the redundant "saw nothing" answers are left unsaid.
                        var inc = CaseProgress.Current(S);
                        Me("p_ask_where", new Dictionary<string, string> { { "time", ClockFmt.Vague(Testimony.CaseWindow(S).t1 - 60) } });
                        if (r.Trust + r.Like < -0.3f) { Say("refuse_answer", null, Emotion.Angry); break; }
                        // asked only once they actually answer (a refusal leaves them "아직" on the board and in the hints)
                        if (inc != null) S.K(me).Facts.Add($"asked:{id}:case:{inc.Id}");
                        var w = Testimony.Where(this, npc, me); Say(w.Key, w.Slots, w.Lie && c.Composure < 70 ? Emotion.Fear : Emotion.Neutral, Anim.Think, w.Prop, w.Lie);
                        U(me, id, "p_ask_saw");   // rendered (the line bank draws), not said
                        Utterance nothing = null; int said = 0;
                        foreach (var s in Testimony.Saw(this, npc, me, 2))
                        {
                            var u = U(id, me, s.Key, s.Slots, Emotion.Neutral, Anim.Think, s.Prop, s.Lie);
                            if (s.Key == "saw_nothing") { nothing = nothing ?? u; continue; }
                            res.Add(u); said++;
                        }
                        U(me, id, "p_ask_heard");
                        { var h = Testimony.Heard(this, npc, me); var u = U(id, me, h.Key, h.Slots, Emotion.Neutral, Anim.Think, h.Prop); if (h.Key == "saw_nothing") nothing = nothing ?? u; else { res.Add(u); said++; } }
                        if (said == 0 && nothing != null) res.Add(nothing);
                        break;
                    }
                case "q_where":
                    {
                        Me("p_ask_where", new Dictionary<string, string> { { "time", ClockFmt.Vague(Testimony.CaseWindow(S).t1 - 60) } });
                        if (r.Trust + r.Like < -0.3f) { Say("refuse_answer", null, Emotion.Angry); break; }
                        var w = Testimony.Where(this, npc, me); Say(w.Key, w.Slots, w.Lie && c.Composure < 70 ? Emotion.Fear : Emotion.Neutral, Anim.Think, w.Prop, w.Lie);
                        break;
                    }
                case "q_saw":
                    {
                        Me("p_ask_saw");
                        if (r.Trust + r.Like < -0.3f) { Say("refuse_answer", null, Emotion.Angry); break; }
                        foreach (var s in Testimony.Saw(this, npc, me, 2)) Say(s.Key, s.Slots, Emotion.Neutral, Anim.Think, s.Prop, s.Lie);
                        break;
                    }
                case "q_heard": { Me("p_ask_heard"); var h = Testimony.Heard(this, npc, me); Say(h.Key, h.Slots, Emotion.Neutral, Anim.Think, h.Prop); break; }
                case "q_suspect": { Me("p_ask_suspect"); var s = Testimony.Suspect(this, npc); Say(s.Key, s.Slots, Emotion.Neutral, Anim.Think); break; }
                case "q_share":
                    {
                        Me("p_ask_share");
                        if (r.Trust < 0.05f && r.Like < 0.2f) { Say("keep_find"); break; }
                        var mine = S.K(me).Evidence.Where(m => m.Chapter == S.Chapter && m.Loop == S.Loop).Select(m => m.Root).ToList();
                        var ev = S.K(id).Evidence.Where(e => e.Chapter == S.Chapter && e.Loop == S.Loop && !e.Hidden && !mine.Any(m => Evidences.SameThing(m, e.Root))).OrderByDescending(e => e.Important ? 1 : 0).ThenByDescending(e => e.Acquired).FirstOrDefault();
                        if (ev == null) { Say("saw_nothing"); break; }
                        Say("share_find", new Dictionary<string, string> { { "item", CaseBoard.Describe(this, ev).Title }, { "place", S.RoomName(ev.Room) } }, Emotion.Neutral, Anim.Present);
                        Evidences.Relay(this, ev, id); ev.SharedWith.Add(me);
                        break;
                    }
                case "show":
                    {
                        var ev = S.K(me).Evidence.FirstOrDefault(e => e.Id == arg); if (ev == null) break;
                        Me("p_share", new Dictionary<string, string> { { "item", CaseBoard.Describe(this, ev).Title } });
                        var copy = Evidences.Add(this, id, ev.Kind, ev.Title + " (민혁에게 들음)", ev.Desc, Cast.NameOf(me), ev.Root, ev.T0, ev.T1, ev.Room, ev.CanKnow, "직접 본 게 아니다", false, ev.Props.Select(p => p.Clone()).ToArray());
                        ev.SharedWith.Add(id); Testimony.UpdateSuspicion(this, npc);
                        Relations.Change(S, id, me, trust: 0.04f, memory: "조사한 자료를 나눠 줬다");
                        Say(IsCulpritNow(id) && ev.Props.Any(p => p.A == id) ? "panic" : "agree", new Dictionary<string, string> { { "t", "@" + me } }, IsCulpritNow(id) ? Emotion.Fear : Emotion.Neutral);
                        break;
                    }
                case "bye": Me("p_bye"); Say("bye"); break;
                case "together": res.AddRange(ChooseTogether(npc, arg)); break;   // --- time-on-demand (Systems/Together.cs)
            }
            return res;
        }

        // --- time-on-demand: in a still world, small talk warms someone up once per clock hour (the first of chat / likes /
        // compliment / tease within the hour counts; the rest is just talk). Continuous: always counts.
        bool ChatGain(string npc)
        {
            if (!OnDemand) return true;
            string k = "chatgain:" + npc;
            if (S.Flags.TryGetValue(k, out var t) && S.Clock - t < 60) return false;
            S.Flags[k] = S.Clock; return true;
        }

        bool IsCulpritNow(string id) => S.Incidents.Values.Any(i => i.Loop == S.Loop && i.Chapter == S.Chapter && i.Culprit == id);

        public static int GiftScore(CastDef c, Item it)
        {
            string name = (it.Kor ?? "") + " " + it.Type;
            foreach (var l in c.Likes) if (name.Contains(l) || l.Contains(it.Kor) || Similar(l, it)) return 2;
            foreach (var d in c.Dislikes) if (name.Contains(d)) return -1;
            if (it.Def?.Consumable == true) return 1;
            if (it.Def?.IsWeapon == true) return -1;
            return 0;
        }
        static bool Similar(string like, Item it)
        {
            switch (it.Type)
            {
                case "Candy": return like.Contains("사탕"); case "Chocolate": return like.Contains("초콜릿"); case "Bread": return like.Contains("빵");
                case "Snack": return like.Contains("과자"); case "Soda": return like.Contains("탄산"); case "Beer": return like.Contains("맥주");
                case "Tea": return like.Contains("차"); case "Sticker": return like.Contains("스티커"); case "WindupToy": return like.Contains("태엽");
                case "PaperModel": return like.Contains("종이"); case "Button": return like.Contains("단추"); case "HandWarmer": return like.Contains("핫팩");
                case "Flower": return like.Contains("식물") || like.Contains("꽃"); case "Book": return like.Contains("책") || like.Contains("독서");
                case "Thermos": return like.Contains("보온병"); case "Invitation": return like.Contains("초대");
            }
            return false;
        }

        /// <summary>Called by the UI when an utterance finishes playing: the world hears it and time moves.</summary>
        public void Spoken(Utterance u)
        {
            var a = S.A(u.Speaker); if (a == null) return;
            S.Log("Speech", u.Speaker, u.Listener, room: a.Room, data: u.Key + "|" + u.Text);
            if (u.Prop != null) Learn(u.Listener ?? Cast.Player, u.Speaker, u.Prop, u.Text, u.Lie, false);
            // bystanders overhear
            foreach (var h in S.Actors.Values)
            {
                if (h.Id == u.Speaker || h.Id == u.Listener || !h.Alive || h.Pose == Pose.Sleep || h.Pos.f != a.Pos.f || h.Pos.DistXZ(a.Pos) > 9f) continue;
                if (u.Prop != null) Learn(h.Id, u.Speaker, u.Prop, u.Text, u.Lie, true);
            }
            // a spoken line takes a little world time (~40 clock seconds)
            // --- time-on-demand: in a still daily world the minutes are kept and passed right after the conversation closes
            if (OnDemand && S.Phase == Phase.Daily) PendingTalk += 0.7;
            else Advance(0.7);
        }

        public void Advance(double clockMinutes)
        {
            int ticks = (int)Math.Ceiling(clockMinutes / Math.Max(0.001, S.ClockRate) * SimTime.PerSecond);
            RunTicks(Math.Min(ticks, 20000));
        }

        // ------------------------------------------------------------------ world interactions
        public bool PlayerPickUp(Item it)
        {
            if (it == null || it.Holder != null || it.Pos.f != P.Pos.f || it.Pos.DistXZ(P.Pos) > 2.6f) return false;
            bool ok = PickUp(P, it); if (ok) S.K(Cast.Player).ItemSeen[it.Id] = (P.Room, S.Clock);
            if (it.Surface.Any(s => s.StartsWith("secret:"))) ReadEnvelope(it);
            return ok;
        }
        public void PlayerDrop(Item it, P3 at) { if (it != null && it.Holder == Cast.Player) DropItem(P, it, at); }
        public void PlayerPlaceItemPhysics(Item it, P3 at) { if (it == null || it.Holder != null) return; it.Pos = at; it.Room = S.Layout.RoomAt(at); it.LastMovedTick = S.Tick; }

        void ReadEnvelope(Item env)
        {
            var subj = env.Surface.First(s => s.StartsWith("secret:")).Substring(7);
            S.K(Cast.Player).Facts.Add("secret:" + subj);
            Evidences.Add(this, Cast.Player, EvKind.Document, $"{Cast.NameOf(subj)}의 과거가 적힌 봉투", $"{Cast.NameOf(subj)} — {Cast.Get(subj).Secret}", "과거의 봉투", "envelope:" + env.Id, S.Clock, S.Clock, env.Room, "봉투에 적힌 과거", "지금 무슨 생각인지, 이번 사건과 관련이 있는지", true);
            S.Emit(GameEventType.Notice, Cast.Player, subj, text: "봉투를 읽었다", key: "envelope");
        }

        public string PlayerDoor(Door d, string action)
        {
            if (d == null || d.Pos.DistXZ(P.Pos) > 2.5f) return "너무 멀다";
            switch (action)
            {
                case "open": if (d.Locked) { S.K(Cast.Player).DoorLocked[d.Id] = true; Sound(SoundKind.Knock, d.Pos, 0.2f, Cast.Player); return "잠겨 있다"; } SetDoor(P, d, !d.Open, null); return null;
                case "lock": if (!HasKey(P, d)) return "맞는 열쇠가 없다"; SetDoor(P, d, false, true, "민혁"); return null;
                case "unlock": if (!HasKey(P, d)) return "맞는 열쇠가 없다"; SetDoor(P, d, null, false, "민혁"); return null;
                case "knock": Sound(SoundKind.Knock, d.Pos, 0.5f, Cast.Player); S.Log("Knock", Cast.Player, data: "door" + d.Id); KnockAnswer(d); return null;
            }
            return null;
        }

        void KnockAnswer(Door d)
        {
            var bed = S.Layout.Room(d.RoomA)?.Type == RoomType.Bedroom ? S.Layout.Room(d.RoomA) : S.Layout.Room(d.RoomB);
            if (bed == null || bed.Owner == null) return;
            var o = S.A(bed.Owner); if (o == null || !o.Alive || o.Room != bed.Id) return;
            if (o.Pose == Pose.Sleep) Wake(o);
            var r = S.R(o.Id, Cast.Player);
            if (r.Trust + r.Like > -0.1f && o.Needs.Fear < 0.8f) { d.Locked = false; d.Open = true; DoorChanged(d, o); Speak(o, "greet", Cast.Player); }
            else Speak(o, "busy", Cast.Player, new Dictionary<string, string> { { "act", "휴식" } });
        }

        public Evidence PlayerExamine(object target)
        {
            switch (target)
            {
                case Actor a when !a.Alive || a.Status == ActorStatus.Unconscious: return Evidences.ExamineBody(this, P, a, true);
                case Trace t: return Evidences.ExamineTrace(this, P, t);
                case Item it: return Evidences.ExamineItem(this, P, it);
                case Door d: return Evidences.ExamineDoor(this, P, d);
                case Furniture f: return ExamineFurniture(f);
            }
            return null;
        }

        Evidence ExamineFurniture(Furniture f)
        {
            var def = FurnitureCatalog.Get(f.Type);
            string kor = def?.Kor ?? f.Type;
            string desc = kor;
            var props = new List<Prop>();
            S.K(Cast.Player).Examined.Add("furn:" + f.Id);
            if (f.Type == "Switchboard") { desc = "회로 상태: " + string.Join(", ", S.Layout.Circuits.Select(c => c.Name + (S.CircuitOn(c.Id) ? " 켜짐" : " 꺼짐"))); var touched = S.Traces.Where(t => t.Type == "SwitchTouched").OrderByDescending(t => t.Clock).FirstOrDefault(); if (touched != null) { desc += ". " + touched.Know; props.Add(new Prop { Kind = PropKind.LightsChanged, Value = touched.Desc, T0 = touched.Clock - 15, T1 = touched.Clock + 15 }); } }
            if (f.Type == "PressConsole" || f.Type == "Press") { var armed = S.Ledger.Where(e => e.Type == "PressArmed").OrderByDescending(e => e.Clock).FirstOrDefault(); desc = "프레스 조작부다. 가동 횟수 계기와 타이머 다이얼이 달려 있다." + (armed != null ? $" 타이머를 맞춘 흔적이 있다(작동 시각: {ClockFmt.Vague(S.PressFireAt >= 0 ? S.PressFireAt : armed.Clock + 6)})." : " 최근에 쓴 흔적은 없다."); if (armed != null) props.Add(new Prop { Kind = PropKind.MachineUsed, Value = "프레스", T0 = armed.Clock - 10, T1 = armed.Clock + 10 }); }
            Grammars.ExamineFurniture(this, P, f, ref desc, props);
            bool clockOff = false;
            if (f.Type == "ClockCase") { var room = S.Layout.Room(f.Room); bool off = room?.Type == RoomType.ClockMuseum && f.Variant % 2 == 1; int offMin = (f.Variant * 17 % 90) - 45; clockOff = off && offMin != 0; desc = clockOff ? $"시계 바늘이 가리키는 시각은 {ClockFmt.Vague(S.Clock + offMin)} — 종소리와 어긋나 있다" : $"시계는 제 시각을 가리킨다 — {ClockFmt.Vague(S.Clock)}"; }
            // a clock that reads right is not a clue
            props.RemoveAll(p => p.Kind == PropKind.ClockOffset && (p.Value == "0" || p.Value == "-0"));
            // looking is not filing: only a piece of furniture with something to say about it earns a card
            bool notable = props.Count > 0 || f.Marks.Count > 0 || f.Moved || clockOff || (f.Type == "Switchboard" && S.Phase == Phase.Investigation);
            if (!notable) return new Evidence { Loose = true, Kind = EvKind.ObjectState, Title = kor, Line = $"평범한 {kor}{(LineBank.Josa(kor, "이") == "이" ? "이다" : "다")}.", Owner = Cast.Player, Room = f.Room, T0 = S.Clock, T1 = S.Clock, Root = "furn:" + f.Id };
            var lines = new List<string>();
            if (desc != kor) lines.AddRange(desc.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0));
            if (f.Marks.Count > 0) lines.Add("흔적: " + string.Join(", ", f.Marks));
            if (f.Moved) lines.Add("원래 자리에서 옮겨졌다");
            if (lines.Count == 0) lines.Add($"{S.RoomName(f.Room)}에 있다");
            return Evidences.File(this, Cast.Player, EvKind.ObjectState, kor, string.Join("\n", lines), "직접 조사", "furn:" + f.Id, S.Clock, S.Clock, f.Room, "지금 상태", "누가 언제 이렇게 만들었는지", true, true, lines[0].TrimEnd('.') + ".", false, props.ToArray());
        }

        public string PlayerOperate(Furniture f, int arg)
        {
            if (f == null) return null;
            var trapMsg = Tricks.PlayerOperate(this, f); if (trapMsg != null) return trapMsg;
            if (f.Type == "Workbench")
            {
                // put a broken thing back together from its pieces (the repair itself leaves marks)
                var frags = Carried(P).Where(i => i.Type == "Fragment" && i.ParentItem != null).GroupBy(i => i.ParentItem).OrderByDescending(g => g.Count()).FirstOrDefault();
                if (frags == null) return "고칠 조각이 없다";
                var parent = S.I(frags.Key); if (parent == null) return "무엇의 조각인지 모르겠다";
                int total = S.Items.Values.Count(i => i.Type == "Fragment" && i.ParentItem == frags.Key);
                if (frags.Count() < total) return $"조각이 모자란다 ({frags.Count()}/{total})";
                foreach (var fr in frags.ToList()) { P.Pocket.Remove(fr.Id); if (P.HandL == fr.Id) P.HandL = null; if (P.HandR == fr.Id) P.HandR = null; S.Items.Remove(fr.Id); S.Emit(GameEventType.ItemMoved, Cast.Player, data: fr.Id, text: "remove"); }
                parent.Damage = 1; parent.Surface.Add("repaired"); if (parent.Holder == null) { parent.Pos = P.Pos; parent.Room = P.Room; }
                S.Log("Repair", Cast.Player, item: parent.Id, room: P.Room); S.Emit(GameEventType.ItemState, Cast.Player, data: parent.Id, text: "repaired");
                return parent.Kor + "의 조각을 맞춰 붙였다 — 금 간 자국은 남는다";
            }
            if (f.Type == "Switchboard") { bool on = !S.CircuitOn(arg); SetCircuit(arg, on, Cast.Player); return (on ? "전원을 켰다 — " : "전원을 껐다 — ") + S.Layout.Circuits[arg].Name; }
            if (f.Type == "PressConsole") { if (arg == 1) { S.PressFireAt = S.Clock + 0.3; S.PressArmedBy = Cast.Player; S.Log("PressArmed", Cast.Player, data: "manual"); return "프레스를 작동시켰다"; } if (arg == 2) { S.PressFireAt = -1; return "타이머를 풀었다"; } }
            if (f.Type == "Sink" || f.Type == "Washer")
            {
                var it = Held(P); if (it == null) { if (P.BloodOnClothes > 0.1f) { P.BloodOnClothes *= 0.2f; S.Log("Wash", Cast.Player, data: "hands"); return "손을 씻었다"; } return "씻을 것이 없다"; }
                it.Washed = it.Bloody || it.Washed; it.Bloody = false; it.Surface.Remove("blood"); if (it.Washed && !it.Surface.Contains("residue")) it.Surface.Add("residue");
                S.Log("Wash", Cast.Player, item: it.Id, room: P.Room); Sound(SoundKind.Splash, P.Pos, 0.2f, Cast.Player); AddTrace("Water", P.Pos, P.Room, Cast.Player, null, 0.35f, 1, "개수대 주변의 물기", "최근 누군가 물을 썼다", "무엇을 씻었는지");
                S.Emit(GameEventType.ItemState, Cast.Player, data: it.Id, text: "washed"); return it.Kor + "에 물을 부어 깨끗이 씻었다";
            }
            return null;
        }

        public void PlayerStrike(Actor target, BodyRegion region, float lx, float ly, float lz,
            float dx = 0, float dy = 0, float dz = 0, float contactImpulse = 0, float contactEnergy = 0, float nx = 0, float ny = 0, float nz = 0, float damageScale = 1)
        {
            var w = Held(P, d => d != null && d.IsWeapon);
            var def = w?.Def; var dt = def?.Dmg ?? DamageType.Blunt; int sev = def?.Sev ?? 1;
            if (contactImpulse > 0) sev = Math.Max(1, Math.Min(4, (int)Math.Round(sev * Math.Max(.35f, Math.Min(1.2f, damageScale)))));
            bool aware = Math.Abs(MathX.DeltaAngle(target.Yaw, MathX.AngleDeg(P.Pos.x - target.Pos.x, P.Pos.z - target.Pos.z))) < 100;
            if (!aware) sev = Math.Min(4, sev + 1);
            if (!S.Flags.ContainsKey("attackstart:" + Cast.Player + ":" + target.Id)) { S.Flags["attackstart:" + Cast.Player + ":" + target.Id] = S.Clock; S.Log("AttackBegin", Cast.Player, target.Id, room: target.Room, pos: target.Pos); }
            Strike(Cast.Player, target, region, dt, sev, w?.Id, "player", lx, ly, lz, dx, dy, dz, contactImpulse, contactEnergy, nx, ny, nz);
            if (target.Alive && !target.IsButler) Relations.Change(S, target.Id, Cast.Player, fear: 0.6f, grudge: 0.6f, trust: -1f, memory: "민혁이 나를 공격했다", tag: "enemy");
            EmergencyTrigger(4);   // --- time-on-demand: a fight runs the clock (OnDemand)
        }

        public bool PlayerFirstAid(Actor v) => FirstAid(P, v);

        public bool PlayerCarry(Actor t)
        {
            if (t == null || t.Status == ActorStatus.Active || t.CarriedBy != null || P.Carrying != null || t.Pos.DistXZ(P.Pos) > 2.2f) return false;
            P.Carrying = t.Id; t.CarriedBy = Cast.Player;
            S.Log("CarryStart", Cast.Player, t.Id, room: P.Room, pos: P.Pos);
            S.Emit(GameEventType.Carry, Cast.Player, t.Id, value: 1);
            if (!t.Alive) { var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == t.Id && i.Loop == S.Loop); if (inc != null) { inc.BodyMoved = true; inc.Notes.Add("민혁이 시신을 옮김"); } AddTrace("DragMark", P.Pos, P.Room, Cast.Player, t.Id, 0.5f, 1, "무언가를 끈 자국", "무거운 것이 끌려 옮겨졌다", "무엇을, 누가 옮겼는지"); }
            return true;
        }

        public void PlayerReleaseCarry()
        {
            var t = S.A(P.Carrying); if (t == null) { P.Carrying = null; return; }
            t.CarriedBy = null; P.Carrying = null; t.Pos = P.Pos; t.Room = P.Room;
            S.Log("CarryEnd", Cast.Player, t.Id, room: P.Room, pos: P.Pos);
            S.Emit(GameEventType.Carry, Cast.Player, t.Id, value: 0);
            if (S.Layout.Room(P.Room)?.Type == RoomType.Infirmary && t.Alive && t.Body.Critical) FirstAid(P, t);
        }
        public void PlayerReport() => Cases.PlayerReport(this);

        public void PlayerSleep()
        {
            var bed = S.Layout.BedroomOf(Cast.Player);
            P.Pose = Pose.Sleep;
            double until = S.Minute >= 20 * 60 ? (1440 - S.Minute) + 7 * 60 + 30 : Math.Max(30, (7 * 60 + 30) - S.Minute);
            Wait(until, () => S.Phase != Phase.Daily);
            P.Pose = Pose.Stand; P.Needs.Energy = 1;
        }

        public void PlayerAcceptGoal(string id) { if (S.Goals.TryGetValue(id, out var g) && g.Owner == Cast.Player) g.PlayerAccepted = true; }

        /// <summary>Prologue: the butler's opening speech ends and daily life begins.</summary>
        public void EndPrologue()
        {
            if (S.Phase != Phase.Prologue) return;
            SetPhase(Phase.Daily);
            foreach (var a in S.LivingNpcs) { Interrupt(a, S.R(Stream.Life).Range(0.2f, 3f)); a.Act = null; }
            S.Log("PrologueEnd", Cast.Butler);
        }
    }
}
