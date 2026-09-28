using BL23.Game.Characters;
using BL23.Game.Mansion;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// First-person viewmodel for the player's own hands (the world body is shadow-only while it shows): sleeved forearms
    /// and bare hands in the protagonist's coat and skin colours, with the held weapon. It appears for the player's actions —
    /// a knife / club swing (wind-up → strike → follow-through → recovery), a shove, aiming and firing a gun or the crossbow
    /// (recoil, a slow crossbow crank, reloading), a hold on someone's throat that shakes with their struggle, tying someone
    /// up — with a little walk bob and mouse sway, a sparing FOV kick and camera nudge. Driven from Interaction.
    /// </summary>
    public sealed class FirstPersonHands : MonoBehaviour
    {
        public static FirstPersonHands I;
        /// <summary>The hands are on screen (ActorView keeps the world body shadow-only meanwhile).</summary>
        public static bool Active => I != null && I._vis > 0.02f;
        /// <summary>Probe only: hold the aim pose without a mouse.</summary>
        public static bool ProbeAim;

        enum Mode { None, Swing, Shove, Aim, Reload, Hold, Bind }
        Mode _mode; float _t0, _dur; ActionAnim _swing; string _gunType; bool _aim; Assault _hold;
        Transform _root, _armL, _armR, _handL, _handR, _weapon; string _weaponItem;
        Vector3 _pL, _pR; Quaternion _rL = Quaternion.identity, _rR = Quaternion.identity;
        float _vis, _recoil, _fovKick, _baseFov = -1f; Vector3 _sway; LineRenderer _cord;
        PlayerController _p;

        public static void Ensure(PlayerController p)
        {
            if (p == null || p.Cam == null) return;
            if (I != null && I._p == p) return;
            var go = new GameObject("FirstPersonHands"); go.transform.SetParent(p.Cam.transform, false);
            I = go.AddComponent<FirstPersonHands>(); I._p = p; I.Build();
        }

        // ================================================================== what Interaction asks for
        public static void PlaySwing(ActionAnim kind) { if (I == null) return; I.Begin(Mode.Swing, 0.66f); I._swing = kind; }
        public static void PlayShove() { if (I == null) return; I.Begin(Mode.Shove, 0.45f); }
        public static void PlayBind() { if (I == null) return; I.Begin(Mode.Bind, 1.6f); }
        public static void PlayReload(string gunType, float secs) { if (I == null) return; I._gunType = gunType; I.Begin(Mode.Reload, Mathf.Max(0.8f, secs)); }
        public static void SetAim(bool on, Item gun) { if (I == null) return; I._aim = (on || ProbeAim) && gun != null; if (gun != null) I._gunType = gun.Type; }
        public static void SetHold(Assault x) { if (I == null) return; I._hold = x; }
        /// <summary>A shot went off: the gun kicks up and back, the view jolts (sparingly).</summary>
        public static void Kick(string type)
        {
            if (I == null) return;
            I._recoil = type == "HuntingShotgun" ? 1.4f : type == "Crossbow" ? 0.45f : type == "DuelingPistol" ? 1.1f : 0.85f;
            I._fovKick = type == "HuntingShotgun" ? 3.2f : type == "Crossbow" ? 0.6f : 1.8f;
            if (I._p != null) { I._p.Pitch -= type == "HuntingShotgun" ? 2.2f : type == "Crossbow" ? 0.4f : 1.2f; I._p.Nudge(-I._p.Cam.transform.forward * 0.03f, 0.12f); }
        }
        /// <summary>Where the muzzle is on screen (world), when the hands are up.</summary>
        public static Vector3? MuzzleWorld(Vector3 dir)
        {
            if (I == null || I._weapon == null || !Active) return null;
            float len = I._gunType == "HuntingShotgun" ? 0.78f : I._gunType == "Crossbow" ? 0.45f : I._gunType == "DuelingPistol" ? 0.32f : 0.22f;
            return I._weapon.position + I._weapon.forward * len + I._weapon.up * 0.1f;
        }

        void Begin(Mode m, float dur) { _mode = m; _t0 = Time.time; _dur = dur; }

        // ================================================================== meshes
        void Build()
        {
            MansionMats.Init();
            _root = new GameObject("vm").transform; _root.SetParent(transform, false);
            var look = Cast.Get(Cast.Player)?.Look;
            Color coat = new Color(0.16f, 0.15f, 0.2f), skin = new Color(0.95f, 0.82f, 0.74f);
            if (look != null)
            {
                if (ColorUtility.TryParseHtmlString(look.Skin, out var sk)) skin = sk;
                foreach (var g in look.Wear) if (g != null && !string.IsNullOrEmpty(g.Color) && ColorUtility.TryParseHtmlString(g.Color, out var gc)) { coat = gc; if (g.Kind.ToString().Contains("Coat") || g.Kind.ToString().Contains("Jacket")) break; }
            }
            _armL = Part("sleeveL", SleeveMesh(coat)); _armR = Part("sleeveR", SleeveMesh(coat));
            _handL = Part("handL", HandMesh(skin, true)); _handR = Part("handR", HandMesh(skin, false));
            _cord = new GameObject("cord").AddComponent<LineRenderer>(); _cord.transform.SetParent(_root, false);
            _cord.positionCount = 2; _cord.startWidth = _cord.endWidth = 0.008f; _cord.sharedMaterial = MansionMats.Get(S.Linen); _cord.useWorldSpace = true;
            _cord.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; _cord.enabled = false;
            _pL = new Vector3(-0.24f, -0.55f, 0.3f); _pR = new Vector3(0.24f, -0.55f, 0.3f);
            SetVisible(false);
        }
        Transform Part(string name, Mesh m)
        {
            var go = new GameObject(name); go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = MansionMats.Get(S.Cloth);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            return go.transform;
        }
        static Mesh SleeveMesh(Color c)
        {
            var mb = new MeshBuilder(); mb.Set(S.Cloth, c);
            mb.Push(Vector3.zero, Quaternion.Euler(90, 0, 0), Vector3.one); mb.Cyl(new Vector3(0, -0.42f, 0), 0.05f, 0.42f, 10, false, 0.043f); mb.Pop();   // along -Z from the wrist
            mb.Set(S.Cloth, c * 0.8f); mb.Push(Vector3.zero, Quaternion.Euler(90, 0, 0), Vector3.one); mb.Cyl(new Vector3(0, -0.03f, 0), 0.047f, 0.03f, 10, false); mb.Pop();   // cuff
            return mb.ToMesh("vm_sleeve", out _);
        }
        static Mesh HandMesh(Color skin, bool left)
        {
            var mb = new MeshBuilder(); mb.Set(S.Flesh, skin); float s = left ? -1f : 1f;
            mb.BevelBox(new Vector3(0, 0, 0.045f), new Vector3(0.075f, 0.03f, 0.085f), 0.012f);                 // palm
            for (int i = 0; i < 4; i++) mb.BevelBox(new Vector3(s * (-0.027f + i * 0.018f), -0.006f, 0.1f), new Vector3(0.016f, 0.018f, 0.045f), 0.006f);   // curled fingers
            mb.BevelBox(new Vector3(s * -0.045f, 0.004f, 0.05f), new Vector3(0.018f, 0.018f, 0.05f), 0.006f);   // thumb
            return mb.ToMesh("vm_hand", out _);
        }

        void SetVisible(bool on)
        {
            foreach (var r in _root.GetComponentsInChildren<Renderer>(true)) if (r != _cord) r.enabled = on;
            if (!on && _cord != null) _cord.enabled = false;
        }

        void SyncWeapon()
        {
            var S0 = Session.I?.S; var me = S0?.Player; string id = me?.HandR;
            if (id == _weaponItem) return;
            _weaponItem = id;
            if (_weapon != null) { Destroy(_weapon.gameObject); _weapon = null; }
            var it = S0?.I(id); if (it?.Def == null) return;
            GameObject v = ViolenceProps.Visual(it.Def);
            if (v == null)
            {
                v = MurderProps.Create(it.Def, null) ?? PropFactory.CreateItem(it.Def, null);
                if (v != null) { foreach (var c in v.GetComponentsInChildren<Collider>()) Destroy(c); foreach (var rb in v.GetComponentsInChildren<Rigidbody>()) Destroy(rb); foreach (var pm in v.GetComponentsInChildren<PropMaterial>()) Destroy(pm); }
            }
            if (v == null) return;
            _weapon = v.transform; _weapon.SetParent(_handR, false); _weapon.localPosition = new Vector3(0, 0.0f, 0.045f); _weapon.localRotation = Quaternion.identity;
            foreach (var r in v.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.enabled = _vis > 0.02f; }
        }

        // ================================================================== motion
        void LateUpdate()
        {
            if (_p == null || _p.Cam == null) return;
            bool fp = _p.FirstPerson && _p.Controlling && !_p.Scripted;
            SyncWeapon();
            float now = Time.time, k = _dur > 0 ? Mathf.Clamp01((now - _t0) / _dur) : 1f;
            if (_mode != Mode.None && k >= 1f && _mode != Mode.Aim) _mode = Mode.None;
            bool want = fp && (_mode != Mode.None || _aim || _hold != null && _hold.Active);
            _vis = Mathf.MoveTowards(_vis, want ? 1f : 0f, Time.deltaTime * (want ? 7f : 4f));
            SetVisible(_vis > 0.02f);
            // FOV: a gentle zoom while aiming, a small kick on a shot
            if (_baseFov < 0f) _baseFov = _p.Cam.fieldOfView;
            _fovKick = Mathf.MoveTowards(_fovKick, 0f, Time.deltaTime * 14f);
            float fovWant = (_aim ? _baseFov * 0.84f : _baseFov) + _fovKick;
            if (fp) _p.Cam.fieldOfView = Mathf.Lerp(_p.Cam.fieldOfView, fovWant, 1f - Mathf.Exp(-10f * Time.deltaTime));
            if (_vis <= 0.02f) { _cord.enabled = false; return; }
            // the pose for this moment
            Vector3 L = new Vector3(-0.22f, -0.5f, 0.32f), R = new Vector3(0.22f, -0.5f, 0.32f);
            Quaternion rl = Quaternion.Euler(10, 8, 0), rr = Quaternion.Euler(10, -8, 0);
            bool longGun = _gunType == "HuntingShotgun" || _gunType == "Crossbow";
            if (_hold != null && _hold.Active)
            {
                float s = _hold.Intensity, sh = Mathf.Sin(now * (18f + 10f * s)) * 0.012f * s;
                L = new Vector3(-0.1f + sh, -0.2f, 0.42f); R = new Vector3(0.1f - sh, -0.2f + sh, 0.42f);
                rl = Quaternion.Euler(0, 55, -20); rr = Quaternion.Euler(0, -55, 20);
                bool cord = _weapon != null && Session.I?.S?.I(_weaponItem)?.Def?.Dmg == DamageType.Choke;
                _cord.enabled = cord; if (cord) { _cord.SetPosition(0, _handL.position + _handL.forward * 0.08f); _cord.SetPosition(1, _handR.position + _handR.forward * 0.08f); }
            }
            else
            {
                _cord.enabled = false;
                switch (_mode)
                {
                    case Mode.Swing:
                        {
                            // wind-up 0-.3, strike .3-.6, follow-through .6-.8, recovery .8-1
                            Vector3 wind = _swing == ActionAnim.Overhead ? new Vector3(0.18f, 0.08f, 0.2f) : _swing == ActionAnim.Stab ? new Vector3(0.2f, -0.2f, 0.12f) : new Vector3(0.36f, -0.05f, 0.28f);
                            Vector3 hit = _swing == ActionAnim.Overhead ? new Vector3(0.04f, -0.28f, 0.55f) : _swing == ActionAnim.Stab ? new Vector3(0.02f, -0.12f, 0.68f) : new Vector3(-0.2f, -0.18f, 0.5f);
                            Vector3 thru = _swing == ActionAnim.Overhead ? new Vector3(-0.02f, -0.45f, 0.4f) : _swing == ActionAnim.Stab ? new Vector3(0.05f, -0.16f, 0.6f) : new Vector3(-0.36f, -0.26f, 0.36f);
                            Quaternion rw = Quaternion.Euler(_swing == ActionAnim.Overhead ? -60 : 10, -30, _swing == ActionAnim.Slash ? 60 : 0), rh = Quaternion.Euler(_swing == ActionAnim.Overhead ? 40 : 0, _swing == ActionAnim.Slash ? 40 : -5, 0);
                            if (k < 0.3f) { float u = Ease(k / 0.3f); R = Vector3.Lerp(R, wind, u); rr = Quaternion.Slerp(rr, rw, u); }
                            else if (k < 0.6f) { float u = Mathf.Pow((k - 0.3f) / 0.3f, 0.6f); R = Vector3.Lerp(wind, hit, u); rr = Quaternion.Slerp(rw, rh, u); }
                            else if (k < 0.8f) { float u = Ease((k - 0.6f) / 0.2f); R = Vector3.Lerp(hit, thru, u); rr = Quaternion.Slerp(rh, rh * Quaternion.Euler(20, 0, 0), u); }
                            else { float u = Ease((k - 0.8f) / 0.2f); R = Vector3.Lerp(thru, new Vector3(0.2f, -0.35f, 0.35f), u); }
                            L = new Vector3(-0.2f, -0.36f, 0.3f); break;
                        }
                    case Mode.Shove:
                        {
                            float u = k < 0.25f ? Ease(k / 0.25f) : 1f - Ease((k - 0.25f) / 0.75f);
                            L = Vector3.Lerp(L, new Vector3(-0.13f, -0.15f, 0.58f), u); R = Vector3.Lerp(R, new Vector3(0.13f, -0.15f, 0.58f), u);
                            rl = Quaternion.Slerp(rl, Quaternion.Euler(-70, 0, 0), u); rr = Quaternion.Slerp(rr, Quaternion.Euler(-70, 0, 0), u); break;
                        }
                    case Mode.Bind:
                        {
                            float w = Mathf.Sin(now * 9f) * 0.04f;
                            L = new Vector3(-0.07f + w, -0.36f, 0.45f); R = new Vector3(0.07f - w, -0.34f - w * 0.5f, 0.45f);
                            rl = Quaternion.Euler(50, 40, 0); rr = Quaternion.Euler(50, -40, 0); break;
                        }
                    case Mode.Reload:
                        {
                            R = longGun ? new Vector3(0.08f, -0.3f, 0.42f) : new Vector3(0.04f, -0.24f, 0.42f);
                            rr = longGun ? Quaternion.Euler(28, -10, 0) : Quaternion.Euler(20, -25, 60);
                            if (_gunType == "Crossbow")
                            {
                                // the slow crank: the left hand turns the windlass round and round
                                float a = now * 5.2f; L = new Vector3(-0.02f + Mathf.Cos(a) * 0.05f, -0.28f + Mathf.Sin(a) * 0.05f, 0.3f); rl = Quaternion.Euler(0, 70, 0);
                            }
                            else if (_gunType == "DuelingPistol") { float a = Mathf.PingPong(now * 1.6f, 1f); L = new Vector3(0.0f, -0.12f - a * 0.1f, 0.5f); rl = Quaternion.Euler(-80, 0, 0); }
                            else { float a = Mathf.PingPong(now * 2.4f, 1f); L = new Vector3(-0.02f, -0.24f + a * 0.04f, 0.4f); rl = Quaternion.Euler(20, 50, 0); }
                            break;
                        }
                    default:
                        if (_aim)
                        {
                            if (longGun) { R = new Vector3(0.06f, -0.12f, 0.28f); L = new Vector3(-0.01f, -0.13f, 0.52f); rr = Quaternion.identity; rl = Quaternion.Euler(0, 20, -60); }
                            else { R = new Vector3(0.0f, -0.1f, 0.44f); L = new Vector3(-0.035f, -0.14f, 0.4f); rr = Quaternion.identity; rl = Quaternion.Euler(0, 35, -30); }
                        }
                        break;
                }
            }
            // recoil: up and back, settling fast
            _recoil = Mathf.MoveTowards(_recoil, 0f, Time.deltaTime * 5.5f);
            float rc = _recoil * _recoil;
            R += new Vector3(0, 0.02f * rc, -0.06f * rc); L += new Vector3(0, 0.015f * rc, -0.045f * rc);
            rr = Quaternion.Euler(-14f * rc, 0, 0) * rr;
            // sway and bob
            float mx = Input.GetAxis("Mouse X"), my = Input.GetAxis("Mouse Y");
            _sway = Vector3.Lerp(_sway, new Vector3(-mx * 0.012f, -my * 0.01f, 0f), 1f - Mathf.Exp(-8f * Time.deltaTime));
            float speed = new Vector2(_p.Velocity.x, _p.Velocity.z).magnitude, bt = now * (6f + speed * 1.4f);
            var bob = new Vector3(Mathf.Cos(bt * 0.5f) * 0.006f, Mathf.Abs(Mathf.Sin(bt * 0.5f)) * 0.008f, 0f) * Mathf.Clamp01(speed / 3f) * (_aim ? 0.35f : 1f);
            L += _sway + bob; R += _sway + bob;
            // hidden below the view while fading in / out
            float hide = 1f - _vis; L += Vector3.down * 0.4f * hide; R += Vector3.down * 0.4f * hide;
            float sm = 1f - Mathf.Exp(-(_mode == Mode.Swing ? 30f : 16f) * Time.deltaTime);
            _pL = Vector3.Lerp(_pL, L, sm); _pR = Vector3.Lerp(_pR, R, sm); _rL = Quaternion.Slerp(_rL, rl, sm); _rR = Quaternion.Slerp(_rR, rr, sm);
            Pose(_handL, _armL, _pL, _rL, -1f); Pose(_handR, _armR, _pR, _rR, 1f);
        }

        /// <summary>Hand at p (camera space) facing r; the sleeve runs from an elbow below and to the side up to the wrist.</summary>
        void Pose(Transform hand, Transform sleeve, Vector3 p, Quaternion r, float side)
        {
            hand.localPosition = p; hand.localRotation = r;
            var elbow = p + new Vector3(side * 0.12f, -0.22f, -0.3f);
            var dir = p - elbow;
            sleeve.localPosition = p; sleeve.localRotation = Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward, Vector3.up);
            sleeve.localScale = new Vector3(1f, 1f, Mathf.Clamp(dir.magnitude / 0.42f, 0.6f, 1.6f));
        }
        static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    }
}
