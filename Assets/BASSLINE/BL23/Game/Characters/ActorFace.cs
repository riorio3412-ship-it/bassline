using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>Cell indices of the per-character face atlas (shared with the editor face painter).</summary>
    public static class FaceCells
    {
        // eyes (4x4)
        public const int EyeOpen = 0, EyeHalf = 1, EyeClosed = 2, EyeHappy = 3, EyeWide = 4, EyeAngry = 5, EyeSad = 6, EyeFear = 7,
            EyeDead = 8, EyeBlank = 9, EyeTeary = 10, EyePain = 11, EyeBreak = 12, EyeHalfLid = 13, EyeNarrow = 14, EyeLaugh = 15;
        // brows (4x2)
        public const int BrowNeutral = 0, BrowRaised = 1, BrowAngry = 2, BrowSad = 3, BrowFurrow = 4, BrowRelaxed = 5, BrowSkeptic = 6, BrowBreak = 7;
        // mouths (4x4)
        public const int MouthNeutral = 0, MouthSmile = 1, MouthGrin = 2, MouthFrown = 3, MouthTalkA = 4, MouthTalkO = 5, MouthOpenWide = 6, MouthSmirk = 7,
            MouthGritted = 8, MouthWavy = 9, MouthLaugh = 10, MouthDead = 11, MouthDisgust = 12, MouthSadOpen = 13, MouthPain = 14, MouthBreak = 15;

        public struct State
        {
            public int EyeL, EyeR, BrowL, BrowR, Mouth;
            public float Tears, Blush, Gloom;
            public bool CanBlink;
            public int TalkOpen, TalkClosed;
        }

        public static State For(Expr e, float intensity)
        {
            var s = new State { CanBlink = true, TalkOpen = MouthTalkA, TalkClosed = MouthNeutral };
            bool strong = intensity >= 0.45f;
            switch (e)
            {
                case Expr.Neutral: Set(ref s, EyeOpen, BrowNeutral, MouthNeutral); break;
                case Expr.Smile: Set(ref s, strong ? EyeOpen : EyeOpen, BrowRelaxed, MouthSmile); s.Blush = 0.25f * intensity; s.TalkClosed = MouthSmile; break;
                case Expr.Grin: Set(ref s, strong ? EyeHappy : EyeOpen, BrowRaised, MouthGrin); s.Blush = 0.35f * intensity; s.TalkOpen = MouthLaugh; s.TalkClosed = MouthGrin; s.CanBlink = !strong; break;
                case Expr.Angry: Set(ref s, strong ? EyeAngry : EyeNarrow, BrowAngry, strong ? MouthGritted : MouthFrown); s.TalkOpen = MouthTalkO; s.TalkClosed = MouthGritted; break;
                case Expr.Sad: Set(ref s, EyeSad, BrowSad, MouthFrown); s.TalkOpen = MouthSadOpen; s.TalkClosed = MouthFrown; break;
                case Expr.Surprised: Set(ref s, EyeWide, BrowRaised, strong ? MouthOpenWide : MouthTalkO); s.TalkOpen = MouthOpenWide; s.TalkClosed = MouthTalkO; break;
                case Expr.Fear: Set(ref s, EyeFear, BrowSad, MouthWavy); s.Gloom = strong ? 0.9f * intensity : 0.3f; s.TalkOpen = MouthSadOpen; s.TalkClosed = MouthWavy; break;
                case Expr.Smirk: s.EyeL = EyeHalfLid; s.EyeR = strong ? EyeOpen : EyeHalfLid; s.BrowL = BrowSkeptic; s.BrowR = BrowNeutral; s.Mouth = MouthSmirk; s.TalkClosed = MouthSmirk; break;
                case Expr.Disgust: Set(ref s, EyeNarrow, BrowFurrow, MouthDisgust); s.Gloom = strong ? 0.35f : 0f; s.TalkClosed = MouthDisgust; break;
                case Expr.Blank: Set(ref s, EyeBlank, BrowNeutral, MouthNeutral); break;
                case Expr.Dead: Set(ref s, EyeDead, BrowRelaxed, MouthDead); s.CanBlink = false; s.TalkOpen = MouthDead; s.TalkClosed = MouthDead; break;
                case Expr.Pain: Set(ref s, strong ? EyePain : EyeSad, BrowFurrow, strong ? MouthPain : MouthGritted); s.CanBlink = !strong; s.TalkOpen = MouthPain; s.TalkClosed = MouthGritted; break;
                case Expr.Crying: Set(ref s, EyeTeary, BrowSad, MouthSadOpen); s.Tears = strong ? 1f : 0.5f; s.Blush = 0.45f; s.TalkOpen = MouthSadOpen; s.TalkClosed = MouthFrown; break;
                // charpolish step 0: placeholder mapping for the appended Expr.Scream (face implementer refines it)
                case Expr.Scream: Set(ref s, EyeFear, BrowSad, MouthOpenWide); s.Gloom = 0.5f; s.CanBlink = false; s.TalkOpen = MouthOpenWide; s.TalkClosed = MouthOpenWide; break;
                case Expr.Laugh: Set(ref s, EyeLaugh, BrowRaised, MouthLaugh); s.Blush = 0.4f * intensity; s.CanBlink = false; s.TalkOpen = MouthLaugh; s.TalkClosed = MouthGrin; break;
                case Expr.Break: Set(ref s, EyeBreak, BrowBreak, MouthBreak); s.Gloom = 1f; s.CanBlink = false; s.TalkOpen = MouthOpenWide; s.TalkClosed = MouthBreak; break;
                // motion track: gasping for air / wide-eyed terror
                case Expr.Choke: Set(ref s, EyeFear, BrowSad, MouthOpenWide); s.Gloom = 0.35f * intensity; s.CanBlink = false; s.TalkOpen = MouthOpenWide; s.TalkClosed = MouthPain; break;
                case Expr.Panic: Set(ref s, EyeWide, BrowSad, strong ? MouthSadOpen : MouthWavy); s.Gloom = 0.6f * intensity; s.CanBlink = false; s.TalkOpen = MouthOpenWide; s.TalkClosed = MouthWavy; break;
            }
            return s;
        }

        static void Set(ref State s, int eye, int brow, int mouth)
        {
            s.EyeL = s.EyeR = eye; s.BrowL = s.BrowR = brow; s.Mouth = mouth;
        }
    }

    /// <summary>
    /// Drives the face: atlas cells on the face material (expressions, blink, mouth flap) and, on scanned heads,
    /// procedural blend shapes that move the face itself (smile corners, jaw, brows, cheeks).
    /// </summary>
    public class ActorFace : MonoBehaviour, IActorStep
    {
        public Renderer FaceRenderer;
        public int FaceMaterialIndex;
        public bool Enabled = true;

        static readonly int IdState0 = Shader.PropertyToID("_FaceState0");
        static readonly int IdState1 = Shader.PropertyToID("_FaceState1");
        static readonly string[] ShapeNames = { "SmileL", "SmileR", "JawOpen", "BrowUp", "BrowDown", "CheekUp" };
        const int SmileL = 0, SmileR = 1, Jaw = 2, BrowUp = 3, BrowDown = 4, Cheek = 5;

        readonly System.Collections.Generic.List<Material> _mats = new System.Collections.Generic.List<Material>();
        Expr _expr = Expr.Neutral;
        float _intensity = 1f;
        FaceCells.State _state;
        bool _talking, _blinkEnabled = true;
        float _nextBlink, _blinkT = -1f;
        float _talkT, _nextFlap;
        bool _mouthOpen;
        int _flapBig;
        Vector4 _last0 = new Vector4(-1, -1, -1, -1), _last1 = new Vector4(-1, -1, -1, -1);
        public System.Action<Expr, float> OnExpression;
        public Expr Current => _expr;

        // blend shapes (scanned heads)
        SkinnedMeshRenderer _smr;
        int[] _shape;
        readonly float[] _target = new float[6], _cur = new float[6];
        bool _hasShapes;
        public bool HasShapes => _hasShapes;

        public void Bind(Material instanceMat)
        {
            if (instanceMat != null && !_mats.Contains(instanceMat)) _mats.Add(instanceMat);
            BindShapes();
            _state = FaceCells.For(_expr, _intensity);
            ShapeTargets();
            _nextBlink = _ft + Random.Range(1.5f, 4f);
            Push(true);
        }

        void BindShapes()
        {
            if (_shape != null) return;
            _smr = FaceRenderer as SkinnedMeshRenderer;
            _shape = new int[ShapeNames.Length];
            for (int i = 0; i < _shape.Length; i++) _shape[i] = -1;
            if (_smr == null || _smr.sharedMesh == null) return;
            for (int i = 0; i < _shape.Length; i++) { _shape[i] = _smr.sharedMesh.GetBlendShapeIndex(ShapeNames[i]); if (_shape[i] >= 0) _hasShapes = true; }
        }

        public void SetExpression(Expr e, float intensity)
        {
            _expr = e; _intensity = Mathf.Clamp01(intensity);
            _state = ShownState();
            ShapeTargets();
            OnExpression?.Invoke(e, _intensity);
            Push(true);
        }

        /// <summary>Blend-shape targets (0..100) for the current expression.</summary>
        void ShapeTargets()
        {
            for (int i = 0; i < _target.Length; i++) _target[i] = 0f;
            if (!_hasShapes) return;
            float k = ShownIntensity;
            switch (ShownExpr)
            {
                case Expr.Smile: _target[SmileL] = _target[SmileR] = 75f * k; _target[Cheek] = 40f * k; _target[BrowUp] = 12f * k; break;
                case Expr.Grin: _target[SmileL] = _target[SmileR] = 100f * k; _target[Cheek] = 80f * k; _target[BrowUp] = 20f * k; break;
                case Expr.Laugh: _target[SmileL] = _target[SmileR] = 100f * k; _target[Cheek] = 100f * k; _target[Jaw] = 35f * k; _target[BrowUp] = 30f * k; break;
                case Expr.Angry: _target[BrowDown] = 100f * k; _target[Cheek] = 25f * k; break;
                case Expr.Sad: _target[BrowUp] = 45f * k; _target[BrowDown] = 35f * k; break;
                case Expr.Surprised: _target[BrowUp] = 100f * k; _target[Jaw] = 45f * k; break;
                case Expr.Fear: _target[BrowUp] = 80f * k; _target[BrowDown] = 30f * k; _target[Jaw] = 20f * k; break;
                case Expr.Smirk: _target[SmileR] = 85f * k; _target[SmileL] = 10f * k; _target[BrowUp] = 15f * k; break;
                case Expr.Disgust: _target[BrowDown] = 65f * k; _target[SmileL] = 30f * k; _target[Cheek] = 50f * k; break;
                case Expr.Blank: break;
                case Expr.Dead: _target[Jaw] = 30f; break;
                case Expr.Pain: _target[BrowDown] = 90f * k; _target[BrowUp] = 30f * k; _target[Cheek] = 70f * k; _target[Jaw] = 15f * k; break;
                case Expr.Crying: _target[BrowUp] = 60f * k; _target[BrowDown] = 40f * k; _target[Jaw] = 15f * k; break;
                case Expr.Break: _target[SmileL] = _target[SmileR] = 100f; _target[Jaw] = 25f; _target[BrowUp] = 70f; break;
                case Expr.Scream: _target[BrowUp] = 100f * k; _target[BrowDown] = 30f * k; _target[Jaw] = 85f * k; _target[Cheek] = 20f * k; break;   // charpolish step 0 placeholder
                case Expr.Choke: _target[BrowUp] = 85f * k; _target[BrowDown] = 45f * k; _target[Jaw] = 60f * k; _target[Cheek] = 30f * k; break;
                case Expr.Panic: _target[BrowUp] = 100f * k; _target[BrowDown] = 20f * k; _target[Jaw] = 30f * k; break;
            }
        }

        // ---------------------------------------------------------------- dark faces ("ink shadow" psychological faces)
        static readonly int IdDark = Shader.PropertyToID("_DarkFace");
        static readonly int IdDarkCol = Shader.PropertyToID("_DarkFaceColor");
        /// <summary>Glow colour of the dark-face eyes (per character; pale by default).</summary>
        public Color DarkGlow = new Color(0.86f, 0.9f, 1f);
        DarkFace _dk = DarkFace.None;
        float _dkWant, _dkShadow, _dkGlow, _dkHold = -1f, _dkTime;
        Vector4 _lastDark = new Vector4(-1, -1, -1, -1);
        public DarkFace Dark => _dkShadow > 0.001f || _dkWant > 0f ? _dk : DarkFace.None;

        /// <summary>Starts / ends a dark face. The shadow creeps down over ~0.35 s, the glow fades in after it; hold &gt; 0
        /// reverts automatically after that many seconds.</summary>
        public void SetDarkFace(DarkFace kind, float intensity = 1f, float hold = -1f)
        {
            if (kind == DarkFace.None) { _dkWant = 0f; _dkHold = -1f; return; }
            if (kind != _dk && _dkShadow > 0.05f) _dkShadow = Mathf.Min(_dkShadow, 0.3f);   // switching: re-creep from near the brow
            _dk = kind; _dkWant = Mathf.Clamp01(intensity); _dkHold = hold;
            ShapeTargets();
        }

        /// <summary>Jumps the dark face to its target (editor QA / cuts).</summary>
        public void SnapDarkFace() { _dkShadow = _dkWant; _dkGlow = _dkWant; PushDark(true); SnapShapes(); }

        void TickDark(float dt)
        {
            _dkTime += dt;
            if (_dkHold > 0f) { _dkHold -= dt; if (_dkHold <= 0f) { _dkWant = 0f; _dkHold = -1f; } }
            float rise = _dkWant > _dkShadow ? 1f / 0.35f : 1f / 0.45f;
            _dkShadow = Mathf.MoveTowards(_dkShadow, _dkWant, dt * rise);
            float gWant = _dkWant * Mathf.Clamp01((_dkShadow / Mathf.Max(0.01f, _dkWant) - 0.35f) / 0.5f);
            _dkGlow = Mathf.MoveTowards(_dkGlow, gWant, dt / (gWant > _dkGlow ? 0.4f : 0.3f));
            PushDark(false);
        }

        void PushDark(bool force)
        {
            if (_mats.Count == 0) return;
            bool on = _dkShadow > 0.0005f || _dkGlow > 0.0005f;
            var v = on ? new Vector4(_dkShadow, _dkGlow, (int)_dk, _dkTime % 600f) : Vector4.zero;
            if (!force && v == _lastDark) return;
            foreach (var m in _mats) { m.SetVector(IdDark, v); m.SetColor(IdDarkCol, DarkGlow); }
            _lastDark = v;
        }

        /// <summary>Blend-shape targets while a dark face is up (the real mouth / brows move under the painted grin).</summary>
        float DarkShape(int i, float baseTarget)
        {
            if (_dkShadow <= 0.001f) return baseTarget;
            float d = 0f;
            float tw = Mathf.PerlinNoise(_dkTime * 7f, 0.3f);
            switch (_dk)
            {
                case DarkFace.HollowGrin: d = i == SmileL || i == SmileR ? 95f : i == Cheek ? 70f : i == BrowDown ? 45f : 0f; break;
                case DarkFace.CorneredStare: d = i == SmileL ? 40f + 25f * tw : i == SmileR ? 45f : i == BrowUp ? 75f : 0f; break;
                case DarkFace.VeiledSmirk: d = i == SmileR ? 60f : i == SmileL ? 12f : i == BrowDown ? 25f : i == Cheek ? 25f : 0f; break;
            }
            return Mathf.Lerp(baseTarget, d, Mathf.Clamp01(_dkShadow * 1.5f));
        }

        /// <summary>charpolish step 0 contract (owner: implementer 1, face; called by the motion look-at chain): eye gaze
        /// relative to the head, yaw/pitch in degrees (+yaw = character's left, +pitch = up). Stub until the face fills it in.</summary>
        public void SetGaze(Vector2 yawPitchDeg) { }
        /// <summary>charpolish step 0b stub (owner: implementer 1, face): mouth shapes paced by the text on screen (see
        /// ActorRig.SetTalkText). Until implemented, SetTalking keeps driving the random flap.</summary>
        public void SetTalkText(string text, float charsPerSecond) { }

        /// <summary>motion track: physical strain on the face. gasp01 = gasping for air (mouth working, eyes wide), flush01 = reddening
        /// (struggle, strangling), pale01 = draining pallor (unconscious, dying). All 0 = off.</summary>
        public void SetStrain(float gasp01, float flush01, float pale01)
        {
            _gaspWant = Mathf.Clamp01(gasp01); _flushWant = Mathf.Clamp01(flush01); _paleWant = Mathf.Clamp01(pale01);
        }

        // ---------------------------------------------------------------- motion track: transient expressions and strain
        float _ft;                                  // own clock (runtime: Update; editor QA: Step)
        Expr _trExpr; float _trUntil = -1f, _trI = 1f;
        float _gaspWant, _flushWant, _paleWant, _gasp, _flush, _pale;
        Vector4 _lastStrain = new Vector4(-1, -1, -1, -1);
        static readonly int IdStrain = Shader.PropertyToID("_FaceStrain");

        /// <summary>Shows 'e' for 'seconds' over the kernel's expression (a wince when struck, surprise when startled), then
        /// returns to it by itself.</summary>
        public void PushTransient(Expr e, float intensity, float seconds)
        {
            if (_expr == Expr.Dead) return;
            _trExpr = e; _trI = Mathf.Clamp01(intensity); _trUntil = _ft + Mathf.Max(0.05f, seconds);
            _state = ShownState(); ShapeTargets(); Push(true);
        }

        bool TransientOn => _trUntil > _ft && _expr != Expr.Dead;
        Expr ShownExpr => _gasp > 0.15f && _expr != Expr.Dead ? Expr.Choke : TransientOn ? _trExpr : _expr;
        float ShownIntensity => _gasp > 0.15f && _expr != Expr.Dead ? Mathf.Max(0.5f, _gasp) : TransientOn ? _trI : _intensity;
        FaceCells.State ShownState() => FaceCells.For(ShownExpr, ShownIntensity);

        public int StepOrder => 5;
        /// <summary>IActorStep: editor QA advances the face with the animation (no random blinks / flaps there).</summary>
        public void Step(float dt) { if (!Application.isPlaying) FaceTick(dt, false); }

        public void SetTalking(bool on) { _talking = on; if (!on) _mouthOpen = false; }
        public void SetBlink(bool on) { _blinkEnabled = on; }

        void Update()
        {
            if (_mats.Count == 0 || !Enabled) return;
            FaceTick(Time.deltaTime, true);
        }

        void FaceTick(float dt, bool live)
        {
            if (_mats.Count == 0 || !Enabled) return;
            bool wasTrans = TransientOn; Expr wasShown = ShownExpr;
            _ft += dt;
            float t = _ft;
            // strain follows its targets (reddening builds slowly, pallor drains slowly)
            _gasp = Mathf.MoveTowards(_gasp, _gaspWant, dt * 2.5f);
            _flush = Mathf.MoveTowards(_flush, _flushWant, dt * 0.35f);
            _pale = Mathf.MoveTowards(_pale, _paleWant, dt * 0.25f);
            if ((wasTrans && !TransientOn) || wasShown != ShownExpr) { _state = ShownState(); ShapeTargets(); }
            // blink
            if (live && _blinkEnabled && _state.CanBlink && _dkShadow < 0.25f)
            {
                if (_blinkT < 0f && t >= _nextBlink) _blinkT = 0f;
                if (_blinkT >= 0f)
                {
                    _blinkT += dt;
                    if (_blinkT > 0.16f)
                    {
                        _blinkT = -1f;
                        _nextBlink = t + (Random.value < 0.18f ? 0.25f : Random.Range(2.2f, 5.5f));
                    }
                }
            }
            else _blinkT = -1f;
            // mouth flap
            if (live && _talking)
            {
                if (t >= _nextFlap)
                {
                    _mouthOpen = !_mouthOpen || Random.value < 0.2f;
                    _flapBig = Random.value < 0.3f ? 1 : 0;
                    _nextFlap = t + (_mouthOpen ? Random.Range(0.07f, 0.13f) : Random.Range(0.05f, 0.11f));
                }
            }
            TickDark(dt);
            Push(false);
            PushStrain(false);
            TickShapes(dt);
        }

        /// <summary>Gasping rhythm: 0 = mouth shut / straining, 1 = wide open gulp (irregular, faster when weaker).</summary>
        float GaspOpen()
        {
            float rate = 1.1f + 0.9f * _gasp;
            float ph = _ft * rate + Mathf.PerlinNoise(_ft * 0.7f, 3.3f) * 0.6f;
            float c = ph - Mathf.Floor(ph);
            return c < 0.38f ? Mathf.Sin(c / 0.38f * Mathf.PI) : 0f;
        }

        void PushStrain(bool force)
        {
            if (_mats.Count == 0) return;
            var v = new Vector4(_flush, _pale, _gasp, 0f);
            if (!force && (v - _lastStrain).sqrMagnitude < 1e-6f) return;
            foreach (var m in _mats) m.SetVector(IdStrain, v);
            _lastStrain = v;
        }

        /// <summary>Advances blend-shape smoothing (also used by editor QA tools).</summary>
        public void TickShapes(float dt)
        {
            if (!_hasShapes || _smr == null) return;
            for (int i = 0; i < _cur.Length; i++)
            {
                float target = DarkShape(i, _target[i]);
                if (i == Jaw && PaintedMouth) target = 0f;
                if (i == Jaw && _talking) target = Mathf.Max(target, _mouthOpen ? (_flapBig == 1 ? 55f : 32f) : 6f);
                if (i == Jaw && _gasp > 0.15f) target = Mathf.Max(target, (18f + 62f * GaspOpen()) * _gasp);
                if (i == BrowUp && _gasp > 0.15f) target = Mathf.Max(target, 70f * _gasp);
                if (i == Cheek && _blinkT >= 0f) target = Mathf.Max(target, 25f);
                float rate = i == Jaw ? 28f : 9f;
                _cur[i] = Mathf.Lerp(_cur[i], target, 1f - Mathf.Exp(-dt * rate));
                if (_shape[i] >= 0) _smr.SetBlendShapeWeight(_shape[i], _cur[i]);
            }
        }

        /// <summary>A painted (non-neutral) mouth cell is showing (static expression, not talking / gasping).</summary>
        bool PaintedMouth => _hasShapes && !_talking && _gasp <= 0.15f && _dkShadow <= 0.25f && _state.Mouth != FaceCells.MouthNeutral && _state.Mouth != FaceCells.MouthSmile && _state.Mouth != FaceCells.MouthSmirk;

        /// <summary>Jumps the blend shapes to their targets (no smoothing).</summary>
        public void SnapShapes()
        {
            if (!_hasShapes || _smr == null) return;
            for (int i = 0; i < _cur.Length; i++) { _cur[i] = i == Jaw && PaintedMouth ? 0f : DarkShape(i, _target[i]); if (_shape[i] >= 0) _smr.SetBlendShapeWeight(_shape[i], _cur[i]); }
        }

        void Push(bool force)
        {
            if (_mats.Count == 0) return;
            int eyeL = _state.EyeL, eyeR = _state.EyeR;
            if (_blinkT >= 0f)
            {
                int b = (_blinkT < 0.04f || _blinkT > 0.11f) ? FaceCells.EyeHalf : FaceCells.EyeClosed;
                if (eyeL != FaceCells.EyeClosed) eyeL = b;
                if (eyeR != FaceCells.EyeClosed) eyeR = b;
            }
            int mouth = _state.Mouth;
            if (_talking)
            {
                if (_mouthOpen) mouth = (_flapBig == 1 && _state.TalkOpen == FaceCells.MouthTalkA) ? FaceCells.MouthTalkO : _state.TalkOpen;
                else mouth = _state.TalkClosed;
            }
            // dark face: the shader paints eyes / mouth; the atlas shows the plain open eyes and the neutral mouth
            // strain: gasping mouth, eyes wide then glazing as the colour drains
            if (_gasp > 0.15f && _expr != Expr.Dead)
            {
                mouth = GaspOpen() > 0.3f ? FaceCells.MouthOpenWide : FaceCells.MouthPain;
                int e = _pale > 0.55f ? FaceCells.EyeHalf : (_ft % 2.3f) < 0.25f ? FaceCells.EyePain : FaceCells.EyeFear;
                eyeL = eyeR = e;
            }
            if (_dkShadow > 0.25f) { mouth = FaceCells.MouthNeutral; eyeL = eyeR = _dk == DarkFace.CorneredStare ? FaceCells.EyeWide : FaceCells.EyeOpen; }
            // scanned heads: one mouth path at a time. Talking and gasping move the real jaw (blend shape) under the scan's own mouth;
            // a painted mouth (static expression) keeps the jaw closed (TickShapes), so two mouths never show at once.
            if (_hasShapes && (_talking || _gasp > 0.15f)) mouth = FaceCells.MouthNeutral;
            // scanned heads: closed-mouth smiles are done by moving the real mouth (blend shapes), not painted over it
            if (_hasShapes && (mouth == FaceCells.MouthSmile || mouth == FaceCells.MouthSmirk)) mouth = FaceCells.MouthNeutral;
            var s0 = new Vector4(eyeL, eyeR, _state.BrowL, _state.BrowR);
            var s1 = new Vector4(mouth, _state.Tears, Mathf.Max(_state.Blush, _flush * 0.9f), _state.Gloom);
            if (force || s0 != _last0) { foreach (var m in _mats) m.SetVector(IdState0, s0); _last0 = s0; }
            if (force || s1 != _last1) { foreach (var m in _mats) m.SetVector(IdState1, s1); _last1 = s1; }
        }
    }
}
