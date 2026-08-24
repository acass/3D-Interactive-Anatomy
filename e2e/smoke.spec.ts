import { test, expect } from "@playwright/test";
import { features } from "../src/manifest";
import { openApp } from "./_helpers";

test.describe("smoke", () => {
  test("dev server serves page, model and entry script", async ({ request }) => {
    const page = await request.get("/");
    expect(page.status()).toBe(200);
    expect(await page.text()).toContain('<model-viewer');

    const glb = await request.head("/SKULL.glb");
    expect(glb.status()).toBe(200);
    expect(Number(glb.headers()["content-length"])).toBeGreaterThan(8_000_000);

    const main = await request.get("/src/main.ts");
    expect(main.status()).toBe(200);
  });

  test("app boots: model loads, callouts render, status idle, no errors", async ({ page }) => {
    const errors = await openApp(page);
    await expect(page.locator(".callout")).toHaveCount(features.length);
    for (const f of features) {
      await expect(page.locator(`.callout[data-id="${f.id}"]`)).toHaveText(f.label);
    }
    await expect(page.locator("aside #status")).toHaveText("idle");
    expect(errors.page).toEqual([]);
    expect(errors.console).toEqual([]);
  });
});
