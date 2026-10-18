using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

namespace CBMod
{
    // Zooms the arena camera out.
    //
    // What we learned from the first version: scaling only CinemachineFramingTransposer.m_CameraDistance
    // did nothing visible. The game's camera uses group framing (min distance 50, max 75), so the distance
    // is computed every frame from the group size and only *clamped* to [min, max]. Depending on the
    // adjustment mode it may even be the FOV (zoom) that is driven instead of the distance (dolly).
    // So instead of one value we scale the whole framing range (distance, min/max distance, min/max FOV,
    // min/max ortho size), and we pick what to scale from the transposer's AdjustmentMode (ZoomMethod = Auto).
    //
    // All values are written as "original x factor" every tick, so nothing compounds. If the game itself
    // changes a value we wrote, that value is taken as the new original.
    internal static class CameraZoom
    {
        private class State
        {
            public bool Have;
            public float Dist, MinDist, MaxDist, MinFov, MaxFov, MinOrtho, MaxOrtho, LensFov, LensOrtho;
            public float LastDist, LastLensFov, LastLensOrtho;
            public bool Applied;
        }

        private static readonly Dictionary<int, State> States = new Dictionary<int, State>();
        private static float _lastFactor;
        private static int _lastTargetCount = -1;
        private static bool _errorLogged;

        // Call from LateUpdate so it runs after the game's own camera code. Returns an on-screen note when
        // a new zoom value was applied (factor or target changed), otherwise null.
        public static string Tick()
        {
            try
            {
                var targets = FindTargets();
                float f = Mathf.Max(1f, Cfg.CameraZoomFactor.Value);
                string note = null;

                foreach (var (vcam, ft) in targets)
                {
                    int id = vcam.GetInstanceID();
                    if (!States.TryGetValue(id, out var st)) { st = new State(); States[id] = st; }

                    if (!Features.CameraZoomOut)
                    {
                        if (st.Applied) { Restore(vcam, ft, st); st.Applied = false; st.Have = false; }
                        continue;
                    }

                    var lens = vcam.m_Lens;
                    bool ortho = lens.Orthographic;

                    // First sight, or the game changed a value we wrote: remember it as the original.
                    if (!st.Have)
                    {
                        Capture(ft, lens, st);
                        st.Have = true;
                        Plugin.L.LogInfo($"Camera zoom: '{vcam.name}' captured: distance={st.Dist} (min {st.MinDist}, max {st.MaxDist}), " +
                                         $"fov min/max={st.MinFov}/{st.MaxFov}, lensFov={st.LensFov}, ortho={ortho} size={st.LensOrtho}, " +
                                         $"adjustment={ft.m_AdjustmentMode}, groupFraming={ft.m_GroupFramingMode}, groupSize={ft.m_GroupFramingSize}, " +
                                         $"maxDollyIn/Out={ft.m_MaxDollyIn}/{ft.m_MaxDollyOut}");
                    }
                    else
                    {
                        if (st.Applied && Mathf.Abs(ft.m_CameraDistance - st.LastDist) > 0.001f) st.Dist = ft.m_CameraDistance;
                        if (st.Applied && Mathf.Abs(lens.FieldOfView - st.LastLensFov) > 0.001f) st.LensFov = lens.FieldOfView;
                        if (st.Applied && Mathf.Abs(lens.OrthographicSize - st.LastLensOrtho) > 0.001f) st.LensOrtho = lens.OrthographicSize;
                    }

                    string how = Apply(vcam, ft, st, f, ortho);
                    st.Applied = true;

                    if (Mathf.Abs(f - _lastFactor) > 0.0001f || targets.Count != _lastTargetCount)
                    {
                        Plugin.L.LogInfo($"Camera zoom: applied ({how}) x{f} on '{vcam.name}': distance={ft.m_CameraDistance} " +
                                         $"(min {ft.m_MinimumDistance}, max {ft.m_MaximumDistance}), fov min/max={ft.m_MinimumFOV}/{ft.m_MaximumFOV}, lensFov={vcam.m_Lens.FieldOfView}");
                        note = Loc.F("msg.zoom_applied", ft.m_CameraDistance.ToString("F1"), how);
                    }
                }

                if (Features.CameraZoomOut) { _lastFactor = f; _lastTargetCount = targets.Count; }
                else { _lastFactor = 0f; _lastTargetCount = -1; }
                return note;
            }
            catch (Exception e)
            {
                if (!_errorLogged) { _errorLogged = true; Plugin.L.LogError("Camera zoom error: " + e); }
                return null;
            }
        }

        private static void Capture(CinemachineFramingTransposer ft, LensSettings lens, State st)
        {
            st.Dist = ft.m_CameraDistance;
            st.MinDist = ft.m_MinimumDistance; st.MaxDist = ft.m_MaximumDistance;
            st.MinFov = ft.m_MinimumFOV; st.MaxFov = ft.m_MaximumFOV;
            st.MinOrtho = ft.m_MinimumOrthoSize; st.MaxOrtho = ft.m_MaximumOrthoSize;
            st.LensFov = lens.FieldOfView; st.LensOrtho = lens.OrthographicSize;
        }

