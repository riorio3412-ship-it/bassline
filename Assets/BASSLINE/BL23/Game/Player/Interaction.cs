using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Audio;
using BL23.Game.Characters;
using BL23.Game.Physicality;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>What the player is looking at, and what E / R / G / B / L / K / Q / mouse do with it.</summary>
    public sealed class Interaction : MonoBehaviour
    {
        Session _s; PlayerController _p; GameState S => _s.S; Simulation Sim => _s.Sim;
        public string TargetKind; public string TargetLabel; public string Hint;
        Actor _actor; Item _item; Door _door; Furniture _furn; Trace _trace;
        float _examHold; float _throwHold; float _attackCd;
        PhysicalMelee _melee;
        static readonly HashSet<string> UsableFurniture = new HashSet<string> { "Switchboard", "PressConsole", "Sink", "Washer", "Bed", "Terminal", "Clock", "ClockCase", "Press", "Piano", "Piano_Upright", "MedCabinet", "Altar", "Fireplace", "Aquarium", "Dollhouse", "FileCabinet", "Recorder", "Speaker", "TicketBooth" };
        // --- time-on-demand (begin): what the view rests on, and what E / X do with a piece of furniture
        Furniture _rayFurn; Vector3 _aimPoint; float _rayDist; FurnPlan _plan;
        string _lastUseKey; float _lastUseAt = -9f;
        /// <summary>The person under the crosshair (a still Yusti nods when looked at).</summary>
        public string LookedActor => _actor != null ? _actor.Id : null;
        // --- time-on-demand (end)

        public void Init(Session s, PlayerController p) { _s = s; _p = p; }

        public void Tick()
        {
            Find(); Looked();
            if (TargetKind == "actor" && _actor != null && Hint != null) Hint += ViolenceHint(_actor);   // --- violence track: V 묶기 / 풀어 주기
            if (_s.World.ViewOf(Cast.Player)?.Physical?.BlocksActions == true) return;
            SeatedHint();   // --- time-on-demand: seated — the seat (or any seat) in view says how to get up, never "E 앉기"
            var me = S.Player;
            _attackCd -= Time.deltaTime;
            // --- primary
            if (Input.GetKeyDown(KeyCode.E) && Time.frameCount != CaseReport.ClosedFrame && Time.frameCount != DialogueUI.ClosedFrame) Primary();
            if (Input.GetKeyDown(KeyCode.X)) Secondary();
            // --- examine (hold R)
            if (Input.GetKey(KeyCode.R) && Examinable() && !(Input.GetMouseButton(1) && Sim.Held(me, d => BL23.Sim.Violence.IsRanged(d)) != null)) { _examHold += Time.deltaTime; Hud.I?.Progress(_examHold / ExamTime()); if (_examHold >= ExamTime()) { _examHold = 0; Examine(); } }
            else { if (_examHold > 0) Hud.I?.Progress(0); _examHold = 0; }
            // --- drop / throw
            if (Input.GetKey(KeyCode.G)) _throwHold += Time.deltaTime;
            if (Input.GetKeyUp(KeyCode.G)) { DropOrThrow(_throwHold > 0.35f); _throwHold = 0; }
            if (Input.GetKeyDown(KeyCode.B)) { Sim.PlayerReport(); Sfx.Play("hand_bell", transform.position, 0.8f); Hud.I?.Toast("종을 울렸다", Pal.Gold); }
            if (Input.GetKeyDown(KeyCode.L) && _door != null) Motion(ActionAnim.Use, 0.5f);
            if (Input.GetKeyDown(KeyCode.K) && _door != null) Motion(ActionAnim.Knock, 1.0f);
            if (Input.GetKeyDown(KeyCode.L) && _door != null) { var d = _door; string r = Sim.PlayerDoor(d, d.Locked ? "unlock" : "lock"); Hud.I?.Toast(r ?? (d.Locked ? "문을 잠갔다" : "자물쇠를 풀었다"), r == null ? Pal.Cyan : Pal.TextDim, 1.4f); }
            if (Input.GetKeyDown(KeyCode.K) && _door != null) Sim.PlayerDoor(_door, "knock");
            if (Input.GetKeyDown(KeyCode.Q) && _actor != null && _actor.Alive && _actor.Status == ActorStatus.Unconscious) { bool ok = Sim.PlayerFirstAid(_actor); Hud.I?.Toast(ok ? "응급처치를 했다" : "구급상자가 있어야 한다. 아니면 구급실로 옮기자", ok ? Pal.Good : Pal.TextDim); }
            // --- concealment (Game/Player/Concealer.cs): Q hides what is in hand / draws it back out (hold: the wheel) — unless Q is first aid here
            Conceal.TickKeys(_actor != null && _actor.Alive && _actor.Status == ActorStatus.Unconscious);
            if (Conceal.WheelOpen) { TargetLabel = null; Hint = null; }   // the wheel owns the middle of the screen
            if (Input.GetKeyDown(KeyCode.T)) _s.Menu.OpenWait();
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.N)) _s.Note.Toggle();
            if (Input.GetKeyDown(KeyCode.M)) _s.Note.OpenTab("map");
            if (Input.GetKeyDown(KeyCode.J)) _s.Note.OpenTab("goals");
            if (Input.GetKeyDown(KeyCode.I)) _s.Note.OpenTab("inventory");
            if (Input.GetKeyDown(KeyCode.Escape) && Time.frameCount != MenuUI.ClosedFrame && Time.frameCount != BacklogUI.ClosedFrame && Time.frameCount != CaseReport.ClosedFrame && Time.frameCount != DialogueUI.ClosedFrame) _s.Menu.OpenPause();
            // --- violence track (Game/Violence, Sim/Violence): guns and the crossbow, a hold on someone, tying up / untying
            FirstPersonHands.Ensure(_p);
            if (ViolenceInput(me)) return;
            // --- attack: hold right mouse to raise, left mouse to strike (same contact rules as NPCs)
            bool aiming = Input.GetMouseButton(1) && Sim.Held(me, d => d != null && d.IsWeapon) != null;
            Hud.I?.Aim(aiming);
            if (aiming && Input.GetMouseButtonDown(0) && _attackCd <= 0 && !(_p.Grab?.Holding ?? false)) { _attackCd = 0.7f; Swing(); }
            if (!aiming && Input.GetMouseButton(1) && Input.GetMouseButtonDown(0) && _attackCd <= 0 && !(_p.Grab?.Holding ?? false))
            {
                _attackCd = .7f;
                var target = _actor != null ? _s.World.ViewOf(_actor.Id) : null;
                if (target != null && Vector3.Distance(_p.FeetPosition, target.transform.position) < 1.5f)
                {
                    var direction = _p.Cam.transform.forward;
                    target.Physical?.Shove(direction, 90, target.transform.position + Vector3.up * 1.15f);
                    _s.World.ViewOf(Cast.Player)?.Rig?.Anim?.PlayAction(ActionAnim.Shove, .5f);
                    FirstPersonHands.PlayShove();   // --- violence track: both hands out; and a shove breaks someone's hold on a victim
                    if (BL23.Sim.Violence.DoingTo(S, _actor.Id) != null) { Sim.ViolenceBreak(_actor.Id, Cast.Player); Hud.I?.Toast(LineBank.FixParticles(Cast.GivenOf(_actor.Id) + "을(를) 떼어 냈다"), Pal.Gold, 1.6f); }
                }
            }
        }

        float ExamTime() => _actor != null ? 2.2f : _trace != null ? 1.3f : 0.8f;
        bool Examinable() => _actor != null && (_actor.Status != ActorStatus.Active) || _item != null || _door != null || _furn != null || _trace != null;
        /// <summary>Aiming at something R can examine (the 도움 친절 crosshair ring).</summary>
        public bool CanExamine => Examinable();

        // things already examined say so ("· 살펴봄"), so nothing is looked at twice by accident (per chapter)
        readonly HashSet<string> _looked = new HashSet<string>();   // per session (a new game or a load starts clean)
        string LookKey() => _actor != null && _actor.Status != ActorStatus.Active ? "body:" + _actor.Id : _item != null ? "item:" + _item.Id : _trace != null ? "trace:" + _trace.Id : _door != null ? "door:" + _door.Id : _furn != null ? "furn:" + _furn.Id : null;
        void Looked()
        {
            var key = LookKey(); if (key == null || TargetLabel == null) return;
            if (S.K(Cast.Player).Examined.Contains(key) || _looked.Contains($"{S.Loop}:{S.Chapter}:{key}")) TargetLabel += " <size=70%><color=#9A8E7C>· 살펴봄</color></size>";
        }

        void Find()
        {
            _actor = null; _item = null; _door = null; _furn = null; _trace = null; TargetKind = null; TargetLabel = null; Hint = null;
            var cam = _p.Cam; var me = S.Player; if (cam == null || me == null) return;
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            float best = 3.2f;
            // actors (by distance to the view ray; colliders may be hitboxes)
            foreach (var a in S.Actors.Values)
            {
                if (a.IsPlayer || a.Status == ActorStatus.Executed || a.Status == ActorStatus.Escaped || a.Pos.f != me.Pos.f) continue;
                var v = _s.World.ViewOf(a.Id); if (v == null) continue;
                Vector3 c = a.Alive && a.Status == ActorStatus.Active ? v.transform.position + Vector3.up * 1.1f : v.transform.position + Vector3.up * 0.25f;
                float along = Vector3.Dot(c - ray.origin, ray.direction); if (along < 0.2f || along > best) continue;
                float off = Vector3.Cross(ray.direction, c - ray.origin).magnitude;
                if (off < (a.Status == ActorStatus.Active ? 0.45f : 0.8f) && Visible(ray.origin, c)) { best = along; _actor = a; }
            }
            // items by collider
            // --- time-on-demand (begin): also the furniture the view rests on (and the point on it); a thing lying in an open
            // drawer or behind open doors wins even when the cabinet's box is hit first
            _rayFurn = null; _plan = null; _aimPoint = ray.origin + ray.direction * 1.5f; _rayDist = 99f;
            if (Physics.Raycast(ray, out var hit, 2.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                var tag = hit.collider.GetComponentInParent<ItemTag>();
                if (tag != null) { var it = S.I(tag.ItemId); if (it != null && it.Holder == null && hit.distance < best) { _item = it; _actor = null; best = hit.distance; } }
                var fv = tag == null ? hit.collider.GetComponentInParent<BL23.Game.Mansion.FurnitureView>() : null;
                if (fv != null && fv.Id >= 0 && fv.Id < S.Layout.Furniture.Count)
                {
                    _rayFurn = S.Layout.Furniture[fv.Id]; _aimPoint = hit.point; _rayDist = hit.distance;
                    if (_actor == null || hit.distance < best) { var inside = SlottedNear(_rayFurn, ray, hit.point); if (inside != null) { _item = inside; _actor = null; best = hit.distance; } }
                }
            }
            // --- time-on-demand (end)
            if (_actor != null) { TargetKind = "actor"; var doing = _actor.Status == ActorStatus.Active ? IntentLines.Doing(S, _actor) : null;
                if (doing != null && TimeLink.WorldStill && _actor.Act?.Cur != null && _actor.Act.Cur.Kind == "GoTo") doing = doing.Replace("가는 중", "가려던 참");   /* --- time-on-demand: stopped mid-way */
                if (doing != null && TimeLink.WorldStill) { var un = FrozenLife.UnplacedLabel(S, _actor); if (un != null) doing = un; }   /* --- time-on-demand: "playing" beside the piano, not at it */
                TargetLabel =_actor.Alive ? Cast.NameOf(_actor.Id) + (doing != null ? $"\n<size=64%><color=#B8AC98>{doing}</color></size>" : "") : Cast.NameOf(_actor.Id) + " (움직이지 않는다)"; Hint = _actor.Status == ActorStatus.Active ? (!Sim.CanTalk(_actor, out var busy) ? $"<color=#9A8E7C>{busy}</color>" : Sim.BondAvailable(_actor) != null ? "E 말 걸기 · <color=#E8C170>◆ 할 이야기가 있어 보인다</color>" : "E 말 걸기") : "R 살펴보기 · E 둘러메기 · Q 응급처치"; return; }
            if (_item != null) { TargetKind = "item"; TargetLabel = _item.Kor; Hint = InContainer(_item) ? "E 줍기 · R 살펴보기" : "E 줍기 · Z 들기·던지기 · R 살펴보기"; return; }   // (time-on-demand: a thing in a drawer is not flung about)
            // --- time-on-demand (begin): the piece of furniture the view rests on comes before a door further along
            if (_rayFurn != null && _rayDist < 2.6f && Targetable(_rayFurn)) _furn = _rayFurn;
            // --- time-on-demand (end)
            // doors by proximity in view
            if (_furn == null) foreach (var d in S.Layout.Doors)
            {
                if (d.Pos.f != me.Pos.f) continue; var dp = _s.World.ToWorld(d.Pos) + Vector3.up * 1.1f;
                float along = Vector3.Dot(dp - ray.origin, ray.direction); if (along < 0.2f || along > 2.4f) continue;
                if (Vector3.Cross(ray.direction, dp - ray.origin).magnitude > d.Width * 0.6f) continue;
                _door = d; break;
            }
            // interactable furniture
            if (_door == null && _furn == null)
                foreach (var fid in S.Layout.Room(me.Room)?.Furniture ?? new List<int>())
                {
                    var f = S.Layout.Furniture[fid]; if (!Targetable(f)) continue;   // (time-on-demand: containers and seats too)
                    var fp = _s.World.ToWorld(f.Pos) + Vector3.up * Mathf.Min(1.2f, Mathf.Max(0.5f, f.H * 0.6f));
                    float along = Vector3.Dot(fp - ray.origin, ray.direction); if (along < 0.2f || along > 2.6f + Mathf.Max(f.W, f.D) * 0.4f) continue;
                    if (Vector3.Cross(ray.direction, fp - ray.origin).magnitude > Mathf.Max(0.6f, Mathf.Max(f.W, f.D) * 0.55f)) continue;
                    _furn = f; break;
                }
            // traces (decals) near the aim point
            if (_door == null && _furn == null)
            {
                Vector3 aim = Physics.Raycast(ray, out var h2, 4f, ~0, QueryTriggerInteraction.Ignore) ? h2.point : ray.origin + ray.direction * 2.5f;
                var tr = S.Traces.Where(t => !t.Cleaned && t.Pos.f == me.Pos.f && t.Visibility <= 1 && t.Type != "PowerResidue" && Vector3.Distance(_s.World.ToWorld(t.Pos), aim) < 0.9f).OrderBy(t => Vector3.Distance(_s.World.ToWorld(t.Pos), aim)).FirstOrDefault();
                if (tr != null) { _trace = tr; TargetKind = "trace"; TargetLabel = tr.Desc; Hint = "R 살펴보기"; return; }
            }
            if (_door != null)
            {
                TargetKind = "door"; var ra = S.Layout.Room(_door.RoomA); var rb = S.Layout.Room(_door.RoomB);
                var other = ra?.Id == me.Room ? rb : ra; TargetLabel = (other?.Name ?? "문") + (_door.Locked ? " (잠김)" : "");
                Hint = (_door.Locked ? "E 열어 보기" : _door.Open ? "E 닫기" : "E 열기") + (_door.Lockable ? " · L 잠그기/풀기" : "") + " · K 노크 · R 살펴보기"; return;
            }
            // --- time-on-demand (begin): a thing lying in an open part of the piece in view is what the hand goes for
            if (_furn != null && _rayFurn != _furn)
            {
                var inside = SlottedNear(_furn, ray, ray.origin + ray.direction * Mathf.Clamp(Vector3.Dot(_s.World.ToWorld(_furn.Pos) - ray.origin, ray.direction), 0.3f, 2.6f));
                if (inside != null) { _item = inside; _furn = null; TargetKind = "item"; TargetLabel = _item.Kor; Hint = "E 줍기 · R 살펴보기"; return; }
            }
            // --- time-on-demand (end)
            if (_furn != null) { TargetKind = "furniture"; TargetLabel = FurnitureCatalog.Get(_furn.Type)?.Kor ?? _furn.Type; Hint = FurnHint(_furn); Hint = Conceal.Decorate(_furn, Hint, StashOnX(_furn)) ?? Hint; }   // (concealment: "E 넣어 두기" / "X 넣어 두기")
        }

        // --- concealment (begin) ---------------------------------------------------------------------------------------------
        Concealer _conceal;
        /// <summary>Hiding things on the body and in furniture (Q / E / R at a hiding place).</summary>
        public Concealer Conceal => _conceal != null ? _conceal : (_conceal = Concealer.For(_s, _p, this));
        /// <summary>Containers take things on E (the spec: "E 넣어 두기"); a piece whose E is its own use (sit, sleep, play, read)
        /// keeps it, and the hiding act goes on X.</summary>
        bool StashOnX(Furniture f) => f != null && !TimeLink.IsContainer(f) && (_plan != null && _furn == f ? _plan : PlanFor(f)).E != null;
        /// <summary>E (or X) at a hiding place: put what is in hand there, or take back what 민혁 hid there. True when it was used for it.</summary>
        bool ConcealUse(Furniture f)
        {
            if (f == null || !Concealment.IsPlace(f)) return false;
            if (Sim.Held(S.Player) != null) return Conceal.Stash(f);
            return !ContainerView.Has(f, out _) && Conceal.RetrieveAt(f);
        }
        // --- concealment (end) -----------------------------------------------------------------------------------------------

        // --- time-on-demand (begin) ------------------------------------------------------------------------------------------
        // Simple things are simple and instant: a door, a drawer or a lid opens at once (E the part under the crosshair,
        // X all of it); a seat is sat on at once (any movement key gets up); winding a clock, pouring tea, feeding the fish,
        // putting on a record happen at once, each with its motion. Things that really take a while (reading for half an
        // hour, cooking) say how long in the hint and are spent through the time director.
        bool Targetable(Furniture f) => f != null && (UsableFurniture.Contains(f.Type) || TimeLink.IsContainer(f) || Sim.FurnitureActions(f).Count > 0 || Concealment.IsPlace(f));   // (concealment: any hiding place)

        bool InContainer(Item it) => it != null && _s.World.Items.TryGetValue(it.Id, out var iv) && iv.InContainer;

        const string SeatedLine = "이동 키로 일어나기 · T 시간 보내기";
        /// <summary>Seated: nothing in view, the own seat, or another seat — the prompt is how to get up (the own seat: E too). Another
        /// seat is not sat on from a seat (get up first); people, things within reach and the rest keep their own prompts.</summary>
        void SeatedHint()
        {
            if (!_p.Seated) return;
            if (_furn != null && Sim.FurnitureActions(_furn).Any(a => a.Id == "sit"))
            {
                bool mine = TimeLink.PlayerOnSpot(out var msp) && msp.Furniture == _furn.Id;
                TargetKind = "seated"; TargetLabel = mine ? (FurnitureCatalog.Get(_furn.Type)?.Kor ?? _furn.Type) : null;
                Hint = mine ? "이동 키·E 일어나기" + (_plan?.X != null ? " · X " + _plan.X : "") + " · T 시간 보내기" : SeatedLine;
                if (!mine) { _furn = null; _plan = null; }
                return;
            }
            if (Hint == null) { TargetKind = "seated"; Hint = SeatedLine; }
        }

        /// <summary>A found thing lying in an open part of f, nearest the view ray (null when none).</summary>
        Item SlottedNear(Furniture f, Ray ray, Vector3 hp)
        {
            if (f == null || !ContainerView.Has(f, out var op) || !op.AnyOpen) return null;
            Item best = null; float bd = 0.22f;
            for (int i = 0; i < op.Count; i++)
            {
                if (!op.IsOpen(i)) continue;
                foreach (var s in ContainerView.InPart(f, op, i))
                {
                    var it = S.I(s.Item); if (it == null || it.Holder != null) continue;
                    if (!_s.World.Items.TryGetValue(it.Id, out var iv) || iv.Visual == null || !iv.Reachable) continue;
                    var r = iv.Visual.GetComponentInChildren<Renderer>(); var c = r != null ? r.bounds.center : iv.Visual.transform.position;
                    float along = Vector3.Dot(c - ray.origin, ray.direction); if (along < 0.2f || along > 3.2f || (c - hp).sqrMagnitude > 0.81f) continue;
                    float off = Vector3.Cross(ray.direction, c - ray.origin).magnitude;
                    if (off < bd) { bd = off; best = it; }
                }
            }
            return best;
        }

        /// <summary>What E and X do with a piece of furniture: the words for the hint and the deeds.</summary>
        sealed class FurnPlan { public string E, X, Key; public bool EDim; public Action DoE, DoX; }

        static string Mins(double m) { int n = (int)Math.Round(m); return n >= 60 ? (n % 60 == 0 ? $"{n / 60}시간" : $"{n / 60}시간 {n % 60}분") : $"{n}분"; }
        static string Label(PlayerAction a, string over = null) => (over ?? a.Label) + (TimeLink.IsPastime(a) ? $" ({Mins(a.Minutes)})" : "");
        bool Night() { int m = S.Minute; return m >= 20 * 60 || m < 6 * 60 + 30; }

        FurnPlan PlanFor(Furniture f)
        {
            var p = new FurnPlan { Key = f.Id + ":e" };
            if (KnownTrap(f)) { p.E = "받침 다시 고정하기"; p.DoE = () => { Motion(ActionAnim.Use, 0.8f); var msg = Sim.PlayerOperate(f, 0); if (msg != null) { Hud.I?.Toast(msg, Pal.Gold, 3); Sfx.Play("metal_clang", null, 0.5f); } }; return p; }
            switch (f.Type)
            {
                case "Switchboard": p.E = "배전반 열기"; p.DoE = () => _s.Menu.OpenSwitchboard(f); return p;
                case "PressConsole": p.E = "조작하기"; p.DoE = () => _s.Menu.OpenPressConsole(f); return p;
            }
            var acts = Sim.FurnitureActions(f);
            // containers with doors, drawers or a lid: E the part under the crosshair, X everything (or what else it offers)
            if (ContainerView.Has(f, out var op))
            {
                int part = op.PartNearest(_rayFurn == f ? _aimPoint : AimAt(f)); var pp = part >= 0 ? op.Parts[part] : null;
                p.Key = f.Id + ":p" + part;
                p.E = (pp != null && pp.IsOpen ? "닫기" : "열기") + (op.Count > 1 && pp != null ? $" ({ContainerView.PartName(pp)})" : "");
                p.DoE = () => { if (part >= 0) ContainerView.Toggle(f, op, part); };
                var other = acts.FirstOrDefault(a => a.Id != "search" && a.Id != "sit");
                if (other != null) { p.X = Label(other); p.DoX = () => Do(f, other); }
                else if (op.Count > 1) { bool all = op.Parts.All(q => q.IsOpen); p.X = all ? "전부 닫기" : "전부 열기"; p.DoX = () => ContainerView.ToggleAll(f, op); }
                return p;
            }
            // other containers (a desk, crates, open shelves): rummage through at once
            if (TimeLink.IsContainer(f) && acts.Any(a => a.Id == "search"))
            {
                var room = S.Layout.Room(f.Room); bool theirs = room?.Owner != null && room.Owner != Cast.Player;
                p.E = theirs ? "뒤져 보기 — 남의 방이다" : "뒤져 보기"; p.DoE = () => ContainerView.Rummage(f);
                var other = acts.FirstOrDefault(a => a.Id != "search" && a.Id != "sit"); if (other != null) { p.X = Label(other); p.DoX = () => Do(f, other); }
                return p;
            }
            switch (f.Type)
            {
                case "Sink": p.E = "손 씻기"; p.DoE = () => Operate(f, Pal.Cyan, ActionAnim.Wash); return p;
                case "Washer": { p.E = "씻기"; p.DoE = () => Operate(f, Pal.Cyan, ActionAnim.Wash); var w = acts.FirstOrDefault(a => a.Id == "wash" && TimeLink.IsPastime(a)); if (w != null) { p.X = Label(w); p.DoX = () => Do(f, w); } return p; }
                case "Workbench": { p.E = "조각 맞추기·고치기"; p.DoE = () => Operate(f, Pal.Gold, ActionAnim.Use); var c = acts.FirstOrDefault(a => a.Id == "craft"); if (c != null) { p.X = Label(c, "만들기"); p.DoX = () => Do(f, c); } return p; }
                case "Bed":
                    if (S.Layout.Room(f.Room)?.Owner == Cast.Player)
                    {
                        bool can = Night() && S.Phase == Phase.Daily;
                        p.E = "잠자리에 들기"; p.EDim = !can;
                        p.DoE = () => { if (S.Phase != Phase.Daily) Hud.I?.Toast("지금은 잘 수 없다", Pal.TextDim, 1.6f); else if (!Night()) Hud.I?.Toast("밤에만 잘 수 있다 — X 잠깐 눈 붙이기", Pal.TextDim, 2.2f); else _s.Cine.Sleep(); };
                        var nap = acts.FirstOrDefault(a => a.Id == "nap"); if (nap != null) { p.X = Label(nap); p.DoX = () => Do(f, nap); }
                    }
                    return p;
            }
            // seats: sit at once; X is what else the seat offers (추모하기, 함께 식사하기)
            var sit = acts.FirstOrDefault(a => a.Id == "sit");
            if (sit != null)
            {
                bool mine = _p.Seated && TimeLink.PlayerOnSpot(out var sp) && sp.Furniture == f.Id;
                p.E = mine ? "일어나기" : "앉기"; p.DoE = () => { if (mine) _p.StandUp(); else Sit(f); };
                var other = acts.FirstOrDefault(a => a.Id != "sit"); if (other != null) { p.X = Label(other); p.DoX = () => Do(f, other); }
                return p;
            }
            // everything else: E the simple thing (or the only pastime), X the pastime
            var inst = acts.Where(a => !TimeLink.IsPastime(a)).ToList(); var past = acts.Where(TimeLink.IsPastime).ToList();
            var e = inst.Count > 0 ? inst[0] : past.FirstOrDefault();
            var x = inst.Count > 0 ? (past.FirstOrDefault() ?? (inst.Count > 1 ? inst[1] : null)) : (past.Count > 1 ? past[1] : null);
            if (e != null) { p.E = Label(e); p.DoE = () => Do(f, e); }
            if (x != null) { p.X = Label(x); p.DoX = () => Do(f, x); }
            return p;
        }

        Vector3 AimAt(Furniture f)
        {
            var cam = _p.Cam.transform; var c = _s.World.ToWorld(f.Pos) + Vector3.up * Mathf.Clamp(f.H * 0.5f, 0.3f, 1.2f);
            return cam.position + cam.forward * Mathf.Clamp(Vector3.Dot(c - cam.position, cam.forward), 0.3f, 2.8f);
        }

        void Operate(Furniture f, Color c, ActionAnim anim) { Motion(anim, 1.0f); var r = Sim.PlayerOperate(f, 0); if (r != null) Hud.I?.Toast(r, c, 3); }

        /// <summary>Do one of the furniture's actions: sit, an instant thing, or a pastime (through the time director).</summary>
        void Do(Furniture f, PlayerAction act)
        {
            if (act == null) return;
            if (act.Id == "sit") { Sit(f); return; }
            if (!TimeLink.IsPastime(act)) { Instant(f, act); return; }
            // a pastime somewhere else: get up first (the staging walks over and takes its own seat)
            if (_p.Seated && !(TimeLink.PlayerOnSpot(out var sp) && sp.Furniture == f.Id)) _p.StandUp();
            _s.Cine.Activity(f, act);
        }

        /// <summary>E on a seat: sit at once on the nearest free place of it.</summary>
        void Sit(Furniture f)
        {
            var me = S.Player; if (me == null || f == null) return;
            if (TimeLink.PlayerOnSpot(out var cur) && cur.Furniture == f.Id && _p.Seated) return;
            var aim = _rayFurn == f ? new P3(me.Pos.f, _aimPoint.x, _aimPoint.z) : me.Pos;
            var sp = TimeLink.Sit(f, aim);
            if (sp == null) { Hud.I?.Toast("빈자리가 없다", Pal.TextDim, 1.4f); return; }
            _p.EnterSeat(sp);
        }

        /// <summary>A simple thing done at once (no time passes): the motion first, then the result. Also the facade for
        /// CinematicUI.Activity when an action takes no minutes.</summary>
        public void Instant(Furniture f, PlayerAction act)
        {
            if (f == null || act == null || S.Player == null) return;
            if (act.Id == "sit") { Sit(f); return; }
            if (act.Id == "search" && TimeLink.IsContainer(f))
            {
                // a piece with doors / drawers / a lid is opened (what it keeps shows in its slots); anything else is rummaged
                if (ContainerView.Has(f, out var cop)) { bool shut = false; for (int i = 0; i < cop.Count; i++) if (!cop.IsOpen(i)) shut = true; if (shut) ContainerView.ToggleAll(f, cop); }
                else ContainerView.Rummage(f);
                return;
            }
            InstantMotion(f, act);
            var res = Sim.PlayerUse(f, act.Id);
            if (res == null) return;
            if (!string.IsNullOrEmpty(res.Text)) Hud.I?.Toast(res.Text, res.ItemMade != null ? Pal.Gold : Pal.Text, res.ItemMade != null ? 3.2f : 2.4f);
            if (res.LoreTitle != null) Hud.I?.Lore(res.LoreTitle, res.LoreText);
            if (res.Music == "any") MusicDirector.I?.Skip();
        }

        void InstantMotion(Furniture f, PlayerAction act)
        {
            var fp = _s.World.ToWorld(f.Pos) + Vector3.up * Mathf.Clamp(f.H * 0.7f, 0.4f, 1.5f);
            var to = fp - _p.Cam.transform.position; var dir = to.sqrMagnitude > 1e-4f ? to.normalized : _p.Cam.transform.forward;
            switch (act.Id)
            {
                case "view": Gesture(BL23.Game.Characters.Gesture.LookAround, 1.4f); _p.Nudge(dir * 0.15f, 1.3f); break;
                case "tea": case "drink": Motion(ActionAnim.Use, 0.9f); HandProp("cup", 1.8f); Sfx.Play("amb_cutlery", fp, 0.25f); break;
                case "feed": Motion(ActionAnim.Use, 0.8f); _p.Nudge(dir * 0.08f, 0.8f); break;
                case "listen": Motion(ActionAnim.Use, 1.0f); break;
                case "wind": Motion(ActionAnim.Use, 1.2f); Sfx.Play("clock_tick", fp, 0.6f); _p.Nudge(dir * 0.1f, 1.1f); break;
                case "fire": Motion(ActionAnim.PickUp, 1.0f); Sfx.Play("candle_flare", fp, 0.6f); _p.Nudge(Vector3.down * 0.22f + dir * 0.08f, 1.0f); break;
                case "groom": Motion(ActionAnim.Use, 1.0f); break;
                case "inventory": case "log": Motion(ActionAnim.Examine, 1.0f); _p.Nudge(dir * 0.1f, 1.0f); break;
                case "wash": Motion(ActionAnim.Wash, 1.0f); break;
                default: Motion(ActionAnim.Use, 0.8f); break;
            }
        }

        /// <summary>A cup (a book, a sheet) in 민혁's hand for a moment — seen in first person.</summary>
        void HandProp(string kind, float secs)
        {
            var rig = _s.World.ViewOf(Cast.Player)?.Rig; if (rig == null || rig.HandAnchorR == null || S.Player.HandR != null) return;
            var go = HandProps.Attach(rig.HandAnchorR, kind); if (go != null) Destroy(go, secs);
        }

        bool Guard(string key)
        {
            if (key != null && key == _lastUseKey && Time.time - _lastUseAt < 0.35f) return false;
            _lastUseKey = key; _lastUseAt = Time.time; return true;
        }

        // ---- probe helpers (AutoProbe.Time)
        Furniture Fur(int fid) => fid >= 0 && fid < S.Layout.Furniture.Count ? S.Layout.Furniture[fid] : null;
        /// <summary>Probe: open one part of a container (true when it is open afterwards).</summary>
        public bool ProbeOpen(int fid, int part)
        {
            var f = Fur(fid); if (f == null || !ContainerView.Has(f, out var op) || part < 0 || part >= op.Count) return false;
            if (!op.IsOpen(part)) ContainerView.Toggle(f, op, part);
            return op.IsOpen(part);
        }
        /// <summary>Probe: pick up the first thing found in the container opened last, or else what the crosshair is on.</summary>
        public bool ProbePickUp()
        {
            var it = ContainerView.FirstVisibleIn(Fur(ContainerView.LastOpened)) ?? _item; if (it == null) return false;
            if (!Sim.PlayerPickUp(it)) return false;
            Motion(ActionAnim.PickUp, 0.7f); Hud.I?.Toast(LineBank.FixParticles(it.Kor + "을(를) 집었다"), Pal.Text, 1.2f); return true;
        }
        /// <summary>Probe: X on a container — everything opens (true when every part is open afterwards).</summary>
        public bool ProbeOpenAll(int fid)
        {
            var f = Fur(fid); if (f == null || !ContainerView.Has(f, out var op)) return false;
            bool shut = false; for (int i = 0; i < op.Count; i++) if (!op.IsOpen(i)) shut = true;
            if (shut) ContainerView.ToggleAll(f, op);
            for (int i = 0; i < op.Count; i++) if (!op.IsOpen(i)) return false;
            return true;
        }
        /// <summary>Probe: sit on this seat (true when seated).</summary>
        public bool ProbeSit(int fid) { var f = Fur(fid); if (f == null) return false; Sit(f); return _p.Seated; }
        /// <summary>Probe: get up.</summary>
        public bool ProbeStand() { _p.StandUp(); return !TimeLink.KernelSeated; }
        // --- time-on-demand (end) --------------------------------------------------------------------------------------------

        bool KnownTrap(Furniture f) => S.Traps.Any(x => x.Active && x.Furniture == f.Id && S.K(Cast.Player).Facts.Contains("knows-trap:" + x.Id));

        string FurnHint(Furniture f)
        {
            // --- time-on-demand (begin): E the simple thing, X what else (with its minutes when it takes a while)
            _plan = PlanFor(f);
            var parts = new List<string>();
            if (_plan.E != null) parts.Add(_plan.EDim ? $"<color=#9A8E7C>E {_plan.E}</color>" : "E " + _plan.E);
            if (_plan.X != null) parts.Add("X " + _plan.X);
            parts.Add(f.Type == "Clock" || f.Type == "ClockCase" ? "R 시계 보기" : f.Type == "DoorLogger" && _plan.E == null ? "R 출입 기록 보기" : "R 살펴보기");
            return string.Join(" · ", parts);
            // --- time-on-demand (end)
        }

        bool Visible(Vector3 from, Vector3 to)
        {
            var dir = to - from; if (Physics.Raycast(from, dir.normalized, out var h, dir.magnitude - 0.35f, ~0, QueryTriggerInteraction.Ignore)) return h.collider.GetComponentInParent<ActorRig>() != null || h.collider.GetComponentInParent<ActorView>() != null;
            return true;
        }

        /// <summary>민혁's body does what the player does (seen in third person, in mirrors and in cinematics).</summary>
        void Motion(ActionAnim a, float secs) { var an = _s.World.ViewOf(Cast.Player)?.Rig?.Anim; if (an != null) an.PlayAction(a, secs); }
        void Gesture(BL23.Game.Characters.Gesture g, float secs) { var an = _s.World.ViewOf(Cast.Player)?.Rig?.Anim; if (an != null) an.PlayGesture(g, secs); }

        void Primary()
        {
            var me = S.Player;
            if (_actor != null)
            {
                if (_actor.Status == ActorStatus.Active) { _s.Dialogue.Open(_actor); return; }
                if (me.Carrying == null) { if (Sim.PlayerCarry(_actor)) Hud.I?.Toast(LineBank.FixParticles(Cast.GivenOf(_actor.Id) + (BL23.Sim.Violence.Dragging(S, me) ? "을(를) 들어 올리지 못해 겨드랑이를 잡고 끈다 — E로 놓기" : "을(를) 둘러멨다 — E로 내려놓기")), Pal.Gold); return; }   // (violence track: too heavy → dragged)
            }
            if (me.Carrying != null) { Sim.PlayerReleaseCarry(); Hud.I?.Toast("내려놓았다", Pal.TextDim, 1.2f); return; }
            if (_item != null) { if (Sim.PlayerPickUp(_item)) { Motion(ActionAnim.PickUp, 0.7f); Sfx.Play("ui_confirm", null, 0.3f); Hud.I?.Toast(LineBank.FixParticles(_item.Kor + "을(를) 집었다"), Pal.Text, 1.2f); } return; }
            if (_door != null) { Motion(ActionAnim.OpenDoor, 0.6f); var r = Sim.PlayerDoor(_door, "open"); if (r != null) Hud.I?.Toast(r, Pal.TextDim, 1.2f); return; }
            if (_furn != null && !StashOnX(_furn) && ConcealUse(_furn)) return;   // --- concealment: E 넣어 두기 / 숨겨 둔 것 꺼내기
            if (_furn != null) UseFurniture(_furn);
        }

        void UseFurniture(Furniture f)
        {
            // --- time-on-demand (begin): the plan the hint showed (open the part, sit, the instant thing, the pastime)
            var p = _plan != null && _furn == f ? _plan : PlanFor(f);
            if (p.DoE == null) { Examine(); return; }
            if (!Guard(p.Key)) return;
            p.DoE();
            // --- time-on-demand (end)
        }

        /// <summary>X: the other thing this furniture offers (open everything, read for half an hour, a bed's nap...).</summary>
        void Secondary()
        {
            // --- time-on-demand (begin)
            if (_furn == null) return;
            if (StashOnX(_furn) && ConcealUse(_furn)) return;   // --- concealment: a seat / bed / piano keeps its E; hiding goes on X
            var p = _plan != null ? _plan : PlanFor(_furn);
            if (p.DoX == null || !Guard(_furn.Id + ":x")) return;
            p.DoX();
            // --- time-on-demand (end)
        }

        void Examine()
        {
            Evidence ev = null;
            if (_actor != null || _trace != null) Motion(ActionAnim.PickUp, 1.1f); else Gesture(BL23.Game.Characters.Gesture.LookAround, 1.4f);   // bend down to a body or a mark; look over anything else
            if (_actor != null) { ev = Sim.PlayerExamine(_actor); if (ev != null) { Hud.I?.MarkShown(ev.Id); _s.Note.ShowBody(_actor, ev); } return; }   // the notebook opens on it: no second toast
            if (_item != null) ev = Sim.PlayerExamine(_item);
            else if (_trace != null) ev = Sim.PlayerExamine(_trace);
            else if (_door != null) ev = Sim.PlayerExamine(_door);
            else if (_furn != null) { ev = Sim.PlayerExamine(_furn); Conceal.LookInto(_furn); }   // (concealment: a close look at a hiding place turns up what is hidden there)
            var lk = LookKey(); if (lk != null) _looked.Add($"{S.Loop}:{S.Chapter}:{lk}");
            if (ev != null) { if (ev.Loose) Hud.I?.Look(ev); else Hud.I?.Card(ev); }   // a look with nothing notable is a caption, not a card
        }

        void DropOrThrow(bool thr)
        {
            var me = S.Player; var it = Sim.Held(me); if (it == null) return;
            var cam = _p.Cam.transform; var at = cam.position + cam.forward * 0.6f;
            int f = me.Pos.f;
            Motion(thr ? ActionAnim.Overhead : ActionAnim.PutDown, thr ? 0.45f : 0.6f);
            Sim.PlayerDrop(it, new P3(f, at.x, at.z));
            if (_s.World.Items.TryGetValue(it.Id, out var iv)) { if (thr) iv.Throw(at, cam.forward * 7f + Vector3.up * 1.5f); }
        }

        void Swing()
        {
            if (_melee == null) { _melee = gameObject.AddComponent<PhysicalMelee>(); _melee.Init(_s, _p); }
            if (_melee.Begin())
            {
                // --- violence track: the first-person hands carry the swing (wind-up → strike → follow-through → recovery)
                var w = Sim.Held(S.Player, d => d != null && d.IsWeapon);
                FirstPersonHands.PlaySwing(w?.Def?.Dmg == DamageType.Stab ? ActionAnim.Stab : w?.Def?.Dmg == DamageType.Cut ? ActionAnim.Slash : ActionAnim.Overhead);
            }
        }

        // --- violence track (begin) ------------------------------------------------------------------------------------------
        float _fireCd; float _seizeConfirmAt = -9f; string _seizeConfirm;
        bool Near(Actor a, float d) => a != null && a.Pos.f == S.Player.Pos.f && a.Pos.DistXZ(S.Player.Pos) <= d;

        /// <summary>Guns and the crossbow (hold right mouse to aim, left to fire, R to load), a hold on someone's throat (a cord
        /// in hand: right + left mouse; bare hands: right mouse + V; V again lets go), tying up / untying (V). Returns true when
        /// it used the input this frame.</summary>
        bool ViolenceInput(Actor me)
        {
            _fireCd -= Time.deltaTime;
            var held = Sim.Held(me); bool rmb = Input.GetMouseButton(1);
            // holding someone: the hands stay on them until V (or right + left mouse) lets go, or they break free
            var hold = BL23.Sim.Violence.DoingTo(S, Cast.Player);
            FirstPersonHands.SetHold(hold);
            if (hold != null)
            {
                Hud.I?.Aim(false);
                if (Input.GetKeyDown(KeyCode.V) || rmb && Input.GetMouseButtonDown(0)) { Sim.PlayerLetGo(); Hud.I?.Toast("손을 놓았다", Pal.TextDim, 1.2f); }
                return true;
            }
            // a gun or the crossbow
            if (held != null && BL23.Sim.Violence.IsRanged(held.Def))
            {
                FirstPersonHands.SetAim(rmb, held);
                Hud.I?.Aim(rmb);
                if (rmb && Input.GetKeyDown(KeyCode.R))
                {
                    int before = Sim.PlayerRounds(); var msg = Sim.PlayerLoad(); bool ok = Sim.PlayerRounds() > before;
                    Hud.I?.Toast(msg, ok ? Pal.Text : Pal.TextDim, 1.8f);
                    if (ok) FirstPersonHands.PlayReload(held.Type, BL23.Sim.Violence.LoadTicks(held.Type) / 10f);
                    return true;
                }
                if (rmb && Input.GetMouseButtonDown(0) && _fireCd <= 0f && !(_p.Grab?.Holding ?? false)) { _fireCd = held.Type == "Revolver" ? 0.35f : 0.8f; Fire(); }
                return rmb;
            }
            FirstPersonHands.SetAim(false, null);
            // a cord in hand: the garrote
            if (held != null && BL23.Sim.Methods.IsCord(held.Def) && rmb && Input.GetMouseButtonDown(0) && _actor != null && _actor.Alive && _actor.Status == ActorStatus.Active && Near(_actor, 1.3f)) { Seize(); return true; }
            // V: tie up / untie; right mouse + V: bare hands (the throat, the water, a pillow)
            if (Input.GetKeyDown(KeyCode.V) && _actor != null && _actor.Alive && Near(_actor, 1.9f))
            {
                if (rmb) { Seize(); return true; }
                if (BL23.Sim.Violence.Bound(S, _actor.Id) != null) { var m = Sim.PlayerUnbind(_actor); Hud.I?.Toast(m, Pal.Text, 2f); if (BL23.Sim.Violence.Bound(S, _actor.Id) == null) FirstPersonHands.PlayBind(); return true; }
                var r = Sim.PlayerBind(_actor); bool tied = BL23.Sim.Violence.Bound(S, _actor.Id) != null;
                Hud.I?.Toast(r, tied ? Pal.Gold : Pal.TextDim, 2.4f); if (tied) FirstPersonHands.PlayBind();
                return true;
            }
            return false;
        }

        void Fire()
        {
            var cam = _p.Cam.transform; var f = cam.forward;
            float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, pitch = Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
            float eyeY = cam.position.y - _s.World.ToWorld(S.Player.Pos).y;
            var shot = Sim.PlayerFire(eyeY, yaw, pitch);
            if (shot == null)
            {
                if (Sim.PlayerRounds() == 0) { Hud.I?.Toast("탄이 없다 — 겨눈 채 R로 장전", Pal.TextDim, 1.6f); Sfx.PlayEx("light_switch", cam.position, 0.4f, 1.5f); }
                return;
            }
            // flash, recoil and the impacts follow the kernel's shot event (ViolencePresenter)
            var hit = shot.Impacts.Find(i => i.Kind == "body");
            if (hit != null && hit.Actor != null) Hud.I?.Toast(LineBank.FixParticles(Cast.GivenOf(hit.Actor) + "의 " + WoundText.Region(hit.Region) + "에 맞았다"), Pal.Blood, 1.8f);
        }

        void Seize()
        {
            // the first time on someone: a warning, as with a weapon (the second time is meant)
            if (_seizeConfirm != _actor.Id || Time.time - _seizeConfirmAt > 4f) { _seizeConfirm = _actor.Id; _seizeConfirmAt = Time.time; Hud.I?.Toast(LineBank.FixParticles("한 번 더 하면 " + Cast.GivenOf(_actor.Id) + "을(를) 정말로 붙잡는다"), Pal.Blood, 2.4f); return; }
            _seizeConfirm = null;
            var x = Sim.PlayerSeize(_actor);
            if (x == null) { Hud.I?.Toast("붙잡을 수 없다", Pal.TextDim, 1.2f); return; }
            Hud.I?.Toast(x.Kind == AssaultKind.Drown ? "머리채를 잡아 물속으로 짓누른다 — V로 놓기" : x.Kind == AssaultKind.Smother ? "얼굴을 짓누른다 — V로 놓기" : "목을 조른다 — V로 놓기", Pal.Blood, 2.6f);
        }

        /// <summary>The extra key hints on someone in reach.</summary>
        string ViolenceHint(Actor a)
        {
            if (a == null || !a.Alive || !Near(a, 1.9f)) return "";
            if (BL23.Sim.Violence.Bound(S, a.Id) != null) return " · V 풀어 주기";
            bool rope = Sim.Carried(S.Player).Any(i => BL23.Sim.Violence.IsBinding(i.Def));
            return rope && BL23.Sim.Violence.CanBind(S, S.Player, a, null, out _) ? " · V 묶기" : "";
        }
        // --- violence track (end) --------------------------------------------------------------------------------------------
    }
}
