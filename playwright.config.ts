import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "e2e",
  timeout: 90_000,
  retries: 0,
  workers: 1, // model-viewer pulls an 8MB GLB per page; serial keeps timings honest
  reporter: [["list"], ["json", { outputFile: "test-results/results.json" }]],
  use: {
    baseURL: "http://localhost:5173",
    permissions: [], // mic is never auto-granted; the deny path is a test
    trace: "retain-on-failure",
    launchOptions: { args: ["--enable-unsafe-swiftshader"] }, // WebGL in headless
  },
  projects: [{ name: "chromium", use: { browserName: "chromium" } }],
  webServer: {
    command: "npm run dev",
    url: "http://localhost:5173",
    reuseExistingServer: true,
    timeout: 30_000,
  },
});
