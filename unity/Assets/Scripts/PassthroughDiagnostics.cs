// Temporary instrumentation for the "compositor says background black" problem.
// The Quest shell reported [App Enabled for PT: 0], which means the app never turned
// Passthrough on. This logs what AR Foundation actually thinks is running.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class PassthroughDiagnostics : MonoBehaviour
{
    public ARCameraManager cameraManager;
    public Camera targetCamera;

    float _timer;
    int _reports;

    void Update()
    {
        if (_reports >= 12) return;
        _timer += Time.deltaTime;
        if (_timer < 1f) return;
        _timer = 0f;
        _reports++;

        var subsystems = new List<XRCameraSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        var running = subsystems.Count > 0 && subsystems[0].running;

        Debug.Log(
            $"[pt] sessionState={ARSession.state} " +
            $"camMgr={(cameraManager == null ? "null" : cameraManager.enabled.ToString())} " +
            $"camSubsystems={subsystems.Count} running={running} " +
            $"clear={targetCamera?.clearFlags} bg={targetCamera?.backgroundColor} " +
            $"descriptor={(subsystems.Count > 0 ? subsystems[0].subsystemDescriptor.id : "none")} " +
            // A head pose frozen at (0.00, 0.00, 0.00) means TrackedPoseDriver is getting
            // nothing and every world-locked object is rendering head-locked.
            $"head={targetCamera?.transform.position.ToString("F2")}");
    }
}
