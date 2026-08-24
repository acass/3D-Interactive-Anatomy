// Load: 50 concurrent GLB fetches against `vite preview` (needs `npm run build` first),
// then 5 concurrent proxy sessions each asking one question (5 Gemini turns).
// Run: npm run test:load
import { spawn } from "node:child_process";
import { startProxy, Client, realKey } from "./_proxy";
import { features } from "../src/manifest";

const pct = (xs: number[], p: number) => {
  const s = [...xs].sort((a, b) => a - b);
  return s[Math.min(s.length - 1, Math.floor((p / 100) * s.length))];
};

async function staticLoad() {
  const port = 4173;
  const preview = spawn("node_modules/.bin/vite", ["preview", "--port", String(port), "--strictPort"], {
    stdio: ["ignore", "pipe", "pipe"],
  });
  await new Promise<void>((resolve, reject) => {
    preview.stdout.on("data", (d) => d.toString().includes("Local:") && resolve());
    preview.on("exit", (c) => reject(new Error(`vite preview exited ${c}`)));
    setTimeout(() => reject(new Error("vite preview did not start")), 15_000).unref();
  });
  try {
    const N = 50;
    const t0 = performance.now();
    const results = await Promise.all(
      Array.from({ length: N }, async () => {
        const s = performance.now();
        const r = await fetch(`http://localhost:${port}/SKULL.glb`);
        const buf = await r.arrayBuffer();
        return { ms: performance.now() - s, ok: r.ok, bytes: buf.byteLength };
      })
    );
    const wall = Math.round(performance.now() - t0);
    const ms = results.map((r) => r.ms);
    const errors = results.filter((r) => !r.ok).length;
    const bytes = results.reduce((a, r) => a + r.bytes, 0);
    console.log(
      `STATIC ${N} concurrent GET /SKULL.glb: wall=${wall}ms p50=${Math.round(pct(ms, 50))}ms p95=${Math.round(pct(ms, 95))}ms max=${Math.round(Math.max(...ms))}ms errors=${errors} throughput=${Math.round(bytes / 1048576 / (wall / 1000))}MB/s`
    );
    if (errors) process.exitCode = 1;
  } finally {
    preview.kill();
  }
}

async function proxyLoad() {
  const N = 5;
  const proxy = await startProxy(8792, realKey);
  try {
    const rows = await Promise.all(
      features.slice(0, N).map(async (f) => {
        const c = new Client(proxy.url);
        const row: Record<string, any> = { ask: f.id };
        try {
          await c.open();
          c.send({ type: "text", text: `Tell me about the ${f.label.toLowerCase()}.` });
          await c.waitFor((m) => m.type === "manifest", 30_000);
          row.manifestMs = c.ms();
          await c.waitFor((m) => m.type === "audio", 60_000);
          row.firstAudioMs = c.ms();
          await c.waitFor((m) => m.type === "turnComplete", 90_000);
          row.turnCompleteMs = c.ms();
          row.focus = c.messages.find((m) => m.type === "focus")?.featureId ?? null;
          row.audioChunks = c.count("audio");
          row.errors = c.messages.filter((m) => m.type === "status" && /error|failed/.test(m.status)).map((m) => m.status);
        } catch (e) {
          row.error = (e as Error).message;
          process.exitCode = 1;
        } finally {
          c.close();
        }
        return row;
      })
    );
    console.log(`PROXY ${N} concurrent sessions (GEMINI_TURNS=${N}):`);
    for (const r of rows) console.log("  " + JSON.stringify(r));
    const wrong = rows.filter((r) => r.focus !== r.ask);
    if (wrong.length) console.log(`  NOTE ${wrong.length}/${N} sessions focused a different bone than asked`);
  } finally {
    proxy.stop();
  }
}

(async () => {
  await staticLoad();
  if (realKey) await proxyLoad();
  else console.log("SKIP proxy load: no key");
  process.exit(process.exitCode ?? 0);
})();
