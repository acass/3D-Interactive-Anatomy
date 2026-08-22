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

    // The recenter button (A/X). Moves the skull back to arm's length in front of you.
    public void Recenter()
    {
        if (_head == null) return;
        transform.position = _head.position + Flattened(_head.forward) * distance
                             + Vector3.down * dropBelowEyes;
        FaceHead();
    }

    // Neutral pose without moving it. The tutor's resetView uses this: once the skull is
    // world-locked it is a thing on your table, and teleporting it to your face because
    // you said "show the whole skull" breaks that. Moving it stays a deliberate act --
    // the grab trigger or the recenter button.
    public void FaceHead()
    {
        if (_head == null) return;
        // Derived from where the skull actually is, not from where the head is looking:
        // after a grab the skull can be anywhere in the room, and head.forward would
        // orient it to face a wall.
        var toHead = Flattened(_head.position - transform.position);
        // The skull's +Z faces the viewer at rest, so its +Z points at the head.
        transform.rotation = Quaternion.LookRotation(toHead, Vector3.up);
        transform.localScale = Vector3.one * scale;
    }

    static Vector3 Flattened(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
        return direction.normalized;
    }
}
