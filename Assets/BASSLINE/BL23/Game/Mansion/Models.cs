using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>Loads converted CC0 models (Resources/Mansion/Models) and places them fitted to a footprint.</summary>
    public static class Models
    {
        static readonly Dictionary<string, MansionModel> _cache = new Dictionary<string, MansionModel>();
        public enum Anchor { Bottom, Center, Top }

        public static MansionModel Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_cache.TryGetValue(id, out var m)) return m;
            m = Resources.Load<MansionModel>("Mansion/Models/" + id);
            if (m != null && m.Mesh == null) m = null;
            _cache[id] = m;
            return m;
        }

        /// <summary>Uniform scale that fits the model bounds into 'fit' (components &lt;= 0 are ignored).</summary>
        public static float FitScale(MansionModel m, Vector3 fit, bool rotated90 = false)
        {
            var s = m.Bounds.size;
            if (rotated90) s = new Vector3(s.z, s.y, s.x);
            float k = float.MaxValue;
            if (fit.x > 0 && s.x > 1e-4f) k = Mathf.Min(k, fit.x / s.x);
            if (fit.y > 0 && s.y > 1e-4f) k = Mathf.Min(k, fit.y / s.y);
            if (fit.z > 0 && s.z > 1e-4f) k = Mathf.Min(k, fit.z / s.z);
            return k == float.MaxValue ? 1f : k;
        }

        /// <summary>
        /// Instantiate a model. 'fit' is the target box (uniform fit); if 'stretch' > 0 the non-uniform ratio toward the box
        /// is allowed up to that fraction (0.15 = +-15%). pos is bottom-centre / centre / top-centre depending on anchor.
        /// </summary>
        public static GameObject Place(MansionModel m, Transform parent, Vector3 pos, float yaw, Vector3 fit, Color? tint, bool center = false,
                                       Anchor anchor = Anchor.Bottom, float stretch = 0.12f, float recolor = -1f, float modelYaw = 0f)
        {
            if (m == null) return null;
            if (center) anchor = Anchor.Center;
            var go = new GameObject(m.name);
            go.transform.SetParent(parent, false);
            var holder = new GameObject("mesh"); holder.transform.SetParent(go.transform, false);
            holder.AddComponent<MeshFilter>().sharedMesh = m.Mesh;
            var mr = holder.AddComponent<MeshRenderer>();
            var mats = new Material[m.Parts.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = MansionMats.ModelPart(m, i, tint, recolor);
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = ShadowCastingMode.On;
            // scale: uniform fit, then allow a little stretch toward the footprint
            var b = m.Bounds;
            bool rot = Mathf.Abs(Mathf.Repeat(modelYaw, 180f) - 90f) < 1f;
            var bs = rot ? new Vector3(b.size.z, b.size.y, b.size.x) : b.size;
            float k = FitScale(m, fit, rot);
            Vector3 sc = new Vector3(k, k, k);
            if (stretch > 0)
            {
                if (fit.x > 0 && bs.x > 1e-4f) sc.x = Mathf.Clamp(fit.x / bs.x, k, k * (1 + stretch));
                if (fit.y > 0 && bs.y > 1e-4f) sc.y = Mathf.Clamp(fit.y / bs.y, k, k * (1 + stretch));
                if (fit.z > 0 && bs.z > 1e-4f) sc.z = Mathf.Clamp(fit.z / bs.z, k, k * (1 + stretch));
            }
            holder.transform.localRotation = Quaternion.Euler(0, modelYaw, 0);
            // scale is applied in the rotated frame: put it on an intermediate so axes match the footprint
            var scaleNode = new GameObject("fit"); scaleNode.transform.SetParent(go.transform, false);
            holder.transform.SetParent(scaleNode.transform, false);
            scaleNode.transform.localScale = sc;
            var rb = holder.transform.localRotation * b.center;
            Vector3 bsz = bs;
            Vector3 offset = new Vector3(-rb.x * sc.x, 0, -rb.z * sc.z);
            float ymin = (rb.y - bsz.y * 0.5f) * sc.y, ymax = (rb.y + bsz.y * 0.5f) * sc.y;
            offset.y = anchor == Anchor.Bottom ? -ymin : anchor == Anchor.Top ? -ymax : -(ymin + ymax) * 0.5f;
            scaleNode.transform.localPosition = offset;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0) * (parent != null ? Quaternion.identity : Quaternion.identity);
            return go;
        }
    }
}
