using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// The relationship web of CharacterBible §3 ("매듭": the mansion gathered people whose threads already cross).
    ///   · Ties  — the 36 ties (§3.2): who, type, level (A public · B one-sided · C hidden), history and how it shifts.
    ///   · Seeds — the starting directed values/tags/memories/facts (§3.4), applied at every loop start by one hook in
    ///             Relations.InitLoop, AFTER the eight original pairs. No RNG is drawn, so the rest of the loop is unchanged.
    ///   · Knots — the five hidden knots (§3.3). They start dormant (S.Flags "knot:Kx" = -1, nothing any resident knows);
    ///             RevealKnot adds the facts and pressures when a bond scene, a CH06 envelope or testimony uncovers one.
    /// All state lives in existing Rel values/Tags/Memory, Knowledge.Facts and S.Flags (save round-trip unchanged).
    /// Tag names are descriptive only (the kernel reacts to friend/family/lover alone).
    /// </summary>
    public static class CastWeb
    {
        public sealed class Tie
        {
            public int No; public string A, B; public bool Mutual; public string Type; public string Level; public bool Doc;
            public string History, Tension, Shift;
        }

        public sealed class Seed
        {
            public string From, To; public float? Like, Trust, Respect, Attach, Romance, Fear, Grudge, Jealous, Depend;
            public string Tag; public string Memory; public string Fact; public int Tie;
        }

        public sealed class Knot
        {
            public string Id, Title; public string[] Who; public string Hidden; public string RevealPath;
            /// <summary>What a reveal changes: (holder, fact) — "knows-my-secret:X" is what Relations.Pressure reads.</summary>
            public (string holder, string fact)[] Facts = new (string, string)[0];
            /// <summary>Relationship shifts on reveal: from, to, like, trust, fear, grudge, memory.</summary>
            public (string from, string to, float like, float trust, float fear, float grudge, string memory)[] Shifts = new (string, string, float, float, float, float, string)[0];
        }

        // ------------------------------------------------------------------ §3.2 ties (No, A, B, mutual, type, level, doc)
        static Tie T(int no, string a, string b, bool mutual, string type, string level, bool doc, string history, string tension, string shift)
            => new Tie { No = no, A = a, B = b, Mutual = mutual, Type = type, Level = level, Doc = doc, History = history, Tension = tension, Shift = shift };

        public static readonly Tie[] Ties =
        {
            T(1,  "P01", "P02", true,  "test",        "A", true,  "여기서 처음 만났다. 진우가 민혁을 시험하기로 한다.", "사탕 시험. '넌 예측이 안 돼.'", "믿음 → '이번엔 무서워서 보는 거야' / 배신 → 차가운 경멸"),
            T(2,  "P03", "P05", false, "grudge",      "B", true,  "이현의 서명한 거래로 서윤네 30년 인쇄소가 무너졌다. 이현은 서윤을 알아보지 못한다.", "명단에 그 이름을 제일 반듯하게 쓴다. 인사를 받으면 펜이 멈춘다.", "B4에서 이름을 말한다. 잊혔다는 걸 알면 분노·동기. 성장: 복수 대신 검증."),
            T(3,  "P05", "P06", true,  "deal",        "A", true,  "태겸이 급여를 맞추려고 이현의 비자금을 돌렸다.", "이현이 말할 때마다 태겸이 회중시계를 본다.", "CH05/06 폭로 → 결별. 장부가 서로의 동기가 된다."),
            T(4,  "P04", "P14", true,  "siblings",    "A", true,  "은결은 도시마다 도윤의 뒷정리를 했다('상자는 늘 셋').", "은결은 그를 눈앞에 두고 싶고, 도윤은 그만두길 바란다.", "플레이어가 한쪽과 가까워지면 다른 쪽이 질투. 폭로되면 은결의 중립이 깨진다."),
            T(5,  "P15", "P05", true,  "expose",      "A", true,  "가온이 세 번 확인한 이현 자금 기사.", "이현은 가온을 피하고, 가온은 '원문 그대로' 정정을 요구한다.", "식탁에서 공개 충돌. 서로에게 가장 강한 동기."),
            T(6,  "P10", "P16", false, "protect",     "A", true,  "채령의 가게가 가장 힘들던 한 달, 준서가 밥을 챙겼다.", "준서는 따라가고, 채령은 거절한다.", "거절을 존중하면 도시락 하나를 받는다. 무시하면 원한."),
            T(7,  "P13", "P11", true,  "hope",        "A", true,  "세나는 손의 진실을 해린에게만 말했다(팀에는 '인대'라고).", "'고칠 수 있냐고 자꾸 물어.'", "해린의 정직한 '못 고쳐'가 관계를 깊게 한다. 거짓 '문제없어'는 깨뜨린다."),
            T(8,  "P08", "P09", true,  "rival",       "A", true,  "재하는 목소리를 토씨까지 되살리고, 라온은 템포가 늘 틀렸다고 듣는다.", "템포를 두고 서로 긁는다.", "매듭 K2가 경쟁을 USB를 둘러싼 충돌로 바꾼다."),
            T(9,  "P14", "P02", false, "oldacq",      "B", false, "은결의 장례식장이 진우 친구의 장례를 치렀다. 울지 않고 사탕을 먹으며 조문객을 보던 소년을 은결만 기억한다.", "'그날도 사탕을 드시고 계셨죠.' 한마디에 진우가 굳는다.", "밝혀지면 그날의 그를 본 유일한 사람. 두려움 → 고백."),
            T(10, "P06", "P18", true,  "debt",        "B", false, "민서가 태겸 창고에서 한 달 야간 조로 일했다. 초과 근무 열한 시간이 지급되지 않았다.", "민서는 모르는 척, 태겸은 알아서 가렵다.", "열한 시간을 갚으면 태겸의 성장. 공개되면 창피. 끝내 외면하면 민서가 끊어 낸다."),
            T(11, "P16", "P12", false, "blame",       "A", false, "수아 그룹의 활동 중지로 채령의 가장 큰 계약이 날아갔다(진짜 원인은 채령의 과주문).", "차가운 가시, 죄책감에 과하게 다정한 수아.", "'내 탓이야'로 끝난다. 탓하면 식탁에서 번진다."),
            T(12, "P07", "P12", false, "crush",       "A", false, "수아는 모두에게 다정하고, 시온은 그걸 자기 몫으로 읽는다.", "시온이 자리를 맡아 두고, 수아는 모두에게 똑같이 고마워한다.", "수아의 분명하고 다정한 '아니야'가 성장이자 시온의 실연. 옆에 앉는 사람에게 질투."),
            T(13, "P17", "P12", false, "fan",         "B", false, "예담은 한때 수아의 영상을 동의 없이 잘라 올리는 팬 계정을 운영했다.", "예담이 찍으면 수아가 움찔한다.", "계정을 알아보면 상처. 예담의 사과가 예담의 성장."),
            T(14, "P02", "P03", true,  "feud",        "A", false, "여기서 처음 만났다. 진우는 '뼛속 깊은 선인'을 싫어하고, 서윤은 읽히기를 거부한다.", "진우는 명단을 '착한 척 표'라 부르고, 서윤은 그를 명단에서 뺀다.", "중재 → 마지못한 존중('너 같은 사람이 제일 안 무너져'). 악화 → 사망 뒤 짝 제도에서 제외."),
            T(15, "P04", "P11", true,  "workshop",    "A", false, "공방을 같이 쓴다. 해린은 속죄하려 고치고, 도윤은 통제하려 복원한다.", "도윤의 공구는 늘 '너무' 깨끗하다.", "해린이 가장 먼저 의심하거나, 플레이어가 보증하면 조용한 보호자."),
            T(16, "P04", "P13", false, "fascination", "A", false, "도윤은 의수의 선에 감탄하고, 세나는 동정하지 않는 그가 처음엔 좋다.", "세나는 대상화를 존중으로 착각한다.", "'넌 손만 보잖아' → 혐오. 플레이어가 알아챌 수 있는 붉은 깃발."),
            T(17, "P13", "P18", false, "accused",     "A", true,  "첫날 세나가 창고 열쇠를 두고 민서를 몬다. 태겸의 대여표에는 시온이 적혀 있다.", "어색함. 민서는 아무 말도 하지 않는다.", "'민서야, 내가 틀렸어…'가 세나의 성장. 또 몰면 민서가 끊어 낸다."),
            T(18, "P10", "P02", true,  "feed",        "A", false, "—", "준서는 사탕 대신 죽을 주고, 도발에 넘어가지 않는다.", "진우는 오후마다 부엌에서 존다. 절대 인정하지 않는다."),
            T(19, "P10", "P18", true,  "colleague",   "A", false, "—", "부엌과 운반의 짝. 보리차 대 밥.", "따뜻하고 안정적. 한쪽이 죽으면 슬픔의 닻."),
            T(20, "P03", "P17", true,  "oddcouple",   "A", false, "—", "예담은 당번표를 게임으로 만들고, 서윤은 싫은 척한다.", "동맹. 서윤을 쉬게 하는 사람이 예담이다."),
            T(21, "P03", "P06", true,  "ally",        "A", false, "서윤은 정리하고 태겸은 센다.", "서로 존중한다.", "그가 이현의 돈을 돌린 걸 알면: '그 장부에 우리 가게 이름도 있었어요?'"),
            T(22, "P15", "P18", true,  "map",         "A", true,  "민서가 걸음을 세고, 가온이 출처를 단다.", "조용한 온기. 그의 존경은 말해지지 않는다.", "안정적인 동료. 민서가 미적분 책 얘기를 하는 상대."),
            T(23, "P05", "P16", false, "debt",        "A", false, "채령이 이현의 캠프 스타일링을 맡고 청록 넥타이를 골랐다. 스캔들 뒤 대금은 끝내 받지 못했다.", "채령의 가게 핑계에 두 번째가 붙는다.", "대금 대신 '거래'를 제안하면 채령이 그를 경멸한다."),
            T(24, "P12", "P05", false, "suspect",     "B", true,  "'위에서 온' 전화 한 통이 수아의 활동을 얼렸다. 회색 양복이 다가오면 굳는다.", "이현은 수아에게 다정하고, 그래서 더 나쁘다.", "끝내 그로 확정되지 않는다. 태겸의 두 번째 장부가 진짜 전화한 사람을 가리킬 수도 있다."),
            T(25, "P11", "P06", true,  "tools",       "A", true,  "—", "'지금 쓰잖아. 손부터 빼.' / '예약 시간은 지났습니다.'", "매듭 K4."),
            T(26, "P11", "P08", true,  "warmth",      "A", false, "해린이 라온의 이어폰 선을 고치고, 라온은 핫팩을 두고 간다.", "없음.", "슬픔의 닻."),
            T(27, "P08", "P07", true,  "banter",      "A", true,  "도서실 고함 사건.", "음악하는 사람끼리의 놀림.", "매듭 K2."),
            T(28, "P09", "P17", true,  "filming",     "A", false, "재하는 찍히는 게 싫고, 예담은 몰래 그의 데뷔작 팬이다.", "'끄자. 지금 이 얼굴은 무대 밖이야.'", "재하 앞에서 영상을 지우면 신뢰."),
            T(29, "P09", "P13", true,  "tempo",       "A", true,  "—", "재하는 세나가 남의 말을 자르지 못하게 하고, 세나는 그를 느리다고 한다.", "서로를 고쳐 주며 더 나은 배심원이 된다."),
            T(30, "P09", "P12", true,  "stage",       "A", false, "3년 전 웹드라마를 같이 찍었다.", "서로의 가짜 웃음을 알아보는 유일한 둘.", "정직한 거울. 몰래 찍히는 걸 둘 다 싫어한다."),
            T(31, "P14", "P17", true,  "deathgame",   "A", false, "—", "예담이 죽음을 퀴즈로 만들고, 은결이 멈춰 세운다.", "예담의 성장: 은결과 백합을 접는다."),
            T(32, "P14", "P10", true,  "caretakers",  "A", false, "—", "추모 식사: 준서가 짓고 은결이 차린다.", "둘의 추모 자리가 사건 뒤의 행사가 된다."),
            T(33, "P07", "P10", true,  "party",       "A", false, "—", "준서는 첫날부터 그를 '래퍼'가 아니라 '시온'이라 부른다.", "슬픔의 닻."),
            T(34, "P07", "P18", false, "hyung",       "A", false, "—", "민서는 시온의 가사를 웃지 않고 끝까지 듣는 유일한 사람이다.", "—"),
            T(35, "P15", "P06", false, "holding",     "B", false, "가온의 기사가 그 장부를 건드렸다. 태겸이 장부를 갖고 있다는 걸 알지만, 세 번 확인하지 못해 아직 쓰지 않았다.", "태겸이 그녀의 수첩을 지켜본다.", "터뜨리면 그의 동기. 동의를 구하면 그의 신뢰."),
            T(36, "P13", "P01", false, "outsider",    "A", false, "—", "'계약 없는 놈이 제일 수상하지.'", "증거로 된 변호가 그녀를 설득한다. 그에 대해 틀렸다는 것이 사과의 계기가 된다."),
        };

        // ------------------------------------------------------------------ §3.4 seeding values (set, not added — as the eight original pairs do)
        static Seed S_(int tie, string from, string to, float? like = null, float? trust = null, float? respect = null, float? attach = null, float? romance = null, float? fear = null, float? grudge = null, float? jealous = null, float? depend = null, string tag = null, string memory = null, string fact = null)
            => new Seed { Tie = tie, From = from, To = to, Like = like, Trust = trust, Respect = respect, Attach = attach, Romance = romance, Fear = fear, Grudge = grudge, Jealous = jealous, Depend = depend, Tag = tag, Memory = memory, Fact = fact };

        public static readonly Seed[] Seeds =
        {
            S_(9,  "P14", "P02", respect: 0.10f, tag: "oldacq", memory: "장례식장에서 울지 않던 학생. 사탕을 먹고 있었다", fact: "oldacq:P02:funeral"),
            S_(10, "P06", "P18", respect: 0.20f, tag: "owes", memory: "야간 조에서 불평 한 번 안 하던 사람", fact: "debt:P18:overtime"),
            S_(10, "P18", "P06", grudge: 0.15f, trust: -0.10f, tag: "debt", memory: "초과 근무 열한 시간. 받지 못했다", fact: "knows:P06:employer"),
            S_(11, "P16", "P12", grudge: 0.25f, like: -0.05f, tag: "blame", memory: "저 그룹 활동 중지 때문에 우리 가게 계약이 날아갔다"),
            S_(11, "P12", "P16", like: 0.15f, fear: 0.05f, tag: "guilt", memory: "우리 때문에 문 닫은 가게가 있다고 들었다"),
            S_(12, "P07", "P12", like: 0.30f, romance: 0.35f, tag: "crush"),
            S_(13, "P17", "P12", like: 0.30f, tag: "fan", memory: "편집하던 직캠 속 그 사람", fact: "secret:P17:fanedit"),
            S_(14, "P02", "P03", like: -0.05f, trust: -0.10f, tag: "feud", memory: "착한 척하는 표"),
            S_(14, "P03", "P02", like: -0.10f, trust: -0.10f, tag: "feud", memory: "사람을 떠보는 사람"),
            S_(15, "P04", "P11", respect: 0.20f, tag: "workshop", memory: "같은 작업대, 다른 손"),
            S_(15, "P11", "P04", respect: 0.15f, fear: 0.05f, tag: "workshop", memory: "같은 작업대, 다른 손"),
            S_(16, "P04", "P13", like: 0.10f, tag: "fascination", memory: "의수의 선이 곱다"),
            S_(16, "P13", "P04", like: 0.15f, memory: "날 불쌍하게 안 보는 사람"),
            S_(18, "P10", "P02", like: 0.10f, tag: "feed"),
            S_(18, "P02", "P10", like: -0.05f, respect: 0.10f),
            S_(19, "P10", "P18", like: 0.20f, tag: "colleague"),
            S_(19, "P18", "P10", like: 0.20f, tag: "colleague"),
            S_(33, "P07", "P10", like: 0.20f, tag: "party"),
            S_(33, "P10", "P07", like: 0.20f, tag: "party"),
            S_(20, "P03", "P17", like: 0.15f, tag: "oddcouple"),
            S_(20, "P17", "P03", like: 0.25f, tag: "oddcouple"),
            S_(22, "P15", "P18", like: 0.20f, tag: "map"),
            S_(22, "P18", "P15", like: 0.20f, tag: "map"),
            S_(32, "P14", "P10", like: 0.15f, tag: "caretakers"),
            S_(32, "P10", "P14", like: 0.15f, tag: "caretakers"),
            S_(26, "P11", "P08", like: 0.20f, tag: "warmth"),
            S_(26, "P08", "P11", like: 0.20f, tag: "warmth"),
            S_(30, "P09", "P12", like: 0.20f, tag: "stage"),
            S_(30, "P12", "P09", like: 0.20f, tag: "stage"),
            S_(21, "P03", "P06", trust: 0.15f, respect: 0.15f, tag: "ally"),
            S_(21, "P06", "P03", trust: 0.15f, respect: 0.15f, tag: "ally"),
            S_(35, "P15", "P06", tag: "holding", fact: "secret:P06:불법자금"),
            S_(23, "P16", "P05", grudge: 0.20f, tag: "debt", memory: "선거 캠프 스타일링 대금, 아직 못 받았다"),
            S_(23, "P05", "P16", tag: "owes", memory: "그 넥타이는 그 사람이 골랐다"),
            S_(24, "P12", "P05", fear: 0.10f, tag: "suspect", memory: "회색 양복"),
            S_(25, "P11", "P06", grudge: 0.05f, tag: "tools"),
            S_(25, "P06", "P11", grudge: 0.05f, tag: "tools"),
            S_(28, "P09", "P17", like: -0.05f, tag: "filming"),
            S_(28, "P17", "P09", like: 0.25f, tag: "fan"),
            S_(34, "P07", "P18", respect: 0.20f, tag: "hyung"),
            S_(7,  "P11", "P13", fact: "secret:P13:hand"),
            S_(36, "P13", "P01", trust: -0.10f, tag: "outsider"),
            S_(0,  "P03", "P01", like: 0.05f),
            // ties the bible table leaves without numbers: a light public texture only
            S_(27, "P08", "P07", like: 0.08f, tag: "banter"),
            S_(27, "P07", "P08", like: 0.12f, tag: "banter"),
            S_(29, "P09", "P13", respect: 0.08f, tag: "tempo"),
            S_(29, "P13", "P09", respect: 0.05f, tag: "tempo"),
        };

        // ------------------------------------------------------------------ §3.3 hidden knots (dormant at loop start)
        public static readonly Knot[] Knots =
        {
            new Knot
            {
                Id = "K1", Title = "작년 겨울", Who = new[] { "P10", "P13" },
                Hidden = "준서가 고열로 배달 승합차를 몰던 밤, 택시와 부딪혀 젊은 여자가 오른손을 잃었다. 그 사람이 세나다. 준서는 '손을 잃은 젊은 여자분', 세나는 '배달 승합차'만 안다.",
                RevealPath = "준서 B4('그날 밤 배달')와 세나 B4('승합차')를 루프를 건너 듣거나, 세나에게 온 CH06 봉투.",
                Facts = new[] { ("P13", "knot:K1"), ("P10", "knot:K1"), ("P10", "knows-my-secret:P13") },
                Shifts = new[] { ("P13", "P10", -0.20f, -0.25f, 0f, 0.30f, "그날 밤 그 승합차를 몰던 사람"), ("P10", "P13", 0f, 0f, 0.15f, 0f, "내가 그 사람의 손을…") },
            },
            new Knot
            {
                Id = "K2", Title = "USB", Who = new[] { "P08", "P07", "P09" },
                Hidden = "라온의 밴드 리더 '형'은 시온이 밀고해 수감된 시온의 큰형이다. 재하의 떠난 연인이 그 데모의 가이드 보컬을 불렀다. 라온의 USB에는 그 목소리가 있다.",
                RevealPath = "재하가 무대에 오른 적 없는 곡을 흥얼거림 → 라온 '그 곡 어디서 들었어?' / 시온 수첩의 '옥상 관객 다섯 — 지금은 전부 벽 안에'.",
                Facts = new[] { ("P08", "knot:K2"), ("P07", "knot:K2"), ("P09", "knot:K2"), ("P07", "knows-my-secret:P08"), ("P08", "knows-my-secret:P09") },
                Shifts = new[] { ("P08", "P07", -0.20f, -0.20f, 0f, 0.25f, "형을 벽 안에 넣은 사람"), ("P09", "P08", 0f, 0f, 0f, 0.10f, "그 목소리를 주머니에 넣고 다닌다") },
            },
            new Knot
            {
                Id = "K3", Title = "한 사장님", Who = new[] { "P15", "P03" },
                Hidden = "가온의 정확한 이현 기사가 인쇄소를 거래처로 적었다. 가게가 알았는지는 확인하지 않았다. 가게는 무너졌다. 가온의 제목 없는 일기의 '한 사장님'이 서윤의 아버지다.",
                RevealPath = "가온 B4 + 서윤 B2('잉크 냄새').",
                Facts = new[] { ("P15", "knot:K3"), ("P03", "knot:K3"), ("P15", "knows-my-secret:P03") },
                Shifts = new[] { ("P03", "P15", -0.15f, -0.20f, 0f, 0.20f, "맞는 기사였다. 그래서 더 할 말이 없다") },
            },
            new Knot
            {
                Id = "K4", Title = "센서 로트", Who = new[] { "P11", "P06" },
                Hidden = "해린이 알면서 넘긴 승강기 센서는 태겸의 창고를 거친 병행 수입 로트였다.",
                RevealPath = "해린은 부품 탓을 하고 싶어진다(결점). 진실: 알고 있었다. 태겸: '그 계산은 안 끝납니다.'",
                Facts = new[] { ("P11", "knot:K4"), ("P06", "knot:K4"), ("P11", "knows-my-secret:P06") },
                Shifts = new[] { ("P11", "P06", -0.10f, -0.10f, 0f, 0.12f, "그 부품이 거쳐 간 창고"), ("P06", "P11", 0f, -0.10f, 0f, 0f, "알면서 넘긴 사람") },
            },
            new Knot
            {
                Id = "K5", Title = "장례식", Who = new[] { "P14", "P02" },
                Hidden = "(B단계) 은결만 기억한다. 진우는 그날 자기를 본 사람이 있다는 걸 모른다.",
                RevealPath = "은결의 한마디 '그날도 사탕을 드시고 계셨죠.' → 진우 B4의 메아리.",
                Facts = new[] { ("P02", "knot:K5"), ("P14", "knot:K5"), ("P02", "knows-my-secret:P14") },
                Shifts = new[] { ("P02", "P14", 0f, 0f, 0.15f, 0f, "그날의 나를 본 사람") },
            },
        };

        // ------------------------------------------------------------------ API
        /// <summary>Loop start (Relations.InitLoop, after the eight original pairs): seed every tie's starting values and put the
        /// knots to sleep. Deterministic, no RNG.</summary>
        public static void Apply(GameState S)
        {
            foreach (var s in Seeds)
            {
                if (S.A(s.From) == null && Cast.Get(s.From) == null) continue;
                var r = S.R(s.From, s.To);
                if (s.Like.HasValue) r.Like = s.Like.Value;
                if (s.Trust.HasValue) r.Trust = s.Trust.Value;
                if (s.Respect.HasValue) r.Respect = s.Respect.Value;
                if (s.Attach.HasValue) r.Attach = s.Attach.Value;
                if (s.Romance.HasValue) r.Romance = s.Romance.Value;
                if (s.Fear.HasValue) r.Fear = s.Fear.Value;
                if (s.Grudge.HasValue) r.Grudge = s.Grudge.Value;
                if (s.Jealous.HasValue) r.Jealous = s.Jealous.Value;
                if (s.Depend.HasValue) r.Depend = s.Depend.Value;
                if (s.Tag != null) r.Tags.Add(s.Tag);
                if (s.Memory != null && !r.Memory.Contains(s.Memory)) r.Memory.Add(s.Memory);
                if (s.Fact != null) S.K(s.From).Facts.Add(s.Fact);
            }
            foreach (var k in Knots) S.Flags["knot:" + k.Id] = -1;
        }

        public static Knot KnotOf(string id) => Knots.FirstOrDefault(k => k.Id == id);
        public static bool KnotRevealed(GameState S, string id) => S.Flags.TryGetValue("knot:" + id, out var v) && v >= 0;

        /// <summary>Uncover a knot this loop (bond IV, CH06 envelope, cross-testimony): facts to its members, the pressures of §3.3,
        /// and a ledger line. Returns false when unknown or already revealed. Callers: none yet (bonds/rules/envelopes hook later).</summary>
        public static bool RevealKnot(GameState S, string id, string by = null)
        {
            var k = KnotOf(id); if (k == null || KnotRevealed(S, id)) return false;
            S.Flags["knot:" + id] = S.Clock;
            foreach (var (holder, fact) in k.Facts) S.K(holder).Facts.Add(fact);
            foreach (var sh in k.Shifts) Relations.Change(S, sh.from, sh.to, like: sh.like, trust: sh.trust, fear: sh.fear, grudge: sh.grudge, memory: sh.memory, tag: "knot:" + id);
            S.Log("KnotRevealed", by, data: id + "|" + k.Title, secret: true);
            return true;
        }

        /// <summary>Every tie a resident is part of (either end).</summary>
        public static IEnumerable<Tie> TiesOf(string id) => Ties.Where(t => t.A == id || t.B == id);
        /// <summary>The tie between two residents in either direction, or null.</summary>
        public static Tie TieBetween(string a, string b) => Ties.FirstOrDefault(t => (t.A == a && t.B == b) || (t.A == b && t.B == a));
        /// <summary>Residents this one has a tie with (the natural targets of pair lines, key@Pxx).</summary>
        public static IEnumerable<string> Partners(string id) => TiesOf(id).Select(t => t.A == id ? t.B : t.A).Distinct();
    }
}
