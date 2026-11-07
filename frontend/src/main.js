import Phaser from 'phaser';
import { Maze, TILE } from './maze.js';
import { BrokerMemory } from './memory.js';
import { Broker, fallbackDecision, pick } from './broker.js';
import { StrategistLink } from './net.js';
import { Panel, showPostMortem } from './ui.js';
import { Voice } from './voice.js';

const params = new URLSearchParams(location.search);
const SEED = parseInt(params.get('seed') || '1337', 10);
const FULL = params.get('mode') === 'full';
const GAME_SECONDS = FULL ? 30 * 60 : 5 * 60;
const STRATEGY_INTERVAL = 5;      // seconds between routine strategist calls
const STRUCTURAL_COOLDOWN = 8;    // seconds between maze changes
const MAX_BROKER_CLOSED = 4;

class VaultScene extends Phaser.Scene {
  constructor() { super('vault'); }

  create() {
    this.maze = new Maze(SEED);
    this.mem = new BrokerMemory();
    this.broker = new Broker(this, this.maze, this.mem);
    this.voice = new Voice();
    this.panel = new Panel();
    this.link = new StrategistLink((s) => this.panel.link(s));
    this.t0 = performance.now();
    this.elapsed = 0;
    this.lives = 3;
    this.progress = { fuse: false, power: false, keycard: false };
    this.bottles = 2;
    this.fog = true;
    this.lastStrategy = -99;
    this.lastStructural = -99;
    this.brokerClosed = [];
    this.pendingOutcome = null;
    this.fastSamples = [];
    this.over = false;
    this.lastReflexSay = 0;

    const m = this.maze;
    // static maze
    const g = this.add.graphics();
    for (let y = 0; y < m.h; y++) for (let x = 0; x < m.w; x++) {
      if (m.grid[y][x] === 1) { g.fillStyle(0x1a1f26); g.fillRect(x * TILE, y * TILE, TILE, TILE); g.lineStyle(1, 0x232a33); g.strokeRect(x * TILE, y * TILE, TILE, TILE); }
      else { g.fillStyle(m.roomAt(x, y) ? 0x10161c : 0x0b0f13); g.fillRect(x * TILE, y * TILE, TILE, TILE); }
    }
    // room labels
    for (const [name, rm] of Object.entries(m.rooms)) {
      this.add.text(rm.x * TILE + 2, rm.y * TILE + 1, name.toUpperCase(), { fontSize: '9px', color: '#3b4a58', fontFamily: 'Consolas' });
    }
    this.doorG = this.add.graphics();
    this.itemG = this.add.graphics();
    this.drawDoors(); this.drawItems();

    // player + broker
    this.player = { x: (m.start.x + 0.5) * TILE, y: (m.start.y + 0.5) * TILE, hidden: false, running: false, locker: null, facing: { x: 1, y: 0 } };
    Object.defineProperty(this.player, 'tile', { get: () => ({ x: Math.floor(this.player.x / TILE), y: Math.floor(this.player.y / TILE) }) });
    this.playerG = this.add.circle(this.player.x, this.player.y, TILE * 0.3, 0x7fd1ff);
    this.brokerG = this.add.container(this.broker.x, this.broker.y, [
      this.add.circle(0, 0, TILE * 0.42, 0x6b0000, 0.5),
      this.add.circle(0, 0, TILE * 0.3, 0xe04848),
      this.add.circle(0, 0, TILE * 0.1, 0xffffff),
    ]);
    this.alarmRect = this.add.rectangle(0, 0, m.w * TILE, m.h * TILE, 0xff0000, 0).setOrigin(0);

    // fog of war with a soft light circle
    const lightSize = 512;
    const tex = this.textures.createCanvas('light', lightSize, lightSize);
    const ctx = tex.getContext();
    const grd = ctx.createRadialGradient(lightSize / 2, lightSize / 2, 0, lightSize / 2, lightSize / 2, lightSize / 2);
    grd.addColorStop(0, 'rgba(255,255,255,1)'); grd.addColorStop(0.6, 'rgba(255,255,255,0.85)'); grd.addColorStop(1, 'rgba(255,255,255,0)');
    ctx.fillStyle = grd; ctx.fillRect(0, 0, lightSize, lightSize); tex.refresh();
    this.lightImg = this.make.image({ key: 'light', add: false });
    this.fogRT = this.add.renderTexture(0, 0, m.w * TILE, m.h * TILE).setOrigin(0).setDepth(5);

    // input
    this.keys = this.input.keyboard.addKeys('W,A,S,D,UP,DOWN,LEFT,RIGHT,SHIFT,E,SPACE,V,M');
    this.keys.E.on('down', () => this.interact());
    this.keys.SPACE.on('down', () => this.throwBottle());
    this.keys.V.on('down', () => { this.fog = !this.fog; });
    this.keys.M.on('down', () => this.micNoise());

    this.emitEvent({ event: 'game_started', seed: SEED, mode: FULL ? 'full' : 'demo' });
    this.say('Welcome to the Vault. I will be studying you.', 'strategist');
    this.updateHud();
    this.time.addEvent({ delay: 250, loop: true, callback: () => this.refreshPanel() });
  }

