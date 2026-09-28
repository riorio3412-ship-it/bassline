namespace BL23.Sim
{
    public static partial class LineBank
    {
        // Factions, gatherings and house events (SocialEventsDesign.md). Each resident in their own voice (CharacterBible §2:
        // one owner per tic); slots: {t} the faction's leader, {group} what people call it ("시온네"), {n} how many.
        static void Init_Social()
        {
            // ═════════ 무리 — the faction's own people ═════════
            VP("P03", "tt_faction", "정리하면, 요즘 저희 {n}이 같이 다녀요. 명단은 제가 들고 있어요.");
            VC("P03", "tt_faction", "정리하면, 요즘 우리 {n}이 같이 다녀. 명단은 내가 들고 있어.");
            VP("P05", "tt_faction", "자, 자. 요컨대 뜻이 맞는 사람끼리 모인 것뿐입니다. 문은 열려 있어요.");
            VP("P06", "tt_faction", "흠. 몇 분과 협정을 맺었습니다. 조건은 하나, 서로를 지키는 것.");
            VC("P07", "tt_faction", "요즘 우리끼리 모이는 거 알지? 크하하, 들어오고 싶으면 말만 해.", "우리 무리 이름? {group}. 촌스럽다고? 의리는 안 촌스러워.");
            VC("P09", "tt_faction", "{you:아}, 우리 요즘 같이 다녀. 같이 있으면 덜 무서워서.");
            VP("P10", "tt_faction", "요즘 몇 명이서 같이 밥 먹어요. 우선 앉아요. 자리 하나 더 놓을까요?");
            VP("P12", "tt_faction", "요즘 저희끼리 자주 뭉쳐요. 에헤헤, 무대 동기 같은 느낌?");
            VC("P13", "tt_faction", "솔직히 팀 짜는 게 이기는 법이야. 우리 팀 있어. 한 판 붙어 볼래?");
            VC("P17", "tt_faction", "있지있지, 우리 팀 생겼어! 팀 이름은 아직 판정 중.");
            VP("P18", "tt_faction", "허. 몇 명이서 교대로 서로 살핍니다. 네 시간씩입니다.");
            VP("ANY", "tt_faction", "요즘 몇 명이서 같이 다녀요. 그게 덜 무서워서요.");
            VC("ANY", "tt_faction", "요즘 몇 명이서 같이 다녀. 덜 무서워서.");

            VP("P03", "tt_faction_in", "명단에 넣어 뒀어요. 빠지면 제가 찾으러 가요.");
            VP("P05", "tt_faction_in", "자, 자. 서로 챙기는 게 질서의 첫 문장이죠.");
            VP("P06", "tt_faction_in", "협정은 지킵니다. 됐습니다.");
            VC("P07", "tt_faction_in", "의리지. 크하하.");
            VC("P09", "tt_faction_in", "응. 같이 있으면 대사가 덜 떨려.");
            VP("P10", "tt_faction_in", "밥은 제가 챙겨요. 한 명도 안 빠지게.");
            VC("P11", "tt_faction_in", "오, 우리 팀 규격 딱 맞아. 헤헤.");
            VP("P12", "tt_faction_in", "우리 반짝이 팀이에요. 에헤헤.");
            VC("P13", "tt_faction_in", "우리 쪽 건드리면 GG야.");
            VP("P15", "tt_faction_in", "서로 한 말은 제가 적어 둘게요. 틀리지 않게요.");
            VC("P16", "tt_faction_in", "뭐, 어쨌든. 나쁘지 않아.");
            VC("P17", "tt_faction_in", "판정합니다. 우리 팀 최고!");
            VP("P18", "tt_faction_in", "제 순번은 새벽입니다. 끝났습니다.");
            VP("ANY", "tt_faction_in", "저도 같이요.");
            VC("ANY", "tt_faction_in", "나도 같이야.");

            // ═════════ 무리 — the ones outside it ═════════
            VC("P02", "tt_faction_out", "흐응, {group}? 무리 짓는 거 재밌네. 맞혀 볼까, 누가 제일 먼저 배신할지.", "끼리끼리 모이면 편하지. 나중에 서로 찌르기도 편하고. 큭.");
            VP("P04", "tt_faction_out", "그렇군요. 모두 제자리를 찾아가는군요.");
            VP("P05", "tt_faction_out", "자, 자. 무리가 생기면 파벌이 되고, 파벌은 표가 됩니다. 기억해 두세요.");
            VP("P06", "tt_faction_out", "흠. 무리는 셈이 빠릅니다. 빠지는 사람도 빠르고요.");
            VC("P08", "tt_faction_out", "아 뭐, 몰려다니든가. 난 혼자가 편해.", "됐고, {group} 소리 좀 줄여. 박자가 다 똑같아서 시끄러워.");
            VC("P11", "tt_faction_out", "오, 팀? …난 부품 하나 남는 느낌이네. 헤헤, 괜찮아.");
            VP("P14", "tt_faction_out", "그런가요. 무리는 매듭과 같아서, 풀 때 가장 아픕니다.");
            VP("P15", "tt_faction_out", "잠깐만요. 무리 안에서 나온 말은 출처가 흐려져요. 적어 둘게요.");
            VC("P16", "tt_faction_out", "풋. 끼리끼리. 별로.");
            VC("P13", "tt_faction_out", "솔직히 {group} 좀 거슬려. 우르르 다니는 거.");
            VC("P07", "tt_faction_out", "{group}? 레알? 나 빼고? …아니, 안 부러워.");
            VP("P10", "tt_faction_out", "무리가 어떻든 밥상은 하나예요. 다 같이 먹어요.");
            VC("P17", "tt_faction_out", "있지있지, 팀 나누기? 그럼 난 심판 할래. 판정합니다, 반칙 없기.");
            VP("P03", "tt_faction_out", "정리하면, 무리가 몇 개든 식탁은 하나예요. 다들 여기 앉아요.");
            VC("P09", "tt_faction_out", "{you:아}, 우리도 무리 하나 만들까? …농담이야. 반만.");
            VP("P12", "tt_faction_out", "와, 팀이다. 저도 끼면 안 돼요? 에헤헤…");
            VP("P18", "tt_faction_out", "허. 무리는 셈이 편합니다. 저는 혼자 셉니다.");
            VP("P04", "tt_faction_in", "제자리에 있는 기분이군요. 괜찮으시다면, 계속.");
            VP("P14", "tt_faction_in", "후후. 매듭이 단단하군요.");
            VP("ANY", "tt_faction_out", "…무리 짓는 거, 전 좀 불안해요.");
            VC("ANY", "tt_faction_out", "…몰려다니는 거, 난 좀 불안해.");

            // ═════════ 저택 행사 (HouseEvents) — 유스티's notices and openings ═════════
            Add("NPC00", "y_hev_banquet", P(
                "알려 드립니다. 오늘 {time}, {place}에서 저택이 연회를 엽니다.|초대는 모두에게 드립니다. 오실지는 각자 정하십시오.",
                "오늘 저녁은 저택이 차립니다. {time}, {place}입니다.|잔을 채워 두겠습니다. …건배는 한 번뿐입니다."));
            Add("NPC00", "y_hev_masque", P(
                "알려 드립니다. 오늘 {time}, {place}에서 가면의 밤을 엽니다.|문 앞에서 가면을 나눠 드립니다. 오늘 밤만은 이름을 묻지 않는 것이 예법입니다."));
            Add("NPC00", "y_hev_hunt", P(
                "알려 드립니다. 오늘 {time}, {place}에서 보물찾기를 시작합니다.|구역표를 읽겠습니다.|{list}|한 분이 한 구역씩입니다. 찾으신 분께는 선물이 있습니다."));
            Add("NPC00", "y_hev_stars", P(
                "알려 드립니다. 오늘 {time}, {place}에서 별을 봅니다.|그 시간 동안 그곳의 불은 꺼 두겠습니다. 발밑을 조심하십시오."));
            Add("NPC00", "y_hev_vigil", P(
                "알려 드립니다. 오늘 {time}, {place}에 촛불을 켜 두겠습니다.|기도하실 분, 쉬실 분, 누구든 오셔도 됩니다."));
            Add("NPC00", "hev_open_banquet", P("잔을 채워 두었습니다. 오늘 밤은 저택이 차린 자리입니다.|건배는 조금 뒤에 하겠습니다. 그때까지 편히 드십시오."));
            Add("NPC00", "hev_open_masque", P("가면을 쓰십시오. 오늘 밤만은 누가 누구인지 묻지 않는 것이 예법입니다."));
            Add("NPC00", "hev_open_hunt", P("저택 곳곳에 작은 상자를 숨겨 두었습니다. 아침에 읽어 드린 구역을 맡아 주십시오.|찾으신 분께는 저녁에 작은 선물이 있습니다."));
            Add("NPC00", "hev_open_stars", P("불을 끄겠습니다. 별은 어두울수록 잘 보입니다.|…오늘 밤은 제가 한 걸음 물러나 있겠습니다."));
            Add("NPC00", "hev_open_vigil", P("촛불만 켜 두겠습니다. 눈을 감으셔도 좋습니다.|기도하실 분은 기도를, 쉬실 분은 쉬십시오."));

            // ═════════ 연회 ═════════
            VC("P07", "hev_banquet", "크하하, 공짜 술이다! 오늘은 내가 제일 먼저 취한다. 레알.");
            VC("P02", "hev_banquet", "연회래. 흐응, 다들 웃는 얼굴 좀 봐. 누가 제일 먼저 잔을 내려놓을까?");
            VP("P05", "hev_banquet", "자, 자. 이런 자리일수록 첫 잔이 중요합니다. 우리 모두를 위해.");
            VP("P12", "hev_banquet", "대박, 샹들리에 봐요! 오늘도 반짝! …아, 무서운 거 잊을 뻔했다.");
            VP("P10", "hev_banquet", "음식은 누가 한 거예요? …간 좀 봐 줄래요? 제가 한 게 아니라서 불안해요.");
            VP("P06", "hev_banquet", "흠. 이 연회의 값은 누가 치르는지부터 궁금하군요.");
            VC("P16", "hev_banquet", "풋. 드레스 코드 없는 연회. 별로. 그래도 왔어.");
            VC("P13", "hev_banquet", "솔직히 공짜 밥은 좋다. 근데 불 꺼지면 난 벽에 붙어 있을 거야.");
            VP("P15", "hev_banquet", "잠깐만요. 잔은 누가 채웠어요? 적어 둘게요. 습관이에요.");
            VP("P18", "hev_banquet", "허. 제 몫은 반만 받겠습니다. 남은 건 싸 가겠습니다.");
            VP("P03", "hev_banquet", "정리하면, 오늘은 다 같이 웃는 날이에요. 잔은 제가 셀게요.");
            VC("P09", "hev_banquet", "{you:아}, 샹들리에 아래 서 봐. …아니, 그냥. 조명이 예뻐서.");
            VP("ANY", "hev_banquet", "…연회라니, 이상하게 들떠요.");
            VC("ANY", "hev_banquet", "…연회라니. 이상하게 들떠.");
            VP("ANY", "hev_banquet_toast", "좋아요. 짧게 해요. 다들 잔 들어요.");
            VC("ANY", "hev_banquet_toast", "좋아. 짧게 해. 다들 잔 들어.");
            VC("P07", "hev_banquet_toast", "크하하, 건배사는 원래 내 담당인데. 해 봐.");
            VP("P03", "hev_banquet_toast", "좋아요. 다들 잔 들었는지 제가 셀게요.");
            VP("ANY", "hev_banquet_wary", "…그것도 맞는 말이네요. 저도 한 모금만.");
            VC("ANY", "hev_banquet_wary", "…그것도 맞네. 나도 한 모금만.");
            VP("P15", "hev_banquet_wary", "잠깐만요, 저도요. 잔은 제가 보고 마실게요.");
            VC("P13", "hev_banquet_wary", "솔직히 나도 안 마실래. 네 말 맞아.");

            // ═════════ 가면의 밤 ═════════
            VC("P02", "hev_masque", "가면이라. 흐응, 표정을 못 보면 재미없는데. …아, 목소리는 들리지.");
            VC("P07", "hev_masque", "야, 이거 쓰니까 나 누군지 모르지? …목소리로 안다고? 씨발.");
            VC("P17", "hev_masque", "있지있지, 가면 퀴즈 하자! 누가 누군지 맞히기! 판정은 내가 할게.");
            VP("P12", "hev_masque", "가면 쓰니까 오히려 편해요. 웃는 연습 안 해도 되잖아요. 에헤헤.");
            VC("P09", "hev_masque", "{you:아}, 가면은 대사를 쉽게 해 줘. 얼굴이 안 보이니까.");
            VP("P14", "hev_masque", "…가면은 익숙합니다. 매일 쓰는 것과 크게 다르지 않군요.");
            VC("P08", "hev_masque", "아 뭐, 가면 좋네. 아무도 말 안 걸잖아.");
            VC("P16", "hev_masque", "가면 핏은 괜찮네. 끈이 좀 촌스럽지만. 뭐, 어쨌든.");
            VP("P15", "hev_masque", "잠깐만요. 가면 쓴 사람 말은 출처를 어떻게 적어요? …곤란하네요.");
            VP("P04", "hev_masque", "그렇군요. 얼굴을 가리면, 손이 더 잘 보입니다.");
            VP("ANY", "hev_masque", "…가면을 쓰니까 누가 누군지 모르겠어요.");
            VC("ANY", "hev_masque", "…가면 쓰니까 누가 누군지 모르겠어.");
            VP("ANY", "hev_masque_guess", "그게 재밌는 거잖아요.");
            VC("ANY", "hev_masque_guess", "그게 재밌는 거잖아.");
            VC("P17", "hev_masque_guess", "판정합니다! 방금 너, 목소리로 들켰어!");
            VC("P02", "hev_masque_guess", "흐응, 모른다니 다행이다. 나도 너 모르는 척할게.");
            VP("ANY", "hev_masque_bare", "…그럼 표정 다 보이겠네요.");
            VC("ANY", "hev_masque_bare", "…그럼 표정 다 보이겠네.");
            VP("P14", "hev_masque_bare", "…용감하시군요. 맨얼굴이 더 많은 걸 숨기기도 합니다만.");

            // ═════════ 보물찾기 ═════════
            VC("P17", "hev_hunt", "짜잔! 보물찾기! 이건 내 전문이야. 판정합니다, 내가 일등!");
            VC("P11", "hev_hunt", "오! 상자라며? 잠금장치 있으면 내 거야. 헤헤.");
            VC("P13", "hev_hunt", "한 판 붙자. 제일 먼저 찾는 사람이 이기는 거지? GG 칠 준비 해.");
            VP("P03", "hev_hunt", "정리하면, 한 사람에 한 구역. 끝나면 여기로 다시 모여요. 제가 셀게요.");
            VP("P06", "hev_hunt", "흠. 선물의 값어치가 궁금하군요. 조건부터 보겠습니다.");
            VC("P02", "hev_hunt", "혼자 한 구역씩이래. 흐응, 저택은 친절하네. 혼자 있게 해 주고.");
            VP("P18", "hev_hunt", "허. 제 구역은 걸어서 이백 걸음입니다. 다녀오겠습니다.");
            VC("P08", "hev_hunt", "…혼자 찾으래서 온 거야. 사람 많은 건 질색이라.");
            VP("P10", "hev_hunt", "찾으면 간식 줄게요. 다들 빈속으로 뛰지 마요.");
            VP("P12", "hev_hunt", "혼자요? …좀 무섭다. 에헤헤, 아니에요, 할 수 있어요.");
            VP("ANY", "hev_hunt", "…혼자 한 구역이요? 좀 무섭네요.");
            VC("ANY", "hev_hunt", "…혼자 한 구역? 좀 무섭네.");
            VP("ANY", "hev_hunt_pair", "…구역이 다르잖아요. 그래도, 좋아요.");
            VC("ANY", "hev_hunt_pair", "…구역 다르잖아. 그래도 좋아.");
            VP("P12", "hev_hunt_pair", "진짜요? 좋아요! 혼자 무서웠어요. 에헤헤.");
            VC("P09", "hev_hunt_pair", "{you:아}, 응. 둘이면 대사가 덜 떨려.");

            // ═════════ 별 보는 밤 ═════════
            VC("P09", "hev_stars", "{you:아}, 별은 막이 안 내려서 좋아. 계속 떠 있잖아.");
            VP("P12", "hev_stars", "와… 진짜 별이다. 여기 와서 처음으로 소원 비는 게 안 무서워요.");
            VC("P07", "hev_stars", "야, 저거 별자리 아냐? 모르겠다. 크하하, 그냥 멋있다.");
            VC("P08", "hev_stars", "…조용하네. 이런 박자면 괜찮아.");
            VC("P17", "hev_stars", "있지있지, 별 이름 퀴즈! …아니야, 오늘은 그냥 보자.");
            VP("P14", "hev_stars", "별도 한때는 살아 있던 빛이라고 하지요. 후후.");
            VP("P03", "hev_stars", "정리하면… 아무것도 정리 안 하고 싶은 밤이에요.");
            VC("P02", "hev_stars", "별은 표정이 없어서 재미없어. …근데 이상하게 계속 보게 되네.");
            VP("P15", "hev_stars", "잠깐만요. 오늘 날씨 적어 둘게요. 맑음, 별 많음.");
            VC("P16", "hev_stars", "별로. …라고 하기엔 너무 예쁘네. 풋.");
            VP("ANY", "hev_stars", "…별이 이렇게 많았네요.");
            VC("ANY", "hev_stars", "…별이 이렇게 많았구나.");
            VP("ANY", "hev_stars_name", "…몰라요. 그래도 같이 보니까 좋네요.");
            VC("ANY", "hev_stars_name", "…몰라. 그래도 같이 보니까 좋다.");
            VP("P12", "hev_stars_name", "저건… 모르겠어요. 우리가 이름 붙일까요? 에헤헤.");
            VC("P09", "hev_stars_name", "{you:아}, 이름은 몰라. 대신 오늘 밤 걸로 기억할게.");
            VP("P15", "hev_stars_name", "잠깐만요, 찾아볼게요. …틀리면 정정할게요.");
            VP("ANY", "hev_stars_wish", "…별한테라면, 빌어도 되겠네요.");
            VC("ANY", "hev_stars_wish", "…별한테라면 빌어도 되겠다.");
            VP("P03", "hev_stars_wish", "좋아요. 정리하면, 이건 저택 소원이 아니에요. 우리 소원이에요.");

            // ═════════ 밤의 기도 ═════════
            VP("P03", "hev_vigil", "…다들 무사하기를. 정리하면, 그게 다예요.");
            VP("P14", "hev_vigil", "…기도는 산 사람을 위한 것이지요. 촛불이 흔들리지 않게 하겠습니다.");
            VP("P10", "hev_vigil", "…다 같이 밥 먹게 해 주세요. 그거면 돼요.");
            VP("P12", "hev_vigil", "…무대 말고, 여기서 다 같이 나가게 해 주세요.");
            VP("P04", "hev_vigil", "…그렇군요. 촛불 아래에선 모두 같은 그림자군요.");
            VP("P05", "hev_vigil", "…자, 자. 오늘만은 연설 없이 가겠습니다.");
            VP("P18", "hev_vigil", "…허. 사 년 동안 기도는 안 했습니다. 오늘은 하겠습니다.");
            VC("P09", "hev_vigil", "…세상에, 이렇게 조용한 무대는 처음이야.");
            VC("P17", "hev_vigil", "…오늘은 퀴즈 없어. 판정도 없어. 그냥 빌게.");
            VC("P13", "hev_vigil", "솔직히 안 믿어. …그래도 눈은 감을게.");
            VP("ANY", "hev_vigil", "…모두 무사하기를.");
            VC("ANY", "hev_vigil", "…다들 무사하기를.");
            VP("ANY", "hev_vigil_amen", "…네. 다들요.");
            VC("ANY", "hev_vigil_amen", "…응. 다들.");

            // ═════════ 오늘 밤의 초대 (the table before the house's evening): going, or not, and why ═════════
            VC("P07", "tt_hev_go", "오늘 밤 {act}! 크하하, 다들 와. 안 오면 내가 데리러 간다.");
            VC("P17", "tt_hev_go", "있지있지, 오늘 {act}이래! 짜잔, 나는 벌써 준비 끝!");
            VP("P12", "tt_hev_go", "오늘 {act} 가실 거죠? 저 벌써 두근거려요. 에헤헤.");
            VP("P05", "tt_hev_go", "자, 자. 오늘 {act}, 다 같이 가는 게 서로를 지키는 길입니다.");
            VP("P03", "tt_hev_go", "정리하면, 오늘 {act}은 {time}이에요. 오는 사람 제가 셀게요.");
            VC("P09", "tt_hev_go", "{you:아}, 오늘 {act} 갈 거지? 같이 가자.");
            VP("P10", "tt_hev_go", "오늘 {act} 전에 뭐라도 드시고 가요. 빈속은 안 돼요.");
            VC("P13", "tt_hev_go", "오늘 {act}? 솔직히 재밌겠다. 간다.");
            VP("ANY", "tt_hev_go", "오늘 {act}, 가실 거죠?");
            VC("ANY", "tt_hev_go", "오늘 {act}, 갈 거지?");

            VC("P02", "tt_hev_yes", "흐응, 가야지. 다들 어떤 얼굴로 오나 보고 싶거든.");
            VP("P06", "tt_hev_yes", "흠. 가겠습니다. 빠지는 쪽이 더 비싸게 먹힐 것 같군요.");
            VC("P11", "tt_hev_yes", "오! 갈래. 어떻게 돌아가는지 보고 싶어.");
            VP("P15", "tt_hev_yes", "갈게요. 누가 왔는지 적어 두려고요.");
            VC("P16", "tt_hev_yes", "뭐, 어쨌든. 갈게. 입을 옷은 있어.");
            VP("P18", "tt_hev_yes", "허. 가겠습니다. 끝나면 바로 자겠습니다.");
            VP("P14", "tt_hev_yes", "…가겠습니다. 자리가 비면 쓸쓸하니까요.");
            VP("P04", "tt_hev_yes", "그렇군요. 괜찮으시다면 저도 한자리.");
            VP("ANY", "tt_hev_yes", "저도 갈게요.");
            VC("ANY", "tt_hev_yes", "나도 갈래.");

            VC("P08", "tt_hev_no_crowd", "난 패스. 사람 많은 데는 박자가 안 맞아.");
            VC("P02", "tt_hev_no_crowd", "흐응, 다 같이 우르르? 난 구경꾼이 좋아. 안 가.");
            VP("P04", "tt_hev_no_crowd", "그렇군요. 저는 조용한 쪽이 제자리입니다. 사양하겠습니다.");
            VP("P14", "tt_hev_no_crowd", "…저는 사양하겠습니다. 소란스러운 자리는 익숙하지 않아서요.");
            VC("P16", "tt_hev_no_crowd", "별로. 사람 많은 데 가면 옷이 구겨져.");
            VP("ANY", "tt_hev_no_crowd", "…저는 사람 많은 데는 좀. 오늘은 빠질게요.");
            VC("ANY", "tt_hev_no_crowd", "…난 사람 많은 데는 좀. 빠질래.");

            VP("P12", "tt_hev_no_fear", "…무서워요. 불 꺼지고 그러면… 오늘은 방에 있을래요.");
            VC("P11", "tt_hev_no_fear", "씨, 난 못 가. 어두운 데서 뭐가 튀어나올 것 같아.");
            VC("P09", "tt_hev_no_fear", "{you:아}, 미안. 오늘은… 무서워서 못 가겠어.");
            VP("P03", "tt_hev_no_fear", "…솔직히 오늘은 무서워요. 정리가 안 돼요. 방에 있을게요.");
            VC("P17", "tt_hev_no_fear", "…판정 보류. 오늘은 무서워서 안 갈래.");
            VP("P10", "tt_hev_no_fear", "…오늘은 부엌에 있을게요. 거기가 제일 덜 무서워요.");
            VP("ANY", "tt_hev_no_fear", "…무서워서요. 오늘은 방에 있을게요.");
            VC("ANY", "tt_hev_no_fear", "…무서워서. 오늘은 방에 있을래.");

            VC("P07", "tt_hev_no_enemy", "그 인간 온다며? 그럼 난 안 가. 레알.");
            VC("P13", "tt_hev_no_enemy", "솔직히 거기 오는 놈 중에 꼴도 보기 싫은 놈 있어. 패스.");
            VP("P06", "tt_hev_no_enemy", "흠. 같은 자리에 앉고 싶지 않은 분이 계셔서요. 사양하겠습니다.");
            VC("P16", "tt_hev_no_enemy", "그 사람 온다며. 풋. 그럼 난 됐어.");
            VC("P02", "tt_hev_no_enemy", "거기 누가 오는지 알잖아? 흐응, 난 안 가. 재미없어져.");
            VP("P05", "tt_hev_no_enemy", "자, 자. 불필요한 충돌은 피하는 게 질서죠. 오늘은 빠지겠습니다.");
            VP("ANY", "tt_hev_no_enemy", "…그 사람 온다면서요. 전 안 갈래요.");
            VC("ANY", "tt_hev_no_enemy", "…그 사람 온다며. 난 안 가.");

            VP("ANY", "tt_hev_no_lead", "…저희 쪽은 오늘 안 가기로 했어요.");
            VC("ANY", "tt_hev_no_lead", "…우리 쪽은 오늘 안 가기로 했어.");
            VC("P07", "tt_hev_no_lead", "우리 쪽 대장이 안 간대. 의리지. 나도 패스.");
            VP("P12", "tt_hev_no_lead", "다들 안 간대서요… 저도 오늘은 같이 있을게요.");

            VP("ANY", "tt_hev_no", "…저는 오늘은 쉴래요.");
            VC("ANY", "tt_hev_no", "…난 오늘은 쉴래.");
            VP("P18", "tt_hev_no", "허. 오늘은 쉬겠습니다. 몸이 셈을 못 따라갑니다.");
            VC("P08", "tt_hev_no", "…됐고, 오늘은 잘래.");

            // ═════════ 주민 모임 (Grammars) — the host opens their own evening ═════════
            VP("ANY", "gath_open_tea", "차 우렸어요. 식기 전에 드세요. 오늘은 무서운 얘기 빼고요.");
            VC("ANY", "gath_open_tea", "차 우렸어. 식기 전에 마셔. 오늘은 무서운 얘기 빼고.");
            VP("P10", "gath_open_tea", "우선 앉아요. 차는 제가 따를게요. 과자도 구웠어요. 허허.");
            VP("P14", "gath_open_tea", "차를 준비했습니다. 따뜻할 때 드십시오. 후후.");
            VP("ANY", "gath_open_cards", "카드 섞을게요. 오늘은 돈 말고 과자 걸고 해요.");
            VC("ANY", "gath_open_cards", "카드 섞는다. 오늘은 과자 걸고 하자.");
            VC("P02", "gath_open_cards", "자, 한 판. 거는 건 과자, 보는 건 표정. 큭.");
            VC("P13", "gath_open_cards", "한 판 붙자. 봐주는 거 없다. GG 칠 준비 해.");
            VP("ANY", "gath_open_music", "몇 곡 준비했어요. 틀려도 박수는 쳐 주세요.");
            VC("ANY", "gath_open_music", "몇 곡 준비했어. 틀려도 박수 쳐.");
            VC("P09", "gath_open_music", "{you:아}, 오늘은 조명 대신 촛불이야. 첫 곡 간다.");
            VP("P12", "gath_open_music", "오늘도 반짝! 작은 무대지만 진심이에요.");
            VP("ANY", "gath_open_reading", "오늘 읽을 책이에요. 한 장씩 돌아가며 읽어요.");
            VC("ANY", "gath_open_reading", "오늘 읽을 책. 한 장씩 돌아가며 읽자.");
            VP("P15", "gath_open_reading", "원문 그대로 읽을게요. 틀리면 정정해 주세요.");
            VP("P03", "gath_open_reading", "정리하면, 한 사람에 한 쪽씩이에요. 순서는 제가 정할게요.");
            VP("ANY", "gath_open_party", "작은 파티예요. 오늘만큼은 저택 생각 하지 마요.");
            VC("ANY", "gath_open_party", "작은 파티야. 오늘만큼은 저택 생각 하지 마.");
            VC("P07", "gath_open_party", "크하하, 파티다! 오늘은 내 플레이리스트야. 불만은 안 받는다.");
            VC("P17", "gath_open_party", "짜잔! 파티 시작! 규칙은 하나, 우울한 얼굴 금지!");
            VP("ANY", "gath_open_show", "준비한 걸 보여 드릴게요. 끝까지 봐 주세요.");
            VC("ANY", "gath_open_show", "준비한 거 보여 줄게. 끝까지 봐.");
            VC("P09", "gath_open_show", "막 올린다. {you:아}, 맨 앞자리 비워 뒀어.");
            VP("ANY", "gath_open_film", "영사기 돌릴게요. 불 끕니다. 무서운 건 아니에요.");
            VC("ANY", "gath_open_film", "영사기 돌린다. 불 끈다. 무서운 건 아니야.");
            VC("P08", "gath_open_film", "…불 끌게. 소리는 내가 맞췄어. 조용히 봐.");

            // ═════════ 모임 — the ones not asked, told so at the table ═════════
            VC("P02", "tt_event_out", "흐응, 나는 안 불렀네? 괜찮아. 표정 구경은 여기서도 되니까.");
            VC("P07", "tt_event_out", "야, 나 빼고 {act}? 레알 서운하다.");
            VP("P12", "tt_event_out", "어… 저는 초대 못 받았네요. 에헤헤, 괜찮아요.");
            VC("P13", "tt_event_out", "솔직히 안 불러 준 건 좀 짜증 나.");
            VC("P08", "tt_event_out", "아 뭐, 안 불러도 돼. 안 갈 거였어.");
            VC("P09", "tt_event_out", "{you:아}… 나도 끼면 안 될까? 아, 아니야.");
            VC("P17", "tt_event_out", "있지있지, 나는 초대장 못 받았어! 판정합니다, 반칙!");
            VC("P16", "tt_event_out", "풋. 나 빼고 하는구나. 별로.");
            VP("P05", "tt_event_out", "자, 자. 초대 명단이란 게 원래 정치죠. 하하하.");
            VP("P06", "tt_event_out", "흠. 명단에서 빠졌군요. 셈은 해 두겠습니다.");
            VP("ANY", "tt_event_out", "…저는 초대 못 받았네요.");
            VC("ANY", "tt_event_out", "…난 초대 못 받았네.");
        }
    }
}
