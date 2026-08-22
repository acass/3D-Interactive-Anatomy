// Labels in 3D: one text label per feature at its anchor, shown for the focused feature
// or for whichever bone you are pointing at. The bone itself is the marker (BoneHighlighter
// tints it), so there is no dot. Seven always-on labels around a 0.24m skull viewed from
// 0.6m would overlap into mush, which is why the web app's always-on labels did not
// survive the port.
using System;
using System.Collections.Generic;
using UnityEngine;

public class CalloutView : MonoBehaviour
{
    public float labelOffset = 0.03f;
    public float labelScale = 0.004f;

    [Tooltip("Assigned by SceneBuilder from a real asset. Do NOT load at runtime: " +
             "an unreferenced asset is stripped from the build.")]
    public Font labelFont;

    [Tooltip("Controller or hand ray; pointing it at a bone reveals that bone's label. Optional.")]
    public Transform pointer;
    public float pointerRange = 3f;

    public event Action<string> OnCalloutClicked;

    class Callout
    {
        public string Id;
        public GameObject Label;
    }

    readonly List<Callout> _callouts = new List<Callout>();
    // GLB node name -> feature id, so a ray hit on "Parietal bone right" resolves to "parietal".
    readonly Dictionary<string, string> _featureByNode =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    Transform _head;
    string _activeId;
    string _pointedId;

    void Awake() => _head = Camera.main != null ? Camera.main.transform : null;

    public void Build(FeatureDto[] features, Transform anchorSpace)
    {
        Clear();
        if (features == null) return;

        var boneRenderers = anchorSpace.GetComponentsInChildren<Renderer>(true);

        foreach (var feature in features)
        {
            WarnIfOffBone(feature, anchorSpace.TransformPoint(feature.LocalPosition), boneRenderers);

            // ponytail: legacy TextMesh, not TextMeshPro -- TMP needs its essential
            // resources imported before it renders at all, and these labels are three
            // words each. Swap to TMP if legibility on device disappoints.
            var label = new GameObject($"label-{feature.id}");
            label.transform.SetParent(anchorSpace, false);
            label.transform.localPosition = feature.LocalPosition + Vector3.up * labelOffset;
            label.transform.localScale = Vector3.one * labelScale;
            var text = label.AddComponent<TextMesh>();
            text.text = feature.label;
            text.font = labelFont;
            text.fontSize = 64;
            text.characterSize = 1f;
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;
            if (text.font != null)
                label.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            label.SetActive(false);

            _callouts.Add(new Callout { Id = feature.id, Label = label });
            if (feature.meshNames != null)
                foreach (var node in feature.meshNames) _featureByNode[node] = feature.id;
        }
    }

    // ponytail: AABB containment, not surface distance -- enough to catch an axis
    // mirror, which is the only way these anchors have gone wrong so far.
    static void WarnIfOffBone(FeatureDto feature, Vector3 worldPos, Renderer[] renderers)
    {
        if (feature.meshNames == null || feature.meshNames.Length == 0) return;
        var matched = false;
        foreach (var renderer in renderers)
        {
            var name = renderer.gameObject.name;
            if (!Array.Exists(feature.meshNames, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                continue;
            matched = true;
            var bounds = renderer.bounds;
            bounds.Expand(0.02f); // 1cm of slack each side: anchors sit on the surface
            if (bounds.Contains(worldPos)) return;
        }
        // Unknown mesh names are BoneHighlighter's warning, not ours.
        if (matched)
            Debug.LogWarning($"[CalloutView] '{feature.id}' anchor is outside its bone bounds; " +
                             "check FeatureDto.AnchorAxisFlip");
    }

    public void SetActive(string featureId)
    {
        _activeId = featureId;
        Refresh();
    }

    void Refresh()
    {
        foreach (var callout in _callouts)
            callout.Label.SetActive(callout.Id == _activeId || callout.Id == _pointedId);
    }

    void LateUpdate()
    {
        if (_head == null)
        {
            if (Camera.main == null) return;
            _head = Camera.main.transform;
        }

        foreach (var callout in _callouts)
        {
            if (!callout.Label.activeSelf) continue;
            callout.Label.transform.rotation =
                Quaternion.LookRotation(callout.Label.transform.position - _head.position, Vector3.up);
        }

        UpdatePointing();
    }

    // The ray hits the bone's own MeshCollider (SceneBuilder.AddCollidersForPointing);
    // the collider's node name maps back to the feature through meshNames.
    void UpdatePointing()
    {
        if (pointer == null) return;
        string hitId = null;
        if (Physics.Raycast(pointer.position, pointer.forward, out var hit, pointerRange))
            _featureByNode.TryGetValue(hit.collider.gameObject.name, out hitId);
        if (hitId == _pointedId) return;
        _pointedId = hitId;
        Refresh();
    }

    // Called by the input script when the pointed-at bone is selected.
    public void ClickPointed()
    {
        if (_pointedId != null) OnCalloutClicked?.Invoke(_pointedId);
    }

    public void Clear()
    {
        foreach (var callout in _callouts) Destroy(callout.Label);
        _callouts.Clear();
        _featureByNode.Clear();
        _activeId = null;
        _pointedId = null;
    }
}
