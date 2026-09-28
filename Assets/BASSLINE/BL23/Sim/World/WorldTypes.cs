using System;
using System.Collections.Generic;

namespace BL23.Sim
{
    public enum RoomType
    {
        Corridor, GrandHall, Dining, Kitchen, Lounge, Library, Archive, MusicRoom, Theater, Greenhouse, Infirmary, Laundry,
        Workshop, Storage, GameRoom, Pool, WaterRoom, PowerRoom, MachineRoom, Gallery, Wardrobe, Chapel, Bedroom, ButlerRoom,
        Elevator, Courtroom, Parlor, Closet, Landing, Stairwell,
        Study, TeaRoom, Courtyard, DollRoom, TrophyRoom, WineCellar, BoilerRoom, GuestRoom,
        // 용도 불명 (MysteryRoomArchetype)
        RainCorridor, EmptyAuditorium, WaitingRoom, WhiteDoors, MirrorWater, ClockMuseum,
        // 사건을 돕는 방 (BL23 살인 콘텐츠: 증거 소각·저온 보관·암실) — appended so saved ids of the rooms above keep their values
        Incinerator, ColdStorage, Darkroom,
        // 오너의 방 그림 (SocialEventsDesign §5): 행사·보물찾기·트릭의 무대 — appended so saved ids of the rooms above keep their values
        Observatory, Oracle, ContractRoom, DreamRoom, SecretStacks, Lab, PhoneRoom, Armory, Gym, Pantry
    }

    public static class RoomInfo
    {
        public static bool IsMystery(RoomType t) => t >= RoomType.RainCorridor && t <= RoomType.ClockMuseum;
        public static bool IsPassage(RoomType t) => t == RoomType.Corridor || t == RoomType.Landing || t == RoomType.GrandHall || t == RoomType.Stairwell;
        public static string Kor(RoomType t)
        {
            switch (t)
            {
                case RoomType.Corridor: return "복도";
                case RoomType.GrandHall: return "대현관 홀";
                case RoomType.Dining: return "식당";
                case RoomType.Kitchen: return "주방";
                case RoomType.Lounge: return "라운지";
                case RoomType.Library: return "도서실";
                case RoomType.Archive: return "기록실";
                case RoomType.MusicRoom: return "음악실";
                case RoomType.Theater: return "소극장";
                case RoomType.Greenhouse: return "온실 회랑";
                case RoomType.Infirmary: return "구급실";
                case RoomType.Laundry: return "세탁실";
                case RoomType.Workshop: return "공방";
                case RoomType.Storage: return "창고";
                case RoomType.GameRoom: return "게임실";
                case RoomType.Pool: return "실내 수영장";
                case RoomType.WaterRoom: return "수질 관리실";
                case RoomType.PowerRoom: return "전력실";
                case RoomType.MachineRoom: return "기계실";
                case RoomType.Gallery: return "복원 갤러리";
                case RoomType.Wardrobe: return "의상실";
                case RoomType.Chapel: return "추모 예배실";
                case RoomType.Bedroom: return "개인실";
                case RoomType.ButlerRoom: return "집사실";
                case RoomType.Elevator: return "심판장 승강기";
                case RoomType.Courtroom: return "심판장";
                case RoomType.Parlor: return "작은 응접실";
                case RoomType.Closet: return "수납실";
                case RoomType.Landing: return "2층 회랑";
                case RoomType.Stairwell: return "계단실";
                case RoomType.Study: return "서재";
                case RoomType.TeaRoom: return "다과실";
                case RoomType.Courtyard: return "달빛 안뜰";
                case RoomType.DollRoom: return "인형의 방";
                case RoomType.TrophyRoom: return "박제 전시실";
                case RoomType.WineCellar: return "와인 저장고";
                case RoomType.BoilerRoom: return "보일러실";
                case RoomType.GuestRoom: return "빈 객실";
                case RoomType.RainCorridor: return "비가 내리는 회랑";
                case RoomType.EmptyAuditorium: return "끊긴 방송의 청중석";
                case RoomType.WaitingRoom: return "도착지 없는 대기실";
                case RoomType.WhiteDoors: return "높이가 다른 문들의 흰 방";
                case RoomType.MirrorWater: return "천장을 비추는 물의 방";
                case RoomType.ClockMuseum: return "시간이 다른 시계 전시실";
                case RoomType.Incinerator: return "소각실";
                case RoomType.ColdStorage: return "저온 보관실";
                case RoomType.Darkroom: return "사진 암실";
                case RoomType.Observatory: return "관측실";
                case RoomType.Oracle: return "신탁실";
                case RoomType.ContractRoom: return "계약의 방";
                case RoomType.DreamRoom: return "꿈 기록실";
                case RoomType.SecretStacks: return "비밀 서고";
                case RoomType.Lab: return "실험실";
                case RoomType.PhoneRoom: return "전화실";
                case RoomType.Armory: return "무기고";
                case RoomType.Gym: return "체육실";
                case RoomType.Pantry: return "식료품 저장실";
            }
            return t.ToString();
        }
        /// <summary>Rooms closed by the nightly operations policy (야간 폐쇄).</summary>
        public static bool NightLocked(RoomType t) => t == RoomType.Dining || t == RoomType.Pool || t == RoomType.Kitchen || t == RoomType.Theater;
    }

