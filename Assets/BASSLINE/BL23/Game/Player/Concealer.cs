using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Audio;
using BL23.Game.Characters;
using BL23.Game.Mansion;
using BL23.Game.Physicality;
using BL23.Sim;
using TMPro;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// Hiding things, in first person (kernel: Sim/Systems/Concealment.cs).
    ///  · Q with something in hand: it goes out of sight — a pocket, inside the jacket, the cross-bag (the prompt says which,
    ///    or why it can't: "너무 커서 숨길 수 없다"). Q with the hands free: the last hidden weapon (or thing) comes back out;
    ///    hold Q for the wheel (scroll or 1–8 to choose, let go to draw).
    ///  · E at a hiding place with something in hand: "넣어 두기" — into the drawer, under the mattress, behind the books...
    ///    E at a place where 민혁 hid something (hands free): take it back out. R there: a close look turns up what is hidden.
    /// Each is a short hand motion (the contact IK reach when the body has one; the nearest ActionAnim otherwise), a rustle,
    /// and a toast; when someone saw it, 민혁 feels it ("…의 시선이 느껴진다").
    /// </summary>
    public sealed class Concealer : MonoBehaviour
    {
        Session _s; PlayerController _p;
        GameState S => _s.S; Simulation Sim => _s.Sim;
        public static Concealer I;
        float _qDown = -1f, _lockUntil; bool _wheel; int _sel; List<Item> _wheelItems = new List<Item>();
        ConcealUI _ui;

        public static Concealer For(Session s, PlayerController p, Component host)
        {
            var c = host.GetComponent<Concealer>() ?? host.gameObject.AddComponent<Concealer>();
            c._s = s; c._p = p; I = c; return c;
        }

        /// <summary>Hands busy with a motion (no second act until it is done).</summary>
        public bool Busy => Time.time < _lockUntil;
        public bool WheelOpen => _wheel;
        public IReadOnlyList<Item> WheelItems => _wheelItems;
        public int WheelSel => _sel;

        void Start() { if (_ui == null) { var go = new GameObject("ConcealUI"); DontDestroyOnLoad(go); _ui = go.AddComponent<ConcealUI>(); _ui.Init(this); } }
        void OnDestroy() { if (_ui != null) Destroy(_ui.gameObject); if (I == this) I = null; }

        // ------------------------------------------------------------------ what the prompts say
        /// <summary>The line under the crosshair about the hands: what is held and what Q does with it; or what is hidden on the body.</summary>
        public string HandsLine()
        {
            var me = S?.Player; if (me == null || Sim == null) return null;
            var h = Sim.Held(me);
            if (h != null)
            {
                var (label, can, _) = Sim.ConcealPrompt(h);
                string stain = h.Bloody ? " <color=#C04A55>· 피 묻음</color>" : "";
                return $"<color=#D6AD62>손</color> {h.Kor}{stain} <color=#B8AC98>— 모두에게 보인다</color>   " + (can ? $"<color=#D6AD62>Q</color> {label}" : $"<color=#9A8E7C>{label}</color>");
            }
            var hid = Hidden();
            if (hid.Count == 0) return null;
            string names = string.Join(" · ", hid.Take(3).Select(x => x.it.Kor)) + (hid.Count > 3 ? $" 외 {hid.Count - 3}" : "");
            return $"<color=#D6AD62>품</color> {names}   <color=#D6AD62>Q</color> 꺼내기 <size=80%><color=#9A8E7C>(길게: 고르기)</color></size>";
        }

        /// <summary>Things hidden on the body worth drawing (not the room keys), jacket and bag first.</summary>
        List<(Item it, BodySlot slot)> Hidden() => Sim.PlayerConcealed().Where(x => x.it.KeyFor == null && x.it.Def?.Key != true).ToList();

        /// <summary>Interaction's hint for a piece of furniture, with the hiding acts put first: "E 넣어 두기 (매트리스 밑)" with
        /// something in hand, "E 숨겨 둔 식칼 꺼내기" at a place 민혁 hid something (hands free). Null: leave the hint alone.</summary>
        /// <param name="onX">The piece's own E stays (a seat, a bed, a piano): the hiding act goes on X instead.</param>
        public string Decorate(Furniture f, string hint, bool onX)
        {
            if (f == null || S?.Player == null || !Concealment.IsPlace(f)) return null;
            var h = Sim.Held(S.Player); string key = onX ? "X" : "E";
            string first = null;
            if (h != null)
            {
                var (label, can) = Sim.StashPrompt(f, h);
                if (label == null) return null;
                first = can ? key + " " + label : $"<color=#9A8E7C>{label}</color>";
            }
            else if (!ContainerView.Has(f, out _))
            {
                var mine = Sim.PlayerStashesAt(f).FirstOrDefault();
                if (mine != null) first = LineBank.FixParticles($"{key} 숨겨 둔 {mine.Kor} 꺼내기");
            }
            if (first == null) return null;
            var parts = (hint ?? "").Split(new[] { " · " }, System.StringSplitOptions.RemoveEmptyEntries).Where(p => !p.StartsWith(key + " ") && !p.StartsWith("<color=#9A8E7C>" + key + " ")).ToList();
            parts.Insert(onX ? Mathf.Min(1, parts.Count) : 0, first);
            return string.Join(" · ", parts);
        }

        // ------------------------------------------------------------------ keys (from Interaction.Tick)
        /// <summary>Q: tap = hide what is in hand / draw the last hidden thing; hold = the wheel. firstAid: Q belongs to first aid now.</summary>
        public void TickKeys(bool firstAid)
        {
            if (S?.Player == null || Sim == null) return;
            if (firstAid) { _qDown = -1f; CloseWheel(); return; }
            if (Input.GetKeyDown(KeyCode.Q)) _qDown = Time.unscaledTime;
            if (_qDown >= 0f && Input.GetKey(KeyCode.Q) && !_wheel && Time.unscaledTime - _qDown > 0.28f) OpenWheel();
            if (_wheel)
            {
                float wh = Input.mouseScrollDelta.y;
                if (wh > 0.01f) { _sel = (_sel - 1 + _wheelItems.Count) % _wheelItems.Count; UISfx.Hover(); }
                else if (wh < -0.01f) { _sel = (_sel + 1) % _wheelItems.Count; UISfx.Hover(); }
                for (int i = 0; i < Mathf.Min(8, _wheelItems.Count); i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { _sel = i; UISfx.Hover(); }
            }
            if (_qDown >= 0f && Input.GetKeyUp(KeyCode.Q))
            {
                _qDown = -1f;
                if (_wheel) { var pick = _sel >= 0 && _sel < _wheelItems.Count ? _wheelItems[_sel] : null; CloseWheel(); if (pick != null) Draw(pick); return; }
                var h = Sim.Held(S.Player);
                if (h != null) Conceal(h);
                else { var q = QuickPick(); if (q != null) Draw(q); else { Hud.I?.Toast("숨겨 둔 것이 없다", Pal.TextDim, 1.2f); UISfx.Deny(); } }
            }
        }

        void OpenWheel()
        {
            _wheelItems = Hidden().Select(x => x.it).Take(8).ToList();
            if (_wheelItems.Count == 0) return;
            var q = QuickPick(); _sel = Mathf.Max(0, _wheelItems.IndexOf(q));
            _wheel = true; UISfx.Page();
        }
        void CloseWheel() { _wheel = false; }

        /// <summary>The thing a quick Q draws: a weapon from the jacket or bag first, then anything there, then the pockets.</summary>
        Item QuickPick()
        {
            var hid = Hidden(); if (hid.Count == 0) return null;
            return hid.FirstOrDefault(x => x.slot != BodySlot.Pocket && x.it.Def?.IsWeapon == true).it
                ?? hid.FirstOrDefault(x => x.slot != BodySlot.Pocket).it
                ?? hid.FirstOrDefault(x => x.it.Def?.IsWeapon == true).it
                ?? hid[0].it;
        }

        // ------------------------------------------------------------------ the acts
        /// <summary>Hand → body (prefer: a particular part of the clothes).</summary>
        public bool Conceal(Item it, BodySlot prefer = BodySlot.None)
        {
            if (it == null || Busy) return false;
            var r = Sim.PlayerConceal(it, prefer);
            if (!r.Ok) { Tell(it, r.Why ?? "숨길 수 없다", Pal.TextDim, 1.6f); UISfx.Deny(); return false; }
            Play("tuck", r.Slot, null, r.Seconds);
            LastItem = it.Id; Say(r, it.Bloody ? Pal.Blood : Pal.Text);
            return true;
        }

        /// <summary>Body → hand.</summary>
        public bool Draw(Item it)
        {
            if (it == null || Busy) return false;
            var slot = Concealment.SlotOf(S, it);
            var r = Sim.PlayerDraw(it);
            if (!r.Ok) { Tell(it, r.Why ?? "꺼낼 수 없다", Pal.TextDim, 1.4f); UISfx.Deny(); return false; }
            Play("draw", slot, null, r.Seconds, handDriven: true);   // (the item transfer reaches for it at the hip and closes the hand on it)
            LastItem = it.Id; Say(r, Pal.Text);
            return true;
        }

        /// <summary>E at a hiding place with something in hand: put it away there (a drawer or a door swings open for the hand).</summary>
        public bool Stash(Furniture f, Item it = null)
        {
            it = it ?? Sim.Held(S.Player);
            if (f == null || it == null || Busy) return false;
            var (label, can) = Sim.StashPrompt(f, it);
            if (!can) { Tell(it, label ?? "여기에는 숨길 데가 없다", Pal.TextDim, 1.6f); UISfx.Deny(); return true; }
            bool fromHand = S.Player.HandR == it.Id || S.Player.HandL == it.Id;
            var r = Sim.PlayerStash(it, f);
            if (!r.Ok) { Tell(it, r.Why ?? "넣어 둘 수 없다", Pal.TextDim, 1.6f); UISfx.Deny(); return true; }
            if (ContainerView.Has(f, out var op)) StartCoroutine(OpenForHand(f, op, r.Seconds));
            Play("stash", BodySlot.None, f, r.Seconds, handDriven: fromHand);   // (from a hand: the item transfer places it where it goes)
            LastItem = it.Id; Say(r, Pal.Gold);
            StartCoroutine(Settle(it.Id));
            return true;
        }

        /// <summary>Take a stashed thing back (standing at its place).</summary>
        public bool Retrieve(Item it)
        {
            if (it == null || Busy) return false;
            var f = it.StashF >= 0 && it.StashF < S.Layout.Furniture.Count ? S.Layout.Furniture[it.StashF] : null;
            // the hand reaches for it where it lies (not at the hip, where a thing drawn from a pocket would come from)
            var me = S.Player; bool near = f != null && f.Pos.f == me.Pos.f && me.Pos.DistXZ(f.Pos) <= Concealment.Reach(f);
            if (near && it.Holder == null && it.Hidden && _s.World.Items.TryGetValue(it.Id, out var iv) && iv.Visual != null && !iv.InContainer)
            { iv.Visual.transform.position = PlacePoint(f); iv.Visual.SetActive(true); }
            var r = Sim.PlayerRetrieve(it);
            if (!r.Ok) { Tell(it, r.Why ?? "꺼낼 수 없다", Pal.TextDim, 2f); UISfx.Deny(); if (_s.World.Items.TryGetValue(it.Id, out var iv2)) iv2.Refresh("hidden"); return false; }
            Play("retrieve", BodySlot.None, f, r.Seconds, handDriven: me.HandR == it.Id || me.HandL == it.Id);
            LastItem = it.Id; Say(r, Pal.Text);
            return true;
        }

        /// <summary>E at a place with something 민혁 hid (or saw hidden) there: take it back (the first of them).</summary>
        public bool RetrieveAt(Furniture f)
        {
            var it = Sim.PlayerStashesAt(f).FirstOrDefault(); if (it == null) return false;
            Retrieve(it); return true;
        }

        /// <summary>R at a hiding place that has no door or drawer: a close look turns up what is hidden there.</summary>
        public void LookInto(Furniture f)
        {
            if (f == null || !Concealment.IsPlace(f) || TimeLink.IsContainer(f)) return;
            var r = Sim.PlayerSearchStash(f);
            if (!r.Ok) return;
            Play("search", BodySlot.None, f, r.Seconds);
            if (r.Found.Count > 0)
            {
                foreach (var it in r.Found) if (_s.World.Items.TryGetValue(it.Id, out var iv)) { iv.Refresh("reveal"); iv.HopTo(_s.World.ToWorld(it.Pos) + Vector3.up * 0.05f); }
                Hud.I?.Toast(r.Text, Pal.Gold, 3f); Sfx.Play("parchment", null, 0.35f);
            }
            else Hud.I?.Toast(r.Text, Pal.TextDim, 1.6f);
            if (r.SeenBy != null && r.Found.Count == 0) Hud.I?.Toast(LineBank.FixParticles($"{Cast.GivenOf(r.SeenBy)}이(가) 그걸 봤다"), Pal.Blood, 2f);
        }

        /// <summary>The last result in words, for the notebook's detail (the HUD toasts sit under the notebook).</summary>
        public string LastText { get; private set; }
        public string LastItem { get; private set; }
        void Tell(Item it, string text, Color c, float secs)
        {
            LastItem = it?.Id; LastText = text;
            Hud.I?.Toast(text, c, secs);
        }

        void Say(ConcealResult r, Color c)
        {
            LastText = r.Text;
            if (!string.IsNullOrEmpty(r.Text)) Hud.I?.Toast(r.Text, r.Stained ? Pal.Blood : c, 2.2f);
            if (r.SeenBy != null && S.A(r.SeenBy) != null) { string seen = LineBank.FixParticles($"{Cast.GivenOf(r.SeenBy)}의 시선이 느껴진다"); Hud.I?.Toast(seen, Pal.Blood, 2.6f); LastText += " — " + seen; }
        }

        // ------------------------------------------------------------------ motions
        Vector3 PlacePoint(Furniture f)
        {
            var p = Concealment.PlaceFor(f); var c = _s.World.ToWorld(f.Pos);
            float y = p != null && p.Low ? Mathf.Min(0.35f, f.H * 0.6f) : Mathf.Clamp(f.H * 0.55f, 0.4f, 1.35f);
            var toMe = _p.transform.position - c; toMe.y = 0; var dir = toMe.sqrMagnitude > 1e-4f ? toMe.normalized : Vector3.forward;
            return c + Vector3.up * y + dir * Mathf.Min(0.3f, Mathf.Min(f.W, f.D) * 0.35f);
        }

        /// <summary>The body does it: the contact reach of the arm (the physicality layer) toward the jacket, the pocket, the bag or
        /// the place; the closest ActionAnim when the arm can't; a small lean of the view and a rustle either way.</summary>
        /// <param name="handDriven">The physicality layer's item transfer already moves the hand (a draw into a hand, a place from a hand):
        /// only the view and the sound are added here.</param>
        void Play(string act, BodySlot slot, Furniture f, float secs, bool handDriven = false)
        {
            secs = Mathf.Max(0.4f, secs); _lockUntil = Time.time + secs * 0.9f;
            var rig = _s.World.ViewOf(Cast.Player)?.Rig;
            var t = rig != null ? rig.transform : _p.transform;
            Vector3 target;
            bool low = f != null && (Concealment.PlaceFor(f)?.Low ?? false);
            if (f != null) target = PlacePoint(f);
            else if (slot == BodySlot.Coat && rig?.Chest != null) target = rig.Chest.position + t.forward * 0.1f - t.right * 0.07f;   // the inside pocket, left breast
            else if (slot == BodySlot.Bag && rig?.Hips != null) target = rig.Hips.position - t.right * 0.22f + t.forward * 0.02f;   // the bag at the left hip
            else target = (rig?.Hips != null ? rig.Hips.position : t.position + Vector3.up * 0.95f) + t.right * 0.17f + t.forward * 0.04f;   // a trouser pocket
            var pac = rig != null ? rig.GetComponent<PhysicalActionController>() : null;
            bool reached = handDriven && pac != null || pac != null && !pac.Busy && pac.CanUseHand(false) && pac.Reach(target, false, Mathf.Clamp(secs * 0.8f, 0.45f, 1.4f));
            var an = rig?.Anim;
            if (!reached && an != null)
            {
                // (no tuck clip yet: the nearest ones — a hand to the chest for the jacket, a hand down for a pocket or the bag)
                if (act == "tuck" || act == "draw") { if (slot == BodySlot.Coat) an.PlayGesture(Gesture.HandOnChest, secs); else an.PlayAction(ActionAnim.PutDown, secs); }
                else if (act == "search") an.PlayAction(low ? ActionAnim.PickUp : ActionAnim.Search, secs);
                else if (f != null && (f.Type == "Planter" || f.Type == "Plant")) an.PlayAction(ActionAnim.Garden, secs);
                else an.PlayAction(low ? ActionAnim.PickUp : ActionAnim.Use, secs);
            }
            // the view: a glance down to the jacket, or a lean to the place
            if (f == null) _p.Nudge(Vector3.down * (slot == BodySlot.Coat ? 0.07f : 0.1f), secs * 0.8f);
            else { var d = target - _p.Cam.transform.position; var dir = d.sqrMagnitude > 1e-4f ? d.normalized : _p.Cam.transform.forward; _p.Nudge(dir * 0.1f + (low ? Vector3.down * 0.2f : Vector3.zero), secs); }
            var at = f != null ? target : _p.transform.position + Vector3.up;
            string sfx = f == null ? "parchment" : f.Type == "Aquarium" ? "water_splash" : f.Type == "Planter" || f.Type == "Plant" ? "step_carpet" : act == "search" ? "amb_page" : "amb_creak";
            Sfx.Play(sfx, at, f == null ? 0.22f : 0.3f);
        }

        /// <summary>A drawer or a door of the piece opens for the hand and shuts again after (presentation only: nothing is "found").</summary>
        IEnumerator OpenForHand(Furniture f, OpenableParts op, float secs)
        {
            if (op == null || op.Count == 0) yield break;
            int part = op.PartNearest(PlacePoint(f)); if (part < 0) yield break;
            bool wasOpen = op.IsOpen(part);
            if (!wasOpen) { op.Open(part); Sfx.Play(op.Parts[part].Kind == OpenableParts.PartKind.Drawer ? "chair_scrape" : "door_creak", PlacePoint(f), 0.25f); }
            yield return new WaitForSeconds(Mathf.Max(0.6f, secs));
            if (!wasOpen && op != null) { op.Close(part); Sfx.Play("door_close", PlacePoint(f), 0.2f); }
        }

        /// <summary>After the hand lets go at the place, the thing goes where the kernel keeps it (a drawer's slot, or out of sight).</summary>
        IEnumerator Settle(string id)
        {
            float t0 = Time.time;
            while (Time.time - t0 < 2.5f)
            {
                yield return null;
                if (!_s.World.Items.TryGetValue(id, out var iv) || iv == null) yield break;
                if (!iv.ContactTransfer && !iv.Held) { iv.Refresh("stash"); yield break; }
            }
            if (_s.World.Items.TryGetValue(id, out var v2) && v2 != null) { PhysicalItemTransfer.Abort(v2); v2.Refresh("stash"); }
        }

        // ------------------------------------------------------------------ probe helpers (AutoProbe.Conceal)
        public void ProbeOpenWheel() { OpenWheel(); }
        public void ProbeCloseWheel() { CloseWheel(); }
    }

    /// <summary>The hands line under the crosshair and the quick-draw wheel (hold Q).</summary>
    public sealed class ConcealUI : MonoBehaviour
    {
        Concealer _c; Canvas _cv; SlantPanel _hands; TextMeshProUGUI _handsText; RectTransform _wheel; readonly List<(SlantPanel bg, TextMeshProUGUI t)> _slots = new List<(SlantPanel, TextMeshProUGUI)>(); TextMeshProUGUI _wheelTitle;
        string _last; int _lastSel = -2, _lastCount = -1; float _poll;

        public void Init(Concealer c)
        {
            _c = c; _cv = UIKit.Root("ConcealUI", 12); var t = _cv.transform;
            _hands = UIKit.Slant(t, "Hands", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-470, 50), new Vector2(470, 88), Pal.A(Pal.Ink, 0.96f), Pal.A(Pal.Panel, 0.94f), Pal.A(Pal.Gold, 0.6f), 0);
            _hands.EdgeLeftOnly = true;
            _handsText = UIKit.Text(_hands.transform, "T", "", 19, Pal.A(Pal.Text, 0.95f), TextAlignmentOptions.Center, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(14, 0), new Vector2(-14, 0));
            _handsText.textWrappingMode = TextWrappingModes.NoWrap; _handsText.overflowMode = TextOverflowModes.Ellipsis;
            _hands.gameObject.SetActive(false);
            _wheel = UIKit.Rect(t, "Wheel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -250), new Vector2(300, 250));
            UIKit.Img(_wheel, "Dim", Pal.A(Pal.Ink, 0.86f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-250, -250), new Vector2(250, 250)).sprite = Disc();
            _wheelTitle = UIKit.Text(_wheel, "Title", "", 20, Pal.A(Pal.Gold, 0.95f), TextAlignmentOptions.Center, Fonts.Serif, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-260, 262), new Vector2(260, 330));
            _wheel.gameObject.SetActive(false);
        }

        static Sprite _disc;
        static Sprite Disc()
        {
            if (_disc != null) return _disc;
            int n = 128; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false); var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float dx = x - n / 2f + 0.5f, dy = y - n / 2f + 0.5f; float r = Mathf.Sqrt(dx * dx + dy * dy) / (n / 2f); float a = Mathf.Clamp01((1f - r) * 6f) * Mathf.Lerp(0.9f, 0.55f, r); px[x + y * n] = new Color32(255, 255, 255, (byte)(a * 255)); }
            tex.SetPixels32(px); tex.Apply(); _disc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f)); return _disc;
        }

        void Update()
        {
            var s = Session.I; if (_c == null || s == null || s.S == null || s.S.Player == null) { if (_cv != null) _cv.enabled = false; return; }
            var S = s.S;
            bool explore = (S.Phase == Phase.Daily || S.Phase == Phase.Investigation || S.Phase == Phase.Assembly) && !(s.Dialogue?.Active ?? false) && !(s.Note?.Open ?? false)
                           && !(s.Trial?.Active ?? false) && !(s.Reveal?.Active ?? false) && !(s.Cine?.Busy ?? false) && !CaseReport.Open && !(s.Menu?.Open ?? false) && !(s.TimeDir?.Active ?? false);
            _cv.enabled = explore;
            if (!explore) return;
            if ((_poll -= Time.unscaledDeltaTime) <= 0f)
            {
                _poll = 0.12f;   // (a few times a second is plenty for a line about the hands)
                string line = null; try { line = _c.HandsLine(); } catch (System.Exception) { }
                if (line != _last) { _last = line; _handsText.text = line ?? ""; UIKit.SetActive(_hands, line != null); }
            }
            bool wheel = _c.WheelOpen && _c.WheelItems.Count > 0;
            UIKit.SetActive(_wheel, wheel);
            if (!wheel) { _lastSel = -2; return; }
            if (_c.WheelSel == _lastSel && _c.WheelItems.Count == _lastCount) return;
            _lastSel = _c.WheelSel; _lastCount = _c.WheelItems.Count;
            foreach (var x in _slots) if (x.bg != null) Destroy(x.bg.gameObject);
            _slots.Clear();
            int n = _c.WheelItems.Count;
            for (int i = 0; i < n; i++)
            {
                var it = _c.WheelItems[i]; bool on = i == _c.WheelSel;
                float ang = Mathf.PI / 2f - i * Mathf.PI * 2f / Mathf.Max(1, n); float r = 180f;
                var c = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
                var bg = UIKit.Slant(_wheel, "Slot" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), c - new Vector2(118, 30), c + new Vector2(118, 30), on ? Pal.A(Pal.MagentaDim, 0.95f) : Pal.A(Pal.Panel2, 0.92f), Pal.A(Pal.Ink, 0.95f), on ? Pal.Gold : Pal.A(Pal.Gold, 0.4f), 10);
                var slot = Concealment.SlotOf(S, it);
                var t = UIKit.Text(bg.transform, "T", $"<size=70%><color=#9A8E7C>{i + 1}</color></size>  {it.Kor}\n<size=68%><color=#B8AC98>{Concealment.SlotWord(Cast.Player, slot)}{(it.Bloody ? " · <color=#C04A55>피</color>" : "")}</color></size>", 19, on ? Color.white : Pal.Text, TextAlignmentOptions.Center, Fonts.Body, Vector2.zero, Vector2.one, new Vector2(6, 2), new Vector2(-6, -2));
                t.lineSpacing = -6;
                _slots.Add((bg, t));
            }
            var sel = _c.WheelSel >= 0 && _c.WheelSel < n ? _c.WheelItems[_c.WheelSel] : null;
            _wheelTitle.text = sel != null ? $"{sel.Kor}\n<size=70%><color=#B8AC98>Q에서 손을 떼면 꺼낸다 · 휠·숫자로 고르기</color></size>" : "";
        }

        void OnDestroy() { if (_cv != null) Destroy(_cv.gameObject); }
    }
}
