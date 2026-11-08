// Maze generation, doors, zones, BFS reachability and A* pathfinding (port of the web version).
// Grid: 0 = floor, 1 = wall. Doors sit on floor tiles and block when closed. Same seed -> same maze.
using System;
using System.Collections.Generic;

public class Door { public string id; public int x, y; public bool closed; public string kind; public string zone; public string room; }
// kinds: "door" (BROKER can seal), "shutter" (BROKER can open), "gate" (escape-room lockdown, engine only), "blast" (central exit, engine only)
public class Locker { public string id; public int x, y; public string zone; }
public class Item { public string type; public int x, y; }
public class Room { public string name; public int x, y, w, h, cx, cy; }
public struct Tile { public int x, y; public Tile(int x, int y) { this.x = x; this.y = y; } }

public class Mulberry32
{
    uint a;
    public Mulberry32(uint seed) { a = seed; }
    public double Next()
    {
        unchecked
        {
            a += 0x6D2B79F5;
            uint t = a;
            t = (t ^ (t >> 15)) * (1 | t);
            t ^= t + (t ^ (t >> 7)) * (61 | t);
            return ((t ^ (t >> 14))) / 4294967296.0;
        }
    }
}

public class VaultMaze
{
    public readonly int cw, ch, w, h;
    public int[,] grid; // [y,x]
    public List<Door> doors = new List<Door>();
    public Dictionary<int, Door> doorAt = new Dictionary<int, Door>();
    public List<Locker> lockers = new List<Locker>();
    public Dictionary<string, Room> rooms = new Dictionary<string, Room>();
    public List<Item> items = new List<Item>();
    public Tile start, exitPos;
    public List<Tile> cameraSpots = new List<Tile>();
    public static readonly string[] PUZZLE_ROOMS = { "power", "surveillance", "emergency" };
    Mulberry32 rng;

