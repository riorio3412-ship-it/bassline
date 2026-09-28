using System.Reflection;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// The well's behaviour over a trial. Stone never moves. The chandelier and the four great chains swing at their true
    /// pendulum periods; the Black Sun's fibres creep and its corona breathes. The trial deepens (Depth): late doorways light
    /// up; at the vote and the verdict far eyes wake in a wave that climbs the well; seconds after the room stares, the Sun
    /// dims (a blink, never a flash); the far watchers, who gazed up at the Sun, are found turned toward the court (never seen
    /// turning: a sector turns only while it is out of frame). At the verdict the pupil contracts and everything holds still.
    /// Budget: well under 0.1 ms, no allocations.
    /// </summary>
    public sealed partial class CourtroomView
    {
        // ------------------------------------------------------------------ camera pin (MansionView.Cull reads these)
        static readonly bool PinLive = typeof(MansionView).GetField("CourtPinVersion", BindingFlags.Public | BindingFlags.Static) != null;
        /// <summary>How high above the court floor the trial lens may rise (read by the trial camera by reflection).</summary>
        public float CameraCeiling => PinLive ? 36f : 10.5f;
        bool _pinOverride;
        /// <summary>Keep the court rendered and lit (floor -2 lights) for the current view camera even far above the floor.</summary>
        public void PinCamera(bool on) { _pinOverride = on; }
        /// <summary>True while a trial / court vista camera is the view camera (or PinCamera(true)).</summary>
        public bool Pinned
        {
            get
            {
                if (_pinOverride) return true;
                var cam = _view != null ? _view.ViewCamera : null;
                return cam != null && cam.isActiveAndEnabled && (cam.name == "TrialCamera" || cam.name == "CourtVistaCam");
            }
        }
        /// <summary>Is a world point inside the well (where a pinned camera still belongs to the court)?</summary>
        public bool Holds(Vector3 p)
        {
            float dx = p.x - _c.x, dz = p.z - _c.z, dy = p.y - _c.y;
            return dx * dx + dz * dz <= 36f * 36f && dy >= -60f && dy <= 110f;
        }
        public int RoomId => _rv != null && _rv.Room != null ? _rv.Room.Id : -1;
        public float StareLevel => Eyes != null ? Eyes.StareLevel : 0f;
        /// <summary>0..1 trial progress (stares, time under the Sun).</summary>
        public float Depth => _depth;
        /// <summary>Unscaled time the verdict beat began (-1 before it).</summary>
        public float VerdictAt => _verdictT0;

        // ------------------------------------------------------------------ beats (explicit triggers for the cinematics)
        /// <summary>"open", "vote", "verdict", "execution", "organ", "calm".</summary>
        public void Beat(string key)
        {
            if (!_built || key == null) return;
            float t = Time.unscaledTime;
            switch (key)
            {
                case "open": ResetTrial(); StartCounterweight(t); break;
                case "vote": StartWave(t, false, true); break;
                case "verdict": StartVerdict(t); break;
                case "execution": StartWave(t, _verdictT0 > 0f, true); ScheduleSunBlink(t + 1.5f, true); break;
                case "organ": break;
                case "calm": if (!_waveHold && _waveT0 > 0f) _waveT0 = Mathf.Min(_waveT0, t - 11.8f); break;
            }
        }
        // ------------------------------------------------------------------ framings for the trial lens (read by the cinematics)
        /// <summary>The Black Sun's pupil (the look-up target; same as Zenith).</summary>
        public Vector3 SunPoint => Zenith;
        /// <summary>The opening reveal starts here: podium-eye height behind the far podiums, near the rim.</summary>
        public Vector3 RevealFrom => _c - _jd * 7.2f + _rt * 0.8f + Vector3.up * 1.5f;
        /// <summary>...looking up the throne side of the well at ~70° (piers, galleries and the lancet climbing to the Sun near the
        /// top of a 50° frame); then tilt down the lancet (LancetPoint) to Yusti (JudgeAnchor), under 5°/s.</summary>
        public Vector3 RevealLook => RevealFrom + (_jd * Mathf.Cos(70f * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(70f * Mathf.Deg2Rad)) * 40f;

        /// <summary>
        /// Framings of the well for the trial lens, by name (lens position, look-at point, vertical fov). All stay inside the
        /// court's stage circle except "high" (which needs the court pin), and none puts an additive veil in front of a face.
        /// reveal_start / reveal_end: the opening (look up the well, then down to the throne). well_up: the canonical up-shot of
        /// the Black Sun (clear of the chandelier's ring and the lift tower); verdict_up: the same, for the vote and verdict
        /// beats (the eye wave and the pupil contracting). judge_wide: low on the far side, the lancet, pier trunks and chains
        /// filling the top third above Yusti. throne_tele: the throne compressed against the lancet. high: 30 m up the well,
        /// looking down past the chandelier at the lit island. abyss: over the parapet, down into the well.
        /// </summary>
        public bool WellShot(string key, out Vector3 pos, out Vector3 look, out float fov)
        {
            var up = Vector3.up; var head = ButlerAnchor != null ? ButlerAnchor.position + up * 1.25f : _c + _jd * 8.5f + up * 3.6f;
            switch (key)
            {
                case "reveal_start": pos = RevealFrom; look = RevealLook; fov = 50f; return true;
                case "reveal_end": pos = _c - _jd * 6.1f - _rt * 0.6f + up * 2.4f; look = head + up * 0.1f; fov = 46f; return true;
                // off the judge-lift axis: the chandelier's thread and the lift tower no longer stack into one ladder under the Sun
                case "well_up": case "verdict_up": pos = _c + Dir(60f) * 5f + up * 1.5f; look = Zenith; fov = 60f; return true;
                case "judge_wide": pos = _c - _jd * 6.4f + up * 1.2f; look = head + up * 4.2f; fov = 52f; return true;
                case "throne_tele": pos = _c - _jd * 8.6f + up * 1.0f; look = head - up * 0.05f; fov = 22f; return true;
                case "high": pos = _c - _jd * 3.4f + _rt * 1.8f + up * 30f; look = _c + _jd * 0.5f; fov = 52f; return true;
                case "abyss": pos = _c + Dir(130f) * 9.0f + up * 7f; look = AbyssPoint; fov = 55f; return true;
            }
            pos = look = Vector3.zero; fov = 0f; return false;
        }

        /// <summary>Freeze all motion (chandelier, chains, puffs, counterweight). Eyes and the Sun remain.</summary>
        public void Still(bool on) { _still = on; }
        /// <summary>Re-time the leviathan's next pass so its head crosses the well's axis in 'seconds' (at least 16 s).</summary>
        public void FishCrossIn(float seconds) { if (_built) FishCross(seconds); }

        // ------------------------------------------------------------------ state
        bool _built, _still, _wasPinned, _wasVisible, _noticeArmed, _fogWarned;
        float _depth, _depthBumpAt = -99f, _judgeWatchAt = -99f, _unpinAt = -99f;
        float _waveT0 = -1f, _lastWaveAt = -999f; int _waves; bool _waveHold;
        float _verdictT0 = -1f, _wBlinkT0 = -99f, _sunBlinkT0 = -99f, _sunBlinkAt = -1f, _lastSunBlink = -999f, _holdT0 = -99f;
        float _motionT, _noticeTimer, _fogTimer, _cwT0 = -1f, _cwNext, _cwFrom, _cwTo, _cwDur;
        readonly bool[] _turned = new bool[Sectors];
        readonly Mesh[] _watchUpMesh = new Mesh[Sectors];
        readonly Plane[] _planes = new Plane[6];
        readonly float[] _band01 = new float[4], _fogK = new float[4];
        float _kHaze = 1f, _kPuffs = 1f, _kShafts = 1f, _iris01;
        // the wave climbs: each band opens 1.3 s after the one below it (a ring of eyes rising ~15 m/s), closes top-first
        static readonly float[] WaveOpen = { 0f, 1.3f, 2.6f, 3.9f }, WaveClose = { 3.9f, 2.6f, 1.3f, 0f }, BlinkRipple = { 0f, 0.4f, 0.75f, 1.1f }, BandRef = { 45f, 60f, 75f, 90f };
        const float WaveFade = 1.8f;
        /// <summary>A vote read from the stare pattern (no explicit Beat) waits this long: the ballot fills the screen first.</summary>
        const float VoteDelay = 10f;
        float _wavePendingAt = -1f;
        static readonly int PInt = Shader.PropertyToID("_Intensity");

        static float S01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        void ResetTrial()
        {
            _depth = 0f; _depthBumpAt = -99f; _waves = 0; _lastWaveAt = -999f; _waveT0 = -1f; _waveHold = false; _verdictT0 = -1f; _iris01 = 0f;
            _lastSunBlink = -999f; _sunBlinkAt = -1f; _still = false; _noticeArmed = false; _holdT0 = -99f; _wavePendingAt = -1f;
            ResetGathering(); ResetOmens(); ResetJury();
            if (_fishT0 < 0f) _fishNext = Time.unscaledTime + 80f + Hash(Time.unscaledTime * 0.31f + 2.7f) * 60f;
            for (int s = 0; s < Sectors; s++)
            {
                if (_turned[s] && _watchR[s] != null && _watchUpMesh[s] != null) _watchR[s].GetComponent<MeshFilter>().sharedMesh = _watchUpMesh[s];
                _turned[s] = false;
            }
        }

        void StartWave(float t, bool hold, bool force)
        {
            if (!hold && !force && (t - _lastWaveAt < 90f || _waves >= 2)) return;
            if (!hold && _waveHold) return;
            // the verdict takes over an open wave where it stands (no blink back to dark); a closing or closed one climbs anew
            if (_waveT0 < 0f || (hold && t - _waveT0 > 11.8f)) _waveT0 = t;
            _waveHold = hold; _lastWaveAt = t; if (!hold) _waves++;
            if (Eyes != null) Eyes.Hush(hold ? 8f : 5f);   // one layer of eyes owns the moment
            if (!hold && _fishT0 < 0f && _depth >= 0.35f) _fishNext = Mathf.Min(_fishNext > 0f ? _fishNext : float.MaxValue, t + 3f + Hash(t) * 7f);
        }

        void StartVerdict(float t)
        {
            if (_verdictT0 > 0f) return;
            _verdictT0 = t; StartWave(t, true, true); _noticeArmed = true; _still = true;
            ScheduleSunBlink(t + 0.8f, true);
        }

        void ScheduleSunBlink(float at, bool force)
        {
            if (!force && at - _lastSunBlink < 90f) return;
            if (_sunBlinkAt < 0f || at < _sunBlinkAt) _sunBlinkAt = at;
        }

        void WellWatch(Vector3 p)
        {
            if (!_built || ButlerAnchor == null) return;
            if ((p - ButlerAnchor.position).sqrMagnitude < 9f) _judgeWatchAt = Time.unscaledTime;
        }

        void WellStare(float k)
        {
            if (!_built) return;
            float t = Time.unscaledTime;
            if (k >= 0.7f && t - _depthBumpAt >= 30f) { _depth = Mathf.Clamp01(_depth + 0.06f); _depthBumpAt = t; }
            bool verdictPhase = false;
            try { var s = Session.I?.S; verdictPhase = s != null && (s.Phase == BL23.Sim.Phase.Verdict || s.Phase == BL23.Sim.Phase.Execution); } catch (System.Exception) { }
            if (k >= 0.99f && t - _judgeWatchAt < 6f && verdictPhase) StartVerdict(t);
            else if (k >= 0.88f && k <= 0.92f && _wavePendingAt < 0f && _waveT0 < 0f) _wavePendingAt = t + VoteDelay;
            if (k >= 0.8f) { ScheduleSunBlink(t + 3f, false); BellToll(t); }   // the room's great stares come with the court's toll
            if (k >= 0.9f)
            {
                _holdT0 = t;
                if (_depth >= 0.6f) _noticeArmed = true;
            }
        }

        void WellBlink()
        {
            if (!_built) return;
            float t = Time.unscaledTime; _wBlinkT0 = t; ScheduleSunBlink(t, false);
        }

        void StartCounterweight(float t) { _cwT0 = t; _cwFrom = 6f; _cwTo = 30f; _cwDur = 24f; _cwNext = t + 24f + 80f + Hash(t) * 40f; }

        float KeyGain(float t)
        {
            float g = 1f + 0.04f * Mathf.Sin(t * Mathf.PI * 2f / 11f);
            // at the verdict the thing that rises behind the lancet stands in the light: the cold key through it falls away
            if (_presence01 > 0f) g *= Mathf.Lerp(1f, 0.52f, S01(_presence01));
            return g;
        }

        float BandLevel(int b, float t)
        {
            if (_waveT0 < 0f) return 0f;
            float lvl = S01((t - _waveT0 - WaveOpen[b]) / WaveFade);
            if (!_waveHold) lvl *= 1f - S01((t - (_waveT0 + 11.8f + WaveClose[b])) / WaveFade);
            float dd = t - _wBlinkT0 - BlinkRipple[b];
            if (dd >= 0f && dd < 0.25f) lvl *= 1f - Mathf.Sin(dd / 0.25f * Mathf.PI);
            return lvl;
        }

        float SunBlink(float t)
        {
            float d = t - _sunBlinkT0;
            if (d < 0f || d > 2.05f) return 1f;
            if (d < 0.5f) return Mathf.Lerp(1f, 0.05f, S01(d / 0.5f));
            if (d < 0.85f) return 0.05f;
            return Mathf.Lerp(0.05f, 1f, S01((d - 0.85f) / 1.2f));
        }

        // ------------------------------------------------------------------ per frame
        void UpdateWell(float dtScaled)
        {
            if (!_built) return;
            var cam = _view != null && _view.ViewCamera != null ? _view.ViewCamera : Camera.main;
            if (_backdrop != null && cam != null) _backdrop.position = cam.transform.position;
            float t = Time.unscaledTime, dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            bool pinned = Pinned, vis = _rv != null && _rv.Visible;
            // a new trial = the court camera comes back after a long absence (cutaways and probe vistas keep the trial going)
            if (pinned && !_wasPinned && (_unpinAt < 0f || t - _unpinAt > 60f)) { ResetTrial(); StartCounterweight(t); }
            if (!pinned && _wasPinned) _unpinAt = t;
            _wasPinned = pinned;
            if (!pinned && _unpinAt > 0f && t - _unpinAt > 30f && (_waveT0 > 0f || _verdictT0 > 0f)) ResetTrial();
            if (vis && !_wasVisible && !pinned) StartCounterweight(t);
            _wasVisible = vis;
            GuardStrays(pinned && vis, dt);
            CleanLens(pinned && vis);
            if (!vis) return;

            if (pinned) _depth = Mathf.Clamp01(_depth + dt / 900f);
            if (_sunBlinkAt > 0f && t >= _sunBlinkAt) { _sunBlinkT0 = t; _lastSunBlink = t; _sunBlinkAt = -1f; }
            if (_wavePendingAt > 0f && t >= _wavePendingAt) { _wavePendingAt = -1f; StartWave(t, false, false); }
            if (_waveT0 > 0f && !_waveHold && t - _waveT0 > 11.8f + 2.8f + 3.2f) _waveT0 = -1f;

            // fog compensation for the additive layers (authored for exp² density 0.020)
            _fogTimer -= dt;
            if (_fogTimer <= 0f)
            {
                _fogTimer = 0.5f;
                float rho = FogStripped ? 0.020f : RenderSettings.fog ? RenderSettings.fogDensity : 0f;   // no fog on screen: nothing to compensate
                float K(float dref) { float a = Mathf.Exp(-(rho * dref) * (rho * dref)), b = Mathf.Exp(-(0.020f * dref) * (0.020f * dref)); return Mathf.Clamp(a / b, 0.25f, 1.6f); }
                _kHaze = K(30f); _kPuffs = K(30f); _kShafts = K(30f);
                for (int b = 0; b < 4; b++) _fogK[b] = K(BandRef[b]);
                if (pinned && !_fogWarned && !FogStripped && (rho < 0.016f || rho > 0.03f)) { _fogWarned = true; Debug.LogWarning($"[CourtWell] fog density {rho:0.000} is outside the court's tuned range (0.016..0.03)"); }
            }

            // motion (frozen by Still; the breath-hold stills the chandelier after the room stares)
            if (!_still) _motionT += dt;
            float hold = _holdT0 > 0f ? (t - _holdT0 < 3f ? 1f - S01((t - _holdT0) / 3f) : S01((t - _holdT0 - 3f) / 10f)) : 1f;
            _chandAmp = Mathf.MoveTowards(_chandAmp, _still ? 0f : hold, dt * 0.5f);
            if (_chandPivot != null)
            {
                float w = _motionT * Mathf.PI * 2f / 21.2f;
                _chandPivot.localRotation = Quaternion.AngleAxis(0.14f * _chandAmp * Mathf.Sin(w), _rt) * Quaternion.AngleAxis(0.09f * _chandAmp * Mathf.Sin(w * 1.07f + 1.1f), _jd);
            }
            for (int i = 0; i < _chainPivot.Length; i++)
            {
                var p = _chainPivot[i]; if (p == null) continue;
                float ph = i == 0 ? 0f : i == 1 ? 1.7f : i == 2 ? 3.1f : 4.4f; float w = _motionT * Mathf.PI * 2f / 21.8f + ph;
                var radial = p.position - _c; radial.y = 0f; radial = radial.sqrMagnitude > 1e-3f ? radial.normalized : _jd; var tang = Vector3.Cross(Vector3.up, radial);
                p.localRotation = Quaternion.AngleAxis(0.2f * Mathf.Sin(w), tang) * Quaternion.AngleAxis(0.13f * Mathf.Sin(w * 1.07f + ph), radial);
            }
            if (_puffs != null) _puffs.localRotation = Quaternion.AngleAxis(_motionT * 0.2f, Vector3.up);
            if (_corona != null) _corona.localRotation = Quaternion.AngleAxis(t * 0.25f, Vector3.up);
            if (_iris != null) _iris.localRotation = Quaternion.AngleAxis(-t * 0.18f, Vector3.up);
            UpdateFish(t, dt);
            if (_counter != null && _cwT0 > 0f)
            {
                if (!_still)
                {
                    if (t >= _cwNext && t - _cwT0 > _cwDur) { _cwT0 = t; _cwFrom = _counterH; _cwTo = Mathf.Clamp(_counterH + (Hash(t * 0.37f) < 0.5f ? -3f : 3f), 8f, 32f); _cwDur = 15f; _cwNext = t + 15f + 80f + Hash(t * 1.3f) * 40f; }
                    float u = S01((t - _cwT0) / _cwDur); _counterH = Mathf.Lerp(_cwFrom, _cwTo, u);
                }
                _counter.position = _c + _liftDir * 10.8f + Vector3.up * _counterH;
            }

            // materials: the Sun, the iris, the eye bands, the haze, the late doorways
            float breathe = 1f + 0.05f * Mathf.Sin(t * Mathf.PI * 2f / 23f);
            if (_mCorona != null) _mCorona.SetFloat(PInt, breathe * SunBlink(t));
            _iris01 = _verdictT0 > 0f ? S01((t - _verdictT0 - 2.5f) / 3f) : Mathf.MoveTowards(_iris01, 0f, dt * 0.3f);
            if (_mIris != null) _mIris.SetFloat(PInt, _iris01);
            for (int b = 0; b < 4; b++) { _band01[b] = BandLevel(b, t); if (_mBand[b] != null) _mBand[b].SetFloat(PInt, _band01[b] * _fogK[b]); }
            float tens = _tensionShown;
            // the air of the well thickens as the trial deepens (and leans in when the room tightens)
            if (_mHaze != null) _mHaze.SetFloat(PInt, _kHaze * (1f + 0.25f * tens) * (1f + 0.3f * _depth));
            if (_mPuffs != null) _mPuffs.SetFloat(PInt, _kPuffs);
            if (_mShafts != null) _mShafts.SetFloat(PInt, _kShafts);
            if (_mDoorsLate != null) _mDoorsLate.SetFloat(PInt, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.8f, _depth)));
            // at the verdict a spiral of doorways lights up the well, step by step, from the bottom
            for (int i = 0; i < SpiralSteps; i++) if (_mDoorsV[i] != null) _mDoorsV[i].SetFloat(PInt, _verdictT0 > 0f ? S01((t - _verdictT0 - 1.5f - i * 1.1f) / 1.5f) : 0f);
            UpdateGathering(cam, dt);
            UpdatePresence(t); UpdateWalker(cam, dt); UpdateBell(dt); UpdateHourglass(t, dt);   // CourtroomView.Omens.cs
            UpdateJury(cam, dt); UpdateWake(t);   // CourtroomView.Jury.cs

            // the noticing turn: a sector turns to the court only while the view camera cannot see it
            if (_noticeArmed && cam != null)
            {
                _noticeTimer -= dt;
                if (_noticeTimer <= 0f)
                {
                    _noticeTimer = 0.2f;
                    GeometryUtility.CalculateFrustumPlanes(cam, _planes);
                    for (int s = 0; s < Sectors; s++)
                    {
                        if (_turned[s] || _watchR[s] == null || _watchCourtMesh[s] == null) continue;
                        if (GeometryUtility.TestPlanesAABB(_planes, _watchR[s].bounds)) continue;
                        var mf = _watchR[s].GetComponent<MeshFilter>(); if (_watchUpMesh[s] == null) _watchUpMesh[s] = mf.sharedMesh;
                        mf.sharedMesh = _watchCourtMesh[s]; _turned[s] = true;
                    }
                }
            }
        }

        /// <summary>Probe/diagnostics: the well's state in one line.</summary>
        public string WellState() =>
            $"depth {_depth:0.00} pinned {Pinned} waves {_waves} hold {_waveHold} bands {_band01[0]:0.00}/{_band01[1]:0.00}/{_band01[2]:0.00}/{_band01[3]:0.00} iris {_iris01:0.00} still {_still} turned {(_turned[0] ? 1 : 0)}{(_turned[1] ? 1 : 0)}{(_turned[2] ? 1 : 0)}{(_turned[3] ? 1 : 0)}{(_turned[4] ? 1 : 0)}{(_turned[5] ? 1 : 0)} fog {RenderSettings.fogDensity:0.000} fogStripped {FogStripped} kHaze {_kHaze:0.00} fish {(_fishT0 > 0f ? "passing" : "parked")} gathered {_gatherShown}/{_gatherWant} pendingWave {(_wavePendingAt > 0f ? (_wavePendingAt - Time.unscaledTime).ToString("0.0") : "-")} eyes {_eyes} watchers {_watchers} presence {_presence01:0.00} walker {_walkU:0.00} lamp {_walkLamp:0.00} bell {_bellAmp:0.0}";

        void OnDestroy()
        {
            foreach (var m in new[] { _mStone, _mStoneFar, _mVoid, _mBackdrop, _mDoorsEarly, _mDoorsLate, _mCorona, _mPupil, _mIris, _mHaze, _mShafts, _mPuffs, _mBand[0], _mBand[1], _mBand[2], _mBand[3], _mBelow, _mFish, _mFishRim })
                if (m != null) Destroy(m);
            foreach (var m in _mDoorsV) if (m != null) Destroy(m);
            foreach (var m in new[] { _mPresence, _mPresenceRim, _mWalkLamp, _mWalkHalo }) if (m != null) Destroy(m);
            if (_mWake != null) Destroy(_mWake);
            if (_mFlame != null) Destroy(_mFlame);
            if (_lensProfile != null) Destroy(_lensProfile);
        }
    }
}
