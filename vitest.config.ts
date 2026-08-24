import { defineConfig } from "vitest/config";

// Playwright specs (e2e/) and the proxy scripts (tests/) are not vitest tests.
export default defineConfig({ test: { include: ["src/**/*.test.ts"] } });
