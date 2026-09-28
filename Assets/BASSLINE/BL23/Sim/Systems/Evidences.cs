using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>Evidence cards. Each card keeps acquisition, time window, place, source, root (provenance) and scope
    /// ("알 수 있는 것 / 알 수 없는 것"). Cards are per owner; nobody gets another person's card without a transfer.
    ///
    /// The player's notebook follows two rules (clues v2-lite): a card is a thing — one card per body, scene mark, notable
    /// object and record device, and one sheet per witness per chapter that grows as the witness says more — and looking is
    /// not filing: a look that turns up nothing notable is returned as a Loose caption and never stored. NPC cards keep the
    /// original behaviour byte for byte.</summary>
    public static class Evidences
    {
        public static Evidence Add(Simulation sim, string owner, EvKind kind, string title, string desc, string source, string root, double t0, double t1, int room, string can, string cannot, bool direct = true, params Prop[] props)
            => File(sim, owner, kind, title, desc, source, root, t0, t1, room, can, cannot, direct, true, null, false, props);

        /// <summary>Same as <see cref="Add"/> but files silently (the notebook still gets the card; no toast).</summary>
        public static Evidence AddQuiet(Simulation sim, string owner, EvKind kind, string title, string desc, string source, string root, double t0, double t1, int room, string can, string cannot, bool direct = true, params Prop[] props)
            => File(sim, owner, kind, title, desc, source, root, t0, t1, room, can, cannot, direct, false, null, false, props);

        // ------------------------------------------------------------------ filing
        static readonly string[] StampedRoots = { "furn:", "door:", "dev:", "mdev:", "devlog:", "clockoff:" };

        /// <summary>The identity of the thing a card is about: re-looking at the same furniture/door/device later is the same card.</summary>
        public static string RootKey(string root)
        {
            if (string.IsNullOrEmpty(root)) return root ?? "";
            foreach (var pre in StampedRoots)
                if (root.StartsWith(pre, StringComparison.Ordinal))
                {
                    int i = root.LastIndexOf(':'); if (i <= pre.Length - 1) return root;
                    string tail = root.Substring(i + 1); if (tail.Length > 0 && tail.All(char.IsDigit) && root.IndexOf(':', pre.Length) == i) return root.Substring(0, i);
                    return root;
                }
            if (root.StartsWith("item:", StringComparison.Ordinal))
            {
                foreach (var suf in new[] { ":bw", ":b", ":w", ":" }) if (root.EndsWith(suf, StringComparison.Ordinal) && root.Length > 5 + suf.Length) return root.Substring(0, root.Length - suf.Length);
                return root;
            }
            return root;
        }

        /// <summary>Two cards about the same body: the NPC's own seen/exam roots carry the owner, so compare by victim.</summary>
        static string BodyFamily(string root)
        {
            if (root == null) return null;
            if (root.StartsWith("bodyexam:", StringComparison.Ordinal)) { var p = root.Split(':'); return p.Length > 1 ? "exam:" + p[1] : null; }
            if (root.StartsWith("body:", StringComparison.Ordinal)) { var p = root.Split(':'); return p.Length > 1 ? "seen:" + p[1] : null; }
            if (root.StartsWith("official:", StringComparison.Ordinal)) { var p = root.Split(':'); return p.Length > 1 ? "official:" + p[1] : null; }
            return null;
        }

        /// <summary>Same thing, as far as the notebook is concerned (same RootKey, or the same body's same kind of look).</summary>
        internal static bool SameThing(string rootA, string rootB)
        {
            if (RootKey(rootA) == RootKey(rootB)) return true;
            var fa = BodyFamily(rootA); return fa != null && fa == BodyFamily(rootB);
        }

        static string PropKey(Prop p) => $"{p.Kind}|{p.A}|{p.B}|{p.Room}|{p.Item}|{p.Value}|{Math.Round(p.T0)}";
        static bool CurrentState(PropKind k) => k == PropKind.DoorState || k == PropKind.ItemState || k == PropKind.ItemAt || k == PropKind.ClockOffset;

        internal static Evidence File(Simulation sim, string owner, EvKind kind, string title, string desc, string source, string root, double t0, double t1, int room, string can, string cannot, bool direct, bool emit, string line, bool copy, params Prop[] props)
        {
            var S = sim.S; var k = S.K(owner); title = LineBank.FixParticles(title); desc = LineBank.FixParticles(desc);
            if (owner != Cast.Player)
            {
                // NPC notebooks: unchanged — same root + same title = same card (re-reading the same record is not a new source)
                var existing = k.Evidence.FirstOrDefault(e => e.Root == root && e.Title == title);
                if (existing != null) { existing.Desc = desc; existing.T0 = t0; existing.T1 = t1; return existing; }
                var ev0 = new Evidence { Id = S.NewId("ev"), Owner = owner, Kind = kind, Title = title, Desc = desc, Source = source, Direct = direct, Root = root, T0 = t0, T1 = t1, Room = room, CanKnow = can, CannotKnow = cannot, Acquired = S.Clock, Loop = S.Loop, Chapter = S.Chapter, Copy = copy };
                ev0.Props.AddRange(props.Where(p => p != null));
                k.Evidence.Add(ev0);
                return ev0;
            }
            line = LineBank.FixParticles(line);
            // a clock that reads right is not a clue
            var incoming = props.Where(p => p != null && !(p.Kind == PropKind.ClockOffset && (p.Value == "0" || p.Value == "-0"))).ToList();
            string rk = RootKey(root);
            var hit = k.Evidence.Where(e => e.Loop == S.Loop && e.Chapter == S.Chapter && e.Direct == direct && e.Copy == copy && RootKey(e.Root) == rk).OrderBy(e => e.Hidden ? 1 : 0).FirstOrDefault();
            Evidence ev;
            if (hit != null)
            {
                ev = hit;
                var before = new HashSet<string>(ev.Props.Where(p => CaseBoard.Sentence(sim, p) != null).Select(p => CaseBoard.Sentence(sim, p)));
                ev.Desc = desc; ev.T0 = t0; ev.T1 = t1; ev.Room = room; if (line != null) ev.Line = line;
                // what is true now replaces what was true then (a door, an object's state, a clock)
                var newKeys = new HashSet<string>(incoming.Select(PropKey));
                foreach (var p in incoming.Where(p => CurrentState(p.Kind)).ToList())
                    ev.Props.RemoveAll(q => q.Kind == p.Kind && q.Item == p.Item && !newKeys.Contains(PropKey(q)));
                // a second look at a body keeps the first estimate (the notebook doesn't jump; one window, not two)
                if (ev.Props.Any(q => q.Kind == PropKind.DeathWindow && q.Value == "exam"))
                {
                    var keep = ev.Props.First(q => q.Kind == PropKind.DeathWindow && q.Value == "exam");
                    incoming.RemoveAll(p => p.Kind == PropKind.DeathWindow && p.Value == "exam");
                    if (kind == EvKind.Body) { ev.T0 = keep.T0; ev.T1 = keep.T1; }
                }
                var have = new HashSet<string>(ev.Props.Select(PropKey));
                bool news = false;
                foreach (var p in incoming)
                {
                    if (!have.Add(PropKey(p))) continue;
                    ev.Props.Add(p);
                    var s = CaseBoard.Sentence(sim, p); if (s != null && !before.Contains(s)) news = true;
                }
                if (news && emit && !ev.Hidden) S.Emit(GameEventType.Evidence, owner, text: CaseBoard.Describe(sim, ev).Title, data: ev.Id, key: "update");
            }
            else
            {
                ev = new Evidence { Id = S.NewId("ev"), Owner = owner, Kind = kind, Title = title, Desc = desc, Source = source, Direct = direct, Root = root, T0 = t0, T1 = t1, Room = room, CanKnow = can, CannotKnow = cannot, Acquired = S.Clock, Loop = S.Loop, Chapter = S.Chapter, Copy = copy, Line = line };
                var seen = new HashSet<string>();
                foreach (var p in incoming) if (seen.Add(PropKey(p))) ev.Props.Add(p);
                k.Evidence.Add(ev);
                if (emit && !(root != null && root.StartsWith("official:", StringComparison.Ordinal))) S.Emit(GameEventType.Evidence, owner, text: CaseBoard.Describe(sim, ev).Title, data: ev.Id, key: "new");
            }
            // a direct look supersedes what was only heard about the same thing
            if (direct && !copy)
                foreach (var c in k.Evidence.Where(e => e.Copy && !e.Hidden && e.Loop == S.Loop && e.Chapter == S.Chapter && RootKey(e.Root) == rk)) c.Hidden = true;
            // a body's seen card, exam and the official notice are one card
            if (root != null && (root.StartsWith("official:", StringComparison.Ordinal)) && ev.Props.Any(p => p.A != null)) FoldBody(sim, ev.Props.First(p => p.A != null).A);
            return ev;
        }

        // ------------------------------------------------------------------ what's worth writing down
        /// <summary>
        /// Not every sentence is a clue. Outside an investigation only plainly suspicious things are written down (blood, a
        /// disguise, an injury, something missing, an accusation). During one, what bears on the case is: anything about the
        /// victim, and anything about where people were around the time of death — judged on the public window (from a few
        /// hours before discovery, widened by the player's own death estimate), never on the true time of death.
        /// </summary>
        public static bool Worthwhile(Simulation sim, Prop p)
        {
            var S = sim.S; if (p == null) return false;
            switch (p.Kind) { case PropKind.Culprit: case PropKind.Bloodied: case PropKind.Disguised: case PropKind.Injured: case PropKind.ItemMissing: return true; }
            if (S.Phase != Phase.Investigation && S.Phase != Phase.Assembly && S.Phase != Phase.Trial) return false;
            var inc = CaseProgress.Current(S); if (inc == null) return false;
            if (p.A == inc.Victim || p.B == inc.Victim) return true;
            double found = Testimony.CaseWindow(S).t1; var known = CaseBoard.KnownWindow(sim);
            double t0 = Math.Min(found - 210, known.t0 - 60), t1 = inc.ConfirmClock + 10;
            return p.T1 >= t0 && p.T0 <= t1;
        }

        /// <summary>Clues that stand on their own even when merely overheard: accusations, blood, disguises, injuries, missing
        /// things, and anything said about the victim.</summary>
        static bool Strong(Simulation sim, Prop p)
        {
            switch (p.Kind) { case PropKind.Culprit: case PropKind.Bloodied: case PropKind.Disguised: case PropKind.Injured: case PropKind.ItemMissing: return true; }
            var inc = CaseProgress.Current(sim.S); return inc != null && (p.A == inc.Victim || p.B == inc.Victim);
        }

        /// <summary>When an investigation opens, what was heard earlier that now matters is recalled into the witnesses' sheets (silently).</summary>
        public static void RecallRelevant(Simulation sim, string who)
        {
            var S = sim.S; var k = S.K(who);
            // only this chapter's talk: what someone said about the new victim in an earlier case belongs to that case
            foreach (var st in k.Statements.ToList()) if (st.Prop != null && st.Speaker != who && st.Clock >= S.Ch.ChapterStartClock && Worthwhile(sim, st.Prop)) FromStatement(sim, who, st.Speaker, st.Prop, st.Text, st.Hearsay, false);
        }

        public static void FromStatement(Simulation sim, string who, string from, Prop p, string text, bool overheard) => FromStatement(sim, who, from, p, text, overheard, true);

        /// <summary>One sheet per witness per chapter: every worthwhile thing they say becomes a bullet on it. The same fact said
        /// again (another hour, another retelling) widens the bullet it already has.</summary>
        static void FromStatement(Simulation sim, string who, string from, Prop p, string text, bool overheard, bool emit)
        {
            var S = sim.S;
            if (!Worthwhile(sim, p)) return;
            // overheard chatter about where people were stays in memory (the trial still knows it) but is not filed: only what
            // concerns the victim, or plainly suspicious things, earn a bullet when it was not said to you
            if (overheard && !Strong(sim, p)) return;
            var k = S.K(who);
            var sheet = k.Evidence.FirstOrDefault(e => e.Kind == EvKind.Testimony && e.Subject == from && !e.Copy && e.Loop == S.Loop && e.Chapter == S.Chapter);
            bool created = false;
            if (sheet == null)
            {
                sheet = File(sim, who, EvKind.Testimony, $"{Cast.GivenOf(from)}의 증언", "", Cast.NameOf(from), $"wit:{from}:{S.Loop}:{S.Chapter}", p.T0, p.T1, p.Room,
                    "그 사람이 이렇게 말했다는 사실", "그 말이 사실인지 — 다른 근거로 확인해야 한다", false, false, null, false);
                sheet.Subject = from; created = true;
            }
            var ctx = CaseBoard.Context(sim);
            bool hadNotable = !created && sheet.Props.Any(q => CaseBoard.NotableBullet(ctx, q, from));
            var c = p.Clone();
            string fk = CaseBoard.FactKeyNoTime(c);
            var same = sheet.Props.FirstOrDefault(q => CaseBoard.FactKeyNoTime(q) == fk && c.T0 <= q.T1 + 45 && c.T1 >= q.T0 - 45);
            bool added = false;
            if (same != null) { same.T0 = Math.Min(same.T0, c.T0); same.T1 = Math.Max(same.T1, c.T1); }
            else { sheet.Props.Add(c); added = true; }
            // what they said, in their words (a rendered line may carry page breaks)
            string q0 = string.Join(" ", LineBank.Pages(text));
            if (q0.Length > 0 && !sheet.Quotes.Contains(q0)) { sheet.Quotes.Add(q0); while (sheet.Quotes.Count > 8) sheet.Quotes.RemoveAt(0); }
            sheet.Desc = string.Join("\n", sheet.Quotes);
            sheet.T0 = sheet.Props.Min(q => q.T0); sheet.T1 = sheet.Props.Max(q => q.T1);
            var top = sheet.Props.OrderByDescending(q => CaseBoard.BulletScore(ctx, q, from)).ThenBy(q => q.T0).FirstOrDefault(); if (top != null && top.Room >= 0) sheet.Room = top.Room;
            if (!emit || !added || !CaseBoard.NotableBullet(ctx, c, from)) return;
            S.Emit(GameEventType.Evidence, who, text: $"{Cast.GivenOf(from)}의 증언", data: sheet.Id, key: hadNotable ? "update" : "new");
        }

        /// <summary>Something another person found, passed on to the player: a hearsay copy (Copy, never Direct) unless the player
        /// already has a card about that thing — then only who shared it is noted. <paramref name="fromGiven"/> is the giver's actor id.</summary>
        public static Evidence Relay(Simulation sim, Evidence src, string fromGiven)
        {
            var S = sim.S; var k = S.K(Cast.Player); if (src == null) return null;
            var have = k.Evidence.FirstOrDefault(e => e.Loop == S.Loop && e.Chapter == S.Chapter && SameThing(e.Root, src.Root));
            if (have != null) { if (fromGiven != null && !have.SharedWith.Contains(fromGiven)) have.SharedWith.Add(fromGiven); return have; }
            var d = CaseBoard.Describe(sim, src);
            string given = Cast.GivenOf(fromGiven) ?? fromGiven ?? "누군가";
            var ev = File(sim, Cast.Player, src.Kind, d.Title, src.Desc, $"{given}에게 들음", src.Root, src.T0, src.T1, src.Room, src.CanKnow, "직접 보지 않았다 — " + src.CannotKnow, false, false, d.Line, true, src.Props.Select(p => p.Clone()).ToArray());
            ev.Subject = src.Subject; ev.TraceId = src.TraceId;
            if (fromGiven != null && !ev.SharedWith.Contains(fromGiven)) ev.SharedWith.Add(fromGiven);
            // a body the player already looked at: the copy adds nothing
            if (src.Kind == EvKind.Body && src.Subject != null) FoldBody(sim, src.Subject);
            if (!ev.Hidden) S.Emit(GameEventType.Evidence, Cast.Player, text: CaseBoard.Describe(sim, ev).Title, data: ev.Id, key: "new");
            return ev;
        }

        /// <summary>One visible card per body: the player's seen card, exam and official notice fold into one (exam first).</summary>
        public static Evidence FoldBody(Simulation sim, string victim)
        {
            var S = sim.S; var k = S.K(Cast.Player); if (victim == null) return null;
            var incIds = S.Incidents.Values.Where(i => i.Victim == victim && i.Loop == S.Loop).Select(i => "official:" + i.Id).ToList();
            bool Official(string r) => r != null && incIds.Any(x => r == x || r.StartsWith(x + ":", StringComparison.Ordinal));
            var mine = k.Evidence.Where(e => e.Loop == S.Loop && e.Chapter == S.Chapter && e.Direct && !e.Copy).ToList();
            var exam = mine.FirstOrDefault(e => e.Root == "bodyexam:" + victim + ":" + Cast.Player);
            var seen = mine.FirstOrDefault(e => e.Root == "body:" + victim + ":" + Cast.Player);
            var off = mine.Where(e => Official(e.Root)).OrderBy(e => e.Root.Length).ThenBy(e => e.Root, StringComparer.Ordinal).ToList();
            var primary = exam ?? seen ?? off.FirstOrDefault(); if (primary == null) return null;
            var have = new HashSet<string>(primary.Props.Select(PropKey));
            foreach (var o in new[] { exam, seen }.Concat(off).Where(e => e != null && e != primary))
            {
                foreach (var p in o.Props) if (have.Add(PropKey(p))) primary.Props.Add(p.Clone());
                o.Hidden = true;
            }
            primary.Hidden = false; primary.Subject = victim; primary.Important = true;
            if (exam == null) primary.Line = $"{S.RoomName(FoundRoomOf(S, victim, primary.Room))}에서 발견됐다. 아직 자세히 살펴보지 않았다.";
            // what others told the player about this body adds nothing once the player has looked for themselves
            foreach (var c in k.Evidence.Where(e => e.Copy && !e.Hidden && e.Loop == S.Loop && e.Chapter == S.Chapter))
            {
                var fam = BodyFamily(c.Root); if (fam == null) continue;
                bool covered = fam == "exam:" + victim ? exam != null : fam == "seen:" + victim ? exam != null || seen != null : Official(c.Root);
                if (covered) c.Hidden = true;
            }
            return primary;
        }

        static int FoundRoomOf(GameState S, string victim, int fallback)
        {
            var inc = S.Incidents.Values.Where(i => i.Victim == victim && i.Loop == S.Loop).OrderBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
            return inc != null && inc.FoundRoom >= 0 ? inc.FoundRoom : fallback;
        }

        /// <summary>One natural sentence for a proposition (empty for bookkeeping ones). Kept for older callers.</summary>
        public static string PropText(Simulation sim, Prop p) => CaseBoard.Sentence(sim, p) ?? "";

        // ------------------------------------------------------------------ bodies
        public static void BodySeenCard(Simulation sim, Actor o, Actor body, Sighting s)
        {
            if (!o.IsPlayer && !o.IsButler && o.Def.Obs < 60) return;
            var S = sim.S;
            string pose = body.Pose == Pose.LieFront ? "엎드린" : body.Pose == Pose.LieBack ? "누운" : body.Pose == Pose.Slumped ? "주저앉은" : "쓰러진";
            if (!o.IsPlayer)
            {
                string light = sim.RoomLight(body.Room) < 0.25f ? " 조명이 어두워 세부는 잘 보이지 않았다." : "";
                Add(sim, o.Id, EvKind.Body, $"쓰러져 있던 {Cast.NameOf(body.Id)}", $"{S.RoomName(body.Room)}에서 {pose} 채 발견됐다.{light}", "직접 목격", "body:" + body.Id + ":" + o.Id, S.Clock, S.Clock, body.Room,
                    "언제, 어디서, 어떤 자세로 발견됐는지", "어디서, 언제, 무엇 때문에 숨졌는지", true,
                    new Prop { Kind = PropKind.FoundPlace, A = body.Id, Room = body.Room, T0 = S.Clock, T1 = S.Clock });
                return;
            }
            var lines = new List<string> { $"{pose} 채 발견됐다" };
            if (sim.RoomLight(body.Room) < 0.25f) lines.Add("조명이 어두워 자세히 보이지 않았다");
            var prev = VisibleBody(S, body.Id);
            var ev = File(sim, o.Id, EvKind.Body, $"{Cast.GivenOf(body.Id)}의 {(body.Alive ? "상태" : "시신")}", string.Join("\n", lines), "직접 목격", "body:" + body.Id + ":" + o.Id, S.Clock, S.Clock, body.Room,
                "언제, 어디서, 어떤 자세로 발견됐는지", "어디서, 언제, 무엇 때문에 숨졌는지", true, false, body.Alive ? $"{pose} 채 움직이지 않는다 — 아직 숨이 붙어 있다." : null, false,
                new Prop { Kind = PropKind.FoundPlace, A = body.Id, Room = body.Room, T0 = S.Clock, T1 = S.Clock });
            if (body.Alive) { ev.Subject = body.Id; S.Emit(GameEventType.Evidence, o.Id, text: CaseBoard.Describe(sim, ev).Title, data: ev.Id, key: "new"); return; }   // someone down, not a body
            var primary = FoldBody(sim, body.Id) ?? ev;
            S.Emit(GameEventType.Evidence, o.Id, text: CaseBoard.Describe(sim, primary).Title, data: primary.Id, key: prev == null ? "new" : "update");
        }

        /// <summary>The player's visible card about this body, if any (the fold's primary).</summary>
        static Evidence VisibleBody(GameState S, string victim) =>
            S.K(Cast.Player).Evidence.FirstOrDefault(e => e.Loop == S.Loop && e.Chapter == S.Chapter && !e.Hidden && e.Direct && !e.Copy && e.Important && e.Subject == victim);

        public static void OfficialFile(Simulation sim, Actor a, Incident inc)
        {
            var S = sim.S; var v = S.A(inc.Victim);
            if (S.RuleActive("CH14")) { Rules.SplitOfficialFile(sim, a, inc); if (a.IsPlayer) FoldBody(sim, inc.Victim); return; }
            if (!a.IsPlayer)
            {
                Add(sim, a.Id, EvKind.Announcement, $"{Cast.NameOf(inc.Victim)}의 죽음을 알린 방송", $"{ClockFmt.Vague(inc.ConfirmClock, S.Clock)}, 종이 울리고 유스티가 {S.RoomName(inc.FoundRoom)}에서 {Cast.NameOf(inc.Victim)}의 죽음을 알렸다. 방송으로 알 수 있는 건 그때 이미 숨져 있었다는 사실뿐이다.",
                    "유스티 방송", "official:" + inc.Id, inc.ConfirmClock, inc.ConfirmClock, inc.FoundRoom, "종이 울릴 때 이미 숨져 있었다", "언제, 어떻게, 누구 손에 숨졌는지", true,
                    new Prop { Kind = PropKind.FoundPlace, A = inc.Victim, Room = inc.FoundRoom, T0 = inc.ConfirmClock, T1 = inc.ConfirmClock },
                    new Prop { Kind = PropKind.DeathWindow, A = inc.Victim, T0 = S.Ch.ChapterStartClock, T1 = inc.ConfirmClock, Value = "official" });
                return;
            }
            File(sim, a.Id, EvKind.Announcement, $"{Cast.GivenOf(inc.Victim)}의 시신", $"유스티가 {ClockFmt.Anchor(inc.ConfirmClock, S.Clock)} {S.RoomName(inc.FoundRoom)}에서 죽음을 알렸다",
                "유스티 방송", "official:" + inc.Id, inc.ConfirmClock, inc.ConfirmClock, inc.FoundRoom, "종이 울릴 때 이미 숨져 있었다", "언제, 어떻게, 누구 손에 숨졌는지", true, false, null, false,
                new Prop { Kind = PropKind.FoundPlace, A = inc.Victim, Room = inc.FoundRoom, T0 = inc.ConfirmClock, T1 = inc.ConfirmClock },
                new Prop { Kind = PropKind.DeathWindow, A = inc.Victim, T0 = S.Ch.ChapterStartClock, T1 = inc.ConfirmClock, Value = "official" });
            FoldBody(sim, inc.Victim);
        }

        /// <summary>Body examination. Detail depends on light, time spent, Obs, gore setting does not remove information.</summary>
        public static Evidence ExamineBody(Simulation sim, Actor ex, Actor body, bool detailed)
        {
            var S = sim.S; var k = S.K(ex.Id); k.Examined.Add("body:" + body.Id);
            var rng = S.R(Stream.Perception);
            float skill = ex.Def.Obs / 100f; if (ex.Ability == "StateSense") skill += 0.3f;
            float light = sim.RoomLight(body.Room); if (sim.Carried(ex).Any(i => i.Def?.Light == true)) light = Math.Max(light, 0.7f);
            var props = new List<Prop>();
            var lines = new List<string>();
            var visible = body.Body.Wounds.Where(w => w.Sev >= 2 || detailed && skill > 0.6f).ToList();
            if (light < 0.2f) visible = visible.Where(w => w.Sev >= 3).ToList();
            bool player = ex.IsPlayer;
            var pmNote = new Dictionary<Wound, bool>();   // player: postmortem wound → clearly read as postmortem?
            foreach (var w in visible)
            {
                // the same random draws in the same order for everyone (the Perception stream is shared)
                bool clear = w.Postmortem && skill + rng.F() * 0.3f > 0.8f;
                if (player) { if (w.Postmortem) pmNote[w] = clear; }
                else
                {
                    string pm = w.Postmortem ? (clear ? " — 피가 거의 번지지 않았다. 숨진 뒤에 생긴 상처로 보인다" : " — 다른 상처와 피가 난 모양이 다르다") : "";
                    lines.Add("· " + (Violence.Phrase(w) != null ? WoundText.Region(w.Region) + "에 " + Violence.Phrase(w) + " (" + WoundText.Sev(w.Sev) + ")" : w.Type == DamageType.Choke && w.Region == BodyRegion.Head ? "얼굴을 짓눌러 숨을 막은 흔적" : WoundText.Region(w.Region) + "에 " + WoundText.Type(w.Type) + " (" + WoundText.Sev(w.Sev) + ")") + pm);   // a pillow, not a cord (Methods); (violence track: a gunshot / bolt / grip reads as what it is)
                }
                var wp = new Prop { Kind = PropKind.Wound, A = body.Id, Value = WoundText.Region(w.Region) + "에 " + WoundText.Type(w.Type), Item = w.Type.ToString() };
                if (!player || !props.Any(q => q.Kind == PropKind.Wound && q.Value == wp.Value)) props.Add(wp);
            }
            var main = body.Body.Wounds.Where(w => !w.Postmortem).OrderByDescending(w => w.Sev).FirstOrDefault();
            if (main != null && visible.Contains(main)) props.Add(new Prop { Kind = PropKind.WeaponType, A = body.Id, Value = main.Type.ToString() });
            if (body.Body.Wounds.Count == 0 && !player) lines.Add("· 눈에 띄는 외상이 없다");
            // time since death estimate (window width shrinks with skill; never exact)
            double death = body.Body.DeathClock;
            double width = Math.Max(12, 70 - skill * 50); if (ex.Ability == "StateSense") width = 10;
            double center = death + body.Body.TodShift + rng.Range(-(float)width * 0.2f, (float)width * 0.2f);
            double t0 = center - width / 2, t1 = Math.Min(S.Clock, center + width / 2);
            if (t0 > t1 - 6) t0 = t1 - Math.Max(6, width * 0.5);   // a warmed body can read "later than now": keep the window ordered
            if (!player) lines.Add($"· 체온과 몸이 굳은 정도로 보아, 숨진 시각은 {ClockFmt.VagueRange(t0, t1, S.Clock)}(으)로 짐작된다");
            props.Add(new Prop { Kind = PropKind.DeathWindow, A = body.Id, T0 = t0, T1 = t1, Value = "exam" });
            var extra = new List<string>();   // player: wet / moved (after the set-piece notes)
            if (body.Wet) { if (player) extra.Add("머리와 옷이 젖어 있다"); else lines.Add("· 머리와 옷이 젖어 있다"); props.Add(new Prop { Kind = PropKind.Wet, A = body.Id, T0 = S.Clock, T1 = S.Clock }); }
            var inc = S.Incidents.Values.FirstOrDefault(i => i.Victim == body.Id && i.Loop == S.Loop);
            if (inc != null && inc.BodyMoved && (skill + rng.F() * 0.4f > 0.75f || S.Traces.Any(t => t.Type == "DragMark" && t.Victim == body.Id && t.Room == body.Room)))
            { if (player) extra.Add("핏자국이 흐른 방향이 지금 자세와 맞지 않는다 — 누가 옮겼을 수 있다"); else lines.Add("· 몸에 남은 핏자국이 흐른 방향이 지금 자세와 맞지 않는다 — 누가 옮겼을 수 있다"); props.Add(new Prop { Kind = PropKind.BodyMoved, A = body.Id, Value = "likely" }); }
            else props.Add(new Prop { Kind = PropKind.BodyMoved, A = body.Id, Value = "unknown" });
            var notes = player ? new List<string>() : lines;
            SetPieces.BodyNotes(sim, ex, body, skill, rng, notes, props);
            bool defence = body.Body.Wounds.Any(w => w.Region == BodyRegion.HandL || w.Region == BodyRegion.HandR || w.Region == BodyRegion.ArmL || w.Region == BodyRegion.ArmR) && skill > 0.5f;
            if (defence && !player) lines.Add("· 팔과 손에 막으려다 생긴 듯한 상처가 있다 — 저항했던 것 같다");
            var pocket = body.Pocket.Select(S.I).Where(i => i != null).Select(i => i.Kor).ToList();
            bool noKey = detailed && !pocket.Any(p => p.Contains("열쇠")) && S.Layout.BedroomOf(body.Id) != null;
            if (!player)
            {
                if (detailed) lines.Add(pocket.Count > 0 ? "· 소지품: " + string.Join(", ", pocket) : "· 주머니가 비어 있다");
                if (noKey) lines.Add("· 자기 방 열쇠가 보이지 않는다");
                string desc0 = string.Join("\n", lines);
                var ev0 = Add(sim, ex.Id, EvKind.Body, $"{Cast.NameOf(body.Id)}의 시신에 남은 흔적", desc0, "직접 조사", "bodyexam:" + body.Id + ":" + ex.Id, t0, t1, body.Room,
                    "어디에 어떤 상처가 있는지, 대략 언제 숨졌는지", "누가 했는지, 정확히 몇 시에 어디서 숨졌는지(발견된 곳과 다를 수 있다)", true, props.ToArray());
                ev0.Subject = body.Id; ev0.Important = true;
                Testimony.UpdateSuspicion(sim, ex);
                return ev0;
            }
            // ---- the player's card: a hedged headline, then the death estimate, what the set pieces show, grouped wounds, the rest
            bool alive = body.Alive;   // someone unconscious: no death estimate, not a body
            if (alive) props.RemoveAll(p => p.Kind == PropKind.DeathWindow || p.Kind == PropKind.BodyMoved);
            else
            {
                // looking again keeps the first estimate (the notebook doesn't jump between two windows)
                var first = k.Evidence.FirstOrDefault(e => e.Root == "bodyexam:" + body.Id + ":" + ex.Id && e.Loop == S.Loop && e.Chapter == S.Chapter)?.Props.FirstOrDefault(p => p.Kind == PropKind.DeathWindow && p.Value == "exam");
                if (first != null) { var nw = props.Last(p => p.Kind == PropKind.DeathWindow); nw.T0 = first.T0; nw.T1 = first.T1; t0 = first.T0; t1 = first.T1; }
            }
            var outl = new List<string> { alive ? null : CaseBoard.Sentence(sim, props.Last(p => p.Kind == PropKind.DeathWindow)) };
            outl.AddRange(notes.Select(Strip));
            outl.AddRange(WoundGroups(visible, pmNote));
            if (defence) outl.Add("팔과 손에 막으려다 생긴 듯한 상처가 있다");
            outl.AddRange(extra);
            if (detailed) outl.Add(pocket.Count > 0 ? "소지품: " + string.Join(", ", pocket) : "주머니가 비어 있다");
            if (noKey) outl.Add("자기 방 열쇠가 보이지 않는다");
            string headline = alive ? "숨은 붙어 있지만 의식이 없다."
                : props.Any(p => p.Kind == PropKind.TraceAt && p.A == body.Id && (p.Value == "poisoned" || p.Value == "dose-window")) ? "독에 중독된 흔적이 있다."
                : main != null && visible.Contains(main) ? CauseLine(main) : "눈에 띄는 외상이 없다.";
            var prev = VisibleBody(S, body.Id);
            var ev = File(sim, ex.Id, EvKind.Body, $"{Cast.GivenOf(body.Id)}의 {(alive ? "상태" : "시신")}", string.Join("\n", outl.Where(x => !string.IsNullOrEmpty(x))), "직접 조사", "bodyexam:" + body.Id + ":" + ex.Id, alive ? S.Clock : t0, alive ? S.Clock : t1, body.Room,
                "어디에 어떤 상처가 있는지, 대략 언제 숨졌는지", "누가 했는지, 정확히 몇 시에 어디서 숨졌는지(발견된 곳과 다를 수 있다)", true, false, headline, false, props.ToArray());
            ev.Subject = body.Id; ev.Important = !alive;
            if (!alive) FoldBody(sim, body.Id);
            S.Emit(GameEventType.Evidence, ex.Id, text: CaseBoard.Describe(sim, ev).Title, data: ev.Id, key: prev == null ? "new" : "update");
            Testimony.UpdateSuspicion(sim, ex);
            return ev;
        }

        static string Strip(string s) { s = (s ?? "").Trim(); if (s.StartsWith("·")) s = s.Substring(1).TrimStart(); return s; }

        static string CauseLine(Wound w)
        {
            { var vc = Violence.CauseLine(w); if (vc != null) return vc; }   // --- violence track: shot / bolt / bare hands
            if (w.Type == DamageType.Choke && w.Region == BodyRegion.Head) return "얼굴이 짓눌려 숨이 막힌 것으로 보인다.";
            switch (w.Type)
            {
                case DamageType.Choke: return "목이 졸려 숨진 것으로 보인다.";
                case DamageType.Blunt: return "둔기에 맞아 숨진 것으로 보인다.";
                case DamageType.Cut: return "날붙이에 베여 숨진 것으로 보인다.";
                case DamageType.Stab: return "날붙이에 찔려 숨진 것으로 보인다.";
                case DamageType.Crush: return "무언가에 짓눌려 숨진 것으로 보인다.";
                case DamageType.Drown: return "물에 빠져 숨진 것으로 보인다.";
                case DamageType.Shock: return "감전되어 숨진 것으로 보인다.";
                case DamageType.Fall: return "높은 곳에서 떨어져 숨진 것으로 보인다.";
                case DamageType.Burn: return "불에 타 숨진 것으로 보인다.";
            }
            return "눈에 띄는 외상이 없다.";
        }

        static string WoundPhrase(DamageType t, BodyRegion r)
        {
            if (t == DamageType.Choke && r == BodyRegion.Head) return "짓눌린 자국";
            switch (t)
            {
                case DamageType.Cut: return "베인 상처"; case DamageType.Stab: return "찔린 상처"; case DamageType.Blunt: return "둔기로 맞은 자국";
                case DamageType.Crush: return "짓눌린 자국"; case DamageType.Drown: return "물을 들이켠 흔적"; case DamageType.Choke: return "목 졸린 자국";
                case DamageType.Shock: return "전기에 덴 자국"; case DamageType.Fall: return "떨어지며 부딪힌 자국"; case DamageType.Burn: return "화상";
            }
            return "상처";
        }

        static int RegionFamily(BodyRegion r)
        {
            switch (r) { case BodyRegion.ArmL: case BodyRegion.ArmR: case BodyRegion.HandL: case BodyRegion.HandR: return 1; case BodyRegion.LegL: case BodyRegion.LegR: case BodyRegion.FootL: case BodyRegion.FootR: return 2; }
            return 0;
        }

        /// <summary>"머리·왼어깨에 둔기로 맞은 자국 (3곳)" — one line per kind of wound and part of the body.</summary>
        static List<string> WoundGroups(List<Wound> visible, Dictionary<Wound, bool> pm)
        {
            var res = new List<string>();
            foreach (var g in visible.GroupBy(w => Violence.GroupKey(w, RegionFamily(w.Region)) is int vk && vk >= 0 ? vk : ((int)w.Type) * 10 + RegionFamily(w.Region)).OrderByDescending(g => g.Max(w => w.Sev)).ThenBy(g => g.Key))   // (violence track: its marks group by what made them)
            {
                var ws = g.ToList();
                var regions = ws.Select(w => WoundText.Region(w.Region)).Distinct().ToList();
                string phrase = Violence.Phrase(ws[0]) ?? WoundPhrase(ws[0].Type, ws[0].Region);
                string where = ws[0].Type == DamageType.Choke && ws[0].Region == BodyRegion.Head ? "얼굴" : string.Join("·", regions);
                string s = $"{where}에 {phrase}" + (ws.Count > 1 ? $" ({ws.Count}곳)" : "");
                var pms = ws.Where(w => w.Postmortem).ToList();
                if (pms.Count > 0) s += pms.Any(w => pm.TryGetValue(w, out var c) && c) ? " — 숨진 뒤에 생긴 상처로 보인다" : " — 다른 상처와 피가 난 모양이 다르다";
                res.Add(s);
            }
            return res;
        }

        // ------------------------------------------------------------------ scene marks, objects, doors
        public static Evidence ExamineTrace(Simulation sim, Actor ex, Trace t)
        {
            var S = sim.S; var k = S.K(ex.Id); k.Examined.Add("trace:" + t.Id);
            if (t.Cleaned && ex.Ability != "Afterglow") return null;
            double age = S.Clock - t.Clock; double w = Math.Max(10, age * 0.5);
            string fresh = age < 20 ? "아직 마르지 않았다" : age < 90 ? "거의 말랐다" : "완전히 말라 있다";
            var tlines = new List<string>(); var tprops = new List<Prop> { new Prop { Kind = PropKind.TraceAt, Room = t.Room, Value = t.Desc, T0 = S.Clock - age - w / 2, T1 = S.Clock - age + w / 2, Item = t.Type } };
            SetPieces.TraceNotes(sim, ex, t, tlines, tprops);
            if (!ex.IsPlayer)
            {
                string desc = t.Desc + (t.Type.StartsWith("Blood") ? $" — {fresh}." : ".") + (t.Blurred ? " 윤곽이 이상하게 흐릿하다." : "");
                if (tlines.Count > 0) desc += "\n" + string.Join("\n", tlines);
                var ev0 = Add(sim, ex.Id, EvKind.Trace, $"{t.Desc} ({S.RoomName(t.Room)})", desc, "직접 조사", "trace:" + t.Id, S.Clock - age - w / 2, S.Clock - age + w / 2, t.Room, t.Know, t.Unknown, true, tprops.ToArray());
                ev0.TraceId = t.Id;
                return ev0;
            }
            // the title already names the mark: the headline says what it tells (how fresh, what happened here)
            var lines = new List<string>();
            if (t.Type.StartsWith("Blood")) lines.Add(fresh);
            if (!string.IsNullOrEmpty(t.Know)) lines.Add(t.Know);
            if (t.Blurred) lines.Add("윤곽이 이상하게 흐릿하다");
            lines.AddRange(tlines.Select(Strip));
            if (lines.Count == 0) lines.Add(t.Desc);
            string head = lines[0].TrimEnd('.') + ".";
            // a card is a thing: the same kind of mark in the same room (a trail of footprints, drops of blood) is one card
            double w0 = S.Clock - age - w / 2, w1 = S.Clock - age + w / 2;
            var trail = S.K(ex.Id).Evidence.FirstOrDefault(e => e.Kind == EvKind.Trace && e.Direct && !e.Copy && e.Loop == S.Loop && e.Chapter == S.Chapter && e.Room == t.Room
                && e.TraceId != t.Id && e.Props.Any(p => p.Kind == PropKind.TraceAt && p.Item == t.Type && p.Value == t.Desc));
            string root = "trace:" + t.Id;
            if (trail != null) { root = trail.Root; w0 = Math.Min(w0, trail.T0); w1 = Math.Max(w1, trail.T1); }
            var ev = File(sim, ex.Id, EvKind.Trace, $"{t.Desc} ({S.RoomName(t.Room)})", string.Join("\n", lines), "직접 조사", root, w0, w1, t.Room, t.Know, t.Unknown, true, true, head, false, tprops.ToArray());
            if (ev.TraceId == null) ev.TraceId = t.Id;
            return ev;
        }

        static string Copula(string word) => word + (LineBank.Josa(word, "이") == "이" ? "이다" : "다");

        public static Evidence ExamineItem(Simulation sim, Actor ex, Item it)
        {
            var S = sim.S; var k = S.K(ex.Id); k.Examined.Add("item:" + it.Id);
            var lines = new List<string>(); var props = new List<Prop>();
            if (it.Bloody) { lines.Add("붉은 얼룩이 묻어 있다"); props.Add(new Prop { Kind = PropKind.ItemState, Item = it.Type, Value = "피 묻음", T0 = S.Clock, T1 = S.Clock }); }
            else if (it.Washed || it.Surface.Contains("residue")) { lines.Add("씻은 듯 젖어 있고, 이음새에 붉은 얼룩이 옅게 남아 있다"); props.Add(new Prop { Kind = PropKind.ItemState, Item = it.Type, Value = "세척 흔적", T0 = S.Clock, T1 = S.Clock }); }
            if (it.Damage >= 3) lines.Add("부서져 있다"); else if (it.Damage > 0) lines.Add("흠집이 있다");
            if (it.Hidden) { lines.Add("눈에 띄지 않는 곳에 숨겨져 있었다"); props.Add(new Prop { Kind = PropKind.ItemState, Item = it.Type, Value = "숨겨짐", Room = it.Room, T0 = S.Clock, T1 = S.Clock }); }
            if (it.HomeRoom >= 0 && it.Room != it.HomeRoom && it.Holder == null) { lines.Add($"원래 {S.RoomName(it.HomeRoom)}에 있던 물건이다"); props.Add(new Prop { Kind = PropKind.ItemAt, Item = it.Type, Room = it.Room, T0 = S.Clock, T1 = S.Clock, Value = "moved" }); }
            if (it.KeyFor != null && it.Owner != null) lines.Add($"{Cast.GivenOf(it.Owner)}의 방 열쇠");
            if (it.ParentItem != null) lines.Add($"{S.I(it.ParentItem)?.Kor ?? "다른 물건"}에서 떨어져 나온 조각");
            Tricks.ExamineItem(sim, ex, it, lines, props);
            SetPieces.ItemNotes(sim, ex, it, lines, props);
            if (!ex.IsPlayer)
            {
                if (lines.Count == 0) lines.Add("특별히 이상한 점은 없다");
                return Add(sim, ex.Id, EvKind.ObjectState, $"{it.Kor} ({S.RoomName(it.Room)})", string.Join(". ", lines) + ".", "직접 조사", "item:" + it.Id + ":" + (it.Bloody ? "b" : "") + (it.Washed ? "w" : ""), S.Clock, S.Clock, it.Room,
                    "이 물건이 지금 어디에 어떤 상태로 있는지", "누가 언제 썼는지, 어쩌다 이렇게 됐는지", true, props.ToArray());
            }
            // looking is not filing: a moved cup is not a clue, a moved key or weapon is
            bool movedOnly = props.Count > 0 && props.All(p => p.Kind == PropKind.ItemAt && p.Value == "moved") && it.Def?.Key != true && it.Def?.IsWeapon != true && it.KeyFor == null;
            bool notable = (props.Count > 0 && !movedOnly) || it.Damage > 0 || it.ParentItem != null || it.KeyFor != null
                || (S.Phase == Phase.Investigation && it.Def?.IsWeapon == true && CaseBoard.SceneRooms(sim).Contains(it.Room));
            if (!notable) return new Evidence { Loose = true, Kind = EvKind.ObjectState, Title = it.Kor, Line = "특별히 눈에 띄는 점은 없다.", Owner = ex.Id, Room = it.Room, T0 = S.Clock, T1 = S.Clock, Root = "item:" + it.Id };
            if (lines.Count == 0) lines.Add($"{S.RoomName(it.Room)}에 있다");
            var clean = lines.Select(Strip).Where(x => x.Length > 0).ToList();
            return File(sim, ex.Id, EvKind.ObjectState, it.Kor, string.Join("\n", clean), "직접 조사", "item:" + it.Id, S.Clock, S.Clock, it.Room,
                "이 물건이 지금 어디에 어떤 상태로 있는지", "누가 언제 썼는지, 어쩌다 이렇게 됐는지", true, true, clean[0].TrimEnd('.') + ".", false, props.ToArray());
        }

        public static void ExamineRoomQuick(Simulation sim, Actor ex, int room)
        {
            var S = sim.S;
            // weapons missing from their home spots
            foreach (var it in S.Items.Values.Where(i => i.HomeRoom == room && i.Def != null && i.Def.IsWeapon))
            {
                if (it.Room == room && it.Holder == null) continue;
                if (!ex.IsPlayer)
                {
                    Add(sim, ex.Id, EvKind.ObjectState, $"{S.RoomName(room)}에서 없어진 {it.Def.Kor}", $"{S.RoomName(room)}에 있어야 할 {it.Def.Kor} 하나가 보이지 않는다.", "직접 조사", "missing:" + it.Id + ":" + room, S.Ch.ChapterStartClock, S.Clock, room,
                        "지금 제자리에 없다는 것", "누가 언제 가져갔는지", true, new Prop { Kind = PropKind.ItemMissing, Item = it.Type, Room = room, T0 = S.Ch.ChapterStartClock, T1 = S.Clock });
                    continue;
                }
                string kor = it.Def.Kor;
                File(sim, ex.Id, EvKind.ObjectState, $"없어진 {kor}", $"{S.RoomName(room)}에 있어야 할 {kor}{LineBank.Josa(kor, "이")} 보이지 않는다.", "직접 조사", "missing:" + it.Id + ":" + room, S.Ch.ChapterStartClock, S.Clock, room,
                    "지금 제자리에 없다는 것", "누가 언제 가져갔는지", true, true, $"{S.RoomName(room)}에 있어야 할 {kor}{LineBank.Josa(kor, "이")} 보이지 않는다.", false,
                    new Prop { Kind = PropKind.ItemMissing, Item = it.Type, Room = room, T0 = S.Ch.ChapterStartClock, T1 = S.Clock });
            }
            foreach (var it in S.Items.Values.Where(i => i.Room == room && i.Holder == null && (i.Bloody || i.Washed) && !(i.Hidden && i.StashF >= 0)))   // (concealment: a stash is found by searching its place, not by a look round the room)
                ExamineItem(sim, ex, it);
            foreach (var t in S.Traces.Where(t => t.Room == room && t.Visibility <= 1 && !t.Cleaned)) ExamineTrace(sim, ex, t);
            Tricks.RoomQuick(sim, ex, room);
            Testimony.UpdateSuspicion(sim, ex);
        }

        /// <summary>The room a door is named after: the non-passage side (the scene, when it is one of them).</summary>
        public static int DoorRoom(GameState S, Door d)
        {
            if (d == null) return -1;
            var inc = CaseProgress.Current(S);
            if (inc != null && (d.RoomA == inc.FoundRoom || d.RoomB == inc.FoundRoom)) return inc.FoundRoom;
            var ra = S.Layout.Room(d.RoomA); var rb = S.Layout.Room(d.RoomB);
            if (ra != null && RoomInfo.IsPassage(ra.Type) && rb != null && !RoomInfo.IsPassage(rb.Type)) return d.RoomB;
            return d.RoomA;
        }

        public static Evidence ExamineDoor(Simulation sim, Actor ex, Door d)
        {
            var S = sim.S; string state = d.Locked ? "잠겨 있다" : d.Open ? "열려 있다" : "닫혀 있지만 잠기지 않았다";
            var ra = S.Layout.Room(d.RoomA); var rb = S.Layout.Room(d.RoomB);
            var keyInfo = d.KeyId == null ? "열쇠로 잠그는 문이 아니다" : d.KeyId == "key_master" ? "밤이면 저택이 스스로 잠그는 문" : d.KeyId == "key_butler" ? "집사만 여닫는 문" : $"{Cast.GivenOf(d.KeyId.Substring(4))}의 방 열쇠로 잠기는 문";
            // a close look at the door, its gap and its thumb-turn can turn up what was done to it
            string extra = ""; var found = new List<string>();
            foreach (var t in S.Traces.Where(t => !t.Cleaned && (t.Type == "ThreadFiber" || t.Type == "Scratch") && t.Pos.f == d.Pos.f && t.Pos.DistXZ(d.Pos) < 0.9f).ToList())
            { var tev = ExamineTrace(sim, ex, t); if (tev != null) { extra += " " + t.Desc + "."; found.Add(t.Desc); } }
            if (!ex.IsPlayer)
                return Add(sim, ex.Id, EvKind.ObjectState, $"{ra?.Name}–{rb?.Name} 문", $"지금 {state}. {keyInfo}.{extra}", "직접 확인", "door:" + d.Id + ":" + (int)S.Clock, S.Clock, S.Clock, d.RoomA,
                    "지금 문이 어떤 상태인지", "그때도 잠겨 있었는지 — 지금 모습만으로는 알 수 없다", true, new Prop { Kind = PropKind.DoorState, Room = d.RoomA, Value = d.Locked ? "locked" : "unlocked", T0 = S.Clock, T1 = S.Clock, Item = d.Id.ToString() });
            S.K(ex.Id).Examined.Add("door:" + d.Id);
            int room = DoorRoom(S, d); string title = $"{S.RoomName(room)} 문";
            var inc = CaseProgress.Current(S);
            bool notable = found.Count > 0 || (S.Phase == Phase.Investigation && inc != null && (d.RoomA == inc.FoundRoom || d.RoomB == inc.FoundRoom));
            if (!notable) return new Evidence { Loose = true, Kind = EvKind.ObjectState, Title = title, Line = $"지금 {state}.", Owner = ex.Id, Room = room, T0 = S.Clock, T1 = S.Clock, Root = "door:" + d.Id };
            var lines = new List<string> { $"지금 {state}", keyInfo }; lines.AddRange(found);
            return File(sim, ex.Id, EvKind.ObjectState, title, string.Join("\n", lines), "직접 확인", "door:" + d.Id, S.Clock, S.Clock, room,
                "지금 문이 어떤 상태인지", "그때도 잠겨 있었는지 — 지금 모습만으로는 알 수 없다", true, true, $"지금 {state}.", false,
                new Prop { Kind = PropKind.DoorState, Room = d.RoomA, Value = d.Locked ? "locked" : "unlocked", T0 = S.Clock, T1 = S.Clock, Item = d.Id.ToString() });
        }
    }
}
