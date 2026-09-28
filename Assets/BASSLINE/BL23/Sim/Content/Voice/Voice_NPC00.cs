using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    public static partial class LineBank
    {
        // NPC00 유스티 — voice pack (CharacterBible §2 / §4 NPC00, DailyLifeDesign §3.1·§8.2).
        // 하십시오체만, 누구든 성+이름+" 님". 절차가 몸을 얻은 존재: 보는 것은 전부, 말하는 것은 절차가 요구하는 것만.
        // 가진 말버릇: "알려 드립니다." · "정해진 대로" · "절차에 따라". 즐겨 쓰는 말: 절차, 기록, 자리, 물결.
        // 농담·놀림·유죄 의견·힌트·반말·거짓말은 없다. 감정은 "…"(물결이 멎는 순간)과 금붕어로만 새어 나온다.
        // 공지 하나에 감각 묘사(물·유리·금붕어·벽의 온기)는 많아야 하나. 두려움은 숫자가 나른다: 놓은 자리 수, 며칠째인지, 잠그는 방 수.
        // 숫자는 전부 '공개된' 것만 센다: 아직 아무도 찾지 못한 시신에게도 의자는 놓인다(YustiVoice.Seats) — 공지가 사건을 흘리지 않도록.
        // 공지(y_x)는 LifeAI.Announce → YustiVoice.Announcement가 이 팩의 "yv_x" 풀에서 말한다(없으면 y_x 그대로). 옛 문구(Lines_NPC00)는
        // 훅이 빠졌을 때의 대체 문구로만 남는다 — 호텔 안내원 같던 아침·식사·밤 공지를 이렇게 갈아 끼운다.
        // 루프: 주민은 모른다(CharacterBible §5.1 #21). 2루프부터 민혁 이름 앞에서만 한 박자 멈추고(~loop2), 3루프부터 방 안 대화에서만
        // 기억이 조금 샌다(~loop3: "늘", "이번에도", "잊지 않습니다"). 공개 공지에는 루프 변주를 두지 않는다.
        // 남의 말버릇(그렇군요·괜찮으시다면·끝났습니다.·적어 둘게요·그런가요·세상에·솔직히…)은 쓰지 않는다. 욕설·음담: 쓰지 않는다 — 대신
        // 남의 욕설·음담을 들었을 때의 반응(react_swear / react_dirty)만 있다. 전부 '기록'으로 받는다.
        static void Init_Voice_NPC00()
        {
            const string A = "NPC00";
            Announcements(A);
            TrialProcedure(A);
            RoomTalk(A);
        }

        // ================================================================== 1. 공지 (LifeAI.Announce → YustiVoice)
        // 쓸 수 있는 숫자 슬롯(YustiVoice.Numbers): {seats}/{seatsc} 놓은 자리 수(열일곱 / 열한 개의 '열한'), {fewer}/{fewerc} 최근 하루 사이에
        // 치운 의자 수(하나 / 한 벌의 '한' — afterdeath 풀에서만), {gonec} 이번에 치운 의자 모두, {dayth} 이 저택에서 며칠째(첫 / 두 번째…),
        // {quiet} 조용한 날 수(이틀째…), {hungerth} 저택이 삼킨 방 순서(첫 / 두 번째…), {locksc} 밤에 잠그는(아침에는 연) 방 수. 모두 공개 기록 기준이다.
        static void Announcements(string A)
        {
            // ───────── 아침 (07:00) ─────────
            VP(A, "yv_morning",
                "알려 드립니다. 아침입니다. 밤사이 잠갔던 문을 모두 열었습니다.",
                "아침입니다. 이 저택에서 맞으시는 {dayth} 아침입니다.|잠갔던 방 {locksc} 곳을 열었습니다. 식사는 여덟 시입니다.",
                "알려 드립니다. 아침입니다.|식사는 여덟 시와 저녁 여섯 시 반, 두 번입니다. 점심은 각자 드십시오.",
                "아침입니다. 복도의 등을 낮의 밝기로 올렸습니다.|밤사이 잠갔던 문은 정해진 대로 모두 열었습니다.",
                "알려 드립니다. 아침입니다. 커튼을 모두 걷었습니다.|문은 열려 있습니다. 여덟 시에 아침 식탁을 차리겠습니다.",
                "좋은 아침입니다. 밤사이 잠갔던 문을 열었습니다.|정해진 대로, 오늘의 절차를 시작하겠습니다.",
                "아침입니다. 문을 열었습니다.|오늘도 정해진 시각에 종이 울립니다. 여덟 시, 저녁 여섯 시 반, 밤 열 시입니다.",
                "알려 드립니다. 아침입니다.|잠갔던 방 {locksc} 곳의 문을 열었습니다. 이제 들어가실 수 있습니다.",
                "아침입니다. 밤이 지났습니다. 문을 모두 열었습니다.",
                "알려 드립니다. {dayth} 아침입니다. 문은 정해진 대로 열었습니다.");
            // 죽음이 없는 날이 하루를 넘으면(quiet) — 저택이 굶주리기 전의 압박
            CtxP(A, "yv_morning", "quiet",
                "좋은 아침입니다. 조용한 날이 {quiet}입니다.|…어항의 물이 조금 식었습니다.",
                "알려 드립니다. 아침입니다. 문을 모두 열었습니다.|죽음을 알리는 종이 {quiet} 울리지 않았습니다.",
                "아침입니다. 식당의 의자는 어제와 같은 수입니다.|…저택은 조용한 날을 오래 견디지 않습니다.",
                "아침입니다. 조용한 날이 {quiet} 이어지고 있습니다.|정해진 대로 문을 열었습니다. 벽이 조금 따뜻합니다.",
                "알려 드립니다. 오늘로 조용한 날이 {quiet}입니다.|저는 그 날수를 세고 있습니다. 문은 모두 열었습니다.");
            CtxP(A, "yv_morning", "hunger",
                "아침입니다. 문을 열었습니다.|열리지 않는 방이 있더라도, 제가 잠근 것이 아닙니다.",
                "알려 드립니다. 아침입니다. 오늘도 식탁의 음식은 조금 적습니다.|저택이 아직 배고파하고 있습니다.",
                "아침입니다. 밤사이 벽이 조금 따뜻해졌습니다.|…저택이 굶주리고 있습니다. 문은 열 수 있는 만큼 열었습니다.",
                "좋은 아침입니다. 열 수 있는 문은 모두 열었습니다.|열 수 없는 문은, 저택의 것입니다.");
            CtxP(A, "yv_morning", "afterdeath",
                "아침입니다. 정해진 대로 문을 모두 열었습니다.|…문을 열고 나오실 분이 줄었습니다.",
                "알려 드립니다. 아침입니다.|오늘 아침 식탁에 놓을 자리는 {seats}입니다.",
                "아침입니다. 문을 모두 열었습니다.|…금붕어가 아직 헤엄치지 않습니다. 신경 쓰지 마십시오.",
                "아침입니다. 치운 의자는 벽 쪽에 세워 두었습니다.|정해진 대로입니다. 문은 모두 열었습니다.");
            CtxP(A, "yv_morning", "aftertrial",
                "아침입니다. 심판을 치른 뒤의 첫 아침입니다.|문을 모두 열었습니다. 식당의 자리는 {seats}입니다.",
                "알려 드립니다. 아침입니다.|심판장의 촛불은 새것으로 갈아 두었습니다. 정해진 대로입니다.",
                "아침입니다. 지난 챕터의 특별 규칙은 모두 효력을 잃었습니다.|문을 열었습니다. 여덟 시에 식탁을 차립니다.",
                "아침입니다. 항아리의 돌은 모두 꺼내 씻어 두었습니다.|…문을 모두 열었습니다.",
                "알려 드립니다. 아침입니다. 지금까지 치운 의자는 {gonec} 개입니다.|문은 모두 열었습니다.");
            // 챕터 특별 규칙이 살아 있는 동안의 아침 — 이미 공표한 규칙을 되짚기만 한다(해법·암시 없음)
            CtxP(A, "yv_morning", "rule_CH02",
                "아침입니다. 두 날개 사이의 통로는 오늘도 닫혀 있습니다.|조 편성은 게시판에 있습니다. 식당은 함께 쓰십시오.",
                "알려 드립니다. ‘양익 생활’은 오늘도 이어집니다.|맡으신 날개에서 지내십시오. 구조와 신고는 언제든 오가셔도 됩니다.");
            CtxP(A, "yv_morning", "rule_CH06",
                "아침입니다. ‘과거의 봉투’는 아직 유효합니다.|봉투를 받으신 분은, 열지 않으셔도 됩니다.",
                "알려 드립니다. 아침입니다. 봉투는 더 배달하지 않습니다.|받으신 봉투를 어떻게 할지는 받으신 분이 정하십시오.");
            CtxP(A, "yv_morning", "rule_CH07",
                "아침입니다. 공용 장비 두 가지는 오늘도 대여표에 적고 빌리십시오.|한 번에 20분입니다. 대여표는 진행대에 있습니다.");
            CtxP(A, "yv_morning", "rule_CH09",
                "알려 드립니다. 아침입니다.|기한을 통지받으신 분께 다시 알려 드립니다. 기한은 이번 정산입니다.");
            CtxP(A, "yv_morning", "rule_CH13",
                "아침입니다. ‘교대 열쇠’의 교대는 오후 두 시와 밤 열 시입니다.|명단은 게시판에 있습니다.");
            CtxP(A, "yv_morning", "rule_CH21",
                "아침입니다. 이번 챕터의 한도는 세 분입니다.|…한도는 명령이 아닙니다.",
                "알려 드립니다. 아침입니다. ‘세 번째 빈자리’는 아직 유효합니다.|의자는 오늘도 전부 놓겠습니다.");
            CtxP(A, "yv_morning", "rule_CH22",
                "아침입니다. 밤사이에도 소리는 멎지 않았습니다.|들리는 곳과 나는 곳이 다를 수 있습니다.");
            CtxP(A, "yv_morning", "rule_CH23",
                "아침입니다. 조명은 낮춘 그대로입니다.|손전등의 불빛은 다른 분들 눈에도 보입니다.",
                "알려 드립니다. 아침이지만 저택은 어둡습니다. 규칙대로입니다.|문은 모두 열었습니다.");

            // ───────── 아침 식사 (08:00) — 자리 수가 이 공지의 전부다 ─────────
            VP(A, "yv_meal_breakfast",
                "아침을 차렸습니다. 자리는 {seats}입니다. 따뜻할 때 드십시오.",
                "알려 드립니다. 식당에 아침을 차렸습니다. 수저는 {seatsc} 벌입니다.",
                "아침 식사입니다. 의자 {seatsc} 개를 놓았습니다.|늦게 오시는 분의 수프는 식지 않게 두겠습니다.",
                "식당에 아침을 차렸습니다. 빵은 {seatsc} 개, 잔도 {seatsc} 개입니다.",
                "알려 드립니다. 여덟 시입니다. 아침 식사가 준비되었습니다.|자리는 {seats}입니다.",
                "아침 식사입니다. 수프는 정해진 온도로 데웠습니다. 자리는 {seats}입니다.",
                "식당에 아침을 차렸습니다. {seatsc} 자리입니다.|정해진 대로, 한 자리도 빼지 않았습니다.",
                "아침 식사가 준비되었습니다. 식당으로 오십시오. 자리는 {seats}입니다.");
            CtxP(A, "yv_meal_breakfast", "afterdeath",
                "아침을 차렸습니다. 자리는 {seats}입니다.|치운 의자는 벽 쪽에 두었습니다.",
                "알려 드립니다. 아침 식사입니다. 자리는 {seats}입니다.|…정해진 대로, 전부 놓았습니다.",
                "아침을 차렸습니다. 의자를 {seatsc} 개 놓았습니다.|…세는 데 조금 오래 걸렸습니다.",
                "식당에 아침을 차렸습니다. 수저를 {fewerc} 벌 거두었습니다.|드실 수 있는 만큼만 드십시오.");
            CtxP(A, "yv_meal_breakfast", "hunger",
                "아침을 차렸습니다. 자리는 {seats}입니다.|한 분 한 분의 몫이 조금 줄었습니다. 제가 줄인 것이 아닙니다.",
                "알려 드립니다. 아침 식사입니다. 오늘은 빵이 모자랍니다.|저택이 굶주리는 동안에는 나누어 드십시오.",
                "식당에 아침을 차렸습니다. 접시는 {seatsc} 개입니다.|접시에 오른 양은 저택이 정했습니다.");
            CtxP(A, "yv_meal_breakfast", "aftertrial",
                "아침을 차렸습니다. 자리를 다시 셌습니다. {seats}입니다.",
                "알려 드립니다. 식당에 아침을 차렸습니다.|심판 뒤로 자리를 새로 놓았습니다. {seats}입니다.");
            CtxP(A, "yv_meal_breakfast", "quiet",
                "아침을 차렸습니다. 자리는 어제와 같은 {seats}입니다.",
                "아침 식사입니다. 의자는 그대로 {seatsc} 개입니다. 식기 전에 드십시오.");
            CtxP(A, "yv_meal_breakfast", "rule_CH15",
                "아침을 차렸습니다. 권해 드린 짝의 자리는 저녁 식탁에 놓겠습니다.|아침은 편한 자리에 앉으십시오.");

            // ───────── 저녁 식사 (18:30) ─────────
            VP(A, "yv_meal_dinner",
                "저녁을 차렸습니다. 자리는 {seats}입니다.|촛불은 식사가 끝날 때까지 켜 두겠습니다.",
                "알려 드립니다. 저녁 식사가 준비되었습니다. 수저는 {seatsc} 벌입니다.",
                "식당에 저녁을 차렸습니다. 자리는 {seats}입니다.|늦으시는 분의 국은 식지 않게 두겠습니다.",
                "알려 드립니다. 여섯 시 반입니다. 식당에 {seatsc} 자리를 놓았습니다.",
                "저녁 식사입니다. 자리는 {seats}입니다.|오시지 않는 분의 의자도 치우지 않습니다.",
                "저녁을 차렸습니다. 의자 {seatsc} 개, 잔 {seatsc} 개입니다. 정해진 대로입니다.",
                "알려 드립니다. 식당에 저녁을 차렸습니다.|오늘의 마지막 식사입니다. 자리는 {seats}입니다.",
                "저녁 종이 울렸습니다. 식탁에는 {seatsc} 자리를 놓았습니다.");
            CtxP(A, "yv_meal_dinner", "afterdeath",
                "저녁을 차렸습니다. 자리는 {seats}입니다.|…정해진 대로, 전부 놓았습니다.",
                "알려 드립니다. 저녁 식사입니다. 치운 의자는 {fewer}입니다.|자리는 {seats}입니다.",
                "식당에 저녁을 차렸습니다. 수저를 {fewerc} 벌 도로 거두었습니다.|…드실 수 있는 만큼만 드십시오.",
                "저녁을 차렸습니다. 자리가 줄었습니다. {seats}입니다.",
                "알려 드립니다. 저녁 식사입니다.|…촛불을 {fewer} 덜 켰습니다. 자리는 {seats}입니다.");
            CtxP(A, "yv_meal_dinner", "hunger",
                "저녁을 차렸습니다. 자리는 {seats}입니다.|오늘 저녁도 몫이 줄었습니다. 저택이 아직 배고파합니다.",
                "알려 드립니다. 저녁 식사입니다. 냄비가 어제보다 가볍습니다.|제가 덜어 낸 것이 아닙니다.",
                "식당에 저녁을 차렸습니다. 접시는 {seatsc} 개, 접시 위는 조금 비었습니다.");
            CtxP(A, "yv_meal_dinner", "aftertrial",
                "저녁을 차렸습니다. 자리는 {seats}입니다.|심판이 지나간 뒤에도 식탁은 같은 시각에 차립니다.",
                "알려 드립니다. 식당에 저녁을 차렸습니다.|의자를 다시 셌습니다. {seats}입니다.");
            CtxP(A, "yv_meal_dinner", "quiet",
                "저녁을 차렸습니다. 오늘도 자리는 {seats}입니다.",
                "알려 드립니다. 저녁 식사입니다. 의자는 어제와 같은 {seatsc} 개입니다.");
            CtxP(A, "yv_meal_dinner", "rule_CH15",
                "저녁을 차렸습니다. 권해 드린 짝의 자리는 나란히 놓았습니다.|따르지 않으셔도 됩니다. 다른 자리도 비워 두었습니다.");

            // ───────── 밤 (22:00) ─────────
            VP(A, "yv_night",
                "밤입니다. 곧 몇몇 방의 문을 잠급니다. 무사히 주무시기를 바랍니다.",
                "알려 드립니다. 밤 열 시입니다.|오늘 밤 잠그는 방은 {locksc} 곳입니다. 안에 아무도 없으면 잠깁니다.",
                "밤입니다. 이 저택에서의 {dayth} 밤입니다.|복도의 등은 방으로 가시는 길만큼만 남겨 두었습니다.",
                "알려 드립니다. 밤입니다. 식당과 주방은 곧 잠깁니다.|두고 오신 물건은 아침에 찾으십시오.",
                "밤입니다. 정해진 대로 문을 잠급니다.|문이 잠겨 있어도 도움 요청과 신고는 받습니다.",
                "알려 드립니다. 밤입니다. 오늘 정해진 종은 이것으로 마칩니다.",
                "밤입니다. 복도의 등을 낮추겠습니다. 방으로 돌아가십시오.",
                "하루를 마칠 시각입니다. 잠글 방은 {locksc} 곳입니다.|…주무십시오.");
            CtxP(A, "yv_night", "afterdeath",
                "밤입니다. 정해진 대로 문을 잠급니다.|…오늘은 어항의 물이 조금 탁합니다.",
                "알려 드립니다. 밤입니다.|치운 의자는 내일 아침에도 그대로 두겠습니다.",
                "밤입니다. 복도의 등은 켜 둡니다. 방문은 각자 잠그셔도 됩니다.");
            CtxP(A, "yv_night", "hunger",
                "밤입니다. 문을 잠급니다.|저택이 밤사이 무엇을 삼킬지는 저도 정하지 않습니다.",
                "알려 드립니다. 밤입니다. 벽이 따뜻하더라도 기대지 마십시오.");
            CtxP(A, "yv_night", "quiet",
                "밤입니다. 조용한 하루였습니다. 정해진 대로 문을 잠급니다.",
                "알려 드립니다. 밤입니다. 오늘도 죽음을 알리는 종은 울리지 않았습니다.|…저택이 그 날수를 세고 있습니다.");
            CtxP(A, "yv_night", "aftertrial",
                "밤입니다. 심판장의 문도 닫았습니다. 다음에 열 때까지입니다.",
                "알려 드립니다. 밤입니다. 지난 판결은 기록에 남겼습니다.|문을 잠급니다.");

            // ───────── 저택의 허기 (Hunger.Morning) — {place}: 삼킨 방, {hungerth}: 몇 번째 방 ─────────
            VP(A, "yv_hunger",
                "알려 드립니다. 저택이 {place:을} 삼켰습니다. 오늘부터 식사가 조금 줄어듭니다.",
                "알려 드립니다. {place}의 문이 닫혔습니다.|저택이 삼킨 {hungerth} 방입니다. 저택의 배가 불러야 다시 열립니다.",
                "알려 드립니다. 조용한 날이 길었습니다. {place:이} 닫혔습니다.|식탁의 몫도 줄어듭니다. 규칙이 아니라, 이 집의 성질입니다.",
                "{place}의 문고리가 따뜻해졌습니다. 이제 열리지 않습니다.|알려 드립니다. 저택이 굶주리고 있습니다.",
                "알려 드립니다. 밤사이 {place:이} 벽 속으로 들어갔습니다.|제 열쇠로도 열리지 않습니다. …저택은 오래 참지 않습니다.");

            // ───────── 시신 발견 (Cases, 세 명 이상 확인 뒤) — 확인한 것은 신원과 장소뿐 ─────────
            VP(A, "yv_body",
                "…종소리가 세 번 울렸습니다. 알려 드립니다.|{place}에서 {victim} 님의 시신이 발견되었습니다.|곧 수사 시간을 드리겠습니다. 현장은 되도록 그대로 두십시오.",
                "알려 드립니다. {victim} 님께서 돌아가셨습니다. 장소는 {place}입니다.|곧 수사를 시작합니다.",
                "종소리를 들으셨을 것입니다. {place}에서 {victim} 님의 죽음을 확인했습니다.|…절차를 시작하겠습니다.",
                "알려 드립니다. {place}에서 {victim} 님의 죽음을 확인했습니다.|확인한 것은 신원과 장소뿐입니다. 곧 수사 시간을 드리겠습니다.",
                "종이 세 번 울렸습니다. {victim} 님입니다. 장소는 {place}입니다.|…정해진 대로, 수사를 시작합니다.",
                "알려 드립니다. {victim} 님의 자리를 거두어야 합니다.|장소는 {place}입니다. 현장에는 되도록 손대지 마십시오.");
            // 그를 알던 몇 사람: 수아(장갑의 별 — 물결이 한 번 멎었던 사람), 은결(서로 장례의 예를 갖추던 사람), 민혁(이름 앞의 한 박자)
            AboutP(A, "yv_body", "P12",
                "…알려 드립니다.|{place}에서 {victim} 님의 시신이 발견되었습니다.|…|정해진 대로, 수사를 시작하겠습니다.");
            AboutP(A, "yv_body", "P14",
                "알려 드립니다. {place}에서 {victim} 님의 시신이 발견되었습니다.|…예를 갖추어 알려 드렸습니다. 곧 수사를 시작합니다.");
            AboutP(A, "yv_body", "P01",
                "…알려 드립니다. {place}에서, {victim} 님의 시신이 발견되었습니다.|…절차는 멈추지 않습니다. 수사를 시작합니다.");

            // ───────── 수사 ─────────
            VP(A, "yv_invest_start",
                "지금부터 수사를 시작합니다. 정해진 시간 동안 저택을 살펴보십시오.|잠겨 있던 공용 시설은 모두 열어 두었습니다.",
                "수사 시간입니다. 현장은 발견된 그대로 두었습니다. 제가 손댄 것은 없습니다.|종이 다시 울리면 심판장으로 모이십시오.",
                "알려 드립니다. 절차에 따라 수사를 시작합니다.|예정된 모임은 모두 미룹니다. 공용 구역은 모두 열려 있습니다.",
                "수사를 시작합니다. 무엇을 보셨는지는 보신 분만 아십니다.|종이 다시 울리기 전까지입니다.");
            VP(A, "yv_invest_extend",
                "사망자가 더 확인되었습니다. 규칙대로 수사 시간을 30분 늘리겠습니다.",
                "알려 드립니다. 수사 시간을 30분 늘립니다. 서두르시되, 성급하지는 마십시오.",
                "절차에 따라 수사 시간을 30분 늘립니다. 종은 그만큼 늦게 울립니다.");
            // 심판 소집 — 심판장에 마련한 자리도 공개 기록 기준으로 센다
            VP(A, "yv_trial_summon",
                "알려 드립니다. 심판할 시각입니다. 중앙 홀의 승강기로 오십시오.|심판장에 마련한 자리는 {seats}입니다.",
                "심판장으로 모여 주십시오. 중앙 홀의 승강기가 기다리고 있습니다.",
                "알려 드립니다. 수사를 마칩니다. 모두 중앙 홀 승강기로 오십시오.|자리는 {seats}입니다. 늦으셔도 자리는 비워 두겠습니다.",
                "종이 다시 울렸습니다. 심판장으로 내려가실 시각입니다.|정해진 대로, 한 분도 빠짐없이 오십시오.");
            // 심판 뒤, 시계가 멈춘 채 이어지는 하루(Settlements.Continuation) — 밤이면 방으로, 낮이면 하루로
            VP(A, "yv_trial_night",
                "심판을 마쳤습니다. 밤이 깊었습니다.|모두 방으로 돌아가 쉬십시오. 아침 종이 울리면 문을 다시 열겠습니다.",
                "오늘의 절차는 여기까지입니다.|곧 몇몇 방의 문을 잠급니다. 방으로 가시는 길의 등은 켜 두었습니다.",
                "심판을 마쳤습니다. 내일 아침 식탁에는 {seatsc} 자리를 놓겠습니다.|…밤입니다. 방으로 돌아가십시오.",
                "판결을 전했습니다. 기록은 제가 보관합니다.|복도의 등은 남겨 두겠습니다. 주무십시오.");
            VP(A, "yv_trial_day",
                "심판을 마쳤습니다. 남은 하루는 각자 보내십시오.|공용 시설은 모두 열려 있습니다.",
                "오늘의 절차는 여기까지입니다. 다음 식사 때는 {seatsc} 자리를 놓겠습니다.",
                "심판을 마쳤습니다. 저택의 일과는 평소대로 이어집니다. 정해진 대로입니다.",
                "판결을 전했습니다. 항아리의 돌은 제가 거두겠습니다.|문은 모두 열려 있습니다.");
            // 고장 공지 (Grammars.NewFault) — {act}는 길다("이상한 소리를 내는 수영장 펌프"): 이름은 다음 쪽으로
            VP(A, "yv_repair",
                "알려 드립니다. {place}에서 {act:이} 말썽입니다.|{t} 님께서 손봐 주시기로 하셨습니다.",
                "알려 드립니다. {place}의 {act:이} 고장 났습니다.|수리는 {t} 님께서 맡으셨습니다.",
                "{place}에서 {act:이} 말썽입니다.|{t} 님께서 손보러 가십니다. 고쳐지기 전까지는 조심하십시오.",
                "알려 드립니다. {place}에 고장이 생겼습니다. {act}입니다.|{t} 님께서 맡으셨습니다.");

            // ───────── 아직 부르는 곳이 없는 공지 (DailyLifeDesign §3.4 행사 · §8.3 추모) ─────────
            // 행사/추모 시스템이 sim.Announce("y_event" / "y_memorial", slots)로 부르면 그대로 쓰인다.
            // y_event 슬롯: {t} 주최자 이름, {place} 장소, {when} 시각("오후 3시"), {act} 행사 이름("퀴즈쇼") · y_memorial 슬롯: {t} 준비한 사람, {place}.
            VP(A, "y_event",
                "알려 드립니다. {when}, {place}에서 모임이 있습니다.|{t} 님께서 여시는 {act}입니다. 참석은 자유입니다.",
                "{t} 님께서 {act:을} 여십니다.|장소는 {place}, 시각은 {when}입니다. 오시지 않아도 불이익은 없습니다.",
                "알려 드립니다. 오늘 {when}, {place}입니다.|{t} 님의 {act}입니다. 정해진 절차는 아닙니다. 가실지는 각자 정하십시오.");
            VP(A, "y_memorial",
                "알려 드립니다. {t} 님께서 {place}에 추모 자리를 마련하셨습니다.|참석은 자유입니다.",
                "{place}에 추모 자리가 마련되었습니다. {t} 님께서 준비하셨습니다.|오시지 않으셔도 기록에 남기지 않습니다.",
                "알려 드립니다. {place}에서 추모가 있습니다. {t} 님께서 맡으셨습니다.|…의자는 넉넉히 놓아 두었습니다.");
        }

        // ================================================================== 2. 심판 절차 (TrialSystem.Yusti → 리졸버)
        // 판사석에서 말하는 것은 절차뿐: 개정, 규칙 상기, 지목 요청, 투표, 동률·추첨, 결과. 논의에는 끼지 않는다.
        static void TrialProcedure(string A)
        {
            VP(A, "y_trial_open",
                "모두 자리에 서셨습니다. 지금부터 심판을 시작합니다.|본 것과 들은 것은 나누어 말씀해 주십시오.",
                "촛불을 켰습니다. 심판을 시작하겠습니다.|‘모릅니다’라는 대답도 기록에 그대로 남습니다.",
                "심판을 시작합니다. 저는 절차와 판결만 맡습니다.|논의는 여러분의 몫입니다.");
            CtxP(A, "y_trial_open", "rule_CH01",
                "심판을 시작합니다. 이번 투표는 공개됩니다.|누가 누구의 항아리에 돌을 넣었는지, 마감한 뒤 모두 밝힙니다.");
            CtxP(A, "y_trial_open", "rule_CH04",
                "심판을 시작합니다. 이번 챕터에서 처음 하신 말씀은 봉인되어 있습니다.|고치실 것이 있으면 새로 말씀해 주십시오.");
            CtxP(A, "y_trial_open", "rule_CH18",
                "심판을 시작합니다.|새로 지목받으신 분께는 반론할 기회를 한 번 먼저 드립니다.");
            VP(A, "y_accuse_call",
                "논의는 충분히 들었습니다. 범인이라고 생각하시는 분의 이름을 말씀해 주십시오.",
                "지목할 시각입니다. 의심하시는 분이 있다면, 한 분씩 이름을 불러 주십시오.",
                "촛불이 반을 넘었습니다. …지목해 주십시오.");
            VP(A, "y_vote_call",
                "투표를 시작합니다. 돌은 한 분에 하나입니다. 기권은 없습니다.",
                "항아리를 열어 두었습니다. 범인이라고 생각하시는 분의 항아리에 돌을 넣으십시오.|넣은 돌은 꺼낼 수 없습니다.");
            CtxP(A, "y_vote_call", "rule_CH01",
                "투표를 시작합니다. 이번에는 누가 어느 항아리에 돌을 넣었는지 공개됩니다.|그 점을 아시고 넣으십시오.");
            VP(A, "y_vote_tie",
                "돌의 수가 같습니다. 같은 수를 받으신 분들만 두고, 한 번 더 투표합니다.",
                "가장 많은 돌이 같은 수입니다. 다시 넣어 주십시오.|다시 같다면 공개 추첨으로 정합니다.");
            VP(A, "y_vote_draw",
                "표가 끝내 갈리지 않았습니다. 공개 추첨을 하겠습니다.|…뽑힌 분은 {t} 님입니다.",
                "다시 같습니다. 절차에 따라 추첨합니다.|{t} 님입니다.");
            VP(A, "y_vote_open",
                "‘공개 표결’에 따라 모든 표를 밝힙니다.|{list}",
                "누가 누구를 고르셨는지 알려 드립니다. 규칙대로입니다.|{list}");
            VP(A, "y_vote_result",
                "돌을 모두 셌습니다. 가장 많은 돌이 든 항아리는 {t} 님의 것입니다.",
                "결과를 알려 드립니다. …{t} 님입니다.",
                "항아리를 비웠습니다. 가장 무거운 항아리는 {t} 님의 것이었습니다.");
            VP(A, "y_trial_rule",
                "특별 규칙을 짚어 드립니다. ‘{name}’입니다.|{desc}");
            // 판결 한 줄(지금은 TrialDirectorUI가 고정 문장을 쓴다 — 이 키로 바꾸면 여기서 말한다)
            VP(A, "y_verdict_correct",
                "지목은… 맞았습니다. 정해진 대로 집행하겠습니다.",
                "판결을 말씀드립니다. {t} 님은 이번 심판이 찾던 범인이 맞습니다.|…정해진 대로입니다.");
            VP(A, "y_verdict_wrong",
                "판결을 말씀드립니다. {t} 님은 범인이 아닙니다.|…규칙대로 정산하겠습니다.",
                "지목은 틀렸습니다. {t} 님은 이번 심판이 찾던 범인이 아닙니다.");
        }

        // ================================================================== 3. 그의 방에서 (민혁만 찾아온다)
        // y_idle: 대화를 열 때와 "가볍게 말을 건다" · y_refuse_hint: "범인이 누군지 묻는다" · bye: "그만 간다". {you} = "김민혁 님".
        // 다른 주민 이야기는 과거형으로만 한다 — 그 사람이 이미 없어도 어긋나지 않게.
        static void RoomTalk(string A)
        {
            VP(A, "y_idle",
                "필요한 것이 있으시면 말씀하십시오. 답할 수 있는 범위는 정해져 있습니다.",
                "차는 따라 드릴 수 있습니다. 저는 마시지 않습니다.",
                "앉으셔도 됩니다. 이 방의 의자는 손님을 위해 있습니다.",
                "저택의 시계는 가끔 흐트러집니다. 저는 하루에 하나씩 바로잡습니다.",
                "장갑은 하루에 세 번 갈아 낍니다.|오수아 님께서 붙이신 별은 그때마다 옮겨 붙입니다.",
                "김진우 님께서 어항을 두드리신 적이 있습니다. 금붕어는 멈추지 않았습니다.",
                "윤해린 님께서 제 머리에 태엽이 있느냐고 물으셨습니다. 없습니다.|열어 보시는 것은 허락하지 않았습니다.",
                "차은결 님께서는 저를 집사님이라고 부르셨습니다.|같은 일을 하는 사람끼리의 예의라고 하셨습니다.",
                "남가온 님께서 저를 그리셨습니다. 금붕어는 넷이었습니다.|몇 마리인지 저는 세어 본 적이 없습니다.",
                "거짓말은 하지 않습니다. 말하지 않는 것이 많을 뿐입니다.",
                "기록은 제가 합니다. 고치지 않고, 지우지도 않습니다.",
                "종은 제가 치지 않습니다. 저택이 칩니다. 저는 그다음을 알려 드립니다.",
                "식사를 거르신 분의 몫은 부엌에 남겨 둡니다. 버리지 않습니다.",
                "밤에는 복도 등을 한 칸씩 남겨 둡니다. 방으로 돌아가시는 길만큼입니다.",
                "{you}께는 기록상 계약이 없습니다. 그 이상은 말씀드릴 수 없습니다.",
                "이 방에 오시는 분은 드뭅니다. 대개 {you}이십니다.",
                "저는 잠을 자지 않습니다. 이 의자에 앉아 저택의 소리를 듣습니다.",
                "신들께서는 지금도 보고 계십니다. 저는 그분들이 보시는 것을 기록합니다.",
                "홍차와 보리차가 있습니다. 보리차는 늘 넉넉히 끓여 둡니다.",
                "질문하셔도 됩니다. 답할 수 없는 것은 답할 수 없다고 말씀드리겠습니다.",
                "이 저택의 차는 매일 같은 온도로 우립니다. 그것만은 제 뜻대로 됩니다.",
                "어항 속 금붕어는 제 일부입니다. …아마 그럴 것입니다.",
                "복도의 그림은 제가 고르지 않았습니다. 주인들의 취향입니다.",
                "식당의 의자는 매일 제가 셉니다. 숟가락까지 세시는 분을 본 적도 있습니다.",
                "여러분의 이름은 모두 제가 적었습니다. 한 분도 빠짐없이, 같은 글씨로.",
                "이 방의 문은 잠그지 않습니다. 절차상 그렇게 되어 있습니다.",
                "금붕어는 헤엄칠 때 소리를 내지 않습니다. 멈출 때도 그렇습니다.",
                "차를 새로 내어 드리겠습니다. 식은 차를 드실 이유는 없습니다.",
                "회중시계는 한 번도 멈춘 적이 없습니다. 태엽을 감은 적도 없습니다.",
                "제가 드릴 수 있는 것은 차와 절차와 자리입니다. 그 밖의 것은 드릴 수 없습니다.",
                "저택의 방은 모두 기록에 있습니다. 저택이 삼킨 방도 지우지 않습니다.",
                "여러분이 오시기 전에도 이 방은 이렇게 조용했습니다.");
            CtxP(A, "y_idle", "morning",
                "아침입니다. 조금 전 식당의 의자를 셌습니다. 차를 드리겠습니다.",
                "시계는 하루에 하나씩, 아홉 시에 바로잡습니다. 어느 시계인지는 그날 정합니다.",
                "아침 식사는 여덟 시입니다. 수프는 제가 데웁니다. 맛은 보지 않습니다.",
                "밤사이 잠갔던 문은 아침 일곱 시에 엽니다. 이 방의 문은 처음부터 잠그지 않습니다.");
            CtxP(A, "y_idle", "day",
                "낮에는 저택이 조용합니다. 저는 그동안 기록을 정리합니다.",
                "오후의 빛이 어항을 지나면, 이 방 벽에 물무늬가 생깁니다.",
                "저녁 종은 여섯 시 반에 울립니다. 그때까지 드릴 수 있는 것은 차뿐입니다.",
                "점심은 드셨습니까. 제 절차에는 점심이 없어서, 여쭙기만 합니다.");
            CtxP(A, "y_idle", "evening",
                "저녁 식사를 마치면 촛불을 셉니다. 켠 수와 끈 수가 같아야 합니다.",
                "곧 밤입니다. 열 시가 되면 몇몇 방의 문을 잠급니다.",
                "밤에 주무셔야 하니, 저녁 차는 연하게 우리겠습니다.",
                "저녁 식탁을 치우면 의자는 원래 자리에 돌려 둡니다.");
            CtxP(A, "y_idle", "night",
                "밤에는 몇몇 방이 잠깁니다. 방으로 돌아가시는 길은 밝혀 두었습니다.",
                "이 시각에 오시는 분은 드뭅니다. …말씀하십시오.",
                "주무시지 않으셨습니까. 저는 잠을 자지 않습니다. 말씀은 들을 수 있습니다.",
                "밤에는 금붕어가 천천히 헤엄칩니다. 멈추지는 않습니다.",
                "이 방은 밤에도 잠그지 않습니다. 정해진 대로입니다.");
            CtxP(A, "y_idle", "afterdeath",
                "자리를 거두어야 합니다. 정해진 대로입니다.",
                "…물결이 가라앉기를 기다리고 있었습니다. 말씀하십시오.",
                "의자를 치울 때는 소리가 나지 않게 들어냅니다. 그것도 절차입니다.",
                "수사에 관한 것은 답해 드릴 수 없습니다. 차는 내어 드릴 수 있습니다.",
                "…오늘은 금붕어가 한쪽 구석에만 있습니다. 신경 쓰지 마십시오.",
                "종이 울린 날에도 차는 같은 온도로 우립니다. 그것만은 바뀌지 않습니다.",
                "오늘은 차를 조금 늦게 우렸습니다. …물이 끓는 것을 오래 보고 있었습니다.",
                "종이 울리면 저는 자리를 셉니다. 오늘도 셌습니다.");
            CtxP(A, "y_idle", "aftertrial",
                "항아리의 돌은 모두 꺼내 씻어 두었습니다. 다음에도 같은 돌을 씁니다.",
                "판결은 제가 전했습니다. 그 판결을 두고 드릴 말씀은 없습니다.",
                "심판장의 촛불은 새것으로 갈아 두었습니다. 정해진 대로입니다.",
                "판결 뒤에도 자리는 정해진 대로 셉니다. 줄어든 그대로입니다.",
                "심판장의 문은 닫아 두었습니다. 다음에 열 때까지 아무도 들어가지 않습니다.");
            CtxP(A, "y_idle", "hunger",
                "저택이 배고파하고 있습니다. 제가 전해 드릴 수 있는 건 그 사실뿐입니다.",
                "닫힌 방은 제 열쇠로도 열리지 않습니다. 저택이 삼킨 것은 저택만 돌려줍니다.",
                "식탁의 음식이 줄어든 것은 제가 아껴서가 아닙니다.",
                "벽이 따뜻합니다. 손을 대지는 마십시오.");
            CtxP(A, "y_idle", "quiet",
                "조용한 날이 이어지고 있습니다. …저택은 조용한 것을 오래 참지 않습니다.",
                "종이 울리지 않은 지 하루가 넘었습니다. 저는 그 시간을 세고 있습니다.",
                "의자는 오늘도 어제와 같은 수를 놓습니다.",
                "오늘도 종은 울리지 않았습니다. …저택의 벽이 조금 따뜻합니다.",
                "조용한 날에도 저는 자리를 셉니다. 세는 일은 줄지 않습니다.");
            CtxP(A, "y_idle", "rule_CH05", "계약 문장은 두 분 것만 알려 드렸습니다. 나머지는 답해 드릴 수 없습니다.");
            CtxP(A, "y_idle", "rule_CH06", "봉투의 내용은 저도 읽지 않았습니다. …읽을 필요가 없었습니다.");
            CtxP(A, "y_idle", "rule_CH09", "기한을 통지받으신 분이 누구인지는 말씀드릴 수 없습니다.");
            CtxP(A, "y_idle", "rule_CH21", "이번 챕터의 한도는 세 분입니다. 한도는 명령이 아닙니다.");
            CtxP(A, "y_idle", "rule_CH23", "어둡습니다. 이 방의 등만은 끄지 않았습니다. 규칙에 어긋나지 않는 만큼입니다.");
            // 2루프부터: 민혁의 이름 앞에서 한 박자 — 그 이상은 없다
            CtxP(A, "y_idle", "loop2",
                "…김민혁 님. …아닙니다. 필요한 것이 있으시면 말씀하십시오.",
                "오셨습니까. …김민혁 님.",
                "…{you}. 차를 내어 드리겠습니다.",
                "{you}. …앉으십시오. 의자는 그대로 있습니다.");
            // 3루프부터: 기억이 조금 샌다 — 방 안, 민혁에게만
            CtxP(A, "y_idle", "loop3",
                "금붕어는 금방 잊는다지요. …저는 잊지 않습니다.",
                "…그 의자에 앉으시는군요. 늘 그 의자였습니다. …아닙니다. 편히 앉으십시오.",
                "차를 드리겠습니다. 온도는… 이번에도 같게 맞췄습니다.",
                "물은 한 번 비워졌다가 다시 찼습니다. …금붕어는 같은 금붕어입니다.",
                "{you}께서 이 방에 오신 것이 몇 번째인지, 저는 세고 있습니다.");

            // "범인이 누군지 묻는다" — 거절도 절차다. 죽음 뒤의 거절은 사고인지 살인인지도 앞서 말하지 않는다
            VP(A, "y_refuse_hint",
                "그 질문에 답하면 심판이 필요 없어집니다. 절차는 그렇게 되어 있지 않습니다.",
                "저는 봅니다. 그리고 말씀드리지 않습니다. 둘 다 절차입니다.",
                "제가 이름을 말씀드리는 것은 판결 때뿐입니다.",
                "의심은 여러분의 몫입니다. 판결은 제 몫입니다. 섞지 않겠습니다.",
                "그 질문은 심판장에서 하십시오. 거기서는 모두가 답해야 합니다.",
                "제가 답하면, 여러분의 표가 제 표가 됩니다. 그럴 수는 없습니다.",
                "{you}께서 찾으신 것만이 심판에서 힘을 가집니다. 제 말은 그렇지 않습니다.",
                "…금붕어는 그 질문에 멈추지 않습니다. 답해 드릴 수 없습니다.",
                "제 얼굴을 보셔도 물뿐입니다. 답은 거기 없습니다.",
                "기록에 있는 사실만 전해 드립니다. 그 답은 아직 기록되지 않았습니다.");
            CtxP(A, "y_refuse_hint", "afterdeath",
                "종이 울렸습니다. 제가 확인한 것은 신원과 장소뿐입니다.|그다음은 여러분이 살피실 일입니다.",
                "이 방에서 보내시는 시간은, 현장에서는 쓰지 못하는 시간입니다.",
                "수사에 도움이 될 말씀은 드릴 수 없습니다. 방해가 될 말씀도 드리지 않습니다.",
                "그 질문의 답은 심판장에서만 나옵니다. 이 방에서는 나오지 않습니다.",
                "제가 아는 것과 제가 말할 수 있는 것은 다릅니다. 오늘은 특히 그렇습니다.");
            CtxP(A, "y_refuse_hint", "loop2", "…{you}. 그 질문에는 답해 드릴 수 없습니다.");
            CtxP(A, "y_refuse_hint", "loop3", "…그 질문은 처음이 아니십니다. 답은 같습니다. 드릴 수 없습니다.");

            // "그만 간다"
            VP(A, "bye",
                "문은 열어 두겠습니다.",
                "가 보십시오. 복도의 등은 켜 두었습니다.",
                "차는 식기 전에 치우겠습니다.",
                "이 방은 늘 열려 있습니다. 정해진 대로입니다.",
                "…조심해서 돌아가십시오, 라고 말씀드리는 데까지가 제 일입니다.");
            CtxP(A, "bye", "night",
                "밤입니다. 방으로 가시는 길의 등은 켜 두었습니다.",
                "주무십시오. 저는 여기 있겠습니다.");
            CtxP(A, "bye", "afterdeath",
                "가 보십시오. 저는 여기 있겠습니다.",
                "…다녀오십시오.",
                "가 보십시오. 차는 식지 않게 두겠습니다.",
                "그럼. …정해진 대로, 저는 여기 있겠습니다.");
            CtxP(A, "bye", "loop2", "그럼. …김민혁 님.");
            CtxP(A, "bye", "loop3", "…또 오십시오. 저는 기다리는 데 익숙합니다.");

            // 남의 욕설·음담을 들었을 때 — 그는 욕하지도 웃지도 않는다. 받아 적을 뿐이다(지금은 부르는 곳이 없다: 심판·방 대화가 쓰면 된다)
            VP(A, "react_swear",
                "지금 하신 말씀도 기록에 그대로 남습니다.",
                "욕설은 고치지 않고 적습니다. 절차상 그렇습니다.",
                "…말씀은 들었습니다. 한 글자도 빼지 않고 적겠습니다.",
                "목소리를 낮추실 필요는 없습니다. 다만 그 말도 기록됩니다.");
            VP(A, "react_dirty",
                "그 농담도 기록에 남습니다. 웃으실 분은 웃으십시오.",
                "…금붕어는 그 말에 멈추지 않았습니다. 계속하십시오.",
                "절차와 상관없는 말씀입니다. 그래도 기록은 합니다.");

            // 3루프 이상에서만 쓰는 균열(break_line) — 지금은 부르는 곳이 없다. 부른다면 3루프부터.
            VP(A, "break_line",
                "정해진 대로… 정해진, 대로. …물이 멈췄습니다. 곧 다시 흐릅니다.",
                "…김민혁 님. 이번에도 저는 기록만 합니다. 이번에도.");
        }
    }

    /// <summary>
    /// Yusti's announcements (voice pack NPC00; CharacterBible NPC00, DailyLifeDesign §3.1). <see cref="Simulation.Announce"/> asks this
    /// first (one hooked line in LifeAI): a Yusti key "y_x" is spoken from the pack's "yv_x" pools when they exist, else from "y_x",
    /// through the line resolver (situations afterdeath / aftertrial / hunger / quiet / rule_CHxx, a line about the dead, no repeats),
    /// with the numbers that carry the dread filled in (<see cref="Numbers"/>). Every count is PUBLIC: a body nobody has found still has
    /// its chair, so an announcement never gives a death away. Returns null when Yusti has no line for the key — the house voice
    /// (Lines_House) keeps its old path. Kernel-safe: randomness is the resolver's (Stream.Dialogue), memory is in S.Flags.
    /// </summary>
    public static class YustiVoice
    {
        public static string Announcement(Simulation sim, string voice, string key, Dictionary<string, string> slots)
        {
            if (sim == null || key == null || voice != Cast.Butler || !key.StartsWith("y_", StringComparison.Ordinal)) return null;
            var S = sim.S;
            string k = key;
            // the morning bell said right after a 심판 in the continuous flow can fall at any hour: "아침입니다" only in the morning
            if (k == "y_morning") { int m = S.Minute; if (m < 5 * 60 || m >= 11 * 60) k = m >= 20 * 60 || m < 5 * 60 ? "y_trial_night" : "y_trial_day"; }
            string vk = "yv_" + k.Substring(2);
            string use = Has(vk) ? vk : Has(k) ? k : null;
            if (use == null) return null;
            var d = slots != null ? new Dictionary<string, string>(slots) : new Dictionary<string, string>();
            Numbers(S, d, k);
            // the dead by id, so a line about one person (yv_body#P12) can be chosen; Render names them back ("오수아 님")
            if (d.TryGetValue("victim", out var vn) && vn != null && !vn.StartsWith("@", StringComparison.Ordinal))
            {
                var c = Cast.All.FirstOrDefault(x => x.Name == vn);
                if (c != null) d["victim"] = "@" + c.Id;
            }
            return sim.Render(Cast.Butler, null, use, d);
        }

        static bool Has(string key) => LineBank.Has(Cast.Butler, key) || LineBank.HasSuffixed(Cast.Butler, key, '~') || LineBank.HasSuffixed(Cast.Butler, key, '#');

        /// <summary>The number slots (only those the caller did not pass). All counts are public knowledge.</summary>
        public static void Numbers(GameState S, Dictionary<string, string> d, string key = null)
        {
            void Put(string name, string v) { if (!d.ContainsKey(name)) d[name] = v; }
            int seats = Seats(S), all = Cast.Participants.Count(), gone = Math.Max(0, all - seats), fewer = Fewer(S);
            Put("seats", Native(seats, false)); Put("seatsc", Native(seats, true));
            Put("gonec", gone > 0 ? Native(gone, true) : "몇");
            Put("fewerc", fewer > 0 ? Native(fewer, true) : "몇"); Put("fewer", Native(Math.Max(1, fewer), false));
            Put("dayth", Ordinal(S.Day));
            Put("quiet", DayCount(Math.Max(1, (int)Math.Floor((S.Clock - QuietSince(S)) / 1440.0) + 1)) + "째");
            Put("hungerth", Ordinal(Math.Max(1, Hunger.Level(S))));
            int locks = NightRooms(S, key == "y_morning");
            Put("locksc", locks > 0 ? Native(locks, true) : "몇");
        }

        /// <summary>Chairs Yusti lays: every participant except those publicly gone (a confirmed death this loop, executed, escaped).
        /// A body nobody has found yet keeps its chair.</summary>
        public static int Seats(GameState S)
        {
            int n = 0;
            foreach (var c in Cast.Participants)
            {
                var a = S.A(c.Id); if (a == null) continue;
                if (a.Status == ActorStatus.Executed || a.Status == ActorStatus.Escaped) continue;
                if (a.Status == ActorStatus.Dead && S.Incidents.Values.Any(i => i.Loop == S.Loop && i.Victim == a.Id && i.Confirmed)) continue;
                n++;
            }
            return n;
        }

        /// <summary>Chairs taken away in the last day: deaths confirmed within 24 h, plus whoever left at the last 심판 if the chapter it
        /// closed began less than a day ago.</summary>
        static int Fewer(GameState S)
        {
            int n = 0;
            foreach (var i in S.Incidents.Values)
                if (i.Loop == S.Loop && i.Confirmed && i.ConfirmClock >= 0 && S.Clock - i.ConfirmClock < 24 * 60 && i.Victim != Cast.Butler) n++;
            var st = S.Settlements.LastOrDefault(x => x.Loop == S.Loop);
            if (st != null && st.Chapter < S.Chapter && S.Clock - S.Ch.ChapterStartClock < 24 * 60) { if (st.Executed != null) n++; if (st.Escaped != null) n++; }
            return n;
        }

        /// <summary>Rooms the house locks at night (morning: those locked right now, about to open).</summary>
        static int NightRooms(GameState S, bool lockedNow)
        {
            var L = S.Layout; if (L == null) return 0;
            int n = 0;
            foreach (var r in L.Rooms)
            {
                if (!RoomInfo.NightLocked(r.Type)) continue;
                foreach (var did in r.Doors)
                {
                    var door = did >= 0 && did < L.Doors.Count ? L.Doors[did] : null;
                    if (door != null && door.NightPolicy && !door.Sealed && (!lockedNow || door.Locked)) { n++; break; }
                }
            }
            return n;
        }

        /// <summary>Start of the current quiet spell: the chapter start or the last confirmed death, whichever is later.</summary>
        static double QuietSince(GameState S)
        {
            double t = S.Ch.ChapterStartClock;
            foreach (var i in S.Incidents.Values) if (i.Loop == S.Loop && i.Confirmed && i.ConfirmClock > t) t = i.ConfirmClock;
            return t;
        }

        static readonly string[] OnesS = { "", "하나", "둘", "셋", "넷", "다섯", "여섯", "일곱", "여덟", "아홉" };
        static readonly string[] OnesC = { "", "한", "두", "세", "네", "다섯", "여섯", "일곱", "여덟", "아홉" };
        static readonly string[] Tens = { "", "열", "스물", "서른", "마흔", "쉰", "예순", "일흔", "여든", "아흔" };
        static readonly string[] Days = { "", "하루", "이틀", "사흘", "나흘", "닷새", "엿새", "이레", "여드레", "아흐레", "열흘" };

        /// <summary>Native Korean number 1–99: standalone ("열하나", "스물") or before a counter ("열한 개", "스무 자리").</summary>
        public static string Native(int n, bool counter)
        {
            if (n <= 0 || n >= 100) return n.ToString();
            int t = n / 10, o = n % 10;
            if (o == 0) return counter && t == 2 ? "스무" : Tens[t];
            return Tens[t] + (counter ? OnesC[o] : OnesS[o]);
        }
        /// <summary>"첫" for 1, else "두 번째", "열한 번째", "스무 번째" — before a noun ("첫 아침", "세 번째 밤").</summary>
        public static string Ordinal(int n) => n <= 1 ? "첫" : Native(n, true) + " 번째";
        /// <summary>A span of days: 하루, 이틀, 사흘 … 열흘, then "11일".</summary>
        public static string DayCount(int n) => n >= 1 && n < Days.Length ? Days[n] : n + "일";
    }
}
