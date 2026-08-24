import { test, expect } from "@playwright/test";
import { features } from "../src/manifest";
import { orbitFromNormal, parseVec } from "../src/orbit";
import { hasGeminiKey, openApp } from "./_helpers";

const FOCUS_RADIUS = 0.28; // mirrors src/main.ts
const temporal = features.find((f) => f.id === "temporal")!;

test.describe("UI", () => {
  test("clicking a callout focuses it: class, dimming, camera target and orbit", async ({ page }) => {
    const errors = await openApp(page);
    const mv = page.locator("#viewer");
    const btn = page.locator(`.callout[data-id="${temporal.id}"]`);

    await btn.click({ force: true }); // hotspots can face away from the camera
    await expect(mv).toHaveClass(/focused/);
    await expect(btn).toHaveClass(/active/);
    await expect(btn).toHaveCSS("opacity", "1");
    await expect(page.locator('.callout[data-id="frontal"]')).toHaveCSS("opacity", "0.25");

    // getCameraTarget()/getCameraOrbit() report the interpolated camera
    // (interpolation-decay=200ms), so poll until it settles on the anchor.
    const [px, py, pz] = parseVec(temporal.position);
    await expect
      .poll(() => mv.evaluate((el: any) => el.getCameraTarget().x), { timeout: 5000 })
      .toBeCloseTo(px, 3);
    const target = await mv.evaluate((el: any) => el.getCameraTarget());
    expect(target.y).toBeCloseTo(py, 3);
    expect(target.z).toBeCloseTo(pz, 3);

    const [theta, phi] = orbitFromNormal(parseVec(temporal.normal), FOCUS_RADIUS)
      .split(/rad |m/)
      .map(Number);
    await expect
      .poll(() => mv.evaluate((el: any) => el.getCameraOrbit().radius), { timeout: 5000 })
      .toBeCloseTo(FOCUS_RADIUS, 2);
    const orbit = await mv.evaluate((el: any) => el.getCameraOrbit());
    expect(orbit.theta).toBeCloseTo(theta, 1);
    expect(orbit.phi).toBeCloseTo(phi, 1);
    expect(await mv.evaluate((el: any) => el.autoRotate)).toBe(false);

    expect(errors.page).toEqual([]);
  });

  test("idle timeout returns to the default pose", async ({ page }) => {
    test.slow();
    await openApp(page);
    const mv = page.locator("#viewer");
    await page.locator(`.callout[data-id="${temporal.id}"]`).click({ force: true });
    await expect(mv).toHaveClass(/focused/);
    await expect(mv).not.toHaveClass(/focused/, { timeout: 15_000 }); // IDLE_TIMEOUT_MS = 12s
    expect(await mv.evaluate((el: any) => el.autoRotate)).toBe(true);
    expect(await mv.evaluate((el: any) => el.cameraTarget)).toBe("auto auto auto");
  });

  test("?author mode announces itself", async ({ page }) => {
    await openApp(page, "/?author");
    await expect(page.locator("aside #status")).toContainText("AUTHOR MODE");
  });

  test("mic button without permission reports an error and stays off", async ({ page }) => {
    test.skip(!hasGeminiKey(), "mic path opens the Live session first; needs VITE_GEMINI_API_KEY");
    const errors = await openApp(page);
    await page.locator("#mic-btn").click();
    await expect(page.locator("aside #status")).toHaveText(/^mic error:/, { timeout: 30_000 });
    await expect(page.locator("#mic-btn")).not.toHaveClass(/live/);
    await expect(page.locator("#mic-btn")).toHaveText("🎤");
    expect(errors.page).toEqual([]);
  });

  test("stress: 100 rapid focus cycles across all callouts leave the app consistent", async ({ page }) => {
    const errors = await openApp(page);
    const ids = features.map((f) => f.id);
    const t0 = Date.now();
    await page.evaluate((ids) => {
      for (let i = 0; i < 100; i++) {
        for (const id of ids) {
          (document.querySelector(`.callout[data-id="${id}"]`) as HTMLElement).click();
        }
      }
    }, ids);
    const ms = Date.now() - t0;
    test.info().annotations.push({ type: "perf", description: `700 focus calls in ${ms}ms` });
    await expect(page.locator("#viewer")).toHaveClass(/focused/);
    await expect(page.locator(".callout.active")).toHaveCount(1);
    await expect(page.locator(".callout.active")).toHaveAttribute("data-id", ids[ids.length - 1]);
    await page.locator("#chat-input").fill("still responsive");
    await expect(page.locator("#chat-input")).toHaveValue("still responsive");
    expect(errors.page).toEqual([]);
  });
});

test.describe("E2E (Gemini)", () => {
  test.skip(!hasGeminiKey(), "needs VITE_GEMINI_API_KEY in .env");

  test("typed question connects, streams a reply and focuses the bone", async ({ page }) => {
    test.slow();
    const errors = await openApp(page);
    await page.locator("#chat-input").fill("tell me about the temporal bone");
    await page.locator('button[type="submit"]').click();

    await expect(page.locator(".line.you")).toHaveText("tell me about the temporal bone");
    await expect(page.locator("aside #status")).toHaveText("connected", { timeout: 30_000 });
    await expect(page.locator(`.callout[data-id="${temporal.id}"]`)).toHaveClass(/active/, {
      timeout: 45_000,
    });
    await expect(page.locator("#viewer")).toHaveClass(/focused/);
    await expect(page.locator(".line.guide").first()).not.toHaveText("", { timeout: 45_000 });
    expect(errors.page).toEqual([]);
  });
});

test.describe("chaos", () => {
  test("offline network: status reports the failure and the UI stays usable", async ({ page, context }) => {
    test.skip(!hasGeminiKey(), "without a key the app fails earlier with a config message");
    const errors = await openApp(page);
    await context.setOffline(true);
    await page.locator("#chat-input").fill("hello");
    await page.locator('button[type="submit"]').click();
    await expect(page.locator("aside #status")).toHaveText(/error|closed|disconnected|failed/, {
      timeout: 30_000,
    });
    await context.setOffline(false);
    // Local interactions still work after the failure.
    await page.locator(`.callout[data-id="${temporal.id}"]`).click({ force: true });
    await expect(page.locator("#viewer")).toHaveClass(/focused/);
    expect(errors.page).toEqual([]);
  });
});