        // Returns a short description of what was scaled.
        private static string Apply(CinemachineVirtualCamera vcam, CinemachineFramingTransposer ft, State st, float f, bool ortho)
        {
            string method = (Cfg.ZoomMethod.Value ?? "Auto").Trim();
            string adj = ft.m_AdjustmentMode.ToString();
            bool dolly, zoom;
            if (method.Equals("Distance", StringComparison.OrdinalIgnoreCase)) { dolly = true; zoom = false; }
            else if (method.Equals("Fov", StringComparison.OrdinalIgnoreCase)) { dolly = false; zoom = true; }
            else if (method.Equals("Both", StringComparison.OrdinalIgnoreCase)) { dolly = true; zoom = true; }
            else   // Auto: follow what the transposer itself drives
            {
                zoom = adj.IndexOf("Zoom", StringComparison.OrdinalIgnoreCase) >= 0;
                dolly = adj.IndexOf("Dolly", StringComparison.OrdinalIgnoreCase) >= 0 || !zoom;
            }

            var parts = new List<string>();
            if (dolly)
            {
                ft.m_MinimumDistance = st.MinDist * f;
                ft.m_MaximumDistance = st.MaxDist * f;   // also lifts the game's 75 clamp
                ft.m_CameraDistance = st.Dist * f;
                st.LastDist = ft.m_CameraDistance;
                parts.Add("distance");
            }
            if (zoom)
            {
                if (ortho)
                {
                    ft.m_MinimumOrthoSize = st.MinOrtho * f;
                    ft.m_MaximumOrthoSize = st.MaxOrtho * f;
                    var lens = vcam.m_Lens;
                    lens.OrthographicSize = st.LensOrtho * f;
                    vcam.m_Lens = lens;
                    st.LastLensOrtho = lens.OrthographicSize;
                    parts.Add("ortho size");
                }
                else
                {
                    // Cinemachine FOV above ~120 degrees looks broken, so cap it.
                    ft.m_MinimumFOV = Mathf.Min(st.MinFov * f, 120f);
                    ft.m_MaximumFOV = Mathf.Min(st.MaxFov * f, 120f);
                    var lens = vcam.m_Lens;
                    lens.FieldOfView = Mathf.Min(st.LensFov * f, 120f);
                    vcam.m_Lens = lens;
                    st.LastLensFov = lens.FieldOfView;
                    parts.Add("fov");
                }
            }
            return string.Join("+", parts);
        }

        private static void Restore(CinemachineVirtualCamera vcam, CinemachineFramingTransposer ft, State st)
        {
            ft.m_CameraDistance = st.Dist;
            ft.m_MinimumDistance = st.MinDist; ft.m_MaximumDistance = st.MaxDist;
            ft.m_MinimumFOV = st.MinFov; ft.m_MaximumFOV = st.MaxFov;
            ft.m_MinimumOrthoSize = st.MinOrtho; ft.m_MaximumOrthoSize = st.MaxOrtho;
            var lens = vcam.m_Lens;
            lens.FieldOfView = st.LensFov;
            lens.OrthographicSize = st.LensOrtho;
            vcam.m_Lens = lens;
            Plugin.L.LogInfo($"Camera zoom: restored '{vcam.name}'");
        }

        // The virtual cameras that are live and use a framing transposer, falling back to ArenaCamera's own.
        private static List<(CinemachineVirtualCamera, CinemachineFramingTransposer)> FindTargets()
        {
            var result = new List<(CinemachineVirtualCamera, CinemachineFramingTransposer)>();
            var brain = UnityEngine.Object.FindObjectOfType<CinemachineBrain>();
            string activeName = null;
            if (brain != null)
            {
                var active = brain.ActiveVirtualCamera;
                if (active != null) activeName = active.Name;
            }

            var all = UnityEngine.Object.FindObjectsOfType<CinemachineVirtualCamera>();
            foreach (var v in all)
            {
                if (v == null || !v.isActiveAndEnabled) continue;
                if (activeName != null && v.name != activeName) continue;
                var ft = v.GetCinemachineComponent<CinemachineFramingTransposer>();
                if (ft != null) result.Add((v, ft));
            }

            if (result.Count == 0 && ArenaCamera.InstanceExists)
            {
                var cam = ArenaCamera.Instance;
                if (cam.cinemachineCamera != null && cam.framingTransposer != null)
                    result.Add((cam.cinemachineCamera, cam.framingTransposer));
            }
            return result;
        }

        // Logs every camera and virtual camera in the scene. Used to find out which one really shows the game.
        public static void Dump()
        {
            try
            {
                Plugin.L.LogInfo("Camera dump: ---- begin ----");
                foreach (var c in Camera.allCameras)
                    Plugin.L.LogInfo($"Camera '{c.name}': enabled={c.enabled} ortho={c.orthographic} fov={c.fieldOfView} orthoSize={c.orthographicSize} " +
                                     $"depth={c.depth} pos={c.transform.position} brain={(c.GetComponent<CinemachineBrain>() != null)}");

                var brain = UnityEngine.Object.FindObjectOfType<CinemachineBrain>();
                Plugin.L.LogInfo("Camera dump: brain=" + (brain != null ? brain.name : "NONE") +
                                 " active=" + (brain != null && brain.ActiveVirtualCamera != null ? brain.ActiveVirtualCamera.Name : "none"));

                foreach (var v in UnityEngine.Object.FindObjectsOfType<CinemachineVirtualCamera>())
                {
                    var ft = v.GetCinemachineComponent<CinemachineFramingTransposer>();
                    Plugin.L.LogInfo($"VCam '{v.name}': enabled={v.isActiveAndEnabled} priority={v.Priority} ortho={v.m_Lens.Orthographic} " +
                                     $"fov={v.m_Lens.FieldOfView} orthoSize={v.m_Lens.OrthographicSize} framingTransposer={(ft != null)}" +
                                     (ft != null ? $" distance={ft.m_CameraDistance} min={ft.m_MinimumDistance} max={ft.m_MaximumDistance} " +
                                                   $"adjustment={ft.m_AdjustmentMode} groupFraming={ft.m_GroupFramingMode}" : ""));
                }
                Plugin.L.LogInfo("Camera dump: ---- end ----");
            }
            catch (Exception e) { Plugin.L.LogError("Camera dump error: " + e); }
        }
    }
}
