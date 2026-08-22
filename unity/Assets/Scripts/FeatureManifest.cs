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

    // Raw glTF-space values. glTF is right-handed and Unity is left-handed, so the
    // importer flips one axis -- WHICH one is an importer detail we do not assume
    // here. TutorController.anchorAxisFlip carries the correction and is verified
    // against a known anchor on device (mandible must land on the lower jaw).
    public Vector3 RawPosition => ParseVec(position);
    public Vector3 RawNormal => ParseVec(normal);

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
