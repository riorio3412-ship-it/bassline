using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>Rope loops at the wrists / ankles and a cloth gag, generated once per actor and attached to the bones.</summary>
    public sealed class RestraintVisuals : MonoBehaviour
    {
        ActorRig _rig;
        GameObject _wrists, _ankles, _gag;
        static Material _rope, _cloth;

        public static RestraintVisuals Ensure(ActorRig rig)
        {
            if (rig == null) return null;
            var v = rig.GetComponent<RestraintVisuals>();
            if (v == null) { v = rig.gameObject.AddComponent<RestraintVisuals>(); v._rig = rig; }
            return v;
        }

        public void Set(RestraintFlags f)
        {
            if (_rig == null) _rig = GetComponent<ActorRig>();
            float s = _rig.Height / 1.75f;
            bool wrists = (f & (RestraintFlags.WristsFront | RestraintFlags.WristsBack)) != 0;
            if (wrists && _wrists == null)
            {
                _wrists = new GameObject("Rope_Wrists"); _wrists.transform.SetParent(transform, false);
                LimbRing(_wrists.transform, HBone.LowerArmL, HBone.HandL, 0.042f * s, 0.011f * s, -0.018f * s, RopeMat());
                LimbRing(_wrists.transform, HBone.LowerArmR, HBone.HandR, 0.042f * s, 0.011f * s, -0.018f * s, RopeMat());
            }
            if (_wrists != null) _wrists.SetActive(wrists);
            bool ankles = (f & RestraintFlags.Ankles) != 0;
            if (ankles && _ankles == null)
            {
                _ankles = new GameObject("Rope_Ankles"); _ankles.transform.SetParent(transform, false);
                LimbRing(_ankles.transform, HBone.LowerLegL, HBone.FootL, 0.05f * s, 0.012f * s, -0.055f * s, RopeMat());
                LimbRing(_ankles.transform, HBone.LowerLegR, HBone.FootR, 0.05f * s, 0.012f * s, -0.055f * s, RopeMat());
            }
            if (_ankles != null) _ankles.SetActive(ankles);
            bool gag = (f & RestraintFlags.Gag) != 0;
            if (gag && _gag == null) MakeGag(s);
            if (_gag != null) _gag.SetActive(gag);
        }

        /// <summary>A loop around a limb near its distal joint, parented to the proximal bone (so wrist / ankle bends don't tilt it).</summary>
        void LimbRing(Transform group, HBone proximal, HBone distal, float radius, float thick, float along, Material mat)
        {
            Transform a = _rig.Bone(proximal), b = _rig.Bone(distal);
            if (a == null || b == null) return;
            Vector3 axis = (b.position - a.position).normalized;
            var go = MakeRing(proximal + "_rope", radius, thick, mat);
            go.transform.SetParent(a, false);
            go.transform.SetPositionAndRotation(b.position + axis * along, Quaternion.FromToRotation(Vector3.up, axis));
            // keep a handle so the group toggles it
            go.AddComponent<RestraintPart>().Group = group.gameObject;
        }

        void MakeGag(float s)
        {
            var head = _rig.Bone(HBone.Head); var neck = _rig.Bone(HBone.Neck);
            if (head == null) return;
            // the head's canonical frame (works for any pose: standing, lying, slumped)
            Quaternion hr = _rig.Anim != null ? _rig.Anim.CanonicalRotation(HBone.Head) : head.rotation;
            Vector3 up = hr * Vector3.up, fwd = hr * Vector3.forward;
            Vector3 eye = _rig.EyeAnchor != null ? _rig.EyeAnchor.position : head.position + up * 0.08f * s + fwd * 0.08f * s;
            Vector3 centre = head.position + up * Vector3.Dot(eye - head.position, up);   // head axis at eye height
            _gag = MakeRing("Gag", 0.085f * s, 0.013f * s, ClothMat());
            _gag.transform.SetParent(head, false);
            _gag.transform.SetPositionAndRotation(centre - up * 0.05f * s + fwd * 0.016f * s, Quaternion.LookRotation(fwd, up) * Quaternion.Euler(12f, 0f, 0f));
            _gag.transform.localScale = new Vector3(0.077f * s, 0.11f * s, 0.092f * s);
        }

        static GameObject MakeRing(string name, float radius, float thick, Material mat)
        {
            var go = new GameObject(name);
            go.transform.localScale = Vector3.one * radius;
            go.AddComponent<MeshFilter>().sharedMesh = RingMesh(thick / Mathf.Max(1e-3f, radius));
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static Material RopeMat() => _rope != null ? _rope : (_rope = MakeMat(new Color(0.55f, 0.43f, 0.28f)));
        static Material ClothMat() => _cloth != null ? _cloth : (_cloth = MakeMat(new Color(0.8f, 0.76f, 0.68f)));
        static Material MakeMat(Color c)
        {
            var sh = Shader.Find("BL23/ToonCharacter");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(sh);
            m.SetColor("_BaseColor", c);
            if (m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", 0.4f);
            return m;
        }

        static readonly System.Collections.Generic.Dictionary<int, Mesh> _rings = new System.Collections.Generic.Dictionary<int, Mesh>();
        static Mesh RingMesh(float tube)
        {
            int key = Mathf.RoundToInt(tube * 1000f);
            if (_rings.TryGetValue(key, out var m) && m != null) return m;
            const int seg = 20, side = 8;
            var v = new Vector3[seg * side]; var n = new Vector3[seg * side]; var tri = new int[seg * side * 6];
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vector3 c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int j = 0; j < side; j++)
                {
                    float b = j * Mathf.PI * 2f / side;
                    Vector3 dir = c * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    v[i * side + j] = c + dir * tube; n[i * side + j] = dir;
                    int i2 = (i + 1) % seg, j2 = (j + 1) % side, k = (i * side + j) * 6;
                    tri[k] = i * side + j; tri[k + 1] = i * side + j2; tri[k + 2] = i2 * side + j;
                    tri[k + 3] = i2 * side + j; tri[k + 4] = i * side + j2; tri[k + 5] = i2 * side + j2;
                }
            }
            m = new Mesh { name = "RestraintRing" };
            m.vertices = v; m.normals = n; m.triangles = tri; m.RecalculateBounds();
            _rings[key] = m;
            return m;
        }
    }
}
