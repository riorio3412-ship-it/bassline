using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    public enum Gender { M, F }

    /// <summary>How a character talks. Used by the line renderer for generic templates.</summary>
    [Serializable]
    public sealed class SpeechStyle
    {
        public bool PoliteDefault;      // 초면 존댓말 여부
        public string SelfPolite = "저", SelfCasual = "나";
        public string CallSuffixPolite = "씨", CallSuffixCasual = "";  // 민혁 씨 / 민혁아(자동)
        public string[] Fillers = new string[0];
        public string[] Exclaims = new string[0];
        public string Laugh = "하하";
        public float Verbosity = 0.5f;   // 0 짧음 .. 1 김
        public float VoicePitch = 1f;    // babble voice pitch
        public float VoiceRate = 1f;
    }

    /// <summary>AI personality weights 0..1. Not visible to the player.</summary>
    [Serializable]
    public sealed class Persona
    {
        public float Morality;      // 금기 강도: 높을수록 살인 억제
        public float Aggression;
        public float Sociability;
        public float Curiosity;
        public float Loyalty;
        public float Fearfulness;
        public float Ambition;
        public float Honesty;
        public float Pride;
        public float Jealousy;
        public float WishDesire;    // 계약 소원에 대한 집착
        public float Romance;       // 연애 관심 경향
        public float Grudge;        // 원한을 오래 품는 정도
    }

    public enum HairStyle
    {
        MessyShort, CurlyShort, Bob, WolfCut, SlickedBack, SidePart55, Dreadlocks, ShaggyMid, WavyChin,
        Cropped, BunMessy, LongRibbon, ShortRedStreak, HimeLong, BobBangs, SideBraid, TwinTails, Buzz, None
    }

    public enum Garment
    {
        Shirt, Tshirt, Turtleneck, Knit, Hoodie, TankTop, Blouse, Vest, Blazer, SuitJacket, LongCoat, TrenchCoat, FurCoat,
        Cardigan, Capelet, Apron, Overalls, Pants, Jeans, Slacks, CargoPants, Skirt, PleatedSkirt, LongDress, Shorts, Tights,
        KneeSocks, StripedStockings, Sneakers, HighTops, Boots, Loafers, DressShoes, PlatformShoes, Tailcoat, WorkShirt,
        CroppedJacket, DenimJacket, MourningDress, WaistTiedOveralls
    }

    public enum Accessory
    {
        Glasses, RoundGlasses, Sunglasses, Goggles, Earphone, Headset, Scarf, SilkScarf, Ribbon, BigRibbon, Necklace, GoldChain,
        Rings, Brooch, PearlBrooch, Watch, DigitalWatch, CrossBag, Briefcase, Armband, Lanyard, Beret, Beanie, Headband,
        HairPin, StarPins, ProstheticHandR, Thermos, Notebook, TapeMeasure, ToolBelt, PenEar, AquariumHead, WhiteGloves,
        LaceGloves, PocketWatch, Clipboard, Smartphone, BassBracelet, GoldTooth, Tie, RibbonTie, Choker, Earrings, LilyFlower
    }

    [Serializable]
    public sealed class GarmentSpec { public Garment Kind; public string Color; public string Color2; public string Pattern; }

    [Serializable]
    public sealed class LookSpec
    {
        /// <summary>"GLB:minhyuk" for provided scans, "PROC" for procedural actors.</summary>
        public string Model = "PROC";
        public float Build = 0.4f;          // 0 slender .. 1 heavy
        public float Shoulders = 0.5f;      // 0 narrow .. 1 broad
        public float HeadScale = 1f;
        public string Skin = "#F2D6C4";
        public HairStyle Hair;
        public string HairColor = "#2A2320";
        public string HairColor2 = "";      // streak / gradient
        public string EyeColor = "#3B2F2A";
        public float EyeSharp = 0.5f;       // 0 round/soft .. 1 sharp
        public List<GarmentSpec> Wear = new List<GarmentSpec>();
        public List<Accessory> Acc = new List<Accessory>();
        public string AccColor = "#C9A24A";
        public string Silhouette = "";      // 설계 메모
    }

    [Serializable]
    public sealed class CastDef
    {
        public string Id;           // P01..P18, NPC00
        public string Name;         // 김민혁
        public string Given;        // 민혁
        public Gender Gender;
        public int HeightCm;
        public string Job;
        public string Contract;     // 소원 문장 (비공개)
        public int Obs, Infer, Argue, Empathy, Deceit, Composure;
        public string Core;         // 유지할 성격
        public string Flaw;
        public string Secret;       // 제작자 비밀 (비공개)
        public string[] Likes = new string[0];      // 대화 화제 / 선물 태그
        public string[] Dislikes = new string[0];
        public string[] Hobbies = new string[0];    // activity ids
        public string[] FavRooms = new string[0];   // RoomType names
        public Persona P = new Persona();
        public SpeechStyle Speech = new SpeechStyle();
        public LookSpec Look = new LookSpec();
        public bool IsPlayer => Id == "P01";
        public bool IsButler => Id == "NPC00";
    }

    public static class Cast
    {
        public const string Player = "P01";
        public const string Butler = "NPC00";
        static List<CastDef> _all;
        static Dictionary<string, CastDef> _byId;
        public static IReadOnlyList<CastDef> All { get { Ensure(); return _all; } }
        public static IEnumerable<CastDef> Participants { get { Ensure(); return _all.Where(c => !c.IsButler); } }
        public static IEnumerable<CastDef> Npcs { get { Ensure(); return _all.Where(c => !c.IsButler && !c.IsPlayer); } }
        public static CastDef Get(string id) { Ensure(); return id != null && _byId.TryGetValue(id, out var c) ? c : null; }
        public static string NameOf(string id) => Get(id)?.Name ?? id;
        public static string GivenOf(string id) => Get(id)?.Given ?? id;
        /// <summary>Dominant hand (public: everyone has seen them eat and write). A few of the cast are left-handed.</summary>
        public static bool LeftHanded(string id) => id == "P06" || id == "P09" || id == "P13" || id == "P16";

        static GarmentSpec G(Garment k, string c, string c2 = "", string pat = "") => new GarmentSpec { Kind = k, Color = c, Color2 = c2, Pattern = pat };

        static void Ensure()
        {
            if (_all != null) return;
            _all = Build();
            _byId = _all.ToDictionary(c => c.Id);
        }

        static List<CastDef> Build()
        {
            var L = new List<CastDef>();

            L.Add(new CastDef
            {
                Id = "P01", Name = "김민혁", Given = "민혁", Gender = Gender.M, HeightCm = 175, Job = "진학 준비생", Contract = "없음 (계약 없이 들어온 외부인)",
                Obs = 76, Infer = 68, Argue = 57, Empathy = 90, Deceit = 31, Composure = 58,
                Core = "다정하고 눈치를 보지만 자기 판단에 책임지려 함", Flaw = "부탁을 모두 받아들이다 결정이 늦어짐", Secret = "왜 계약 없이 이곳에 왔는지는 아직 모른다",
                Likes = new[] { "빵", "협동 게임", "산책" }, Dislikes = new[] { "고함" }, Hobbies = new[] { "walk", "game" },
                P = new Persona { Morality = 0.9f, Sociability = 0.7f, Curiosity = 0.7f, Loyalty = 0.8f, Fearfulness = 0.5f, Honesty = 0.8f },
                Speech = new SpeechStyle { PoliteDefault = true, Fillers = new[] { "음", "저기" }, Exclaims = new[] { "어?", "잠깐만요" }, Laugh = "하하", VoicePitch = 1.0f },
                Look = new LookSpec { Model = "GLB:minhyuk", Hair = HairStyle.MessyShort, HairColor = "#5A3E2B", EyeColor = "#6B5B53",
                    Wear = { G(Garment.Knit, "#EDE6D6"), G(Garment.CroppedJacket, "#5C4636"), G(Garment.Pants, "#1E1E22"), G(Garment.Sneakers, "#E8E4DC") }, Acc = { Accessory.CrossBag }, Silhouette = "제공 GLB" }
            });

            L.Add(new CastDef
            {
                Id = "P02", Name = "김진우", Given = "진우", Gender = Gender.M, HeightCm = 163, Job = "심리학과 학생", Contract = "죽은 친구의 부활",
                Obs = 92, Infer = 94, Argue = 88, Empathy = 82, Deceit = 97, Composure = 79,
                Core = "타인 반응을 시험하고 내려다봄. 내키는 대로 말하고 우는 척도 하는, 갈피를 잡을 수 없는 어그로. 천진한 웃음으로 태연히 남을 괴롭히고 음흉하게 깔봄", Flaw = "통제할 수 없는 진심에 약함",
                Secret = "친구의 죽음을 막지 못했고, 자기가 한 말이 그 죽음에 영향을 줬다는 사실을 숨기고 있다",
                Likes = new[] { "사탕", "정치", "심리학 책", "사람 관찰" }, Dislikes = new[] { "뻔한 위로", "명령" }, Hobbies = new[] { "read", "observe", "game" }, FavRooms = new[] { "Library", "Lounge" },
                P = new Persona { Morality = 0.42f, Aggression = 0.45f, Sociability = 0.7f, Curiosity = 0.95f, Loyalty = 0.25f, Fearfulness = 0.2f, Ambition = 0.6f, Honesty = 0.12f, Pride = 0.85f, Jealousy = 0.4f, WishDesire = 0.8f, Romance = 0.3f, Grudge = 0.5f },
                Speech = new SpeechStyle { PoliteDefault = false, Fillers = new[] { "흐응", "글쎄", "에이" }, Exclaims = new[] { "오?", "재밌네", "훌쩍" }, Laugh = "큭큭", Verbosity = 0.5f, VoicePitch = 1.12f, VoiceRate = 1.15f },
                Look = new LookSpec { Model = "GLB:jinwoo", Hair = HairStyle.CurlyShort, HairColor = "#1C1A1C", EyeColor = "#5E5A5C", Silhouette = "제공 GLB (163cm)" }
            });

            L.Add(new CastDef
            {
                Id = "P03", Name = "한서윤", Given = "서윤", Gender = Gender.F, HeightCm = 170, Job = "대학생·학생회장", Contract = "가족이 다시 일어설 만큼의 돈",
                Obs = 80, Infer = 85, Argue = 88, Empathy = 77, Deceit = 35, Composure = 84,
                Core = "책임감·솔직함이 강함. 절망 속에서도 동료와 희망을 믿고, 믿음직스럽지 못한 민혁을 챙긴다. 산뜻한 얼굴로 엄한 말을 하지만 악의는 없다", Flaw = "도움받기를 패배처럼 여기고 한계를 늦게 인정", Secret = "이현이 벌인 일 때문에 가족 사업이 무너졌다",
                Likes = new[] { "필기구", "달지 않은 과자", "분담표", "작은 동물" }, Dislikes = new[] { "무책임", "새치기" }, Hobbies = new[] { "organize", "read", "cleanup" }, FavRooms = new[] { "Library", "Archive", "Dining" },
                P = new Persona { Morality = 0.8f, Aggression = 0.3f, Sociability = 0.7f, Curiosity = 0.6f, Loyalty = 0.9f, Fearfulness = 0.2f, Ambition = 0.6f, Honesty = 0.88f, Pride = 0.7f, Jealousy = 0.2f, WishDesire = 0.75f, Romance = 0.3f, Grudge = 0.7f },
                Speech = new SpeechStyle { PoliteDefault = true, Fillers = new[] { "그러니까" }, Exclaims = new[] { "잠깐", "정리하죠" }, Laugh = "후훗", Verbosity = 0.6f, VoicePitch = 1.25f },
                Look = new LookSpec { Hair = HairStyle.Bob, HairColor = "#1B2233", HairColor2 = "#2E3D5E", EyeColor = "#15151A", EyeSharp = 0.65f, Build = 0.3f, Skin = "#F4DCCB",
                    Wear = { G(Garment.Shirt, "#F1EFEA"), G(Garment.Blazer, "#1F2A44", "#C9A24A", "trim"), G(Garment.PleatedSkirt, "#3A3D45"), G(Garment.Tights, "#15151A"), G(Garment.Loafers, "#2A1A14") },
                    Acc = { Accessory.RibbonTie, Accessory.Armband, Accessory.Clipboard, Accessory.HairPin }, AccColor = "#B3263A", Silhouette = "단정한 수직선, 붉은 학생회 완장이 유일한 강조색" }
            });

            L.Add(new CastDef
            {
                Id = "P04", Name = "차도윤", Given = "도윤", Gender = Gender.M, HeightCm = 184, Job = "미술품 복원 보조원", Contract = "자기 범행 기록을 모두 지우는 것",
                Obs = 91, Infer = 86, Argue = 79, Empathy = 62, Deceit = 93, Composure = 94,
                Core = "예의·세심함", Flaw = "그 뒤의 통제·대상화·잔혹함", Secret = "경찰에 쫓기던 연쇄 살인범이다. 은결과는 남매 사이다",
                Likes = new[] { "오래된 액자", "도자기", "무향 차", "복원" }, Dislikes = new[] { "소음", "무례" }, Hobbies = new[] { "restore", "tea", "walk" }, FavRooms = new[] { "Gallery", "Workshop", "Lounge" },
                P = new Persona { Morality = 0.18f, Aggression = 0.55f, Sociability = 0.45f, Curiosity = 0.5f, Loyalty = 0.4f, Fearfulness = 0.1f, Ambition = 0.5f, Honesty = 0.15f, Pride = 0.6f, Jealousy = 0.3f, WishDesire = 0.7f, Romance = 0.2f, Grudge = 0.5f },
                Speech = new SpeechStyle { PoliteDefault = true, Fillers = new[] { "그렇군요" }, Exclaims = new[] { "흠" }, Laugh = "후", Verbosity = 0.45f, VoicePitch = 0.85f, VoiceRate = 0.9f },
                Look = new LookSpec { Model = "GLB:doyun", Hair = HairStyle.WolfCut, HairColor = "#141416", EyeColor = "#A9ADB2", Silhouette = "제공 GLB" }
            });

            L.Add(new CastDef
            {
                Id = "P05", Name = "백이현", Given = "이현", Gender = Gender.M, HeightCm = 178, Job = "정당 청년대변인", Contract = "자신과 얽힌 불법 장부를 모두 거둬들이는 것",
                Obs = 74, Infer = 81, Argue = 96, Empathy = 85, Deceit = 84, Composure = 82,
                Core = "사교적 중재자처럼 보임", Flaw = "자기 지위와 최소 손해 우선. 공익 언어로 사익 포장", Secret = "태겸을 통해 불법 자금을 굴렸고, 서윤 가족이 입은 피해와 가온의 폭로에도 얽혀 있다",
                Likes = new[] { "금", "수영", "발표", "인맥" }, Dislikes = new[] { "기자", "망신" }, Hobbies = new[] { "swim", "speech", "socialize" }, FavRooms = new[] { "Pool", "Lounge", "GrandHall" },
                P = new Persona { Morality = 0.4f, Aggression = 0.35f, Sociability = 0.9f, Curiosity = 0.4f, Loyalty = 0.3f, Fearfulness = 0.55f, Ambition = 0.95f, Honesty = 0.2f, Pride = 0.9f, Jealousy = 0.5f, WishDesire = 0.8f, Romance = 0.4f, Grudge = 0.6f },
                Speech = new SpeechStyle { PoliteDefault = true, Fillers = new[] { "요컨대", "말씀하신 건" }, Exclaims = new[] { "자, 자", "좋습니다" }, Laugh = "하하하", Verbosity = 0.8f, VoicePitch = 0.95f, VoiceRate = 1.05f },
                Look = new LookSpec { Hair = HairStyle.SlickedBack, HairColor = "#0E0E10", EyeColor = "#2B2320", EyeSharp = 0.6f, Build = 0.45f, Shoulders = 0.6f,
                    Wear = { G(Garment.Shirt, "#F4F4F2"), G(Garment.SuitJacket, "#B9BCC0"), G(Garment.Slacks, "#B9BCC0"), G(Garment.DressShoes, "#1A1A1A") },
                    Acc = { Accessory.Tie, Accessory.GoldTooth, Accessory.Watch, Accessory.Rings }, AccColor = "#1F8F8A", Silhouette = "기획서 유지: 올백, 밝은 회색 정장, 청록 넥타이, 금니" }
            });

            L.Add(new CastDef
            {
                Id = "P06", Name = "권태겸", Given = "태겸", Gender = Gender.M, HeightCm = 181, Job = "소규모 유통업 대표", Contract = "막대한 돈",
                Obs = 82, Infer = 87, Argue = 83, Empathy = 47, Deceit = 72, Composure = 87,
                Core = "효율·교환·약속 대가 중시", Flaw = "호의를 빚으로 받아들여 도움을 청하기 어려움", Secret = "이현의 불법 정치자금 관리에 손을 댔다",
                Likes = new[] { "쓴 초콜릿", "재고 정리", "대여표", "포장지" }, Dislikes = new[] { "공짜 호의", "지각" }, Hobbies = new[] { "organize", "trade", "read" }, FavRooms = new[] { "Storage", "Archive", "Dining" },
                P = new Persona { Morality = 0.5f, Aggression = 0.4f, Sociability = 0.5f, Curiosity = 0.45f, Loyalty = 0.5f, Fearfulness = 0.45f, Ambition = 0.85f, Honesty = 0.45f, Pride = 0.7f, Jealousy = 0.35f, WishDesire = 0.75f, Romance = 0.2f, Grudge = 0.55f },
                Speech = new SpeechStyle { PoliteDefault = true, Fillers = new[] { "계산해 보면", "조건은" }, Exclaims = new[] { "됐습니다" }, Laugh = "흠", Verbosity = 0.45f, VoicePitch = 0.9f },
                Look = new LookSpec { Hair = HairStyle.SidePart55, HairColor = "#3A2A20", EyeColor = "#3A3030", EyeSharp = 0.7f, Build = 0.35f, Shoulders = 0.55f,
                    Wear = { G(Garment.Shirt, "#E9E4D8"), G(Garment.Vest, "#2F4A3A", "", "pinstripe"), G(Garment.Slacks, "#2A2A2E"), G(Garment.DressShoes, "#3A2418") },
                    Acc = { Accessory.Briefcase, Accessory.PocketWatch, Accessory.Tie }, AccColor = "#B8893A",
                    Silhouette = "리디자인: 짙은 녹색 줄무늬 조끼+셔츠 소매 걷음, 각진 가방과 회중시계 체인. 안경 없음(사용자 지시 41: 태겸에게 안경을 씌우지 않는다)" }
            });

            L.Add(new CastDef
            {
                Id = "P07", Name = "유시온", Given = "시온", Gender = Gender.M, HeightCm = 172, Job = "래퍼", Contract = "틀어진 모든 관계가 나아지는 것",
                Obs = 89, Infer = 83, Argue = 42, Empathy = 83, Deceit = 44, Composure = 29,
                Core = "호탕하고 자신만만함. 폭언을 일삼고 입만 열면 섹드립이 쏟아진다. 생각을 거치지 않고 아무에게나", Flaw = "돈·체면·인정 집착. 속상한 말을 쌓았다가 터뜨림. 허세 뒤에 외로움을 숨김", Secret = "몸담았던 갱단의 내부 밀고자였다",
                Likes = new[] { "맥주", "돈", "모임", "독서" }, Dislikes = new[] { "무시", "밀고" }, Hobbies = new[] { "party", "music", "read" }, FavRooms = new[] { "Lounge", "MusicRoom", "Dining" },
                P = new Persona { Morality = 0.5f, Aggression = 0.8f, Sociability = 0.95f, Curiosity = 0.5f, Loyalty = 0.7f, Fearfulness = 0.45f, Ambition = 0.6f, Honesty = 0.6f, Pride = 0.92f, Jealousy = 0.55f, WishDesire = 0.5f, Romance = 0.8f, Grudge = 0.7f },
                Speech = new SpeechStyle { PoliteDefault = false, Fillers = new[] { "야", "브로", "와 씨" }, Exclaims = new[] { "와우!", "레알?", "크하하!" }, Laugh = "크하하", Verbosity = 0.85f, VoicePitch = 0.95f, VoiceRate = 1.2f },
                Look = new LookSpec { Hair = HairStyle.Dreadlocks, HairColor = "#E36A1E", HairColor2 = "#F29A4A", EyeColor = "#3A2A1E", Build = 0.45f, Skin = "#E8C5A8",
                    Wear = { G(Garment.Tshirt, "#161616"), G(Garment.FurCoat, "#D9C3A0"), G(Garment.CargoPants, "#2C2C30"), G(Garment.HighTops, "#EDEDED") },
                    Acc = { Accessory.Sunglasses, Accessory.GoldChain, Accessory.Rings, Accessory.Earrings }, AccColor = "#E3B23C", Silhouette = "기획서 유지: 주황 레게, 선글라스, 모피 코트, 금 목걸이·반지" }
            });

            L.Add(new CastDef
            {
                Id = "P08", Name = "서라온", Given = "라온", Gender = Gender.M, HeightCm = 180, Job = "인디 밴드 베이시스트", Contract = "옛 밴드 동료의 미발표 음원 소유권",
                Obs = 86, Infer = 63, Argue = 70, Empathy = 73, Deceit = 55, Composure = 70,
                Core = "허세를 싫어함. 어둡고 조용한 분위기로 사람들과 잘 어울리지 않는다. 소심하진 않고 할 말은 하지만 말투가 까칠하다", Flaw = "사람을 밀어내고 혼자 삭인다. 모욕도 짧게 받아치고 넘김", Secret = "옛 동료의 미발표 음원을 자기 것으로 만들고 싶어 한다",
                Likes = new[] { "리듬 게임", "핫팩", "베이스", "소리" }, Dislikes = new[] { "허세", "소음" }, Hobbies = new[] { "music", "game", "rest" }, FavRooms = new[] { "MusicRoom", "Corridor", "GameRoom" },
                P = new Persona { Morality = 0.62f, Aggression = 0.45f, Sociability = 0.25f, Curiosity = 0.55f, Loyalty = 0.55f, Fearfulness = 0.3f, Ambition = 0.5f, Honesty = 0.6f, Pride = 0.55f, Jealousy = 0.6f, WishDesire = 0.65f, Romance = 0.3f, Grudge = 0.55f },
                Speech = new SpeechStyle { PoliteDefault = false, Fillers = new[] { "아 뭐", "…" }, Exclaims = new[] { "헐", "하." }, Laugh = "크", Verbosity = 0.25f, VoicePitch = 0.88f, VoiceRate = 0.95f },
                Look = new LookSpec { Hair = HairStyle.ShaggyMid, HairColor = "#5E7486", HairColor2 = "#8FA6B8", EyeColor = "#44505A", EyeSharp = 0.4f, Build = 0.3f,
                    Wear = { G(Garment.Hoodie, "#4F6F6C", "", "faded"), G(Garment.DenimJacket, "#23262E"), G(Garment.Jeans, "#3B4250", "", "ripped"), G(Garment.HighTops, "#8B2F2F") },
                    Acc = { Accessory.Earphone, Accessory.BassBracelet, Accessory.Beanie }, AccColor = "#C9C9C9",
                    Silhouette = "리디자인: 회청색 덥수룩 머리+비니, 바랜 청록 후드 위 검은 청재킷, 한쪽 이어폰 선, 베이스 줄 팔찌" }
            });

            L.Add(new CastDef
            {
                Id = "P09", Name = "문재하", Given = "재하", Gender = Gender.M, HeightCm = 176, Job = "배우", Contract = "떠난 연인이 돌아오는 것",
                Obs = 78, Infer = 59, Argue = 91, Empathy = 88, Deceit = 89, Composure = 57,
                Core = "친근·화려·예의 있음", Flaw = "힘든 일과 책임을 미루고 듣기 좋은 말로 회피", Secret = "떠난 연인의 뜻과 자기 바람을 헷갈리고 있다",
                Likes = new[] { "베르베르 인형", "공연 팸플릿", "칭찬", "작은 공연" }, Dislikes = new[] { "비판", "침묵" }, Hobbies = new[] { "perform", "socialize", "music" }, FavRooms = new[] { "Theater", "MusicRoom", "Lounge" },
                P = new Persona { Morality = 0.55f, Aggression = 0.3f, Sociability = 0.9f, Curiosity = 0.45f, Loyalty = 0.45f, Fearfulness = 0.6f, Ambition = 0.6f, Honesty = 0.35f, Pride = 0.7f, Jealousy = 0.55f, WishDesire = 0.8f, Romance = 0.85f, Grudge = 0.35f },
                Speech = new SpeechStyle { PoliteDefault = false, Fillers = new[] { "있잖아", "음~" }, Exclaims = new[] { "세상에", "브라보" }, Laugh = "아하하", Verbosity = 0.75f, VoicePitch = 1.02f, VoiceRate = 1.05f },
                Look = new LookSpec { Hair = HairStyle.WavyChin, HairColor = "#9A5A2E", HairColor2 = "#C98446", EyeColor = "#6A4A2A", EyeSharp = 0.3f, Build = 0.35f,
                    Wear = { G(Garment.Turtleneck, "#EDE3CF"), G(Garment.LongCoat, "#B07C4E"), G(Garment.Slacks, "#3B2E28"), G(Garment.Boots, "#2A1C14") },
                    Acc = { Accessory.SilkScarf, Accessory.Rings, Accessory.Earrings }, AccColor = "#1E8A63",
                    Silhouette = "리디자인: 구리빛 턱선 웨이브, 에메랄드 실크 스카프, 크림 터틀넥+카멜 롱코트의 무대 배우 실루엣" }
            });

            L.Add(new CastDef
            {
                Id = "P10", Name = "강준서", Given = "준서", Gender = Gender.M, HeightCm = 187, Job = "요리사", Contract = "어떤 맛이든 재현하는 신의 식재료 ‘백야의 씨앗’",
                Obs = 73, Infer = 52, Argue = 61, Empathy = 92, Deceit = 39, Composure = 82,
                Core = "식사와 휴식을 챙기는 온화함", Flaw = "거절을 보호 요청으로 오해하고 남의 결정을 대신함", Secret = "사람을 먹이고 살리고 싶은 욕심이 남의 안전과 부딪칠 수 있다",
                Likes = new[] { "요리", "식물", "배식", "정리" }, Dislikes = new[] { "음식 낭비", "굶기" }, Hobbies = new[] { "cook", "garden", "cleanup" }, FavRooms = new[] { "Kitchen", "Dining", "Greenhouse" },
                P = new Persona { Morality = 0.8f, Aggression = 0.25f, Sociability = 0.8f, Curiosity = 0.35f, Loyalty = 0.85f, Fearfulness = 0.3f, Ambition = 0.4f, Honesty = 0.7f, Pride = 0.4f, Jealousy = 0.3f, WishDesire = 0.55f, Romance = 0.5f, Grudge = 0.3f },
                Speech = new SpeechStyle { PoliteDefault = true, Fillers = new[] { "그래도", "우선" }, Exclaims = new[] { "어이쿠" }, Laugh = "허허", Verbosity = 0.55f, VoicePitch = 0.78f, VoiceRate = 0.88f },
                Look = new LookSpec { Hair = HairStyle.Cropped, HairColor = "#1F1A17", EyeColor = "#3A2C24", EyeSharp = 0.2f, Build = 0.7f, Shoulders = 0.95f,
                    Wear = { G(Garment.Shirt, "#4E7A4A"), G(Garment.Apron, "#2F5638", "#E8E2CF", "trim"), G(Garment.Pants, "#2B2B30"), G(Garment.Boots, "#2A2320") },
                    Acc = { Accessory.Headband }, AccColor = "#E8E2CF",
                    Silhouette = "리디자인(사용자 지시 28): 가장 큰 키·넓은 어깨, 초록 셔츠 걷어 올린 소매+짙은 초록 앞치마(크림 테두리)와 크림 머리띠. 왼쪽 눈 밑에 작은 점" }
            });

            L.Add(new CastDef
            {
                Id = "P11", Name = "윤해린", Given = "해린", Gender = Gender.F, HeightCm = 168, Job = "기계공학 전문가", Contract = "결함을 알면서 넘긴 의뢰 때문에 일어난 사고를 되돌리는 것",
                Obs = 93, Infer = 90, Argue = 64, Empathy = 60, Deceit = 28, Composure = 72,
                Core = "호기심·직설성", Flaw = "고치지 못하면 자기 가치가 없다고 느껴 검증 부족을 숨길 위험", Secret = "결함을 알면서도 마감 때문에 넘긴 의뢰로 의뢰인이 죽었다",
                Likes = new[] { "태엽 장난감", "탄산수", "수리", "장치" }, Dislikes = new[] { "대충", "거짓말" }, Hobbies = new[] { "repair", "inspect", "game" }, FavRooms = new[] { "Workshop", "MachineRoom", "PowerRoom" },
                P = new Persona { Morality = 0.75f, Aggression = 0.3f, Sociability = 0.45f, Curiosity = 0.95f, Loyalty = 0.6f, Fearfulness = 0.35f, Ambition = 0.55f, Honesty = 0.8f, Pride = 0.65f, Jealousy = 0.25f, WishDesire = 0.8f, Romance = 0.3f, Grudge = 0.35f },
                Speech = new SpeechStyle { PoliteDefault = false, Fillers = new[] { "잠깐", "그러니까" }, Exclaims = new[] { "오!", "이거 봐" }, Laugh = "헤헤", Verbosity = 0.4f, VoicePitch = 1.2f, VoiceRate = 1.25f },
                Look = new LookSpec { Hair = HairStyle.BunMessy, HairColor = "#3B2418", EyeColor = "#4A3322", EyeSharp = 0.5f, Build = 0.35f,
                    Wear = { G(Garment.TankTop, "#141414"), G(Garment.WaistTiedOveralls, "#E08A1C", "", "oilstain"), G(Garment.Boots, "#3B2E24") },
                    Acc = { Accessory.Goggles, Accessory.ToolBelt, Accessory.PenEar }, AccColor = "#7FA8B8",
                    Silhouette = "리디자인: 연필 꽂은 올림머리, 이마의 보호안경, 허리에 묶은 주황 작업복과 공구 벨트" }
            });

            L.Add(new CastDef
            {
                Id = "P12", Name = "오수아", Given = "수아", Gender = Gender.F, HeightCm = 165, Job = "아이돌", Contract = "화려하게 활동에 복귀하는 것",
                Obs = 81, Infer = 58, Argue = 90, Empathy = 91, Deceit = 88, Composure = 64,
                Core = "밝고 눈치 빠름", Flaw = "갈등 회피로 양쪽에 다른 확신을 줌", Secret = "정치인의 압력으로 활동이 멈췄다 (배후는 아직 모른다)",
                Likes = new[] { "스티커", "매운 과자", "팬레터", "굿즈" }, Dislikes = new[] { "카메라 없는 고요", "무시" }, Hobbies = new[] { "perform", "socialize", "rest" }, FavRooms = new[] { "Theater", "Lounge", "Greenhouse" },
                P = new Persona { Morality = 0.55f, Aggression = 0.25f, Sociability = 0.9f, Curiosity = 0.5f, Loyalty = 0.45f, Fearfulness = 0.6f, Ambition = 0.85f, Honesty = 0.35f, Pride = 0.7f, Jealousy = 0.6f, WishDesire = 0.85f, Romance = 0.6f, Grudge = 0.45f },
                Speech = new SpeechStyle { PoliteDefault = true, Fillers = new[] { "음~", "있잖아요" }, Exclaims = new[] { "꺄", "대박" }, Laugh = "에헤헤", Verbosity = 0.65f, VoicePitch = 1.4f, VoiceRate = 1.15f },
                Look = new LookSpec { Hair = HairStyle.LongRibbon, HairColor = "#121014", EyeColor = "#3A2030", EyeSharp = 0.2f, Build = 0.25f,
                    Wear = { G(Garment.Blouse, "#FFFFFF"), G(Garment.CroppedJacket, "#F2A7C3"), G(Garment.PleatedSkirt, "#F6F2F4"), G(Garment.KneeSocks, "#FFFFFF"), G(Garment.PlatformShoes, "#1B1B1B") },
                    Acc = { Accessory.BigRibbon, Accessory.StarPins, Accessory.Choker }, AccColor = "#111111",
                    Silhouette = "리디자인: 긴 흑발에 커다란 검정 리본, 파스텔 핑크 크롭 재킷과 흰 플리츠, 별 핀" }
            });

            L.Add(new CastDef
            {
                Id = "P13", Name = "정세나", Given = "세나", Gender = Gender.F, HeightCm = 173, Job = "프로게이머", Contract = "잃은 손을 되찾는 것",
                Obs = 84, Infer = 61, Argue = 76, Empathy = 64, Deceit = 32, Composure = 86,
                Core = "행동력·정의감", Flaw = "약자를 먼저 정하고 조사 전 결론", Secret = "손목을 잃고 의수를 쓰고 있다",
                Likes = new[] { "경쟁 게임", "반복 연습", "경기 기록 검토" }, Dislikes = new[] { "비겁함", "동정" }, Hobbies = new[] { "game", "exercise", "inspect" }, FavRooms = new[] { "GameRoom", "Pool", "Corridor" },
                P = new Persona { Morality = 0.72f, Aggression = 0.55f, Sociability = 0.55f, Curiosity = 0.55f, Loyalty = 0.75f, Fearfulness = 0.2f, Ambition = 0.75f, Honesty = 0.8f, Pride = 0.75f, Jealousy = 0.35f, WishDesire = 0.8f, Romance = 0.35f, Grudge = 0.55f },
                Speech = new SpeechStyle { PoliteDefault = false, Fillers = new[] { "야", "솔직히" }, Exclaims = new[] { "좋아!", "뭐?" }, Laugh = "하", Verbosity = 0.35f, VoicePitch = 1.08f, VoiceRate = 1.15f },
                Look = new LookSpec { Hair = HairStyle.ShortRedStreak, HairColor = "#121214", HairColor2 = "#D0213A", EyeColor = "#2A1A1A", EyeSharp = 0.8f, Build = 0.35f,
                    Wear = { G(Garment.Hoodie, "#18181C", "#D0213A", "teamlogo"), G(Garment.CargoPants, "#2A2E36"), G(Garment.HighTops, "#D0213A") },
                    Acc = { Accessory.Glasses, Accessory.Headset, Accessory.ProstheticHandR }, AccColor = "#E6E6EA",
                    Silhouette = "리디자인: 붉은 브리지 짧은 흑발, 뿔테, 목에 건 헤드셋, 팀 로고 후드, 매트 흰색 일상 의수(오른손)" }
            });

            L.Add(new CastDef
            {
                Id = "P14", Name = "차은결", Given = "은결", Gender = Gender.F, HeightCm = 171, Job = "장의사", Contract = "도윤이 자신의 뜻대로 움직이는 것",
                Obs = 87, Infer = 65, Argue = 62, Empathy = 90, Deceit = 46, Composure = 90,
                Core = "슬픔을 존중하는 듯함", Flaw = "중립으로 책임 회피. 시신의 고요함에 기이한 끌림, 도윤에 대한 애착과 통제", Secret = "도윤의 범죄를 덮어 주며 감쌀지 붙잡아 둘지 갈등한다. 도윤과는 남매 사이다",
                Likes = new[] { "종이공예", "차", "산책", "말장난" }, Dislikes = new[] { "소란", "무례한 조문" }, Hobbies = new[] { "craft", "tea", "walk" }, FavRooms = new[] { "Chapel", "Greenhouse", "Library" },
                P = new Persona { Morality = 0.42f, Aggression = 0.3f, Sociability = 0.4f, Curiosity = 0.5f, Loyalty = 0.9f, Fearfulness = 0.2f, Ambition = 0.4f, Honesty = 0.45f, Pride = 0.5f, Jealousy = 0.75f, WishDesire = 0.7f, Romance = 0.2f, Grudge = 0.6f },
                Speech = new SpeechStyle { PoliteDefault = true, Fillers = new[] { "그런가요" }, Exclaims = new[] { "어머나" }, Laugh = "후후", Verbosity = 0.4f, VoicePitch = 1.05f, VoiceRate = 0.82f },
                Look = new LookSpec { Hair = HairStyle.HimeLong, HairColor = "#0B0B0D", EyeColor = "#7A7D84", EyeSharp = 0.55f, Build = 0.25f, Skin = "#F5E6DE",
                    Wear = { G(Garment.MourningDress, "#121216", "", "lace"), G(Garment.Tights, "#0E0E10"), G(Garment.Loafers, "#0E0E10") },
                    Acc = { Accessory.PearlBrooch, Accessory.LaceGloves, Accessory.LilyFlower }, AccColor = "#EDE8DA",
                    Silhouette = "리디자인: 무릎까지 오는 히메컷 흑발, 높은 칼라의 검은 상복 드레스, 진주 브로치와 레이스 장갑, 흰 백합" }
            });

            L.Add(new CastDef
            {
                Id = "P15", Name = "남가온", Given = "가온", Gender = Gender.F, HeightCm = 167, Job = "기자", Contract = "천재적인 두뇌",
                Obs = 95, Infer = 88, Argue = 82, Empathy = 58, Deceit = 59, Composure = 80,
                Core = "집요함·권위 의심·정정할 용기", Flaw = "공개 자체를 정의라 믿어 타인의 선택을 빼앗을 수 있음", Secret = "예전에 오보로 무고한 사람을 범인으로 몰아 죽음에 이르게 했다",
                Likes = new[] { "지도 낙서", "편의점 커피", "일기", "특종" }, Dislikes = new[] { "은폐", "권위" }, Hobbies = new[] { "investigate", "read", "walk" }, FavRooms = new[] { "Archive", "Library", "Corridor" },
                P = new Persona { Morality = 0.72f, Aggression = 0.4f, Sociability = 0.5f, Curiosity = 1f, Loyalty = 0.4f, Fearfulness = 0.3f, Ambition = 0.8f, Honesty = 0.6f, Pride = 0.7f, Jealousy = 0.4f, WishDesire = 0.6f, Romance = 0.2f, Grudge = 0.5f },
                Speech = new SpeechStyle { PoliteDefault = true, Fillers = new[] { "확인할게요" }, Exclaims = new[] { "잠깐만요" }, Laugh = "흐", Verbosity = 0.35f, VoicePitch = 1.15f, VoiceRate = 1.1f },
                Look = new LookSpec { Hair = HairStyle.BobBangs, HairColor = "#C1272D", EyeColor = "#3B2A20", EyeSharp = 0.75f, Build = 0.3f,
                    Wear = { G(Garment.Turtleneck, "#1D1D22"), G(Garment.TrenchCoat, "#C99A2E"), G(Garment.Slacks, "#2E2A26"), G(Garment.Boots, "#3A2A1E") },
                    Acc = { Accessory.Notebook, Accessory.Lanyard, Accessory.Beret, Accessory.PenEar }, AccColor = "#1D1D22",
                    Silhouette = "리디자인: 앞머리 있는 새빨간 단발+검은 베레모, 겨자색 트렌치코트, 목에 건 취재 명찰" }
            });

            L.Add(new CastDef
            {
                Id = "P16", Name = "신채령", Given = "채령", Gender = Gender.F, HeightCm = 169, Job = "스타일리스트", Contract = "성공한 사업체를 갖는 것",
                Obs = 88, Infer = 79, Argue = 78, Empathy = 54, Deceit = 75, Composure = 85,
                Core = "자립·경계 중시", Flaw = "남의 취향을 낮추고 실패 책임을 외부로 돌림", Secret = "사업이 망한 게 자기 탓이라는 걸 인정하지 못한다",
                Likes = new[] { "단추", "유치한 마스코트", "소품 정리", "전시" }, Dislikes = new[] { "간섭", "촌스러움" }, Hobbies = new[] { "style", "exhibit", "organize" }, FavRooms = new[] { "Wardrobe", "Gallery", "Lounge" },
                P = new Persona { Morality = 0.5f, Aggression = 0.45f, Sociability = 0.45f, Curiosity = 0.5f, Loyalty = 0.4f, Fearfulness = 0.35f, Ambition = 0.85f, Honesty = 0.4f, Pride = 0.9f, Jealousy = 0.55f, WishDesire = 0.75f, Romance = 0.35f, Grudge = 0.65f },
                Speech = new SpeechStyle { PoliteDefault = false, Fillers = new[] { "뭐", "어쨌든" }, Exclaims = new[] { "흥", "별로" }, Laugh = "풋", Verbosity = 0.4f, VoicePitch = 1.0f, VoiceRate = 0.95f },
                Look = new LookSpec { Hair = HairStyle.SideBraid, HairColor = "#3A2440", HairColor2 = "#5A3A66", EyeColor = "#4A3050", EyeSharp = 0.7f, Build = 0.3f,
                    Wear = { G(Garment.Turtleneck, "#141416"), G(Garment.CroppedJacket, "#A8246E", "", "bigshoulder"), G(Garment.Slacks, "#1A1A1E", "", "wide"), G(Garment.Boots, "#141416") },
                    Acc = { Accessory.Brooch, Accessory.TapeMeasure, Accessory.Earrings }, AccColor = "#C9CCD2",
                    Silhouette = "리디자인: 굵은 먹보라 옆 땋은머리, 어깨 각진 자홍 재킷, 목에 건 줄자와 은 브로치" }
            });

            L.Add(new CastDef
            {
                Id = "P17", Name = "송예담", Given = "예담", Gender = Gender.F, HeightCm = 160, Job = "숏폼 콘텐츠 창작자", Contract = "언니의 지금 주소와 단 한 번의 만남",
                Obs = 83, Infer = 68, Argue = 80, Empathy = 52, Deceit = 79, Composure = 45,
                Core = "상상력이 풍부함", Flaw = "모두 같은 놀이를 해야 갈등이 끝난다고 믿고 참여를 강요", Secret = "자기가 연락하지 않았을 뿐인 언니를 실종됐다고 말한다",
                Likes = new[] { "종이 모형", "엉뚱한 퀴즈", "초대장", "촬영" }, Dislikes = new[] { "거절", "지루함" }, Hobbies = new[] { "film", "party", "craft" }, FavRooms = new[] { "Lounge", "Greenhouse", "Theater" },
                P = new Persona { Morality = 0.55f, Aggression = 0.4f, Sociability = 0.95f, Curiosity = 0.8f, Loyalty = 0.5f, Fearfulness = 0.55f, Ambition = 0.6f, Honesty = 0.4f, Pride = 0.6f, Jealousy = 0.65f, WishDesire = 0.75f, Romance = 0.5f, Grudge = 0.55f },
                Speech = new SpeechStyle { PoliteDefault = false, Fillers = new[] { "있지있지", "근데에" }, Exclaims = new[] { "짜잔!", "에엥?" }, Laugh = "히히", Verbosity = 0.7f, VoicePitch = 1.5f, VoiceRate = 1.3f },
                Look = new LookSpec { Hair = HairStyle.TwinTails, HairColor = "#EFD98A", HairColor2 = "#F7ECC0", EyeColor = "#3E7A6A", EyeSharp = 0.1f, Build = 0.2f, HeadScale = 1.05f,
                    Wear = { G(Garment.Blouse, "#FFF4F8"), G(Garment.Capelet, "#8FE0C8", "", "toybuttons"), G(Garment.Skirt, "#F7C6DA", "", "layered"), G(Garment.StripedStockings, "#FFFFFF", "#8FE0C8"), G(Garment.PlatformShoes, "#F29CB8") },
                    Acc = { Accessory.Smartphone, Accessory.Lanyard, Accessory.HairPin }, AccColor = "#F29CB8",
                    Silhouette = "리디자인: 백금발 양갈래, 장난감 단추의 민트 케이프, 겹겹 파스텔 치마와 줄무늬 스타킹, 목에 건 스마트폰" }
            });

            L.Add(new CastDef
            {
                Id = "P18", Name = "임민서", Given = "민서", Gender = Gender.M, HeightCm = 182, Job = "노동자", Contract = "안정적인 집과 4년 치 생활비",
                Obs = 90, Infer = 71, Argue = 48, Empathy = 72, Deceit = 29, Composure = 88,
                Core = "과묵함·공정한 분담·맡은 일 완수", Flaw = "자신의 필요를 말하지 않아 불만이 단절로 터짐", Secret = "공부를 계속하고 싶지만, 가족에게는 공부가 싫어서 일을 택했다고 말했다",
                Likes = new[] { "산책", "숫자 퍼즐", "운반", "보온병에 담은 차" }, Dislikes = new[] { "무임승차", "허풍" }, Hobbies = new[] { "walk", "puzzle", "carry" }, FavRooms = new[] { "Storage", "Garden", "Library" },
                P = new Persona { Morality = 0.75f, Aggression = 0.35f, Sociability = 0.3f, Curiosity = 0.5f, Loyalty = 0.8f, Fearfulness = 0.25f, Ambition = 0.45f, Honesty = 0.85f, Pride = 0.55f, Jealousy = 0.3f, WishDesire = 0.65f, Romance = 0.3f, Grudge = 0.75f },
                Speech = new SpeechStyle { PoliteDefault = true, Fillers = new[] { "음" }, Exclaims = new[] { "..." }, Laugh = "허", Verbosity = 0.15f, VoicePitch = 0.8f, VoiceRate = 0.85f },
                Look = new LookSpec { Hair = HairStyle.Buzz, HairColor = "#2A2622", EyeColor = "#3A3228", EyeSharp = 0.45f, Build = 0.75f, Shoulders = 0.9f, Skin = "#DDB898",
                    Wear = { G(Garment.WorkShirt, "#7A7B5A", "", "faded"), G(Garment.Pants, "#3E3A32"), G(Garment.Boots, "#4A3A2A") },
                    Acc = { Accessory.DigitalWatch, Accessory.Thermos }, AccColor = "#5A6A4A",
                    Silhouette = "리디자인: 짧게 민 머리, 바랜 올리브 작업 셔츠 소매 걷음, 낡은 디지털시계와 보온병, 큰 체격" }
            });

            L.Add(new CastDef
            {
                Id = "NPC00", Name = "유스티", Given = "유스티", Gender = Gender.M, HeightCm = 186, Job = "집사 (하급 신)", Contract = "-",
                Obs = 80, Infer = 80, Argue = 80, Empathy = 50, Deceit = 0, Composure = 100,
                Core = "격식 있는 존댓말, 정확한 절차 언어", Flaw = "감정은 물결의 정지로만 드러남", Secret = "",
                P = new Persona { Morality = 1f, Honesty = 1f, Sociability = 0.5f },
                Speech = new SpeechStyle { PoliteDefault = true, SelfPolite = "저", Fillers = new string[0], Exclaims = new string[0], Laugh = "", Verbosity = 0.5f, VoicePitch = 0.7f, VoiceRate = 0.8f },
                Look = new LookSpec { Hair = HairStyle.None, Build = 0.3f, Shoulders = 0.6f, Skin = "#E9E4DC",
                    Wear = { G(Garment.Shirt, "#F7F4EC"), G(Garment.Vest, "#E7DDC4", "", "brocade"), G(Garment.Tailcoat, "#0E0E12", "#2A2A30", "damask"), G(Garment.Slacks, "#0E0E12"), G(Garment.DressShoes, "#050505") },
                    Acc = { Accessory.AquariumHead, Accessory.WhiteGloves, Accessory.PocketWatch, Accessory.Tie }, AccColor = "#B08A3A",
                    Silhouette = "어항 머리(물과 금붕어), 검은 연미복, 아이보리 브로케이드 조끼, 흰 장갑" }
            });

            return L;
        }
    }

    /// <summary>The 18 senior gods. Names preserved from the source; ability link is a BL23 design default.</summary>
    public static class Gods
    {
        public static readonly string[] Names = { "에이온", "벨루아", "녹티스", "오르페", "라케시", "메르카", "실레아", "팔림", "테르마", "에레보", "루미아", "네메아", "모르타", "베리타", "오블리", "하르파", "아스테르", "테세라" };
    }
}