  // ---------- helpers ----------
  now() { return this.elapsed; }

  emitEvent(ev) {
    const e = { ...ev, t: Math.round(this.elapsed) };
    this.mem.push(e);
    const significant = ['item_collected', 'power_restored', 'player_captured', 'player_spotted', 'player_hiding', 'distraction_detected', 'door_blocked_by_player_route'];
    if (significant.includes(ev.event)) this.requestStrategy(ev.event);
  }

  say(text, kind = 'strategist') {
    if (kind === 'reflex') {
      if (this.elapsed - this.lastReflexSay < 3) return;
      this.lastReflexSay = this.elapsed;
    }
    document.getElementById('subtitle').textContent = 'THE BROKER: ' + text;
    this.voice.speak(text, kind);
  }

  onBrokerState(s, reason) { /* panel picks it up on refresh */ }

  objectiveTargets() {
    const m = this.maze, t = [];
    const fuse = m.items.find(i => i.type === 'fuse'); if (fuse) t.push(fuse);
    if (!this.progress.power) t.push(m.generatorPos);
    const kc = m.items.find(i => i.type === 'keycard'); if (kc) t.push(kc);
    t.push(m.exitPos);
    return t;
  }

  objectivePoint(name) {
    const m = this.maze, rm = m.rooms;
    const map = { fuse: rm.storage, storage: rm.storage, generator: rm.generator, keycard: rm.security, security: rm.security, exit: rm.exit, start: rm.start };
    if (map[name]) return { x: map[name].cx, y: map[name].cy };
    if (name) return m.zoneCentre(name);
    return null;
  }

  closableDoors() {
    const pt = this.player.tile, targets = this.objectiveTargets();
    return this.maze.doors.filter(d => !d.closed && this.maze.canClose(d.id, pt, targets)).map(d => d.id);
  }

  // ---------- slow path: the strategist ----------
  buildState() {
    const m = this.maze, ls = this.mem.lastSeen;
    return {
      time_left_s: Math.round(GAME_SECONDS - this.elapsed),
      lives: this.lives,
      progress: { ...this.progress },
      profile: this.mem.profile(),
      last_seen: ls ? { zone: m.zoneOf(ls.x, ls.y), seconds_ago: Math.round((performance.now() - ls.t) / 1000) } : null,
      broker: { state: this.broker.state, zone: m.zoneOf(this.broker.tile.x, this.broker.tile.y) },
      recent_events: this.mem.events.slice(-10),
      doors: m.doors.map(d => ({ id: d.id, kind: d.kind, zone: d.zone, closed: d.closed })),
      closable_doors: this.closableDoors(),
      openable_passages: m.doors.filter(d => d.closed).map(d => d.id),
      lockers: m.lockers.map(l => ({ id: l.id, zone: l.zone, uses: this.mem.lockerUses[l.id] || 0 })),
      last_decision: this.mem.decisions.length ? this.mem.decisions[this.mem.decisions.length - 1] : null,
    };
  }

  async requestStrategy(trigger) {
    if (this.over || this.link.busy) return;
    this.lastStrategy = this.elapsed;
    const state = this.buildState();
    state.trigger = trigger;
    const res = await this.link.decide(state, 4500);
    let dec = res?.decision;
    if (!dec) dec = fallbackDecision(state);
    dec.latency_ms = res?.latency_ms ?? null;
    this.applyDecision(dec);
  }

