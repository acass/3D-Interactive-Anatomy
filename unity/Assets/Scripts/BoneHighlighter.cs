// Tints the actual bone sub-mesh when a feature is focused -- the thing model-viewer
// could not do (docs/adr/0001) and the reason the Quest build renders in Unity.
//
// The GLB shares ONE material across all 55 nodes, so highlighting must go through a
// MaterialPropertyBlock per renderer. Touching renderer.material would instance the
// material and silently break every other bone.
using System.Collections.Generic;
using UnityEngine;

public class BoneHighlighter : MonoBehaviour
{
    [Tooltip("Root of the imported skull; children are matched by GLB node name.")]
    public Transform skullRoot;

    public Color highlightColor = new Color(0.35f, 0.65f, 1f);

    [Tooltip("Shader properties tried in order. The first one the material actually has " +
             "is used, so this survives whichever shader glTFast assigns on import.")]
    public string[] candidateProperties = { "_EmissionColor", "_EmissiveFactor", "_BaseColor", "_Color" };

    readonly Dictionary<string, List<Renderer>> _byName =
        new Dictionary<string, List<Renderer>>(System.StringComparer.OrdinalIgnoreCase);
    readonly List<Renderer> _lit = new List<Renderer>();

    MaterialPropertyBlock _block;
    string _property;
    bool _isEmissionProperty;
    Color _restoreColor = Color.white;

    void Awake()
    {
        _block = new MaterialPropertyBlock();
        if (skullRoot == null) skullRoot = transform;

        foreach (var renderer in skullRoot.GetComponentsInChildren<Renderer>(true))
        {
            var key = renderer.gameObject.name;
            if (!_byName.TryGetValue(key, out var list))
                _byName[key] = list = new List<Renderer>();
            list.Add(renderer);

            if (_property == null) ResolveProperty(renderer.sharedMaterial);
        }

        if (_property == null)
            Debug.LogError("[BoneHighlighter] no usable colour property found; highlighting is disabled");
        else
            Debug.Log($"[BoneHighlighter] tinting via {_property} across {_byName.Count} nodes, restore={_restoreColor}");
    }

    void ResolveProperty(Material material)
    {
        if (material == null) return;
        foreach (var candidate in candidateProperties)
        {
            if (!material.HasProperty(candidate)) continue;
            _property = candidate;
            _isEmissionProperty = candidate.Contains("Emission") || candidate.Contains("Emissive");
            // Restore to whatever the model actually ships with, not an assumed white.
            _restoreColor = _isEmissionProperty ? Color.black : material.GetColor(candidate);
            // Emission contributes nothing unless the keyword is on; the base colour
            // stays black so unhighlighted bones look unchanged.
            if (_isEmissionProperty) material.EnableKeyword("_EMISSION");
            return;
        }
    }

    public void Highlight(IEnumerable<string> meshNames)
    {
        Clear();
        if (_property == null || meshNames == null) return;

        foreach (var name in meshNames)
        {
            if (!_byName.TryGetValue(name, out var renderers))
            {
                Debug.LogWarning($"[BoneHighlighter] no node named '{name}' in the model");
                continue;
            }
            foreach (var renderer in renderers)
            {
                renderer.GetPropertyBlock(_block);
                _block.SetColor(_property, highlightColor);
                renderer.SetPropertyBlock(_block);
                _lit.Add(renderer);
            }
        }
    }

    public void Clear()
    {
        foreach (var renderer in _lit)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(_block);
            _block.SetColor(_property, _restoreColor);
            renderer.SetPropertyBlock(_block);
        }
        _lit.Clear();
    }
}
