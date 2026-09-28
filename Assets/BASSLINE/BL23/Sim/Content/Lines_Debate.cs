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
                "{fragment} 다른 데서 당했다면 흔적이 거기 남았겠죠. {place}이에요.",
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
                "{place}이에요. 거기 말고 다른 데서 무슨 일이 있었다는 흔적, 저는 못 봤어요.",
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
            ["re_scoff"] = (new[] { "그걸 지금 증거라고 내놓는 거예요?", "하, 그 정도로 사람을 몰아요?", "…그 은판이 그 얘기랑 무슨 상관이에요?", "민혁 씨, 다시 보세요. 그건 아니에요.", "그건 아무것도 증명 못 해요." },
                            new[] { "그걸 지금 증거라고 내놓는 거야?", "하, 그 정도로 사람을 몰아?", "…그 은판이 그 얘기랑 무슨 상관인데?", "민혁, 다시 봐. 그건 아니야.", "그건 아무것도 증명 못 해." }),

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
            ["break_quiet"] = (new[] { "…그 사진까지 있을 줄은 몰랐네요.", "…그렇게까지 다 보였어요?", "…결국, 거기까지 보였네요." }, new[] { "…그 사진까지 있을 줄은 몰랐네.", "…그렇게까지 다 보였어?", "…결국, 거기까지 보였네." }),
            ["innocent_defend"] = (new[] { "저 아니에요. {fragment}", "왜 저예요? {fragment} 확인해 보세요." }, new[] { "나 아니야. {fragment}", "왜 나야? {fragment} 확인해 봐." }),


            // ---------------- truth-side and case-shaped theories (v0 director, DebateTheories.cs)
            ["th_truth_writing"] = (new[] {
                "잠깐만요. 제가 {victim}을(를) 봤을 땐 거의 즉사였어요. 그 상태로 글씨를 쓸 수 있었을까요?",
                "그 글씨, {victim}이(가) 쓴 게 아닐 수도 있어요. 그렇게 맞고 손가락을 움직일 틈이 있었을지…",
                "저는 시신을 가까이서 봤어요. 쓰러지자마자 숨이 멎었을 거예요. 그럼 그 글씨는 누가 쓴 거죠?" },
                new[] {
                "잠깐. 내가 {victim}을(를) 봤을 땐 거의 즉사였어. 그 상태로 글씨를 썼다고?",
                "그 글씨, {victim}이(가) 쓴 게 아닐 수도 있어. 그렇게 맞고 손가락 움직일 틈이 있었겠냐고.",
                "나 시신 가까이서 봤어. 쓰러지자마자 끝났을 거야. 그럼 그 글씨는 누가 쓴 건데?" }),
            ["th_place_marks"] = (new[] { "{place}에도 {fragment}이(가) 있었어요. 일은 거기서 먼저 벌어진 거예요.", "발견된 곳만 보면 안 돼요. {place}에 {fragment}이(가) 남아 있었어요." },
                                  new[] { "{place}에도 {fragment} 있었어. 일은 거기서 먼저 난 거야.", "발견된 데만 보지 마. {place}에 {fragment} 남아 있었다고." }),
            ["th_time_body"] = (new[] { "몸이 아직 따뜻했어요. {time}보다 이르게 숨졌을 리 없어요.", "제가 만졌을 때 {victim}은(는) 따뜻했어요. {time}쯤이에요, 분명히." },
                                new[] { "몸이 아직 따뜻했어. {time}보다 일찍 죽었을 리 없어.", "내가 만졌을 때 {victim} 따뜻했다고. {time}쯤이야." }),
            ["th_time_heard"] = (new[] { "아니에요. 전 {time}에 {sound} 소리를 들었어요. 그때였어요.", "{time}쯤 {sound} 소리가 났어요. 몸이 따뜻했다고 해도, 그 소리는 거짓말을 안 해요." },
                                 new[] { "아니야. 나 {time}에 {sound} 소리 들었어. 그때였다고.", "{time}쯤 {sound} 소리 났어. 몸이 따뜻했든 말든, 그 소리는 진짜야." }),
            ["th_culprit_time"] = (new[] { "체온은 속일 수 없잖아요. 시신이 제일 정직해요.", "만져 본 사람이 따뜻했다잖아요. 그걸로 된 거 아니에요?" },
                                   new[] { "체온은 못 속이잖아. 시신이 제일 정직해.", "만져 본 사람이 따뜻했다잖아. 그럼 된 거 아냐?" }),
            ["th_sealed"] = (new[] { "문은 안에서 잠겨 있었어요. 제가 열고 들어갔으니까 알아요.", "{place} 문, 잠겨 있었어요. 아무도 드나들 수 없었어요." },
                             new[] { "문은 안에서 잠겨 있었어. 내가 열고 들어갔으니까 알아.", "{place} 문 잠겨 있었다고. 아무도 못 드나들었어." }),
            ["th_sealed_thread"] = (new[] { "그런데 문 아래쪽에… 실 같은 게 지나간 자국이 있었어요. 밖에서도 잠글 수 있었던 거 아닐까요?", "밀실이라기엔 이상해요. 문틈에 뭔가 끌린 자국이 있었거든요." },
                                    new[] { "근데 문 아래에 실 같은 게 지나간 자국 있었어. 밖에서도 잠글 수 있었던 거 아냐?", "밀실이라기엔 이상해. 문틈에 뭔가 끌린 자국이 있었거든." }),
            ["th_culprit_sealed"] = (new[] { "밀실이었던 건 사실이잖아요. 그걸 어떻게 설명해요?", "문이 잠겨 있었다는데, 그 이상 뭐가 필요해요?" },
                                     new[] { "밀실이었던 건 사실이잖아. 그걸 어떻게 설명할 건데?", "문이 잠겨 있었다며. 뭐가 더 필요해?" }),
            ["th_weapon_decoy"] = (new[] { "시신 옆에 피 묻은 {item}이(가) 있었잖아요. 그걸로 친 거예요.", "흉기는 {item}이에요. 피가 묻은 채로 거기 있었다고요." },
                                   new[] { "시신 옆에 피 묻은 {item} 있었잖아. 그걸로 친 거야.", "흉기는 {item}이야. 피 묻은 채로 거기 있었다고." }),
            ["th_weapon_wound"] = (new[] { "상처를 보면 달라요. {wound}이었어요. 그 물건으로 생길 상처가 아니에요.", "저는 상처를 봤어요. {wound}. 그걸로는 그렇게 안 돼요." },
                                   new[] { "상처를 보면 달라. {wound}이었어. 그걸로 생길 상처가 아니야.", "나 상처 봤어. {wound}. 그걸론 그렇게 안 돼." }),
            ["th_culprit_weapon"] = (new[] { "피 묻은 {item}이(가) 바로 옆에 있었어요. 그보다 확실한 게 어디 있어요?", "눈앞에 흉기가 있는데 왜 다른 걸 찾아요?" },
                                     new[] { "피 묻은 {item}이(가) 바로 옆에 있었잖아. 뭐가 더 확실해?", "눈앞에 흉기가 있는데 왜 딴 걸 찾아?" }),
            ["th_accident"] = (new[] { "{victim}은(는) 사고로 떨어진 거예요. 거기 원래 위험했잖아요.", "누가 한 게 아니라… 사고예요. 그렇게 믿고 싶어요." },
                               new[] { "{victim} 사고로 떨어진 거야. 거기 원래 위험했잖아.", "누가 한 게 아니라… 사고야. 그렇게 믿고 싶어." }),
            ["th_natural"] = (new[] { "{victim}, 요즘 몸이 안 좋았잖아요. 병이었을 거예요.", "누가 손을 댄 흔적은 없었어요. 그냥… 쓰러진 거예요." },
                              new[] { "{victim} 요즘 몸 안 좋았잖아. 병이었을 거야.", "누가 손댄 흔적은 없었어. 그냥… 쓰러진 거야." }),
            ["th_suicide"] = (new[] { "{victim}이(가) 스스로… 그랬을 거예요. 유서도 있었잖아요.", "마지막 편지를 봤어요. 스스로 택한 거예요." },
                              new[] { "{victim}이(가) 스스로… 그랬을 거야. 유서도 있었잖아.", "마지막 편지 봤어. 스스로 택한 거야." }),
            ["th_not_accident"] = (new[] { "사고라기엔 {victim}의 몸에 남은 게 이상해요. 누군가 손을 댄 흔적이 있었어요.", "저는 시신을 봤어요. 혼자 그렇게 될 수는 없어요." },
                                   new[] { "사고라기엔 {victim} 몸에 남은 게 이상해. 누가 손댄 흔적이 있었어.", "나 시신 봤어. 혼자 그렇게 될 수는 없어." }),
            ["th_culprit_accident"] = (new[] { "사고예요. 누가 {victim}에게 그런 짓을 하겠어요.", "우리 중에 그럴 사람은 없어요. 사고로 두는 게 맞아요." },
                                       new[] { "사고야. 누가 {victim}한테 그런 짓을 하겠어.", "우리 중에 그럴 사람 없어. 사고로 두는 게 맞아." }),
            ["th_link_witness"] = (new[] { "{time}쯤 {place}에서 {target}을(를) 봤어요. 그 시간에, 거기서요.", "말할까 말까 했는데… {time}쯤 {place}에 {target}이(가) 있었어요." },
                                   new[] { "{time}쯤 {place}에서 {target} 봤어. 그 시간에, 거기서.", "말할까 말까 했는데… {time}쯤 {place}에 {target} 있었어." }),
            ["th_link_witness_held"] = (new[] { "{time}쯤 {place}에서 {target}을(를) 봤어요. 손에 {item}을(를) 들고 있었어요.", "…{target}이(가) {item}을(를) 들고 {place}에 있었어요. {time}쯤이에요." },
                                        new[] { "{time}쯤 {place}에서 {target} 봤어. 손에 {item} 들고 있었어.", "…{target}이(가) {item} 들고 {place}에 있었어. {time}쯤." }),

            // ---------------- the room drawn in (the floor, requests, alibis, defenders)
            ["re_where_all"] = (new[] { "잠깐만요, 그럼 다른 사람들은요? 그 시간에 다들 어디 있었는데요?", "저만 몰아세우지 말고요. 그때 다들 어디 있었는지부터 말해 봐요." },
                                new[] { "잠깐, 그럼 다른 사람들은? 그 시간에 다들 어디 있었는데?", "나만 몰지 말고. 그때 다들 어디 있었는지부터 말해 봐." }),
            ["re_confirm_alibi"] = (new[] { "…{target}, {place}에 있었어요. 제가 봤어요. {time}쯤이었나.", "맞아요, {place}에서 {target}을(를) 봤어요. {time}쯤에요." },
                                    new[] { "…{target}, {place}에 있었어. 내가 봤어. {time}쯤이었나.", "맞아, {place}에서 {target} 봤어. {time}쯤." }),
            ["re_request"] = (new[] { "저… 할 말이 있어요. {place} 얘기예요.", "잠깐만요. 그때 {place} 쪽에서 본 게 있어요." },
                              new[] { "저기… 할 말 있어. {place} 얘기야.", "잠깐. 그때 {place} 쪽에서 본 게 있어." }),
            ["re_request_plate"] = (new[] { "저… {plate}. 그거, 제가 본 거예요.", "{plate}을(를) 다시 보세요. 거기 답이 있어요." },
                                    new[] { "저기… {plate}. 그거 내가 본 거야.", "{plate} 다시 봐. 거기 답이 있어." }),
            ["re_defend_other"] = (new[] { "{target}이(가) 그랬을 리 없어요. 제가 알아요.", "{target}은(는) 아니에요. 제가 보증할게요." },
                                   new[] { "{target}이(가) 그랬을 리 없어. 내가 알아.", "{target}은(는) 아니야. 내가 보증해." }),
            ["ask_culprit_detail"] = (new[] { "몇 번을 말해요. {place}에 있었다니까요. {with}도 봤어요.", "{place}이었어요. 거짓말할 이유가 없잖아요. {with}한테 물어보세요." },
                                      new[] { "몇 번을 말해. {place}에 있었다니까. {with}도 봤어.", "{place}이었어. 거짓말할 이유가 없잖아. {with}한테 물어봐." }),
            ["duel_ask_where"] = (new[] { "{place}에 있었다니까요. 혼자였다고 거짓말이 되는 건 아니잖아요.", "몇 번을 물어도 같아요. {place}이었어요." },
                                  new[] { "{place}에 있었다니까. 혼자였다고 거짓말이 되는 건 아니잖아.", "몇 번을 물어도 같아. {place}이었어." }),
            ["duel_ask_weapon"] = (new[] { "본 적도 없다니까요. 그런 걸 제가 왜 들고 다녀요?", "그 물건이 어디 있었는지도 몰랐어요. 정말이에요." },
                                   new[] { "본 적도 없다니까. 그런 걸 내가 왜 들고 다녀?", "그게 어디 있었는지도 몰랐어. 진짜야." }),
            ["duel_ask_push"] = (new[] { "제 눈으로 봤다니까요. {target}이(가) 그쪽으로 가는 걸요.", "제가 없는 말을 지어낸다는 거예요? 봤어요, 분명히." },
                                 new[] { "내 눈으로 봤다니까. {target}이(가) 그쪽으로 가는 거.", "내가 없는 말을 지어낸다는 거야? 봤어, 분명히." }),
            ["duel_ask_final"] = (new[] { "질문이 틀렸어요. 증거가 있느냐고요.", "말 돌리지 마세요. 증거를 대 보시라니까요." },
                                  new[] { "질문이 틀렸어. 증거가 있냐고.", "말 돌리지 마. 증거 대 보라니까." }),
            ["duel_scoff"] = (new[] { "그걸로 절 몰겠다고요?", "하… 그 사진이 뭘 말해 준다는 거예요?", "고작 그거예요?", "좀 더 제대로 된 걸 가져오세요." },
                              new[] { "그걸로 날 몰겠다고?", "하… 그 사진이 뭘 말해 준다는 건데?", "고작 그거야?", "좀 더 제대로 된 걸 가져와." }),
            ["ask_with_time"] = (new[] { "…잠깐만요. 제가 {target}을(를) 본 건 {time}이에요. 그 전은… 저도 몰라요.", "제가 본 건 {reason}의 일이에요. 그 전에 어디 있었는지는 모르겠어요." },
                                 new[] { "…잠깐. 내가 {target} 본 건 {time}이야. 그 전은… 나도 몰라.", "내가 본 건 {reason}의 일이야. 그 전엔 어디 있었는지 몰라." }),
            ["ask_again"] = (new[] { "이미 다 말했잖아요.", "같은 걸 몇 번이나 물어요?" }, new[] { "이미 다 말했잖아.", "같은 거 몇 번이나 물어?" }),
            ["ask_plate"] = (new[] { "{plate}에 그렇게 찍혀 있잖아요. 제가 지어낸 게 아니에요.", "제가 본 게 아니라 {plate}이(가) 그렇게 말하고 있어요." },
                             new[] { "{plate}에 그렇게 찍혀 있잖아. 내가 지어낸 거 아니야.", "내가 본 게 아니라 {plate}이(가) 그렇게 말하잖아." }),
            ["res_vindicated"] = (new[] { "…거봐요. 제 말이 맞았죠.", "그렇죠. 제가 본 게 맞았어요." }, new[] { "…거봐. 내 말이 맞았지.", "그렇지. 내가 본 게 맞았어." }),
            ["res_culprit_cornered"] = (new[] { "그, 그건… 잠깐만요. 그건 제가 말한 게 아니라…", "아니에요, 그건… 그건 달라요!" }, new[] { "그, 그건… 잠깐. 그건 내가 말한 게 아니라…", "아니야, 그건… 그건 달라!" }),
            ["npc_show"] = (new[] { "제가 대신 말할게요. {plate}을(를) 보세요.", "{holder}, 이걸 보세요. {plate}." }, new[] { "내가 말할게. {plate} 봐.", "{holder}, 이거 봐. {plate}." }),
            ["npc_takes_floor"] = (new[] { "…더는 못 기다리겠어요.", "제가 할게요." }, new[] { "…더는 못 기다리겠다.", "내가 할게." }),

            // ---------------- the duel (DebateCulprit.cs)
            ["duel_open"] = (new[] { "…저요? 좋아요. 끝까지 들어 볼게요.", "제가 {victim}을(를)? …그래요, 어디 한번 말해 보세요." }, new[] { "…나? 좋아. 끝까지 들어 줄게.", "내가 {victim}을(를)? …그래, 어디 한번 말해 봐." }),
            ["duel_press"] = (new[] { "할 말 없어요? 그럼 제 말이 맞는 거죠.", "왜 조용해요? 대답해 보세요." }, new[] { "할 말 없어? 그럼 내 말이 맞는 거지.", "왜 조용해? 대답해 봐." }),
            ["duel_cut"] = (new[] { "그건… 그건 제가—", "그 사진은… 그건—" }, new[] { "그건… 그건 내가—", "그 사진은… 그건—" }),
            ["duel_hold"] = (new[] { "…이걸로 끝이에요? 그럼 제가 한 게 아니에요.", "그 정도로는 아무것도 증명 못 해요." }, new[] { "…이걸로 끝이야? 그럼 내가 한 거 아니야.", "그 정도론 아무것도 증명 못 해." }),
            ["vote_line"] = (new[] { "{target}. 제 돌은 거기 넣을게요.", "저는 {target}이에요.", "…{target}이에요. 확신은 없지만요.", "{target}. 그게 제 답이에요." },
                             new[] { "{target}. 내 돌은 거기 넣을게.", "난 {target}이야.", "…{target}. 확신은 없지만.", "{target}. 그게 내 답이야." }),
            ["vote_sure"] = (new[] { "…{target}이에요. 이젠 의심할 여지가 없어요.", "{target}. 다 들었으니까요.", "{target}이에요. 그 촛불이 다 꺼지는 걸 봤잖아요.", "망설일 이유가 없어요. {target}이에요.", "{target}. …미안하지만요." },
                             new[] { "…{target}이야. 이젠 의심할 여지가 없어.", "{target}. 다 들었으니까.", "{target}이야. 촛불 다 꺼지는 거 봤잖아.", "망설일 이유 없어. {target}.", "{target}. …미안하지만." }),


            // ---------------- the roll call (DebateRoom.cs)
            ["rc_open"] = (new[] { "시간은 {time}이에요. 그 시간에 다들 어디 있었는지부터 맞춰 봐요.", "{time}이에요. 한 사람씩, 그때 있던 곳을 말해 봐요." },
                           new[] { "{time}이야. 그 시간에 다들 어디 있었는지부터 맞춰 보자.", "{time}. 한 명씩 그때 있던 데 말해 봐." }),
            ["rc_with"] = (new[] { "저는 {place}에 있었어요. {with}하고 같이요.", "{time}이면… {place}요. {with}도 거기 있었어요." },
                           new[] { "나 {place}에 있었어. {with}하고 같이.", "{time}이면… {place}. {with}도 거기 있었어." }),
            ["rc_confirm"] = (new[] { "맞아요, {target}하고 같이 있었어요.", "네, 저도 {target}을(를) 봤어요. 같이 있었어요." },
                              new[] { "맞아, {target}하고 같이 있었어.", "응, 나도 {target} 봤어. 같이 있었어." }),
            ["rc_near"] = (new[] { "저는… {place} 근처에 있었어요. 그때는 아무것도 몰랐어요.", "{time}이면 {place} 쪽을 지나갔어요. …의심받을 줄은 알았어요." },
                           new[] { "나는… {place} 근처에 있었어. 그땐 아무것도 몰랐어.", "{time}이면 {place} 쪽 지나갔어. …의심받을 줄 알았어." }),
            ["rc_didnt_see"] = (new[] { "…어? 저는 {target}을(를) 못 봤는데요.", "잠깐만요, 저는 거기서 {target}을(를) 본 기억이 없어요." },
                                new[] { "…어? 난 {target} 못 봤는데.", "잠깐, 난 거기서 {target} 본 기억 없어." }),
            ["rc_alone"] = (new[] { "저는 {place}에 있었어요. 혼자였어요.", "{place}에 혼자 있었어요. 증명해 줄 사람은… 없네요.", "그 시간엔 {place}에 있었어요. 아무도 못 봤어요." },
                            new[] { "난 {place}에 있었어. 혼자.", "{place}에 혼자 있었어. 증명해 줄 사람은… 없네.", "그 시간엔 {place}에 있었어. 아무도 못 봤어." }),
            ["rc_sum"] = (new[] { "그럼 서로 같이 있었다고 확인된 사람들은 일단 빼도 되겠네요.", "서로 확인해 준 사람들은 일단 제외해요. 남은 사람들 얘기를 해야죠." },
                          new[] { "그럼 서로 같이 있었던 게 확인된 사람들은 일단 빼도 되겠네.", "서로 확인해 준 사람들은 일단 빼자. 남은 사람들 얘기를 해야지." }),

            // ---------------- 「상처」 and the amended theories
            ["th_wound_guess"] = (new[] { "{item}(으)로 {how} 거예요. 그날 저녁에 {item}을(를) 들고 다니는 사람을 봤다는 말도 있었고요.", "제 생각엔… {item}이에요. {item}(으)로 {how} 거예요." },
                                  new[] { "{item}(으)로 {how} 거야. 그날 저녁에 {item} 들고 다니는 사람 봤다는 얘기도 있었잖아.", "내 생각엔… {item}이야. {item}(으)로 {how} 거야." }),
            ["th_culprit_wound"] = (new[] { "{item}이(가) 맞을 거예요. 그 밖에 뭐가 있겠어요.", "저도 {item}이(가) 맞다고 생각해요." },
                                    new[] { "{item}이(가) 맞을 거야. 그거 말고 뭐가 있겠어.", "나도 {item}이(가) 맞다고 봐." }),
            ["ev_message"] = (new[] { "그럼… 누가 {victim}의 손 옆에 대신 써 놓았다는 거예요?", "…그럼 그 글씨는 누가, 언제 쓴 거죠?" },
                              new[] { "그럼… 누가 {victim} 손 옆에 대신 써 놨다는 거야?", "…그럼 그 글씨는 누가, 언제 쓴 건데?" }),
            ["ev_place"] = (new[] { "그럼… {place}에서 맞고, {found}까지 걸어 나온 거네요.", "…{place}에서 시작된 거라면, {victim}은(는) 도망치다가 쓰러진 거예요." },
                            new[] { "그럼… {place}에서 맞고 {found}까지 걸어 나온 거네.", "…{place}에서 시작된 거면, {victim}은(는) 도망치다가 쓰러진 거야." }),
            ["ev_wound"] = (new[] { "…그럼 흉기는 따로 있다는 거네요. 그건 어디 있는데요?", "제가 잘못 짚었네요. 그럼 뭘로…?" },
                            new[] { "…그럼 흉기는 따로 있다는 거네. 그건 어딨는데?", "내가 잘못 짚었네. 그럼 뭘로…?" }),
            ["ev_tod"] = (new[] { "그럼… 누가 시신을 데웠다는 거예요? 시간을 속이려고요?" }, new[] { "그럼… 누가 시신을 데웠다는 거야? 시간 속이려고?" }),
            ["ev_seal"] = (new[] { "그럼 밖에서 잠갔다는 거네요. 누군가 그렇게 보이게 만든 거예요." }, new[] { "그럼 밖에서 잠갔다는 거네. 누가 그렇게 보이게 만든 거야." }),
            ["ev_swap"] = (new[] { "그럼 그 물건엔 피만 발라 둔 거예요? 진짜 흉기는 따로 있고요?" }, new[] { "그럼 그건 피만 발라 둔 거야? 진짜 흉기는 따로 있고?" }),
            ["ev_cause"] = (new[] { "…그럼 사고처럼 보이게 누가 꾸몄다는 거네요." }, new[] { "…그럼 사고처럼 보이게 누가 꾸몄다는 거네." }),

            // ---------------- bystanders
            ["re_realize"] = (new[] { "그럼 우리, 여태 엉뚱한 사람을 의심하고 있었던 거예요?", "…그렇게 간단한 얘기가 아니었네요." }, new[] { "그럼 우리 여태 엉뚱한 사람 의심한 거야?", "…그렇게 간단한 얘기가 아니었네." }),
            ["re_framed"] = (new[] { "누가 제 이름을 써 놨다는 거예요? 누구예요, 대체!", "…저한테 뒤집어씌우려고 했다고요? 누가요?" }, new[] { "누가 내 이름을 써 놨다는 거야? 누구야, 대체!", "…나한테 뒤집어씌우려고 했다고? 누가?" }),
            ["re_duel_gasp"] = (new[] { "…정말이에요? {target}이(가)?", "말도 안 돼… {target}이(가) 그랬다고요?" }, new[] { "…진짜야? {target}이(가)?", "말도 안 돼… {target}이(가) 그랬다고?" }),
            ["re_betrayed"] = (new[] { "{target}, 당신이었어요? 저한테 뒤집어씌우려고…!", "그 글씨도, 그 말도 전부… {target}이(가) 한 거였어요?" }, new[] { "{target}, 너였어? 나한테 뒤집어씌우려고…!", "그 글씨도, 그 말도 전부… {target}이(가) 한 거였어?" }),
            ["re_grief"] = (new[] { "{victim}은(는)… 그렇게 가면 안 되는 사람이었어요.", "왜요… 왜 {victim}이었어요?" }, new[] { "{victim}은(는)… 그렇게 가면 안 되는 애였어.", "왜… 왜 하필 {victim}이었는데?" }),

            // ---------------- 민혁 (player) — plain, polite
            ["p_show"] = (new[] { "{plate}. 이걸 보세요.", "{holder}, {plate}을(를) 봐 주세요.", "{plate}. 이걸 보면 달라요." }, new string[0]),
            ["p_infer"] = (new[] { "그렇다면… {reason}.", "그렇다면 이렇게밖에 설명이 안 돼요. {reason}." }, new string[0]),
            ["p_ask"] = (new[] { "{holder}, 그걸 어떻게 아셨어요? 직접 보셨어요?", "{holder}, 본 거예요, 들은 거예요?" }, new string[0]),
            ["p_listen"] = (new[] { "…조금 더 들어 볼게요.", "계속 말해 주세요." }, new string[0]),
            ["p_reframe"] = (new[] { "그렇다면 문제는… {reason}", "그럼 이제 물어야 할 건 하나예요. {reason}" }, new string[0]),
            ["p_accuse"] = (new[] { "{target}. 당신이에요.", "제 생각엔… {target}이에요." }, new string[0]),
        };

        /// <summary>A line for <paramref name="speaker"/> at <paramref name="key"/>, never the same variant twice for that speaker in this 심판.</summary>
        public static string Say(GameState S, string speaker, string key, Dictionary<string, string> slots, List<string> used, string salt)
        {
            if (!L.TryGetValue(key, out var v)) return null;
            bool polite = speaker == Cast.Player || speaker == Cast.Butler || (Cast.Get(speaker)?.Speech?.PoliteDefault ?? true);
            var pool = polite ? v.p : v.c; if (pool.Length == 0) pool = polite ? v.c : v.p; if (pool.Length == 0) return null;
            var order = Enumerable.Range(0, pool.Length).OrderBy(i => MurderHash.U01(S, "dl:" + speaker + ":" + key + ":" + salt + ":" + i)).ToList();
            // never the same variant twice from one speaker; and, while there are others, not the one someone else just said
            string reg = polite ? "p" : "c";
            bool Mine(int i) => used != null && used.Contains(speaker + "|" + key + "|" + i);
            bool Anyone(int i) => used != null && used.Contains("*|" + key + "|" + reg + i);
            int pick = order.Where(i => !Mine(i) && !Anyone(i)).DefaultIfEmpty(-1).First();
            if (pick < 0) pick = order.Where(i => !Mine(i)).DefaultIfEmpty(order[0]).First();
            if (used != null) { used.Add(speaker + "|" + key + "|" + pick); if (!used.Contains("*|" + key + "|" + reg + pick)) used.Add("*|" + key + "|" + reg + pick); }
            string text = pool[pick];
            if (slots != null)
                foreach (var kv in slots)
                {
                    string val = kv.Value ?? "", k = "{" + kv.Key + "}";
                    bool b = Batchim(val);
                    // the copula after a name: 시온이에요 / 민서예요, 시온이야 / 민서야, 시온이잖아 / 민서잖아
                    text = text.Replace(k + "이에요", val + (b ? "이에요" : "예요")).Replace(k + "이야", val + (b ? "이야" : "야")).Replace(k + "이잖아", val + (b ? "이잖아" : "잖아")).Replace(k + "이었", val + (b ? "이었" : "였")).Replace(k, val);
                }
            text = System.Text.RegularExpressions.Regex.Replace(text, "\\{[a-z]+\\}", "");
            // a speaker's own filler now and then, for the talkative ones
            var st = Cast.Get(speaker)?.Speech;
            bool arguing = key.StartsWith("th_") || key.StartsWith("push_") || key == "re_doubt" || key == "re_where_all";   // fillers colour arguments, never a concession or a break
            if (arguing && !text.StartsWith("…") && st != null && st.Fillers != null && st.Fillers.Length > 0 && st.Verbosity >= 0.55f && MurderHash.U01(S, "dlf:" + speaker + ":" + key + ":" + salt) < 0.22)
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
