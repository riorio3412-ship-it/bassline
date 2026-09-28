using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// Daily life in the conversation menu (the Bonds pattern): a scene told in dialogue shows its choices as the only options
    /// until it is over. Residents who come to 민혁 with a purpose (their heart event at an appointment, a moment after time
    /// together, a small gift, jealousy, a rumour, a callback to an old choice) have a "lifego" option on top — the
    /// presentation picks it for them right after the greeting, so they simply say what they came to say.
    /// Hooks (one line each): PlayerApi.Options → LifeOptions, PlayerApi.Choose → LifeChoose.
    /// </summary>
    public sealed partial class Simulation
    {
        // ================================================================== purposes ("lifego")
        static readonly string[] GoOrder = { "heart", "confide", "hang", "verdict", "grief", "gift", "rival", "jealous", "flirt", "rumour", "callback" };

        string GoPrefix(string npc) => "lifego:" + npc + "|";

        /// <summary>Queue something a resident means to say to 민혁 (one per kind; the strongest is said first).</summary>
        internal void LifePurpose(string npc, string kind, string arg)
        {
            if (npc == null || kind == null) return;
            string pre = GoPrefix(npc) + kind + "|";
            foreach (var f in PK.Facts.Where(f => f.StartsWith(pre, StringComparison.Ordinal)).ToList()) PK.Facts.Remove(f);
            PK.Facts.Add(pre + (arg ?? "-") + "|" + S.Day);
        }

        /// <summary>The purpose a resident has for 민혁 right now: (kind, arg), or null. Stale ones (older than a day) are dropped;
        /// a heart due at an appointment keeps.</summary>
        internal (string kind, string arg)? LifeGoFor(string npc)
        {
            string pre = GoPrefix(npc);
            // a choice from an earlier day that this resident brings up the next time they talk (callbacks wait for a conversation)
            if (S.Phase == Phase.Daily && !PK.Facts.Any(f => f.StartsWith(pre, StringComparison.Ordinal)))
            {
                string cb = "life:cb:" + npc + ":";
                var old = PK.Facts.Where(f => f.StartsWith(cb, StringComparison.Ordinal)).OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault(f => { var p = f.Split(new[] { ':' }, 5); return p.Length == 5 && int.TryParse(p[3], out var d) && d < S.Day; });
                if (old != null) PK.Facts.Add(pre + "callback|-|" + S.Day);
            }
            var mine = PK.Facts.Where(f => f.StartsWith(pre, StringComparison.Ordinal)).OrderBy(f => f, StringComparer.Ordinal).ToList();
            (string kind, string arg)? best = null; int bestRank = 99;
            foreach (var f in mine)
            {
                var p = f.Substring(pre.Length).Split('|'); if (p.Length < 3) { PK.Facts.Remove(f); continue; }
                int day = int.TryParse(p[2], out var d) ? d : 0;
                if (p[0] != "heart" && S.Day - day > 1) { PK.Facts.Remove(f); continue; }
                int rank = Array.IndexOf(GoOrder, p[0]); if (rank < 0) rank = 50;
                if (rank < bestRank) { bestRank = rank; best = (p[0], p[1]); }
            }
            return best;
        }
        void DropPurpose(string npc, string kind)
        {
            string pre = GoPrefix(npc) + kind + "|";
            foreach (var f in PK.Facts.Where(f => f.StartsWith(pre, StringComparison.Ordinal)).ToList()) PK.Facts.Remove(f);
        }

        /// <summary>Presentation: does this resident have something to say to 민혁 the moment a conversation opens?</summary>
        public bool LifeWantsToSay(string npc) => npc != null && (LifeGoFor(npc) != null || (_dlg.TryGetValue(npc, out var r) && !r.Over));

        /// <summary>Presentation: the kind of thing this resident means to say to 민혁 ("heart" at an appointment, "hang" after time
        /// together, "gift", "jealous", "rumour" …), or null. Only during daily life.</summary>
        public string LifeGoKind(string npc) => npc == null || S.Phase != Phase.Daily ? null : LifeGoFor(npc)?.kind;

        string GoLabel(string npc, (string kind, string arg) go)
        {
            switch (go.kind)
            {
                case "heart": { var h = LifeData.Heart(npc, int.TryParse(go.arg, out var n) ? n : 1); return $"「{h?.Title ?? "약속"}」 — 이야기를 듣는다"; }
                case "hang": return "…함께한 시간의 끝";
                case "gift": return "무언가를 건네려는 것 같다";
                case "jealous": case "rival": return "할 말이 있어 보인다";
                case "rumour": return "소문 하나를 들려주려 한다";
                case "verdict": return "잠깐 걷자고 한다";
                case "flirt": return "어쩐지 눈을 오래 맞춘다";
                case "grief": return "조용히 곁에 선다";
                case "confide": return "무언가 털어놓고 싶어 보인다";
                default: return "할 말이 있어 보인다";
            }
        }

        // ================================================================== PlayerApi hooks
        /// <summary>PlayerApi.Options hook. True = the options added are the only ones (a scene is waiting for 민혁's choice).</summary>
        internal bool LifeOptions(Actor npc, List<DialogueOption> o)
        {
            if (npc == null || npc.IsButler || npc.IsPlayer) return false;
            var run = DlgRun(npc.Id);
            if (run != null && !run.Over)
            {
                var opts = VisibleOpts(run);
                if (opts.Count > 0)
                {
                    for (int i = 0; i < opts.Count; i++) o.Add(new DialogueOption { Id = "lifepick", Arg = i.ToString(CultureInfo.InvariantCulture), Label = OptLabel(opts[i], run) });
                    return true;
                }
                EndDlg(npc.Id);
            }
            if (S.Phase != Phase.Daily) return false;
            var go = LifeGoFor(npc.Id);
            if (go != null) o.Add(new DialogueOption { Id = "lifego", Label = GoLabel(npc.Id, go.Value) });
            var h = HeartReady(npc, out _);
            if (h != null && go == null) o.Add(new DialogueOption { Id = "lifeheart", Label = $"「{h.Title}」 — 따로 할 얘기가 있어 보인다" });
            var rums = RumoursShared(npc.Id);
            if (rums.Count > 0)
            {
                o.Add(new DialogueOption { Id = "lifetrace", Label = "그 소문, 누구한테 들었는지 묻는다", Sub = rums.Select(r => (r.rid, r.text)).ToList() });
                var fix = rums.Where(r => RumourPlayerKnowsTruth(r.rid)).ToList();
                if (fix.Count > 0) o.Add(new DialogueOption { Id = "lifecorrect", Label = "그 소문은 사실이 아니라고 말한다", Sub = fix.Select(r => (r.rid, r.text)).ToList() });
            }
            return false;
        }

        /// <summary>PlayerApi.Choose hook. Returns the lines, or null when daily life does not handle this option.</summary>
        internal List<Utterance> LifeChoose(Actor npc, string opt, string arg)
        {
            if (npc == null || opt == null) return null;
            switch (opt)
            {
                case "lifepick":
                    {
                        var run = DlgRun(npc.Id); if (run == null) return new List<Utterance>();
                        var res = RunPick(run, int.TryParse(arg, out var i) ? i : 0);
                        if (run.Over) FinishDlg(npc.Id, run); else SavePend(npc.Id, run);
                        return res;
                    }
                case "lifego": return StartGo(npc);
                case "lifeheart": return AskHeart(npc);
                case "lifetrace": return RumourTrace(npc, arg);
                case "lifecorrect": return RumourCorrect(npc, arg);
                case "gift": return LifeGift(npc, arg);
            }
            return null;
        }

        // ================================================================== dialogue runs
        SceneRun DlgRun(string npc)
        {
            if (_dlg.TryGetValue(npc, out var run)) return run;
            // after a load: the scene waiting for a choice is kept in a player fact
            string pre = "lifepend:" + npc + "|";
            var f = PK.Facts.FirstOrDefault(x => x.StartsWith(pre, StringComparison.Ordinal));
            if (f == null) return null;
            var p = f.Substring(pre.Length).Split('|');
            PK.Facts.Remove(f);
            if (p.Length < 4) return null;
            var sc = LifeData.Get(p[0]) ?? GenRebuild(p[0], npc, p.Length > 3 ? p[3] : "");
            var beat = sc?.Beat(p[1]); if (beat == null) return null;
            run = new SceneRun { Scene = sc, Kind = p[2], Npc = npc, Beat = beat, Room = S.A(npc)?.Room ?? -1 };
            DecodeCtx(p.Length > 3 ? p[3] : "", run.Ctx); run.Cast.Add(npc); run.Cast.Add(Cast.Player);
            _dlg[npc] = run; SavePend(npc, run);
            return run;
        }

        void SavePend(string npc, SceneRun run)
        {
            string pre = "lifepend:" + npc + "|";
            foreach (var f in PK.Facts.Where(x => x.StartsWith(pre, StringComparison.Ordinal)).ToList()) PK.Facts.Remove(f);
            if (run.Over || run.Beat == null) return;
            PK.Facts.Add(pre + run.Scene.Id + "|" + run.Beat.Id + "|" + run.Kind + "|" + EncodeCtx(run.Ctx));
        }

        void EndDlg(string npc)
        {
            _dlg.Remove(npc);
            string pre = "lifepend:" + npc + "|";
            foreach (var f in PK.Facts.Where(x => x.StartsWith(pre, StringComparison.Ordinal)).ToList()) PK.Facts.Remove(f);
        }

        static string EncodeCtx(Dictionary<string, string> ctx) => string.Join(";", ctx.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + "=" + (kv.Value ?? "").Replace(";", ",").Replace("|", "/").Replace("=", "-")));
        static void DecodeCtx(string s, Dictionary<string, string> ctx)
        {
            if (string.IsNullOrEmpty(s)) return;
            foreach (var kv in s.Split(';')) { int i = kv.IndexOf('='); if (i > 0) ctx[kv.Substring(0, i)] = kv.Substring(i + 1); }
        }

        List<Utterance> BeginDlg(Actor npc, LScene sc, string kind, Dictionary<string, string> ctx = null)
        {
            var run = new SceneRun { Scene = sc, Kind = kind, Npc = npc.Id, Beat = sc.First, Room = npc.Room };
            run.Ctx["place"] = npc.Room.ToString(CultureInfo.InvariantCulture);
            if (ctx != null) foreach (var kv in ctx) run.Ctx[kv.Key] = kv.Value;
            run.Cast.Add(npc.Id); run.Cast.Add(Cast.Player);
            _dlg[npc.Id] = run;
            var lines = RunFrom(run);
            foreach (var u in lines) LifeLineCount(u.Speaker, u.Text);
            if (run.Over) FinishDlg(npc.Id, run); else SavePend(npc.Id, run);
            S.Log("LifeScene", Cast.Player, npc.Id, room: npc.Room, data: kind + ":" + sc.Id);
            return lines;
        }

        void FinishDlg(string npc, SceneRun run)
        {
            EndDlg(npc);
            switch (run.Kind)
            {
                case "heart":
                    if (run.Scene is HeartDef h)
                    {
                        S.Flags["lh:" + npc] = Math.Max(LifeHearts(npc), h.No); S.Flags[$"lhd:{npc}:{h.No}"] = S.Day;
                        Relations.Change(S, npc, Cast.Player, attach: 0.03f, memory: $"「{h.Title}」");
                        S.Log("HeartScene", Cast.Player, npc, data: h.Id + ":" + run.Picks);
                    }
                    break;
                case "hang":
                    if (run.Scene is HangDef g) { Remember(npc, "hang:" + g.Act); S.Flags[$"lhang:{npc}:{S.Day}"] = 1; S.Log("HangScene", Cast.Player, npc, data: g.Id); }
                    break;
            }
            if (run.Note != null) S.Emit(GameEventType.Notice, Cast.Player, npc, text: run.Note, key: "life_note");
        }

        List<Utterance> StartGo(Actor npc)
        {
            var go = LifeGoFor(npc.Id); if (go == null) return new List<Utterance>();
            var (kind, arg) = go.Value;
            DropPurpose(npc.Id, kind);
            LScene sc = null; var ctx = new Dictionary<string, string>(); string runKind = "life";
            switch (kind)
            {
                case "heart": { int no = int.TryParse(arg, out var n) ? n : LifeHearts(npc.Id) + 1; sc = LifeData.Heart(npc.Id, no); runKind = "heart"; break; }
                case "hang": { sc = LifeData.Get(arg); runKind = "hang"; break; }
                default: sc = GenBuild(kind, npc.Id, arg, ctx); break;
            }
            if (sc == null || sc.First == null) return new List<Utterance>();
            return BeginDlg(npc, sc, runKind, ctx);
        }

        // ================================================================== generated dialogue scenes (gift to 민혁, jealousy, rumours, callbacks …)
        readonly Dictionary<string, LScene> _gen = new Dictionary<string, LScene>(StringComparer.Ordinal);

        LScene GenRebuild(string id, string npc, string ctxEnc)
        {
            if (_gen.TryGetValue(id, out var s)) return s;
            if (!id.StartsWith("gen:")) return null;
            var p = id.Split(':'); if (p.Length < 4) return null;
            var ctx = new Dictionary<string, string>(); DecodeCtx(ctxEnc, ctx);
            return GenBuild(p[1], p[2], string.Join(":", p.Skip(3)), ctx);
        }

        static LL GL(string who, string text, Emotion e = Emotion.Neutral) => new LL { Who = who, Text = text, Emo = e };
        static LOpt GO(string label, params LL[] reply) { var o = new LOpt { Label = label }; o.Reply.AddRange(reply); return o; }
        static LFx GFx(string from, string to, float like = 0, float trust = 0, float attach = 0, float grudge = 0, float jealous = 0, float respect = 0, string mem = null)
            => new LFx { From = from, To = to, Like = like, Trust = trust, Attach = attach, Grudge = grudge, Jealous = jealous, Respect = respect, Memory = mem };

        /// <summary>Raw template of a resident's voice line for a daily-life key (their own, else the shared ANY), or null.</summary>
        string VoiceRaw(string npc, string key, bool casual)
        {
            var pool = LineBank.Pool(npc, key, casual, out _);
            if (pool == null || pool.Length == 0) pool = LineBank.Pool(LineBank.Shared, key, casual, out _);
            if (pool == null || pool.Length == 0) return null;
            string fk = $"lrv:{npc}:{key}"; int last = (int)LF(fk, -1);
            int pick = pool.Length == 1 ? 0 : LR.R(pool.Length - (last >= 0 && last < pool.Length ? 1 : 0));
            if (pool.Length > 1 && last >= 0 && last < pool.Length && pick >= last) pick++;
            S.Flags[fk] = pick;
            return pool[pick];
        }

        LScene GenBuild(string kind, string npc, string arg, Dictionary<string, string> ctx)
        {
            string id = $"gen:{kind}:{npc}:{arg}";
            var sc = new GenScene { Id = id, Kind = kind, Npc = npc, Title = kind };
            bool cas = S.HasRel(npc, Cast.Player) && S.R(npc, Cast.Player).Casual;
            string V(string key) => VoiceRaw(npc, key, cas);
            switch (kind)
            {
                case "confide":
                    {
                        // the house's push got to them (HousePush): they tell 민혁 what the wish's token does to them at night;
                        // his answer holds them back (the wall mended) or, understanding too well, loosens it a little
                        sc.Title = "속내";
                        sc.Open(GL(npc, V("confide_open") ?? "…잠깐 얘기해도 돼요? 그 견본 말이에요. 자꾸 생각나요.", Emotion.Sad));
                        var give = GO("그 견본, 저한테 맡겨요.", GL(npc, V("confide_give") ?? "…네. 맡길게요. 제가 들고 있으면 안 될 것 같아요.", Emotion.Sad))
                                     .Do(GFx(npc, "me", trust: 0.05f, attach: 0.02f, mem: "견본을 맡아 줬다")).Know($"mend:{npc}=0.06;flag:gavesample:{S.Loop}:{npc}");
                        give.LabelC = "그 견본, 나한테 맡겨.";
                        var hope = GO("소원은 다른 방법으로 이뤄요. 같이 찾아요.", GL(npc, V("confide_hope") ?? "…다른 방법이 있다면요. 같이 찾아 줘요.", Emotion.Smile))
                                     .Do(GFx(npc, "me", like: 0.03f, trust: 0.02f, mem: "소원을 같이 찾자고 했다")).Know($"mend:{npc}=0.04");
                        hope.LabelC = "소원은 다른 방법으로 이루자. 같이 찾아.";
                        var feel = GO("…그 마음, 저도 알 것 같아요.", GL(npc, V("confide_understood") ?? "…고마워요. 알아주는 사람이 있어서.", Emotion.Sad))
                                     .Do(GFx(npc, "me", trust: 0.06f, attach: 0.03f, mem: "흔들리는 마음을 알아줬다")).Know($"erode:{npc}=0.02");
                        feel.LabelC = "…그 마음, 나도 알 것 같아.";
                        sc.Choice(give, hope, feel);
                        break;
                    }
                case "gift":
                    {
                        string type = arg; var def = ItemCatalog.Get(type); if (def == null) return null;
                        ctx["item"] = def.Kor;
                        sc.Open(GL(npc, V("life_give") ?? "이거 받아요. {item}. 그냥 생각나서요.", Emotion.Smile));
                        var thank = GO("고마워요. 잘 쓸게요.", GL(npc, V("life_give_thanks") ?? "…응. 그럼 됐어.", Emotion.Smile)).Give(type).Do(GFx(npc, "me", like: 0.03f, attach: 0.03f, mem: "건넨 {item}을(를) 받아 줬다"));
                        thank.LabelC = "고마워. 잘 쓸게.";
                        var tease = GO("이거 혹시 뇌물이에요?", GL(npc, V("life_give_tease") ?? "뇌물이면 더 비싼 걸 줬지.", Emotion.Smirk)).Give(type).Do(GFx(npc, "me", like: npc == "P05" || npc == "P16" ? -0.01f : 0.03f));
                        tease.LabelC = "이거 뇌물이지?";
                        var no = GO("마음만 받을게요. 괜찮아요.", GL(npc, V("life_give_refused") ?? "…그래. 알았어.", Emotion.Blank)).Do(GFx(npc, "me", like: npc == "P18" ? 0.01f : -0.02f, respect: npc == "P18" ? 0.03f : 0));
                        no.LabelC = "마음만 받을게. 괜찮아.";
                        sc.Choice(thank, tease, no);
                        break;
                    }
                case "jealous":
                    {
                        ctx["t"] = arg;
                        sc.Open(GL(npc, V("life_jealous") ?? "요즘 {t}하고만 붙어 다니더라.", Emotion.Blank));
                        var a = GO("{t} 씨랑은 그냥 할 얘기가 많았어요. 서운했어요?", GL(npc, V("life_jealous_soothed") ?? "서운하긴. …조금.", Emotion.Smile)).Do(GFx(npc, "me", like: 0.04f, attach: 0.02f), GFx(npc, arg, jealous: -0.05f)).Mem("soothed");
                        a.LabelC = "{t}랑은 그냥 할 얘기가 많았어. 서운했어?";
                        var b = GO("질투하는 거예요?", GL(npc, V("life_jealous_teased") ?? "질투 같은 소리 하네.", Emotion.Smirk)).Do(GFx(npc, "me", like: 0.02f));
                        b.LabelC = "질투해?";
                        var c = GO("제가 누구랑 있든 그건 제 일이죠.", GL(npc, V("life_jealous_hurt") ?? "…그렇지. 네 일이지.", Emotion.Sad)).Do(GFx(npc, "me", like: -0.03f, grudge: 0.03f), GFx(npc, arg, jealous: 0.04f));
                        c.LabelC = "내가 누구랑 있든 내 일이잖아.";
                        sc.Choice(a, b, c);
                        break;
                    }
                case "rival":
                    {
                        ctx["t"] = arg;
                        sc.Open(GL(npc, V("life_rival") ?? "{t}한테 너무 가까이 가지 마. 그냥 하는 말이야.", Emotion.Blank));
                        var a = GO("알았어요. 선은 지킬게요.", GL(npc, V("life_rival_ok") ?? "…그래. 그럼 됐어.")).Do(GFx(npc, "me", trust: 0.04f), GFx(npc, arg, jealous: -0.03f));
                        a.LabelC = "알았어. 선은 지킬게.";
                        var b = GO("그건 {t} 씨가 정할 일이에요.", GL(npc, V("life_rival_defied") ?? "…말은 잘하네.", Emotion.Angry)).Do(GFx(npc, "me", grudge: 0.04f, respect: 0.03f), GFx(npc, "me", jealous: 0.05f));
                        b.LabelC = "그건 {t}가 정할 일이야.";
                        var c = GO("걱정돼서 그러는 거죠?", GL(npc, V("life_rival_seen") ?? "걱정은 무슨. …그래, 조금.", Emotion.Sad)).Do(GFx(npc, "me", like: 0.03f, attach: 0.02f));
                        c.LabelC = "걱정돼서 그러는 거지?";
                        sc.Choice(a, b, c);
                        break;
                    }
                case "rumour":
                    {
                        var r = RumourOf(npc, arg); if (r == null) return null;
                        ctx["rumour"] = RumourText(r.Value, npc, Cast.Player);
                        sc.Open(GL(npc, V("rumour_tell") ?? "{rumour}", Emotion.Neutral));
                        var a = GO("누가 그래요? 처음 말한 사람이요.", GL(npc, "{from}한테 들었어요.")).Do(GFx(npc, "me", trust: 0.02f));
                        a.LabelC = "누가 그래? 처음 말한 사람.";
                        ctx["from"] = r.Value.from == Cast.Player ? "me" : r.Value.from;
                        var b = GO("확인된 얘기예요?", GL(npc, V("rumour_unsure") ?? "그건… 모르지. 들은 거야.")).Do(GFx(npc, "me", respect: 0.02f));
                        b.LabelC = "확인된 얘기야?";
                        var c = GO("그런 말은 옮기지 마요.", GL(npc, V("rumour_scolded") ?? "…알았어. 안 옮길게.", Emotion.Blank)).Do(GFx(npc, "me", like: -0.01f, respect: 0.03f));
                        c.LabelC = "그런 말은 옮기지 마.";
                        c.FactList = "flag:rumhush:" + npc + ":" + arg;
                        sc.Choice(a, b, c);
                        // 민혁 has heard it now
                        RumourLearn(Cast.Player, r.Value, npc);
                        break;
                    }
                case "flirt":
                    {
                        // a resident drawn to 민혁 (stage 2+) says so in their own way (anyone: the owner lifted the exclusions)
                        if (NoRomance(npc)) return null;
                        string key = LineBank.Has(npc, "flirt@" + Cast.Player) || LineBank.Has(npc, "flirt") ? "flirt" : "love_hint";
                        var u = LifeKeyU(npc, Cast.Player, key, new SceneRun { Kind = "life", Npc = npc });
                        if (u == null) return null;
                        sc.Open(GL(npc, u.Text.Replace("{", "(").Replace("}", ")"), Emotion.Smile));
                        var back = GO("…저도 그 얘기 하려고 했어요.", GL(npc, V("flirt_react") ?? "…그래? 그럼 비긴 거다.", Emotion.Smile)).Do(GFx(npc, "me", like: 0.03f, attach: 0.03f), new LFx { From = npc, To = "me", Romance = 0.05f, Memory = "서로 눈이 오래 머물렀다" });
                        back.LabelC = "…나도 그 얘기 하려고 했어.";
                        var laugh = GO("농담이죠? 하하.", GL(npc, V("joke_react") ?? "…응, 농담. 반쯤.", Emotion.Smirk)).Do(GFx(npc, "me", like: 0.01f));
                        laugh.LabelC = "농담이지? 하하.";
                        var no = GO("저는 지금처럼 편한 게 좋아요.", GL(npc, V("life_jealous_hurt") ?? "…그래. 지금처럼.", Emotion.Blank)).Do(GFx(npc, "me", respect: 0.03f), new LFx { From = npc, To = "me", Romance = -0.05f });
                        no.LabelC = "난 지금처럼 편한 게 좋아.";
                        sc.Choice(back, laugh, no);
                        break;
                    }
                case "callback":
                    {
                        var f = PK.Facts.Where(x => x.StartsWith("life:cb:" + npc + ":", StringComparison.Ordinal)).OrderBy(x => x, StringComparer.Ordinal)
                            .FirstOrDefault(x => { var q = x.Split(new[] { ':' }, 5); return q.Length == 5 && int.TryParse(q[3], out var d0) && d0 < S.Day; });
                        if (f == null) return null;
                        PK.Facts.Remove(f);
                        var parts = f.Split(new[] { ':' }, 5); if (parts.Length < 5) return null;
                        sc.Open(GL(npc, parts[4], Emotion.Smile));
                        break;
                    }
                case "verdict":
                    {
                        sc.Open(GL(npc, V("after_trial_walk") ?? "잠깐 걸을래? 오늘은 방에 바로 들어가기 싫어서.", Emotion.Sad));
                        var a = GO("같이 걸어요.", GL(npc, V("after_trial_walk_yes") ?? "…고마워. 말은 안 해도 돼.", Emotion.Sad)).Do(GFx(npc, "me", like: 0.03f, attach: 0.05f, trust: 0.03f, mem: "심판이 끝난 밤, 같이 걸었다"));
                        a.LabelC = "같이 걷자.";
                        var b = GO("오늘은 혼자 있고 싶어요. 미안해요.", GL(npc, V("after_trial_walk_no") ?? "응. 그런 날이지.", Emotion.Blank)).Do(GFx(npc, "me", respect: 0.02f));
                        b.LabelC = "오늘은 혼자 있고 싶어. 미안.";
                        sc.Choice(a, b);
                        break;
                    }
                case "grief":
                    {
                        ctx["victim"] = arg;
                        sc.Open(GL(npc, V("grief_share") ?? "{victim}… 아직도 그 자리에 있을 것 같아.", Emotion.Sad));
                        var a = GO("저도 그래요.", GL(npc, V("grief_share_back") ?? "…응.", Emotion.Sad)).Do(GFx(npc, "me", attach: 0.04f, trust: 0.03f, mem: "{victim} 얘기를 같이 했다"));
                        a.LabelC = "나도 그래.";
                        var b = GO("말없이 곁에 서 있는다", GL(npc, V("grief_share_silent") ?? "…고마워. 그냥 있어 줘서.", Emotion.Sad)).Act().Do(GFx(npc, "me", attach: 0.05f));
                        sc.Choice(a, b);
                        if (npc == "P14") { var lily = sc.First.Opts; foreach (var o in lily) o.GiftType = "Flower"; }
                        break;
                    }
                default: return null;
            }
            _gen[id] = sc;
            return sc;
        }

        // ================================================================== gifts (Gifts.cs through PlayerApi "gift")
        /// <summary>A gift from 민혁: the resident's own table (Data/Gifts.cs) first, the old keyword match otherwise; the
        /// signature gift plays its unique reaction once per loop; 채령 logs every gift with the date.</summary>
        internal List<Utterance> LifeGift(Actor npc, string itemId)
        {
            var it = S.I(itemId); if (it == null || it.Holder != Cast.Player) return null;
            var id = npc.Id; var me = Cast.Player; var c = npc.Def; var prefs = Gifts.Get(id);
            int? table = Gifts.Score(id, it.Type);
            bool sig = Gifts.IsSignature(id, it.Type) && !S.Flags.ContainsKey($"giftsig:{id}");
            int score = table ?? GiftScore(c, it);
            if (sig && score < 1) score = 1;
            var res = new List<Utterance>();
            var slots = new Dictionary<string, string> { { "item", it.Kor } };
            res.Add(U(me, id, "p_gift", slots));
            if (sig && prefs?.SignatureLine != null)
            {
                S.Flags[$"giftsig:{id}"] = S.Day;
                res.Add(new Utterance { Speaker = id, Listener = me, Key = "gift_love", Text = LineBank.FixParticles(prefs.SignatureLine), Emotion = Emotion.Smile, Gesture = Anim.Talk });
                PK.Facts.Add($"bondnote:{id}:{it.Kor}을(를) 선물했다 — 오래 기억할 것 같다");
            }
            else if (score < 0 && prefs?.DislikeLine != null) res.Add(new Utterance { Speaker = id, Listener = me, Key = "gift_hate", Text = LineBank.FixParticles(prefs.DislikeLine), Emotion = Emotion.Disgust, Gesture = Anim.Talk });
            else
            {
                string key = score >= 2 ? "gift_love" : score == 1 ? "gift_like" : score == 0 ? "gift_meh" : "gift_hate";
                res.Add(U(id, me, key, slots, score >= 1 ? Emotion.Smile : score < 0 ? Emotion.Disgust : Emotion.Neutral));
            }
            if (score >= 0)
            {
                DropItem(P, it, npc.Pos); it.Holder = null; PickUp(npc, it); it.Owner = id;
                if (id == "P16")
                {
                    S.K("P16").Facts.Add($"gift-log:{S.Day}:{it.Kor}");
                    var log = VoiceRaw("P16", "gift_log", true);
                    if (log != null) res.Add(new Utterance { Speaker = id, Listener = me, Key = "life", Text = LifeRenderText(log, id, me, new SceneRun { Kind = "life", Npc = id, Ctx = { ["item"] = it.Kor, ["n"] = S.Day.ToString(CultureInfo.InvariantCulture) } }), Emotion = Emotion.Blank });
                }
            }
            float like = score >= 2 ? 0.09f : score == 1 ? 0.06f : score == 0 ? 0.03f : -0.03f;
            Relations.Change(S, id, me, like: like, attach: score >= 2 ? 0.05f : sig ? 0.04f : 0, memory: sig ? it.Kor + ", 잊지 못할 선물" : score >= 1 ? it.Kor + ", 선물로 받았다" : score < 0 ? "싫어하는 걸 받았다" : null);
            Remember(id, "gift:" + it.Type);
            if (score >= 1) PK.Facts.Add($"likes:{id}:{it.Kor}");
            LFinc($"lfav:{S.Day}:{id}");
            S.Log("GiftGiven", me, id, item: it.Id, data: it.Type + ":" + score + (sig ? ":sig" : ""));
            return res;
        }
    }
}
