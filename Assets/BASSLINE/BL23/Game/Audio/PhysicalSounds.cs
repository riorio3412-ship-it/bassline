using System.Collections.Generic;
using UnityEngine;
using SimRegion = BL23.Sim.BodyRegion;
using CharRegion = BL23.Game.Characters.BodyRegion;
using DamageType = BL23.Sim.DamageType;
using Mat = BL23.Sim.Mat;

namespace BL23.Game.Audio
{
    public enum BlowKind { Punch, Kick, Slap, Shove }
    public enum FallSurface { Wood, Stone, Carpet, Water }
    public enum GunKind { Revolver, DuelingPistol, Shotgun, Rifle, Crossbow }
    public enum GunAction { Fire, Cock, Reload, DryFire, CasingDrop }
    public enum HitSurface { Wood, Stone, Flesh, Ricochet }
    public enum RoomSize { Auto, Small, Medium, Large }
    public enum StruggleKind { Strangle, Ligature, Smother, Drown, Bound, Scuffle }
    public enum VocalKind { Pain, Gasp, Startle, Scream, Sob, Panic, Death, Choke, Effort, Surface }
    public enum RestraintSound { Tie, KnotPull, TapeRip, TapeWrap, Strain, Hop, Crawl }
    public enum PropSound
    {
        ChairTopple, TableScrape, ChairPullOut, ChairPushIn, DrawerOpen, DrawerClose, DrawerHandle, CabinetOpen, CabinetClose,
        TableLeanCreak, CandlestickFall, BooksFall, HandOver, BodyThudWall, BodyThudTable, ScuffleFeet, BoneCrack, StabPull, WaterSlosh
    }

