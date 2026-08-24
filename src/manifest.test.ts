import { describe, it, expect } from "vitest";
import { existsSync } from "node:fs";
import { features, details, MODEL_SRC } from "./manifest";
import { parseVec } from "./orbit";

const isVec3 = (v: number[]) => v.length === 3 && v.every(Number.isFinite);

describe("feature manifest", () => {
  it("has unique feature ids", () => {
    const ids = features.map((f) => f.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("has unique detail ids", () => {
    const ids = details.map((d) => d.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it.each(features.map((f) => [f.id, f] as const))("%s has valid anchor data", (_, f) => {
    expect(isVec3(parseVec(f.position))).toBe(true);
    const n = parseVec(f.normal);
    expect(isVec3(n)).toBe(true);
    expect(Math.hypot(...n)).toBeGreaterThan(0);
    expect(f.label.trim()).not.toBe("");
    expect(f.explanation.trim()).not.toBe("");
    expect(f.meshNames.length).toBeGreaterThan(0);
  });

  it("MODEL_SRC points at a file in public/", () => {
    expect(existsSync(`public${MODEL_SRC}`)).toBe(true);
  });
});