    public enum Mat { Paper, Cloth, Wood, Metal, Glass, Ceramic, Stone, Plastic, Leather, Food, Flesh, Liquid, Plant }

    public enum DamageType { None, Cut, Stab, Blunt, Crush, Drown, Burn, Shock, Choke, Fall }

    [Serializable]
    public sealed class Door
    {
        public int Id;
        public int RoomA, RoomB;           // room ids
        public P3 Pos;                     // center of opening
        public bool AlongX;                // opening spans the x axis (wall runs east-west)
        public float Width = 1.3f;
        public bool Lockable;
        public bool Locked;
        public bool Sealed;                // grown shut by the hungry house (impassable for everyone until released)
        public bool Open;                  // physically ajar
        public string KeyId;               // item id of the key, null if no key
        public bool NightPolicy;           // 야간 운영 잠금 대상
        public bool Blocked;               // 가구 등으로 막힘
        public bool FixedByAbility;        // 권능 '고정'
        public long FixedUntilTick;
        public string SealedBy;            // 권능 '봉인' 소유자
        public bool SealBroken;
        public List<string> LockLog = new List<string>(); // A-ledger reference ids
    }

    [Serializable]
    public sealed class Stair
    {
        public int Id; public string Name;
        public P3 A, B;             // endpoints on different floors
        public int RoomA, RoomB;
        public float Seconds = 6f;  // traversal time (sim seconds)
        public bool Grand;
    }

    [Serializable]
    public sealed class Spot
    {
        public int Id; public int Room; public int Furniture = -1;
        public P3 Pos; public float Yaw;
        public P3 Approach;         // free standing point used for pathing (seat/bed spots are on the furniture)
        public bool OnFurniture;
        public string Tag;          // "sit","eat","cook","read","work","sleep","stand","view","play","swim","pray","perform"...
        public string Occupant;     // actor id reserved
    }

    [Serializable]
    public sealed class Furniture
    {
        public int Id; public int Room;
        public string Type;         // key into FurnitureCatalog
        public P3 Pos; public float Yaw;
        public float W, D, H;       // world footprint after rotation handled by generator (W along local x)
        public bool Blocks = true;
        public Mat Material = Mat.Wood;
        public int Variant;
        public string Tint;         // color override (dream palette)
        // physical state
        public int Damage;          // 0 intact .. 3 broken
        public List<string> Marks = new List<string>();
        public bool Moved; public P3 Origin;
        // Sim/Systems/FurnitureChanges.cs: every committed change bumps Rev (people remember the Rev they last took in)
        public int Rev;
    }

    [Serializable]
    public sealed class Room
    {
        public int Id; public RoomType Type; public string Name;
        public int Floor; public RectF Rect;
        public float CeilingH = 4f;
        public string Owner;                 // bedroom owner
        public int Circuit;                  // power circuit id
        public int SoundZone;
        public string Palette;               // dream palette id
        public int Variant;                  // interior variant
        public int MysteryGimmick;           // mystery room gimmick selector
        public float BaseLight = 1f;         // designed light level 0..1
        public List<int> Doors = new List<int>();
        public List<int> Furniture = new List<int>();
        public List<int> Spots = new List<int>();
        public bool Exterior;                // has exterior windows
        public bool Void;                    // open to below (hall on 2F)
        public string Note;                  // 공개 가능한 방 설명
    }

    [Serializable]
    public sealed class Circuit
    {
        public int Id; public string Name; public bool On = true; public bool Emergency;
        public List<int> Rooms = new List<int>();
    }

    public sealed class FurnitureDef
    {
        public string Type; public string Kor; public float W, D, H; public Mat Mat; public bool Blocks = true; public string[] Spots = new string[0];
        public bool Movable; public float Mass = 30f; public bool Fragile;
    }

