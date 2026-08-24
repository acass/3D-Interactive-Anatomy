// README user stories, driven through the real UI against the real Gemini Live model.
// Costs 2 Gemini turns. Run alone with: npx playwright test --grep @acceptance
import { test, expect, type Page } from "@playwright/test";
import { features } from "../src/manifest";
import { hasGeminiKey, openApp } from "./_helpers";

async function ask(page: Page, text: string) {
  await page.locator("#chat-input").fill(text);
  await page.locator('button[type="submit"]').click();
}

test.describe("acceptance @acceptance", () => {
  test.skip(!hasGeminiKey(), "needs VITE_GEMINI_API_KEY in .env");
  test.describe.configure({ mode: "serial" });

  test("tour starts at the first manifest bone and asks before moving on; reset clears focus", async ({ page }) => {
    test.slow();
    const errors = await openApp(page);
    const mv = page.locator("#viewer");

    // README: "Take a tour" — the tutor walks the bones in manifest order.
    await ask(page, "give me a tour");
    await expect(page.locator("aside #status")).toHaveText("connected", { timeout: 30_000 });
    await expect(page.locator(".callout.active")).toHaveAttribute("data-id", features[0].id, {
      timeout: 45_000,
    });
    // Prompt rule: explain one region, then ask if they're ready for the next one.
    await expect
      .poll(async () => (await page.locator(".line.guide").last().textContent()) ?? "", {
        timeout: 45_000,
      })
      .toMatch(/\?/);
    const guideText = (await page.locator(".line.guide").last().textContent()) ?? "";
    test.info().annotations.push({ type: "transcript", description: guideText.slice(0, 300) });

    // README: "Reset" — ask to see the whole skull.
    await ask(page, "show me the whole skull");
    await expect(mv).not.toHaveClass(/focused/, { timeout: 45_000 });
    expect(await mv.evaluate((el: any) => el.autoRotate)).toBe(true);

    expect(errors.page).toEqual([]);
  });
});