    static readonly string[,] ZONES = {
        { "north-west", "north", "north-east" },
        { "west", "centre", "east" },
        { "south-west", "south", "south-east" } };
    public static readonly string[] ZONE_LIST = { "north-west", "north", "north-east", "west", "centre", "east", "south-west", "south", "south-east" };
    public static readonly int[,] DIRS = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };

    public VaultMaze(uint seed = 1337, int cellsW = 17, int cellsH = 11)
    {
        rng = new Mulberry32(seed);
        cw = cellsW; ch = cellsH; w = cw * 2 + 1; h = ch * 2 + 1;
        grid = new int[h, w];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) grid[y, x] = 1;
        Generate();
    }

    public int Key(int x, int y) => y * w + x;
    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;

    public string ZoneOf(int x, int y)
    {
        int zx = Math.Min(2, (int)Math.Floor((double)x / w * 3));
        int zy = Math.Min(2, (int)Math.Floor((double)y / h * 3));
        return ZONES[Math.Max(0, zy), Math.Max(0, zx)];
    }

    public Tile ZoneCentre(string name)
    {
        for (int zy = 0; zy < 3; zy++) for (int zx = 0; zx < 3; zx++)
            if (ZONES[zy, zx] == name)
                return NearestFloor((int)((zx + 0.5) / 3 * w), (int)((zy + 0.5) / 3 * h));
        return NearestFloor(w / 2, h / 2);
    }

    public Tile NearestFloor(int x, int y)
    {
        Tile best = start; int bd = int.MaxValue;
        for (int yy = 0; yy < h; yy++) for (int xx = 0; xx < w; xx++)
            if (grid[yy, xx] == 0) { int d = Math.Abs(xx - x) + Math.Abs(yy - y); if (d < bd) { bd = d; best = new Tile(xx, yy); } }
        return best;
    }

    void Shuffle<T>(List<T> a) { for (int i = a.Count - 1; i > 0; i--) { int j = (int)Math.Floor(rng.Next() * (i + 1)); var t = a[i]; a[i] = a[j]; a[j] = t; } }

    // ---- generation tuning (backrooms: loopy, open halls with pillars, things spread out) ----
    public const double BRAID = 0.4, PILLAR_REMOVE = 0.5;
    public const int HALLS = 5, DOORS = 24, SHUTTERS = 12, BOTTLES = 9, CAMERA_SPOTS = 16;
    public List<Room> halls = new List<Room>(); // open areas, in tile coordinates

    static bool IsEdge(int x, int y) => (x % 2 == 0 && y % 2 == 1) || (x % 2 == 1 && y % 2 == 0); // wall between two cells
    static int Manhattan(Tile a, Tile b) => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);

    void Generate()
    {
        // 1) DFS maze
        var visited = new HashSet<int>(); var stack = new List<(int, int)> { (0, 0) };
        visited.Add(0); grid[1, 1] = 0;
        while (stack.Count > 0)
        {
            var (cx, cy) = stack[stack.Count - 1];
            var nbrs = new List<(int, int, int, int)>();
            for (int d = 0; d < 4; d++)
            {
                int nx = cx + DIRS[d, 0], ny = cy + DIRS[d, 1];
                if (nx >= 0 && ny >= 0 && nx < cw && ny < ch && !visited.Contains(ny * cw + nx)) nbrs.Add((nx, ny, DIRS[d, 0], DIRS[d, 1]));
            }
            if (nbrs.Count == 0) { stack.RemoveAt(stack.Count - 1); continue; }
            var (bx, by, dx, dy) = nbrs[(int)Math.Floor(rng.Next() * nbrs.Count)];
            grid[cy * 2 + 1 + dy, cx * 2 + 1 + dx] = 0;
            grid[by * 2 + 1, bx * 2 + 1] = 0;
            visited.Add(by * cw + bx); stack.Add((bx, by));
        }
        // 2) heavy braid -> lots of loops and alternate routes
        for (int y = 1; y < h - 1; y++) for (int x = 1; x < w - 1; x++)
            if (grid[y, x] == 1 && IsEdge(x, y) && rng.Next() < BRAID) grid[y, x] = 0;
        // 3) rooms
        AddRoom("containment", 0, 0, 2, 2);
        AddRoom("power", cw - 3, 0, 3, 2);
        AddRoom("surveillance", 0, ch - 2, 3, 2);
        AddRoom("central", cw / 2 - 1, ch / 2 - 1, 2, 2);
        AddRoom("emergency", cw - 2, ch - 2, 2, 2);
        // 3a) the heavy braid opens rooms on many sides: trim each room back to MAX_ROOM_OPENINGS (never disconnecting anything)
        foreach (var rm in rooms.Values) TrimOpenings(rm);
        // 3b) open halls: clear every wall between cells in a block; the cell corners stay as pillars (some removed)
        for (int attempt = 0; attempt < 200 && halls.Count < HALLS; attempt++)
        {
            int hw = 3 + (int)(rng.Next() * 3), hh = 3 + (int)(rng.Next() * 2);
            int hx = (int)(rng.Next() * (cw - hw + 1)), hy = (int)(rng.Next() * (ch - hh + 1));
            var hr = new Room { name = "hall_" + (halls.Count + 1), x = hx * 2 + 1, y = hy * 2 + 1, w = hw * 2 - 1, h = hh * 2 - 1 };
            hr.cx = hr.x + hr.w / 2; hr.cy = hr.y + hr.h / 2;
            bool clash = false;
            foreach (var rm in rooms.Values) if (Overlaps(hr, rm, 3)) clash = true;   // keep a corridor ring around rooms
            foreach (var o in halls) if (Overlaps(hr, o, 2)) clash = true;            // halls don't merge into one blob
            if (clash) continue;
            for (int yy = hr.y; yy < hr.y + hr.h; yy++) for (int xx = hr.x; xx < hr.x + hr.w; xx++)
            {
                if (grid[yy, xx] == 0) continue;
                if (IsEdge(xx, yy) || rng.Next() < PILLAR_REMOVE) grid[yy, xx] = 0;
            }
            halls.Add(hr);
        }
        // 4) doors on corridor passages (not in halls: you'd just walk around them)
        var cands = new List<Tile>();
        for (int y = 1; y < h - 1; y++) for (int x = 1; x < w - 1; x++)
            if (grid[y, x] == 0 && IsEdge(x, y) && !InRoomPadded(x, y) && HallAt(x, y, 1) == null) cands.Add(new Tile(x, y));
        Shuffle(cands);
        var picked = new List<Tile>();
        foreach (var c in cands)
        {
            if (picked.Count >= DOORS) break;
            if (picked.Exists(p => Manhattan(p, c) < 6)) continue;
            picked.Add(c);
        }
        foreach (var p in picked) AddDoor(p.x, p.y, "door", false);
        // 5) shutters: walls the BROKER can open
        var sc = new List<Tile>();
        for (int y = 1; y < h - 1; y++) for (int x = 1; x < w - 1; x++)
            if (grid[y, x] == 1 && IsEdge(x, y)) sc.Add(new Tile(x, y));
        Shuffle(sc);
        int ns = 0;
        foreach (var t in sc)
        {
            if (ns >= SHUTTERS) break;
            if (doors.Exists(d => Math.Abs(d.x - t.x) + Math.Abs(d.y - t.y) < 3) || HallAt(t.x, t.y, 1) != null) continue;
            if (InRoomPadded(t.x, t.y)) continue; // never bypass an escape-room lockdown
            grid[t.y, t.x] = 0; AddDoor(t.x, t.y, "shutter", true); ns++;
        }
        // 5b) room gates: every opening into a puzzle room becomes a lockdown gate; the central room gets blast doors
        foreach (var rn in new[] { "power", "surveillance", "emergency", "central" })
        {
            var rm = rooms[rn]; int gi = 0;
            for (int yy = rm.y - 1; yy <= rm.y + rm.h; yy++) for (int xx = rm.x - 1; xx <= rm.x + rm.w; xx++)
            {
                bool inside = xx >= rm.x && xx < rm.x + rm.w && yy >= rm.y && yy < rm.y + rm.h;
                bool corner = (xx == rm.x - 1 || xx == rm.x + rm.w) && (yy == rm.y - 1 || yy == rm.y + rm.h);
                if (inside || corner || !InBounds(xx, yy) || grid[yy, xx] != 0 || doorAt.ContainsKey(Key(xx, yy))) continue;
                var g = new Door { id = $"{(rn == "central" ? "blast" : "gate")}_{rn}_{++gi}", x = xx, y = yy, kind = rn == "central" ? "blast" : "gate", closed = rn == "central", zone = ZoneOf(xx, yy), room = rn };
                doors.Add(g); doorAt[Key(xx, yy)] = g;
            }
        }
        start = new Tile(rooms["containment"].cx, rooms["containment"].cy);
        exitPos = new Tile(rooms["central"].cx, rooms["central"].cy);
        // free cell centres outside rooms
        var floor = new List<Tile>();
        for (int y = 1; y < h - 1; y++) for (int x = 1; x < w - 1; x++)
            if (grid[y, x] == 0 && !doorAt.ContainsKey(Key(x, y)) && x % 2 == 1 && y % 2 == 1 && RoomAt(x, y) == null) floor.Add(new Tile(x, y));
        Shuffle(floor);
        // 6) lockers: two in each puzzle room, plus one against a wall in every zone so there is always a hiding spot nearby
        var spots = new List<Tile>();
        foreach (var kv in rooms)
        {
            if (kv.Key == "containment" || kv.Key == "central") continue;
            var rm = kv.Value;
            spots.Add(new Tile(rm.x, rm.y)); spots.Add(new Tile(rm.x + rm.w - 1, rm.y + rm.h - 1));
        }
        foreach (var z in ZONE_LIST)
        {
            foreach (var t in floor)
            {
                if (ZoneOf(t.x, t.y) != z || HallAt(t.x, t.y) != null || WallCount(t) < 2) continue;
                if (spots.Exists(o => Manhattan(o, t) < 6) || Manhattan(t, start) < 4) continue;
                spots.Add(t); break;
            }
            if (!spots.Exists(o => ZoneOf(o.x, o.y) == z)) // fallback: any free cell in the zone
                foreach (var t in floor)
                    if (ZoneOf(t.x, t.y) == z && !spots.Exists(o => Manhattan(o, t) < 4) && Manhattan(t, start) >= 4) { spots.Add(t); break; }
        }
        for (int i = 0; i < spots.Count; i++) lockers.Add(new Locker { id = "locker_" + (i + 1), x = spots[i].x, y = spots[i].y, zone = ZoneOf(spots[i].x, spots[i].y) });
        // 7) items, spread out
        foreach (var t in floor)
        {
            if (items.Count >= BOTTLES) break;
            if (lockers.Exists(l => l.x == t.x && l.y == t.y) || items.Exists(o => Math.Abs(o.x - t.x) + Math.Abs(o.y - t.y) < 6)) continue;
            items.Add(new Item { type = "bottle", x = t.x, y = t.y });
        }
        // camera spots: straight corridor cells first, then hall edges; spread out
        bool Straight(Tile t) => (Walkable(t.x - 1, t.y) && Walkable(t.x + 1, t.y) && !Walkable(t.x, t.y - 1) && !Walkable(t.x, t.y + 1)) ||
                                 (Walkable(t.x, t.y - 1) && Walkable(t.x, t.y + 1) && !Walkable(t.x - 1, t.y) && !Walkable(t.x + 1, t.y));
        bool HallEdge(Tile t) { var hr = HallAt(t.x, t.y); return hr != null && (t.x == hr.x || t.y == hr.y || t.x == hr.x + hr.w - 1 || t.y == hr.y + hr.h - 1); }
        foreach (var pass in new Func<Tile, bool>[] { Straight, HallEdge })
            foreach (var t in floor)
            {
                if (cameraSpots.Count >= CAMERA_SPOTS) break;
                if (!pass(t) || Manhattan(t, start) < 6 || cameraSpots.Exists(c => Manhattan(c, t) < 6)) continue;
                if (lockers.Exists(l => l.x == t.x && l.y == t.y)) continue;
                cameraSpots.Add(t);
            }
    }

    public const int MAX_ROOM_OPENINGS = 3;
    void TrimOpenings(Room rm)
    {
        var open = new List<Tile>();
        for (int yy = rm.y - 1; yy <= rm.y + rm.h; yy++) for (int xx = rm.x - 1; xx <= rm.x + rm.w; xx++)
        {
            bool inside = xx >= rm.x && xx < rm.x + rm.w && yy >= rm.y && yy < rm.y + rm.h;
            bool corner = (xx == rm.x - 1 || xx == rm.x + rm.w) && (yy == rm.y - 1 || yy == rm.y + rm.h);
            if (!inside && !corner && InBounds(xx, yy) && grid[yy, xx] == 0) open.Add(new Tile(xx, yy));
        }
        Shuffle(open);
        int total = Reachable(1, 1).Count;
        foreach (var t in open)
        {
            if (open.Count <= MAX_ROOM_OPENINGS) break;
            grid[t.y, t.x] = 1;
            if (Reachable(1, 1).Count == total - 1) { total--; open = open.FindAll(o => o.x != t.x || o.y != t.y); }
            else grid[t.y, t.x] = 0;
        }
    }

    static bool Overlaps(Room a, Room b, int pad) =>
        a.x - pad < b.x + b.w && b.x - pad < a.x + a.w && a.y - pad < b.y + b.h && b.y - pad < a.y + a.h;

    public Room HallAt(int x, int y, int pad = 0)
    {
        foreach (var hr in halls) if (x >= hr.x - pad && x < hr.x + hr.w + pad && y >= hr.y - pad && y < hr.y + hr.h + pad) return hr;
        return null;
    }

    int WallCount(Tile t)
    {
        int n = 0; for (int d = 0; d < 4; d++) if (!InBounds(t.x + DIRS[d, 0], t.y + DIRS[d, 1]) || grid[t.y + DIRS[d, 1], t.x + DIRS[d, 0]] == 1) n++;
        return n;
    }

    void AddRoom(string name, int cx, int cy, int cww, int chh)
    {
        int x = cx * 2 + 1, y = cy * 2 + 1, rw = cww * 2 - 1, rh = chh * 2 - 1;
        for (int yy = y; yy < y + rh; yy++) for (int xx = x; xx < x + rw; xx++) grid[yy, xx] = 0;
        rooms[name] = new Room { name = name, x = x, y = y, w = rw, h = rh, cx = x + rw / 2, cy = y + rh / 2 };
    }

    bool InRoomPadded(int x, int y)
    {
        foreach (var rm in rooms.Values) if (x >= rm.x - 1 && x <= rm.x + rm.w && y >= rm.y - 1 && y <= rm.y + rm.h) return true;
        return false;
    }

    public string RoomAt(int x, int y)
    {
        foreach (var rm in rooms.Values) if (x >= rm.x && x < rm.x + rm.w && y >= rm.y && y < rm.y + rm.h) return rm.name;
        return null;
    }

    void AddDoor(int x, int y, string kind, bool closed)
    {
        int n = doors.FindAll(q => q.kind == kind).Count + 1;
        var d = new Door { id = (kind == "door" ? "door_" : "shutter_") + n, x = x, y = y, kind = kind, closed = closed, zone = ZoneOf(x, y) };
        doors.Add(d); doorAt[Key(x, y)] = d;
    }

    public Door FindDoor(string id) => doors.Find(d => d.id == id);

    public bool Walkable(int x, int y, string extraClosed = null)
    {
        if (!InBounds(x, y) || grid[y, x] != 0) return false;
        if (doorAt.TryGetValue(Key(x, y), out var d)) { if (d.closed || d.id == extraClosed) return false; }
        return true;
    }

    // engineDoorsOpen: treat escape-room gates / blast doors as open (they open by game rules, the BROKER can't hold them shut)
    public HashSet<int> Reachable(int sx, int sy, string extraClosed = null, bool engineDoorsOpen = false)
    {
        var seen = new HashSet<int> { Key(sx, sy) }; var q = new Queue<Tile>(); q.Enqueue(new Tile(sx, sy));
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                int nx = c.x + DIRS[d, 0], ny = c.y + DIRS[d, 1];
                bool ok = Walkable(nx, ny, extraClosed);
                if (!ok && engineDoorsOpen && InBounds(nx, ny) && doorAt.TryGetValue(Key(nx, ny), out var ed) && (ed.kind == "gate" || ed.kind == "blast") && ed.id != extraClosed) ok = true;
                if (!seen.Contains(Key(nx, ny)) && ok) { seen.Add(Key(nx, ny)); q.Enqueue(new Tile(nx, ny)); }
            }
        }
        return seen;
    }

    // FAIRNESS RULE: closing this door must keep every remaining objective reachable from the player
    public bool CanClose(string doorId, Tile player, List<Tile> targets)
    {
        var d = FindDoor(doorId);
        if (d == null || d.closed || d.kind != "door" || (d.x == player.x && d.y == player.y)) return false;
        var reach = Reachable(player.x, player.y, doorId, true);
        foreach (var t in targets) if (!reach.Contains(Key(t.x, t.y))) return false;
        return true;
    }

    public List<Tile> Path(Tile s, Tile t, bool ignoreDoors = false)
    {
        if (!InBounds(t.x, t.y)) return null;
        int start = Key(s.x, s.y), goal = Key(t.x, t.y);
        var open = new SortedSet<(int f, int id)>(); var g = new Dictionary<int, int> { [start] = 0 }; var came = new Dictionary<int, int>();
        open.Add((Math.Abs(s.x - t.x) + Math.Abs(s.y - t.y), start));
        int iter = 0;
        while (open.Count > 0 && iter++ < 6000)
        {
            var cur = open.Min; open.Remove(cur);
            int c = cur.id;
            if (c == goal)
            {
                var outp = new List<Tile>(); int k = c;
                while (true) { outp.Add(new Tile(k % w, k / w)); if (!came.TryGetValue(k, out k)) break; }
                outp.Reverse(); return outp;
            }
            int cx = c % w, cy = c / w;
            for (int d = 0; d < 4; d++)
            {
                int nx = cx + DIRS[d, 0], ny = cy + DIRS[d, 1];
                bool ok = ignoreDoors ? (InBounds(nx, ny) && grid[ny, nx] == 0) : Walkable(nx, ny);
                if (!ok) continue;
                int nk = Key(nx, ny), ng = g[c] + 1;
                if (!g.TryGetValue(nk, out int old) || ng < old)
                {
                    if (g.ContainsKey(nk)) open.Remove((old + Math.Abs(nx - t.x) + Math.Abs(ny - t.y), nk));
                    g[nk] = ng; came[nk] = c; open.Add((ng + Math.Abs(nx - t.x) + Math.Abs(ny - t.y), nk));
                }
            }
        }
        return null;
    }

    public bool LineOfSight(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
        int x = x0, y = y0;
        while (true)
        {
            if (!(x == x0 && y == y0) && !(x == x1 && y == y1))
            {
                if (grid[y, x] != 0) return false;
                if (doorAt.TryGetValue(Key(x, y), out var d) && d.closed) return false;
            }
            if (x == x1 && y == y1) return true;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x += sx; }
            if (e2 <= dx) { err += dx; y += sy; }
        }
    }
}
