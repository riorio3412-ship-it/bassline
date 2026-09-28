using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BASSLINE.AuthoringData;
using UnityEngine;

namespace BASSLINE.Authoring
{
    public static partial class MansionBuilder
    {
        static MansionProp Prop(MansionRoom r, string id, string function, Vector3 local, bool movable = false, bool protect = false)
        {
            string stableSuffix = System.Text.RegularExpressions.Regex.Replace(id, "[^A-Za-z0-9_-]", "_");
            var go = new GameObject("OBJ_" + r.RoomId + "_" + stableSuffix); go.transform.SetParent(r.transform.Find("Furniture"), false); go.transform.position = r.FloorCenter + local;
            var prop = go.AddComponent<MansionProp>(); prop.ObjectId = go.name; prop.RoomId = r.RoomId; prop.Function = function; prop.Movable = movable; prop.Protected = protect; prop.UsePoint = go.transform.position + new Vector3(0, 0, -1);
            ProductionImporter.Identity(go, prop.ObjectId, movable ? "MovableFunctionalObject" : "FixedFunctionalObject"); return prop;
        }
        static void Furnish(MansionRoom r)
        {
            var p = r.FloorCenter; var parent = r.transform.Find("Furniture"); float w = r.Bounds.size.x, d = r.Bounds.size.z;
            if (r.RoomId.StartsWith("R_BED_", StringComparison.Ordinal))
            {
                Bed(r, "Bed", new Vector3(-2, 0, 1.8f));
                Desk(r, "Desk", new Vector3(2, 0, 1), 1.8f, .75f);
                Cabinet(r, "Wardrobe", new Vector3(-2.2f, 0, -2.85f), 2, 2.25f, .7f);
                Cabinet(r, "StorageChest", new Vector3(2, 0, -2.7f), 1.4f, .65f, .75f);
                var book = Prop(r, "PersonalNotebook", "개인 수첩", new Vector3(2.3f, .79f, 1), true);
                LocalBox(book.transform, "ClosedNotebook", Vector3.zero, new Vector3(.19f, .028f, .26f), cyan);
                Sign(parent, "RoomNumber", p + new Vector3(0, 1.5f, 3.88f), Quaternion.identity, RoomLabel(r), 2.2f, .36f);
                return;
            }
            if (r.GeometryType == "Corridor")
            {
                // Direction bands follow only actual public connections. No NPC positions are encoded.
                if (w > d)
                {
                    for (float x = -w / 2 + 7; x < w / 2; x += 18)
                        Sign(parent, "Wayfinding", p + new Vector3(x, 2.4f, -d / 2 + .13f), Quaternion.Euler(0, 180, 0), RoomLabel(r), 3, .42f);
                }
                else Sign(parent, "WingSign", p + new Vector3(0, 2.35f, d / 2 - .13f), Quaternion.identity, RoomLabel(r), Mathf.Min(w - .4f, 3), .42f);
                if (r.RoomId == "R_CE" || r.RoomId == "R_CW") Clock(r, new Vector3(0, 2.3f, -2.3f));
                return;
            }
            switch (r.RoomId)
            {
                case "R_HALL":
                    Clock(r, new Vector3(0, 3.8f, 10.8f));
                    foreach (float x in new[] { -7.3f, 7.3f }) Planter(r, "HallPlant" + x, new Vector3(x, 0, -4), .9f, 1.4f);
                    foreach (float x in new[] { -7f, 7f }) Box(parent, "BenchBackRail", p + new Vector3(x, .82f, -8.84f), new Vector3(7, .34f, .12f), wood);
                    Sign(parent, "HallTitle", new Vector3(-7.2f, 3.2f, 10.84f), Quaternion.identity, "B A S S L I N E", 6, .8f);
                    break;
                case "R_ANNOUNCE":
                    var notice = Prop(r, "RulesBoard", "공식 규칙판", new Vector3(0, 0, 1.55f), false, true);
                    LocalBox(notice.transform, "Frame", new Vector3(0, 1.65f, 0), new Vector3(4.4f, 2.7f, .20f), metal);
                    LocalBox(notice.transform, "Face", new Vector3(0, 1.65f, -.12f), new Vector3(4.1f, 2.4f, .035f), stone, false);
                    Sign(notice.transform, "NoticeHeading", new Vector3(0, 2.45f, -.15f), Quaternion.Euler(0, 180, 0), "공식 공지 · 시설 운영", 3.7f, .4f);
                    for (int i = 0; i < 4; i++) LocalBox(notice.transform, "PostedSheet", new Vector3(-1.35f + i * .9f, 1.5f, -.15f), new Vector3(.65f, 1, .01f), cloth, false);
                    var speaker = Prop(r, "AnnouncementSpeaker", "공식 방송기", new Vector3(-2.3f, 3.7f, 1.5f), false, true);
                    LocalBox(speaker.transform, "Speaker", Vector3.zero, new Vector3(.48f, .65f, .35f), metal);
                    break;
                case "R_VEST":
                    Box(parent, "EntranceMat", p + new Vector3(0, .008f, 0), new Vector3(4, .015f, 3), red, false);
                    Cabinet(r, "UmbrellaStand", new Vector3(-3.8f, 0, 2.8f), .6f, .65f, .6f); break;
                case "R_EXT":
                    foreach (float x in new[] { -6f, 6f }) Planter(r, "ExteriorPlanter" + x, new Vector3(x, 0, 5), 1.2f, 1.5f); break;
                case "R_DINING":
                    foreach (float x in new[] { -5f, 0f, 5f }) Desk(r, "DiningTable" + x, new Vector3(x, 0, 0), 1.3f, 3.65f);
                    Cabinet(r, "ServingCounter", new Vector3(0, 0, -4.8f), 6, .9f, 1);
                    for (int i = 0; i < 3; i++) Cup(r, "Cup" + i, new Vector3(-1.2f + i * 1.2f, .96f, -4.8f));
                    Window(r, new Vector3(-7.85f, 2.8f, 0), 7, 2.7f, 90); break;
                case "R_KITCHEN":
                    Cabinet(r, "CookingCounter", new Vector3(0, 0, 3.95f), 7, .9f, 1.25f);
                    Cabinet(r, "SinkCounter", new Vector3(-3.85f, 0, 0), 1.25f, .9f, 5);
                    LocalBox(parent, "SinkBasin", p + new Vector3(-3.85f, .91f, 0), new Vector3(.85f, .1f, 1), metal);
                    Cabinet(r, "Refrigerator", new Vector3(3.7f, 0, 3.3f), 1.1f, 2.15f, 1);
                    Cart(r, "ServingCart", new Vector3(2.7f, 0, -3));
                    var utensil = Prop(r, "CookingTool", "조리 도구", new Vector3(.8f, 1, 3.9f), true); LocalBox(utensil.transform, "Handle", Vector3.zero, new Vector3(.035f, .035f, .28f), wood); break;
                case "R_FOOD": case "R_STORAGE": case "R_COLD": case "R_ABANDON":
                    for (int i = -1; i <= 1; i++) Shelf(r, "StorageRack" + i, new Vector3(i * (w / 2 - 1.5f), 0, d / 2 - .8f), 2.25f, r.RoomId == "R_COLD" ? metal : wood, true);
                    Shelf(r, "SideRack", new Vector3(-w / 2 + 1.4f, 0, -d / 2 + 1.1f), 2.2f, wood, true);
                    if (r.RoomId == "R_STORAGE") Cart(r, "TransportCart", new Vector3(w / 2 - 2, 0, -d / 2 + 1.5f));
                    if (r.RoomId == "R_COLD") Sign(parent, "Temperature", p + new Vector3(0, 1.6f, d / 2 - .15f), Quaternion.identity, "저온 보관 · 상태 확인", 2.6f, .4f);
                    break;
                case "R_LOUNGE":
                    Desk(r, "LowTable", new Vector3(1.5f, 0, -.9f), 2.5f, 1.1f, .43f);
                    var pamphlet = Prop(r, "Pamphlet", "공개 팸플릿", new Vector3(1.5f, .49f, -.9f), true); LocalBox(pamphlet.transform, "Paper", Vector3.zero, new Vector3(.21f, .012f, .28f), stone); break;
                case "R_BATH_A": case "R_BATH_B":
                    for (int i = 0; i < 2; i++)
                    {
                        float x = -.3f + i * 2.25f;
                        Box(parent, "PrivacyPartition", p + new Vector3(x - 1.05f, 1.1f, 1.55f), new Vector3(.12f, 2.2f, 2.5f), stone);
                        Box(parent, "StallFront", p + new Vector3(x, 1.1f, .35f), new Vector3(1.85f, 2.2f, .10f), aged);
                        Cylinder(parent, "SanitaryFixture", p + new Vector3(x, .30f, 1.5f), .35f, .6f, stone);
                    }
                    Cabinet(r, "Washbasin", new Vector3(2.3f, 0, -2.15f), 2.1f, .85f, .85f); break;
                case "R_LIBRARY":
                    for (int i = 0; i < 12; i++)
                    {
                        float angle = (15 + i * 30) * Mathf.Deg2Rad; var position = new Vector3(Mathf.Cos(angle) * 9, 0, Mathf.Sin(angle) * 9);
                        var shelf = Shelf(r, "CircularBookshelf" + i, position, 1.75f, wood, false); shelf.transform.rotation = Quaternion.Euler(0, -i * 30 - 105, 0);
                    }
                    for (int i = 0; i < 4; i++) { float a = (45 + i * 90) * Mathf.Deg2Rad; Desk(r, "ReadingTable" + i, new Vector3(Mathf.Cos(a) * 2.1f, -2, Mathf.Sin(a) * 2.1f), 1.0f, 1); }
                    break;
                case "R_READING":
                    Desk(r, "ReadDeskA", new Vector3(0, 0, 2.7f), 6.2f, .65f); Desk(r, "ReadDeskB", new Vector3(0, 0, 1.1f), 6.2f, .65f);
                    Shelf(r, "Bookshelf", new Vector3(0, 0, -4.15f), 5.8f, wood, false); break;
                case "R_ARCHIVE":
                    for (int i = 0; i < 2; i++)
                    {
                        float x = -2.1f + i * 4.2f; Desk(r, "TerminalDesk" + i, new Vector3(x, 0, -2.3f), 2, .85f);
                        var terminal = Prop(r, "OfficialTerminal" + (i + 1), "공식 기록 열람 단말", new Vector3(x, .79f, -2.3f), false, true);
                        LocalBox(terminal.transform, "Stand", new Vector3(0, .17f, .1f), new Vector3(.12f, .34f, .12f), metal);
                        LocalBox(terminal.transform, "Screen", new Vector3(0, .46f, .12f), new Vector3(.78f, .50f, .065f), black);
                        LocalBox(terminal.transform, "Display", new Vector3(0, .46f, .08f), new Vector3(.70f, .42f, .012f), cyan, false);
                        terminal.UsePoint = p + new Vector3(x, 0, -3.25f);
                    }
                    Cabinet(r, "ProtectedArchive", new Vector3(4.1f, 0, 0), .85f, 2.3f, 3); break;
                case "R_MUSIC":
                    var piano = Prop(r, "Piano", "피아노", new Vector3(-3, 0, -2.8f));
                    LocalBox(piano.transform, "Body", new Vector3(0, .8f, .25f), new Vector3(2.6f, 1.6f, 1), wood);
                    LocalBox(piano.transform, "Keyboard", new Vector3(0, .83f, -.45f), new Vector3(2.35f, .10f, .42f), stone);
                    for (int i = 0; i < 21; i++) LocalBox(piano.transform, "Key", new Vector3(-1.08f + i * .108f, .895f, -.40f), new Vector3(.045f, .04f, .22f), black, false);
                    Cabinet(r, "InstrumentCabinet", new Vector3(4.8f, 0, -3.7f), 1.4f, 2, .6f); break;
                case "R_ARCADE":
                    for (int i = 0; i < 6; i++) ArcadeCabinet(r, i, new Vector3(-5.7f + i * 2.15f, 0, -4.75f), i < 2);
                    Desk(r, "BoardTableA", new Vector3(-3.5f, 0, -.4f), 1.5f, 1.5f); Desk(r, "BoardTableB", new Vector3(1, 0, -.4f), 1.5f, 1.5f);
                    for (int x = -7; x < 8; x++) for (int z = -5; z < 6; z++) if ((x + z) % 2 == 0) Box(parent, "CheckCeiling", p + new Vector3(x + .5f, 4.985f, z + .5f), new Vector3(.99f, .015f, .99f), black, false);
                    for (int i = 0; i < 6; i++) { var cloud = GameObject.CreatePrimitive(PrimitiveType.Sphere); cloud.name = "CloudMural"; cloud.transform.SetParent(parent, false); cloud.transform.position = p + new Vector3(-7.8f, 2.9f, -3 + i); cloud.transform.localScale = new Vector3(.03f, .7f, 1.9f); cloud.GetComponent<Renderer>().sharedMaterial = glass; UnityEngine.Object.DestroyImmediate(cloud.GetComponent<Collider>()); }
                    break;
                case "R_THEATER":
                    Box(parent, "Stage", p + new Vector3(0, .3f, 5.7f), new Vector3(13, .6f, 3.8f), wood);
                    Box(parent, "StageStep", p + new Vector3(-6.1f, .15f, 3.3f), new Vector3(1.1f, .3f, 1), stone);
                    foreach (float x in new[] { -6.8f, 6.8f }) Box(parent, "Curtain", p + new Vector3(x, 2.8f, 5.6f), new Vector3(1, 5.6f, .28f), red);
                    Box(parent, "StageBackdrop", p + new Vector3(0, 3.4f, 7.7f), new Vector3(13, 5.6f, .12f), black); break;
                case "R_EXHIBIT":
                    for (int i = 0; i < 4; i++)
                    {
                        float x = -5 + i * 3.4f; var stand = Prop(r, "Exhibit" + i, "전시 조각", new Vector3(x, 0, 3.3f), false, true);
                        LocalBox(stand.transform, "Plinth", new Vector3(0, .55f, 0), new Vector3(.8f, 1.1f, .8f), stone);
                        var form = Cylinder(stand.transform, "Sculpture", stand.transform.position + Vector3.up * 1.6f, .24f, 1.0f, metal); form.transform.rotation = Quaternion.Euler(20, 0, 12);
                    }
                    for (int i = 0; i < 4; i++) { var frame = Prop(r, "Frame" + i, i == 3 ? "비어 있는 액자" : "전시 액자", new Vector3(-5 + i * 3.4f, 1.8f, -5.85f), false, true); LocalBox(frame.transform, "Border", Vector3.zero, new Vector3(1.8f, 1.4f, .12f), wood); LocalBox(frame.transform, "Canvas", new Vector3(0, 0, .075f), new Vector3(1.6f, 1.2f, .015f), i == 3 ? stone : glass, false); }
                    break;
                case "R_GREEN": case "R_GARDEN": case "R_ROOF":
                    for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2) Planter(r, "PlantBed" + x + "_" + z, new Vector3(x * (w / 2 - 2.7f), 0, z * (d / 2 - 3.1f)), 1.35f, r.RoomId == "R_GREEN" ? 2.2f : 1.3f);
                    if (r.RoomId == "R_GREEN") { Desk(r, "PottingBench", new Vector3(0, 0, -9.4f), 4, 1.2f); Window(r, new Vector3(-8.85f, 4.5f, 0), 17, 3.2f, 90); }
                    break;
                case "R_FLOWER":
                    for (int side = -1; side <= 1; side += 2) for (int i = -4; i <= 4; i++) if (!(side == -1 && i == 0)) Planter(r, "Flower" + side + "_" + i, new Vector3(side * 1.67f, 0, i * 2.3f), .18f, .65f); break;
                case "R_WORK":
                    for (int i = 0; i < 4; i++) Desk(r, "Workbench" + i, new Vector3(-3.2f + i % 2 * 6.4f, 0, -3 + i / 2 * 6), 2.5f, 1.25f, .9f);
                    Cabinet(r, "ToolCabinet", new Vector3(0, 0, 4.2f), 2.8f, 2.3f, .7f);
                    for (int i = 0; i < 5; i++) { var tool = Prop(r, "Tool" + i, "등록 작업 도구", new Vector3(-3.9f + i * .3f, .97f, 3), true); LocalBox(tool.transform, "Handle", Vector3.zero, new Vector3(.06f, .06f, .32f), metal); } break;
                case "R_LAUNDRY":
                    for (int i = 0; i < 3; i++) { var machine = Prop(r, "Washer" + i, "세탁기", new Vector3(-2.8f + i * 2.8f, 0, 2.8f)); LocalBox(machine.transform, "Body", new Vector3(0, .65f, 0), new Vector3(1.25f, 1.3f, 1.1f), stone); var drum = Cylinder(machine.transform, "Door", machine.transform.position + new Vector3(0, .65f, -.58f), .4f, .055f, metal, false); drum.transform.rotation = Quaternion.Euler(90, 0, 0); }
                    Desk(r, "FoldingTable", new Vector3(-2.5f, 0, -2.7f), 3, 1); Cart(r, "LaundryBasket", new Vector3(2.8f, 0, -2.6f)); break;
                case "R_MACHINE": case "R_GEN": case "R_WATER":
                    for (int i = -1; i <= 1; i++) { var machine = Prop(r, "Machine" + i, r.RoomId == "R_GEN" ? "발전기" : "설비 외장", new Vector3(i * 3.25f, 0, d / 2 - 1.8f), false, true); LocalBox(machine.transform, "Casing", new Vector3(0, 1, 0), new Vector3(2, 2, 1.6f), metal); for (int g = 0; g < 4; g++) LocalBox(machine.transform, "Vent", new Vector3(0, .6f + g * .2f, -.82f), new Vector3(1.4f, .045f, .015f), black, false); }
                    var panel = Prop(r, "ControlPanel", "설비 상태 패널", new Vector3(-w / 2 + .25f, 1.4f, -d / 2 + 1.5f), false, true); LocalBox(panel.transform, "Cabinet", Vector3.zero, new Vector3(.35f, 1.7f, 1.6f), metal); panel.UsePoint = p + new Vector3(-w / 2 + 1.2f, 0, -d / 2 + 1.5f);
                    if (r.RoomId == "R_WATER") Desk(r, "SampleTable", new Vector3(1, 0, -2.8f), 2.4f, 1); break;
                case "R_MAINT":
                    for (int i = 0; i < 3; i++) { var pipe = Cylinder(parent, "FixedPipe", p + new Vector3(1.65f, .8f + i * .8f, 0), .11f, 10.5f, metal); pipe.transform.rotation = Quaternion.Euler(90, 0, 0); } break;
                case "R_AID": case "R_RECOVERY":
                    int beds = r.RoomId == "R_AID" ? 2 : 3;
                    for (int i = 0; i < beds; i++) { float x = -3.2f + i * 2.7f; Bed(r, "RestBed" + i, new Vector3(x, 0, -1.6f), 1.2f); if (i < beds - 1) Box(parent, "PrivacyScreen", p + new Vector3(x + 1.1f, .95f, -1.8f), new Vector3(.08f, 1.9f, 2), cloth); }
                    Cabinet(r, "CareSupplies", new Vector3(3.8f, 0, 1.8f), 1.6f, 1.6f, .7f); break;
                case "R_FLOOD":
                    Box(parent, "ShallowWaterSurface", p + new Vector3(0, .12f, 0), new Vector3(3.75f, .015f, 17.7f), water, false);
                    Box(parent, "DryWaitingDeck", p + new Vector3(0, .05f, -7.6f), new Vector3(3.65f, .10f, 2), stone);
                    Sign(parent, "DepthMark", p + new Vector3(1.86f, .7f, 0), Quaternion.Euler(0, 90, 0), "0.12 m", .6f, .25f); break;
                case "R_ANNEX":
                    Cabinet(r, "OldReception", new Vector3(-3, 0, -3.5f), 4, 1.1f, 1);
                    Sign(parent, "OldDirectory", p + new Vector3(0, 1.9f, 5.86f), Quaternion.identity, "별관 안내", 4, 1.3f); break;
                case "R_CLOSED":
                    // Six fixed sealed alcoves, distinct from the eighteen occupied bedrooms.
                    // The 3m corridor is east/west so the actual eastern entry reaches its centre.
                    for (int side = -1; side <= 1; side += 2) for (int i = 0; i < 3; i++)
                    {
                        float x = -4.5f + i * 4.5f, z = side * 1.6f;
                        Box(parent, "SealedAlcoveDoor", p + new Vector3(x, 1.2f, z), new Vector3(1.2f, 2.4f, .15f), wood);
                        Box(parent, "AlcoveLintel", p + new Vector3(x, 3, z), new Vector3(1.2f, 1.2f, .16f), aged);
                        for (int jamb = -1; jamb <= 1; jamb += 2) Box(parent, "AlcoveWall", p + new Vector3(x + jamb * 1.425f, 1.8f, z), new Vector3(1.65f, 3.6f, .16f), aged);
                        Box(parent, "AlcoveDivider", p + new Vector3(x - 2.25f, 1.8f, side * 4.8f), new Vector3(.16f, 3.6f, 6.4f), aged);
                        Sign(parent, "AlcoveNumber", p + new Vector3(x, 2.6f, z - side * .1f), Quaternion.Euler(0, side > 0 ? 0 : 180, 0), "C" + (i + (side > 0 ? 4 : 1)), 1, .25f);
                    }
                    break;
                case "R_MEETING":
                    Desk(r, "MeetingTableA", new Vector3(0, 0, 2.7f), 8.8f, .65f); Desk(r, "MeetingTableB", new Vector3(-1.5f, 0, 1.1f), 5.8f, .65f);
                    var board = Prop(r, "MeetingBoard", "회의 칠판", new Vector3(0, 1.7f, -4.8f)); LocalBox(board.transform, "Board", Vector3.zero, new Vector3(5, 2, .12f), black); break;
                case "R_OBSERVE":
                    Window(r, new Vector3(-6.85f, 2.8f, 0), 9, 3.5f, 90); Window(r, new Vector3(0, 2.8f, -5.85f), 11, 3.5f, 180); break;
                case "R_ASTRO":
                    var telescope = Prop(r, "Telescope", "관측 망원경", new Vector3(-3, 0, -2));
                    Cylinder(telescope.transform, "Mount", telescope.transform.position + Vector3.up * .7f, .1f, 1.4f, metal);
                    var tube = Cylinder(telescope.transform, "TelescopeTube", telescope.transform.position + Vector3.up * 1.7f, .24f, 2.1f, metal); tube.transform.rotation = Quaternion.Euler(65, 25, 0);
                    Desk(r, "StarChartDesk", new Vector3(2.5f, 0, -4.7f), 3, 1); break;
                case "R_TRIAL":
                    var stage = Prop(r, "YustiPresentationPosition", "유스티 연출 위치", new Vector3(0, 0, 0), false, true);
                    Cylinder(stage.transform, "CentralPlinth", p + Vector3.up * .16f, 1.5f, .32f, black);
                    // Neutral mannequin proxy is a prop, not a nineteenth participant.
                    var mannequin = Prop(r, "NeutralMannequin", "중립 마네킹", new Vector3(0, 0, 2), false, true);
                    Cylinder(mannequin.transform, "MannequinBody", mannequin.transform.position + Vector3.up * .95f, .22f, 1.2f, cloth);
                    var head = GameObject.CreatePrimitive(PrimitiveType.Sphere); head.name = "NeutralHead"; head.transform.SetParent(mannequin.transform, false); head.transform.localPosition = new Vector3(0, 1.7f, 0); head.transform.localScale = Vector3.one * .31f; head.GetComponent<Renderer>().sharedMaterial = cloth;
                    break;
            }
        }

