// THE VAULT: OUTBREAK - game manager. Builds the whole 3D world from code at Play (no scene setup needed),
// runs the game loop (3 escape rooms, cameras, 3 days, central exit), validates THE BROKER's strategic
// decisions, and draws the live agent panel, minimap, puzzles and post-mortem.
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

public partial class VaultGame : MonoBehaviour
{
    public const float TILE = 2.5f, WALL_H = 3f;
    public uint seed = 1337;
    public bool demoMode = true;
    const float STRATEGY_INTERVAL = 4f, STRUCTURAL_COOLDOWN = 6.5f, GRACE = 15f; const int MAX_BROKER_CLOSED = 4;
    public float demoSeconds = 420, fullSeconds = 1800; // round length (demo = 7 min)
    const float CATCH_DIST = 1.2f, DOOR_SPEED = 6f;
    static readonly Color FOG_BASE = new Color(0.012f, 0.01f, 0.007f);

    public VaultMaze maze; public BrokerMemory mem; public BrokerAgent broker; public FirstPersonPlayer player; StrategistClient client;
    public int day = 1; int bottles = 2; float elapsed, lastStrategy = GRACE - STRATEGY_INTERVAL, lastStructural = -99, lastReflexSay = -99;
    public Dictionary<string, EscapeRoom> rooms = new Dictionary<string, EscapeRoom>();
    readonly List<SecurityCamera> cams = new List<SecurityCamera>();
    List<string> brokerClosed = new List<string>();
    (DecisionRecord rec, float at, int sightings, int captures, string door)? pending;
    bool started, over, won, showPanel = true, revealBroker, puzzleOpen, centralOpen;
    string subtitle = "", toast = "", seedText = "1337"; float toastUntil, dayBannerUntil;
    Tile lastTile;
    float GameSeconds => demoMode ? demoSeconds : fullSeconds;
    readonly List<float> fastSamples = new List<float>();
    int RoomsDone => rooms.Values.Count(r => r.done);
    EscapeRoom ActiveRoom => rooms.Values.FirstOrDefault(r => r.active);

