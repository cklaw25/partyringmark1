// WebSocket link to the Python strategist (slow path). Never blocks the game loop.
const URL = (import.meta.env.VITE_BROKER_WS) || 'ws://localhost:8000/ws';

export class StrategistLink {
  constructor(onStatus) {
    this.onStatus = onStatus;
    this.busy = false;
    this.pending = new Map();
    this.id = 0;
    this.latencies = [];
    this.status = 'connecting';
    this.connect();
  }

  connect() {
    try { this.ws = new WebSocket(URL); } catch { this.setStatus('offline'); return; }
    this.ws.onopen = () => this.setStatus('online');
    this.ws.onclose = () => { this.setStatus('offline'); setTimeout(() => this.connect(), 3000); };
    this.ws.onerror = () => {};
    this.ws.onmessage = (msg) => {
      const data = JSON.parse(msg.data);
      const p = this.pending.get(data.id);
      if (!p) return;
      this.pending.delete(data.id);
      const ms = performance.now() - p.sent;
      this.latencies.push(ms); if (this.latencies.length > 50) this.latencies.shift();
      p.resolve(data.error ? null : { decision: data.decision, latency_ms: Math.round(ms), model: data.model });
    };
  }

  setStatus(s) { this.status = s; this.onStatus?.(s); }

  p95() {
    if (!this.latencies.length) return null;
    const s = [...this.latencies].sort((a, b) => a - b);
    return Math.round(s[Math.min(s.length - 1, Math.floor(s.length * 0.95))]);
  }
  last() { return this.latencies.length ? Math.round(this.latencies[this.latencies.length - 1]) : null; }

  decide(state, timeoutMs) {
    if (!this.ws || this.ws.readyState !== 1) return Promise.resolve(null);
    this.busy = true;
    const id = ++this.id;
    return new Promise((resolve) => {
      const done = (v) => { this.busy = false; resolve(v); };
      this.pending.set(id, { resolve: done, sent: performance.now() });
      this.ws.send(JSON.stringify({ type: 'decide', id, state }));
      setTimeout(() => { if (this.pending.has(id)) { this.pending.delete(id); done(null); } }, timeoutMs);
    });
  }
}
