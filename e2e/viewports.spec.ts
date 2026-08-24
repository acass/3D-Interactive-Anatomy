// Cross-platform: the same page across desktop, tablet and phone widths.
import { test, expect } from "@playwright/test";
import { features } from "../src/manifest";
import { openApp } from "./_helpers";

const viewports = [
  { name: "desktop", width: 1440, height: 900 },
  { name: "tablet", width: 1024, height: 768 },
  { name: "phone", width: 390, height: 844 },
];

for (const vp of viewports) {
  test(`layout holds at ${vp.name} ${vp.width}x${vp.height}`, async ({ page }) => {
    await page.setViewportSize({ width: vp.width, height: vp.height });
    const errors = await openApp(page);
    await expect(page.locator(".callout")).toHaveCount(features.length);

    const m = await page.evaluate(() => ({
      scrollWidth: document.documentElement.scrollWidth,
      innerWidth: innerWidth,
      viewerWidth: document.getElementById("viewer")!.getBoundingClientRect().width,
      inputWidth: document.getElementById("chat-input")!.getBoundingClientRect().width,
    }));
    test.info().annotations.push({ type: "layout", description: JSON.stringify(m) });

    expect(m.scrollWidth, "no horizontal page scroll").toBeLessThanOrEqual(m.innerWidth);
    expect(m.viewerWidth, "viewer keeps a usable width").toBeGreaterThanOrEqual(300);
    expect(m.inputWidth, "chat input keeps a usable width").toBeGreaterThanOrEqual(120);
    await expect(page.locator("#chat-input")).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toBeVisible();
    expect(errors.page).toEqual([]);
  });
}
