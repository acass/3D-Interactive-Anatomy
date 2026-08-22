// Callouts in 3D: a small dot at every anchor all the time, and a text label that
// expands for the focused feature or for whichever dot you are pointing at.
//
// Seven text labels around a 0.24m skull viewed from 0.6m would overlap into mush,
// which is why the web app's always-on labels did not survive the port.
using System;
using System.Collections.Generic;
using UnityEngine;

public class CalloutView : MonoBehaviour
{
    public float dotRadius = 0.006f;
    public float labelOffset = 0.03f;
    public float labelScale = 0.004f;
    public Color dotColor = new Color(0.95f, 0.95f, 1f);
    public Color activeColor = new Color(0.35f, 0.65f, 1f);

    [Tooltip("Assigned by SceneBuilder from a real asset. Do NOT Shader.Find at runtime: " +
             "an unreferenced shader is stripped from the build and Find returns null.")]
    public Material dotMaterial;

    [Tooltip("Assigned by SceneBuilder for the same reason as dotMaterial.")]
    public Font labelFont;

    [Tooltip("Controller or hand ray used to reveal a label by pointing. Optional.")]
    public Transform pointer;
    public float pointerRange = 3f;

    public event Action<string> OnCalloutClicked;

    class Callout
    {
        public string Id;
        public GameObject Dot;
        public GameObject Label;
        public Renderer DotRenderer;
    }

    readonly List<Callout> _callouts = new List<Callout>();
    readonly Dictionary<string, Callout> _byId = new Dictionary<string, Callout>();
    Material _dotMaterial; // shared asset, never instantiated or destroyed here
    Transform _head;
    string _activeId;
    string _pointedId;

    void Awake() => _head = Camera.main != null ? Camera.main.transform : null;

    public void Build(FeatureDto[] features, Transform anchorSpace)
    {
        Clear();
        if (features == null) return;

        if (dotMaterial == null)
        {
            Debug.LogError("[CalloutView] dotMaterial is not assigned; callouts disabled");
            return;
        }
        _dotMaterial = dotMaterial;

        var boneRenderers = anchorSpace.GetComponentsInChildren<Renderer>(true);

        foreach (var feature in features)
        {
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = $"callout-{feature.id}";
            dot.transform.SetParent(anchorSpace, false);
            dot.transform.localPosition = feature.LocalPosition;
            dot.transform.localScale = Vector3.one * (dotRadius * 2f);
            WarnIfOffBone(feature, dot.transform.position, boneRenderers);
            var dotRenderer = dot.GetComponent<Renderer>();
            dotRenderer.sharedMaterial = _dotMaterial;
            dotRenderer.SetPropertyBlock(ColorBlock(dotColor));

            // ponytail: legacy TextMesh, not TextMeshPro -- TMP needs its essential
            // resources imported before it renders at all, and these labels are three
            // words each. Swap to TMP if legibility on device disappoints.
            var label = new GameObject($"label-{feature.id}");
            label.transform.SetParent(dot.transform, false);
            label.transform.localPosition = Vector3.up * (labelOffset / (dotRadius * 2f));
            label.transform.localScale = Vector3.one * (labelScale / (dotRadius * 2f));
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

            var callout = new Callout { Id = feature.id, Dot = dot, Label = label, DotRenderer = dotRenderer };
            _callouts.Add(callout);
            _byId[feature.id] = callout;
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
            Debug.LogWarning($"[CalloutView] '{feature.id}' dot is outside its bone bounds; " +
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
        {
            var isActive = callout.Id == _activeId;
            var isPointed = callout.Id == _pointedId;
            callout.Label.SetActive(isActive || isPointed);
            callout.DotRenderer.SetPropertyBlock(ColorBlock(isActive ? activeColor : dotColor));
            callout.Dot.transform.localScale = Vector3.one * (dotRadius * 2f) * (isActive ? 1.6f : 1f);
        }
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

    void UpdatePointing()
    {
        if (pointer == null) return;
        string hitId = null;
        if (Physics.Raycast(pointer.position, pointer.forward, out var hit, pointerRange))
        {
            foreach (var callout in _callouts)
                if (hit.collider.gameObject == callout.Dot) { hitId = callout.Id; break; }
        }
        if (hitId == _pointedId) return;
        _pointedId = hitId;
        Refresh();
    }

    // Called by the input script when the pointed-at callout is selected.
    public void ClickPointed()
    {
        if (_pointedId != null) OnCalloutClicked?.Invoke(_pointedId);
    }

    public void Clear()
    {
        foreach (var callout in _callouts) Destroy(callout.Dot);
        _callouts.Clear();
        _byId.Clear();
        _activeId = null;
        _pointedId = null;
    }

    static MaterialPropertyBlock _block;
    static MaterialPropertyBlock ColorBlock(Color color)
    {
        _block ??= new MaterialPropertyBlock();
        _block.SetColor("_BaseColor", color);
        return _block;
    }
}
