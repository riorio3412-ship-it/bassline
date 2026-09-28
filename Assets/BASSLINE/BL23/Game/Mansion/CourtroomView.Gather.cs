using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// The window gathers. The Judge's Lancet is behind Yusti in every shot of the throne; as the trial deepens, hooded figures
    /// are found standing in it, black against the bone glass on the transom ledges (one when the trial is well under way, three
    /// after the room's first great stare, five at the verdict). Nobody ever sees one arrive: a stage is only added while the
    /// lancet is out of the view camera's frustum, so the player cuts back to the judge and they are simply there.
    /// </summary>
    public sealed partial class CourtroomView
    {
        MeshFilter _gatherMf; Bounds _lancetBounds; bool _lancetBoundsSet;
        readonly Mesh[] _gatherStage = new Mesh[4];   // 0 = nobody, 1 = one, 2 = three, 3 = five
        int _gatherShown, _gatherWant; float _gatherTimer;

        void LancetWatchers()
        {
            // (x across the lancet at r 38, standing height, pose 0 upright / 1 bowed / 2 leaning, stage it joins)
            var figs = new (float x, float y, int pose, int stage)[]
            {
                (-4.6f, 7.4f, 0, 1),
                (4.4f, 7.4f, 1, 2), (1.6f, 20.9f, 0, 2),
                (-1.4f, 7.4f, 2, 3), (4.3f, 20.9f, 1, 3),
            };
            var inward = -_jd;
            for (int s = 0; s < 4; s++)
            {
                var q = new VQ { Mode = 1f };
                foreach (var f in figs)
                {
                    if (f.stage > s || s == 0) continue;
                    var feet = WP(0f, f.x, 39.25f, f.y);
                    var rot = Quaternion.LookRotation(inward) * Quaternion.Euler(f.pose == 1 ? 16f : f.pose == 2 ? 6f : 0f, 0f, f.pose == 2 ? 7f : 0f);
                    var m = Matrix4x4.TRS(feet, rot, Vector3.one * 1.22f);
                    Blob(q, m, new Vector3(0f, 0.62f, 0f), new Vector3(0.26f, 0.63f, 0.2f));
                    Blob(q, m, new Vector3(0f, 1.1f, 0.02f), new Vector3(0.24f, 0.2f, 0.19f));
                    Blob(q, m, new Vector3(0f, 1.33f, f.pose == 1 ? 0.07f : 0.02f), new Vector3(0.16f, 0.2f, 0.17f));
                    Blob(q, m, new Vector3(0f, 1.49f, -0.06f), new Vector3(0.06f, 0.09f, 0.07f));
                }
                var mesh = q.ToMesh("CourtLancetGathering" + s); BakeVoid(mesh, Vector3.zero);   // the same air as the glass behind them
                _gatherStage[s] = mesh;
            }
            var go = new GameObject("CourtLancetGathering"); go.transform.SetParent(_wellRoot, false);
            _gatherMf = go.AddComponent<MeshFilter>(); _gatherMf.sharedMesh = _gatherStage[0];
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = _mVoid;
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false; mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _rv.Renderers.Add(mr); _wellRends.Add(mr); _rends++;
            // the window's own extent (what the lens must not be looking at when someone new appears)
            _lancetBounds = new Bounds(WP(0f, 0f, 39.2f, 10f), Vector3.zero);
            foreach (var x in new[] { -7f, 7f }) foreach (var y in new[] { -7f, 26f }) foreach (var r in new[] { 38f, 39.6f }) _lancetBounds.Encapsulate(WP(0f, x, r, y));   // the lower lights, where they stand
            _lancetBoundsSet = true;
        }

        /// <summary>A low-poly ellipsoid (black, explicit value) into a VQ, in the frame m.</summary>
        static void Blob(VQ q, Matrix4x4 m, Vector3 c, Vector3 r)
        {
            const int SEG = 10, RINGS = 6; var k = Color.white;
            for (int i = 0; i < RINGS; i++)
            {
                float a0 = -Mathf.PI / 2 + Mathf.PI * i / RINGS, a1 = -Mathf.PI / 2 + Mathf.PI * (i + 1) / RINGS;
                for (int s = 0; s < SEG; s++)
                {
                    float b0 = s / (float)SEG * Mathf.PI * 2f, b1 = (s + 1) / (float)SEG * Mathf.PI * 2f;
                    Vector3 P(float a, float b) => m.MultiplyPoint3x4(c + Vector3.Scale(new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(a), Mathf.Cos(a) * Mathf.Sin(b)), r));
                    var p00 = P(a0, b0); var p01 = P(a1, b0); var p11 = P(a1, b1); var p10 = P(a0, b1);
                    var ctr = m.MultiplyPoint3x4(c); var want = (p00 + p11) * 0.5f - ctr;
                    q.Quad(p00, p01, p11, p10, k, k, k, k, 0.002f, 0.002f, 0.002f, 0.002f, want);
                }
            }
        }

        /// <summary>Per frame (from UpdateWell): which stage the trial has earned, and add it only while the window is out of frame.</summary>
        void UpdateGathering(Camera cam, float dt)
        {
            if (_gatherMf == null) return;
            int want = _verdictT0 > 0f ? 3 : _waves > 0 ? 2 : _depth >= 0.3f ? 1 : 0;
            _gatherWant = Mathf.Max(_gatherWant, want);
            if (_gatherShown >= _gatherWant || cam == null || !_lancetBoundsSet) return;
            _gatherTimer -= dt; if (_gatherTimer > 0f) return; _gatherTimer = 0.2f;
            GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            if (GeometryUtility.TestPlanesAABB(_planes, _lancetBounds)) return;
            _gatherShown = _gatherWant; _gatherMf.sharedMesh = _gatherStage[_gatherShown];
        }

        void ResetGathering()
        {
            _gatherShown = 0; _gatherWant = 0;
            if (_gatherMf != null && _gatherStage[0] != null) _gatherMf.sharedMesh = _gatherStage[0];
        }
    }
}
