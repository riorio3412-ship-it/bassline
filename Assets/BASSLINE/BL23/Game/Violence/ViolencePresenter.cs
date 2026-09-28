using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Characters;
using BL23.Game.Mansion;
using BL23.Game.Physicality;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// Presents the kernel's violence state (Sim/Violence) every frame: paired prolonged kills driven by the assault's
    /// struggle intensity (the character track's BeginPaired when installed, else IK reaches, pulses, faces and gestures from
    /// what main already has), bound bodies (rope rings, a gag, the restraint pose), bodies dragged instead of carried, shots
    /// (muzzle flash, smoke, recoil, impacts, blood), the house flinching at a gunshot, furniture knocked over for real, and the
    /// victim's defensive reaction to an NPC's blow. The kernel decides everything; nothing here changes an outcome.
    /// </summary>
    public sealed class ViolencePresenter : MonoBehaviour
    {
        public static ViolencePresenter I;
        WorldPresenter W; GameState S => Session.I?.S;
        public static void Attach(WorldPresenter w)
        {
            if (w == null) return;
            var p = w.GetComponent<ViolencePresenter>() ?? w.gameObject.AddComponent<ViolencePresenter>(); p.W = w; I = p;
        }

        sealed class Pair
        {
            public string Id, A, V; public AssaultKind Kind; public bool Paired; public AssaultPhase Seen = (AssaultPhase)(-1);
            public float NextPulse, NextSound, NextVocal, NextReach, NextSplash, NextAct, Flush, Pale; public int Beat;
        }
        readonly Dictionary<string, Pair> _pairs = new Dictionary<string, Pair>();
        readonly Dictionary<string, List<Transform>> _binds = new Dictionary<string, List<Transform>>();
        readonly Dictionary<string, bool> _dragShown = new Dictionary<string, bool>();
        readonly Dictionary<string, float> _dragSound = new Dictionary<string, float>();
        readonly HashSet<string> _ready = new HashSet<string>();   // NPCs in a firearm aim stance (off when their attack step ends)
        static bool Muted => Session.I?.TimeDir != null && Session.I.TimeDir.MuteWorldAudio;

        void Update()
        {
            var s = S; if (s?.Violence == null || W == null) return;
            try
            {
                foreach (var x in s.Violence.Assaults) if (x.Active || _pairs.ContainsKey(x.Id)) TickPair(x);
                TickBindings(s);
                TickDrags(s);
                TickReady(s);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ================================================================== prolonged kills
        static string PairedName(AssaultKind k) => k == AssaultKind.StrangleRear ? "GarroteRear" : k == AssaultKind.StrangleFront ? "LigatureFront" : k == AssaultKind.StrangleManual ? "Strangle" : k.ToString();
        /// <summary>The published paired pose's surface height: the water line for a drowning, the mattress for a smothering in bed.</summary>
        float PairedSurface(Assault x)
        {
            if (x.Kind == AssaultKind.Drown)
                switch (x.WaterType) { case "Bathtub": return 0.55f; case "Sink": case "Basin": return 0.85f; case "Fountain": return 0.5f; case "Well": return 0.8f; case "ShallowWater": return 0.1f; default: return 0f; }
            if (x.Kind == AssaultKind.Smother)
            {
                var v = S.A(x.Victim); if (v == null) return 0f;
                foreach (var fi in S.Layout.Furniture) if (fi.Pos.f == v.Pos.f && fi.Type != null && fi.Type.Contains("Bed") && fi.Pos.DistXZ(v.Pos) < 1.3f) return 0.55f;
            }
            return 0f;
        }
        void TickReady(GameState s)
        {
            if (_ready.Count == 0) return;
            foreach (var id in new List<string>(_ready))
            {
                var a = s.A(id);
                if (a != null && a.Alive && a.Status == ActorStatus.Active && a.Act?.Cur?.Kind == "Attack") continue;
                var v = W.ViewOf(id); if (v?.Rig?.Anim != null) { CharBridge.SetCombatReady(v.Rig.Anim, false, "None"); CharBridge.SetAim(v.Rig.Anim, null); }
                _ready.Remove(id);
            }
        }
        static string SoundKindOf(AssaultKind k) => k == AssaultKind.Drown ? "Drown" : k == AssaultKind.Smother ? "Smother" : k == AssaultKind.StrangleManual ? "Strangle" : "Garrote";

        void TickPair(Assault x)
        {
            var av = W.ViewOf(x.Attacker); var vv = W.ViewOf(x.Victim);
            var an = av?.Rig?.Anim; var vn = vv?.Rig?.Anim;
            if (an == null || vn == null) { if (!x.Active) _pairs.Remove(x.Id); return; }
            float now = Time.time;
            if (!_pairs.TryGetValue(x.Id, out var p))
            {
                p = new Pair { Id = x.Id, A = x.Attacker, V = x.Victim, Kind = x.Kind };
                _pairs[x.Id] = p;
                p.Paired = CharBridge.BeginPaired(an, vn, PairedName(x.Kind), x.Intensity);
                if (p.Paired) { float surf = PairedSurface(x); if (surf > 0f) CharBridge.SetPairedSurface(an, surf); }
                if (!p.Paired) { an.PlayAction(ActionAnim.Strangle, 999f); vn.PlayAction(ActionAnim.Struggle, 0.8f); }
                if (!Muted) PhysSfx.Struggle(SoundKindOf(x.Kind), 1f, vv.HeadPos);
                av.Rig.SetExpression(Expr.Angry, 0.8f);
                if (x.Kind == AssaultKind.Drown && !Muted) { var w = WaterPoint(x, vv); ViolenceFx.Splash(w, 0.9f); }
            }
            if (!x.Active) { End(p, x, av, vv); _pairs.Remove(x.Id); return; }
            if (p.Seen != x.Phase) { p.Seen = x.Phase; OnPhase(p, x, av, vv); }
            if (PhysicalCharacter.WorldPaused) return;
            float s = x.Intensity;
            // --- the bodies
            if (p.Paired) CharBridge.SetPairedIntensity(an, s);
            else Fallback(p, x, av, vv, s, now);
            // --- the face: gasping, flushed, then pale and slack
            if (x.Phase == AssaultPhase.Struggle || x.Phase == AssaultPhase.Seize) { p.Flush = Mathf.MoveTowards(p.Flush, 0.9f, Time.deltaTime * 0.08f); p.Pale = 0f; }
            else if (x.Phase == AssaultPhase.Weaken) { p.Flush = Mathf.MoveTowards(p.Flush, 0.35f, Time.deltaTime * 0.12f); p.Pale = Mathf.MoveTowards(p.Pale, 0.6f, Time.deltaTime * 0.1f); }
            else { p.Flush = Mathf.MoveTowards(p.Flush, 0f, Time.deltaTime * 0.1f); p.Pale = Mathf.MoveTowards(p.Pale, 0.95f, Time.deltaTime * 0.1f); }
            if (x.Phase != AssaultPhase.Unconscious) CharBridge.SetStrain(vv.Rig, x.Phase == AssaultPhase.Weaken ? 0.4f + 0.4f * s : 0.6f + 0.4f * s, p.Flush, p.Pale);
            else CharBridge.SetStrain(vv.Rig, 0f, 0f, p.Pale);
            // --- sounds and water
            if (!Muted && now >= p.NextSound && x.Phase != AssaultPhase.Unconscious)
            {
                p.NextSound = now + Mathf.Lerp(1.8f, 0.7f, s) * UnityEngine.Random.Range(0.8f, 1.2f);
                PhysSfx.Struggle(SoundKindOf(x.Kind), s, vv.HeadPos);
            }
            if (!Muted && now >= p.NextVocal && x.Phase != AssaultPhase.Unconscious && !x.Sedated)
            {
                p.NextVocal = now + UnityEngine.Random.Range(1.8f, 3.4f);
                PhysSfx.Vocal(x.Victim, x.Kind == AssaultKind.Drown ? "Gurgle" : x.Phase == AssaultPhase.Weaken ? "Gasp" : "Choke", vv.HeadPos);
            }
            if (x.Kind == AssaultKind.Drown && now >= p.NextSplash)
            {
                var w = WaterPoint(x, vv);
                if (x.Phase == AssaultPhase.Unconscious) { p.NextSplash = now + 1.4f; ViolenceFx.Bubbles(w, 3); }
                else { p.NextSplash = now + Mathf.Lerp(1.1f, 0.3f, s); ViolenceFx.Splash(w, s); ViolenceFx.Bubbles(w, Mathf.RoundToInt(4 + 8 * s)); }
            }
        }

        /// <summary>Where the head meets the water: the pool / shallow water surface at floor level, a basin at counter height.</summary>
        Vector3 WaterPoint(Assault x, ActorView vv)
        {
            var head = vv.HeadPos; var f = x.Water >= 0 && x.Water < S.Layout.Furniture.Count ? S.Layout.Furniture[x.Water] : null;
            float floorY = W.ToWorld(S.A(x.Victim).Pos).y;
            bool basin = x.WaterType == "Sink" || x.WaterType == "Basin";
            var fwd = vv.transform.forward;
            var p = head + fwd * 0.35f;
            p.y = basin ? floorY + 0.84f : x.WaterType == "ShallowWater" ? floorY + 0.12f : floorY - 0.04f;
            if (f != null && basin) { var c = W.ToWorld(f.Pos); p = new Vector3(Mathf.Lerp(p.x, c.x, 0.5f), p.y, Mathf.Lerp(p.z, c.z, 0.5f)); }
            return p;
        }

        void OnPhase(Pair p, Assault x, ActorView av, ActorView vv)
        {
            var an = av.Rig.Anim; var vn = vv.Rig.Anim;
            switch (x.Phase)
            {
                case AssaultPhase.Struggle: CharBridge.Face(vv.Rig, "Choke", Expr.Pain, 1f); break;
                case AssaultPhase.Weaken:
                    CharBridge.Face(vv.Rig, "Choke", Expr.Pain, 0.7f);
                    if (!p.Paired) { vn.PlayAction(ActionAnim.Hurt, 1.4f); }
                    break;
                case AssaultPhase.Unconscious:
                    vv.Rig.SetExpression(Expr.Blank, 1f);
                    if (!p.Paired) an.PlayAction(ActionAnim.Strangle, 999f);   // the hold goes on over the limp body
                    break;
            }
        }

        /// <summary>Without the paired motion: the attacker's hands reach the throat (or the head / the pillow) through the
        /// contact IK, the victim claws at the attacker's hands, kicks and twists in bursts that follow the struggle.</summary>
        void Fallback(Pair p, Assault x, ActorView av, ActorView vv, float s, float now)
        {
            var an = av.Rig.Anim; var vn = vv.Rig.Anim;
            if (now >= p.NextAct) { p.NextAct = now + 1f; if (an.CurrentAction != ActionAnim.Strangle) an.PlayAction(ActionAnim.Strangle, 999f); }
            var acts = av.Physical?.Actions;
            if (acts != null && now >= p.NextReach)
            {
                p.NextReach = now + 0.55f;
                Transform target = x.Kind == AssaultKind.Drown || x.Kind == AssaultKind.Smother ? vv.Rig.Bone(HBone.Head) : vv.Rig.Bone(HBone.Neck);
                if (target != null) acts.Reach(target.position, p.Beat % 2 == 0, 0.8f);
            }
            if (x.Phase == AssaultPhase.Unconscious) return;
            if (now >= p.NextPulse)
            {
                p.NextPulse = now + Mathf.Lerp(1.3f, 0.45f, s); p.Beat++;
                var vacts = vv.Physical?.Actions;
                if (x.Phase == AssaultPhase.Weaken) { if (p.Beat % 3 == 0) vn.PlayAction(ActionAnim.Hurt, 0.9f); return; }
                switch (p.Beat % 4)
                {
                    case 0: vn.PlayAction(ActionAnim.Struggle, 0.7f); break;
                    case 1: if (vacts != null && av.Rig.HandAnchorR != null) vacts.Reach(av.Rig.HandAnchorR.position, false, 0.45f); else vn.PlayGesture(Characters.Gesture.Flinch, 0.5f); break;
                    case 2: vn.PlayGesture(s > 0.7f ? Characters.Gesture.Cower : Characters.Gesture.Flinch, 0.6f); break;
                    case 3: if (vacts != null && av.Rig.HandAnchorL != null) vacts.Reach(av.Rig.HandAnchorL.position, true, 0.45f); else vn.PlayAction(ActionAnim.Struggle, 0.6f); break;
                }
            }
        }

        void End(Pair p, Assault x, ActorView av, ActorView vv)
        {
            var an = av?.Rig?.Anim; var vn = vv?.Rig?.Anim;
            if (p.Paired) { CharBridge.EndPaired(an); CharBridge.EndPaired(vn); }
            if (an != null && an.CurrentAction == ActionAnim.Strangle) an.PlayAction(ActionAnim.None, 0f);
            av?.Physical?.Actions?.Cancel(); vv?.Physical?.Actions?.Cancel();
            if (vv?.Rig != null) CharBridge.SetStrain(vv.Rig, 0f, 0f, x.Phase == AssaultPhase.Dead ? 1f : 0f);
            if (x.Phase == AssaultPhase.Escaped || x.Phase == AssaultPhase.Released)
            {
                var v = S.A(x.Victim);
                if (v != null && v.Alive && v.Status == ActorStatus.Active && vv != null)
                {
                    // wrenched free: a stagger away from the attacker, gasping
                    var away = vv.transform.position - av.transform.position; away.y = 0f; if (away.sqrMagnitude < 1e-4f) away = -vv.transform.forward;
                    vv.Physical?.Shove(away.normalized, 70f, vv.transform.position + Vector3.up * 1.1f);
                    CharBridge.PlayStagger(vn, away.normalized, 0.8f);
                    CharBridge.Gesture(vn, "Recoil", Characters.Gesture.Flinch, 0.8f);
                    CharBridge.Face(vv.Rig, "Panic", Expr.Fear, 1f);
                    if (!Muted) PhysSfx.Vocal(x.Victim, "Gasp", vv.HeadPos);
                }
                if (av?.Rig != null && x.Outcome != null && x.Outcome.Contains("떼어")) av.Physical?.Shove((av.transform.position - vv.transform.position).normalized, 90f, av.transform.position + Vector3.up * 1.2f);
            }
        }

        // ================================================================== bound bodies
        void TickBindings(GameState s)
        {
            // new / removed bindings
            foreach (var b in s.Violence.Bindings)
            {
                bool on = !b.Off && S.A(b.Actor) != null;
                bool shown = _binds.ContainsKey(b.Actor);
                if (on && !shown) ShowBinding(b);
                else if (!on && shown && Violence.Bound(s, b.Actor) == null) HideBinding(b.Actor);
            }
            foreach (var id in _binds.Keys.ToList()) if (Violence.Bound(s, id) == null) HideBinding(id);
            // straining
            foreach (var kv in _binds)
            {
                var a = s.A(kv.Key); var v = W.ViewOf(kv.Key); if (a == null || v?.Rig?.Anim == null) continue;
                CharBridge.SetRestraintStruggle(v.Rig.Anim, a.Alive && a.Status == ActorStatus.Active && a.Anim == Anim.Struggle ? 0.55f + 0.35f * Mathf.PerlinNoise(Time.time * 1.7f, kv.Key.Length) : 0f);
            }
        }

        void ShowBinding(Binding b)
        {
            var v = W.ViewOf(b.Actor); if (v?.Rig == null) return;
            var rig = v.Rig; var list = new List<Transform>(); var c = ViolenceFx.BindColor(b.Material); float sc = rig.Height > 0.5f ? rig.Height / 1.75f : 1f;
            if (b.Wrists)
            {
                list.Add(ViolenceFx.BindRing(rig.Bone(HBone.HandL), rig.Bone(HBone.LowerArmL), 0.042f * sc, 0.009f, c, Vector3.zero));
                list.Add(ViolenceFx.BindRing(rig.Bone(HBone.HandR), rig.Bone(HBone.LowerArmR), 0.042f * sc, 0.009f, c, Vector3.zero));
            }
            if (b.Ankles)
            {
                list.Add(ViolenceFx.BindRing(rig.Bone(HBone.FootL), rig.Bone(HBone.LowerLegL), 0.055f * sc, 0.01f, c, Vector3.zero));
                list.Add(ViolenceFx.BindRing(rig.Bone(HBone.FootR), rig.Bone(HBone.LowerLegR), 0.055f * sc, 0.01f, c, Vector3.zero));
            }
            if (b.Gag && rig.HasBone(HBone.Head))
            {
                var head = rig.Bone(HBone.Head); var g = ViolenceFx.BindRing(head, rig.Bone(HBone.Neck), 0.092f * sc, 0.012f, b.Material == "Tape" ? c : new Color(0.85f, 0.82f, 0.76f), Vector3.zero);
                if (g != null) { var al = g.GetComponent<BindAlign>(); if (al != null) al.enabled = false; g.gameObject.AddComponent<GagFollow>().Head = head; }
                list.Add(g);
            }
            _binds[b.Actor] = list.Where(t => t != null).ToList();
            CharBridge.SetRestraint(rig.Anim, b.Wrists, b.Ankles, b.Gag, true);
        }

        void HideBinding(string actor)
        {
            if (_binds.TryGetValue(actor, out var l)) foreach (var t in l) if (t != null) Destroy(t.gameObject);
            _binds.Remove(actor);
            var v = W.ViewOf(actor); if (v?.Rig?.Anim != null) CharBridge.SetRestraint(v.Rig.Anim, false, false, false);
        }

        // ================================================================== carried or dragged
        void TickDrags(GameState s)
        {
            foreach (var a in s.Actors.Values)
            {
                if (a.Carrying == null) { if (_dragShown.ContainsKey(a.Id)) { var v0 = W.ViewOf(a.Id); if (v0?.Rig?.Anim != null) CharBridge.SetDragging(v0.Rig.Anim, null); _dragShown.Remove(a.Id); } continue; }
                bool drag = Violence.Dragging(s, a);
                var v = W.ViewOf(a.Id); var body = W.ViewOf(a.Carrying); if (v?.Rig?.Anim == null || body?.Rig == null) continue;
                if (!_dragShown.TryGetValue(a.Id, out var shown) || shown != drag)
                {
                    _dragShown[a.Id] = drag;
                    if (drag) { v.Rig.Anim.SetCarrying(null); CharBridge.SetDragging(v.Rig.Anim, body.Rig, "Armpits"); if (a.IsPlayer || a.Anim == Anim.Drag) v.Rig.Anim.PlayAction(ActionAnim.Drag, 999f); }
                    else { CharBridge.SetDragging(v.Rig.Anim, null); v.Rig.Anim.SetCarrying(body.Rig); }
                }
                if (drag && !Muted && v.Velocity.magnitude > 0.15f)
                {
                    _dragSound.TryGetValue(a.Id, out var next);
                    if (Time.time >= next) { _dragSound[a.Id] = Time.time + 0.65f; PhysSfx.Struggle("Drag", Mathf.Clamp01(v.Velocity.magnitude), body.transform.position); }
                }
            }
        }

        /// <summary>ActorView asks: is this carried body dragged along the floor behind its carrier (not on a shoulder)?</summary>
        public static bool DraggedBy(GameState s, Actor body, out Actor carrier)
        {
            carrier = body?.CarriedBy != null ? s.A(body.CarriedBy) : null;
            return carrier != null && Violence.Dragging(s, carrier);
        }

        // ================================================================== kernel events
        public void OnEvent(GameEvent e)
        {
            try
            {
                switch (e.Type)
                {
                    case GameEventType.Anim: if (e.Data != null) OnAnim(e); break;
                    case GameEventType.Sound: if (e.Text == "Gunshot") OnGunshot(e); break;
                    case GameEventType.Furniture: OnFurniture(e); break;
                    case GameEventType.Strike: OnStrike(e); break;
                }
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        void OnAnim(GameEvent e)
        {
            var parts = e.Data.Split('|'); var who = W.ViewOf(e.Actor);
            switch (parts[0])
            {
                case "shot": OnShot(parts.Length > 1 ? parts[1] : null); break;
                case "aim":
                    {
                        var t = W.ViewOf(e.Target); if (who?.Rig?.Anim == null) break;
                        var gun = S.I(parts.Length > 1 ? parts[1] : null);
                        CharBridge.SetHeldWeapon(who.Rig.Anim, CharBridge.WeaponClassOf(gun?.Def), gun?.Def?.Mass ?? 1f);
                        if (CharBridge.SetCombatReady(who.Rig.Anim, true, CharBridge.WeaponClassOf(gun?.Def)) && e.Actor != Cast.Player) _ready.Add(e.Actor);
                        CharBridge.SetAim(who.Rig.Anim, t != null ? t.HeadPos - Vector3.up * 0.3f : (Vector3?)null);
                        if (!CharBridge.Action(who.Rig.Anim, "Aim", ActionAnim.None, 1.6f)) who.Rig.Anim.PlayGesture(Characters.Gesture.Point, 1.6f);
                        if (t?.Rig?.Anim != null && S.A(e.Target)?.Status == ActorStatus.Active) { CharBridge.Gesture(t.Rig.Anim, "HandsUp", Characters.Gesture.Cower, 1.8f); CharBridge.Face(t.Rig, "Panic", Expr.Fear, 1f); }
                        break;
                    }
                case "reload":
                    {
                        if (who?.Rig?.Anim == null) break; var gun = S.I(parts.Length > 1 ? parts[1] : null);
                        bool bow = gun?.Type == "Crossbow";
                        CharBridge.Action(who.Rig.Anim, bow ? "CockCrossbow" : "Reload", ActionAnim.Use, gun != null ? Violence.LoadTicks(gun.Type) / 10f : 2f);
                        if (!Muted) PhysSfx.Reload(gun?.Type ?? "Revolver", who.transform.position + Vector3.up);
                        break;
                    }
                case "dry": if (!Muted && who != null) Audio.Sfx.PlayEx("light_switch", who.transform.position + Vector3.up, 0.5f, 1.4f); break;
                case "bind":
                    {
                        if (who?.Rig?.Anim != null) CharBridge.Action(who.Rig.Anim, "TieUp", ActionAnim.Use, 1.6f);
                        var t = W.ViewOf(e.Target); if (!Muted && t != null) PhysSfx.Struggle("Rope", 0.5f, t.transform.position + Vector3.up * 0.6f);
                        break;
                    }
                case "unbind": if (who?.Rig?.Anim != null && e.Actor != e.Target) CharBridge.Action(who.Rig.Anim, "Untie", ActionAnim.Use, 1.2f); break;
            }
        }

        /// <summary>A shot: flash and smoke at the muzzle, the recoil, the impacts (blood, splinters, dust), glass that breaks.</summary>
        void OnShot(string id)
        {
            var shot = S.Violence.Shots.LastOrDefault(x => x.Id == id); if (shot == null) return;
            var shooter = W.ViewOf(shot.By); bool bolt = shot.WeaponType == "Crossbow";
            float floorY = W.ToWorld(shot.From).y;
            var dir = Quaternion.Euler(-shot.Pitch, shot.Yaw, 0f) * Vector3.forward;
            Vector3 muzzle = shooter != null && !(shot.By == Cast.Player && Session.I.Player != null && Session.I.Player.FirstPerson)
                ? (shooter.Rig?.HandAnchorR != null ? shooter.Rig.HandAnchorR.position : shooter.transform.position + Vector3.up * 1.35f) + dir * (shot.WeaponType == "HuntingShotgun" ? 0.75f : bolt ? 0.45f : 0.22f)
                : FirstPersonHands.MuzzleWorld(dir) ?? new Vector3(shot.From.x, floorY + shot.Y0, shot.From.z) + dir * 0.4f;
            if (!bolt) ViolenceFx.MuzzleFlash(muzzle, dir, shot.WeaponType == "HuntingShotgun");
            if (!Muted) PhysSfx.Gun(shot.WeaponType, muzzle);
            if (shooter?.Rig?.Anim != null && shot.By != Cast.Player)
            {
                if (!CharBridge.PlayAttack(shooter.Rig.Anim, "Shoot", CharBridge.WeaponClassOf(ItemCatalog.Get(shot.WeaponType)), muzzle + dir * 6f)
                    && !CharBridge.Action(shooter.Rig.Anim, "Shoot", ActionAnim.None, 0.45f)) shooter.Rig.Anim.PlayGesture(Characters.Gesture.Flinch, 0.35f);
                shooter.Physical?.Shove(-dir, shot.WeaponType == "HuntingShotgun" ? 45f : 18f, shooter.transform.position + Vector3.up * 1.4f);
            }
            Vector3 last = muzzle + dir * 30f; bool first = true;
            foreach (var imp in shot.Impacts)
            {
                var at = W.ToWorld(imp.At); at.y = W.ToWorld(imp.At).y + imp.Y;
                if (first && !bolt) { ViolenceFx.Tracer(muzzle, at); first = false; }
                last = at;
                switch (imp.Kind)
                {
                    case "body": case "exit":
                        {
                            var v = W.ViewOf(imp.Actor);
                            var p = v?.Rig != null ? (v.Rig.Bone(v.Rig.RegionBone((Characters.BodyRegion)imp.Region))?.position ?? at) : at;
                            ViolenceFx.BloodMist(p, imp.Kind == "exit" ? dir : -dir * 0.4f + dir * 0.6f, imp.Kind == "exit" ? 1f : 0.6f);
                            if (!Muted) PhysSfx.Impact(DamageType.Stab, imp.Region, p);
                            break;
                        }
                    case "wall": case "door": case "floor": case "furniture":
                        {
                            BL23.Sim.Mat mat = imp.Kind == "door" ? BL23.Sim.Mat.Wood : imp.Kind == "floor" ? BL23.Sim.Mat.Stone : BL23.Sim.Mat.Stone;
                            if (imp.Furniture >= 0 && imp.Furniture < S.Layout.Furniture.Count) { var f = S.Layout.Furniture[imp.Furniture]; mat = FurnitureCatalog.Get(f.Type)?.Mat ?? f.Material; PushFurniture(f, at, dir, shot.WeaponType == "HuntingShotgun" ? 60f : bolt ? 25f : 35f, 140f, 0.9f); }
                            Vector3 n = -dir;
                            if (Physics.Raycast(at - dir * 0.6f, dir, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore)) { at = hit.point; n = hit.normal; }
                            ViolenceFx.ImpactPuff(at, n, mat);
                            if (!Muted && mat == BL23.Sim.Mat.Glass) PhysSfx.Break(mat, at);
                            break;
                        }
                }
            }
            // everyone near the shooter sees the flash before they hear it (the house-wide flinch comes with the Sound event)
            if (shot.By == Cast.Player) FirstPersonHands.Kick(shot.WeaponType);
        }

        void OnGunshot(GameEvent e)
        {
            var pos = W.ToWorld(e.Pos) + Vector3.up * 1.3f;
            foreach (var kv in W.Actors)
            {
                var v = kv.Value; if (v == null || kv.Key == e.Actor) continue;
                var a = S.A(kv.Key); if (a == null || !a.Alive || a.Status != ActorStatus.Active) continue;
                float d = Vector3.Distance(v.HeadPos, pos); if (d > 40f) continue;
                bool occluded = Physics.Linecast(v.HeadPos, pos, out var hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<PhysicalCharacter>() == null && hit.collider.GetComponentInParent<ActorRig>() == null;
                float k = Mathf.Clamp01(1f - d / 40f) * (occluded ? 0.55f : 1f);
                v.Physical?.Startle(pos, Mathf.Clamp01(0.35f + 0.65f * k));
                if (v.Rig?.Anim != null) { if (!CharBridge.HearNoise(v.Rig.Anim, pos, k)) v.Rig.Anim.PlayGesture(k > 0.6f ? Characters.Gesture.Cower : Characters.Gesture.Surprised, 1.1f); }
                if (k > 0.5f) v.Rig?.SetExpression(Expr.Fear, 1f);
            }
            if (Session.I.Player != null && S.Player != null && e.Actor != Cast.Player)
            {
                float d = Vector3.Distance(Session.I.Player.FeetPosition, pos);
                if (d < 20f) Session.I.Player.Nudge(UnityEngine.Random.onUnitSphere * 0.03f * (1f - d / 20f), 0.18f);
            }
        }

        void OnFurniture(GameEvent e)
        {
            if (e.Id < 0 || e.Id >= S.Layout.Furniture.Count) return;
            var f = S.Layout.Furniture[e.Id];
            if (e.Text == "knocked")
            {
                // a kick sends it over for real: an impulse away from the struggle at two-thirds of its height
                float yaw = e.Value; var dir = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                var c = W.ToWorld(f.Pos) + Vector3.up * Mathf.Max(0.2f, f.H * 0.66f);
                var def = FurnitureCatalog.Get(f.Type); float mass = def?.Mass ?? 8f;
                PushFurniture(f, c, dir, Mathf.Clamp(mass * 3.2f, 12f, 140f), 45f, 0f);
                if (!Muted) PhysSfx.Fall("Wood", 60f, c);
            }
        }

        void PushFurniture(Furniture f, Vector3 point, Vector3 dir, float impulse, float energy, float sharpness)
        {
            var go = W.Mansion?.FurnitureObject(f.Id); if (go == null) return;
            var prop = go.GetComponent<PhysicalProp>() ?? PhysicalProp.EnsureFurniture(go, f, FurnitureCatalog.Get(f.Type));
            prop?.ApplyImpact(point, dir.normalized * impulse, energy, sharpness);
        }

        /// <summary>An NPC's blow is coming: the victim's defence (arms up, a grab at the wrist, a twist away); a shove sends the
        /// body stumbling for real (the kernel still decides a fall at an edge).</summary>
        void OnStrike(GameEvent e)
        {
            var av = W.ViewOf(e.Actor); var tv = W.ViewOf(e.Target); if (av == null || tv?.Rig?.Anim == null) return;
            var t = S.A(e.Target); if (t == null || !t.Alive || t.Status != ActorStatus.Active || Violence.HeldIn(S, t.Id) != null) return;
            var dir = tv.transform.position - av.transform.position; dir.y = 0f; if (dir.sqrMagnitude < 1e-4f) dir = tv.transform.forward;
            if (e.Data == "Shove") { tv.Physical?.Shove(dir.normalized, 150f, tv.transform.position + Vector3.up * 1.15f); return; }
            if (e.Data == "Strangle" || e.Data == "Garrote" || e.Data == "Smother" || e.Data == "Drown") return;
            bool facing = Vector3.Angle(tv.transform.forward, -dir) < 80f;
            if (!facing) return;
            string[] defs = { "Defend", "GrabWrist", "TurnAway", "Dodge" };
            string pick = defs[Mathf.Abs((e.Actor + e.Target).GetHashCode() + Time.frameCount) % defs.Length];
            if (!CharBridge.PlayDefense(tv.Rig.Anim, pick, av.transform.position)) tv.Rig.Anim.PlayGesture(Characters.Gesture.Cower, 0.7f);
        }
    }

    /// <summary>A gag rides round the lower face (the head bone's axes differ between rigs: kept level, round the mouth).</summary>
    public sealed class GagFollow : MonoBehaviour
    {
        public Transform Head;
        void LateUpdate()
        {
            if (Head == null) return;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, Head.up);
            transform.position = Head.position + Head.up * 0.02f + Head.forward * 0.015f;
        }
    }
}
