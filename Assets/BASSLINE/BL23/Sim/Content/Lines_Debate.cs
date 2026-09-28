using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// Lines for the debate 심판 (Sim/Trial/Debate). Plain spoken Korean, one register each (존댓말 / 반말), several variants
    /// per moment so nobody repeats themselves. Slots: {victim} {target} {holder} {place} {time} {item} {sound} {glyph} {with}
    /// {fragment} {reason} — filled with names already in their call form; particles are fixed afterwards ("이(가)" → "이"/"가").
    /// The content of a theory is always the holder's own perception ({fragment}); the family only gives the sentence its shape.
    /// </summary>
    public static class DebateLines
    {
        static readonly Dictionary<string, (string[] p, string[] c)> L = new Dictionary<string, (string[] p, string[] c)>
        {
            // ---------------- theories (one checkable claim each, in the holder's own words)
            ["th_writing"] = (new[] {
                "{victim}이(가) 마지막에 남긴 글자, 다들 보셨죠. 「{glyph}」. 이 저택에서 그 글자로 시작하는 이름은 {target}뿐이에요.",
                "피로 쓴 「{glyph}」… 숨이 끊어지기 전에 범인 이름을 남기려 한 거예요. {target}이에요.",
                "저는 그 글씨를 바로 앞에서 봤어요. 「{glyph}」. 억지로 다르게 읽을 이유가 없잖아요. {target}이에요." },
                new[] {
                "「{glyph}」. 다들 봤잖아. {victim}이(가) 마지막에 {target} 이름을 쓴 거야.",
                "피로 쓴 「{glyph}」 봤지? 죽어 가면서 쓴 거야. {target} 말고 누가 있어.",
                "난 그 글씨 코앞에서 봤어. 「{glyph}」. 그냥 읽으면 {target}이야." }),
            ["th_place_found"] = (new[] {
                "고인 피도, 몸싸움 흔적도 전부 {place}에 있었어요. 거기서 당한 거예요.",
                "{fragment} 다른 데서 당했다면 흔적이 거기 남았겠죠. {place}예요.",
                "제가 본 건 {place}뿐이에요. 피가 거기 고여 있었어요. 거기서 맞은 거예요." },
                new[] {
                "피도 흔적도 다 {place}였잖아. 거기서 당한 거지.",
                "{fragment} 딴 데서 맞았으면 거기 뭐라도 남았겠지. {place}야.",
                "내가 본 건 {place}야. 피가 거기 고여 있었다고. 거기서 맞은 거야." }),
            ["th_place_heard"] = (new[] {
                "아니요, 소리는 {place} 쪽에서 났어요. {time}쯤, {sound} 소리가 한 번.",
                "{fragment} 제 귀가 틀리지 않았다면, {place} 쪽이었어요.",
                "저는 그때 소리를 들었어요. {place} 쪽이었어요. 발견된 곳이 아니라요." },
                new[] {
                "아니, 소리는 {place} 쪽이었어. {time}쯤에 {sound} 소리.",
                "{fragment} 내 귀가 멀쩡하면 {place} 쪽이야.",
                "나 그때 들었어. {place} 쪽이었다고. 발견된 데 말고." }),
            ["th_self_alibi"] = (new[] {
                "잠깐만요, 저 아니에요! {time}쯤이면 저는 {place}에 있었어요.",
                "왜 제 이름이 나와요? 그 시간엔 {place}에 있었다고요. 물어보시면 알 거예요.",
                "저는… {time}쯤 {place}에 있었어요. 그 글자 하나로 사람을 몰지 마세요." },
                new[] {
                "잠깐, 나 아니야! {time}쯤이면 나 {place}에 있었어.",
                "왜 내 이름이 나와? 그때 나 {place}에 있었다고. 물어보면 알아.",
                "나는… {time}쯤 {place}에 있었어. 글자 하나로 사람 몰지 마." }),
            ["th_culprit_backs"] = (new[] {
                "글씨는 거짓말을 안 하잖아요. 마지막 힘으로 쓴 거예요.",
                "사람이 죽기 직전엔 무슨 힘이든 나요. 그 글자, 그냥 넘길 게 아니에요.",
                "저도 믿고 싶진 않아요. 하지만 {victim}이(가) 남긴 걸 무시할 순 없잖아요." },
                new[] {
                "글씨는 거짓말 안 해. 마지막 힘으로 쓴 거지.",
                "죽기 직전엔 없던 힘도 나. 그 글자, 대충 넘길 게 아니야.",
                "나도 믿기 싫어. 근데 {victim}이(가) 남긴 걸 무시할 거야?" }),
            ["th_culprit_place"] = (new[] {
                "{place}예요. 거기 말고 다른 데서 무슨 일이 있었다는 흔적, 저는 못 봤어요.",
                "복잡하게 생각하지 말죠. 쓰러진 자리가 당한 자리예요." },
                new[] {
                "{place}야. 딴 데서 뭔 일 있었다는 흔적, 난 못 봤어.",
                "복잡하게 생각하지 마. 쓰러진 자리가 당한 자리야." }),
            ["th_alibi_culprit"] = (new[] {
                "저는 그때 {place}에 있었어요. {with}도 저를 봤을 거예요.",
                "{time}쯤이요? 저는 {place}에 있었습니다. {with}한테 물어보세요.",
                "제가 어디 있었는지는 {with}이(가) 알아요. {place}였어요." },
                new[] {
                "난 그때 {place}에 있었어. {with}도 날 봤을걸.",
                "{time}쯤? 나 {place}에 있었어. {with}한테 물어봐.",
                "내가 어디 있었는지는 {with}이(가) 알아. {place}였어." }),
            ["th_held"] = (new[] {
                "그러고 보니… {time}쯤 {place}에서 {target}이(가) {item}을(를) 들고 있었어요. 전 그게 계속 마음에 걸려요.",
                "{fragment} 그걸 왜 들고 다녔는지, {target}한테 먼저 들어야 해요.",
                "{target}의 손에 {item}이(가) 있었어요. 제 눈으로 봤어요." },
                new[] {
                "그러고 보니까… {time}쯤 {place}에서 {target}이(가) {item} 들고 있었어. 그게 계속 걸려.",
                "{fragment} 그걸 왜 들고 다녔는지 {target}한테 먼저 들어야지.",
                "{target} 손에 {item} 있었어. 내 눈으로 봤다고." }),
            ["th_near"] = (new[] {
                "{time}쯤 {place} 근처에서 {target}을(를) 봤어요. 그 시간에 거기 있던 사람이 있다는 거예요.",
                "{fragment} 그때 거기 있었던 건 {target}이에요." },
                new[] {
                "{time}쯤 {place} 근처에서 {target} 봤어. 그 시간에 거기 누가 있었다는 거지.",
                "{fragment} 그때 거기 있던 건 {target}이야." }),
            ["th_tangent"] = (new[] {
                "혹시… 이 저택이 한 짓은 아닐까요? 밤마다 벽에서 소리가 나잖아요.",
                "저기, 진짜로 사람이 한 게 맞아요? 저는 아직도 그게…",
                "유령이 쓴 글씨라면요? …아, 아니에요. 죄송해요." },
                new[] {
                "혹시 이 집이 한 짓 아니야? 밤마다 벽에서 소리 나잖아.",
                "근데 진짜 사람이 한 거 맞아? 난 아직도 좀…",
                "유령이 쓴 거면? …아, 아니다. 됐어." }),
            ["re_tangent_down"] = (new[] { "지금 그런 얘기 할 때 아니에요.", "…그 얘긴 나중에 해요.", "제발 좀 진지하게요." },
                                   new[] { "지금 그런 소리 할 때야?", "그 얘긴 나중에 하자.", "좀 진지하게 해." }),

            // ---------------- the room pushing back (interruptions, doubts, corrections, pile-ons)
            ["re_doubt"] = (new[] { "잠깐만요, {holder}. 그거 직접 본 거예요?", "{holder}, 그 얘기 어디서 나온 거예요?", "듣기엔 그럴듯한데… 확인한 거예요, {holder}?" },
                            new[] { "잠깐, {holder}. 그거 직접 본 거야?", "{holder}, 그거 어디서 나온 얘기야?", "그럴듯한데… 확인은 한 거야, {holder}?" }),
            ["re_pileon"] = (new[] { "저도 그게 이상했어요.", "맞아요. 제가 봐도 그래요." }, new[] { "나도 그게 이상했어.", "맞아. 내가 봐도 그래." }),
            ["re_protest"] = (new[] { "말도 안 돼요! 제가 왜 {victim}을(를)…", "지금 저를 의심하는 거예요? 기가 막혀서…", "아니라고요! 몇 번을 말해요!" },
                              new[] { "말도 안 돼! 내가 왜 {victim}을(를)…", "지금 나 의심하는 거야? 어이가 없네.", "아니라고! 몇 번을 말해!" }),
            ["re_fear"] = (new[] { "그럼… 범인이 아직 이 안에 있다는 거잖아요.", "그런 사람이 여기 서 있다고요? 저 옆에?" },
                           new[] { "그럼… 범인이 아직 여기 있다는 거잖아.", "그런 인간이 여기 서 있다고? 내 옆에?" }),
            ["re_scoff"] = (new[] { "그걸 지금 증거라고 내놓는 거예요?", "하, 그 정도로 사람을 몰아요?" }, new[] { "그걸 지금 증거라고 내놓는 거야?", "하, 그 정도로 사람을 몰아?" }),

            // ---------------- answers when asked how they know (캐묻기)
            ["ask_saw"] = (new[] { "직접 봤어요. {fragment}", "제 눈으로 봤어요. {fragment} 그건 확실해요." }, new[] { "직접 봤어. {fragment}", "내 눈으로 봤다니까. {fragment} 그건 확실해." }),
            ["ask_heard"] = (new[] { "봤다고는 안 했어요. 들었어요. {fragment}", "소리만 들었어요. {fragment}" }, new[] { "봤다곤 안 했어. 들었어. {fragment}", "소리만 들었어. {fragment}" }),
            ["ask_guess"] = (new[] { "…본 건 아니에요. 그냥 그렇게 생각했어요.", "확인한 건 아니에요. 그럴 것 같았을 뿐이에요." }, new[] { "…본 건 아니야. 그냥 그렇게 생각했어.", "확인한 건 아니고. 그럴 것 같았을 뿐이야." }),
            ["ask_hearsay"] = (new[] { "{with}이(가) 그랬어요. 저는 들은 대로 말한 거예요.", "제가 본 게 아니라 {with}한테 들었어요." }, new[] { "{with}이(가) 그랬어. 난 들은 대로 말한 거야.", "내가 본 게 아니라 {with}한테 들었어." }),
            ["ask_correct"] = (new[] { "…다시 생각해 보니, 얼굴까지 본 건 아니에요. {fragment}", "아니, 잠깐만요. 제가 확실히 본 건 {fragment} 거기까지예요." },
                               new[] { "…다시 생각해 보니까 얼굴까진 못 봤어. {fragment}", "아니, 잠깐. 내가 확실히 본 건 {fragment} 거기까지야." }),
            ["ask_owner_explains"] = (new[] { "그건… {reason}", "제가 들고 있었던 건 맞아요. 하지만 {reason}" }, new[] { "그건… {reason}", "내가 들고 있던 건 맞아. 근데 {reason}" }),

            // ---------------- results
            ["res_holder_concede"] = (new[] { "…그렇네요. 제가 잘못 봤어요.", "그 사진을 보니… 할 말이 없네요.", "…제가 성급했어요. 미안해요." },
                                      new[] { "…그러네. 내가 잘못 봤어.", "그 사진 보니까… 할 말이 없다.", "…내가 성급했어. 미안." }),
            ["res_holder_stubborn"] = (new[] { "그래도… 그래도 이상한 건 이상한 거잖아요!", "…그게 다 설명이 된다고요? 정말요?" }, new[] { "그래도… 이상한 건 이상한 거잖아!", "…그걸로 다 설명된다고? 진짜?" }),
            ["res_accused_relief"] = (new[] { "…고마워요. 이제 좀 숨이 쉬어지네요.", "봐요, 제가 아니라고 했잖아요." }, new[] { "…고마워. 이제 좀 숨 쉬겠다.", "봐, 나 아니라고 했잖아." }),
            ["res_flip_owner"] = (new[] { "그 사진은 그렇게 읽는 게 아니에요. {reason}", "그건 제 일이에요. {reason}" }, new[] { "그 사진 그렇게 읽는 거 아니야. {reason}", "그건 내 일이야. {reason}" }),
            ["res_culprit_shaken"] = (new[] { "…그게 뭐 어쨌다는 거예요.", "그 정도로 뭘 알 수 있는데요?" }, new[] { "…그게 뭐 어쨌다고.", "그걸로 뭘 알 수 있는데?" }),

            // ---------------- reversals and the push toward a scapegoat
            ["push_scapegoat"] = (new[] { "그럼 남는 건 {target}이에요. {fragment}", "{target}, 그 시간에 어디 있었는지 똑바로 말해 봐요.", "다들 {target}은(는) 왜 안 봐요? {fragment}" },
                                  new[] { "그럼 남는 건 {target}이야. {fragment}", "{target}, 그 시간에 어디 있었는지 똑바로 말해 봐.", "다들 {target}은(는) 왜 안 봐? {fragment}" }),
            ["push_echo"] = (new[] { "…듣고 보니 그래요.", "저도 그 생각 했어요." }, new[] { "…듣고 보니 그러네.", "나도 그 생각 했어." }),

            // ---------------- the duel
            ["duel_demand"] = (new[] { "…그래서요. 제가 했다는 건 어디 있는데요?", "사진 몇 장으로 사람을 잡겠다는 거예요? 증거를 대 보세요.", "좋아요. 그럼 증거를 보여 주세요. 제가 했다는 증거를." },
                               new[] { "…그래서. 내가 했다는 건 어디 있는데.", "사진 몇 장으로 사람 잡겠다고? 증거 대 봐.", "됐고, 증거 대 봐. 내가 했다는 거." }),
            ["duel_retreat"] = (new[] { "…알았어요, 그건 인정해요. {concede} 하지만 {keeps}", "그래요, 그건 맞아요. 그렇다고 제가 {victim}을(를) 해쳤다는 건 아니잖아요." },
                                new[] { "…알았어, 그건 인정할게. {concede} 근데 {keeps}", "그래, 그건 맞아. 그렇다고 내가 {victim}을(를) 해쳤다는 건 아니잖아." }),
            ["duel_attack_player"] = (new[] { "민혁 씨는요? 민혁 씨는 그때 어디 있었는데요?", "남의 말만 캐지 말고 민혁 씨부터 말해 보시죠." }, new[] { "민혁 너는? 넌 그때 어디 있었는데?", "남 얘기만 하지 말고 너부터 말해 봐." }),
            ["break_quiet"] = (new[] { "…그 사진까지 있을 줄은 몰랐네요.", "…그렇게까지 다 보였어요?", "……." }, new[] { "…그 사진까지 있을 줄은 몰랐네.", "…그렇게까지 다 보였어?", "……." }),
            ["innocent_defend"] = (new[] { "저 아니에요. {fragment}", "왜 저예요? {fragment} 확인해 보세요." }, new[] { "나 아니야. {fragment}", "왜 나야? {fragment} 확인해 봐." }),

            // ---------------- 민혁 (player) — plain, polite
            ["p_show"] = (new[] { "이 사진을 보세요.", "이걸 보면 달라요.", "{holder}, 이 은판을 봐 주세요." }, new string[0]),
            ["p_ask"] = (new[] { "{holder}, 그걸 어떻게 아셨어요? 직접 보셨어요?", "{holder}, 본 거예요, 들은 거예요?" }, new string[0]),
            ["p_listen"] = (new[] { "…조금 더 들어 볼게요.", "계속 말해 주세요." }, new string[0]),
            ["p_reframe"] = (new[] { "그렇다면 문제는… {reason}", "그럼 이제 물어야 할 건 이거예요. {reason}" }, new string[0]),
            ["p_accuse"] = (new[] { "{target}. 당신이에요.", "제 생각엔… {target}이에요." }, new string[0]),
        };

        /// <summary>A line for <paramref name="speaker"/> at <paramref name="key"/>, never the same variant twice for that speaker in this 심판.</summary>
        public static string Say(GameState S, string speaker, string key, Dictionary<string, string> slots, List<string> used, string salt)
        {
            if (!L.TryGetValue(key, out var v)) return null;
            bool polite = speaker == Cast.Player || speaker == Cast.Butler || (Cast.Get(speaker)?.Speech?.PoliteDefault ?? true);
            var pool = polite ? v.p : v.c; if (pool.Length == 0) pool = polite ? v.c : v.p; if (pool.Length == 0) return null;
            var order = Enumerable.Range(0, pool.Length).OrderBy(i => MurderHash.U01(S, "dl:" + speaker + ":" + key + ":" + salt + ":" + i)).ToList();
            int pick = order.FirstOrDefault(i => used == null || !used.Contains(speaker + "|" + key + "|" + i));
            if (used != null && order.All(i => used.Contains(speaker + "|" + key + "|" + i))) pick = order[0];
            used?.Add(speaker + "|" + key + "|" + pick);
            string text = pool[pick];
            if (slots != null)
                foreach (var kv in slots)
                {
                    string val = kv.Value ?? "", k = "{" + kv.Key + "}";
                    bool b = Batchim(val);
                    // the copula after a name: 시온이에요 / 민서예요, 시온이야 / 민서야, 시온이잖아 / 민서잖아
                    text = text.Replace(k + "이에요", val + (b ? "이에요" : "예요")).Replace(k + "이야", val + (b ? "이야" : "야")).Replace(k + "이잖아", val + (b ? "이잖아" : "잖아")).Replace(k, val);
                }
            text = System.Text.RegularExpressions.Regex.Replace(text, "\\{[a-z]+\\}", "");
            // a speaker's own filler now and then, for the talkative ones
            var st = Cast.Get(speaker)?.Speech;
            if (st != null && st.Fillers != null && st.Fillers.Length > 0 && st.Verbosity >= 0.55f && MurderHash.U01(S, "dlf:" + speaker + ":" + key + ":" + salt) < 0.22)
                text = st.Fillers[(int)(MurderHash.U01(S, "dlf2:" + speaker + ":" + salt) * st.Fillers.Length) % st.Fillers.Length] + " " + text;
            return LineBank.FixParticles(text.Replace("  ", " ").Trim());
        }

        public static bool Has(string key) => L.ContainsKey(key);

        static bool Batchim(string w)
        {
            if (string.IsNullOrEmpty(w)) return false;
            char c = w[w.Length - 1];
            return c >= 0xAC00 && c <= 0xD7A3 ? (c - 0xAC00) % 28 != 0 : char.IsDigit(c) && "0136789".IndexOf(c) >= 0;
        }
    }
}
