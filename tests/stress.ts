// Stress: hammer the proxy with connect/disconnect churn and an oversize frame.
// Uses a bad key so Gemini rejects every session: no token cost.
// Run: npm run test:stress
import { execFileSync } from "node:child_process";
import { startProxy, Client } from "./_proxy";

const rssMB = (pid: number) => Number(execFileSync("ps", ["-o", "rss=", "-p", String(pid)]).toString().trim()) / 1024;

(async () => {
  const proxy = await startProxy(8793, "bad-key-for-testing");
  let failed = false;
  try {
    const N = 100;
    const rss0 = rssMB(proxy.proc.pid!);
    const t0 = performance.now();
    for (let i = 0; i < N; i++) {
      const c = new Client(proxy.url);
      await c.open();
      c.send({ type: "text", text: "x" });
      c.close();
    }
    // Let in-flight upstream connects settle, then measure.
    await new Promise((r) => setTimeout(r, 5000));
    const rss1 = rssMB(proxy.proc.pid!);
    console.log(`CHURN ${N} connect/close in ${Math.round(performance.now() - t0)}ms; RSS ${rss0.toFixed(1)}MB -> ${rss1.toFixed(1)}MB (+${(rss1 - rss0).toFixed(1)}MB); alive=${proxy.alive()}`);
    if (!proxy.alive() || rss1 - rss0 > 50) failed = true;

    // Oversize frame before the session exists: ws default maxPayload is 100MB, so
    // this is accepted and buffered in `pending`.
    const big = new Client(proxy.url);
    await big.open();
    const payload = JSON.stringify({ type: "text", text: "x".repeat(20 * 1024 * 1024) });
    big.send(payload);
    const st = await big.waitFor((m) => m.type === "status", 30_000, true);
    console.log(`OVERSIZE 20MB frame: accepted=${!big.closed || !!st} status=${st?.status ?? "(closed)"} alive=${proxy.alive()}`);
    if (!proxy.alive()) failed = true;

    // Still serving after all that?
    const last = new Client(proxy.url);
    await last.open();
    const s = await last.waitFor((m) => m.type === "status", 30_000);
    console.log(`AFTER: new client gets status "${s!.status}"; alive=${proxy.alive()}`);
    if (!proxy.alive()) failed = true;
  } catch (e) {
    console.error("FAIL", (e as Error).message);
    failed = true;
  } finally {
    proxy.stop();
  }
  process.exit(failed ? 1 : 0);
})();
