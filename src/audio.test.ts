import { describe, it, expect } from "vitest";
import { b64ToInt16, int16ToB64 } from "./audio";

describe("PCM <-> base64", () => {
  it("round-trips signed 16-bit samples, including negatives and extremes", () => {
    const samples = Int16Array.from([0, 1, -1, 32767, -32768, 12345, -6789]);
    const restored = b64ToInt16(int16ToB64(samples));
    expect(Array.from(restored)).toEqual(Array.from(samples));
  });

  it("preserves length", () => {
    const samples = new Int16Array(4096).map((_, i) => (i * 37) % 65536 - 32768);
    expect(b64ToInt16(int16ToB64(samples)).length).toBe(4096);
  });
});
