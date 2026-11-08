// World building for VaultGame: the maze becomes the BACKROOMS (wallpaper, carpet, fluorescent panels, grime,
// graffiti) and the rooms become the FACILITY (concrete, wet floors, pipes, cabinets). Also owns the ceiling lights,
// door visuals, lockers, bottles, thrown-bottle effects, exit chevrons and terminal screens.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using A = VaultArt;
using P = VaultProps;

public partial class VaultGame
{
    readonly Dictionary<string, P.DoorVisual> doorVis = new Dictionary<string, P.DoorVisual>();
    readonly Dictionary<string, bool> doorMoving = new Dictionary<string, bool>();
    readonly List<P.Panel> panels = new List<P.Panel>();
    readonly Dictionary<string, (Transform hinge, Vector3 inside, Vector3 front)> lockerInfo = new Dictionary<string, (Transform, Vector3, Vector3)>();
    readonly List<Renderer> chevrons = new List<Renderer>();
    readonly Dictionary<string, Renderer[]> terminalScreens = new Dictionary<string, Renderer[]>();
    readonly Dictionary<string, string> terminalState = new Dictionary<string, string>();
    readonly Dictionary<string, Material> screenCache = new Dictionary<string, Material>();
    Transform staticRoot, dynRoot; Light beacon; bool lightsPowered, chevronsLit; int shardCount;

    struct Face { public Tile t; public Vector3 center, normal, along; public bool room; }
    static readonly string[] GRAFFITI = { "IT LEARNS", "DONT RUN", "IT HEARS YOU", "IT REMEMBERS", "NOT THE LOCKERS", "3 DAYS", "TRUST NO NOISE", "IT KNOWS", "KEEP MOVING", "HELP", "|||| |||| ||", "WHO IS THE HOST?" };
    static readonly Dictionary<string, string> ZONE_CODE = new Dictionary<string, string> { ["north-west"] = "NW", ["north"] = "N", ["north-east"] = "NE", ["west"] = "W", ["centre"] = "C", ["east"] = "E", ["south-west"] = "SW", ["south"] = "S", ["south-east"] = "SE" };

    Vector3 DirW(int dx, int dy) => new Vector3(dx, 0, -dy);
    static float H01(int x, int y, int s) => TexGen.Hash(x, y, s);

    void BuildWorld(System.Random rng)
    {
        staticRoot = new GameObject("Static").transform; staticRoot.SetParent(transform);
        dynRoot = new GameObject("Dynamic").transform; dynRoot.SetParent(transform);
        float W = maze.w * TILE, Hh = maze.h * TILE;
        var mid = new Vector3(W / 2 - TILE / 2, 0, -(Hh / 2 - TILE / 2));
        A.Cube("Floor", staticRoot, A.Carpet(new Vector2(maze.w / 2f, maze.h / 2f)), mid + Vector3.down * 0.05f, new Vector3(W, 0.1f, Hh), collider: true);
        A.Cube("Ceiling", staticRoot, A.Ceiling(new Vector2(maze.w, maze.h)), mid + Vector3.up * (WALL_H + 0.05f), new Vector3(W, 0.1f, Hh));

        // walls: wallpaper in the maze, concrete around the facility rooms
        for (int y = 0; y < maze.h; y++) for (int x = 0; x < maze.w; x++)
        {
            if (maze.grid[y, x] != 1) continue;
            bool nearRoom = false;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) if (maze.InBounds(x + dx, y + dy) && maze.RoomAt(x + dx, y + dy) != null) nearRoom = true;
            float hsh = H01(x, y, 5);
            var m = nearRoom ? A.ConcreteWall(hsh < 0.5f ? 0 : 1) : A.Wallpaper(hsh < 0.4f ? 0 : hsh < 0.65f ? 1 : hsh < 0.8f ? 2 : 3);
            A.Cube("Wall", staticRoot, m, World(new Tile(x, y)) + Vector3.up * WALL_H / 2, new Vector3(TILE, WALL_H, TILE), collider: true);
        }
        // facility floors
        foreach (var rm in maze.rooms.Values)
            for (int yy = rm.y; yy < rm.y + rm.h; yy++) for (int xx = rm.x; xx < rm.x + rm.w; xx++)
                A.Cube("RoomFloor", staticRoot, A.ConcreteFloor, World(new Tile(xx, yy)) + Vector3.up * 0.005f, new Vector3(TILE, 0.01f, TILE));

