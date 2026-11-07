// Maze generation, doors, zones, BFS reachability and A* pathfinding.
// Tile grid: 0 = floor, 1 = wall. Doors live on floor tiles and block when closed.

export const TILE = 28;

// Deterministic RNG so the same seed gives the same maze ("two players, one maze").
export function mulberry32(seed) {
  let a = seed >>> 0;
  return function () {
    a |= 0; a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const ZONE_NAMES = [
  ['north-west', 'north', 'north-east'],
  ['west', 'centre', 'east'],
  ['south-west', 'south', 'south-east'],
];

export class Maze {
  constructor(seed = 1337, cellsW = 13, cellsH = 9) {
    this.seed = seed;
    this.rng = mulberry32(seed);
    this.cw = cellsW; this.ch = cellsH;
    this.w = cellsW * 2 + 1; this.h = cellsH * 2 + 1;
    this.grid = [];
    for (let y = 0; y < this.h; y++) this.grid.push(new Array(this.w).fill(1));
    this.doors = [];      // {id, x, y, closed, kind:'door'|'shutter', zone}
    this.doorAt = new Map();
    this.lockers = [];    // {id, x, y, zone}
    this.rooms = {};      // name -> {x,y,w,h, cx, cy}
    this.items = [];      // {type, x, y}
    this.generate();
  }

  key(x, y) { return y * this.w + x; }
  inBounds(x, y) { return x >= 0 && y >= 0 && x < this.w && y < this.h; }

  zoneOf(x, y) {
    const zx = Math.min(2, Math.floor((x / this.w) * 3));
    const zy = Math.min(2, Math.floor((y / this.h) * 3));
    return ZONE_NAMES[zy][zx];
  }

  zoneCentre(name) {
    for (let zy = 0; zy < 3; zy++) for (let zx = 0; zx < 3; zx++) {
      if (ZONE_NAMES[zy][zx] === name) {
        const cx = Math.floor(((zx + 0.5) / 3) * this.w);
        const cy = Math.floor(((zy + 0.5) / 3) * this.h);
        return this.nearestFloor(cx, cy);
      }
    }
    return this.nearestFloor(Math.floor(this.w / 2), Math.floor(this.h / 2));
  }

  nearestFloor(x, y) {
    let best = null, bd = 1e9;
    for (let yy = 0; yy < this.h; yy++) for (let xx = 0; xx < this.w; xx++) {
      if (this.grid[yy][xx] === 0) {
        const d = Math.abs(xx - x) + Math.abs(yy - y);
        if (d < bd) { bd = d; best = { x: xx, y: yy }; }
      }
    }
    return best;
  }

  generate() {
    const r = this.rng;
    // 1) DFS maze on the cell grid
    const visited = new Set();
    const stack = [[0, 0]];
    visited.add('0,0');
    this.grid[1][1] = 0;
    while (stack.length) {
      const [cx, cy] = stack[stack.length - 1];
      const nbrs = [[1, 0], [-1, 0], [0, 1], [0, -1]]
        .map(([dx, dy]) => [cx + dx, cy + dy, dx, dy])
        .filter(([nx, ny]) => nx >= 0 && ny >= 0 && nx < this.cw && ny < this.ch && !visited.has(nx + ',' + ny));
      if (!nbrs.length) { stack.pop(); continue; }
      const [nx, ny, dx, dy] = nbrs[Math.floor(r() * nbrs.length)];
      this.grid[cy * 2 + 1 + dy][cx * 2 + 1 + dx] = 0;
      this.grid[ny * 2 + 1][nx * 2 + 1] = 0;
      visited.add(nx + ',' + ny);
      stack.push([nx, ny]);
    }
    // 2) Braid: knock out extra walls so there are loops (the BROKER can redirect without trapping)
    for (let y = 1; y < this.h - 1; y++) for (let x = 1; x < this.w - 1; x++) {
      if (this.grid[y][x] !== 1) continue;
      const horiz = x % 2 === 0 && y % 2 === 1; // wall between two cells left/right
      const vert = x % 2 === 1 && y % 2 === 0;
      if ((horiz || vert) && r() < 0.16) this.grid[y][x] = 0;
    }
    // 3) Rooms (fixed layout, aligned to cells)
    const room = (name, cx, cy, cw, chh) => {
      const x = cx * 2 + 1, y = cy * 2 + 1, w = cw * 2 - 1, h = chh * 2 - 1;
      for (let yy = y; yy < y + h; yy++) for (let xx = x; xx < x + w; xx++) this.grid[yy][xx] = 0;
      this.rooms[name] = { x, y, w, h, cx: x + Math.floor(w / 2), cy: y + Math.floor(h / 2) };
    };
    room('start', 0, 0, 2, 2);
    room('storage', this.cw - 3, 0, 3, 2);
    room('generator', 0, this.ch - 2, 3, 2);
    room('security', Math.floor(this.cw / 2) - 1, Math.floor(this.ch / 2) - 1, 2, 2);
    room('exit', this.cw - 2, this.ch - 2, 2, 2);

    // 4) Candidate door tiles = floor tiles that sit between two cells (passages)
    const candidates = [];
    for (let y = 1; y < this.h - 1; y++) for (let x = 1; x < this.w - 1; x++) {
      if (this.grid[y][x] !== 0) continue;
      if (this.inRoom(x, y)) continue;
      const horiz = x % 2 === 0 && y % 2 === 1;
      const vert = x % 2 === 1 && y % 2 === 0;
      if (horiz || vert) candidates.push({ x, y });
    }
    // spread doors out: pick, then skip anything within 3 tiles
    shuffle(candidates, r);
    const picked = [];
    for (const c of candidates) {
      if (picked.length >= 16) break;
      if (picked.some(p => Math.abs(p.x - c.x) + Math.abs(p.y - c.y) < 5)) continue;
      picked.push(c);
    }
    picked.forEach((p, i) => this.addDoor(p.x, p.y, 'door', false));

    // 5) Shutters = walls between two cells that the BROKER can OPEN as alternative passages
    const shutterCands = [];
    for (let y = 1; y < this.h - 1; y++) for (let x = 1; x < this.w - 1; x++) {
      if (this.grid[y][x] !== 1) continue;
      const horiz = x % 2 === 0 && y % 2 === 1;
      const vert = x % 2 === 1 && y % 2 === 0;
      if (horiz || vert) shutterCands.push({ x, y });
    }
    shuffle(shutterCands, r);
    let ns = 0;
    for (const s of shutterCands) {
      if (ns >= 8) break;
      if (this.doors.some(d => Math.abs(d.x - s.x) + Math.abs(d.y - s.y) < 4)) continue;
      this.grid[s.y][s.x] = 0;
      this.addDoor(s.x, s.y, 'shutter', true);
      ns++;
    }

    // 6) Lockers (hiding places) along room edges + some corridors
    const lockerSpots = [];
    for (const [name, rm] of Object.entries(this.rooms)) {
      if (name === 'start' || name === 'exit') continue;
      lockerSpots.push({ x: rm.x, y: rm.y }, { x: rm.x + rm.w - 1, y: rm.y + rm.h - 1 });
    }
    for (let i = 0; i < 4; i++) {
      const c = candidates[(i * 7 + 3) % candidates.length];
      if (c && !this.doorAt.has(this.key(c.x, c.y))) {
        // put the locker on a cell tile next to the passage
        const cell = this.grid[c.y][c.x - 1] === 0 && (c.x - 1) % 2 === 1 ? { x: c.x - 1, y: c.y } : { x: c.x, y: c.y - 1 };
        if (this.grid[cell.y]?.[cell.x] === 0) lockerSpots.push(cell);
      }
    }
    lockerSpots.forEach((p, i) => this.lockers.push({ id: 'locker_' + (i + 1), x: p.x, y: p.y, zone: this.zoneOf(p.x, p.y) }));

    // 7) Items + objectives
    const rm = this.rooms;
    this.start = { x: rm.start.cx, y: rm.start.cy };
    this.items.push({ type: 'fuse', x: rm.storage.cx, y: rm.storage.cy });
    this.items.push({ type: 'keycard', x: rm.security.cx, y: rm.security.cy });
    this.generatorPos = { x: rm.generator.cx, y: rm.generator.cy };
    this.exitPos = { x: rm.exit.cx, y: rm.exit.cy };
    // throwable bottles scattered
    const floor = [];
    for (let y = 1; y < this.h - 1; y++) for (let x = 1; x < this.w - 1; x++)
      if (this.grid[y][x] === 0 && !this.doorAt.has(this.key(x, y)) && x % 2 === 1 && y % 2 === 1) floor.push({ x, y });
    shuffle(floor, r);
    floor.slice(0, 6).forEach(p => this.items.push({ type: 'bottle', x: p.x, y: p.y }));
  }

  inRoom(x, y) {
    return Object.values(this.rooms).some(rm => x >= rm.x - 1 && x <= rm.x + rm.w && y >= rm.y - 1 && y <= rm.y + rm.h);
  }

  roomAt(x, y) {
    for (const [n, rm] of Object.entries(this.rooms))
      if (x >= rm.x && x < rm.x + rm.w && y >= rm.y && y < rm.y + rm.h) return n;
    return null;
  }

  addDoor(x, y, kind, closed) {
    const d = { id: (kind === 'door' ? 'door_' : 'shutter_') + (this.doors.filter(q => q.kind === kind).length + 1), x, y, kind, closed, zone: this.zoneOf(x, y) };
    this.doors.push(d);
    this.doorAt.set(this.key(x, y), d);
  }

  walkable(x, y, extraClosed = null) {
    if (!this.inBounds(x, y) || this.grid[y][x] !== 0) return false;
    const d = this.doorAt.get(this.key(x, y));
    if (d) {
      if (extraClosed && extraClosed.has(d.id)) return false;
      if (d.closed) return false;
    }
    return true;
  }

  // BFS from (sx,sy): returns Set of reachable keys
  reachable(sx, sy, extraClosed = null) {
    const seen = new Set([this.key(sx, sy)]);
    const q = [[sx, sy]];
    while (q.length) {
      const [x, y] = q.shift();
      for (const [dx, dy] of DIRS) {
        const nx = x + dx, ny = y + dy, k = this.key(nx, ny);
        if (!seen.has(k) && this.walkable(nx, ny, extraClosed)) { seen.add(k); q.push([nx, ny]); }
      }
    }
    return seen;
  }

  // FAIRNESS RULE: would closing this door still let the player reach every remaining objective?
  canClose(doorId, playerTile, targets) {
    const d = this.doors.find(q => q.id === doorId);
    if (!d || d.closed) return false;
    if (d.x === playerTile.x && d.y === playerTile.y) return false;
    const extra = new Set([doorId]);
    const reach = this.reachable(playerTile.x, playerTile.y, extra);
    return targets.every(t => reach.has(this.key(t.x, t.y)));
  }

  // A* on the tile grid. ignoreDoors=true lets the BROKER's patrol plan "through" doors it controls.
  path(sx, sy, tx, ty, ignoreDoors = false) {
    if (!this.inBounds(tx, ty)) return null;
    const start = this.key(sx, sy), goal = this.key(tx, ty);
    const open = new MinHeap();
    const g = new Map([[start, 0]]);
    const came = new Map();
    open.push(start, 0);
    const h = (x, y) => Math.abs(x - tx) + Math.abs(y - ty);
    let iter = 0;
    while (open.size() && iter++ < 5000) {
      const cur = open.pop();
      if (cur === goal) {
        const out = [];
        let c = cur;
        while (c !== undefined) { out.push({ x: c % this.w, y: Math.floor(c / this.w) }); c = came.get(c); }
        return out.reverse();
      }
      const cx = cur % this.w, cy = Math.floor(cur / this.w);
      for (const [dx, dy] of DIRS) {
        const nx = cx + dx, ny = cy + dy;
        const ok = ignoreDoors ? (this.inBounds(nx, ny) && this.grid[ny][nx] === 0) : this.walkable(nx, ny);
        if (!ok) continue;
        const nk = this.key(nx, ny);
        const ng = g.get(cur) + 1;
        if (ng < (g.get(nk) ?? 1e9)) {
          g.set(nk, ng); came.set(nk, cur);
          open.push(nk, ng + h(nx, ny));
        }
      }
    }
    return null;
  }

  // Line of sight through floor tiles (closed doors block sight)
  lineOfSight(x0, y0, x1, y1) {
    let dx = Math.abs(x1 - x0), dy = -Math.abs(y1 - y0);
    let sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
    let x = x0, y = y0;
    while (true) {
      if (!(x === x0 && y === y0) && !(x === x1 && y === y1)) {
        if (this.grid[y][x] !== 0) return false;
        const d = this.doorAt.get(this.key(x, y));
        if (d && d.closed) return false;
      }
      if (x === x1 && y === y1) return true;
      const e2 = 2 * err;
      if (e2 >= dy) { err += dy; x += sx; }
      if (e2 <= dx) { err += dx; y += sy; }
    }
  }
}

export const DIRS = [[1, 0], [-1, 0], [0, 1], [0, -1]];

function shuffle(a, r) {
  for (let i = a.length - 1; i > 0; i--) { const j = Math.floor(r() * (i + 1)); [a[i], a[j]] = [a[j], a[i]]; }
  return a;
}

class MinHeap {
  constructor() { this.a = []; }
  size() { return this.a.length; }
  push(v, p) {
    const a = this.a; a.push([p, v]);
    let i = a.length - 1;
    while (i > 0) { const pi = (i - 1) >> 1; if (a[pi][0] <= a[i][0]) break; [a[pi], a[i]] = [a[i], a[pi]]; i = pi; }
  }
  pop() {
    const a = this.a; const top = a[0]; const last = a.pop();
    if (a.length) {
      a[0] = last; let i = 0;
      while (true) {
        const l = 2 * i + 1, r = l + 1; let m = i;
        if (l < a.length && a[l][0] < a[m][0]) m = l;
        if (r < a.length && a[r][0] < a[m][0]) m = r;
        if (m === i) break; [a[m], a[i]] = [a[i], a[m]]; i = m;
      }
    }
    return top[1];
  }
}
