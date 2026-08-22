// Streaming playback of the tutor's voice: base64 24kHz PCM16 chunks from the proxy
// land in a ring buffer that an AudioClip PCMReaderCallback drains on the audio thread.
//
// NOTE: nothing in OnPCMRead may log or allocate. Debug.Log in an audio callback is a
// known cause of glitching, and this callback runs hundreds of times a second.
using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class TutorAudio : MonoBehaviour
{
    const int SourceRate = 24000;      // Live API output rate (see proxy.ts)
    const int BufferSeconds = 10;

    AudioSource _source;
    float[] _ring;
    int _write;
    int _read;
    readonly object _lock = new object();

    public bool IsSpeaking { get; private set; }

    void Awake()
    {
        _ring = new float[SourceRate * BufferSeconds];
        _source = GetComponent<AudioSource>();
        _source.loop = true;
        _source.spatialBlend = 0f; // voice sits with the listener, not in the room
        // Unity resamples the 24kHz clip to the output rate for us.
        _source.clip = AudioClip.Create("tutor", SourceRate, 1, SourceRate, true, OnPCMRead);
        _source.Play();
    }

    // Called from LiveClient.OnAudio on the main thread.
    public void Enqueue(byte[] pcm16)
    {
        lock (_lock)
        {
            for (int i = 0; i + 1 < pcm16.Length; i += 2)
            {
                var sample = (short)(pcm16[i] | (pcm16[i + 1] << 8));
                _ring[_write] = sample / 32768f;
                _write = (_write + 1) % _ring.Length;
                if (_write == _read) _read = (_read + 1) % _ring.Length; // overrun: drop oldest
            }
            IsSpeaking = true;
        }
    }

    // Barge-in: the user talked over the answer, so drop everything not yet played.
    public void Flush()
    {
        lock (_lock)
        {
            _read = _write;
            IsSpeaking = false;
        }
    }

    void OnPCMRead(float[] data)
    {
        lock (_lock)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (_read == _write) { data[i] = 0f; continue; } // starved: silence, not noise
                data[i] = _ring[_read];
                _read = (_read + 1) % _ring.Length;
            }
            if (_read == _write) IsSpeaking = false;
        }
    }
}
