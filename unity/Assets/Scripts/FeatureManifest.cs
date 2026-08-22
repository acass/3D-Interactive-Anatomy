// DTOs for the Feature Manifest the proxy pushes on connect (server/proxy.ts).
// The manifest is authored once in src/manifest.ts (ADR 0002); nothing here is a
// second copy of it. `explanation` is intentionally absent: it stays server-side.
using System;
using System.Globalization;
using UnityEngine;

[Serializable]
public class FeatureDto
{
    public string id;
    public string label;
    public string position;    // "x y z" in glTF model space
    public string normal;      // "x y z" outward surface normal
    public string[] meshNames; // GLB node names, including mirrored halves

    // Anchors are authored in glTF space (right-handed, +Z = face), which is what
    // model-viewer renders directly. glTFast converts to Unity's left-handed space by
    // negating X only -- measured, not assumed: com.unity.cloud.gltfast
    // Runtime/Scripts/NodeExtension.cs:65 (-node.translation[0]) and
    // Runtime/Scripts/Jobs.cs:771 (tmp.x *= -1). SKULL.glb has flat nodes with no
    // transforms, so this one flip is the whole conversion. CalloutView.Build warns
    // if a dot lands outside its bone's bounds, which is how a wrong flip shows up.
    public static readonly Vector3 AnchorAxisFlip = new Vector3(-1f, 1f, 1f);

    // Anchor in the skull root's local space (same parent as the imported mesh).
    public Vector3 LocalPosition => Vector3.Scale(ParseVec(position), AnchorAxisFlip);
    public Vector3 LocalNormal => Vector3.Scale(ParseVec(normal), AnchorAxisFlip);

    // Mirrors parseVec in src/orbit.ts: whitespace-separated floats.
    public static Vector3 ParseVec(string s)
    {
        if (string.IsNullOrEmpty(s)) return Vector3.zero;
        var parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) return Vector3.zero;
        return new Vector3(
            float.Parse(parts[0], CultureInfo.InvariantCulture),
            float.Parse(parts[1], CultureInfo.InvariantCulture),
            float.Parse(parts[2], CultureInfo.InvariantCulture));
    }
}

// Every proxy -> client message flattened into one shape. JsonUtility leaves
// absent fields at their defaults, so one type covers every message type.
[Serializable]
public class ProxyMessage
{
    public string type;       // status | manifest | transcript | turnComplete | interrupted | audio | focus | reset
    public string status;
    public string text;
    public string data;       // base64 24kHz PCM16
    public string featureId;
    public FeatureDto[] features;
}