  applyDecision(dec) {
    if (this.over) return;
    const m = this.maze;
    let ok = true, note = '';
    if (dec.action === 'BLOCK_CORRIDOR') {
      if (this.elapsed - this.lastStructural < STRUCTURAL_COOLDOWN) { ok = false; note = 'rejected: structural cooldown'; }
      else if (!this.closableDoors().includes(dec.target)) { ok = false; note = 'rejected: would trap the player / invalid door'; }
      else {
        const d = m.doors.find(q => q.id === dec.target);
        d.closed = true; this.brokerClosed.push(d.id); this.lastStructural = this.elapsed;
        if (this.brokerClosed.length > MAX_BROKER_CLOSED) { const old = m.doors.find(q => q.id === this.brokerClosed.shift()); if (old) old.closed = false; }
        this.drawDoors(); this.cameras.main.shake(150, 0.004);
        note = `sealed ${d.id} (${d.zone})`;
      }
    } else if (dec.action === 'OPEN_PASSAGE') {
      const d = m.doors.find(q => q.id === dec.target);
      if (!d || !d.closed) { ok = false; note = 'rejected: not a closed passage'; }
      else if (this.elapsed - this.lastStructural < STRUCTURAL_COOLDOWN) { ok = false; note = 'rejected: structural cooldown'; }
      else { d.closed = false; this.brokerClosed = this.brokerClosed.filter(i => i !== d.id); this.lastStructural = this.elapsed; this.drawDoors(); note = `opened ${d.id} (${d.zone})`; }
    } else if (dec.action === 'ACTIVATE_ALARM') {
      note = 'alarm on for 8 s';
    }
    if (ok) ok = this.broker.applyStrategy(dec, this) !== false;
    if (!ok && !note) note = 'rejected';
    if (!ok) {
      // safe alternative
      const alt = { action: 'INVESTIGATE_ZONE', target: this.mem.profile().preferred_zone || 'centre', reason: 'safe alternative after rejected action' };
      this.broker.applyStrategy(alt, this);
    }
    const record = {
      t: Math.round(this.elapsed), action: dec.action, target: dec.target ?? null, reason: dec.reason, prediction: dec.prediction,
      confidence: dec.confidence, dialogue: dec.dialogue, source: dec.source || 'claude', valid: ok, note, latency_ms: dec.latency_ms, outcome: 'pending',
    };
    this.mem.decisions.push(record);
    this.mem.log.push({ event: 'broker_decision', ...record });
    this.pendingOutcome = { record, at: this.elapsed, playerZone: m.zoneOf(this.player.tile.x, this.player.tile.y), sightings: this.mem.sightings, captures: this.mem.captures.length, door: dec.target };
    if (dec.dialogue && ok) this.say(dec.dialogue, 'strategist');
    this.panel.decision(record);
  }

  evaluateOutcome() {
    const po = this.pendingOutcome;
    if (!po || this.elapsed - po.at < 8) return;
    const r = po.record;
    let out;
    if (!r.valid) out = 'not executed';
    else if (this.mem.captures.length > po.captures) out = 'SUCCESS: player captured';
    else if (this.mem.sightings > po.sightings) out = 'partial: player spotted';
    else if (r.action === 'BLOCK_CORRIDOR') out = (this.mem.doorPasses[po.door] ? 'player rerouted around the sealed door' : 'player did not come this way, prediction weak');
    else out = 'no contact, prediction may be wrong';
    r.outcome = out;
    this.pendingOutcome = null;
    this.panel.outcome(r);
  }

