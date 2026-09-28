using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>Per-furniture runtime state (damage visuals).</summary>
    public sealed class FurnitureView : MonoBehaviour
    {
        public int Id; public string Type; public int Damage;
        MansionView _view; Quaternion _visRot; readonly List<GameObject> _marks = new List<GameObject>();
        FurnitureDef _def;

        internal void Init(MansionView v, Furniture f, FurnitureDef def)
        {
            _view = v; Id = f.Id; Type = f.Type; _def = def; Damage = 0;
            if (f.Damage > 0) SetDamage(f.Damage);
        }

        /// <summary>0 intact .. 3 broken. Adds scratches / cracks, tilts and (fragile) shatters the visual.</summary>
        public void SetDamage(int d)
        {
            d = Mathf.Clamp(d, 0, 3);
            if (d == Damage) return;
            foreach (var m in _marks) if (m != null) Destroy(m);
            _marks.Clear();
            Damage = d;
            var material = GetComponent<PropMaterial>();
            if (material != null) material.Damage = d;
            GetComponent<Physicality.PhysicalProp>()?.NotifyDamageChanged();
            var vis = transform.Find("vis");
            var bounds = new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one);
            var rends = GetComponentsInChildren<Renderer>();
            if (rends.Length > 0) { bounds = rends[0].bounds; foreach (var r in rends) bounds.Encapsulate(r.bounds); }
            var top = new Vector3(bounds.center.x, bounds.max.y + 0.002f, bounds.center.z);
            var front = bounds.center + transform.forward * (bounds.extents.z + 0.005f);
            if (d >= 1) _marks.Add(TraceFactory.Create("Scratch", top, Vector3.up, Mathf.Min(0.6f, bounds.size.x * 0.5f), new Color(0.85f, 0.8f, 0.7f, 0.7f)));
            if (d >= 2) _marks.Add(TraceFactory.Create("Crack", front, transform.forward, Mathf.Min(0.8f, bounds.size.y * 0.5f), new Color(0.1f, 0.08f, 0.08f, 0.9f)));
            if (vis != null)
            {
                if (_visRot == default) _visRot = vis.localRotation;
                vis.localRotation = d >= 2 ? _visRot * Quaternion.Euler((Id % 2 == 0 ? 1 : -1) * (d == 3 ? 9f : 3f), 0, (Id % 3 - 1) * (d == 3 ? 7f : 2f)) : _visRot;
            }
            if (d == 3 && _def != null && (_def.Fragile || _def.Mat == Mat.Glass || _def.Mat == Mat.Ceramic))
            {
                PropMaterial.Shatter(bounds.center, bounds.size, _def.Mat, 14);
                _marks.Add(TraceFactory.Create("Fragment", new Vector3(bounds.center.x, transform.position.y + 0.004f, bounds.center.z), Vector3.up, Mathf.Max(0.6f, bounds.size.x), new Color(0.8f, 0.85f, 0.9f, 0.9f)));
            }
        }
    }

    /// <summary>Hydraulic press ram (MachineRoom). SetPress(ram01, powered) drives it smoothly.</summary>
    public sealed class PressView : MonoBehaviour
    {
        Transform _ram; float _bed, _top, _target, _y; GameObject _beacon; Light _beaconLight; bool _powered;
        public float Ram01 { get; private set; }

        internal void Init(Transform ram, float bedY, float topY, GameObject beacon, Light beaconLight)
        {
            _ram = ram; _bed = bedY; _top = topY; _y = topY; _target = topY; _beacon = beacon; _beaconLight = beaconLight;
            Set(0, false); Snap();
        }

        public void Set(float ram01, bool powered)
        {
            Ram01 = Mathf.Clamp01(ram01);
            _target = Mathf.Lerp(_top, _bed, Ram01);
            _powered = powered;
            if (_beacon != null) _beacon.SetActive(powered);
            if (_beaconLight != null) _beaconLight.color = powered ? new Color(1f, 0.25f, 0.05f) : new Color(0.2f, 0.05f, 0.02f);
        }

        public void Snap() { _y = _target; if (_ram != null) _ram.localPosition = new Vector3(0, _y, 0); }

        void Update()
        {
            if (_ram == null) return;
            _y = Mathf.MoveTowards(_y, _target, Time.deltaTime * (_target < _y ? 0.9f : 1.6f));
            _ram.localPosition = new Vector3(0, _y, 0);
            if (_powered && _beacon != null) _beacon.transform.Rotate(0, 360f * Time.deltaTime, 0);
        }
    }

    /// <summary>Power room switchboard: one lever + indicator pair per circuit.</summary>
    public sealed class SwitchboardView : MonoBehaviour
    {
        readonly Dictionary<int, (Transform lever, GameObject on, GameObject off)> _l = new Dictionary<int, (Transform, GameObject, GameObject)>();
        internal void Add(int circuit, Transform lever, GameObject on, GameObject off) { _l[circuit] = (lever, on, off); SetLever(circuit, true); }
        public void SetLever(int circuit, bool on)
        {
            if (!_l.TryGetValue(circuit, out var e)) return;
            if (e.lever != null) e.lever.localRotation = Quaternion.Euler(on ? -35f : 35f, 0, 0);
            if (e.on != null) e.on.SetActive(on);
            if (e.off != null) e.off.SetActive(!on);
        }
    }

    /// <summary>Pendulum / hanging swing.</summary>
    public sealed class Swinger : MonoBehaviour
    {
        public float Amplitude = 10f, Period = 2f, Phase; public Vector3 Axis = Vector3.forward;
        Quaternion _base; bool _init;
        void Update()
        {
            if (!_init) { _base = transform.localRotation; _init = true; }
            float a = Mathf.Sin((Time.time + Phase) * Mathf.PI * 2f / Mathf.Max(0.1f, Period)) * Amplitude;
            transform.localRotation = _base * Quaternion.AngleAxis(a, Axis);
        }
    }

    /// <summary>Slow spinner (fans, clocks, beacons).</summary>
    public sealed class Spinner : MonoBehaviour
    {
        public Vector3 Axis = Vector3.up; public float Speed = 30f;
        void Update() { transform.Rotate(Axis, Speed * Time.deltaTime, Space.Self); }
    }

    /// <summary>
    /// Goldfish swimming on smooth loops inside a local box (aquariums) or through the air (Yusti's motif).
    /// One component animates the whole school; fish meshes are shared.
    /// </summary>
    public sealed class FishSchool : MonoBehaviour
    {
        struct Fish { public Transform T; public Vector3 A, B, C; public float Speed, Phase, Tail; }
        readonly List<Fish> _fish = new List<Fish>();
        Vector3 _extents;
        static Mesh _body, _tail;

        public void Build(Vector3 extents, int count, float size, int seed, MansionPalette pal)
        {
            _extents = extents;
            EnsureMeshes();
            var rnd = new System.Random(seed * 17 + 3);
            var matBody = MansionMats.Get(S.GlossPaint);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("fish" + i); go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * size * (0.8f + (float)rnd.NextDouble() * 0.5f);
                var body = new GameObject("body"); body.transform.SetParent(go.transform, false);
                body.AddComponent<MeshFilter>().sharedMesh = _body;
                var mr = body.AddComponent<MeshRenderer>(); mr.sharedMaterial = MansionMats.Get(S.GlossPaint); mr.shadowCastingMode = ShadowCastingMode.Off;
                var tail = new GameObject("tail"); tail.transform.SetParent(go.transform, false); tail.transform.localPosition = new Vector3(0, 0, -0.9f);
                tail.AddComponent<MeshFilter>().sharedMesh = _tail;
                var tr = tail.AddComponent<MeshRenderer>(); tr.sharedMaterial = MansionMats.Get(S.GlossPaint); tr.shadowCastingMode = ShadowCastingMode.Off;
                _fish.Add(new Fish
                {
                    T = go.transform,
                    A = new Vector3((float)rnd.NextDouble() * 0.9f + 0.3f, (float)rnd.NextDouble() * 0.5f + 0.2f, (float)rnd.NextDouble() * 0.9f + 0.3f),
                    B = new Vector3((float)rnd.NextDouble() * 6.28f, (float)rnd.NextDouble() * 6.28f, (float)rnd.NextDouble() * 6.28f),
                    C = new Vector3(1f + (float)rnd.NextDouble(), 1.3f + (float)rnd.NextDouble(), 0.7f + (float)rnd.NextDouble()),
                    Speed = 0.12f + (float)rnd.NextDouble() * 0.1f, Phase = (float)rnd.NextDouble() * 100f, Tail = 6f + (float)rnd.NextDouble() * 4f
                });
            }
            Step(0f);
        }

        static void EnsureMeshes()
        {
            if (_body != null) return;
            var mb = new MeshBuilder();
            // body: orange with white belly via vertex colors
            mb.Set(S.GlossPaint, new Color(1f, 0.42f, 0.08f));
            mb.Push(Vector3.zero, Quaternion.Euler(90, 0, 0), new Vector3(0.5f, 1f, 0.7f));
            mb.Lathe(new[] { new Vector2(0.001f, -0.9f), new Vector2(0.25f, -0.6f), new Vector2(0.42f, -0.1f), new Vector2(0.4f, 0.3f), new Vector2(0.25f, 0.65f), new Vector2(0.001f, 0.85f) }, 10);
            mb.Pop();
            mb.Set(S.GlossPaint, new Color(1f, 1f, 0.95f));
            mb.Sphere(new Vector3(0.18f, 0.12f, 0.55f), 0.07f, 6, 4); mb.Sphere(new Vector3(-0.18f, 0.12f, 0.55f), 0.07f, 6, 4);
            mb.Set(S.GlossPaint, new Color(0.02f, 0.02f, 0.02f));
            mb.Sphere(new Vector3(0.2f, 0.12f, 0.58f), 0.035f, 5, 3); mb.Sphere(new Vector3(-0.2f, 0.12f, 0.58f), 0.035f, 5, 3);
            mb.Set(S.GlossPaint, new Color(1f, 0.55f, 0.2f));
            mb.Quad(new Vector3(0, 0.25f, -0.3f), new Vector3(0, 0.55f, -0.1f), new Vector3(0, 0.3f, 0.2f), new Vector3(0, 0.25f, 0.1f));
            mb.Quad(new Vector3(0, 0.25f, 0.1f), new Vector3(0, 0.3f, 0.2f), new Vector3(0, 0.55f, -0.1f), new Vector3(0, 0.25f, -0.3f));
            _body = mb.ToMesh("FishBody", out _);
            var tb = new MeshBuilder();
            tb.Set(S.GlossPaint, new Color(1f, 0.5f, 0.15f));
            tb.Quad(new Vector3(0, 0, 0.1f), new Vector3(0, 0.45f, -0.55f), new Vector3(0, 0, -0.35f), new Vector3(0, -0.45f, -0.55f));
            tb.Quad(new Vector3(0, -0.45f, -0.55f), new Vector3(0, 0, -0.35f), new Vector3(0, 0.45f, -0.55f), new Vector3(0, 0, 0.1f));
            _tail = tb.ToMesh("FishTail", out _);
        }

        void Update() { Step(Time.time); }

        void Step(float t)
        {
            for (int i = 0; i < _fish.Count; i++)
            {
                var f = _fish[i];
                float u = (t + f.Phase) * f.Speed;
                Vector3 P(float uu) => new Vector3(Mathf.Sin(uu * f.C.x + f.B.x) * f.A.x * _extents.x, Mathf.Sin(uu * f.C.y + f.B.y) * f.A.y * _extents.y, Mathf.Sin(uu * f.C.z + f.B.z) * f.A.z * _extents.z) * 0.8f;
                var p = P(u); var q = P(u + 0.02f);
                f.T.localPosition = p;
                var d = q - p; if (d.sqrMagnitude > 1e-8f) f.T.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
                var tail = f.T.childCount > 1 ? f.T.GetChild(1) : null;
                if (tail != null) tail.localRotation = Quaternion.Euler(0, Mathf.Sin((t + f.Phase) * f.Tail) * 25f, 0);
            }
        }
    }
}
