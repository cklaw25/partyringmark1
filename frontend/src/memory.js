// THE HOST's memory: short-term events + long-term behavioural profile.
// All plain code (fast path). It persists across captures within a game.

export class BrokerMemory {
  constructor() {
    this.events = [];          // short-term: last ~40 events
    this.log = [];             // full timeline for the post-mortem
    this.zoneVisits = {};      // zone -> seconds spent
    this.doorPasses = {};      // door id -> times the player walked through
    this.lockerUses = {};      // locker id -> times hidden
    this.moveTime = 0;
    this.runTime = 0;
    this.hides = 0;
    this.throws = 0;
    this.distractionsCaught = 0;  // times THE HOST investigated a throw and found nobody
    this.distractionsWorked = 0;  // times a throw pulled THE HOST away and the player was not caught
    this.noiseTrust = 1.0;        // how much THE HOST believes noises = the player
    this.captures = [];           // {x,y,zone,how,t}
    this.lastSeen = null;         // {x,y,t}
    this.sightings = 0;
    this.decisions = [];          // {t, action, target, reason, confidence, prediction, outcome}
  }

  push(ev) {
    this.events.push(ev);
    if (this.events.length > 40) this.events.shift();
    this.log.push(ev);
  }

  tick(dt, zone, moving, running) {
    this.zoneVisits[zone] = (this.zoneVisits[zone] || 0) + dt;
    if (moving) { this.moveTime += dt; if (running) this.runTime += dt; }
  }

  top(obj) {
    let best = null, bv = 0, total = 0;
    for (const [k, v] of Object.entries(obj)) { total += v; if (v > bv) { bv = v; best = k; } }
    return { key: best, share: total ? bv / total : 0, count: bv };
  }

  profile() {
    const runRatio = this.moveTime > 1 ? this.runTime / this.moveTime : 0;
    const z = this.top(this.zoneVisits);
    const d = this.top(this.doorPasses);
    const l = this.top(this.lockerUses);
    return {
      running_frequency: round(runRatio),
      hiding_count: this.hides,
      preferred_zone: z.key, preferred_zone_confidence: round(z.share),
      preferred_door: d.key, preferred_door_uses: d.count,
      favourite_locker: l.key, favourite_locker_uses: l.count,
      distractions_thrown: this.throws,
      distractions_detected: this.distractionsCaught,
      noise_trust: round(this.noiseTrust),
      capture_count: this.captures.length,
      sightings: this.sightings,
      playstyle: runRatio > 0.45 ? 'aggressive' : (this.hides >= 2 || this.throws >= 2 ? 'stealth' : 'cautious'),
    };
  }

  // the deception mechanic: every throw THE HOST falls for lowers its trust in noises
  onDistractionFound() {
    this.distractionsCaught++;
    this.noiseTrust = Math.max(0.2, this.noiseTrust - 0.2);
  }
  onRealNoiseConfirmed() {
    this.noiseTrust = Math.min(1, this.noiseTrust + 0.1);
  }
}

function round(v) { return Math.round(v * 100) / 100; }
