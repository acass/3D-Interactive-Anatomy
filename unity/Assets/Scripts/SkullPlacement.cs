// Puts the skull an arm's length in front of you at launch, then leaves it in the room.
// It is a physical object: no auto-rotate, no idle timeout (both dropped from the web
// version deliberately -- an object that re-orients while you lean in breaks passthrough).
using UnityEngine;

public class SkullPlacement : MonoBehaviour
{
    [Tooltip("Metres in front of the head on spawn and recenter.")]
    public float distance = 0.6f;

    [Tooltip("Metres below eye height, so it sits in comfortable view.")]
    public float dropBelowEyes = 0.1f;

    [Tooltip("Life-size skull is ~0.24m tall. Tune on device without a rebuild.")]
    public float scale = 1.0f;

    [Tooltip("Frames to wait for a valid head pose before placing.")]
    public int settleFrames = 30;

    Transform _head;
    int _frames;
    bool _placed;

    void Awake()
    {
        _head = Camera.main != null ? Camera.main.transform : null;
        transform.localScale = Vector3.one * scale;
    }

    void Update()
    {
        if (_placed) return;
        if (_head == null)
        {
            if (Camera.main == null) return;
            _head = Camera.main.transform;
        }
        // The head pose is (0,0,0) for the first frames of an XR session; placing then
        // buries the skull in the floor at the origin.
        if (++_frames < settleFrames) return;
        Recenter();
        _placed = true;
    }

    // Also invoked by the tutor's resetView and by the recenter button.
    public void Recenter()
    {
        if (_head == null) return;
        var forward = _head.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        transform.position = _head.position + forward * distance + Vector3.down * dropBelowEyes;
        transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
        transform.localScale = Vector3.one * scale;
    }
}
