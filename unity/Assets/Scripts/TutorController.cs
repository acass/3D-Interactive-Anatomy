// Wires the proxy's events to the room: rotate the skull to present the asked-about
// bone, tint that bone, light its callout, stream the caption.
//
// The camera never moves. In XR the camera is the user's head, so the web app's
// cameraOrbit fly-to (src/main.ts focusFeature) becomes a rotation of the object.
using System.Collections.Generic;
using UnityEngine;

public class TutorController : MonoBehaviour
{
    public LiveClient client;
    public SkullPlacement placement;
    public CalloutView callouts;
    public BoneHighlighter highlighter;
    public TutorAudio audioOut;
    public TextMesh caption;
    public TextMesh status;

    [Tooltip("Root the anchors and rotation apply to (the skull's own transform).")]
    public Transform skull;

    [Tooltip("Seconds for the skull to slerp round to a newly focused feature.")]
    public float turnSeconds = 0.8f;

    [Tooltip("Same handedness correction CalloutView applies to anchor positions.")]
    public Vector3 anchorAxisFlip = new Vector3(1f, 1f, -1f);

    readonly Dictionary<string, FeatureDto> _features = new Dictionary<string, FeatureDto>();
    Quaternion _turnFrom, _turnTo;
    float _turnElapsed = -1f;
    string _caption = "";
    Transform _head;

    void Awake()
    {
        if (skull == null) skull = placement != null ? placement.transform : transform;
        _head = Camera.main != null ? Camera.main.transform : null;
    }

    void OnEnable()
    {
        client.OnStatus += SetStatus;
        client.OnManifest += BuildFromManifest;
        client.OnTranscript += AppendCaption;
        client.OnTurnComplete += EndCaption;
        client.OnInterrupted += OnInterrupted;
        client.OnAudio += audioOut.Enqueue;
        client.OnFocus += Focus;
        client.OnReset += ResetView;
    }

    void OnDisable()
    {
        client.OnStatus -= SetStatus;
        client.OnManifest -= BuildFromManifest;
        client.OnTranscript -= AppendCaption;
        client.OnTurnComplete -= EndCaption;
        client.OnInterrupted -= OnInterrupted;
        client.OnAudio -= audioOut.Enqueue;
        client.OnFocus -= Focus;
        client.OnReset -= ResetView;
    }

    void BuildFromManifest(FeatureDto[] features)
    {
        _features.Clear();
        if (features == null) return;
        foreach (var feature in features) _features[feature.id] = feature;

        callouts.anchorAxisFlip = anchorAxisFlip;
        callouts.Build(features, skull);
        callouts.OnCalloutClicked -= OnCalloutClicked;
        callouts.OnCalloutClicked += OnCalloutClicked;
        SetStatus($"manifest: {features.Length} features");
    }

    // Clicking a callout asks the tutor about it, so the spoken answer and the visual
    // focus stay in step -- the web app does the same via focusFeature (src/main.ts:32).
    void OnCalloutClicked(string featureId)
    {
        if (!_features.TryGetValue(featureId, out var feature)) return;
        Focus(featureId);
        client.SendText($"Tell me about the {feature.label}.");
    }

    public void Focus(string featureId)
    {
        if (!_features.TryGetValue(featureId, out var feature))
        {
            Debug.LogWarning($"[TutorController] unknown feature '{featureId}'");
            return;
        }

        callouts.SetActive(featureId);
        highlighter.Highlight(feature.meshNames);
        TurnTo(feature);
    }

    // Rotate the skull about its own centre so the feature's authored outward normal
    // points at the head. The normal is the same one the web app derives its camera
    // orbit from (src/orbit.ts), used here from the other end.
    void TurnTo(FeatureDto feature)
    {
        if (_head == null)
        {
            if (Camera.main == null) return;
            _head = Camera.main.transform;
        }

        var localNormal = Vector3.Scale(feature.RawNormal, anchorAxisFlip).normalized;
        if (localNormal.sqrMagnitude < 0.0001f) return;

        var toHead = (_head.position - skull.position).normalized;
        _turnFrom = skull.rotation;
        _turnTo = Quaternion.FromToRotation(skull.rotation * localNormal, toHead) * skull.rotation;
        _turnElapsed = 0f;
    }

    public void ResetView()
    {
        callouts.SetActive(null);
        highlighter.Clear();
        placement?.Recenter();
        _turnElapsed = -1f;
    }

    void Update()
    {
        if (_turnElapsed < 0f) return;
        _turnElapsed += Time.deltaTime;
        var t = Mathf.Clamp01(_turnElapsed / Mathf.Max(0.01f, turnSeconds));
        skull.rotation = Quaternion.Slerp(_turnFrom, _turnTo, Mathf.SmoothStep(0f, 1f, t));
        if (t >= 1f) _turnElapsed = -1f;
    }

    void OnInterrupted()
    {
        audioOut.Flush();
        _caption = "";
        if (caption != null) caption.text = "";
    }

    void AppendCaption(string text)
    {
        _caption += text;
        if (caption != null) caption.text = Wrap(_caption, 48);
    }

    void EndCaption() => _caption = "";

    void SetStatus(string text)
    {
        if (status != null) status.text = text;
        Debug.Log($"[status] {text}");
    }

    // TextMesh has no word wrap of its own.
    static string Wrap(string text, int columns)
    {
        var words = text.Split(' ');
        var builder = new System.Text.StringBuilder();
        var lineLength = 0;
        foreach (var word in words)
        {
            if (lineLength + word.Length > columns) { builder.Append('\n'); lineLength = 0; }
            builder.Append(word).Append(' ');
            lineLength += word.Length + 1;
        }
        return builder.ToString();
    }
}
