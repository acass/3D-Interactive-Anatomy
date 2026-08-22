// One-shot project configuration for the Quest 3 build. Run:
//   Unity -batchmode -nographics -projectPath unity -executeMethod ProjectSetup.Configure -quit -logFile -
//
// Every feature id below was read out of the installed package source, not recalled:
//   com.unity.xr.meta-openxr 2.5.1  Runtime/Subsystems/{Camera,Session}/*Feature.cs
//   com.unity.xr.openxr 1.18.0      Runtime/Features/Interactions/*.cs
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEditor.XR.OpenXR.Features;

public static class ProjectSetup
{
    const string UrpDir = "Assets/Rendering";
    const string UrpAssetPath = UrpDir + "/QuestURP.asset";
    const string UrpRendererPath = UrpDir + "/QuestURP_Renderer.asset";

    // "Meta Quest: Camera (Passthrough)" -- requests XR_FB_passthrough.
    const string FeatureCamera  = "com.unity.openxr.feature.arfoundation-meta-camera";
    const string FeatureSession = "com.unity.openxr.feature.arfoundation-meta-session";
    // "Meta Quest Support". Without this the build injects no
    // com.oculus.intent.category.VR / focusaware / headtracking manifest entries, and
    // the Quest shell refuses the app an immersive session (black screen, HasFocus = 0).
    const string FeatureQuestSupport = "com.unity.openxr.feature.metaquest";
    const string FeatureQuestPlus = "com.unity.openxr.feature.input.metaquestplus";
    const string FeatureOculusTouch = "com.unity.openxr.feature.input.oculustouch";

    public static void Configure()
    {
        SetupUrp();
        SetupPlayer();
        SetupXr();
        AssetDatabase.SaveAssets();
        Debug.Log("[setup] done");
    }

    static void SetupUrp()
    {
        Directory.CreateDirectory(UrpDir);

        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(UrpRendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, UrpRendererPath);
        }

        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
        if (pipeline == null)
        {
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, UrpAssetPath);
        }

        // Quest 3 is a mobile tile GPU: MSAA over post-processing, no HDR, no shadow cascades.
        pipeline.msaaSampleCount = 4;
        pipeline.supportsHDR = false;
        pipeline.shadowDistance = 10f;
        EditorUtility.SetDirty(pipeline);

        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        Debug.Log("[setup] URP asset assigned");
    }

    static void SetupPlayer()
    {
        var android = NamedBuildTarget.Android;
        PlayerSettings.companyName = "Interactive Anatomy";
        PlayerSettings.productName = "Skull Tutor XR";
        PlayerSettings.SetApplicationIdentifier(android, "com.interactiveanatomy.skulltutorxr");

        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        // ARCameraFeature declares API 32 as its minimum; Quest 3 runs Android 12L.
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.forceInternetPermission = true;

        // Always-on mic. Unity adds RECORD_AUDIO when a build references Microphone,
        // but the runtime request in MicStreamer needs the manifest entry to exist.
        PlayerSettings.Android.forceSDCardPermission = false;
        PlayerSettings.allowUnsafeCode = false;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan });
        Debug.Log("[setup] player settings applied");
    }

    static void SetupXr()
    {
        var settings = GetOrCreateXrGeneralSettings();
        if (settings.Manager == null)
        {
            settings.Manager = ScriptableObject.CreateInstance<XRManagerSettings>();
            settings.Manager.name = "Android XR Manager";
            AssetDatabase.AddObjectToAsset(settings.Manager, settings);
        }

        if (!XRPackageMetadataStore.AssignLoader(settings.Manager, "OpenXRLoader", BuildTargetGroup.Android))
            Debug.LogError("[setup] failed to assign OpenXRLoader for Android");
        settings.InitManagerOnStart = true;
        EditorUtility.SetDirty(settings);

        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
        foreach (var id in new[] { FeatureQuestSupport, FeatureSession, FeatureCamera, FeatureQuestPlus, FeatureOculusTouch })
        {
            var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, id);
            if (feature == null) { Debug.LogError($"[setup] OpenXR feature not found: {id}"); continue; }
            feature.enabled = true;
            EditorUtility.SetDirty(feature);
            Debug.Log($"[setup] enabled OpenXR feature {id}");
        }
        AssetDatabase.SaveAssets();
    }

    static XRGeneralSettings GetOrCreateXrGeneralSettings()
    {
        const string dir = "Assets/XR";
        const string path = dir + "/XRGeneralSettings.asset";
        Directory.CreateDirectory(dir);

        EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
            out XRGeneralSettingsPerBuildTarget perTarget);

        if (perTarget == null)
        {
            perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
            if (perTarget == null)
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, path);
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
        }

        if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);

        return perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
    }
}
