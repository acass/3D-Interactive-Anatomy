// Controller input: grip to move the skull, trigger to select a bone, recenter, mute.
//
// Uses UnityEngine.XR.InputDevices (built into the XR module) rather than XRI's
// interactors. XRI is installed and is the right home for richer interaction later,
// but its grab path needs an InteractionManager, ray interactors and an InputActionAsset
// authored in the scene -- all of which have to be hand-built for a batchmode scene.
// ponytail: 60 lines of device polling instead; move to XRGrabInteractable
// (UnityEngine.XR.Interaction.Toolkit.Interactables) when hand tracking gets tuned.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class XRControls : MonoBehaviour
{
    public Transform skull;
    public SkullPlacement placement;
    public CalloutView callouts;
    public MicStreamer mic;

    [Tooltip("Follows the right controller; CalloutView raycasts along it.")]
    public Transform pointer;

    public float grabRange = 2f;

    InputDevice _right;
    InputDevice _left;
    bool _wasGrabbing;
    bool _wasTrigger;
    bool _wasRecenter;
    bool _wasMute;
    Vector3 _grabOffset;
    Quaternion _grabRotationOffset;

    void Update()
    {
        EnsureDevices();
        if (!_right.isValid) return;

        _right.TryGetFeatureValue(CommonUsages.devicePosition, out var pos);
        _right.TryGetFeatureValue(CommonUsages.deviceRotation, out var rot);
        if (pointer != null)
        {
            // Device pose is in tracking space; the camera's parent is that space.
            var space = Camera.main != null ? Camera.main.transform.parent : null;
            pointer.position = space != null ? space.TransformPoint(pos) : pos;
            pointer.rotation = space != null ? space.rotation * rot : rot;
        }

        HandleGrab();
        HandleButtons();
    }

    void EnsureDevices()
    {
        if (!_right.isValid) _right = GetDevice(InputDeviceCharacteristics.Right);
        if (!_left.isValid) _left = GetDevice(InputDeviceCharacteristics.Left);
    }

    static InputDevice GetDevice(InputDeviceCharacteristics side)
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller | side, devices);
        return devices.Count > 0 ? devices[0] : default;
    }

    void HandleGrab()
    {
        if (skull == null || pointer == null) return;
        _right.TryGetFeatureValue(CommonUsages.triggerButton, out var trigger);
        _right.TryGetFeatureValue(CommonUsages.gripButton, out var grip);

        // Trigger with the ray on a bone selects it, same as clicking a callout in the
        // web app. Grip moves the skull, so the two never compete for one button.
        if (trigger && !_wasTrigger) callouts?.ClickPointed();
        _wasTrigger = trigger;

        if (grip && !_wasGrabbing)
        {
            if (Physics.Raycast(pointer.position, pointer.forward, out var hit, grabRange)
                && hit.transform.IsChildOf(skull))
            {
                _grabOffset = Quaternion.Inverse(pointer.rotation) * (skull.position - pointer.position);
                _grabRotationOffset = Quaternion.Inverse(pointer.rotation) * skull.rotation;
                _wasGrabbing = true;
            }
        }
        else if (grip && _wasGrabbing)
        {
            skull.position = pointer.position + pointer.rotation * _grabOffset;
            skull.rotation = pointer.rotation * _grabRotationOffset;
        }
        else if (!grip)
        {
            _wasGrabbing = false;
        }
    }

    void HandleButtons()
    {
        // A / X: bring the skull back in front of you.
        _right.TryGetFeatureValue(CommonUsages.primaryButton, out var primary);
        if (primary && !_wasRecenter) placement?.Recenter();
        _wasRecenter = primary;

        // B / Y: mute the always-on mic.
        _right.TryGetFeatureValue(CommonUsages.secondaryButton, out var secondary);
        if (secondary && !_wasMute) mic?.ToggleMute();
        _wasMute = secondary;
    }
}
