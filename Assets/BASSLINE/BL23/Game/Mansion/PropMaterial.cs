using System;
using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Physical material of a furniture piece or item. Collisions above the material threshold produce
    /// material-specific results (glass/ceramic shatter into fragments, wood splinters and cracks, metal dents,
    /// paper crumples, cloth tears...) and raise <see cref="OnDamaged"/> for the game layer.
    /// </summary>
    public sealed class PropMaterial : MonoBehaviour
    {
        public Mat Mat = Mat.Wood;
        public bool Fragile;
        public int Damage;                 // 0 intact .. 3 destroyed
        public int FurnitureId = -1;
        public string ItemId;
        /// <summary>Scale on the impulse thresholds (heavier props are sturdier).</summary>
        public float Toughness = 1f;
        /// <summary>When false, collisions only raise events (no visual change).</summary>
        public bool AutoVisuals = true;

        /// <summary>(prop, newDamage 0..3, contact point, impulse N*s)</summary>
        public static event Action<PropMaterial, int, Vector3, float> OnDamaged;

        float _cool;
        float _born = -1f;
        /// <summary>Ignore impacts for a moment (a prop being woken, a body settling): settling is not breaking.</summary>
        public void Settle(float seconds) { _cool = Mathf.Max(_cool, Time.time + seconds); }

        /// <summary>Impulse (N*s) needed for one damage step.</summary>
        public static float Threshold(Mat m)
        {
            switch (m)
            {
                case Mat.Glass: return 2.0f;
                case Mat.Ceramic: return 2.6f;
                case Mat.Food: return 2.5f;
                case Mat.Plant: return 3.5f;
                case Mat.Paper: return 5f;
                case Mat.Flesh: return 5f;
                case Mat.Plastic: return 6f;
                case Mat.Cloth: return 8f;
                case Mat.Leather: return 9f;
                case Mat.Wood: return 10f;
                case Mat.Metal: return 16f;
                case Mat.Stone: return 30f;
                case Mat.Liquid: return float.MaxValue;
            }
            return 10f;
        }

        static int _probeLog = -1;
        void OnEnable() { _born = Time.time; }   // spawned, re-enabled or respawned: the first second is settling, not breaking
        void Start() { if (Damage < 3) Physicality.PhysicalProp.Ensure(gameObject); }

        void OnCollisionEnter(Collision col)
        {
            if (_born < 0f) _born = Time.time;
            if (Time.time - MansionView.LastBuildTime < 4f || Time.time - _born < 1.0f) return;   // the house settling at load is not a break
            if (Time.time < _cool) return;
            var physical = GetComponent<Physicality.PhysicalProp>();
            if (physical != null) { physical.HandleCollision(col); return; }
            // resting contacts, a nudge, a prop dropped a few centimetres onto its shelf: nothing that slow breaks anything
            // (a fall from table height arrives at ~4 m/s, a throw faster)
            float rel = col.relativeVelocity.magnitude;
            if (rel < 2.2f) return;
            float imp = col.impulse.magnitude;
            float thr = Threshold(Mat) * Toughness * (Fragile ? 0.7f : 1f);
            if (imp < thr) return;
            if (_probeLog < 0) _probeLog = Array.IndexOf(Environment.GetCommandLineArgs(), "-bl23probe") >= 0 ? 1 : 0;
            if (_probeLog == 1) Debug.Log($"[PropBreak] {name} item={ItemId} furn={FurnitureId} imp={imp:0.0} thr={thr:0.0} rel={rel:0.0} other={col.collider.name} t={Time.time:0.0}");
            var p = col.contactCount > 0 ? col.GetContact(0).point : transform.position;
            var n = col.contactCount > 0 ? col.GetContact(0).normal : Vector3.up;
            int steps = imp > thr * 4f ? 3 : imp > thr * 2f ? 2 : 1;
            ApplyDamage(p, n, imp, steps);
        }

        /// <summary>Damage by an external cause (weapon hit, script). Returns the new damage level.</summary>
        public int ApplyDamage(Vector3 point, Vector3 normal, float impulse, int steps = 1)
        {
            _cool = Time.time + 0.15f;
            int before = Damage;
            Damage = Mathf.Clamp(Damage + Mathf.Max(1, steps), 0, 3);
            if (Damage == before) return Damage;
            if (AutoVisuals) Result(point, normal, impulse, before);
            GetComponent<Physicality.PhysicalProp>()?.NotifyDamageChanged();
            OnDamaged?.Invoke(this, Damage, point, impulse);
            return Damage;
        }

        void Result(Vector3 p, Vector3 n, float impulse, int before)
        {
            var fv = GetComponent<FurnitureView>();
            var b = WorldBounds();
            switch (Mat)
            {
                case Mat.Glass:
                case Mat.Ceramic:
                    if (Damage >= 2 || Fragile)
                    {
                        Shatter(b.center, b.size, Mat, Mathf.Clamp((int)(b.size.magnitude * 20), 6, 18));
                        TraceFactory.Create("Fragment", new Vector3(b.center.x, FloorBelow(b.center), b.center.z), Vector3.up, Mathf.Max(0.35f, b.size.x * 1.5f), Mat == Mat.Glass ? new Color(0.8f, 0.9f, 1f, 0.9f) : new Color(0.95f, 0.93f, 0.9f, 0.9f));
                        if (fv == null) { HideVisuals(); Damage = 3; }
                    }
                    else TraceFactory.Create("Crack", p + n * 0.002f, n, 0.25f, new Color(0.9f, 0.95f, 1f, 0.8f));
                    break;
                case Mat.Wood:
                    TraceFactory.Create(Damage >= 2 ? "Crack" : "Scratch", p + n * 0.002f, n, Damage >= 2 ? 0.4f : 0.25f, new Color(0.25f, 0.15f, 0.08f, 0.85f));
                    if (Damage >= 2) Splinters(p, n, 5 + Damage * 2);
                    break;
                case Mat.Metal:
                    TraceFactory.Create("Dent", p + n * 0.002f, n, 0.12f + Mathf.Min(0.2f, impulse * 0.004f), new Color(0.2f, 0.2f, 0.22f, 0.6f));
                    Squash(n, 0.03f * Damage);
                    break;
                case Mat.Paper:
                    Crumple();
                    break;
                case Mat.Cloth:
                case Mat.Leather:
                    TraceFactory.Create("Scratch", p + n * 0.002f, n, 0.2f, new Color(0.1f, 0.08f, 0.08f, 0.8f));
                    Squash(Vector3.up, 0.05f * Damage);
                    break;
                case Mat.Stone:
                    TraceFactory.Create("Crack", p + n * 0.002f, n, 0.35f, new Color(0.15f, 0.14f, 0.14f, 0.85f));
                    if (Damage >= 2) Chips(p, n, 4);
                    break;
                case Mat.Plastic:
                    TraceFactory.Create("Scratch", p + n * 0.002f, n, 0.15f, new Color(0.9f, 0.9f, 0.9f, 0.6f));
                    Squash(n, 0.04f * Damage);
                    break;
                case Mat.Food:
                case Mat.Plant:
                case Mat.Flesh:
                    TraceFactory.Create(Mat == Mat.Flesh ? "BloodSmear" : "Soil", new Vector3(p.x, FloorBelow(p), p.z), Vector3.up, 0.3f, Mat == Mat.Flesh ? new Color(0.35f, 0.02f, 0.03f, 0.9f) : Mat == Mat.Plant ? new Color(0.2f, 0.3f, 0.1f, 0.9f) : new Color(0.55f, 0.4f, 0.25f, 0.9f));
                    Squash(Vector3.up, 0.15f * Damage);
                    break;
            }
            if (fv != null) fv.SetDamage(Damage);
        }

        Bounds WorldBounds()
        {
            var rs = GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(transform.position, Vector3.one * 0.2f);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        static float FloorBelow(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 0.05f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore)) return hit.point.y + 0.003f;
            return p.y;
        }

        void HideVisuals()
        {
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var l in GetComponentsInChildren<Light>()) if (l.gameObject != gameObject) Destroy(l.gameObject); else l.enabled = false;   // a smashed lamp goes dark (the light GO carries URP data)
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            var rb = GetComponent<Rigidbody>(); if (rb != null) rb.isKinematic = true;
        }

        void Squash(Vector3 worldDir, float amount)
        {
            if (amount <= 0) return;
            var local = transform.InverseTransformDirection(worldDir).normalized;
            var s = transform.localScale;
            s -= Vector3.Scale(s, new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z))) * amount;
            transform.localScale = s;
        }

        void Crumple()
        {
            // replace the paper visual with a crumpled ball of the same volume
            var b = WorldBounds();
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            var mb = new MeshBuilder();
            mb.Set(S.Paper, Color.white);
            float r0 = Mathf.Max(0.03f, Mathf.Pow(b.size.x * b.size.y * b.size.z + 1e-5f, 1f / 3f) * 0.6f);
            var rnd = new System.Random((int)(transform.position.x * 1000 + transform.position.z * 7919));
            for (int i = 0; i < 10; i++)
            {
                var dir = new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f).normalized;
                mb.Push(dir * r0 * 0.4f, Quaternion.LookRotation(dir), Vector3.one);
                mb.Tri(new Vector3(-r0 * 0.7f, 0, 0), new Vector3(0, r0 * 0.7f, 0.1f * r0), new Vector3(r0 * 0.7f, -r0 * 0.2f, 0));
                mb.Tri(new Vector3(r0 * 0.7f, -r0 * 0.2f, 0), new Vector3(0, r0 * 0.7f, 0.1f * r0), new Vector3(-r0 * 0.7f, 0, 0));
                mb.Pop();
            }
            var go = new GameObject("crumpled"); go.transform.SetParent(transform, false);
            go.transform.position = b.center;
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Crumple", out var slots);
            go.AddComponent<MeshRenderer>().sharedMaterials = MansionMats.Materials(slots);
        }

        // ------------------------------------------------------------------ fragments
        static Mesh[] _shards;
        static readonly Queue<GameObject> _live = new Queue<GameObject>();
        const int MaxFragments = 240;

        static void EnsureShards()
        {
            if (_shards != null) return;
            _shards = new Mesh[6];
            var rnd = new System.Random(77);
            for (int k = 0; k < _shards.Length; k++)
            {
                var mb = new MeshBuilder();
                var pts = new List<Vector2>();
                int n = 3 + rnd.Next(3);
                for (int i = 0; i < n; i++) { float a = i / (float)n * Mathf.PI * 2 + (float)rnd.NextDouble() * 0.6f; float r = 0.5f + (float)rnd.NextDouble() * 0.5f; pts.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r)); }
                pts.Reverse();
                mb.Prism(pts, -0.08f, 0.08f, true, true);
                _shards[k] = mb.ToMesh("Shard" + k, out _);
            }
        }

        /// <summary>Spawn rigidbody shards around a centre (glass / ceramic / stone).</summary>
        public static void Shatter(Vector3 center, Vector3 size, Mat mat, int count)
        {
            EnsureShards();
            int slot = mat == Mat.Glass ? S.Glass : mat == Mat.Ceramic ? S.Porcelain : mat == Mat.Stone ? S.StoneFloor : S.WoodLight;
            var m = MansionMats.Get(slot);
            var rnd = new System.Random(center.GetHashCode());
            float s = Mathf.Clamp(size.magnitude * 0.12f, 0.025f, 0.12f);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Fragment");
                go.transform.position = center + new Vector3(((float)rnd.NextDouble() - 0.5f) * size.x, ((float)rnd.NextDouble() - 0.5f) * size.y, ((float)rnd.NextDouble() - 0.5f) * size.z) * 0.6f;
                go.transform.rotation = Quaternion.Euler((float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f);
                go.transform.localScale = Vector3.one * s * (0.5f + (float)rnd.NextDouble());
                go.AddComponent<MeshFilter>().sharedMesh = _shards[rnd.Next(_shards.Length)];
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = ShadowCastingMode.Off;
                var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(1.4f, 0.16f, 1.4f);
                var rb = go.AddComponent<Rigidbody>(); rb.mass = 0.02f; rb.linearDamping = 0.5f; rb.angularDamping = 0.5f;
                rb.linearVelocity = new Vector3(((float)rnd.NextDouble() - 0.5f) * 2.5f, (float)rnd.NextDouble() * 1.5f, ((float)rnd.NextDouble() - 0.5f) * 2.5f);
                rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 10f;
                var pm = go.AddComponent<PropMaterial>(); pm.Mat = mat; pm.Damage = 3; pm.AutoVisuals = false; pm.ItemId = "fragment";
                _live.Enqueue(go);
                while (_live.Count > MaxFragments) { var old = _live.Dequeue(); if (old != null) Destroy(old); }
            }
        }

        static void Splinters(Vector3 p, Vector3 n, int count)
        {
            var m = MansionMats.Get(S.WoodLight);
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Splinter";
                go.GetComponent<Renderer>().sharedMaterial = m;
                go.transform.position = p + n * 0.05f + UnityEngine.Random.insideUnitSphere * 0.05f;
                go.transform.localScale = new Vector3(0.012f, 0.012f, 0.05f + UnityEngine.Random.value * 0.08f);
                go.transform.rotation = UnityEngine.Random.rotation;
                var rb = go.AddComponent<Rigidbody>(); rb.mass = 0.01f; rb.linearVelocity = (n + UnityEngine.Random.insideUnitSphere) * 1.5f;
                _live.Enqueue(go);
            }
        }

        static void Chips(Vector3 p, Vector3 n, int count)
        {
            var m = MansionMats.Get(S.StoneFloor);
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Chip";
                go.GetComponent<Renderer>().sharedMaterial = m;
                go.transform.position = p + n * 0.05f;
                go.transform.localScale = Vector3.one * (0.02f + UnityEngine.Random.value * 0.03f);
                go.transform.rotation = UnityEngine.Random.rotation;
                var rb = go.AddComponent<Rigidbody>(); rb.mass = 0.05f; rb.linearVelocity = (n + UnityEngine.Random.insideUnitSphere) * 1.2f;
                _live.Enqueue(go);
            }
        }
    }
}
