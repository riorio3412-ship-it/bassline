using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>Verlet spring chain for hair / coat tails / ribbons. Runs after ActorAnimator.</summary>
    [DefaultExecutionOrder(100)]
    public class SpringChain : MonoBehaviour, IActorStep
    {
        public int StepOrder => 100;   // IActorStep (charpolish step 0)
        public Transform[] Bones = new Transform[0];
        public Vector3 TailLocal = new Vector3(0, -0.1f, 0);   // tip of the last bone in its local space
        [Range(0, 1)] public float Stiffness = 0.12f;
        [Range(0, 1)] public float Damping = 0.18f;
        public float Gravity = 2.5f;
        public float Radius = 0.025f;
        public float MaxAngle = 70f;

        Quaternion[] _restLocal;
        Vector3[] _restDir;   // local direction to the next joint
        float[] _len;
        Vector3[] _pos, _prev;
        bool _init;
        ActorRig _rig;
        readonly List<Transform> _colT = new List<Transform>();
        readonly List<float> _colR = new List<float>();
        readonly List<Vector3> _colOff = new List<Vector3>();
        Vector3 _lastRoot;

        void Start() { Init(); }

        void Init()
        {
            if (_init || Bones == null || Bones.Length == 0) return;
            _init = true;
            int n = Bones.Length;
            _restLocal = new Quaternion[n]; _restDir = new Vector3[n]; _len = new float[n];
            _pos = new Vector3[n]; _prev = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                _restLocal[i] = Bones[i].localRotation;
                Vector3 childLocal = i + 1 < n ? Bones[i + 1].localPosition : TailLocal;
                _len[i] = childLocal.magnitude;
                _restDir[i] = _len[i] > 1e-5f ? childLocal / _len[i] : Vector3.down;
                _pos[i] = _prev[i] = Bones[i].TransformPoint(childLocal);
            }
            _rig = GetComponentInParent<ActorRig>();
            if (_rig != null)
            {
                float s = _rig.Height > 0 ? _rig.Height / 1.75f : 1f;
                AddCol(_rig.Bone(HBone.Chest), new Vector3(0, 0.08f * s, -0.02f), 0.13f * s);
                AddCol(_rig.Bone(HBone.Spine), new Vector3(0, 0.02f, -0.01f), 0.12f * s);
                AddCol(_rig.Bone(HBone.Hips), new Vector3(0, -0.04f, -0.02f), 0.14f * s);
                AddCol(_rig.Bone(HBone.Head), new Vector3(0, 0.1f * s, 0f), 0.11f * s);
                AddCol(_rig.Bone(HBone.UpperLegL), new Vector3(0, -0.15f * s, 0), 0.085f * s);
                AddCol(_rig.Bone(HBone.UpperLegR), new Vector3(0, -0.15f * s, 0), 0.085f * s);
                AddCol(_rig.Bone(HBone.UpperLegL), new Vector3(0, -0.35f * s, 0), 0.07f * s);
                AddCol(_rig.Bone(HBone.UpperLegR), new Vector3(0, -0.35f * s, 0), 0.07f * s);
            }
            _lastRoot = transform.position;
        }

        void AddCol(Transform t, Vector3 off, float r)
        {
            if (t == null) return;
            // do not collide with the bone the chain hangs from (e.g. hair vs head)
            foreach (var b in Bones) if (b == t) return;
            _colT.Add(t); _colOff.Add(off); _colR.Add(r);
        }

        public void ResetState()
        {
            if (!_init) return;
            for (int i = 0; i < Bones.Length; i++)
            {
                Bones[i].localRotation = _restLocal[i];
                _pos[i] = _prev[i] = Bones[i].TransformPoint(_restDir[i] * _len[i]);
            }
        }

        void LateUpdate()
        {
            if (!_init) { Init(); if (!_init) return; }
            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f);
            Step(dt);
        }

        public void Step(float dt)
        {
            if (!_init) Init();
            if (!_init) return;
            if ((transform.position - _lastRoot).sqrMagnitude > 1f) ResetState();
            _lastRoot = transform.position;
            int n = Bones.Length;
            float scale = _rig != null && _rig.Height > 0 ? _rig.Height / 1.75f : 1f;
            for (int i = 0; i < n; i++) Bones[i].localRotation = _restLocal[i];
            for (int i = 0; i < n; i++)
            {
                Transform b = Bones[i];
                Vector3 origin = b.position;
                Vector3 target = origin + b.rotation * _restDir[i] * _len[i];
                Vector3 vel = (_pos[i] - _prev[i]) * (1f - Damping);
                _prev[i] = _pos[i];
                Vector3 p = _pos[i] + vel + Vector3.down * Gravity * dt * dt;
                p += (target - p) * Stiffness;
                // collisions
                for (int c = 0; c < _colT.Count; c++)
                {
                    Vector3 cc = _colT[c].TransformPoint(_colOff[c]);
                    float r = _colR[c] + Radius * scale;
                    Vector3 d = p - cc;
                    float dm = d.magnitude;
                    if (dm < r && dm > 1e-5f) p = cc + d / dm * r;
                }
                // length constraint + angle limit
                Vector3 dir = p - origin;
                if (dir.sqrMagnitude < 1e-10f) dir = target - origin;
                Vector3 restW = (target - origin).normalized;
                dir = Vector3.RotateTowards(restW, dir.normalized, MaxAngle * Mathf.Deg2Rad, 0f);
                p = origin + dir * _len[i];
                _pos[i] = p;
                b.rotation = Quaternion.FromToRotation(b.rotation * _restDir[i], dir) * b.rotation;
            }
        }
    }
}
