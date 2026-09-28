using System;
using System.Collections.Generic;
using System.Reflection;
using BL23.Game.Characters;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// The character track's newer motion API (ViolenceMotionContract.md "Published names": ActorAnimator.BeginPaired /
    /// SetPairedIntensity / EndPaired, SetRestraint, SetDragging, SetAim, SetHeldWeapon, PlayHit, PlayFall, PlayStartle,
    /// HearNoise, SetPain, PlayDefense; ActorRig.SetStrain; the appended ActionAnim / Gesture / Expr values) is built in the lab
    /// and installed into main later. Everything here calls it by name through reflection and falls back to the closest motion
    /// main already has, so the violence track never waits for an install and picks the real motions up the day they land.
    /// </summary>
    public static class CharBridge
    {
        static readonly Dictionary<string, MethodInfo> _m = new Dictionary<string, MethodInfo>();
        static readonly HashSet<string> _warned = new HashSet<string>();
        static readonly Assembly Asm = typeof(ActorAnimator).Assembly;

        static MethodInfo M(Type t, string name, int argc)
        {
            string key = t.Name + "." + name + "/" + argc;
            if (_m.TryGetValue(key, out var mi)) return mi;
            mi = null;
            foreach (var c in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                if (c.Name == name && c.GetParameters().Length == argc) { mi = c; break; }
            _m[key] = mi; return mi;
        }
        static Type T(string name) => Asm.GetType("BL23.Game.Characters." + name);
        static object E(string type, string value)
        {
            var t = T(type); if (t == null || !t.IsEnum || string.IsNullOrEmpty(value)) return null;
            try { return Enum.Parse(t, value, true); } catch (Exception) { return null; }
        }
        static bool Call(object target, string name, params object[] args)
        {
            if (target == null) return false;
            var mi = M(target.GetType(), name, args.Length); if (mi == null) return false;
            try { mi.Invoke(target, args); return true; }
            catch (Exception e) { if (_warned.Add(name)) Debug.LogWarning($"[BL23 violence] {name} failed: {e.InnerException?.Message ?? e.Message}"); return false; }
        }
        /// <summary>True once the named method exists on ActorAnimator (the lab's API has been installed).</summary>
        public static bool Has(string method, int argc) => M(typeof(ActorAnimator), method, argc) != null;

        // ================================================================== actions / gestures / faces by name
        public static bool Action(ActorAnimator an, string name, ActionAnim fallback, float dur)
        {
            if (an == null) return false;
            if (Enum.TryParse(name, out ActionAnim a)) { an.PlayAction(a, dur); return true; }
            if (fallback != ActionAnim.None) an.PlayAction(fallback, dur);
            return false;
        }
        public static bool Gesture(ActorAnimator an, string name, Gesture fallback, float dur)
        {
            if (an == null) return false;
            if (Enum.TryParse(name, out Gesture g)) { an.PlayGesture(g, dur); return true; }
            if (fallback != Characters.Gesture.None) an.PlayGesture(fallback, dur);
            return false;
        }
        public static void Face(ActorRig rig, string name, Expr fallback, float intensity = 1f)
        {
            if (rig == null) return;
            rig.SetExpression(Enum.TryParse(name, out Expr e) ? e : fallback, intensity);
        }

        // ================================================================== paired prolonged kills
        /// <summary>Begin a paired act (kind: Strangle / GarroteRear / LigatureFront / Smother / Drown). False = not installed yet.</summary>
        public static bool BeginPaired(ActorAnimator attacker, ActorAnimator victim, string kind, float struggle01)
        {
            var k = E("PairedKind", kind); var role = E("PairedRole", "Attacker");
            if (attacker == null || victim == null || k == null || role == null) return false;
            return Call(attacker, "BeginPaired", k, role, victim, struggle01);
        }
        public static bool SetPairedIntensity(ActorAnimator an, float s) => Call(an, "SetPairedIntensity", Mathf.Clamp01(s));
        public static bool EndPaired(ActorAnimator an) => Call(an, "EndPaired");
        public static bool PairedActive(ActorAnimator an)
        {
            if (an == null) return false; var p = an.GetType().GetProperty("PairedActive"); if (p == null) return false;
            try { var v = p.GetValue(an); return v != null && v.ToString() != "None"; } catch (Exception) { return false; }
        }

        // ================================================================== restraint, dragging, weapons
        public static bool SetRestraint(ActorAnimator an, bool wrists, bool ankles, bool gag, bool behind = true)
        {
            var t = T("RestraintFlags"); if (an == null || t == null) return false;
            int f = 0;
            if (wrists) f |= Convert.ToInt32(E("RestraintFlags", behind ? "WristsBack" : "WristsFront") ?? 0);
            if (ankles) f |= Convert.ToInt32(E("RestraintFlags", "Ankles") ?? 0);
            if (gag) f |= Convert.ToInt32(E("RestraintFlags", "Gag") ?? 0);
            return Call(an, "SetRestraint", Enum.ToObject(t, f));
        }
        public static bool SetRestraintStruggle(ActorAnimator an, float s) => Call(an, "SetRestraintStruggle", Mathf.Clamp01(s));
        public static bool SetDragging(ActorAnimator an, ActorRig body, string grip = "Armpits")
        {
            var g = E("DragGrip", grip); if (an == null || g == null) return false;
            return Call(an, "SetDragging", body, g);
        }
        public static bool SetAim(ActorAnimator an, Vector3? target) => Call(an, "SetAim", target);
        /// <summary>Weapon class names: Knife Blade Club Heavy Long Cord Pillow Vial Pistol Rifle Crossbow.</summary>
        public static bool SetHeldWeapon(ActorAnimator an, string cls, float mass)
        {
            var c = E("WeaponClass", cls ?? "None"); if (an == null || c == null) return false;
            return Call(an, "SetHeldWeapon", c, mass);
        }
        public static string WeaponClassOf(BL23.Sim.ItemDef d)
        {
            if (d == null) return "None";
            switch (d.Type) { case "Revolver": case "DuelingPistol": return "Pistol"; case "HuntingShotgun": return "Rifle"; case "Crossbow": return "Crossbow"; case "Pillow": return "Pillow"; }
            if (d.Dmg == BL23.Sim.DamageType.Choke) return "Cord";
            if (d.Tag == "poison" || d.Tag == "sedate") return "Vial";
            if (d.Dmg == BL23.Sim.DamageType.Stab) return d.Size > 0.6f ? "Long" : "Knife";
            if (d.Dmg == BL23.Sim.DamageType.Cut) return "Blade";
            if (d.Dmg == BL23.Sim.DamageType.Blunt) return d.Size > 0.7f ? "Long" : d.Heavy || d.Mass > 2.2f ? "Heavy" : "Club";
            return "None";
        }

        // ================================================================== reactions
        public static bool PlayHit(ActorAnimator an, BodyRegion region, Vector3 dir, float strength) => Call(an, "PlayHit", region, dir, Mathf.Clamp01(strength));
        public static bool PlayFall(ActorAnimator an, Vector3 dir, float strength) => Call(an, "PlayFall", dir, Mathf.Clamp01(strength));
        public static bool PlayStartle(ActorAnimator an, Vector3 source, float strength) => Call(an, "PlayStartle", source, Mathf.Clamp01(strength));
        public static bool HearNoise(ActorAnimator an, Vector3 source, float loud) => Call(an, "HearNoise", source, Mathf.Clamp01(loud));
        public static bool SetPain(ActorAnimator an, float pain) => Call(an, "SetPain", Mathf.Clamp01(pain));
        public static bool PlayDefense(ActorAnimator an, string kind, Vector3 attackerWorld)
        {
            if (an == null || !Enum.TryParse(kind, out ActionAnim a)) return false;
            return Call(an, "PlayDefense", a, attackerWorld);
        }
        /// <summary>Face strain: gasping, the face flushing red, then paling (ActorRig.SetStrain).</summary>
        public static bool SetStrain(ActorRig rig, float gasp, float flush, float pale) => Call(rig, "SetStrain", Mathf.Clamp01(gasp), Mathf.Clamp01(flush), Mathf.Clamp01(pale));
        /// <summary>Water surface (drown) or bed top (smother) in metres above the floor, after BeginPaired (0 = floor).</summary>
        public static bool SetPairedSurface(ActorAnimator an, float metres) => Call(an, "SetPairedSurface", metres);
        public static bool SetCombatReady(ActorAnimator an, bool on, string cls)
        {
            var c = E("WeaponClass", cls ?? "None"); if (an == null || c == null) return false;
            return Call(an, "SetCombatReady", on, c);
        }
        /// <summary>PlayAttack(kind, weaponClass, target, strength): the published attack beat (Shoot recoils). False = not installed.</summary>
        public static bool PlayAttack(ActorAnimator an, string kind, string cls, Vector3? target, float strength = 1f)
        {
            var c = E("WeaponClass", cls ?? "None"); if (an == null || c == null || !Enum.TryParse(kind, out ActionAnim a)) return false;
            return Call(an, "PlayAttack", a, c, target, strength);
        }
        public static bool PlayStagger(ActorAnimator an, Vector3 dir, float strength) => Call(an, "PlayStagger", dir, Mathf.Clamp01(strength));
    }

    /// <summary>
    /// The audio track's physical sound pack (Game/Audio/PhysicalSounds.cs: Impact, Fall, Break, Gun, Struggle, Vocal) by
    /// reflection; until it lands, the nearest existing Sfx id. This track never makes sound ids of its own.
    /// </summary>
    public static class PhysSfx
    {
        static Type _t; static bool _looked;
        static readonly Dictionary<string, MethodInfo> _m = new Dictionary<string, MethodInfo>();
        static readonly HashSet<string> _warned = new HashSet<string>();
        static Type T { get { if (!_looked) { _looked = true; _t = typeof(Audio.Sfx).Assembly.GetType("BL23.Game.Audio.PhysicalSounds"); } return _t; } }
        static bool Muted => Session.I?.TimeDir != null && Session.I.TimeDir.MuteWorldAudio;

        static bool Try(string name, params object[] args)
        {
            var t = T; if (t == null) return false;
            string key = name + "/" + args.Length;
            if (!_m.TryGetValue(key, out var mi))
            {
                mi = null; foreach (var c in t.GetMethods(BindingFlags.Public | BindingFlags.Static)) if (c.Name == name && c.GetParameters().Length == args.Length) { mi = c; break; }
                _m[key] = mi;
            }
            if (mi == null) return false;
            var ps = mi.GetParameters(); var call = new object[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                var pt = ps[i].ParameterType; var a = args[i];
                if (a == null) { call[i] = null; continue; }
                if (pt.IsInstanceOfType(a)) { call[i] = a; continue; }
                var nt = Nullable.GetUnderlyingType(pt); if (nt != null && nt.IsInstanceOfType(a)) { call[i] = a; continue; }
                if (pt.IsEnum) { try { call[i] = Enum.Parse(pt, a.ToString(), true); continue; } catch (Exception) { return false; } }
                if (pt == typeof(string)) { call[i] = a.ToString(); continue; }
                if (pt == typeof(float) && a is IConvertible) { call[i] = Convert.ToSingle(a); continue; }
                if (pt == typeof(int) && a is IConvertible) { call[i] = Convert.ToInt32(a); continue; }
                return false;
            }
            try { mi.Invoke(null, call); return true; }
            catch (Exception e) { if (_warned.Add(name)) Debug.LogWarning($"[BL23 violence] PhysicalSounds.{name} failed: {e.InnerException?.Message ?? e.Message}"); return false; }
        }

        public static void Gun(string kind, Vector3 pos)
        {
            if (Muted || Try("Gun", kind, pos)) return;
            if (kind == "Crossbow") Audio.Sfx.PlayEx("blade_whoosh", pos, 0.5f, 0.6f);
            else { Audio.Sfx.PlayEx("wood_crack", pos, 1f, 0.45f); Audio.Sfx.PlayEx("door_close", pos, 1f, 0.55f); }
        }
        public static void Reload(string kind, Vector3 pos) { if (Muted || Try("Gun", kind + "Reload", pos)) return; Audio.Sfx.PlayEx("metal_clang", pos, 0.22f, kind == "Crossbow" ? 0.7f : 1.5f); }
        public static void Struggle(string kind, float intensity, Vector3 pos)
        {
            if (Muted || Try("Struggle", kind, intensity, pos)) return;
            if (kind == "Drown") Audio.Sfx.Play("water_splash", pos, 0.25f + 0.55f * intensity);
            else if (kind == "Drag") Audio.Sfx.PlayEx("chair_scrape", pos, 0.16f + 0.1f * intensity, 0.65f);
            else Audio.Sfx.PlayEx("slap", pos, 0.12f + 0.25f * intensity, 0.7f);
        }
        public static void Vocal(string actor, string kind, Vector3 pos)
        {
            if (Muted || Try("Vocal", actor, kind, pos)) return;
            if (kind == "Choke" || kind == "Gasp" || kind == "Gurgle" || kind == "Pain") Audio.Sfx.PlayEx("gasp", pos, 0.35f, kind == "Gurgle" ? 0.7f : 0.9f);
        }
        public static void Impact(BL23.Sim.DamageType t, BL23.Sim.BodyRegion r, Vector3 pos)
        {
            if (Muted || Try("Impact", t, r, pos)) return;
            Audio.Sfx.Play(t == BL23.Sim.DamageType.Stab ? "stab" : t == BL23.Sim.DamageType.Cut ? "slash" : "blunt_hit", pos, 0.7f);
        }
        public static void Fall(string surface, float energy, Vector3 pos) { if (Muted || Try("Fall", surface, energy, pos)) return; Audio.Sfx.Play("body_fall", pos, Mathf.Clamp(0.3f + energy * 0.002f, 0.3f, 0.9f)); }
        public static void Break(BL23.Sim.Mat m, Vector3 pos)
        {
            if (Muted || Try("Break", m, pos)) return;
            Audio.Sfx.Play(m == BL23.Sim.Mat.Glass ? "glass_shatter" : m == BL23.Sim.Mat.Ceramic ? "ceramic_break" : "wood_crack", pos, 0.8f);
        }
    }
}
