// Gemini Live proxy for the Quest app. Holds the API key (never ships in the APK)
// and reuses the exact brain from src/live.ts. Unity connects over a plain JSON
// WebSocket; this fans each client out to its own Gemini Live session.
//
//   Unity -> proxy : {type:"text", text} | {type:"audio", data:<b64 16kHz pcm>}
//   proxy -> Unity : {type:"manifest", features:[...]}  (once, on connect)
//                    {type:"status"|"transcript"|"turnComplete"|"interrupted"}
//                    {type:"audio", data:<b64 24kHz pcm>}
//                    {type:"focus", featureId} | {type:"reset"}
//
// Run: npm run proxy  (needs GEMINI_API_KEY in .env)

import { WebSocketServer, WebSocket } from "ws";
import { GoogleGenAI, Modality, type Session } from "@google/genai";
import {
  LIVE_MODEL,
  focusFeature,
  resetView,
  systemInstruction,
} from "../src/live";
import { features } from "../src/manifest";

const PORT = Number(process.env.PROXY_PORT ?? 8787);
const apiKey = process.env.GEMINI_API_KEY ?? process.env.VITE_GEMINI_API_KEY;
if (!apiKey) {
  console.error("Missing GEMINI_API_KEY (or VITE_GEMINI_API_KEY) in env.");
  process.exit(1);
}

const ai = new GoogleGenAI({ apiKey });
const wss = new WebSocketServer({ port: PORT });
console.log(`proxy listening on ws://0.0.0.0:${PORT}`);

wss.on("connection", async (client: WebSocket) => {
  const send = (o: object) => {
    if (client.readyState === WebSocket.OPEN) client.send(JSON.stringify(o));
  };
  let session: Session | null = null;

  // Attach the message listener BEFORE awaiting connect — ws drops messages that
  // arrive with no listener, and clients send their first turn on open. Buffer
  // until the Gemini session is ready, then flush in order.
  const pending: any[] = [];
  const handle = (m: any) => {
    if (m.type === "text") {
      session!.sendClientContent({
        turns: [{ role: "user", parts: [{ text: String(m.text) }] }],
        turnComplete: true,
      });
    } else if (m.type === "audio") {
      session!.sendRealtimeInput({
        audio: { data: String(m.data), mimeType: "audio/pcm;rate=16000" },
      });
    }
  };
  client.on("message", (raw) => {
    let m: any;
    try {
      m = JSON.parse(raw.toString());
    } catch {
      return;
    }
    if (session) handle(m);
    else pending.push(m);
  });

  try {
    session = await ai.live.connect({
      model: LIVE_MODEL,
      config: {
        responseModalities: [Modality.AUDIO],
        outputAudioTranscription: {},
        systemInstruction: systemInstruction(),
        tools: [{ functionDeclarations: [focusFeature, resetView] }],
      },
      callbacks: {
        onopen: () => send({ type: "status", status: "connected" }),
        onerror: (e: any) =>
          send({ type: "status", status: `error: ${e?.message ?? "failed"}` }),
        onclose: (e: any) =>
          send({ type: "status", status: `closed: ${e?.reason ?? e?.code}` }),
        onmessage: (msg: any) => {
          for (const call of msg.toolCall?.functionCalls ?? []) {
            if (call.name === "focusFeature")
              send({ type: "focus", featureId: String(call.args?.featureId) });
            else if (call.name === "resetView") send({ type: "reset" });
            if (call.id)
              session!.sendToolResponse({
                functionResponses: [
                  { id: call.id, name: call.name, response: { result: "ok" } },
                ],
              });
          }
          if (msg.serverContent?.interrupted) send({ type: "interrupted" });
          const t = msg.serverContent?.outputTranscription?.text;
          if (t) send({ type: "transcript", text: t });
          for (const p of msg.serverContent?.modelTurn?.parts ?? []) {
            if (p.text) send({ type: "transcript", text: p.text });
            const audio = p.inlineData?.data;
            if (audio && p.inlineData?.mimeType?.startsWith("audio/"))
              send({ type: "audio", data: audio });
          }
          if (msg.serverContent?.turnComplete) send({ type: "turnComplete" });
        },
      },
    });
  } catch (err) {
    send({ type: "status", status: `connect failed: ${(err as Error).message}` });
    client.close();
    return;
  }

  // The Feature Manifest is the single source of truth (ADR 0002). The Quest app has
  // no copy of it: it builds its callouts and bone mapping from this push, so editing
  // src/manifest.ts and restarting the proxy updates the headset with no APK rebuild.
  // `explanation` is deliberately withheld — it belongs to systemInstruction(), not the client.
  send({
    type: "manifest",
    features: features.map(({ id, label, position, normal, meshNames }) => ({
      id,
      label,
      position,
      normal,
      meshNames,
    })),
  });

  for (const m of pending.splice(0)) handle(m); // flush anything that arrived pre-connect

  client.on("close", () => session?.close());
});
