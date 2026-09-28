namespace BL23.Sim
{
    public static partial class LineBank
    {
        // ANY — 인물 전용 대사가 없을 때 쓰는 공용 대체. 성격색 없이 무난한 존댓말 1 + 반말 1.
        // 누구 입에서 나와도 어색하지 않게: 자기 이름·직업·취향·비밀을 말하지 않는다.
        // break_line은 진우·도윤·유스티 전용 연출이라 공용 대체를 두지 않는다.
        static void Init_ANY()
        {
            const string A = "ANY";

            // ───────── 일상 ─────────
            Add(A, "intro_self", P("처음 뵙겠습니다. 잘 부탁드려요."), C("안녕. 잘 부탁해."));
            Add(A, "greet", P("안녕하세요, {you}."), C("안녕, {you:아}."));
            Add(A, "greet_morning", P("좋은 아침이에요. 잘 주무셨어요?"), C("좋은 아침. 잘 잤어?"));
            Add(A, "greet_night", P("늦었네요. 조심히 들어가세요."), C("늦었다. 조심히 들어가."));
            Add(A, "greet_close", P("{you}, 반가워요. 기다렸어요."), C("{you:아}, 왔구나. 기다렸어."));
            Add(A, "greet_cold", P("무슨 일이세요?"), C("무슨 일이야?"));
            Add(A, "bye", P("그럼 나중에 봬요."), C("그럼 나중에 봐."));
            Add(A, "busy", P("미안해요, 지금 {act} 중이라서요."), C("미안, 지금 {act} 중이야."));
            Add(A, "small_talk", P("여기 온 뒤로 시간이 이상하게 흘러요."), C("여기 온 뒤로 시간 감각이 이상해."));
            Add(A, "hurt_react", P("아…! 뭐 하는 거예요, 지금?"), C("아…! 뭐 하는 거야, 지금?"));
            Add(A, "startle", P("깜짝이야. 조심 좀 해 주세요."), C("깜짝이야. 조심 좀 해."));
            Add(A, "talk_like", P("{topic} 얘기라면 좋아요."), C("{topic} 얘기면 좋지."));
            Add(A, "talk_mansion", P("이 저택은 볼수록 낯설어요."), C("이 저택은 볼수록 낯설어."));
            Add(A, "talk_wish", P("소원이요? 쉽게 말할 수 있는 건 아니에요."), C("소원? 쉽게 말할 수 있는 건 아니야."));
            Add(A, "talk_other_good", P("{t:은} 좋은 사람 같아요."), C("{t:은} 좋은 사람 같아."));
            Add(A, "talk_other_bad", P("{t:은} 좀 대하기 어려워요."), C("{t:은} 좀 대하기 어려워."));
            Add(A, "gossip_saw", P("{time}쯤 {t:이} {place}에 있었어요."), C("{time}쯤 {t:이} {place}에 있었어."));
            Add(A, "ask_player", P("{you:은} 요즘 어떻게 지내세요?"), C("넌 요즘 어떻게 지내?"));
            Add(A, "compliment_react", P("고마워요. 쑥스럽네요."), C("고마워. 쑥스럽다."));
            Add(A, "tease_react", P("놀리지 마세요."), C("놀리지 마."));
            Add(A, "gift_love", P("{item}! 정말 좋아요. 고마워요."), C("{item}? 진짜 좋다. 고마워."));
            Add(A, "gift_like", P("{item}, 고마워요. 잘 쓸게요."), C("{item}, 고마워. 잘 쓸게."));
            Add(A, "gift_meh", P("{item}… 고마워요. 마음은 받을게요."), C("{item}… 고마워. 마음은 받을게."));
            Add(A, "gift_hate", P("미안하지만 {item:은} 좀 곤란해요."), C("미안, {item:은} 좀 별로야."));
            Add(A, "favor_ask", P("혹시 {item:을} 구해 주실 수 있어요?"), C("혹시 {item} 좀 구해 줄 수 있어?"));
            Add(A, "favor_thanks", P("고마워요. 덕분에 살았어요."), C("고마워. 덕분에 살았어."));
            Add(A, "favor_accept", P("좋아요. 제가 할게요."), C("좋아. 내가 할게."));
            Add(A, "favor_refuse", P("미안해요. 그건 어렵겠어요."), C("미안. 그건 어렵겠어."));
            Add(A, "invite_ask", P("{act:이나} 같이 할래요? {place:으로} 가서요."), C("{act:이나} 같이 할래? {place:으로} 가서."));
            Add(A, "invite_yes", P("좋아요. 같이 가요."), C("좋아. 같이 가자."));
            Add(A, "invite_no", P("미안해요. 이번엔 어렵겠어요."), C("미안, 이번엔 안 되겠어."));
            Add(A, "accompany_start", P("같이 가요."), C("같이 가자."));
            Add(A, "accompany_end", P("여기까지만 같이 갈게요. 고마웠어요."), C("여기까지만 같이 갈게. 고마웠어."));
            Add(A, "casual_offer", P("우리 말 편하게 할까요?"), C("우리 이제 말 편하게 하자."));
            Add(A, "casual_yes", P("좋아요. …아니, 좋아."), C("좋아, 편하게 하자."));
            Add(A, "casual_no", P("아직은 존댓말이 편해요."), C("아직은 좀 어색해. 나중에."));
            Add(A, "secret_hint", P("누구에게나 말 못 할 사정은 있잖아요."), C("누구나 말 못 할 사정은 있잖아."));
            Add(A, "secret_share", P("이건 {you}한테만 하는 얘기예요. 저도 숨긴 게 있어요."), C("너한테만 하는 얘기야. 나도 숨긴 게 있어."));
            Add(A, "love_hint", P("{you:이랑} 있으면 마음이 편해요."), C("너랑 있으면 마음이 편해."));
            Add(A, "love_confess", P("{you}, 좋아해요."), C("{you:아}, 좋아해."));
            Add(A, "love_yes", P("…저도요."), C("…나도."));
            Add(A, "love_no", P("마음은 고마워요. 그래도… 미안해요."), C("마음은 고마워. 그래도… 미안해."));
            Add(A, "love_rejected", P("그렇군요. 알겠어요."), C("그렇구나. 알았어."));
            Add(A, "argue_open", P("{t}, 그 말은 좀 지나쳤어요."), C("{t}, 그 말은 좀 심했어."));
            Add(A, "argue_reply", P("{t}, 제 말도 좀 들어 주세요."), C("{t}, 내 말도 좀 들어."));
            Add(A, "argue_makeup", P("{t}, 아까는 미안했어요."), C("{t}, 아까는 미안했어."));
            Add(A, "argue_stormoff", P("{t}, 더 얘기하고 싶지 않아요."), C("{t}, 더 얘기하기 싫어."));
            Add(A, "apology", P("미안해요. 제 잘못이에요."), C("미안해. 내 잘못이야."));
            Add(A, "forgive", P("괜찮아요. 사과 받을게요."), C("괜찮아. 사과 받을게."));
            Add(A, "not_forgive", P("아직은 받아들이기 어려워요."), C("아직은 못 받아들이겠어."));
            Add(A, "warn", P("{t:은} 조심하세요."), C("{t} 조심해."));
            Add(A, "fear_general", P("솔직히 무서워요."), C("솔직히 무서워."));
            Add(A, "fear_of", P("{t:이} 조금 무서워요."), C("{t:이} 좀 무서워."));
            Add(A, "react_rule", P("규칙이 또 바뀌었네요. 조심해야겠어요."), C("규칙이 또 바뀌었네. 조심해야겠다."));
            Add(A, "meal", P("밥 먹어요. 먹어야 버티죠."), C("밥 먹자. 먹어야 버티지."));
            Add(A, "doing_act", P("{act} 중이에요."), C("{act} 중이야."));
            Add(A, "sleepy", P("졸리네요."), C("졸려."));
            Add(A, "night_walk", P("잠이 안 와서 걷는 중이에요."), C("잠이 안 와서 걷는 중이야."));
            Add(A, "grief", P("{victim}… 믿기지 않아요."), C("{victim}… 믿기지 않아."));
            Add(A, "grief_close", P("{victim}… 이럴 수는 없어요."), C("{victim}… 이럴 순 없어."));
            Add(A, "empty_seat", P("{victim}의 자리가 비어 있네요."), C("{victim} 자리가 비어 있네."));
            Add(A, "after_trial_relief", P("끝났어요. 일단 쉬어요."), C("끝났다. 일단 쉬자."));
            Add(A, "after_trial_guilt", P("우리가 한 선택이 계속 생각나요."), C("우리가 한 선택이 계속 생각나."));
            Add(A, "suspicious_of_player", P("{you}, 뭔가 숨기는 거 있어요?"), C("너 뭔가 숨기는 거 있지?"));
            Add(A, "trust_player", P("{you:은} 믿을 수 있어요."), C("넌 믿을 수 있어."));

            // ───────── 수사 ─────────
            Add(A, "scream_discover", P("누가 좀 와 주세요!"), C("누가 좀 와 봐!"));
            Add(A, "discover_shock", P("{victim}…? 말도 안 돼요."), C("{victim}…? 말도 안 돼."));
            Add(A, "investigate_comment", P("여기 뭔가 이상해요."), C("여기 뭔가 이상해."));
            Add(A, "alibi_where", P("{time}에는 {place}에 있었어요."), C("{time}엔 {place}에 있었어."));
            Add(A, "alibi_with", P("{time}에 {place}에서 {t:와} 같이 있었어요."), C("{time}에 {place}에서 {t:이랑} 같이 있었어."));
            Add(A, "saw_person", P("{time}쯤 {place}에서 {t:을} 봤어요."), C("{time}쯤 {place}에서 {t:을} 봤어."));
            Add(A, "saw_person_unsure", P("{time}쯤 {place}에 누군가 있었어요. 누군지는 모르겠어요."), C("{time}쯤 {place}에 누가 있었어. 누군지는 모르겠어."));
            Add(A, "saw_item", P("{t:이} {place}에서 {item:을} 들고 있었어요."), C("{t:이} {place}에서 {item:을} 들고 있었어."));
            Add(A, "heard_sound", P("{time}쯤 {place} 쪽에서 {sound} 소리를 들었어요."), C("{time}쯤 {place} 쪽에서 {sound} 소리를 들었어."));
            Add(A, "saw_nothing", P("아무것도 못 봤어요."), C("아무것도 못 봤어."));
            Add(A, "refuse_answer", P("그건 말하고 싶지 않아요."), C("그건 말하기 싫어."));
            Add(A, "suspect", P("{t:이} 의심스러워요. {reason}요."), C("{t:이} 의심스러워. {reason}."));
            Add(A, "share_find", P("{item}, 이것 좀 보세요. {place}에서 확인한 거예요."), C("{item}, 이거 봐. {place}에서 확인한 거야."));
            Add(A, "keep_find", P("찾은 건 있지만 아직은 말 못 해요."), C("찾은 건 있는데 아직 말 못 해."));
            Add(A, "ask_find", P("뭐 찾은 거 있어요?"), C("뭐 찾은 거 있어?"));
            Add(A, "body_exam_comment", P("{victim}… 조금만 살펴볼게요."), C("{victim}… 조금만 볼게."));
            Add(A, "time_pressure", P("시간이 얼마 없어요."), C("시간 얼마 없어."));

            // ───────── 재판 ─────────
            Add(A, "trial_first", P("시작하죠. 차분하게 하나씩 봐요."), C("시작하자. 차분하게 하나씩 보자."));
            Add(A, "claim_alibi", P("{time}에 저는 {place}에 있었어요."), C("{time}에 난 {place}에 있었어."));
            Add(A, "claim_saw", P("{time}쯤 {place}에서 {t:을} 직접 봤어요."), C("{time}쯤 {place}에서 {t:을} 직접 봤어."));
            Add(A, "claim_heard", P("{time}쯤 {place} 쪽에서 {sound} 소리를 들었어요."), C("{time}쯤 {place} 쪽에서 {sound} 소리를 들었어."));
            Add(A, "claim_theory", P("{place}에서 {item:으로} 당한 거라면 앞뒤가 맞아요."), C("{place}에서 {item:으로} 당한 거라면 앞뒤가 맞아."));
            Add(A, "accuse", P("제 생각엔… 범인은 {t:이에요}."), C("{t}, 네가 범인이라고 생각해."));
            Add(A, "accuse_player", P("{you}, 미안하지만… 지금은 그쪽이 제일 의심스러워요."), C("{you:아}, 미안한데 네가 의심스러워."));
            Add(A, "defend_self", P("저는 아니에요. 근거를 보여 주세요."), C("난 아니야. 근거 보여 줘."));
            Add(A, "defend_other", P("{t:을} 몰아가기엔 근거가 부족해요."), C("{t:을} 몰아가기엔 근거가 부족해."));
            Add(A, "agree", P("{t} 말에 동의해요."), C("{t} 말에 동의해."));
            Add(A, "object", P("잠깐만요. {t}, 그건 이상해요."), C("잠깐. {t}, 그건 이상해."));
            Add(A, "counter", P("{t}, 그럼 이건 어떻게 설명하죠?"), C("{t}, 그럼 이건 어떻게 설명할 건데?"));
            Add(A, "concede", P("…제가 틀렸네요."), C("…내가 틀렸네."));
            Add(A, "retract", P("아까 한 말은 정정할게요."), C("아까 한 말 정정할게."));
            Add(A, "panic", P("잠, 잠깐만요! 그게 아니에요!"), C("잠, 잠깐만! 그게 아니야!"));
            Add(A, "stay_silent", P("…할 말이 없어요."), C("…할 말 없어."));
            Add(A, "ask_source", P("{t}, 그건 어디서 들은 거예요?"), C("{t}, 그거 어디서 들었어?"));
            Add(A, "source_direct", P("제가 직접 봤어요."), C("내가 직접 봤어."));
            Add(A, "source_hearsay", P("{t}한테 들은 거예요. 직접 보진 못했어요."), C("{t}한테 들었어. 직접 보진 못했어."));
            Add(A, "pressure_player", P("{you}, 뭔가 알고 있다면 말해 주세요."), C("{you:아}, 뭔가 알면 말해 줘."));
            Add(A, "final_defense", P("제발 다시 생각해 주세요. 저는 아니에요."), C("제발 다시 생각해 줘. 난 아니야."));
            Add(A, "confess", P("…제가 했어요.|소원을 포기할 수가 없었어요."), C("…내가 했어.|소원을 포기할 수 없었어."));
            Add(A, "vote_line", P("저는 {t}에게 투표할게요."), C("난 {t}한테 투표할게."));
            Add(A, "verdict_correct_react", P("맞았어요. …그래도 기쁘지는 않네요."), C("맞았어. …그래도 기쁘진 않아."));
            Add(A, "verdict_wrong_react", P("틀렸어요… 우리가 틀렸어요."), C("틀렸어… 우리가 틀렸어."));
            Add(A, "execution_last", P("…여기까지인가 봐요."), C("…여기까지인가 보네."));
            Add(A, "escape_line", P("이제 저는 갈게요. 미안해요."), C("이제 난 갈게. 미안해."));
            Add(A, "victim_named", P("{victim}… 그 이름을 잊지 말아요."), C("{victim}… 그 이름 잊지 말자."));
        }
    }
}
