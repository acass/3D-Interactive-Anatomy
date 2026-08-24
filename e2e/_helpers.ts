import { readFileSync } from "node:fs";
import type { Page } from "@playwright/test";

// Vite loads .env for the browser bundle; Playwright does not, so peek at the file
// to decide whether Gemini-backed specs can run.
export function hasGeminiKey(): boolean {
  try {
    return /^VITE_GEMINI_API_KEY=\S+/m.test(readFileSync(".env", "utf8"));
  } catch {
    return false;
  }
}

export interface PageErrors {
  console: string[];
  page: string[];
}

export function trackErrors(page: Page): PageErrors {
  const errors: PageErrors = { console: [], page: [] };
  page.on("console", (m) => {
    if (m.type() === "error") errors.console.push(m.text());
  });
  page.on("pageerror", (e) => errors.page.push(e.message));
  return errors;
}

// Resolves when <model-viewer> has finished loading the GLB.
export function waitForModel(page: Page, timeout = 60_000) {
  return page.evaluate(
    (t) =>
      new Promise<number>((resolve, reject) => {
        const mv = document.getElementById("viewer") as any;
        const t0 = performance.now();
        if (mv.loaded) return resolve(0);
        const timer = setTimeout(() => reject(new Error("model-viewer load timeout")), t);
        mv.addEventListener(
          "load",
          () => {
            clearTimeout(timer);
            resolve(performance.now() - t0);
          },
          { once: true }
        );
      }),
    timeout
  );
}

export async function openApp(page: Page, path = "/") {
  const errors = trackErrors(page);
  await page.goto(path);
  await waitForModel(page);
  return errors;
}