        // fluorescent ceiling panels on every other cell (some broken), every third one a real light
        int panelIdx = 0;
        for (int y = 1; y < maze.h; y += 2) for (int x = 1; x < maze.w; x += 2)
        {
            if (maze.grid[y, x] != 0 || maze.RoomAt(x, y) != null || (x / 2 + y / 2) % 2 != 0) continue;
            bool broken = H01(x, y, 6) < 0.08f, light = !broken && panelIdx++ % 3 == 0;
            panels.Add(P.CeilingPanel(World(new Tile(x, y)), dynRoot, broken, light));
        }

        // doors, gates, shutters, blast doors
        foreach (var d in maze.doors)
        {
            bool alongX = (maze.InBounds(d.x - 1, d.y) && maze.grid[d.y, d.x - 1] == 0) || (maze.InBounds(d.x + 1, d.y) && maze.grid[d.y, d.x + 1] == 0);
            var dv = P.Door(d, World(new Tile(d.x, d.y)), alongX, dynRoot, staticRoot);
            doorVis[d.id] = dv; doorGo[d.id] = dv.root; doorMoving[d.id] = false;
            if (d.kind == "gate" || d.kind == "blast")
            {
                float yaw = alongX ? 90 : 0;
                foreach (float off in new[] { -0.75f, 0.75f })
                {
                    var c = World(new Tile(d.x, d.y)) + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, off);
                    var dec = A.Decal(staticRoot, A.HazardDecal, c, Vector3.up, new Vector2(TILE * 0.9f, 0.32f), 0, 0.008f);
                    dec.transform.rotation = Quaternion.LookRotation(Vector3.down, Quaternion.Euler(0, yaw, 0) * Vector3.forward);
                }
            }
        }

        // lockers: backs against a wall, door facing out
        foreach (var l in maze.lockers)
        {
            Vector3 wall = Vector3.forward;
            for (int d = 0; d < 4; d++) { int nx = l.x + VaultMaze.DIRS[d, 0], ny = l.y + VaultMaze.DIRS[d, 1]; if (!maze.InBounds(nx, ny) || maze.grid[ny, nx] == 1) { wall = DirW(VaultMaze.DIRS[d, 0], VaultMaze.DIRS[d, 1]); break; } }
            var num = l.id.Replace("locker_", "").PadLeft(2, '0');
            P.Locker(num, World(new Tile(l.x, l.y)), wall, dynRoot, out var hinge, out var inside);
            lockerInfo[l.id] = (hinge, inside, -wall);
        }
        foreach (var it in maze.items) itemGo[it] = BuildItem(it);

        // rooms: hanging signs, emergency lights, terminals, monitors, facility dressing
        foreach (var rm in maze.rooms.Values)
        {
            string disp = rm.name == "central" ? "CENTRAL EXIT" : rm.name == "containment" ? "CONTAINMENT" : rooms[rm.name].title;
            var ink = rm.name == "central" ? new Color(0.25f, 1f, 0.4f) : new Color(1f, 0.32f, 0.25f);
            // hung just off-centre so it clears the terminal and its conduit
            float signYaw = rm.w >= rm.h ? 0 : 90; var signOff = Quaternion.Euler(0, signYaw, 0) * new Vector3(0, 0, rooms.ContainsKey(rm.name) ? 0.8f : 0f);
            P.HangingSign(disp, ink, World(new Tile(rm.cx, rm.cy)) + signOff + Vector3.up * 2.5f, signYaw, 0.3f, staticRoot);
            var lg = new GameObject("RoomLight_" + rm.name); lg.transform.SetParent(transform);
            lg.transform.position = World(new Tile(rm.cx, rm.cy)) + Vector3.up * (WALL_H - 0.3f);
            var li = lg.AddComponent<Light>(); li.type = LightType.Point; li.range = 11; li.intensity = 0.8f; li.color = new Color(0.8f, 0.1f, 0.1f);
            roomLights.Add(li);
            if (rooms.ContainsKey(rm.name))
            {
                terminalScreens[rm.name] = P.Terminal(World(new Tile(rm.cx, rm.cy)), staticRoot, TerminalScreen(rm.name, "STANDBY"));
                terminalState[rm.name] = "STANDBY";
            }
            if (rm.name == "central")
            {
                beacon = new GameObject("Beacon").AddComponent<Light>(); beacon.transform.SetParent(transform);
                beacon.transform.position = World(new Tile(rm.cx, rm.cy)) + Vector3.up * (WALL_H - 0.2f);
                beacon.type = LightType.Spot; beacon.spotAngle = 70; beacon.range = 16; beacon.intensity = 0; beacon.color = new Color(1f, 0.6f, 0.1f);
            }
        }
        // surveillance monitors: the digits you must read in 3D
        var sv = rooms["surveillance"]; var srm = sv.room;
        var corners = new[] { new Tile(srm.x, srm.y), new Tile(srm.x + srm.w - 1, srm.y), new Tile(srm.x, srm.y + srm.h - 1), new Tile(srm.x + srm.w - 1, srm.y + srm.h - 1) };
        for (int i = 0; i < 4; i++)
        {
            var pos = World(corners[i]) + Vector3.up * 2.2f; var face = World(new Tile(srm.cx, srm.cy)) - World(corners[i]); face.y = 0;
            P.Monitor(pos, face.normalized, staticRoot, A.Screen(new[] { "CAM " + sv.camLabels[i], sv.camDigits[i].ToString() }, new Color(0.45f, 1f, 0.95f), 1.8f, 96, 80, 3));
        }

        DecorateFaces(rng);
        DecorateFloors(rng);
        BuildExitChevrons();

        // security cameras (positions depend on the seed)
        var spots = maze.cameraSpots.OrderBy(_ => rng.Next()).Take(11).ToList();
        int ci = 0;
        foreach (var s in spots)
        {
            bool horiz = maze.Walkable(s.x - 1, s.y) && maze.Walkable(s.x + 1, s.y);
            float yaw = horiz ? (rng.Next(2) == 0 ? 90 : 270) : (rng.Next(2) == 0 ? 0 : 180);
            var go = new GameObject("SecurityCam_" + (++ci)); go.transform.SetParent(transform);
            var cam = go.AddComponent<SecurityCamera>(); cam.Build("cam_" + ci, s, World(s) + Vector3.up * (WALL_H - 0.25f), yaw);
            cams.Add(cam);
        }

        StaticBatchingUtility.Combine(staticRoot.gameObject);
    }

    List<Face> WallFaces()
    {
        var faces = new List<Face>();
        for (int y = 0; y < maze.h; y++) for (int x = 0; x < maze.w; x++)
        {
            if (maze.grid[y, x] != 0 || maze.doorAt.ContainsKey(maze.Key(x, y))) continue;
            for (int d = 0; d < 4; d++)
            {
                int nx = x + VaultMaze.DIRS[d, 0], ny = y + VaultMaze.DIRS[d, 1];
                if (!maze.InBounds(nx, ny) || maze.grid[ny, nx] != 1) continue;
                var dw = DirW(VaultMaze.DIRS[d, 0], VaultMaze.DIRS[d, 1]);
                faces.Add(new Face { t = new Tile(x, y), center = World(new Tile(x, y)) + dw * (TILE / 2), normal = -dw, along = Vector3.Cross(Vector3.up, -dw), room = maze.RoomAt(x, y) != null });
            }
        }
        return faces;
    }

    void DecorateFaces(System.Random rng)
    {
        var faces = WallFaces().OrderBy(_ => rng.Next()).ToList();
        float R() => (float)rng.NextDouble();
        Vector3 Spot(Face f, float w, float y) => f.center + f.along * ((R() - 0.5f) * Mathf.Max(0, TILE - w - 0.1f)) + Vector3.up * y;
        var lockerTiles = new HashSet<int>(maze.lockers.Select(l => maze.Key(l.x, l.y)));
        int graffiti = 0; var stencilled = new HashSet<string>(); var caged = new HashSet<string>();
        foreach (var f in faces)
        {
            if (!f.room)
            {
                if (R() < 0.3f) { float w = 1.2f + R(), h = 1.0f + R() * 0.8f; A.Decal(staticRoot, A.Stain(rng.Next(3)), Spot(f, w, WALL_H - h / 2 - 0.02f), f.normal, new Vector2(w, h)); }
                if (R() < 0.08f) { float s = 1f + R() * 0.6f; A.Decal(staticRoot, A.Mould(rng.Next(2)), Spot(f, s, 0.55f), f.normal, new Vector2(s, s), R() * 360); }
                if (R() < 0.15f) A.Decal(staticRoot, A.Scuff, Spot(f, 1.2f, 0.32f), f.normal, new Vector2(1.2f, 0.5f), 0, 0.014f);
                if (R() < 0.06f) A.Decal(staticRoot, A.Vent, Spot(f, 0.6f, 2.5f), f.normal, new Vector2(0.6f, 0.45f), 0, 0.016f);
                if (R() < 0.08f) A.Decal(staticRoot, A.Outlet, Spot(f, 0.12f, 0.32f), f.normal, new Vector2(0.12f, 0.18f), 0, 0.016f);
                if (graffiti < GRAFFITI.Length && R() < 0.12f && Mathf.Abs(f.t.x - maze.start.x) + Mathf.Abs(f.t.y - maze.start.y) > 3)
                {
                    var col = R() < 0.6f ? new Color(0.5f, 0.05f, 0.04f) : new Color(0.06f, 0.06f, 0.06f);
                    var mat = A.Graffiti(GRAFFITI[graffiti], col, graffiti * 17 + 3, out float aspect);
                    float w = Mathf.Min(2.3f, 0.55f * aspect), h = w / aspect;
                    A.Decal(staticRoot, mat, Spot(f, w, 1.25f + R() * 0.4f), f.normal, new Vector2(w, h), (R() - 0.5f) * 8f, 0.018f);
                    graffiti++;
                }
                string zone = maze.ZoneOf(f.t.x, f.t.y);
                if (!stencilled.Contains(zone) && R() < 0.25f)
                {
                    stencilled.Add(zone);
                    var mat = A.Stencil($"SECTOR {ZONE_CODE[zone]}-{1 + rng.Next(9)}", new Color(0.82f, 0.82f, 0.76f), rng.Next(100), out float aspect);
                    float h = 0.28f; A.Decal(staticRoot, mat, Spot(f, h * aspect, 1.9f), f.normal, new Vector2(h * aspect, h), 0, 0.02f);
                }
            }
            else
            {
                // facility: pipes along the top of every wall, conduits, cabinets, cracks, a caged lamp
                var a = f.center + f.normal * 0.14f - f.along * (TILE / 2); var b = f.center + f.normal * 0.14f + f.along * (TILE / 2);
                P.PipeRun(a + Vector3.up * 2.6f, b + Vector3.up * 2.6f, 0.065f, A.RustPipe, staticRoot);
                P.PipeRun(a + Vector3.up * 2.38f + f.normal * -0.05f, b + Vector3.up * 2.38f + f.normal * -0.05f, 0.03f, A.Pipe, staticRoot);
                if (R() < 0.3f) { var c = Spot(f, 0.2f, 0) + f.normal * 0.06f; P.PipeRun(c + Vector3.up * 0.25f, c + Vector3.up * 2.38f, 0.025f, A.Pipe, staticRoot); A.Cube("junction", staticRoot, A.DarkSteel, c + Vector3.up * 1.45f, new Vector3(0.22f, 0.28f, 0.22f)); }
                if (R() < 0.25f) A.Decal(staticRoot, A.Crack(rng.Next(2)), Spot(f, 1.2f, 0.6f + R() * 1.5f), f.normal, new Vector2(1.2f, 1.2f), R() * 360);
                if (R() < 0.2f) A.Decal(staticRoot, A.Stain(rng.Next(3)), Spot(f, 1.4f, WALL_H - 0.75f), f.normal, new Vector2(1.4f, 1.4f));
                string rn = maze.RoomAt(f.t.x, f.t.y);
                if (!lockerTiles.Contains(maze.Key(f.t.x, f.t.y)) && R() < 0.3f) P.Cabinet(f.center, f.normal, staticRoot, rng.Next(100));
                else if (!caged.Contains(rn))
                {
                    caged.Add(rn); var c = f.center + f.normal * 0.08f + Vector3.up * 2.15f;
                    A.Cube("lamp", staticRoot, A.Glow(new Color(1f, 0.85f, 0.6f), 2.5f), c, new Vector3(0.2f, 0.12f, 0.12f), Quaternion.LookRotation(f.normal));
                    for (int i = -1; i <= 1; i++) A.Cube("cage", staticRoot, A.DarkSteel, c + f.along * (i * 0.08f) + f.normal * 0.07f, new Vector3(0.012f, 0.16f, 0.012f));
                }
            }
        }
        // facility ceilings: big pipes running across each room
        foreach (var rm in maze.rooms.Values)
        {
            var west = World(new Tile(rm.x, rm.y)) - new Vector3(TILE / 2, 0, 0); var east = World(new Tile(rm.x + rm.w - 1, rm.y)) + new Vector3(TILE / 2, 0, 0);
            for (int yy = rm.y; yy < rm.y + rm.h; yy += 2)
            {
                float z = World(new Tile(rm.x, yy)).z;
                P.PipeRun(new Vector3(west.x, WALL_H - 0.22f, z + 0.4f), new Vector3(east.x, WALL_H - 0.22f, z + 0.4f), 0.09f, A.RustPipe, staticRoot);
                P.PipeRun(new Vector3(west.x, WALL_H - 0.14f, z - 0.3f), new Vector3(east.x, WALL_H - 0.14f, z - 0.3f), 0.05f, A.Pipe, staticRoot);
            }
        }
    }

    void DecorateFloors(System.Random rng)
    {
        float R() => (float)rng.NextDouble();
        var cells = new List<Tile>();
        for (int y = 1; y < maze.h; y++) for (int x = 1; x < maze.w; x++) if (maze.grid[y, x] == 0 && !maze.doorAt.ContainsKey(maze.Key(x, y))) cells.Add(new Tile(x, y));
        cells = cells.OrderBy(_ => rng.Next()).ToList();
        var used = new HashSet<int>(maze.lockers.Select(l => maze.Key(l.x, l.y)));
        foreach (var it in maze.items) used.Add(maze.Key(it.x, it.y));
        used.Add(maze.Key(maze.start.x, maze.start.y));
        int puddles = 0, papers = 0, rot = 0, signs = 0;
        foreach (var t in cells)
        {
            var c = World(t) + new Vector3((R() - 0.5f) * 1.2f, 0, (R() - 0.5f) * 1.2f);
            bool room = maze.RoomAt(t.x, t.y) != null;
            if (puddles < 22 && R() < 0.12f)
            {
                float s = 0.8f + R() * 1.2f; A.Decal(staticRoot, A.Puddle(rng.Next(3)), c, Vector3.up, new Vector2(s, s * (0.7f + R() * 0.3f)), R() * 360, 0.006f); puddles++;
                if (!room && signs < 4 && R() < 0.3f && !used.Contains(maze.Key(t.x, t.y))) { P.WetFloorSign(c + new Vector3(0.6f, 0, 0.3f), R() * 360, staticRoot); signs++; }
            }
            else if (!room && rot < 30 && R() < 0.1f) { float s = 0.9f + R(); A.Decal(staticRoot, A.Mould(rng.Next(2)), c, Vector3.up, new Vector2(s, s), R() * 360, 0.005f); rot++; }
            else if (room && R() < 0.3f) A.Decal(staticRoot, A.Crack(rng.Next(2)), c, Vector3.up, new Vector2(1.4f, 1.4f), R() * 360, 0.006f);
            if (papers < 26 && R() < 0.1f) { A.Decal(staticRoot, A.Paper(rng.Next(3)), c + new Vector3(0.3f, 0, -0.2f), Vector3.up, new Vector2(0.21f, 0.27f), R() * 360, 0.004f); papers++; }
        }
        // cardboard boxes stacked in dead ends
        foreach (var t in cells)
        {
            if (t.x % 2 == 0 || t.y % 2 == 0 || maze.RoomAt(t.x, t.y) != null || used.Contains(maze.Key(t.x, t.y))) continue;
            int open = 0, ox = 0, oy = 0;
            for (int d = 0; d < 4; d++) { int nx = t.x + VaultMaze.DIRS[d, 0], ny = t.y + VaultMaze.DIRS[d, 1]; if (maze.InBounds(nx, ny) && maze.grid[ny, nx] == 0) { open++; ox = VaultMaze.DIRS[d, 0]; oy = VaultMaze.DIRS[d, 1]; } }
            if (open != 1 || R() > 0.6f) continue;
            var back = -DirW(ox, oy); var side = Vector3.Cross(Vector3.up, back);
            var basePos = World(t) + back * (TILE / 2 - 0.45f);
            int n = 1 + rng.Next(3);
            for (int i = 0; i < n; i++)
            {
                float s = 0.45f + R() * 0.2f; var p = basePos + side * ((i % 2 == 0 ? -1 : 1) * 0.55f);
                P.Box(p, s, R() * 30 - 15, staticRoot);
                if (i == 0 && R() < 0.5f) P.Box(p + Vector3.up * s * 0.8f, s * 0.8f, R() * 40 - 20, staticRoot);
            }
        }
    }

    // painted chevrons at junctions, pointing along the shortest route to the central exit; they light up once it opens
    void BuildExitChevrons()
    {
        var dist = new int[maze.h, maze.w]; for (int y = 0; y < maze.h; y++) for (int x = 0; x < maze.w; x++) dist[y, x] = -1;
        var q = new Queue<Tile>(); q.Enqueue(maze.exitPos); dist[maze.exitPos.y, maze.exitPos.x] = 0;
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            for (int d = 0; d < 4; d++) { int nx = c.x + VaultMaze.DIRS[d, 0], ny = c.y + VaultMaze.DIRS[d, 1]; if (maze.InBounds(nx, ny) && maze.grid[ny, nx] == 0 && dist[ny, nx] < 0) { dist[ny, nx] = dist[c.y, c.x] + 1; q.Enqueue(new Tile(nx, ny)); } }
        }
        var placed = new List<Tile>();
        for (int y = 1; y < maze.h; y += 2) for (int x = 1; x < maze.w; x += 2)
        {
            if (maze.grid[y, x] != 0 || maze.RoomAt(x, y) != null || dist[y, x] <= 2 || placed.Count >= 30) continue;
            int open = 0; Tile best = new Tile(x, y); int bd = dist[y, x];
            for (int d = 0; d < 4; d++) { int nx = x + VaultMaze.DIRS[d, 0], ny = y + VaultMaze.DIRS[d, 1]; if (maze.InBounds(nx, ny) && maze.grid[ny, nx] == 0) { open++; if (dist[ny, nx] >= 0 && dist[ny, nx] < bd) { bd = dist[ny, nx]; best = new Tile(nx, ny); } } }
            if (open < 3 || placed.Exists(p => Mathf.Abs(p.x - x) + Mathf.Abs(p.y - y) < 4)) continue;
            var dir = DirW(best.x - x, best.y - y);
            var g = A.Decal(staticRoot, A.ChevronDim, World(new Tile(x, y)) + dir * 0.55f, Vector3.up, new Vector2(0.7f, 0.7f), 0, 0.009f);
            g.transform.rotation = Quaternion.LookRotation(Vector3.down, dir);
            chevrons.Add(g.GetComponent<Renderer>()); placed.Add(new Tile(x, y));
        }
    }

    GameObject BuildItem(Item it)
    {
        var g = P.Bottle(dynRoot);
        g.transform.position = World(new Tile(it.x, it.y)) + new Vector3(0.25f, 0, 0.15f);
        g.transform.rotation = Quaternion.Euler(0, H01(it.x, it.y, 9) * 360, 0);
        return g;
    }

    Material TerminalScreen(string room, string status)
    {
        string key = room + "|" + status;
        if (!screenCache.TryGetValue(key, out var m))
        {
            var col = status == "COMPLETE" ? new Color(0.3f, 1f, 0.45f) : status.StartsWith("LOCKS") || status == "BREACHED" ? new Color(1f, 0.3f, 0.2f) : new Color(0.4f, 0.95f, 1f);
            screenCache[key] = m = A.Screen(new[] { room.ToUpper(), "TERMINAL", status }, col, 1.6f, 128, 96, 2);
        }
        return m;
    }

    // ---------- per-frame world updates ----------
    void RollPanels(bool powered)
    {
        var r = new System.Random((int)seed + (powered ? 7 : 0));
        foreach (var p in panels)
        {
            p.on = !p.broken && r.NextDouble() < (powered ? 0.92 : 0.45);
            p.flicker = p.broken || r.NextDouble() < (powered ? 0.05 : 0.2);
            SetPanel(p, p.on);
        }
        if (sfx != null) sfx.humVolume = powered ? 0.06f : 0.03f;
    }

    void SetPanel(P.Panel p, bool lit)
    {
        p.diffuser.sharedMaterial = lit ? A.PanelOn : A.PanelOff;
        if (p.light != null) p.light.enabled = lit;
        if (p.shaft != null) p.shaft.enabled = lit;
    }

    void TickWorld(float dt)
    {
        bool powered = rooms["power"].done;
        if (powered != lightsPowered) { lightsPowered = powered; RollPanels(powered); }
        foreach (var p in panels)
        {
            if (!p.flicker || Time.time < p.next) continue;
            p.next = Time.time + Random.Range(0.03f, 0.35f) * (Random.value < 0.15f ? 8f : 1f);
            SetPanel(p, p.on ? Random.value < 0.75f : Random.value < (p.broken ? 0.05f : 0.12f));
        }
        // doors: slide, warning lamps, motor + clang sounds
        foreach (var d in maze.doors)
        {
            var dv = doorVis[d.id]; var g = dv.root; float ty = d.closed ? WALL_H / 2 : -WALL_H / 2 + 0.05f;
            var p = g.transform.position; bool moving = Mathf.Abs(p.y - ty) > 0.01f;
            if (moving && !doorMoving[d.id]) { doorMoving[d.id] = true; sfx.Play("motor", new Vector3(p.x, 1.5f, p.z), d.kind == "blast" ? 1f : 0.7f, 0.05f, 28f, d.kind == "blast" ? 0.6f : 1f); }
            p.y = Mathf.MoveTowards(p.y, ty, DOOR_SPEED * dv.speed * dt); g.transform.position = p;
            if (doorMoving[d.id] && Mathf.Abs(p.y - ty) <= 0.01f) { doorMoving[d.id] = false; sfx.Play("clang", new Vector3(p.x, 1.5f, p.z), 0.8f, 0.08f, 32f, d.kind == "blast" ? 0.6f : 1f); }
            g.GetComponent<Collider>().enabled = d.closed || p.y > 0.5f;
            bool lampOn = d.kind == "blast" ? !d.closed : d.closed;
            var lm = lampOn ? dv.lampOn : dv.lampOff;
            if (d.kind == "door" && lampOn) lm = (Time.time * 2f) % 1f < 0.5f ? dv.lampOn : dv.lampOff; // blinking while sealed
            foreach (var r in dv.lamps) if (r.sharedMaterial != lm) r.sharedMaterial = lm;
        }
        // exit route lights up once the central exit opens; beacon spins
        if (centralOpen && !chevronsLit) { chevronsLit = true; foreach (var c in chevrons) c.sharedMaterial = A.ChevronLit; }
        if (beacon != null) { beacon.intensity = centralOpen ? 3f : 0f; beacon.transform.rotation = Quaternion.Euler(55, Time.time * 240f, 0); }
        // terminal screens follow each room's state
        foreach (var r in rooms.Values)
        {
            string st = r.done ? "COMPLETE" : r.active && !r.breached ? $"LOCKS {r.locks}/3" : r.breached ? "BREACHED" : "STANDBY";
            if (terminalState[r.name] == st) continue;
            terminalState[r.name] = st; var m = TerminalScreen(r.name, st); foreach (var s in terminalScreens[r.name]) s.sharedMaterial = m;
        }
    }

    // ---------- effects ----------
    // door swings open and shut; while you hide inside, its inner face is hidden so the vent-slit view looks out
    IEnumerator SwingLocker(Transform hinge, bool hideWhenShut)
    {
        foreach (var r in hinge.GetComponentsInChildren<Renderer>()) r.enabled = true;
        for (float t = 0; t < 1f; t += Time.deltaTime / 0.55f)
        {
            float a = Mathf.Sin(t * Mathf.PI) * 95f; hinge.localRotation = Quaternion.Euler(0, -a, 0); yield return null;
        }
        hinge.localRotation = Quaternion.identity;
        if (hideWhenShut && player.hidden) foreach (var r in hinge.GetComponentsInChildren<Renderer>()) r.enabled = false;
    }

    IEnumerator ThrowFx(Vector3 from, Vector3 to, NoiseEvent noise)
    {
        var b = P.Bottle(dynRoot); float dur = Mathf.Clamp(Vector3.Distance(from, to) / 14f, 0.25f, 0.7f);
        sfx.Play("whoosh", from, 0.6f);
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float k = t / dur; b.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * (0.5f + dur);
            b.transform.Rotate(720 * Time.deltaTime, 0, 300 * Time.deltaTime); yield return null;
        }
        Destroy(b);
        sfx.Play("shatter", to + Vector3.up * 0.2f, 1f, 0.08f, 40f);
        broker.Hear(noise);
        // glass shards scatter and stay on the floor
        var shards = new List<(Transform tr, Vector3 v, Vector3 spin)>();
        int n = shardCount < 200 ? 12 : 0; shardCount += n;
        for (int i = 0; i < n; i++)
        {
            var s = A.Cube("shard", dynRoot, A.BottleGlass, to + Vector3.up * 0.1f, new Vector3(Random.Range(0.02f, 0.05f), 0.005f, Random.Range(0.02f, 0.05f)), Random.rotation);
            shards.Add((s.transform, new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(0.8f, 2.4f), Random.Range(-1.6f, 1.6f)), Random.insideUnitSphere * 720f));
        }
        A.Decal(dynRoot, A.Puddle(0), to, Vector3.up, new Vector2(0.6f, 0.45f), Random.value * 360, 0.007f);
        for (float t = 0; t < 1.2f; t += Time.deltaTime)
        {
            for (int i = 0; i < shards.Count; i++)
            {
                var (tr, v, spin) = shards[i]; v.y -= 9.8f * Time.deltaTime; var p = tr.position + v * Time.deltaTime;
                if (p.y < 0.004f) { p.y = 0.004f; v = new Vector3(v.x * 0.4f, -v.y * 0.25f, v.z * 0.4f); spin *= 0.5f; }
                tr.position = p; tr.Rotate(spin * Time.deltaTime); shards[i] = (tr, v, spin);
            }
            yield return null;
        }
        foreach (var (tr, _, _) in shards) tr.rotation = Quaternion.Euler(0, Random.value * 360, 0);
    }

    void BuildDust(Transform cam)
    {
        var go = new GameObject("Dust"); go.transform.SetParent(cam, false); go.transform.localPosition = new Vector3(0.2f, -0.15f, 0.2f);
        var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = true; main.startLifetime = 3.5f; main.startSpeed = 0.02f; main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.022f);
        main.maxParticles = 250; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startColor = new Color(1f, 0.95f, 0.85f, 0.35f);
        var em = ps.emission; em.rateOverTime = 60;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.ConeVolume; sh.angle = 22; sh.radius = 0.05f; sh.length = 6f;
        var no = ps.noise; no.enabled = true; no.strength = 0.06f; no.frequency = 0.25f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient(); grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(0, 1) });
        col.color = grad;
        var r = go.GetComponent<ParticleSystemRenderer>(); r.material = A.Additive(A.DotTex, Color.white); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
    }
}
