// THE BROKER - the fast path. Moves, sees, hears and reacts every frame with plain code.
// The slow path (Claude strategist) only sets the BROKER's strategy + maze changes; it never drives a frame.

import { TILE } from './maze.js';

export const ACTIONS = [
  'PATROL', 'INVESTIGATE_ZONE', 'SEARCH_HIDING_PLACES', 'INTERCEPT_ROUTE',
  'BLOCK_CORRIDOR', 'OPEN_PASSAGE', 'GUARD_OBJECTIVE', 'ACTIVATE_ALARM',
];

export class Broker {
  constructor(scene, maze, memory) {
    this.scene = scene; this.maze = maze; this.mem = memory;
    const p = maze.zoneCentre('centre');
    this.x = (p.x + 0.5) * TILE; this.y = (p.y + 0.5) * TILE;
    this.path = []; this.state = 'PATROL'; this.stateReason = 'starting patrol';
    this.strategy = { action: 'PATROL', target: null };
    this.searchQueue = [];
    this.investigation = null; // {x,y,kind,fromX,fromY,t}
    this.loseSightTimer = 0;
    this.alarmUntil = 0;
    this.repathTimer = 0;
    this.guardPoint = null;
    this.lastSeenPlayer = false;
    this.knowsHidingSpot = null;
  }

  get tile() { return { x: Math.floor(this.x / TILE), y: Math.floor(this.y / TILE) }; }

  speed(now) {
    const base = { PATROL: 2.4, INVESTIGATE: 3.0, SEARCH: 2.6, CHASE: 4.3, INTERCEPT: 3.6, GUARD: 2.0 }[this.state] || 2.5;
    return (now < this.alarmUntil ? base * 1.2 : base) * TILE;
  }

  goTo(tx, ty, ignoreDoors = false) {
    const t = this.tile;
    const p = this.maze.path(t.x, t.y, tx, ty, ignoreDoors) || this.maze.path(t.x, t.y, tx, ty, true);
    this.path = p ? p.slice(1) : [];
    return !!p;
  }

  setState(s, reason) {
    if (this.state !== s) this.scene.onBrokerState?.(s, reason);
    this.state = s; this.stateReason = reason;
  }

  // ---------- perception ----------
  canSee(player, powerOn) {
    if (player.hidden) return false;
    const t = this.tile, pt = player.tile;
    const range = (powerOn ? 7 : 5) + (player.running ? 1 : 0);
    const d = Math.hypot(pt.x - t.x, pt.y - t.y);
    return d <= range && this.maze.lineOfSight(t.x, t.y, pt.x, pt.y);
  }

  // noise from the world: kind = 'run' | 'throw' | 'generator' | 'door' | 'mic'
  hear(n) {
    if (this.state === 'CHASE') return;
    const t = this.tile;
    const d = Math.hypot(n.x - t.x, n.y - t.y);
    if (d > n.radius) return;
    if (n.kind === 'throw') {
      // DECEPTION LEARNING: once it has been fooled, it checks where the throw CAME FROM instead
      if (this.mem.noiseTrust >= 0.5) {
        this.investigation = { ...n, t: performance.now() };
        this.setState('INVESTIGATE', `heard a ${n.kind} (trust in noises ${Math.round(this.mem.noiseTrust * 100)}%)`);
        this.goTo(n.x, n.y);
        this.scene.say?.(pick(['What was that?', 'I hear you.', 'Something fell...']), 'reflex');
      } else {
        this.investigation = { x: n.fromX, y: n.fromY, kind: 'throw_origin', t: performance.now() };
        this.setState('INVESTIGATE', `ignores the landing spot, checks where it was thrown FROM (trust ${Math.round(this.mem.noiseTrust * 100)}%)`);
        this.goTo(n.fromX, n.fromY);
        this.scene.say?.(pick(['Another bottle? I know where you threw it from.', 'Not falling for that again.']), 'reflex');
      }
      return;
    }
    this.investigation = { ...n, t: performance.now() };
    this.setState('INVESTIGATE', `heard ${n.kind} noise`);
    this.goTo(n.x, n.y);
    if (n.kind !== 'run') this.scene.say?.(pick(['I heard that.', 'Coming.', 'There you are...']), 'reflex');
  }

