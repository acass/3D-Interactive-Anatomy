// Shared bits for the proxy test scripts: spawn server/proxy.ts on a port with a
// given key, and a WebSocket wrapper that records every message.
import { spawn, type ChildProcess } from "node:child_process";
import { WebSocket } from "ws";

export const realKey = process.env.GEMINI_API_KEY ?? process.env.VITE_GEMINI_API_KEY ?? "";

export interface Proxy {
  proc: ChildProcess;
  port: number;
  url: string;
  alive(): boolean;
  stop(): void;
}

export function startProxy(port: number, key: string): Promise<Proxy> {
  return new Promise((resolve, reject) => {
    const proc = spawn("node_modules/.bin/tsx", ["server/proxy.ts"], {
      env: { ...process.env, PROXY_PORT: String(port), GEMINI_API_KEY: key, VITE_GEMINI_API_KEY: key },
      stdio: ["ignore", "pipe", "pipe"],
    });
    let out = "";
    const onData = (d: Buffer) => {
      out += d.toString();
      if (out.includes("proxy listening")) {
        proc.stdout!.off("data", onData);
        resolve({
          proc,
          port,
          url: `ws://localhost:${port}`,
          alive: () => proc.exitCode === null && !proc.killed,
          stop: () => proc.kill(),
        });
      }
    };
    proc.stdout!.on("data", onData);
    proc.stderr!.on("data", (d) => process.stderr.write(`[proxy:${port}] ${d}`));
    proc.on("exit", (code) => reject(new Error(`proxy exited early (${code}): ${out}`)));
    setTimeout(() => reject(new Error("proxy did not start in 15s")), 15_000).unref();
  });
}

export type Msg = { type: string; [k: string]: any };

export class Client {
  ws: WebSocket;
  messages: Msg[] = [];
  closed = false;
  readonly t0 = performance.now();
  private waiters: Array<() => void> = [];

  constructor(url: string) {
    this.ws = new WebSocket(url);
    this.ws.on("message", (raw) => {
      this.messages.push(JSON.parse(raw.toString()));
      this.waiters.splice(0).forEach((w) => w());
    });
    this.ws.on("close", () => {
      this.closed = true;
      this.waiters.splice(0).forEach((w) => w());
    });
    this.ws.on("error", () => {});
  }

  open(): Promise<void> {
    return new Promise((r, j) => {
      this.ws.once("open", r);
      this.ws.once("error", j);
    });
  }

  send(o: unknown) {
    this.ws.send(typeof o === "string" ? o : JSON.stringify(o));
  }

  count(type: string) {
    return this.messages.filter((m) => m.type === type).length;
  }

  // Resolves with the first message matching pred (or when the socket closes if
  // allowClose), rejects on timeout.
  waitFor(pred: (m: Msg) => boolean, timeoutMs: number, allowClose = false): Promise<Msg | null> {
    return new Promise((resolve, reject) => {
      const check = () => {
        const hit = this.messages.find(pred);
        if (hit) return resolve(hit), true;
        if (this.closed && allowClose) return resolve(null), true;
        return false;
      };
      if (check()) return;
      const timer = setTimeout(() => reject(new Error(`timeout waiting ${timeoutMs}ms`)), timeoutMs);
      const w = () => {
        if (check()) clearTimeout(timer);
        else this.waiters.push(w);
      };
      this.waiters.push(w);
    });
  }

  ms() {
    return Math.round(performance.now() - this.t0);
  }

  close() {
    this.ws.close();
  }
}