  // ---------- player actions ----------
  interact() {
    if (this.over) return;
    const p = this.player, m = this.maze, pt = p.tile;
    if (p.hidden) { p.hidden = false; p.locker = null; this.emitEvent({ event: 'player_left_hiding' }); return; }
    const locker = m.lockers.find(l => Math.abs(l.x - pt.x) + Math.abs(l.y - pt.y) <= 1);
    if (locker) {
      p.hidden = true; p.locker = locker; p.x = (locker.x + 0.5) * TILE; p.y = (locker.y + 0.5) * TILE;
      p.hidSeenBy = this.broker.canSee({ ...p, hidden: false, tile: { x: locker.x, y: locker.y } }, this.progress.power) ? 'broker' : null;
      this.mem.hides++; this.mem.lockerUses[locker.id] = (this.mem.lockerUses[locker.id] || 0) + 1;
      this.emitEvent({ event: 'player_hiding', hiding_place: locker.id, zone: locker.zone, seen_entering: !!p.hidSeenBy });
      return;
    }
    if (Math.abs(m.generatorPos.x - pt.x) + Math.abs(m.generatorPos.y - pt.y) <= 1) {
      if (this.progress.power) return;
      if (!this.progress.fuse) { this.toast('The generator needs a fuse.'); return; }
      this.progress.power = true;
      this.emitEvent({ event: 'power_restored', zone: m.zoneOf(pt.x, pt.y) });
      this.broker.hear({ x: pt.x, y: pt.y, radius: 60, kind: 'generator' });
      this.say('Power restored. And now I know exactly where you are.', 'reflex');
      this.updateHud(); return;
    }
    if (Math.abs(m.exitPos.x - pt.x) + Math.abs(m.exitPos.y - pt.y) <= 1) {
      if (this.progress.keycard && this.progress.power) this.endGame(true);
      else this.toast('Exit locked: needs power + keycard.');
    }
  }

  throwBottle() {
    if (this.over || this.player.hidden) return;
    if (this.bottles <= 0) { this.toast('No bottles left.'); return; }
    this.bottles--;
    const pt = this.player.tile, f = this.player.facing;
    let x = pt.x, y = pt.y;
    for (let i = 0; i < 7; i++) { const nx = x + f.x, ny = y + f.y; if (!this.maze.walkable(nx, ny)) break; x = nx; y = ny; }
    const splash = this.add.circle((x + 0.5) * TILE, (y + 0.5) * TILE, 4, 0xffe08a).setDepth(6);
    this.tweens.add({ targets: splash, radius: TILE * 3, alpha: 0, duration: 700, onComplete: () => splash.destroy() });
    this.mem.throws++;
    this.emitEvent({ event: 'object_thrown', landing_zone: this.maze.zoneOf(x, y), thrown_from_zone: this.maze.zoneOf(pt.x, pt.y) });
    this.broker.hear({ x, y, radius: 12, kind: 'throw', fromX: pt.x, fromY: pt.y });
    this.updateHud();
  }

  micNoise() {
    // placeholder for the real-microphone feed (P2): M simulates a shout at the player's position
    const pt = this.player.tile;
    this.emitEvent({ event: 'noise', source: 'microphone', zone: this.maze.zoneOf(pt.x, pt.y) });
    this.broker.hear({ x: pt.x, y: pt.y, radius: 14, kind: 'mic' });
  }

  capture(how) {
    if (this.over) return;
    const pt = this.player.tile, m = this.maze;
    this.lives--;
    this.mem.captures.push({ x: pt.x, y: pt.y, zone: m.zoneOf(pt.x, pt.y), how, t: Math.round(this.elapsed) });
    this.emitEvent({ event: 'player_captured', zone: m.zoneOf(pt.x, pt.y), how, lives_left: this.lives });
    this.cameras.main.flash(300, 160, 0, 0);
    if (this.lives <= 0) { this.endGame(false); return; }
    this.say(pick(['Caught you. I will remember how.', 'Again. You are learning slower than me.']), 'strategist');
    // respawn in a room far from the BROKER that can still reach every objective
    const b = this.broker.tile, targets = this.objectiveTargets();
    const rooms = Object.values(m.rooms).filter(rm => {
      const reach = m.reachable(rm.cx, rm.cy);
      return targets.every(t => reach.has(m.key(t.x, t.y)));
    }).sort((a, c) => (Math.abs(c.cx - b.x) + Math.abs(c.cy - b.y)) - (Math.abs(a.cx - b.x) + Math.abs(a.cy - b.y)));
    const rm = rooms[0] || m.rooms.start;
    Object.assign(this.player, { x: (rm.cx + 0.5) * TILE, y: (rm.cy + 0.5) * TILE, hidden: false, locker: null });
    this.broker.setState('PATROL', 'reset after capture'); this.broker.path = [];
    this.updateHud();
  }

