using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// The mansion's planar-reflection camera ("PlanarCam") re-renders the room every frame for the floor's mirror. In the court
    /// the only mirrored surface is a 12 cm obsidian bead in the floor mosaic, so while the view is in the court that camera draws
    /// nothing (culling mask 0 for that one render). When the view is elsewhere on the court's floor band the well alone is left out
    /// of it: switched off just before it renders and restored right after, exactly as the mansion's cull had it. Every other
    /// camera (the trial lens, the probe vistas, the screen) sees the court unchanged.
    /// </summary>
    public sealed partial class CourtroomView
    {
        readonly List<MeshRenderer> _wellRends = new List<MeshRenderer>();
        readonly List<MeshRenderer> _hiddenForCam = new List<MeshRenderer>();
        bool _renderHooked;

        void OnEnable()
        {
            if (_renderHooked) return;
            RenderPipelineManager.beginCameraRendering += BeforeCamera; RenderPipelineManager.endCameraRendering += AfterCamera; _renderHooked = true;
        }

        void OnDisable()
        {
            if (_lens != null) _lens.weight = 0f;
            if (!_renderHooked) return;
            RenderPipelineManager.beginCameraRendering -= BeforeCamera; RenderPipelineManager.endCameraRendering -= AfterCamera; _renderHooked = false;
            RestoreForCam();
        }

        void BeforeCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (!_built || cam == null || cam.name != "PlanarCam" || _rv == null || !_rv.Visible) return;
            // while the view is in the court the reflection camera draws nothing at all: the clock floor is not a mirror and the
            // only planar surface here is a 12 cm bead, which is not worth re-rendering the lit island, its people and its twenty
            // lights every frame (the camera is copied from the view camera each frame, so the mask never outlives this render)
            if (ViewInCourt())
            {
                if (!_planarMasked) { _planarMask = cam.cullingMask; _planarMasked = true; }
                cam.cullingMask = 0;
                if (!_planarLogged) { _planarLogged = true; Debug.Log("[CourtWell] the floor's planar reflection draws nothing in the court"); }
                return;
            }
            for (int i = 0; i < _wellRends.Count; i++) { var r = _wellRends[i]; if (r != null && r.enabled) { r.enabled = false; _hiddenForCam.Add(r); } }
        }
        bool _planarLogged, _planarMasked; int _planarMask;

        /// <summary>Is the view camera in the court (a trial / vista camera, or simply standing in the court room)?</summary>
        bool ViewInCourt()
        {
            if (Pinned) return true;
            var vc = _view != null ? _view.ViewCamera : null;
            return vc != null && _view.RoomAtWorld(vc.transform.position) == RoomId;
        }

        void AfterCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (_planarMasked && cam != null && cam.name == "PlanarCam") { cam.cullingMask = _planarMask; _planarMasked = false; }
            if (_hiddenForCam.Count > 0) RestoreForCam();
        }

        void RestoreForCam()
        {
            for (int i = 0; i < _hiddenForCam.Count; i++) if (_hiddenForCam[i] != null) _hiddenForCam[i].enabled = true;
            _hiddenForCam.Clear();
        }
    }
}
