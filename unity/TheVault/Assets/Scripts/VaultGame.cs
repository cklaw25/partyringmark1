// THE VAULT: OUTBREAK - game manager. Builds the whole 3D world from code at Play (no scene setup needed),
// runs the game loop, applies + validates THE BROKER's strategic decisions, and draws the live agent panel.
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class VaultGame : MonoBehaviour
{
    public const float TILE = 2f, WALL_H = 3f;
    public uint seed = 1337;
    public bool demoMode = true;               // 5-minute demo; false = 30-minute full game
    const float STRATEGY_INTERVAL = 5f, STRUCTURAL_COOLDOWN = 8f; const int MAX_BROKER_CLOSED = 4;

    public VaultMaze maze; public BrokerMemory mem; public BrokerAgent broker; public FirstPersonPlayer player; StrategistClient client;
    public Dictionary<string, bool> progress = new Dictionary<string, bool> { ["fuse"] = false, ["power"] = false, ["keycard"] = false };
    int lives = 3, bottles = 2; float elapsed, lastStrategy = -99, lastStructural = -99, lastReflexSay = -99;
    List<string> brokerClosed = new List<string>();
    (DecisionRecord rec, float at, int sightings, int captures, string door)? pending;
    bool started, over, won, showPanel = true, revealBroker;
    string subtitle = "", toast = ""; float toastUntil;
    Tile lastTile;
    float GameSeconds => demoMode ? 300 : 1800;
    readonly List<float> fastSamples = new List<float>();

    // world objects
    readonly Dictionary<string, GameObject> doorGo = new Dictionary<string, GameObject>();
    readonly Dictionary<Item, GameObject> itemGo = new Dictionary<Item, GameObject>();
    readonly List<Light> roomLights = new List<Light>();
    readonly List<Transform> signs = new List<Transform>();
    string seedText = "1337";
    GameObject generatorGo; Renderer generatorR;
    Texture2D minimap; float minimapTimer;
    Vector2 pmScroll;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindObjectOfType<VaultGame>() != null) return;
        foreach (var c in FindObjectsOfType<Camera>()) c.gameObject.SetActive(false);
        foreach (var l in FindObjectsOfType<Light>()) if (l.type == LightType.Directional) l.gameObject.SetActive(false);
        new GameObject("VAULT").AddComponent<VaultGame>();
    }

    // ---------- coordinates ----------
    public Vector3 World(Tile t) => new Vector3(t.x * TILE, 0, -t.y * TILE);
    public Tile TileOf(Vector3 p) => new Tile(Mathf.RoundToInt(p.x / TILE), Mathf.RoundToInt(-p.z / TILE));

    // ---------- setup ----------
    void Start()
    {
        maze = new VaultMaze(seed); mem = new BrokerMemory(); seedText = seed.ToString();
        client = gameObject.AddComponent<StrategistClient>();
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.025f, 0.025f, 0.035f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.07f; RenderSettings.fogColor = Color.black;
        BuildWorld();
        var pgo = new GameObject("Player"); pgo.transform.SetParent(transform);
        player = pgo.AddComponent<FirstPersonPlayer>(); player.Build(this, World(maze.start));
        player.Look(90);
        var bgo = BuildBrokerBody(); broker = bgo.AddComponent<BrokerAgent>(); broker.Init(this);
        lastTile = player.T;
        minimap = new Texture2D(maze.w, maze.h) { filterMode = FilterMode.Point };
        Emit("game_started", ("seed", (int)seed), ("mode", demoMode ? "demo" : "full"));
    }

    Material Mat(Color c, Color? emission = null)
    {
        var m = new Material(Shader.Find("Standard")); m.color = c; m.SetFloat("_Glossiness", 0.15f);
        if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); }
        return m;
    }

    GameObject Box(string name, Vector3 pos, Vector3 size, Material m, bool collider = true)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(transform);
        g.transform.position = pos; g.transform.localScale = size; g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Destroy(g.GetComponent<Collider>());
        return g;
    }

    void BuildWorld()
    {
        var wallM = Mat(new Color(0.22f, 0.24f, 0.27f)); var floorM = Mat(new Color(0.12f, 0.12f, 0.13f)); var roomFloorM = Mat(new Color(0.16f, 0.15f, 0.14f));
        var doorM = Mat(new Color(0.5f, 0.08f, 0.08f), new Color(0.6f, 0.02f, 0.02f)); var shutterM = Mat(new Color(0.35f, 0.25f, 0.12f), new Color(0.15f, 0.08f, 0f));
        var lockerM = Mat(new Color(0.25f, 0.33f, 0.27f));
        float W = maze.w * TILE, H = maze.h * TILE;
        Box("Floor", new Vector3(W / 2 - TILE / 2, -0.05f, -(H / 2 - TILE / 2)), new Vector3(W, 0.1f, H), floorM);
        Box("Ceiling", new Vector3(W / 2 - TILE / 2, WALL_H + 0.05f, -(H / 2 - TILE / 2)), new Vector3(W, 0.1f, H), Mat(new Color(0.05f, 0.05f, 0.06f)), false);
        for (int y = 0; y < maze.h; y++) for (int x = 0; x < maze.w; x++)
        {
            var p = World(new Tile(x, y));
            if (maze.grid[y, x] == 1) Box("Wall", p + Vector3.up * WALL_H / 2, new Vector3(TILE, WALL_H, TILE), wallM);
            else if (maze.RoomAt(x, y) != null) Box("RoomFloor", p + Vector3.up * 0.005f, new Vector3(TILE, 0.01f, TILE), roomFloorM, false);
        }
        foreach (var d in maze.doors)
        {
            var g = Box(d.id, World(new Tile(d.x, d.y)) + Vector3.up * (d.closed ? WALL_H / 2 : -WALL_H / 2 + 0.05f), new Vector3(TILE * 0.98f, WALL_H, TILE * 0.98f), d.kind == "door" ? doorM : shutterM);
            doorGo[d.id] = g;
        }
        foreach (var l in maze.lockers) Box(l.id, World(new Tile(l.x, l.y)) + Vector3.up * 1.0f, new Vector3(0.9f, 2.0f, 0.9f), lockerM, false);
        foreach (var it in maze.items) itemGo[it] = BuildItem(it);
        generatorGo = Box("Generator", World(maze.generatorPos) + Vector3.up * 0.6f, new Vector3(1.4f, 1.2f, 1.0f), Mat(new Color(0.8f, 0.4f, 0.1f), new Color(0.4f, 0.15f, 0f)));
        generatorR = generatorGo.GetComponent<Renderer>();
        Box("Exit", World(maze.exitPos) + Vector3.up * 1.1f, new Vector3(1.2f, 2.2f, 0.2f), Mat(Color.white, new Color(0.7f, 0.9f, 1f)), false);
        // room lights: dim red emergency light until power returns
        foreach (var rm in maze.rooms.Values)
        {
            var sign = new GameObject("Sign_" + rm.name); sign.transform.SetParent(transform);
            sign.transform.position = World(new Tile(rm.cx, rm.cy)) + Vector3.up * (WALL_H - 0.55f);
            var tm = sign.AddComponent<TextMesh>(); tm.text = rm.name.ToUpper(); tm.fontSize = 64; tm.characterSize = 0.05f;
            tm.anchor = TextAnchor.MiddleCenter; tm.color = new Color(0.9f, 0.35f, 0.3f);
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); sign.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            signs.Add(sign.transform);
            var lg = new GameObject("RoomLight_" + rm.name); lg.transform.SetParent(transform);
            lg.transform.position = World(new Tile(rm.cx, rm.cy)) + Vector3.up * (WALL_H - 0.3f);
            var li = lg.AddComponent<Light>(); li.type = LightType.Point; li.range = 9; li.intensity = 0.7f; li.color = new Color(0.8f, 0.1f, 0.1f);
            roomLights.Add(li);
        }
    }

    GameObject BuildItem(Item it)
    {
        Color c = it.type == "fuse" ? new Color(1f, 0.8f, 0.2f) : it.type == "keycard" ? new Color(0.3f, 1f, 0.85f) : new Color(0.6f, 0.65f, 0.6f);
        var g = it.type == "bottle" ? GameObject.CreatePrimitive(PrimitiveType.Cylinder) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = it.type; g.transform.SetParent(transform); Destroy(g.GetComponent<Collider>());
        g.transform.position = World(new Tile(it.x, it.y)) + Vector3.up * 0.9f;
        g.transform.localScale = it.type == "bottle" ? new Vector3(0.15f, 0.2f, 0.15f) : it.type == "keycard" ? new Vector3(0.4f, 0.05f, 0.28f) : new Vector3(0.25f, 0.4f, 0.25f);
        g.GetComponent<Renderer>().sharedMaterial = Mat(c, c * 0.8f);
        if (it.type != "bottle") { var l = new GameObject("glow").AddComponent<Light>(); l.transform.SetParent(g.transform, false); l.type = LightType.Point; l.range = 4; l.intensity = 1.5f; l.color = c; }
        return g;
    }

    GameObject BuildBrokerBody()
    {
        var g = new GameObject("THE_BROKER"); g.transform.SetParent(transform);
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.transform.SetParent(g.transform, false); Destroy(body.GetComponent<Collider>());
        body.transform.localPosition = Vector3.up * 1.1f; body.transform.localScale = new Vector3(0.8f, 1.1f, 0.8f);
        body.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.05f, 0.05f, 0.06f));
        var eyeM = Mat(Color.red, new Color(4f, 0f, 0f));
        foreach (float ex in new[] { -0.17f, 0.17f })
        {
            var e = GameObject.CreatePrimitive(PrimitiveType.Sphere); e.transform.SetParent(g.transform, false); Destroy(e.GetComponent<Collider>());
            e.transform.localPosition = new Vector3(ex, 1.75f, 0.33f); e.transform.localScale = Vector3.one * 0.13f; e.GetComponent<Renderer>().sharedMaterial = eyeM;
        }
        var li = new GameObject("eyeLight").AddComponent<Light>(); li.transform.SetParent(g.transform, false); li.transform.localPosition = new Vector3(0, 1.75f, 0.6f);
        li.type = LightType.Point; li.range = 6; li.intensity = 2.5f; li.color = Color.red;
        return g;
    }

    // ---------- events / voice ----------
    public void Emit(string name, params (string k, object v)[] kv)
    {
        var e = new GameEvent { t = elapsed, name = name }; foreach (var (k, v) in kv) e.data[k] = v;
        mem.Push(e);
        var significant = new[] { "item_collected", "power_restored", "player_captured", "player_spotted", "player_hiding", "distraction_detected" };
        if (significant.Contains(name)) RequestStrategy(name);
    }

    public void Say(string text, bool reflex)
    {
        if (reflex) { if (elapsed - lastReflexSay < 3) return; lastReflexSay = elapsed; }
        subtitle = text; client.Say(text, !reflex);
    }

    void Toast(string s) { toast = s; toastUntil = Time.time + 3f; }

    // ---------- objectives / fairness ----------
    List<Tile> ObjectiveTargets()
    {
        var t = new List<Tile>();
        foreach (var it in maze.items) if (it.type == "fuse" || it.type == "keycard") t.Add(new Tile(it.x, it.y));
        if (!progress["power"]) t.Add(maze.generatorPos);
        t.Add(maze.exitPos); return t;
    }

    public Tile? ObjectivePoint(string name)
    {
        var r = maze.rooms;
        switch (name)
        {
            case "fuse": case "storage": return new Tile(r["storage"].cx, r["storage"].cy);
            case "generator": return new Tile(r["generator"].cx, r["generator"].cy);
            case "keycard": case "security": return new Tile(r["security"].cx, r["security"].cy);
            case "exit": return new Tile(r["exit"].cx, r["exit"].cy);
            case "start": return new Tile(r["start"].cx, r["start"].cy);
        }
        if (!string.IsNullOrEmpty(name) && VaultMaze.ZONE_LIST.Contains(name)) return maze.ZoneCentre(name);
        return null;
    }

    List<string> ClosableDoors()
    {
        var pt = player.T; var targets = ObjectiveTargets();
        return maze.doors.Where(d => !d.closed && maze.CanClose(d.id, pt, targets)).Select(d => d.id).ToList();
    }

    // ---------- slow path ----------
    string BuildStateJson(string trigger)
    {
        var ls = mem.lastSeen; var bt = broker.T;
        var state = new Dictionary<string, object>
        {
            ["trigger"] = trigger,
            ["time_left_s"] = Mathf.RoundToInt(GameSeconds - elapsed), ["lives"] = lives,
            ["progress"] = new Dictionary<string, object>(progress.ToDictionary(k => k.Key, k => (object)k.Value)),
            ["profile"] = mem.Profile(),
            ["last_seen"] = ls.HasValue ? new Dictionary<string, object> { ["zone"] = maze.ZoneOf(ls.Value.x, ls.Value.y), ["seconds_ago"] = Mathf.RoundToInt(Time.time - mem.lastSeenTime) } : null,
            ["broker"] = new Dictionary<string, object> { ["state"] = broker.state, ["zone"] = maze.ZoneOf(bt.x, bt.y) },
            ["recent_events"] = mem.events.Skip(Mathf.Max(0, mem.events.Count - 10)).Select(e => (object)e.ToDict()).ToList(),
            ["doors"] = maze.doors.Select(d => (object)new Dictionary<string, object> { ["id"] = d.id, ["kind"] = d.kind, ["zone"] = d.zone, ["closed"] = d.closed }).ToList(),
            ["closable_doors"] = ClosableDoors().Cast<object>().ToList(),
            ["openable_passages"] = maze.doors.Where(d => d.closed).Select(d => (object)d.id).ToList(),
            ["lockers"] = maze.lockers.Select(l => (object)new Dictionary<string, object> { ["id"] = l.id, ["zone"] = l.zone, ["uses"] = mem.lockerUses.TryGetValue(l.id, out var u) ? u : 0 }).ToList(),
            ["last_decision"] = mem.decisions.Count > 0 ? mem.decisions[mem.decisions.Count - 1].ToDict() : null,
        };
        return MiniJson.Write(new Dictionary<string, object> { ["state"] = state });
    }

    void RequestStrategy(string trigger)
    {
        if (over || !started || client.busy) return;
        lastStrategy = elapsed;
        client.Decide(BuildStateJson(trigger), 8f, dec =>
        {
            if (dec == null) dec = BrokerAgent.Fallback(mem, progress, ClosableDoors());
            ApplyDecision(dec);
        });
    }

    void ApplyDecision(Decision dec)
    {
        if (over) return;
        bool ok = true; string note = "";
        if (dec.action == "BLOCK_CORRIDOR")
        {
            if (elapsed - lastStructural < STRUCTURAL_COOLDOWN) { ok = false; note = "rejected: structural cooldown"; }
            else if (!ClosableDoors().Contains(dec.target)) { ok = false; note = "rejected: would trap the player / invalid door"; }
            else
            {
                var d = maze.FindDoor(dec.target); d.closed = true; brokerClosed.Add(d.id); lastStructural = elapsed;
                if (brokerClosed.Count > MAX_BROKER_CLOSED) { var old = maze.FindDoor(brokerClosed[0]); brokerClosed.RemoveAt(0); if (old != null) old.closed = false; }
                note = $"sealed {d.id} ({d.zone})";
            }
        }
        else if (dec.action == "OPEN_PASSAGE")
        {
            var d = maze.FindDoor(dec.target);
            if (d == null || !d.closed) { ok = false; note = "rejected: not a closed passage"; }
            else if (elapsed - lastStructural < STRUCTURAL_COOLDOWN) { ok = false; note = "rejected: structural cooldown"; }
            else { d.closed = false; brokerClosed.Remove(d.id); lastStructural = elapsed; note = $"opened {d.id} ({d.zone})"; }
        }
        else if (dec.action == "ACTIVATE_ALARM") note = "alarm on for 8 s";
        if (ok) ok = broker.ApplyStrategy(dec);
        if (!ok) broker.ApplyStrategy(new Decision { action = "INVESTIGATE_ZONE", target = mem.PreferredZone ?? "centre", reason = "safe alternative after rejected action" });
        var rec = new DecisionRecord { t = elapsed, action = dec.action, target = dec.target, reason = dec.reason, prediction = dec.prediction, confidence = dec.confidence,
            dialogue = dec.dialogue, source = dec.source, valid = ok, note = note, latencyMs = dec.latencyMs };
        mem.decisions.Add(rec);
        mem.log.Add(new GameEvent { t = elapsed, name = "broker_decision", data = { ["rec"] = rec } });
        pending = (rec, elapsed, mem.sightings, mem.captures.Count, dec.target);
        if (ok && !string.IsNullOrEmpty(dec.dialogue)) Say(dec.dialogue, false);
    }

    void EvaluateOutcome()
    {
        if (!pending.HasValue || elapsed - pending.Value.at < 8) return;
        var p = pending.Value; var r = p.rec;
        if (!r.valid) r.outcome = "not executed";
        else if (mem.captures.Count > p.captures) r.outcome = "SUCCESS: player captured";
        else if (mem.sightings > p.sightings) r.outcome = "partial: player spotted";
        else if (r.action == "BLOCK_CORRIDOR") r.outcome = "player had to reroute";
        else r.outcome = "no contact, prediction may be wrong";
        pending = null;
    }

    // ---------- player actions ----------
    void Interact()
    {
        var pt = player.T;
        if (player.hidden)
        {
            player.hidden = false; player.Teleport(player.hideReturnPos); player.locker = null; Emit("player_left_hiding"); return;
        }
        var locker = maze.lockers.FirstOrDefault(l => Mathf.Abs(l.x - pt.x) + Mathf.Abs(l.y - pt.y) <= 1);
        if (locker != null)
        {
            player.seenEnteringLocker = broker.CanSee(new Tile(locker.x, locker.y), false, false, progress["power"]);
            player.hideReturnPos = player.transform.position; player.hidden = true; player.locker = locker;
            player.Teleport(World(new Tile(locker.x, locker.y)) + Vector3.up * 1f);
            mem.hides++; mem.lockerUses.TryGetValue(locker.id, out var u); mem.lockerUses[locker.id] = u + 1;
            Emit("player_hiding", ("hiding_place", locker.id), ("zone", locker.zone), ("seen_entering", player.seenEnteringLocker));
            return;
        }
        if (Mathf.Abs(maze.generatorPos.x - pt.x) + Mathf.Abs(maze.generatorPos.y - pt.y) <= 1)
        {
            if (progress["power"]) return;
            if (!progress["fuse"]) { Toast("The generator needs a fuse."); return; }
            progress["power"] = true;
            generatorR.sharedMaterial = Mat(new Color(0.2f, 0.9f, 0.3f), new Color(0.1f, 0.6f, 0.1f));
            foreach (var li in roomLights) { li.color = new Color(1f, 0.92f, 0.8f); li.intensity = 1.4f; }
            RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.06f);
            Emit("power_restored", ("zone", maze.ZoneOf(pt.x, pt.y)));
            broker.Hear(new NoiseEvent { x = pt.x, y = pt.y, radius = 60, kind = "generator" });
            Say("Power restored. And now I know exactly where you are.", false);
            return;
        }
        if (Mathf.Abs(maze.exitPos.x - pt.x) + Mathf.Abs(maze.exitPos.y - pt.y) <= 1)
        {
            if (progress["keycard"] && progress["power"]) EndGame(true); else Toast("Exit locked: needs power + keycard.");
        }
    }

    void ThrowBottle()
    {
        if (player.hidden) return;
        if (bottles <= 0) { Toast("No bottles left."); return; }
        bottles--;
        var pt = player.T; var f = player.FacingDir(); int x = pt.x, y = pt.y;
        for (int i = 0; i < 7; i++) { int nx = x + f.x, ny = y + f.y; if (!maze.Walkable(nx, ny)) break; x = nx; y = ny; }
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(g.GetComponent<Collider>()); g.transform.SetParent(transform);
        g.transform.position = World(new Tile(x, y)) + Vector3.up * 0.1f; g.transform.localScale = Vector3.one * 0.3f;
        g.GetComponent<Renderer>().sharedMaterial = Mat(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.7f, 0.2f)); Destroy(g, 1.5f);
        mem.throws++;
        Emit("object_thrown", ("landing_zone", maze.ZoneOf(x, y)), ("thrown_from_zone", maze.ZoneOf(pt.x, pt.y)));
        broker.Hear(new NoiseEvent { x = x, y = y, radius = 12, kind = "throw", fromX = pt.x, fromY = pt.y });
    }

    void MicNoise()
    {
        var pt = player.T;
        Emit("noise", ("source", "microphone"), ("zone", maze.ZoneOf(pt.x, pt.y)));
        broker.Hear(new NoiseEvent { x = pt.x, y = pt.y, radius = 14, kind = "mic" });
    }

    public void Capture(string how)
    {
        if (over) return;
        var pt = player.T; lives--;
        mem.captures.Add((pt.x, pt.y, maze.ZoneOf(pt.x, pt.y), how, elapsed));
        Emit("player_captured", ("zone", maze.ZoneOf(pt.x, pt.y)), ("how", how), ("lives_left", lives));
        if (lives <= 0) { EndGame(false); return; }
        Say(Random.value < 0.5f ? "Caught you. I will remember how." : "Again. You are learning slower than me.", false);
        // respawn in a room far from the BROKER that can still reach every objective
        var b = broker.T; var targets = ObjectiveTargets();
        var room = maze.rooms.Values.Where(rm => { var reach = maze.Reachable(rm.cx, rm.cy); return targets.All(t => reach.Contains(maze.Key(t.x, t.y))); })
            .OrderByDescending(rm => Mathf.Abs(rm.cx - b.x) + Mathf.Abs(rm.cy - b.y)).FirstOrDefault() ?? maze.rooms["start"];
        player.hidden = false; player.locker = null;
        player.Teleport(World(new Tile(room.cx, room.cy)) + Vector3.up * 1f);
        broker.ResetAfterCapture();
    }

    void EndGame(bool w)
    {
        over = true; won = w; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        Emit(w ? "player_escaped" : "game_over");
        Say(w ? "You escaped. This time." : "Game over. You were very predictable.", false);
    }

    void OnTile(Tile t)
    {
        if (maze.doorAt.TryGetValue(maze.Key(t.x, t.y), out var door))
        {
            mem.doorPasses.TryGetValue(door.id, out var n); mem.doorPasses[door.id] = n + 1;
            Emit("door_used", ("door", door.id), ("zone", door.zone), ("times", n + 1));
        }
        var it = maze.items.FirstOrDefault(i => i.x == t.x && i.y == t.y);
        if (it != null)
        {
            if (it.type == "keycard" && !progress["power"]) { Toast("Security terminal has no power. Restore the generator first."); return; }
            maze.items.Remove(it); Destroy(itemGo[it]); itemGo.Remove(it);
            if (it.type == "bottle") bottles++;
            else { progress[it.type] = true; Emit("item_collected", ("item", it.type), ("zone", maze.ZoneOf(t.x, t.y))); Toast("Collected: " + it.type); }
        }
    }

    // ---------- loop ----------
    void Update()
    {
        if (!started || over)
        {
            if (Input.GetKeyDown(KeyCode.Tab)) showPanel = !showPanel;
            return;
        }
        var sw = Stopwatch.StartNew();
        float dt = Mathf.Min(0.05f, Time.deltaTime); elapsed += dt;
        if (Input.GetKeyDown(KeyCode.Escape)) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        if (Input.GetKeyDown(KeyCode.E)) Interact();
        if (Input.GetKeyDown(KeyCode.Space)) ThrowBottle();
        if (Input.GetKeyDown(KeyCode.M)) MicNoise();
        if (Input.GetKeyDown(KeyCode.Tab)) showPanel = !showPanel;
        if (Input.GetKeyDown(KeyCode.V)) revealBroker = !revealBroker;

        player.Tick(dt);
        var pt = player.T;
        if (pt.x != lastTile.x || pt.y != lastTile.y) { OnTile(pt); lastTile = pt; }
        mem.Tick(dt, maze.ZoneOf(pt.x, pt.y), player.moving, player.running);
        runNoise -= dt;
        if (player.running && runNoise <= 0) { runNoise = 0.7f; broker.Hear(new NoiseEvent { x = pt.x, y = pt.y, radius = 6, kind = "run" }); if (Random.value < 0.15f) Emit("player_running", ("zone", maze.ZoneOf(pt.x, pt.y))); }

        broker.Tick(dt, player, progress["power"]);
        var bp = broker.transform.position; var pp = player.transform.position; bp.y = pp.y = 0;
        if (!player.hidden && Vector3.Distance(bp, pp) < 1.1f) Capture("caught in " + maze.ZoneOf(pt.x, pt.y));

        if (elapsed - lastStrategy > STRATEGY_INTERVAL) RequestStrategy("interval");
        EvaluateOutcome();
        if (GameSeconds - elapsed <= 0) EndGame(false);

        // visuals: doors slide, items spin, alarm pulses
        foreach (var d in maze.doors)
        {
            var g = doorGo[d.id]; float ty = d.closed ? WALL_H / 2 : -WALL_H / 2 + 0.05f;
            var p = g.transform.position; p.y = Mathf.MoveTowards(p.y, ty, 4f * dt); g.transform.position = p;
            var col = g.GetComponent<Collider>(); col.enabled = d.closed || p.y > 0.5f;
        }
        foreach (var g in itemGo.Values) g.transform.Rotate(0, 90 * dt, 0);
        foreach (var s in signs) s.rotation = Quaternion.LookRotation(s.position - player.cam.transform.position);
        RenderSettings.fogColor = broker.AlarmOn ? new Color(0.25f * (0.5f + 0.5f * Mathf.Sin(Time.time * 8)), 0, 0) : Color.black;
        player.flashlight.intensity = (progress["power"] ? 2.2f : 1.8f) * (Random.value < 0.01f ? 0.2f : 1f);

        sw.Stop(); fastSamples.Add((float)sw.Elapsed.TotalMilliseconds); if (fastSamples.Count > 60) fastSamples.RemoveAt(0);
    }
    float runNoise;

    // ---------- GUI ----------
    GUIStyle S(int size, Color c, bool bold = false, TextAnchor a = TextAnchor.UpperLeft)
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = size, richText = true, wordWrap = true, alignment = a };
        s.normal.textColor = c; if (bold) s.fontStyle = FontStyle.Bold; return s;
    }
    static readonly Color RED = new Color(0.9f, 0.3f, 0.3f), BLUE = new Color(0.5f, 0.75f, 0.9f), DIM = new Color(0.6f, 0.62f, 0.65f);

    void OnGUI()
    {
        if (maze == null) return;
        if (!started) { StartScreen(); return; }
        if (over) { PostMortem(); return; }
        float sw = Screen.width, sh = Screen.height;
        if (player.hidden)
        {
            GUI.color = new Color(0, 0, 0, 0.92f); GUI.DrawTexture(new Rect(0, 0, sw, sh * 0.42f), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(0, sh * 0.58f, sw, sh * 0.42f), Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.Label(new Rect(0, sh * 0.6f, sw, 30), "Hiding in " + player.locker.id + " (E to leave)", S(16, DIM, false, TextAnchor.MiddleCenter));
        }
        GUI.Label(new Rect(sw / 2 - 5, sh / 2 - 10, 20, 20), "+", S(16, new Color(1, 1, 1, 0.6f)));
        float left = Mathf.Max(0, GameSeconds - elapsed);
        string hud = $"<b><color=#e04848>THE VAULT: OUTBREAK</color></b>   {new string('♥', lives)}{new string('♡', 3 - lives)}   {(int)(left / 60)}:{(int)(left % 60):00}   " +
                     $"fuse {(progress["fuse"] ? "✓" : "✗")} · power {(progress["power"] ? "✓" : "✗")} · keycard {(progress["keycard"] ? "✓" : "✗")} · bottles {bottles}";
        GUI.Label(new Rect(12, 8, 900, 26), hud, S(16, Color.white));
        GUI.Label(new Rect(12, 32, 900, 22), "WASD move · SHIFT run (loud) · E interact/hide · SPACE throw · M mic shout · TAB panel · V reveal BROKER on map · ESC mouse", S(12, DIM));
        GUI.Label(new Rect(sw * 0.15f, sh - 90, sw * 0.7f, 40), "<b>THE BROKER:</b> " + subtitle, S(20, RED, false, TextAnchor.MiddleCenter));
        if (Time.time < toastUntil) GUI.Label(new Rect(sw * 0.2f, sh - 130, sw * 0.6f, 30), toast, S(16, Color.yellow, false, TextAnchor.MiddleCenter));
        DrawMinimap();
        if (showPanel) DrawPanel();
    }

    void StartScreen()
    {
        float w = 760, h = 460, x = (Screen.width - w) / 2, y = (Screen.height - h) / 2;
        GUI.color = new Color(0, 0, 0, 0.9f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(x, y, w, h));
        GUILayout.Label("<b>THE VAULT: OUTBREAK</b>", S(34, RED));
        GUILayout.Label("<i>In most games, you learn the maze. In this one, the maze learns you.</i>", S(16, new Color(1f, 0.6f, 0.6f)));
        GUILayout.Space(10);
        GUILayout.Label("The facility is controlled by THE BROKER. It watches how you move, remembers it, and rewires the maze against you.", S(15, Color.white));
        GUILayout.Label("<b>Goal:</b> fuse → generator (restore power) → keycard (security) → exit.", S(15, Color.white));
        GUILayout.Label("You can trick it. Throw bottles. Fake a favourite route. But it learns.", S(15, Color.white));
        GUILayout.Space(8);
        GUILayout.Label($"Strategist link: {(client != null && client.online ? "<color=#6bd68a>online (Claude)</color>" : "<color=#ff6b6b>offline: fallback rules</color>")}", S(14, DIM));
        GUILayout.BeginHorizontal();
        GUILayout.Label("Maze seed (same seed = same maze, for the two-players demo):", S(14, DIM), GUILayout.Width(430));
        seedText = GUILayout.TextField(seedText, GUILayout.Width(90));
        if (GUILayout.Button("Load", GUILayout.Width(60)) && uint.TryParse(seedText, out var ns) && ns != seed)
        {
            var go = new GameObject("VAULT"); go.AddComponent<VaultGame>().seed = ns; Destroy(gameObject);
        }
        demoMode = GUILayout.Toggle(demoMode, demoMode ? " Demo (5 min)" : " Full (30 min)");
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        if (GUILayout.Button("ENTER THE VAULT", GUILayout.Height(44)))
        {
            started = true; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            Say("Welcome to the Vault. I will be studying you.", false);
        }
        GUILayout.EndArea();
    }

    void DrawMinimap()
    {
        minimapTimer -= Time.deltaTime;
        if (minimapTimer <= 0)
        {
            minimapTimer = 0.15f;
            bool seenRecently = mem.lastSeen.HasValue && Time.time - mem.lastSeenTime < 2f;
            for (int y = 0; y < maze.h; y++) for (int x = 0; x < maze.w; x++)
            {
                Color c = maze.grid[y, x] == 1 ? new Color(0.25f, 0.27f, 0.3f) : new Color(0.06f, 0.07f, 0.08f);
                if (maze.doorAt.TryGetValue(maze.Key(x, y), out var d)) c = d.closed ? (d.kind == "door" ? new Color(0.85f, 0.15f, 0.15f) : new Color(0.5f, 0.35f, 0.15f)) : new Color(0.2f, 0.4f, 0.55f);
                minimap.SetPixel(x, maze.h - 1 - y, c);
            }
            foreach (var l in maze.lockers) minimap.SetPixel(l.x, maze.h - 1 - l.y, new Color(0.3f, 0.6f, 0.35f));
            var pt = player.T; minimap.SetPixel(pt.x, maze.h - 1 - pt.y, new Color(0.4f, 0.8f, 1f));
            if (revealBroker || seenRecently) { var bt = broker.T; minimap.SetPixel(bt.x, maze.h - 1 - bt.y, Color.red); }
            minimap.Apply();
        }
        float s = 8; GUI.DrawTexture(new Rect(12, Screen.height - maze.h * s - 12, maze.w * s, maze.h * s), minimap);
    }

    void DrawPanel()
    {
        float w = 420, x = Screen.width - w - 10;
        GUI.color = new Color(0.03f, 0.04f, 0.05f, 0.85f); GUI.DrawTexture(new Rect(x, 10, w, Screen.height - 20), Texture2D.whiteTexture); GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(x + 10, 16, w - 20, Screen.height - 32));
        var h = S(13, BLUE, true); var b = S(13, Color.white);
        GUILayout.Label("<b>THE BROKER: LIVE AGENT STATUS</b>", S(15, RED));
        GUILayout.Label("Link: " + (client.online ? $"<color=#6bd68a>online (Claude strategist, {client.model})</color>" : "<color=#ff6b6b>offline (fallback rules)</color>"), b);
        GUILayout.Label($"State: <b>{broker.state}</b> · {broker.stateReason}", b);
        GUILayout.Label($"Strategy: <b>{broker.strategy.action}</b>{(broker.strategy.target != null ? " → " + broker.strategy.target : "")}", b);
        GUILayout.Label("RECENT OBSERVATIONS", h);
        foreach (var e in mem.events.Skip(Mathf.Max(0, mem.events.Count - 6)).Reverse()) GUILayout.Label($"<color=#888>{Mathf.RoundToInt(e.t)}s</color> {Describe(e)}", S(12, Color.white));
        GUILayout.Label("MEMORY (BEHAVIOUR PROFILE)", h);
        Bar("Running tendency", mem.RunRatio); Bar("Trust in noises", mem.noiseTrust); Bar("Prefers zone: " + (mem.PreferredZone ?? "?"), mem.PreferredZoneShare);
        GUILayout.Label($"<color=#999>playstyle <b>{mem.Playstyle}</b> · hides {mem.hides} (fav {mem.FavouriteLocker ?? "-"}) · throws {mem.throws}, {mem.distractionsCaught} seen through · fav door {mem.PreferredDoor ?? "-"} ({mem.PreferredDoorUses}) · captures {mem.captures.Count}</color>", S(12, Color.white));
        var last = mem.decisions.LastOrDefault();
        GUILayout.Label("PREDICTION", h);
        GUILayout.Label(last == null ? "-" : $"{last.prediction} <color=#888>(confidence {Mathf.RoundToInt(last.confidence * 100)}%)</color>", b);
        GUILayout.Label("DECISION", h);
        GUILayout.Label(last == null ? "-" : $"<b><color={(last.valid ? "#6bd68a" : "#ff6b6b")}>{last.action}{(last.target != null ? " → " + last.target : "")}</color></b> <color=#888>[{last.source}{(last.latencyMs > 0 ? ", " + last.latencyMs + " ms" : "")}]</color>\n{last.reason}{(string.IsNullOrEmpty(last.note) ? "" : "\n<color=#888>engine: " + last.note + "</color>")}", b);
        GUILayout.Label("OUTCOME OF LAST DECISION", h);
        GUILayout.Label(last == null ? "-" : last.outcome, b);
        GUILayout.Label("LATENCY", h);
        float fp = fastSamples.Count > 0 ? fastSamples.Average() : 0;
        GUILayout.Label($"fast path (game loop): <b>{fp:0.00} ms</b>/frame\nstrategist round trip: <b>{(client.Last >= 0 ? client.Last + " ms" : "-")}</b> (p95 {(client.P95 >= 0 ? client.P95 + " ms" : "-")})", b);
        GUILayout.EndArea();
    }

    void Bar(string label, float v)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, S(12, Color.white), GUILayout.Width(170));
        var r = GUILayoutUtility.GetRect(150, 12); r.y += 4; r.height = 8;
        GUI.color = new Color(0.12f, 0.15f, 0.18f); GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = RED; GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(v), r.height), Texture2D.whiteTexture); GUI.color = Color.white;
        GUILayout.Label(Mathf.RoundToInt(v * 100) + "%", S(12, Color.white), GUILayout.Width(40));
        GUILayout.EndHorizontal();
    }

    static string Get(GameEvent e, string k) => e.data.TryGetValue(k, out var v) ? System.Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : "?";

    string Describe(GameEvent e)
    {
        switch (e.name)
        {
            case "door_used": return $"used {Get(e, "door")} ({Get(e, "zone")}) x{Get(e, "times")}";
            case "player_hiding": return $"hid in {Get(e, "hiding_place")}{(Get(e, "seen_entering") == "True" ? " (SEEN)" : "")}";
            case "object_thrown": return $"threw a bottle into {Get(e, "landing_zone")}";
            case "item_collected": return $"collected {Get(e, "item")}";
            case "player_spotted": return $"spotted player in {Get(e, "zone")}";
            case "player_captured": return $"<color=#ff6b6b>CAPTURED player ({Get(e, "how")})</color>";
            case "distraction_detected": return $"saw through a distraction (trust {Get(e, "noise_trust")})";
            case "power_restored": return "power restored (loud)";
            case "player_running": return $"running in {Get(e, "zone")}";
            case "noise": return $"heard a real-world noise ({Get(e, "source")})";
            case "broker_decision": var r = (DecisionRecord)e.data["rec"]; return $"<b><color=#e04848>BROKER → {r.action}{(r.target != null ? " " + r.target : "")}</color></b> <color=#999>because {r.reason}; outcome: {r.outcome}</color>";
            default: return e.name;
        }
    }

    void PostMortem()
    {
        float w = Mathf.Min(900, Screen.width - 40), h = Screen.height - 60, x = (Screen.width - w) / 2;
        GUI.color = new Color(0, 0, 0, 0.92f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(x, 30, w, h));
        GUILayout.Label($"<b>{(won ? "ESCAPED" : "CAPTURED")}: INCIDENT POST-MORTEM</b>", S(28, RED));
        GUILayout.Label(won ? "You got out. THE BROKER has filed a report." : "THE BROKER has filed a report on how it beat you.", S(15, new Color(1f, 0.6f, 0.6f)));
        int good = mem.decisions.Count(d => d.outcome.StartsWith("SUCCESS") || d.outcome.StartsWith("partial"));
        int deception = mem.throws > 0 ? Mathf.RoundToInt(100f * (mem.throws - mem.distractionsCaught) / mem.throws) : 0;
        GUILayout.Label($"<b>Survival time</b> {Mathf.RoundToInt(elapsed)} s · <b>Captures</b> {mem.captures.Count} · <b>Your playstyle</b> {mem.Playstyle} · <b>Deception score</b> {deception}% · <b>BROKER decisions that worked</b> {good}/{mem.decisions.Count}", S(15, Color.white));
        GUILayout.Label($"<b>What it learned about you:</b> runs {Mathf.RoundToInt(mem.RunRatio * 100)}% of the time · favourite zone {mem.PreferredZone ?? "-"} · favourite door {mem.PreferredDoor ?? "-"} · favourite locker {mem.FavouriteLocker ?? "-"} · trust in your noises ended at {Mathf.RoundToInt(mem.noiseTrust * 100)}%", S(15, Color.white));
        GUILayout.Label("INCIDENT TIMELINE", S(14, BLUE, true));
        pmScroll = GUILayout.BeginScrollView(pmScroll, GUILayout.Height(h - 260));
        var keyEvents = new[] { "player_spotted", "player_captured", "item_collected", "power_restored", "player_hiding", "distraction_detected", "broker_decision", "player_escaped", "game_over" };
        foreach (var e in mem.log.Where(e => keyEvents.Contains(e.name))) GUILayout.Label($"<color=#888>{Mathf.RoundToInt(e.t)}s</color>  {Describe(e)}", S(13, Color.white));
        GUILayout.EndScrollView();
        if (GUILayout.Button("PLAY AGAIN (same maze)", GUILayout.Height(40)))
        {
            var go = new GameObject("VAULT"); go.AddComponent<VaultGame>().seed = seed;
            Destroy(gameObject);
        }
        GUILayout.EndArea();
    }
}
