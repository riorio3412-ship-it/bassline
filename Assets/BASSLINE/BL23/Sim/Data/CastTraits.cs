using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// Per-character traits from CharacterBible §2 (voice matrix), §4 (entries), §6 (witness lenses) and DailyLifeDesign §4.4
    /// (who loves what), §7.2/§7.7 (covers and innocent quirks), §8.2 (grief behaviour). DATA ONLY: nothing in the kernel reads it
    /// yet except the voice tests and authoring checks. Planned readers: tells on Utterance.Lie (F8), hangouts (F7), the day
    /// board (haunts, F18), foreshadow covers (F12), murder-planner method affinity (MF, optional).
    /// </summary>
    public static class CastTraits
    {
        public enum Fav { Refuse = -1, Will = 0, Love = 1 }

        public sealed class Traits
        {
            public string Id;
            /// <summary>Register in one line (존댓말/반말 and when it changes).</summary>
            public string Register;
            /// <summary>Signature tics this resident OWNS (nobody else may use them, bible §2 "one owner per tic").</summary>
            public string[] Tics = new string[0];
            public string Laugh;
            public string[] PetWords = new string[0];
            public string[] NeverSays = new string[0];
            /// <summary>The lying tell (performed on Utterance.Lie): what the body does, and the phrase if any.</summary>
            public string TellGesture, TellPhrase;
            /// <summary>What this resident notices as a witness (bible §6.2).</summary>
            public string Lens;
            /// <summary>Where they usually are: morning / afternoon / evening / night (RoomType names; "" = own room / varies).</summary>
            public string[] Haunts = new string[4];
            /// <summary>Fixed-time habits (clock minute of day → what), e.g. 은결's 09:12 walk.</summary>
            public (int minute, string what)[] Fixed = new (int, string)[0];
            /// <summary>Habits that look like murder preparation but are innocent (DailyLife §7.7) — red herrings.</summary>
            public string Quirk, QuirkLooksLike;
            /// <summary>Visible grief behaviour the day after a death (DailyLife §8.2).</summary>
            public string Grief;
            /// <summary>Case-role weights 1–5 (bible "case roles"): culprit, victim, witness, red herring, accomplice. Weights, not casting.</summary>
            public int Culprit, Victim, Witness, RedHerring, Accomplice;
            /// <summary>Hangout activities (DailyLife §4.4): ♥ love, ○ will, ✕ refuse (with the refusal line).</summary>
            public Dictionary<string, Fav> Hangouts = new Dictionary<string, Fav>();
            public string RefuseLine;
            /// <summary>A few words for the day board / notebook: the resident's hook.</summary>
            public string Hook;
        }

        /// <summary>Hangout activity ids in DailyLife §4.4 column order.</summary>
        public static readonly string[] HangoutActs = { "cards", "piano", "tea", "cook", "garden", "read", "billiards", "darts", "darkroom", "chess", "walk", "swim", "craft", "repair", "lines", "bar" };

        static Dictionary<string, Fav> H(string row)
        {
            // row: 16 marks in HangoutActs order, '♥' love, '○' will, '✕' refuse
            var d = new Dictionary<string, Fav>(); var marks = row.Replace(" ", "");
            for (int i = 0; i < HangoutActs.Length && i < marks.Length; i++) d[HangoutActs[i]] = marks[i] == '♥' ? Fav.Love : marks[i] == '✕' ? Fav.Refuse : Fav.Will;
            return d;
        }

        public static readonly Dictionary<string, Traits> All = new Dictionary<string, Traits>
        {
            ["P01"] = new Traits
            {
                Id = "P01", Hook = "계약 없이 들어온 단 한 사람", Register = "부드러운 해요체 → 합의 후 조심스러운 반말",
                Tics = new[] { "혹시…", "괜찮으면" }, Laugh = "하하", PetWords = new[] { "빵", "같이", "부탁" }, NeverSays = new[] { "살인자", "증거보다 많은 단정" },
                Lens = "—", Haunts = new[] { "", "", "", "" },
            },
            ["P02"] = new Traits
            {
                Id = "P02", Hook = "사람의 반응을 수집하는 심리학도", Register = "반말만. 존댓말은 비꼴 때만",
                Tics = new[] { "흐응", "맞혀 볼까?" }, Laugh = "큭", PetWords = new[] { "반응", "표정", "손", "사탕" }, NeverSays = new[] { "괜찮아(위로로)", "가벼운 미안" },
                TellGesture = "거짓말 직전에 사탕을 깨문다", TellPhrase = "대답 대신 질문으로 되받는다",
                Lens = "얼굴", Haunts = new[] { "Dining", "Library", "Lounge", "" }, Fixed = new[] { (23 * 60 + 40, "어디에 있든 시계를 본다") },
                Quirk = "사탕 껍질을 꼬아 만든 작은 끈을 머문 자리에 남긴다", QuirkLooksLike = "현장의 서명",
                Grief = "하루 동안 사탕을 권하지 않는다. 죽은 사람의 의자에 껍질 하나를 둔다.",
                Culprit = 3, Victim = 2, Witness = 2, RedHerring = 5, Accomplice = 1,
                Hangouts = H("♥○○✕✕♥○○○♥○✕✕✕○○"), RefuseLine = "요리? 먹는 쪽이면 몰라도.",
            },
            ["P03"] = new Traits
            {
                Id = "P03", Hook = "매일 아침 사람 수를 세는 학생회장", Register = "단정한 해요체 → 담백한 반말",
                Tics = new[] { "정리하면,", "그러니까", "열여섯, 열일곱…" }, Laugh = "후훗", PetWords = new[] { "칸", "몫", "당번", "명단", "빈칸" }, NeverSays = new[] { "도와줘(요)", "대충" },
                TellGesture = "펜을 가지런히 맞춘다", TellPhrase = "괜찮아요, 괜찮아요",
                Lens = "사람 수와 일정", Haunts = new[] { "Dining", "Library", "Dining", "" }, Fixed = new[] { (7 * 60, "대현관 게시판에 명단을 붙인다") },
                Quirk = "당번 일 때문에 열쇠를 맡아 둔다", QuirkLooksLike = "열쇠 접근",
                Grief = "명단의 이름을 자를 대고 지운다.",
                Culprit = 2, Victim = 3, Witness = 4, RedHerring = 3, Accomplice = 1,
                Hangouts = H("○○♥○○♥✕✕○○♥✕○○✕✕"), RefuseLine = "당구는 잘 몰라요. 대신 산책은 어때요? 한 바퀴만요.",
            },
            ["P04"] = new Traits
            {
                Id = "P04", Hook = "무엇이든 제자리로 되돌리는 복원사", Register = "느린 하십시오체+해요체, '~씨'. 반말은 은결에게만",
                Tics = new[] { "그렇군요.", "괜찮으시다면" }, Laugh = "후", PetWords = new[] { "제자리", "결", "금", "흠", "붙이다", "허락" }, NeverSays = new[] { "반말(은결 외)", "당신", "죽이다" },
                TellGesture = "본 것보다 좁혀 말한 뒤 찻잔을 정확히 제자리에 둔다", TellPhrase = "들어갔을 때는…",
                Lens = "제자리에 없는 것", Haunts = new[] { "Gallery", "Workshop", "TeaRoom", "Corridor" }, Fixed = new[] { (23 * 60, "복도 액자를 하나씩 바로잡는다") },
                Quirk = "밤 11시에 복도를 걸으며 액자를 바로잡는다", QuirkLooksLike = "밤의 미행",
                Grief = "죽은 사람의 물건 하나를 복원하려 한다.",
                Culprit = 3, Victim = 2, Witness = 2, RedHerring = 5, Accomplice = 1,
                Hangouts = H("○♥♥✕○○✕♥○♥○✕♥○✕✕"), RefuseLine = "요리는 제 손이 할 일이 아닌 것 같습니다. 차라면 제가 내리지요.",
            },
            ["P05"] = new Traits
            {
                Id = "P05", Hook = "남의 말을 먼저 요약해 주는 청년대변인", Register = "공개: 하십시오체와 수사. 사적: 짧은 반말, 웃음 없음",
                Tics = new[] { "자, 자", "말씀하신 건 ~라는 거죠?", "요컨대" }, Laugh = "하하하", PetWords = new[] { "절차", "질서", "공익", "첫 문장", "정정" }, NeverSays = new[] { "제 잘못입니다", "돈(공개석상)" },
                TellGesture = "상대 말을 먼저 요약하고 재킷 단추를 만진다", TellPhrase = "말씀하신 건…",
                Lens = "누가 누구 옆에 섰는지", Haunts = new[] { "Dining", "Pool", "Lounge", "" },
                Quirk = "밤에 수영장에 떠 있다", QuirkLooksLike = "수영장과 물 관리실 출입",
                Grief = "두 번 고쳐 쓴 추도사를 읽는다. 가온이 초고를 알아본다.",
                Culprit = 3, Victim = 4, Witness = 1, RedHerring = 2, Accomplice = 2,
                Hangouts = H("♥✕○✕✕○○✕✕○○♥✕✕♥♥"), RefuseLine = "피아노는 연습한 적이 없어서요. 하하하. 대신 수영장에서 뵙죠.",
            },
            ["P06"] = new Traits
            {
                Id = "P06", Hook = "모든 호의를 장부에 적는 유통업 대표", Register = "사무적 하십시오체. 갈등하면 짧은 반말",
                Tics = new[] { "조건은?", "기한은?", "됐습니다.", "계산해 보면" }, Laugh = "흠", PetWords = new[] { "대여표", "반납", "미수금", "셈", "포장" }, NeverSays = new[] { "그냥(진심일 때만)" },
                TellGesture = "회중시계를 본다", TellPhrase = "개인 거래는 없었습니다",
                Lens = "대여표의 시각", Haunts = new[] { "Storage", "Archive", "Lounge", "" },
                Quirk = "무엇이든 빌리고 돌려준다", QuirkLooksLike = "흉기 접근",
                Grief = "죽은 사람의 물건을 목록으로 만들고 장부에 '반납 불가'라고 적는다.",
                Culprit = 2, Victim = 3, Witness = 4, RedHerring = 2, Accomplice = 4,
                Hangouts = H("♥✕○✕✕○♥✕✕♥○✕○○✕○"), RefuseLine = "피아노는 배운 적이 없습니다. 체스라면 한 판에 삼십 분, 어떻습니까?",
            },
            ["P07"] = new Traits
            {
                Id = "P07", Hook = "모두에게 별명을 붙이는 래퍼", Register = "큰 소리 반말. 도서실에서는 속삭임",
                Tics = new[] { "셰프/회장님/사장님/스타/베이스/형님", "레알?", "야!" }, Laugh = "크하하", PetWords = new[] { "의리", "무대", "박자", "단어" }, NeverSays = new[] { "밀고(가볍게)", "무섭다는 인정" },
                TellGesture = "말이 빨라지고 커지며 선글라스를 내린다", TellPhrase = null,
                Lens = "도서실과 바에 누가 있었는지", Haunts = new[] { "Dining", "Library", "Lounge", "" },
                Quirk = "밤마다 시끄럽게 바로 걸어간다", QuirkLooksLike = "밤중 외출",
                Grief = "수첩에 죽은 사람을 위한 단어를 적고 추모 자리에서 읽는다.",
                Culprit = 2, Victim = 3, Witness = 2, RedHerring = 3, Accomplice = 1,
                Hangouts = H("○○✕○✕♥♥○✕✕○○✕✕○♥"), RefuseLine = "차? 그거 할아버지 음료잖아. 맥주면 간다.",
            },
            ["P08"] = new Traits
            {
                Id = "P08", Hook = "모든 소리를 박자로 듣는 베이시스트", Register = "건조한 반말. 허세에는 욕",
                Tics = new[] { "아 뭐,", "됐고,", "헐" }, Laugh = "크", PetWords = new[] { "박자", "템포", "반음", "엇박", "핫팩" }, NeverSays = new[] { "단체 구호", "대박", "농담이야(사과로)" },
                TellGesture = "빠져 있는 이어폰을 만진다", TellPhrase = "이어폰 끼고 있었어",
                Lens = "소리와 횟수", Haunts = new[] { "GameRoom", "Corridor", "MusicRoom", "" },
                Quirk = "보일러실 근처에서 울림을 잰다", QuirkLooksLike = "소음 가리기",
                Grief = "음악실에서 긴 저음 하나를 친다. 아무도 들여보내지 않는다.",
                Culprit = 2, Victim = 2, Witness = 5, RedHerring = 2, Accomplice = 1,
                Hangouts = H("○♥○✕✕✕○♥✕✕♥✕✕○✕○"), RefuseLine = "책? 글자는 박자가 없어. 걷는 거면 가.",
            },
            ["P09"] = new Traits
            {
                Id = "P09", Hook = "누구의 말이든 토씨까지 되살리는 배우", Register = "부드러운 반말, 상대 이름부터",
                Tics = new[] { "{이름}아,", "있잖아", "세상에", "브라보" }, Laugh = "아하하", PetWords = new[] { "객석", "막", "대사", "기다림" }, NeverSays = new[] { "내가 할게", "촬영 얘기" },
                TellGesture = "이름을 먼저 부르지 않는다. 말이 너무 매끄러워지고 손을 숨긴다", TellPhrase = null,
                Lens = "정확한 말", Haunts = new[] { "Lounge", "Theater", "Dining", "" },
                Quirk = "남의 목소리를 흉내 낸다", QuirkLooksLike = "목소리 알리바이",
                Grief = "쉬지 않고 말하다가 문장 한가운데서 멈춘다.",
                Culprit = 2, Victim = 2, Witness = 3, RedHerring = 3, Accomplice = 2,
                Hangouts = H("○♥○✕✕♥✕✕✕✕○✕✕✕♥○"), RefuseLine = "민혁아, 카메라는 좀… 찍히는 거 말고 딴 거 하자.",
            },
            ["P10"] = new Traits
            {
                Id = "P10", Hook = "숟가락 열여덟 개를 놓는 요리사", Register = "동의를 구하는 해요체 → 느긋한 반말. 화나면 호칭이 빠진다",
                Tics = new[] { "우선 앉아요", "간 좀 봐 줄래요?", "어이쿠" }, Laugh = "허허", PetWords = new[] { "끼니", "숟가락", "몫", "간", "누룽지" }, NeverSays = new[] { "싫으면 말고", "음식 버리기" },
                TellGesture = "이미 깨끗한 손을 앞치마에 닦고 음식을 권한다", TellPhrase = "잠깐 나갔을 뿐이야",
                Lens = "끼니", Haunts = new[] { "Kitchen", "Greenhouse", "Kitchen", "" }, Fixed = new[] { (6 * 60 + 30, "부엌에 불을 켠다") },
                Quirk = "오후마다 칼을 간다", QuirkLooksLike = "흉기 준비",
                Grief = "숟가락을 하나 더 놓고, 스스로 치우지 못한다.",
                Culprit = 1, Victim = 3, Witness = 3, RedHerring = 2, Accomplice = 3,
                Hangouts = H("✕✕○♥♥✕✕✕✕✕○✕✕✕✕○"), RefuseLine = "카드는 잘 몰라요. 대신 간식은 제가 가져갈게요.",
            },
            ["P11"] = new Traits
            {
                Id = "P11", Hook = "뜯어보고 고치는 기계공학자", Register = "빠르고 짧은 반말. 무서우면 욕",
                Tics = new[] { "오!", "이거 봐", "됐다!" }, Laugh = "헤헤", PetWords = new[] { "규격", "공차", "헐겁다", "태엽", "기름칠" }, NeverSays = new[] { "완벽해", "대충" },
                TellGesture = "고글을 내린다", TellPhrase = "문제없어. 다 봤어.",
                Lens = "장치", Haunts = new[] { "Workshop", "PowerRoom", "Workshop", "" },
                Quirk = "매일 차단기와 배선을 본다", QuirkLooksLike = "함정 설치",
                Grief = "죽은 사람이 망가뜨린 걸 고치려다 못 고치고 조용히 욕한다.",
                Culprit = 2, Victim = 2, Witness = 3, RedHerring = 3, Accomplice = 1,
                Hangouts = H("♥✕✕✕✕✕○♥○○✕✕○♥✕✕"), RefuseLine = "꽃? 그런 건 관리 못 해. 뭐 고칠 거 없어?",
            },
            ["P12"] = new Traits
            {
                Id = "P12", Hook = "빨간 녹화등에 먼저 웃는 아이돌", Register = "밝은 해요체 → 부드러운 반말. 진심은 낮은 목소리와 '나는'",
                Tics = new[] { "꺄", "대박", "오늘도 반짝!", "에이~" }, Laugh = "에헤헤", PetWords = new[] { "카메라", "조명", "반짝", "스티커" }, NeverSays = new[] { "딱 잘라 '싫어요'", "면전의 험담" },
                TellGesture = "가장 밝게 웃고 눈이 방 모서리(카메라 자리)로 간다", TellPhrase = null,
                Lens = "누가 누구와 이야기했는지", Haunts = new[] { "Dining", "Theater", "Lounge", "" },
                Quirk = "모두와 따로따로 이야기한다", QuirkLooksLike = "쪽지 전달",
                Grief = "죽은 사람 방문의 별 스티커를 떼어 간직한다.",
                Culprit = 1, Victim = 2, Witness = 2, RedHerring = 3, Accomplice = 3,
                Hangouts = H("♥○○○♥✕✕✕○✕○✕○✕♥✕"), RefuseLine = "당구는 자신 없어요… 대신 온실 가요! 화분한테 인사하는 거 보여 줄게요.",
            },
            ["P13"] = new Traits
            {
                Id = "P13", Hook = "왼손으로 다시 배운 프로게이머", Register = "퉁명한 반말, 욕(존나, 씨)",
                Tics = new[] { "솔직히", "한 판 더", "GG" }, Laugh = "하", PetWords = new[] { "판", "리플레이", "각", "판정", "반응 속도" }, NeverSays = new[] { "도와줘", "불쌍", "확인" },
                TellGesture = "의수를 후드 주머니에 넣는다", TellPhrase = "괜찮다고",
                Lens = "속도와 방향", Haunts = new[] { "GameRoom", "Pool", "GameRoom", "" },
                Quirk = "복도에서 뛴다", QuirkLooksLike = "현장에서 도주",
                Grief = "'리셋이 안 되잖아.' 왼손에 쥐가 날 때까지 연습한다.",
                Culprit = 2, Victim = 2, Witness = 3, RedHerring = 3, Accomplice = 1,
                Hangouts = H("○✕✕✕✕✕♥♥✕○○♥✕✕✕○"), RefuseLine = "피아노? 손 두 개 쓰는 거잖아. 딴 거.",
            },
            ["P14"] = new Traits
            {
                Id = "P14", Hook = "모든 죽음 앞에서 흔들리지 않는 장의사", Register = "낮고 고른 -습니다. 가까워지면 -요",
                Tics = new[] { "그런가요.", "어머나", "(무표정한 말장난, 장면당 하나)" }, Laugh = "후후", PetWords = new[] { "매듭", "접다", "제자리", "백합" }, NeverSays = new[] { "힘내세요", "불쌍해라", "(장면당 둘 이상의) 조용/고요" },
                TellGesture = "질문 뒤의 침묵이 짧아진다", TellPhrase = null,
                Lens = "다룬 순서", Haunts = new[] { "Chapel", "Greenhouse", "TeaRoom", "" }, Fixed = new[] { (9 * 60 + 12, "세 번째 기둥 모퉁이를 도는 산책") },
                Quirk = "9시 12분 산책 길에 제자리에 없는 것을 치운다", QuirkLooksLike = "증거 인멸(그리고 가끔은 정말 그렇다)",
                Grief = "백합을 접고 추모 자리를 준비한다. 산책 길의 한 곳을 건너뛴다.",
                Culprit = 2, Victim = 2, Witness = 2, RedHerring = 3, Accomplice = 5,
                Hangouts = H("✕♥♥✕♥♥✕✕✕○♥✕♥✕✕✕"), RefuseLine = "카드는 사양하겠습니다. 차가 식기 전이라면, 다른 걸 하지요.",
            },
            ["P15"] = new Traits
            {
                Id = "P15", Hook = "확인은 세 번, 제목은 마지막인 기자", Register = "짧은 해요체, 확인 질문, 침묵",
                Tics = new[] { "잠깐만요.", "직접 보셨어요, 들으셨어요?", "적어 둘게요" }, Laugh = "흐", PetWords = new[] { "출처", "원문", "정정", "제목", "날씨" }, NeverSays = new[] { "확실해요(세 번 전엔)", "제보자 이름" },
                TellGesture = "수첩을 덮는다(평소엔 절대 덮지 않는다)", TellPhrase = "제보자는 없습니다",
                Lens = "출처", Haunts = new[] { "Archive", "Library", "Dining", "" },
                Quirk = "문 기록기를 읽는다", QuirkLooksLike = "자기 흔적 확인",
                Grief = "날짜와 날씨만 적는다. 제목은 없다.",
                Culprit = 2, Victim = 4, Witness = 4, RedHerring = 1, Accomplice = 1,
                Hangouts = H("○✕♥✕✕♥✕✕♥○♥✕✕✕○✕"), RefuseLine = "피아노는 잘 몰라요. 모르는 걸 아는 척은 안 해요. 산책이면 갈게요.",
            },
            ["P16"] = new Traits
            {
                Id = "P16", Hook = "선물마다 날짜를 적는 스타일리스트", Register = "나른한 반말, 짧은 대구",
                Tics = new[] { "별로.", "풋.", "뭐, 어쨌든." }, Laugh = "풋", PetWords = new[] { "핏", "선", "단추", "날짜", "원단" }, NeverSays = new[] { "고마워(그냥)", "헐", "고함" },
                TellGesture = "주머니 속 꽥 사장을 쥔다", TellPhrase = "기억 안 나",
                Lens = "옷", Haunts = new[] { "Wardrobe", "Gallery", "Lounge", "" },
                Quirk = "실과 줄자를 늘 가지고 다닌다", QuirkLooksLike = "밀실 트릭",
                Grief = "자기 선물 장부에서 죽은 사람이 마지막으로 준 것을 찾아본다.",
                Culprit = 2, Victim = 2, Witness = 3, RedHerring = 2, Accomplice = 1,
                Hangouts = H("○✕○✕○✕✕○♥✕○✕♥✕✕○"), RefuseLine = "피아노는 손을 너무 드러내. 별로.",
            },
            ["P17"] = new Traits
            {
                Id = "P17", Hook = "모두를 같은 놀이에 초대해야 안심하는 크리에이터", Register = "음정이 크게 오르내리는 반말, 겹말. 거절 뒤엔 작아진다",
                Tics = new[] { "짜잔!", "있지있지", "판정합니다." }, Laugh = "히히", PetWords = new[] { "문제", "초대장", "규칙", "벌칙", "모형" }, NeverSays = new[] { "자기 이름으로 '나'", "구독/좋아요" },
                TellGesture = "말의 리듬이 끊기고 심판 목소리로 바뀐다", TellPhrase = "판정합니다.",
                Lens = "사진과 영상", Haunts = new[] { "DollRoom", "Lounge", "GameRoom", "" },
                Quirk = "모임 시간을 자꾸 바꾼다", QuirkLooksLike = "알리바이 조작",
                Grief = "종이 저택 모형에 죽은 사람의 종이 인형을 올렸다가 다시 뗀다.",
                Culprit = 2, Victim = 3, Witness = 3, RedHerring = 2, Accomplice = 1,
                Hangouts = H("♥○✕○○✕○○♥✕○✕♥✕○✕"), RefuseLine = "책은… 지루해. 대신 퀴즈 내 줄까?",
            },
            ["P18"] = new Traits
            {
                Id = "P18", Hook = "끝낸 일만 짧게 보고하는 노동자", Register = "짧은 하십시오체 → 짧은 반말",
                Tics = new[] { "끝났습니다.", "(걸음 수·분)" }, Laugh = "허", PetWords = new[] { "몫", "공평", "걸음", "보리차", "사 년" }, NeverSays = new[] { "힘들어요" },
                TellGesture = "전자시계를 본다", TellPhrase = "힘들지 않습니다",
                Lens = "걸음과 무게", Haunts = new[] { "Storage", "Greenhouse", "Library", "Library" },
                Quirk = "걸음을 세며 복도를 걷는다", QuirkLooksLike = "동선 연습",
                Grief = "당번표에서 죽은 사람의 몫을 말없이 한다.",
                Culprit = 1, Victim = 2, Witness = 5, RedHerring = 2, Accomplice = 2,
                Hangouts = H("✕✕♥○♥○○✕✕○♥✕✕○✕✕"), RefuseLine = "술은 안 합니다. 보리차면 가겠습니다.",
            },
            ["NPC00"] = new Traits
            {
                Id = "NPC00", Hook = "어항 머리의 집사, 하급 신", Register = "하십시오체만, '~ 님'",
                Tics = new[] { "알려 드립니다.", "정해진 대로", "절차에 따라" }, Laugh = "", PetWords = new[] { "절차", "기록", "자리", "물결" }, NeverSays = new[] { "농담", "유죄에 대한 의견", "힌트", "반말" },
                TellGesture = "거짓말은 하지 않는다. 무거운 진실 앞에서 금붕어가 멈춘다", TellPhrase = null,
                Lens = "—", Haunts = new[] { "ButlerRoom", "ButlerRoom", "ButlerRoom", "ButlerRoom" }, Fixed = new[] { (9 * 60, "시계 한 개를 바로잡는 순회") },
                Grief = "자리 수를 말한다. 한 번의 공지 동안 금붕어가 멈춘다.",
            },
        };

        /// <summary>Tic → its one owner (bible §2 collision fixes). Anyone else must say the meaning in their own words.</summary>
        public static readonly Dictionary<string, string> TicOwner = new Dictionary<string, string>
        {
            ["잠깐만요"] = "P15", ["그러니까"] = "P03", ["있잖아"] = "P09", ["헐"] = "P08", ["브라보"] = "P09", ["흐응"] = "P02", ["맞혀 볼까"] = "P02",
            ["정리하면"] = "P03", ["그렇군요"] = "P04", ["괜찮으시다면"] = "P04", ["자, 자"] = "P05", ["요컨대"] = "P05", ["조건은?"] = "P06", ["기한은?"] = "P06",
            ["레알"] = "P07", ["아 뭐,"] = "P08", ["됐고,"] = "P08", ["세상에"] = "P09", ["간 좀 봐"] = "P10", ["어이쿠"] = "P10", ["됐다!"] = "P11",
            ["꺄"] = "P12", ["대박"] = "P12", ["반짝"] = "P12", ["솔직히"] = "P13", ["한 판 더"] = "P13", ["GG"] = "P13", ["어머나"] = "P14", ["그런가요"] = "P14",
            ["적어 둘게요"] = "P15", ["별로."] = "P16", ["풋."] = "P16", ["뭐, 어쨌든"] = "P16", ["짜잔"] = "P17", ["있지있지"] = "P17", ["판정합니다"] = "P17",
            ["끝났습니다"] = "P18", ["알려 드립니다"] = "NPC00",
        };

        /// <summary>Laugh → owner (§2): each laugh belongs to one voice.</summary>
        public static readonly Dictionary<string, string> LaughOwner = new Dictionary<string, string>
        {
            ["큭"] = "P02", ["후훗"] = "P03", ["하하하"] = "P05", ["흠"] = "P06", ["크하하"] = "P07", ["아하하"] = "P09", ["허허"] = "P10", ["헤헤"] = "P11",
            ["에헤헤"] = "P12", ["후후"] = "P14", ["흐"] = "P15", ["풋"] = "P16", ["히히"] = "P17", ["허"] = "P18",
        };

        public static Traits Get(string id) => id != null && All.TryGetValue(id, out var t) ? t : null;
        /// <summary>Hangout preference for an activity (Will when unknown).</summary>
        public static Fav HangoutFav(string id, string act) => Get(id)?.Hangouts != null && Get(id).Hangouts.TryGetValue(act, out var f) ? f : Fav.Will;
        /// <summary>The haunt RoomType name for a clock minute (0 morning &lt;12:00, 1 afternoon &lt;18:00, 2 evening &lt;22:00, 3 night), or null.</summary>
        public static string HauntAt(string id, int minuteOfDay)
        {
            var t = Get(id); if (t == null) return null;
            int b = minuteOfDay < 12 * 60 ? 0 : minuteOfDay < 18 * 60 ? 1 : minuteOfDay < 22 * 60 ? 2 : 3;
            var h = t.Haunts != null && b < t.Haunts.Length ? t.Haunts[b] : null;
            return string.IsNullOrEmpty(h) ? null : h;
        }
    }
}