    public static class FurnitureCatalog
    {
        static Dictionary<string, FurnitureDef> _d;
        public static FurnitureDef Get(string t) { Ensure(); return _d.TryGetValue(t, out var d) ? d : null; }
        public static IEnumerable<FurnitureDef> All { get { Ensure(); return _d.Values; } }
        static void A(string t, string k, float w, float d, float h, Mat m, bool movable = false, float mass = 30, bool fragile = false, bool blocks = true, params string[] spots)
            => _d[t] = new FurnitureDef { Type = t, Kor = k, W = w, D = d, H = h, Mat = m, Movable = movable, Mass = mass, Fragile = fragile, Blocks = blocks, Spots = spots };
        static void Ensure()
        {
            if (_d != null) return; _d = new Dictionary<string, FurnitureDef>();
            A("LongTable", "긴 식탁", 5.6f, 1.4f, 0.78f, Mat.Wood, false, 120);
            A("Chair", "의자", 0.55f, 0.55f, 0.95f, Mat.Wood, true, 7, false, false, "sit");
            A("Armchair", "안락의자", 0.95f, 0.9f, 1.05f, Mat.Cloth, true, 25, false, true, "sit");
            A("Sofa", "소파", 2.2f, 0.95f, 0.9f, Mat.Cloth, true, 60, false, true, "sit", "sit");
            A("CoffeeTable", "낮은 탁자", 1.2f, 0.7f, 0.45f, Mat.Wood, true, 20);
            A("RoundTable", "원탁", 1.3f, 1.3f, 0.76f, Mat.Wood, true, 35);
            A("Sideboard", "찬장", 2.0f, 0.55f, 0.95f, Mat.Wood, false, 80);
            A("Counter", "조리대", 2.4f, 0.7f, 0.92f, Mat.Stone, false, 200, false, true, "cook");
            A("Stove", "화덕", 1.2f, 0.8f, 0.92f, Mat.Metal, false, 150, false, true, "cook");
            A("Sink", "개수대", 1.2f, 0.7f, 0.92f, Mat.Metal, false, 80, false, true, "wash");
            A("Fridge", "냉장고", 0.9f, 0.8f, 2.0f, Mat.Metal, false, 110);
            A("KnifeRack", "칼꽂이", 0.5f, 0.3f, 0.35f, Mat.Wood, true, 3, false, false);
            A("Island", "아일랜드 조리대", 2.2f, 1.0f, 0.92f, Mat.Stone, false, 250, false, true, "cook");
            A("Bookshelf", "책장", 2.0f, 0.45f, 2.6f, Mat.Wood, false, 120);
            A("ReadingTable", "열람대", 1.8f, 0.9f, 0.76f, Mat.Wood, false, 60, false, true, "read", "read");
            A("Fireplace", "벽난로", 2.2f, 0.7f, 1.6f, Mat.Stone, false, 900);
            A("Piano", "그랜드 피아노", 1.6f, 2.0f, 1.0f, Mat.Wood, false, 400, false, true, "play");
            A("MusicStand", "보면대", 0.5f, 0.4f, 1.2f, Mat.Metal, true, 3, false, false, "play");
            A("Drums", "드럼 세트", 1.8f, 1.4f, 1.1f, Mat.Metal, false, 60, false, true, "play");
            A("Stage", "무대", 6.0f, 3.2f, 0.6f, Mat.Wood, false, 2000, false, false, "perform", "perform");
            A("Seats", "객석 의자", 5.0f, 0.8f, 0.9f, Mat.Cloth, false, 120, false, true, "sit", "sit", "sit");
            A("CostumeRack", "의상 걸이", 1.8f, 0.6f, 1.8f, Mat.Metal, true, 25);
            A("Mirror", "전신 거울", 0.9f, 0.2f, 2.0f, Mat.Glass, true, 15, true);
            A("Planter", "화분대", 2.0f, 0.8f, 0.8f, Mat.Stone, false, 300, false, true, "garden");
            A("Bench", "벤치", 1.8f, 0.5f, 0.45f, Mat.Wood, true, 25, false, true, "sit", "sit");
            A("Bed", "침대", 1.4f, 2.1f, 0.6f, Mat.Cloth, false, 70, false, true, "sleep");
            A("Desk", "책상", 1.2f, 0.6f, 0.76f, Mat.Wood, true, 30, false, true, "work");
            A("Wardrobe", "옷장", 1.2f, 0.6f, 2.1f, Mat.Wood, false, 90);
            A("Nightstand", "협탁", 0.45f, 0.4f, 0.55f, Mat.Wood, true, 8);
            A("InfirmaryBed", "간이 침대", 0.9f, 2.0f, 0.7f, Mat.Metal, true, 40, false, true, "rest");
            A("MedCabinet", "구급 약장", 1.0f, 0.4f, 1.9f, Mat.Metal, false, 50, true);
            A("Washer", "세탁기", 0.7f, 0.7f, 0.9f, Mat.Metal, false, 70, false, true, "laundry");
            A("DryRack", "건조대", 1.6f, 0.6f, 1.5f, Mat.Metal, true, 6);
            A("Workbench", "작업대", 2.2f, 0.9f, 0.9f, Mat.Wood, false, 110, false, true, "work", "work");
            A("ToolWall", "공구 벽", 2.0f, 0.2f, 2.0f, Mat.Metal, false, 40, false, false);
            A("Crates", "나무 상자", 1.2f, 1.2f, 1.2f, Mat.Wood, true, 40);
            A("Shelves", "선반", 2.0f, 0.6f, 2.2f, Mat.Metal, false, 60);
            A("GameTable", "게임 탁자", 1.6f, 1.0f, 0.8f, Mat.Wood, false, 50, false, true, "play", "play");
            A("Arcade", "아케이드 기기", 0.8f, 0.9f, 1.8f, Mat.Plastic, false, 90, true, true, "play");
            A("PoolTable", "당구대", 2.6f, 1.5f, 0.85f, Mat.Wood, false, 300, false, true, "play");
            A("PoolWater", "수영장 물", 10f, 6f, -1.6f, Mat.Liquid, false, 0, false, true);
            A("Lounger", "선베드", 0.7f, 1.9f, 0.4f, Mat.Plastic, true, 10, false, true, "rest");
            A("PumpUnit", "펌프 설비", 1.6f, 1.0f, 1.4f, Mat.Metal, false, 400);
            A("Terminal", "관리 단말", 0.8f, 0.5f, 1.3f, Mat.Metal, false, 60, true, true, "operate");
            A("Switchboard", "배전반", 2.4f, 0.4f, 2.0f, Mat.Metal, false, 200, false, true, "operate");
            A("Generator", "발전기", 2.0f, 1.4f, 1.6f, Mat.Metal, false, 900);
            A("Press", "유압 프레스", 2.4f, 2.0f, 2.8f, Mat.Metal, false, 5000);
            A("PressConsole", "프레스 조작부", 0.9f, 0.6f, 1.2f, Mat.Metal, false, 80, true, true, "operate");
            A("Pipes", "배관", 3.0f, 0.4f, 2.5f, Mat.Metal, false, 200, false, false);
            A("Easel", "이젤", 0.8f, 0.8f, 1.8f, Mat.Wood, true, 6, false, true, "work");
            A("Pedestal", "전시 받침대", 0.8f, 0.8f, 1.1f, Mat.Stone, true, 60);
            A("FrameWall", "액자 벽", 3.0f, 0.15f, 2.4f, Mat.Wood, false, 30, true, false);
            A("DressForm", "마네킹", 0.6f, 0.6f, 1.7f, Mat.Cloth, true, 8);
            A("VanityDesk", "화장대", 1.4f, 0.6f, 1.6f, Mat.Wood, false, 40, true, true, "style");
            A("Pew", "예배 의자", 3.0f, 0.7f, 1.0f, Mat.Wood, false, 60, false, true, "pray", "pray");
            A("Altar", "추모 제단", 2.0f, 1.0f, 1.2f, Mat.Stone, false, 700, false, true, "pray");
            A("Candelabra", "촛대", 0.5f, 0.5f, 1.6f, Mat.Metal, true, 6, false, false);
            A("Statue", "조각상", 1.0f, 1.0f, 2.4f, Mat.Stone, false, 900);
            A("Chandelier", "샹들리에", 2.0f, 2.0f, 1.5f, Mat.Glass, false, 120, true, false);
            A("Clock", "괘종시계", 0.6f, 0.4f, 2.2f, Mat.Wood, false, 60, true);
            A("ClockCase", "시계 진열장", 2.0f, 0.6f, 1.8f, Mat.Glass, false, 90, true);
            A("FileCabinet", "문서 보관함", 1.0f, 0.6f, 1.4f, Mat.Metal, false, 70);
            A("Recorder", "녹음 장치 거치대", 0.6f, 0.4f, 1.0f, Mat.Metal, true, 5, true, false);
            A("DoorLogger", "출입 기록기", 0.3f, 0.15f, 0.5f, Mat.Metal, false, 4, true, false);
            A("Plant", "관엽 식물", 0.7f, 0.7f, 1.6f, Mat.Plant, true, 15);
            A("Rug", "러그", 3.0f, 2.0f, 0.02f, Mat.Cloth, true, 10, false, false);
            A("DoorFrameFree", "홀로 선 문", 1.2f, 0.3f, 2.6f, Mat.Wood, false, 50, false, true);
            A("AudSeat", "청중석 의자", 0.6f, 0.6f, 0.9f, Mat.Cloth, false, 12, false, true, "sit");
            A("Speaker", "방송 스피커", 0.8f, 0.6f, 1.6f, Mat.Metal, false, 30, true);
            A("WaitBench", "대기실 긴 의자", 3.0f, 0.6f, 0.9f, Mat.Metal, false, 40, false, true, "sit", "sit");
            A("TicketBooth", "안내 창구", 1.6f, 1.0f, 1.3f, Mat.Wood, false, 90);
            A("ShallowWater", "얕은 물", 6f, 5f, 0.15f, Mat.Liquid, false, 0, false, false);
            A("RainFrame", "빗줄기 기둥", 0.4f, 0.4f, 3.5f, Mat.Glass, false, 20, false, true);
            A("ButlerDesk", "집사 진행대", 1.6f, 0.8f, 1.1f, Mat.Wood, false, 80);
            A("TrialStand", "증언대", 0.9f, 0.7f, 1.1f, Mat.Wood, false, 60, false, true, "stand");
            A("GrandStair", "대계단", 6f, 10f, 4.8f, Mat.Stone, false, 9999, false, true);
            A("Aquarium", "수조 벽", 3.0f, 0.8f, 2.2f, Mat.Glass, false, 600, true);
            A("Piano_Upright", "업라이트 피아노", 1.5f, 0.6f, 1.3f, Mat.Wood, false, 220, false, true, "play");
            A("Organ", "파이프 오르간", 3.0f, 1.0f, 3.2f, Mat.Wood, false, 1500, false, true, "play");
            A("Cart", "운반 카트", 1.0f, 0.6f, 0.9f, Mat.Metal, true, 18, false, false);
            A("Trolley", "배식 카트", 0.9f, 0.5f, 0.9f, Mat.Metal, true, 15, false, false);
            A("Fountain", "마른 분수", 2.4f, 2.4f, 1.4f, Mat.Stone, false, 2000);
            A("Dollhouse", "거대한 인형의 집", 1.8f, 1.0f, 1.9f, Mat.Wood, false, 90, true);
            A("DollShelf", "인형 진열장", 2.0f, 0.5f, 2.2f, Mat.Wood, false, 70, true);
            A("Barrel", "술통", 0.8f, 0.8f, 1.0f, Mat.Wood, true, 60);
            A("WineRack", "와인 선반", 2.2f, 0.5f, 2.2f, Mat.Wood, false, 90, true);
            A("Boiler", "보일러", 1.8f, 1.4f, 2.2f, Mat.Metal, false, 1500);
            A("StuffedBeast", "박제 짐승", 1.4f, 0.8f, 1.6f, Mat.Cloth, false, 60);
            A("TeaCart", "찻잔 카트", 0.9f, 0.5f, 0.9f, Mat.Metal, true, 14, true, false);
            // activity furniture: every big room offers several things to do
            A("BarCounter", "바 카운터", 3.0f, 0.75f, 1.1f, Mat.Wood, false, 300, false, true);
            A("BarStool", "바 의자", 0.45f, 0.45f, 0.8f, Mat.Metal, true, 6, false, false, "sit");
            A("Gramophone", "축음기", 0.6f, 0.6f, 1.25f, Mat.Wood, true, 14, true, true, "listen");
            A("Jukebox", "주크박스", 0.95f, 0.65f, 1.65f, Mat.Plastic, false, 120, true, true, "listen");
            A("Dartboard", "다트판", 0.7f, 0.12f, 1.8f, Mat.Wood, false, 6, false, false, "play");
            A("ChessTable", "체스 탁자", 0.8f, 0.8f, 0.75f, Mat.Wood, true, 20, false, true, "play", "play");
            A("Globe", "지구의", 0.7f, 0.7f, 1.2f, Mat.Wood, true, 12, false, true, "view");
            A("DivingBoard", "다이빙대", 0.7f, 2.4f, 1.0f, Mat.Metal, false, 150, false, true, "swim");
            A("FilmProjector", "영사기", 0.7f, 0.9f, 1.35f, Mat.Metal, true, 25, true, true, "operate");
            A("Telescope", "망원경", 0.8f, 0.8f, 1.6f, Mat.Metal, true, 10, true, true, "view");
            A("Harp", "하프", 0.8f, 0.6f, 1.8f, Mat.Wood, true, 25, false, true, "play");
            A("Lectern", "강대상", 0.7f, 0.6f, 1.2f, Mat.Wood, true, 20, false, true, "perform");
            A("SewingTable", "재봉틀 탁자", 1.1f, 0.6f, 0.95f, Mat.Wood, false, 40, false, true, "work");
            A("BirdCage", "기계새 새장", 0.7f, 0.7f, 1.9f, Mat.Metal, true, 12, true, true, "view");
            A("PunchingBag", "샌드백", 0.6f, 0.6f, 1.9f, Mat.Cloth, false, 40, false, true, "play");
            A("DisplayCase", "유리 진열장", 1.6f, 0.6f, 1.9f, Mat.Glass, false, 90, true, true, "view");
            A("CardCatalog", "도서 목록함", 1.2f, 0.55f, 1.3f, Mat.Wood, false, 70, false, true, "read");
            // dressing furniture: completes seating groups, bedsides and wall compositions (lived-in rooms)
            A("SideTable", "보조 탁자", 0.5f, 0.5f, 0.66f, Mat.Wood, true, 8, false, true);
            A("Console", "콘솔 탁자", 1.4f, 0.45f, 0.86f, Mat.Wood, false, 45, false, true);
            A("Ottoman", "발받침 의자", 0.8f, 0.55f, 0.45f, Mat.Leather, true, 12, false, true, "sit");
            A("DayBed", "긴 의자", 1.9f, 0.82f, 0.95f, Mat.Cloth, false, 45, false, true, "sit", "sit");
            A("RockingChair", "흔들의자", 0.7f, 0.85f, 1.0f, Mat.Wood, true, 12, false, true, "sit");
            A("Chest", "낡은 궤짝", 0.95f, 0.55f, 0.62f, Mat.Wood, false, 40, false, true);
            A("Cabinet", "장식장", 2.0f, 0.62f, 2.2f, Mat.Wood, false, 120, true, true);
            A("FloorLamp", "스탠드 등", 0.42f, 0.42f, 1.7f, Mat.Metal, true, 6, true, false);
            // rooms that help a crime (BL23 murder content): the incinerator's furnace, the cold store's racks, the darkroom's trays
            A("Incinerator", "소각로", 1.6f, 1.3f, 2.2f, Mat.Metal, false, 1200, false, true, "operate");
            A("ColdLocker", "냉장 보관대", 2.2f, 0.7f, 2.1f, Mat.Metal, false, 180);
            A("DevelopTable", "현상 작업대", 2.0f, 0.8f, 0.95f, Mat.Metal, false, 90, false, true, "work");
        }
    }

