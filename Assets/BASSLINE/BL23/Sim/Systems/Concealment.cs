using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>Where a concealed or stashed thing is kept (the rest of Item lives in World/WorldTypes.cs).</summary>
    public sealed partial class Item
    {
        /// <summary>While the thing is in its holder's Pocket list: the part of the body that keeps it out of sight — "pocket",
        /// "coat" or "bag" (null reads as a pocket, the old default). Meaningless once it leaves that list.</summary>
        public string Worn;
        /// <summary>The piece of furniture it was put in, under or behind (Hidden stays true until someone finds it); -1 = not stashed.</summary>
        public int StashF = -1;
    }

    /// <summary>Where on the body a thing can be carried out of sight.</summary>
    public enum BodySlot { None, Pocket, Coat, Bag }

    /// <summary>What a person's clothes allow: an inner pocket (a coat, a jacket, a hoodie's pouch, a cape) and a bag, with room for how many.</summary>
    public sealed class Outfit
    {
        public const int PocketCap = 8;
        public string Coat; public int CoatCap; public float CoatMax;
        public string Bag; public int BagCap; public float BagMax; public string BagKind;   // "cross", "brief", "tool"
    }

    /// <summary>A place in a piece of furniture where a thing can be put out of sight: a drawer, under a mattress, behind the books.</summary>
    public sealed class StashPlace
    {
        public string Type;
        /// <summary>The place in words ("매트리스 밑", "책장의 책 뒤") and what putting something there is ("밀어 넣었다").</summary>
        public string Where, Put;
        public float MaxSize, MaxMass; public int Capacity;
        /// <summary>How readily a searcher turns up what is there (0..1): a drawer gives it away, the soil of a planter hardly.</summary>
        public float Find;
        /// <summary>Bend down for it (under a bed, into the soil); wets what goes in (the aquarium); leaves soil on it; flat things only (a rug).</summary>
        public bool Low, Wets, Soils, Flat;
        /// <summary>How long the hands are busy (presentation and an NPC's pause).</summary>
        public float Seconds;
    }

    /// <summary>The outcome of one act: tuck, draw, stash, retrieve or search.</summary>
    public sealed class ConcealResult
    {
        public bool Ok; public string Text; public string Why; public BodySlot Slot; public int Furniture = -1; public float Seconds;
        /// <summary>Who saw it happen (nearest first in SeenBy), and whether the clothes took a stain.</summary>
        public string SeenBy; public List<string> Seen = new List<string>(); public bool Stained;
        public List<Item> Found = new List<Item>();
    }

    /// <summary>
    /// Carrying things out of sight, and putting them away. A small thing goes into a pocket; a knife, an ice pick, a chisel, a coil
    /// of rope or a small bottle goes inside a coat (if the clothes have one); a bag (a cross-bag, a briefcase, a tool belt) takes a
    /// little more; busts, pokers, candlesticks and saws cannot be hidden on the body at all. Anything can be put in, under or behind
    /// a piece of furniture that has room for it — a drawer, a wardrobe, under a mattress, behind the books, in a planter's soil —
    /// and taken back later. Every one of these is a short motion that others may see; a bloody or wet thing tucked into clothes
    /// marks them. NPC culprits use the same acts for their own hiding, and investigators search such places.
    /// Deterministic: the only randomness is an NPC search's luck (S.R(Stream.Conceal)).
    /// </summary>
    public static class Concealment
    {
        // ------------------------------------------------------------------ what fits on the body
        /// <summary>Never under anyone's clothes: busts, pokers, candlesticks, saws and the like (big, heavy, long or messy).</summary>
        static readonly HashSet<string> NeverOnBody = new HashSet<string> { "Statuette", "FirePoker", "Candlestick", "Hacksaw", "BoneSaw", "Crowbar", "PipeSection", "CueStick", "Mop", "SeveredPart", "Pillow", "Bucket", "Vase", "Iron", "Tea" };
        /// <summary>Flat, thin or folded: a pocket takes them even when they are long (a letter opener, a note, a folded cord).</summary>
        static readonly HashSet<string> Slim = new HashSet<string> { "LetterOpener", "Scalpel", "PaletteKnife", "PianoWire", "Thread", "Tripwire", "Fuse", "Sedative", "PoisonVial", "Foxglove", "Envelope", "Document", "Invitation", "Notebook", "Scarf", "CurtainCord", "Sticker", "Button", "Candy", "Chocolate", "HandWarmer", "WindupToy", "Recorder" };
        /// <summary>Fits a bag but nobody tucks it into a coat (a cup, a plate, a first-aid box, a folded sheet).</summary>
        static readonly HashSet<string> BagOnly = new HashSet<string> { "Cup", "WineGlass", "Plate", "FirstAidKit", "PaperModel", "Sheet", "Bread" };

        /// <summary>The smallest part of the body a thing of this kind can go (None: it cannot be hidden on the body at all).</summary>
        public static BodySlot BodyClass(ItemDef d)
        {
            if (d == null) return BodySlot.None;
            if (NeverOnBody.Contains(d.Type) || d.TwoHanded || d.Heavy || d.Size >= 0.55f || d.Mass > 2.5f || d.Mat == Mat.Liquid || d.Mat == Mat.Flesh) return BodySlot.None;
            if (BagOnly.Contains(d.Type)) return BodySlot.Bag;
            if (d.Key || Slim.Contains(d.Type) || d.Size <= 0.12f && d.Mass <= 0.5f) return BodySlot.Pocket;
            bool soft = d.Mat == Mat.Cloth || d.Mat == Mat.Paper;
            if (d.Size <= 0.36f && d.Mass <= 1.3f || soft && d.Size <= 0.5f && d.Mass <= 1.0f) return BodySlot.Coat;
            return BodySlot.Bag;
        }

        /// <summary>Why it can't be hidden on the body (for the greyed prompt).</summary>
        public static string TooBig(ItemDef d)
        {
            if (d == null) return "숨길 수 없다";
            if (d.Type == "Tea") return "쏟아질 테니 숨길 수 없다";
            if (d.Heavy || d.Mass > 2.5f) return "너무 무거워서 숨길 수 없다";
            if (d.Size >= 0.55f || d.Type == "FirePoker" || d.Type == "CueStick" || d.Type == "Mop" || d.Type == "PipeSection" || d.Type == "Crowbar") return "너무 길어서 숨길 수 없다";
            return "너무 커서 숨길 수 없다";
        }

        static bool IsTool(ItemDef d) => d != null && (d.Tag == "tool" || d.Tag == "saw" || d.Light || d.Mat == Mat.Metal && d.Size <= 0.36f || d.Tag == "cable" || d.Tag == "trap" || d.Tag == "thread");

        // ------------------------------------------------------------------ what each person's clothes allow
        static Outfit O(string coat, int cap, float max, string bag = null, int bcap = 0, float bmax = 0, string kind = null)
            => new Outfit { Coat = coat, CoatCap = cap, CoatMax = max, Bag = bag, BagCap = bcap, BagMax = bmax, BagKind = kind };
        /// <summary>The cast as they are dressed (the scanned models' real clothes, not only the design sheet).</summary>
        static readonly Dictionary<string, Outfit> Dressed = new Dictionary<string, Outfit>
        {
            ["P01"] = O("재킷 안주머니", 2, 0.34f, "크로스백", 3, 0.5f, "cross"),
            ["P02"] = O("코트 안주머니", 3, 0.36f),
            ["P03"] = O("블레이저 안주머니", 2, 0.34f),
            ["P04"] = O("재킷 안주머니", 2, 0.34f),
            ["P05"] = O("정장 안주머니", 2, 0.34f),
            ["P06"] = O("망토 안", 3, 0.4f, "서류 가방", 3, 0.45f, "brief"),
            ["P07"] = O("모피 코트 안", 3, 0.4f),
            ["P08"] = O("청재킷 안주머니", 2, 0.34f),
            ["P09"] = O("코트 안주머니", 3, 0.36f),
            ["P10"] = O("코트 안주머니", 2, 0.36f),
            ["P11"] = O(null, 0, 0, "공구 벨트", 3, 0.36f, "tool"),
            ["P12"] = O("재킷 안주머니", 2, 0.3f),
            ["P13"] = O("후드 앞주머니", 1, 0.3f),
            ["P14"] = O(null, 0, 0),
            ["P15"] = O("트렌치코트 안주머니", 3, 0.36f),
            ["P16"] = O("재킷 안주머니", 2, 0.34f),
            ["P17"] = O("케이프 안", 1, 0.3f),
            ["P18"] = O(null, 0, 0),
            ["NPC00"] = O("연미복 안주머니", 2, 0.34f),
        };
        static readonly Dictionary<string, Outfit> _derived = new Dictionary<string, Outfit>();

        public static Outfit OutfitOf(string actor)
        {
            if (actor == null) return O(null, 0, 0);
            if (Dressed.TryGetValue(actor, out var o)) return o;
            lock (_derived)
            {
                if (_derived.TryGetValue(actor, out o)) return o;
                // anyone else: read the design sheet (a coat or a jacket has an inside pocket; a bag is a bag)
                var look = Cast.Get(actor)?.Look; string coat = null; int cap = 0; float max = 0; string bag = null; int bcap = 0; float bmax = 0; string kind = null;
                if (look != null)
                {
                    foreach (var g in look.Wear)
                        switch (g.Kind)
                        {
                            case Garment.LongCoat: case Garment.TrenchCoat: case Garment.FurCoat: coat = "코트 안주머니"; cap = 3; max = 0.36f; break;
                            case Garment.Blazer: case Garment.SuitJacket: case Garment.CroppedJacket: case Garment.DenimJacket: case Garment.Tailcoat: if (coat == null) { coat = "재킷 안주머니"; cap = 2; max = 0.34f; } break;
                            case Garment.Hoodie: case Garment.Capelet: case Garment.Apron: if (coat == null) { coat = g.Kind == Garment.Hoodie ? "후드 앞주머니" : g.Kind == Garment.Apron ? "앞치마 주머니" : "케이프 안"; cap = 1; max = 0.3f; } break;
                        }
                    if (look.Acc.Contains(Accessory.CrossBag)) { bag = "크로스백"; bcap = 3; bmax = 0.5f; kind = "cross"; }
                    else if (look.Acc.Contains(Accessory.Briefcase)) { bag = "서류 가방"; bcap = 3; bmax = 0.45f; kind = "brief"; }
                    else if (look.Acc.Contains(Accessory.ToolBelt)) { bag = "공구 벨트"; bcap = 3; bmax = 0.36f; kind = "tool"; }
                }
                o = O(coat, cap, max, bag, bcap, bmax, kind); _derived[actor] = o; return o;
            }
        }

        public static string SlotKey(BodySlot s) => s == BodySlot.Coat ? "coat" : s == BodySlot.Bag ? "bag" : "pocket";

        /// <summary>Where on its holder's body a thing is kept (None when it is in a hand, on the floor or with nobody).</summary>
        public static BodySlot SlotOf(GameState S, Item it)
        {
            if (it?.Holder == null) return BodySlot.None;
            var h = S.A(it.Holder); if (h == null || !h.Pocket.Contains(it.Id)) return BodySlot.None;
            if (it.Worn == "coat") return BodySlot.Coat; if (it.Worn == "bag") return BodySlot.Bag; if (it.Worn == "pocket") return BodySlot.Pocket;
            // put straight into the pocket list by older code: read the size
            var cls = BodyClass(it.Def); var o = OutfitOf(h.Id);
            if (cls == BodySlot.Pocket || cls == BodySlot.None) return BodySlot.Pocket;
            if (cls == BodySlot.Coat && o.Coat != null) return BodySlot.Coat;
            return o.Bag != null ? BodySlot.Bag : o.Coat != null ? BodySlot.Coat : BodySlot.Pocket;
        }

        /// <summary>"주머니", "재킷 안주머니", "크로스백" … (the person's own words for that part of their clothes).</summary>
        public static string SlotWord(string actor, BodySlot s)
        {
            var o = OutfitOf(actor);
            return s == BodySlot.Coat ? o.Coat ?? "품" : s == BodySlot.Bag ? o.Bag ?? "가방" : "주머니";
        }

        public static int Count(GameState S, Actor a, BodySlot s) { int n = 0; foreach (var id in a.Pocket) if (SlotOf(S, S.I(id)) == s) n++; return n; }

        public static int Capacity(string actor, BodySlot s)
        {
            var o = OutfitOf(actor);
            return s == BodySlot.Pocket ? Outfit.PocketCap : s == BodySlot.Coat ? (o.Coat != null ? o.CoatCap : 0) : s == BodySlot.Bag ? (o.Bag != null ? o.BagCap : 0) : 0;
        }

        /// <summary>Can this thing go into that part of this person's clothes (its size and kind, room left)? why: the reason when not.</summary>
        public static bool Fits(GameState S, Actor a, Item it, BodySlot s, out string why)
        {
            why = null; var d = it?.Def; if (a == null || d == null) { why = "숨길 수 없다"; return false; }
            var o = OutfitOf(a.Id); var cls = BodyClass(d);
            if (cls == BodySlot.None) { why = TooBig(d); return false; }
            switch (s)
            {
                case BodySlot.Pocket: if (cls != BodySlot.Pocket) { why = "주머니에 들어가지 않는다"; return false; } break;
                case BodySlot.Coat:
                    {
                        if (o.Coat == null) { why = "품에 숨길 만한 옷차림이 아니다"; return false; }
                        float max = o.CoatMax + (d.Mat == Mat.Cloth || d.Mat == Mat.Paper ? 0.14f : 0f);
                        if (cls == BodySlot.Bag || d.Size > max) { why = "품에 숨기기엔 너무 크다"; return false; }
                        break;
                    }
                case BodySlot.Bag:
                    if (o.Bag == null) { why = "가방이 없다"; return false; }
                    if (d.Size > o.BagMax) { why = LineBank.FixParticles($"{o.Bag}에 들어가지 않는다"); return false; }
                    if (o.BagKind == "tool" && !IsTool(d)) { why = "공구 벨트에는 공구만 걸 수 있다"; return false; }
                    break;
                default: why = "숨길 수 없다"; return false;
            }
            int used = Count(S, a, s) - (SlotOf(S, it) == s ? 1 : 0);
            if (used >= Capacity(a.Id, s)) { why = s == BodySlot.Pocket ? "주머니가 꽉 찼다" : s == BodySlot.Coat ? "품이 꽉 찼다" : LineBank.FixParticles($"{o.Bag}이(가) 꽉 찼다"); return false; }
            return true;
        }

        /// <summary>The best part of the body for this thing (the smallest that fits and has room), or None with the reason.</summary>
        public static BodySlot BestSlot(GameState S, Actor a, Item it, out string why)
        {
            why = null; var cls = BodyClass(it?.Def);
            if (cls == BodySlot.None) { why = TooBig(it?.Def); return BodySlot.None; }
            var order = cls == BodySlot.Pocket ? new[] { BodySlot.Pocket, BodySlot.Coat, BodySlot.Bag } : cls == BodySlot.Coat ? new[] { BodySlot.Coat, BodySlot.Bag } : new[] { BodySlot.Bag };
            string first = null;
            foreach (var s in order) { if (Fits(S, a, it, s, out var w)) return s; first = first ?? w; }
            why = first; return BodySlot.None;
        }

        // ------------------------------------------------------------------ places in furniture
        static StashPlace P(string type, string where, string put, float maxSize, float maxMass, int cap, float find, float secs, bool low = false, bool wets = false, bool soils = false, bool flat = false)
            => new StashPlace { Type = type, Where = where, Put = put, MaxSize = maxSize, MaxMass = maxMass, Capacity = cap, Find = find, Seconds = secs, Low = low, Wets = wets, Soils = soils, Flat = flat };
        static readonly Dictionary<string, StashPlace> Places = new[]
        {
            // with doors, drawers or a lid: what is inside shows in its slot when the part is opened
            P("Wardrobe", "옷장 속 옷가지 사이", "밀어 넣었다", 1.5f, 30f, 5, 0.9f, 1.2f),
            P("Cabinet", "장식장 안쪽", "넣어 두었다", 0.8f, 12f, 4, 0.85f, 1.1f),
            P("Sideboard", "찬장 안쪽", "넣어 두었다", 0.8f, 12f, 4, 0.85f, 1.1f),
            P("Nightstand", "협탁 서랍", "넣어 두었다", 0.4f, 3f, 3, 0.95f, 0.9f, low: true),
            P("FileCabinet", "문서 보관함 서랍", "넣어 두었다", 0.4f, 3f, 3, 0.85f, 1.0f),
            P("MedCabinet", "약장 안", "넣어 두었다", 0.4f, 3f, 3, 0.9f, 0.9f),
            P("Chest", "궤짝 안", "넣어 두었다", 0.9f, 20f, 5, 0.9f, 1.2f, low: true),
            P("CardCatalog", "목록함 서랍", "끼워 두었다", 0.35f, 2f, 3, 0.7f, 1.0f),
            P("ColdLocker", "냉장 보관대 안쪽", "밀어 넣었다", 0.9f, 20f, 4, 0.85f, 1.2f),
            P("Fridge", "냉장고 안쪽", "밀어 넣었다", 0.8f, 12f, 3, 0.9f, 1.0f),
            P("ButlerDesk", "진행대 서랍", "넣어 두었다", 0.4f, 3f, 3, 0.9f, 0.9f),
            P("DisplayCase", "유리 진열장 안", "넣어 두었다", 0.8f, 12f, 3, 1.0f, 1.0f),
            P("Counter", "조리대 아래 칸", "밀어 넣었다", 0.8f, 12f, 4, 0.85f, 1.0f, low: true),
            // rummaged (no moving parts)
            P("Desk", "책상 서랍", "넣어 두었다", 0.4f, 3f, 3, 0.9f, 0.9f),
            P("VanityDesk", "화장대 서랍", "넣어 두었다", 0.4f, 3f, 3, 0.9f, 0.9f),
            P("Crates", "나무 상자 속", "묻어 두었다", 0.9f, 20f, 5, 0.75f, 1.3f, low: true),
            P("Shelves", "선반 안쪽 상자 뒤", "밀어 넣었다", 0.8f, 12f, 4, 0.75f, 1.1f),
            P("DollShelf", "인형들 뒤", "숨겨 두었다", 0.4f, 3f, 3, 0.6f, 1.2f),
            P("Barrel", "빈 술통 속", "떨어뜨려 넣었다", 0.8f, 12f, 3, 0.7f, 1.1f),
            P("CostumeRack", "걸린 의상들 사이", "끼워 넣었다", 1.0f, 12f, 4, 0.75f, 1.1f),
            // no door, no drawer: nothing shows until someone looks there
            P("Bed", "매트리스 밑", "밀어 넣었다", 0.9f, 4f, 3, 0.75f, 1.6f, low: true),
            P("Bookshelf", "책장의 책 뒤", "끼워 두었다", 0.4f, 3.5f, 3, 0.55f, 1.4f),
            P("Piano", "피아노 뚜껑 안", "넣어 두었다", 1.0f, 5f, 2, 0.6f, 1.5f),
            P("Piano_Upright", "피아노 윗판 안", "넣어 두었다", 0.6f, 4f, 2, 0.6f, 1.4f),
            P("Planter", "화분대 흙 속", "묻었다", 0.6f, 5f, 3, 0.45f, 2.2f, low: true, soils: true),
            P("Plant", "화분 흙 속", "묻었다", 0.35f, 3f, 1, 0.4f, 2.0f, low: true, soils: true),
            P("Washer", "빨래 더미 속", "쑤셔 넣었다", 0.6f, 4f, 3, 0.6f, 1.3f, low: true),
            P("Sofa", "소파 쿠션 밑", "밀어 넣었다", 0.5f, 3f, 2, 0.75f, 1.2f, low: true),
            P("Armchair", "안락의자 쿠션 틈", "밀어 넣었다", 0.4f, 2f, 1, 0.75f, 1.1f, low: true),
            P("DayBed", "긴 의자 쿠션 밑", "밀어 넣었다", 0.5f, 3f, 2, 0.75f, 1.2f, low: true),
            P("Clock", "괘종시계 추 상자 안", "넣어 두었다", 0.6f, 4f, 2, 0.5f, 1.4f),
            P("Aquarium", "수조 바닥 자갈 속", "묻었다", 0.4f, 5f, 2, 0.5f, 1.8f, wets: true),
            P("Dollhouse", "인형의 집 안", "넣어 두었다", 0.4f, 3f, 2, 0.6f, 1.3f),
            P("StuffedBeast", "박제 짐승 뱃속", "쑤셔 넣었다", 0.6f, 5f, 2, 0.35f, 1.8f),
            P("Organ", "오르간 파이프 뒤", "밀어 넣었다", 1.2f, 8f, 3, 0.5f, 1.5f),
            P("Altar", "제단 천 밑", "밀어 넣었다", 0.9f, 8f, 3, 0.7f, 1.2f, low: true),
            P("WineRack", "와인병 사이", "끼워 넣었다", 0.35f, 2f, 2, 0.6f, 1.1f),
        }.ToDictionary(p => p.Type);

        /// <summary>The hiding place this piece of furniture offers (null: nowhere to put anything).</summary>
        public static StashPlace PlaceFor(Furniture f)
        {
            if (f == null || f.Damage >= 3 || !Places.TryGetValue(f.Type, out var p)) return null;
            return p;
        }
        public static bool IsPlace(Furniture f) => PlaceFor(f) != null;

        /// <summary>How close a person must stand to put something in (or take it out).</summary>
        public static float Reach(Furniture f) => f == null ? 0 : Math.Max(f.W, f.D) * 0.6f + 1.5f;
        /// <summary>The old rule for a hidden thing lying loose near furniture (the same as Containers.ContainerReach).</summary>
        static float NearReach(Furniture f) => Math.Max(f.W, f.D) * 0.6f + 0.9f;

        /// <summary>Things stashed in this piece right now (hidden, not yet found), by id.</summary>
        public static List<Item> StashedIn(GameState S, Furniture f)
        {
            var l = new List<Item>(); if (f == null) return l;
            foreach (var it in S.Items.Values) if (it.StashF == f.Id && it.Holder == null && it.Hidden) l.Add(it);
            l.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id)); return l;
        }

        static bool FlatThing(ItemDef d) => d != null && (d.Mat == Mat.Paper || d.Mat == Mat.Cloth || d.Key || Slim.Contains(d.Type));

        /// <summary>Can this thing be put into this place (size, weight, kind, room left)? why: the reason when not.</summary>
        public static bool CanStash(GameState S, Furniture f, Item it, out string why)
        {
            why = null; var p = PlaceFor(f); var d = it?.Def;
            if (p == null || d == null) { why = "여기에는 숨길 데가 없다"; return false; }
            if (d.Size > p.MaxSize) { why = LineBank.FixParticles($"{p.Where}에 넣기엔 너무 크다"); return false; }
            if (d.Mass > p.MaxMass) { why = LineBank.FixParticles($"{p.Where}에 넣기엔 너무 무겁다"); return false; }
            if (p.Flat && !FlatThing(d)) { why = LineBank.FixParticles($"{p.Where}에는 납작한 것만 들어간다"); return false; }
            if (p.Wets && d.Mat == Mat.Paper) { why = "물에 젖으면 못 쓰게 된다"; return false; }
            if (S.Items.Values.Count(x => x.StashF == f.Id && x.Holder == null && x.Hidden && x != it) >= p.Capacity) { why = LineBank.FixParticles($"{p.Where}에는 더 들어갈 자리가 없다"); return false; }
            return true;
        }

        /// <summary>The furniture a hidden thing is kept in: the place it was stashed in, or (older hides) the nearest place whose
        /// reach covers where it lies. Null when it lies loose (or is not hidden).</summary>
        public static Furniture PlaceOf(GameState S, Item it)
        {
            if (it == null || it.Holder != null || !it.Hidden) return null;
            if (it.StashF >= 0) return it.StashF < S.Layout.Furniture.Count ? S.Layout.Furniture[it.StashF] : null;
            return null;
        }

        /// <summary>The words for a stashed thing's place: "개인실 매트리스 밑".</summary>
        public static string PlaceName(GameState S, Furniture f) { var p = PlaceFor(f); return p == null ? S.RoomName(f?.Room ?? -1) : $"{S.RoomName(f.Room)} {p.Where}"; }

        /// <summary>For NPC hiding: in this room, the place that takes this thing, nearest to `near` (null when the room has none).</summary>
        public static Furniture PlaceIn(GameState S, Room room, Item it, P3 near)
        {
            if (room == null || it == null) return null;
            Furniture best = null; float bd = float.MaxValue;
            foreach (var fid in room.Furniture)
            {
                var f = S.Layout.Furniture[fid]; if (f.Pos.f != near.f || !CanStash(S, f, it, out _)) continue;
                var p = PlaceFor(f); if (p.Find >= 0.95f) continue;   // a culprit does not choose a glass case
                // the less obvious, the better (a planter beats a drawer), then the nearest
                float d = near.DistXZ(f.Pos) + p.Find * 6f;
                if (d < bd || d == bd && best != null && f.Id < best.Id) { bd = d; best = f; }
            }
            return best;
        }

        /// <summary>The motion and its length for an act (presentation and an NPC's busy time).</summary>
        public static float Seconds(string act, BodySlot slot, StashPlace p)
        {
            switch (act)
            {
                case "tuck": return slot == BodySlot.Pocket ? 0.5f : slot == BodySlot.Coat ? 0.8f : 1.0f;
                case "draw": return slot == BodySlot.Pocket ? 0.5f : slot == BodySlot.Coat ? 0.7f : 0.9f;
                case "stash": case "retrieve": return p?.Seconds ?? 1.2f;
                case "search": return (p?.Seconds ?? 1.2f) + 0.6f;
            }
            return 0.8f;
        }

        /// <summary>How easily an act catches the eye (scales how far away it is noticed).</summary>
        public static float Conspicuous(string act, BodySlot slot, StashPlace p, Item it)
        {
            float c = act == "tuck" ? (slot == BodySlot.Pocket ? 0.55f : slot == BodySlot.Coat ? 0.85f : 0.75f)
                    : act == "draw" ? (slot == BodySlot.Pocket ? 0.6f : 0.9f)
                    : act == "stash" || act == "retrieve" ? (p != null && p.Low ? 1.0f : 0.85f) : 0.9f;
            if (it?.Def?.IsWeapon == true) c *= 1.1f;
            if (it != null && it.Bloody) c *= 1.2f;
            return c;
        }

        /// <summary>What was seen, in words: "진우가 식칼을 코트 안주머니에 넣었다" (seen = false) or "…넣는 걸 봤다" (a memory).
        /// Without a plain look the thing is "무언가" and the clothes are just "품속" / "주머니" / "가방".</summary>
        public static string Deed(GameState S, string doer, Item it, string act, BodySlot slot, Furniture f, bool plain, bool seen)
        {
            string g = Cast.GivenOf(doer), item = plain ? it.Kor : "무언가";
            string part = plain ? SlotWord(doer, slot) : slot == BodySlot.Coat ? "품속" : slot == BodySlot.Bag ? "가방" : "주머니";
            string where = PlaceFor(f)?.Where ?? "어딘가";
            string s;
            switch (act)
            {
                case "tuck": s = $"{g}이(가) {item}을(를) {part}에 " + (seen ? "넣는 걸 봤다" : "넣었다"); break;
                case "draw": s = $"{g}이(가) {part}에서 {item}을(를) " + (seen ? "꺼내는 걸 봤다" : "꺼냈다"); break;
                case "stash": s = $"{g}이(가) {(f != null ? S.RoomName(f.Room) + " " : "")}{where}에 {item}을(를) " + (seen ? "숨기는 걸 봤다" : "숨겼다"); break;
                default: s = $"{g}이(가) {where}에서 {item}을(를) " + (seen ? "꺼내는 걸 봤다" : "꺼냈다"); break;
            }
            return LineBank.FixParticles(s);
        }

        // ------------------------------------------------------------------ acts (validated commands for anyone, the player included)
        /// <summary>Hand → body: the thing goes into a pocket, inside the coat or into the bag (prefer = None: the best that fits).</summary>
        public static ConcealResult Conceal(Simulation sim, Actor a, Item it, BodySlot prefer = BodySlot.None) => sim.DoConceal(a, it, prefer);
        /// <summary>Body → hand: out of the pocket, the coat or the bag into a free hand.</summary>
        public static ConcealResult Draw(Simulation sim, Actor a, Item it) => sim.DoDraw(a, it);
        /// <summary>Hand (or body) → furniture: into a drawer, under the mattress, behind the books... hidden until found.</summary>
        public static ConcealResult Stash(Simulation sim, Actor a, Item it, Furniture f) => sim.DoStash(a, it, f);
        /// <summary>Furniture → hand: take back a stashed thing (anyone's, if you know it is there).</summary>
        public static ConcealResult Retrieve(Simulation sim, Actor a, Item it) => sim.DoRetrieve(a, it);
        /// <summary>Search a place: what is stashed there may turn up (always for the player's own thorough look; by luck and skill for NPCs).</summary>
        public static ConcealResult Search(Simulation sim, Actor a, Furniture f) => sim.DoSearch(a, f, !a.IsPlayer);

        /// <summary>Does this culprit tuck the weapon out of sight before walking off with it (the careful kind, if it fits)?</summary>
        public static bool WouldTuck(Simulation sim, Actor a, Item it)
        {
            if (a == null || it == null || it.Holder != a.Id || a.Pocket.Contains(it.Id)) return false;
            if ((a.Def?.Deceit ?? 0) < 50 && (a.Def?.Composure ?? 0) < 60) return false;
            return BestSlot(sim.S, a, it, out _) != BodySlot.None;
        }

        /// <summary>Things the player remembers stashing (or saw someone stash), with where: from the notebook's point of view.</summary>
        public static List<(Item it, Furniture f, string by)> Remembered(GameState S, string who = Cast.Player)
        {
            var res = new List<(Item, Furniture, string)>(); var k = S.K(who);
            foreach (var fact in k.Facts.Where(x => x.StartsWith("stash:", StringComparison.Ordinal) || x.StartsWith("saw-stash:", StringComparison.Ordinal)).OrderBy(x => x, StringComparer.Ordinal))
            {
                var p = fact.Split(':'); bool saw = p[0] == "saw-stash";
                if (p.Length < 3 || !int.TryParse(p[2], out var fid) || fid < 0 || fid >= S.Layout.Furniture.Count) continue;
                var it = S.I(p[1]); if (it == null) continue;
                res.Add((it, S.Layout.Furniture[fid], saw ? (p.Length > 3 ? p[3] : null) : who));
            }
            return res;
        }
    }

    // ================================================================================== the kernel side (Simulation)
    public sealed partial class Simulation
    {
        static string Given(string id) => Cast.GivenOf(id);

        /// <summary>People absorbed in something (reading, cooking, eating, playing...) notice less of what others do with their hands.</summary>
        static bool Absorbed(Actor o) => o.Anim == Anim.Read || o.Anim == Anim.Write || o.Anim == Anim.Cook || o.Anim == Anim.Eat || o.Anim == Anim.Drink || o.Anim == Anim.Play
                                         || o.Anim == Anim.Craft || o.Anim == Anim.Garden || o.Anim == Anim.Pray || o.Anim == Anim.Cry || o.Anim == Anim.Swim || o.Anim == Anim.Exercise;

        /// <summary>Everyone who sees `doer` do something with their hands right now: looking that way, near enough for the light,
        /// no wall between, attention not elsewhere. plain: close and lit enough to tell what the thing was.</summary>
        internal List<(Actor who, bool plain)> Watchers(Actor doer, float conspicuous)
        {
            var res = new List<(Actor, bool)>(); if (doer == null) return res;
            foreach (var o in S.Actors.Values.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                if (o == doer || !o.Alive || o.Status != ActorStatus.Active || o.Pose == Pose.Sleep || o.IsButler || o.StairId >= 0 || o.CarriedBy != null) continue;
                if (o.Pos.f != doer.Pos.f) continue;
                float d = o.Pos.DistXZ(doer.Pos); if (d > 16f) continue;
                float light = Math.Max(RoomLight(doer.Room), RoomLight(o.Room) * 0.5f);
                float obs = (o.Def?.Obs ?? 50) / 100f;
                float range = (2.0f + 9.5f * light) * conspicuous * (0.8f + obs * 0.45f);
                if (!o.IsPlayer && o.TalkingTo != null && o.TalkingTo != doer.Id) range *= 0.6f;
                if (!o.IsPlayer && Absorbed(o)) range *= 0.6f;
                if (d > range) continue;
                float ang = Math.Abs(MathX.DeltaAngle(o.Yaw, MathX.AngleDeg(doer.Pos.x - o.Pos.x, doer.Pos.z - o.Pos.z)));
                if (ang > (o.IsPlayer ? 50f : 65f) && d > 1.3f) continue;
                if (!S.Layout.Nav(o.Pos.f).Ray(o.Pos.x, o.Pos.z, doer.Pos.x, doer.Pos.z, _doorOpen ??= DoorOpenNow, false, out _)) continue;
                bool plain = light > 0.18f && d <= 1.6f + 4.2f * light * (0.7f + obs * 0.6f);
                res.Add((o, plain));
            }
            return res;
        }

        /// <summary>Someone saw it: memories, suspicion, a word or two; the player gets a notice (and a card when it matters).</summary>
        void ConcealSeen(Actor doer, Item it, string act, Furniture f, ConcealResult res)
        {
            var place = Concealment.PlaceFor(f);
            var list = Watchers(doer, Concealment.Conspicuous(act, res.Slot, place, it)); if (list.Count == 0) return;
            var d = it.Def; bool weapon = d?.IsWeapon == true; bool bloody = it.Bloody; bool sly = d != null && (d.Tag == "poison" || d.Tag == "sedate");
            string where = place?.Where;
            foreach (var (o, plain) in list)
            {
                res.Seen.Add(o.Id);
                bool bad = plain && (weapon || bloody || sly);
                bool placed = act == "stash" || act == "retrieve";
                var k = S.K(o.Id);
                if (o.IsPlayer)
                {
                    string text = Concealment.Deed(S, doer.Id, it, act, res.Slot, f, plain, false) + (plain && bloody ? " — 붉은 얼룩이 보였다" : "");
                    S.Emit(GameEventType.Notice, Cast.Player, doer.Id, text: text, key: "conceal");
                    if (placed && plain && f != null && act == "stash") k.Facts.Add($"saw-stash:{it.Id}:{f.Id}:{doer.Id}");
                    // a card only for what matters: a weapon, blood, a dose — or a key or a paper plainly hidden somewhere
                    if (bad || placed && plain && (d?.Key == true || d?.Tag == "document" || it.KeyFor != null))
                    {
                        var props = new List<Prop> { new Prop { Kind = PropKind.Held, A = doer.Id, Item = it.Type, Room = doer.Room, T0 = S.Clock, T1 = S.Clock, Value = "concealed" } };
                        if (bloody) props.Add(new Prop { Kind = PropKind.Bloodied, A = doer.Id, Room = doer.Room, T0 = S.Clock, T1 = S.Clock });
                        if (placed && f != null) props.Add(new Prop { Kind = PropKind.ItemAt, Item = it.Type, Room = f.Room, T0 = S.Clock, T1 = S.Clock, Value = "stashed" });
                        string title = LineBank.FixParticles(placed ? $"{Given(doer.Id)}이(가) {where}에 숨긴 {it.Kor}" : $"{Given(doer.Id)}이(가) 몸에 숨긴 {it.Kor}");
                        string line = Concealment.Deed(S, doer.Id, it, act, res.Slot, f, true, false) + ".";
                        string desc = $"{ClockFmt.Vague(S.Clock)}, {S.RoomName(doer.Room)}에서 " + Concealment.Deed(S, doer.Id, it, act, res.Slot, f, true, true) + "." + (bloody ? "\n붉은 얼룩이 묻어 있었다." : "");
                        var ev = Evidences.File(this, o.Id, EvKind.Sighting, title, desc, "직접 목격", $"conceal:{doer.Id}:{it.Id}", S.Clock, S.Clock, doer.Room,
                            placed ? "그것을 거기 숨겼다는 것" : "그때 그것을 몸에 숨겨 지니고 있었다는 것", "그것으로 무엇을 했는지", true, true, line, false, props.ToArray());
                        if (ev != null) ev.Subject = doer.Id;
                    }
                    continue;
                }
                // an NPC: it becomes something they saw (a sighting their testimony can use)
                k.Sightings.Add(new Sighting { Target = doer.Id, Room = doer.Room, T0 = S.Clock, T1 = S.Clock, IdConf = plain ? 0.9f : 0.6f, Held = plain ? it.Type : null, Bloody = plain && bloody, Root = "conceal:" + o.Id + ":" + S.Seq });
                if (k.Sightings.Count > 2500) k.Sightings.RemoveRange(0, 500);
                k.LastSeen[doer.Id] = (doer.Room, S.Clock);
                if (placed && plain && f != null) k.Facts.Add($"saw-stash:{it.Id}:{f.Id}:{doer.Id}");
                if (!k.Facts.Add($"saw-conceal:{doer.Id}:{it.Id}:{act}:{S.Day}")) continue;   // the same thing seen again today changes nothing more
                float sus = bloody && plain ? 0.25f : weapon && plain ? 0.12f : sly && plain ? 0.1f : act == "tuck" || placed ? 0.03f : 0.02f;
                if (S.Phase == Phase.Investigation) sus *= 1.5f;
                k.Suspicion[doer.Id] = (k.Suspicion.TryGetValue(doer.Id, out var s0) ? s0 : 0f) + sus;
                if (bad || !plain && doer.IsPlayer || placed && plain)
                    Relations.Change(S, o.Id, doer.Id, trust: bad ? -0.05f : -0.01f, fear: weapon && plain ? 0.05f : bloody && plain ? 0.08f : 0f,
                        memory: Concealment.Deed(S, doer.Id, it, act, res.Slot, f, plain, true));
                // a word, when close and the thing is plainly wrong (only to the player: feedback that they were seen)
                if (doer.IsPlayer && o.Pos.DistXZ(doer.Pos) < 5f && (bad || !plain && act == "tuck") && o.TalkingTo == null && o.PlanId == null)
                {
                    string fk = $"concealbark:{o.Id}:{S.Day}";
                    if (!S.Flags.ContainsKey(fk))
                    {
                        S.Flags[fk] = S.Clock;
                        Speak(o, bloody && plain ? "conceal_notice_blood" : weapon && plain ? "conceal_notice_weapon" : "conceal_notice", doer.Id, new Dictionary<string, string> { { "item", it.Kor } });
                    }
                }
            }
            res.SeenBy = list.OrderBy(x => x.who.Pos.DistXZ(doer.Pos)).ThenBy(x => x.who.Id, StringComparer.Ordinal).First().who.Id;
        }

        /// <summary>An NPC's hands are busy for the motion; the view gets a cue for the finer gesture.</summary>
        void ConcealMotion(Actor a, string act, float secs, Anim anim, int furniture, Item it)
        {
            if (!a.IsPlayer) { a.Anim = anim; a.BusyUntil = Math.Max(a.BusyUntil, S.Tick + (long)Math.Ceiling(secs * SimTime.PerSecond)); a.Speed = 0; }
            S.Emit(GameEventType.Anim, a.Id, key: act, id: furniture, data: it?.Id, value: secs, pos: a.Pos, room: a.Room);
        }

        // ------------------------------------------------------------------ the acts
        internal ConcealResult DoConceal(Actor a, Item it, BodySlot prefer)
        {
            var res = new ConcealResult();
            if (a == null || it == null || it.Holder != a.Id) { res.Why = "손에 든 것이 없다"; return res; }
            var cur = Concealment.SlotOf(S, it);
            if (cur != BodySlot.None && (prefer == BodySlot.None || prefer == cur)) { res.Ok = true; res.Slot = cur; res.Text = LineBank.FixParticles($"{it.Kor}은(는) 이미 {Concealment.SlotWord(a.Id, cur)}에 있다"); return res; }
            var slot = prefer != BodySlot.None && Concealment.Fits(S, a, it, prefer, out var pw) ? prefer : Concealment.BestSlot(S, a, it, out pw);
            if (slot == BodySlot.None) { res.Why = pw ?? Concealment.TooBig(it.Def); return res; }
            if (a.HandR == it.Id) a.HandR = null; if (a.HandL == it.Id) a.HandL = null;
            if (!a.Pocket.Contains(it.Id)) a.Pocket.Add(it.Id);
            it.Worn = Concealment.SlotKey(slot); it.Room = -1; it.LastMovedTick = S.Tick;
            res.Ok = true; res.Slot = slot; res.Seconds = Concealment.Seconds("tuck", slot, null);
            // blood or water soaks into the lining: a mark on the clothes someone may notice
            if (slot != BodySlot.Bag)
            {
                if (it.Bloody) { float mark = slot == BodySlot.Coat ? 0.32f : 0.18f; if (a.BloodOnClothes < mark) { a.BloodOnClothes = mark; res.Stained = true; } S.Log("ConcealStain", a.Id, item: it.Id, room: a.Room, data: "blood", secret: true); }
                if (it.Wet || WashedRecently(it)) { a.Wet = true; a.WetUntil = Math.Max(a.WetUntil, S.Clock + 30); S.Flags["wetsleeve:" + a.Id] = S.Clock; res.Stained = true; S.Log("ConcealStain", a.Id, item: it.Id, room: a.Room, data: "wet", secret: true); }
            }
            string word = Concealment.SlotWord(a.Id, slot);
            res.Text = LineBank.FixParticles(slot == BodySlot.Coat ? $"{it.Kor}을(를) {word}에 숨겼다" : $"{it.Kor}을(를) {word}에 넣었다") + (res.Stained ? (it.Bloody ? " — 안감에 피가 배어 나온다" : " — 옷이 축축해졌다") : "");
            S.Log("Conceal", a.Id, item: it.Id, room: a.Room, pos: a.Pos, data: Concealment.SlotKey(slot) + ":" + it.Type, secret: true);
            S.Emit(GameEventType.ItemMoved, a.Id, data: it.Id, text: "conceal", pos: a.Pos);
            ConcealMotion(a, "tuck", res.Seconds, slot == BodySlot.Pocket ? Anim.PutDown : Anim.Use, -1, it);
            ConcealSeen(a, it, "tuck", null, res);
            return res;
        }

        bool WashedRecently(Item it)
        {
            if (!it.Washed) return false;
            for (int i = S.Ledger.Count - 1, n = 0; i >= 0 && n < 4000; i--, n++) { var e = S.Ledger[i]; if (S.Clock - e.Clock > 30) break; if (e.Type == "Wash" && e.Item == it.Id) return true; }
            return false;
        }

        internal ConcealResult DoDraw(Actor a, Item it)
        {
            var res = new ConcealResult();
            var slot = Concealment.SlotOf(S, it);
            if (a == null || it == null || it.Holder != a.Id || slot == BodySlot.None) { res.Why = "꺼낼 것이 없다"; return res; }
            if (a.HandR == null && a.Body.HandR > 0.3f) a.HandR = it.Id;
            else if (a.HandL == null && a.Body.HandL > 0.3f) a.HandL = it.Id;
            else { res.Why = "두 손이 모두 차 있다"; return res; }
            a.Pocket.Remove(it.Id); it.Worn = null; it.LastMovedTick = S.Tick; it.LastUser = a.Id;
            res.Ok = true; res.Slot = slot; res.Seconds = Concealment.Seconds("draw", slot, null);
            res.Text = LineBank.FixParticles($"{Concealment.SlotWord(a.Id, slot)}에서 {it.Kor}을(를) 꺼냈다");
            S.Log("Draw", a.Id, item: it.Id, room: a.Room, pos: a.Pos, data: Concealment.SlotKey(slot) + ":" + it.Type, secret: true);
            S.Emit(GameEventType.ItemMoved, a.Id, data: it.Id, text: "draw", pos: a.Pos);
            ConcealMotion(a, "draw", res.Seconds, Anim.Use, -1, it);
            ConcealSeen(a, it, "draw", null, res);
            return res;
        }

        internal ConcealResult DoStash(Actor a, Item it, Furniture f)
        {
            var res = new ConcealResult { Furniture = f?.Id ?? -1 };
            var p = Concealment.PlaceFor(f);
            if (a == null || it == null || it.Holder != a.Id) { res.Why = "넣어 둘 것이 없다"; return res; }
            if (p == null) { res.Why = "여기에는 숨길 데가 없다"; return res; }
            if (f.Pos.f != a.Pos.f || a.Pos.DistXZ(f.Pos) > Concealment.Reach(f)) { res.Why = "너무 멀다"; return res; }
            if (!Concealment.CanStash(S, f, it, out var why)) { res.Why = why; return res; }
            var slotBefore = Concealment.SlotOf(S, it);
            _stashing = true;
            try { DropItem(a, it, f.Pos, true); }   // logs HideItem (the reveal and the trial read it), clears the hands / pocket
            finally { _stashing = false; }
            it.Pos = f.Pos; it.Room = f.Room; it.StashF = f.Id; it.Hidden = true; it.Worn = null;
            if (p.Wets) it.Wet = true;
            if (p.Soils && !it.Surface.Contains("흙")) it.Surface.Add("흙");
            S.K(a.Id).Facts.Add($"stash:{it.Id}:{f.Id}");
            res.Ok = true; res.Slot = slotBefore; res.Seconds = Concealment.Seconds("stash", BodySlot.None, p);
            res.Text = LineBank.FixParticles($"{it.Kor}을(를) {p.Where}에 {p.Put}");
            S.Log("Stash", a.Id, item: it.Id, room: f.Room, pos: f.Pos, data: $"f{f.Id}:{f.Type}", secret: true);
            ConcealMotion(a, "stash", res.Seconds, p.Low ? Anim.PickUp : Anim.PutDown, f.Id, it);
            ConcealSeen(a, it, "stash", f, res);
            return res;
        }

        internal ConcealResult DoRetrieve(Actor a, Item it)
        {
            var res = new ConcealResult();
            var f = it != null && it.StashF >= 0 && it.StashF < S.Layout.Furniture.Count ? S.Layout.Furniture[it.StashF] : null;
            var p = Concealment.PlaceFor(f);
            if (a == null || it == null || f == null || it.Holder != null)
            {
                // remembered, but it is not there any more
                if (a != null && it != null) ForgetStash(a, it);
                res.Why = it == null ? "없다" : LineBank.FixParticles($"숨겨 둔 {it.Kor}이(가) 없다 — 누군가 가져갔다");
                return res;
            }
            res.Furniture = f.Id;
            if (f.Pos.f != a.Pos.f || a.Pos.DistXZ(f.Pos) > Concealment.Reach(f)) { res.Why = LineBank.FixParticles($"너무 멀다 — {Concealment.PlaceName(S, f)}"); return res; }
            if (a.HandR != null && a.HandL != null && Concealment.BodyClass(it.Def) != BodySlot.Pocket) { res.Why = "두 손이 모두 차 있다"; return res; }
            if (!PickUp(a, it)) { res.Why = "손이 모자라다"; return res; }   // (the PickUp hook forgets the place)
            res.Ok = true; res.Seconds = Concealment.Seconds("retrieve", BodySlot.None, p);
            res.Text = LineBank.FixParticles($"{p?.Where ?? "숨겨 둔 곳"}에서 {it.Kor}을(를) 꺼냈다");
            S.Log("Retrieve", a.Id, item: it.Id, room: f.Room, pos: f.Pos, data: $"f{f.Id}:{f.Type}", secret: true);
            ConcealMotion(a, "retrieve", res.Seconds, p != null && p.Low ? Anim.PickUp : Anim.Use, f.Id, it);
            ConcealSeen(a, it, "retrieve", f, res);
            return res;
        }

        void ForgetStash(Actor a, Item it)
        {
            var k = S.K(a.Id);
            foreach (var fact in k.Facts.Where(x => x.StartsWith("stash:" + it.Id + ":", StringComparison.Ordinal) || x.StartsWith("saw-stash:" + it.Id + ":", StringComparison.Ordinal)).ToList()) k.Facts.Remove(fact);
        }

        /// <summary>Search one place. The player's own look is thorough (it always turns up what is there); an NPC's depends on the
        /// place and their eye (Stream.Conceal). What turns up comes out in front of the piece, in plain view.</summary>
        internal ConcealResult DoSearch(Actor a, Furniture f, bool roll)
        {
            var res = new ConcealResult { Furniture = f?.Id ?? -1 };
            var p = Concealment.PlaceFor(f); if (a == null || p == null) { res.Why = "뒤질 데가 없다"; return res; }
            res.Ok = true; res.Seconds = Concealment.Seconds("search", BodySlot.None, p);
            var here = Concealment.StashedIn(S, f);
            var rng = roll && here.Count > 0 ? S.R(Stream.Conceal) : null;
            foreach (var it in here)
            {
                if (S.K(a.Id).Facts.Contains($"stash:{it.Id}:{f.Id}")) continue;   // one's own stash is no find (it stays put away)
                if (rng != null && !rng.Chance(Math.Min(0.97, p.Find * (0.55 + (a.Def?.Obs ?? 50) / 200.0)))) continue;
                RevealStash(a, it, f);
                res.Found.Add(it);
            }
            S.K(a.Id).Facts.Add($"searched:{f.Id}:{S.Loop}:{S.Chapter}");
            S.Log("SearchPlace", a.Id, room: f.Room, data: $"f{f.Id}:{res.Found.Count}", secret: true);
            res.Text = res.Found.Count == 0 ? LineBank.FixParticles($"{p.Where}에는 아무것도 없다")
                     : LineBank.FixParticles($"{p.Where}에서 찾았다: " + string.Join(", ", res.Found.Select(i => i.Kor + (i.Bloody ? " (붉은 얼룩)" : ""))));
            return res;
        }

        /// <summary>A stashed thing is found: it comes out in front of the piece (plain view), the finder remembers it.</summary>
        void RevealStash(Actor finder, Item it, Furniture f)
        {
            it.Hidden = false; it.StashF = -1;
            it.Pos = FrontOf(f, 0.25f); it.Room = S.Layout.RoomAt(it.Pos) >= 0 ? S.Layout.RoomAt(it.Pos) : f.Room; it.LastMovedTick = S.Tick;
            S.K(finder.Id).ItemSeen[it.Id] = (it.Room, S.Clock);
            ForgetStash(finder, it);
            S.Log("ItemFound", finder.Id, item: it.Id, room: f.Room, pos: it.Pos, data: "stash:f" + f.Id);
            S.Emit(GameEventType.ItemMoved, finder.Id, data: it.Id, text: "revealed", pos: it.Pos, room: it.Room);
        }

        /// <summary>A walkable point in front of a piece of furniture, inside its room (for walking up to it or for what comes out of it).</summary>
        public P3 FrontOf(Furniture f, float gap = 0.55f)
        {
            double r = f.Yaw * Math.PI / 180.0; float fx = (float)Math.Sin(r), fz = (float)Math.Cos(r);
            foreach (var (dx, dz, half) in new[] { (fx, fz, f.D), (-fx, -fz, f.D), (fz, -fx, f.W), (-fz, fx, f.W) })
            {
                var p = Snap(new P3(f.Pos.f, f.Pos.x + dx * (half * 0.5f + gap), f.Pos.z + dz * (half * 0.5f + gap)));
                if (S.Layout.RoomAt(p) == f.Room && p.DistXZ(f.Pos) < Concealment.Reach(f)) return p;
            }
            var sp = S.Layout.Spots.FirstOrDefault(s => s.Furniture == f.Id);
            return sp != null ? sp.Approach : Snap(f.Pos);
        }

        // ------------------------------------------------------------------ hooks (Movement.cs, one line each)
        /// <summary>PickUp: a stashed thing taken out is no longer stashed; where it goes on the body is decided afresh.</summary>
        internal void ConcealOnPickUp(Actor a, Item it)
        {
            it.Worn = null;
            if (it.StashF >= 0) { it.StashF = -1; it.Hidden = false; ForgetStash(a, it); }
        }

        /// <summary>DropItem: a thing put down is not worn; a thing hidden near a hiding place (a culprit's drop, a burial in a
        /// planter) is kept in that place — the same record as a stash — so the view shows it there and a search can find it.</summary>
        [NonSerialized] bool _stashing;   // DoStash puts the thing in its chosen place itself
        internal void ConcealOnDrop(Actor a, Item it, bool hidden)
        {
            it.Worn = null; it.StashF = -1;
            if (!hidden || it.Room < 0 || _stashing) return;
            var room = S.Layout.Room(it.Room); if (room == null) return;
            Furniture best = null; float bd = float.MaxValue;
            foreach (var fid in room.Furniture)
            {
                var f = S.Layout.Furniture[fid]; if (f.Pos.f != it.Pos.f || !Concealment.IsPlace(f)) continue;
                float d = it.Pos.DistXZ(f.Pos); if (d >= Math.Max(f.W, f.D) * 0.6f + 0.9f) continue;
                if (!Concealment.CanStash(S, f, it, out _)) continue;
                if (d < bd || d == bd && best != null && f.Id < best.Id) { bd = d; best = f; }
            }
            if (best == null) return;
            it.StashF = best.Id;
            if (a != null) S.K(a.Id).Facts.Add($"stash:{it.Id}:{best.Id}");
        }

        /// <summary>The NPC steps of this system (Movement.Execute): C_Tuck, C_Draw, C_Stash, C_Retrieve, C_Search. True when handled.</summary>
        internal bool ConcealStep(Actor a, ActionStep st)
        {
            switch (st.Kind)
            {
                case "C_Tuck": { var it = S.I(st.Item); if (it != null && it.Holder == a.Id) DoConceal(a, it, BodySlot.None); NextStep(a); return true; }
                case "C_Draw": { var it = S.I(st.Item); if (it != null && it.Holder == a.Id) DoDraw(a, it); NextStep(a); return true; }
                case "C_Stash":
                    {
                        var it = S.I(st.Item); var f = st.Furniture >= 0 && st.Furniture < S.Layout.Furniture.Count ? S.Layout.Furniture[st.Furniture] : null;
                        if (it != null && f != null && it.Holder == a.Id) { var r = DoStash(a, it, f); if (!r.Ok) DropItem(a, it, a.Pos, true); }
                        NextStep(a); return true;
                    }
                case "C_Retrieve": { var it = S.I(st.Item); if (it != null) DoRetrieve(a, it); NextStep(a); return true; }
                case "C_Search":
                    {
                        var f = st.Furniture >= 0 && st.Furniture < S.Layout.Furniture.Count ? S.Layout.Furniture[st.Furniture] : null;
                        if (f == null) { NextStep(a); return true; }
                        a.Speed = 0; a.Yaw = MathX.AngleDeg(f.Pos.x - a.Pos.x, f.Pos.z - a.Pos.z);
                        var p = Concealment.PlaceFor(f); a.Anim = Anim.Search; if (p != null && p.Low) a.Pose = Pose.Crouch;
                        if (a.Act.StepEnd > 0 && S.Clock < a.Act.StepEnd) return true;
                        if (a.Pose == Pose.Crouch) a.Pose = Pose.Stand;
                        var res = DoSearch(a, f, true);
                        foreach (var it in res.Found) OnStashFound(a, it, f);
                        NextStep(a); return true;
                    }
            }
            return false;
        }

        /// <summary>An investigator found something hidden: they look at it, say so (loudly if it is a weapon or bloody), grow
        /// suspicious of whoever's room it was in; the player hears of it when nearby.</summary>
        void OnStashFound(Actor a, Item it, Furniture f)
        {
            var p = Concealment.PlaceFor(f); string where = p?.Where ?? "구석";
            bool bad = it.Def?.IsWeapon == true || it.Bloody || it.Def?.Tag == "poison" || it.Def?.Tag == "sedate";
            Evidences.ExamineItem(this, a, it);
            var room = S.Layout.Room(f.Room);
            if (bad && room?.Owner != null && room.Owner != a.Id)
            {
                var k = S.K(a.Id); k.Suspicion[room.Owner] = (k.Suspicion.TryGetValue(room.Owner, out var s0) ? s0 : 0f) + (it.Bloody ? 0.3f : 0.18f);
                k.Facts.Add($"found-stash:{it.Id}:{f.Id}:{room.Owner}");
            }
            Speak(a, bad ? "stash_found_bad" : "stash_found", null, new Dictionary<string, string> { { "item", it.Kor }, { "place", where } },
                prop: bad ? new Prop { Kind = PropKind.ItemState, Item = it.Type, Room = f.Room, Value = "숨겨짐", T0 = S.Clock, T1 = S.Clock } : null, loud: bad);
            var me = S.Player;
            if (me != null && me.Alive && me.Pos.f == a.Pos.f && (me.Room == a.Room || me.Pos.DistXZ(a.Pos) < 12f))
                S.Emit(GameEventType.Notice, Cast.Player, a.Id, text: LineBank.FixParticles($"{Given(a.Id)}이(가) {where}에서 {it.Kor}을(를) 찾아냈다"), key: "stash_found");
        }

        // ------------------------------------------------------------------ investigators search hiding places (Movement.Facilities)
        /// <summary>During an investigation an idle, observant person now and then goes through a hiding place near the scene
        /// (drawers, under the mattress, behind the books, the soil of a planter): a stash can be found by someone other than the
        /// player. Each person searches a place at most once per chapter and a few places in all.</summary>
        [NonSerialized] readonly Dictionary<int, int> _searchTier = new Dictionary<int, int>();
        internal void ConcealTick()
        {
            if (S.Phase != Phase.Investigation) return;
            List<int> tiers = null;
            // (cheap every tick: only someone who has just finished what they were doing is considered — actor order is fixed)
            foreach (var a in S.Actors.Values)
            {
                if (a.Act != null || a.IsPlayer || a.IsButler || !a.Alive || a.Status != ActorStatus.Active || a.Pose == Pose.Sleep || a.PlanId != null || a.TalkingTo != null || a.Carrying != null || a.CarriedBy != null) continue;
                float eye = (a.Def?.Obs ?? 50) / 100f, cur = a.Def?.P.Curiosity ?? 0.5f;
                if (eye < 0.45f && cur < 0.5f) continue;
                string ck = $"csearch:{a.Id}:{S.Loop}:{S.Chapter}";
                int done = S.Flags.TryGetValue(ck, out var dn) ? (int)dn : 0; if (done >= 3) continue;
                if (tiers == null)
                {
                    // where to look, in order: the scene (tier 0), the rooms around it (1), the rooms things are usually hidden in —
                    // closets, stores, the laundry, the greenhouse, the wine cellar, empty guest rooms (2)
                    tiers = new List<int>(); _searchTier.Clear();
                    foreach (var inc in S.Incidents.Values)
                    {
                        if (inc.Loop != S.Loop || inc.Chapter != S.Chapter || !inc.Confirmed) continue;
                        var v = S.A(inc.Victim); int r = v != null && v.Room >= 0 ? v.Room : inc.FoundRoom; if (r >= 0 && !tiers.Contains(r)) { tiers.Add(r); _searchTier[r] = 0; }
                    }
                    foreach (var r0 in tiers.ToList()) foreach (var n in S.Layout.Neighbors(r0)) if (!tiers.Contains(n)) { tiers.Add(n); _searchTier[n] = 1; }
                    foreach (var r in S.Layout.Rooms)
                        if (!tiers.Contains(r.Id) && (r.Type == RoomType.Closet || r.Type == RoomType.Storage || r.Type == RoomType.Laundry || r.Type == RoomType.Greenhouse || r.Type == RoomType.WineCellar || r.Type == RoomType.GuestRoom)) { tiers.Add(r.Id); _searchTier[r.Id] = 2; }
                }
                if (tiers.Count == 0) return;
                var rng = S.R(Stream.Conceal);
                if (!rng.Chance(0.12 + eye * 0.25 + cur * 0.15)) continue;
                // and the room of whoever they suspect most (tier 1): what's under that mattress?
                var ks = S.K(a.Id); string sus = null; float sv = 0.2f;
                foreach (var kv in ks.Suspicion) if (kv.Value > sv && kv.Key != a.Id && S.A(kv.Key)?.Alive == true) { sv = kv.Value; sus = kv.Key; }
                int susRoom = sus != null ? S.Layout.BedroomOf(sus)?.Id ?? -1 : -1;
                Furniture pick = null; float bd = float.MaxValue;
                foreach (var rid in susRoom >= 0 && !tiers.Contains(susRoom) ? tiers.Append(susRoom) : tiers)
                {
                    var room = S.Layout.Room(rid); if (room == null || room.Furniture.Count == 0) continue;
                    int tier = rid == susRoom ? 1 : _searchTier.TryGetValue(rid, out var tv) ? tv : 2;
                    foreach (var fid in room.Furniture)
                    {
                        var f = S.Layout.Furniture[fid]; if (f.Pos.f != a.Pos.f && tier > 0 || !Concealment.IsPlace(f) || ks.Facts.Contains($"searched:{f.Id}:{S.Loop}:{S.Chapter}")) continue;
                        float d = a.Pos.Dist(f.Pos) + rng.F() * 4f + tier * 8f;
                        if (d < bd || d == bd && pick != null && f.Id < pick.Id) { bd = d; pick = f; }
                    }
                }
                if (pick == null) continue;
                S.Flags[ck] = done + 1;
                S.K(a.Id).Facts.Add($"searched:{pick.Id}:{S.Loop}:{S.Chapter}");
                var pl = Concealment.PlaceFor(pick);
                var act = new Activity { Id = $"inv:csearch:{pick.Id}:{S.Tick}", Label = LineBank.FixParticles($"{pl.Where} 뒤지는 중"), Priority = 10 };
                act.Steps.Add(GoTo(FrontOf(pick)));
                act.Steps.Add(new ActionStep { Kind = "C_Search", Furniture = pick.Id, Duration = 1.2 + pl.Seconds * 0.5 });
                Assign(a, act);
                a.NextThink = S.Clock + 0.5;
                S.Log("SearchStart", a.Id, room: pick.Room, data: $"f{pick.Id}:{pick.Type}", secret: true);
            }
        }

        // ------------------------------------------------------------------ the player's side (validated commands)
        /// <summary>Q: the thing in hand goes out of sight (prefer: a particular part of the clothes).</summary>
        public ConcealResult PlayerConceal(Item it, BodySlot prefer = BodySlot.None) => P == null ? new ConcealResult { Why = "지금은 할 수 없다" } : DoConceal(P, it, prefer);
        /// <summary>Q (hands free) / the notebook: take a concealed thing out into a free hand.</summary>
        public ConcealResult PlayerDraw(Item it) => P == null ? new ConcealResult { Why = "지금은 할 수 없다" } : DoDraw(P, it);
        /// <summary>E at a hiding place with something in hand (or from the notebook): put it away there.</summary>
        public ConcealResult PlayerStash(Item it, Furniture f) => P == null ? new ConcealResult { Why = "지금은 할 수 없다" } : DoStash(P, it, f);
        /// <summary>Take back a stashed thing (standing at its place).</summary>
        public ConcealResult PlayerRetrieve(Item it) => P == null ? new ConcealResult { Why = "지금은 할 수 없다" } : DoRetrieve(P, it);

        /// <summary>R at a hiding place: a close look turns up whatever is hidden there (prying in someone else's room is noticed).</summary>
        public ConcealResult PlayerSearchStash(Furniture f)
        {
            var me = P; if (me == null || f == null) return new ConcealResult { Why = "뒤질 데가 없다" };
            var res = DoSearch(me, f, false);
            foreach (var it in res.Found) if (it.Bloody || it.Washed || it.Def?.IsWeapon == true || it.Def?.Tag == "poison" || it.Def?.Tag == "sedate" || it.KeyFor != null) Evidences.ExamineItem(this, me, it);
            var room = S.Layout.Room(f.Room);
            if (room?.Owner != null && room.Owner != Cast.Player)
            {
                string pryKey = $"pry:{f.Id}:{S.Day}";
                var seen = Watchers(me, 0.9f).Select(x => x.who).Where(x => !x.IsPlayer).ToList();
                if (seen.Count > 0 && !S.Flags.ContainsKey(pryKey))
                {
                    S.Flags[pryKey] = S.Clock;
                    foreach (var w in seen) Relations.Change(S, w.Id, Cast.Player, like: w.Id == room.Owner ? -0.08f : -0.02f, trust: w.Id == room.Owner ? -0.07f : -0.03f, memory: w.Id == room.Owner ? "민혁이 내 방 물건을 뒤졌다" : "민혁이 남의 방을 뒤지는 걸 봤다");
                    var first = seen.FirstOrDefault(x => x.Id == room.Owner) ?? seen[0];
                    res.SeenBy = first.Id; res.Text += LineBank.FixParticles($" — {Given(first.Id)}이(가) 그걸 봤다");
                }
            }
            return res;
        }

        /// <summary>The prompt for the thing in hand: what Q would do ("품에 숨기기", "주머니에 넣기", "크로스백에 넣기") or why it can't.</summary>
        public (string label, bool can, BodySlot slot) ConcealPrompt(Item it)
        {
            var me = P; if (me == null || it == null) return (null, false, BodySlot.None);
            var slot = Concealment.BestSlot(S, me, it, out var why);
            if (slot == BodySlot.None) return (why, false, slot);
            string word = Concealment.SlotWord(me.Id, slot);
            return (slot == BodySlot.Coat ? "품에 숨기기" : LineBank.FixParticles($"{word}에 넣기"), true, slot);
        }

        /// <summary>The prompt for putting the thing in hand into this piece of furniture ("넣어 두기 (매트리스 밑)") or why not.</summary>
        public (string label, bool can) StashPrompt(Furniture f, Item it)
        {
            var p = Concealment.PlaceFor(f); if (p == null || it == null) return (null, false);
            if (!Concealment.CanStash(S, f, it, out var why)) return (why, false);
            return ($"넣어 두기 ({p.Where})", true);
        }

        /// <summary>What the player has concealed on the body, pocket first (for the notebook and the quick-draw wheel).</summary>
        public List<(Item it, BodySlot slot)> PlayerConcealed()
        {
            var me = P; var l = new List<(Item, BodySlot)>(); if (me == null) return l;
            foreach (var id in me.Pocket) { var it = S.I(id); if (it != null) l.Add((it, Concealment.SlotOf(S, it))); }
            return l.OrderBy(x => x.Item2 == BodySlot.Coat ? 0 : x.Item2 == BodySlot.Bag ? 1 : 2).ThenBy(x => x.Item1.Id, StringComparer.Ordinal).ToList();
        }

        /// <summary>Stashes the player remembers at this piece (theirs or seen), still hidden there as far as they know.</summary>
        public List<Item> PlayerStashesAt(Furniture f)
            => f == null ? new List<Item>() : Concealment.Remembered(S).Where(x => x.f.Id == f.Id).Select(x => x.it).Distinct().ToList();
    }
}
