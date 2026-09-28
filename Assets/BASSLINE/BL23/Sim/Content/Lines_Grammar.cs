namespace BL23.Sim
{
    public static partial class LineBank
    {
        // 일상 문법(IG01–12)·트릭 문법용 공용 대사. 인물 전용 대사가 있으면 그쪽이 우선한다.
        static void Init_ZGrammar()
        {
            const string A = "ANY";
            // 대여·반납 (IG01/IG06)
            Add(A, "borrow_ask", P("혹시 {item} 잠깐 빌릴 수 있을까요?"), C("혹시 {item} 잠깐 빌려줄 수 있어?"));
            Add(A, "borrow_yes", P("그래요. 가져다 드릴게요."), C("그래. 가져다줄게."));
            Add(A, "borrow_no", P("미안해요, 그건 좀 곤란해요."), C("미안, 그건 좀 곤란해."));
            Add(A, "lend_give", P("여기요, {item}. 다 쓰면 돌려주세요."), C("자, {item}. 다 쓰면 돌려줘."));
            Add(A, "return_item", P("빌린 {item}, 돌려드릴게요. 고마웠어요."), C("빌린 {item}, 돌려줄게. 고마웠어."));
            Add(A, "return_thanks", P("잘 돌려받았어요."), C("응, 잘 받았어."));
            Add(A, "return_where", P("그거라면 {place}에 두고 왔는데요… 못 보셨어요?"), C("그거 {place}에 두고 왔는데… 못 봤어?"));
            Add(A, "item_missing", P("제 {item:이} 없어졌어요. 어디 갔는지 모르겠어요."), C("내 {item:이} 없어졌어. 어디 갔는지 모르겠어."));
            Add(A, "item_missing_suspect", P("제 {item:이} 없어졌어요. {t:이} 가져간 것 같아요."), C("내 {item:이} 없어졌어. {t:이} 가져간 것 같아."));
            Add(A, "coat_offer", P("추워 보여요. 제 외투라도 입으세요."), C("추워 보여. 내 외투라도 입어."));
            Add(A, "coat_thanks", P("고마워요. 나중에 꼭 돌려드릴게요."), C("고마워. 나중에 꼭 돌려줄게."));
            // 초대·모임 (IG02, 모임 안의 사건)
            Add(A, "gathering_invite", P("{time}부터 {place}에서 {act:을} 해요. 오실래요?"), C("{time}부터 {place}에서 {act:을} 해. 올래?"));
            Add(A, "gathering_change", P("아, 모임 일정이 바뀌었어요. {time}에 {place}에서 해요."), C("아, 모임 일정 바뀌었어. {time}에 {place}에서 해."));
            Add(A, "gathering_yes", P("좋아요, 갈게요."), C("좋아, 갈게."));
            Add(A, "gathering_no", P("미안해요, 이번엔 빠질게요."), C("미안, 이번엔 빠질게."));
            Add(A, "gathering_chat", P("이렇게 모이니까 조금은 안심이 되네요."), C("이렇게 모이니까 좀 안심된다."));
            Add(A, "gathering_leave", P("잠깐만 다녀올게요. 금방 올게요."), C("잠깐만 다녀올게. 금방 와."));
            Add(A, "gathering_back", P("다녀왔어요. 무슨 얘기 하고 있었어요?"), C("다녀왔어. 무슨 얘기 하고 있었어?"));
            Add(A, "gathering_stoodup", P("…아무도 안 오네요. {t:이} 여기라고 했는데."), C("…아무도 안 오네. {t:이} 여기라고 했는데."));
            Add(A, "gathering_explain", P("그날 모임 말인데요, 장소가 바뀌었던 거 몰랐어요?"), C("그날 모임 말이야, 장소 바뀐 거 몰랐어?"));
            Add(A, "gathering_explain_reply", P("몰랐어요… 그래서 아무도 없었던 거군요."), C("몰랐어… 그래서 아무도 없었구나."));
            // 수리 (IG07)
            Add(A, "repair_claim", P("그건 다 고쳐 놨어요. 이제 괜찮을 거예요."), C("그거 다 고쳐 놨어. 이제 괜찮을 거야."));
            // 녹음 (IG03) — 방 안에서 혼잣말·연습
            Add(A, "rehearse", P("…음, 음. 아, 아. 오늘은 방에서 좀 쉬어야겠다."), C("…음, 음. 아, 아. 오늘은 방에서 좀 쉬어야겠다."));
            // 함정 발견
            Add(A, "trap_found", P("다들 조심하세요! {place}에 가구가 넘어지게 누가 장치를 해 놨어요!"), C("다들 조심해! {place}에 가구가 넘어지게 누가 장치해 놨어!"));
            // 대리 전달 (IG10)
            Add(A, "courier_ask", P("부탁이 있어요. 이 쪽지, {t}한테 좀 전해 줄 수 있어요? 제가 줬다는 말은 하지 말고요."), C("부탁 하나만. 이 쪽지 {t}한테 좀 전해 줄래? 내가 줬단 말은 하지 말고."));
            Add(A, "courier_deliver", P("누가 이걸 전해 달래서요."), C("누가 이거 전해 달래."));
            Add(A, "courier_confess", P("…사실 {time}쯤 {t:이} {victim} 씨한테 쪽지를 전해 달라고 했어요. 무슨 내용인지는 몰랐어요."), C("…사실 {time}쯤 {t:이} {victim}한테 쪽지 전해 달라고 했어. 내용은 몰랐어."));
            Add(A, "saw_handover", P("{time}쯤 {t:이} {u}에게 쪽지 같은 걸 건네는 걸 봤어요."), C("{time}쯤 {t:이} {u}한테 쪽지 같은 걸 건네는 거 봤어."));
            Add(A, "heard_voice", P("{time}쯤 {place} 쪽에서 {t}의 목소리를 들었어요. 모습을 본 건 아니에요."), C("{time}쯤 {place} 쪽에서 {t} 목소리를 들었어. 모습을 본 건 아니야."));
            // 유스티 공지
            const string B = "NPC00";
            Add(B, "y_repair", P("알려 드립니다. {place}에서 {act:이} 말썽입니다. {t} 님께서 손봐 주시기로 하셨습니다."));
            Add(B, "y_rule_CH10_now", P("공개 질의 시간입니다. 한 분씩, 사건 무렵 어디에 계셨는지 말씀해 주십시오. 모두가 듣고 있습니다."));
            Add(B, "y_rule_CH18_apply", P("‘반론 우선권’에 따라 {t} 님의 반론부터 먼저 듣겠습니다."));
        }
    }
}
