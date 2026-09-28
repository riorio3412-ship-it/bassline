namespace BL23.Sim
{
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // Pair scenes (F4, DailyLifeDesign §3.6) — the core set: the design's flashpoints and the web's strongest ties
    // (CharacterBible §3.2 #). Ids start with "PS" (rumours made from a scene remember which one: 민혁 can correct them if he saw it).
    // Pair(id, A, B, title).Kind_(conflict|rival|tease|flirt|ally|comic|warm).At(RoomTypes).Time(morning|day|evening|night|meal)
    //   .Only(condition).Open(lines).Choice(options).Npc_(NPC-only outcome).Talk(rumour kind).TieNpc(state).Every(days)
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    public static partial class LifeData
    {
        static void Init_Life_Pairs_Core()
        {
            // #27 라온 × 시온 — the library shout
            Pair("PS_P08P07_library", "P08", "P07", "도서실의 볼륨").Kind_("rival").At("Library;Study").Time("morning|day|evening")
                .Open(
                    L("P07", "야, 베이스! 여기 있었냐!", Emotion.Grin),
                    L("P08", "도서실이야. 네 볼륨, 여기서만 반으로 줄여 줄래?"),
                    L("P07", "반? 크하하, 반으로 줄이면 그게 랩이냐, 속삭임이지."),
                    If("present:P18", L("P08", "속삭여. 저기 민서 퍼즐 풀다 연필 멈췄잖아.")),
                    If("!present:P18", L("P08", "속삭여. 책장 넘기는 박자가 다 틀어졌어.")),
                    L("P07", "…쉿. 됐냐? 근데 라임 하나 떠올랐는데, 이거 들어 봐.", Emotion.Smirk))
                .Choice(
                    O("둘 다 앉아요. 속삭이는 랩, 저도 들어 볼래요.",
                        L("P07", "오, 브로 좀 아는데? 쉿… 저택, 적막, 저녁.", Emotion.Grin),
                        L("P08", "…크. 박자는 맞네. 반음만 내려."))
                        .Do(ToMe("P07", like: 0.04f), ToMe("P08", like: 0.03f), Rel("P08", "P07", like: 0.03f)).Tie("P08:P07:jam").Mem("whisper_rap"),
                    O("시온 씨, 여긴 도서실이에요.",
                        L("P07", "…알았어, 알았어. 내가 나간다. 치사하게.", Emotion.Sad),
                        L("P08", "고마워. 한 명은 말이 통하네."))
                        .Do(ToMe("P08", like: 0.04f, respect: 0.02f), ToMe("P07", like: -0.03f), Rel("P07", "P08", grudge: 0.02f)),
                    O("라온 씨, 좀 봐줘요. 신나서 그런 거잖아요.",
                        L("P08", "…됐고. 이어폰 낄게.", Emotion.Blank),
                        L("P07", "봐, 브로는 알잖아! 크하하.", Emotion.Laugh))
                        .Do(ToMe("P07", like: 0.04f), ToMe("P08", like: -0.03f), Rel("P08", "P07", grudge: 0.02f)))
                .Npc_(Rel("P08", "P07", like: -0.02f, grudge: 0.02f), Rel("P07", "P08", like: -0.01f)).Talk("argued");

            // #6 준서 → 채령 — protection vs boundary (doc D01)
            Pair("PS_P10P16_alone", "P10", "P16", "혼자 가게 둬").Kind_("conflict").Time("morning|day|evening")
                .Open(
                    L("P10", "전시실 가요? 같이 가요. 혼자 보내기 좀 그래서요."),
                    L("P16", "싫다고 했잖아. 호의면 그쯤 알아들어.", Emotion.Blank),
                    L("P10", "…도시락만이라도요. 오늘 점심 안 드셨잖아요."),
                    L("P16", "봐. 또 대신 정해. 내가 먹었는지 안 먹었는지도.", Emotion.Angry))
                .Choice(
                    O("채령 씨가 싫다잖아요. 준서 씨, 오늘은 보내 줘요.",
                        L("P10", "…네. 알겠어요.", Emotion.Sad),
                        L("P16", "…풋. 도시락은 두고 가. 먹는다는 말은 아니야."))
                        .Do(ToMe("P16", like: 0.03f, trust: 0.05f), ToMe("P10", respect: 0.02f), Rel("P16", "P10", grudge: -0.03f)).Tie("P10:P16:respected").Mem("respect_refusal"),
                    O("말없이 지켜본다",
                        L("P10", "…그래도 멀리서 걸을게요. 신경 쓰지 마요."),
                        L("P16", "그게 신경 쓰이는 거라고.", Emotion.Angry))
                        .Act().Do(Rel("P16", "P10", grudge: 0.05f), ToMe("P16", like: -0.01f)).Mem("stayed_silent"),
                    O("제가 같이 갈게요. 준서 씨 대신.",
                        L("P16", "…너까지? 됐어. 셋이 가면 행진이야.", Emotion.Angry),
                        L("P10", "허허, 민혁 씨가 가면 저는 안심이에요."))
                        .Do(ToMe("P10", like: 0.04f), ToMe("P16", like: -0.03f, grudge: 0.02f)))
                .Npc_(Rel("P16", "P10", grudge: 0.04f, like: -0.02f)).Talk("argued");

            // #17 세나 → 민서 — the missing storage key (day 1, doc example)
            Pair("PS_P13P18_key", "P13", "P18", "창고 열쇠").Kind_("conflict").Only("day<=1").Every(30)
                .Open(
                    L("P13", "야, 창고 열쇠 마지막으로 가진 거 너지? 어디 뒀어.", Emotion.Angry),
                    L("P18", "제자리에 뒀습니다."),
                    L("P13", "제자리 어디. 지금 없잖아. 솔직히 너 말고 누가 창고를 들락거려?"),
                    L("P18", "못 봤으면, 없었다는 뜻은 아닙니다.", Emotion.Blank))
                .Choice(
                    O("태겸 씨 대여표부터 봐요. 누가 빌렸는지 적혀 있을 거예요.",
                        L("P13", "…대여표? 하, 그걸 누가 일일이…"),
                        L("P18", "시온 씨 이름일 겁니다. 오후 두 시에 빌려 갔습니다."),
                        L("P13", "…씨. 됐어. 틀렸으면 틀렸다고 할게. 나중에.", Emotion.Sad))
                        .Do(ToMe("P18", like: 0.03f, trust: 0.06f), ToMe("P13", respect: 0.03f)).Know("note:첫날 창고 열쇠는 시온이 빌려 갔다(태겸의 대여표)")
                        .Mem("slip_first").Tie("P13:P18:checked").Later("어제 열쇠 건. 대여표 보자고 한 거, 네 말이 맞았어. …민서한테는 아직 말 못 했어."),
                    O("세나 씨, 증거도 없잖아요.",
                        L("P13", "…넌 뭔데 끼어. 계약도 없는 놈이.", Emotion.Angry),
                        L("P18", "…고맙습니다."))
                        .Do(ToMe("P13", like: -0.03f, grudge: 0.02f), ToMe("P18", like: 0.03f, trust: 0.03f)).Mem("defended_minseo"),
                    O("말없이 자리를 피한다",
                        L("P18", "…끝났습니다. 저는 할 일이 있어서요.", Emotion.Blank))
                        .Act().Do(Rel("P18", "P13", grudge: 0.05f), ToMe("P18", trust: -0.01f)).Mem("stayed_out"))
                .Npc_(Rel("P18", "P13", grudge: 0.06f, trust: -0.05f), Rel("P13", "P18", like: -0.03f)).Talk("argued");

            // #14 진우 × 서윤 — the roster ("착한 척 표")
            Pair("PS_P02P03_roster", "P02", "P03", "착한 척 표").Kind_("conflict").At("GrandHall;Dining;Library;Archive").Time("morning|day")
                .Open(
                    L("P02", "이 표, 칸이 너무 반듯해. 착한 척도 줄 맞춰서 하네.", Emotion.Smirk),
                    L("P03", "진우 씨, 그 표로 누가 밥을 굶는지가 정해져요."),
                    L("P02", "흐응, 화났어? 목소리가 작아졌는데."),
                    L("P03", "작아진 게 아니라, 정리된 거예요.", Emotion.Blank))
                .Choice(
                    O("서윤 씨 표 덕분에 다들 밥 먹는 거예요.",
                        L("P03", "…고마워요. 정리하면, 민혁 씨 칸은 오늘 설거지예요.", Emotion.Smile),
                        L("P02", "큭. 편드는 값이 설거지네."))
                        .Do(ToMe("P03", like: 0.04f, trust: 0.03f), ToMe("P02", like: -0.02f)),
                    O("진우 말도 일리는 있어요. 칸이 좀 빡빡하긴 해요.",
                        L("P02", "봐. 민혁은 보는 눈이 있어.", Emotion.Grin),
                        L("P03", "…빡빡한 건 제 칸이에요. 다른 분 칸이 아니라.", Emotion.Blank))
                        .Do(ToMe("P02", like: 0.04f), ToMe("P03", like: -0.03f)),
                    O("둘 다 제 칸에 적어 주세요. 싸울 시간에 같이 하게요.",
                        L("P03", "…후훗. 좋아요. 진우 씨도 같이요.", Emotion.Smile),
                        L("P02", "…나까지? 흐응. 재밌네, 너.", Emotion.Smirk))
                        .Do(ToMe("P03", like: 0.03f, respect: 0.03f), ToMe("P02", like: 0.02f, respect: 0.03f), Rel("P02", "P03", grudge: -0.02f), Rel("P03", "P02", grudge: -0.02f))
                        .Tie("P02:P03:truce").Mem("both_on_roster").Later("서윤 표에 내 이름 있더라. 네 칸 바로 옆에. …설거지는 네가 해."))
                .Npc_(Rel("P03", "P02", grudge: 0.04f), Rel("P02", "P03", like: -0.02f)).Talk("argued");

            // #25 해린 × 태겸 — the same tool (doc D12), self-resolving
            Pair("PS_P11P06_tool", "P11", "P06", "십오 분").Kind_("comic").At("Workshop;Storage").Time("morning|day|evening")
                .Open(
                    L("P11", "지금 쓰잖아. 손부터 빼."),
                    L("P06", "예약 시간은 지났습니다. 끝나는 시각을 말씀해 주시죠."),
                    L("P11", "…십오 분."),
                    L("P06", "십오 분. 적어 두겠습니다. 늦으면 쓴 초콜릿 한 조각."),
                    L("P11", "초콜릿은 네가 좋아하는 거잖아. 그게 무슨 벌이야.", Emotion.Smirk),
                    L("P06", "벌이 아니라 조건입니다. 흠."))
                .Choice(
                    O("제가 시계 봐 줄게요. 딱 십오 분.",
                        L("P11", "헤헤, 심판이 생겼네.", Emotion.Laugh),
                        L("P06", "공정하군요. 좋습니다."))
                        .Do(ToMe("P11", like: 0.03f), ToMe("P06", like: 0.02f, respect: 0.03f)),
                    O("초콜릿은 제가 낼게요. 둘 다 여유 있게 쓰세요.",
                        L("P06", "…그건 조건 위반입니다. 제삼자 부담은 장부에 칸이 없습니다.", Emotion.Blank),
                        L("P11", "크, 칸 하나 만들어. 민혁 칸.", Emotion.Laugh))
                        .Do(ToMe("P11", like: 0.04f), ToMe("P06", like: 0.01f)))
                .Npc_(Rel("P11", "P06", grudge: 0.01f));

            // #10 태겸 × 민서 — chore count, and the lie ("처음 뵙습니다")
            Pair("PS_P06P18_dishes", "P06", "P18", "사흘 연속 설거지").Kind_("rival").At("Dining;Kitchen").Time("morning|day|evening")
                .Open(
                    L("P06", "민서 씨, 사흘 연속 설거지입니다. 계산이 안 맞습니다."),
                    L("P18", "제가 하는 게 빠릅니다."),
                    L("P06", "빠른 거랑 공평한 건 다릅니다. …전에도 그렇게 말씀하셨죠."),
                    L("P18", "…처음 뵙습니다.", Emotion.Blank),
                    L("P18", "벌써 세 시네요. 끝내겠습니다."))
                .Choice(
                    O("두 분, 전에 만난 적 있죠?",
                        L("P18", "…없습니다.", Emotion.Blank),
                        L("P06", "…흠. 제가 착각했나 봅니다. 장부에 없는 얼굴이라."))
                        .Do(ToMe("P18", trust: 0.04f)).Know("note:태겸과 민서는 서로 아는 사이 같다. 둘 다 모른 척한다").Mem("noticed_debt"),
                    O("설거지 당번, 제가 한 칸 맡을게요.",
                        L("P18", "…괜찮습니다. 끝났습니다."),
                        L("P06", "민혁 씨 칸, 적어 두겠습니다. 이제야 공평해지는군요."))
                        .Do(ToMe("P06", like: 0.03f, respect: 0.02f), ToMe("P18", like: 0.02f)),
                    O("민서 씨가 좋다는데 그냥 둬요.",
                        L("P06", "좋다는 말은 안 했습니다. 빠르다고 했죠."),
                        L("P18", "…허."))
                        .Do(ToMe("P06", respect: -0.02f), Rel("P06", "P18", respect: 0.02f)))
                .Npc_(Rel("P18", "P06", grudge: 0.03f));

            // #5 가온 × 이현 — correction at the table
            Pair("PS_P15P05_correction", "P15", "P05", "원문 그대로").Kind_("conflict").At("Dining;Lounge").Time("morning|evening|meal")
                .Open(
                    L("P15", "백이현 씨, 어제 '모두를 위한 확인'이라고 하셨죠. 누가 확인하는지는 안 정하셨어요."),
                    L("P05", "자, 자. 식사 자리에서까지 기사 쓰실 건 아니죠?", Emotion.Smile),
                    L("P15", "기사는 제목이 없을 때 제일 정확해요. 지금은 제목 없어요."),
                    L("P05", "요컨대, 제가 틀렸다는 말씀이시죠? 하하하. 좋습니다, 정정하죠."),
                    L("P15", "원문 그대로요. 적어 둘게요.", Emotion.Blank))
                .Choice(
                    O("가온 씨 말이 맞아요. 누가 확인하는지부터 정해요.",
                        L("P05", "…좋습니다. 민혁 씨가 그렇게 말씀하시니. 잠깐, 단추가…", Emotion.Blank),
                        L("P15", "고마워요. 이건 원문으로 남길게요."))
                        .Do(ToMe("P15", like: 0.03f, trust: 0.05f), ToMe("P05", like: -0.03f, grudge: 0.02f, mem: "식탁에서 기자 편을 들었다")).Mem("sided_gaon"),
                    O("이현 씨 말대로 밥은 좀 먹고 해요.",
                        L("P15", "…먹으면서 적을게요.", Emotion.Blank),
                        L("P05", "하하하. 역시 식탁엔 평화가 제일입니다.", Emotion.Smile))
                        .Do(ToMe("P05", like: 0.04f), ToMe("P15", like: -0.02f, respect: -0.01f)).Mem("sided_ihyun"),
                    O("두 분 말씀, 저도 원문 그대로 기억해 둘게요.",
                        L("P15", "…흐. 기자가 둘이네요.", Emotion.Smile),
                        L("P05", "…기록하는 분이 늘어서 기쁩니다. 하하.", Emotion.Smirk))
                        .Do(ToMe("P15", like: 0.03f, respect: 0.02f), ToMe("P05", like: -0.01f)).Mem("recorded_both"))
                .Npc_(Rel("P05", "P15", grudge: 0.04f), Rel("P15", "P05", trust: -0.02f)).Talk("argued");

            // #11 채령 → 수아 — blame
            Pair("PS_P16P12_date", "P16", "P12", "날짜만 적어 둔 거야").Kind_("conflict").Time("morning|day|evening")
                .Open(
                    L("P16", "그 그룹 활동 중지만 아니었어도. …뭐, 어쨌든.", Emotion.Blank),
                    L("P12", "채령 씨… 미안해요. 제가 뭘 할 수 있었을지는 모르지만.", Emotion.Sad),
                    L("P16", "사과 받으려던 거 아니야. 날짜만 적어 둔 거지."),
                    L("P12", "…그날 저도 울었어요. 이유는 달랐겠지만요."))
                .Choice(
                    O("채령 씨 가게, 무슨 일이 있었어요?",
                        L("P16", "…원단을 세 배로 받았어. 월세도 감당 못 할 데로 들어갔고.", Emotion.Blank),
                        L("P16", "그리고 쟤네 활동 중지. 핑계가 세 개면 하나쯤은 진짜겠지."))
                        .Do(ToMe("P16", trust: 0.04f)).Know("note:가게가 망한 이유를 셋으로 댄다 — 원단, 월세, 수아네 활동 중지").Mem("asked_shop"),
                    O("수아 씨 잘못은 아니잖아요.",
                        L("P12", "…고마워요. 근데 그렇게 말하면 채령 씨가 더 아파요.", Emotion.Sad),
                        L("P16", "풋. 정답. 넌 가끔 수아보다 눈치가 없어."))
                        .Do(ToMe("P12", like: 0.03f), ToMe("P16", like: -0.03f)),
                    O("말없이 둘 사이에 앉는다",
                        L("P12", "…에헤헤. 가운데 앉으셨네요.", Emotion.Smile),
                        L("P16", "…뭐, 어쨌든. 자리는 넓네."))
                        .Act().Do(ToMe("P12", like: 0.02f), ToMe("P16", like: 0.02f), Rel("P16", "P12", grudge: -0.02f)))
                .Npc_(Rel("P16", "P12", grudge: 0.03f), Rel("P12", "P16", fear: 0.02f)).Talk("argued");

            // #12 시온 → 수아 — the saved window seat
            Pair("PS_P07P12_window", "P07", "P12", "창가 자리").Kind_("warm").At("Dining").Time("morning|meal|evening")
                .Open(
                    L("P07", "스타! 네 자리 맡아 놨어, 창가. 햇빛 제일 좋은 데.", Emotion.Grin),
                    L("P12", "와, 고마워요! 근데 서윤 씨랑 먼저 약속해서… 다음엔 꼭요!", Emotion.Smile),
                    L("P07", "…어, 그래. 다음엔. 크하하, 다음엔 꼭이다!", Emotion.Laugh))
                .Choice(
                    O("시온 씨, 그 창가 자리 제가 앉아도 돼요?",
                        L("P07", "…야, 브로. 너도 햇빛 좋아하냐? 앉아. 크하하.", Emotion.Smile))
                        .Do(ToMe("P07", like: 0.04f, attach: 0.02f)).Mem("took_window_seat"),
                    O("말없이 창밖을 본다",
                        L("P07", "…오늘 햇빛, 별로네.", Emotion.Sad))
                        .Act())
                .Npc_(Rel("P07", "P03", jealous: 0.03f), Rel("P07", "P12", romance: 0.02f));

            // #16 도윤 → 세나 — fascination (a red flag the player can notice)
            Pair("PS_P04P13_line", "P04", "P13", "선이 고운 손").Kind_("rival").At("Workshop;GameRoom;Gallery;Lounge").Time("morning|day|evening")
                .Open(
                    L("P04", "그 손, 선이 곱군요. 이음새가 거의 보이지 않습니다."),
                    L("P13", "…불쌍하다는 말보단 낫네. 근데 왜 손만 봐?"),
                    L("P04", "좋은 물건은 오래 보게 됩니다. 실례였다면 사과드리지요."),
                    L("P13", "물건? …하. 솔직히 방금 그 단어는 좀 별로였어.", Emotion.Blank))
                .Choice(
                    O("도윤 씨, 세나 씨 손은 물건이 아니에요.",
                        L("P04", "…그렇군요. 표현을 고르겠습니다.", Emotion.Blank),
                        L("P13", "…고마워. 방금 좀 소름 돋았거든."))
                        .Do(ToMe("P13", like: 0.03f, trust: 0.05f), ToMe("P04", like: -0.03f), Rel("P13", "P04", like: -0.05f)).Tie("P04:P13:warned").Mem("called_out_doyun"),
                    O("말없이 도윤의 손을 본다",
                        L("P04", "…차가 식었군요. 잔을 제자리에 두겠습니다."),
                        L("P13", "뭐야, 둘 다 왜 조용해."))
                        .Act().Know("note:도윤은 세나의 의수를 오래 본다 — '좋은 물건'이라고 했다").Mem("noticed_doyun"),
                    O("의수, 누가 조정해 줘요? 진짜 잘 맞네요.",
                        L("P13", "…해린이 봐 줘. 만든 건 병원이고."),
                        L("P04", "해린 씨 손이군요. 역시."))
                        .Do(ToMe("P13", like: 0.02f)).Know("note:세나의 의수는 해린이 조정해 준다"))
                .Npc_(Rel("P13", "P04", like: 0.02f), Rel("P04", "P13", like: 0.02f));

            // #28 재하 × 예담 — filming
            Pair("PS_P09P17_filming", "P09", "P17", "무대 밖의 얼굴").Kind_("conflict").Time("morning|day|evening")
                .Open(
                    L("P09", "예담아, 그거 찍고 있지? 끄자. 지금 이 얼굴은 무대 밖이야.", Emotion.Blank),
                    L("P17", "……응."),
                    L("P17", "지울게. 지금."))
                .Choice(
                    O("지우는 거, 재하 씨 앞에서 해요.",
                        L("P17", "응. 봐. …삭제. 끝."),
                        L("P09", "…고마워, 예담아. 브라보는 무대에서만 하는 건데, 지금은 하고 싶네.", Emotion.Smile))
                        .Do(ToMe("P09", like: 0.03f, trust: 0.03f), ToMe("P17", trust: 0.03f), Rel("P09", "P17", like: 0.06f, trust: 0.05f)).Tie("P09:P17:deleted").Mem("watched_delete"),
                    O("예담 씨, 뭘 찍고 있었어요?",
                        L("P17", "…재하가 혼자 대사 연습하는 거. 옛날 공연이랑 똑같아서.", Emotion.Sad),
                        L("P09", "옛날 공연? 너 그걸 어떻게…"),
                        L("P17", "아, 아무것도 아니야! 지울게."))
                        .Do(ToMe("P17", like: 0.02f)).Know("note:예담은 재하의 데뷔 공연을 기억한다 — 몰래 팬이었던 것 같다"),
                    O("찍어 둬도 되지 않아요? 좋은 장면인데.",
                        L("P09", "민혁아. …그건 아니야.", Emotion.Angry))
                        .Do(ToMe("P09", like: -0.04f, trust: -0.03f), ToMe("P17", like: 0.02f)))
                .Npc_(Rel("P09", "P17", like: 0.03f)).TieNpc("P09:P17:deleted");

            // doc 05 2 — 예담 × 채령: the invitation with a penalty
            Pair("PS_P17P16_invite", "P17", "P16", "벌칙 있는 초대").Kind_("tease").Time("morning|day")
                .Open(
                    L("P17", "짜잔! 퀴즈쇼 초대장. 빠지면 벌칙!", Emotion.Grin),
                    L("P16", "벌칙 있는 초대는 초대가 아니야.", Emotion.Blank),
                    L("P17", "……"),
                    L("P17", "그럼… 벌칙 없는 걸로 다시 만들어 올게.", Emotion.Sad))
                .Choice(
                    O("'안 와도 돼'라고 써 주면 되겠네요.",
                        L("P17", "안 와도 돼… 그래도 네 자리는 만들어 둘게. 이름표까지.", Emotion.Smile),
                        L("P16", "…풋. 십 분만 갈게. 십 분."))
                        .Do(ToMe("P17", like: 0.04f, trust: 0.03f), ToMe("P16", like: 0.03f), Rel("P16", "P17", like: 0.05f)).Tie("P16:P17:optional").Mem("optional_invite"),
                    O("채령 씨도 한 번쯤 와 봐요. 재밌을 거예요.",
                        L("P16", "…간섭이야. 별로.", Emotion.Blank))
                        .Do(ToMe("P16", like: -0.03f), ToMe("P17", like: 0.02f)),
                    O("벌칙이 뭔데요?",
                        L("P17", "벌칙은… 종이 모형 하나 접기!", Emotion.Grin),
                        L("P16", "…그건 좀 재밌네. 아니, 안 가."))
                        .Do(ToMe("P17", like: 0.03f)))
                .Npc_(Rel("P17", "P16", like: -0.02f), Rel("P16", "P17", like: -0.01f));

            // #18 준서 × 진우 — porridge before candy (comic)
            Pair("PS_P10P02_porridge", "P10", "P02", "죽 먼저, 사탕 나중").Kind_("comic").At("Dining;Kitchen").Time("morning|meal")
                .Open(
                    L("P02", "죽 말고 사탕 없어?"),
                    L("P10", "죽 먹고 사탕 먹어요. 순서가 있어요."),
                    L("P02", "흐응, 명령은 싫은데."),
                    L("P10", "명령 아니에요. 부탁이에요. 허허.", Emotion.Smile),
                    L("P02", "…부탁이면 한 숟가락만."))
                .Choice(
                    O("진우, 두 그릇째인 것 같은데요.",
                        L("P02", "…먹은 거 아니야. 맛본 거야.", Emotion.Smirk),
                        L("P10", "허허, 맛만 두 그릇이네요.", Emotion.Laugh))
                        .Do(ToMe("P02", like: 0.02f), ToMe("P10", like: 0.02f)),
                    O("말없이 사탕 하나를 진우 그릇 옆에 둔다",
                        L("P02", "…큭. 순서 지켰네, 너.", Emotion.Smirk))
                        .Act().Do(ToMe("P02", like: 0.04f)).Mem("candy_after_porridge"))
                .Npc_(Rel("P02", "P10", like: 0.02f)).Every(3);

            // #2 서윤 → 이현 — the greeting (the pen stops)
            Pair("PS_P03P05_pen", "P03", "P05", "멈춘 펜").Kind_("rival").At("Dining;GrandHall;Lounge").Time("morning")
                .Open(
                    L("P05", "서윤 씨, 좋은 아침입니다! 오늘도 표가 반듯하네요.", Emotion.Smile),
                    L("P03", "…네. 좋은 아침이에요."),
                    L("P03", "…펜이 잠깐 안 나와서요. 괜찮아요.", Emotion.Blank))
                .Choice(
                    O("서윤 씨, 괜찮아요?",
                        L("P03", "괜찮아요, 괜찮아요. …펜 좀 똑바로 놓을게요."))
                        .Do(ToMe("P03", trust: 0.02f)).Know("note:이현이 인사하면 서윤의 펜이 멈춘다"),
                    O("이현 씨, 오늘은 일찍 나오셨네요.",
                        L("P05", "하하하. 아침 첫 문장은 제가 쓰는 게 버릇이라서요.", Emotion.Smile))
                        .Do(ToMe("P05", like: 0.02f)))
                .Npc_(Rel("P03", "P05", grudge: 0.01f)).Every(3);

            // #8 라온 × 재하 — the imitation is half a beat fast
            Pair("PS_P08P09_tempo", "P08", "P09", "반 박자").Kind_("rival").Time("day|evening")
                .Open(
                    L("P09", "라온아, 방금 그거 들었어? 이현 씨 말투. '자, 자. 요컨대—'", Emotion.Grin),
                    L("P08", "그 사람 말투, 네가 하면 반 박자 빨라."),
                    L("P09", "라온아, 그래도 단어는 하나도 안 틀렸어."),
                    L("P08", "단어가 다가 아니라니까. 숨 쉬는 자리가 달라."))
                .Choice(
                    O("라온 씨는 그걸 어떻게 들어요?",
                        L("P08", "박자로. 사람마다 숨 쉬는 박자가 있어. 흉내는 그걸 못 따라 해."),
                        L("P09", "…그럼 너한텐 내 흉내가 다 들키겠네.", Emotion.Smile))
                        .Do(ToMe("P08", like: 0.03f, respect: 0.02f)).Know("note:라온은 흉내와 진짜 목소리를 숨 쉬는 박자로 가려낸다").Mem("asked_breath"),
                    O("재하 씨, 제 말투도 해 줘요.",
                        L("P09", "민혁아, 음… '혹시… 괜찮으면.'", Emotion.Laugh),
                        L("P08", "크. 그건 똑같네. 박자까지."))
                        .Do(ToMe("P09", like: 0.04f), ToMe("P08", like: 0.02f)),
                    O("둘 다 대단해요. 한 명은 따라 하고, 한 명은 잡아내고.",
                        L("P09", "아하하, 짝꿍이네 우리.", Emotion.Laugh),
                        L("P08", "짝꿍은 무슨. …뭐, 틀린 말은 아니고."))
                        .Do(ToMe("P08", like: 0.02f), ToMe("P09", like: 0.02f), Rel("P08", "P09", like: 0.03f), Rel("P09", "P08", like: 0.03f)).Tie("P08:P09:respect"))
                .Npc_(Rel("P08", "P09", jealous: 0.02f), Rel("P09", "P08", like: 0.01f));

            // #31 은결 × 예담 — "death is not a quiz" (after a death)
            Pair("PS_P14P17_quiz", "P14", "P17", "정답이 없는 문제").Kind_("conflict").Only("afterdeath").Every(5)
                .Open(
                    L("P17", "추리 퀴즈! 흉기는 뭐였을까요? 힌트는—", Emotion.Grin),
                    L("P14", "예담 씨."),
                    L("P14", "죽음은 퀴즈가 아닙니다. 정답이 없으니까요.", Emotion.Blank),
                    L("P17", "……"),
                    L("P17", "…응. 알았어.", Emotion.Sad))
                .Choice(
                    O("예담 씨도 무서워서 그런 거예요.",
                        L("P17", "…응. 무서우면 문제로 만들어. 버릇이야.", Emotion.Sad),
                        L("P14", "그런가요. …그럼 제 옆에서 백합을 같이 접으시죠. 문제보다 손이 덜 떨립니다."))
                        .Do(ToMe("P17", like: 0.03f, trust: 0.05f), ToMe("P14", like: 0.03f), Rel("P17", "P14", like: 0.06f), Rel("P14", "P17", like: 0.04f)).Tie("P14:P17:lily").Mem("defended_yedam"),
                    O("은결 씨 말이 맞아요.",
                        L("P17", "……", Emotion.Blank),
                        L("P14", "…고맙습니다. 너무 세게 말했다면, 나중에 사과하겠습니다."))
                        .Do(ToMe("P14", respect: 0.03f), ToMe("P17", like: -0.02f)),
                    O("말없이 예담 옆에 앉는다",
                        L("P17", "…고마워.", Emotion.Sad))
                        .Act().Do(ToMe("P17", attach: 0.04f)))
                .Npc_(Rel("P17", "P14", fear: 0.03f), Rel("P14", "P17", like: -0.01f));

            // #7 세나 × 해린 — "can it get better?"
            Pair("PS_P13P11_hand", "P13", "P11", "원래대로").Kind_("warm").At("Workshop;GameRoom").Time("day|evening")
                .Open(
                    L("P13", "해린. 솔직히 말해 줘. 이거 더 좋아질 수 있어?"),
                    L("P11", "…조정은 할 수 있어. 각도, 반응 속도, 그런 거."),
                    L("P13", "그런 거 말고. 원래대로."),
                    L("P11", "……", Emotion.Blank))
                .Choice(
                    O("해린 씨가 대답하게 기다려요.",
                        L("P11", "…못 고쳐. 원래대로는. …미안.", Emotion.Sad),
                        L("P13", "…씨. 알았어. 알았다고. …그래도 조정은 해 줘.", Emotion.Sad))
                        .Do(ToMe("P11", trust: 0.05f), Rel("P13", "P11", trust: 0.06f, attach: 0.04f)).Tie("P11:P13:honest").Mem("let_harin_answer"),
                    O("해린 씨라면 할 수 있을 거예요.",
                        L("P11", "…문제없어. 다 봤어."),
                        L("P13", "진짜지? …진짜지?", Emotion.Surprised))
                        .Do(ToMe("P13", like: 0.03f), Rel("P11", "P13", fear: 0.04f)).Know("tell").For("P11").Tie("P11:P13:lie"),
                    O("세나 씨, 왼손으로도 충분히 빠르잖아요.",
                        L("P13", "0.1초. 그 0.1초 동안 아무것도 못 했어.", Emotion.Angry),
                        L("P13", "…넌 몰라. 그냥 몰라."))
                        .Do(ToMe("P13", like: -0.03f)))
                .Npc_(Rel("P11", "P13", fear: 0.02f));

            // #33 시온 × 준서 — late-night ramen
            Pair("PS_P07P10_ramen", "P07", "P10", "야식은 인권").Kind_("comic").At("Kitchen;Dining").Time("evening|night")
                .Open(
                    L("P07", "셰프! 라면 끓여 줘. 야식은 인권이야.", Emotion.Grin),
                    L("P10", "시온 씨, 라면은 속 버려요. 누룽지 끓여 줄게요."),
                    L("P07", "누룽지? 크하하, 할아버지 음료 다음엔 할아버지 야식이냐."),
                    L("P10", "그거 먹고 자면 내일 아침 목소리가 달라요. 허허."),
                    L("P07", "…목소리? 레알? 그럼 두 그릇."))
                .Choice(
                    O("저도 한 그릇 주세요.",
                        L("P10", "허허, 세 그릇. 오늘 부엌 바쁘네요.", Emotion.Laugh),
                        L("P07", "브로 합류! 오늘 야식 파티다!", Emotion.Grin))
                        .Do(ToMe("P10", like: 0.03f), ToMe("P07", like: 0.03f), Rel("P07", "P10", like: 0.03f)),
                    O("라면이 더 맛있지 않아요?",
                        L("P07", "그치! 브로 알잖아!", Emotion.Grin),
                        L("P10", "…둘 다 누룽지예요. 거절은 안 받아요.", Emotion.Smile))
                        .Do(ToMe("P07", like: 0.03f), ToMe("P10", like: 0.01f)))
                .Npc_(Rel("P07", "P10", like: 0.03f), Rel("P10", "P07", like: 0.02f)).Every(3);

            // #22 가온 × 민서 — the map by steps
            Pair("PS_P15P18_map", "P15", "P18", "출처는 다리").Kind_("warm").At("Library;Archive;Corridor;GrandHall").Time("morning|day|evening")
                .Open(
                    L("P15", "민서 씨, 식당에서 도서실까지 몇 걸음이었죠?"),
                    L("P18", "백열두 걸음입니다. 잠겨 있으면 서른 걸음 더입니다."),
                    L("P15", "적어 둘게요. 출처는… 민서 씨 다리."),
                    L("P18", "허. 제 다리가 출처입니까."))
                .Choice(
                    O("지도 완성되면 저도 보여 줘요.",
                        L("P15", "완성되면요. 제목은 마지막에 붙이니까, 아직 제목 없는 지도예요."),
                        L("P18", "…삼 층은 아직 못 셌습니다. 내일 끝납니다."))
                        .Do(ToMe("P15", like: 0.03f), ToMe("P18", like: 0.03f)).Know("note:가온과 민서가 걸음 수로 저택 지도를 만들고 있다").Mem("map_fan"),
                    O("민서 씨 걸음이랑 제 걸음은 다르지 않아요?",
                        L("P18", "다릅니다. 제 한 걸음은 칠십오 센티미터입니다. 재 봤습니다."),
                        L("P15", "…그래서 민서 씨 다리가 출처예요.", Emotion.Smile))
                        .Do(ToMe("P18", like: 0.02f, respect: 0.02f)))
                .Npc_(Rel("P15", "P18", like: 0.03f), Rel("P18", "P15", like: 0.03f));

            // #3 이현 × 태겸 — the pool, the pocket watch
            Pair("PS_P05P06_pool", "P05", "P06", "물 위의 회의").Kind_("ally").At("Lounge;Pool;Parlor").Time("day|evening")
                .Open(
                    L("P05", "태겸 씨, 저녁에 수영장 어떻습니까. 물 위에선 아무도 받아 적지 못하거든요.", Emotion.Smile),
                    L("P06", "조건은?"),
                    L("P05", "하하하. 조건이라니, 그냥 수영이죠."),
                    L("P06", "그냥, 이라는 말은 장부에 없습니다. …여덟 시. 삼십 분.", Emotion.Blank))
                .Choice(
                    O("저도 가도 돼요? 수영.",
                        L("P05", "…물론이죠. 하하. 민혁 씨도 물에 뜨는 건 자신 있으시죠?", Emotion.Smirk),
                        L("P06", "셋이면 계산이 달라집니다. 흠."))
                        .Do(ToMe("P05", like: 0.01f), ToMe("P06", like: 0.01f)).Know("note:이현과 태겸은 수영장에서 따로 만난다").Mem("pool_three"),
                    O("태겸 씨, 시계는 왜 자꾸 봐요?",
                        L("P06", "시간이 돈이니까요."),
                        L("P05", "하하하. 태겸 씨 농담은 늘 계산돼 있죠."))
                        .Do(ToMe("P06", respect: -0.01f)).Know("note:태겸은 이현이 말할 때마다 회중시계를 본다"))
                .Npc_(Rel("P05", "P06", trust: 0.02f), Rel("P06", "P05", fear: 0.02f));

            // #26 해린 × 라온 — the earphone cable
            Pair("PS_P11P08_cable", "P11", "P08", "왼쪽 이어폰").Kind_("warm").Time("morning|day|evening")
                .Open(
                    L("P08", "해린. 이어폰 선 또 끊겼어. 왼쪽."),
                    L("P11", "오! 이리 줘. 이거 봐, 피복이 헐거워. 삼 분.", Emotion.Grin),
                    L("P08", "핫팩 하나. 수리비."),
                    L("P11", "헤헤. 식은 거 말고 새 거로.", Emotion.Laugh))
                .Choice(
                    O("해린 씨, 제 것도 고쳐 줄 수 있어요?",
                        L("P11", "뭔데? 가져와. 안 되면 안 된다고 할게. 진짜로."))
                        .Do(ToMe("P11", like: 0.04f)),
                    O("말없이 지켜본다",
                        L("P08", "…삼 분이라더니 이 분 반. 크."))
                        .Act().Do(ToMe("P08", like: 0.01f)))
                .Npc_(Rel("P11", "P08", like: 0.02f), Rel("P08", "P11", like: 0.02f)).Every(3);

            // #4 은결 × 도윤 — night corridor
            Pair("PS_P14P04_corridor", "P14", "P04", "벽이 얇은 복도").Kind_("conflict").At("Corridor;Gallery;Landing").Time("night").Only("night")
                .Open(
                    L("P14", "도윤아, 이 시간에 복도는 왜."),
                    L("P04", "액자가 또 왼쪽으로 기울었습니다. …은결아, 방에서 얘기하자. 여긴 벽이 얇아."),
                    L("P14", "얇으니까 여기서 묻는 거야. 방에선 네가 문부터 닫잖아.", Emotion.Blank),
                    If("present:P01", L("P14", "…민혁 씨. 아무 일도 아닙니다. 가족 사이의 일이에요.")))
                .Choice(
                    O("도윤 씨, 액자는 내일 같이 바로잡아요.",
                        L("P04", "…괜찮으시다면요. 제자리는 제가 압니다."),
                        L("P14", "…고맙습니다. 동생을 부탁하는 말처럼 들렸다면, 그건 맞습니다."))
                        .Do(ToMe("P04", like: 0.02f), ToMe("P14", trust: 0.03f)),
                    O("두 분, 가족이었어요?",
                        L("P14", "…비슷하게 생겼다는 말은 자주 듣습니다. 후후.", Emotion.Blank))
                        .Do(ToMe("P14", like: -0.02f)).Know("note:은결과 도윤은 둘만 있을 때 말을 놓는다. 가족일지도 모른다").Mem("asked_siblings"),
                    O("말없이 지나간다",
                        L("P04", "…"))
                        .Act().Do(ToMe("P14", respect: 0.02f)))
                .Npc_(Rel("P04", "P14", fear: 0.02f));
        }
    }
}