  endGame(won) {
    this.over = true;
    this.emitEvent({ event: won ? 'player_escaped' : 'game_over' });
    this.say(won ? 'You escaped. This time.' : 'Game over. You were very predictable.', 'strategist');
    setTimeout(() => showPostMortem(won, this.mem, this.elapsed, () => location.reload()), 1200);
  }

  toast(msg) { document.getElementById('subtitle').textContent = msg; }

  // ---------- drawing ----------
  drawDoors() {
    const g = this.doorG; g.clear();
    for (const d of this.maze.doors) {
      const x = d.x * TILE, y = d.y * TILE;
      if (d.closed) { g.fillStyle(d.kind === 'shutter' ? 0x4a3b1a : 0x8a1c1c); g.fillRect(x + 2, y + 2, TILE - 4, TILE - 4); }
      else { g.lineStyle(2, d.kind === 'shutter' ? 0xb08a3a : 0x3a6b8a); g.strokeRect(x + 4, y + 4, TILE - 8, TILE - 8); }
    }
  }

  drawItems() {
    const g = this.itemG; g.clear();
    const m = this.maze;
    for (const l of m.lockers) { g.fillStyle(0x3d5a3d); g.fillRect(l.x * TILE + 6, l.y * TILE + 3, TILE - 12, TILE - 6); }
    for (const it of m.items) {
      const cx = (it.x + 0.5) * TILE, cy = (it.y + 0.5) * TILE;
      const col = { fuse: 0xffd24a, keycard: 0x4affd2, bottle: 0x9a9a9a }[it.type];
      g.fillStyle(col); g.fillCircle(cx, cy, it.type === 'bottle' ? 4 : 7);
    }
    g.fillStyle(this.progress.power ? 0x4aff6a : 0xff8a2a); g.fillRect(m.generatorPos.x * TILE + 4, m.generatorPos.y * TILE + 4, TILE - 8, TILE - 8);
    g.fillStyle(0xffffff); g.fillRect(m.exitPos.x * TILE + 2, m.exitPos.y * TILE + 2, TILE - 4, TILE - 4);
  }

  updateHud() {
    document.getElementById('hud-lives').textContent = '♥'.repeat(Math.max(0, this.lives)) + '♡'.repeat(Math.max(0, 3 - this.lives));
    const p = this.progress;
    document.getElementById('hud-items').textContent = `fuse ${p.fuse ? '✓' : '✗'} · power ${p.power ? '✓' : '✗'} · keycard ${p.keycard ? '✓' : '✗'} · bottles ${this.bottles}`;
  }

  refreshPanel() {
    const fp = this.fastSamples.length ? this.fastSamples.reduce((a, b) => a + b, 0) / this.fastSamples.length : 0;
    this.panel.refresh({
      state: this.broker.state + ' · ' + this.broker.stateReason,
      strategy: this.broker.strategy,
      events: this.mem.events.slice(-7),
      profile: this.mem.profile(),
      fastMs: fp,
      link: this.link,
    });
  }

