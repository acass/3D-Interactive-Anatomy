// Keeps the caption and status text below the skull and readable, whatever the skull
// is doing. As plain children they inherit the skull's rotation: mirrored at rest
// (SkullPlacement points the skull's +Z at the viewer, and a TextMesh reads correctly
// only from behind its +Z) and upside down after a turn.
using UnityEngine;

public class HeadLabel : MonoBehaviour
{
    [Tooltip("Transform this label follows -- the skull root.")]
    public Transform anchor;

    [Tooltip("World-space offset from the anchor. Not rotated with it.")]
    public Vector3 worldOffset = new Vector3(0f, -0.18f, 0f);

    Transform _head;

    void LateUpdate()
    {
        if (anchor == null) return;
        if (_head == null)
        {
            if (Camera.main == null) return;
            _head = Camera.main.transform;
        }

        transform.position = anchor.position + worldOffset;
        // Same billboard as CalloutView: a TextMesh reads correctly when its +Z points
        // away from the viewer.
        transform.rotation = Quaternion.LookRotation(transform.position - _head.position, Vector3.up);
    }
}
