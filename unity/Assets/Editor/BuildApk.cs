// APK build entry point. Run:
//   Unity -batchmode -nographics -projectPath unity -executeMethod BuildApk.Build -quit -logFile -
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildApk
{
    const string OutputPath = "Build/SkullTutorXR.apk";

    public static void Build()
    {
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SkullTutorXR.unity" },
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log($"[apk] {summary.result} size={summary.totalSize} errors={summary.totalErrors}");

        foreach (var step in report.steps)
            foreach (var message in step.messages)
                if (message.type == LogType.Error || message.type == LogType.Exception)
                    Debug.LogError($"[apk] {message.content}");

        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