    readonly Dictionary<string, GameObject> doorGo = new Dictionary<string, GameObject>();
    readonly Dictionary<Item, GameObject> itemGo = new Dictionary<Item, GameObject>();
    readonly List<Light> roomLights = new List<Light>();
    readonly List<Transform> signs = new List<Transform>();
    Texture2D minimap; float minimapTimer; Vector2 pmScroll, panelScroll; float runNoise;
    // atmosphere: sound, screen effects, how close THE BROKER is
    VaultAudio sfx; DangerFX fx;
    float danger, dangerTimer, dangerTarget, brokerStepAcc, nextGrowl; Vector3 brokerPrev; string brokerPrevState = "PATROL";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindObjectOfType<VaultGame>() != null) return;
        foreach (var c in FindObjectsOfType<Camera>()) c.gameObject.SetActive(false);
        foreach (var l in FindObjectsOfType<Light>()) if (l.type == LightType.Directional) l.gameObject.SetActive(false);
        new GameObject("VAULT").AddComponent<VaultGame>();
    }

    public Vector3 World(Tile t) => new Vector3(t.x * TILE, 0, -t.y * TILE);
    public Tile TileOf(Vector3 p) => new Tile(Mathf.RoundToInt(p.x / TILE), Mathf.RoundToInt(-p.z / TILE));

    // ---------- setup ----------
    void Start()
    {
        maze = new VaultMaze(seed); mem = new BrokerMemory(); seedText = seed.ToString();
        client = gameObject.AddComponent<StrategistClient>();
        var rng = new System.Random((int)seed);
        foreach (var n in VaultMaze.PUZZLE_ROOMS)
        {
            var er = new EscapeRoom(n, maze.rooms[n], rng); er.gates = maze.doors.Where(d => d.room == n).ToList(); rooms[n] = er;
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        // dark, close fog: you see your flashlight, pools of light under working panels, and glows fading into black
        RenderSettings.ambientLight = new Color(0.032f, 0.029f, 0.023f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.075f; RenderSettings.fogColor = FOG_BASE;
        QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 6); QualitySettings.shadowDistance = 30f;
        // reflect a dim warm environment, not the default blue sky (which tints every wet/metal surface)
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = VaultArt.DarkReflection; RenderSettings.reflectionIntensity = 1f;
        sfx = gameObject.AddComponent<VaultAudio>();
        BuildWorld(rng);
        var pgo = new GameObject("Player"); pgo.transform.SetParent(transform);
        player = pgo.AddComponent<FirstPersonPlayer>(); player.Build(this, World(maze.start)); player.Look(90);
        sfx.Init(player.cam.transform);
        fx = player.cam.gameObject.AddComponent<DangerFX>();
        sfx.OnBeat = () => fx.pulse = 1f;
        BuildDust(player.cam.transform);
        RollPanels(false);
        player.onStep = () =>
        {
            bool concrete = maze.RoomAt(player.T.x, player.T.y) != null;
            sfx.Play(concrete ? "step_concrete" : "step_carpet", null, player.crouched ? 0.15f : player.running ? 0.7f : 0.4f, 0.08f);
        };
        EscapeRoom.Sfx = n => sfx.Play(n, null, 0.6f);
        var bgo = BuildBrokerBody(); broker = bgo.AddComponent<BrokerAgent>(); broker.Init(this); brokerPrev = broker.transform.position;
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

    TextMesh Label(string text, Vector3 pos, Color c, float size = 0.05f, bool billboard = true)
    {
        var go = new GameObject("Label"); go.transform.SetParent(transform); go.transform.position = pos;
        var tm = go.AddComponent<TextMesh>(); tm.text = text; tm.fontSize = 64; tm.characterSize = size; tm.anchor = TextAnchor.MiddleCenter; tm.color = c;
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
        if (billboard) signs.Add(go.transform);
        return tm;
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
        var significant = new[] { "room_entered", "puzzle_mistake", "room_breached", "room_completed", "central_exit_unlocked", "player_captured", "player_spotted", "camera_spotted", "player_hiding", "distraction_detected" };
        if (significant.Contains(name)) RequestStrategy(name);
    }

    public void Say(string text, bool reflex)
    {
        if (reflex) { if (elapsed - lastReflexSay < 3) return; lastReflexSay = elapsed; }
        subtitle = text; client.Say(text, !reflex);
    }

    void Toast(string s) { toast = s; toastUntil = Time.time + 3.5f; }

    // ---------- objectives / fairness ----------
    List<Tile> ObjectiveTargets()
    {
        var t = rooms.Values.Where(r => !r.done).Select(r => new Tile(r.room.cx, r.room.cy)).ToList();
        t.Add(maze.exitPos); return t;
    }

    string NextRoom()
    {
        var pt = player.T;
        return rooms.Values.Where(r => !r.done).OrderBy(r => Mathf.Abs(r.room.cx - pt.x) + Mathf.Abs(r.room.cy - pt.y)).Select(r => r.name).FirstOrDefault();
    }

    public Tile? ObjectivePoint(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (name == "exit" || name == "central_exit") name = "central";
        if (name == "start") name = "containment";
        if (maze.rooms.TryGetValue(name, out var rm))
        {
            // a sealed room can't be entered: wait just outside one of its gates
            if (rooms.TryGetValue(name, out var er) && er.active && !er.breached && er.gates.Count > 0)
            {
                var g = er.gates[0];
                for (int d = 0; d < 4; d++) { int nx = g.x + VaultMaze.DIRS[d, 0], ny = g.y + VaultMaze.DIRS[d, 1]; if (maze.Walkable(nx, ny) && maze.RoomAt(nx, ny) == null) return new Tile(nx, ny); }
            }
            if (name == "central" && !centralOpen) { var b = maze.doors.FirstOrDefault(d => d.kind == "blast"); if (b != null) for (int d = 0; d < 4; d++) { int nx = b.x + VaultMaze.DIRS[d, 0], ny = b.y + VaultMaze.DIRS[d, 1]; if (maze.Walkable(nx, ny)) return new Tile(nx, ny); } }
            return new Tile(rm.cx, rm.cy);
        }
        if (VaultMaze.ZONE_LIST.Contains(name)) return maze.ZoneCentre(name);
        return null;
    }

    List<string> ClosableDoors()
    {
        var pt = player.T; var targets = ObjectiveTargets();
        return maze.doors.Where(d => d.kind == "door" && !d.closed && maze.CanClose(d.id, pt, targets)).Select(d => d.id).ToList();
    }

    // ---------- slow path ----------
    string BuildStateJson(string trigger)
    {
        var ls = mem.lastSeen; var bt = broker.T; var ar = ActiveRoom;
        var state = new Dictionary<string, object>
        {
            ["trigger"] = trigger, ["day"] = day, ["days_left"] = 3 - day + 1,
            ["time_left_s"] = Mathf.RoundToInt(GameSeconds - elapsed),
            ["rooms"] = rooms.Values.ToDictionary(r => r.name, r => (object)new Dictionary<string, object> { ["done"] = r.done, ["sealed_with_player_inside"] = r.active && !r.breached, ["locks_left"] = r.locks, ["mistakes"] = r.mistakes }),
            ["rooms_done"] = RoomsDone, ["central_exit_open"] = centralOpen,
            ["player_in_room"] = ar?.name,
            ["profile"] = mem.Profile(),
            ["last_seen"] = ls.HasValue ? new Dictionary<string, object> { ["zone"] = maze.ZoneOf(ls.Value.x, ls.Value.y), ["seconds_ago"] = Mathf.RoundToInt(Time.time - mem.lastSeenTime), ["room"] = maze.RoomAt(ls.Value.x, ls.Value.y) } : null,
            ["hunter"] = new Dictionary<string, object> { ["state"] = broker.state, ["zone"] = maze.ZoneOf(bt.x, bt.y), ["threat"] = broker.Threat },
            ["cameras"] = cams.Select(c => (object)new Dictionary<string, object> { ["id"] = c.id, ["zone"] = maze.ZoneOf(c.tile.x, c.tile.y), ["times_spotted_player"] = c.spots }).ToList(),
            ["recent_events"] = mem.events.Skip(Mathf.Max(0, mem.events.Count - 10)).Select(e => (object)e.ToDict()).ToList(),
            ["closable_doors"] = ClosableDoors().Cast<object>().ToList(),
            ["openable_passages"] = maze.doors.Where(d => d.closed && d.kind == "shutter").Select(d => (object)d.id).ToList(),
            ["lockers"] = maze.lockers.Select(l => (object)new Dictionary<string, object> { ["id"] = l.id, ["zone"] = l.zone, ["uses"] = mem.lockerUses.TryGetValue(l.id, out var u) ? u : 0 }).ToList(),
            ["last_decision"] = mem.decisions.Count > 0 ? mem.decisions[mem.decisions.Count - 1].ToDict() : null,
        };
        return MiniJson.Write(new Dictionary<string, object> { ["state"] = state });
    }

    void RequestStrategy(string trigger)
    {
        if (over || !started || client.busy) return;
        if (elapsed < GRACE && day == 1 && trigger == "interval") return; // no hunting at the very start
        lastStrategy = elapsed;
        client.Decide(BuildStateJson(trigger), 8f, dec =>
        {
            if (dec == null) dec = BrokerAgent.Fallback(mem, RoomsDone, ActiveRoom?.name, NextRoom(), ClosableDoors());
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
            if (d == null || !d.closed || (d.kind != "shutter" && d.kind != "door")) { ok = false; note = "rejected: not a passage it controls"; }
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

    // ---------- escape rooms ----------
    void SetGates(EscapeRoom r, bool closed) { foreach (var g in r.gates) g.closed = closed; }

    void EnterRoom(EscapeRoom r)
    {
        r.Begin(); SetGates(r, true);
        var pt = player.T; mem.lastSeen = pt; mem.lastSeenTime = Time.time;
        Emit("room_entered", ("room", r.name), ("locks_left", r.locks));
        sfx.Play("lock", null, 0.8f, 0f, 30f, 0.8f);
        Toast($"{r.title}: LOCKDOWN. Solve the terminal puzzle. Every mistake breaks a lock.");
        if (r.name == "surveillance") Say($"Welcome to my eyes. The code is {r.fakeCode}. Trust me.", false);
        else Say(r.name == "power" ? "The power chamber. Careful which breaker you touch." : "The emergency release. Your hands are shaking already.", false);
    }

    void RoomMistake(EscapeRoom r, string why)
    {
        r.locks--; r.mistakes++;
        sfx.Play("buzzer", null, 0.7f, 0f); sfx.Play("clang", null, 0.6f, 0.05f, 30f, 1.4f); fx.flash = 0.5f;
        Emit("puzzle_mistake", ("room", r.name), ("why", why), ("locks_left", r.locks));
        Say(r.locks == 2 ? "One lock gone. I can hear it break." : r.locks == 1 ? "Two locks remaining? No. Only one." : "The door is open. I'm coming in.", false);
        if (r.locks <= 0)
        {
            r.breached = true; SetGates(r, false); puzzleOpen = false; player.frozen = false; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            Emit("room_breached", ("room", r.name));
            broker.Hunt(new Tile(r.room.cx, r.room.cy), $"{r.name} room breached: all 3 locks broken");
            broker.ApplyStrategy(new Decision { action = "ACTIVATE_ALARM" });
        }
    }

    void RoomSolved(EscapeRoom r)
    {
        r.done = true; r.active = false; SetGates(r, false); puzzleOpen = false; player.frozen = false; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        Emit("room_completed", ("room", r.name), ("mistakes", r.mistakes));
        sfx.Play("chime", null, 0.8f, 0f); sfx.Play("lock", null, 0.8f, 0f);
        if (r.name == "power") { foreach (var li in roomLights) { li.color = new Color(1f, 0.92f, 0.8f); li.intensity = 1.4f; } RenderSettings.ambientLight = new Color(0.045f, 0.042f, 0.034f); }
        Toast($"{r.title} COMPLETE ({RoomsDone}/3)");
        if (RoomsDone >= 3)
        {
            centralOpen = true; foreach (var d in maze.doors.Where(d => d.kind == "blast")) d.closed = false;
            Emit("central_exit_unlocked");
            sfx.Play("rumble", null, 1f, 0f);
            Say("All three. The central exit is open. Now you have to reach it.", false);
            Toast("ALL ROOMS DONE: the central exit blast doors are OPEN. Reach the centre of the maze!");
        }
    }

    void TickRooms(float dt)
    {
        var r = ActiveRoom; if (r == null) return;
        if (r.breached) return;
        mem.lastSeen = player.T; mem.lastSeenTime = Time.time; // room cameras watch you inside
        r.timer -= dt;
        if (r.timer <= 0) { r.timer = 45f; RoomMistake(r, "timer expired (emergency state, +45 s)"); }
    }

    // ---------- player actions ----------
    void Interact()
    {
        var pt = player.T;
        if (puzzleOpen) { ClosePuzzle(); return; }
        if (player.hidden)
        {
            var li0 = lockerInfo[player.locker.id]; sfx.Play("creak", li0.inside, 0.7f); StartCoroutine(SwingLocker(li0.hinge, false));
            player.hidden = false; player.Teleport(player.hideReturnPos); player.locker = null; Emit("player_left_hiding"); return;
        }
        var ar = ActiveRoom;
        if (ar != null && !ar.breached && Mathf.Abs(ar.room.cx - pt.x) + Mathf.Abs(ar.room.cy - pt.y) <= 1)
        {
            puzzleOpen = true; player.frozen = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; return;
        }
        var locker = maze.lockers.FirstOrDefault(l => Mathf.Abs(l.x - pt.x) + Mathf.Abs(l.y - pt.y) <= 1);
        if (locker != null)
        {
            player.seenEnteringLocker = broker.CanSee(new Tile(locker.x, locker.y), false, false, rooms["power"].done);
            player.hideReturnPos = player.transform.position; player.hidden = true; player.locker = locker;
            var li = lockerInfo[locker.id];
            player.Teleport(li.inside); player.Look(Quaternion.LookRotation(li.front).eulerAngles.y);
            sfx.Play("creak", li.inside, 0.7f); StartCoroutine(SwingLocker(li.hinge, true));
            mem.hides++; mem.lockerUses.TryGetValue(locker.id, out var u); mem.lockerUses[locker.id] = u + 1;
            Emit("player_hiding", ("hiding_place", locker.id), ("zone", locker.zone), ("seen_entering", player.seenEnteringLocker));
        }
    }

    void ClosePuzzle() { puzzleOpen = false; player.frozen = false; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

    void ThrowBottle()
    {
        if (player.hidden || puzzleOpen) return;
        if (bottles <= 0) { Toast("No bottles left."); return; }
        bottles--;
        var pt = player.T; var f = player.FacingDir(); int x = pt.x, y = pt.y;
        for (int i = 0; i < 7; i++) { int nx = x + f.x, ny = y + f.y; if (!maze.Walkable(nx, ny)) break; x = nx; y = ny; }
        mem.throws++;
        Emit("object_thrown", ("landing_zone", maze.ZoneOf(x, y)), ("thrown_from_zone", maze.ZoneOf(pt.x, pt.y)));
        // the bottle flies in an arc; THE BROKER hears it where (and when) it smashes
        var ct = player.cam.transform;
        StartCoroutine(ThrowFx(ct.position + ct.forward * 0.4f + ct.right * 0.2f - ct.up * 0.15f, World(new Tile(x, y)) + new Vector3(Random.Range(-0.4f, 0.4f), 0.02f, Random.Range(-0.4f, 0.4f)),
            new NoiseEvent { x = x, y = y, radius = 12, kind = "throw", fromX = pt.x, fromY = pt.y }));
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
        var pt = player.T;
        mem.captures.Add((pt.x, pt.y, maze.ZoneOf(pt.x, pt.y), how, elapsed));
        sfx.Play("stinger", null, 1f, 0f); fx.flash = 1f; danger = 0;
        Emit("player_captured", ("zone", maze.ZoneOf(pt.x, pt.y)), ("how", how), ("day_lost", day));
        if (day >= 3) { EndGame(false); return; }
        day++;
        foreach (var r in rooms.Values) if (r.active || r.breached) { r.ResetAfterCapture(); SetGates(r, false); }
        if (puzzleOpen) ClosePuzzle();
        Say(day == 2 ? "Day two. I remember exactly how I caught you." : "Day three. Your last day. I have learned everything I need.", false);
        dayBannerUntil = Time.time + 3f;
        if (day >= 3) foreach (var c in cams) { c.rangeTiles += 1; c.speed *= 1.3f; }
        player.hidden = false; player.locker = null;
        player.Teleport(World(maze.start) + Vector3.up * 1f); player.Look(90);
        broker.ResetAfterCapture();
    }

    void EndGame(bool w)
    {
        over = true; won = w; puzzleOpen = false; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        Emit(w ? "player_escaped" : "game_over");
        Say(w ? "You escaped. This time." : "Game over. You were very predictable.", false);
    }

    void OnTile(Tile t)
    {
        if (maze.doorAt.TryGetValue(maze.Key(t.x, t.y), out var door) && (door.kind == "door" || door.kind == "shutter"))
        {
            mem.doorPasses.TryGetValue(door.id, out var n); mem.doorPasses[door.id] = n + 1;
            Emit("door_used", ("door", door.id), ("zone", door.zone), ("times", n + 1));
        }
        var it = maze.items.FirstOrDefault(i => i.x == t.x && i.y == t.y);
        if (it != null) { maze.items.Remove(it); Destroy(itemGo[it]); itemGo.Remove(it); bottles++; Toast("Picked up a bottle (CLICK to throw)"); }
        var roomName = maze.RoomAt(t.x, t.y);
        if (roomName != null && rooms.TryGetValue(roomName, out var er) && !er.done && !er.active && ActiveRoom == null) EnterRoom(er);
        if (roomName == "central" && centralOpen) EndGame(true);
    }

    // ---------- loop ----------
    void Update()
    {
        if (!started || over) { if (Input.GetKeyDown(KeyCode.Tab)) showPanel = !showPanel; return; }
        var sw = Stopwatch.StartNew();
        float dt = Mathf.Min(0.05f, Time.deltaTime); elapsed += dt;
        if (Input.GetKeyDown(KeyCode.Escape)) { if (puzzleOpen) ClosePuzzle(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        bool wasLocked = Cursor.lockState == CursorLockMode.Locked;
        if (Input.GetMouseButtonDown(0) && !wasLocked && !puzzleOpen) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        if (Input.GetKeyDown(KeyCode.E)) Interact();
        // left click throws (but not the click that re-captures the mouse); SPACE is jump, handled by the player
        if (Input.GetMouseButtonDown(0) && wasLocked && !puzzleOpen && !player.hidden) ThrowBottle();
        if (Input.GetKeyDown(KeyCode.M)) MicNoise();
        if (Input.GetKeyDown(KeyCode.Tab)) showPanel = !showPanel;
        if (Input.GetKeyDown(KeyCode.V)) revealBroker = !revealBroker;

        player.Tick(dt);
        var pt = player.T;
        // landings are heard; faster landings carry further
        if (player.landedThisFrame) { broker.Hear(new NoiseEvent { x = pt.x, y = pt.y, radius = 3 + player.landSpeed * 0.5f, kind = "land" }); sfx.Play("land", null, 0.55f, 0.08f); }
        if (player.jumpedThisFrame) sfx.Play("jump", null, 0.3f, 0.1f);
        if (pt.x != lastTile.x || pt.y != lastTile.y) { OnTile(pt); lastTile = pt; }
        if (over) return;
        mem.Tick(dt, maze.ZoneOf(pt.x, pt.y), player.moving, player.running);
        runNoise -= dt;
        if (player.running && runNoise <= 0) { runNoise = 0.7f; broker.Hear(new NoiseEvent { x = pt.x, y = pt.y, radius = 6, kind = "run" }); if (Random.value < 0.15f) Emit("player_running", ("zone", maze.ZoneOf(pt.x, pt.y))); }

        // cameras: the BROKER's eyes in the maze
        foreach (var c in cams)
        {
            if (Time.time < c.cooldownUntil) continue;
            if (c.CanSee(maze, pt, player.transform.position, player.crouched, player.hidden))
            {
                c.alertUntil = Time.time + 2f; c.cooldownUntil = Time.time + 4f; c.spots++;
                mem.lastSeen = pt; mem.lastSeenTime = Time.time; mem.sightings++;
                Emit("camera_spotted", ("camera", c.id), ("zone", maze.ZoneOf(pt.x, pt.y)));
                broker.Hear(new NoiseEvent { x = pt.x, y = pt.y, radius = 999, kind = "camera" });
                Say("Camera " + c.id.Replace("cam_", "") + " sees you.", true);
                sfx.Play("beep", c.transform.position, 0.9f, 0f, 35f); fx.flash = Mathf.Max(fx.flash, 0.45f);
            }
        }

        broker.Tick(dt, player, rooms["power"].done);
        var bp = broker.transform.position; var pp = player.transform.position; bp.y = pp.y = 0;
        if (!player.hidden && Vector3.Distance(bp, pp) < CATCH_DIST) Capture("caught in " + maze.ZoneOf(pt.x, pt.y));

        TickRooms(dt);
        if (elapsed - lastStrategy > STRATEGY_INTERVAL) RequestStrategy("interval");
        EvaluateOutcome();
        if (GameSeconds - elapsed <= 0) EndGame(false);

        TickWorld(dt);
        foreach (var s in signs) s.rotation = Quaternion.LookRotation(s.position - player.cam.transform.position);
        var ar = ActiveRoom;
        bool alarm = broker.AlarmOn || (ar != null && ar.locks < 3 && !ar.breached);
        RenderSettings.fogColor = alarm ? new Color(0.25f * (0.5f + 0.5f * Mathf.Sin(Time.time * 8)), 0, 0) : FOG_BASE;
        TickAtmosphere(dt, alarm);

        sw.Stop(); fastSamples.Add((float)sw.Elapsed.TotalMilliseconds); if (fastSamples.Count > 60) fastSamples.RemoveAt(0);
    }

    // ---------- atmosphere: heartbeat, danger screen, BROKER footsteps ----------
    void TickAtmosphere(float dt, bool alarm)
    {
        // how close THE BROKER is, by walking distance (a wall between you and a long way round doesn't count)
        dangerTimer -= dt;
        if (dangerTimer <= 0)
        {
            dangerTimer = 0.25f;
            var bt = broker.T; var pt = player.T;
            var path = maze.Path(bt, pt) ?? maze.Path(bt, pt, true);
            float d = path != null ? path.Count - 1 : Mathf.Sqrt((bt.x - pt.x) * (bt.x - pt.x) + (bt.y - pt.y) * (bt.y - pt.y)) * 1.5f;
            dangerTarget = Mathf.Pow(Mathf.Clamp01((12f - d) / 10f), 1.3f);
            if (broker.state == "CHASE" && d < 16) dangerTarget = Mathf.Max(dangerTarget, 0.3f);
        }
        danger = Mathf.MoveTowards(danger, dangerTarget, dt * (dangerTarget > danger ? 1.2f : 0.5f));
        float fxDanger = Mathf.SmoothStep(0, 1, Mathf.Clamp01((danger - 0.4f) / 0.6f)); // the screen only reacts when it's really close
        fx.danger = fxDanger;
        player.fovOffset = -8f * fxDanger * (fx.reduceEffects ? 0.3f : 1f);
        player.flashFlicker = fxDanger;
        player.flashBase = rooms["power"].done ? 4.2f : 3.8f;

        sfx.danger = danger; sfx.hidden = player.hidden; sfx.alarm = alarm;
        sfx.threat = broker.Threat == "ACTIVE HUNT" ? 1f : broker.Threat == "INVESTIGATION" ? 0.5f : 0f;
        sfx.slideLevel = player.state == FirstPersonPlayer.MoveState.GroundSlide ? Mathf.Clamp01(player.HorizontalSpeed / 8f) : 0f;

        // heavy footsteps and a growl when it starts hunting
        var bp = broker.transform.position; float bd = Vector3.Distance(bp, brokerPrev); if (bd < 3f) brokerStepAcc += bd; brokerPrev = bp;
        if (brokerStepAcc > 1.4f) { brokerStepAcc = 0; sfx.Play("bstep", bp + Vector3.up * 0.2f, 0.9f, 0.08f, 28f); }
        if (broker.state != brokerPrevState)
        {
            if ((broker.state == "CHASE" || broker.state == "INVESTIGATE" || broker.state == "SEARCH") && Time.time > nextGrowl)
            { sfx.Play("growl", bp + Vector3.up * 1.6f, broker.state == "CHASE" ? 1f : 0.7f, 0.1f, 32f); nextGrowl = Time.time + 8f; }
            brokerPrevState = broker.state;
        }
    }

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
        // play area = screen minus the side panel, so nothing hides behind it; every label wraps + measures its height
        float pw = PlayW, ui = UiScale;
        GUI.Label(new Rect(pw / 2 - 5, sh / 2 - 10, 20, 20), "+", S(16, new Color(1, 1, 1, 0.6f)));
        float left = Mathf.Max(0, GameSeconds - elapsed);
        string threat = broker.Threat; string tc = threat == "ACTIVE HUNT" ? "#ff4040" : threat == "INVESTIGATION" ? "#ffb040" : "#6bd68a";
        string roomsTxt = string.Join(" · ", rooms.Values.Select(r => $"{r.name} {(r.done ? "✓" : r.active ? $"[{r.locks}/3]" : "✗")}"));
        float y = 6;
        y = WrapLabel(new Rect(12, y, pw - 24, 0), $"<b><color=#e04848>THE VAULT</color></b>  <b>DAY {day}/3</b>  <color={tc}><b>{threat}</b></color>  {(int)(left / 60)}:{(int)(left % 60):00}  rooms: {roomsTxt}  bottles {bottles}{(player.crouched ? "  <color=#7fb3d5>CROUCHING</color>" : "")}", S(Fs(16), Color.white));
        y = WrapLabel(new Rect(12, y, pw - 24, 0), "WASD move · SHIFT run (loud) · SPACE jump · C crouch (hold) / slide (tap while sprinting or mid-air) · E interact/hide/terminal · CLICK throw · TAB panel · V hunter on map · ESC mouse", S(Fs(12), DIM));
        var ar = ActiveRoom;
        y = DrawObjective(pw, y + 4, ar);
        if (ar != null && !ar.breached && !puzzleOpen)
            WrapLabel(new Rect(12, y, pw - 24, 0), $"<b>{ar.title} LOCKDOWN</b>  locks {new string('■', ar.locks)}{new string('□', 3 - ar.locks)}  {Mathf.CeilToInt(ar.timer)} s", S(Fs(17), Color.yellow, false, TextAnchor.MiddleCenter));
        var subS = S(Fs(20), RED, false, TextAnchor.MiddleCenter); string subT = "<b>THE BROKER:</b> " + subtitle;
        float subH = subS.CalcHeight(new GUIContent(subT), pw * 0.9f);
        GUI.Label(new Rect(pw * 0.05f, sh - subH - 16, pw * 0.9f, subH), subT, subS);
        if (Time.time < toastUntil) { var ts = S(Fs(16), Color.yellow, false, TextAnchor.MiddleCenter); float th = ts.CalcHeight(new GUIContent(toast), pw * 0.9f); GUI.Label(new Rect(pw * 0.05f, sh - subH - th - 24, pw * 0.9f, th), toast, ts); }
        if (Time.time < dayBannerUntil)
        {
            GUI.color = new Color(0, 0, 0, Mathf.Clamp01(dayBannerUntil - Time.time)); GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.Label(new Rect(0, sh / 2 - 60, sw, 120), $"<b>DAY {day} OF 3</b>\n<size=18>You wake up in containment. THE BROKER remembers you.</size>", S(48, RED, false, TextAnchor.MiddleCenter));
        }
        DrawMinimap();
        if (showPanel) DrawPanel();
        else
        {
            if (GUI.Button(new Rect(sw - 190, 8, 180, 28), "◂ BROKER panel (TAB)")) showPanel = true;
        }
        if (puzzleOpen && ar != null)
        {
            float w = Mathf.Min(560, pw - 40), h = Mathf.Min(440, sh - 40), x = (pw - w) / 2, py = (sh - h) / 2;
            GUI.color = new Color(0.02f, 0.03f, 0.04f, 0.95f); GUI.DrawTexture(new Rect(x - 10, py - 10, w + 20, h + 20), Texture2D.whiteTexture); GUI.color = Color.white;
            int res = ar.DrawPuzzle(new Rect(x, py, w, h), S(Fs(18), RED), S(Fs(14), Color.white));
            if (res == 1) RoomMistake(ar, ar.name == "power" ? "wrong breaker" : ar.name == "surveillance" ? "wrong code" : "missed the lever");
            else if (res == 2) RoomSolved(ar);
        }
    }

    // ---------- objective guidance: always tell the player what to do next + where ----------
    (string text, Tile? target) CurrentObjective(EscapeRoom ar)
    {
        if (ar != null && !ar.breached) return ($"SOLVE THE {ar.title} PUZZLE: walk to the green terminal, press E", new Tile(ar.room.cx, ar.room.cy));
        if (ar != null && ar.breached) return ("THE ROOM IS BREACHED. RUN! Lose the hunter, then come back", null);
        if (RoomsDone >= 3) return ("ALL 3 ROOMS DONE: REACH THE CENTRAL EXIT", maze.exitPos);
        var n = NextRoom(); var r = rooms[n];
        return ($"FIND THE {r.title}  ({RoomsDone}/3 rooms done)", new Tile(r.room.cx, r.room.cy));
    }

    // returns the y below what it drew
    float DrawObjective(float pw, float y, EscapeRoom ar)
    {
        var (text, target) = CurrentObjective(ar);
        string dist = ""; float ang = 0; bool arrow = false;
        if (target.HasValue)
        {
            Vector3 to = World(target.Value) - player.transform.position; to.y = 0;
            dist = $"  ·  {Mathf.RoundToInt(to.magnitude)} m";
            ang = Vector3.SignedAngle(new Vector3(player.transform.forward.x, 0, player.transform.forward.z), to, Vector3.up);
            arrow = !puzzleOpen && to.magnitude > 2.5f;
        }
        float w = Mathf.Min(pw - 24, 820), x = (pw - w) / 2;
        var st = S(Fs(16), new Color(1f, 0.85f, 0.2f), false, TextAnchor.MiddleCenter);
        string content = $"<b>OBJECTIVE:</b> {text}{dist}";
        float h = st.CalcHeight(new GUIContent(content), w - 16) + 8;
        GUI.color = new Color(0, 0, 0, 0.65f); GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture); GUI.color = Color.white;
        GUI.Label(new Rect(x + 8, y + 4, w - 16, h - 8), content, st);
        y += h + 4;
        if (arrow)
        {
            float a = 44 * UiScale; var pivot = new Vector2(pw / 2, y + a / 2 + 2);
            var m = GUI.matrix; GUIUtility.RotateAroundPivot(ang, pivot);
            GUI.Label(new Rect(pivot.x - a, pivot.y - a, a * 2, a * 2), "▲", S(Fs(44), new Color(1f, 0.85f, 0.2f), true, TextAnchor.MiddleCenter));
            GUI.matrix = m;
            y += a + 6;
        }
        objectiveTile = target;
        return y;
    }
    Tile? objectiveTile;

    // layout helpers: the HUD scales with the window and never sits behind the side panel
    float UiScale => Mathf.Clamp(Mathf.Min(Screen.width / 1600f, Screen.height / 900f), 0.65f, 1.3f);
    int Fs(int baseSize) => Mathf.Max(10, Mathf.RoundToInt(baseSize * UiScale));
    float PanelW => Mathf.Clamp(Screen.width * 0.3f, 260, 420);
    float PlayW => Screen.width - (showPanel ? PanelW + 20 : 0);
    float WrapLabel(Rect r, string text, GUIStyle st)
    {
        float h = st.CalcHeight(new GUIContent(text), r.width); GUI.Label(new Rect(r.x, r.y, r.width, h), text, st); return r.y + h;
    }

    void StartScreen()
    {
        float w = Mathf.Min(780, Screen.width - 40), h = Mathf.Min(520, Screen.height - 40), x = (Screen.width - w) / 2, y = (Screen.height - h) / 2;
        GUI.color = new Color(0, 0, 0, 0.9f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(x, y, w, h));
        GUILayout.Label("<b>THE VAULT: OUTBREAK</b>", S(34, RED));
        GUILayout.Label("<i>The maze isn't just trapping you. It's learning how you escape.</i>", S(16, new Color(1f, 0.6f, 0.6f)));
        GUILayout.Space(8);
        GUILayout.Label("<b>HOW TO ESCAPE</b>", S(18, new Color(1f, 0.85f, 0.2f)));
        GUILayout.Label("<b>1.</b>  Find the <b>3 puzzle rooms</b>. Follow the yellow arrow ▲", S(17, Color.white));
        GUILayout.Label("<b>2.</b>  Solve each room's puzzle. <b>3 mistakes</b> and the door opens for the monster", S(17, Color.white));
        GUILayout.Label("<b>3.</b>  Reach the <b>exit in the centre</b> of the maze", S(17, Color.white));
        GUILayout.Space(4);
        GUILayout.Label("<color=#ff6b6b><b>THE BROKER</b></color> (red eyes) hunts you and <b>learns your habits</b>. Getting caught costs 1 of your <b>3 days</b>.\nRun = fast but loud · SPACE = jump · tap C while sprinting or mid-air = slide, SPACE while sliding = hop (chain them to go faster - loudly) · C = crouch under cameras · E = hide in lockers · CLICK = throw a bottle to distract it", S(14, DIM));
        GUILayout.Space(6);
        GUILayout.Label($"Strategist link: {(client != null && client.online ? "<color=#6bd68a>online (Claude)</color>" : "<color=#ff6b6b>offline: fallback rules</color>")}", S(14, DIM));
        GUILayout.BeginHorizontal();
        GUILayout.Label("Maze seed (same seed = same maze):", S(14, DIM), GUILayout.Width(260));
        seedText = GUILayout.TextField(seedText, GUILayout.Width(90));
        if (GUILayout.Button("Load", GUILayout.Width(60)) && uint.TryParse(seedText, out var ns) && ns != seed) { var go = new GameObject("VAULT"); go.AddComponent<VaultGame>().seed = ns; Destroy(gameObject); }
        demoMode = GUILayout.Toggle(demoMode, demoMode ? " Demo (7 min)" : " Full (30 min)");
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
                if (maze.doorAt.TryGetValue(maze.Key(x, y), out var d))
                    c = d.kind == "gate" ? (d.closed ? new Color(0.45f, 0.45f, 1f) : new Color(0.2f, 0.2f, 0.45f))
                      : d.kind == "blast" ? (d.closed ? new Color(1f, 0.75f, 0.1f) : new Color(0.4f, 0.3f, 0.05f))
                      : d.closed ? (d.kind == "door" ? new Color(0.85f, 0.15f, 0.15f) : new Color(0.5f, 0.35f, 0.15f)) : new Color(0.2f, 0.4f, 0.55f);
                minimap.SetPixel(x, maze.h - 1 - y, c);
            }
            foreach (var l in maze.lockers) minimap.SetPixel(l.x, maze.h - 1 - l.y, new Color(0.3f, 0.6f, 0.35f));
            foreach (var c in cams) minimap.SetPixel(c.tile.x, maze.h - 1 - c.tile.y, Time.time < c.alertUntil ? Color.red : new Color(1f, 0.6f, 0.2f));
            if (objectiveTile.HasValue && Time.time % 0.8f < 0.45f)
            {
                var ot = objectiveTile.Value; var orn = maze.RoomAt(ot.x, ot.y);
                if (orn != null) { var rr = maze.rooms[orn]; for (int yy = rr.y; yy < rr.y + rr.h; yy++) for (int xx = rr.x; xx < rr.x + rr.w; xx++) minimap.SetPixel(xx, maze.h - 1 - yy, new Color(1f, 0.85f, 0.2f)); }
            }
            var pt = player.T; minimap.SetPixel(pt.x, maze.h - 1 - pt.y, new Color(0.4f, 0.8f, 1f));
            if (revealBroker || seenRecently || broker.Threat == "ACTIVE HUNT") { var bt = broker.T; minimap.SetPixel(bt.x, maze.h - 1 - bt.y, Color.red); }
            minimap.Apply();
        }
        float s = Mathf.Max(4f, 7f * UiScale); GUI.DrawTexture(new Rect(12, Screen.height - maze.h * s - 12, maze.w * s, maze.h * s), minimap);
    }

    void DrawPanel()
    {
        float w = PanelW, x = Screen.width - w - 10;
        GUI.color = new Color(0.03f, 0.04f, 0.05f, 0.85f); GUI.DrawTexture(new Rect(x, 10, w, Screen.height - 20), Texture2D.whiteTexture); GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(x + 10, 16, w - 20, Screen.height - 32));
        var h = S(Fs(13), BLUE, true); var b = S(Fs(13), Color.white);
        GUILayout.BeginHorizontal();
        GUILayout.Label("<b>THE BROKER: LIVE AGENT STATUS</b>", S(Fs(15), RED));
        if (GUILayout.Button("✕", GUILayout.Width(28), GUILayout.Height(24))) showPanel = false;
        GUILayout.EndHorizontal();
        panelScroll = GUILayout.BeginScrollView(panelScroll);
        GUILayout.Label("Link: " + (client.online ? $"<color=#6bd68a>online (Claude strategist, {client.model})</color>" : "<color=#ff6b6b>offline (fallback rules)</color>"), b);
        GUILayout.Label($"Threat: <b>{broker.Threat}</b> · hunter {broker.state} · {broker.stateReason}", b);
        GUILayout.Label($"Strategy: <b>{broker.strategy.action}</b>{(broker.strategy.target != null ? " → " + broker.strategy.target : "")}", b);
        var ls = mem.lastSeen;
        GUILayout.Label($"Knows where you are? {(ls.HasValue && Time.time - mem.lastSeenTime < 3 ? "<color=#ff6b6b>YES</color>" : ls.HasValue ? $"last seen {Mathf.RoundToInt(Time.time - mem.lastSeenTime)} s ago in {maze.ZoneOf(ls.Value.x, ls.Value.y)}" : "no")}", b);
        GUILayout.Label("RECENT OBSERVATIONS", h);
        foreach (var e in mem.events.Skip(Mathf.Max(0, mem.events.Count - 6)).Reverse()) GUILayout.Label($"<color=#888>{Mathf.RoundToInt(e.t)}s</color> {Describe(e)}", S(12, Color.white));
        GUILayout.Label("MEMORY (BEHAVIOUR PROFILE, KEPT ACROSS DAYS)", h);
        Bar("Running tendency", mem.RunRatio); Bar("Trust in noises", mem.noiseTrust); Bar("Prefers zone: " + (mem.PreferredZone ?? "?"), mem.PreferredZoneShare);
        GUILayout.Label($"<color=#999>playstyle <b>{mem.Playstyle}</b> · hides {mem.hides} (fav {mem.FavouriteLocker ?? "-"}) · throws {mem.throws}, {mem.distractionsCaught} seen through · fav door {mem.PreferredDoor ?? "-"} ({mem.PreferredDoorUses}) · camera spots {cams.Sum(c => c.spots)} · captures {mem.captures.Count}</color>", S(12, Color.white));
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
        GUILayout.EndScrollView();
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
            case "player_spotted": return $"hunter spotted player in {Get(e, "zone")}";
            case "camera_spotted": return $"<color=#ffb040>{Get(e, "camera")} spotted player in {Get(e, "zone")}</color>";
            case "player_captured": return $"<color=#ff6b6b>CAPTURED player ({Get(e, "how")}), day {Get(e, "day_lost")} lost</color>";
            case "distraction_detected": return $"saw through a distraction (trust {Get(e, "noise_trust")})";
            case "room_entered": return $"<color=#ffd24a>player sealed in {Get(e, "room")} room</color>";
            case "puzzle_mistake": return $"<color=#ffb040>{Get(e, "room")}: {Get(e, "why")}, locks left {Get(e, "locks_left")}</color>";
            case "room_breached": return $"<color=#ff6b6b>{Get(e, "room")} room BREACHED</color>";
            case "room_completed": return $"<color=#6bd68a>{Get(e, "room")} room completed</color>";
            case "central_exit_unlocked": return "<color=#6bd68a>central exit unlocked</color>";
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
        GUILayout.Label($"<b>{(won ? "ESCAPE SUCCESSFUL: YOU OUTSMARTED THE BROKER" : "CAPTURED")}: INCIDENT POST-MORTEM</b>", S(26, RED));
        GUILayout.Label(won ? "You got out. THE BROKER has filed a report." : "THE BROKER has filed a report on how it beat you.", S(15, new Color(1f, 0.6f, 0.6f)));
        int good = mem.decisions.Count(d => d.outcome.StartsWith("SUCCESS") || d.outcome.StartsWith("partial"));
        int deception = mem.throws > 0 ? Mathf.RoundToInt(100f * (mem.throws - mem.distractionsCaught) / mem.throws) : 0;
        GUILayout.Label($"<b>Escape day</b> {day} · <b>Captures</b> {mem.captures.Count} · <b>Rooms completed</b> {RoomsDone}/3 · <b>Puzzle mistakes</b> {rooms.Values.Sum(r => r.mistakes)} · <b>Camera spots</b> {cams.Sum(c => c.spots)} · <b>Deception score</b> {deception}% · <b>BROKER decisions that worked</b> {good}/{mem.decisions.Count}", S(15, Color.white));
        GUILayout.Label($"<b>What it learned about you:</b> playstyle {mem.Playstyle} · runs {Mathf.RoundToInt(mem.RunRatio * 100)}% of the time · favourite zone {mem.PreferredZone ?? "-"} · favourite door {mem.PreferredDoor ?? "-"} · favourite locker {mem.FavouriteLocker ?? "-"} · trust in your noises ended at {Mathf.RoundToInt(mem.noiseTrust * 100)}%", S(15, Color.white));
        GUILayout.Label("INCIDENT TIMELINE", S(14, BLUE, true));
        pmScroll = GUILayout.BeginScrollView(pmScroll, GUILayout.Height(h - 260));
        var keyEvents = new[] { "player_spotted", "camera_spotted", "player_captured", "room_entered", "puzzle_mistake", "room_breached", "room_completed", "central_exit_unlocked", "player_hiding", "distraction_detected", "broker_decision", "player_escaped", "game_over" };
        foreach (var e in mem.log.Where(e => keyEvents.Contains(e.name))) GUILayout.Label($"<color=#888>{Mathf.RoundToInt(e.t)}s</color>  {Describe(e)}", S(13, Color.white));
        GUILayout.EndScrollView();
        if (GUILayout.Button("PLAY AGAIN (same maze)", GUILayout.Height(40))) { var go = new GameObject("VAULT"); go.AddComponent<VaultGame>().seed = seed; Destroy(gameObject); }
        GUILayout.EndArea();
    }
}
