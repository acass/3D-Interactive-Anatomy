# Interactive Skull Anatomy

<img width="1106" height="629" alt="image" src="https://github.com/user-attachments/assets/e1376f7e-fe0d-4cdf-b35f-c0a45bb877dc" />

A web app that shows a 3D human skull with callouts for its major bones. Ask about a
bone by voice or text — or say "give me a tour" — and the camera flies to frame it
while an anatomy tutor explains it. See [CONTEXT.md](CONTEXT.md) for the glossary and
[docs/adr/](docs/adr/) for decisions.

## Run

```bash
npm install
cp .env.example .env      # then add your Gemini API key
npm run dev
```

Open the printed localhost URL.

- **Ask about a bone** — type "tell me about the temporal bone" and the camera flies there.
- **Take a tour** — say "give me a tour" and the tutor walks the bones in order.
- **Reset** — ask to "see the whole skull" or click a dimmed callout.
- Get a key at https://aistudio.google.com/apikey

## Author mode — placing anchors

The [Feature Manifest](src/manifest.ts) is the single source of truth. To capture the
`position` and `normal` for a feature, open the app with `?author`:

```
http://localhost:5173/?author
```

Click a spot on the model; the position and normal print to the browser console.
Paste them into `src/manifest.ts`. Swapping in a different model = replace `MODEL_SRC`
(drop a `.glb` in `public/`) and rewrite the `features` array. No code changes.

## Test

```bash
npm test          # camera-orbit math
npm run typecheck
```

## Status / roadmap

- [x] Skull + bone callouts, camera fly-to, idle auto-rotate, quiet-timeout return
- [x] Author mode for anchor capture
- [x] Live API with `focusFeature` / `resetView` function calling
- [x] Anatomy tutor: faithful bone explanations, guided tour, bounded free answers
- [x] Spoken audio out (transcript shown on screen)
- [x] Open-mic voice input with barge-in (🎤 button)
- [ ] Quiz / "test me" mode
- [ ] Deeper content tiers (student / clinical) and extra callouts (orbit, sutures)

## Security ceiling

The Gemini key is injected via Vite and **is visible in the browser bundle**. Fine for
localhost. **Before hosting this anywhere**, add an ephemeral-token endpoint: a tiny
server route holds the real key and mints short-lived tokens the browser uses to open
the Live session, so the key never ships. Swap `src/live.ts` to fetch a token instead
of reading `VITE_GEMINI_API_KEY`.
