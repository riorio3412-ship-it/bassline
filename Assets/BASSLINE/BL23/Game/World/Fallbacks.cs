using System.Collections.Generic;
using BL23.Game.Characters;
using BL23.Game.Mansion;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>Emergency visuals used only if the art modules fail to build (so the game stays playable/diagnosable).</summary>
    public static class FallbackMansion
    {
        static Material _mat;
        static Material Mat(Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh); m.color = c; return m;
        }

        public static MansionView Build(Layout L, Transform parent)
        {
            var root = new GameObject("FallbackMansion"); root.transform.SetParent(parent, false);
            var view = root.AddComponent<MansionView>();
            var wall = Mat(new Color(0.35f, 0.3f, 0.4f)); var floor = Mat(new Color(0.2f, 0.18f, 0.22f));
            foreach (var r in L.Rooms)
            {
                if (r.Void) continue;
                var f = GameObject.CreatePrimitive(PrimitiveType.Cube); f.transform.SetParent(root.transform, false);
                f.transform.position = new Vector3(r.Rect.CX, L.FloorY(r.Floor) - 0.05f, r.Rect.CZ); f.transform.localScale = new Vector3(r.Rect.W, 0.1f, r.Rect.D); f.GetComponent<Renderer>().sharedMaterial = floor;
            }
            foreach (var w in L.Walls())
            {
                if (w.Open || w.DoorId >= 0) continue;
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.transform.SetParent(root.transform, false);
                float y = L.FloorY(w.Floor); float h = L.Floor(w.Floor).Height - 0.3f;
                g.transform.position = new Vector3((w.x0 + w.x1) / 2, y + h / 2, (w.z0 + w.z1) / 2);
                g.transform.localScale = new Vector3(Mathf.Max(0.2f, w.x1 - w.x0), h, Mathf.Max(0.2f, w.z1 - w.z0));
                g.GetComponent<Renderer>().sharedMaterial = wall;
            }
            var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional; light.transform.SetParent(root.transform); light.transform.rotation = Quaternion.Euler(50, 30, 0); light.intensity = 0.8f;
            return view;
        }

        public static GameObject Decal(Vector3 pos, Vector3 n, float size, Color c)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(q.GetComponent<Collider>());
            q.transform.position = pos + n * 0.01f; q.transform.rotation = Quaternion.LookRotation(-n) ; q.transform.localScale = Vector3.one * size;
            var m = Mat(c); q.GetComponent<Renderer>().sharedMaterial = m; return q;
        }

        public static GameObject ItemBox(ItemDef def)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            float s = Mathf.Clamp(def?.Size ?? 0.2f, 0.05f, 0.9f);
            g.transform.localScale = new Vector3(s * 0.4f, s * 0.3f, s);
            g.GetComponent<Renderer>().sharedMaterial = Mat(def != null && def.IsWeapon ? new Color(0.6f, 0.6f, 0.65f) : new Color(0.7f, 0.6f, 0.4f));
            return g;
        }
    }

    public static class FallbackActor
    {
        public static ActorRig Create(CastDef def, Transform parent)
        {
            var go = new GameObject("FallbackRig_" + def.Id); go.transform.SetParent(parent, false);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.transform.SetParent(go.transform, false);
            float h = def.HeightCm / 100f; body.transform.localScale = new Vector3(0.45f, h / 2f, 0.35f); body.transform.localPosition = new Vector3(0, h / 2f, 0);
            Object.Destroy(body.GetComponent<Collider>());
            var rig = go.AddComponent<ActorRig>(); rig.ActorId = def.Id; rig.Height = h;
            var eye = new GameObject("Eye").transform; eye.SetParent(go.transform, false); eye.localPosition = new Vector3(0, h - 0.1f, 0.1f); rig.EyeAnchor = eye;
            var hand = new GameObject("HandR").transform; hand.SetParent(go.transform, false); hand.localPosition = new Vector3(0.3f, h * 0.5f, 0.2f); rig.HandAnchorR = hand; rig.HandAnchorL = hand;
            rig.Anim = go.AddComponent<ActorAnimator>();
            var col = go.AddComponent<CapsuleCollider>(); col.height = h; col.radius = 0.25f; col.center = new Vector3(0, h / 2, 0);
            return rig;
        }
    }
}
