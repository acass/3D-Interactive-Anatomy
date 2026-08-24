import { test, expect } from "@playwright/test";
import { features } from "../src/manifest";
import { openApp } from "./_helpers";

// WCAG 2.x relative luminance / contrast ratio, from computed "rgb(r, g, b)" strings.
function luminance(rgb: string) {
  const [r, g, b] = rgb.match(/\d+/g)!.slice(0, 3).map(Number).map((c) => {
    const s = c / 255;
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}
function contrast(fg: string, bg: string) {
  const [a, b] = [luminance(fg), luminance(bg)].sort((x, y) => y - x);
  return (a + 0.05) / (b + 0.05);
}

test.describe("accessibility", () => {
  test("every callout is a named button", async ({ page }) => {
    await openApp(page);
    for (const f of features) {
      await expect(page.getByRole("button", { name: f.label, exact: true })).toHaveCount(1);
    }
  });

  test("keyboard: Tab reaches callouts and chat controls; Enter activates a callout", async ({ page }) => {
    await openApp(page);
    const seen = new Set<string>();
    for (let i = 0; i < 20; i++) {
      await page.keyboard.press("Tab");
      const desc = await page.evaluate(() => {
        const el = document.activeElement as HTMLElement | null;
        if (!el) return "";
        return el.id || el.dataset.id || `${el.tagName.toLowerCase()}[type=${(el as any).type}]`;
      });
      seen.add(desc);
      if (desc === "chat-input") {
        await page.keyboard.press("Tab");
        seen.add(await page.evaluate(() => (document.activeElement as HTMLElement).tagName.toLowerCase() + "[type=" + (document.activeElement as any).type + "]"));
        break;
      }
    }
    for (const f of features) expect(seen, `callout ${f.id} reachable by Tab`).toContain(f.id);
    expect(seen).toContain("mic-btn");
    expect(seen).toContain("chat-input");
    expect(seen).toContain("button[type=submit]");

    await page.locator('.callout[data-id="temporal"]').focus();
    await page.keyboard.press("Enter");
    await expect(page.locator("#viewer")).toHaveClass(/focused/);
    await expect(page.locator('.callout[data-id="temporal"]')).toHaveClass(/active/);
  });

  test("labels, live regions and contrast", async ({ page }) => {
    await openApp(page);
    // Each soft assertion is one finding; the test fails if any is violated.
    await expect.soft(page.locator("#mic-btn"), "mic button needs a real accessible name (aria-label), not an emoji").toHaveAccessibleName(/mic/i);
    await expect.soft(page.locator("#chat-input"), "chat input should have a label, not just a placeholder").toHaveAttribute("aria-label", /.+/);
    await expect.soft(page.locator("aside #status"), "status is updated by script; needs aria-live so screen readers hear it").toHaveAttribute("aria-live", /polite|assertive/);
    await expect.soft(page.locator("#transcript"), "transcript is a streaming conversation; role=log makes it announced").toHaveAttribute("role", "log");
    await expect.soft(page.locator("#viewer"), "model-viewer should carry an accessible description of the model").toHaveAttribute("alt", /.+/);

    const bodyBg = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
    const statusFg = await page.evaluate(() => getComputedStyle(document.getElementById("status")!).color);
    const statusRatio = contrast(statusFg, bodyBg);
    test.info().annotations.push({ type: "contrast", description: `#status ${statusFg} on ${bodyBg} = ${statusRatio.toFixed(2)}:1` });
    expect.soft(statusRatio, "#status text contrast must be >= 4.5:1 (WCAG AA, 11px text)").toBeGreaterThanOrEqual(4.5);

    const ph = await page.evaluate(() => {
      const el = document.getElementById("chat-input")!;
      return [getComputedStyle(el, "::placeholder").color, getComputedStyle(el).backgroundColor];
    });
    const phRatio = contrast(ph[0], ph[1]);
    test.info().annotations.push({ type: "contrast", description: `placeholder ${ph[0]} on ${ph[1]} = ${phRatio.toFixed(2)}:1` });
    expect.soft(phRatio, "placeholder contrast must be >= 4.5:1").toBeGreaterThanOrEqual(4.5);
  });
});
