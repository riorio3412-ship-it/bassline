using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>Directed relationships. Tags come from agreements/events, not thresholds alone. Only experienced events change values.</summary>
    public static class Relations
    {
        public static void InitLoop(GameState S)
        {
            var rng = S.R(Stream.Life);
            var ids = Cast.Participants.Select(c => c.Id).ToList();
            foreach (var a in ids) foreach (var b in ids)
                {
                    if (a == b) continue;
                    var ca = Cast.Get(a); var cb = Cast.Get(b); var r = S.R(a, b);
                    float compat = Compat(ca, cb);
                    r.Like = 0.02f + compat * 0.08f + rng.Range(-0.03f, 0.03f);
                    r.Trust = 0.04f + ca.P.Loyalty * 0.03f;
                    r.Respect = 0.05f;
                    r.Casual = !ca.Speech.PoliteDefault;
                }
            // Yusti: everyone speaks formally to him
            // fixed starting facts (03 부록 C.3). Only the people who know get the values/facts.
            void Set(string a, string b, Action<Rel> f, string fact = null) { f(S.R(a, b)); if (fact != null) S.K(a).Facts.Add(fact); }
            Set("P02", "P01", r => { r.Like = 0.25f; r.Trust = 0.05f; r.Tags.Add("test"); }, "rel:진우는 민혁을 시험해 보고 싶다");
            Set("P01", "P02", r => { r.Like = 0.12f; });
            Set("P03", "P05", r => { r.Grudge = 0.55f; r.Trust = -0.2f; r.Like = -0.15f; r.Tags.Add("grudge"); r.Memory.Add("이현의 일로 우리 집이 무너졌다"); }, "secret:P05:서윤가족");
            Set("P05", "P03", r => { r.Fear = 0.2f; r.Tags.Add("guilt"); }, "secret:P05:서윤가족");
            Set("P05", "P06", r => { r.Trust = 0.25f; r.Depend = 0.35f; r.Fear = 0.2f; r.Tags.Add("deal"); r.Tags.Add("colleague"); }, "secret:P06:불법자금");
            Set("P06", "P05", r => { r.Trust = 0.2f; r.Depend = 0.3f; r.Fear = 0.25f; r.Tags.Add("deal"); r.Tags.Add("colleague"); }, "secret:P05:불법자금");
            Set("P04", "P14", r => { r.Attach = 0.6f; r.Like = 0.3f; r.Fear = 0.15f; r.Casual = true; r.Tags.Add("family"); }, "secret:P14:남매");
            Set("P14", "P04", r => { r.Attach = 0.85f; r.Like = 0.5f; r.Jealous = 0.4f; r.Casual = true; r.Tags.Add("family"); r.Tags.Add("control"); }, "secret:P04:범죄");
            S.K("P14").Facts.Add("secret:P04:남매"); S.K("P04").Facts.Add("secret:P04:남매");
            Set("P15", "P05", r => { r.Respect = -0.2f; r.Trust = -0.3f; r.Tags.Add("exposed"); }, "secret:P05:스캔들");
            Set("P05", "P15", r => { r.Grudge = 0.45f; r.Fear = 0.4f; r.Like = -0.25f; r.Tags.Add("grudge"); r.Memory.Add("저 기자가 내 스캔들을 폭로했다"); }, "fact:P15:기자");
            Set("P10", "P16", r => { r.Like = 0.3f; r.Attach = 0.25f; r.Tags.Add("protect"); });
            Set("P16", "P10", r => { r.Like = 0.05f; r.Grudge = 0.1f; r.Tags.Add("boundary"); r.Memory.Add("준서는 내 결정을 대신하려 든다"); });
            Set("P13", "P11", r => { r.Like = 0.2f; r.Depend = 0.35f; r.Tags.Add("hope"); });
            Set("P11", "P13", r => { r.Like = 0.15f; r.Fear = 0.15f; r.Tags.Add("pressure"); });
            Set("P08", "P09", r => { r.Jealous = 0.3f; r.Respect = 0.1f; r.Tags.Add("rival"); });
            Set("P09", "P08", r => { r.Jealous = 0.2f; r.Like = 0.1f; r.Tags.Add("rival"); });
            // own contract is known to self
            foreach (var c in Cast.Participants) { S.K(c.Id).Facts.Add("contract:" + c.Id); S.K(c.Id).Facts.Add("secret:" + c.Id); }
            S.K("P04").Facts.Add("secret:P04:범죄");
            CastWeb.Apply(S);   // bible §3: the other 28 ties + dormant knots (Data/CastWeb.cs)
        }

        public static float Compat(CastDef a, CastDef b)
        {
            float s = 0;
            s += 1 - Math.Abs(a.P.Sociability - b.P.Sociability);
            s += 1 - Math.Abs(a.P.Honesty - b.P.Honesty);
            s += a.Hobbies.Intersect(b.Hobbies).Count() * 0.5f;
            s += a.Likes.Intersect(b.Likes).Count() * 0.4f;
            s -= (a.P.Pride + b.P.Pride) * 0.3f;
            return MathX.Clamp(s / 3f, -0.5f, 1f);
        }

        /// <summary>Apply an experienced change. Logs a short memory line for the relationship UI (only for events the subject perceived).</summary>
        public static void Change(GameState S, string a, string b, float like = 0, float trust = 0, float respect = 0, float attach = 0, float romance = 0, float fear = 0, float grudge = 0, float jealous = 0, string memory = null, string tag = null, string untag = null)
        {
            if (a == b || a == null || b == null) return;
            var r = S.R(a, b);
            // diminishing returns: repeated small positives shrink (no infinite gift farming)
            float dim = 1f / (1f + r.Talks * 0.08f);
            r.Like = MathX.Clamp(r.Like + (like > 0 ? like * dim : like), -1, 1); r.Trust = MathX.Clamp(r.Trust + (trust > 0 ? trust * dim : trust), -1, 1);
            r.Respect = MathX.Clamp(r.Respect + respect, -1, 1); r.Attach = MathX.Clamp(r.Attach + attach, 0, 1); r.Romance = MathX.Clamp(r.Romance + romance, 0, 1);
            r.Fear = MathX.Clamp(r.Fear + fear, 0, 1); r.Grudge = MathX.Clamp(r.Grudge + grudge, 0, 1); r.Jealous = MathX.Clamp(r.Jealous + jealous, 0, 1);
            if (memory != null) { r.Memory.Add($"{ClockFmt.DayHM(S.Clock)} {memory}"); if (r.Memory.Count > 30) r.Memory.RemoveAt(0); }
            if (tag != null) r.Tags.Add(tag); if (untag != null) r.Tags.Remove(untag);
            // emergent labels
            if (r.Like > 0.45f && r.Trust > 0.35f && S.R(b, a).Like > 0.4f) r.Tags.Add("friend");
            if (r.Grudge > 0.5f) r.Tags.Add("grudge"); else if (r.Grudge < 0.2f) r.Tags.Remove("grudge");
            if (r.Like < -0.35f && S.R(b, a).Like < -0.3f) r.Tags.Add("enemy");
            if (a == Cast.Player || b == Cast.Player) S.Emit(GameEventType.Relationship, a, b, text: memory);
        }

        /// <summary>Murder pressure from real knowledge. Returns (pressure, best target, motive tag). Never grows from mere elapsed time.</summary>
        public static (float pressure, string target, string motive) Pressure(Simulation sim, Actor a)
        {
            var S = sim.S; var c = a.Def; var k = S.K(a.Id);
            float wish = c.P.WishDesire;
            // the premise itself (a hidden killer gets the wish) is always there; it grows only through events:
            // CH09 deadline, witnessing trials/executions, failed non-violent attempts, stress/anger from real conflicts
            float wishPush = wish * 0.55f;
            if (S.RuleActive("CH09") && S.Rule("CH09").Targets.Contains(a.Id)) wishPush += wish * 0.4f;
            wishPush += Math.Min(0.45f, S.Settlements.Count(s => s.Loop == S.Loop) * 0.13f * wish);   // each execution witnessed: "살아남아 소원을" pressure
            wishPush += a.Needs.Grief * 0.1f + (1 - a.Needs.Energy) * 0.05f;
            wishPush += a.Needs.Stress * 0.35f + a.Needs.Anger * 0.2f + c.P.Aggression * 0.25f;
            if (S.Flags.TryGetValue("altfail:" + a.Id, out var af)) wishPush += (float)Math.Min(0.4, af * 0.12);
            if (S.Flags.TryGetValue("desp:" + a.Id, out var desp)) wishPush *= (float)desp;   // per-loop temperament (a different person breaks first each loop)
            if (S.Incidents.Values.Any(i => i.Loop == S.Loop && i.Chapter == S.Chapter)) wishPush += a.Needs.Fear * 0.15f;
            string bestT = null; float bestV = 0; string motive = "wish";
            foreach (var t in S.Living)
            {
                if (t.Id == a.Id) continue;
                var r = S.R(a.Id, t.Id);
                float v = r.Grudge * (0.6f + c.P.Grudge * 0.6f) + r.Jealous * c.P.Jealousy * 0.6f + r.Fear * (k.Facts.Contains("threat:" + t.Id) ? 0.9f : 0.25f);
                if (k.Facts.Contains("knows-my-secret:" + t.Id)) v += 0.35f * (1 - c.P.Honesty);
                // wish-driven target choice (the "convenient" victim): weak ties, isolated, low threat
                float conv = wishPush * (0.5f + 0.5f * (1 - MathX.Clamp01(r.Attach + Math.Max(0, r.Like)))) * (t.IsPlayer ? 0.85f : 1f);
                float total = Math.Max(v, conv * 0.8f) + Math.Min(v, conv) * 0.3f;
                // strong bonds block
                total -= (r.Attach * 0.9f + Math.Max(0, r.Like) * 0.6f + (r.Tags.Contains("lover") ? 0.6f : 0) + (r.Tags.Contains("family") ? 0.8f : 0));
                if (total > bestV) { bestV = total; bestT = t.Id; motive = v > conv ? (r.Grudge > r.Fear ? "grudge" : r.Fear > r.Jealous ? "fear" : "jealousy") : "wish"; }
            }
            // restraint (morality, fear, empathy, this loop's state of mind) plus the wall nobody has a reason to cross yet (Conscience)
            float inhibit = Conscience.Inhibit(S, a);
            return (bestV - inhibit, bestT, motive);
        }

    }
}
