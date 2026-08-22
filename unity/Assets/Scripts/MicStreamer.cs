// Always-on mic capture -> 16kHz mono PCM16 -> base64 -> proxy.
// Gemini Live does its own VAD and barge-in, so there is no push-to-talk: the stream
// runs from launch and the mute toggle only stops the send.
using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

[RequireComponent(typeof(LiveClient))]
public class MicStreamer : MonoBehaviour
{
    const int TargetRate = 16000;   // what the Live API expects (see proxy.ts)
    const int ClipSeconds = 1;
    const float SendIntervalSeconds = 0.1f;

    public bool Muted { get; private set; }

    LiveClient _client;
    AudioClip _clip;
    string _device;
    int _captureRate;
    int _readPos;
    float _sendTimer;
    float[] _scratch;
    bool _running;

    void Awake() => _client = GetComponent<LiveClient>();

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
            return; // Update retries once the grant lands
        }
#endif
        TryStart();
    }

    void TryStart()
    {
        if (_running) return;
        if (Microphone.devices.Length == 0) return;
        _device = Microphone.devices[0];

        // Do not assume the headset can capture at 16kHz. Ask, then resample if not.
        Microphone.GetDeviceCaps(_device, out var minFreq, out var maxFreq);
        bool anyRate = minFreq == 0 && maxFreq == 0;
        _captureRate = anyRate || (TargetRate >= minFreq && TargetRate <= maxFreq)
            ? TargetRate
            : maxFreq;

        _clip = Microphone.Start(_device, true, ClipSeconds, _captureRate);
        if (_clip == null) { Debug.LogError("[MicStreamer] Microphone.Start returned null"); return; }
        _scratch = new float[_clip.samples * _clip.channels];
        _readPos = 0;
        _running = true;
        Debug.Log($"[MicStreamer] {_device} at {_captureRate}Hz (target {TargetRate}Hz)");
    }

    public void SetMuted(bool muted) => Muted = muted;
    public void ToggleMute() => Muted = !Muted;

    void Update()
    {
        if (!_running) { TryStart(); return; }

        _sendTimer += Time.unscaledDeltaTime;
        if (_sendTimer < SendIntervalSeconds) return;
        _sendTimer = 0f;

        var writePos = Microphone.GetPosition(_device);
        if (writePos < 0 || writePos == _readPos) return;

        int available = writePos - _readPos;
        if (available < 0) available += _clip.samples; // ring wrapped
        if (available <= 0) return;

        var samples = new float[available];
        // GetData reads the whole ring; copy out only the new span, honouring the wrap.
        _clip.GetData(_scratch, 0);
        for (int i = 0; i < available; i++)
            samples[i] = _scratch[(_readPos + i) % _clip.samples];
        _readPos = writePos;

        if (Muted || !_client.Connected) return; // drained, just not sent

        var resampled = _captureRate == TargetRate ? samples : Resample(samples, _captureRate, TargetRate);
        _client.SendAudio(Convert.ToBase64String(ToPcm16(resampled)));
    }

    // Linear interpolation. Good enough for speech into a VAD; a windowed-sinc filter
    // would be the upgrade if aliasing ever shows up in recognition quality.
    static float[] Resample(float[] input, int fromRate, int toRate)
    {
        if (input.Length == 0) return input;
        var ratio = (double)fromRate / toRate;
        var outLength = (int)(input.Length / ratio);
        var output = new float[outLength];
        for (int i = 0; i < outLength; i++)
        {
            var src = i * ratio;
            var i0 = (int)src;
            var i1 = Mathf.Min(i0 + 1, input.Length - 1);
            var frac = (float)(src - i0);
            output[i] = Mathf.Lerp(input[i0], input[i1], frac);
        }
        return output;
    }

    static byte[] ToPcm16(float[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            var clamped = Mathf.Clamp(samples[i], -1f, 1f);
            var value = (short)(clamped * short.MaxValue);
            bytes[i * 2] = (byte)(value & 0xFF);
            bytes[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }
        return bytes;
    }

    void OnDestroy()
    {
        if (_running) Microphone.End(_device);
    }
}