    [Serializable]
    public sealed class ItemDef
    {
        public string Type; public string Kor; public Mat Mat; public float Mass; public float Size;
        public DamageType Dmg = DamageType.None; public float Reach = 0.6f; public int Sev = 0; // base severity 0..4
        public bool TwoHanded; public bool Light; public bool Key; public bool Cleanable = true; public bool Wearable; public bool Container;
        public bool Heavy; public bool Consumable; public string Tag = "";
        public bool IsWeapon => Dmg != DamageType.None && Sev > 0;
    }

    public static class ItemCatalog
    {
        static Dictionary<string, ItemDef> _d;
        public static ItemDef Get(string t) { Ensure(); return t != null && _d.TryGetValue(t, out var d) ? d : null; }
        public static IEnumerable<ItemDef> All { get { Ensure(); return _d.Values; } }
        static ItemDef A(string t, string k, Mat m, float mass, float size, DamageType dmg = DamageType.None, int sev = 0, float reach = 0.6f)
        { var d = new ItemDef { Type = t, Kor = k, Mat = m, Mass = mass, Size = size, Dmg = dmg, Sev = sev, Reach = reach }; _d[t] = d; return d; }
        static void Ensure()
        {
            if (_d != null) return; _d = new Dictionary<string, ItemDef>();
            A("KitchenKnife", "식칼", Mat.Metal, 0.2f, 0.33f, DamageType.Stab, 3, 0.7f);
            A("Cleaver", "고기 칼", Mat.Metal, 0.6f, 0.35f, DamageType.Cut, 3, 0.7f);
            A("RollingPin", "밀대", Mat.Wood, 0.8f, 0.45f, DamageType.Blunt, 2, 0.8f);
            A("FryingPan", "프라이팬", Mat.Metal, 1.4f, 0.5f, DamageType.Blunt, 2, 0.8f);
            A("Wrench", "렌치", Mat.Metal, 1.1f, 0.35f, DamageType.Blunt, 3, 0.75f);
            A("Hammer", "망치", Mat.Metal, 0.9f, 0.33f, DamageType.Blunt, 3, 0.75f);
            A("Chisel", "끌", Mat.Metal, 0.25f, 0.25f, DamageType.Stab, 2, 0.6f);
            A("Scissors", "재단 가위", Mat.Metal, 0.3f, 0.25f, DamageType.Stab, 2, 0.6f);
            A("PaletteKnife", "복원용 나이프", Mat.Metal, 0.1f, 0.22f, DamageType.Cut, 2, 0.6f);
            A("Candlestick", "은 촛대", Mat.Metal, 1.6f, 0.45f, DamageType.Blunt, 3, 0.85f);
            A("FirePoker", "부지깽이", Mat.Metal, 1.3f, 0.8f, DamageType.Stab, 3, 1.1f);
            A("Trophy", "트로피", Mat.Metal, 2.0f, 0.35f, DamageType.Blunt, 3, 0.7f);
            A("Bottle", "유리병", Mat.Glass, 0.7f, 0.3f, DamageType.Blunt, 2, 0.7f);
            A("Vase", "꽃병", Mat.Ceramic, 1.2f, 0.4f, DamageType.Blunt, 2, 0.7f);
            A("Rope", "밧줄", Mat.Cloth, 0.8f, 0.4f, DamageType.Choke, 3, 0.5f).Tag = "bind";
            A("Scarf", "스카프", Mat.Cloth, 0.2f, 0.3f, DamageType.Choke, 2, 0.5f);
            A("PipeSection", "쇠파이프", Mat.Metal, 2.2f, 0.9f, DamageType.Blunt, 3, 1.1f);
            A("Book", "두꺼운 책", Mat.Paper, 1.2f, 0.3f, DamageType.Blunt, 1, 0.6f);
            A("Cup", "찻잔", Mat.Ceramic, 0.2f, 0.1f);
            A("Plate", "접시", Mat.Ceramic, 0.4f, 0.25f);
            A("WineGlass", "와인잔", Mat.Glass, 0.15f, 0.2f);
            var fl = A("Flashlight", "손전등", Mat.Metal, 0.4f, 0.25f, DamageType.Blunt, 1, 0.6f); fl.Light = true;
            A("FirstAidKit", "구급상자", Mat.Plastic, 1.2f, 0.35f).Tag = "rescue";
            A("Towel", "수건", Mat.Cloth, 0.3f, 0.3f).Tag = "clean";
            A("Bucket", "양동이", Mat.Plastic, 1.0f, 0.35f).Tag = "clean";
            A("Mop", "대걸레", Mat.Wood, 1.2f, 1.3f, DamageType.Blunt, 1, 1.2f).Tag = "clean";
            A("Bleach", "세정제", Mat.Plastic, 1.0f, 0.25f).Tag = "clean";
            A("Sheet", "침대 시트", Mat.Cloth, 1.0f, 0.5f).Tag = "wrap";
            var mk = A("MasterKey", "예비 열쇠 꾸러미", Mat.Metal, 0.2f, 0.1f); mk.Key = true;
            var rk = A("RoomKey", "방 열쇠", Mat.Metal, 0.05f, 0.08f); rk.Key = true;
            A("Recorder", "소형 녹음기", Mat.Plastic, 0.2f, 0.12f).Tag = "record";
            A("Camera", "즉석 카메라", Mat.Plastic, 0.5f, 0.15f).Tag = "photo";
            var mask = A("TheaterMask", "무대 가면", Mat.Plastic, 0.2f, 0.25f); mask.Wearable = true; mask.Tag = "disguise";
            var cloak = A("Cloak", "무대 망토", Mat.Cloth, 1.2f, 0.6f); cloak.Wearable = true; cloak.Tag = "disguise";
            var coat = A("Raincoat", "비옷", Mat.Plastic, 0.8f, 0.6f); coat.Wearable = true; coat.Tag = "disguise";
            var apron = A("SpareApron", "여벌 앞치마", Mat.Cloth, 0.3f, 0.4f); apron.Wearable = true;
            A("Envelope", "봉투", Mat.Paper, 0.05f, 0.2f).Tag = "document";
            A("Document", "문서", Mat.Paper, 0.05f, 0.25f).Tag = "document";
            A("Thermos", "보온병", Mat.Metal, 0.6f, 0.28f, DamageType.Blunt, 1, 0.6f);
            A("Notebook", "수첩", Mat.Paper, 0.15f, 0.15f);
            A("Candy", "사탕", Mat.Food, 0.01f, 0.03f).Consumable = true;
            A("Chocolate", "쓴 초콜릿", Mat.Food, 0.05f, 0.1f).Consumable = true;
            A("Bread", "따뜻한 빵", Mat.Food, 0.2f, 0.2f).Consumable = true;
            A("Snack", "매운 과자", Mat.Food, 0.1f, 0.2f).Consumable = true;
            A("Soda", "탄산수", Mat.Glass, 0.4f, 0.22f).Consumable = true;
            A("Beer", "맥주", Mat.Glass, 0.4f, 0.22f).Consumable = true;
            A("Tea", "무향 차", Mat.Food, 0.1f, 0.1f).Consumable = true;
            A("Sticker", "스티커", Mat.Paper, 0.01f, 0.05f);
            A("WindupToy", "태엽 장난감", Mat.Metal, 0.1f, 0.08f);
            A("PaperModel", "종이 모형", Mat.Paper, 0.05f, 0.2f);
            A("Button", "고풍 단추", Mat.Metal, 0.01f, 0.02f);
            A("HandWarmer", "핫팩", Mat.Plastic, 0.05f, 0.1f);
            A("Flower", "꽃", Mat.Plant, 0.05f, 0.3f);
            A("Fragment", "파편", Mat.Glass, 0.05f, 0.1f, DamageType.Cut, 1, 0.4f);
            A("Invitation", "초대장", Mat.Paper, 0.02f, 0.15f).Tag = "document";
            A("Fuse", "퓨즈", Mat.Metal, 0.05f, 0.06f);
            A("Tripwire", "낚싯줄 뭉치", Mat.Plastic, 0.05f, 0.08f).Tag = "trap";
            A("Thread", "튼튼한 재봉실 타래", Mat.Cloth, 0.05f, 0.07f).Tag = "thread";
            A("PoisonVial", "디기탈리스 추출액", Mat.Glass, 0.1f, 0.08f).Tag = "poison";
            A("Sedative", "수면 유도제", Mat.Plastic, 0.02f, 0.05f).Tag = "sedate";
            // BL23 murder content: weapons and tools that live in particular rooms (Methods.SpawnLoop places them every loop)
            A("LetterOpener", "은제 종이칼", Mat.Metal, 0.12f, 0.22f, DamageType.Stab, 2, 0.6f);
            A("Statuette", "청동 흉상", Mat.Metal, 3.2f, 0.34f, DamageType.Blunt, 3, 0.7f).Heavy = true;
            A("Bookend", "대리석 북엔드", Mat.Stone, 2.4f, 0.2f, DamageType.Blunt, 3, 0.65f);
            A("Decanter", "크리스털 디캔터", Mat.Glass, 1.6f, 0.32f, DamageType.Blunt, 2, 0.7f);
            A("IcePick", "얼음 송곳", Mat.Metal, 0.15f, 0.22f, DamageType.Stab, 3, 0.6f);
            A("GardenShears", "전정 가위", Mat.Metal, 1.1f, 0.5f, DamageType.Cut, 3, 0.8f);
            A("Crowbar", "쇠지렛대", Mat.Metal, 2.6f, 0.75f, DamageType.Blunt, 3, 1.0f).Heavy = true;
            A("Iron", "무쇠 다리미", Mat.Metal, 2.8f, 0.25f, DamageType.Blunt, 3, 0.6f);
            A("Scalpel", "수술용 메스", Mat.Metal, 0.03f, 0.12f, DamageType.Cut, 3, 0.55f);
            A("CueStick", "당구 큐", Mat.Wood, 0.55f, 1.45f, DamageType.Blunt, 2, 1.35f);
            A("SkinningKnife", "박제용 칼", Mat.Metal, 0.2f, 0.3f, DamageType.Cut, 3, 0.65f);
            A("CurtainCord", "커튼 끈", Mat.Cloth, 0.25f, 0.3f, DamageType.Choke, 3, 0.5f).Tag = "cord";
            A("PianoWire", "피아노 줄", Mat.Metal, 0.05f, 0.08f, DamageType.Choke, 3, 0.45f).Tag = "cord";
            A("ExtensionCord", "연장 전선", Mat.Plastic, 0.7f, 0.32f, DamageType.Choke, 2, 0.5f).Tag = "cable";
            A("Pliers", "절연 펜치", Mat.Metal, 0.35f, 0.2f, DamageType.Blunt, 1, 0.55f).Tag = "tool";
            A("Hacksaw", "쇠톱", Mat.Metal, 0.6f, 0.55f, DamageType.Cut, 2, 0.7f).Tag = "saw";
            A("BoneSaw", "정육용 뼈 톱", Mat.Metal, 0.9f, 0.6f, DamageType.Cut, 2, 0.75f).Tag = "saw";
            A("SeveredPart", "천에 싼 꾸러미", Mat.Flesh, 4.0f, 0.55f).Tag = "part";   // a cut-off piece of a body, wrapped (interim model until Gore.Dismember owns the pieces)
            A("Pillow", "베개", Mat.Cloth, 0.6f, 0.5f).Tag = "smother";
            A("DevChemical", "현상 약품 병", Mat.Glass, 0.4f, 0.12f).Tag = "poison";
            A("Foxglove", "디기탈리스 잎 뭉치", Mat.Plant, 0.02f, 0.1f).Tag = "poison";
            // --- violence track (Sim/Violence): gothic-era firearms and a crossbow (Tag firearm/crossbow: fired, not swung — as a
            // club they are only a butt), their ammunition (kept in other rooms), a binding tape, and what violence leaves behind
            A("Revolver", "은장 리볼버", Mat.Metal, 1.1f, 0.3f, DamageType.Blunt, 2, 0.65f).Tag = "firearm";
            A("DuelingPistol", "결투용 권총", Mat.Metal, 1.3f, 0.42f, DamageType.Blunt, 2, 0.7f).Tag = "firearm";
            { var sg = A("HuntingShotgun", "쌍열 엽총", Mat.Metal, 3.4f, 1.15f, DamageType.Blunt, 2, 1.1f); sg.Tag = "firearm"; sg.TwoHanded = true; sg.Heavy = true; }
            { var xb = A("Crossbow", "사냥용 석궁", Mat.Wood, 3.2f, 0.85f, DamageType.Blunt, 2, 0.9f); xb.Tag = "crossbow"; xb.TwoHanded = true; }
            A("Cartridges", "리볼버 탄약 상자", Mat.Metal, 0.4f, 0.12f).Tag = "ammo";
            A("ShotShells", "엽총 탄 상자", Mat.Paper, 0.5f, 0.14f).Tag = "ammo";
            A("PowderFlask", "화약통과 납탄 주머니", Mat.Metal, 0.4f, 0.12f).Tag = "ammo";
            A("BoltQuiver", "석궁 화살 묶음", Mat.Leather, 0.6f, 0.45f).Tag = "ammo";
            A("Bolt", "석궁 화살", Mat.Wood, 0.08f, 0.4f, DamageType.Stab, 1, 0.5f).Tag = "bolt";
            A("Tape", "절연 테이프", Mat.Plastic, 0.1f, 0.08f).Tag = "bind";
            A("HairStrands", "머리카락 한 움큼", Mat.Cloth, 0.005f, 0.05f).Tag = "trace";
            A("SpentShell", "빈 엽총 탄피", Mat.Metal, 0.02f, 0.03f).Tag = "trace";
            A("Wadding", "그을린 종이 패치", Mat.Paper, 0.005f, 0.03f).Tag = "trace";
            // --- violence track (end)
        }
    }

