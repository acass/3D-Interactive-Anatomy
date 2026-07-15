// Headless check for the proxy: sends one text turn, prints every event.
// Proves the Gemini brain + focusFeature tool-calling work over the WS before
// any Unity/XR exists.  Run the proxy first (npm run proxy), then: npm run proxy:test
import { WebSocket } from "ws";

const url = process.env.PROXY_URL ?? "ws://localhost:8787";
const prompt = process.argv.slice(2).join(" ") || "Tell me about the temporal bone.";
const ws = new WebSocket(url);
let audioChunks = 0;
let focused: string | null = null;

ws.on("open", () => {
  console.log(`> ${prompt}`);
  ws.send(JSON.stringify({ type: "text", text: prompt }));
});
ws.on("message", (raw) => {
  const m = JSON.parse(raw.toString());
  if (m.type === "audio") audioChunks++;
  else if (m.type === "focus") (focused = m.featureId), console.log(`[focus] ${m.featureId}`);
  else if (m.type === "reset") console.log("[reset]");
  else if (m.type === "transcript") process.stdout.write(m.text);
  else if (m.type === "turnComplete") {
    console.log(`\n[done] audioChunks=${audioChunks} focused=${focused}`);
    // A "temporal bone" prompt must both speak and focus the temporal feature.
    if (audioChunks === 0) console.error("FAIL: no audio streamed");
    if (/temporal/i.test(prompt) && focused !== "temporal")
      console.error(`FAIL: expected focus=temporal, got ${focused}`);
    ws.close();
    process.exit(0);
  } else console.log(`[${m.type}] ${m.status ?? ""}`);
});
ws.on("error", (e) => (console.error("ws error:", e.message), process.exit(1)));
