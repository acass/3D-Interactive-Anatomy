// Performance: measured, recorded as annotations. Only a generous ceiling is asserted
// until a baseline exists (see docs/TEST-REPORT.md).
import { test, expect } from "@playwright/test";
import { trackErrors, waitForModel } from "./_helpers";

test("page and model load timings", async ({ page }) => {
  test.slow();
  trackErrors(page);
  const t0 = Date.now();
  await page.goto("/");
  const modelMs = await waitForModel(page);
  const total = Date.now() - t0;

  const nav = await page.evaluate(() => {
    const n = performance.getEntriesByType("navigation")[0] as PerformanceNavigationTiming;
    const glb = performance
      .getEntriesByType("resource")
      .find((r) => r.name.endsWith("/SKULL.glb")) as PerformanceResourceTiming | undefined;
    const mem = (performance as any).memory;
    return {
      domContentLoaded: Math.round(n.domContentLoadedEventEnd),
      loadEvent: Math.round(n.loadEventEnd),
      glbDurationMs: glb ? Math.round(glb.duration) : null,
      glbBytes: glb?.transferSize ?? glb?.encodedBodySize ?? null,
      jsHeapMB: mem ? Math.round(mem.usedJSHeapSize / 1048576) : null,
      resourceCount: performance.getEntriesByType("resource").length,
    };
  });

  const summary = { navToModelLoadMs: total, modelLoadEventMs: Math.round(modelMs), ...nav };
  test.info().annotations.push({ type: "perf", description: JSON.stringify(summary) });
  console.log("PERF", JSON.stringify(summary));

  expect(total, "model visible within 30s on localhost").toBeLessThan(30_000);
});