  // ---------- strategy from the slow path (validated by the scene) ----------
  applyStrategy(dec, ctx) {
    this.strategy = dec;
    const m = this.maze;
    switch (dec.action) {
      case 'PATROL':
        this.setState('PATROL', dec.reason || 'patrolling');
        this.path = [];
        break;
      case 'INVESTIGATE_ZONE': {
        const c = m.zoneCentre(dec.target || 'centre');
        this.setState('INVESTIGATE', dec.reason || `checking ${dec.target}`);
        this.investigation = { x: c.x, y: c.y, kind: 'zone', t: performance.now() };
        this.goTo(c.x, c.y);
        break;
      }
      case 'SEARCH_HIDING_PLACES': {
        let lockers = m.lockers.slice();
        if (dec.target && dec.target.startsWith('locker')) lockers = lockers.filter(l => l.id === dec.target).concat(lockers.filter(l => l.id !== dec.target));
        lockers.sort((a, b) => (this.mem.lockerUses[b.id] || 0) - (this.mem.lockerUses[a.id] || 0));
        this.searchQueue = lockers.slice(0, 3);
        this.setState('SEARCH', dec.reason || 'searching hiding places');
        this.nextSearch();
        break;
      }
      case 'INTERCEPT_ROUTE':
      case 'GUARD_OBJECTIVE': {
        const pt = ctx.objectivePoint(dec.target);
        if (!pt) return false;
        this.guardPoint = pt;
        this.setState(dec.action === 'GUARD_OBJECTIVE' ? 'GUARD' : 'INTERCEPT', dec.reason || 'moving to cut you off');
        this.goTo(pt.x, pt.y, true);
        break;
      }
      case 'ACTIVATE_ALARM':
        this.alarmUntil = performance.now() + 8000;
        break;
      default: break; // BLOCK_CORRIDOR / OPEN_PASSAGE handled by the scene (maze changes)
    }
    return true;
  }

  nextSearch() {
    const l = this.searchQueue.shift();
    if (!l) { this.setState('PATROL', 'search finished'); this.path = []; return; }
    this.currentLocker = l;
    this.goTo(l.x, l.y, false);
  }

  // ---------- per-frame update ----------
  update(dt, now, player, powerOn) {
    const sees = this.canSee(player, powerOn);
    if (sees) {
      if (!this.lastSeenPlayer) {
        this.mem.sightings++;
        this.scene.emitEvent({ event: 'player_spotted', zone: this.maze.zoneOf(player.tile.x, player.tile.y), confidence: 1 });
        this.scene.say?.(pick(['Found you.', 'There you are.', 'Run.']), 'reflex');
      }
      this.mem.lastSeen = { x: player.tile.x, y: player.tile.y, t: now };
      this.loseSightTimer = 0;
      this.setState('CHASE', 'player in sight');
      this.repathTimer -= dt;
      if (this.repathTimer <= 0 || !this.path.length) { this.goTo(player.tile.x, player.tile.y); this.repathTimer = 0.25; }
    } else if (this.state === 'CHASE') {
      this.loseSightTimer += dt;
      if (player.hidden && player.hidSeenBy === 'broker') {
        // it watched you get in
        this.knowsHidingSpot = player.locker;
        this.goTo(player.locker.x, player.locker.y);
      } else if (this.loseSightTimer > 1.5) {
        const ls = this.mem.lastSeen;
        const near = this.maze.lockers.filter(l => ls && Math.abs(l.x - ls.x) + Math.abs(l.y - ls.y) < 10)
          .sort((a, b) => (this.mem.lockerUses[b.id] || 0) - (this.mem.lockerUses[a.id] || 0));
        this.searchQueue = near.slice(0, 2);
        this.setState('SEARCH', 'lost sight, searching nearby hiding places');
        if (ls) this.goTo(ls.x, ls.y); else this.nextSearch();
      }
    }
    this.lastSeenPlayer = sees;

    // move along the path
    if (this.path.length) {
      const n = this.path[0];
      const tx = (n.x + 0.5) * TILE, ty = (n.y + 0.5) * TILE;
      const dx = tx - this.x, dy = ty - this.y, d = Math.hypot(dx, dy);
      const step = this.speed(now) * dt;
      // closed door ahead? wait / repath
      const door = this.maze.doorAt.get(this.maze.key(n.x, n.y));
      if (door && door.closed) { this.path = []; }
      else if (d <= step) { this.x = tx; this.y = ty; this.path.shift(); }
      else { this.x += (dx / d) * step; this.y += (dy / d) * step; }
    } else {
      this.onArrive(now, player);
    }
  }

