# Test report — 2026-08-24

**Update, same day:** F1, F2, F6 and F7 fixed and re-verified (see "Fixes applied" at the end).
Offline suites are fully green; `npm run test:proxy` passes with 0 findings.

Fifteen test types run against the web app, the Gemini proxy and the Unity project.
Everything rerunnable is checked in; one-off measurements are recorded here.
Gemini Live usage for the whole run: 11 text turns.

## Verdicts

| # | Type | Verdict | What ran | Decisive evidence |
|---|------|---------|----------|-------------------|
| 1 | Unit | PASS | `npm test` — 4 files, 24 tests (orbit math, PCM codec, manifest invariants, Gemini tool contract) | `Tests 24 passed (24)` |
| 2 | Integration | PASS (2 findings, fixed) | `npm run test:proxy` — proxy <-> Gemini Live, manifest push, tool calls, pending buffer | `focus=temporal at 4639ms, audio=52 chunks`; findings F2 |
| 3 | API | PASS | Same script: model `gemini-3.1-flash-live-preview` accepted, `focusFeature`/`resetView` tool calls arrive, 24kHz PCM streamed | `status connected`, `reset forwarded` |
| 4 | E2E | PASS | Playwright: type "tell me about the temporal bone" in the real UI, real Gemini | status `connected`, temporal callout `.active`, guide transcript non-empty |
| 5 | UI | ~~FAIL~~ PASS after fix | Playwright: callout click, dimming, camera target/orbit, idle reset, `?author`, mic-denied path | camera target stays at model center — F1 |
| 6 | Regression | PASS | Existing 7 tests, `npm run typecheck`, `npm run build`, server typecheck (`server/` is outside `tsconfig`) | all exit 0; server needs `--types node,vite/client` |
| 7 | Performance | MEASURED | Playwright timings + build output | nav to model 1743ms; DCL 61ms; GLB 8.3MB in 30ms (local); JS heap 28MB; main chunk 1375KB (362KB gzip); 700 focus calls in 6ms |
| 8 | Load | PASS | `npm run test:load` — 50 concurrent GLB GETs on `vite preview`; 5 concurrent proxy sessions | static: wall 241ms, p95 226ms, 0 errors. proxy: manifest ~88ms, first audio 0.9–1.4s, turn complete 9.7–16.4s, 5/5 correct focus, 0 errors |
| 9 | Stress | PASS, 1 finding | `npm run test:stress` — 100 connect/close churn, 20MB frame, bad key (no token cost) | RSS +0.0MB, alive after all; 20MB frame accepted — F3 |
| 10 | Security | FINDINGS | `npm audit`, secret scan of git history, bundle scan, proxy code audit | history clean; key present in `dist/assets/index-*.js` (documented ceiling); 7 audit vulns; F3, F4, F5 |
| 11 | Accessibility | ~~FAIL~~ PASS after fix | Playwright: roles/names, Tab order, Enter activation, aria attributes, WCAG contrast | keyboard and callout names pass; F7 |
| 12 | Cross-platform | PARTIAL (phone fixed) | Viewports 1440/1024/390; Chromium; Node 22; Unity 6000.3.12f1 batchmode compile for Android | desktop/tablet pass, phone fails — F6; Unity `Assembly-CSharp.dll` built, 0 `error CS`, exit 0 |
| 13 | Smoke | PASS | `/`, `/SKULL.glb`, `/src/main.ts` 200; model `load` event; 7 callouts; status `idle`; zero console errors | 2/2 |
| 14 | Acceptance | PASS | README stories through the real UI: tour, reset (ask-about-bone covered by E2E) | tour focused `frontal` first and asked "Are you ready to see the top of the head next?"; reset cleared focus |
| 15 | Chaos / Resilience | PASS (2 findings, fixed) | Bad key, garbage frames, send-before-session, client drop mid-turn, browser offline, sends to dead session | proxy survives all; browser shows error and stays usable; F2 |

Skipped, and why: WebKit/Firefox (only Chromium installed), Unity PlayMode / headset (no device attached, no test framework package), Lighthouse and axe (would add dependencies).

## Findings, most severe first

**F1 — Bug: focus never centers on the anchor (web).** `src/main.ts:54` sets `mv.cameraTarget = f.position` with unitless numbers. `<model-viewer>` 4.3.1 silently ignores that string: after clicking Temporal, `getCameraTarget()` stays at `(-0.0001, 0.0878, -0.0015)` (the auto center) while `"-0.070m 0.108m -0.035m"` moves it to the anchor. The orbit direction from `orbitFromNormal` is applied correctly, so the camera looks along the right normal but at the skull's center from 0.16m. Verified — ran both forms in the browser. Fix: `mv.cameraTarget = f.position.split(/\s+/).map((v) => v + "m").join(" ")`. `e2e/app.spec.ts` "clicking a callout" fails until then.

**F2 — Proxy lies about connection state and keeps dead sessions open.** `server/proxy.ts:77` sends `status: connected` on socket open, before Gemini validates the key; with a bad key the client receives `connected`, then the manifest, then `closed: API key not valid`, and its socket is left open. Later `text`/`audio` messages are handed to the closed session and vanish silently (proxy does not crash — verified). Fix: in the `onclose` callback call `client.close()`; optionally hold `connected` until the first `serverContent` arrives.

**F3 — Proxy has no hardening.** Binds `0.0.0.0`, no auth token, no origin check, no `maxPayload` (a 20MB frame was accepted and buffered), no rate limit, unbounded `pending` array before the session exists. Fine on a trusted LAN, not beyond it. Fix: `new WebSocketServer({ port, maxPayload: 1 << 20 })` and a shared secret checked in `verifyClient` or on the first message.

