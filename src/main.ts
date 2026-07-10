import "@google/model-viewer";
import { features, MODEL_SRC } from "./manifest";
import { orbitFromNormal, parseVec } from "./orbit";
import { connectLive } from "./live";
import { AudioPlayer, MicStreamer } from "./audio";

const FOCUS_RADIUS = 0.16; // meters from the anchor when focused (SKULL.glb is ~0.24m tall)
const IDLE_TIMEOUT_MS = 12000; // return to idle pose after this much quiet
const DEFAULT_ORBIT = "0deg 80deg 105%"; // idle framing faces the front of the skull

const mv = document.getElementById("viewer") as any; // <model-viewer>
const transcript = document.getElementById("transcript")!;
const form = document.getElementById("chat-form") as HTMLFormElement;
const input = document.getElementById("chat-input") as HTMLInputElement;
const statusEl = document.getElementById("status")!;
const micBtn = document.getElementById("mic-btn") as HTMLButtonElement;

const player = new AudioPlayer();
const mic = new MicStreamer();

mv.src = MODEL_SRC;

// --- Callouts (hybrid: all shown in idle, active highlighted / others dimmed) ---
for (const f of features) {
  const btn = document.createElement("button");
  btn.slot = `hotspot-${f.id}`;
  btn.className = "callout";
  btn.dataset.position = f.position;
  btn.dataset.normal = f.normal;
  btn.dataset.id = f.id;
  btn.textContent = f.label;
  btn.addEventListener("click", () => focusFeature(f.id)); // clickable too, not just voice/text
  mv.appendChild(btn);
}

function setActive(id: string | null) {
  mv.classList.toggle("focused", id !== null);
  for (const el of mv.querySelectorAll(".callout")) {
    el.classList.toggle("active", (el as HTMLElement).dataset.id === id);
  }
}

// --- Camera control ---
let idleTimer: number | undefined;
function bumpIdleTimer() {
  clearTimeout(idleTimer);
  idleTimer = window.setTimeout(resetView, IDLE_TIMEOUT_MS);
}

function focusFeature(id: string) {
  const f = features.find((x) => x.id === id);
  if (!f) return;
  mv.autoRotate = false;
  mv.cameraTarget = f.position;
  mv.cameraOrbit = orbitFromNormal(parseVec(f.normal), FOCUS_RADIUS);
  setActive(id);
  bumpIdleTimer();
}

function resetView() {
  clearTimeout(idleTimer);
  mv.cameraTarget = "auto auto auto";
  mv.cameraOrbit = DEFAULT_ORBIT;
  mv.autoRotate = true;
  setActive(null);
}

resetView(); // start in idle pose

// --- Author mode: ?author -> click the model to capture position + normal ---
if (new URLSearchParams(location.search).has("author")) {
  statusEl.textContent = "AUTHOR MODE — click the model; anchors print to the console";
  mv.addEventListener("click", (e: MouseEvent) => {
    const hit = mv.positionAndNormalFromPoint(e.clientX, e.clientY);
    if (!hit) return;
    console.log(
      `position: "${hit.position.toString()}"  normal: "${hit.normal.toString()}"`
    );
  });
}

// --- Transcript ---
function addLine(who: "you" | "guide", text: string) {
  const line = document.createElement("div");
  line.className = `line ${who}`;
  line.textContent = text;
  transcript.appendChild(line);
  transcript.scrollTop = transcript.scrollHeight;
  return line;
}

// --- Live API wiring (text path) ---
let live: Awaited<ReturnType<typeof connectLive>> | null = null;
let currentGuideLine: HTMLElement | null = null;

async function ensureConnected() {
  if (live) return live;
  live = await connectLive({
    onStatus: (s) => (statusEl.textContent = s),
    onText: (t) => {
      if (!currentGuideLine) currentGuideLine = addLine("guide", "");
      currentGuideLine.textContent += t;
      transcript.scrollTop = transcript.scrollHeight;
      bumpIdleTimer(); // activity keeps the camera on the feature
    },
    onTurnComplete: () => {
      currentGuideLine = null;
    },
    onFocus: (id) => focusFeature(id),
    onReset: () => resetView(),
    onAudio: (b64) => player.enqueue(b64),
    onInterrupted: () => player.stop(),
  });
  return live;
}

form.addEventListener("submit", async (e) => {
  e.preventDefault();
  const text = input.value.trim();
  if (!text) return;
  input.value = "";
  addLine("you", text);
  bumpIdleTimer();
  try {
    await player.resume(); // user gesture — unlock audio output
    const conn = await ensureConnected();
    conn.sendText(text);
  } catch {
    /* status already surfaced */
  }
});

// --- Open mic (barge-in) ---
micBtn.addEventListener("click", async () => {
  if (mic.active) {
    mic.stop();
    micBtn.classList.remove("live");
    micBtn.textContent = "🎤";
    return;
  }
  try {
    await player.resume(); // user gesture — unlock audio output
    const conn = await ensureConnected();
    await mic.start((b64) => conn.sendAudioChunk(b64));
    micBtn.classList.add("live");
    micBtn.textContent = "◉ Listening";
  } catch (err) {
    statusEl.textContent = `mic error: ${(err as Error).message}`;
  }
});