    /// <summary>
    /// The physical / violence sound pack (Resources/Sfx, manifest _manifest_phys.txt, built by BL23Lab/AudioLab/Forge `phys`;
    /// ids and usage in Documentation/BL23/ViolenceMotionContract.md, "Published sound ids (audio track)").
    /// Everything plays through <see cref="Sfx"/> in 3D with variant rotation and pitch/level jitter; callers never need file
    /// names. Kernel state decides what happens; these calls only present it.
    /// <code>
    /// PhysicalSounds.Impact(DamageType.Stab, BodyRegion.Abdomen, hitPos, 0.8f);
    /// PhysicalSounds.Vocal("P03", VocalKind.Pain, headPos);
    /// var bed = PhysicalSounds.Struggle(StruggleKind.Drown, 1f, basinPos, victimId: "P12", attackerId: "P04");
    /// bed.SetIntensity(struggle01);   // every tick, 1 = thrashing .. 0 = limp
    /// bed.Stop(released: true);       // or Stop() when the victim is dead
    /// PhysicalSounds.Gun(GunKind.Revolver, muzzlePos);   // room tail by room size, the whole house hears it
    /// </code>
    /// </summary>
    public static class PhysicalSounds
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _voices.Clear(); _beds.Clear(); _runner = null; _rng = new System.Random(0x5EED); }

        static System.Random _rng = new System.Random(0x5EED);
        static float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        static AudioSource P(string id, Vector3 pos, float vol = 1f, float pitch = 1f, float lowpassHz = 0f)
            => id == null ? null : Sfx.PlayEx(id, pos, Mathf.Clamp01(vol), pitch, -1, lowpassHz);

        // ------------------------------------------------------------------ impacts
        /// <summary>The id a wound of this kind in this region sounds like (null for silent kinds such as burns).</summary>
        public static string ImpactId(DamageType type, SimRegion region)
        {
            bool head = region == SimRegion.Head || region == SimRegion.Neck;
            switch (type)
            {
                case DamageType.Stab: return "hit_stab";
                case DamageType.Cut: return "hit_slash";
                case DamageType.Blunt: case DamageType.Crush: return head ? "hit_blunt_head" : "hit_blunt_body";
                case DamageType.Fall: return "hit_blunt_body";
                case DamageType.Choke: return "struggle_cloth";
                case DamageType.Drown: return "drown_thrash";
                case DamageType.None: return "hit_punch";
                default: return null;   // Burn, Shock: no body sound
            }
        }

        /// <summary>A weapon meeting a body: knife stab/slash through cloth, blunt blows to the head (skull knock) or the body.
        /// strength01 scales level (and a heavy crush to the head adds a bone crack).</summary>
        public static AudioSource Impact(DamageType type, SimRegion region, Vector3 pos, float strength01 = 1f)
        {
            string id = ImpactId(type, region); if (id == null) return null;
            float s = Mathf.Clamp01(strength01);
            var src = P(id, pos, Mathf.Lerp(0.55f, 1f, s), 1f - 0.04f * s);
            bool head = region == SimRegion.Head || region == SimRegion.Neck;
            if ((type == DamageType.Crush && s > 0.6f) || (type == DamageType.Blunt && head && s > 0.85f)) P("bone_crack", pos, 0.55f + 0.35f * s);
            return src;
        }
        /// <summary>Same with the character track's region enum (identical order).</summary>
        public static AudioSource Impact(DamageType type, CharRegion region, Vector3 pos, float strength01 = 1f) => Impact(type, (SimRegion)(int)region, pos, strength01);

        /// <summary>Bare-handed contact: punch, kick, slap, shove (a shove is cloth and a stumble of feet).</summary>
        public static AudioSource Blow(BlowKind kind, Vector3 pos, float strength01 = 1f)
        {
            float v = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(strength01));
            switch (kind)
            {
                case BlowKind.Punch: return P("hit_punch", pos, v);
                case BlowKind.Kick: return P("hit_kick", pos, v);
                case BlowKind.Slap: return P("slap", pos, v);
                default: P("scuffle_feet", pos, v * 0.8f); return P("struggle_cloth", pos, v);
            }
        }

        // ------------------------------------------------------------------ bodies
        /// <summary>A body hitting the floor (energy01: 0 a slump .. 1 a full-height collapse).</summary>
        public static AudioSource Fall(FallSurface surface, float energy01, Vector3 pos)
        {
            float e = Mathf.Clamp01(energy01);
            string id = surface == FallSurface.Water ? "fall_body_water" : surface == FallSurface.Carpet ? "fall_body_carpet" : surface == FallSurface.Stone ? "fall_body_stone" : "fall_body_wood";
            return P(id, pos, Mathf.Lerp(0.45f, 1f, e), Mathf.Lerp(1.04f, 0.94f, e));
        }
        /// <summary>By floor material name (as <see cref="Sfx.Footstep"/>): "Marble", "Wood", "Carpet", "Stone", "Water"...</summary>
        public static AudioSource Fall(string floorMaterial, float energy01, Vector3 pos) => Fall(SurfaceOf(floorMaterial), energy01, pos);

        public static FallSurface SurfaceOf(string floorMaterial)
        {
            if (string.IsNullOrEmpty(floorMaterial)) return FallSurface.Stone;
            string s = floorMaterial.ToLowerInvariant();
            if (s.Contains("water") || s.Contains("wet") || s.Contains("pool") || s.Contains("bath") || s.Contains("puddle")) return FallSurface.Water;
            if (s.Contains("carpet") || s.Contains("rug") || s.Contains("fabric") || s.Contains("cloth") || s.Contains("bed") || s.Contains("grass") || s.Contains("soil")) return FallSurface.Carpet;
            if (s.Contains("wood") || s.Contains("parquet") || s.Contains("plank") || s.Contains("board") || s.Contains("stage")) return FallSurface.Wood;
            return FallSurface.Stone;
        }

        /// <summary>One heave of a body dragged across the floor (call on each pull of the dragger).</summary>
        public static AudioSource Drag(FallSurface surface, Vector3 pos, float vol = 1f)
        {
            if (surface == FallSurface.Water) return P("water_slosh_floor", pos, vol * 0.5f);
            return P(surface == FallSurface.Carpet ? "body_drag_carpet" : "body_drag_wood", pos, vol, surface == FallSurface.Stone ? 1.06f : 1f);
        }

        // ------------------------------------------------------------------ furniture and props
        /// <summary>Something breaking: glass and porcelain shatter, wood cracks (large = a table or a door), metal clangs...</summary>
        public static AudioSource Break(Mat material, Vector3 pos, float energy01 = 1f, bool large = false)
        {
            float v = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(energy01));
            switch (material)
            {
                case Mat.Glass: return P("glass_shatter", pos, v);
                case Mat.Ceramic: return P("ceramic_break", pos, v);
                case Mat.Stone: return P("ceramic_break", pos, v, 0.72f);
                case Mat.Wood: return P(large ? "wood_break_table" : "wood_break_chair", pos, v);
                case Mat.Metal: return P(large ? "metal_clang" : "metal_dent", pos, v);
                case Mat.Paper: return P("books_fall", pos, v);
                case Mat.Cloth: case Mat.Leather: return P("tape_rip", pos, v * 0.8f, 0.65f);
                case Mat.Plastic: return P("wood_crack", pos, v, 1.3f);
                case Mat.Plant: return P("wood_crack", pos, v * 0.8f, 1.25f);
                case Mat.Liquid: return P("water_splash", pos, v);
                default: return P("hit_blunt_body", pos, v * 0.8f, 1.15f);   // Food, Flesh
            }
        }

        public static string PropId(PropSound s)
        {
            switch (s)
            {
                case PropSound.ChairTopple: return "chair_topple";
                case PropSound.TableScrape: return "table_scrape";
                case PropSound.ChairPullOut: return "chair_pull_out";
                case PropSound.ChairPushIn: return "chair_push_in";
                case PropSound.DrawerOpen: return "drawer_open";
                case PropSound.DrawerClose: return "drawer_close";
                case PropSound.DrawerHandle: return "drawer_handle";
                case PropSound.CabinetOpen: return "cabinet_open";
                case PropSound.CabinetClose: return "cabinet_close";
                case PropSound.TableLeanCreak: return "table_lean_creak";
                case PropSound.CandlestickFall: return "candlestick_fall";
                case PropSound.BooksFall: return "books_fall";
                case PropSound.HandOver: return "handover_rustle";
                case PropSound.BodyThudWall: return "body_thud_wall";
                case PropSound.BodyThudTable: return "body_thud_table";
                case PropSound.ScuffleFeet: return "scuffle_feet";
                case PropSound.BoneCrack: return "bone_crack";
                case PropSound.StabPull: return "hit_stab_pull";
                case PropSound.WaterSlosh: return "water_slosh_floor";
                default: return null;
            }
        }
        public static AudioSource Prop(PropSound sound, Vector3 pos, float vol = 1f) => P(PropId(sound), pos, vol);

        /// <summary>Picking up / putting down by material (glass, metal, wood, paper, cloth, ceramic).</summary>
        public static AudioSource Handle(Mat material, bool pickUp, Vector3 pos, float vol = 1f)
        {
            string m; float pitch = 1f;
            switch (material)
            {
                case Mat.Glass: case Mat.Liquid: m = "glass"; break;
                case Mat.Metal: m = "metal"; break;
                case Mat.Wood: m = "wood"; break;
                case Mat.Plastic: m = "wood"; pitch = 1.2f; break;
                case Mat.Paper: m = "paper"; break;
                case Mat.Ceramic: m = "ceramic"; break;
                case Mat.Stone: m = "ceramic"; pitch = 0.8f; break;
                default: m = "cloth"; break;   // Cloth, Leather, Food, Plant, Flesh
            }
            return P((pickUp ? "handle_pick_" : "handle_put_") + m, pos, vol, pitch);
        }

        // ------------------------------------------------------------------ restraint
        public static string RestraintId(RestraintSound s)
        {
            switch (s)
            {
                case RestraintSound.Tie: return "rope_tie";
                case RestraintSound.KnotPull: return "rope_knot_pull";
                case RestraintSound.TapeRip: return "tape_rip";
                case RestraintSound.TapeWrap: return "tape_wrap";
                case RestraintSound.Strain: return "rope_strain";
                case RestraintSound.Hop: return "bound_hop";
                default: return "bound_crawl";
            }
        }
        public static AudioSource Restraint(RestraintSound sound, Vector3 pos, float vol = 1f) => P(RestraintId(sound), pos, vol);

        // ------------------------------------------------------------------ firearms and the crossbow
        static readonly RaycastHit[] _hits = new RaycastHit[32];

        /// <summary>
        /// A shot. The dry report plays at the muzzle (heard up to 80 m) with a room tail picked by the size of the room
        /// (RoomSize.Auto measures it with a few raycasts); behind walls it is muffled, and a low through-the-walls boom (2D)
        /// makes sure the whole house hears it. The crossbow is nearly silent: only its twang.
        /// </summary>
        public static void Gun(GunKind kind, Vector3 pos, RoomSize size = RoomSize.Auto)
        {
            if (kind == GunKind.Crossbow) { P("xbow_twang", pos); return; }
            string dry = kind == GunKind.Revolver ? "gun_revolver_shot" : kind == GunKind.DuelingPistol ? "gun_pistol_shot" : kind == GunKind.Shotgun ? "gun_shotgun_shot" : "gun_rifle_shot";
            if (size == RoomSize.Auto) size = MeasureRoom(pos);
            string tail = size == RoomSize.Small ? "gun_tail_small" : size == RoomSize.Medium ? "gun_tail_medium" : "gun_tail_large";
            var ear = AudioEars.Ear; Vector3 earPos = ear != null ? ear.position : pos;
            float dist = Vector3.Distance(pos, earPos);
            bool occluded = dist > 1.5f && Occluded(pos, earPos);
            if (!occluded) { P(dry, pos, 1f); P(tail, pos, 0.9f); }
            else { P(dry, pos, 0.8f, 1f, 1100f); P(tail, pos, 0.85f, 1f, 1500f); }
            float far = Mathf.Clamp01((dist - 6f) / 25f); if (occluded) far = Mathf.Max(far, 0.6f);
            float house = Mathf.Lerp(0.2f, 1f, far) * Mathf.Clamp01(1.35f - dist / 90f);
            if (kind == GunKind.Shotgun || kind == GunKind.Rifle) house = Mathf.Min(1f, house * 1.15f);
            if (house > 0.03f) Sfx.PlayEx("gun_distant", null, house);
        }

        /// <summary>Handling: cock (hammer / crossbow windlass), reload (cylinder / powder and ramrod / shells / bolt), dry fire,
        /// a casing or shell dropping. GunAction.Fire calls <see cref="Gun"/>.</summary>
        public static AudioSource GunHandling(GunKind kind, GunAction action, Vector3 pos)
        {
            if (action == GunAction.Fire) { Gun(kind, pos); return null; }
            string id = null;
            switch (kind)
            {
                case GunKind.Revolver:
                    id = action == GunAction.Cock ? "gun_revolver_cock" : action == GunAction.Reload ? "gun_revolver_reload" : action == GunAction.DryFire ? "gun_dry_fire" : "gun_casing_drop"; break;
                case GunKind.DuelingPistol:
                    id = action == GunAction.Cock ? "gun_pistol_cock" : action == GunAction.Reload ? "gun_pistol_reload" : action == GunAction.DryFire ? "gun_dry_fire" : null; break;   // a muzzle-loader drops no casing
                case GunKind.Shotgun: case GunKind.Rifle:
                    id = action == GunAction.Cock ? "gun_shotgun_cock" : action == GunAction.Reload ? "gun_shotgun_reload" : action == GunAction.DryFire ? "gun_dry_fire" : "gun_shell_drop"; break;
                case GunKind.Crossbow:
                    id = action == GunAction.Cock ? "xbow_crank" : action == GunAction.Reload ? "xbow_load" : action == GunAction.DryFire ? "xbow_twang" : null; break;
            }
            return P(id, pos, action == GunAction.DryFire && kind == GunKind.Crossbow ? 0.6f : 1f);
        }

        public static AudioSource BulletHit(HitSurface surface, Vector3 pos)
            => P(surface == HitSurface.Wood ? "bullet_wood" : surface == HitSurface.Stone ? "bullet_stone" : surface == HitSurface.Flesh ? "bullet_flesh" : "bullet_ricochet", pos);
        /// <summary>A bullet hitting a prop of this material (metal ricochets, glass shatters).</summary>
        public static AudioSource BulletHit(Mat material, Vector3 pos)
        {
            switch (material)
            {
                case Mat.Wood: case Mat.Paper: case Mat.Cloth: case Mat.Leather: case Mat.Plant: case Mat.Plastic: return BulletHit(HitSurface.Wood, pos);
                case Mat.Flesh: case Mat.Food: return BulletHit(HitSurface.Flesh, pos);
                case Mat.Metal: P("metal_dent", pos, 0.8f); return BulletHit(HitSurface.Ricochet, pos);
                case Mat.Glass: P("glass_shatter", pos, 0.9f); return BulletHit(HitSurface.Wood, pos);
                case Mat.Liquid: return P("water_splash", pos);
                default: return BulletHit(HitSurface.Stone, pos);   // Stone, Ceramic
            }
        }
        /// <summary>A crossbow bolt striking wood (with a shaft quiver) or a body.</summary>
        public static AudioSource Bolt(bool flesh, Vector3 pos) => P(flesh ? "xbow_bolt_flesh" : "xbow_bolt_wood", pos);

        static RoomSize MeasureRoom(Vector3 p)
        {
            Vector3 o = p + Vector3.up * 0.2f; float sum = 0f;
            for (int k = 0; k < 8; k++) sum += RayDist(o, Quaternion.Euler(0f, k * 45f + 22.5f, 0f) * Vector3.forward, 30f);
            float avg = sum / 8f, up = RayDist(o, Vector3.up, 20f);
            if (avg < 3.2f && up < 3.6f) return RoomSize.Small;
            if (avg < 7.5f && up < 6.5f) return RoomSize.Medium;
            return RoomSize.Large;
        }
        static float RayDist(Vector3 o, Vector3 dir, float max)
        {
            int n = Physics.RaycastNonAlloc(o, dir, _hits, max, ~0, QueryTriggerInteraction.Ignore); float best = max;
            for (int i = 0; i < n; i++) { var h = _hits[i]; if (h.distance < 0.5f || IsSmallOrMoving(h)) continue; if (h.distance < best) best = h.distance; }
            return best;
        }
        static bool Occluded(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a; float len = d.magnitude; if (len < 1f) return false;
            int n = Physics.RaycastNonAlloc(a, d / len, _hits, len, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) { var h = _hits[i]; if (h.distance < 0.8f || h.distance > len - 0.8f || IsSmallOrMoving(h)) continue; return true; }
            return false;
        }
        static bool IsSmallOrMoving(RaycastHit h)
        {
            if (h.rigidbody != null && !h.rigidbody.isKinematic) return true;
            var sz = h.collider.bounds.size; return Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z)) < 1.4f;   // people, props: not walls
        }

        // ------------------------------------------------------------------ resident voices
        sealed class Voice { public AudioSource Src; public AudioClip Clip; public VocalKind Kind; public float T; }
        static readonly Dictionary<string, Voice> _voices = new Dictionary<string, Voice>();

        /// <summary>The vocal id for a resident (vo_p03_scream). Unknown ids (the butler, tests) borrow a resident voice of
        /// the same gender; without the pack, the old synthesized gasp / scream.</summary>
        public static string VocalId(string actorId, VocalKind kind)
        {
            string k = kind.ToString().ToLowerInvariant();
            if (!string.IsNullOrEmpty(actorId))
            {
                string id = "vo_" + actorId.ToLowerInvariant() + "_" + k;
                if (Sfx.Has(id)) return id;
            }
            var c = BL23.Sim.Cast.Get(actorId); bool female = c != null && c.Gender == BL23.Sim.Gender.F;
            string fb = "vo_" + (female ? "p03" : "p01") + "_" + k;
            if (Sfx.Has(fb)) return fb;
            return kind == VocalKind.Scream ? "scream_muffled" : kind == VocalKind.Gasp || kind == VocalKind.Startle || kind == VocalKind.Surface ? "gasp" : null;
        }

        static int Priority(VocalKind k)
        {
            switch (k)
            {
                case VocalKind.Death: return 5;
                case VocalKind.Scream: case VocalKind.Surface: return 4;
                case VocalKind.Pain: case VocalKind.Choke: return 3;
                case VocalKind.Startle: case VocalKind.Gasp: return 2;
                default: return 1;   // Sob, Panic, Effort
            }
        }

        /// <summary>
        /// A resident's vocal reaction in their own voice (pitch and vocal tract per resident, gender and age matched).
        /// One voice per person: a more urgent reaction cuts a lesser one, a lesser one waits. lowpassHz muffles it
        /// (a pillow ~600, a hand or a gag ~900, under water ~800, through a wall ~1000).
        /// </summary>
        public static AudioSource Vocal(string actorId, VocalKind kind, Vector3 pos, float vol = 1f, float lowpassHz = 0f)
        {
            string id = VocalId(actorId, kind); if (id == null) return null;
            string key = actorId ?? "?"; float now = Time.unscaledTime;
            if (_voices.TryGetValue(key, out var cur) && cur.Src != null && cur.Src.isPlaying && cur.Src.clip == cur.Clip)
            {
                int pn = Priority(kind), pc = Priority(cur.Kind);
                if (pn < pc || (pn == pc && now - cur.T < 0.35f)) return null;
                cur.Src.Stop();
            }
            var src = P(id, pos, vol, 1f, lowpassHz);
            if (src != null) _voices[key] = new Voice { Src = src, Clip = src.clip, Kind = kind, T = now };
            return src;
        }
        public static bool IsVocalizing(string actorId) => actorId != null && _voices.TryGetValue(actorId, out var v) && v.Src != null && v.Src.isPlaying && v.Src.clip == v.Clip;
        public static void StopVocal(string actorId) { if (actorId != null && _voices.TryGetValue(actorId, out var v) && v.Src != null && v.Src.clip == v.Clip) v.Src.Stop(); }

        // ------------------------------------------------------------------ prolonged kills (struggle beds)
        static readonly List<StruggleBed> _beds = new List<StruggleBed>();
        static PhysicalSoundsRunner _runner;

        /// <summary>
        /// Start the sound bed of a prolonged struggle (strangling by hand or with a cord, smothering, drowning, fighting one's
        /// bonds, a scuffle). Drive it with <see cref="StruggleBed.SetIntensity"/> every tick (1 = thrashing, 0 = limp) and end
        /// it with <see cref="StruggleBed.Stop"/>. It schedules varied one-shots (heels drumming, cloth, rope creak, splashes,
        /// bubbles, the victim's choking in their own voice, the attacker's effort) and a water loop for drowning.
        /// </summary>
        public static StruggleBed Struggle(StruggleKind kind, float intensity01, Vector3 pos, string victimId = null, string attackerId = null, bool gagged = false)
        {
            if (_runner == null)
            {
                var go = new GameObject("BL23_PhysicalSounds"); Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<PhysicalSoundsRunner>();
            }
            var b = new StruggleBed(kind, pos, victimId, attackerId, gagged); b.SetIntensity(intensity01);
            _beds.Add(b); return b;
        }
        public static void StopAllStruggles() { for (int i = _beds.Count - 1; i >= 0; i--) _beds[i].Stop(); }

        internal static void Tick(float dt)
        {
            for (int i = _beds.Count - 1; i >= 0; i--) { var b = _beds[i]; if (!b.Active) { _beds.RemoveAt(i); continue; } b.Tick(dt); }
        }
        internal static float Rand(float a, float b) => R(a, b);
        internal static AudioSource Play(string id, Vector3 pos, float vol, float lowpassHz) => P(id, pos, vol, 1f, lowpassHz);
    }

    /// <summary>A running struggle sound bed (see <see cref="PhysicalSounds.Struggle"/>).</summary>
    public sealed class StruggleBed
    {
        public StruggleKind Kind { get; }
        public string Victim { get; }
        public string Attacker { get; }
        public bool Gagged { get; set; }
        public bool Active { get; private set; } = true;
        public float Intensity { get; private set; }

        sealed class Layer
        {
            public string Id; public bool IsVoice, AttackerVoice; public VocalKind Voice;
            public float RateLo, RateHi, VolLo, VolHi, MinI, Lowpass, Timer;
        }
        readonly List<Layer> _layers = new List<Layer>();
        Vector3 _pos; Transform _follow; float _smooth; SfxHandle _loop;

        internal StruggleBed(StruggleKind kind, Vector3 pos, string victim, string attacker, bool gagged)
        {
            Kind = kind; _pos = pos; Victim = victim; Attacker = attacker; Gagged = gagged;
            float gag = gagged ? 900f : 0f;
            switch (kind)
            {
                case StruggleKind.Strangle:
                case StruggleKind.Ligature:
                    Add("strangle_heels", 0f, 2.0f, 0.45f, 1f, 0.15f);
                    Add("struggle_cloth", 0.3f, 1.6f, 0.5f, 1f, 0f);
                    Add("scuffle_feet", 0f, 0.6f, 0.5f, 0.9f, 0.45f);
                    if (kind == StruggleKind.Ligature) Add("rope_creak_tension", 0.6f, 1.8f, 0.55f, 1f, 0f);
                    Voice(false, VocalKind.Choke, 0.25f, 0.55f, 0.45f, 1f, 0.03f, gag);
                    Voice(true, VocalKind.Effort, 0f, 0.3f, 0.5f, 0.8f, 0.35f, 0f);
                    break;
                case StruggleKind.Smother:
                    Add("smother_pillow", 0.4f, 1.5f, 0.5f, 1f, 0f);
                    Add("struggle_cloth", 0.3f, 1.2f, 0.5f, 0.9f, 0f);
                    Add("strangle_heels", 0f, 1.3f, 0.4f, 0.8f, 0.2f, 2000f);
                    Voice(false, VocalKind.Choke, 0.25f, 0.5f, 0.5f, 1f, 0.03f, 700f);
                    Voice(false, VocalKind.Scream, 0f, 0.2f, 0.6f, 1f, 0.6f, 600f);
                    Voice(true, VocalKind.Effort, 0f, 0.25f, 0.5f, 0.8f, 0.4f, 0f);
                    break;
                case StruggleKind.Drown:
                    _loop = Sfx.Loop("water_churn_loop", pos, 0.2f, 0.4f);
                    Add("drown_thrash", 0f, 1.8f, 0.5f, 1f, 0.1f);
                    Add("drown_bubbles", 0.35f, 1.4f, 0.5f, 1f, 0f);
                    Add("drown_gurgle", 0f, 0.5f, 0.5f, 0.9f, 0.1f);
                    Add("water_slosh_floor", 0f, 0.4f, 0.5f, 0.9f, 0.5f);
                    Voice(false, VocalKind.Choke, 0.15f, 0.45f, 0.45f, 0.9f, 0.05f, 800f);
                    Voice(true, VocalKind.Effort, 0f, 0.3f, 0.5f, 0.8f, 0.4f, 0f);
                    break;
                case StruggleKind.Bound:
                    Add("rope_strain", 0.2f, 1.3f, 0.5f, 1f, 0f);
                    Add("struggle_cloth", 0.2f, 1.0f, 0.5f, 0.9f, 0f);
                    Voice(false, VocalKind.Effort, 0f, 0.45f, 0.5f, 0.9f, 0.2f, gag);
                    Voice(false, VocalKind.Panic, 0.1f, 0.28f, 0.5f, 0.8f, 0.05f, gag);
                    break;
                default: // Scuffle
                    Add("scuffle_feet", 0.4f, 1.6f, 0.5f, 1f, 0f);
                    Add("struggle_cloth", 0.5f, 1.6f, 0.5f, 1f, 0f);
                    Voice(false, VocalKind.Effort, 0f, 0.5f, 0.5f, 0.9f, 0.2f, 0f);
                    Voice(true, VocalKind.Effort, 0f, 0.5f, 0.5f, 0.9f, 0.2f, 0f);
                    break;
            }
            foreach (var l in _layers) l.Timer = PhysicalSounds.Rand(0f, 0.4f);
        }

        void Add(string id, float rateLo, float rateHi, float volLo, float volHi, float minI, float lowpass = 0f)
            => _layers.Add(new Layer { Id = id, RateLo = rateLo, RateHi = rateHi, VolLo = volLo, VolHi = volHi, MinI = minI, Lowpass = lowpass });
        void Voice(bool attacker, VocalKind k, float rateLo, float rateHi, float volLo, float volHi, float minI, float lowpass)
            => _layers.Add(new Layer { IsVoice = true, AttackerVoice = attacker, Voice = k, RateLo = rateLo, RateHi = rateHi, VolLo = volLo, VolHi = volHi, MinI = minI, Lowpass = lowpass });

        /// <summary>Struggle strength from the kernel assault state: 1 = thrashing, 0 = limp.</summary>
        public void SetIntensity(float struggle01) { Intensity = Mathf.Clamp01(struggle01); }
        public void SetPosition(Vector3 p) { _pos = p; }
        public void Follow(Transform t) { _follow = t; }

        /// <summary>End the bed. released: the victim was let go (someone came, the attacker fled) — a surfacing / freed
        /// victim who is still alive heaves a desperate gasp and coughs.</summary>
        public void Stop(bool released = false, bool victimAlive = true)
        {
            if (!Active) return;
            Active = false;
            if (_loop != null) { _loop.Stop(1.2f); _loop = null; }
            if (released && victimAlive && Kind != StruggleKind.Bound && Kind != StruggleKind.Scuffle)
            {
                if (Kind == StruggleKind.Drown) { PhysicalSounds.Play("drown_thrash", _pos, 0.8f, 0f); PhysicalSounds.Play("water_slosh_floor", _pos, 0.8f, 0f); }
                if (!string.IsNullOrEmpty(Victim)) PhysicalSounds.Vocal(Victim, VocalKind.Surface, _pos);
            }
        }

        internal void Tick(float dt)
        {
            if (_follow != null) _pos = _follow.position;
            _smooth = Mathf.MoveTowards(_smooth, Intensity, dt * 1.5f);
            float i = _smooth;
            if (_loop != null) { _loop.SetPosition(_pos); _loop.SetVolume(0.15f + 0.85f * i); }
            foreach (var l in _layers)
            {
                l.Timer -= dt; if (l.Timer > 0f) continue;
                float rate = Mathf.Lerp(l.RateLo, l.RateHi, Mathf.Pow(i, 1.2f));
                l.Timer = rate > 0.01f ? (1f / rate) * PhysicalSounds.Rand(0.6f, 1.4f) : 0.5f;
                if (i < l.MinI || rate <= 0.01f) continue;
                float vol = Mathf.Lerp(l.VolLo, l.VolHi, i);
                if (l.IsVoice)
                {
                    string who = l.AttackerVoice ? Attacker : Victim; if (string.IsNullOrEmpty(who)) continue;
                    float lp = l.Lowpass > 0f ? l.Lowpass : (!l.AttackerVoice && Gagged ? 900f : 0f);
                    PhysicalSounds.Vocal(who, l.Voice, _pos + Vector3.up * 0.3f, vol, lp);
                }
                else PhysicalSounds.Play(l.Id, _pos, vol, l.Lowpass);
            }
        }
    }
}