**F4 — API key ships in the browser bundle.** `grep AIza dist/assets/index-*.js` hits. Already documented in README as the localhost ceiling; unchanged, just confirmed. Do not host `dist/` anywhere.

**F5 — `npm audit`: 7 vulnerabilities, all in build tooling.** critical `vitest` (fix = vitest@4, major), high `vite` (vite@8, major), high `postcss` and `nanoid` (`npm audit fix`, non-breaking), moderate `esbuild`, `vite-node`, `@vitest/mocker`. Nothing in the runtime bundle. Suggested: run `npm audit fix` now; schedule the vite/vitest major bumps.

**F6 — Phone layout unusable.** At 390px the `1fr 340px` grid leaves the viewer 50px wide. Fix: `@media (max-width: 700px) { body { grid-template-columns: 1fr; grid-template-rows: 1fr auto; } }`.

**F7 — Accessibility gaps** (`e2e/a11y.spec.ts` "labels, live regions and contrast"):
- `#mic-btn` accessible name is the emoji; add `aria-label="Toggle microphone"` and `aria-pressed`.
- `#chat-input` has only a placeholder; add `aria-label`.
- `#status` changes by script with no `aria-live="polite"`.
- `#transcript` streams conversation with no `role="log"`.
- `<model-viewer>` has no `alt`.
- Placeholder contrast 3.92:1 (`#757575` on `#14161c`); needs 4.5:1. `#status` at 5.92:1 passes.

**F8 — Build weight.** `public/SKULL.glb.bak` (6.99MB) is served and copied into `dist/`; delete it. Main JS chunk is 1375KB in one file (`@google/genai` + `model-viewer`); `build.rollupOptions.output.manualChunks` would split it. `office-bg.png` is 1MB.

**F9 — Dead config.** `.env` carries `VITE_LEAD_WEBHOOK_URL`; nothing in `src/`, `server/` or `unity/` reads it.

**F10 — Mic button opens a Live session before checking mic permission** (`src/main.ts:143`). A denied mic still costs a Gemini session. Start the mic first, then connect.

**F11 — `server/` is not typechecked by `npm run typecheck`.** It passes today with `tsc --noEmit --strict --module esnext --moduleResolution bundler --types node,vite/client server/*.ts`; add that as a script or a `tsconfig.server.json`.

## What was added

- `playwright.config.ts`, `e2e/` (smoke, app, acceptance, a11y, viewports, perf) — `npm run e2e`; Gemini specs skip themselves when `.env` has no key; `--grep @acceptance` runs the README stories alone.
- `src/manifest.test.ts`, `src/live.test.ts`, one case in `src/audio.test.ts` — `npm test`.
- `tests/proxy.test.ts`, `tests/load.ts`, `tests/stress.ts`, `tests/_proxy.ts` — `npm run test:proxy | test:load | test:stress`.
- `vitest.config.ts` so vitest ignores `e2e/` and `tests/`.
- devDependency `@playwright/test` + Chromium.

## Rerun

```bash
npm test && npm run typecheck && npm run build   # unit, regression
npm run e2e                                      # smoke, UI, E2E, acceptance, a11y, viewports, perf, chaos (3 Gemini turns)
npm run test:proxy                               # integration, API, chaos (2 Gemini turns)
npm run test:load                                # load (5 Gemini turns; build first)
npm run test:stress                              # stress (no Gemini cost)
U="/Applications/Unity/Hub/Editor/6000.3.12f1/Unity.app/Contents/MacOS/Unity"
$U -batchmode -nographics -quit -projectPath unity -buildTarget Android -logFile /tmp/unity.log && ! grep -q "error CS" /tmp/unity.log
```

## Fixes applied (same day)

- **F1** `src/main.ts`: `cameraTarget` now gets `m` units. Re-verified: `getCameraTarget()` lands on the anchor. This exposed a second masked issue: model-viewer clamps orbit radius to its auto `min-camera-orbit` (~0.287m for this model), so `FOCUS_RADIUS` was never honored. Added `min-camera-orbit="auto auto 0.1m"` to `index.html` and set `FOCUS_RADIUS` to 0.28: 0.16 (now reachable) framed a wall of bone with no context; 0.28 matches the framing users already had, now centered on the anchor. Screenshots checked for temporal, maxilla, occipital.
- **F2** `server/proxy.ts`: `connected` is sent on Gemini's first message (`setupComplete`) instead of socket open; `onclose` now closes the client. Bad-key sequence is now just `closed: API key not valid`, socket closed. `src/live.ts` (browser path) still announces on open; harmless there because the status line is replaced by the close reason.
- **F6** `index.html`: `@media (max-width: 700px)` stacks the layout, sidebar capped at 45vh.
- **F7** `index.html` / `src/main.ts`: `aria-label` + `aria-pressed` on the mic button, `aria-label` on the input, `aria-live="polite"` on status, `role="log"` on the transcript, `alt` on `<model-viewer>`, placeholder color `#9aa0ad` (6.4:1).

Re-verification: `npm test` 24/24, `npm run typecheck` 0, server typecheck 0, Playwright offline specs 14/14 (E2E/acceptance not rerun: the fixes don't touch the Gemini path; 2 further proxy turns spent on the `test:proxy` rerun, 13 total), `npm run test:proxy` 6 checks, 0 findings.
Still open: F3, F4, F5, F8, F9, F10, F11.