    /// <summary>A portable object instance with physical history. Location is a single union: held / in container / on floor.
    /// (partial: where a concealed or stashed thing is kept — Worn, StashF — lives in Systems/Concealment.cs)</summary>
    [Serializable]
    public sealed partial class Item
    {
        public string Id; public string Type; public string Name;
        public string Holder;              // actor id holding it (hand or pocket)
        public bool InHand;
        public P3 Pos; public int Room = -1; public float Yaw;
        public string Owner;               // original owner (personal items)
        public int HomeRoom = -1; public P3 HomePos; // where it belongs
        public int Damage;                 // 0..3
        public bool Bloody; public bool Washed; public bool Wet; public bool Hidden;
        public string ParentItem;          // fragment origin
        public List<string> Surface = new List<string>();  // "blood","residue","fingerprint-ish smudge","scratch","dent"...
        public string KeyFor;              // door / room scope if key
        public string LastUser;            // restricted
        public long LastMovedTick;
        public string MarkedBy; public string SealedBy;
        public int WeightClassOverride;    // 권능 '무게'
        public long WeightUntil;
        // recorder (IG03): one stored clip, optional delayed playback
        public string RecVoice; public double RecAt = -1; public string RecText; public double PlayAt = -1; public double PlayedAt = -1; public string ArmedBy;
        // written notes / invitations (IG02, IG10)
        public string Note; public string NoteFrom; public int NoteRev;
        public ItemDef Def => ItemCatalog.Get(Type);
        public string Kor => Name ?? Def?.Kor ?? Type;
    }
}
