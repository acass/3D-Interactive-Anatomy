// WebSocket link to server/proxy.ts, which holds the Gemini key and runs the same
// system prompt the web app uses. Protocol is documented at the top of proxy.ts.
//
// Threading: ClientWebSocket receive/send run off the main thread; every event is
// marshalled back through a queue drained in Update so handlers can touch Unity objects.
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class LiveClient : MonoBehaviour
{
    [Tooltip("LAN address of the machine running `npm run proxy`.")]
    public string host = "192.168.1.10";
    public int port = 8787;

    // On-device override so the address can be changed without an APK rebuild.
    const string HostPrefKey = "proxyHost";

    public event Action<string> OnStatus;
    public event Action<FeatureDto[]> OnManifest;
    public event Action<string> OnTranscript;
    public event Action OnTurnComplete;
    public event Action OnInterrupted;
    public event Action<byte[]> OnAudio;     // decoded 24kHz PCM16
    public event Action<string> OnFocus;
    public event Action OnReset;

    readonly ConcurrentQueue<Action> _main = new ConcurrentQueue<Action>();
    readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1); // SendAsync is not reentrant
    ClientWebSocket _ws;
    CancellationTokenSource _cts;
    volatile bool _connected;

    public bool Connected => _connected;

    public string Host
    {
        get => PlayerPrefs.GetString(HostPrefKey, host);
        set { PlayerPrefs.SetString(HostPrefKey, value); PlayerPrefs.Save(); }
    }

    void Start()
    {
        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    void Update()
    {
        while (_main.TryDequeue(out var a)) a();
    }

    void OnDestroy()
    {
        _cts?.Cancel();
        try { _ws?.Abort(); } catch (ObjectDisposedException) { /* already torn down */ }
    }

    void Post(Action a) => _main.Enqueue(a);

    async Task RunAsync(CancellationToken token)
    {
        var backoffSeconds = 1;
        while (!token.IsCancellationRequested)
        {
            var url = $"ws://{Host}:{port}";
            try
            {
                Post(() => OnStatus?.Invoke($"connecting {url}"));
                _ws = new ClientWebSocket();
                await _ws.ConnectAsync(new Uri(url), token);
                _connected = true;
                backoffSeconds = 1;
                Post(() => OnStatus?.Invoke("connected"));
                await ReceiveLoopAsync(token);
            }
            catch (OperationCanceledException)
            {
                return; // shutting down
            }
            catch (WebSocketException e)
            {
                Post(() => OnStatus?.Invoke($"link down: {e.Message}"));
            }
            catch (Exception e)
            {
                // Any other failure is still just a dead link; log it and retry rather
                // than leaving the headset with a silent app and no status.
                Debug.LogError($"[LiveClient] {e.GetType().Name}: {e.Message}");
                Post(() => OnStatus?.Invoke($"link error: {e.Message}"));
            }
            finally
            {
                _connected = false;
                try { _ws?.Dispose(); } catch (ObjectDisposedException) { }
                _ws = null;
            }

            if (token.IsCancellationRequested) return;
            Post(() => OnStatus?.Invoke($"reconnecting in {backoffSeconds}s"));
            try { await Task.Delay(backoffSeconds * 1000, token); }
            catch (OperationCanceledException) { return; }
            backoffSeconds = Mathf.Min(backoffSeconds * 2, 15);
        }
    }

    async Task ReceiveLoopAsync(CancellationToken token)
    {
        var buffer = new byte[64 * 1024];
        var message = new StringBuilder();
        while (_ws.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                Post(() => OnStatus?.Invoke("closed by proxy"));
                return;
            }
            message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if (!result.EndOfMessage) continue; // audio frames arrive fragmented
            var json = message.ToString();
            message.Clear();
            Dispatch(json);
        }
    }

    void Dispatch(string json)
    {
        ProxyMessage m;
        try { m = JsonUtility.FromJson<ProxyMessage>(json); }
        catch (ArgumentException e) { Debug.LogError($"[LiveClient] bad JSON: {e.Message}"); return; }
        if (m == null || string.IsNullOrEmpty(m.type)) return;

        switch (m.type)
        {
            case "status":       Post(() => OnStatus?.Invoke(m.status)); break;
            case "manifest":     Post(() => OnManifest?.Invoke(m.features)); break;
            case "transcript":   Post(() => OnTranscript?.Invoke(m.text)); break;
            case "turnComplete": Post(() => OnTurnComplete?.Invoke()); break;
            case "interrupted":  Post(() => OnInterrupted?.Invoke()); break;
            case "focus":        Post(() => OnFocus?.Invoke(m.featureId)); break;
            case "reset":        Post(() => OnReset?.Invoke()); break;
            case "audio":
                // Decode off the main thread; the audio path is the hot one.
                byte[] pcm;
                try { pcm = Convert.FromBase64String(m.data); }
                catch (FormatException e) { Debug.LogError($"[LiveClient] bad audio b64: {e.Message}"); return; }
                Post(() => OnAudio?.Invoke(pcm));
                break;
        }
    }

    public async void SendText(string text)
    {
        await SendJsonAsync(JsonUtility.ToJson(new OutText { type = "text", text = text }));
    }

    public async void SendAudio(string base64Pcm16k)
    {
        await SendJsonAsync(JsonUtility.ToJson(new OutAudio { type = "audio", data = base64Pcm16k }));
    }

    async Task SendJsonAsync(string json)
    {
        if (_ws == null || _ws.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(json);
        await _sendLock.WaitAsync();
        try
        {
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (WebSocketException e)
        {
            // Link died mid-send; the receive loop's reconnect handles recovery.
            Debug.LogWarning($"[LiveClient] send failed: {e.Message}");
        }
        catch (ObjectDisposedException) { /* socket torn down during reconnect */ }
        finally { _sendLock.Release(); }
    }

    [Serializable] class OutText { public string type; public string text; }
    [Serializable] class OutAudio { public string type; public string data; }
}
