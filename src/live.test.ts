import { describe, it, expect } from "vitest";
import { focusFeature, resetView, systemInstruction, LIVE_MODEL } from "./live";
import { features, details } from "./manifest";

describe("Gemini tool contract", () => {
  it("focusFeature enum equals manifest ids in order", () => {
    const schema = focusFeature.parameters.properties.featureId;
    expect(schema.enum).toEqual(features.map((f) => f.id));
    expect(focusFeature.parameters.required).toEqual(["featureId"]);
  });

  it("resetView takes no parameters", () => {
    expect(resetView.parameters.properties).toEqual({});
  });

  it("names a live model", () => {
    expect(LIVE_MODEL).toMatch(/live/);
  });
});

describe("systemInstruction", () => {
  const text = systemInstruction();

  it("mentions every feature id and label", () => {
    for (const f of features) {
      expect(text).toContain(`- ${f.id} ("${f.label}")`);
      expect(text).toContain(f.explanation);
    }
  });

  it("mentions every detail label and text", () => {
    for (const d of details) {
      expect(text).toContain(`- ${d.label}: ${d.text}`);
    }
  });

  it("tour order matches manifest order", () => {
    expect(text).toContain(`in this order: ${features.map((f) => f.id).join(", ")}.`);
  });
});
