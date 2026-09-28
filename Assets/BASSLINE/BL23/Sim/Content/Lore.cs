using System.Collections.Generic;

namespace BL23.Sim
{
    /// <summary>
    /// Things to find by spending time in the rooms: books on the shelves, what the telescope shows, the films in the
    /// projector booth, the curiosities in the cases. Flavour and hints about the mansion — never case evidence.
    /// Each entry is read once per loop per reader; later loops may show changed text (the house rewrites itself).
    /// </summary>
    public static class Lore
    {
        public sealed class Entry { public string Id; public string Title; public string Text; public string AltText; }

        static Entry E(string id, string title, string text, string alt = null) => new Entry { Id = id, Title = title, Text = text, AltText = alt };

        public static readonly List<Entry> Books = new List<Entry>
        {
            E("b_guest", "『손님 명부, 표지 없음』", "이름 열여덟 개가 적혀 있다. 모두 같은 필체다. 마지막 줄은 비어 있고, 잉크가 아직 마르지 않은 것처럼 번들거린다.", "이름이 하나 더 늘었다. 읽으려 하면 글자가 물속처럼 흔들린다."),
            E("b_house", "『저택의 문법』", "\"방은 머무는 사람을 닮아 간다. 오래 비워 둔 방은 가장 마지막에 머문 사람의 버릇을 흉내 낸다.\"", "\"방은 떠난 사람을 기억한다. 문을 열 때마다 조금씩 잊는다.\""),
            E("b_fish", "『물고기의 예법』 — 저자 미상", "집사는 숨을 쉬지 않는 대신 물을 간다. 수조에 새 물을 부을 때마다 집사가 조금 더 정중해진다고 적혀 있다."),
            E("b_clock", "『멈춘 시계를 믿지 말 것』", "\"모든 시계가 같은 시각을 가리킨다면, 그중 하나는 거짓말을 따라 하고 있는 것이다.\""),
            E("b_bass", "『저음부(低音部)에 관하여』", "음악에서 베이스라인은 들리지 않는 척하며 모든 것을 떠받친다. 이 책의 여백에는 누군가 \"우리도 그렇다\"라고 연필로 적어 두었다."),
            E("b_loop", "『반복되는 여름의 기록』", "같은 날이 몇 번 되풀이되었는지 세던 사람이 있었다. 기록은 '일곱'에서 끊겨 있다. 그다음 장은 뜯겨 나갔다."),
            E("b_poison", "『정원사의 독초 도감』", "디기탈리스, 협죽도, 은방울꽃. 아름다운 것일수록 심장을 먼저 멈춘다는 문장에 밑줄이 그어져 있다."),
            E("b_lock", "『자물쇠와 그 친구들』", "안쪽 잠금쇠는 손가락이 아니어도 돌아간다. 실 한 가닥과 인내심, 문 아래 한 뼘의 틈이면 충분하다며 그림까지 그려져 있다."),
            E("b_mirror", "『거울을 뒤집어 보는 법』", "\"거울 속의 당신은 왼손잡이다. 그 사실을 기억하는 사람은 드물다.\""),
            E("b_saints", "『추모 예배실의 성인들』", "스테인드글라스 속 성인들에게는 얼굴이 없다. 대신 저마다 손에 든 물건이 누구인지 알려 준다고 한다."),
            E("b_cook", "『저택의 부엌에서』", "하루 세 번 울리는 식사 종은 이 집의 심장 박동이다. 종소리를 한 번이라도 거른 날에는 반드시 누군가 사라졌다고 적혀 있다."),
            E("b_trial", "『심판의 방 설계도』", "원형의 방, 열여덟 개의 연단. 설계자는 '모두가 서로를 보도록, 그리고 아무도 서로의 등을 보지 못하도록'이라고 메모했다."),
            E("b_diary", "『누군가의 일기 (찢긴 쪽)』", "\"오늘도 그 사람은 온실에 있었다. 매일 같은 시각, 같은 자리. 그래서 알 수 있었다. 어디에 없을지를.\""),
            E("b_colour", "『색의 온도』", "난로 가까이 둔 것은 늦게 식고, 지하 저장고에 둔 것은 빨리 식는다. 식은 정도로 시각을 재는 사람은 그걸 잊기 쉽다."),
        };

        public static readonly List<Entry> Views = new List<Entry>
        {
            E("v_moon", "망원경 — 달", "달이 너무 가깝다. 표면의 무늬가 이 저택의 평면도와 닮았다. 계단이 있어야 할 자리에 검은 점이 하나 있다."),
            E("v_window", "망원경 — 창밖", "정원 너머에는 아무것도 없다. 안개가 아니라, 정말로 아무것도. 렌즈를 닦아도 그대로다."),
            E("v_globe", "지구의", "대륙의 모양이 이상하다. 돌려 보면 모든 대륙이 이 저택의 방 모양이다. 바다는 칠해져 있지 않다."),
            E("v_cage", "기계새", "태엽 새가 한쪽 눈으로 이쪽을 본다. 부리를 벌리더니, 방금 이 방에서 들린 말소리를 그대로 흉내 낸다."),
            E("v_case", "유리 진열장", "라벨이 붙은 물건들: '첫 번째 여름의 단추', '두 번째 여름의 이', '세 번째 여름의 편지'. 네 번째 칸은 비어 있다."),
            E("v_aquarium", "수조", "작은 물고기들이 한 방향으로만 헤엄친다. 시계 방향. 가끔 한 마리가 거꾸로 돌다가 사라진다."),
            E("v_fountain", "마른 분수", "물이 없는데도 분수 바닥이 젖어 있다. 동전 대신 단추와 열쇠가 몇 개 떨어져 있다."),
            E("v_pool", "수영장 바닥", "물 아래 타일 무늬가 글자처럼 보인다. 다시 보면 그냥 타일이다."),
            E("v_portrait", "초상화들", "액자 속 인물들의 시선이 방 한가운데로 모인다. 그 자리에 서면, 누군가 뒤에서 숨을 쉬는 것 같다."),
            E("v_stars", "망원경 — 별", "별자리 하나가 매일 밤 한 칸씩 움직인다. 오늘은 식당 쪽을 가리키고 있다."),
        };

        public static readonly List<Entry> Films = new List<Entry>
        {
            E("f_party", "필름 #3 — 파티", "흑백 필름 속에서 열여덟 명이 춤을 춘다. 얼굴 부분만 필름이 녹아 있다. 소리 없는 필름인데도, 다들 같은 음악을 듣는 것처럼 발이 척척 맞는다."),
            E("f_butler", "필름 #7 — 집사", "머리가 수조인 집사가 카메라를 향해 정중하게 허리를 숙인다. 필름이 끝날 때까지 고개를 들지 않는다."),
            E("f_empty", "필름 #11 — 빈 방", "아무도 없는 방을 찍은 필름. 13분쯤 지나자, 의자 하나가 저절로 반 뼘 밀려난다."),
            E("f_trial", "필름 #13 — 심판", "원형의 방과 촛불. 투표용 돌이 항아리에 떨어지는 소리만 되풀이된다. 마지막 장면에서 누군가 손으로 카메라를 가린다."),
        };

        public static readonly string[] CookResults = { "Bread", "Snack", "Tea", "Chocolate" };
        public static readonly string[] CraftResults = { "PaperModel", "Sticker", "Button" };
    }
}
