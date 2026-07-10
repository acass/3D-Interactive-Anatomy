import { describe, it, expect } from "vitest";
import { orbitFromNormal, parseVec } from "./orbit";

const HALF_PI = Math.PI / 2;

describe("orbitFromNormal", () => {
  it("front normal (+z) -> theta 0, phi 90deg", () => {
    const [theta, phi] = orbitFromNormal([0, 0, 1], 2).split(/rad |m/);
    expect(Number(theta)).toBeCloseTo(0);
    expect(Number(phi)).toBeCloseTo(HALF_PI);
  });

  it("top normal (+y) -> phi 0 (looking straight down)", () => {
    const [, phi] = orbitFromNormal([0, 1, 0], 2).split(/rad |m/);
    expect(Number(phi)).toBeCloseTo(0);
  });

  it("right normal (+x) -> theta 90deg, phi 90deg", () => {
    const [theta, phi] = orbitFromNormal([1, 0, 0], 2).split(/rad |m/);
    expect(Number(theta)).toBeCloseTo(HALF_PI);
    expect(Number(phi)).toBeCloseTo(HALF_PI);
  });

  it("normalizes non-unit normals", () => {
    const [theta, phi] = orbitFromNormal([0, 0, 5], 3).split(/rad |m/);
    expect(Number(theta)).toBeCloseTo(0);
    expect(Number(phi)).toBeCloseTo(HALF_PI);
  });
});

describe("parseVec", () => {
  it("parses an x y z string", () => {
    expect(parseVec(" 1  2 3 ")).toEqual([1, 2, 3]);
  });
});
