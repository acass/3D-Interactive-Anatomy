// Batchmode package installer. Run:
//   Unity -batchmode -nographics -projectPath unity -executeMethod PackageBootstrap.AddPackages -logFile -
//
// Versions are deliberately unpinned so the Package Manager resolves what is actually
// compatible with this editor -- no guessed version strings.
//
// Each successful add triggers a domain reload, which wipes static state, so the
// remaining queue lives in SessionState and the run resumes itself after every reload.
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

[InitializeOnLoad]
public static class PackageBootstrap
{
    const string QueueKey = "PackageBootstrap.Queue";
    const string ActiveKey = "PackageBootstrap.Active";

    static readonly string[] Wanted =
    {
        "com.unity.render-pipelines.universal",
        "com.unity.xr.arfoundation",
        "com.unity.xr.openxr",
        "com.unity.xr.meta-openxr",
        "com.unity.xr.interaction.toolkit",
        "com.unity.cloud.gltfast",
    };

    static AddRequest _request;
    static string _current;

    static PackageBootstrap()
    {
        // Resume after a domain reload triggered by the previous add.
        if (SessionState.GetBool(ActiveKey, false))
            EditorApplication.delayCall += Next;
    }

    public static void AddPackages()
    {
        SessionState.SetString(QueueKey, string.Join(",", Wanted));
        SessionState.SetBool(ActiveKey, true);
        Next();
    }

    static void Next()
    {
        var queue = SessionState.GetString(QueueKey, "")
            .Split(',').Where(s => s.Length > 0).ToList();

        if (queue.Count == 0)
        {
            SessionState.SetBool(ActiveKey, false);
            Debug.Log("[bootstrap] all packages added");
            EditorApplication.Exit(0);
            return;
        }

        _current = queue[0];
        // Persist the remainder BEFORE adding, so a reload mid-add does not repeat it.
        SessionState.SetString(QueueKey, string.Join(",", queue.Skip(1)));
        Debug.Log($"[bootstrap] adding {_current}");
        _request = Client.Add(_current);
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (_request == null || !_request.IsCompleted) return;
        EditorApplication.update -= Tick;

        if (_request.Status == StatusCode.Failure)
        {
            Debug.LogError($"[bootstrap] FAILED {_current}: {_request.Error.message}");
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"[bootstrap] added {_request.Result.name}@{_request.Result.version}");
        _request = null;
        Next(); // if a reload preempts this, the static constructor resumes
    }
}
