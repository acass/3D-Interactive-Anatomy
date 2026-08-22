// Builds the Quest scene from code so it is reviewable and re-runnable. Run:
//   Unity -batchmode -nographics -projectPath unity -executeMethod SceneBuilder.Build -quit -logFile -
//
// Passthrough requirements come from the installed package docs
// (com.unity.xr.meta-openxr Documentation~/features/camera.md):
//   - an ARCameraManager on the camera enables Passthrough
//   - the camera background must be Solid Color with alpha 0, or Passthrough is covered
//   - the Tracked Pose Driver must bind centerEyePosition on the XR HMD
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

public static class SceneBuilder
{
    const string ScenePath = "Assets/Scenes/SkullTutorXR.unity";
    const string ModelPath = "Assets/Models/SKULL.glb";

    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildLighting();
        var camera = BuildRig(out var pointer);
        var skull = BuildSkull(out var placement, out var highlighter, out var caption, out var status);
        BuildTutor(camera, pointer, skull, placement, highlighter, caption, status);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log($"[scene] saved {ScenePath}");
    }

    static void BuildLighting()
    {
        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        // Passthrough shows a real room; a flat ambient keeps the skull from looking
        // pasted on without needing a baked probe.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
    }

    static Camera BuildRig(out Transform pointer)
    {
        new GameObject("AR Session").AddComponent<ARSession>();

        var originGo = new GameObject("XR Origin");
        var origin = originGo.AddComponent<XROrigin>();

        var offset = new GameObject("Camera Offset");
        offset.transform.SetParent(originGo.transform, false);

        var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
        cameraGo.transform.SetParent(offset.transform, false);

        var camera = cameraGo.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;   // the skull sits 0.6m away and gets leaned into
        camera.farClipPlane = 50f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f); // alpha 0 or Passthrough is hidden
        cameraGo.AddComponent<AudioListener>();
        var cameraManager = cameraGo.AddComponent<ARCameraManager>(); // turns Passthrough on

        var diagnostics = cameraGo.AddComponent<PassthroughDiagnostics>();
        diagnostics.cameraManager = cameraManager;
        diagnostics.targetCamera = camera;

        var driver = cameraGo.AddComponent<TrackedPoseDriver>();
        driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
        driver.positionInput = new InputActionProperty(new InputAction(
            "Position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
        driver.rotationInput = new InputActionProperty(new InputAction(
            "Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));

        origin.Camera = camera;
        origin.CameraFloorOffsetObject = offset;
        origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

        // Moved to the right controller each frame by XRControls; CalloutView rays along it.
        pointer = new GameObject("Pointer").transform;
        pointer.SetParent(originGo.transform, false);

        return camera;
    }

    static GameObject BuildSkull(out SkullPlacement placement, out BoneHighlighter highlighter,
        out TextMesh caption, out TextMesh status)
    {
        var root = new GameObject("Skull");
        placement = root.AddComponent<SkullPlacement>();

        // glTFast picks its material generator from the render pipeline active at IMPORT
        // time (Runtime/Scripts/Material/MaterialGenerator.cs) and declares no dependency
        // on the pipeline, so a model imported before URP was assigned keeps Built-In
        // materials forever. Those shaders have no UniversalForward pass, so URP skips
        // every renderer and the skull is invisible -- not magenta. Reimport under URP.
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogError($"[scene] {ModelPath} did not import; is glTFast installed?");
        }
        else
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "SKULL";
            instance.transform.SetParent(root.transform, false);
            AddCollidersForPointing(instance);
            AssertUrpMaterials(instance);
        }

        highlighter = root.AddComponent<BoneHighlighter>();
        highlighter.skullRoot = root.transform;

        caption = MakeLabel(root.transform, "Caption", new Vector3(0f, -0.18f, 0f), 0.006f);
        status = MakeLabel(root.transform, "Status", new Vector3(0f, -0.26f, 0f), 0.004f);
        status.text = "starting";
        status.color = new Color(0.65f, 0.68f, 0.75f);

        return root;
    }

    // The skull rendering at all depends on its materials being URP ones, and the failure
    // is silent at runtime: no error, no magenta, just nothing drawn. Fail the build log
    // instead.
    static void AssertUrpMaterials(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogError("[scene] skull has no renderers");
            return;
        }

        var shader = renderers[0].sharedMaterial != null ? renderers[0].sharedMaterial.shader : null;
        var name = shader != null ? shader.name : "<none>";
        if (shader == null || !name.StartsWith("Shader Graphs/"))
            Debug.LogError($"[scene] skull material uses '{name}', not a URP shader; " +
                           "it will not render. Delete Library/ and rebuild.");
        else
            Debug.Log($"[scene] skull: {renderers.Length} renderers, shader {name}");
    }

    // Pointing needs a bone-accurate hit: trigger selects the bone under the ray and
    // hovering reveals its label, so every node gets its own MeshCollider. Nothing
    // simulates against them (raycasts only), so 55 of them cost load-time cooking,
    // not frame time. The grip-grab ray hits the same colliders.
    static void AddCollidersForPointing(GameObject model)
    {
        var count = 0;
        foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            count++;
        }
        if (count == 0) Debug.LogError("[scene] skull has no MeshFilters; pointing and grab will not work");
        else Debug.Log($"[scene] skull: {count} mesh colliders");
    }

    // Anchored below the skull but never rotating with it: as a plain child these read
    // mirrored (the skull's +Z faces the viewer, a TextMesh is readable from behind its
    // +Z) and turn upside down with it. HeadLabel pins position and billboards them.
    static TextMesh MakeLabel(Transform anchor, string name, Vector3 worldOffset, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(anchor, false);
        go.transform.localPosition = worldOffset; // HeadLabel overwrites this each frame
        go.transform.localScale = Vector3.one * size;
        var text = go.AddComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 64;
        text.anchor = TextAnchor.UpperCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.white;
        go.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;

        var follow = go.AddComponent<HeadLabel>();
        follow.anchor = anchor;
        follow.worldOffset = worldOffset;
        return text;
    }

    static void BuildTutor(Camera camera, Transform pointer, GameObject skull,
        SkullPlacement placement, BoneHighlighter highlighter, TextMesh caption, TextMesh status)
    {
        var go = new GameObject("Tutor");

        var client = go.AddComponent<LiveClient>();
        // Set with PROXY_HOST=<mac-lan-ip> when running SceneBuilder.Build, so the LAN
        // address is not baked into source. Overridable on device via the proxyHost PlayerPref.
        var host = System.Environment.GetEnvironmentVariable("PROXY_HOST");
        if (!string.IsNullOrEmpty(host)) client.host = host;
        else Debug.LogWarning("[scene] PROXY_HOST not set; falling back to the default in " +
                              "LiveClient. Rebuilding without it silently reverts the address.");
        Debug.Log($"[scene] proxy host = {client.host}");
        var mic = go.AddComponent<MicStreamer>();
        go.AddComponent<AudioSource>();
        var audioOut = go.AddComponent<TutorAudio>();
        var callouts = go.AddComponent<CalloutView>();
        callouts.pointer = pointer;
        callouts.labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var controller = go.AddComponent<TutorController>();
        controller.client = client;
        controller.placement = placement;
        controller.callouts = callouts;
        controller.highlighter = highlighter;
        controller.audioOut = audioOut;
        controller.caption = caption;
        controller.status = status;
        controller.skull = skull.transform;

        var controls = go.AddComponent<XRControls>();
        controls.skull = skull.transform;
        controls.placement = placement;
        controls.callouts = callouts;
        controls.mic = mic;
        controls.pointer = pointer;
    }
}
