using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>
    /// 유스티's glass aquarium head: a goldfish swims inside the water volume, bubbles rise,
    /// and emotions show only as changes of the water (per the design: "감정은 물결의 정지로만 드러남").
    /// </summary>
    public class AquariumHead : MonoBehaviour
    {
        public Transform Fish;
        public Transform FishTail;
        public Transform[] Bubbles = new Transform[0];
        public Renderer Water;
        public Renderer Glass;
        public Vector3 WaterCenter;      // local
        public Vector3 WaterHalf = new Vector3(0.13f, 0.07f, 0.1f);

        static readonly int IdCalm = Shader.PropertyToID("_Calm");
        static readonly int IdBreak = Shader.PropertyToID("_BreakAmount");
        static readonly int IdGlow = Shader.PropertyToID("_Glow");
        static readonly int IdDeep = Shader.PropertyToID("_Deep");

        Material _water, _glass;
        float _calm, _calmTarget, _speed = 1f, _speedTarget = 1f, _break, _t, _bubbleRate = 0.8f;
        Vector3 _fishPos, _fishVel;
        float[] _bubbleT;
        Color _deep0;
        Color _deepTarget;
        bool _talk;
        float _freezeFish;

        void Start()
        {
            if (Water != null) { _water = Water.material; _deep0 = _water.GetColor(IdDeep); _deepTarget = _deep0; }
            if (Glass != null) _glass = Glass.material;
            _bubbleT = new float[Bubbles.Length];
            for (int i = 0; i < _bubbleT.Length; i++) _bubbleT[i] = Random.value;
            _fishPos = WaterCenter;
        }

        public void SetMood(Expr e, float intensity)
        {
            _calmTarget = 0f; _speedTarget = 1f; _bubbleRate = 0.8f; _freezeFish = 0f;
            _deepTarget = _deep0;
            switch (e)
            {
                case Expr.Angry: case Expr.Disgust: case Expr.Blank:
                    _calmTarget = 1f; _speedTarget = 0.05f; _bubbleRate = 0f; break; // the ripples stop
                case Expr.Sad: case Expr.Crying:
                    _speedTarget = 0.35f; _bubbleRate = 0.2f; _deepTarget = new Color(0.03f, 0.18f, 0.35f, _deep0.a); break;
                case Expr.Surprised: case Expr.Fear:
                    _speedTarget = 2.8f; _bubbleRate = 4f; break;
                case Expr.Smile: case Expr.Grin: case Expr.Laugh:
                    _speedTarget = 1.6f; _bubbleRate = 2f; break;
                case Expr.Smirk:
                    _speedTarget = 0.7f; break;
                case Expr.Dead:
                    _calmTarget = 1f; _speedTarget = 0f; _bubbleRate = 0f; _freezeFish = 1f; break;
                case Expr.Break:
                    _speedTarget = 3.5f; _bubbleRate = 6f; _deepTarget = new Color(0.4f, 0.02f, 0.12f, _deep0.a); break;
            }
        }

        public void SetTalking(bool on) { _talk = on; }
        public void SetBreak(float t) { _break = t; if (_water != null) _water.SetFloat(IdBreak, t); if (_glass != null) _glass.SetFloat(IdBreak, t); }
        public void SetWet(bool on) { }

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            _calm = Mathf.MoveTowards(_calm, _calmTarget, dt * 1.5f);
            _speed = Mathf.MoveTowards(_speed, _speedTarget, dt * 2f);
            if (_water != null)
            {
                _water.SetFloat(IdCalm, _calm);
                if (_water.HasProperty(IdDeep)) _water.SetColor(IdDeep, Color.Lerp(_water.GetColor(IdDeep), _deepTarget, dt * 2f));
            }
            // fish: wander on a smooth path inside the water box
            if (Fish != null)
            {
                float s = _speed * (1f + _break * 2f);
                float t = _t * 0.45f;
                Vector3 goal = WaterCenter + new Vector3(
                    Mathf.Sin(t * 1.1f + 0.3f) * WaterHalf.x * 0.72f,
                    Mathf.Sin(t * 0.7f + 1.1f) * WaterHalf.y * 0.5f,
                    Mathf.Sin(t * 1.7f) * WaterHalf.z * 0.6f);
                if (_break > 0.3f) goal += Random.insideUnitSphere * 0.03f * _break;
                if (_freezeFish < 0.5f)
                {
                    Vector3 prev = _fishPos;
                    _fishPos = Vector3.SmoothDamp(_fishPos, goal, ref _fishVel, 0.9f / Mathf.Max(0.05f, s), 0.25f * Mathf.Max(0.05f, s), dt);
                    Vector3 v = _fishPos - prev;
                    Fish.localPosition = _fishPos;
                    if (v.sqrMagnitude > 1e-9f)
                    {
                        Quaternion want = Quaternion.LookRotation(new Vector3(v.x, v.y * 0.3f, v.z).normalized, Vector3.up);
                        Fish.localRotation = Quaternion.Slerp(Fish.localRotation, want, dt * 4f);
                    }
                }
                else
                {
                    // floating belly-up
                    Fish.localPosition = Vector3.Lerp(Fish.localPosition, WaterCenter + Vector3.up * WaterHalf.y * 0.8f, dt);
                    Fish.localRotation = Quaternion.Slerp(Fish.localRotation, Quaternion.Euler(0, 40f, 180f), dt);
                }
                if (FishTail != null)
                    FishTail.localRotation = Quaternion.Euler(0, Mathf.Sin(_t * 9f * Mathf.Max(0.3f, s)) * 28f * Mathf.Min(1f, s + 0.2f) * (1f - _freezeFish), 0);
            }
            // bubbles
            float rate = _bubbleRate + (_talk ? 2.5f : 0f);
            for (int i = 0; i < Bubbles.Length; i++)
            {
                var b = Bubbles[i];
                if (b == null) continue;
                _bubbleT[i] += dt * (0.5f + 0.35f * rate) * (0.7f + 0.6f * ((i * 37) % 10) / 10f);
                if (_bubbleT[i] > 1f)
                {
                    _bubbleT[i] = 0f;
                    b.gameObject.SetActive(rate > 0.05f && Random.value < Mathf.Clamp01(rate * 0.5f + 0.3f));
                }
                float u = _bubbleT[i];
                float x = Mathf.Sin(i * 12.9898f) * WaterHalf.x * 0.6f + Mathf.Sin(_t * 3f + i) * 0.004f;
                float z = Mathf.Sin(i * 78.233f) * WaterHalf.z * 0.6f;
                b.localPosition = WaterCenter + new Vector3(x, -WaterHalf.y * 0.9f + u * WaterHalf.y * 1.8f, z);
                float sc = Mathf.Lerp(0.004f, 0.009f, u) * (1f + (i % 3) * 0.3f);
                b.localScale = Vector3.one * sc;
            }
        }
    }
}