  // ---------- main loop ----------
  update(time, deltaMs) {
    if (this.over) return;
    const t0 = performance.now();
    const dt = Math.min(0.05, deltaMs / 1000);
    this.elapsed += dt;
    const p = this.player, m = this.maze, k = this.keys;

    // movement
    let vx = 0, vy = 0;
    if (!p.hidden) {
      if (k.A.isDown || k.LEFT.isDown) vx -= 1;
      if (k.D.isDown || k.RIGHT.isDown) vx += 1;
      if (k.W.isDown || k.UP.isDown) vy -= 1;
      if (k.S.isDown || k.DOWN.isDown) vy += 1;
    }
    const moving = vx !== 0 || vy !== 0;
    p.running = moving && k.SHIFT.isDown;
    if (moving) {
      const len = Math.hypot(vx, vy); vx /= len; vy /= len;
      if (Math.abs(vx) > Math.abs(vy)) p.facing = { x: Math.sign(vx), y: 0 }; else p.facing = { x: 0, y: Math.sign(vy) };
      const sp = (p.running ? 5.2 : 3.3) * TILE * dt;
      const before = p.tile;
      this.tryMove(p.x + vx * sp, p.y);
      this.tryMove(p.x, p.y + vy * sp);
      const after = p.tile;
      if (after.x !== before.x || after.y !== before.y) this.onPlayerTile(after);
    }
    const pt = p.tile;
    this.mem.tick(dt, m.zoneOf(pt.x, pt.y), moving, p.running);

    // running is loud
    this.runNoiseTimer = (this.runNoiseTimer || 0) - dt;
    if (p.running && this.runNoiseTimer <= 0) {
      this.runNoiseTimer = 0.7;
      this.broker.hear({ x: pt.x, y: pt.y, radius: 6, kind: 'run' });
      if (Math.random() < 0.15) this.emitEvent({ event: 'player_running', zone: m.zoneOf(pt.x, pt.y) });
    }

    // BROKER fast path
    this.broker.update(dt, performance.now(), p, this.progress.power);
    if (!p.hidden && Math.hypot(this.broker.x - p.x, this.broker.y - p.y) < TILE * 0.6) this.capture('caught in ' + m.zoneOf(pt.x, pt.y));

    // slow path schedule
    if (this.elapsed - this.lastStrategy > STRATEGY_INTERVAL) this.requestStrategy('interval');
    this.evaluateOutcome();

    // timer
    const left = Math.max(0, GAME_SECONDS - this.elapsed);
    document.getElementById('hud-timer').textContent = `${Math.floor(left / 60)}:${String(Math.floor(left % 60)).padStart(2, '0')}`;
    if (left <= 0) this.endGame(false);

    // visuals
    this.playerG.setPosition(p.x, p.y).setAlpha(p.hidden ? 0.35 : 1);
    this.brokerG.setPosition(this.broker.x, this.broker.y);
    const alarm = performance.now() < this.broker.alarmUntil;
    this.alarmRect.setFillStyle(0xff0000, alarm ? 0.08 + 0.08 * Math.sin(time / 120) : 0);
    this.fogRT.clear();
    if (this.fog) {
      this.fogRT.fill(0x000000, 0.94);
      const r = (this.progress.power ? 5.5 : 3.4) * TILE * (1 + 0.03 * Math.sin(time / 90));
      this.lightImg.setDisplaySize(r * 2, r * 2);
      this.fogRT.erase(this.lightImg, p.x, p.y);
    }
    this.fastSamples.push(performance.now() - t0);
    if (this.fastSamples.length > 60) this.fastSamples.shift();
  }

  tryMove(nx, ny) {
    const r = TILE * 0.28, m = this.maze;
    const pts = [[nx - r, ny - r], [nx + r, ny - r], [nx - r, ny + r], [nx + r, ny + r]];
    if (pts.every(([x, y]) => m.walkable(Math.floor(x / TILE), Math.floor(y / TILE)))) { this.player.x = nx; this.player.y = ny; }
  }

  onPlayerTile(tile) {
    const m = this.maze;
    const door = m.doorAt.get(m.key(tile.x, tile.y));
    if (door) {
      this.mem.doorPasses[door.id] = (this.mem.doorPasses[door.id] || 0) + 1;
      this.emitEvent({ event: 'door_used', door: door.id, zone: door.zone, times: this.mem.doorPasses[door.id] });
    }
    // pick up items
    const idx = m.items.findIndex(i => i.x === tile.x && i.y === tile.y);
    if (idx >= 0) {
      const it = m.items[idx];
      if (it.type === 'keycard' && !this.progress.power) { this.toast('Security terminal has no power. Restore the generator first.'); return; }
      m.items.splice(idx, 1);
      if (it.type === 'bottle') this.bottles++;
      else { this.progress[it.type] = true; this.emitEvent({ event: 'item_collected', item: it.type, zone: m.zoneOf(tile.x, tile.y) }); }
      this.drawItems(); this.updateHud();
    }
  }
}

const m0 = new Maze(SEED);
const config = {
  type: Phaser.AUTO,
  parent: 'game',
  width: m0.w * TILE,
  height: m0.h * TILE,
  backgroundColor: '#000000',
  scene: [],
};

document.getElementById('start-btn').addEventListener('click', () => {
  document.getElementById('overlay').classList.remove('show');
  const game = new Phaser.Game(config);
  game.scene.add('vault', VaultScene, true);
});
