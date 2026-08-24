// Integration + API + chaos checks for server/proxy.ts. Plain node, no framework.
// Run: npm run test:proxy   (needs the key in .env; costs 2 Gemini turns)
import assert from "node:assert/strict";
import { features } from "../src/manifest";
import { startProxy, Client, realKey } from "./_proxy";

let geminiTurns = 0;
const results: string[] = [];
function pass(name: string) {
  results.push(`PASS ${name}`);
  console.log(`PASS ${name}`);
}

// Findings: contract violations that should not abort the run. Printed at the end;
// any finding makes the script exit 1 so CI notices.
const findings: string[] = [];
function check(cond: boolean, msg: string) {
  if (!cond) {
    findings.push(msg);
    console.log(`FINDING ${msg}`);
  }
}

async function badKeySection() {
  const proxy = await startProxy(8791, "bad-key-for-testing");
  try {
    // Chaos: Gemini rejects the credentials. Observed today: the upstream socket opens
    // (proxy says "connected"), the manifest is pushed, then Gemini closes with
    // "API key not valid". The client is never disconnected.
    const c = new Client(proxy.url);
    await c.open();
    c.send("this is not json");
    c.send({ type: "nope" });
    c.send({});
    c.send({ type: "text", text: "hello" });
    const closed = await c.waitFor((m) => m.type === "status" && /closed|failed|error/.test(m.status), 20_000, true);
    assert.ok(closed, "client hears that the upstream session ended");
    assert.match(String(closed!.status), /API key not valid/, `upstream rejection reason forwarded (${closed!.status})`);
    const statuses = c.messages.filter((m) => m.type === "status").map((m) => m.status);
    console.log(`  bad key status sequence: ${JSON.stringify(statuses)}; manifest=${c.count("manifest")}`);
    check(statuses[0] !== "connected", "proxy reports 'connected' before Gemini has validated the key");
    await c.waitFor(() => false, 2_000, true).catch(() => {});
    check(c.closed, "client socket is not closed after the upstream session dies");
    assert.equal(proxy.alive(), true, "proxy still running");

    // Chaos: client keeps talking to a dead session. Must not crash the proxy.
    if (!c.closed) {
      c.send({ type: "text", text: "are you there?" });
      c.send({ type: "audio", data: "AAAA" });
      await new Promise((r) => setTimeout(r, 3000));
      assert.equal(proxy.alive(), true, "proxy survives sends to a dead session");
      console.log(`  after sends to dead session: alive=${proxy.alive()} clientClosed=${c.closed}`);
      c.close();
    }
    pass("bad key: rejection surfaced, proxy alive");

    // Protocol garbage on a fresh connection does not crash the process.
    const c2 = new Client(proxy.url);
    await c2.open();
    c2.send("{{{{");
    c2.send(Buffer.alloc(0));
    await c2.waitFor((m) => m.type === "status", 20_000, true);
    assert.equal(proxy.alive(), true, "proxy survives garbage frames");
    c2.close();
    pass("bad key: garbage frames ignored, proxy alive");
  } finally {
    proxy.stop();
  }
}

async function realSection() {
  const proxy = await startProxy(8790, realKey);
  try {
    // Client 1: send garbage AND the first turn before the Gemini session exists.
    // Exercises the pending buffer (chaos: client faster than upstream).
    const c = new Client(proxy.url);
    await c.open();
    c.send("not json");
    c.send({ type: "nope" });
    c.send({});
    c.send({ type: "text", text: "Tell me about the temporal bone." });
    geminiTurns++;

    // Protocol: "connected" status first, then the manifest, before any turn output.
    const first = await c.waitFor((m) => m.type !== "status", 30_000);
    assert.equal(first!.type, "manifest", `first non-status message is the manifest, got ${first!.type}`);
    const tManifest = c.ms();
    assert.equal(first!.features.length, features.length, "manifest has every feature");
    assert.deepEqual(
      first!.features.map((f: any) => f.id),
      features.map((f) => f.id),
      "manifest ids match src/manifest.ts in order"
    );
    for (const f of first!.features) {
      assert.ok(!("explanation" in f), `explanation must not leak to client (${f.id})`);
      assert.ok(f.meshNames?.length > 0, `meshNames present (${f.id})`);
      assert.ok(f.position && f.normal && f.label, `anchor fields present (${f.id})`);
    }
    pass(`manifest: ${features.length} features, no explanation leak, ${tManifest}ms after open`);

    const focus = await c.waitFor((m) => m.type === "focus", 60_000);
    assert.equal(focus!.featureId, "temporal", "tool call focused temporal");
    const tFocus = c.ms();
    const done = await c.waitFor((m) => m.type === "turnComplete", 60_000);
    assert.ok(done, "turnComplete arrived");
    assert.ok(c.count("audio") > 0, "spoken audio streamed");
    assert.ok(c.count("transcript") > 0, "transcript streamed");
    const statuses = c.messages.filter((m) => m.type === "status").map((m) => m.status);
    assert.ok(statuses.includes("connected"), `status connected seen (${statuses})`);
    assert.ok(!statuses.some((s) => /^error/.test(s)), `no error status (${statuses})`);
    pass(`turn 1: focus=temporal at ${tFocus}ms, audio=${c.count("audio")} chunks, transcript=${c.count("transcript")} parts, turnComplete at ${c.ms()}ms`);

    // Turn 2: reset tool, then drop the socket mid-stream (chaos: client vanishes).
    const before = c.messages.length;
    c.send({ type: "text", text: "Show me the whole skull." });
    geminiTurns++;
    const reset = await c.waitFor((m, i = c.messages.indexOf(m)) => m.type === "reset" && i >= before, 60_000);
    assert.ok(reset, "resetView tool call forwarded as {type:reset}");
    await c.waitFor((m) => m.type === "audio" && c.messages.indexOf(m) >= before, 60_000);
    c.close();
    pass("turn 2: reset forwarded; client dropped mid-stream");

    await new Promise((r) => setTimeout(r, 1500));
    assert.equal(proxy.alive(), true, "proxy alive after client dropped mid-turn");

    // Client 3: proxy still serves a new session after the drop (no turn sent).
    const c3 = new Client(proxy.url);
    await c3.open();
    const m3 = await c3.waitFor((m) => m.type === "manifest", 30_000);
    assert.equal(m3!.features.length, features.length);
    c3.close();
    pass("new client after mid-turn drop still receives the manifest");
  } finally {
    proxy.stop();
  }
}

(async () => {
  try {
    await badKeySection();
    if (!realKey) {
      console.log("SKIP real Gemini section: no GEMINI_API_KEY / VITE_GEMINI_API_KEY");
    } else {
      await realSection();
    }
    console.log(`\n${results.length} checks passed, ${findings.length} findings. GEMINI_TURNS=${geminiTurns}`);
    for (const f of findings) console.log(`  FINDING ${f}`);
    process.exit(findings.length ? 1 : 0);
  } catch (err) {
    console.error(`\nFAIL: ${(err as Error).message}`);
    console.error(`GEMINI_TURNS=${geminiTurns}`);
    process.exit(1);
  }
})();
