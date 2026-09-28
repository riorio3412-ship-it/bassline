using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BL23.Game.Mansion
{
    public sealed partial class MansionView
    {
        Texture2DArray _ambientTex;
        Color[] _ambBuf, _ambTmp;
        const int AmbW = 112, AmbH = 88;
        Transform _lightRoot;

        // ------------------------------------------------------------------ ambient lattice
        internal Color RoomAmbient(Room r)
        {
            var rv = Rooms[r.Id];
            var pal = rv.Pal;
            bool on = !CircuitOn.TryGetValue(r.Circuit, out var o) || o;
            float power = on ? 1f : (r.Circuit == 0 ? 0.28f : 0.12f);
            if (r.Type == RoomType.Courtroom) power = 1f;
            float k = Mathf.Lerp(0.55f, 1.25f, r.BaseLight) * power * (1f - Darkness * 0.93f);
            Color a0 = pal.Ambient.linear; float lumA = a0.grayscale;
            Color amb = Color.Lerp(new Color(lumA * 1.04f, lumA, lumA * 0.95f), a0, 0.42f) * 1.15f * k;
            // electric warmth when lit + cold moon fill for rooms with windows
            if (on) amb += pal.Warm.linear * 0.06f * r.BaseLight * (1f - Darkness);
            if (r.Exterior || r.Type == RoomType.Courtyard || r.Type == RoomType.Greenhouse) amb += new Color(0.018f, 0.028f, 0.06f) * (1f - Darkness * 0.6f);
            amb += DaylightAmbient(rv);
            switch (r.Type)
            {
                case RoomType.WhiteDoors: amb = new Color(0.6f, 0.6f, 0.62f) * (on ? 1.4f : 0.3f) * (1f - Darkness * 0.9f); break;
                case RoomType.Courtyard: amb += new Color(0.02f, 0.03f, 0.07f); break;
                case RoomType.EmptyAuditorium: amb *= 0.6f; break;
                case RoomType.RainCorridor: amb = amb * 0.6f + new Color(0.01f, 0.02f, 0.04f); break;
            }
            return amb;
        }

        internal void UpdateAmbient()
        {
            if (Rooms == null) return;
            if (_ambientTex == null)
            {
                _ambientTex = new Texture2DArray(AmbW, AmbH, 4, TextureFormat.RGBAHalf, false, true) { name = "MansionAmbient", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                _ambBuf = new Color[AmbW * AmbH]; _ambTmp = new Color[AmbW * AmbH];
            }
            var roomCol = new Color[Layout.Rooms.Count];
            foreach (var r in Layout.Rooms) roomCol[r.Id] = RoomAmbient(r);
            for (int slice = 0; slice < 4; slice++)
            {
                int f = slice - 2;
                Array.Clear(_ambBuf, 0, _ambBuf.Length);
                if (_grids.TryGetValue(f, out var g))
                {
                    for (int j = 0; j < AmbH; j++)
                        for (int i = 0; i < AmbW; i++)
                        {
                            float x = (i + 0.5f) * 0.5f, z = (j + 0.5f) * 0.5f;
                            int id = g.At(x, z);
                            if (id < 0) continue;
                            var r = Layout.Rooms[id];
                            if (r.Void && HallRoom != null) id = HallRoom.Id;
                            _ambBuf[i + j * AmbW] = roomCol[id];
                        }
                }
                // soft bleed through doorways (one 3x3 pass, only from lit neighbours)
                for (int j = 0; j < AmbH; j++)
                    for (int i = 0; i < AmbW; i++)
                    {
                        var c = _ambBuf[i + j * AmbW];
                        if (c.maxColorComponent <= 0) { _ambTmp[i + j * AmbW] = c; continue; }
                        Color sum = c * 4f; float w = 4f;
                        for (int dj = -1; dj <= 1; dj++) for (int di = -1; di <= 1; di++)
                            {
                                if (di == 0 && dj == 0) continue;
                                int ii = i + di, jj = j + dj; if (ii < 0 || jj < 0 || ii >= AmbW || jj >= AmbH) continue;
                                var n = _ambBuf[ii + jj * AmbW]; if (n.maxColorComponent <= 0) continue;
                                sum += n; w += 1f;
                            }
                        _ambTmp[i + j * AmbW] = sum / w;
                    }
                _ambientTex.SetPixels(_ambTmp, slice);
            }
            _ambientTex.Apply(false, false);
            Shader.SetGlobalTexture("_BL_AmbientTex", _ambientTex);
            Shader.SetGlobalVector("_BL_AmbientParams", new Vector4(1f / (AmbW * 0.5f), 1f / (AmbH * 0.5f), 1f, 1f));
            Shader.SetGlobalVector("_BL_AmbientFloorY", new Vector4(-5.2f, -0.35f, 4.65f, 0));
        }

        // ------------------------------------------------------------------ lights & fixtures
        internal LightRec AddLight(RoomView rv, Vector3 pos, Color col, float intensity, float range, LightType type = LightType.Point, bool shadows = false,
                                   float flicker = 0f, bool fire = false, bool moon = false, bool emergency = false, Quaternion? rot = null, float spot = 60f, Texture cookie = null, bool neon = false)
        {
            if (_lightRoot == null) { _lightRoot = new GameObject("Lights").transform; _lightRoot.SetParent(_root, false); }
            var go = new GameObject(type + "_" + rv.Room.Id);
            go.transform.SetParent(rv.Root, false);
            go.transform.position = pos;
            if (rot.HasValue) go.transform.rotation = rot.Value;
            var l = go.AddComponent<Light>();
            l.type = type; l.color = col; l.intensity = intensity; l.range = range;
            if (type == LightType.Spot) { l.spotAngle = spot; l.innerSpotAngle = spot * 0.55f; }
            l.shadows = LightShadows.None;
            l.shadowBias = 0.02f; l.shadowNormalBias = 0.35f; l.shadowNearPlane = 0.15f;
            l.renderMode = LightRenderMode.ForcePixel;
            if (cookie != null) l.cookie = cookie;
            var ad = go.AddComponent<UniversalAdditionalLightData>();
            ad.usePipelineSettings = true;
            // point shadows use 6 atlas slices: keep them at the medium tier so the atlas never has to downscale everything
            if (shadows && Application.isPlaying) ad.additionalLightsShadowResolutionTier = type == LightType.Point ? UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium : UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh;
            var rec = new LightRec { Light = l, Base = intensity, Range = range, Circuit = rv.Room.Type == RoomType.Courtroom ? -1 : rv.Room.Circuit, Emergency = emergency, Moon = moon, Fire = fire, Flicker = flicker, Seed = UnityEngine.Random.value * 100f, Shadows = shadows, Room = rv.Room.Id, Neon = neon };
            rv.Lights.Add(rec); AllLights.Add(rec);
            l.enabled = false;
            return rec;
        }

        MansionModel _chand1, _chand2, _chand3, _lantern;
        internal static bool NeonStrips = false;

        void BuildLighting()
        {
            _chand1 = Models.Get("Chandelier_01"); _chand2 = Models.Get("Chandelier_02"); _chand3 = Models.Get("Chandelier_03"); _lantern = Models.Get("lantern_chandelier_01");
            foreach (var rv in Rooms)
            {
                if (rv == null) continue;
                try
                {
                    LightRoom(rv);
                    // glass roofs (pool, conservatories) take the sky too
                    if (!rv.Room.Void && rv.Style != null && rv.Style.CeilKind == 3 && rv.Room.Type != RoomType.Greenhouse && rv.Room.Type != RoomType.Courtyard) SkyFills(rv);
                }
                catch (Exception e) { Debug.LogException(e); }
            }
            BuildMoonlight();
            // everything visible until the first cull
            foreach (var l in AllLights) _lightVisible.Add(l);
        }

        void LightRoom(RoomView rv)
        {
            var r = rv.Room; var pal = rv.Pal; var R = r.Rect;
            if (r.Void || r.Type == RoomType.Courtroom) return;
            var mb = new MeshBuilder();
            float bl = Mathf.Lerp(0.6f, 1.15f, r.BaseLight);
            Color warm = pal.Warm;
            float ceil = rv.CeilY;
            bool big = R.Area > 45f;
            bool isHall = r.Type == RoomType.GrandHall;
            // ---- main ceiling fixtures
            switch (r.Type)
            {
                case RoomType.GrandHall:
                    {
                        // under the landing ring: pendant lanterns; the great chandelier hangs in the void
                        var vr = VoidRect;
                        Chandelier(rv, mb, new Vector3(vr.CX, 9.0f - 3.4f, vr.CZ), 2.2f, warm, 85f * bl, 19f, true, 2);
                        foreach (var c in new[] { new Vector2(R.x0 + 1.5f, R.z0 + 1.5f), new Vector2(R.x1 - 1.5f, R.z0 + 1.5f), new Vector2(R.x0 + 1.5f, R.z1 - 1.5f), new Vector2(R.x1 - 1.5f, R.z1 - 1.5f) })
                            Lantern(rv, mb, new Vector3(c.x, ceil - 0.9f, c.y), warm, 10f * bl, 7.5f);
                        break;
                    }
                case RoomType.Landing:
                    {
                        var vr = VoidRect;
                        foreach (var c in new[] { new Vector2(R.x0 + 1.5f, R.CZ), new Vector2(R.x1 - 1.5f, R.CZ), new Vector2(R.CX, R.z0 + 1.5f), new Vector2(R.CX, R.z1 - 1.5f) })
                            Lantern(rv, mb, new Vector3(c.x, ceil - 0.8f, c.y), warm, 6f * bl, 6.5f);
                        break;
                    }
                case RoomType.Corridor:
                case RoomType.Stairwell:
                    {
                        bool alongX = R.W >= R.D; float len = Math.Max(R.W, R.D);
                        int n = Math.Max(1, (int)Math.Round(len / 7f));
                        for (int i = 0; i < n; i++)
                        {
                            float t = (i + 0.5f) / n;
                            var p = alongX ? new Vector3(R.x0 + t * R.W, ceil - 0.7f, R.CZ) : new Vector3(R.CX, ceil - 0.7f, R.z0 + t * R.D);
                            if (r.Floor < 0) Bulb(rv, mb, p + Vector3.up * 0.3f, r.Type == RoomType.Stairwell ? new Color(0.72f, 0.8f, 0.95f) : new Color(1f, 0.8f, 0.55f), 4.5f * bl, 6.5f, 0.35f);   // going down, the light turns cold
                            else { Lantern(rv, mb, p, warm, 5.5f * bl, 7f); if (i == n - 1 && (r.Id % 3) == 0 && rv.Lights.Count > 0) rv.Lights[rv.Lights.Count - 1].Flicker = 0.75f; }   // one lantern in a few corridors that will not stay lit
                        }
                        break;
                    }
                // (continued below the switch: the descent to the basement gets its own lanterns)
                case RoomType.Courtyard:
                case RoomType.Greenhouse:
                    {
                        // lanterns on posts are placed by decor; a soft cold fill from the sky
                        SkyFills(rv);
                        var p = new Vector3(R.CX + R.W * 0.25f, ceil - 1.0f, R.CZ);
                        Lantern(rv, mb, p, warm, 3.5f * bl, 6f);
                        break;
                    }
                case RoomType.WhiteDoors:
                    {
                        // overexposed: flat white light from everywhere
                        int n = Math.Max(1, (int)(R.Area / 18f));
                        for (int i = 0; i < n; i++)
                        {
                            float t = (i + 0.5f) / n;
                            var p = R.W >= R.D ? new Vector3(R.x0 + t * R.W, ceil - 0.3f, R.CZ) : new Vector3(R.CX, ceil - 0.3f, R.z0 + t * R.D);
                            PanelLight(rv, mb, p, new Color(1f, 0.98f, 0.95f), 14f, 9f);
                        }
                        break;
                    }
                case RoomType.MachineRoom:
                case RoomType.PowerRoom:
                case RoomType.BoilerRoom:
                case RoomType.WaterRoom:
                case RoomType.Laundry:
                case RoomType.Storage:
                case RoomType.Closet:
                case RoomType.WineCellar:
                case RoomType.Workshop:
                case RoomType.Incinerator:
                case RoomType.ColdStorage:
                    {
                        int n = Math.Max(1, (int)Math.Round(R.Area / 26f));
                        for (int i = 0; i < n; i++)
                        {
                            float t = (i + 0.5f) / n;
                            var p = R.W >= R.D ? new Vector3(R.x0 + t * R.W, ceil - 0.5f, R.CZ) : new Vector3(R.CX, ceil - 0.5f, R.z0 + t * R.D);
                            var c = r.Type == RoomType.WineCellar ? warm : r.Type == RoomType.ColdStorage ? new Color(0.72f, 0.88f, 1f) : (r.Type == RoomType.Laundry || r.Type == RoomType.WaterRoom ? new Color(0.8f, 0.95f, 1f) : new Color(1f, 0.82f, 0.6f));
                            Bulb(rv, mb, p, c, 6.5f * bl, Math.Max(6f, Math.Max(R.W, R.D) * 0.6f), r.Type == RoomType.Storage || r.Type == RoomType.Closet || r.Type == RoomType.ColdStorage ? 0.6f : 0.15f, cage: true);
                        }
                        break;
                    }
                case RoomType.Infirmary:
                case RoomType.WaitingRoom:
                case RoomType.Kitchen:
                    {
                        int n = Math.Max(1, (int)Math.Round(Math.Max(R.W, R.D) / 4.5f));
                        for (int i = 0; i < n; i++)
                        {
                            float t = (i + 0.5f) / n;
                            var p = R.W >= R.D ? new Vector3(R.x0 + t * R.W, ceil - 0.05f, R.CZ) : new Vector3(R.CX, ceil - 0.05f, R.z0 + t * R.D);
                            var c = r.Type == RoomType.Kitchen ? new Color(1f, 0.9f, 0.75f) : new Color(0.85f, 1f, 0.95f);
                            FluoTube(rv, mb, p, R.W >= R.D, c, 6f * bl, 6.5f, r.Type == RoomType.WaitingRoom ? 0.8f : 0.25f);
                        }
                        break;
                    }
                case RoomType.EmptyAuditorium:
                case RoomType.Theater:
                    {
                        // few dim house lights; stage/screen lights come from dressing
                        Lantern(rv, mb, new Vector3(R.CX, ceil - 0.8f, R.CZ), warm, 4f * bl, 8f);
                        break;
                    }
                case RoomType.Darkroom:
                    {
                        // only the safelight: everything the colour of a wound
                        Bulb(rv, mb, new Vector3(R.CX, ceil - 0.55f, R.CZ), new Color(1f, 0.1f, 0.06f), 2.4f, Math.Max(R.W, R.D) * 0.8f, 0.05f, cage: true);
                        break;
                    }
                case RoomType.RainCorridor:
                    {
                        int n = Math.Max(1, (int)Math.Round(Math.Max(R.W, R.D) / 5f));
                        for (int i = 0; i < n; i++)
                        {
                            float t = (i + 0.5f) / n;
                            var p = R.W >= R.D ? new Vector3(R.x0 + t * R.W, ceil - 0.6f, R.CZ) : new Vector3(R.CX, ceil - 0.6f, R.z0 + t * R.D);
                            Bulb(rv, mb, p, new Color(0.55f, 0.75f, 1f), 4.5f, 6f, 0.3f);
                        }
                        break;
                    }
                default:
                    {
                        if (big)
                        {
                            bool alongX = R.W >= R.D; float len = Math.Max(R.W, R.D);
                            int n = Math.Max(1, (int)Math.Round(len / 7.5f));
                            for (int i = 0; i < n; i++)
                            {
                                float t = (i + 0.5f) / n;
                                var p = alongX ? new Vector3(R.x0 + t * R.W, ceil - 1.3f, R.CZ) : new Vector3(R.CX, ceil - 1.3f, R.z0 + t * R.D);
                                Chandelier(rv, mb, p, 1.2f, warm, 15f * bl, 9.5f, i == 0 && (r.Type == RoomType.Dining || r.Type == RoomType.Library), (r.Id + i) % 3);
                            }
                        }
                        else
                        {
                            var p = new Vector3(R.CX, ceil - 0.95f, R.CZ);
                            if (r.Type == RoomType.Bedroom || r.Type == RoomType.GuestRoom || r.Type == RoomType.Study) Lantern(rv, mb, p, warm, 7.5f * bl, Math.Max(R.W, R.D) * 0.9f);
                            else Chandelier(rv, mb, p + Vector3.up * 0.3f, 0.8f, warm, 9f * bl, Math.Max(R.W, R.D) * 0.9f, false, r.Id % 3);
                        }
                        break;
                    }
            }
            // ---- the way down: a lantern over the head of the basement flight, another at its foot (cooler)
            if (r.Type == RoomType.Stairwell)
                foreach (var st in Layout.Stairs)
                {
                    if (st.Grand || (st.Name == "심판장 승강기" || st.Name == "재판장 승강기") || Math.Min(st.A.f, st.B.f) != -1) continue;
                    var top = st.A.f > st.B.f ? st.A : st.B; var foot = st.A.f > st.B.f ? st.B : st.A;
                    if (r.Floor == top.f && r.Rect.Contains(top.x, top.z))
                        Lantern(rv, mb, new Vector3(top.x, ceil - 1.0f, top.z), warm, 6f * bl, 7f);
                    if (r.Floor == foot.f && r.Rect.Contains(foot.x, foot.z))
                        Lantern(rv, mb, new Vector3(foot.x, ceil - 0.9f, foot.z), new Color(0.75f, 0.82f, 1f), 4f * bl, 6f);
                }
            // ---- wall sconces on solid walls (every other has a real light)
            if (r.Floor >= 0 && r.Type != RoomType.WhiteDoors && r.Type != RoomType.Greenhouse && r.Type != RoomType.Courtyard && r.Type != RoomType.Pool && r.Type != RoomType.Kitchen && r.Type != RoomType.Infirmary && r.Type != RoomType.WaitingRoom && !IsUtility(r.Type))
            {
                int placed = 0; int lit = 0;
                bool corr = r.Type == RoomType.Corridor;
                float spacing = corr ? 4.0f : 3.6f;
                var used = new List<Vector3>();
                for (int i = 0; i < rv.WallSlots.Count; i++)
                {
                    var p = rv.WallSlots[i]; var n = rv.WallSlotN[i];
                    bool near = false; foreach (var u in used) if (Vector3.Distance(u, p) < spacing) { near = true; break; }
                    if (near) continue;
                    if (!WallFree(rv, p, n, 0.14f, rv.FloorY + 1.85f)) continue;   // not on a picture, a pilaster or over a bookcase
                    used.Add(p); rv.WallReserved.Add(new Vector4(p.x, p.z, 0.16f, 0));
                    // corridors: every sconce burns (a pair roughly every 4 m), so no stretch of passage falls into darkness
                    bool withLight = corr ? lit < 12 : (placed % 2 == 0) && lit < (big ? 4 : 2);
                    Sconce(rv, mb, new Vector3(p.x, rv.FloorY + 2.05f, p.z), n, warm, withLight ? 3.2f * bl : 0f);
                    if (withLight) lit++;
                    placed++;
                    if (placed > (corr ? 16 : big ? 10 : 5)) break;
                }
            }
            // ---- a pool of light at every doorway off a passage: a small brass lamp over the door on the passage side
            if ((r.Type == RoomType.Corridor || r.Type == RoomType.Landing) && r.Floor >= -1)
                foreach (int did in r.Doors)
                {
                    var d = Layout.Doors[did];
                    Vector3 into = d.AlongX ? new Vector3(0, 0, Mathf.Sign(r.Rect.CZ - d.Pos.z)) : new Vector3(Mathf.Sign(r.Rect.CX - d.Pos.x), 0, 0);
                    float dh = _doorH.TryGetValue(did, out var hh) ? hh : 2.45f;
                    var lp = new Vector3(d.Pos.x, rv.FloorY + Mathf.Min(dh + 0.35f, rv.CeilY - rv.FloorY - 0.35f), d.Pos.z) + into * 0.16f;
                    mb.Set(S.Brass, new Color(0.62f, 0.5f, 0.34f)); mb.Box(lp + into * -0.06f, new Vector3(0.14f, 0.05f, 0.14f));
                    mb.Rod(lp + into * -0.1f, lp, 0.008f, 4, false);
                    mb.Set(S.Glow, warm, MansionMats.GlowData(1.2f, 0.1f, 0, rv.Room.Circuit)); mb.Sphere(lp + Vector3.down * 0.05f, 0.04f, 8, 6);
                    AddLight(rv, lp + into * 0.35f + Vector3.down * 0.25f, warm, 1.3f * bl, 3.6f, LightType.Point, false, 0.08f);
                }
            // ---- emergency lamps in corridors (red, only when power is out)
            if (r.Type == RoomType.Corridor && rv.WallSlots.Count > 0)
            {
                var p = rv.WallSlots[rv.WallSlots.Count / 2]; var n = rv.WallSlotN[rv.WallSlots.Count / 2];
                var pos = new Vector3(p.x, rv.CeilY - 0.4f, p.z) + n * 0.12f;
                // a small iron-caged lamp with a ruby glass dome: dark glass while the power is on
                mb.Set(S.Iron, new Color(0.14f, 0.13f, 0.12f)); mb.Box(pos + Vector3.up * 0.06f, new Vector3(0.16f, 0.025f, 0.16f));
                for (int k = 0; k < 4; k++) { float a = k * Mathf.PI * 0.5f + 0.785f; mb.Rod(pos + Vector3.up * 0.05f + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.07f, pos + Vector3.down * 0.07f + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.05f, 0.004f, 4, false); }
                mb.Set(S.Glow, new Color(0.5f, 0.05f, 0.04f), MansionMats.GlowData(0.35f, 0.6f, 0, -1));
                mb.Sphere(pos, 0.06f, 10, 7, 0.9f);
                AddLight(rv, pos + n * 0.2f, new Color(1f, 0.1f, 0.08f), 1.6f, 5f, emergency: true, flicker: 0.3f);
            }
            // ---- neon accents
            NeonAccents(rv, mb);
            Emit(rv, mb, "Fixtures", null, ShadowCastingMode.Off);
        }

        static bool IsUtility(RoomType t) => t == RoomType.Incinerator || t == RoomType.ColdStorage || t == RoomType.MachineRoom || t == RoomType.PowerRoom || t == RoomType.BoilerRoom || t == RoomType.WaterRoom || t == RoomType.Laundry || t == RoomType.Storage || t == RoomType.Closet || t == RoomType.WineCellar || t == RoomType.Workshop;

        // Fixture builders -----------------------------------------------------------------------
        void Chandelier(RoomView rv, MeshBuilder mb, Vector3 p, float size, Color warm, float intensity, float range, bool shadows, int style)
        {
            int circuitGroup = rv.Room.Circuit;
            var model = style == 0 ? _chand1 : style == 1 ? _chand2 : _chand3;
            // chain up to the ceiling
            mb.Set(S.Brass, Color.white);
            float top = rv.Room.Void ? 9.0f : rv.CeilY;
            if (rv.Room.Type == RoomType.GrandHall) top = 9.0f;
            mb.Rod(p + Vector3.up * size * 0.45f, new Vector3(p.x, top, p.z), 0.018f, 6, false);
            mb.Push(new Vector3(p.x, top - 0.03f, p.z), 0); mb.Lathe(new[] { new Vector2(0.001f, -0.12f), new Vector2(0.2f, -0.06f), new Vector2(0.28f, 0f) }, 14); mb.Pop();
            if (!rv.Room.Void && rv.Room.Type != RoomType.GrandHall)
            {
                // plaster ceiling rose: concentric rings of leaves, a gilt bead
                float rr = Mathf.Clamp(size * 0.55f, 0.35f, 0.8f);
                mb.Set(S.Ceiling, Color.Lerp(rv.Style.CeilTint, Color.white, 0.3f));
                mb.Push(new Vector3(p.x, top - 0.002f, p.z), 0);
                mb.Lathe(new[] { new Vector2(0.001f, -0.06f), new Vector2(rr * 0.3f, -0.07f), new Vector2(rr * 0.45f, -0.045f), new Vector2(rr * 0.6f, -0.06f), new Vector2(rr * 0.8f, -0.03f), new Vector2(rr * 0.92f, -0.035f), new Vector2(rr, 0f) }, 28);
                for (int i = 0; i < 12; i++) { float a = i / 12f * Mathf.PI * 2; mb.Ellipsoid(new Vector3(Mathf.Cos(a) * rr * 0.7f, -0.045f, Mathf.Sin(a) * rr * 0.7f), new Vector3(0.06f * rr / 0.5f, 0.02f, 0.03f * rr / 0.5f), 6, 3); }
                mb.Set(S.Gold, rv.Pal.Trim); mb.Torus(new Vector3(0, -0.05f, 0), rr * 0.45f, 0.012f, 24, 4);
                mb.Pop();
            }
            if (model != null)
            {
                var go = Models.Place(model, rv.Root, p, 0, new Vector3(size, size, size) * 1.05f, null, true);
                if (go != null) { foreach (var r in go.GetComponentsInChildren<Renderer>()) { rv.Renderers.Add(r); r.shadowCastingMode = ShadowCastingMode.Off; } }
            }
            else
            {
                // procedural fallback: rings of arms with candles
                mb.Set(S.Brass, Color.white);
                mb.Torus(p, size * 0.4f, 0.02f, 24, 6);
                mb.Rod(p + Vector3.down * 0.3f, p + Vector3.up * size * 0.45f, 0.03f, 8);
            }
            // candle-bulbs as glowing flames: only on the procedural fixture (a scanned chandelier has its own candles and
            // shades; a ring of extra candles at a guessed radius floated beside it as a white hoop in mid-air)
            int n = model != null ? 0 : Mathf.Clamp(Mathf.RoundToInt(size * 7), 5, 16);
            float rad = size * 0.38f;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                var c = p + new Vector3(Mathf.Cos(a) * rad, size * 0.08f, Mathf.Sin(a) * rad);
                mb.Set(S.Wax, Color.white); mb.Cyl(c, 0.018f, 0.1f, 6);
                FlameQuad(mb, c + Vector3.up * 0.1f, 0.07f, circuitGroup);
            }
            // glow halo
            mb.Set(S.Halo, warm, MansionMats.GlowData(0.35f, 0.2f, 0, circuitGroup));
            HaloQuad(mb, p + Vector3.up * size * 0.1f, size * 1.4f);
            AddLight(rv, p + Vector3.down * 0.15f, warm, intensity, range, LightType.Point, shadows, 0.12f);
        }

        void Lantern(RoomView rv, MeshBuilder mb, Vector3 p, Color warm, float intensity, float range)
        {
            if (_lantern != null)
            {
                var go = Models.Place(_lantern, rv.Root, p + Vector3.down * 0.15f, 0, Vector3.one * 0.7f, null, true);
                if (go != null) foreach (var r in go.GetComponentsInChildren<Renderer>()) { rv.Renderers.Add(r); r.shadowCastingMode = ShadowCastingMode.Off; }
                mb.Set(S.Iron, Color.white);
                mb.Rod(p + Vector3.up * 0.25f, new Vector3(p.x, rv.CeilY, p.z), 0.012f, 5, false);
            }
            else
            {
                mb.Set(S.Iron, new Color(0.2f, 0.18f, 0.16f));
                mb.Rod(p + Vector3.up * 0.3f, new Vector3(p.x, rv.CeilY, p.z), 0.012f, 5, false);
                mb.Push(p, 45f);
                for (int i = 0; i < 4; i++) { float a = i * 90f * Mathf.Deg2Rad; mb.Box(new Vector3(Mathf.Cos(a) * 0.14f, 0, Mathf.Sin(a) * 0.14f), new Vector3(0.025f, 0.42f, 0.025f)); }
                mb.Box(new Vector3(0, 0.22f, 0), new Vector3(0.34f, 0.04f, 0.34f)); mb.Box(new Vector3(0, -0.22f, 0), new Vector3(0.3f, 0.04f, 0.3f));
                mb.Pop();
            }
            mb.Set(S.Glow, warm, MansionMats.GlowData(2.6f, 0.1f, 0, rv.Room.Circuit));
            mb.Sphere(p + Vector3.down * 0.02f, 0.07f, 8, 6, 1.4f);
            mb.Set(S.Halo, warm, MansionMats.GlowData(0.3f, 0.1f, 0, rv.Room.Circuit));
            HaloQuad(mb, p, 0.9f);
            AddLight(rv, p + Vector3.down * 0.2f, warm, intensity, range, LightType.Point, false, 0.05f);
        }

        void Bulb(RoomView rv, MeshBuilder mb, Vector3 p, Color c, float intensity, float range, float flicker, bool cage = false)
        {
            mb.Set(S.Iron, new Color(0.15f, 0.15f, 0.15f));
            mb.Rod(p, new Vector3(p.x, rv.CeilY, p.z), 0.01f, 5, false);
            mb.Set(S.Glow, c, MansionMats.GlowData(3f, flicker, 0, rv.Room.Circuit));
            mb.Sphere(p + Vector3.down * 0.08f, 0.055f, 8, 6);
            if (cage)
            {
                mb.Set(S.Iron, new Color(0.2f, 0.2f, 0.18f));
                for (int i = 0; i < 4; i++) { float a = i * Mathf.PI * 0.5f; mb.Rod(p + new Vector3(Mathf.Cos(a) * 0.09f, 0, Mathf.Sin(a) * 0.09f), p + new Vector3(Mathf.Cos(a) * 0.07f, -0.17f, Mathf.Sin(a) * 0.07f), 0.006f, 4, false); }
                mb.Push(p + Vector3.down * 0.02f, 0); mb.Lathe(new[] { new Vector2(0.03f, 0.03f), new Vector2(0.12f, -0.02f), new Vector2(0.13f, -0.04f) }, 12); mb.Pop();
            }
            mb.Set(S.Halo, c, MansionMats.GlowData(0.25f, flicker, 0, rv.Room.Circuit));
            HaloQuad(mb, p + Vector3.down * 0.08f, 0.6f);
            AddLight(rv, p + Vector3.down * 0.15f, c, intensity, range, LightType.Point, false, flicker);
        }

        void FluoTube(RoomView rv, MeshBuilder mb, Vector3 p, bool alongX, Color c, float intensity, float range, float flicker)
        {
            var size = alongX ? new Vector3(1.3f, 0.05f, 0.14f) : new Vector3(0.14f, 0.05f, 1.3f);
            mb.Set(S.PaintedMetal, new Color(0.85f, 0.85f, 0.85f));
            mb.Box(p + Vector3.down * 0.03f, size + new Vector3(0.04f, 0.02f, 0.04f));
            mb.Set(S.Glow, c, MansionMats.GlowData(3.2f, flicker, 0, rv.Room.Circuit));
            mb.Box(p + Vector3.down * 0.075f, new Vector3(size.x * 0.95f, 0.03f, size.z * 0.6f));
            AddLight(rv, p + Vector3.down * 0.3f, c, intensity, range, LightType.Point, false, flicker);
        }

        void PanelLight(RoomView rv, MeshBuilder mb, Vector3 p, Color c, float intensity, float range)
        {
            mb.Set(S.Glow, c, MansionMats.GlowData(2.2f, 0, 0, rv.Room.Circuit));
            mb.Box(p, new Vector3(1.2f, 0.03f, 1.2f), MeshBuilder.Faces.NY);
            AddLight(rv, p + Vector3.down * 0.5f, c, intensity, range);
        }

        void Sconce(RoomView rv, MeshBuilder mb, Vector3 p, Vector3 n, Color warm, float intensity)
        {
            var q = Quaternion.LookRotation(n, Vector3.up);
            mb.Push(Matrix4x4.TRS(p, q, Vector3.one));
            // back plate (a blackened bronze shield with a thin gilt edge) + curled iron arm + two candle cups: dark
            // against the wall so the lamp reads as flame and glow, never as a bright yellow cut-out
            mb.Set(S.Iron, new Color(0.16f, 0.13f, 0.1f));
            mb.Box(new Vector3(0, 0, 0.012f), new Vector3(0.11f, 0.28f, 0.022f));
            mb.Set(S.Brass, new Color(0.55f, 0.44f, 0.3f));
            mb.Box(new Vector3(0, 0, 0.009f), new Vector3(0.124f, 0.294f, 0.016f));   // gilt edge peeking out behind the plate
            mb.Set(S.Iron, new Color(0.16f, 0.13f, 0.1f));
            mb.Push(new Vector3(0, -0.1f, 0.03f), Quaternion.Euler(-90, 0, 0), Vector3.one); mb.Lathe(new[] { new Vector2(0.001f, 0f), new Vector2(0.05f, 0.02f), new Vector2(0.02f, 0.05f) }, 8); mb.Pop();
            var arm = new List<Vector3> { new Vector3(0, -0.05f, 0.02f), new Vector3(0, -0.02f, 0.12f), new Vector3(0, 0.06f, 0.2f) };
            mb.Tube(arm, 0.012f, 6);
            foreach (float sx in new[] { -0.09f, 0.09f })
            {
                var b = new Vector3(sx, 0.06f, 0.2f);
                mb.Set(S.Brass, Color.white);
                mb.Rod(new Vector3(0, 0.06f, 0.2f), b, 0.01f, 5, false);
                mb.Push(b, 0); mb.Lathe(new[] { new Vector2(0.001f, -0.02f), new Vector2(0.04f, 0f), new Vector2(0.035f, 0.012f) }, 10); mb.Pop();
                mb.Set(S.Wax, Color.white); mb.Cyl(b + Vector3.up * 0.01f, 0.014f, 0.09f, 6);
            }
            mb.Pop();
            foreach (float sx in new[] { -0.09f, 0.09f })
            {
                var w = p + q * new Vector3(sx, 0.16f, 0.2f);
                FlameQuad(mb, w, 0.06f, rv.Room.Circuit);
            }
            mb.Set(S.Halo, warm, MansionMats.GlowData(0.35f, 0.25f, 0, rv.Room.Circuit));
            HaloQuad(mb, p + q * new Vector3(0, 0.2f, 0.14f), 0.9f);
            if (intensity > 0) AddLight(rv, p + n * 0.35f + Vector3.up * 0.15f, warm, intensity, 4.2f, LightType.Point, false, 0.18f);
        }

        /// <summary>Candle flame billboard: 4 verts at the wick, uv carries the corner (GPU billboard).</summary>
        internal static void FlameQuad(MeshBuilder mb, Vector3 wick, float height, int group)
        {
            mb.Set(S.Flame, new Color(1, 1, 1, 1), new Vector4(1f, 0.6f, height * 1.35f, group + 10));
            mb.QuadRaw(wick, wick, wick, wick, new Vector2(-1, 0), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, 0), Vector3.up);
        }

        /// <summary>Camera-independent soft halo: three crossed quads with radial falloff (additive).</summary>
        internal static void HaloQuad(MeshBuilder mb, Vector3 c, float size)
        {
            var keep = mb.Custom;
            mb.Custom = new Vector4(keep.x, keep.y, size, keep.w);
            mb.QuadRaw(c, c, c, c, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), Vector3.up);
            mb.Custom = keep;
        }

        void NeonAccents(RoomView rv, MeshBuilder mb)
        {
            var r = rv.Room; var pal = rv.Pal; var R = r.Rect;
            // electric neon strips read as a nightclub, not a manor: the gothic look keeps only practical (candle/gas) light
            if (!NeonStrips) return;
            bool neon = r.Type == RoomType.GameRoom || r.Type == RoomType.MusicRoom || r.Type == RoomType.Theater || r.Type == RoomType.Pool || r.Type == RoomType.EmptyAuditorium ||
                        r.Type == RoomType.GrandHall || r.Type == RoomType.Landing || r.Type == RoomType.ClockMuseum || r.Type == RoomType.MirrorWater || r.Type == RoomType.Elevator || r.Type == RoomType.Lounge;
            if (!neon) return;
            // a neon line under the crown on the two long walls (magenta / cyan pair)
            float y = rv.CeilY - 0.34f;
            Color c1 = pal.Neon, c2 = pal.Accent2;
            float inset = 0.13f;
            bool alongX = R.W >= R.D;
            if (r.Type == RoomType.GrandHall || r.Type == RoomType.Landing) { y = r.Type == RoomType.GrandHall ? 4.25f : rv.CeilY - 0.34f; }
            var lines = new List<(Vector3 a, Vector3 b, Color c)>();
            if (alongX)
            {
                lines.Add((new Vector3(R.x0 + 0.6f, y, R.z0 + inset), new Vector3(R.x1 - 0.6f, y, R.z0 + inset), c1));
                lines.Add((new Vector3(R.x0 + 0.6f, y, R.z1 - inset), new Vector3(R.x1 - 0.6f, y, R.z1 - inset), c2));
            }
            else
            {
                lines.Add((new Vector3(R.x0 + inset, y, R.z0 + 0.6f), new Vector3(R.x0 + inset, y, R.z1 - 0.6f), c1));
                lines.Add((new Vector3(R.x1 - inset, y, R.z0 + 0.6f), new Vector3(R.x1 - inset, y, R.z1 - 0.6f), c2));
            }
            foreach (var (a, b, c) in lines)
            {
                mb.Set(S.Glow, c, MansionMats.GlowData(2.6f, 0.05f, 0, r.Circuit));
                mb.Rod(a, b, 0.016f, 6, true);
                float len = Vector3.Distance(a, b);
                int nl = Math.Max(1, (int)(len / 6f));
                for (int i = 0; i < nl; i++)
                {
                    var p = Vector3.Lerp(a, b, (i + 0.5f) / nl);
                    var into = new Vector3(R.CX, p.y, R.CZ) - p; into.y = 0; into.Normalize();
                    AddLight(rv, p + into * 0.4f - Vector3.up * 0.2f, c, 1.1f, 4.5f, neon: true);
                }
            }
        }

        // ------------------------------------------------------------------ moonlight through windows
        void BuildMoonlight()
        {
            var cookie = MansionMats.Proc("WindowCookie");
            var stainedCookie = MansionMats.Proc("StainedCookie");
            var perRoom = new Dictionary<int, int>();
            var rayMb = new Dictionary<int, MeshBuilder>();
            foreach (var w in Windows)
            {
                var rv = Rooms[w.Room]; var r = rv.Room;
                perRoom.TryGetValue(w.Room, out int n);
                int max = r.Rect.Area > 60 ? 3 : r.Type == RoomType.Corridor ? 2 : 1;
                if (n >= max) continue;
                // spread choices: skip some windows in long rows
                if (n > 0 && ((int)(w.Center.x * 7 + w.Center.z * 13) % 2 == 0)) continue;
                perRoom[w.Room] = n + 1;
                w.Lit = true;
                Vector3 inward = w.Normal;
                Vector3 dir = (inward * 1.0f + Vector3.down * 0.75f).normalized;
                Vector3 pos = w.Center - inward * 2.2f + Vector3.up * 1.4f;
                Color moon = w.Stained ? Color.Lerp(new Color(0.75f, 0.8f, 1f), new Color(0.85f, 0.42f, 0.32f), 0.22f) : new Color(0.55f, 0.68f, 1f);
                AddLight(rv, pos, moon, w.Stained ? 16f : 11f, 11f, LightType.Spot, n == 0 && (r.Type == RoomType.Chapel || r.Type == RoomType.Dining || r.Type == RoomType.Library || r.Type == RoomType.Lounge),
                    0, false, true, false, Quaternion.LookRotation(dir), 58f, w.Stained ? stainedCookie : cookie);
                // volumetric shaft (additive cone, soft)
                if (!rayMb.TryGetValue(w.Room, out var mb)) { mb = new MeshBuilder(); rayMb[w.Room] = mb; }
                Color rc = w.Stained ? Color.Lerp(new Color(0.5f, 0.6f, 1f), new Color(0.85f, 0.42f, 0.32f), 0.15f) : new Color(0.45f, 0.58f, 1f);
                mb.Set(S.Ray, rc, MansionMats.GlowData(0.09f, 0f, 0, -1));
                float above = w.Center.y - rv.FloorY;
                float toFloor = above / Mathf.Max(0.2f, -dir.y);
                float depth = Mathf.Abs(inward.x) > 0.5f ? rv.Room.Rect.W : rv.Room.Rect.D;
                float horiz = new Vector2(dir.x, dir.z).magnitude;
                float toWall = (depth - 0.4f) / Mathf.Max(0.1f, horiz);
                RayShaft(mb, w.Center, inward, w.W * 0.85f, w.H * 0.8f, dir, Mathf.Max(0.5f, Mathf.Min(toFloor * 0.95f, toWall)));
            }
            foreach (var kv in rayMb) Emit(Rooms[kv.Key], kv.Value, "MoonShafts", null, ShadowCastingMode.Off);
        }

        /// <summary>Extruded window silhouette along the light direction; uv.y = 0 at the window, 1 at the far end.</summary>
        internal static void RayShaft(MeshBuilder mb, Vector3 winCenter, Vector3 inward, float w, float h, Vector3 dir, float length)
        {
            Vector3 right = Vector3.Cross(Vector3.up, inward).normalized;
            Vector3 up = Vector3.up;
            Vector3[] ring = { -right * w * 0.5f - up * h * 0.5f, -right * w * 0.5f + up * h * 0.5f, right * w * 0.5f + up * h * 0.5f, right * w * 0.5f - up * h * 0.5f };
            Vector3 o0 = winCenter + inward * 0.05f, o1 = o0 + dir * length;
            for (int i = 0; i < 4; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % 4];
                var p0 = o0 + a; var p1 = o0 + b; var q0 = o1 + a * 1.25f; var q1 = o1 + b * 1.25f;
                mb.QuadUV(p0, q0, q1, p1, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            }
        }
    }
}
