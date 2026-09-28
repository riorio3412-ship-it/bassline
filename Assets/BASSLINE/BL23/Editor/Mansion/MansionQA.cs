using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BL23.Game.Mansion;
using BL23.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BL23.EditorTools.Mansion
{
    /// <summary>
    /// Batch QA: generates several loops, builds the mansion, renders screenshots of every room type (doorway eye-level +
    /// wide), grand hall angles, stairs, courtroom, darkness and blackout shots, and logs build time / scene stats.
    /// Unity -batchmode -projectPath lab -executeMethod BL23.EditorTools.Mansion.MansionQA.Run -quit
    /// Optional args: -qaLoops 1,2,3  -qaOnly Hall,Dining  -qaOut path  -qaW 1600 -qaH 900
    /// </summary>
    public static class MansionQA
    {
        static string _out = "Shots";
        static int _w = 1600, _h = 900;
        static Camera _cam;
        static RenderTexture _rt;
        static MansionView _view;
        static readonly StringBuilder _log = new StringBuilder();
        static HashSet<string> _only;

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        [MenuItem("BL23/Mansion/QA screenshots")]
        public static void Run()
        {
            var loops = (Arg("-qaLoops") ?? "1,2,3").Split(',').Select(int.Parse).ToArray();
            var only = Arg("-qaOnly");
            _only = only != null ? new HashSet<string>(only.Split(',')) : null;
            _out = Arg("-qaOut") ?? Path.Combine(Directory.GetCurrentDirectory(), "Shots");
            if (int.TryParse(Arg("-qaW"), out int w)) _w = w;
            if (int.TryParse(Arg("-qaH"), out int h)) _h = h;
            Directory.CreateDirectory(_out);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("QACam");
            _cam = camGo.AddComponent<Camera>();
            MansionAtmosphere.SetupCamera(_cam);
            _cam.fieldOfView = 62f;
            _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            _rt.Create();
            foreach (int loop in loops)
            {
                foreach (var sd in (Arg("-qaSeeds") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).DefaultIfEmpty(null))
                    try { DoLoop(loop, sd); }
                catch (Exception e) { Debug.LogException(e); _log.AppendLine("EXCEPTION loop " + loop + ": " + e); }
            }
            File.WriteAllText(Path.Combine(_out, "qa_log.txt"), _log.ToString());
            Debug.Log("[MansionQA]\n" + _log);
        }

        static bool Want(string key) => _only == null || _only.Any(o => key.IndexOf(o, StringComparison.OrdinalIgnoreCase) >= 0);

        static void DoLoop(int loop, string seedList = null)
        {
            ulong seed = 20260926UL;
            var sArg = seedList ?? Arg("-qaSeed"); if (sArg != null) seed = sArg.StartsWith("0x") ? Convert.ToUInt64(sArg.Substring(2), 16) : ulong.Parse(sArg);
            var rngs = new RngSet { CampaignSeed = seed };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var L = LayoutGenerator.Generate(rngs.CampaignSeed, loop, rngs);
            double gen = sw.Elapsed.TotalMilliseconds;
            // -qaLogger GameRoom : hang a door register (normally added by the running campaign) beside one door of that room type
            var lg = Arg("-qaLogger");
            if (lg != null)
                foreach (var r in L.Rooms.Where(r => r.Type.ToString() == lg && r.Doors.Count > 0).Take(1))
                {
                    var d = L.Doors[r.Doors[0]];
                    var inward = d.AlongX ? new P3(d.Pos.f, d.Pos.x + 0.9f, d.Pos.z + (r.Rect.CZ > d.Pos.z ? 0.35f : -0.35f)) : new P3(d.Pos.f, d.Pos.x + (r.Rect.CX > d.Pos.x ? 0.35f : -0.35f), d.Pos.z + 0.9f);
                    var f = new Furniture { Id = L.Furniture.Count, Room = r.Id, Type = "DoorLogger", Pos = inward, Yaw = 0, W = 0.3f, D = 0.15f, H = 0.5f, Blocks = false, Material = Mat.Metal };
                    L.Furniture.Add(f); r.Furniture.Add(f.Id);
                }
            if (_view != null) UnityEngine.Object.DestroyImmediate(_view.gameObject);
            sw.Restart();
            _view = MansionView.Build(L, null);
            double build = sw.Elapsed.TotalMilliseconds;
            if (Arg("-qaRebuild") != null)
            {
                UnityEngine.Object.DestroyImmediate(_view.gameObject);
                sw.Restart(); _view = MansionView.Build(L, null); double warm = sw.Elapsed.TotalMilliseconds;
                _log.AppendLine($"warm rebuild {warm:0} ms"); _log.AppendLine(_view.Stats.ToString());
            }
            _view.ViewCamera = _cam;
            // -qaHour 13 : daylight through the glass as at that hour (default: night)
            if (float.TryParse(Arg("-qaHour") ?? "", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float hour)) { MansionView.HourOverride = hour; _view.RefreshDaylight(); }
            else { MansionView.HourOverride = -1f; _view.RefreshDaylight(); }
            _log.AppendLine($"=== loop {loop} skeleton={L.Skeleton} rooms={L.Rooms.Count} furniture={L.Furniture.Count} doors={L.Doors.Count} mystery=[{string.Join(",", L.MysteryTypes)}]");
            _log.AppendLine($"layout gen {gen:0} ms, mansion build {build:0} ms");
            _log.AppendLine(_view.Stats.ToString());
            foreach (var r in L.Rooms) { var ft = r.Furniture.Select(fid => L.Furniture[fid].Type).GroupBy(t => t).Select(g => g.Key + "x" + g.Count()); _log.AppendLine($"  room {r.Id} {r.Type} F{r.Floor} {r.Rect} pal={r.Palette} var={r.Variant} gim={r.MysteryGimmick}: " + string.Join(",", ft)); }
            foreach (var g in L.GenLog.Where(x => x.Contains("dress"))) _log.AppendLine("  genlog: " + g);
            string P = seedList != null ? $"S{seedList}_L{loop}_" : $"L{loop}_";

            // ---- grand hall
            var hall = L.Rooms.First(r => r.Type == RoomType.GrandHall && r.Floor == 0);
            var land = L.Rooms.FirstOrDefault(r => r.Type == RoomType.Landing);
            var stair = L.Stairs.First(s => s.Grand);
            var sA = _view.ToWorld(stair.A); var sB = _view.ToWorld(stair.B);
            var hr = hall.Rect;
            if (Want("Hall"))
            {
                Vector3 toStair = (sB - sA); toStair.y = 0; toStair.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, toStair);
                float halfW = (Mathf.Abs(side.x) > 0.5f ? hr.W : hr.D) * 0.5f;
                Vector3 hallC = new Vector3(hr.CX, 0, hr.CZ);
                Vector3 stairMid = (sA + sB) * 0.5f;
                Vector3 longAx = hr.W >= hr.D ? Vector3.right : Vector3.forward, shortAx = hr.W >= hr.D ? Vector3.forward : Vector3.right;
                float halfL = Mathf.Max(hr.W, hr.D) * 0.5f, halfS = Mathf.Min(hr.W, hr.D) * 0.5f;
                // title-screen style: ~6.8 m from the centre, looking at it
                Shot(P + "Hall_T1_title", hallC + longAx * Mathf.Min(6.8f, halfL - 0.8f) + shortAx * 1.5f + Vector3.up * 2.6f, hallC + Vector3.up * 2.2f, 68);
                Shot(P + "Hall_T2_title", hallC - longAx * Mathf.Min(6.8f, halfL - 0.8f) - shortAx * 1.0f + Vector3.up * 3.2f, hallC + Vector3.up * 2.0f, 68);
                Shot(P + "Hall_T3_title", hallC + shortAx * Mathf.Min(6.8f, halfS - 0.6f) + longAx * 2.5f + Vector3.up * 2.2f, hallC + Vector3.up * 2.4f, 72);
                Shot(P + "Hall_A_stair", hallC + side * (halfW - 2.2f) + toStair * 0.5f + Vector3.up * 1.65f, stairMid + Vector3.up * 1.4f, 70);
                Shot(P + "Hall_C_up", new Vector3(hr.CX + 1.5f, 1.5f, hr.CZ), new Vector3(hr.CX, 9f, hr.CZ + 0.5f), 80);
                if (land != null) Shot(P + "Hall_D_landing", new Vector3(hr.x1 - 1.4f, 4.8f + 1.65f, hr.CZ + 1f), new Vector3(hr.CX, 1.0f, hr.CZ), 72);
                Shot(P + "Hall_E_fromtop", sB + Vector3.up * 1.7f - toStair * 0.3f, sA + Vector3.up * 0.5f, 70);
            }
            // ---- rooms: one per type (all mystery rooms)
            var done = new HashSet<RoomType>();
            foreach (var r in L.Rooms)
            {
                if (r.Void || r.Type == RoomType.Courtroom) continue;
                if (r.Type == RoomType.GrandHall || r.Type == RoomType.Landing) continue;
                bool many = r.Type == RoomType.Bedroom;
                if (done.Contains(r.Type) && !RoomInfo.IsMystery(r.Type) && !(many && done.Count(t => t == RoomType.Bedroom) < 1)) continue;
                string key = $"{r.Type}_{r.Id}";
                if (!Want(key) && !Want(r.Type.ToString())) continue;
                done.Add(r.Type);
                RoomShots(P + key, r);
            }
            // -qaOpen : containers closed and then open, one of each type, with a marker in every slot
            if (Arg("-qaOpen") != null)
            {
                var seenT = new HashSet<string>();
                foreach (var op in UnityEngine.Object.FindObjectsByType<OpenableParts>(FindObjectsSortMode.None).OrderBy(o => o.name))
                {
                    var tn = op.name.Substring(op.name.IndexOf('_') + 1);
                    if (!seenT.Add(tn)) continue;
                    var fid = int.Parse(op.name.Substring(1, op.name.IndexOf('_') - 1));
                    var fr = L.Furniture[fid]; float h = Mathf.Max(0.6f, fr.H);
                    var tgt = op.transform.position + Vector3.up * (h * 0.55f);
                    var fwd = op.transform.forward; var side = op.transform.right;
                    var eye = tgt + fwd * (1.1f + h * 0.55f) + side * 0.35f + Vector3.up * 0.25f;
                    Shot(P + $"Open_{tn}_closed", eye, tgt, 58);
                    for (int i = 0; i < op.Count; i++) op.Snap(i, true);
                    foreach (var sl in op.AllSlots())
                    {
                        var mk = GameObject.CreatePrimitive(PrimitiveType.Cube); UnityEngine.Object.DestroyImmediate(mk.GetComponent<Collider>());
                        mk.transform.SetParent(sl, false); mk.transform.localScale = new Vector3(0.1f, 0.14f, 0.03f); mk.transform.localPosition = new Vector3(0, 0.07f, 0);
                        mk.GetComponent<Renderer>().sharedMaterial = MansionMats.Get(S.LeatherRed);
                    }
                    Shot(P + $"Open_{tn}_open", eye, tgt, 58);
                    _log.AppendLine($"openable {tn}: parts [{string.Join(", ", op.Parts.Select(p => p.Name + ":" + p.Slots.Count))}] glass={op.GlassFront}");
                }
            }
            // the injected door register, close up
            if (lg != null)
            {
                var fl = L.Furniture.LastOrDefault(ff => ff.Type == "DoorLogger");
                if (fl != null)
                {
                    var rm = L.Rooms[fl.Room];
                    var go = GameObject.Find($"F{fl.Id}_DoorLogger"); var bc = go != null ? go.GetComponent<BoxCollider>() : null;
                    Door nd = null; float bd = 99f; foreach (var did in rm.Doors) { var dd = L.Doors[did]; float dist = Mathf.Abs(dd.Pos.x - fl.Pos.x) + Mathf.Abs(dd.Pos.z - fl.Pos.z); if (dist < bd) { bd = dist; nd = dd; } }
                    if (bc != null && nd != null)
                    {
                        foreach (var did in rm.Doors) _view.SetDoor(did, false, false);
                        foreach (var dv in UnityEngine.Object.FindObjectsByType<DoorView>(FindObjectsSortMode.None)) dv.Snap();
                        var tgt = go.transform.TransformPoint(bc.center);
                        var nrm = nd.AlongX ? new Vector3(0, 0, Mathf.Sign(rm.Rect.CZ - nd.Pos.z)) : new Vector3(Mathf.Sign(rm.Rect.CX - nd.Pos.x), 0, 0);
                        _log.AppendLine($"door register at {tgt} facing {nrm}, door {nd.Pos.x:0.00},{nd.Pos.z:0.00} w {nd.Width}");
                        Shot(P + "DoorLogger_close", tgt + nrm * 0.75f + Vector3.up * 0.05f, tgt, 50);
                        Shot(P + "DoorLogger_room", tgt + nrm * 2.6f + Vector3.up * 0.25f + Vector3.Cross(Vector3.up, nrm) * 0.8f, tgt + Vector3.down * 0.2f, 70);
                    }
                }
            }
            // -qaOwners : every resident's room — the door as seen from the corridor, and a wide shot inside
            if (Arg("-qaOwners") != null) OwnerShots(P, L);
            // extra bedrooms (personalised)
            if (Want("Bedroom"))
                foreach (var r in L.Rooms.Where(r => r.Type == RoomType.Bedroom).Skip(1).Take(2)) RoomShots(P + $"Bedroom_{r.Id}_{r.Owner}", r);
            // ---- stairs
            if (Want("Stair"))
                foreach (var st in L.Stairs.Where(s => !s.Grand && s.Name != "심판장 승강기" && s.Name != "재판장 승강기"))
                {
                    var a = _view.ToWorld(st.A); var b = _view.ToWorld(st.B);
                    Vector3 d = b - a; d.y = 0; d.Normalize();
                    var side = Vector3.Cross(Vector3.up, d);
                    var rm = L.Room(st.RoomA).Rect;
                    Shot(P + $"Stair_{st.Id}_low", a - d * 0.9f + side * 1.2f + Vector3.up * 1.6f, (a + b) * 0.5f + Vector3.up * 0.5f, 80);
                    Shot(P + $"Stair_{st.Id}_high", b + d * 0.8f - side * 1.0f + Vector3.up * 1.7f, a + Vector3.up * 0.2f, 80);
                    if (Math.Min(st.A.f, st.B.f) == -1)
                    {
                        // the way down, as the player sees it: from the head of the flight and from halfway down
                        var top = a.y > b.y ? a : b; var foot = a.y > b.y ? b : a;
                        var dn = foot - top; dn.y = 0; dn.Normalize();
                        Shot(P + $"Descent_{st.Id}_top", top - dn * 1.4f + Vector3.up * 1.62f, foot + Vector3.up * 0.8f, 72);
                        var mid = Vector3.Lerp(top, foot, 0.5f);
                        Shot(P + $"Descent_{st.Id}_half", mid + Vector3.up * 1.62f, foot + dn * 1.5f + Vector3.up * 1.2f, 72);
                    }
                }
            // ---- courtroom
            if (Want("Court"))
            {
                var ct = L.Rooms.First(r => r.Type == RoomType.Courtroom); var c = ct.Rect; float fy = L.FloorY(-2);
                Shot(P + "Court_A_wide", new Vector3(c.CX, fy + 4.5f, c.z0 + 1.8f), new Vector3(c.CX, fy + 1.5f, c.CZ + 2f), 78);
                Shot(P + "Court_B_stand", new Vector3(c.CX + 4.4f, fy + 1.6f, c.CZ - 4.4f), new Vector3(c.CX - 2f, fy + 1.8f, c.CZ + 3f), 70);
                Shot(P + "Court_C_up", new Vector3(c.CX, fy + 1.6f, c.CZ), new Vector3(c.CX, fy + 10f, c.CZ + 2f), 85);
            }
            // ---- blackout / darkness
            if (Want("Dark") || Want("Blackout"))
            {
                var wr = L.Rooms.FirstOrDefault(r => r.Floor == 0 && r.Circuit == 1 && !RoomInfo.IsPassage(r.Type) && r.Rect.Area > 30);
                if (wr != null)
                {
                    _view.SetCircuit(1, false);
                    RoomShots(P + $"Blackout_c1_{wr.Type}_{wr.Id}", wr, true);
                    _view.SetCircuit(1, true);
                }
                var corr = L.Rooms.FirstOrDefault(r => r.Type == RoomType.Corridor && r.Floor == 0 && Math.Max(r.Rect.W, r.Rect.D) > 10);
                if (corr != null)
                {
                    _view.SetCircuit(0, false);
                    RoomShots(P + $"Blackout_c0_Corridor_{corr.Id}", corr, true);
                    _view.SetCircuit(0, true);
                }
                _view.SetDarkness(1f);
                var fl = _cam.gameObject.AddComponent<Light>(); fl.type = LightType.Spot; fl.spotAngle = 40; fl.innerSpotAngle = 16; fl.range = 18; fl.intensity = 16; fl.color = new Color(1f, 0.95f, 0.85f); fl.shadows = LightShadows.Soft;
                Vector3 toStair = (sB - sA); toStair.y = 0; toStair.Normalize();
                { Vector3 la = hr.W >= hr.D ? Vector3.right : Vector3.forward; Vector3 hc = new Vector3(hr.CX, 0, hr.CZ); Shot(P + "Darkness_Hall_flashlight", hc + la * Mathf.Min(6.8f, Mathf.Max(hr.W, hr.D) * 0.5f - 0.8f) + Vector3.up * 1.6f, hc + Vector3.up * 1.2f, 70); }
                UnityEngine.Object.DestroyImmediate(fl);
                { Vector3 la = hr.W >= hr.D ? Vector3.right : Vector3.forward; Vector3 hc = new Vector3(hr.CX, 0, hr.CZ); Shot(P + "Darkness_Hall_noflash", hc + la * Mathf.Min(6.8f, Mathf.Max(hr.W, hr.D) * 0.5f - 0.8f) + Vector3.up * 1.6f, hc + Vector3.up * 1.2f, 70); }
                _view.SetDarkness(0f);
            }
        }

        static void RoomShots(string key, Room r, bool onlyDoor = false)
        {
            float fy = _view.Layout.FloorY(r.Floor);
            var R = r.Rect;
            var c = new Vector3(R.CX, fy, R.CZ);
            // doorway eye-level
            Door d = r.Doors.Count > 0 ? _view.Layout.Doors[r.Doors[0]] : null;
            Vector3 eye, look;
            if (d != null)
            {
                var dp = new Vector3(d.Pos.x, fy, d.Pos.z);
                Vector3 into = (c - dp); into = d.AlongX ? new Vector3(0, 0, Mathf.Sign(into.z)) : new Vector3(Mathf.Sign(into.x), 0, 0);
                eye = dp + into * 1.15f + Vector3.up * 1.62f;
                look = c + Vector3.up * 1.1f + into * 1.0f;
                _view.SetDoor(d.Id, true, false);
                foreach (var dv in UnityEngine.Object.FindObjectsByType<DoorView>(FindObjectsSortMode.None)) dv.Snap();
            }
            else
            {
                eye = new Vector3(R.x0 + 0.6f, fy + 1.62f, R.z0 + 0.6f);
                look = c + Vector3.up * 1.2f;
            }
            Shot(key + "_door", eye, look, 70);
            if (onlyDoor) return;
            // wide: from the corner farthest from the door, high
            var corners = new[] { new Vector3(R.x0 + 0.45f, 0, R.z0 + 0.45f), new Vector3(R.x1 - 0.45f, 0, R.z0 + 0.45f), new Vector3(R.x0 + 0.45f, 0, R.z1 - 0.45f), new Vector3(R.x1 - 0.45f, 0, R.z1 - 0.45f) };
            var dpos = d != null ? new Vector3(d.Pos.x, 0, d.Pos.z) : c;
            var far = corners.OrderByDescending(p => (p - dpos).sqrMagnitude).First();
            float ceil = r.CeilingH;
            if (r.Type == RoomType.GrandHall) ceil = 4.4f;
            if (Arg("-qaNoWide") == null) Shot(key + "_wide", far + Vector3.up * (fy + Math.Min(ceil - 0.5f, 2.9f)), c + Vector3.up * 0.6f, 82);
            if (Arg("-qaTop") != null) TopShot(key + "_top", r);
            if (Arg("-qaWalk") != null) WalkShots(key, r);
        }

        /// <summary>Contact-sheet shots of the private rooms: door from the corridor (closed, ~3.3 m back, slightly off-axis
        /// so the doormat and whatever stands beside the door show) and the room wide from the far corner.</summary>
        static void OwnerShots(string P, Layout L)
        {
            foreach (var r in L.Rooms.Where(r => r.Type == RoomType.Bedroom && r.Owner != null).OrderBy(r => r.Owner))
            {
                if (r.Doors.Count == 0) continue;
                var d = L.Doors[r.Doors[0]];
                float fy = L.FloorY(r.Floor);
                var dp = new Vector3(d.Pos.x, fy, d.Pos.z);
                var c = new Vector3(r.Rect.CX, fy, r.Rect.CZ);
                Vector3 into = c - dp; into = d.AlongX ? new Vector3(0, 0, Mathf.Sign(into.z)) : new Vector3(Mathf.Sign(into.x), 0, 0);
                Vector3 along = d.AlongX ? Vector3.right : Vector3.forward;
                // back into the corridor as far as it allows (up to 3.3 m), a little to one side
                float back = 3.3f; int other = d.RoomA == r.Id ? d.RoomB : d.RoomA; var orr = L.Room(other);
                if (orr != null) { var R = orr.Rect; float lim = d.AlongX ? (into.z < 0 ? dp.z - R.z0 : R.z1 - dp.z) : (into.x < 0 ? dp.x - R.x0 : R.x1 - dp.x); back = Mathf.Clamp(lim - 0.35f, 1.4f, 3.3f); }
                _view.SetDoor(d.Id, false, false);
                foreach (var dv in UnityEngine.Object.FindObjectsByType<DoorView>(FindObjectsSortMode.None)) dv.Snap();
                var eye = dp - into * back + along * 0.55f + Vector3.up * 1.62f;
                Shot($"{P}Own_{r.Owner}_a_door", eye, dp + Vector3.up * 1.35f + along * 0.1f, back < 2.2f ? 78f : 64f);
                // inside: from the corner farthest from the door, high, over the room centre
                var R0 = r.Rect;
                var corners = new[] { new Vector3(R0.x0 + 0.45f, 0, R0.z0 + 0.45f), new Vector3(R0.x1 - 0.45f, 0, R0.z0 + 0.45f), new Vector3(R0.x0 + 0.45f, 0, R0.z1 - 0.45f), new Vector3(R0.x1 - 0.45f, 0, R0.z1 - 0.45f) };
                var far = corners.OrderByDescending(p => (p - new Vector3(dp.x, 0, dp.z)).sqrMagnitude).First();
                Shot($"{P}Own_{r.Owner}_b_room", far + Vector3.up * (fy + Math.Min(r.CeilingH - 0.45f, 2.55f)), c + Vector3.up * 0.75f, 84);
                _log.AppendLine($"owner {r.Owner} room {r.Id} F{r.Floor} {r.Rect} door {d.Id} furniture [{string.Join(",", r.Furniture.Select(fid => L.Furniture[fid].Type))}]");
            }
        }

        /// <summary>Plan view of one room: orthographic from just under the ceiling (chandeliers clipped away).</summary>
        static void TopShot(string name, Room r)
        {
            float fy = _view.Layout.FloorY(r.Floor); var R = r.Rect;
            float ceil = r.Type == RoomType.GrandHall ? 4.4f : r.CeilingH;
            float aspect = _w / (float)_h;
            _cam.orthographic = true;
            _cam.orthographicSize = Mathf.Max(R.D * 0.5f, R.W * 0.5f / aspect) + 0.25f;
            _cam.nearClipPlane = Mathf.Max(0.05f, ceil - 2.45f); _cam.farClipPlane = ceil + 1f;
            var pos = new Vector3(R.CX, fy + ceil - 0.05f, R.CZ);
            Shot(name, pos, pos + Vector3.down * 3f + Vector3.forward * 0.001f, 60);
            _cam.orthographic = false; _cam.nearClipPlane = 0.05f; _cam.farClipPlane = 200f;
        }

        /// <summary>First-person walk through the room: in at each doorway along the lane, then from the middle back out.</summary>
        static void WalkShots(string key, Room r)
        {
            float fy = _view.Layout.FloorY(r.Floor); var R = r.Rect;
            var c = new Vector3(R.CX, fy, R.CZ);
            int k = 0;
            foreach (var did in r.Doors.Take(2))
            {
                var d = _view.Layout.Doors[did];
                var dp = new Vector3(d.Pos.x, fy, d.Pos.z);
                Vector3 into = c - dp; into = d.AlongX ? new Vector3(0, 0, Mathf.Sign(into.z)) : new Vector3(Mathf.Sign(into.x), 0, 0);
                // two steps in, eye height, looking along the room toward the far side (a little off-centre, as one walks)
                var eye = dp + into * 2.0f + Vector3.up * 1.62f;
                var other = r.Doors.Count > 1 ? _view.Layout.Doors[r.Doors[(r.Doors.IndexOf(did) + 1) % r.Doors.Count]] : null;
                var look = other != null ? new Vector3(other.Pos.x, fy + 1.3f, other.Pos.z) : c + into * 2f + Vector3.up * 1.2f;
                Shot($"{key}_walk{k++}", eye, look, 72);
            }
            // from the middle of the room back toward the first door
            if (r.Doors.Count > 0)
            {
                var d0 = _view.Layout.Doors[r.Doors[0]];
                Shot($"{key}_walkmid", c + Vector3.up * 1.62f, new Vector3(d0.Pos.x, fy + 1.1f, d0.Pos.z), 76);
            }
        }

        static string Stat(string n)
        {
            var t = typeof(Editor).Assembly.GetType("UnityEditor.UnityStats");
            var p = t?.GetProperty(n, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            return p != null ? n + "=" + p.GetValue(null) : "";
        }

        static void Shot(string name, Vector3 pos, Vector3 target, float fov)
        {
            _cam.transform.position = pos;
            _cam.transform.rotation = Quaternion.LookRotation((target - pos).normalized, Vector3.up);
            _cam.fieldOfView = fov;
            _cam.targetTexture = _rt;
            _view.Cull(pos);
            if (_view.Atmosphere != null)
            {
                int room = _view.RoomAtWorld(pos);
                _view.Atmosphere.SetRoom(room >= 0 ? _view.Layout.Rooms[room] : null);
                _view.Atmosphere.Snap();
                if (_view.Atmosphere.Reflection != null) _view.Atmosphere.Reflection.Render(_cam);
                if (Arg("-qaDumpPlanar") != null && _view.Atmosphere.Reflection != null && _view.Atmosphere.Reflection.Texture != null)
                {
                    var prt = _view.Atmosphere.Reflection.Texture; var prev0 = RenderTexture.active; RenderTexture.active = prt;
                    var pt = new Texture2D(prt.width, prt.height, TextureFormat.RGBAFloat, false); pt.ReadPixels(new Rect(0, 0, prt.width, prt.height), 0, 0); pt.Apply();
                    RenderTexture.active = prev0;
                    var px = pt.GetPixels(); int a0n = 0; for (int q = 0; q < px.Length; q++) { var cc = px[q]; if (cc.a < 0.5f) a0n++; px[q] = new Color(cc.r / (1 + cc.r), cc.g / (1 + cc.g), cc.b / (1 + cc.b), 1); } _log.AppendLine($"planar {name}: alpha<0.5 pixels {a0n}/{px.Length}");
                    var outT = new Texture2D(prt.width, prt.height, TextureFormat.RGB24, false); outT.SetPixels(px); outT.Apply();
                    File.WriteAllBytes(Path.Combine(_out, name + "_planar.png"), outT.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(pt); UnityEngine.Object.DestroyImmediate(outT);
                }
            }
            var req = new RenderPipeline.StandardRequest { destination = _rt };
            if (RenderPipeline.SupportsRenderRequest(_cam, req)) RenderPipeline.SubmitRenderRequest(_cam, req);
            else _cam.Render();
            // second pass: temporal / volume settle
            if (RenderPipeline.SupportsRenderRequest(_cam, req)) RenderPipeline.SubmitRenderRequest(_cam, req);
            var prev = RenderTexture.active; RenderTexture.active = _rt;
            var tex = new Texture2D(_w, _h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, _w, _h), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(_out, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            int lights = 0; foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.enabled && l.isActiveAndEnabled) lights++;
            _log.AppendLine($"shot {name}: activeLights={lights} {Stat("batches")} {Stat("drawCalls")} {Stat("triangles")} {Stat("setPassCalls")}");
        }
    }
}