        static void Desk(MansionRoom r, string id, Vector3 pos, float w, float d, float height = .74f)
        {
            var prop = Prop(r, id, "작업 면 / 테이블", pos);
            LocalBox(prop.transform, "Top", new Vector3(0, height, 0), new Vector3(w, .08f, d), wood);
            foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 }) LocalBox(prop.transform, "Leg", new Vector3(x * (w / 2 - .10f), height / 2, z * (d / 2 - .10f)), new Vector3(.08f, height, .08f), metal);
        }
        static void Cabinet(MansionRoom r, string id, Vector3 pos, float w, float height, float d)
        {
            var prop = Prop(r, id, id, pos); LocalBox(prop.transform, "Carcase", new Vector3(0, height / 2, 0), new Vector3(w, height, d), r.WingId == "WRK" ? metal : wood);
            int panels = Mathf.Max(1, Mathf.RoundToInt(w / .75f));
            for (int i = 0; i < panels; i++) { LocalBox(prop.transform, "DoorPanel", new Vector3(-w / 2 + w / panels * (i + .5f), height / 2, -d / 2 - .013f), new Vector3(w / panels - .035f, height - .06f, .02f), aged, false); LocalBox(prop.transform, "Pull", new Vector3(-w / 2 + w / panels * (i + .6f), height * .6f, -d / 2 - .045f), new Vector3(.04f, .17f, .03f), metal, false); }
        }
        static MansionProp Shelf(MansionRoom r, string id, Vector3 pos, float width, Material finish, bool boxes)
        {
            var prop = Prop(r, id, boxes ? "보관 선반" : "서가", pos);
            for (int side = -1; side <= 1; side += 2) LocalBox(prop.transform, "Upright", new Vector3(side * width / 2, 1.05f, 0), new Vector3(.06f, 2.1f, .65f), finish);
            LocalBox(prop.transform, "Back", new Vector3(0, 1.05f, .30f), new Vector3(width, 2.1f, .04f), finish);
            for (int shelf = 0; shelf < 4; shelf++)
            {
                LocalBox(prop.transform, "Shelf", new Vector3(0, .2f + shelf * .52f, 0), new Vector3(width, .05f, .65f), finish);
                int count = boxes ? 3 : 10;
                for (int i = 0; i < count; i++) LocalBox(prop.transform, boxes ? "StorageBox" : "Book", new Vector3(-width * .43f + i * width * .86f / count, .4f + shelf * .52f, 0), new Vector3(boxes ? width / 4.3f : width / 16, boxes ? .28f : .32f, boxes ? .45f : .23f), i % 3 == 0 ? cyan : i % 3 == 1 ? red : cloth, false);
            }
            return prop;
        }
        static void Bed(MansionRoom r, string id, Vector3 pos, float width = 1.45f)
        {
            var prop = Prop(r, id, "침대 / 휴식", pos); LocalBox(prop.transform, "BedFrame", new Vector3(0, .25f, 0), new Vector3(width + .1f, .5f, 2.2f), wood);
            LocalBox(prop.transform, "Mattress", new Vector3(0, .57f, 0), new Vector3(width, .22f, 2.06f), cloth);
            LocalBox(prop.transform, "Pillow", new Vector3(0, .73f, .68f), new Vector3(width * .65f, .13f, .43f), stone, false);
            LocalBox(prop.transform, "Headboard", new Vector3(0, .65f, 1.12f), new Vector3(width + .13f, 1.3f, .10f), wood);
            var sleep = new GameObject("A_" + r.RoomId.Substring(2) + "_" + id + "_REST"); sleep.transform.SetParent(r.transform.Find("Anchors"), false); sleep.transform.position = prop.transform.position + Vector3.up * .69f;
            var a = sleep.AddComponent<MansionAnchor>(); a.AnchorId = sleep.name; a.RoomId = r.RoomId; a.InteractionType = "Rest"; a.FurnitureId = prop.ObjectId; a.ApproachPoint = prop.transform.position + new Vector3(width / 2 + .7f, 0, -.1f); anchors.Add(a);
        }
        static void Cup(MansionRoom r, string id, Vector3 pos)
        {
            var prop = Prop(r, id, "컵", pos, true); Cylinder(prop.transform, "Cup", prop.transform.position, .055f, .13f, stone);
        }
        static void Planter(MansionRoom r, string id, Vector3 pos, float radius, float height)
        {
            var prop = Prop(r, id, "고정 식재", pos); Cylinder(prop.transform, "Pot", prop.transform.position + Vector3.up * .25f, radius, .5f, stone);
            for (int i = 0; i < 3; i++)
            {
                var plant = GameObject.CreatePrimitive(PrimitiveType.Sphere); plant.name = "PurplePlantMass"; plant.transform.SetParent(prop.transform, false);
                plant.transform.localPosition = new Vector3((i - 1) * radius * .32f, .45f + height * .38f + i * .12f, 0);
                plant.transform.localScale = new Vector3(radius * 1.35f, height, radius * 1.25f); plant.GetComponent<Renderer>().sharedMaterial = purple;
            }
        }
        static void Cart(MansionRoom r, string id, Vector3 pos)
        {
            var prop = Prop(r, id, "운반 카트", pos, true); LocalBox(prop.transform, "Basket", new Vector3(0, .55f, 0), new Vector3(.75f, .6f, 1.1f), metal);
            LocalBox(prop.transform, "Handle", new Vector3(0, 1, .5f), new Vector3(.7f, .05f, .05f), metal);
            foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 }) Cylinder(prop.transform, "Wheel", prop.transform.position + new Vector3(x * .32f, .12f, z * .43f), .12f, .1f, black, false).transform.rotation = Quaternion.Euler(0, 0, 90);
        }
        static void Window(MansionRoom r, Vector3 local, float width, float height, float yaw)
        {
            var prop = Prop(r, "Window" + shapeNumber++, "고정 하늘 창", local, false, true); prop.transform.rotation = Quaternion.Euler(0, yaw, 0);
            LocalBox(prop.transform, "Frame", Vector3.zero, new Vector3(width, height, .07f), metal, false);
            LocalBox(prop.transform, "Glazing", new Vector3(0, 0, -.05f), new Vector3(width - .15f, height - .15f, .015f), glass, false);
            for (float x = -width / 2 + 1.5f; x < width / 2; x += 1.5f) LocalBox(prop.transform, "Mullion", new Vector3(x, 0, -.065f), new Vector3(.055f, height, .04f), metal, false);
        }
        static void Clock(MansionRoom r, Vector3 local)
        {
            var prop = Prop(r, "OfficialClock", "공식 시계", local, false, true);
            var face = Cylinder(prop.transform, "ClockFace", prop.transform.position, .55f, .08f, stone, false); face.transform.rotation = Quaternion.Euler(90, 0, 0);
            LocalBox(prop.transform, "MinuteHand", new Vector3(0, .17f, -.065f), new Vector3(.025f, .34f, .02f), black, false);
            var hour = LocalBox(prop.transform, "HourHand", new Vector3(.1f, .06f, -.08f), new Vector3(.025f, .25f, .02f), black, false); hour.transform.localRotation = Quaternion.Euler(0, 0, -60);
        }
        static void ArcadeCabinet(MansionRoom r, int i, Vector3 pos, bool rhythm)
        {
            var prop = Prop(r, "Arcade" + i, rhythm ? "리듬 게임기" : "게임 캐비닛", pos);
            LocalBox(prop.transform, "Body", new Vector3(0, .92f, 0), new Vector3(1.4f, 1.84f, .9f), metal);
            LocalBox(prop.transform, "Display", new Vector3(0, 1.3f, .47f), new Vector3(1.15f, .65f, .025f), black, false);
            for (int k = 0; k < 4; k++) LocalBox(prop.transform, "ScreenBar", new Vector3(-.4f + k * .27f, 1.3f, .49f), new Vector3(.035f, .38f, .01f), cyan, false);
            LocalBox(prop.transform, "ControlDeck", new Vector3(0, .82f, .55f), new Vector3(1.35f, .15f, .35f), stone);
            if (rhythm) LocalBox(prop.transform, "DancePad", new Vector3(0, .035f, 1.2f), new Vector3(1.45f, .07f, 1.1f), cyan);
        }

        static void CreateSeats()
        {
            foreach (var row in CsvTable.Load(Path.Combine(ProductionImporter.SourceRoot, "02_MANSION/BASSLINE_SEAT_ANCHORS.csv")))
            {
                var r = rooms[row["RoomID"]]; var ground = r.FloorCenter + new Vector3(N(row, "LocalX"), N(row, "LocalY"), N(row, "LocalZ"));
                var chair = new GameObject(row["FurnitureBinding"]); chair.transform.SetParent(r.transform.Find("Seats"), false); chair.transform.position = ground; chair.transform.rotation = Quaternion.Euler(0, N(row, "Yaw"), 0);
                float h = N(row, "SeatHeight"); var finish = r.RoomId == "R_TRIAL" ? black : r.RoomId == "R_THEATER" ? red : cloth;
                LocalBox(chair.transform, "Seat", new Vector3(0, h - .05f, 0), new Vector3(.58f, .1f, .56f), finish);
                LocalBox(chair.transform, "Back", new Vector3(0, h + .26f, -.25f), new Vector3(.58f, .56f, .09f), finish);
                foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 }) LocalBox(chair.transform, "Leg", new Vector3(x * .23f, h / 2, z * .21f), new Vector3(.045f, h, .045f), metal);
                ProductionImporter.Identity(chair, row["FurnitureBinding"], "FixedSeatFurniture");
                var go = new GameObject(row["SeatID"]); go.transform.SetParent(chair.transform, false); go.transform.localPosition = Vector3.up * h;
                var a = go.AddComponent<MansionAnchor>(); a.AnchorId = row["SeatID"]; a.RoomId = r.RoomId; a.FurnitureId = row["FurnitureBinding"]; a.InteractionType = "Seat"; a.ApproachClearance = N(row, "ApproachClearance");
                a.ApproachPoint = ground - chair.transform.forward * .95f;
                a.LookTarget = Socket(go.transform, "LookTarget", new Vector3(0, .9f, 2));
                a.LeftHand = Socket(go.transform, "Hand_L", new Vector3(-.19f, .18f, .23f)); a.RightHand = Socket(go.transform, "Hand_R", new Vector3(.19f, .18f, .23f));
                a.LeftFoot = Socket(go.transform, "Foot_L", new Vector3(-.16f, -h, .38f)); a.RightFoot = Socket(go.transform, "Foot_R", new Vector3(.16f, -h, .38f)); a.SocketsResolved = true;
                anchors.Add(a);
                if (r.RoomId == "R_TRIAL")
                {
                    LocalBox(chair.transform, "SpeakingPodium", new Vector3(0, .56f, 1.1f), new Vector3(.85f, 1.12f, .55f), metal);
                    LocalBox(chair.transform, "PodiumFace", new Vector3(0, 1.13f, 1.1f), new Vector3(.88f, .06f, .58f), black);
                }
            }
        }
        static Transform Socket(Transform parent, string name, Vector3 p)
        {
            var t = Group(parent, name); t.localPosition = p; return t;
        }
        static void CreateAnchors()
        {
            foreach (var row in CsvTable.Load(Path.Combine(ProductionImporter.SourceRoot, "02_MANSION/BASSLINE_INTERACTION_ANCHORS.csv")))
            {
                var r = rooms[row["RoomID"]]; var go = new GameObject(row["AnchorID"]); go.transform.SetParent(r.transform.Find("Anchors"), false);
                go.transform.position = r.FloorCenter + new Vector3(N(row, "LocalX"), N(row, "LocalY"), N(row, "LocalZ")); go.transform.rotation = Quaternion.Euler(0, N(row, "Yaw"), 0);
                var a = go.AddComponent<MansionAnchor>(); a.AnchorId = row["AnchorID"]; a.RoomId = r.RoomId; a.InteractionType = row["InteractionType"]; a.ParticipantSlots = (int)N(row, "ParticipantSlots"); a.ApproachPoint = go.transform.position;
                if(r.RoomId=="R_WORK"&&a.InteractionType=="Work"&&a.AnchorId.StartsWith("A_WORK_BENCH",StringComparison.Ordinal)){
                    int bench=int.Parse(a.AnchorId.Substring("A_WORK_BENCH".Length))-1;
                    var furniture=r.GetComponentsInChildren<MansionProp>().Single(p=>p.ObjectId=="OBJ_R_WORK_Workbench"+bench);
                    a.FurnitureId=furniture.ObjectId;
                    var top=furniture.transform.Find("Top");
                    a.ApproachPoint=new Vector3(top.position.x,r.FloorCenter.y,top.position.z-top.lossyScale.z*.5f-.42f);
                    go.transform.position=a.ApproachPoint;go.transform.rotation=Quaternion.identity;
                }
                a.LookTarget = Socket(go.transform, "LookTarget", Vector3.up * 1.6f + Vector3.forward); a.LeftFoot = Socket(go.transform, "Foot_L", Vector3.left * .16f); a.RightFoot = Socket(go.transform, "Foot_R", Vector3.right * .16f);
                // Generic activity rows do not invent hand sockets. Furniture-specific bindings resolve them separately.
                anchors.Add(a); if (a.InteractionType == "Observe") QueryOrigin(r, "OZ_" + r.RoomId + "_ACT", go.transform.position);
            }
            var pool = rooms["R_POOL"];
            foreach (int sign in new[] { -1, 1 })
            {
                var go = new GameObject("A_POOL_LANE_" + (sign > 0 ? "N" : "S")); go.transform.SetParent(pool.transform.Find("Anchors"), false); go.transform.position = pool.FloorCenter + new Vector3(0, -.7f, sign * 6.5f);
                var a = go.AddComponent<MansionAnchor>(); a.AnchorId = go.name; a.RoomId = pool.RoomId; a.InteractionType = "SwimLane"; a.ApproachPoint = pool.FloorCenter + new Vector3(0, 0, 9); anchors.Add(a);
            }
        }

        static void Illuminate(MansionRoom r)
        {
            if (r.GeometryType == "Subzone" || r.GeometryType == "PerimeterRing") { r.Lights = Array.Empty<Light>(); return; }
            var parent = r.transform.Find("Lighting"); var list = new List<Light>(); float w = r.Bounds.size.x, d = r.Bounds.size.z;
            float height = r.CeilingHeight == 0 ? 3.5f : Mathf.Min(r.CeilingHeight - .3f, 5.3f);
            int nx = Mathf.Max(1, Mathf.CeilToInt(w / 9)), nz = Mathf.Max(1, Mathf.CeilToInt(d / 9));
            if (r.RoomId == "R_TRIAL")
            {
                foreach (var seat in anchors.Where(a => a.RoomId == r.RoomId && a.InteractionType == "Seat"))
                {
                    var light = new GameObject("TrialSeatLight").AddComponent<Light>(); light.transform.SetParent(parent, false); light.transform.position = seat.transform.position + Vector3.up * 4;
                    light.type = LightType.Spot; light.transform.rotation = Quaternion.Euler(90, 0, 0); light.spotAngle = 44; light.range = 6; light.intensity = 4; light.color = new Color(1, .91f, .79f); light.shadows = LightShadows.None; list.Add(light);
                }
            }
            else if (r.RoomId == "R_STAIR")
            {
                foreach (int floor in new[] { -6, 0, 6, 12 }) AddLamp(new Vector3(-6.5f, floor + 2.9f, 16), 8, 2.4f);
            }
            else for (int ix = 0; ix < nx; ix++) for (int iz = 0; iz < nz; iz++)
            {
                var point = r.FloorCenter + new Vector3(-w / 2 + w * (ix + .5f) / nx, height, -d / 2 + d * (iz + .5f) / nz);
                if (r.RoomId == "R_LIBRARY" && (new Vector2(point.x - r.FloorCenter.x, point.z - r.FloorCenter.z)).magnitude > 8.5f) continue;
                AddLamp(point, Mathf.Max(8, height * 2.5f), r.RoomId == "R_HALL" ? 3 : 2);
            }
            r.Lights = list.ToArray();
            void AddLamp(Vector3 point, float range, float intensity)
            {
                var go = new GameObject("LIGHT_" + r.RoomId + "_" + list.Count.ToString("D2")); go.transform.SetParent(parent, false); go.transform.position = point;
                var light = go.AddComponent<Light>(); light.type = LightType.Point; light.range = range; light.intensity = intensity; light.shadows = LightShadows.None;
                light.color = r.LightingProfile == "LP_Privacy" ? new Color(1, .92f, .82f) : r.WingId == "AQU" ? new Color(.79f, .91f, 1) : new Color(.94f, .97f, 1);
                list.Add(light); LocalBox(go.transform, "Luminaire", new Vector3(0, .04f, 0), new Vector3(1.1f, .07f, .34f), lightFace, false);
            }
        }
    }
}
