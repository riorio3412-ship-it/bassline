using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// Gift preferences per resident (DailyLifeDesign §5, bible favourites). DATA ONLY for now: PlayerApi.GiftScore still does the
    /// keyword match; the planned one-line hook (Phase 3, after time-on-demand) is <c>Gifts.Score(npc, item) ?? old score</c>.
    /// Scale matches GiftScore: 2 loved (+.09 like), 1 liked (+.06), 0 neutral (+.03), -1 disliked (-.03 with a line).
    /// Item ids are ItemCatalog types; names marked New are proposed items that do not exist yet (MF owns ItemCatalog).
    /// </summary>
    public static class Gifts
    {
        public sealed class Prefs
        {
            public string Id;
            public string[] Loved = new string[0], Liked = new string[0], Disliked = new string[0];
            /// <summary>Why a disliked gift is refused, in their voice (used with gift_hate).</summary>
            public string DislikeLine;
            /// <summary>The one gift that plays a unique short reaction once per loop.</summary>
            public string Signature, SignatureLine;
            /// <summary>Proposed new item types (not in ItemCatalog yet): Korean names.</summary>
            public string[] New = new string[0];
            /// <summary>Accepts anything (neutral never goes below 0): 태겸 books it, 준서 never refuses food.</summary>
            public bool NeverRefuses;
        }

        static Prefs P(string id, string[] loved, string[] liked, string[] disliked, string dislikeLine, string sig, string sigLine, string[] nw = null, bool never = false)
            => new Prefs { Id = id, Loved = loved, Liked = liked, Disliked = disliked, DislikeLine = dislikeLine, Signature = sig, SignatureLine = sigLine, New = nw ?? new string[0], NeverRefuses = never };
        static string[] L(params string[] s) => s;

        public static readonly Dictionary<string, Prefs> All = new Dictionary<string, Prefs>
        {
            ["P02"] = P("P02", L("Candy"), L("Book"), L("Flower"), "꽃? 뻔한 위로 같아. 흐응, 그런 거 말고.",
                        "ClearCandy", "…투명? 네가 이걸 골랐어? 큭. 맛이 안 보이는 걸 줬네.|믿는다는 뜻이야, 시험이야?", L("투명 사탕")),
            ["P03"] = P("P03", L("GoodPen"), L("Chocolate", "Sticker"), L("Candy"), "사탕은… 괜찮아요. 단 건 잘 안 먹어서요. 마음만 받을게요.",
                        "Sticker", "…이거 햄스터네요. 회의록 닮았어요. 후훗.|명단 맨 끝에 붙일게요.", L("좋은 볼펜")),
            ["P04"] = P("P04", L("Tea"), L("Flower"), L("Sticker"), "스티커는 사양하겠습니다. 붙이면 흔적이 남지요.",
                        "ChippedCup", "고칠 수 있겠습니다. …아니,|고치게 해 주셔서 감사합니다.", L("이 빠진 찻잔")),
            ["P05"] = P("P05", L("GoldCoinChocolate"), L("Soda"), L("Notebook", "Recorder"), "기록하는 물건은 좀… 하하하. 마음만 감사히 받겠습니다.",
                        "GoldCoinChocolate", "금이라. 하하하. 가짜인 걸 알고 주셨죠?|그게 마음에 듭니다.", L("금박 동전 초콜릿")),
            ["P06"] = P("P06", L("Chocolate"), L("WrappingPaper", "Tea"), L(), null,
                        "WrappingPaper", "…이 접힘. 삼각으로 두 번.|이건 장부에 적지 않겠습니다.", L("포장지"), never: true),
            ["P07"] = P("P07", L("Beer", "Book"), L("Snack"), L("Tea"), "무향 차? 야, 이거 할아버지 음료잖아. 크하하. 마음만 받을게.",
                        "Book", "야, 이거 밑줄 그어진 거잖아. 남이 좋아한 문장…|레알 최고의 선물이다."),
            ["P08"] = P("P08", L("HandWarmer"), L("Soda"), L("Invitation"), "초대장이네. 구호 나오는 모임이지? 난 빠질게.",
                        "HandWarmer", "…식은 거네. 이거 모으는 거 어떻게 알았어.|크. 3초 감동."),
            ["P09"] = P("P09", L("OldPlaybill"), L("Flower"), L("Camera"), "민혁아, 카메라는… 찍지 마. 그건 받기 좀 그래.",
                        "OldPlaybill", "세상에… 이 극단, 없어진 데야.|민혁아, 이거 어디서 났어?", L("낡은 공연 팸플릿")),
            ["P10"] = P("P10", L("Flower", "SeedPacket"), L("Bread"), L(), null,
                        "SeedPacket", "심으면 뭐가 날까요.|…누가 심어 준 씨앗은 처음이에요.", L("씨앗 봉투"), never: true),
            ["P11"] = P("P11", L("WindupToy", "Soda"), L("Flashlight"), L("Flower"), "꽃은 관리 못 해. 금방 죽어. 차라리 태엽 달린 거.",
                        "Soda", "안 흔든 거지? …진짜네. 헤헤.|이건 기억해 둘게."),
            ["P12"] = P("P12", L("Sticker", "Snack"), L("Flower"), L("Recorder"), "녹음기는… 몰래 녹음하는 건 싫어요. 미안해요.",
                        "Snack", "쉿, 이거 비밀이에요. …고마워요.|진짜 나한테 준 거죠?"),
            ["P13"] = P("P13", L("Soda"), L("Snack"), L(), null,
                        "HandWarmer", "하나면 되겠네. …농담이야.|웃어도 돼. 진짜로."),
            ["P14"] = P("P14", L("Tea", "Flower"), L("ColorPaper"), L("Invitation"), "초대장은 정중히 사양하겠습니다. 모이는 자리는 서툴러서요.",
                        "Flower", "백합이군요. …이건 산 사람한테 받아도|괜찮은 꽃입니다. 후후.", L("색종이")),
            ["P15"] = P("P15", L("InstantCoffee"), L("Notebook"), L("Sticker"), "스티커는… 수첩에 붙일 데가 없어요. 마음만 받을게요.",
                        "InstantCoffee", "반 봉지. …제가 말한 적 있었나요?|적어 둘게요. 아니, 이건 안 적을게요.", L("인스턴트 커피")),
            ["P16"] = P("P16", L("Button"), L("Mascot"), L("Sticker"), "스티커? 촌스러워. 별로.",
                        "Button", "날짜 적어 둘게.|오늘, 너, 자개 단추 하나.", L("유치한 마스코트 인형")),
            ["P17"] = P("P17", L("PaperModel", "Invitation"), L("Sticker"), L("Book"), "두꺼운 책은 지루해애. 대신 문제 하나 내 줄게.",
                        "PaperModel", "짜잔은 내 대사인데! …이거 어디 방이야?|모형에 붙일래."),
            ["P18"] = P("P18", L("BarleyTea", "PuzzleBook"), L("Bread"), L("Expensive"), "부담됩니다. 이런 건 받을 이유가 없습니다.",
                        "PuzzleBook", "…이건 괜찮습니다. 칠 쪽부터 풀겠습니다. 허.", L("보리차 티백", "숫자 퍼즐집")),
        };

        /// <summary>Score an item type for a resident: 2 loved, 1 liked, -1 disliked, 0 neutral; null when this table has no opinion
        /// (the caller keeps PlayerApi.GiftScore's keyword match).</summary>
        public static int? Score(string npc, string itemType)
        {
            if (npc == null || itemType == null || !All.TryGetValue(npc, out var p)) return null;
            if (p.Loved.Contains(itemType)) return 2;
            if (p.Liked.Contains(itemType)) return 1;
            if (p.Disliked.Contains(itemType)) return p.NeverRefuses ? 0 : -1;
            return null;
        }

        public static Prefs Get(string npc) => npc != null && All.TryGetValue(npc, out var p) ? p : null;
        /// <summary>Is this the resident's signature gift (a unique reaction once per loop: flag "giftsig:&lt;npc&gt;")?</summary>
        public static bool IsSignature(string npc, string itemType) => Get(npc)?.Signature == itemType;
    }
}
