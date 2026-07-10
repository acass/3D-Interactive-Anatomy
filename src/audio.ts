// Audio glue for the Live API.
// Output: 24kHz mono 16-bit PCM chunks streamed back -> queued and played in order.
// Input:  mic captured as 16kHz mono 16-bit PCM -> base64 chunks streamed up.

export function b64ToInt16(b64: string): Int16Array {
  const bin = atob(b64);
  const bytes = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
  return new Int16Array(bytes.buffer);
}

export function int16ToB64(i16: Int16Array): string {
  const bytes = new Uint8Array(i16.buffer);
  let bin = "";
  for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
  return btoa(bin);
}

// Plays a stream of 24kHz PCM chunks back-to-back with no gaps.
export class AudioPlayer {
  private ctx = new AudioContext();
  private nextTime = 0;
  private sources = new Set<AudioBufferSourceNode>();

  resume() {
    return this.ctx.resume();
  }

  enqueue(b64: string) {
    const int16 = b64ToInt16(b64);
    const buf = this.ctx.createBuffer(1, int16.length, 24000);
    const ch = buf.getChannelData(0);
    for (let i = 0; i < int16.length; i++) ch[i] = int16[i] / 32768;

    const src = this.ctx.createBufferSource();
    src.buffer = buf;
    src.connect(this.ctx.destination);
    this.nextTime = Math.max(this.nextTime, this.ctx.currentTime);
    src.start(this.nextTime);
    this.nextTime += buf.duration;
    this.sources.add(src);
    src.onended = () => this.sources.delete(src);
  }

  // Barge-in: stop everything currently queued/playing.
  stop() {
    for (const s of this.sources) {
      try {
        s.stop();
      } catch {
        /* already stopped */
      }
    }
    this.sources.clear();
    this.nextTime = 0;
  }
}

// Captures the mic as 16kHz PCM and hands base64 chunks to a callback.
export class MicStreamer {
  private ctx?: AudioContext;
  private stream?: MediaStream;
  private node?: ScriptProcessorNode;
  private source?: MediaStreamAudioSourceNode;

  async start(onChunk: (b64: string) => void) {
    // Request the mic first; if it's denied this throws before we allocate a
    // context, so `active` stays false and the toggle stays consistent.
    this.stream = await navigator.mediaDevices.getUserMedia({
      audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true },
    });
    this.ctx = new AudioContext({ sampleRate: 16000 });
    this.source = this.ctx.createMediaStreamSource(this.stream);
    this.node = this.ctx.createScriptProcessor(4096, 1, 1);
    this.node.onaudioprocess = (e) => {
      const f32 = e.inputBuffer.getChannelData(0);
      const i16 = new Int16Array(f32.length);
      for (let i = 0; i < f32.length; i++) {
        const s = Math.max(-1, Math.min(1, f32[i]));
        i16[i] = s < 0 ? s * 32768 : s * 32767;
      }
      onChunk(int16ToB64(i16));
    };
    // Route through a muted gain node so onaudioprocess fires without echoing the mic.
    const mute = this.ctx.createGain();
    mute.gain.value = 0;
    this.source.connect(this.node);
    this.node.connect(mute);
    mute.connect(this.ctx.destination);
  }

  stop() {
    this.node?.disconnect();
    this.source?.disconnect();
    this.stream?.getTracks().forEach((t) => t.stop());
    this.ctx?.close();
    this.ctx = undefined;
  }

  get active() {
    return !!this.ctx;
  }
}
