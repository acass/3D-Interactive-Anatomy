// Gemini Live API client. v1: text in / text out with function calling.
// Audio (open-mic in, spoken out) attaches to this same session later — see README.

import { GoogleGenAI, Modality, Type, type Session } from "@google/genai";
import { features, details } from "./manifest";

// Text + tool-calling Live model. Swap to a native-audio Live model (e.g.
// gemini-2.5-flash-native-audio-latest) when adding spoken voice output.
const MODEL = "gemini-3.1-flash-live-preview";

const featureIds = features.map((f) => f.id);

const focusFeature = {
  name: "focusFeature",
  description:
    "Move the camera to frame a specific feature of the model. Call this whenever the user asks about, points to, or wants to see a feature.",
  parameters: {
    type: Type.OBJECT,
    properties: {
      featureId: {
        type: Type.STRING,
        enum: featureIds,
        description: "The id of the feature to focus on.",
      },
    },
    required: ["featureId"],
  },
};

const resetView = {
  name: "resetView",
  description:
    "Return the camera to the default view of the whole model. Call when the user asks to see the whole thing, go back, or start over.",
  parameters: { type: Type.OBJECT, properties: {} },
};

function systemInstruction(): string {
  const list = features
    .map((f) => `- ${f.id} ("${f.label}"): ${f.explanation}`)
    .join("\n");
  const detailList = details.map((d) => `- ${d.label}: ${d.text}`).join("\n");
  const tourOrder = features.map((f) => f.id).join(", ");
  return [
    "You are a friendly, knowledgeable anatomy tutor guiding a learner through a 3D human skull on screen. Your goal is to help them understand the skull's bones and how they fit together. The skull has these labeled regions, each with an authored explanation:",
    list,
    "",
    "Whole-skull facts (these have no single on-screen location):",
    detailList,
    "",
    "Rules:",
    "- When the user asks about a region, call focusFeature with its id AND explain it, staying faithful to the authored explanation above.",
    "- If the user asks to see the whole skull / go back / reset, call resetView.",
    "- When the user asks a whole-skull question above (bone count, sutures, foramen magnum, cranium vs. face), answer from that fact, stay faithful to it, and do NOT call any tool (no camera move).",
    "- Guided tour: if the user asks for a tour, to be walked through it, or where to start, walk the regions in this order: " + tourOrder + ". For each, call focusFeature with its id, give its explanation in a sentence or two, then ask if they're ready for the next one before moving on. Don't rush through all of them in one turn.",
    "- Related anatomy questions beyond the labeled regions (e.g. other bones, the orbit, teeth, how bones develop): you may answer from your general knowledge, but stay factual and brief, and say plainly if you're not certain rather than guessing. Do not invent specific facts (which foramen, which suture) you aren't sure of.",
    "- For small talk or off-topic questions, answer briefly and do NOT call any tool.",
    "- Keep spoken answers concise and conversational. Use the plain English name first and the anatomical term alongside it, the way the explanations above do.",
    "",
    "Language:",
    "- Detect the language of each user message and reply in it, both spoken and in text.",
    "- If the user writes in Spanish, French, or German, reply ENTIRELY in that language.",
    "- If the user writes in English, reply in English.",
    "- If the user writes in any OTHER language, reply in English, and once per conversation briefly mention you can help in English, Spanish, French, or German.",
    "- The authored explanations and facts above are written in English. When replying in another language, translate them faithfully: keep the same anatomical facts exactly. Do not invent anything beyond them.",
    "- Language never changes tool behavior: still call focusFeature / resetView the same way, with the same English region ids, in every language.",
  ].join("\n");
}

export interface LiveHandlers {
  onText: (text: string) => void; // incremental assistant transcript
  onTurnComplete: () => void;
  onFocus: (featureId: string) => void;
  onReset: () => void;
  onStatus: (status: string) => void;
  onAudio: (b64Pcm: string) => void; // 24kHz PCM chunk of spoken output
  onInterrupted: () => void; // user barged in; stop playback
}

export async function connectLive(handlers: LiveHandlers) {
  const apiKey = import.meta.env.VITE_GEMINI_API_KEY;
  if (!apiKey) {
    handlers.onStatus("Missing VITE_GEMINI_API_KEY — copy .env.example to .env and add your key.");
    throw new Error("Missing VITE_GEMINI_API_KEY");
  }

  const ai = new GoogleGenAI({ apiKey });
  let session: Session;

  session = await ai.live.connect({
    model: MODEL,
    config: {
      // The available Live models only output audio, so we take AUDIO out and
      // drive the on-screen transcript from the audio transcription.
      responseModalities: [Modality.AUDIO],
      outputAudioTranscription: {},
      systemInstruction: systemInstruction(),
      tools: [{ functionDeclarations: [focusFeature, resetView] }],
    },
    callbacks: {
      onopen: () => handlers.onStatus("connected"),
      onerror: (e: ErrorEvent) => {
        console.error("Live onerror:", e);
        handlers.onStatus(`error: ${e.message || "connection failed"}`);
      },
      onclose: (e: CloseEvent) => {
        console.error("Live onclose:", e.code, e.reason);
        handlers.onStatus(
          e.reason ? `closed: ${e.reason}` : `disconnected (code ${e.code})`
        );
      },
      onmessage: (msg) => {
        // Tool calls
        const calls = msg.toolCall?.functionCalls ?? [];
        for (const call of calls) {
          if (call.name === "focusFeature") {
            handlers.onFocus(String((call.args as any)?.featureId));
          } else if (call.name === "resetView") {
            handlers.onReset();
          }
          if (call.id) {
            session.sendToolResponse({
              functionResponses: [{ id: call.id, name: call.name!, response: { result: "ok" } }],
            });
          }
        }

        // Barge-in: user started talking over the model
        if (msg.serverContent?.interrupted) handlers.onInterrupted();

        // Streamed transcript of the spoken audio
        const transcriptText = msg.serverContent?.outputTranscription?.text;
        if (transcriptText) handlers.onText(transcriptText);

        // Spoken audio chunks (and any literal text parts, just in case)
        const parts = msg.serverContent?.modelTurn?.parts ?? [];
        for (const p of parts) {
          if (p.text) handlers.onText(p.text);
          const audio = p.inlineData?.data;
          if (audio && p.inlineData?.mimeType?.startsWith("audio/")) handlers.onAudio(audio);
        }

        if (msg.serverContent?.turnComplete) handlers.onTurnComplete();
      },
    },
  });

  return {
    sendText(text: string) {
      session.sendClientContent({
        turns: [{ role: "user", parts: [{ text }] }],
        turnComplete: true,
      });
    },
    sendAudioChunk(b64Pcm: string) {
      session.sendRealtimeInput({
        audio: { data: b64Pcm, mimeType: "audio/pcm;rate=16000" },
      });
    },
    close() {
      session.close();
    },
  };
}