  onArrive(now, player) {
    const t = this.tile;
    if (this.state === 'INVESTIGATE') {
      const inv = this.investigation;
      const pt = player.tile;
      const near = Math.abs(pt.x - t.x) + Math.abs(pt.y - t.y) <= 4 && !player.hidden;
      if (inv && inv.kind === 'throw') {
        if (!near) { this.mem.onDistractionFound(); this.scene.emitEvent({ event: 'distraction_detected', zone: this.maze.zoneOf(t.x, t.y), noise_trust: this.mem.noiseTrust }); this.scene.say?.(pick(['A bottle. Clever.', 'You think that will work twice?']), 'reflex'); }
      } else if (inv && near) this.mem.onRealNoiseConfirmed();
      this.investigation = null;
      this.setState('SEARCH', 'nothing here, checking hiding places');
      const near2 = this.maze.lockers.filter(l => Math.abs(l.x - t.x) + Math.abs(l.y - t.y) < 9);
      this.searchQueue = near2.slice(0, 2);
      this.nextSearch();
      return;
    }
    if (this.state === 'SEARCH') {
      if (this.currentLocker && player.hidden && player.locker && player.locker.id === this.currentLocker.id) {
        this.scene.capture('found you in ' + player.locker.id);
        return;
      }
      this.currentLocker = null;
      this.nextSearch();
      return;
    }
    if (this.state === 'GUARD' || this.state === 'INTERCEPT') {
      // loiter around the guard point
      const g = this.guardPoint || t;
      const ox = g.x + Math.round((Math.random() - 0.5) * 6), oy = g.y + Math.round((Math.random() - 0.5) * 6);
      const f = this.maze.nearestFloor(ox, oy);
      this.goTo(f.x, f.y);
      return;
    }
    if (this.state === 'CHASE') return;
    // PATROL: wander between zone centres and rooms
    const targets = Object.values(this.maze.rooms).map(r => ({ x: r.cx, y: r.cy }))
      .concat(['north', 'south', 'east', 'west', 'centre'].map(z => this.maze.zoneCentre(z)));
    const g = targets[Math.floor(Math.random() * targets.length)];
    this.goTo(g.x, g.y);
  }
}

// Rule-based fallback strategist (used when the backend / Claude is offline or slow).
export function fallbackDecision(state) {
  const p = state.profile;
  const prog = state.progress;
  let d;
  if (prog.keycard && prog.power) d = { action: 'GUARD_OBJECTIVE', target: 'exit', reason: 'player has everything, guarding the exit', prediction: 'heading to the exit', confidence: 0.8, dialogue: 'You will not leave this place.' };
  else if (p.preferred_door && p.preferred_door_uses >= 3 && state.closable_doors.includes(p.preferred_door)) d = { action: 'BLOCK_CORRIDOR', target: p.preferred_door, reason: `player used ${p.preferred_door} ${p.preferred_door_uses} times`, prediction: `will try ${p.preferred_door} again`, confidence: 0.6, dialogue: 'Your favourite door is closed. Predictable.' };
  else if (p.hiding_count >= 2) d = { action: 'SEARCH_HIDING_PLACES', target: p.favourite_locker || null, reason: 'player hides often', prediction: 'hiding in a locker', confidence: 0.55, dialogue: 'I know you like the lockers.' };
  else if (p.running_frequency > 0.45 && p.preferred_zone) d = { action: 'INTERCEPT_ROUTE', target: prog.fuse ? 'generator' : 'fuse', reason: 'fast player heading straight for objectives', prediction: 'rushing the next objective', confidence: 0.5, dialogue: 'Running again? I can hear every step.' };
  else d = { action: 'INVESTIGATE_ZONE', target: p.preferred_zone || 'centre', reason: 'checking the player\'s favourite zone', prediction: `player in ${p.preferred_zone || 'centre'}`, confidence: p.preferred_zone_confidence || 0.3, dialogue: 'I am watching.' };
  return { ...d, source: 'fallback-rules' };
}

export function pick(a) { return a[Math.floor(Math.random() * a.length)]; }
