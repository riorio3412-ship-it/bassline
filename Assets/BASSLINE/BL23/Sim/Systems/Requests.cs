using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// A personal invitation or request from a resident to 민혁 (spec §06 2, P0411 / P0440): "체스 둘 건데 같이 할래?",
    /// "제 만년필 못 보셨어요?", "이 쪽지 좀 전해 줄래?". Delivered in person (they walk up and say it), answered in the
    /// conversation (accept / later / refuse), shown as an appointment card, and kept or broken for real: a kept promise
    /// builds trust, a broken one costs it, a refusal is simply respected.
    /// </summary>
    public sealed class Request
    {
        public string Id; public string From; public string Kind;          // invite, find, deliver
        public string Activity; public int Room = -1; public double At;   // invite: what, where, when
        public string Item; public string To;                              // find: the lost item; deliver: the note and its addressee
        public double Made, Due; public string State = "offered";          // offered, accepted, refused, met, done, missed, expired
        public string Text;
    }

    public sealed partial class Simulation
    {
        static readonly Dictionary<string, string> InviteWord = new Dictionary<string, string>
        {
            ["tea"] = "차 한잔", ["walk"] = "산책", ["eat"] = "식사", ["swim"] = "수영", ["game"] = "게임", ["music"] = "연주", ["listen"] = "음악 감상",
            ["read"] = "책 읽기", ["garden"] = "화초 돌보기", ["bar"] = "한잔", ["chess"] = "체스", ["exercise"] = "운동", ["puzzle"] = "퍼즐", ["craft"] = "종이 공예",
            ["perform"] = "공연 연습", ["film"] = "영화 보기", ["browse"] = "구경", ["socialize"] = "수다 떨기", ["explore"] = "저택 탐방", ["tour"] = "처음 보는 방 구경",
        };

        bool PoliteToPlayer(string id) => !S.R(id, Cast.Player).Casual && (Cast.Get(id)?.Speech.PoliteDefault ?? true);

        public Request OfferFrom(Actor npc) => S.Requests?.FirstOrDefault(r => r.From == npc.Id && r.State == "offered");
        public IEnumerable<Request> OpenRequests() => (S.Requests ?? new List<Request>()).Where(r => r.State == "offered" || r.State == "accepted");

        /// <summary>Once a clock minute: keep appointments, expire old offers, and now and then have someone come with an invitation or a favour.</summary>
        void RequestsTick(int mod)
        {
            if (S.Requests == null) S.Requests = new List<Request>();
            var me = S.Player;
            foreach (var r in S.Requests)
            {
                var from = S.A(r.From);
                if ((r.State == "offered" || r.State == "accepted") && (from == null || !from.Alive)) { r.State = "expired"; continue; }
                if (r.State == "offered" && (S.Clock - r.Made > 90 || (r.Kind == "invite" && S.Clock > r.At - 10))) { r.State = "expired"; continue; }
                if (r.Kind != "invite" || r.State != "accepted") continue;
                // the appointment: the host goes there a little early and spends a while at it
                if (S.Clock >= r.At - 4 && !S.Flags.ContainsKey("reqgo:" + r.Id) && from.Status == ActorStatus.Active && from.PlanId == null)
                {
                    S.Flags["reqgo:" + r.Id] = S.Clock;
                    var room = S.Layout.Room(r.Room);
                    if (room != null)
                    {
                        var act = new Activity { Id = "life:" + r.Activity + ":req", Label = (InviteWord.TryGetValue(r.Activity, out var w) ? w : r.Activity) + " 약속", Priority = 40 };
                        act.Steps.Add(GoTo(RandomPointIn(room, S.R(Stream.Life))));
                        act.Steps.Add(Do(r.Activity, 30, Activities.Get(r.Activity)?.Anim ?? Anim.Talk));
                        Assign(from, act);
                    }
                }
                // 민혁 came: the promise is kept (the scene itself is played by the presentation layer)
                if (me != null && me.Alive && me.Room == r.Room && from.Room == r.Room && S.Clock >= r.At - 5 && S.Clock <= r.At + 40)
                {
                    r.State = "met";
                    Relations.Change(S, r.From, Cast.Player, like: 0.07f, trust: 0.06f, memory: "약속한 대로 와 줬다");
                    Relations.Change(S, Cast.Player, r.From, like: 0.04f);
                    S.Log("RequestMet", r.From, Cast.Player, room: r.Room, data: r.Activity);
                    S.Emit(GameEventType.Notice, r.From, Cast.Player, text: Cast.GivenOf(r.From) + "와(과)의 약속을 지켰다", key: "request_met", data: r.Id);
                }
                else if (S.Clock > r.At + 25)
                {
                    r.State = "missed";
                    Relations.Change(S, r.From, Cast.Player, like: -0.03f, trust: -0.05f, memory: "약속 장소에 오지 않았다");
                    S.Log("RequestMissed", r.From, Cast.Player, room: r.Room, data: r.Activity);
                    S.Emit(GameEventType.Notice, r.From, Cast.Player, text: Cast.GivenOf(r.From) + "와(과)의 약속을 놓쳤다", key: "request_missed", data: r.Id);
                }
            }
            if (S.Phase != Phase.Daily || mod < 8 * 60 + 20 || mod > 21 * 60 || mod % 20 != 0 || me == null || !me.Alive) return;
            if (S.Requests.Count(r => r.State == "offered") >= 1 || S.Requests.Count(r => r.State == "accepted") >= 3) return;
            // --- time-on-demand: in a still world each offer interrupts a wait, so they come at most once every three clock hours
            if (OnDemand && S.Flags.TryGetValue("req:lastoffer", out var lastOffer) && S.Clock - lastOffer < 180) return;
            var rng = S.R(Stream.Life);
            if (!rng.Chance(0.5)) return;
            var cands = S.LivingNpcs.Where(a => a.Status == ActorStatus.Active && a.PlanId == null && a.TalkingTo == null && a.Pose != Pose.Sleep && a.Pos.f == me.Pos.f && a.Pos.Dist(me.Pos) < 30f
                && (S.R(a.Id, Cast.Player).Like > 0.05f || a.Def.P.Sociability > 0.6f)
                && !S.Requests.Any(r => r.From == a.Id && (S.Clock - r.Made < 150 || r.State == "offered" || r.State == "accepted"))).OrderBy(a => a.Id).ToList();
            if (cands.Count == 0) return;
            var who = cands[rng.R(cands.Count)];
            double roll = rng.F(); Request req = null;
            if (roll < 0.25) req = MakeFind(who);
            else if (roll < 0.45) req = MakeDeliver(who, rng);
            if (req == null) req = MakeInvite(who, rng);
            if (req == null) return;
            VoiceRequest(who, req);   // voice pack: the asker's own words (Content/Voice/VoiceHooks.cs)
            S.Requests.Add(req);
            if (OnDemand) S.Flags["req:lastoffer"] = S.Clock;   // --- time-on-demand
            var ask = new Activity { Id = "req:" + req.Id, Label = "민혁 찾아가기", Priority = 30 };
            ask.Steps.Add(new ActionStep { Kind = "Talk", Actor = Cast.Player });
            Assign(who, ask);
            S.Log("Request", who.Id, Cast.Player, data: req.Kind + ":" + (req.Activity ?? req.Item));
        }

        /// <summary>An appointment already made within 30 minutes of this invitation's time (a read; nothing changes).</summary>
        public Request InviteClash(Request r)
        {
            if (r == null || r.Kind != "invite" || S.Requests == null) return null;
            return S.Requests.Where(x => x != r && x.Kind == "invite" && x.State == "accepted" && Math.Abs(x.At - r.At) <= 30).OrderBy(x => Math.Abs(x.At - r.At)).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
        }

        /// <summary>Probe only: have this person come with an invitation right now.</summary>
        public Request ProbeOffer(Actor a) { if (S.Requests == null) S.Requests = new List<Request>(); var r = MakeInvite(a, S.R(Stream.Presentation)); if (r != null) { VoiceRequest(a, r); S.Requests.Add(r); } return r; }

        Request MakeInvite(Actor a, Rng rng)
        {
            var acts = a.Def.Hobbies.Select(h => Activities.HobbyToActivity.TryGetValue(h, out var x) ? x : null).Where(x => x != null && x != "cook" && Activities.Get(x) != null && InviteWord.ContainsKey(x)).Distinct().ToList();
            if (acts.Count == 0) acts = new List<string> { "tea", "walk" };
            var aid = acts[rng.R(acts.Count)]; var def = Activities.Get(aid); if (def == null) return null;
            var rooms = S.Layout.Rooms.Where(r => def.Rooms.Contains(r.Type) && RoomUsable(a, r)).OrderBy(r => r.Id).ToList(); if (rooms.Count == 0) return null;
            var room = rooms[rng.R(rooms.Count)];
            // a real plan, an hour or two ahead, on a round ten minutes ("오후 3시 20분에 온실에서"): time to go about the day and come back
            double at = Math.Ceiling((S.Clock + rng.Range(60f, 120f)) / 10.0) * 10.0;
            bool pol = PoliteToPlayer(a.Id); string what = InviteWord[aid]; string when = ClockFmt.Mark(at, true);
            return new Request
            {
                Id = S.NewId("rq"), From = a.Id, Kind = "invite", Activity = aid, Room = room.Id, At = at, Made = S.Clock, Due = at + 25,
                Text = LineBank.FixParticles(pol ? $"{when}에 {room.Name}에서 같이 {what} 어떠세요? 시간 되시면요." : $"{when}에 {room.Name}에서 같이 {what} 어때? 시간 되면.")
            };
        }

        Request MakeFind(Actor a)
        {
            var bed = S.Layout.BedroomOf(a.Id);
            var lost = S.Items.Values.Where(i => i.Owner == a.Id && i.Holder == null && i.Def != null && !i.Def.Key && i.Type != "Invitation" && !i.Hidden && !i.Bloody && i.Room >= 0 && (bed == null || i.Room != bed.Id)).OrderBy(i => i.Id).FirstOrDefault();
            if (lost == null) return null;   // the thing must really be missing somewhere in the house (nothing is conjured for a quest)
            bool pol = PoliteToPlayer(a.Id);
            return new Request
            {
                Id = S.NewId("rq"), From = a.Id, Kind = "find", Item = lost.Id, Made = S.Clock, Due = S.Clock + 24 * 60,
                Text = LineBank.FixParticles(pol ? $"제 {lost.Def.Kor}이(가) 안 보여요. 어디 두고 왔나 봐요… 혹시 보시면 가져다주실래요?" : $"내 {lost.Def.Kor} 못 봤어? 어디 두고 온 것 같은데, 보이면 좀 가져다줘.")
            };
        }

        Request MakeDeliver(Actor a, Rng rng)
        {
            var to = S.LivingNpcs.Where(x => x != a && x.Status == ActorStatus.Active && S.R(a.Id, x.Id).Like > 0.15f).OrderBy(x => x.Id).ToList();
            if (to.Count == 0) return null;
            var t = to[rng.R(to.Count)];
            bool pol = PoliteToPlayer(a.Id);
            return new Request
            {
                Id = S.NewId("rq"), From = a.Id, Kind = "deliver", To = t.Id, Made = S.Clock, Due = S.Clock + 12 * 60,
                Text = LineBank.FixParticles(pol ? $"이 쪽지, {Cast.GivenOf(t.Id)} 씨에게 전해 주실 수 있어요? 직접 주기는 좀… 부끄러워서요." : $"이 쪽지 {Cast.GivenOf(t.Id)}한테 좀 전해 줄래? 직접 주기는 좀 그래서.")
            };
        }

        /// <summary>민혁's answer to an offered request: "accept", "later" (invite: an hour later) or "refuse". Returns what is said.</summary>
        public List<Utterance> AnswerRequest(Actor npc, string answer)
        {
            var res = new List<Utterance>(); var r = OfferFrom(npc); if (r == null) return res;
            bool pol = PoliteToPlayer(npc.Id);
            Utterance Line(string s, Emotion e = Emotion.Neutral) => new Utterance { Speaker = npc.Id, Listener = Cast.Player, Key = "request", Text = LineBank.FixParticles(s), Emotion = e, Gesture = Anim.Talk };
            if (answer == "refuse")
            {
                r.State = "refused";
                res.Add(Line(pol ? "아, 알겠어요. 다음에 또 여쭤볼게요." : "그래, 알았어. 다음에."));
                S.K(npc.Id).Facts.Add("refused:" + r.Id);
                VoiceAnswer(npc, r, res);   // voice pack (Content/Voice/VoiceHooks.cs)
                return res;
            }
            if (answer == "later" && r.Kind == "invite") { r.At += 60; r.Due += 60; }
            r.State = "accepted";
            switch (r.Kind)
            {
                case "invite":
                    res.Add(Line(pol ? $"좋아요. 그럼 {ClockFmt.Mark(r.At, true)}에 {S.RoomName(r.Room)}에서 봬요." : $"좋아. 그럼 {ClockFmt.Mark(r.At, true)}에 {S.RoomName(r.Room)}에서 보자.", Emotion.Smile));
                    break;
                case "find":
                    res.Add(Line(pol ? "고마워요. 급한 건 아니니까 보이면요." : "고마워. 급한 건 아니니까 보이면.", Emotion.Smile));
                    break;
                case "deliver":
                    {
                        var note = new Item { Id = S.NewId("it"), Type = "Envelope", Name = $"{Cast.GivenOf(r.From)}의 쪽지 ({Cast.GivenOf(r.To)} 앞)", Owner = r.To, Holder = Cast.Player, Room = P.Room, Pos = P.Pos, Note = $"{Cast.GivenOf(r.To)}에게. — {Cast.GivenOf(r.From)}" };
                        S.Items[note.Id] = note; r.Item = note.Id;
                        S.Emit(GameEventType.ItemMoved, Cast.Player, data: note.Id, text: "spawn", pos: P.Pos);
                        res.Add(Line(pol ? "고마워요. 읽지는 마시고요." : "고마워. 읽지는 말고.", Emotion.Smile));
                        break;
                    }
            }
            Relations.Change(S, npc.Id, Cast.Player, like: 0.02f, memory: "부탁을 들어주겠다고 했다");
            VoiceAnswer(npc, r, res);   // voice pack (Content/Voice/VoiceHooks.cs)
            return res;
        }

        /// <summary>An item handed to its owner closes a find or a delivery. Returns the thanks, or null when no request matched.</summary>
        public Utterance RequestHandover(Actor npc, Item it)
        {
            var r = S.Requests?.FirstOrDefault(x => x.State == "accepted" && x.Item == it.Id && ((x.Kind == "find" && x.From == npc.Id) || (x.Kind == "deliver" && x.To == npc.Id)));
            if (r == null) return null;
            r.State = "done"; bool pol = PoliteToPlayer(npc.Id);
            if (r.Kind == "find")
            {
                Relations.Change(S, npc.Id, Cast.Player, like: 0.08f, trust: 0.05f, memory: "잃어버린 물건을 찾아 줬다");
                S.Log("RequestDone", npc.Id, Cast.Player, data: "find");
                return VoiceThanks(npc, r, new Utterance { Speaker = npc.Id, Listener = Cast.Player, Key = "request", Text = pol ? "찾아 주셨네요! 정말 고마워요. 이 은혜는 꼭 갚을게요." : "찾았구나! 진짜 고마워. 이건 빚진 걸로 할게.", Emotion = Emotion.Smile, Gesture = Anim.Bow });   // voice pack
            }
            Relations.Change(S, r.From, Cast.Player, like: 0.05f, trust: 0.06f, memory: "쪽지를 잘 전해 줬다");
            Relations.Change(S, npc.Id, r.From, like: 0.05f, memory: Cast.GivenOf(r.From) + "에게서 쪽지를 받았다");
            S.Log("RequestDone", npc.Id, Cast.Player, data: "deliver:" + r.From);
            return VoiceThanks(npc, r, new Utterance { Speaker = npc.Id, Listener = Cast.Player, Key = "request", Text = LineBank.FixParticles(pol ? $"{Cast.GivenOf(r.From)} 씨가요? …고마워요, 전해 줘서." : $"{Cast.GivenOf(r.From)}이(가)? …고마워, 전해 줘서."), Emotion = Emotion.Surprised, Gesture = Anim.Talk });   // voice pack
        }
    }
}
